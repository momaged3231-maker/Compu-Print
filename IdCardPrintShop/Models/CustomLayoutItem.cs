using System;
using System.IO;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public class CustomLayoutItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = "عنصر مخصص";

        [JsonPropertyName("widthMm")]
        public decimal WidthMm { get; set; } = 30.0m;

        [JsonPropertyName("heightMm")]
        public decimal HeightMm { get; set; } = 40.0m;

        [JsonPropertyName("copies")]
        public int Copies { get; set; } = 1;

        [JsonPropertyName("rotationAllowed")]
        public bool RotationAllowed { get; set; } = true;

        [JsonPropertyName("spacingMm")]
        public decimal SpacingMm { get; set; } = 2.0m;

        [JsonPropertyName("cutMarks")]
        public bool CutMarks { get; set; } = true;

        [JsonPropertyName("sourceFile")]
        public string SourceFile { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "Ready";

        [JsonIgnore]
        public bool IsMissingSource => !string.IsNullOrEmpty(SourceFile) && !File.Exists(SourceFile);

        [JsonIgnore]
        public string SizeDisplay => $"{WidthMm:G29} × {HeightMm:G29} mm";

        public CustomLayoutItem() { }

        public CustomLayoutItem(string name, decimal widthMm, decimal heightMm, int copies = 1, string sourceFile = "", bool rotationAllowed = true)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name;
            WidthMm = widthMm;
            HeightMm = heightMm;
            Copies = copies;
            SourceFile = sourceFile;
            RotationAllowed = rotationAllowed;
            Status = (!string.IsNullOrEmpty(sourceFile) && !File.Exists(sourceFile)) ? "Needs Review" : "Ready";
        }
    }

    public class CustomSizePreset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("widthMm")]
        public decimal WidthMm { get; set; }

        [JsonPropertyName("heightMm")]
        public decimal HeightMm { get; set; }

        public CustomSizePreset() { }

        public CustomSizePreset(string name, decimal widthMm, decimal heightMm)
        {
            Name = name;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        public static List<CustomSizePreset> DefaultPresets => new()
        {
            new("30 × 40 mm", 30.0m, 40.0m),
            new("40 × 50 mm", 40.0m, 50.0m),
            new("50 × 60 mm", 50.0m, 60.0m),
            new("60 × 90 mm", 60.0m, 90.0m),
            new("80 × 100 mm", 80.0m, 100.0m)
        };

        public override string ToString() => $"{Name} ({WidthMm}×{HeightMm} mm)";
    }
}
