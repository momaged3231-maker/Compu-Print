using System;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public interface IPhotoCorrectionService
    {
        Mat ApplyAutoEnhancement(Mat sourceMat);
        Mat ApplyAdjustments(Mat sourceMat, PersonalPhotoParameters parameters);
    }

    public class PhotoCorrectionService : IPhotoCorrectionService
    {
        public Mat ApplyAutoEnhancement(Mat sourceMat)
        {
            if (sourceMat == null || sourceMat.Empty()) return new Mat();

            var result = sourceMat.Clone();

            // 1. Mild Gray-World White Balance
            ApplyMildWhiteBalance(result);

            // 2. Gentle Luminance CLAHE (Contrast Limited Adaptive Histogram Equalization)
            ApplyGentleClahe(result);

            return result;
        }

        private void ApplyMildWhiteBalance(Mat bgrMat)
        {
            var means = Cv2.Mean(bgrMat);
            double avg = (means.Val0 + means.Val1 + means.Val2) / 3.0;

            // Only correct if there is an obvious color cast, and blend only 40% to keep it natural
            double scaleB = 1.0 + (0.4 * ((avg / Math.Max(10, means.Val0)) - 1.0));
            double scaleG = 1.0 + (0.4 * ((avg / Math.Max(10, means.Val1)) - 1.0));
            double scaleR = 1.0 + (0.4 * ((avg / Math.Max(10, means.Val2)) - 1.0));

            // Clamp scales between 0.85 and 1.15 to prevent extreme color shifts
            scaleB = Math.Clamp(scaleB, 0.85, 1.15);
            scaleG = Math.Clamp(scaleG, 0.85, 1.15);
            scaleR = Math.Clamp(scaleR, 0.85, 1.15);

            Mat[] channels = Cv2.Split(bgrMat);
            channels[0].ConvertTo(channels[0], -1, scaleB, 0);
            channels[1].ConvertTo(channels[1], -1, scaleG, 0);
            channels[2].ConvertTo(channels[2], -1, scaleR, 0);
            Cv2.Merge(channels, bgrMat);

            foreach (var ch in channels) ch.Dispose();
        }

        private void ApplyGentleClahe(Mat bgrMat)
        {
            using var lab = new Mat();
            Cv2.CvtColor(bgrMat, lab, ColorConversionCodes.BGR2Lab);

            Mat[] labChannels = Cv2.Split(lab);

            // Underexposure compensation: lift dark photos towards standard portrait luminance (~115-125)
            double meanL = Cv2.Mean(labChannels[0]).Val0;
            if (meanL < 95.0)
            {
                double targetL = 120.0;
                double gain = Math.Clamp(targetL / Math.Max(20.0, meanL), 1.0, 2.5);
                labChannels[0].ConvertTo(labChannels[0], -1, gain, 0);
            }

            // Very mild clipLimit = 1.5, tileGridSize = 8x8
            using var clahe = Cv2.CreateCLAHE(clipLimit: 1.5, tileGridSize: new Size(8, 8));
            using var clahedL = new Mat();
            clahe.Apply(labChannels[0], clahedL);

            // Blend 50% CLAHE with 50% original L channel to avoid harsh shadows
            Cv2.AddWeighted(clahedL, 0.5, labChannels[0], 0.5, 0, labChannels[0]);

            Cv2.Merge(labChannels, lab);
            Cv2.CvtColor(lab, bgrMat, ColorConversionCodes.Lab2BGR);

            foreach (var ch in labChannels) ch.Dispose();
        }

        public Mat ApplyAdjustments(Mat sourceMat, PersonalPhotoParameters parameters)
        {
            if (sourceMat == null || sourceMat.Empty()) return new Mat();

            var result = sourceMat.Clone();

            // 1. Brightness & Contrast
            // alpha = 1 + contrast/100, beta = brightness
            double contrastFactor = 1.0 + (parameters.Contrast / 100.0);
            double brightnessOffset = parameters.Brightness * 1.2;

            if (Math.Abs(parameters.Contrast) > 0.1 || Math.Abs(parameters.Brightness) > 0.1)
            {
                result.ConvertTo(result, -1, contrastFactor, brightnessOffset);
            }

            // 2. Saturation
            if (Math.Abs(parameters.Saturation) > 0.1)
            {
                using var hsv = new Mat();
                Cv2.CvtColor(result, hsv, ColorConversionCodes.BGR2HSV);
                Mat[] hsvChannels = Cv2.Split(hsv);

                double satScale = 1.0 + (parameters.Saturation / 100.0);
                hsvChannels[1].ConvertTo(hsvChannels[1], -1, satScale, 0);

                Cv2.Merge(hsvChannels, hsv);
                Cv2.CvtColor(hsv, result, ColorConversionCodes.HSV2BGR);

                foreach (var ch in hsvChannels) ch.Dispose();
            }

            // 3. Color Temperature (Cool / Warm shift)
            if (Math.Abs(parameters.Temperature) > 0.1)
            {
                Mat[] bgrChannels = Cv2.Split(result);
                double tempShift = parameters.Temperature * 0.8;

                if (tempShift > 0)
                {
                    // Warmer: increase Red, decrease Blue
                    bgrChannels[2].ConvertTo(bgrChannels[2], -1, 1.0, tempShift);
                    bgrChannels[0].ConvertTo(bgrChannels[0], -1, 1.0, -tempShift * 0.5);
                }
                else
                {
                    // Cooler: increase Blue, decrease Red
                    bgrChannels[0].ConvertTo(bgrChannels[0], -1, 1.0, -tempShift);
                    bgrChannels[2].ConvertTo(bgrChannels[2], -1, 1.0, tempShift * 0.5);
                }

                Cv2.Merge(bgrChannels, result);
                foreach (var ch in bgrChannels) ch.Dispose();
            }

            return result;
        }
    }
}
