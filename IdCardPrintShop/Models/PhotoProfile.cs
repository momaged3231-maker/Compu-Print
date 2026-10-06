using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public class PhotoProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "personal_40x60";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "صورة شخصية (40 × 60 mm)";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "المقاس القياسي للصور الشخصية واستمارات المعاملات الرسمية";

        [JsonPropertyName("widthMm")]
        public double WidthMm { get; set; } = 40.0;

        [JsonPropertyName("heightMm")]
        public double HeightMm { get; set; } = 60.0;

        [JsonPropertyName("targetDpi")]
        public double TargetDpi { get; set; } = 300.0;

        /// <summary>
        /// Percentage of total photo height occupied by the head (chin to hair top)
        /// </summary>
        [JsonPropertyName("headHeightPercent")]
        public double HeadHeightPercent { get; set; } = 0.55; // 55%

        /// <summary>
        /// Margin from top of photo frame to top of head / hair
        /// </summary>
        [JsonPropertyName("topMarginPercent")]
        public double TopMarginPercent { get; set; } = 0.12; // 12% headroom

        [JsonPropertyName("recommendedCopies")]
        public int RecommendedCopies { get; set; } = 8;

        [JsonPropertyName("defaultPaperSize")]
        public string DefaultPaperSize { get; set; } = "A4";

        public double AspectRatio => WidthMm / HeightMm;

        public static readonly PhotoProfile Standard4x6 = new()
        {
            Id = "personal_40x60",
            Name = "صورة شخصية (40 × 60 mm)",
            Description = "المقاس الأكثر طلباً في محلات التصوير للبطاقات والاستمارات",
            WidthMm = 40.0,
            HeightMm = 60.0,
            HeadHeightPercent = 0.55,
            TopMarginPercent = 0.12,
            RecommendedCopies = 8
        };

        public static readonly PhotoProfile Small3x4 = new()
        {
            Id = "personal_30x40",
            Name = "صورة شخصية صغيرة (30 × 40 mm)",
            Description = "صورة كارنيهات الطلاب والنوادي ورخص القيادة",
            WidthMm = 30.0,
            HeightMm = 40.0,
            HeadHeightPercent = 0.60,
            TopMarginPercent = 0.10,
            RecommendedCopies = 12
        };

        public static readonly PhotoProfile Square5x5 = new()
        {
            Id = "personal_50x50",
            Name = "صورة مربعة (50 × 50 mm)",
            Description = "صورة مقاس 5x5 سم",
            WidthMm = 50.0,
            HeightMm = 50.0,
            HeadHeightPercent = 0.55,
            TopMarginPercent = 0.12,
            RecommendedCopies = 6
        };

        public static List<PhotoProfile> DefaultProfiles => new()
        {
            Standard4x6,
            Small3x4,
            Square5x5
        };

        public override string ToString() => $"{Name} ({WidthMm}×{HeightMm} mm)";
    }
}
