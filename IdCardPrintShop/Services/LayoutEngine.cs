using System;
using System.Collections.Generic;
using System.Linq;
using IdCardPrintShop.Models;

namespace IdCardPrintShop.Services
{
    public class LayoutEngine : ILayoutEngine
    {
        public LayoutPlan CalculateLayout(IReadOnlyList<CardRegion> cards, LayoutTemplate template, int copies)
        {
            if (copies < 1)
            {
                copies = 1;
            }

            var plan = new LayoutPlan
            {
                PaperSize = template.PaperSize,
                Orientation = template.Orientation,
                ShowCutMarks = template.ShowCutMarks,
                DrawBorderBox = template.DrawBorderBox
            };

            if (cards == null || cards.Count == 0)
            {
                plan.TotalPages = 1;
                return plan;
            }

            var (paperW, paperH) = template.PaperSize.GetDimensions(template.Orientation);
            double cardW = template.CardDimensions.WidthMm;
            double cardH = template.CardDimensions.HeightMm;

            // Identify Front and Back cards
            var frontCard = cards.FirstOrDefault(c => c.Role == CardRole.Front) ?? cards[0];
            var backCard = cards.FirstOrDefault(c => c.Role == CardRole.Back);
            if (backCard == null && cards.Count > 1)
            {
                backCard = cards.FirstOrDefault(c => c.Id != frontCard.Id);
            }

            bool hasPair = backCard != null;

            switch (template.Mode)
            {
                case LayoutMode.FrontBackVertical:
                    BuildVerticalLayout(plan, frontCard, backCard, template, copies, paperW, paperH, cardW, cardH);
                    break;

                case LayoutMode.FrontBackHorizontal:
                    BuildHorizontalLayout(plan, frontCard, backCard, template, copies, paperW, paperH, cardW, cardH);
                    break;

                case LayoutMode.PairsGrid:
                    BuildPairsGridLayout(plan, frontCard, backCard, template, copies, paperW, paperH, cardW, cardH);
                    break;

                case LayoutMode.GridCopies:
                default:
                    BuildGridCopiesLayout(plan, cards, template, copies, paperW, paperH, cardW, cardH);
                    break;
            }

            plan.TotalPages = plan.Items.Count > 0
                ? plan.Items.Max(i => i.PageIndex) + 1
                : 1;

            return plan;
        }

        private void BuildVerticalLayout(
            LayoutPlan plan,
            CardRegion frontCard,
            CardRegion? backCard,
            LayoutTemplate template,
            int copies,
            double paperW,
            double paperH,
            double cardW,
            double cardH)
        {
            double spacingY = template.SpacingY_Mm;
            double spacingX = template.SpacingX_Mm;

            bool isPair = backCard != null;
            double unitW = cardW;
            double unitH = isPair ? (cardH * 2 + spacingY) : cardH;

            double usableW = paperW - template.MarginLeftMm - template.MarginRightMm;
            double usableH = paperH - template.MarginTopMm - template.MarginBottomMm;

            int cols = Math.Max(1, (int)Math.Floor((usableW + spacingX) / (unitW + spacingX)));
            int rows = Math.Max(1, (int)Math.Floor((usableH + spacingY) / (unitH + spacingY)));

            int unitsPerPage = cols * rows;

            for (int copyIdx = 0; copyIdx < copies; copyIdx++)
            {
                int pageIndex = copyIdx / unitsPerPage;
                int posOnPage = copyIdx % unitsPerPage;
                int col = posOnPage % cols;
                int row = posOnPage / cols;

                // Center the active grid on the page for visual balance
                int activeColsOnPage = Math.Min(cols, copies - (pageIndex * unitsPerPage));
                int activeRowsOnPage = (int)Math.Ceiling((double)Math.Min(unitsPerPage, copies - (pageIndex * unitsPerPage)) / cols);

                double totalGridW = (activeColsOnPage * unitW) + ((activeColsOnPage - 1) * spacingX);
                double totalGridH = (activeRowsOnPage * unitH) + ((activeRowsOnPage - 1) * spacingY);

                double originX = Math.Max(template.MarginLeftMm, (paperW - totalGridW) / 2.0);
                double originY = Math.Max(template.MarginTopMm, (paperH - totalGridH) / 2.0);

                double unitX = originX + (col * (unitW + spacingX));
                double unitY = originY + (row * (unitH + spacingY));

                // Place Front
                plan.Items.Add(new LayoutItem
                {
                    PageIndex = pageIndex,
                    X_Mm = unitX,
                    Y_Mm = unitY,
                    Width_Mm = cardW,
                    Height_Mm = cardH,
                    Role = CardRole.Front,
                    CardRegionId = frontCard.Id,
                    Region = frontCard,
                    CopyIndex = copyIdx + 1
                });

                // Place Back if present
                if (isPair && backCard != null)
                {
                    plan.Items.Add(new LayoutItem
                    {
                        PageIndex = pageIndex,
                        X_Mm = unitX,
                        Y_Mm = unitY + cardH + spacingY,
                        Width_Mm = cardW,
                        Height_Mm = cardH,
                        Role = CardRole.Back,
                        CardRegionId = backCard.Id,
                        Region = backCard,
                        CopyIndex = copyIdx + 1
                    });
                }
            }
        }

        private void BuildHorizontalLayout(
            LayoutPlan plan,
            CardRegion frontCard,
            CardRegion? backCard,
            LayoutTemplate template,
            int copies,
            double paperW,
            double paperH,
            double cardW,
            double cardH)
        {
            double spacingX = template.SpacingX_Mm;
            double spacingY = template.SpacingY_Mm;

            bool isPair = backCard != null;
            double unitW = isPair ? (cardW * 2 + spacingX) : cardW;
            double unitH = cardH;

            double usableW = paperW - template.MarginLeftMm - template.MarginRightMm;
            double usableH = paperH - template.MarginTopMm - template.MarginBottomMm;

            int cols = Math.Max(1, (int)Math.Floor((usableW + spacingX) / (unitW + spacingX)));
            int rows = Math.Max(1, (int)Math.Floor((usableH + spacingY) / (unitH + spacingY)));

            int unitsPerPage = cols * rows;

            for (int copyIdx = 0; copyIdx < copies; copyIdx++)
            {
                int pageIndex = copyIdx / unitsPerPage;
                int posOnPage = copyIdx % unitsPerPage;
                int col = posOnPage % cols;
                int row = posOnPage / cols;

                int activeColsOnPage = Math.Min(cols, copies - (pageIndex * unitsPerPage));
                int activeRowsOnPage = (int)Math.Ceiling((double)Math.Min(unitsPerPage, copies - (pageIndex * unitsPerPage)) / cols);

                double totalGridW = (activeColsOnPage * unitW) + ((activeColsOnPage - 1) * spacingX);
                double totalGridH = (activeRowsOnPage * unitH) + ((activeRowsOnPage - 1) * spacingY);

                double originX = Math.Max(template.MarginLeftMm, (paperW - totalGridW) / 2.0);
                double originY = Math.Max(template.MarginTopMm, (paperH - totalGridH) / 2.0);

                double unitX = originX + (col * (unitW + spacingX));
                double unitY = originY + (row * (unitH + spacingY));

                // Place Front
                plan.Items.Add(new LayoutItem
                {
                    PageIndex = pageIndex,
                    X_Mm = unitX,
                    Y_Mm = unitY,
                    Width_Mm = cardW,
                    Height_Mm = cardH,
                    Role = CardRole.Front,
                    CardRegionId = frontCard.Id,
                    Region = frontCard,
                    CopyIndex = copyIdx + 1
                });

                // Place Back if present
                if (isPair && backCard != null)
                {
                    plan.Items.Add(new LayoutItem
                    {
                        PageIndex = pageIndex,
                        X_Mm = unitX + cardW + spacingX,
                        Y_Mm = unitY,
                        Width_Mm = cardW,
                        Height_Mm = cardH,
                        Role = CardRole.Back,
                        CardRegionId = backCard.Id,
                        Region = backCard,
                        CopyIndex = copyIdx + 1
                    });
                }
            }
        }

        private void BuildPairsGridLayout(
            LayoutPlan plan,
            CardRegion frontCard,
            CardRegion? backCard,
            LayoutTemplate template,
            int copies,
            double paperW,
            double paperH,
            double cardW,
            double cardH)
        {
            // Vertical stacked pairs repeated across the sheet
            BuildVerticalLayout(plan, frontCard, backCard, template, copies, paperW, paperH, cardW, cardH);
        }

        private void BuildGridCopiesLayout(
            LayoutPlan plan,
            IReadOnlyList<CardRegion> cards,
            LayoutTemplate template,
            int copies,
            double paperW,
            double paperH,
            double cardW,
            double cardH)
        {
            double spacingX = template.SpacingX_Mm;
            double spacingY = template.SpacingY_Mm;

            double usableW = paperW - template.MarginLeftMm - template.MarginRightMm;
            double usableH = paperH - template.MarginTopMm - template.MarginBottomMm;

            int cols = Math.Max(1, (int)Math.Floor((usableW + spacingX) / (cardW + spacingX)));
            int rows = Math.Max(1, (int)Math.Floor((usableH + spacingY) / (cardH + spacingY)));

            int itemsPerPage = cols * rows;

            // Generate sequence of items to print
            var sequence = new List<CardRegion>();
            for (int c = 0; c < copies; c++)
            {
                foreach (var card in cards)
                {
                    sequence.Add(card);
                }
            }

            int totalItems = sequence.Count;

            for (int i = 0; i < totalItems; i++)
            {
                int pageIndex = i / itemsPerPage;
                int posOnPage = i % itemsPerPage;
                int col = posOnPage % cols;
                int row = posOnPage / cols;

                int itemsOnThisPage = Math.Min(itemsPerPage, totalItems - (pageIndex * itemsPerPage));
                int activeColsOnPage = Math.Min(cols, itemsOnThisPage);
                int activeRowsOnPage = (int)Math.Ceiling((double)itemsOnThisPage / cols);

                double totalGridW = (activeColsOnPage * cardW) + ((activeColsOnPage - 1) * spacingX);
                double totalGridH = (activeRowsOnPage * cardH) + ((activeRowsOnPage - 1) * spacingY);

                double originX = Math.Max(template.MarginLeftMm, (paperW - totalGridW) / 2.0);
                double originY = Math.Max(template.MarginTopMm, (paperH - totalGridH) / 2.0);

                double posX = originX + (col * (cardW + spacingX));
                double posY = originY + (row * (cardH + spacingY));

                var currentCard = sequence[i];

                plan.Items.Add(new LayoutItem
                {
                    PageIndex = pageIndex,
                    X_Mm = posX,
                    Y_Mm = posY,
                    Width_Mm = cardW,
                    Height_Mm = cardH,
                    Role = currentCard.Role,
                    CardRegionId = currentCard.Id,
                    Region = currentCard,
                    CopyIndex = (i / cards.Count) + 1
                });
            }
        }

        public LayoutPlan CalculateCustomSizeLayout(IReadOnlyList<CustomLayoutItem> items, CustomSizeLayoutParameters parameters)
        {
            parameters ??= new CustomSizeLayoutParameters();

            var plan = new LayoutPlan
            {
                PaperSize = parameters.PaperSize,
                ShowCutMarks = parameters.ShowCutMarks,
                DrawBorderBox = parameters.DrawBorderBox
            };

            if (items == null || items.Count == 0)
            {
                plan.TotalPages = 1;
                plan.Orientation = parameters.OrientationMode == CustomLayoutOrientationMode.Landscape
                    ? PaperOrientation.Landscape
                    : PaperOrientation.Portrait;
                return plan;
            }

            // 1. Validate inputs
            foreach (var it in items)
            {
                if (it.WidthMm <= 0 || it.HeightMm <= 0 || it.Copies <= 0)
                {
                    throw new ArgumentException($"الأبعاد وعدد النسخ للعنصر '{it.Name}' يجب أن تكون أكبر من الصفر.");
                }
            }

            double margin = (double)parameters.SafetyMarginMm;
            double spacing = (double)parameters.SpacingMm;

            // Check impossible item against selected paper
            var (pw, ph) = parameters.PaperSize.GetDimensions(PaperOrientation.Portrait);
            double maxPaperDim = Math.Max(pw, ph) - 2 * margin;
            double minPaperDim = Math.Min(pw, ph) - 2 * margin;

            foreach (var it in items)
            {
                double w = (double)it.WidthMm;
                double h = (double)it.HeightMm;
                double itemMax = Math.Max(w, h);
                double itemMin = Math.Min(w, h);

                bool fitsEither = (w <= maxPaperDim && h <= minPaperDim) || (h <= maxPaperDim && w <= minPaperDim);
                if (!it.RotationAllowed)
                {
                    if (parameters.OrientationMode == CustomLayoutOrientationMode.Portrait)
                    {
                        var (pW, pH) = parameters.PaperSize.GetDimensions(PaperOrientation.Portrait);
                        if (w > pW - 2 * margin || h > pH - 2 * margin)
                        {
                            throw new InvalidOperationException("This item is larger than the selected paper.");
                        }
                    }
                    else if (parameters.OrientationMode == CustomLayoutOrientationMode.Landscape)
                    {
                        var (lW, lH) = parameters.PaperSize.GetDimensions(PaperOrientation.Landscape);
                        if (w > lW - 2 * margin || h > lH - 2 * margin)
                        {
                            throw new InvalidOperationException("This item is larger than the selected paper.");
                        }
                    }
                    else if (!fitsEither)
                    {
                        throw new InvalidOperationException("This item is larger than the selected paper.");
                    }
                }
                else if (!fitsEither)
                {
                    throw new InvalidOperationException("This item is larger than the selected paper.");
                }
            }

            // 2. Orientation Handling
            if (parameters.OrientationMode == CustomLayoutOrientationMode.Auto)
            {
                var portraitPlan = BuildPackedPlan(items, parameters.PaperSize, PaperOrientation.Portrait, margin, spacing, parameters.ShowCutMarks, parameters.DrawBorderBox);
                var landscapePlan = BuildPackedPlan(items, parameters.PaperSize, PaperOrientation.Landscape, margin, spacing, parameters.ShowCutMarks, parameters.DrawBorderBox);

                if (portraitPlan.TotalPages < landscapePlan.TotalPages)
                {
                    return portraitPlan;
                }
                if (landscapePlan.TotalPages < portraitPlan.TotalPages)
                {
                    return landscapePlan;
                }

                // If same pages, pick more compact layout
                double pScore = CalculateCompactedScore(portraitPlan);
                double lScore = CalculateCompactedScore(landscapePlan);
                return pScore <= lScore ? portraitPlan : landscapePlan;
            }
            else
            {
                var orientation = parameters.OrientationMode == CustomLayoutOrientationMode.Landscape
                    ? PaperOrientation.Landscape
                    : PaperOrientation.Portrait;
                return BuildPackedPlan(items, parameters.PaperSize, orientation, margin, spacing, parameters.ShowCutMarks, parameters.DrawBorderBox);
            }
        }

        private LayoutPlan BuildPackedPlan(
            IReadOnlyList<CustomLayoutItem> items,
            PaperSize paperSize,
            PaperOrientation orientation,
            double margin,
            double spacing,
            bool showCutMarks,
            bool drawBorderBox)
        {
            var plan = new LayoutPlan
            {
                PaperSize = paperSize,
                Orientation = orientation,
                ShowCutMarks = showCutMarks,
                DrawBorderBox = drawBorderBox
            };

            var (paperW, paperH) = paperSize.GetDimensions(orientation);

            // Expand copies
            var unplaced = new List<(CustomLayoutItem Item, int CopyIndex, double Width, double Height)>();
            foreach (var it in items)
            {
                for (int c = 1; c <= it.Copies; c++)
                {
                    unplaced.Add((it, c, (double)it.WidthMm, (double)it.HeightMm));
                }
            }

            // Sort largest items first
            unplaced = unplaced
                .OrderByDescending(u => Math.Max(u.Width, u.Height))
                .ThenByDescending(u => u.Width * u.Height)
                .ThenBy(u => u.Item.Id)
                .ThenBy(u => u.CopyIndex)
                .ToList();

            var pages = new List<List<LayoutItem>>();

            foreach (var u in unplaced)
            {
                bool placed = false;

                // Try existing pages in order
                for (int pageIdx = 0; pageIdx < pages.Count; pageIdx++)
                {
                    var pageItems = pages[pageIdx];
                    if (TryPlaceOnPage(pageItems, pageIdx, u.Item, u.CopyIndex, u.Width, u.Height, paperW, paperH, margin, spacing, out var placedItem))
                    {
                        pageItems.Add(placedItem);
                        plan.Items.Add(placedItem);
                        placed = true;
                        break;
                    }
                }

                // If not placed, create new page
                if (!placed)
                {
                    int newPageIdx = pages.Count;
                    var newPageItems = new List<LayoutItem>();
                    if (TryPlaceOnPage(newPageItems, newPageIdx, u.Item, u.CopyIndex, u.Width, u.Height, paperW, paperH, margin, spacing, out var placedItem))
                    {
                        newPageItems.Add(placedItem);
                        plan.Items.Add(placedItem);
                        pages.Add(newPageItems);
                    }
                    else
                    {
                        throw new InvalidOperationException("This item is larger than the selected paper.");
                    }
                }
            }

            plan.TotalPages = Math.Max(1, pages.Count);
            return plan;
        }

        private bool TryPlaceOnPage(
            List<LayoutItem> pageItems,
            int pageIndex,
            CustomLayoutItem item,
            int copyIndex,
            double widthMm,
            double heightMm,
            double paperW,
            double paperH,
            double margin,
            double spacing,
            out LayoutItem placedItem)
        {
            placedItem = null!;

            // Generate candidate placement points
            var candidates = new List<(double X, double Y)>
            {
                (margin, margin)
            };

            foreach (var p in pageItems)
            {
                candidates.Add((p.X_Mm + p.Width_Mm + spacing, p.Y_Mm));
                candidates.Add((p.X_Mm, p.Y_Mm + p.Height_Mm + spacing));
                candidates.Add((p.X_Mm + p.Width_Mm + spacing, margin));
                candidates.Add((margin, p.Y_Mm + p.Height_Mm + spacing));
            }

            for (int i = 0; i < pageItems.Count; i++)
            {
                for (int j = 0; j < pageItems.Count; j++)
                {
                    candidates.Add((pageItems[i].X_Mm + pageItems[i].Width_Mm + spacing, pageItems[j].Y_Mm + pageItems[j].Height_Mm + spacing));
                }
            }

            double minDim = Math.Min(widthMm, heightMm);
            double maxRight = paperW - margin - minDim + 1e-4;
            double maxBottom = paperH - margin - minDim + 1e-4;

            // Filter and sort candidates: top-first, left-first
            var sortedCandidates = candidates
                .Where(c => c.X >= margin - 1e-4 && c.Y >= margin - 1e-4 && c.X <= maxRight && c.Y <= maxBottom)
                .Distinct()
                .OrderBy(c => Math.Round(c.Y, 2))
                .ThenBy(c => Math.Round(c.X, 2))
                .ToList();

            foreach (var (cx, cy) in sortedCandidates)
            {
                // Orientation 0: Unrotated
                bool canFit0 = CanPlaceAt(pageItems, cx, cy, widthMm, heightMm, paperW, paperH, margin, spacing);

                // Orientation 1: Rotated (if allowed)
                bool canFit1 = false;
                if (item.RotationAllowed && Math.Abs(widthMm - heightMm) > 0.01)
                {
                    canFit1 = CanPlaceAt(pageItems, cx, cy, heightMm, widthMm, paperW, paperH, margin, spacing);
                }

                if (canFit0 && canFit1)
                {
                    // If both fit at candidate position, select orientation that minimizes vertical extent
                    bool preferRotated = (cy + widthMm) < (cy + heightMm);
                    double chosenW = preferRotated ? heightMm : widthMm;
                    double chosenH = preferRotated ? widthMm : heightMm;
                    int rotDeg = preferRotated ? 90 : 0;

                    placedItem = new LayoutItem
                    {
                        PageIndex = pageIndex,
                        X_Mm = Math.Round(cx, 3),
                        Y_Mm = Math.Round(cy, 3),
                        Width_Mm = Math.Round(chosenW, 3),
                        Height_Mm = Math.Round(chosenH, 3),
                        RotationDegrees = rotDeg,
                        Role = CardRole.Single,
                        CardRegionId = item.Id,
                        CustomItemId = item.Id,
                        ItemName = item.Name,
                        SourceImagePath = item.SourceFile,
                        CopyIndex = copyIndex
                    };
                    return true;
                }
                else if (canFit0)
                {
                    placedItem = new LayoutItem
                    {
                        PageIndex = pageIndex,
                        X_Mm = Math.Round(cx, 3),
                        Y_Mm = Math.Round(cy, 3),
                        Width_Mm = Math.Round(widthMm, 3),
                        Height_Mm = Math.Round(heightMm, 3),
                        RotationDegrees = 0,
                        Role = CardRole.Single,
                        CardRegionId = item.Id,
                        CustomItemId = item.Id,
                        ItemName = item.Name,
                        SourceImagePath = item.SourceFile,
                        CopyIndex = copyIndex
                    };
                    return true;
                }
                else if (canFit1)
                {
                    placedItem = new LayoutItem
                    {
                        PageIndex = pageIndex,
                        X_Mm = Math.Round(cx, 3),
                        Y_Mm = Math.Round(cy, 3),
                        Width_Mm = Math.Round(heightMm, 3),
                        Height_Mm = Math.Round(widthMm, 3),
                        RotationDegrees = 90,
                        Role = CardRole.Single,
                        CardRegionId = item.Id,
                        CustomItemId = item.Id,
                        ItemName = item.Name,
                        SourceImagePath = item.SourceFile,
                        CopyIndex = copyIndex
                    };
                    return true;
                }
            }

            return false;
        }

        private bool CanPlaceAt(
            List<LayoutItem> pageItems,
            double x,
            double y,
            double w,
            double h,
            double paperW,
            double paperH,
            double margin,
            double spacing)
        {
            // Boundary checks
            if (x < margin - 1e-4 || y < margin - 1e-4) return false;
            if (x + w > paperW - margin + 1e-4) return false;
            if (y + h > paperH - margin + 1e-4) return false;

            // Spacing & Overlap check against existing items
            foreach (var p in pageItems)
            {
                bool noOverlap = (x + w + spacing <= p.X_Mm + 1e-4) ||
                                 (p.X_Mm + p.Width_Mm + spacing <= x + 1e-4) ||
                                 (y + h + spacing <= p.Y_Mm + 1e-4) ||
                                 (p.Y_Mm + p.Height_Mm + spacing <= y + 1e-4);

                if (!noOverlap)
                {
                    return false;
                }
            }

            return true;
        }

        private double CalculateCompactedScore(LayoutPlan plan)
        {
            if (plan.Items.Count == 0) return 0;
            return (plan.TotalPages * 100000.0) +
                   plan.Items.Sum(i => i.Y_Mm + i.Height_Mm) +
                   plan.Items.Sum(i => i.X_Mm + i.Width_Mm);
        }
    }
}
