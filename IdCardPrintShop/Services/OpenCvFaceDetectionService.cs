using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public class OpenCvFaceDetectionService : IFaceDetectionService
    {
        private CascadeClassifier? _cascade;
        private readonly string? _cascadePath;
        private readonly object _lock = new();

        public OpenCvFaceDetectionService(string? customCascadePath = null)
        {
            _cascadePath = FindCascadeXmlPath(customCascadePath);
            InitClassifier();
        }

        private static string? FindCascadeXmlPath(string? custom)
        {
            if (!string.IsNullOrEmpty(custom) && File.Exists(custom)) return custom;

            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "haarcascade_frontalface_default.xml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "haarcascade_frontalface_default.xml"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "IdCardPrintShop", "Assets", "haarcascade_frontalface_default.xml")
            };

            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }

            return null;
        }

        private void InitClassifier()
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_cascadePath) && File.Exists(_cascadePath))
                {
                    try
                    {
                        _cascade = new CascadeClassifier(_cascadePath);
                    }
                    catch
                    {
                        _cascade = null;
                    }
                }
            }
        }

        public FaceDetectionResult DetectFaces(string imagePath)
        {
            using var mat = Cv2.ImRead(imagePath);
            if (mat.Empty())
            {
                return new FaceDetectionResult
                {
                    Message = "تعذر تحميل ملف الصورة."
                };
            }
            return DetectFaces(mat);
        }

        public FaceDetectionResult DetectFaces(Mat image)
        {
            var result = new FaceDetectionResult();

            if (image == null || image.Empty())
            {
                result.Message = "الصورة فارغة.";
                return result;
            }

            Rect[] rawFaces = Array.Empty<Rect>();

            lock (_lock)
            {
                if (_cascade != null && !_cascade.Empty())
                {
                    using var gray = new Mat();
                    Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.EqualizeHist(gray, gray);

                    rawFaces = _cascade.DetectMultiScale(
                        gray,
                        scaleFactor: 1.1,
                        minNeighbors: 4,
                        flags: HaarDetectionTypes.ScaleImage,
                        minSize: new Size(Math.Min(40, image.Width / 10), Math.Min(40, image.Height / 10))
                    );
                }
            }

            // Fallback geometric/skin detector if cascade missed or classifier not loaded
            if (rawFaces.Length == 0)
            {
                var fallbackFace = DetectFaceFallbackBySkinColor(image);
                if (fallbackFace.HasValue)
                {
                    rawFaces = new[] { fallbackFace.Value };
                }
            }

            if (rawFaces.Length == 0)
            {
                result.Message = "لم يتم اكتشاف وجه تلقائياً. يمكنك تحديد موضع الوجه والقص يدوياً.";
                return result;
            }

            // Sort faces by area descending (primary subject is usually largest)
            var sortedFaces = rawFaces.OrderByDescending(f => f.Width * f.Height).ToList();

            foreach (var f in sortedFaces)
            {
                // Head dimensions estimation:
                // Face box typically covers eyes, nose, mouth.
                // Full head (hair to chin) extends ~25% above face box and ~10% below chin.
                double headTop = Math.Max(0, f.Y - (f.Height * 0.25));
                double headHeight = f.Height * 1.35;
                double centerX = f.X + (f.Width / 2.0);
                double centerY = f.Y + (f.Height / 2.0);

                result.Faces.Add(new DetectedFace
                {
                    Box = f,
                    Center = new Point2D(centerX, centerY),
                    HeadTopY = headTop,
                    HeadHeight = headHeight,
                    Confidence = 0.90
                });
            }

            if (result.Faces.Count == 1)
            {
                result.Message = "تم اكتشاف الوجه والرأس بنجاح.";
            }
            else
            {
                result.Message = $"تم اكتشاف {result.Faces.Count} وجوه في الصورة. يرجى اختيار الشخص المطلوب.";
            }

            return result;
        }

        private Rect? DetectFaceFallbackBySkinColor(Mat image)
        {
            try
            {
                using var hsv = new Mat();
                Cv2.CvtColor(image, hsv, ColorConversionCodes.BGR2HSV);

                // HSV skin tone range
                using var mask = new Mat();
                Cv2.InRange(hsv, new Scalar(0, 30, 60), new Scalar(25, 255, 255), mask);

                using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(11, 11));
                Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel, iterations: 2);

                Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                // Find largest contour in upper half of image with portrait aspect ratio
                double imgArea = image.Width * image.Height;
                Rect? bestRect = null;
                double maxArea = 0;

                foreach (var c in contours)
                {
                    var area = Cv2.ContourArea(c);
                    if (area < imgArea * 0.015 || area > imgArea * 0.6) continue;

                    var r = Cv2.BoundingRect(c);
                    // Faces/heads are located in upper 75% of image
                    if (r.Y > image.Height * 0.70) continue;
                    double aspect = (double)r.Width / r.Height;
                    if (aspect < 0.45 || aspect > 1.8) continue;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        // If contour includes neck (aspect < 0.85), trim bottom 20% to isolate face
                        if (aspect < 0.85)
                        {
                            bestRect = new Rect(r.X, r.Y, r.Width, (int)(r.Height * 0.80));
                        }
                        else
                        {
                            bestRect = r;
                        }
                    }
                }

                return bestRect;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _cascade?.Dispose();
                _cascade = null;
            }
        }
    }
}
