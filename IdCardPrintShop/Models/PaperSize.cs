using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public enum PaperOrientation
    {
        Portrait = 0,
        Landscape = 1
    }

    public class PaperSize
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "A4";

        [JsonPropertyName("widthMm")]
        public double WidthMm { get; set; } = 210.0;

        [JsonPropertyName("heightMm")]
        public double HeightMm { get; set; } = 297.0;

        public PaperSize() { }

        public PaperSize(string name, double widthMm, double heightMm)
        {
            Name = name;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        public (double Width, double Height) GetDimensions(PaperOrientation orientation)
        {
            return orientation == PaperOrientation.Portrait
                ? (WidthMm, HeightMm)
                : (HeightMm, WidthMm);
        }

        public static readonly PaperSize A4 = new("A4", 210.0, 297.0);
        public static readonly PaperSize A5 = new("A5", 148.0, 210.0);
        public static readonly PaperSize A3 = new("A3", 297.0, 420.0);
        public static readonly PaperSize Letter = new("Letter", 215.9, 279.4);

        public static List<PaperSize> AllStandardSizes => new() { A4, A5, A3, Letter };

        public override string ToString() => $"{Name} ({WidthMm} × {HeightMm} mm)";
    }

    public class CardDimensions
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "بطاقة هوية قياسية (CR80)";

        [JsonPropertyName("widthMm")]
        public double WidthMm { get; set; } = 85.60;

        [JsonPropertyName("heightMm")]
        public double HeightMm { get; set; } = 54.00;

        public CardDimensions() { }

        public CardDimensions(string name, double widthMm, double heightMm)
        {
            Name = name;
            WidthMm = widthMm;
            HeightMm = heightMm;
        }

        public static readonly CardDimensions StandardIdCard = new("بطاقة هوية قياسية (85.6 × 54 mm)", 85.60, 54.00);
        public static readonly CardDimensions PassportDocument = new("جواز سفر - صفحة البيانات (125 × 88 mm)", 125.00, 88.00);
        public static readonly CardDimensions PersonalPhoto4x6 = new("صورة شخصية (40 × 60 mm)", 40.00, 60.00);
        public static readonly CardDimensions PassportPhoto = new("صورة جواز سفر (35 × 45 mm)", 35.00, 45.00);
        public static readonly CardDimensions BusinessCard = new("كارت شخصي (90 × 50 mm)", 90.00, 50.00);

        public static List<CardDimensions> AllStandardDimensions => new()
        {
            StandardIdCard,
            PassportDocument,
            PersonalPhoto4x6,
            PassportPhoto,
            BusinessCard
        };

        public override string ToString() => $"{Name}";
    }
}
