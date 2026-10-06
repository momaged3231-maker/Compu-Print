using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public class SchoolOrder
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("orderNumber")]
        public string OrderNumber { get; set; } = $"SCH-{DateTime.Now:yyyyMMdd-HHmmss}";

        [JsonPropertyName("schoolName")]
        public string SchoolName { get; set; } = "مدرسة / حضانة";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = string.Empty;

        [JsonPropertyName("templateId")]
        public string TemplateId { get; set; } = "tpl-8-4x6";

        [JsonPropertyName("templateName")]
        public string TemplateName { get; set; } = "8 صور 4×6 سم";

        [JsonPropertyName("studentCount")]
        public int StudentCount { get; set; }

        [JsonPropertyName("successCount")]
        public int SuccessCount { get; set; }

        [JsonPropertyName("warningCount")]
        public int WarningCount { get; set; }

        [JsonPropertyName("failedCount")]
        public int FailedCount { get; set; }

        [JsonPropertyName("pagesGenerated")]
        public int PagesGenerated { get; set; }

        [JsonPropertyName("outputFolder")]
        public string OutputFolder { get; set; } = string.Empty;

        [JsonPropertyName("combinedPdfPath")]
        public string CombinedPdfPath { get; set; } = string.Empty;

        [JsonPropertyName("generateIndividualPdfs")]
        public bool GenerateIndividualPdfs { get; set; } = false;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "مكتمل";

        [JsonPropertyName("students")]
        public List<StudentBatchItem> Students { get; set; } = new();
    }
}
