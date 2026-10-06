using System;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public struct RectD : IEquatable<RectD>
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        public RectD(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Equals(RectD other) =>
            Math.Abs(X - other.X) < 0.01 &&
            Math.Abs(Y - other.Y) < 0.01 &&
            Math.Abs(Width - other.Width) < 0.01 &&
            Math.Abs(Height - other.Height) < 0.01;

        public override bool Equals(object? obj) => obj is RectD other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public override string ToString() => $"[X={X:F0}, Y={Y:F0}, W={Width:F0}, H={Height:F0}]";
    }

    public enum PhotoBackgroundType
    {
        PureWhite = 0,
        LightBlue = 1,
        LightGray = 2,
        Transparent = 3,
        Original = 4
    }

    public class PersonalPhotoParameters
    {
        [JsonPropertyName("cropBox")]
        public RectD CropBox { get; set; }

        [JsonPropertyName("faceBox")]
        public RectD FaceBox { get; set; }

        [JsonPropertyName("headCenterX")]
        public double HeadCenterX { get; set; }

        [JsonPropertyName("headCenterY")]
        public double HeadCenterY { get; set; }

        [JsonPropertyName("headHeight")]
        public double HeadHeight { get; set; }

        [JsonPropertyName("backgroundType")]
        public PhotoBackgroundType BackgroundType { get; set; } = PhotoBackgroundType.PureWhite;

        [JsonPropertyName("bgRed")]
        public byte BgRed { get; set; } = 255;

        [JsonPropertyName("bgGreen")]
        public byte BgGreen { get; set; } = 255;

        [JsonPropertyName("bgBlue")]
        public byte BgBlue { get; set; } = 255;

        [JsonPropertyName("removeBackground")]
        public bool RemoveBackground { get; set; } = true;

        [JsonPropertyName("edgeFeatherRadius")]
        public double EdgeFeatherRadius { get; set; } = 2.0;

        // Color adjustments
        [JsonPropertyName("brightness")]
        public double Brightness { get; set; } = 0.0; // -50 to +50

        [JsonPropertyName("contrast")]
        public double Contrast { get; set; } = 0.0;   // -50 to +50

        [JsonPropertyName("saturation")]
        public double Saturation { get; set; } = 0.0; // -50 to +50

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.0;// -30 to +30 (cool/warm)

        [JsonPropertyName("rotationQuarterTurns")]
        public int RotationQuarterTurns { get; set; } = 0;

        [JsonPropertyName("autoEnhance")]
        public bool AutoEnhance { get; set; } = true;

        [JsonPropertyName("isManualAdjusted")]
        public bool IsManualAdjusted { get; set; } = false;

        public PersonalPhotoParameters Clone()
        {
            return new PersonalPhotoParameters
            {
                CropBox = CropBox,
                FaceBox = FaceBox,
                HeadCenterX = HeadCenterX,
                HeadCenterY = HeadCenterY,
                HeadHeight = HeadHeight,
                BackgroundType = BackgroundType,
                BgRed = BgRed,
                BgGreen = BgGreen,
                BgBlue = BgBlue,
                RemoveBackground = RemoveBackground,
                EdgeFeatherRadius = EdgeFeatherRadius,
                Brightness = Brightness,
                Contrast = Contrast,
                Saturation = Saturation,
                Temperature = Temperature,
                RotationQuarterTurns = RotationQuarterTurns,
                AutoEnhance = AutoEnhance,
                IsManualAdjusted = IsManualAdjusted
            };
        }
    }
}
