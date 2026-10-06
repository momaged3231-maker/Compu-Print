using System.Collections.Generic;

namespace IdCardPrintShop.Models
{
    public class LayoutItem
    {
        public int PageIndex { get; set; } = 0;
        public double X_Mm { get; set; }
        public double Y_Mm { get; set; }
        public double Width_Mm { get; set; }
        public double Height_Mm { get; set; }
        public CardRole Role { get; set; }
        public string CardRegionId { get; set; } = string.Empty;
        public int CopyIndex { get; set; } = 1;
        public CardRegion? Region { get; set; }
        public int RotationDegrees { get; set; } = 0;
        public string SourceImagePath { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string CustomItemId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public bool IncludeNameLabel { get; set; } = false;
        public double NameFontSizePt { get; set; } = 8.5;
    }

    public class LayoutPlan
    {
        public PaperSize PaperSize { get; set; } = PaperSize.A4;
        public PaperOrientation Orientation { get; set; } = PaperOrientation.Portrait;
        public int TotalPages { get; set; } = 1;
        public List<LayoutItem> Items { get; set; } = new();
        public bool ShowCutMarks { get; set; } = true;
        public bool DrawBorderBox { get; set; } = true;

        public (double WidthMm, double HeightMm) SheetDimensions => PaperSize.GetDimensions(Orientation);

        public List<LayoutItem> GetItemsForPage(int pageIndex)
        {
            return Items.FindAll(item => item.PageIndex == pageIndex);
        }
    }
}
