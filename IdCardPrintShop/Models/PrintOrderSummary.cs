using System.Collections.Generic;

namespace IdCardPrintShop.Models
{
    public class PrintOrderSummary
    {
        public int TotalFiles { get; set; }
        public int TotalInputPages { get; set; }
        public int TotalOutputPages { get; set; }
        public int TotalCopies { get; set; }

        public int A4_BwCount { get; set; }
        public int A4_ColorCount { get; set; }

        public int A5_BwCount { get; set; }
        public int A5_ColorCount { get; set; }

        public int A3_BwCount { get; set; }
        public int A3_ColorCount { get; set; }

        public static PrintOrderSummary Compute(IEnumerable<DocumentItem> documents)
        {
            var summary = new PrintOrderSummary();

            foreach (var doc in documents)
            {
                if (doc.Status == DocumentItemStatus.Failed) continue;

                summary.TotalFiles++;
                summary.TotalInputPages += doc.PageCount;
                summary.TotalCopies += doc.Copies;

                int pageCountForDoc = doc.ResolvedPageIndices.Count > 0 ? doc.ResolvedPageIndices.Count : doc.PageCount;
                int outputPagesForDoc = pageCountForDoc * doc.Copies;
                summary.TotalOutputPages += outputPagesForDoc;

                string paper = doc.PaperSizeName.ToUpperInvariant();
                bool isBw = doc.PrintMode == PrintColorMode.BlackAndWhite;

                switch (paper)
                {
                    case "A3":
                        if (isBw) summary.A3_BwCount += outputPagesForDoc;
                        else summary.A3_ColorCount += outputPagesForDoc;
                        break;
                    case "A5":
                        if (isBw) summary.A5_BwCount += outputPagesForDoc;
                        else summary.A5_ColorCount += outputPagesForDoc;
                        break;
                    case "A4":
                    default:
                        if (isBw) summary.A4_BwCount += outputPagesForDoc;
                        else summary.A4_ColorCount += outputPagesForDoc;
                        break;
                }
            }

            return summary;
        }

        public override string ToString()
        {
            return $"الملفات: {TotalFiles} | الصفحات الإجمالية: {TotalOutputPages} | A4 أبيض/أسود: {A4_BwCount} | A4 ألوان: {A4_ColorCount} | A3: {A3_BwCount + A3_ColorCount} | A5: {A5_BwCount + A5_ColorCount}";
        }
    }
}
