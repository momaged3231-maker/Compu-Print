using System;
using System.IO;
using System.Threading.Tasks;

namespace IdCardPrintShop.Services
{
    public class DocxConversionResult
    {
        public bool Success { get; set; }
        public string? OutputPdfPath { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public int PageCount { get; set; } = 1;
    }

    public interface IDocxConversionService
    {
        bool IsWordInstalled { get; }
        Task<DocxConversionResult> ConvertDocxToPdfAsync(string docxPath, string? targetPdfPath = null);
    }

    public class DocxConversionService : IDocxConversionService
    {
        public bool IsWordInstalled
        {
            get
            {
                try
                {
                    var wordType = Type.GetTypeFromProgID("Word.Application");
                    return wordType != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<DocxConversionResult> ConvertDocxToPdfAsync(string docxPath, string? targetPdfPath = null)
        {
            return await Task.Run(() =>
            {
                var result = new DocxConversionResult();

                if (!File.Exists(docxPath))
                {
                    result.Success = false;
                    result.ErrorMessage = $"الملف غير موجود: {docxPath}";
                    return result;
                }

                if (!IsWordInstalled)
                {
                    result.Success = false;
                    result.ErrorMessage = "يتطلب تحويل ملفات Word DOCX تثبيت برنامج Microsoft Word على هذا الجهاز. يرجى تثبيت Word أو تصدير المستند كـ PDF أولاً من جهاز العميل.";
                    return result;
                }

                targetPdfPath ??= Path.ChangeExtension(Path.GetTempFileName(), ".pdf");

                dynamic? wordApp = null;
                dynamic? doc = null;

                try
                {
                    var wordType = Type.GetTypeFromProgID("Word.Application");
                    if (wordType == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = "تعذر العثور على محرك Word.Application في النظام.";
                        return result;
                    }

                    wordApp = Activator.CreateInstance(wordType);
                    if (wordApp == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = "تعذر تشغيل تطبيق Microsoft Word.";
                        return result;
                    }
                    wordApp.Visible = false;
                    wordApp.DisplayAlerts = 0; // wdAlertsNone

                    doc = wordApp.Documents.Open(docxPath, ReadOnly: true, Visible: false);

                    // 17 = wdExportFormatPDF
                    doc.ExportAsFixedFormat(targetPdfPath, 17);

                    result.Success = true;
                    result.OutputPdfPath = targetPdfPath;

                    try
                    {
                        // Get page count using doc pages
                        result.PageCount = (int)doc.ComputeStatistics(2 /* wdStatisticPages */);
                    }
                    catch
                    {
                        result.PageCount = 1;
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"فشل تحويل ملف Word إلى PDF: {ex.Message}";
                    return result;
                }
                finally
                {
                    try
                    {
                        doc?.Close(false);
                    }
                    catch { }

                    try
                    {
                        wordApp?.Quit();
                    }
                    catch { }
                }
            });
        }
    }
}
