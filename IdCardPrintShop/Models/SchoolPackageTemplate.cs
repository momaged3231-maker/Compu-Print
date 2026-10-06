using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;

namespace IdCardPrintShop.Models
{
    public enum StudentNamePosition
    {
        UnderEachPhoto = 0,
        BottomOfSheet = 1,
        None = 2
    }

    public class SchoolPackageItem
    {
        [JsonPropertyName("widthMm")]
        public double WidthMm { get; set; } = 40.0;

        [JsonPropertyName("heightMm")]
        public double HeightMm { get; set; } = 60.0;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 8;

        [JsonPropertyName("rotationAllowed")]
        public bool RotationAllowed { get; set; } = false;

        public SchoolPackageItem() { }

        public SchoolPackageItem(double widthMm, double heightMm, int quantity, bool rotationAllowed = false)
        {
            WidthMm = widthMm;
            HeightMm = heightMm;
            Quantity = quantity;
            RotationAllowed = rotationAllowed;
        }
    }

    public class SchoolPackageTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "tpl-8-4x6";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "8 صور 4×6 سم";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "باقة المدارس القياسية: 8 صور 4×6 سم في ورقة A4 لكل طالب";

        [JsonPropertyName("paperSize")]
        public PaperSize PaperSize { get; set; } = PaperSize.A4;

        [JsonPropertyName("paperOrientation")]
        public PaperOrientation PaperOrientation { get; set; } = PaperOrientation.Portrait;

        [JsonPropertyName("items")]
        public List<SchoolPackageItem> Items { get; set; } = new();

        [JsonPropertyName("marginsMm")]
        public double MarginsMm { get; set; } = 6.0;

        [JsonPropertyName("spacingMm")]
        public double SpacingMm { get; set; } = 3.0;

        [JsonPropertyName("showCutMarks")]
        public bool ShowCutMarks { get; set; } = true;

        [JsonPropertyName("drawBorderBox")]
        public bool DrawBorderBox { get; set; } = true;

        [JsonPropertyName("includeStudentName")]
        public bool IncludeStudentName { get; set; } = true;

        [JsonPropertyName("namePosition")]
        public StudentNamePosition NamePosition { get; set; } = StudentNamePosition.UnderEachPhoto;

        [JsonPropertyName("nameFontSizePt")]
        public double NameFontSizePt { get; set; } = 8.5;

        [JsonPropertyName("nameFontFamily")]
        public string NameFontFamily { get; set; } = "Arial";

        [JsonPropertyName("nameAlignment")]
        public TextAlignment NameAlignment { get; set; } = TextAlignment.Center;

        public SchoolPackageTemplate() { }

        public static List<SchoolPackageTemplate> DefaultTemplates => new()
        {
            new SchoolPackageTemplate
            {
                Id = "tpl-8-4x6",
                Name = "8 صور 4×6 سم",
                Description = "8 صور مقاس 40×60 مم لكل طالب مع شريط الاسم أسفل الصور",
                PaperSize = PaperSize.A4,
                PaperOrientation = PaperOrientation.Portrait,
                MarginsMm = 8.0,
                SpacingMm = 3.0,
                Items = new List<SchoolPackageItem> { new SchoolPackageItem(40.0, 60.0, 8) }
            },
            new SchoolPackageTemplate
            {
                Id = "tpl-6-4x6",
                Name = "6 صور 4×6 سم",
                Description = "6 صور مقاس 40×60 مم مريحة على ورقة A4 مع مساحات أوسع",
                PaperSize = PaperSize.A4,
                PaperOrientation = PaperOrientation.Portrait,
                MarginsMm = 10.0,
                SpacingMm = 4.0,
                Items = new List<SchoolPackageItem> { new SchoolPackageItem(40.0, 60.0, 6) }
            },
            new SchoolPackageTemplate
            {
                Id = "tpl-8-35x45",
                Name = "8 صور 3.5×4.5 سم",
                Description = "8 صور مقاس شهادات وجوازات 35×45 مم لكل طالب",
                PaperSize = PaperSize.A4,
                PaperOrientation = PaperOrientation.Portrait,
                MarginsMm = 10.0,
                SpacingMm = 4.0,
                Items = new List<SchoolPackageItem> { new SchoolPackageItem(35.0, 45.0, 8) }
            },
            new SchoolPackageTemplate
            {
                Id = "tpl-4-5x7",
                Name = "4 صور 5×7 سم",
                Description = "4 صور مقاس كبير 50×70 مم للباقات المميزة والتكريم",
                PaperSize = PaperSize.A4,
                PaperOrientation = PaperOrientation.Portrait,
                MarginsMm = 12.0,
                SpacingMm = 6.0,
                Items = new List<SchoolPackageItem> { new SchoolPackageItem(50.0, 70.0, 4) }
            },
            new SchoolPackageTemplate
            {
                Id = "tpl-custom",
                Name = "باقة مخصصة (Custom)",
                Description = "تحديد أبعاد وكميات الصور حسب متطلبات المدرسة أو الحضانة",
                PaperSize = PaperSize.A4,
                PaperOrientation = PaperOrientation.Portrait,
                MarginsMm = 8.0,
                SpacingMm = 3.0,
                Items = new List<SchoolPackageItem> { new SchoolPackageItem(40.0, 60.0, 8) }
            }
        };

        public override string ToString() => Name;
    }
}
