using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using OpenCvSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace IdCardPrintShop.Services
{
    public class SchoolOrderEngine : ISchoolOrderEngine
    {
        static SchoolOrderEngine()
        {
            WindowsFontResolver.EnsureRegistered();
        }

        private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".bmp"
        };

        private readonly HashSet<string> _unsupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".doc", ".txt", ".zip", ".rar", ".7z", ".ini", ".db", ".lnk", ".exe"
        };

        public Task<ScanFolderResult> ScanFolderAsync(
            string folderPath,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var result = new ScanFolderResult
                {
                    SourceFolder = folderPath
                };

                if (!Directory.Exists(folderPath))
                {
                    throw new DirectoryNotFoundException($"المجلد غير موجود: {folderPath}");
                }

                // Get all files (top directory only or shallow, ignore system/hidden)
                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        var name = Path.GetFileName(f);
                        return !name.StartsWith(".") && !name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                result.TotalFiles = files.Count;
                int current = 0;

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    current++;
                    progress?.Report((int)((double)current / files.Count * 100));

                    var ext = Path.GetExtension(file);

                    if (!_supportedExtensions.Contains(ext))
                    {
                        result.UnsupportedFiles.Add(Path.GetFileName(file));
                        continue;
                    }

                    var item = new StudentBatchItem(file);

                    // Validate image
                    ValidateStudentImage(item);

                    if (item.Status == StudentItemStatus.Failed)
                    {
                        result.InvalidImages.Add(item.OriginalFileName);
                    }
                    else
                    {
                        result.ValidCount++;
                    }

                    result.Students.Add(item);
                }

                // Default sort: Alphabetical by Student Name
                result.Students = result.Students
                    .OrderBy(s => s.StudentName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return result;
            }, ct);
        }

        public void ValidateStudentImage(StudentBatchItem item)
        {
            try
            {
                if (!File.Exists(item.OriginalFilePath))
                {
                    item.Status = StudentItemStatus.Failed;
                    item.StatusMessage = "الملف غير موجود";
                    item.ErrorMessage = "ملف الصورة مفقود على القرص.";
                    return;
                }

                using var mat = Cv2.ImRead(item.OriginalFilePath, ImreadModes.Color);
                if (mat == null || mat.Empty())
                {
                    item.Status = StudentItemStatus.Failed;
                    item.StatusMessage = "صورة تالفة";
                    item.ErrorMessage = "تعذر قراءة أو فك ترميز بيانات الصورة.";
                    return;
                }

                item.WidthPx = mat.Width;
                item.HeightPx = mat.Height;

                var warnings = new List<string>();

                // Check resolution
                if (mat.Width < 400 || mat.Height < 500)
                {
                    warnings.Add($"دقة منخفضة ({mat.Width}×{mat.Height} px)");
                }

                // Check aspect ratio
                double ratio = item.AspectRatio;
                if (ratio < 0.35 || ratio > 2.5)
                {
                    warnings.Add($"نسبة أبعاد غير اعتيادية ({ratio:F2})");
                }

                if (warnings.Count > 0)
                {
                    item.Status = StudentItemStatus.Warning;
                    item.StatusMessage = string.Join("، ", warnings);
                    item.Issues = warnings;
                }
                else
                {
                    item.Status = StudentItemStatus.Ready;
                    item.StatusMessage = "جاهز للطباعة";
                }
            }
            catch (Exception ex)
            {
                item.Status = StudentItemStatus.Failed;
                item.StatusMessage = "خطأ فحص";
                item.ErrorMessage = ex.Message;
            }
        }

        public LayoutPlan CalculateSchoolLayout(
            SchoolPackageTemplate template,
            IReadOnlyList<StudentBatchItem> students)
        {
            var plan = new LayoutPlan
            {
                PaperSize = template.PaperSize,
                Orientation = template.PaperOrientation,
                ShowCutMarks = template.ShowCutMarks,
                DrawBorderBox = template.DrawBorderBox
            };

            var (paperW, paperH) = template.PaperSize.GetDimensions(template.PaperOrientation);
            var margins = template.MarginsMm;
            var spacing = template.SpacingMm;

            var activeStudents = students.Where(s => s.IsSelected && s.Status != StudentItemStatus.Failed).ToList();
            if (activeStudents.Count == 0 || template.Items.Count == 0)
            {
                plan.TotalPages = 1;
                return plan;
            }

            var primaryItem = template.Items[0];
            double itemW = primaryItem.WidthMm;
            double itemH = primaryItem.HeightMm;
            int quantity = primaryItem.Quantity;

            // Determine best grid layout (columns and rows)
            int cols = 2;
            int rows = (int)Math.Ceiling((double)quantity / cols);

            if (quantity <= 4)
            {
                cols = 2;
                rows = 2;
            }
            else if (quantity == 6)
            {
                cols = 2;
                rows = 3;
            }
            else if (quantity == 8)
            {
                cols = 2;
                rows = 4;
            }

            double gridW = (cols * itemW) + ((cols - 1) * spacing);
            double gridH = (rows * itemH) + ((rows - 1) * spacing);

            double availW = paperW - (2 * margins);
            double availH = paperH - (2 * margins);

            double startX = margins + Math.Max(0, (availW - gridW) / 2.0);
            double startY = margins + Math.Max(0, (availH - gridH) / 2.0);

            int pageIndex = 0;

            foreach (var student in activeStudents)
            {
                int itemIdx = 0;
                for (int r = 0; r < rows && itemIdx < quantity; r++)
                {
                    for (int c = 0; c < cols && itemIdx < quantity; c++)
                    {
                        double x = startX + c * (itemW + spacing);
                        double y = startY + r * (itemH + spacing);

                        var layoutItem = new LayoutItem
                        {
                            PageIndex = pageIndex,
                            X_Mm = x,
                            Y_Mm = y,
                            Width_Mm = itemW,
                            Height_Mm = itemH,
                            SourceImagePath = student.ProcessedImagePath ?? student.OriginalFilePath,
                            ItemName = student.StudentName,
                            StudentName = student.StudentName,
                            IncludeNameLabel = template.IncludeStudentName,
                            NameFontSizePt = template.NameFontSizePt,
                            CopyIndex = itemIdx + 1,
                            CardRegionId = student.Id
                        };

                        plan.Items.Add(layoutItem);
                        itemIdx++;
                    }
                }

                pageIndex++;
            }

            plan.TotalPages = Math.Max(1, pageIndex);
            return plan;
        }

        public Task<BatchProcessingResult> ProcessBatchAsync(
            SchoolOrder order,
            SchoolPackageTemplate template,
            IProgress<BatchProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                var result = new BatchProcessingResult();
                var logs = new List<string>();

                void Log(string msg)
                {
                    var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}";
                    logs.Add(entry);
                }

                Log($"بدء معالجة طلب المدارس: {order.OrderNumber} - {order.SchoolName}");
                Log($"الباقة المختارة: {template.Name}");

                // Setup output directories
                string baseDir = order.OutputFolder;
                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    string safeDate = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                    baseDir = Path.Combine(AppContext.BaseDirectory, "Output", $"SchoolOrder_{safeDate}");
                    order.OutputFolder = baseDir;
                }

                string inputDir = Path.Combine(baseDir, "Input");
                string processedDir = Path.Combine(baseDir, "Processed");
                string outputDir = Path.Combine(baseDir, "Output");
                string reportsDir = Path.Combine(baseDir, "Reports");

                Directory.CreateDirectory(inputDir);
                Directory.CreateDirectory(processedDir);
                Directory.CreateDirectory(outputDir);
                Directory.CreateDirectory(reportsDir);

                result.OutputDirectory = baseDir;

                var students = order.Students.Where(s => s.IsSelected).ToList();
                result.TotalStudents = students.Count;
                Log($"إجمالي عدد الطلاب المحدد للمعالجة: {students.Count}");

                int currentIdx = 0;
                var processedStudents = new List<StudentBatchItem>();

                var targetItem = template.Items.FirstOrDefault() ?? new SchoolPackageItem(40, 60, 8);
                double targetRatio = targetItem.WidthMm / targetItem.HeightMm;

                foreach (var student in students)
                {
                    ct.ThrowIfCancellationRequested();
                    currentIdx++;

                    progress?.Report(new BatchProgressReport
                    {
                        CurrentIndex = currentIdx,
                        TotalCount = students.Count,
                        CurrentStudentName = student.StudentName,
                        SuccessCount = result.SuccessCount,
                        WarningCount = result.WarningCount,
                        FailedCount = result.FailedCount
                    });

                    try
                    {
                        // Check if file valid
                        if (!File.Exists(student.OriginalFilePath))
                        {
                            throw new FileNotFoundException($"ملف الصورة مفقود: {student.OriginalFilePath}");
                        }

                        using var srcMat = Cv2.ImRead(student.OriginalFilePath, ImreadModes.Color);
                        if (srcMat == null || srcMat.Empty())
                        {
                            throw new InvalidOperationException("تعذر قراءة الصورة بواسطة محرك المعالجة.");
                        }

                        // Smart crop to target aspect ratio
                        using var croppedMat = CropToAspectRatio(srcMat, targetRatio);

                        // Resize to 300 DPI physical pixel dimensions
                        int targetW_px = (int)Math.Round(targetItem.WidthMm / 25.4 * 300.0);
                        int targetH_px = (int)Math.Round(targetItem.HeightMm / 25.4 * 300.0);

                        using var resizedMat = new Mat();
                        Cv2.Resize(croppedMat, resizedMat, new OpenCvSharp.Size(targetW_px, targetH_px), 0, 0, InterpolationFlags.Cubic);

                        // Save processed student photo
                        string safeName = MakeSafeFileName(student.StudentName);
                        string procPath = Path.Combine(processedDir, $"{currentIdx:D3}_{safeName}.png");
                        Cv2.ImWrite(procPath, resizedMat);

                        student.ProcessedImagePath = procPath;
                        student.Status = StudentItemStatus.Processed;
                        student.StatusMessage = "تمت المعالجة بنجاح";

                        processedStudents.Add(student);
                        result.SuccessCount++;
                        Log($"نجاح معالجة الطالب [{currentIdx}/{students.Count}]: {student.StudentName}");

                        // If individual PDF requested
                        if (order.GenerateIndividualPdfs)
                        {
                            string singlePdfPath = Path.Combine(outputDir, $"Student_{currentIdx:D3}_{safeName}.pdf");
                            GenerateSingleStudentPdf(student, template, procPath, singlePdfPath);
                            result.IndividualPdfPaths.Add(singlePdfPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        student.Status = StudentItemStatus.Failed;
                        student.ErrorMessage = ex.Message;
                        student.StatusMessage = $"خطأ: {ex.Message}";
                        result.FailedCount++;
                        result.FailedStudents.Add(student);
                        Log($"خطأ في معالجة الطالب [{currentIdx}/{students.Count}] {student.StudentName}: {ex.Message}");
                    }
                }

                // Generate Combined PDF for all successful students
                if (processedStudents.Count > 0)
                {
                    Log("جاري إنشاء ملف PDF الشامل لجميع الطلاب...");
                    string combinedPdfPath = Path.Combine(outputDir, "School_Order.pdf");
                    var plan = CalculateSchoolLayout(template, processedStudents);
                    GenerateCombinedPdf(plan, template, combinedPdfPath);

                    result.CombinedPdfPath = combinedPdfPath;
                    result.PagesGenerated = plan.TotalPages;
                    order.CombinedPdfPath = combinedPdfPath;
                    order.PagesGenerated = plan.TotalPages;
                    Log($"تم إنشاء ملف PDF بنجاح: {combinedPdfPath} (إجمالي الصفحات: {plan.TotalPages})");
                }
                else
                {
                    Log("تنبيه: لم تنجح معالجة أي طالب لتوليد ملف PDF.");
                }

                order.SuccessCount = result.SuccessCount;
                order.WarningCount = result.WarningCount;
                order.FailedCount = result.FailedCount;
                order.Status = result.FailedCount == 0 ? "مكتمل بنجاح" : "مكتمل مع وجود أخطاء";

                // Save Reports
                string reportJsonPath = Path.Combine(reportsDir, "order-report.json");
                string reportTxtPath = Path.Combine(reportsDir, "order-report.txt");

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(reportJsonPath, JsonSerializer.Serialize(order, jsonOptions), Encoding.UTF8);

                var txtBuilder = new StringBuilder();
                txtBuilder.AppendLine("======================================================================");
                txtBuilder.AppendLine($"تقرير باقة المدارس: {order.SchoolName} ({order.OrderNumber})");
                txtBuilder.AppendLine($"التاريخ: {order.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                txtBuilder.AppendLine($"الباقة: {template.Name} ({template.Description})");
                txtBuilder.AppendLine("======================================================================");
                txtBuilder.AppendLine($"إجمالي الطلاب: {result.TotalStudents}");
                txtBuilder.AppendLine($"الناجح: {result.SuccessCount}");
                txtBuilder.AppendLine($"التنبيهات: {result.WarningCount}");
                txtBuilder.AppendLine($"الأخطاء والفشل: {result.FailedCount}");
                txtBuilder.AppendLine($"الصفحات المولدة: {result.PagesGenerated}");
                txtBuilder.AppendLine($"ملف PDF الشامل: {result.CombinedPdfPath}");
                txtBuilder.AppendLine("======================================================================");
                txtBuilder.AppendLine("قائمة الطلاب:");
                int sNum = 1;
                foreach (var s in order.Students)
                {
                    txtBuilder.AppendLine($"{sNum++:D3}. {s.StudentName,-30} | {s.Status,-10} | {s.StatusMessage}");
                    if (!string.IsNullOrEmpty(s.ErrorMessage))
                    {
                        txtBuilder.AppendLine($"     سبب الخطأ: {s.ErrorMessage}");
                    }
                }
                txtBuilder.AppendLine("======================================================================");
                txtBuilder.AppendLine("سجل الأحداث:");
                foreach (var l in logs)
                {
                    txtBuilder.AppendLine(l);
                }

                File.WriteAllText(reportTxtPath, txtBuilder.ToString(), Encoding.UTF8);

                result.ReportJsonPath = reportJsonPath;
                result.ReportTextPath = reportTxtPath;
                result.Logs = logs;

                Log("اكتملت مهمة الباقة وتم حفظ التقارير.");
                return result;
            }, ct);
        }

        public Task<BatchProcessingResult> ReprocessFailedAsync(
            SchoolOrder order,
            SchoolPackageTemplate template,
            IProgress<BatchProgressReport>? progress = null,
            CancellationToken ct = default)
        {
            var failedOnly = order.Students.Where(s => s.Status == StudentItemStatus.Failed).ToList();
            foreach (var s in failedOnly)
            {
                s.Status = StudentItemStatus.Pending;
                s.ErrorMessage = null;
                s.IsSelected = true;
            }

            return ProcessBatchAsync(order, template, progress, ct);
        }

        private Mat CropToAspectRatio(Mat src, double targetRatio)
        {
            double srcRatio = (double)src.Width / src.Height;

            int cropW = src.Width;
            int cropH = src.Height;

            if (srcRatio > targetRatio)
            {
                // Source is wider than target: crop left & right
                cropW = (int)Math.Round(src.Height * targetRatio);
                int startX = (src.Width - cropW) / 2;
                return new Mat(src, new Rect(startX, 0, cropW, src.Height));
            }
            else
            {
                // Source is taller than target: crop top & bottom (bias 35% from top for face room)
                cropH = (int)Math.Round(src.Width / targetRatio);
                int startY = (int)Math.Round((src.Height - cropH) * 0.35);
                startY = Math.Max(0, Math.Min(startY, src.Height - cropH));
                return new Mat(src, new Rect(0, startY, src.Width, cropH));
            }
        }

        private void GenerateSingleStudentPdf(
            StudentBatchItem student,
            SchoolPackageTemplate template,
            string imagePath,
            string outputPath)
        {
            var singleList = new List<StudentBatchItem> { student };
            var plan = CalculateSchoolLayout(template, singleList);
            GenerateCombinedPdf(plan, template, outputPath);
        }

        private void GenerateCombinedPdf(
            LayoutPlan plan,
            SchoolPackageTemplate template,
            string outputPath)
        {
            using var document = new PdfDocument();
            document.Info.Title = $"School Order - {template.Name}";
            document.Info.Author = "Smart Print Shop Pro";

            var (paperW, paperH) = template.PaperSize.GetDimensions(template.PaperOrientation);

            var hairlinePen = new XPen(XColor.FromArgb(170, 170, 170), 0.35);
            var borderPen = new XPen(XColor.FromArgb(215, 215, 215), 0.35);

            for (int pageIdx = 0; pageIdx < plan.TotalPages; pageIdx++)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromMillimeter(paperW);
                page.Height = XUnit.FromMillimeter(paperH);

                using var gfx = XGraphics.FromPdfPage(page);
                var pageItems = plan.GetItemsForPage(pageIdx);

                foreach (var item in pageItems)
                {
                    var xPt = XUnit.FromMillimeter(item.X_Mm);
                    var yPt = XUnit.FromMillimeter(item.Y_Mm);
                    var wPt = XUnit.FromMillimeter(item.Width_Mm);
                    var hPt = XUnit.FromMillimeter(item.Height_Mm);

                    // Draw image
                    if (!string.IsNullOrEmpty(item.SourceImagePath) && File.Exists(item.SourceImagePath))
                    {
                        try
                        {
                            using var img = XImage.FromFile(item.SourceImagePath);
                            gfx.DrawImage(img, xPt.Point, yPt.Point, wPt.Point, hPt.Point);
                        }
                        catch
                        {
                            var placeholder = new XSolidBrush(XColor.FromArgb(240, 240, 240));
                            gfx.DrawRectangle(placeholder, xPt.Point, yPt.Point, wPt.Point, hPt.Point);
                        }
                    }
                    else
                    {
                        var placeholder = new XSolidBrush(XColor.FromArgb(240, 240, 240));
                        gfx.DrawRectangle(placeholder, xPt.Point, yPt.Point, wPt.Point, hPt.Point);
                    }

                    // Border box
                    if (plan.DrawBorderBox)
                    {
                        gfx.DrawRectangle(borderPen, xPt.Point, yPt.Point, wPt.Point, hPt.Point);
                    }

                    // Cut marks
                    if (plan.ShowCutMarks)
                    {
                        DrawCutMarks(gfx, hairlinePen, item.X_Mm, item.Y_Mm, item.Width_Mm, item.Height_Mm);
                    }

                    // Student name label strip
                    if (item.IncludeNameLabel && !string.IsNullOrWhiteSpace(item.StudentName))
                    {
                        try
                        {
                            double stripHeightMm = 4.2;
                            var stripY = yPt.Point + hPt.Point - XUnit.FromMillimeter(stripHeightMm).Point;
                            var stripH = XUnit.FromMillimeter(stripHeightMm).Point;

                            // Semi-transparent white backing strip for high contrast readability
                            var stripBrush = new XSolidBrush(XColor.FromArgb(235, 255, 255, 255));
                            gfx.DrawRectangle(stripBrush, xPt.Point, stripY, wPt.Point, stripH);

                            var font = new XFont(template.NameFontFamily ?? "Arial", template.NameFontSizePt > 0 ? template.NameFontSizePt : 8.5, XFontStyleEx.Bold);
                            var textBrush = new XSolidBrush(XColor.FromArgb(25, 25, 25));

                            var format = new XStringFormat
                            {
                                Alignment = XStringAlignment.Center,
                                LineAlignment = XLineAlignment.Center
                            };

                            gfx.DrawString(item.StudentName, font, textBrush, new XRect(xPt.Point, stripY, wPt.Point, stripH), format);
                        }
                        catch
                        {
                            // Defensive suppression: ensures font resolution issues never fail the batch
                        }
                    }
                }
            }

            document.Save(outputPath);
        }

        private void DrawCutMarks(XGraphics gfx, XPen pen, double xMm, double yMm, double wMm, double hMm)
        {
            double tickLen = 3.0;
            double gap = 0.8;

            double ToPt(double mm) => XUnit.FromMillimeter(mm).Point;

            // Top-Left Corner
            gfx.DrawLine(pen, ToPt(xMm - gap - tickLen), ToPt(yMm), ToPt(xMm - gap), ToPt(yMm));
            gfx.DrawLine(pen, ToPt(xMm), ToPt(yMm - gap - tickLen), ToPt(xMm), ToPt(yMm - gap));

            // Top-Right Corner
            gfx.DrawLine(pen, ToPt(xMm + wMm + gap), ToPt(yMm), ToPt(xMm + wMm + gap + tickLen), ToPt(yMm));
            gfx.DrawLine(pen, ToPt(xMm + wMm), ToPt(yMm - gap - tickLen), ToPt(xMm + wMm), ToPt(yMm - gap));

            // Bottom-Left Corner
            gfx.DrawLine(pen, ToPt(xMm - gap - tickLen), ToPt(yMm + hMm), ToPt(xMm - gap), ToPt(yMm + hMm));
            gfx.DrawLine(pen, ToPt(xMm), ToPt(yMm + hMm + gap), ToPt(xMm), ToPt(yMm + hMm + gap + tickLen));

            // Bottom-Right Corner
            gfx.DrawLine(pen, ToPt(xMm + wMm + gap), ToPt(yMm + hMm), ToPt(xMm + wMm + gap + tickLen), ToPt(yMm + hMm));
            gfx.DrawLine(pen, ToPt(xMm + wMm), ToPt(yMm + hMm + gap), ToPt(xMm + wMm), ToPt(yMm + hMm + gap + tickLen));
        }

        private string MakeSafeFileName(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            var clean = new string(name.Select(c => invalids.Contains(c) ? '_' : c).ToArray());
            return clean.Trim();
        }
    }
}
