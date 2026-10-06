using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using OpenCvSharp;
using PdfSharp.Pdf.IO;

namespace IdCardPrintShop.Services
{
    public interface IDocumentInspectionService
    {
        Task<DocumentItem> InspectFileAsync(string filePath);
        Task<List<DocumentItem>> InspectFilesAsync(IEnumerable<string> filePaths);
    }

    public class DocumentInspectionService : IDocumentInspectionService
    {
        private readonly IDocxConversionService _docxService;

        public DocumentInspectionService(IDocxConversionService? docxService = null)
        {
            _docxService = docxService ?? new DocxConversionService();
        }

        public async Task<List<DocumentItem>> InspectFilesAsync(IEnumerable<string> filePaths)
        {
            var list = new List<DocumentItem>();
            foreach (var path in filePaths)
            {
                var doc = await InspectFileAsync(path);
                list.Add(doc);
            }
            return list;
        }

        public async Task<DocumentItem> InspectFileAsync(string filePath)
        {
            return await Task.Run(async () =>
            {
                var item = new DocumentItem
                {
                    OriginalPath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Copies = 1,
                    PaperSizeName = "A4",
                    PrintMode = PrintColorMode.BlackAndWhite,
                    Orientation = DocumentOrientationMode.Auto,
                    Scaling = DocumentScalingMode.FitToPage,
                    PageRange = "All"
                };

                if (!File.Exists(filePath))
                {
                    item.Status = DocumentItemStatus.Failed;
                    item.StatusMessage = "الملف غير موجود في المسار المحدد.";
                    return item;
                }

                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                switch (ext)
                {
                    case ".pdf":
                        item.FileType = DocumentFileType.Pdf;
                        InspectPdf(item, filePath);
                        break;

                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".bmp":
                    case ".webp":
                        item.FileType = DocumentFileType.Image;
                        InspectImage(item, filePath);
                        break;

                    case ".docx":
                    case ".doc":
                        item.FileType = DocumentFileType.Docx;
                        await InspectDocxAsync(item, filePath);
                        break;

                    default:
                        item.FileType = DocumentFileType.Unknown;
                        item.Status = DocumentItemStatus.Failed;
                        item.StatusMessage = $"نوع الملف غير مدعوم ({ext}). يدعم النظام PDF، Word DOCX، والصور.";
                        break;
                }

                return item;
            });
        }

        private static void InspectPdf(DocumentItem item, string filePath)
        {
            try
            {
                using var pdf = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
                item.PageCount = Math.Max(1, pdf.PageCount);
                item.ResolvedPageIndices = Enumerable.Range(0, item.PageCount).ToList();

                if (pdf.PageCount > 0)
                {
                    var page = pdf.Pages[0];
                    bool isLandscape = page.Width > page.Height;
                    item.Orientation = isLandscape ? DocumentOrientationMode.Landscape : DocumentOrientationMode.Portrait;

                    // Automatically select A3 if page dimensions are clearly A3 (>350mm or >1000pt)
                    if (page.Width.Point > 950 || page.Height.Point > 950)
                    {
                        item.PaperSizeName = "A3";
                    }
                    else if (page.Width.Point < 450 && page.Height.Point < 620)
                    {
                        item.PaperSizeName = "A5";
                    }
                    else
                    {
                        item.PaperSizeName = "A4";
                    }
                }

                item.Status = DocumentItemStatus.Ready;
                item.StatusMessage = $"مستند PDF ({item.PageCount} صفحة)";
            }
            catch (Exception ex)
            {
                item.Status = DocumentItemStatus.Failed;
                item.StatusMessage = $"فشل فتح ملف الـ PDF: {ex.Message}";
                item.PageCount = 1;
                item.ResolvedPageIndices = new List<int> { 0 };
            }
        }

        private static void InspectImage(DocumentItem item, string filePath)
        {
            try
            {
                // Inspect resolution / dimensions with OpenCV without keeping full buffer
                using var img = Cv2.ImRead(filePath, ImreadModes.Color);
                if (img.Empty())
                {
                    item.Status = DocumentItemStatus.Failed;
                    item.StatusMessage = "تعذر قراءة ملف الصورة.";
                    return;
                }

                item.PageCount = 1;
                item.ResolvedPageIndices = new List<int> { 0 };

                bool isLandscape = img.Width > img.Height;
                item.Orientation = isLandscape ? DocumentOrientationMode.Landscape : DocumentOrientationMode.Portrait;
                item.PaperSizeName = "A4";
                item.PrintMode = PrintColorMode.Color; // Default photos to color

                item.Status = DocumentItemStatus.Ready;
                item.StatusMessage = $"صورة ({img.Width} × {img.Height} px)";
            }
            catch (Exception ex)
            {
                item.Status = DocumentItemStatus.Failed;
                item.StatusMessage = $"فشل قراءة الصورة: {ex.Message}";
            }
        }

        private async Task InspectDocxAsync(DocumentItem item, string filePath)
        {
            if (!_docxService.IsWordInstalled)
            {
                item.Status = DocumentItemStatus.NeedsReview;
                item.StatusMessage = "يتطلب تحويل ملفات Word وجود Microsoft Word مثبت على هذا الجهاز. يرجى تثبيت Word أو تصدير المستند كـ PDF أولاً.";
                item.PageCount = 1;
                item.ResolvedPageIndices = new List<int> { 0 };
                return;
            }

            item.Status = DocumentItemStatus.Processing;
            item.StatusMessage = "جاري فحص وتحويل ملف Word عبر Microsoft Word...";

            var conv = await _docxService.ConvertDocxToPdfAsync(filePath);
            if (conv.Success && !string.IsNullOrEmpty(conv.OutputPdfPath))
            {
                item.ConvertedPdfPath = conv.OutputPdfPath;
                item.PageCount = Math.Max(1, conv.PageCount);
                item.ResolvedPageIndices = Enumerable.Range(0, item.PageCount).ToList();
                item.Status = DocumentItemStatus.Ready;
                item.StatusMessage = $"مستند Word تم تحويله للطباعة ({item.PageCount} صفحة)";
            }
            else
            {
                item.Status = DocumentItemStatus.NeedsReview;
                item.StatusMessage = conv.ErrorMessage;
                item.PageCount = 1;
                item.ResolvedPageIndices = new List<int> { 0 };
            }
        }
    }
}
