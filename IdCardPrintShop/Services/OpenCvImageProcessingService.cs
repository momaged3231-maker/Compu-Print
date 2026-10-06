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
                result.Message = "تم اكتشاف بطاقة واحدة بنجاح.";

                result.DetectedCards.Add(new CardRegion
                {
                    Id = "Card_1",
                    Label = "بطاقة 1",
                    Role = CardRole.Front,
                    SourceImagePath = sourceImagePath,
                    Corners = sortedQuads[0].Corners,
                    Confidence = sortedQuads[0].Confidence,
                    StatusMessage = "تم الاكتشاف التلقائي"
                });
            }
            else if (sortedQuads.Count == 2)
            {
                result.Case = DetectionCase.TwoCards;
                result.IsConfident = true;
                result.Message = "تم اكتشاف بطاقتين (وجه وظهر محتملان).";

                // Provide temporary identity Card A & Card B
                result.DetectedCards.Add(new CardRegion
                {
                    Id = "Card_1",
                    Label = "بطاقة أ (الوجه المقترح)",
                    Role = CardRole.Front,
                    SourceImagePath = sourceImagePath,
                    Corners = sortedQuads[0].Corners,
                    Confidence = sortedQuads[0].Confidence,
                    StatusMessage = "تم الاكتشاف التلقائي (أ)"
                });

                result.DetectedCards.Add(new CardRegion
                {
                    Id = "Card_2",
                    Label = "بطاقة ب (الظهر المقترح)",
                    Role = CardRole.Back,
                    SourceImagePath = sourceImagePath,
                    Corners = sortedQuads[1].Corners,
                    Confidence = sortedQuads[1].Confidence,
                    StatusMessage = "تم الاكتشاف التلقائي (ب)"
                });
            }
            else
            {
                result.Case = DetectionCase.MultipleCards;
                result.IsConfident = true;
                result.Message = $"تم اكتشاف {sortedQuads.Count} بطاقات/مستندات في الصورة.";

                for (int i = 0; i < sortedQuads.Count; i++)
                {
                    result.DetectedCards.Add(new CardRegion
                    {
                        Id = $"Card_{i + 1}",
                        Label = $"بطاقة {i + 1}",
                        Role = (i == 0) ? CardRole.Front : (i == 1 ? CardRole.Back : CardRole.Unknown),
                        SourceImagePath = sourceImagePath,
                        Corners = sortedQuads[i].Corners,
                        Confidence = sortedQuads[i].Confidence,
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
        }

        private List<CandidateQuad> FindCardQuadrilaterals(Mat sourceMat)
        {
            var candidates = new List<CandidateQuad>();
            int width = sourceMat.Width;
            int height = sourceMat.Height;
            double totalImageArea = width * height;

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
            }

            // Filter duplicates / overlapping boxes using Non-Maximum Suppression (IoU)
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

        private void ExtractQuadsFromBinary(Mat binaryMat, List<CandidateQuad> candidates, double scale, double totalImageArea, int origW, int origH, double baseConfidence)
        {
            Cv2.FindContours(binaryMat, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                double originalArea = area / (scale * scale);

                // Filter by area: card should be between 2% and 94% of total image area
                double areaRatio = originalArea / totalImageArea;
                if (areaRatio < 0.02 || areaRatio > 0.94)
                {
                    continue;
                }

                var perimeter = Cv2.ArcLength(contour, true);
                var approx = Cv2.ApproxPolyDP(contour, 0.025 * perimeter, true);

                Point2D[]? quadCorners = null;

                if (approx.Length == 4 && Cv2.IsContourConvex(approx))
                {
                    // 4 vertices found
                    quadCorners = approx.Select(p => new Point2D(p.X / scale, p.Y / scale)).ToArray();
                }
                else if (approx.Length >= 4 && approx.Length <= 10)
                {
                    // Many cards have slightly rounded corners leading to 5-8 vertices.
                    // MinAreaRect gives a tight bounding oriented rectangle.
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

                if (avgWidth < 20 || avgHeight < 20)
                {
                    continue;
                }

                double aspect = Math.Max(avgWidth, avgHeight) / Math.Min(avgWidth, avgHeight);

                // ID Card CR80 aspect ratio is 85.6 / 54.0 = ~1.585
                // Passports and other ID formats range 1.15 to 2.2
                if (aspect < 1.10 || aspect > 2.30)
                {
                    continue;
                }

                // Calculate centroid
                var centroid = new Point2D(
                    sortedCorners.Average(p => p.X),
                    sortedCorners.Average(p => p.Y)
                );

                // Confidence heuristic: proximity to 1.585 aspect ratio and reasonable area
                double aspectDiff = Math.Abs(aspect - 1.585);
                double confidence = baseConfidence * Math.Max(0.5, 1.0 - (aspectDiff * 0.3));

                candidates.Add(new CandidateQuad
                {
                    Corners = sortedCorners,
                    Centroid = centroid,
                    Area = originalArea,
                    AspectRatio = aspect,
                    Confidence = Math.Clamp(confidence, 0.4, 0.98)
                });
            }
        }

        public Point2D[] SortCorners(Point2D[] pts)
        {
            if (pts == null || pts.Length != 4)
            {
                throw new ArgumentException("Must provide exactly 4 points to sort corners.", nameof(pts));
            }

            // Top-Left: smallest (x + y)
            // Bottom-Right: largest (x + y)
            // Top-Right: smallest (y - x) [i.e. largest (x - y)]
            // Bottom-Left: largest (y - x) [i.e. smallest (x - y)]
            var sortedBySum = pts.OrderBy(p => p.X + p.Y).ToList();
            var tl = sortedBySum.First();
            var br = sortedBySum.Last();

            var remaining = pts.Where(p => !p.Equals(tl) && !p.Equals(br)).ToList();
            if (remaining.Count != 2)
            {
                // Fallback by X coordinate
                remaining = pts.OrderBy(p => p.X).ToList();
                return new[] { remaining[0], remaining[1], remaining[3], remaining[2] };
            }

            // Between remaining two, Top-Right has smaller Y (higher on screen) or larger X
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
            var result = new List<CandidateQuad>();
            var sorted = candidates.OrderByDescending(c => c.Area).ToList();

            foreach (var cand in sorted)
            {
                bool isDuplicate = false;
                foreach (var kept in result)
                {
                    // Check centroid distance
                    double dist = cand.Centroid.DistanceTo(kept.Centroid);
                    double diag = Math.Sqrt(cand.Area);

                    if (dist < diag * 0.4) // If centroids are very close, it's the same card detected twice
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
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

        public Mat WarpAndCorrectCard(string imagePath, CardRegion region, double? targetAspectRatio = 85.60 / 54.00)
        {
            var mat = LoadMat(imagePath);
            return WarpAndCorrectCard(mat, region, targetAspectRatio);
        }

        public Mat WarpAndCorrectCard(Mat sourceMat, CardRegion region, double? targetAspectRatio = 85.60 / 54.00)
        {
            if (region.Corners == null || region.Corners.Length != 4)
            {
                throw new ArgumentException("يجب تحديد 4 أركان للبطاقة لإجراء التصحيح.", nameof(region));
            }

            // Apply safety margin outward expansion (Section 7: safety margin داخلي صغير لمنع قص أطراف البطاقة)
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

            if (targetAspectRatio.HasValue && targetAspectRatio.Value > 0)
            {
                double ratio = targetAspectRatio.Value;
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
