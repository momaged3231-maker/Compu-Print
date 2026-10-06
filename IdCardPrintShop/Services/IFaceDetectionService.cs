using System;
using System.Collections.Generic;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public class DetectedFace
    {
        public Rect Box { get; set; }
        public Point2D Center { get; set; }
        public double HeadTopY { get; set; }
        public double HeadHeight { get; set; }
        public double Confidence { get; set; }
    }

    public class FaceDetectionResult
    {
        public List<DetectedFace> Faces { get; set; } = new();
        public bool Success => Faces.Count > 0;
        public bool IsMultipleFaces => Faces.Count > 1;
        public string Message { get; set; } = string.Empty;
    }

    public interface IFaceDetectionService : IDisposable
    {
        FaceDetectionResult DetectFaces(Mat image);
        FaceDetectionResult DetectFaces(string imagePath);
    }
}
