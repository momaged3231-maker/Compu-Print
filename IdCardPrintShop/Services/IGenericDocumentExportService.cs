using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using OpenCvSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace IdCardPrintShop.Services
{
    public class GenericExportResult
    {
        public bool Success { get; set; }
        public string OutputPdfPath { get; set; } = string.Empty;
        public int TotalPagesProduced { get; set; }
        public PrintOrderSummary Summary { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public interface IGenericDocumentExportService
    {
        Task<GenericExportResult> ExportCombinedPdfAsync(
            IEnumerable<DocumentItem> documents,
            string outputPath,
            JobOrder? orderInfo = null);
    }

    public class GenericDocumentExportService : IGenericDocumentExportService
    {
        private const double MmToPoint = 72.0 / 25.4;

        public async Task<GenericExportResult> ExportCombinedPdfAsync(
            IEnumerable<DocumentItem> documents,
            string outputPath,
            JobOrder? orderInfo = null)
        {
            return await Task.Run(() =>
            {
                var result = new GenericExportResult
                {
                    OutputPdfPath = outputPath
                };

                try
                {
                    var docList = new List<DocumentItem>(documents);
                    if (docList.Count == 0)
                    {
                        result.Success = false;
                        result.ErrorMessage = "لا توجد مستندات في قائمة الطباعة.";
                        return result;
                    }

                    using var outputDoc = new PdfDocument();
                    outputDoc.Info.Title = $"مستندات مجمعة للطباعة - {orderInfo?.OrderNumber ?? "طلب جديد"}";
                    outputDoc.Info.Author = "Smart Print Shop Pro";
                    outputDoc.Info.Subject = orderInfo?.Notes ?? string.Empty;

                    int totalPages = 0;

                    foreach (var item in docList)
                    {
                        if (item.Status == DocumentItemStatus.Failed) continue;

                        int copies = Math.Max(1, item.Copies);
                        var targetPaper = item.GetPaperSize();

                        for (int c = 0; c < copies; c++)
                        {
                            if (item.FileType == DocumentFileType.Pdf ||
                                (item.FileType == DocumentFileType.Docx && !string.IsNullOrEmpty(item.ConvertedPdfPath)))
                            {
                                string sourcePdf = item.FileType == DocumentFileType.Docx
                                    ? item.ConvertedPdfPath!
                                    : item.OriginalPath;

                                totalPages += ProcessPdfItem(outputDoc, item, sourcePdf, targetPaper);
                            }
                            else if (item.FileType == DocumentFileType.Image)
                            {
                                totalPages += ProcessImageItem(outputDoc, item, targetPaper);
                            }
                        }
                    }

                    if (totalPages == 0)
                    {
                        result.Success = false;
                        result.ErrorMessage = "لم يتم إنتاج أي صفحات صالحة للطباعة من الملفات المحددة.";
                        return result;
                    }

                    // Save output document
                    outputDoc.Save(outputPath);

                    result.Success = true;
                    result.TotalPagesProduced = totalPages;
                    result.Summary = PrintOrderSummary.Compute(docList);

                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"حدث خطأ أثناء تصدير ملف الطباعة: {ex.Message}";
                    return result;
                }
            });
        }

        private static int ProcessPdfItem(
            PdfDocument outputDoc,
            DocumentItem item,
            string pdfPath,
            PaperSize targetPaper)
        {
            if (!File.Exists(pdfPath)) return 0;

            int pagesAdded = 0;

            try
            {
                using var inputPdf = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
                int totalInPdf = inputPdf.PageCount;

                var pagesToPrint = item.ResolvedPageIndices.Count > 0
                    ? item.ResolvedPageIndices
                    : PageRangeParser.Parse(item.PageRange, totalInPdf);

                foreach (int pageIndex in pagesToPrint)
                {
                    if (pageIndex < 0 || pageIndex >= totalInPdf) continue;

                    var sourcePage = inputPdf.Pages[pageIndex];
                    var sourceOrientation = sourcePage.Width > sourcePage.Height
                        ? PaperOrientation.Landscape
                        : PaperOrientation.Portrait;

                    var effectiveOrientation = item.ResolveEffectiveOrientation(sourceOrientation);
                    var (paperWMm, paperHMm) = targetPaper.GetDimensions(effectiveOrientation);

                    double targetWidthPt = paperWMm * MmToPoint;
                    double targetHeightPt = paperHMm * MmToPoint;

                    var targetPage = outputDoc.AddPage();
                    targetPage.Width = XUnit.FromPoint(targetWidthPt);
                    targetPage.Height = XUnit.FromPoint(targetHeightPt);

                    using var gfx = XGraphics.FromPdfPage(targetPage);

                    using var form = XPdfForm.FromFile(pdfPath);
                    form.PageIndex = pageIndex;

                    double marginPt = 14.17; // ~5mm default printable margin
                    double availW = Math.Max(50, targetWidthPt - (marginPt * 2));
                    double availH = Math.Max(50, targetHeightPt - (marginPt * 2));

                    double formW = form.PointWidth;
                    double formH = form.PointHeight;

                    double scale = 1.0;
                    if (item.Scaling == DocumentScalingMode.FitToPage)
                    {
                        scale = Math.Min(availW / formW, availH / formH);
                    }
                    else if (item.Scaling == DocumentScalingMode.Fill)
                    {
                        scale = Math.Max(targetWidthPt / formW, targetHeightPt / formH);
                    }

                    double drawW = formW * scale;
                    double drawH = formH * scale;
                    double drawX = (targetWidthPt - drawW) / 2.0;
                    double drawY = (targetHeightPt - drawH) / 2.0;

                    gfx.DrawImage(form, drawX, drawY, drawW, drawH);
                    pagesAdded++;
                }
            }
            catch
            {
                // Graceful fallback: continue with remaining files
            }

            return pagesAdded;
        }

        private static int ProcessImageItem(
            PdfDocument outputDoc,
            DocumentItem item,
            PaperSize targetPaper)
        {
            if (!File.Exists(item.OriginalPath)) return 0;

            try
            {
                // Check if B&W conversion requested for image
                string sourceToDraw = item.OriginalPath;
                string? tempBwFile = null;

                if (item.PrintMode == PrintColorMode.BlackAndWhite)
                {
                    try
                    {
                        using var mat = Cv2.ImRead(item.OriginalPath, ImreadModes.Grayscale);
                        if (!mat.Empty())
                        {
                            tempBwFile = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                            Cv2.ImWrite(tempBwFile, mat);
                            sourceToDraw = tempBwFile;
                        }
                    }
                    catch { }
                }

                using var xImg = XImage.FromFile(sourceToDraw);

                var imgOrientation = xImg.PointWidth > xImg.PointHeight
                    ? PaperOrientation.Landscape
                    : PaperOrientation.Portrait;

                var effectiveOrientation = item.ResolveEffectiveOrientation(imgOrientation);
                var (paperWMm, paperHMm) = targetPaper.GetDimensions(effectiveOrientation);

                double targetWidthPt = paperWMm * MmToPoint;
                double targetHeightPt = paperHMm * MmToPoint;

                var targetPage = outputDoc.AddPage();
                targetPage.Width = XUnit.FromPoint(targetWidthPt);
                targetPage.Height = XUnit.FromPoint(targetHeightPt);

                using var gfx = XGraphics.FromPdfPage(targetPage);

                double marginPt = 14.17; // ~5mm
                double availW = Math.Max(50, targetWidthPt - (marginPt * 2));
                double availH = Math.Max(50, targetHeightPt - (marginPt * 2));

                double imgPtW = xImg.PointWidth;
                double imgPtH = xImg.PointHeight;

                double scale = 1.0;
                if (item.Scaling == DocumentScalingMode.FitToPage)
                {
                    scale = Math.Min(availW / imgPtW, availH / imgPtH);
                }
                else if (item.Scaling == DocumentScalingMode.Fill)
                {
                    scale = Math.Max(targetWidthPt / imgPtW, targetHeightPt / imgPtH);
                }

                double drawW = imgPtW * scale;
                double drawH = imgPtH * scale;
                double drawX = (targetWidthPt - drawW) / 2.0;
                double drawY = (targetHeightPt - drawH) / 2.0;

                gfx.DrawImage(xImg, drawX, drawY, drawW, drawH);

                if (tempBwFile != null && File.Exists(tempBwFile))
                {
                    try { File.Delete(tempBwFile); } catch { }
                }

                return 1;
            }
            catch
            {
                return 0;
            }
        }
    }
}
