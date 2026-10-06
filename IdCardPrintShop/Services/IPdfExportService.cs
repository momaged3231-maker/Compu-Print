using System.Collections.Generic;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public interface IPdfExportService
    {
        Task<string> ExportPdfAsync(
            LayoutPlan plan,
            Dictionary<string, Mat> rectifiedCardMats,
            string outputPath,
            JobOrder? orderInfo = null);
    }
}
