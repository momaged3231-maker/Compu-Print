using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using IdCardPrintShop.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace IdCardPrintShop.Services
{
    public class OpenCvImageProcessingService : IImageProcessingService
    {
        private readonly Dictionary<string, Mat> _imageMatCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public Mat LoadMat(string imagePath)
        {
            lock (_lock)
            {
                if (_imageMatCache.TryGetValue(imagePath, out var cached) && !cached.IsDisposed)
                {
                    return cached;
                }

                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"الصورة غير موجودة: {imagePath}");
                }

                // Load with unchanged color channels (BGR)
                var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (mat.Empty())
                {
                    throw new InvalidOperationException($"تعذر تحميل الصورة عبر مكتبة المعالجة: {imagePath}");
                }

                _imageMatCache[imagePath] = mat;
                return mat;
            }
        }

        public DetectionResult DetectCards(string imagePath)
        {
            var mat = LoadMat(imagePath);
            return DetectCards(mat, imagePath);
        }

        public DetectionResult DetectCards(Mat sourceMat, string sourceImagePath = "")
        {
            var result = new DetectionResult
            {
                ImageWidth = sourceMat.Width,
                ImageHeight = sourceMat.Height
            };

            var detectedQuads = FindCardQuadrilaterals(sourceMat);

            if (detectedQuads.Count == 0)
            {
                // Case D: Low confidence / not detected reliably
                result.Case = DetectionCase.LowConfidence;
                result.IsConfident = false;
                result.Message = "مش قادر أحدد حدود البطاقة بدقة. يمكنك تحريك النقاط يدويًا لتحديد الأركان.";

                var fallbackCorners = GetFallbackCardCorners(sourceMat.Width, sourceMat.Height);
                var fallbackCard = new CardRegion
                {
                    Id = "Card_1",
                    Label = "بطاقة (تحديد يدوي)",
                    Role = CardRole.Front,
                    SourceImagePath = sourceImagePath,
                    Corners = fallbackCorners,
                    Confidence = 0.3,
                    IsManualAdjusted = true,
                    StatusMessage = result.Message
                };
                result.DetectedCards.Add(fallbackCard);
                return result;
            }

            // Order detected cards by position (e.g. top-to-bottom or left-to-right)
            var sortedQuads = detectedQuads
                .OrderBy(q => q.Centroid.Y)
                .ThenBy(q => q.Centroid.X)
                .ToList();

            if (sortedQuads.Count == 1)
            {
                result.Case = DetectionCase.SingleCard;
                result.IsConfident = true;
                var q = sortedQuads[0];
                string label = q.DocumentType == CardDocumentType.Passport ? "جواز سفر" : "بطاقة 1";
                result.Message = $"تم اكتشاف {label} بنجاح.";

                result.DetectedCards.Add(new CardRegion
                {
                    Id = "Card_1",
                    Label = label,
                    Role = CardRole.Front,
                    DocumentType = q.DocumentType,
                    SourceImagePath = sourceImagePath,
                    Corners = q.Corners,
                    Confidence = q.Confidence,
                    StatusMessage = "تم الاكتشاف التلقائي"
                });
            }
            else if (sortedQuads.Count == 2)
            {
                result.Case = DetectionCase.TwoCards;
                result.IsConfident = true;
                result.Message = "تم اكتشاف وجه وظهر البطاقة بنجاح في نفس الصورة.";

                result.DetectedCards.Add(new CardRegion
                {
                    Id = "Card_1",
                    Label = "الوجه (Front)",
                    Role = CardRole.Front,
                    DocumentType = sortedQuads[0].DocumentType,
                    SourceImagePath = sourceImagePath,
                    Corners = sortedQuads[0].Corners,
                    Confidence = sortedQuads[0].Confidence,
                    StatusMessage = "تم الاكتشاف التلقائي (الوجه)"
                });

                result.DetectedCards.Add(new CardRegion
                {
                    Id = "Card_2",
                    Label = "الظهر (Back)",
                    Role = CardRole.Back,
                    DocumentType = sortedQuads[1].DocumentType,
                    SourceImagePath = sourceImagePath,
                    Corners = sortedQuads[1].Corners,
                    Confidence = sortedQuads[1].Confidence,
                    StatusMessage = "تم الاكتشاف التلقائي (الظهر)"
                });
            }
            else
            {
                result.Case = DetectionCase.MultipleCards;
                result.IsConfident = true;
                result.Message = $"تم اكتشاف {sortedQuads.Count} بطاقات/مستندات في الصورة.";

                for (int i = 0; i < sortedQuads.Count; i++)
                {
                    var q = sortedQuads[i];
                    result.DetectedCards.Add(new CardRegion
                    {
                        Id = $"Card_{i + 1}",
                        Label = q.DocumentType == CardDocumentType.Passport ? $"جواز سفر {i + 1}" : $"بطاقة {i + 1}",
                        Role = (i == 0) ? CardRole.Front : (i == 1 ? CardRole.Back : CardRole.Single),
                        DocumentType = q.DocumentType,
                        SourceImagePath = sourceImagePath,
                        Corners = q.Corners,
                        Confidence = q.Confidence,
                        StatusMessage = $"تم الاكتشاف التلقائي ({i + 1})"
                    });
                }
            }

            return result;
        }

        private class CandidateQuad
        {
            public Point2D[] Corners { get; set; } = new Point2D[4];
            public Point2D Centroid { get; set; }
            public double Area { get; set; }
            public double AspectRatio { get; set; }
            public double Confidence { get; set; }
            public CardDocumentType DocumentType { get; set; } = CardDocumentType.NationalId;
        }

        private List<CandidateQuad> FindCardQuadrilaterals(Mat sourceMat)
        {
            var candidates = new List<CandidateQuad>();
            int width = sourceMat.Width;
            int height = sourceMat.Height;
            double totalImageArea = (double)width * height;

            // Downscale for speed and noise reduction if image is very large
            double scale = 1.0;
            Mat procMat;
            if (width > 1600 || height > 1600)
            {
                scale = 1200.0 / Math.Max(width, height);
                procMat = new Mat();
                Cv2.Resize(sourceMat, procMat, new Size((int)(width * scale), (int)(height * scale)), 0, 0, InterpolationFlags.Area);
            }
            else
            {
                procMat = sourceMat.Clone();
            }

            using (procMat)
            using (var gray = new Mat())
            using (var blurred = new Mat())
            {
                Cv2.CvtColor(procMat, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

                // Multi-pass detection:
                // Pass 1: Adaptive Thresholding (great for varying lighting & shadows)
                RunThresholdPass(blurred, candidates, scale, totalImageArea, width, height, useAdaptive: true);

                // Pass 2: Otsu / Binary Thresholding (great for high contrast backgrounds)
                RunThresholdPass(blurred, candidates, scale, totalImageArea, width, height, useAdaptive: false);

                // Pass 3: Canny Edge Detection with morphological closing
                RunCannyPass(blurred, candidates, scale, totalImageArea, width, height);

                // Pass 4: Multi-Channel Bilateral + Canny (effective for fabric, textured backgrounds, hands)
                RunMultiChannelPass(procMat, candidates, scale, totalImageArea, width, height);

                // Pass 5: High Luminance / Bright Card Mask (effective for white PVC cards and passports)
                RunLuminancePass(procMat, candidates, scale, totalImageArea, width, height);

                // Pass 6: Hand-held card detection (strong Canny + aggressive dilation + largest-contour MinAreaRect)
                RunHandHeldPass(blurred, candidates, scale, totalImageArea, width, height);
            }

            // Filter duplicates / overlapping boxes using Non-Maximum Suppression and Nested Feature suppression
            return FilterOverlappingQuads(candidates, width, height);
        }

        private void RunThresholdPass(Mat gray, List<CandidateQuad> candidates, double scale, double totalImageArea, int origW, int origH, bool useAdaptive)
        {
            using var thresh = new Mat();
            if (useAdaptive)
            {
                Cv2.AdaptiveThreshold(gray, thresh, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 21, 5);
            }
            else
            {
                Cv2.Threshold(gray, thresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            }

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
            using var closed = new Mat();
            Cv2.MorphologyEx(thresh, closed, MorphTypes.Close, kernel, iterations: 2);

            ExtractQuadsFromBinary(closed, candidates, scale, totalImageArea, origW, origH, 0.85);
        }

        private void RunCannyPass(Mat gray, List<CandidateQuad> candidates, double scale, double totalImageArea, int origW, int origH)
        {
            using var edges = new Mat();
            Cv2.Canny(gray, edges, 50, 150);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            using var dilated = new Mat();
            Cv2.Dilate(edges, dilated, kernel, iterations: 2);

            ExtractQuadsFromBinary(dilated, candidates, scale, totalImageArea, origW, origH, 0.90);
        }

        private void RunMultiChannelPass(Mat procMat, List<CandidateQuad> candidates, double scale, double totalImageArea, int origW, int origH)
        {
            Mat[] channels = Cv2.Split(procMat);
            using var bB = new Mat();
            using var bG = new Mat();
            using var bR = new Mat();
            using var edgeB = new Mat();
            using var edgeG = new Mat();
            using var edgeR = new Mat();
            using var combinedEdges = new Mat();

            Cv2.BilateralFilter(channels[0], bB, 9, 75, 75);
            Cv2.BilateralFilter(channels[1], bG, 9, 75, 75);
            Cv2.BilateralFilter(channels[2], bR, 9, 75, 75);

            Cv2.Canny(bB, edgeB, 40, 120);
            Cv2.Canny(bG, edgeG, 40, 120);
            Cv2.Canny(bR, edgeR, 40, 120);

            Cv2.BitwiseOr(edgeB, edgeG, combinedEdges);
            Cv2.BitwiseOr(combinedEdges, edgeR, combinedEdges);

            using var k9 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 9));
            using var closedEdges = new Mat();
            Cv2.MorphologyEx(combinedEdges, closedEdges, MorphTypes.Close, k9, iterations: 2);

            ExtractQuadsFromBinary(closedEdges, candidates, scale, totalImageArea, origW, origH, 0.92);

            foreach (var ch in channels) ch.Dispose();
        }

        private void RunLuminancePass(Mat procMat, List<CandidateQuad> candidates, double scale, double totalImageArea, int origW, int origH)
        {
            using var lab = new Mat();
            Cv2.CvtColor(procMat, lab, ColorConversionCodes.BGR2Lab);
            Mat[] channels = Cv2.Split(lab);
            using var lChan = channels[0];
            channels[1].Dispose();
            channels[2].Dispose();

            using var brightMask = new Mat();
            Cv2.Threshold(lChan, brightMask, 165, 255, ThresholdTypes.Binary);

            using var k9 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 9));
            using var closedBright = new Mat();
            Cv2.MorphologyEx(brightMask, closedBright, MorphTypes.Close, k9, iterations: 2);

            ExtractQuadsFromBinary(closedBright, candidates, scale, totalImageArea, origW, origH, 0.88);
        }

        /// <summary>
        /// Pass 6: Specifically tuned for hand-held card photos.
        /// Uses aggressive Canny thresholds + morphological close to merge gaps caused by fingers,
        /// then uses MinAreaRect on the largest valid contour to get a rotated bounding box.
        /// </summary>
        private void RunHandHeldPass(Mat blurred, List<CandidateQuad> candidates, double scale, double totalImageArea, int origW, int origH)
        {
            // Strong Canny to isolate hard edges (card border vs fingers/background)
            using var edges = new Mat();
            Cv2.Canny(blurred, edges, 30, 100);

            // Large kernel to bridge gaps where fingers break the card edge
            using var kBig = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(15, 15));
            using var closed = new Mat();
            Cv2.MorphologyEx(edges, closed, MorphTypes.Close, kBig, iterations: 3);
            Cv2.Dilate(closed, closed, kBig, iterations: 1);

            Cv2.FindContours(closed, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours.Length == 0) return;

            // Sort by area descending, try top 3 largest contours
            var sorted = contours
                .Select(c => new { c, area = Cv2.ContourArea(c) })
                .Where(x => {
                    double origArea = x.area / (scale * scale);
                    double r = origArea / totalImageArea;
                    return r >= 0.04 && r <= 0.90;
                })
                .OrderByDescending(x => x.area)
                .Take(3);

            foreach (var item in sorted)
            {
                var rotRect = Cv2.MinAreaRect(item.c);
                var pts = rotRect.Points();
                var quadCorners = pts.Select(p => new Point2D(p.X / scale, p.Y / scale)).ToArray();

                for (int i = 0; i < 4; i++)
                {
                    quadCorners[i].X = Math.Clamp(quadCorners[i].X, 0, origW - 1);
                    quadCorners[i].Y = Math.Clamp(quadCorners[i].Y, 0, origH - 1);
                }

                var sorted4 = SortCorners(quadCorners);

                double w1 = sorted4[0].DistanceTo(sorted4[1]);
                double w2 = sorted4[3].DistanceTo(sorted4[2]);
                double h1 = sorted4[0].DistanceTo(sorted4[3]);
                double h2 = sorted4[1].DistanceTo(sorted4[2]);
                double avgW = (w1 + w2) / 2.0;
                double avgH = (h1 + h2) / 2.0;

                if (avgW < 30 || avgH < 30) continue;

                double aspect = Math.Max(avgW, avgH) / Math.Min(avgW, avgH);
                if (aspect < 1.10 || aspect > 2.60) continue;

                var centroid = new Point2D(
                    sorted4.Average(p => p.X),
                    sorted4.Average(p => p.Y)
                );

                double diffId = Math.Abs(aspect - 1.585);
                double diffPassport = Math.Abs(aspect - 1.420);
                var docType = (diffPassport < diffId) ? CardDocumentType.Passport : CardDocumentType.NationalId;
                double confidence = 0.72 * Math.Max(0.5, 1.0 - (Math.Min(diffId, diffPassport) * 0.3));

                candidates.Add(new CandidateQuad
                {
                    Corners = sorted4,
                    Centroid = centroid,
                    Area = item.area / (scale * scale),
                    AspectRatio = aspect,
                    Confidence = Math.Clamp(confidence, 0.40, 0.88),
                    DocumentType = docType
                });
            }
        }

        private void ExtractQuadsFromBinary(Mat binaryMat, List<CandidateQuad> candidates, double scale, double totalImageArea, int origW, int origH, double baseConfidence)
        {
            Cv2.FindContours(binaryMat, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                double originalArea = area / (scale * scale);

                // Filter by area: card should be between 2% and 88% of total image area
                double areaRatio = originalArea / totalImageArea;
                if (areaRatio < 0.02 || areaRatio > 0.88)
                {
                    continue;
                }

                var perimeter = Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, 0.025 * perimeter, true);

                Point2D[]? quadCorners = null;

                if (approx.Length == 4 && Cv2.IsContourConvex(approx))
                {
                    quadCorners = approx.Select(p => new Point2D(p.X / scale, p.Y / scale)).ToArray();
                }
                else if (approx.Length >= 4 && approx.Length <= 10)
                {
                    var rotRect = Cv2.MinAreaRect(contour);
                    var pts = rotRect.Points();
                    quadCorners = pts.Select(p => new Point2D(p.X / scale, p.Y / scale)).ToArray();
                }

                if (quadCorners == null || quadCorners.Length != 4)
                {
                    continue;
                }

                // Clamp within original image boundaries
                for (int i = 0; i < 4; i++)
                {
                    quadCorners[i].X = Math.Clamp(quadCorners[i].X, 0, origW - 1);
                    quadCorners[i].Y = Math.Clamp(quadCorners[i].Y, 0, origH - 1);
                }

                // Sort corners deterministically: TL, TR, BR, BL
                var sortedCorners = SortCorners(quadCorners);

                // Compute width, height, aspect ratio
                double widthTop = sortedCorners[0].DistanceTo(sortedCorners[1]);
                double widthBottom = sortedCorners[3].DistanceTo(sortedCorners[2]);
                double heightLeft = sortedCorners[0].DistanceTo(sortedCorners[3]);
                double heightRight = sortedCorners[1].DistanceTo(sortedCorners[2]);

                double avgWidth = (widthTop + widthBottom) / 2.0;
                double avgHeight = (heightLeft + heightRight) / 2.0;

                if (avgWidth < 30 || avgHeight < 30)
                {
                    continue;
                }

                double aspect = Math.Max(avgWidth, avgHeight) / Math.Min(avgWidth, avgHeight);

                // ID Card CR80: ~1.585 | Passport ID-3: ~1.420
                // Wider range to handle perspective distortion from camera angle (hand-held)
                if (aspect < 1.10 || aspect > 2.60)
                {
                    continue;
                }

                // Calculate centroid
                var centroid = new Point2D(
                    sortedCorners.Average(p => p.X),
                    sortedCorners.Average(p => p.Y)
                );

                // Confidence based on distance to standard ID (1.585) or Passport (1.420)
                double diffId = Math.Abs(aspect - 1.585);
                double diffPassport = Math.Abs(aspect - 1.420);
                double minAspectDiff = Math.Min(diffId, diffPassport);

                var docType = (diffPassport < diffId) ? CardDocumentType.Passport : CardDocumentType.NationalId;
                double confidence = baseConfidence * Math.Max(0.55, 1.0 - (minAspectDiff * 0.28));

                candidates.Add(new CandidateQuad
                {
                    Corners = sortedCorners,
                    Centroid = centroid,
                    Area = originalArea,
                    AspectRatio = aspect,
                    Confidence = Math.Clamp(confidence, 0.4, 0.98),
                    DocumentType = docType
                });
            }
        }

        public Point2D[] SortCorners(Point2D[] pts)
        {
            if (pts == null || pts.Length != 4)
            {
                throw new ArgumentException("Must provide exactly 4 points to sort corners.", nameof(pts));
            }

            var sortedBySum = pts.OrderBy(p => p.X + p.Y).ToList();
            var tl = sortedBySum.First();
            var br = sortedBySum.Last();

            var remaining = pts.Where(p => !p.Equals(tl) && !p.Equals(br)).ToList();
            if (remaining.Count != 2)
            {
                remaining = pts.OrderBy(p => p.X).ToList();
                return new[] { remaining[0], remaining[1], remaining[3], remaining[2] };
            }

            Point2D tr, bl;
            if (remaining[0].X > remaining[1].X)
            {
                tr = remaining[0];
                bl = remaining[1];
            }
            else
            {
                tr = remaining[1];
                bl = remaining[0];
            }

            return new[] { tl, tr, br, bl };
        }

        private List<CandidateQuad> FilterOverlappingQuads(List<CandidateQuad> candidates, int imageW, int imageH)
        {
            var filtered = new List<CandidateQuad>();
            double totalArea = (double)imageW * imageH;

            // 1. Remove background border frames that cover > 85% and touch image edges
            foreach (var cand in candidates)
            {
                double minX = cand.Corners.Min(p => p.X);
                double maxX = cand.Corners.Max(p => p.X);
                double minY = cand.Corners.Min(p => p.Y);
                double maxY = cand.Corners.Max(p => p.Y);

                double w = maxX - minX;
                double h = maxY - minY;
                double areaRatio = (w * h) / totalArea;

                bool touchesEdges = (minX < 0.03 * imageW && minY < 0.03 * imageH &&
                                     maxX > 0.97 * imageW && maxY > 0.97 * imageH);

                if (areaRatio > 0.85 && touchesEdges)
                {
                    continue; // Skip image outer boundary
                }

                filtered.Add(cand);
            }

            // 2. Sort by bounding box Area descending to suppress internal features (nested elements like face photo, stamps)
            var sorted = filtered.OrderByDescending(c => {
                double w = c.Corners.Max(p => p.X) - c.Corners.Min(p => p.X);
                double h = c.Corners.Max(p => p.Y) - c.Corners.Min(p => p.Y);
                return w * h;
            }).ToList();
            var result = new List<CandidateQuad>();

            foreach (var cand in sorted)
            {
                double candMinX = cand.Corners.Min(p => p.X);
                double candMaxX = cand.Corners.Max(p => p.X);
                double candMinY = cand.Corners.Min(p => p.Y);
                double candMaxY = cand.Corners.Max(p => p.Y);
                double candArea = (candMaxX - candMinX) * (candMaxY - candMinY);

                bool isDuplicateOrNested = false;

                foreach (var kept in result)
                {
                    double keptMinX = kept.Corners.Min(p => p.X);
                    double keptMaxX = kept.Corners.Max(p => p.X);
                    double keptMinY = kept.Corners.Min(p => p.Y);
                    double keptMaxY = kept.Corners.Max(p => p.Y);
                    double keptArea = (keptMaxX - keptMinX) * (keptMaxY - keptMinY);

                    // Centroid containment test (if smaller quad's centroid is inside kept larger quad)
                    if (candArea < keptArea * 0.60 &&
                        cand.Centroid.X >= keptMinX && cand.Centroid.X <= keptMaxX &&
                        cand.Centroid.Y >= keptMinY && cand.Centroid.Y <= keptMaxY)
                    {
                        isDuplicateOrNested = true;
                        break;
                    }

                    // Bounding box intersection
                    double interMinX = Math.Max(candMinX, keptMinX);
                    double interMaxX = Math.Min(candMaxX, keptMaxX);
                    double interMinY = Math.Max(candMinY, keptMinY);
                    double interMaxY = Math.Min(candMaxY, keptMaxY);

                    if (interMaxX > interMinX && interMaxY > interMinY)
                    {
                        double interArea = (interMaxX - interMinX) * (interMaxY - interMinY);

                        // Nested test: If > 55% of candidate is inside an already-kept larger card
                        if (candArea > 0 && (interArea / candArea) > 0.55)
                        {
                            isDuplicateOrNested = true;
                            break;
                        }

                        // IoU overlap test
                        double unionArea = candArea + keptArea - interArea;
                        if (unionArea > 0 && (interArea / unionArea) > 0.38)
                        {
                            isDuplicateOrNested = true;
                            break;
                        }
                    }

                    // Centroid proximity test
                    double dist = cand.Centroid.DistanceTo(kept.Centroid);
                    double diag = Math.Sqrt(cand.Area);
                    if (dist < diag * 0.35)
                    {
                        isDuplicateOrNested = true;
                        break;
                    }
                }

                if (!isDuplicateOrNested)
                {
                    result.Add(cand);
                }
            }

            return result;
        }

        public Point2D[] GetFallbackCardCorners(int imageWidth, int imageHeight)
        {
            // Center a standard 85.6 x 54 mm (~1.585 aspect ratio) rectangle occupying ~60% of image
            double targetAspect = 85.60 / 54.00;
            double cardW = imageWidth * 0.70;
            double cardH = cardW / targetAspect;

            if (cardH > imageHeight * 0.75)
            {
                cardH = imageHeight * 0.70;
                cardW = cardH * targetAspect;
            }

            double startX = (imageWidth - cardW) / 2.0;
            double startY = (imageHeight - cardH) / 2.0;

            return new[]
            {
                new Point2D(startX, startY),                 // Top-Left
                new Point2D(startX + cardW, startY),         // Top-Right
                new Point2D(startX + cardW, startY + cardH), // Bottom-Right
                new Point2D(startX, startY + cardH)          // Bottom-Left
            };
        }

        public Mat WarpAndCorrectCard(string imagePath, CardRegion region, double? targetAspectRatio = null)
        {
            var mat = LoadMat(imagePath);
            return WarpAndCorrectCard(mat, region, targetAspectRatio);
        }

        public Mat WarpAndCorrectCard(Mat sourceMat, CardRegion region, double? targetAspectRatio = null)
        {
            if (region.Corners == null || region.Corners.Length != 4)
            {
                throw new ArgumentException("يجب تحديد 4 أركان للبطاقة لإجراء التصحيح.", nameof(region));
            }

            // Apply safety margin outward expansion
            var expandedCorners = ApplySafetyMargin(region.Corners, region.SafetyMarginPercent, sourceMat.Width, sourceMat.Height);

            // Compute target rectified dimensions
            double widthTop = expandedCorners[0].DistanceTo(expandedCorners[1]);
            double widthBottom = expandedCorners[3].DistanceTo(expandedCorners[2]);
            double heightLeft = expandedCorners[0].DistanceTo(expandedCorners[3]);
            double heightRight = expandedCorners[1].DistanceTo(expandedCorners[2]);

            double maxW = Math.Max(widthTop, widthBottom);
            double maxH = Math.Max(heightLeft, heightRight);
            double avgW = (widthTop + widthBottom) / 2.0;
            double avgH = (heightLeft + heightRight) / 2.0;

            int targetW;
            int targetH;

            double ratio = (targetAspectRatio.HasValue && targetAspectRatio.Value > 0)
                ? targetAspectRatio.Value
                : region.TargetAspectRatio;

            if (ratio > 0)
            {
                if (avgW >= avgH) // Landscape card
                {
                    targetW = (int)Math.Round(maxW);
                    targetH = (int)Math.Round(targetW / ratio);
                }
                else // Portrait card
                {
                    targetH = (int)Math.Round(maxH);
                    targetW = (int)Math.Round(targetH / ratio);
                }
            }
            else
            {
                targetW = (int)Math.Round(maxW);
                targetH = (int)Math.Round(maxH);
            }

            // Ensure dimensions are positive
            targetW = Math.Max(50, targetW);
            targetH = Math.Max(50, targetH);

            // Source points (TL, TR, BR, BL)
            var srcPoints = new Point2f[]
            {
                new((float)expandedCorners[0].X, (float)expandedCorners[0].Y),
                new((float)expandedCorners[1].X, (float)expandedCorners[1].Y),
                new((float)expandedCorners[2].X, (float)expandedCorners[2].Y),
                new((float)expandedCorners[3].X, (float)expandedCorners[3].Y)
            };

            // Destination points
            var dstPoints = new Point2f[]
            {
                new(0, 0),
                new(targetW, 0),
                new(targetW, targetH),
                new(0, targetH)
            };

            using var transform = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);
            var warped = new Mat();
            Cv2.WarpPerspective(sourceMat, warped, transform, new Size(targetW, targetH), InterpolationFlags.Cubic, BorderTypes.Replicate);

            // Apply quarter rotations if any
            int quarters = (region.RotationQuarterTurns % 4 + 4) % 4;
            Mat rotated = warped;
            if (quarters == 1)
            {
                var tmp = new Mat();
                Cv2.Rotate(rotated, tmp, RotateFlags.Rotate90Clockwise);
                rotated.Dispose();
                rotated = tmp;
            }
            else if (quarters == 2)
            {
                var tmp = new Mat();
                Cv2.Rotate(rotated, tmp, RotateFlags.Rotate180);
                rotated.Dispose();
                rotated = tmp;
            }
            else if (quarters == 3)
            {
                var tmp = new Mat();
                Cv2.Rotate(rotated, tmp, RotateFlags.Rotate90Counterclockwise);
                rotated.Dispose();
                rotated = tmp;
            }

            // Apply fine rotation if any
            if (Math.Abs(region.FineRotationDegrees) > 0.05)
            {
                var center = new Point2f(rotated.Width / 2f, rotated.Height / 2f);
                using var rotMat = Cv2.GetRotationMatrix2D(center, -region.FineRotationDegrees, 1.0);
                var fineRotated = new Mat();
                Cv2.WarpAffine(rotated, fineRotated, rotMat, rotated.Size(), InterpolationFlags.Cubic, BorderTypes.Replicate);
                rotated.Dispose();
                rotated = fineRotated;
            }

            return rotated;
        }

        private Point2D[] ApplySafetyMargin(Point2D[] corners, double marginPercent, int maxW, int maxH)
        {
            if (Math.Abs(marginPercent) < 0.01)
            {
                return corners;
            }

            double factor = 1.0 + (marginPercent / 100.0);
            var centroid = new Point2D(corners.Average(p => p.X), corners.Average(p => p.Y));

            var result = new Point2D[4];
            for (int i = 0; i < 4; i++)
            {
                var v = corners[i] - centroid;
                var expanded = centroid + (v * factor);
                result[i] = new Point2D(
                    Math.Clamp(expanded.X, 0, maxW - 1),
                    Math.Clamp(expanded.Y, 0, maxH - 1)
                );
            }

            return result;
        }

        public BitmapSource MatToBitmapSource(Mat mat)
        {
            if (mat == null || mat.Empty())
            {
                throw new ArgumentNullException(nameof(mat));
            }

            // Use OpenCvSharp.WpfExtensions for fast, memory-safe conversion
            var bmp = mat.ToBitmapSource();
            bmp.Freeze();
            return bmp;
        }

        public byte[] MatToPngBytes(Mat mat)
        {
            if (mat == null || mat.Empty())
            {
                return Array.Empty<byte>();
            }

            Cv2.ImEncode(".png", mat, out var buf);
            return buf;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var mat in _imageMatCache.Values)
                {
                    if (!mat.IsDisposed)
                    {
                        mat.Dispose();
                    }
                }
                _imageMatCache.Clear();
            }
        }
    }
}
