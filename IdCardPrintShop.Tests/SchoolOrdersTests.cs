using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using IdCardPrintShop.ViewModels;
using OpenCvSharp;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace IdCardPrintShop.Tests
{
    public class SchoolOrdersTests
    {
        private readonly string _testBaseDir;
        private readonly SchoolOrderEngine _engine = new();

        public SchoolOrdersTests()
        {
            _testBaseDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestSchoolOrders"));
            Directory.CreateDirectory(_testBaseDir);
        }

        private string CreateSyntheticStudentPhoto(string dir, string fileName, int width = 600, int height = 800, Scalar? bgColor = null)
        {
            string path = Path.Combine(dir, fileName);
            using var mat = new Mat(height, width, MatType.CV_8UC3, bgColor ?? new Scalar(230, 220, 200));

            // Draw a face placeholder (head and shoulders)
            Cv2.Circle(mat, new Point(width / 2, (int)(height * 0.4)), (int)(width * 0.25), new Scalar(160, 140, 120), -1);
            Cv2.Ellipse(mat, new Point(width / 2, (int)(height * 0.9)), new Size(width * 0.45, height * 0.35), 0, 0, 360, new Scalar(80, 60, 50), -1);

            Cv2.ImWrite(path, mat);
            return path;
        }

        [Fact]
        public void ExtractStudentName_VariousFormats_NormalizesCorrectly()
        {
            // Case 1: Underscores
            Assert.Equal("Ahmed Mohamed", StudentBatchItem.ExtractStudentName("Ahmed_Mohamed.jpg"));

            // Case 2: Hyphens
            Assert.Equal("Sara Ahmed", StudentBatchItem.ExtractStudentName("Sara-Ahmed.png"));

            // Case 3: Multiple spaces and underscores
            Assert.Equal("Mahmoud Ali Hassan", StudentBatchItem.ExtractStudentName("Mahmoud   Ali_Hassan.jpeg"));

            // Case 4: Numbers and Arabic names
            Assert.Equal("12 منة حسن", StudentBatchItem.ExtractStudentName("12_منة_حسن.webp"));

            // Case 5: Single name
            Assert.Equal("Mustafa", StudentBatchItem.ExtractStudentName("Mustafa.bmp"));

            // Original file name retention
            var item = new StudentBatchItem("C:\\Students\\Omar_Khaled_2026.jpg");
            Assert.Equal("Omar Khaled 2026", item.StudentName);
            Assert.Equal("Omar_Khaled_2026.jpg", item.OriginalFileName);
        }

        [Fact]
        public void SchoolPackageTemplates_DefaultTemplates_MeetRequirements()
        {
            var templates = SchoolPackageTemplate.DefaultTemplates;

            Assert.True(templates.Count >= 5);

            // Template 1: 8x 40x60 mm
            var t1 = templates.FirstOrDefault(t => t.Id == "tpl-8-4x6");
            Assert.NotNull(t1);
            Assert.Equal(40.0, t1.Items[0].WidthMm);
            Assert.Equal(60.0, t1.Items[0].HeightMm);
            Assert.Equal(8, t1.Items[0].Quantity);
            Assert.Equal("A4", t1.PaperSize.Name);

            // Template 2: 6x 40x60 mm
            var t2 = templates.FirstOrDefault(t => t.Id == "tpl-6-4x6");
            Assert.NotNull(t2);
            Assert.Equal(6, t2.Items[0].Quantity);

            // Template 3: 8x 35x45 mm
            var t3 = templates.FirstOrDefault(t => t.Id == "tpl-8-35x45");
            Assert.NotNull(t3);
            Assert.Equal(35.0, t3.Items[0].WidthMm);
            Assert.Equal(45.0, t3.Items[0].HeightMm);

            // Template 4: 4x 50x70 mm
            var t4 = templates.FirstOrDefault(t => t.Id == "tpl-4-5x7");
            Assert.NotNull(t4);
            Assert.Equal(50.0, t4.Items[0].WidthMm);
            Assert.Equal(70.0, t4.Items[0].HeightMm);
            Assert.Equal(4, t4.Items[0].Quantity);

            // Template 5: Custom
            var t5 = templates.FirstOrDefault(t => t.Id == "tpl-custom");
            Assert.NotNull(t5);
        }

        [Fact]
        public void CalculateSchoolLayout_PhysicalAccuracy_StaysWithinPageBounds()
        {
            var template = SchoolPackageTemplate.DefaultTemplates[0]; // 8x 4x6
            var (paperW, paperH) = template.PaperSize.GetDimensions(template.PaperOrientation);

            var students = new List<StudentBatchItem>
            {
                new StudentBatchItem("C:\\fake\\Ahmed.jpg") { Status = StudentItemStatus.Ready },
                new StudentBatchItem("C:\\fake\\Sara.jpg") { Status = StudentItemStatus.Ready }
            };

            var plan = _engine.CalculateSchoolLayout(template, students);

            Assert.Equal(2, plan.TotalPages); // 1 page per student
            Assert.Equal(16, plan.Items.Count); // 8 photos * 2 students

            foreach (var item in plan.Items)
            {
                Assert.Equal(40.0, item.Width_Mm);
                Assert.Equal(60.0, item.Height_Mm);
                Assert.True(item.IncludeNameLabel);

                // Physical bounds check
                Assert.True(item.X_Mm >= template.MarginsMm, $"X={item.X_Mm} should be >= margin {template.MarginsMm}");
                Assert.True(item.X_Mm + item.Width_Mm <= paperW - template.MarginsMm + 0.1, "Item right edge exceeds margin");
                Assert.True(item.Y_Mm >= template.MarginsMm, $"Y={item.Y_Mm} should be >= margin {template.MarginsMm}");
                Assert.True(item.Y_Mm + item.Height_Mm <= paperH - template.MarginsMm + 0.1, "Item bottom edge exceeds margin");
            }
        }

        [Fact]
        public void ValidateStudentImage_DetectsCorruptedAndLowResolution()
        {
            string testDir = Path.Combine(_testBaseDir, "ValidationTest");
            Directory.CreateDirectory(testDir);

            // 1. Valid image
            string validPath = CreateSyntheticStudentPhoto(testDir, "ValidStudent.jpg", 600, 800);
            var itemValid = new StudentBatchItem(validPath);
            _engine.ValidateStudentImage(itemValid);
            Assert.Equal(StudentItemStatus.Ready, itemValid.Status);

            // 2. Low resolution image
            string lowResPath = CreateSyntheticStudentPhoto(testDir, "LowResStudent.jpg", 250, 300);
            var itemLowRes = new StudentBatchItem(lowResPath);
            _engine.ValidateStudentImage(itemLowRes);
            Assert.Equal(StudentItemStatus.Warning, itemLowRes.Status);
            Assert.Contains(itemLowRes.Issues, s => s.Contains("دقة منخفضة"));

            // 3. Corrupted image file
            string corruptPath = Path.Combine(testDir, "CorruptedStudent.jpg");
            File.WriteAllBytes(corruptPath, new byte[] { 0x00, 0x11, 0x22, 0x33 });
            var itemCorrupt = new StudentBatchItem(corruptPath);
            _engine.ValidateStudentImage(itemCorrupt);
            Assert.Equal(StudentItemStatus.Failed, itemCorrupt.Status);
        }

        [Fact]
        public async Task ScanFolderAsync_FiltersUnsupportedAndCorruptedFiles()
        {
            string scanDir = Path.Combine(_testBaseDir, "ScanFolderTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scanDir);

            // 3 Valid student images
            CreateSyntheticStudentPhoto(scanDir, "Ahmed_Ali.jpg");
            CreateSyntheticStudentPhoto(scanDir, "Sara_Hassan.png");
            CreateSyntheticStudentPhoto(scanDir, "Ziad_Omar.webp");

            // 2 Unsupported files
            File.WriteAllText(Path.Combine(scanDir, "notes.txt"), "This is text");
            File.WriteAllText(Path.Combine(scanDir, "exam.pdf"), "%PDF-1.4 dummy");

            // 1 Corrupted image
            File.WriteAllBytes(Path.Combine(scanDir, "Bad_Image.jpg"), new byte[] { 0xFF, 0x00 });

            var result = await _engine.ScanFolderAsync(scanDir);

            Assert.Equal(6, result.TotalFiles);
            Assert.Equal(3, result.ValidCount);
            Assert.Equal(2, result.UnsupportedCount);
            Assert.Equal(1, result.InvalidCount);
            Assert.Equal(4, result.Students.Count); // 3 valid + 1 failed image in list

            // Check default alphabetical sorting A-Z
            Assert.Equal("Ahmed Ali", result.Students[0].StudentName);
        }

        [Fact]
        public async Task ProcessBatchAsync_CompleteGoldenPath_IsolatesErrorsAndGeneratesReports()
        {
            string batchDir = Path.Combine(_testBaseDir, "GoldenPath_" + Guid.NewGuid().ToString("N"));
            string inputDir = Path.Combine(batchDir, "StudentsInput");
            Directory.CreateDirectory(inputDir);

            var students = new List<StudentBatchItem>();

            // Create 9 valid student photos
            string[] names = {
                "Ahmed_Mohamed", "Sara_Ahmed", "Mahmoud_Ali",
                "Menna_Hassan", "Youssef_Ibrahim", "Nour_Khaled",
                "Karim_Tarek", "Mariam_Sayed", "Hassan_Adel"
            };

            for (int i = 0; i < names.Length; i++)
            {
                string path = CreateSyntheticStudentPhoto(inputDir, $"{names[i]}.jpg", 600, 800);
                var item = new StudentBatchItem(path)
                {
                    Status = StudentItemStatus.Ready,
                    IsSelected = true
                };
                students.Add(item);
            }

            // Create 1 corrupted image (Student 10) to test error isolation
            string corruptPath = Path.Combine(inputDir, "Corrupt_Student.jpg");
            File.WriteAllBytes(corruptPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            var corruptItem = new StudentBatchItem(corruptPath)
            {
                Status = StudentItemStatus.Failed,
                IsSelected = true
            };
            students.Add(corruptItem);

            var template = SchoolPackageTemplate.DefaultTemplates[0]; // 8x 4x6 cm
            var order = new SchoolOrder
            {
                SchoolName = "مدرسة التجربة الذهبية",
                SourceFolder = inputDir,
                TemplateId = template.Id,
                TemplateName = template.Name,
                OutputFolder = Path.Combine(batchDir, "OrderOutput"),
                GenerateIndividualPdfs = true,
                Students = students
            };

            var result = await _engine.ProcessBatchAsync(order, template);

            // Assert Error Isolation: 9 succeeded, 1 failed, process didn't stop!
            Assert.Equal(9, result.SuccessCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal(9, result.PagesGenerated);

            // Verify Combined PDF
            Assert.True(File.Exists(result.CombinedPdfPath));
            using (var doc = PdfReader.Open(result.CombinedPdfPath, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(9, doc.PageCount);
            }

            // Verify Individual PDFs were generated for the 9 students
            Assert.Equal(9, result.IndividualPdfPaths.Count);
            foreach (var pdf in result.IndividualPdfPaths)
            {
                Assert.True(File.Exists(pdf));
            }

            // Verify Reports
            Assert.True(File.Exists(result.ReportJsonPath));
            Assert.True(File.Exists(result.ReportTextPath));

            string reportText = File.ReadAllText(result.ReportTextPath);
            Assert.Contains("مدرسة التجربة الذهبية", reportText);
            Assert.Contains("Ahmed Mohamed", reportText);
            Assert.Contains("الناجح: 9", reportText);
            Assert.Contains("الأخطاء والفشل: 1", reportText);
        }

        [Fact]
        public void MainViewModel_SchoolOrders_FilterAndSortWorkAccurately()
        {
            var vm = new MainViewModel(
                new OpenCvImageProcessingService(),
                new LayoutEngine(),
                new PdfSharpExportService(),
                new JobPersistenceService(),
                schoolOrderEngine: _engine);

            vm.SwitchToSchoolOrders();
            Assert.True(vm.IsSchoolOrdersMode);

            vm.Students.Add(new StudentBatchItem("C:\\fake\\Ziad_Hassan.jpg") { Status = StudentItemStatus.Ready });
            vm.Students.Add(new StudentBatchItem("C:\\fake\\Ahmed_Ali.jpg") { Status = StudentItemStatus.Ready });
            vm.Students.Add(new StudentBatchItem("C:\\fake\\Sara_Hassan.jpg") { Status = StudentItemStatus.Warning });
            vm.Students.Add(new StudentBatchItem("C:\\fake\\Bad_Student.jpg") { Status = StudentItemStatus.Failed });

            // Sort A-Z
            vm.StudentSortOrder = "NameAZ";
            vm.ApplyStudentFilters();
            Assert.Equal("Ahmed Ali", vm.FilteredStudents[0].StudentName);

            // Sort Z-A
            vm.StudentSortOrder = "NameZA";
            vm.ApplyStudentFilters();
            Assert.Equal("Ziad Hassan", vm.FilteredStudents[0].StudentName);

            // Filter Ready only
            vm.StudentStatusFilter = "Ready";
            vm.ApplyStudentFilters();
            Assert.Equal(2, vm.FilteredStudents.Count);

            // Filter Warnings only
            vm.StudentStatusFilter = "Warnings";
            vm.ApplyStudentFilters();
            Assert.Single(vm.FilteredStudents);
            Assert.Equal("Sara Hassan", vm.FilteredStudents[0].StudentName);

            // Filter Errors only
            vm.StudentStatusFilter = "Errors";
            vm.ApplyStudentFilters();
            Assert.Single(vm.FilteredStudents);
            Assert.Equal("Bad Student", vm.FilteredStudents[0].StudentName);

            // Search query
            vm.StudentStatusFilter = "All";
            vm.StudentSearchQuery = "Ahmed";
            vm.ApplyStudentFilters();
            Assert.Single(vm.FilteredStudents);
            Assert.Equal("Ahmed Ali", vm.FilteredStudents[0].StudentName);
        }
    }
}
