namespace IdCardPrintShop.Models
{
    public enum CardDocumentType
    {
        NationalId = 0,    // بطاقة رقم قومي / هوية شخصية (CR80: 85.6 × 54 mm)
        Passport = 1,      // جواز سفر - صفحة البيانات (ID-3: 125 × 88 mm)
        Custom = 2         // مخصص
    }

    public static class CardDocumentTypeExtensions
    {
        public static string ToArabicLabel(this CardDocumentType docType) => docType switch
        {
            CardDocumentType.NationalId => "بطاقة هوية / رقم قومي",
            CardDocumentType.Passport => "جواز سفر",
            CardDocumentType.Custom => "مستند مخصص",
            _ => "بطاقة هوية"
        };

        public static double GetTargetAspectRatio(this CardDocumentType docType) => docType switch
        {
            CardDocumentType.Passport => 125.00 / 88.00, // ~1.4205
            _ => 85.60 / 54.00                           // ~1.5852
        };

        public static CardDimensions GetDefaultDimensions(this CardDocumentType docType) => docType switch
        {
            CardDocumentType.Passport => CardDimensions.PassportDocument,
            _ => CardDimensions.StandardIdCard
        };
    }
}
