using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public enum LayoutMode
    {
        FrontBackVertical = 0,   // وجه ثم ظهر فوق بعض (رأسي)
        FrontBackHorizontal = 1, // وجه وظَهر بجوار بعض (أفقي)
        GridCopies = 2,          // تكرار شبكي تلقائي للنسخ
        PairsGrid = 3            // تكرار أزواج (وجه + ظهر) في شبكة
    }

    public class LayoutTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "a4_vertical";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "A4 - وجه وظهر رأسي (الافتراضي)";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "صفحة A4 تحتوي على الوجه والظهر في المنتصف";

        [JsonPropertyName("mode")]
        public LayoutMode Mode { get; set; } = LayoutMode.FrontBackVertical;

        [JsonPropertyName("paperSize")]
        public PaperSize PaperSize { get; set; } = PaperSize.A4;

        [JsonPropertyName("orientation")]
        public PaperOrientation Orientation { get; set; } = PaperOrientation.Portrait;

        [JsonPropertyName("cardDimensions")]
        public CardDimensions CardDimensions { get; set; } = CardDimensions.StandardIdCard;

        [JsonPropertyName("marginTopMm")]
        public double MarginTopMm { get; set; } = 15.0;

        [JsonPropertyName("marginBottomMm")]
        public double MarginBottomMm { get; set; } = 15.0;

        [JsonPropertyName("marginLeftMm")]
        public double MarginLeftMm { get; set; } = 15.0;

        [JsonPropertyName("marginRightMm")]
        public double MarginRightMm { get; set; } = 15.0;

        [JsonPropertyName("spacingX_Mm")]
        public double SpacingX_Mm { get; set; } = 8.0;

        [JsonPropertyName("spacingY_Mm")]
        public double SpacingY_Mm { get; set; } = 12.0;

        [JsonPropertyName("showCutMarks")]
        public bool ShowCutMarks { get; set; } = true;

        [JsonPropertyName("drawBorderBox")]
        public bool DrawBorderBox { get; set; } = true;

        public static List<LayoutTemplate> GetDefaultTemplates()
        {
            return new List<LayoutTemplate>
            {
                new()
                {
                    Id = "a4_vertical",
                    Name = "A4 - وجه وظهر رأسي (الأكثر استخداماً)",
                    Description = "توزيع الوجه والظهر فوق بعضهما في منتصف صفحة A4",
                    Mode = LayoutMode.FrontBackVertical,
                    PaperSize = PaperSize.A4,
                    Orientation = PaperOrientation.Portrait,
                    SpacingY_Mm = 14.0,
                    ShowCutMarks = true
                },
                new()
                {
                    Id = "a4_horizontal",
                    Name = "A4 - وجه وظهر أفقي",
                    Description = "الوجه والظهر متجاوران بالعرض",
                    Mode = LayoutMode.FrontBackHorizontal,
                    PaperSize = PaperSize.A4,
                    Orientation = PaperOrientation.Portrait,
                    SpacingX_Mm = 10.0,
                    ShowCutMarks = true
                },
                new()
                {
                    Id = "a5_vertical",
                    Name = "A5 - وجه وظهر رأسي (ورق نصف A4)",
                    Description = "صفحة A5 مخصصة للطباعة الاقتصادية للعميل الواحد",
                    Mode = LayoutMode.FrontBackVertical,
                    PaperSize = PaperSize.A5,
                    Orientation = PaperOrientation.Portrait,
                    SpacingY_Mm = 10.0,
                    ShowCutMarks = true
                },
                new()
                {
                    Id = "a4_grid_copies",
                    Name = "A4 - تكرار شبكي متعدد (نسخ متعددة)",
                    Description = "ملء صفحة A4 بأكبر عدد ممكن من النسخ تلقائياً",
                    Mode = LayoutMode.GridCopies,
                    PaperSize = PaperSize.A4,
                    Orientation = PaperOrientation.Portrait,
                    SpacingX_Mm = 8.0,
                    SpacingY_Mm = 8.0,
                    ShowCutMarks = true
                },
                new()
                {
                    Id = "a4_pairs_grid",
                    Name = "A4 - تكرار أزواج (وجه + ظهر)",
                    Description = "طباعة عدة أزواج وجه وظهر في صفحة واحدة",
                    Mode = LayoutMode.PairsGrid,
                    PaperSize = PaperSize.A4,
                    Orientation = PaperOrientation.Portrait,
                    SpacingX_Mm = 8.0,
                    SpacingY_Mm = 10.0,
                    ShowCutMarks = true
                }
            };
        }

        public override string ToString() => Name;
    }
}
