using System;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public class SmartBackgroundRemovalService : IBackgroundRemovalService
    {
        private readonly IOnnxModelProvider _onnxProvider;

        public bool IsOnnxModelAvailable => _onnxProvider.IsModelLoaded;
        public string ActiveEngineName => _onnxProvider.IsModelLoaded
            ? $"ONNX ({_onnxProvider.ModelName})"
            : "OpenCV Anatomical Segmentation + Edge Refinement";

        public SmartBackgroundRemovalService(IOnnxModelProvider? onnxProvider = null)
        {
            _onnxProvider = onnxProvider ?? new LocalOnnxModelProvider();
        }

        public Mat GenerateAlphaMask(Mat sourceMat, Rect? faceBox, double featherRadius = 2.0)
        {
            if (sourceMat == null || sourceMat.Empty())
            {
                throw new ArgumentNullException(nameof(sourceMat));
            }

            // 1. Try ONNX local inference if model is available
            if (_onnxProvider.IsModelLoaded)
            {
                var onnxMask = _onnxProvider.RunSegmentationInference(sourceMat);
                if (onnxMask != null && !onnxMask.Empty())
                {
                    RefineMaskEdges(onnxMask, featherRadius);
                    return onnxMask;
                }
            }

            // 2. High-performance OpenCV Anatomical GrabCut
            return GenerateGrabCutMask(sourceMat, faceBox, featherRadius);
        }

        private Mat GenerateGrabCutMask(Mat sourceMat, Rect? faceBox, double featherRadius)
        {
            int w = sourceMat.Width;
            int h = sourceMat.Height;

            // Define person bounding box
            Rect personRect;
            if (faceBox.HasValue && faceBox.Value.Width > 0 && faceBox.Value.Height > 0)
            {
                var fb = faceBox.Value;
                // Person head extends slightly above faceBox
                int headTop = Math.Max(0, (int)(fb.Y - fb.Height * 0.35));
                // Shoulders extend ~1.2x face width to each side
                int shoulderLeft = Math.Max(0, (int)(fb.X - fb.Width * 1.1));
                int shoulderRight = Math.Min(w, (int)(fb.X + fb.Width * 2.1));
                int bodyBottom = h; // down to bottom of image

                personRect = new Rect(shoulderLeft, headTop, shoulderRight - shoulderLeft, bodyBottom - headTop);
            }
            else
            {
                // Default center portrait box if no face was detected
                int marginX = (int)(w * 0.15);
                int marginY = (int)(h * 0.08);
                personRect = new Rect(marginX, marginY, w - (2 * marginX), h - marginY);
            }

            // Clamp personRect to image boundaries with safe 2px margin
            personRect.X = Math.Clamp(personRect.X, 2, w - 5);
            personRect.Y = Math.Clamp(personRect.Y, 2, h - 5);
            personRect.Width = Math.Clamp(personRect.Width, 20, w - personRect.X - 2);
            personRect.Height = Math.Clamp(personRect.Height, 20, h - personRect.Y - 2);

            // Downscale for GrabCut speed if large, then upscale mask
            double scale = 1.0;
            Mat procMat;
            if (w > 800 || h > 800)
            {
                scale = 600.0 / Math.Max(w, h);
                procMat = new Mat();
                Cv2.Resize(sourceMat, procMat, new Size((int)(w * scale), (int)(h * scale)), 0, 0, InterpolationFlags.Area);
            }
            else
            {
                procMat = sourceMat.Clone();
            }

            Rect scaledRect = new(
                (int)(personRect.X * scale),
                (int)(personRect.Y * scale),
                (int)(personRect.Width * scale),
                (int)(personRect.Height * scale)
            );

            // Clamp scaled rect
            scaledRect.X = Math.Clamp(scaledRect.X, 1, procMat.Width - 3);
            scaledRect.Y = Math.Clamp(scaledRect.Y, 1, procMat.Height - 3);
            scaledRect.Width = Math.Clamp(scaledRect.Width, 10, procMat.Width - scaledRect.X - 1);
            scaledRect.Height = Math.Clamp(scaledRect.Height, 10, procMat.Height - scaledRect.Y - 1);

            using var mask = new Mat(procMat.Size(), MatType.CV_8UC1, new Scalar(0));
            using var bgdModel = new Mat();
            using var fgdModel = new Mat();

            try
            {
                // 3 GrabCut iterations is sweet spot for speed (<100ms) and crisp segmentation
                Cv2.GrabCut(procMat, mask, scaledRect, bgdModel, fgdModel, 3, GrabCutModes.InitWithRect);

                // Convert GrabCut result to binary mask: 1 & 3 are foreground (PR_FGD & FGD)
                using var binMaskScaled = new Mat();
                Cv2.Compare(mask, new Scalar(1), binMaskScaled, CmpTypes.EQ); // FGD
                using var prFgd = new Mat();
                Cv2.Compare(mask, new Scalar(3), prFgd, CmpTypes.EQ);         // PR_FGD
                Cv2.BitwiseOr(binMaskScaled, prFgd, binMaskScaled);

                // Resize mask back to full image size
                var fullMask = new Mat();
                if (Math.Abs(scale - 1.0) > 0.01)
                {
                    Cv2.Resize(binMaskScaled, fullMask, sourceMat.Size(), 0, 0, InterpolationFlags.Cubic);
                }
                else
                {
                    fullMask = binMaskScaled.Clone();
                }

                procMat.Dispose();

                // Clean small holes and noise
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
                Cv2.MorphologyEx(fullMask, fullMask, MorphTypes.Close, kernel, iterations: 2);

                RefineMaskEdges(fullMask, featherRadius);
                return fullMask;
            }
            catch
            {
                procMat.Dispose();
                // Safe fallback: solid elliptical foreground mask
                var fallbackMask = new Mat(sourceMat.Size(), MatType.CV_8UC1, new Scalar(0));
                var center = new Point(personRect.X + personRect.Width / 2, personRect.Y + personRect.Height / 2);
                var axes = new Size(personRect.Width / 2, personRect.Height / 2);
                Cv2.Ellipse(fallbackMask, center, axes, 0, 0, 360, new Scalar(255), -1);
                return fallbackMask;
            }
        }

        private void RefineMaskEdges(Mat mask, double featherRadius)
        {
            if (featherRadius <= 0.1) return;

            // Controlled boundary feathering (1-3px) to remove jagged edges without blurry halos
            int ksize = ((int)Math.Ceiling(featherRadius * 2)) | 1; // must be odd
            ksize = Math.Clamp(ksize, 3, 9);

            using var blurred = new Mat();
            Cv2.GaussianBlur(mask, blurred, new Size(ksize, ksize), featherRadius);

            // Blend blurred edges with sharp interior
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(ksize, ksize));
            using var eroded = new Mat();
            using var dilated = new Mat();
            Cv2.Erode(mask, eroded, kernel);
            Cv2.Dilate(mask, dilated, kernel);

            // Region between eroded and dilated is transition zone
            using var boundary = new Mat();
            Cv2.Subtract(dilated, eroded, boundary);

            // In boundary zone, copy smooth blurred values; elsewhere keep solid 0 or 255
            blurred.CopyTo(mask, boundary);
        }

        public Mat RemoveAndReplaceBackground(
            Mat sourceMat,
            Rect? faceBox,
            PhotoBackgroundType bgType,
            byte customR = 255,
            byte customG = 255,
            byte customB = 255,
            double featherRadius = 2.0)
        {
            if (sourceMat == null || sourceMat.Empty())
            {
                throw new ArgumentNullException(nameof(sourceMat));
            }

            if (bgType == PhotoBackgroundType.Original)
            {
                return sourceMat.Clone();
            }

            using var alphaMask = GenerateAlphaMask(sourceMat, faceBox, featherRadius);

            // Determine target background color (BGR)
            Scalar bgColor = bgType switch
            {
                PhotoBackgroundType.PureWhite => new Scalar(255, 255, 255),
                PhotoBackgroundType.LightBlue => new Scalar(250, 225, 195), // BGR for pale sky studio blue
                PhotoBackgroundType.LightGray => new Scalar(235, 235, 235),
                PhotoBackgroundType.Transparent => new Scalar(255, 255, 255),
                _ => new Scalar(customB, customG, customR)
            };

            // Composite foreground over background color
            var result = new Mat(sourceMat.Size(), MatType.CV_8UC3, bgColor);

            // Normalize alpha to 0.0 - 1.0
            using var alphaFloat = new Mat();
            alphaMask.ConvertTo(alphaFloat, MatType.CV_32FC1, 1.0 / 255.0);

            using var srcFloat = new Mat();
            sourceMat.ConvertTo(srcFloat, MatType.CV_32FC3);

            using var bgFloat = new Mat(sourceMat.Size(), MatType.CV_32FC3, bgColor);

            // 3-channel alpha
            using var alpha3Ch = new Mat();
            Cv2.CvtColor(alphaFloat, alpha3Ch, ColorConversionCodes.GRAY2BGR);

            // resultFloat = srcFloat * alpha + bgFloat * (1 - alpha)
            using var invAlpha = new Mat();
            Cv2.Subtract(new Scalar(1.0, 1.0, 1.0), alpha3Ch, invAlpha);

            using var fgPart = new Mat();
            using var bgPart = new Mat();
            Cv2.Multiply(srcFloat, alpha3Ch, fgPart);
            Cv2.Multiply(bgFloat, invAlpha, bgPart);

            using var resultFloat = new Mat();
            Cv2.Add(fgPart, bgPart, resultFloat);

            resultFloat.ConvertTo(result, MatType.CV_8UC3);
            return result;
        }

        public void Dispose()
        {
            _onnxProvider?.Dispose();
        }
    }
}
