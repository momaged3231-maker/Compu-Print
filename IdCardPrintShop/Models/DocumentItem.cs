using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public enum DocumentFileType
    {
        Pdf = 0,
        Image = 1,
        Docx = 2,
        Unknown = 3
    }

    public enum PrintColorMode
    {
        BlackAndWhite = 0,
        Color = 1
    }

    public enum DocumentOrientationMode
    {
        Auto = 0,
        Portrait = 1,
        Landscape = 2
    }

    public enum DocumentScalingMode
    {
        FitToPage = 0,
        ActualSize = 1,
        Fill = 2
    }

    public enum DocumentDuplexMode
    {
        None = 0,
        DuplexLongEdge = 1,
        DuplexShortEdge = 2
    }

    public enum DocumentItemStatus
    {
        Ready = 0,
        NeedsReview = 1,
        Failed = 2,
        Processing = 3
    }

    public class DocumentItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("originalPath")]
        public string OriginalPath { get; set; } = string.Empty;

        [JsonPropertyName("fileType")]
        public DocumentFileType FileType { get; set; } = DocumentFileType.Pdf;

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; } = 1;

        [JsonPropertyName("printMode")]
        public PrintColorMode PrintMode { get; set; } = PrintColorMode.BlackAndWhite;

        [JsonPropertyName("paperSizeName")]
        public string PaperSizeName { get; set; } = "A4";

        [JsonPropertyName("copies")]
        public int Copies { get; set; } = 1;

        [JsonPropertyName("orientation")]
        public DocumentOrientationMode Orientation { get; set; } = DocumentOrientationMode.Auto;

        [JsonPropertyName("scaling")]
        public DocumentScalingMode Scaling { get; set; } = DocumentScalingMode.FitToPage;

        [JsonPropertyName("duplex")]
        public DocumentDuplexMode Duplex { get; set; } = DocumentDuplexMode.None;

        [JsonPropertyName("pageRange")]
        public string PageRange { get; set; } = "All";

        [JsonPropertyName("status")]
        public DocumentItemStatus Status { get; set; } = DocumentItemStatus.Ready;

        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; } = "جاهز للطباعة";

        [JsonPropertyName("convertedPdfPath")]
        public string? ConvertedPdfPath { get; set; }

        [JsonPropertyName("resolvedPageIndices")]
        public List<int> ResolvedPageIndices { get; set; } = new();

        /// <summary>
        /// Total output pages = (number of resolved pages) * Copies
        /// </summary>
        [JsonIgnore]
        public int TotalOutputPages => (ResolvedPageIndices.Count > 0 ? ResolvedPageIndices.Count : PageCount) * Math.Max(1, Copies);

        public PaperSize GetPaperSize()
        {
            return PaperSizeName.ToUpperInvariant() switch
            {
                "A3" => PaperSize.A3,
                "A5" => PaperSize.A5,
                "LETTER" => PaperSize.Letter,
                _ => PaperSize.A4
            };
        }

        public PaperOrientation ResolveEffectiveOrientation(PaperOrientation docNativeOrientation = PaperOrientation.Portrait)
        {
            return Orientation switch
            {
                DocumentOrientationMode.Portrait => PaperOrientation.Portrait,
                DocumentOrientationMode.Landscape => PaperOrientation.Landscape,
                _ => docNativeOrientation
            };
        }

        public DocumentItem Clone()
        {
            return new DocumentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                FileName = FileName,
                OriginalPath = OriginalPath,
                FileType = FileType,
                PageCount = PageCount,
                PrintMode = PrintMode,
                PaperSizeName = PaperSizeName,
                Copies = Copies,
                Orientation = Orientation,
                Scaling = Scaling,
                Duplex = Duplex,
                PageRange = PageRange,
                Status = Status,
                StatusMessage = StatusMessage,
                ConvertedPdfPath = ConvertedPdfPath,
                ResolvedPageIndices = new List<int>(ResolvedPageIndices)
            };
        }
    }
}
