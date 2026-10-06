using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public class PersonalPhotoProcessor : IPersonalPhotoProcessor
    {
        private readonly IFaceDetectionService _faceService;
        private readonly IBackgroundRemovalService _bgService;
        private readonly IPhotoCorrectionService _colorService;
        private readonly IImageProcessingService _imgService;

        public PersonalPhotoProcessor(
            IFaceDetectionService faceService,
            IBackgroundRemovalService bgService,
            IPhotoCorrectionService colorService,
            IImageProcessingService imgService)
        {
            _faceService = faceService;
            _bgService = bgService;
            _colorService = colorService;
            _imgService = imgService;
        }

        public async Task<PersonalPhotoItem> ProcessPhotoAsync(string imagePath, PhotoProfile? profile = null)
        {
            return await Task.Run(() =>
            {
                using var mat = _imgService.LoadMat(imagePath);
                return ProcessPhoto(mat, imagePath, profile);
            });
        }

        public PersonalPhotoItem ProcessPhoto(Mat sourceMat, string sourcePath, PhotoProfile? profile = null)
        {
            profile ??= PhotoProfile.Standard4x6;

            var item = new PersonalPhotoItem
            {
                SourceImagePath = sourcePath,
                DisplayName = Path.GetFileNameWithoutExtension(sourcePath),
                Profile = profile,
                Copies = profile.RecommendedCopies,
                Status = PersonalPhotoStatus.Processing
            };

            // 1. Face Detection
            var faceResult = _faceService.DetectFaces(sourceMat);
            DetectedFace? primaryFace = faceResult.Faces.FirstOrDefault();

            if (primaryFace != null)
            {
                item.Parameters.FaceBox = new RectD(
                    primaryFace.Box.X,
                    primaryFace.Box.Y,
                    primaryFace.Box.Width,
                    primaryFace.Box.Height
                );
                item.Parameters.HeadCenterX = primaryFace.Center.X;
                item.Parameters.HeadCenterY = primaryFace.Center.Y;
                item.Parameters.HeadHeight = primaryFace.HeadHeight;

                if (faceResult.IsMultipleFaces)
                {
                    item.Status = PersonalPhotoStatus.NeedsReview;
                    item.StatusMessage = "تم اكتشاف أكثر من شخص. يرجى تأكيد الشخص المطلوب.";
                }
                else
                {
                    item.Status = PersonalPhotoStatus.Ready;
                    item.StatusMessage = "جاهزة للطباعة";
                }
            }
            else
            {
                item.Status = PersonalPhotoStatus.NeedsReview;
                item.StatusMessage = "لم يتم اكتشاف الوجه تلقائياً. يرجى ضبط إطار القص يدوياً.";
            }

            // 2. Auto Crop & Head Positioning
            item.Parameters.CropBox = CalculateAutoCrop(sourceMat.Width, sourceMat.Height, primaryFace, profile);

            // 3. Defaults
            item.Parameters.RemoveBackground = true;
            item.Parameters.BackgroundType = PhotoBackgroundType.PureWhite;
            item.Parameters.BgRed = 255;
            item.Parameters.BgGreen = 255;
            item.Parameters.BgBlue = 255;

            return item;
        }

        public RectD CalculateAutoCrop(int imageWidth, int imageHeight, DetectedFace? face, PhotoProfile profile)
        {
            double targetAspect = profile.AspectRatio; // e.g. 40 / 60 = 0.66667

            if (face != null)
            {
                // Calculate desired crop dimensions based on head proportion
                double desiredCropH = face.HeadHeight / profile.HeadHeightPercent;
                double desiredCropW = desiredCropH * targetAspect;

                // Scale down if larger than image
                if (desiredCropH > imageHeight)
                {
                    desiredCropH = imageHeight;
                    desiredCropW = desiredCropH * targetAspect;
                }
                if (desiredCropW > imageWidth)
                {
                    desiredCropW = imageWidth;
                    desiredCropH = desiredCropW / targetAspect;
                }

                // Vertical positioning: headroom above top of head/hair
                double cropY = face.HeadTopY - (desiredCropH * profile.TopMarginPercent);
                if (cropY < 0) cropY = 0;
                if (cropY + desiredCropH > imageHeight)
                {
                    cropY = Math.Max(0, imageHeight - desiredCropH);
                }

                // Horizontal positioning: centered on head center
                double cropX = face.Center.X - (desiredCropW / 2.0);
                if (cropX < 0) cropX = 0;
                if (cropX + desiredCropW > imageWidth)
                {
                    cropX = Math.Max(0, imageWidth - desiredCropW);
                }

                return new RectD(cropX, cropY, desiredCropW, desiredCropH);
            }
            else
            {
                // Fallback: Portrait center crop
                double cropH = imageHeight * 0.85;
                double cropW = cropH * targetAspect;

                if (cropW > imageWidth)
                {
                    cropW = imageWidth * 0.90;
                    cropH = cropW / targetAspect;
                }

                double cropX = (imageWidth - cropW) / 2.0;
                double cropY = (imageHeight - cropH) / 4.0; // slightly higher than center

                return new RectD(cropX, cropY, cropW, cropH);
            }
        }

        public Mat RenderFinalPhoto(Mat sourceMat, PersonalPhotoParameters parameters, PhotoProfile profile)
        {
            if (sourceMat == null || sourceMat.Empty()) return new Mat();

            int imgW = sourceMat.Width;
            int imgH = sourceMat.Height;

            // 1. Clamp CropBox
            var cb = parameters.CropBox;
            int cx = Math.Clamp((int)cb.X, 0, imgW - 10);
            int cy = Math.Clamp((int)cb.Y, 0, imgH - 10);
            int cw = Math.Clamp((int)cb.Width, 10, imgW - cx);
            int ch = Math.Clamp((int)cb.Height, 10, imgH - cy);

            var cropRect = new Rect(cx, cy, cw, ch);
            using var cropped = new Mat(sourceMat, cropRect);

            // 2. Color adjustments (brightness, contrast, saturation, temperature)
            Mat colorBase = parameters.AutoEnhance ? _colorService.ApplyAutoEnhancement(cropped) : cropped.Clone();
            using var adjusted = _colorService.ApplyAdjustments(colorBase, parameters);
            colorBase.Dispose();

            // 3. Background removal and replacement
            Mat result;
            if (parameters.RemoveBackground && parameters.BackgroundType != PhotoBackgroundType.Original)
            {
                // Translate face box relative to crop rect
                Rect? relativeFace = null;
                if (parameters.FaceBox.Width > 0 && parameters.FaceBox.Height > 0)
                {
                    var fb = parameters.FaceBox;
                    relativeFace = new Rect(
                        (int)(fb.X - cx),
                        (int)(fb.Y - cy),
                        (int)fb.Width,
                        (int)fb.Height
                    );
                }

                result = _bgService.RemoveAndReplaceBackground(
                    adjusted,
                    relativeFace,
                    parameters.BackgroundType,
                    parameters.BgRed,
                    parameters.BgGreen,
                    parameters.BgBlue,
                    parameters.EdgeFeatherRadius
                );
            }
            else
            {
                result = adjusted.Clone();
            }

            // 4. Rotations if any
            int quarters = (parameters.RotationQuarterTurns % 4 + 4) % 4;
            if (quarters == 1)
            {
                var tmp = new Mat();
                Cv2.Rotate(result, tmp, RotateFlags.Rotate90Clockwise);
                result.Dispose();
                result = tmp;
            }
            else if (quarters == 2)
            {
                var tmp = new Mat();
                Cv2.Rotate(result, tmp, RotateFlags.Rotate180);
                result.Dispose();
                result = tmp;
            }
            else if (quarters == 3)
            {
                var tmp = new Mat();
                Cv2.Rotate(result, tmp, RotateFlags.Rotate90Counterclockwise);
                result.Dispose();
                result = tmp;
            }

            return result;
        }

        public void Dispose()
        {
            _faceService.Dispose();
            _bgService.Dispose();
        }
    }
}
