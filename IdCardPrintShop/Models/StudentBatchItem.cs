using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IdCardPrintShop.Models
{
    public enum StudentItemStatus
    {
        Pending = 0,
        Ready = 1,
        Warning = 2,
        Failed = 3,
        Processed = 4
    }

    public partial class StudentBatchItem : ObservableObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("originalFilePath")]
        public string OriginalFilePath { get; set; } = string.Empty;

        [JsonPropertyName("originalFileName")]
        public string OriginalFileName { get; set; } = string.Empty;

        [ObservableProperty]
        [property: JsonPropertyName("studentName")]
        private string _studentName = string.Empty;

        [ObservableProperty]
        [property: JsonPropertyName("status")]
        private StudentItemStatus _status = StudentItemStatus.Pending;

        [ObservableProperty]
        [property: JsonPropertyName("statusMessage")]
        private string _statusMessage = "قيد الفحص";

        [JsonPropertyName("widthPx")]
        public int WidthPx { get; set; }

        [JsonPropertyName("heightPx")]
        public int HeightPx { get; set; }

        [JsonPropertyName("dpiX")]
        public double DpiX { get; set; } = 300.0;

        [JsonPropertyName("dpiY")]
        public double DpiY { get; set; } = 300.0;

        [JsonPropertyName("aspectRatio")]
        public double AspectRatio => HeightPx > 0 ? (double)WidthPx / HeightPx : 0;

        [JsonPropertyName("fileSizeBytes")]
        public long FileSizeBytes { get; set; }

        [ObservableProperty]
        [property: JsonPropertyName("isSelected")]
        private bool _isSelected = true;

        [JsonPropertyName("issues")]
        public List<string> Issues { get; set; } = new();

        [JsonPropertyName("processedImagePath")]
        public string? ProcessedImagePath { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonIgnore]
        private BitmapSource? _thumbnail;

        [JsonIgnore]
        public BitmapSource? Thumbnail
        {
            get
            {
                if (_thumbnail == null && File.Exists(OriginalFilePath))
                {
                    _thumbnail = LoadSafeThumbnail(OriginalFilePath, 100);
                }
                return _thumbnail;
            }
            set => SetProperty(ref _thumbnail, value);
        }

        public StudentBatchItem() { }

        public StudentBatchItem(string filePath)
        {
            OriginalFilePath = filePath;
            OriginalFileName = Path.GetFileName(filePath);
            StudentName = ExtractStudentName(OriginalFileName);
            if (File.Exists(filePath))
            {
                FileSizeBytes = new FileInfo(filePath).Length;
            }
        }

        public static string ExtractStudentName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // Replace '_' and '-' with space
            var cleaned = nameWithoutExt.Replace('_', ' ').Replace('-', ' ');

            // Normalize multiple spaces into single space
            var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts).Trim();
        }

        public static BitmapSource? LoadSafeThumbnail(string path, int decodePixelWidth)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = decodePixelWidth;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
