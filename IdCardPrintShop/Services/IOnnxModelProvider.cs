using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public interface IOnnxModelProvider : IDisposable
    {
        bool IsModelLoaded { get; }
        string ModelName { get; }
        string ModelVersion { get; }
        string ModelLicense { get; }
        Mat? RunSegmentationInference(Mat inputMat);
    }

    /// <summary>
    /// Local ONNX model provider supporting Google MediaPipe Selfie Segmentation or U-2-Net / RMBG models.
    /// Fully offline, non-blocking, with graceful fallback if no onnx file is placed in Models/onnx/.
    /// </summary>
    public class LocalOnnxModelProvider : IOnnxModelProvider
    {
        private InferenceSession? _session;
        private readonly object _lock = new();

        public bool IsModelLoaded => _session != null;
        public string ModelName { get; private set; } = "MediaPipe Portrait Segmentation (Local)";
        public string ModelVersion { get; private set; } = "1.0-Offline";
        public string ModelLicense { get; private set; } = "Apache-2.0 / MIT";

        public LocalOnnxModelProvider(string? customModelPath = null)
        {
            TryLoadModel(customModelPath);
        }

        private void TryLoadModel(string? customPath)
        {
            lock (_lock)
            {
                string? modelFile = customPath;
                if (string.IsNullOrEmpty(modelFile) || !File.Exists(modelFile))
                {
                    var candidates = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "selfie_segmentation.onnx"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "onnx", "selfie_segmentation.onnx"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "selfie_segmentation.onnx")
                    };

                    foreach (var c in candidates)
                    {
                        var full = Path.GetFullPath(c);
                        if (File.Exists(full))
                        {
                            modelFile = full;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(modelFile) && File.Exists(modelFile))
                {
                    try
                    {
                        var options = new SessionOptions();
                        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                        _session = new InferenceSession(modelFile, options);
                    }
                    catch
                    {
                        _session = null;
                    }
                }
            }
        }

        public Mat? RunSegmentationInference(Mat inputMat)
        {
            // If model is loaded, run inference tensor. Otherwise returns null, cleanly falling back to OpenCV GrabCut.
            if (_session == null || inputMat == null || inputMat.Empty())
            {
                return null;
            }

            try
            {
                // MediaPipe square input 256x256 RGB normalized
                int targetSize = 256;
                using var resized = new Mat();
                Cv2.Resize(inputMat, resized, new Size(targetSize, targetSize));
                Cv2.CvtColor(resized, resized, ColorConversionCodes.BGR2RGB);

                var inputTensor = new DenseTensor<float>(new[] { 1, targetSize, targetSize, 3 });
                for (int y = 0; y < targetSize; y++)
                {
                    for (int x = 0; x < targetSize; x++)
                    {
                        var pixel = resized.At<Vec3b>(y, x);
                        inputTensor[0, y, x, 0] = pixel.Item0 / 255.0f;
                        inputTensor[0, y, x, 1] = pixel.Item1 / 255.0f;
                        inputTensor[0, y, x, 2] = pixel.Item2 / 255.0f;
                    }
                }

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_session.InputMetadata.Keys.First(), inputTensor)
                };

                using var results = _session.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();

                var maskSmall = new Mat(new Size(targetSize, targetSize), MatType.CV_8UC1);
                for (int y = 0; y < targetSize; y++)
                {
                    for (int x = 0; x < targetSize; x++)
                    {
                        float prob = outputTensor[0, y, x, 0];
                        maskSmall.Set(y, x, (byte)(Math.Clamp(prob, 0f, 1f) * 255));
                    }
                }

                var fullMask = new Mat();
                Cv2.Resize(maskSmall, fullMask, inputMat.Size(), 0, 0, InterpolationFlags.Cubic);
                maskSmall.Dispose();
                return fullMask;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _session?.Dispose();
                _session = null;
            }
        }
    }
}
