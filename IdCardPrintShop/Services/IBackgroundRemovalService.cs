using System;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public interface IBackgroundRemovalService : IDisposable
    {
        /// <summary>
        /// Segments person foreground and blends with target background color.
        /// </summary>
        Mat RemoveAndReplaceBackground(
            Mat sourceMat,
            Rect? faceBox,
            PhotoBackgroundType bgType,
            byte customR = 255,
            byte customG = 255,
            byte customB = 255,
            double featherRadius = 2.0);

        /// <summary>
        /// Generates a single-channel alpha mask (0 to 255) for the person foreground.
        /// </summary>
        Mat GenerateAlphaMask(Mat sourceMat, Rect? faceBox, double featherRadius = 2.0);

        bool IsOnnxModelAvailable { get; }
        string ActiveEngineName { get; }
    }
}
