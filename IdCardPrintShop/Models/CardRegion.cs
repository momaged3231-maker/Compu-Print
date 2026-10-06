using System;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public class CardRegion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        [JsonPropertyName("label")]
        public string Label { get; set; } = "بطاقة";

        [JsonPropertyName("sourceImagePath")]
        public string SourceImagePath { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public CardRole Role { get; set; } = CardRole.Unknown;

        /// <summary>
        /// 4 corner points in source image pixel coordinates:
        /// [0]: Top-Left, [1]: Top-Right, [2]: Bottom-Right, [3]: Bottom-Left
        /// </summary>
        [JsonPropertyName("corners")]
        public Point2D[] Corners { get; set; } = new Point2D[4];

        [JsonPropertyName("rotationQuarterTurns")]
        public int RotationQuarterTurns { get; set; } = 0; // 0=0°, 1=90°, 2=180°, 3=270°

        [JsonPropertyName("fineRotationDegrees")]
        public double FineRotationDegrees { get; set; } = 0;

        [JsonPropertyName("safetyMarginPercent")]
        public double SafetyMarginPercent { get; set; } = 1.0;

        [JsonPropertyName("isManualAdjusted")]
        public bool IsManualAdjusted { get; set; } = false;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } = 1.0;

        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; } = string.Empty;

        public double TotalRotationDegrees => (RotationQuarterTurns * 90) + FineRotationDegrees;

        public CardRegion Clone()
        {
            var clonedCorners = new Point2D[Corners.Length];
            Array.Copy(Corners, clonedCorners, Corners.Length);

            return new CardRegion
            {
                Id = Id,
                Label = Label,
                SourceImagePath = SourceImagePath,
                Role = Role,
                Corners = clonedCorners,
                RotationQuarterTurns = RotationQuarterTurns,
                FineRotationDegrees = FineRotationDegrees,
                SafetyMarginPercent = SafetyMarginPercent,
                IsManualAdjusted = IsManualAdjusted,
                Confidence = Confidence,
                StatusMessage = StatusMessage
            };
        }
    }
}
