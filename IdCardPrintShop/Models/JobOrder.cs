using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public class JobOrder
    {
        [JsonPropertyName("orderNumber")]
        public string OrderNumber { get; set; } = $"#{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";

        [JsonPropertyName("customerName")]
        public string CustomerName { get; set; } = "عميل";

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("inputFiles")]
        public List<string> InputFiles { get; set; } = new();

        [JsonPropertyName("detectedCards")]
        public List<CardRegion> DetectedCards { get; set; } = new();

        [JsonPropertyName("selectedTemplateId")]
        public string SelectedTemplateId { get; set; } = "a4_vertical";

        [JsonPropertyName("copies")]
        public int Copies { get; set; } = 1;

        [JsonPropertyName("outputPdfPath")]
        public string OutputPdfPath { get; set; } = string.Empty;

        [JsonPropertyName("jobType")]
        public string JobType { get; set; } = "IdCard"; // "IdCard" or "PersonalPhoto"

        [JsonPropertyName("personalPhotos")]
        public List<PersonalPhotoItem> PersonalPhotos { get; set; } = new();

        [JsonPropertyName("documents")]
        public List<DocumentItem> Documents { get; set; } = new();

        [JsonPropertyName("customSizeItems")]
        public List<CustomLayoutItem> CustomSizeItems { get; set; } = new();

        [JsonPropertyName("customSizeParameters")]
        public CustomSizeLayoutParameters? CustomSizeParameters { get; set; }

        [JsonPropertyName("exportTimestamp")]
        public DateTime? ExportTimestamp { get; set; }
    }
}
