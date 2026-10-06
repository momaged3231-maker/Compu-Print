using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public enum CustomLayoutOrientationMode
    {
        Auto = 0,
        Portrait = 1,
        Landscape = 2
    }

    public class CustomSizeLayoutParameters
    {
        [JsonPropertyName("paperSize")]
        public PaperSize PaperSize { get; set; } = PaperSize.A4;

        [JsonPropertyName("orientationMode")]
        public CustomLayoutOrientationMode OrientationMode { get; set; } = CustomLayoutOrientationMode.Auto;

        [JsonPropertyName("safetyMarginMm")]
        public decimal SafetyMarginMm { get; set; } = 3.0m;

        [JsonPropertyName("spacingMm")]
        public decimal SpacingMm { get; set; } = 2.0m;

        [JsonPropertyName("showCutMarks")]
        public bool ShowCutMarks { get; set; } = true;

        [JsonPropertyName("drawBorderBox")]
        public bool DrawBorderBox { get; set; } = true;

        public CustomSizeLayoutParameters() { }

        public CustomSizeLayoutParameters(PaperSize paperSize, CustomLayoutOrientationMode orientationMode = CustomLayoutOrientationMode.Auto, decimal safetyMarginMm = 3.0m, decimal spacingMm = 2.0m)
        {
            PaperSize = paperSize;
            OrientationMode = orientationMode;
            SafetyMarginMm = safetyMarginMm;
            SpacingMm = spacingMm;
            ShowCutMarks = true;
            DrawBorderBox = true;
        }
    }
}
