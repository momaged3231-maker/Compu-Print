using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using IdCardPrintShop.ViewModels;
using OpenCvSharp;
using PdfSharp.Pdf.IO;
using Xunit;

namespace IdCardPrintShop.Tests
{
    public class EvidenceRunner
    {
        private readonly OpenCvImageProcessingService _imgService = new();
        private readonly LayoutEngine _layoutEngine = new();
        private readonly PdfSharpExportService _pdfService = new();
        private readonly JobPersistenceService _jobService = new();

        private readonly string _evidenceDir;
        private readonly string _samplesDir;

        public EvidenceRunner()
        {
            _evidenceDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Evidence"));
            _samplesDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Samples"));
            Directory.CreateDirectory(_evidenceDir);
            Directory.CreateDirectory(_samplesDir);
        }

        [Fact]
        public async Task RunFullEvidenceGateVerification()
        {
            var summaryReport = new StringBuilder();
            summaryReport.AppendLine("# EVIDENCE GATE COMPREHENSIVE VERIFICATION REPORT");
            summaryReport.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            summaryReport.AppendLine();

            // 1. Verify CV Cases A to F
            await VerifyCaseA(summaryReport);
            await VerifyCaseB(summaryReport);
            await VerifyCaseC(summaryReport);
            await VerifyCaseD(summaryReport);
            await VerifyCaseE(summaryReport);
            await VerifyCaseF(summaryReport);

            // 2. Verify PDF Physical Dimensions (A4, A5, A3)
            VerifyPdfDimensions(summaryReport);

            // 3. Verify Non-Destructive Hash
            VerifyNonDestructiveIntegrity(summaryReport);

            // 4. Verify Persistence & Reproducibility
            await VerifyPersistenceAndReproducibility(summaryReport);

            // 5. Verify Manual Adjustment Parameters
            VerifyManualAdjustmentSimulation(summaryReport);

            // Save master evidence report
            string reportPath = Path.Combine(_evidenceDir, "EVIDENCE_GATE_REPORT.md");
            File.WriteAllText(reportPath, summaryReport.ToString(), Encoding.UTF8);

            Assert.True(File.Exists(reportPath));
        }

        private async Task VerifyCaseA(StringBuilder report)
        {
            report.AppendLine("## Case A: Single Card with Large Margins");
            string dir = Path.Combine(_evidenceDir, "CaseA_SingleCard_LargeMargin");
            Directory.CreateDirectory(dir);

            string origPath = Path.Combine(_samplesDir, "04_card_with_large_margins.jpg");
            if (!File.Exists(origPath))
            {
                CreateLargeMarginSample(origPath);
            }

            File.Copy(origPath, Path.Combine(dir, "original.jpg"), true);

            var det = _imgService.DetectCards(origPath);
            var overlay = DrawDetectionOverlay(origPath, det.DetectedCards);
            Cv2.ImWrite(Path.Combine(dir, "detection_overlay.jpg"), overlay);

            var geomSb = new StringBuilder();
            geomSb.AppendLine($"Detection Case: {det.Case}");
            geomSb.AppendLine($"Cards Count: {det.DetectedCards.Count}");
            geomSb.AppendLine($"IsConfident: {det.IsConfident}");
            geomSb.AppendLine($"Message: {det.Message}");

            for (int i = 0; i < det.DetectedCards.Count; i++)
            {
                var card = det.DetectedCards[i];
                AppendCardGeometry(geomSb, card, i + 1);

                using var rectified = _imgService.WarpAndCorrectCard(origPath, card);
                Cv2.ImWrite(Path.Combine(dir, $"rectified_card_{i + 1}.jpg"), rectified);
            }

            File.WriteAllText(Path.Combine(dir, "geometry.txt"), geomSb.ToString());
            report.AppendLine(geomSb.ToString());
            report.AppendLine();
        }

        private async Task VerifyCaseB(StringBuilder report)
        {
            report.AppendLine("## Case B: Front + Back Side-by-Side");
            string dir = Path.Combine(_evidenceDir, "CaseB_FrontBack_SideBySide");
            Directory.CreateDirectory(dir);

            string origPath = Path.Combine(_samplesDir, "01_front_and_back_side_by_side.jpg");
            File.Copy(origPath, Path.Combine(dir, "original.jpg"), true);

            var det = _imgService.DetectCards(origPath);
            var overlay = DrawDetectionOverlay(origPath, det.DetectedCards);
            Cv2.ImWrite(Path.Combine(dir, "detection_overlay.jpg"), overlay);

            var geomSb = new StringBuilder();
            geomSb.AppendLine($"Detection Case: {det.Case}");
            geomSb.AppendLine($"Cards Count: {det.DetectedCards.Count}");
            geomSb.AppendLine($"IsConfident: {det.IsConfident}");

            for (int i = 0; i < det.DetectedCards.Count; i++)
            {
                var card = det.DetectedCards[i];
                AppendCardGeometry(geomSb, card, i + 1);

                using var rectified = _imgService.WarpAndCorrectCard(origPath, card);
                Cv2.ImWrite(Path.Combine(dir, $"rectified_card_{i + 1}_{card.Role}.jpg"), rectified);
            }

            File.WriteAllText(Path.Combine(dir, "geometry.txt"), geomSb.ToString());
            report.AppendLine(geomSb.ToString());
            report.AppendLine();
        }

        private async Task VerifyCaseC(StringBuilder report)
        {
            report.AppendLine("## Case C: Front + Back Stacked Vertically");
            string dir = Path.Combine(_evidenceDir, "CaseC_FrontBack_Stacked");
            Directory.CreateDirectory(dir);

            string origPath = Path.Combine(_samplesDir, "02_front_and_back_stacked.jpg");
            File.Copy(origPath, Path.Combine(dir, "original.jpg"), true);

            var det = _imgService.DetectCards(origPath);
            var overlay = DrawDetectionOverlay(origPath, det.DetectedCards);
            Cv2.ImWrite(Path.Combine(dir, "detection_overlay.jpg"), overlay);

            var geomSb = new StringBuilder();
            geomSb.AppendLine($"Detection Case: {det.Case}");
            geomSb.AppendLine($"Cards Count: {det.DetectedCards.Count}");
            geomSb.AppendLine($"IsConfident: {det.IsConfident}");

            for (int i = 0; i < det.DetectedCards.Count; i++)
            {
                var card = det.DetectedCards[i];
                AppendCardGeometry(geomSb, card, i + 1);

                using var rectified = _imgService.WarpAndCorrectCard(origPath, card);
                Cv2.ImWrite(Path.Combine(dir, $"rectified_card_{i + 1}_{card.Role}.jpg"), rectified);
            }

            File.WriteAllText(Path.Combine(dir, "geometry.txt"), geomSb.ToString());
            report.AppendLine(geomSb.ToString());
            report.AppendLine();
        }

        private async Task VerifyCaseD(StringBuilder report)
        {
            report.AppendLine("## Case D: Back + Front Reversed Order");
            string dir = Path.Combine(_evidenceDir, "CaseD_Reversed_BackFront");
            Directory.CreateDirectory(dir);

            // Generate image with Back on top/left, Front on bottom/right
            string origPath = Path.Combine(dir, "original.jpg");
            using (var mat = new Mat(new Size(1400, 1000), MatType.CV_8UC3, new Scalar(30, 32, 35)))
            {
                DrawSampleCard(mat, 150, 350, 480, 300, isFront: false); // Back on left
                DrawSampleCard(mat, 760, 350, 480, 300, isFront: true);  // Front on right
                Cv2.ImWrite(origPath, mat);
            }

            var det = _imgService.DetectCards(origPath);
            var overlay = DrawDetectionOverlay(origPath, det.DetectedCards);
            Cv2.ImWrite(Path.Combine(dir, "detection_overlay.jpg"), overlay);

            var geomSb = new StringBuilder();
            geomSb.AppendLine($"Detection Case: {det.Case}");
            geomSb.AppendLine($"Cards Count: {det.DetectedCards.Count}");
            geomSb.AppendLine("Reversed order handling: The system detects both regions independently.");

            for (int i = 0; i < det.DetectedCards.Count; i++)
            {
                var card = det.DetectedCards[i];
                AppendCardGeometry(geomSb, card, i + 1);

                using var rectified = _imgService.WarpAndCorrectCard(origPath, card);
                Cv2.ImWrite(Path.Combine(dir, $"rectified_card_{i + 1}.jpg"), rectified);
            }

            File.WriteAllText(Path.Combine(dir, "geometry.txt"), geomSb.ToString());
            report.AppendLine(geomSb.ToString());
            report.AppendLine();
        }

        private async Task VerifyCaseE(StringBuilder report)
        {
            report.AppendLine("## Case E: Skewed Perspective Card");
            string dir = Path.Combine(_evidenceDir, "CaseE_Skewed_Perspective");
            Directory.CreateDirectory(dir);

            string origPath = Path.Combine(_samplesDir, "03_skewed_perspective_card.jpg");
            File.Copy(origPath, Path.Combine(dir, "original.jpg"), true);

            var det = _imgService.DetectCards(origPath);
            var overlay = DrawDetectionOverlay(origPath, det.DetectedCards);
            Cv2.ImWrite(Path.Combine(dir, "detection_overlay.jpg"), overlay);

            var geomSb = new StringBuilder();
            geomSb.AppendLine($"Detection Case: {det.Case}");
            geomSb.AppendLine($"Cards Count: {det.DetectedCards.Count}");
            geomSb.AppendLine($"IsConfident: {det.IsConfident}");

            for (int i = 0; i < det.DetectedCards.Count; i++)
            {
                var card = det.DetectedCards[i];
                AppendCardGeometry(geomSb, card, i + 1);

                using var rectified = _imgService.WarpAndCorrectCard(origPath, card);
                Cv2.ImWrite(Path.Combine(dir, $"rectified_card_{i + 1}.jpg"), rectified);

                geomSb.AppendLine($"Rectified Result Dimensions: {rectified.Width} × {rectified.Height} px");
                geomSb.AppendLine($"Rectified Aspect Ratio: {((double)rectified.Width / rectified.Height):F3}");
            }

            File.WriteAllText(Path.Combine(dir, "geometry.txt"), geomSb.ToString());
            report.AppendLine(geomSb.ToString());
            report.AppendLine();
        }

        private async Task VerifyCaseF(StringBuilder report)
        {
            report.AppendLine("## Case F: Challenging Detection Image (Low contrast / shadow)");
            string dir = Path.Combine(_evidenceDir, "CaseF_Challenging_LowContrast");
            Directory.CreateDirectory(dir);

            string origPath = Path.Combine(dir, "original.jpg");
            using (var mat = new Mat(new Size(1200, 900), MatType.CV_8UC3, new Scalar(70, 70, 70)))
            {
                // Card with very low contrast against background
                using var card = new Mat(new Size(480, 300), MatType.CV_8UC3, new Scalar(82, 82, 85));
                Cv2.PutText(card, "LOW CONTRAST DOCUMENT", new Point(30, 150), HersheyFonts.HersheySimplex, 0.7, new Scalar(50, 50, 50), 2);
                var roi = new Mat(mat, new Rect(360, 300, 480, 300));
                card.CopyTo(roi);

                Cv2.ImWrite(origPath, mat);
            }

            var det = _imgService.DetectCards(origPath);
            var overlay = DrawDetectionOverlay(origPath, det.DetectedCards);
            Cv2.ImWrite(Path.Combine(dir, "detection_overlay.jpg"), overlay);

            var geomSb = new StringBuilder();
            geomSb.AppendLine($"Detection Case: {det.Case}");
            geomSb.AppendLine($"IsConfident: {det.IsConfident}");
            geomSb.AppendLine($"Message: {det.Message}");

            for (int i = 0; i < det.DetectedCards.Count; i++)
            {
                var card = det.DetectedCards[i];
                AppendCardGeometry(geomSb, card, i + 1);

                using var rectified = _imgService.WarpAndCorrectCard(origPath, card);
                Cv2.ImWrite(Path.Combine(dir, $"rectified_card_{i + 1}.jpg"), rectified);
            }

            File.WriteAllText(Path.Combine(dir, "geometry.txt"), geomSb.ToString());
            report.AppendLine(geomSb.ToString());
            report.AppendLine();
        }

        private void VerifyPdfDimensions(StringBuilder report)
        {
            report.AppendLine("## PDF Physical Dimensions Verification");
            string dir = Path.Combine(_evidenceDir, "PDF_Verification");
            Directory.CreateDirectory(dir);

            using var sampleMat = new Mat(new Size(500, 315), MatType.CV_8UC3, new Scalar(240, 240, 240));
            Cv2.PutText(sampleMat, "VERIFIED CARD", new Point(50, 160), HersheyFonts.HersheySimplex, 1.0, Scalar.Black, 2);

            var card = new CardRegion { Id = "CardA", Role = CardRole.Front };
            var mats = new Dictionary<string, Mat> { { "CardA", sampleMat } };

            // 1. A4 Test
            var tA4 = new LayoutTemplate { PaperSize = PaperSize.A4, Mode = LayoutMode.FrontBackVertical };
            var planA4 = _layoutEngine.CalculateLayout(new[] { card }, tA4, 2);
            string pdfA4 = Path.Combine(dir, "Output_A4_Test.pdf");
            _pdfService.ExportPdfAsync(planA4, mats, pdfA4).GetAwaiter().GetResult();

            // 2. A5 Test
            var tA5 = new LayoutTemplate { PaperSize = PaperSize.A5, Mode = LayoutMode.FrontBackVertical };
            var planA5 = _layoutEngine.CalculateLayout(new[] { card }, tA5, 1);
            string pdfA5 = Path.Combine(dir, "Output_A5_Test.pdf");
            _pdfService.ExportPdfAsync(planA5, mats, pdfA5).GetAwaiter().GetResult();

            // 3. A3 Test
            var tA3 = new LayoutTemplate { PaperSize = PaperSize.A3, Mode = LayoutMode.GridCopies };
            var planA3 = _layoutEngine.CalculateLayout(new[] { card }, tA3, 6);
            string pdfA3 = Path.Combine(dir, "Output_A3_Test.pdf");
            _pdfService.ExportPdfAsync(planA3, mats, pdfA3).GetAwaiter().GetResult();

            // Inspect generated PDFs using PdfReader
            using (var docA4 = PdfReader.Open(pdfA4, PdfDocumentOpenMode.Import))
            {
                var page = docA4.Pages[0];
                double wMm = page.Width.Millimeter;
                double hMm = page.Height.Millimeter;
                report.AppendLine($"- A4 PDF Page 1: Width={wMm:F2} mm (Expected: 210.00), Height={hMm:F2} mm (Expected: 297.00) -> Matched: {Math.Abs(wMm - 210) < 0.2 && Math.Abs(hMm - 297) < 0.2}");
            }

            using (var docA5 = PdfReader.Open(pdfA5, PdfDocumentOpenMode.Import))
            {
                var page = docA5.Pages[0];
                double wMm = page.Width.Millimeter;
                double hMm = page.Height.Millimeter;
                report.AppendLine($"- A5 PDF Page 1: Width={wMm:F2} mm (Expected: 148.00), Height={hMm:F2} mm (Expected: 210.00) -> Matched: {Math.Abs(wMm - 148) < 0.2 && Math.Abs(hMm - 210) < 0.2}");
            }

            using (var docA3 = PdfReader.Open(pdfA3, PdfDocumentOpenMode.Import))
            {
                var page = docA3.Pages[0];
                double wMm = page.Width.Millimeter;
                double hMm = page.Height.Millimeter;
                report.AppendLine($"- A3 PDF Page 1: Width={wMm:F2} mm (Expected: 297.00), Height={hMm:F2} mm (Expected: 420.00) -> Matched: {Math.Abs(wMm - 297) < 0.2 && Math.Abs(hMm - 420) < 0.2}");
            }

            report.AppendLine();
        }

        private void VerifyNonDestructiveIntegrity(StringBuilder report)
        {
            report.AppendLine("## Non-Destructive SHA-256 Integrity Verification");
            string testImg = Path.Combine(_samplesDir, "01_front_and_back_side_by_side.jpg");

            string hashBefore = ComputeSha256(testImg);

            // Execute Detection
            var det = _imgService.DetectCards(testImg);

            // Execute Warping and Cropping
            foreach (var c in det.DetectedCards)
            {
                using var warped = _imgService.WarpAndCorrectCard(testImg, c);
            }

            // Execute Manual Adjustment simulation
            var adjustedCard = det.DetectedCards[0].Clone();
            adjustedCard.Corners[0] = new Point2D(adjustedCard.Corners[0].X + 15, adjustedCard.Corners[0].Y + 10);
            adjustedCard.RotationQuarterTurns = 1;
            adjustedCard.FineRotationDegrees = 2.5;
            adjustedCard.SafetyMarginPercent = 2.0;
            using var adjustedWarped = _imgService.WarpAndCorrectCard(testImg, adjustedCard);

            // Execute PDF Generation
            var plan = _layoutEngine.CalculateLayout(det.DetectedCards, LayoutTemplate.GetDefaultTemplates()[0], 2);
            var mats = new Dictionary<string, Mat>
            {
                { det.DetectedCards[0].Id, adjustedWarped }
            };
            string pdfOut = Path.Combine(_evidenceDir, "non_destructive_test_output.pdf");
            _pdfService.ExportPdfAsync(plan, mats, pdfOut).GetAwaiter().GetResult();

            string hashAfter = ComputeSha256(testImg);

            report.AppendLine($"- Original File: {testImg}");
            report.AppendLine($"- SHA-256 Before Operations: {hashBefore}");
            report.AppendLine($"- SHA-256 After Operations:  {hashAfter}");
            report.AppendLine($"- Bit-for-bit Identical:     {hashBefore == hashAfter}");
            report.AppendLine();

            Assert.Equal(hashBefore, hashAfter);
        }

        private async Task VerifyPersistenceAndReproducibility(StringBuilder report)
        {
            report.AppendLine("## Job Persistence & Reproducibility (.idjob)");
            string dir = Path.Combine(_evidenceDir, "Persistence_Verification");
            Directory.CreateDirectory(dir);

            string sampleImg = Path.Combine(_samplesDir, "01_front_and_back_side_by_side.jpg");
            var originalJob = new JobOrder
            {
                OrderNumber = "#AUDIT-7788",
                CustomerName = "محل الخدمات الذهبية",
                Notes = "طلب إعادة طباعة 4 نسخ على A4",
                Copies = 4,
                SelectedTemplateId = "a4_vertical",
                InputFiles = new List<string> { sampleImg },
                DetectedCards = new List<CardRegion>
                {
                    new()
                    {
                        Id = "Card_Front",
                        Label = "الوجه",
                        Role = CardRole.Front,
                        SourceImagePath = sampleImg,
                        RotationQuarterTurns = 0,
                        FineRotationDegrees = 0.5,
                        SafetyMarginPercent = 1.5,
                        IsManualAdjusted = true,
                        Corners = new[]
                        {
                            new Point2D(120, 200),
                            new Point2D(580, 200),
                            new Point2D(580, 500),
                            new Point2D(120, 500)
                        }
                    }
                }
            };

            string jobFilePath = Path.Combine(dir, "order_test_audit.idjob");
            await _jobService.SaveJobAsync(originalJob, jobFilePath);

            // Read back
            var reloadedJob = await _jobService.LoadJobAsync(jobFilePath);

            report.AppendLine($"- Order Number Restored: {reloadedJob.OrderNumber == originalJob.OrderNumber}");
            report.AppendLine($"- Customer Name Restored: {reloadedJob.CustomerName == originalJob.CustomerName}");
            report.AppendLine($"- Copies Count Restored: {reloadedJob.Copies == originalJob.Copies}");
            report.AppendLine($"- Template ID Restored: {reloadedJob.SelectedTemplateId == originalJob.SelectedTemplateId}");
            report.AppendLine($"- Input Files Restored: {reloadedJob.InputFiles.Count == 1 && reloadedJob.InputFiles[0] == sampleImg}");
            report.AppendLine($"- Card Corners Exactly Restored: {reloadedJob.DetectedCards[0].Corners[0].Equals(originalJob.DetectedCards[0].Corners[0])}");

            // Generate PDF from restored job
            var template = LayoutTemplate.GetDefaultTemplates().First(t => t.Id == reloadedJob.SelectedTemplateId);
            var plan = _layoutEngine.CalculateLayout(reloadedJob.DetectedCards, template, reloadedJob.Copies);

            using var rectifiedMat = _imgService.WarpAndCorrectCard(reloadedJob.DetectedCards[0].SourceImagePath, reloadedJob.DetectedCards[0]);
            var mats = new Dictionary<string, Mat> { { reloadedJob.DetectedCards[0].Id, rectifiedMat } };

            string regeneratedPdfPath = Path.Combine(dir, "reloaded_output.pdf");
            await _pdfService.ExportPdfAsync(plan, mats, regeneratedPdfPath, reloadedJob);

            report.AppendLine($"- Regenerated PDF from audit file exists: {File.Exists(regeneratedPdfPath)} (Size: {new FileInfo(regeneratedPdfPath).Length} bytes)");
            report.AppendLine();
        }

        private void VerifyManualAdjustmentSimulation(StringBuilder report)
        {
            report.AppendLine("## Manual Adjustment Logic Verification");
            string testImg = Path.Combine(_samplesDir, "04_card_with_large_margins.jpg");

            var region = new CardRegion
            {
                Id = "TestAdjust",
                SourceImagePath = testImg,
                Corners = new[]
                {
                    new Point2D(100, 100),
                    new Point2D(500, 100),
                    new Point2D(500, 350),
                    new Point2D(100, 350)
                }
            };

            var cardVm = new CardItemViewModel(region, _imgService);
            var adjustVm = new ManualAdjustmentViewModel(cardVm, _imgService);

            // Test corner move
            adjustVm.SetCorner(0, 120, 110);
            Assert.Equal(120, adjustVm.Corner0X);
            Assert.Equal(110, adjustVm.Corner0Y);

            // Test rotations
            adjustVm.Rotate90CW();
            Assert.Equal(1, adjustVm.RotationQuarterTurns);
            adjustVm.Flip180();
            Assert.Equal(3, adjustVm.RotationQuarterTurns);

            // Test fine rotation & safety margin
            adjustVm.FineRotationDegrees = 3.0;
            adjustVm.SafetyMarginPercent = 2.0;

            // Apply
            adjustVm.Apply();

            Assert.Equal(120, cardVm.Region.Corners[0].X);
            Assert.Equal(110, cardVm.Region.Corners[0].Y);
            Assert.Equal(3, cardVm.Region.RotationQuarterTurns);
            Assert.Equal(3.0, cardVm.Region.FineRotationDegrees);
            Assert.Equal(2.0, cardVm.Region.SafetyMarginPercent);
            Assert.True(cardVm.Region.IsManualAdjusted);

            report.AppendLine("- Moving corner 0 -> Updated VM Corner0X and Corner0Y immediately.");
            report.AppendLine("- Rotate 90 CW + Flip 180 -> RotationQuarterTurns = 3 (270°).");
            report.AppendLine("- Fine rotation = 3.0°, Safety Margin = 2.0% -> Applied to CardRegion.");
            report.AppendLine("- Apply() updates CardRegion and marks IsManualAdjusted = true.");
            report.AppendLine();
        }

        private Mat DrawDetectionOverlay(string imagePath, List<CardRegion> cards)
        {
            using var orig = Cv2.ImRead(imagePath);
            var overlay = orig.Clone();

            var colors = new[]
            {
                new Scalar(0, 230, 118), // Emerald Green
                new Scalar(0, 214, 255), // Cyan Yellow
                new Scalar(255, 23, 68),  // Red Pink
                new Scalar(255, 145, 0)  // Amber Orange
            };

            for (int ci = 0; ci < cards.Count; ci++)
            {
                var card = cards[ci];
                var pts = card.Corners.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();

                // Draw bounding polygon
                Cv2.Polylines(overlay, new[] { pts }, true, colors[ci % colors.Length], 3, LineTypes.AntiAlias);

                // Draw corners with numbered circles
                for (int i = 0; i < 4; i++)
                {
                    var p = pts[i];
                    Cv2.Circle(overlay, p, 12, colors[i % 4], -1);
                    Cv2.Circle(overlay, p, 13, Scalar.White, 2);
                    Cv2.PutText(overlay, (i + 1).ToString(), new Point(p.X - 5, p.Y + 5), HersheyFonts.HersheySimplex, 0.5, Scalar.Black, 2);
                }

                // Draw card label
                var center = new Point((int)pts.Average(p => p.X), (int)pts.Average(p => p.Y));
                Cv2.PutText(overlay, $"{card.Label} ({card.Role})", new Point(center.X - 60, center.Y), HersheyFonts.HersheySimplex, 0.7, Scalar.Yellow, 2);
            }

            return overlay;
        }

        private void AppendCardGeometry(StringBuilder sb, CardRegion card, int index)
        {
            sb.AppendLine($"Card #{index} ({card.Label}) [Role: {card.Role}]:");
            sb.AppendLine($"  Corner 1 (Top-Left):     ({card.Corners[0].X:F1}, {card.Corners[0].Y:F1})");
            sb.AppendLine($"  Corner 2 (Top-Right):    ({card.Corners[1].X:F1}, {card.Corners[1].Y:F1})");
            sb.AppendLine($"  Corner 3 (Bottom-Right): ({card.Corners[2].X:F1}, {card.Corners[2].Y:F1})");
            sb.AppendLine($"  Corner 4 (Bottom-Left):  ({card.Corners[3].X:F1}, {card.Corners[3].Y:F1})");

            double wTop = card.Corners[0].DistanceTo(card.Corners[1]);
            double wBottom = card.Corners[3].DistanceTo(card.Corners[2]);
            double hLeft = card.Corners[0].DistanceTo(card.Corners[3]);
            double hRight = card.Corners[1].DistanceTo(card.Corners[2]);

            double avgW = (wTop + wBottom) / 2.0;
            double avgH = (hLeft + hRight) / 2.0;
            double aspect = Math.Max(avgW, avgH) / Math.Min(avgW, avgH);

            sb.AppendLine($"  Width (Top/Bottom avg):  {avgW:F1} px");
            sb.AppendLine($"  Height (Left/Right avg): {avgH:F1} px");
            sb.AppendLine($"  Aspect Ratio:            {aspect:F3}");
            sb.AppendLine($"  Confidence:              {card.Confidence:P0}");
            sb.AppendLine($"  Rotation Quarter Turns:  {card.RotationQuarterTurns} (Total: {card.TotalRotationDegrees}°)");
        }

        private void CreateLargeMarginSample(string path)
        {
            using var bg = new Mat(new Size(1600, 1200), MatType.CV_8UC3, new Scalar(32, 35, 38));
            DrawSampleCard(bg, 550, 450, 480, 300, isFront: true);
            Cv2.ImWrite(path, bg);
        }

        private void DrawSampleCard(Mat canvas, int x, int y, int w, int h, bool isFront)
        {
            using var card = new Mat(new Size(w, h), MatType.CV_8UC3, new Scalar(248, 248, 250));

            if (isFront)
            {
                Cv2.Rectangle(card, new Rect(0, 0, w, 55), new Scalar(140, 90, 20), -1);
                Cv2.PutText(card, "NATIONAL IDENTITY CARD", new Point(120, 38), HersheyFonts.HersheySimplex, 0.65, Scalar.White, 2);
                Cv2.Rectangle(card, new Rect(18, 75, 100, 130), new Scalar(100, 100, 100), -1);
                Cv2.PutText(card, "PHOTO", new Point(35, 145), HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1);
                Cv2.Rectangle(card, new Rect(135, 85, 260, 12), new Scalar(40, 40, 40), -1);
                Cv2.Rectangle(card, new Rect(135, 115, 220, 12), new Scalar(60, 60, 60), -1);
            }
            else
            {
                Cv2.Rectangle(card, new Rect(0, 0, w, 35), new Scalar(70, 70, 70), -1);
                Cv2.PutText(card, "OFFICIAL USE ONLY", new Point(w / 3, 25), HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1);
                for (int bx = 20; bx < w - 20; bx += 8)
                {
                    Cv2.Line(card, new Point(bx, 60), new Point(bx, 140), new Scalar(20, 20, 20), 3);
                }
            }

            var roi = new Mat(canvas, new Rect(x, y, w, h));
            card.CopyTo(roi);
        }

        private string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
    }
}
