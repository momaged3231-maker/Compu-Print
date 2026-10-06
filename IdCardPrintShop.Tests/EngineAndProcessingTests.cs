using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using OpenCvSharp;
using Xunit;

namespace IdCardPrintShop.Tests
{
    public class EngineAndProcessingTests : IDisposable
    {
        private readonly OpenCvImageProcessingService _imageService = new();
        private readonly LayoutEngine _layoutEngine = new();
        private readonly PdfSharpExportService _pdfService = new();
        private readonly JobPersistenceService _jobService = new();
        private readonly string _testOutputDir;

        public EngineAndProcessingTests()
        {
            _testOutputDir = Path.Combine(Path.GetTempPath(), "IdCardPrintShopTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testOutputDir);
        }

        public void Dispose()
        {
            _imageService.Dispose();
            if (Directory.Exists(_testOutputDir))
            {
                try { Directory.Delete(_testOutputDir, true); } catch { }
            }
        }

        /// <summary>
        /// Helper to generate a realistic synthetic ID card on a textured/contrasting background
        /// </summary>
        private Mat CreateSyntheticCardImage(int imgW, int imgH, (int x, int y, int w, int h)[] cardPlacements, double[]? angles = null)
        {
            // Background (e.g., dark desk/table surface)
            var bg = new Mat(new Size(imgW, imgH), MatType.CV_8UC3, new Scalar(20, 20, 20));

            for (int i = 0; i < cardPlacements.Length; i++)
            {
                var (cx, cy, cw, ch) = cardPlacements[i];
                double angle = angles != null && i < angles.Length ? angles[i] : 0;

                // Create the card itself: white surface with border and details
                using var card = new Mat(new Size(cw, ch), MatType.CV_8UC3, new Scalar(245, 245, 245));
                // Header bar (Cyan/Green with high brightness)
                Cv2.Rectangle(card, new Rect(0, 0, cw, ch / 4), new Scalar(120, 180, 200), -1);
                // Photo box
                Cv2.Rectangle(card, new Rect(15, ch / 3, cw / 4, ch / 2), new Scalar(100, 100, 100), -1);
                // Lines of text
                Cv2.Rectangle(card, new Rect(cw / 3 + 10, ch / 3, cw / 2, 8), new Scalar(30, 30, 30), -1);
                Cv2.Rectangle(card, new Rect(cw / 3 + 10, ch / 3 + 18, cw / 2 - 20, 8), new Scalar(50, 50, 50), -1);
                Cv2.Rectangle(card, new Rect(cw / 3 + 10, ch / 3 + 36, cw / 3, 8), new Scalar(70, 70, 70), -1);

                if (Math.Abs(angle) < 0.1)
                {
                    // Direct blit
                    var roi = new Mat(bg, new Rect(cx, cy, cw, ch));
                    card.CopyTo(roi);
                }
                else
                {
                    // Rotate and warp into background
                    using var rotMat = Cv2.GetRotationMatrix2D(new Point2f(cw / 2f, ch / 2f), angle, 1.0);
                    using var rotatedCard = new Mat();
                    Cv2.WarpAffine(card, rotatedCard, rotMat, card.Size(), InterpolationFlags.Cubic, BorderTypes.Constant, new Scalar(45, 52, 54));
                    var roi = new Mat(bg, new Rect(cx, cy, cw, ch));
                    rotatedCard.CopyTo(roi);
                }
            }

            return bg;
        }

        [Fact]
        public void Test1_SingleCardDetection_WithLargeMargins()
        {
            // Test 1 & Test 7: Single card in image with large whitespace / background margins
            int imgW = 1200, imgH = 900;
            int cardW = 380, cardH = 240; // aspect ~1.58
            int cardX = 410, cardY = 330;

            using var mat = CreateSyntheticCardImage(imgW, imgH, new[] { (cardX, cardY, cardW, cardH) });
            var result = _imageService.DetectCards(mat);

            Assert.True(result.IsConfident);
            Assert.Equal(DetectionCase.SingleCard, result.Case);
            Assert.Single(result.DetectedCards);

            var detected = result.DetectedCards[0];
            var corners = detected.Corners;
            Assert.Equal(4, corners.Length);

            // Verify corners closely match the placed card
            double minX = corners.Min(p => p.X);
            double maxX = corners.Max(p => p.X);
            double minY = corners.Min(p => p.Y);
            double maxY = corners.Max(p => p.Y);

            Assert.InRange(minX, cardX - 25, cardX + 25);
            Assert.InRange(maxX, cardX + cardW - 25, cardX + cardW + 25);
            Assert.InRange(minY, cardY - 25, cardY + 25);
            Assert.InRange(maxY, cardY + cardH - 25, cardY + cardH + 25);
        }

        [Fact]
        public void Test3_TwoCardsSideBySide()
        {
            // Test 3: Front + Back side by side in single image
            int imgW = 1400, imgH = 800;
            int cardW = 380, cardH = 240;

            using var mat = CreateSyntheticCardImage(imgW, imgH, new[]
            {
                (150, 280, cardW, cardH),
                (800, 280, cardW, cardH)
            });

            var result = _imageService.DetectCards(mat);

            Assert.True(result.IsConfident);
            Assert.Equal(DetectionCase.TwoCards, result.Case);
            Assert.Equal(2, result.DetectedCards.Count);
        }

        [Fact]
        public void Test4_TwoCardsStackedVertically()
        {
            // Test 4: Front + Back stacked vertically
            int imgW = 800, imgH = 1200;
            int cardW = 380, cardH = 240;

            using var mat = CreateSyntheticCardImage(imgW, imgH, new[]
            {
                (210, 150, cardW, cardH),
                (210, 650, cardW, cardH)
            });

            var result = _imageService.DetectCards(mat);

            Assert.True(result.IsConfident);
            Assert.Equal(DetectionCase.TwoCards, result.Case);
            Assert.Equal(2, result.DetectedCards.Count);
        }

        [Fact]
        public void Test6_PerspectiveCorrection_WarpsToRectangle()
        {
            // Test 6: Angled/skewed card -> 4-point perspective warp rectifies card to upright rectangle
            int imgW = 800, imgH = 600;
            using var bg = new Mat(new Size(imgW, imgH), MatType.CV_8UC3, new Scalar(30, 30, 30));

            // Create a quad with realistic perspective tilt
            var region = new CardRegion
            {
                Id = "TestCard",
                Corners = new[]
                {
                    new Point2D(120, 110), // TL tilted
                    new Point2D(530, 80),  // TR tilted up
                    new Point2D(500, 370), // BR
                    new Point2D(90, 340)   // BL
                },
                SafetyMarginPercent = 0.0
            };

            using var rectified = _imageService.WarpAndCorrectCard(bg, region);

            Assert.NotNull(rectified);
            Assert.False(rectified.Empty());
            Assert.True(rectified.Width > 300);
            Assert.True(rectified.Height > 200);
        }

        [Fact]
        public void Test8_ManualAdjustment_Override()
        {
            // Test 8: Worker can override corners manually without re-importing
            var region = new CardRegion
            {
                Id = "ManualCard",
                Corners = new[]
                {
                    new Point2D(10, 10),
                    new Point2D(100, 10),
                    new Point2D(100, 70),
                    new Point2D(10, 70)
                }
            };

            // Worker adjusts top right corner
            region.Corners[1] = new Point2D(115, 12);
            region.IsManualAdjusted = true;
            region.RotationQuarterTurns = 1; // 90 degrees

            Assert.True(region.IsManualAdjusted);
            Assert.Equal(90, region.TotalRotationDegrees);
            Assert.Equal(115, region.Corners[1].X);
        }

        [Fact]
        public void Test9_LayoutEngine_CalculatesMultipleCopies()
        {
            // Test 9: Multiple Copies are automatically packed by layout engine
            var cards = new List<CardRegion>
            {
                new() { Id = "CardA", Role = CardRole.Front },
                new() { Id = "CardB", Role = CardRole.Back }
            };

            var template = LayoutTemplate.GetDefaultTemplates().First(t => t.Id == "a4_vertical");

            // Request 3 copies
            var plan = _layoutEngine.CalculateLayout(cards, template, 3);

            Assert.NotNull(plan);
            // 3 copies of front + back = 6 card items
            Assert.Equal(6, plan.Items.Count);
            Assert.Equal(3, plan.Items.Count(i => i.Role == CardRole.Front));
            Assert.Equal(3, plan.Items.Count(i => i.Role == CardRole.Back));
        }

        [Fact]
        public void Test10_LayoutEngine_AutomaticPagination()
        {
            // Test 10: High copy count exceeds 1 sheet capacity -> Automatic pagination
            var cards = new List<CardRegion>
            {
                new() { Id = "CardA", Role = CardRole.Front },
                new() { Id = "CardB", Role = CardRole.Back }
            };

            var template = LayoutTemplate.GetDefaultTemplates().First(t => t.Id == "a4_vertical");

            // Request 12 copies (cannot fit on a single A4 page with standard card dimensions)
            var plan = _layoutEngine.CalculateLayout(cards, template, 12);

            Assert.True(plan.TotalPages > 1);
            Assert.Equal(24, plan.Items.Count);

            // Verify items exist on page 0 and page 1
            var page0Items = plan.GetItemsForPage(0);
            var page1Items = plan.GetItemsForPage(1);

            Assert.NotEmpty(page0Items);
            Assert.NotEmpty(page1Items);

            // All items must be within paper boundaries (Section 19: عدم السماح بوضع عنصر خارج حدود الورقة)
            var (paperW, paperH) = template.PaperSize.GetDimensions(template.Orientation);
            foreach (var item in plan.Items)
            {
                Assert.True(item.X_Mm >= 0);
                Assert.True(item.Y_Mm >= 0);
                Assert.True(item.X_Mm + item.Width_Mm <= paperW + 0.1);
                Assert.True(item.Y_Mm + item.Height_Mm <= paperH + 0.1);
            }
        }

        [Fact]
        public async Task Test11_PdfExportService_GeneratesPrintReadyPdf()
        {
            // Test 11: Generates vector, physical mm PDF matching the LayoutPlan
            var cards = new List<CardRegion>
            {
                new() { Id = "Card1", Role = CardRole.Front },
                new() { Id = "Card2", Role = CardRole.Back }
            };

            var template = LayoutTemplate.GetDefaultTemplates().First(t => t.Id == "a4_vertical");
            var plan = _layoutEngine.CalculateLayout(cards, template, 1);

            // Create synthetic rectified mats for the cards
            using var frontMat = new Mat(new Size(500, 315), MatType.CV_8UC3, new Scalar(255, 200, 200));
            using var backMat = new Mat(new Size(500, 315), MatType.CV_8UC3, new Scalar(200, 255, 200));

            var mats = new Dictionary<string, Mat>
            {
                { "Card1", frontMat },
                { "Card2", backMat }
            };

            string pdfPath = Path.Combine(_testOutputDir, "Output_Test11.pdf");

            var order = new JobOrder
            {
                OrderNumber = "#1001",
                CustomerName = "محمد علي"
            };

            var resultPath = await _pdfService.ExportPdfAsync(plan, mats, pdfPath, order);

            Assert.True(File.Exists(resultPath));
            var fileInfo = new FileInfo(resultPath);
            Assert.True(fileInfo.Length > 1000); // Valid PDF with embedded images
        }

        [Fact]
        public async Task Test12_JobPersistence_SaveAndReload()
        {
            // Test 12: Audit & Reproducibility - Save and reload job JSON preserves all parameters
            var job = new JobOrder
            {
                OrderNumber = "#2045",
                CustomerName = "أحمد إبراهيم",
                Copies = 4,
                SelectedTemplateId = "a4_vertical",
                InputFiles = new List<string> { @"C:\Photos\id.jpg" },
                DetectedCards = new List<CardRegion>
                {
                    new()
                    {
                        Id = "CardA",
                        Role = CardRole.Front,
                        RotationQuarterTurns = 2,
                        Corners = new[]
                        {
                            new Point2D(10, 20),
                            new Point2D(310, 25),
                            new Point2D(305, 190),
                            new Point2D(8, 185)
                        }
                    }
                }
            };

            string jsonPath = Path.Combine(_testOutputDir, "Order_2045.idjob");
            await _jobService.SaveJobAsync(job, jsonPath);

            Assert.True(File.Exists(jsonPath));

            var loadedJob = await _jobService.LoadJobAsync(jsonPath);

            Assert.Equal(job.OrderNumber, loadedJob.OrderNumber);
            Assert.Equal(job.CustomerName, loadedJob.CustomerName);
            Assert.Equal(job.Copies, loadedJob.Copies);
            Assert.Single(loadedJob.DetectedCards);
            Assert.Equal(CardRole.Front, loadedJob.DetectedCards[0].Role);
            Assert.Equal(2, loadedJob.DetectedCards[0].RotationQuarterTurns);
            Assert.Equal(10, loadedJob.DetectedCards[0].Corners[0].X);
        }
    }
}
