using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using OpenCvSharp;

namespace IdCardPrintShop.ViewModels
{
    public partial class ManualAdjustmentViewModel : ObservableObject
    {
        private readonly IImageProcessingService _imageService;
        private readonly CardItemViewModel _cardItemVm;
        private readonly Point2D[] _initialCorners;

        [ObservableProperty]
        private BitmapSource? _sourceImage;

        [ObservableProperty]
        private BitmapSource? _liveRectifiedPreview;

        [ObservableProperty]
        private double _imagePixelWidth;

        [ObservableProperty]
        private double _imagePixelHeight;

        // 4 Corner coordinates (in image pixel space)
        [ObservableProperty]
        private double _corner0X;
        [ObservableProperty]
        private double _corner0Y;

        [ObservableProperty]
        private double _corner1X;
        [ObservableProperty]
        private double _corner1Y;

        [ObservableProperty]
        private double _corner2X;
        [ObservableProperty]
        private double _corner2Y;

        [ObservableProperty]
        private double _corner3X;
        [ObservableProperty]
        private double _corner3Y;

        [ObservableProperty]
        private int _rotationQuarterTurns;

        [ObservableProperty]
        private double _fineRotationDegrees;

        [ObservableProperty]
        private double _safetyMarginPercent;

        [ObservableProperty]
        private CardRole _cardRole;

        [ObservableProperty]
        private CardDocumentType _documentType;

        public bool IsPassport => DocumentType == CardDocumentType.Passport;
        public bool IsNationalId => DocumentType == CardDocumentType.NationalId;

        [ObservableProperty]
        private string _statusMessage = "اسحب الدوائر الأربعة لتحديد أركان البطاقة بدقة.";

        public event Action? RequestClose;

        public ManualAdjustmentViewModel(CardItemViewModel cardItemVm, IImageProcessingService imageService)
        {
            _cardItemVm = cardItemVm;
            _imageService = imageService;

            var r = cardItemVm.Region;
            _initialCorners = (Point2D[])r.Corners.Clone();

            RotationQuarterTurns = r.RotationQuarterTurns;
            FineRotationDegrees = r.FineRotationDegrees;
            SafetyMarginPercent = r.SafetyMarginPercent;
            CardRole = r.Role;
            DocumentType = r.DocumentType;

            LoadSourceImage(r.SourceImagePath);

            Corner0X = r.Corners[0].X;
            Corner0Y = r.Corners[0].Y;
            Corner1X = r.Corners[1].X;
            Corner1Y = r.Corners[1].Y;
            Corner2X = r.Corners[2].X;
            Corner2Y = r.Corners[2].Y;
            Corner3X = r.Corners[3].X;
            Corner3Y = r.Corners[3].Y;

            UpdateLivePreview();
        }

        private void LoadSourceImage(string path)
        {
            if (!File.Exists(path)) return;

            using var mat = _imageService.LoadMat(path);
            ImagePixelWidth = mat.Width;
            ImagePixelHeight = mat.Height;
            SourceImage = _imageService.MatToBitmapSource(mat);
        }

        public void SetCorner(int index, double x, double y)
        {
            x = Math.Clamp(x, 0, ImagePixelWidth - 1);
            y = Math.Clamp(y, 0, ImagePixelHeight - 1);

            switch (index)
            {
                case 0:
                    Corner0X = x;
                    Corner0Y = y;
                    break;
                case 1:
                    Corner1X = x;
                    Corner1Y = y;
                    break;
                case 2:
                    Corner2X = x;
                    Corner2Y = y;
                    break;
                case 3:
                    Corner3X = x;
                    Corner3Y = y;
                    break;
            }

            UpdateLivePreview();
        }

        public void UpdateLivePreview()
        {
            try
            {
                var tempRegion = new CardRegion
                {
                    SourceImagePath = _cardItemVm.Region.SourceImagePath,
                    DocumentType = DocumentType,
                    Corners = new[]
                    {
                        new Point2D(Corner0X, Corner0Y),
                        new Point2D(Corner1X, Corner1Y),
                        new Point2D(Corner2X, Corner2Y),
                        new Point2D(Corner3X, Corner3Y)
                    },
                    RotationQuarterTurns = RotationQuarterTurns,
                    FineRotationDegrees = FineRotationDegrees,
                    SafetyMarginPercent = SafetyMarginPercent
                };

                using var mat = _imageService.WarpAndCorrectCard(tempRegion.SourceImagePath, tempRegion);
                LiveRectifiedPreview = _imageService.MatToBitmapSource(mat);
            }
            catch (Exception ex)
            {
                StatusMessage = $"تنبيه: {ex.Message}";
            }
        }

        [RelayCommand]
        public void Rotate90CW()
        {
            RotationQuarterTurns = (RotationQuarterTurns + 1) % 4;
            UpdateLivePreview();
        }

        [RelayCommand]
        public void Rotate90CCW()
        {
            RotationQuarterTurns = (RotationQuarterTurns + 3) % 4;
            UpdateLivePreview();
        }

        [RelayCommand]
        public void Flip180()
        {
            RotationQuarterTurns = (RotationQuarterTurns + 2) % 4;
            UpdateLivePreview();
        }

        [RelayCommand]
        public void ResetCorners()
        {
            Corner0X = _initialCorners[0].X;
            Corner0Y = _initialCorners[0].Y;
            Corner1X = _initialCorners[1].X;
            Corner1Y = _initialCorners[1].Y;
            Corner2X = _initialCorners[2].X;
            Corner2Y = _initialCorners[2].Y;
            Corner3X = _initialCorners[3].X;
            Corner3Y = _initialCorners[3].Y;
            RotationQuarterTurns = 0;
            FineRotationDegrees = 0;
            UpdateLivePreview();
            StatusMessage = "تمت استعادة الحدود السابقة.";
        }

        [RelayCommand]
        public void AutoDetectAgain()
        {
            try
            {
                var result = _imageService.DetectCards(_cardItemVm.Region.SourceImagePath);
                if (result.DetectedCards.Count > 0)
                {
                    var first = result.DetectedCards[0];
                    Corner0X = first.Corners[0].X;
                    Corner0Y = first.Corners[0].Y;
                    Corner1X = first.Corners[1].X;
                    Corner1Y = first.Corners[1].Y;
                    Corner2X = first.Corners[2].X;
                    Corner2Y = first.Corners[2].Y;
                    Corner3X = first.Corners[3].X;
                    Corner3Y = first.Corners[3].Y;
                    UpdateLivePreview();
                    StatusMessage = result.Message;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"تعذر الاكتشاف: {ex.Message}";
            }
        }

        /// <summary>
        /// Uses the full 6-pass detection pipeline to find card corners and remove
        /// surrounding background (hands, whitespace, shadows).
        /// Falls back to largest-contour MinAreaRect if no card is detected.
        /// </summary>
        [RelayCommand]
        public void AutoCropBackground()
        {
            try
            {
                StatusMessage = "جاري اكتشاف حدود البطاقة تلقائياً...";

                // ── Step 1: Try the full 6-pass service pipeline first ──────────
                var result = _imageService.DetectCards(_cardItemVm.Region.SourceImagePath);
                if (result.DetectedCards.Count > 0)
                {
                    // Pick the highest-confidence card
                    var best = result.DetectedCards
                        .OrderByDescending(c => c.Confidence)
                        .First();

                    Corner0X = best.Corners[0].X;
                    Corner0Y = best.Corners[0].Y;
                    Corner1X = best.Corners[1].X;
                    Corner1Y = best.Corners[1].Y;
                    Corner2X = best.Corners[2].X;
                    Corner2Y = best.Corners[2].Y;
                    Corner3X = best.Corners[3].X;
                    Corner3Y = best.Corners[3].Y;
                    UpdateLivePreview();
                    StatusMessage = $"✅ تم قص الخلفية — {result.Message}";
                    return;
                }

                // ── Step 2: Fallback — largest non-background contour ───────────
                using var src = _imageService.LoadMat(_cardItemVm.Region.SourceImagePath);
                if (src == null || src.Empty())
                {
                    StatusMessage = "تعذر تحميل الصورة.";
                    return;
                }

                int origW = src.Width, origH = src.Height;
                double totalArea = origW * (double)origH;
                double scale = Math.Min(1.0, 1400.0 / Math.Max(origW, origH));

                using var small = new Mat();
                Cv2.Resize(src, small, new Size((int)(origW * scale), (int)(origH * scale)));
                using var gray = new Mat();
                Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);
                using var blurred = new Mat();
                Cv2.GaussianBlur(gray, blurred, new Size(7, 7), 1.5);

                var quad = TryFindCardRect(blurred, scale, origW, origH, totalArea);
                if (quad != null)
                {
                    Corner0X = quad[0].X;  Corner0Y = quad[0].Y;
                    Corner1X = quad[1].X;  Corner1Y = quad[1].Y;
                    Corner2X = quad[2].X;  Corner2Y = quad[2].Y;
                    Corner3X = quad[3].X;  Corner3Y = quad[3].Y;
                    UpdateLivePreview();
                    StatusMessage = "✅ تم قص الخلفية (نتيجة احتياطية) — إذا لم يكن دقيقاً اضبط يدوياً.";
                }
                else
                {
                    StatusMessage = "⚠️ لم يتم العثور على حدود واضحة — جرب التعديل اليدوي أو زر إعادة الاكتشاف.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في القص: {ex.Message}";
            }
        }

        private static Point2D[]? TryFindCardRect(Mat blurred, double scale, int origW, int origH, double totalArea)
        {
            // 3 passes: adaptive, canny+dilate, otsu — return first best result
            var passes = new Func<Mat>[]
            {
                // Pass A: Adaptive threshold (handles uneven lighting / shadows from hands)
                () => {
                    using var adapt = new Mat();
                    Cv2.AdaptiveThreshold(blurred, adapt, 255,
                        AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 19, 4);
                    using var k7 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(7, 7));
                    var closed = new Mat();
                    Cv2.MorphologyEx(adapt, closed, MorphTypes.Close, k7, iterations: 2);
                    return closed;
                },
                // Pass B: Canny + aggressive dilation (great for hand-held)
                () => {
                    using var edges = new Mat();
                    Cv2.Canny(blurred, edges, 25, 90);
                    using var k13 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(13, 13));
                    var closed = new Mat();
                    Cv2.MorphologyEx(edges, closed, MorphTypes.Close, k13, iterations: 3);
                    Cv2.Dilate(closed, closed, k13);
                    return closed;
                },
                // Pass C: Otsu global threshold (flat white/light card on dark background)
                () => {
                    var otsu = new Mat();
                    Cv2.Threshold(blurred, otsu, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                    using var k9 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 9));
                    Cv2.MorphologyEx(otsu, otsu, MorphTypes.Close, k9, iterations: 2);
                    return otsu;
                }
            };

            Point2D[]? best = null;
            double bestScore = 0;

            foreach (var passFunc in passes)
            {
                using var binary = passFunc();
                Cv2.FindContours(binary, out var contours, out _,
                    RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                foreach (var contour in contours)
                {
                    double area = Cv2.ContourArea(contour) / (scale * scale);
                    double ratio = area / totalArea;
                    if (ratio < 0.04 || ratio > 0.92) continue;

                    var rr = Cv2.MinAreaRect(contour);
                    var pts = rr.Points();
                    var quad = pts.Select(p => new Point2D(
                        Math.Clamp(p.X / scale, 0, origW - 1),
                        Math.Clamp(p.Y / scale, 0, origH - 1)
                    )).ToArray();

                    // Sort: TL, TR, BR, BL
                    quad = SortCornersStatic(quad);

                    double w1 = quad[0].DistanceTo(quad[1]);
                    double w2 = quad[3].DistanceTo(quad[2]);
                    double h1 = quad[0].DistanceTo(quad[3]);
                    double h2 = quad[1].DistanceTo(quad[2]);
                    double avgW = (w1 + w2) / 2.0;
                    double avgH = (h1 + h2) / 2.0;
                    if (avgW < 40 || avgH < 40) continue;

                    double aspect = Math.Max(avgW, avgH) / Math.Min(avgW, avgH);
                    if (aspect < 1.05 || aspect > 3.0) continue;

                    // Score: prefer aspect ratios close to standard cards (1.585 / 1.420)
                    double diffId = Math.Abs(aspect - 1.585);
                    double diffPp = Math.Abs(aspect - 1.420);
                    double aspScore = Math.Max(0.3, 1.0 - Math.Min(diffId, diffPp) * 0.5);
                    // Reward larger area
                    double score = ratio * aspScore;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = quad;
                    }
                }
            }

            return best;
        }

        private static Point2D[] SortCornersStatic(Point2D[] pts)
        {
            // TL = min(x+y), BR = max(x+y), TR = min(y-x), BL = max(y-x)
            var sorted = pts.OrderBy(p => p.X + p.Y).ToArray();
            var tl = sorted[0];
            var br = sorted[3];
            var mid1 = sorted[1];
            var mid2 = sorted[2];
            var tr = mid1.Y < mid2.Y ? mid1 : mid2;
            var bl = mid1.Y < mid2.Y ? mid2 : mid1;
            return new[] { tl, tr, br, bl };
        }

        [RelayCommand]
        public void SetAsFront()
        {
            CardRole = CardRole.Front;
        }

        [RelayCommand]
        public void SetAsBack()
        {
            CardRole = CardRole.Back;
        }

        [RelayCommand]
        public void SetDocumentType(CardDocumentType newType)
        {
            DocumentType = newType;
            OnPropertyChanged(nameof(IsPassport));
            OnPropertyChanged(nameof(IsNationalId));
            UpdateLivePreview();
        }

        [RelayCommand]
        public void ApplyNationalIdPreset()
        {
            SetDocumentType(CardDocumentType.NationalId);
            ApplyPresetWithAspectRatio(85.60 / 54.00);
        }

        [RelayCommand]
        public void ApplyPassportPreset()
        {
            SetDocumentType(CardDocumentType.Passport);
            ApplyPresetWithAspectRatio(125.00 / 88.00);
        }

        private void ApplyPresetWithAspectRatio(double targetAspect)
        {
            double cardW = ImagePixelWidth * 0.70;
            double cardH = cardW / targetAspect;
            if (cardH > ImagePixelHeight * 0.75)
            {
                cardH = ImagePixelHeight * 0.70;
                cardW = cardH * targetAspect;
            }

            double startX = (ImagePixelWidth - cardW) / 2.0;
            double startY = (ImagePixelHeight - cardH) / 2.0;

            Corner0X = startX;
            Corner0Y = startY;
            Corner1X = startX + cardW;
            Corner1Y = startY;
            Corner2X = startX + cardW;
            Corner2Y = startY + cardH;
            Corner3X = startX;
            Corner3Y = startY + cardH;

            UpdateLivePreview();
        }

        [RelayCommand]
        public void Apply()
        {
            var r = _cardItemVm.Region;
            r.Corners[0] = new Point2D(Corner0X, Corner0Y);
            r.Corners[1] = new Point2D(Corner1X, Corner1Y);
            r.Corners[2] = new Point2D(Corner2X, Corner2Y);
            r.Corners[3] = new Point2D(Corner3X, Corner3Y);
            r.RotationQuarterTurns = RotationQuarterTurns;
            r.FineRotationDegrees = FineRotationDegrees;
            r.SafetyMarginPercent = SafetyMarginPercent;
            r.Role = CardRole;
            r.DocumentType = DocumentType;
            r.IsManualAdjusted = true;
            r.StatusMessage = "تم التعديل اليدوي";

            _cardItemVm.SetRole(CardRole);
            _cardItemVm.SetDocumentType(DocumentType);
            _cardItemVm.RotationQuarterTurns = RotationQuarterTurns;
            _cardItemVm.IsManualAdjusted = true;
            _cardItemVm.RefreshRectifiedPreview();

            RequestClose?.Invoke();
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke();
        }
    }
}
