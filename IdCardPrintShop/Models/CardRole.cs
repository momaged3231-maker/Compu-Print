namespace IdCardPrintShop.Models
{
    public enum CardRole
    {
        Unknown = 0,
        Front = 1,
        Back = 2,
        Single = 3
    }

    public static class CardRoleExtensions
    {
        public static string ToArabicLabel(this CardRole role) => role switch
        {
            CardRole.Front => "الوجه الأمامي (Front)",
            CardRole.Back => "الوجه الخلفي (Back)",
            CardRole.Single => "بطاقة مفردة",
            _ => "غير محدد"
        };
    }
}
