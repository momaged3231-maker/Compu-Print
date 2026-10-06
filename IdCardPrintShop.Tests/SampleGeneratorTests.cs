using System;
using System.IO;
using IdCardPrintShop.Models;
using IdCardPrintShop.Services;
using OpenCvSharp;
using Xunit;

namespace IdCardPrintShop.Tests
{
    public class SampleGeneratorTests
    {
        [Fact]
        public async Task GenerateSampleShopImages()
        {
            string samplesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Samples");
            samplesDir = Path.GetFullPath(samplesDir);
            Directory.CreateDirectory(samplesDir);

            // 1. Front and Back side-by-side
            using (var mat1 = CreateTwoCardMat(1600, 1000, isStacked: false))
            {
                Cv2.ImWrite(Path.Combine(samplesDir, "01_front_and_back_side_by_side.jpg"), mat1);
            }

            // 2. Front and Back stacked vertically
            using (var mat2 = CreateTwoCardMat(1000, 1500, isStacked: true))
            {
                Cv2.ImWrite(Path.Combine(samplesDir, "02_front_and_back_stacked.jpg"), mat2);
            }

            // 3. Skewed / Angled Card with Perspective distortion
            using (var mat3 = CreateSkewedCardMat(1400, 1000))
            {
                Cv2.ImWrite(Path.Combine(samplesDir, "03_skewed_perspective_card.jpg"), mat3);
            }

            // 4. Card with large background margins
            using (var mat4 = CreateSingleCardLargeMargin(1600, 1200))
            {
                Cv2.ImWrite(Path.Combine(samplesDir, "04_card_with_large_margins.jpg"), mat4);
            }

            Assert.True(File.Exists(Path.Combine(samplesDir, "01_front_and_back_side_by_side.jpg")));
            Assert.True(File.Exists(Path.Combine(samplesDir, "02_front_and_back_stacked.jpg")));
            Assert.True(File.Exists(Path.Combine(samplesDir, "03_skewed_perspective_card.jpg")));
            Assert.True(File.Exists(Path.Combine(samplesDir, "04_card_with_large_margins.jpg")));

            // End-to-End Pipeline Verification:
            // 1. Process 01_front_and_back_side_by_side.jpg -> Detect Front + Back -> Layout A4 Vertical -> Export PDF
            using var imgService = new OpenCvImageProcessingService();
            var layoutEngine = new LayoutEngine();
            var pdfService = new PdfSharpExportService();

            var det1 = imgService.DetectCards(Path.Combine(samplesDir, "01_front_and_back_side_by_side.jpg"));
            Assert.Equal(DetectionCase.TwoCards, det1.Case);
            Assert.Equal(2, det1.DetectedCards.Count);

            var template = LayoutTemplate.GetDefaultTemplates()[0]; // A4 Vertical
            var plan = layoutEngine.CalculateLayout(det1.DetectedCards, template, copies: 1);

            using var matFront = imgService.WarpAndCorrectCard(Path.Combine(samplesDir, "01_front_and_back_side_by_side.jpg"), det1.DetectedCards[0]);
            using var matBack = imgService.WarpAndCorrectCard(Path.Combine(samplesDir, "01_front_and_back_side_by_side.jpg"), det1.DetectedCards[1]);

            var mats = new System.Collections.Generic.Dictionary<string, Mat>
            {
                { det1.DetectedCards[0].Id, matFront },
                { det1.DetectedCards[1].Id, matBack }
            };

            string outputPdfPath = Path.Combine(samplesDir, "Sample_Print_A4_Output.pdf");
            await pdfService.ExportPdfAsync(plan, mats, outputPdfPath);

            Assert.True(File.Exists(outputPdfPath));
            Assert.True(new FileInfo(outputPdfPath).Length > 2000);
        }

        private Mat CreateTwoCardMat(int imgW, int imgH, bool isStacked)
        {
            var bg = new Mat(new Size(imgW, imgH), MatType.CV_8UC3, new Scalar(28, 28, 30));
            int cw = 460, ch = 290;

            int x1, y1, x2, y2;
            if (isStacked)
            {
                x1 = (imgW - cw) / 2;
                y1 = 120;
                x2 = (imgW - cw) / 2;
                y2 = y1 + ch + 150;
            }
            else
            {
                int totalW = cw * 2 + 180;
                x1 = (imgW - totalW) / 2;
                y1 = (imgH - ch) / 2;
                x2 = x1 + cw + 180;
                y2 = (imgH - ch) / 2;
            }

            DrawSampleCard(bg, x1, y1, cw, ch, isFront: true);
            DrawSampleCard(bg, x2, y2, cw, ch, isFront: false);

            return bg;
        }

        private Mat CreateSkewedCardMat(int imgW, int imgH)
        {
            var bg = new Mat(new Size(imgW, imgH), MatType.CV_8UC3, new Scalar(25, 25, 27));
            int cw = 500, ch = 315;

            using var card = new Mat(new Size(cw, ch), MatType.CV_8UC3, new Scalar(250, 250, 250));
            // Card design
            Cv2.Rectangle(card, new Rect(0, 0, cw, 60), new Scalar(160, 80, 20), -1);
            Cv2.Rectangle(card, new Rect(20, 80, 110, 140), new Scalar(120, 120, 120), -1);
            Cv2.Rectangle(card, new Rect(150, 90, 280, 14), new Scalar(30, 30, 30), -1);
            Cv2.Rectangle(card, new Rect(150, 120, 240, 14), new Scalar(50, 50, 50), -1);
            Cv2.Rectangle(card, new Rect(150, 150, 200, 14), new Scalar(50, 50, 50), -1);
            Cv2.PutText(card, "NATIONAL IDENTITY CARD", new Point(150, 42), HersheyFonts.HersheySimplex, 0.7, Scalar.White, 2);

            // Define perspective warp points
            var src = new Point2f[]
            {
                new(0, 0), new(cw, 0), new(cw, ch), new(0, ch)
            };
            var dst = new Point2f[]
            {
                new(280, 220), // TL
                new(880, 160), // TR tilted up
                new(820, 680), // BR
                new(210, 620)  // BL
            };

            using var M = Cv2.GetPerspectiveTransform(src, dst);
            Cv2.WarpPerspective(card, bg, M, bg.Size(), InterpolationFlags.Cubic, BorderTypes.Transparent);

            return bg;
        }

        private Mat CreateSingleCardLargeMargin(int imgW, int imgH)
        {
            var bg = new Mat(new Size(imgW, imgH), MatType.CV_8UC3, new Scalar(32, 35, 38));
            int cw = 480, ch = 300;
            int x = (imgW - cw) / 2 + 50;
            int y = (imgH - ch) / 2 - 30;

            DrawSampleCard(bg, x, y, cw, ch, isFront: true);
            return bg;
        }

        private void DrawSampleCard(Mat canvas, int x, int y, int w, int h, bool isFront)
        {
            using var card = new Mat(new Size(w, h), MatType.CV_8UC3, new Scalar(248, 248, 250));

            if (isFront)
            {
                // Header (Gold/Teal)
                Cv2.Rectangle(card, new Rect(0, 0, w, 55), new Scalar(140, 90, 20), -1);
                Cv2.PutText(card, "REPUBLIC IDENTITY CARD", new Point(140, 38), HersheyFonts.HersheySimplex, 0.65, Scalar.White, 2);
                // Photo
                Cv2.Rectangle(card, new Rect(18, 75, 100, 130), new Scalar(100, 100, 100), -1);
                Cv2.PutText(card, "PHOTO", new Point(35, 145), HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1);
                // Fields
                Cv2.Rectangle(card, new Rect(135, 85, 260, 12), new Scalar(40, 40, 40), -1);
                Cv2.Rectangle(card, new Rect(135, 115, 220, 12), new Scalar(60, 60, 60), -1);
                Cv2.Rectangle(card, new Rect(135, 145, 180, 12), new Scalar(60, 60, 60), -1);
                Cv2.Rectangle(card, new Rect(135, 175, 240, 12), new Scalar(80, 80, 80), -1);
            }
            else
            {
                // Back side with barcode and fingerprint
                Cv2.Rectangle(card, new Rect(0, 0, w, 35), new Scalar(70, 70, 70), -1);
                Cv2.PutText(card, "OFFICIAL USE ONLY", new Point(w / 3, 25), HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1);
                // Barcode simulation
                for (int bx = 20; bx < w - 20; bx += 8)
                {
                    Cv2.Line(card, new Point(bx, 60), new Point(bx, 140), new Scalar(20, 20, 20), 3);
                }
                Cv2.Rectangle(card, new Rect(20, 160, w - 40, 30), new Scalar(220, 220, 220), -1);
                Cv2.PutText(card, "ID NO: 2980101012345678", new Point(40, 182), HersheyFonts.HersheySimplex, 0.55, Scalar.Black, 2);
            }

            var roi = new Mat(canvas, new Rect(x, y, w, h));
            card.CopyTo(roi);
        }
    }
}
