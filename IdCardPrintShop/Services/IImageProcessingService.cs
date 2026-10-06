using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public class DetectionResult
    {
        public List<CardRegion> DetectedCards { get; set; } = new();
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsConfident { get; set; } = true;
        public DetectionCase Case { get; set; } = DetectionCase.SingleCard;
    }

    public enum DetectionCase
    {
        SingleCard,       // بطاقة واحدة
        TwoCards,         // بطاقتان (وجه وظهر)
        MultipleCards,    // أكثر من بطاقتين
        LowConfidence     // لم يتم اكتشاف بطاقة بشكل موثوق
    }

    public interface IImageProcessingService : IDisposable
    {
        DetectionResult DetectCards(string imagePath);
        DetectionResult DetectCards(Mat sourceMat, string sourceImagePath = "");
        Mat WarpAndCorrectCard(string imagePath, CardRegion region, double? targetAspectRatio = 85.60 / 54.00);
        Mat WarpAndCorrectCard(Mat sourceMat, CardRegion region, double? targetAspectRatio = 85.60 / 54.00);
        BitmapSource MatToBitmapSource(Mat mat);
        byte[] MatToPngBytes(Mat mat);
        Mat LoadMat(string imagePath);
        Point2D[] GetFallbackCardCorners(int imageWidth, int imageHeight);
    }
}
