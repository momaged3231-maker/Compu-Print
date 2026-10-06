using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using OpenCvSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace IdCardPrintShop.Services
{
    public class PdfSharpExportService : IPdfExportService
    {
        public Task<string> ExportPdfAsync(
            LayoutPlan plan,
            Dictionary<string, Mat> rectifiedCardMats,
            string outputPath,
            JobOrder? orderInfo = null)
        {
            return Task.Run(() =>
            {
                if (plan.Items.Count == 0)
                {
                    throw new InvalidOperationException("لا توجد عناصر بطاقة في خطة التوزيع لتوليد الـ PDF.");
                }

                // Ensure target directory exists
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using var document = new PdfDocument();
                document.Info.Title = $"طباعة بطاقات - {orderInfo?.CustomerName ?? "طلب"} ({orderInfo?.OrderNumber ?? ""})";
                document.Info.Author = "ID Card Print Shop";
                document.Info.Subject = "Print-ready ID Cards Layout";

                var (paperW_mm, paperH_mm) = plan.SheetDimensions;

                // Pre-encode images to PNG byte arrays to avoid repeated encoding
                var encodedImages = new Dictionary<string, byte[]>();
                foreach (var kvp in rectifiedCardMats)
                {
                    if (kvp.Value != null && !kvp.Value.Empty())
                    {
                        Cv2.ImEncode(".png", kvp.Value, out var buf);
                        encodedImages[kvp.Key] = buf;
                    }
                }

                var hairlinePen = new XPen(XColor.FromArgb(180, 180, 180), 0.35); // Fine cut mark hairline
                var borderPen = new XPen(XColor.FromArgb(215, 215, 215), 0.35);

                for (int pageIdx = 0; pageIdx < plan.TotalPages; pageIdx++)
                {
                    var page = document.AddPage();
                    page.Width = XUnit.FromMillimeter(paperW_mm);
                    page.Height = XUnit.FromMillimeter(paperH_mm);

                    using var gfx = XGraphics.FromPdfPage(page);

                    var pageItems = plan.GetItemsForPage(pageIdx);

                    foreach (var item in pageItems)
                    {
                        byte[]? imgBytes = null;
                        if (!string.IsNullOrEmpty(item.CardRegionId) && encodedImages.TryGetValue(item.CardRegionId, out var b1))
                        {
                            imgBytes = b1;
                        }
                        else if (!string.IsNullOrEmpty(item.CustomItemId) && encodedImages.TryGetValue(item.CustomItemId, out var b2))
                        {
                            imgBytes = b2;
                        }
                        else if (!string.IsNullOrEmpty(item.SourceImagePath) && File.Exists(item.SourceImagePath))
                        {
                            try
                            {
                                using var mat = Cv2.ImRead(item.SourceImagePath);
                                if (mat != null && !mat.Empty())
                                {
                                    if (item.RotationDegrees == 90)
                                    {
                                        Cv2.Rotate(mat, mat, RotateFlags.Rotate90Clockwise);
                                    }
                                    Cv2.ImEncode(".png", mat, out var buf);
                                    imgBytes = buf;
                                }
                            }
                            catch { }
                        }

                        // Physical millimeter positions converted to PDF points
                        var xPt = XUnit.FromMillimeter(item.X_Mm);
                        var yPt = XUnit.FromMillimeter(item.Y_Mm);
                        var wPt = XUnit.FromMillimeter(item.Width_Mm);
                        var hPt = XUnit.FromMillimeter(item.Height_Mm);

                        if (imgBytes != null && imgBytes.Length > 0)
                        {
                            // Draw card image from memory stream
                            using (var ms = new MemoryStream(imgBytes))
                            using (var xImage = XImage.FromStream(ms))
                            {
                                gfx.DrawImage(xImage, xPt.Point, yPt.Point, wPt.Point, hPt.Point);
                            }
                        }
                        else
                        {
                            // Fallback placeholder box for items without image (or missing image)
                            var placeholderBrush = new XSolidBrush(XColor.FromArgb(245, 247, 250));
                            gfx.DrawRectangle(placeholderBrush, xPt.Point, yPt.Point, wPt.Point, hPt.Point);
                        }

                        // Draw faint border box around card if requested
                        if (plan.DrawBorderBox)
                        {
                            gfx.DrawRectangle(borderPen, xPt.Point, yPt.Point, wPt.Point, hPt.Point);
                        }

                        // Draw professional cut marks (علامات القص) at each corner
                        if (plan.ShowCutMarks)
                        {
                            DrawCutMarks(gfx, hairlinePen, item.X_Mm, item.Y_Mm, item.Width_Mm, item.Height_Mm);
                        }
                    }
                }

                document.Save(outputPath);
                return outputPath;
            });
        }

        private void DrawCutMarks(XGraphics gfx, XPen pen, double xMm, double yMm, double wMm, double hMm)
        {
            double tickLenMm = 3.0; // length of cutting line
            double gapMm = 0.8;    // distance from card boundary to avoid touching card edge

            // Millimeters to points conversion
            double ToPt(double mm) => XUnit.FromMillimeter(mm).Point;

            // Top-Left Corner
            gfx.DrawLine(pen, ToPt(xMm - gapMm - tickLenMm), ToPt(yMm), ToPt(xMm - gapMm), ToPt(yMm));
            gfx.DrawLine(pen, ToPt(xMm), ToPt(yMm - gapMm - tickLenMm), ToPt(xMm), ToPt(yMm - gapMm));

            // Top-Right Corner
            gfx.DrawLine(pen, ToPt(xMm + wMm + gapMm), ToPt(yMm), ToPt(xMm + wMm + gapMm + tickLenMm), ToPt(yMm));
            gfx.DrawLine(pen, ToPt(xMm + wMm), ToPt(yMm - gapMm - tickLenMm), ToPt(xMm + wMm), ToPt(yMm - gapMm));

            // Bottom-Left Corner
            gfx.DrawLine(pen, ToPt(xMm - gapMm - tickLenMm), ToPt(yMm + hMm), ToPt(xMm - gapMm), ToPt(yMm + hMm));
            gfx.DrawLine(pen, ToPt(xMm), ToPt(yMm + hMm + gapMm), ToPt(xMm), ToPt(yMm + hMm + gapMm + tickLenMm));

            // Bottom-Right Corner
            gfx.DrawLine(pen, ToPt(xMm + wMm + gapMm), ToPt(yMm + hMm), ToPt(xMm + wMm + gapMm + tickLenMm), ToPt(yMm + hMm));
            gfx.DrawLine(pen, ToPt(xMm + wMm), ToPt(yMm + hMm + gapMm), ToPt(xMm + wMm), ToPt(yMm + hMm + gapMm + tickLenMm));
        }
    }
}
