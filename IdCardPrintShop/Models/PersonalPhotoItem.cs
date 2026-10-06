using System;
using System.Text.Json.Serialization;

namespace IdCardPrintShop.Models
{
    public enum PersonalPhotoStatus
    {
        Imported = 0,
        Processing = 1,
        Ready = 2,
        NeedsReview = 3,
        Failed = 4
    }

    public class PersonalPhotoItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        [JsonPropertyName("sourceImagePath")]
        public string SourceImagePath { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "صورة";

        [JsonPropertyName("status")]
        public PersonalPhotoStatus Status { get; set; } = PersonalPhotoStatus.Imported;

        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; } = "تم الاستيراد";

        [JsonPropertyName("profile")]
        public PhotoProfile Profile { get; set; } = PhotoProfile.Standard4x6;

        [JsonPropertyName("parameters")]
        public PersonalPhotoParameters Parameters { get; set; } = new();

        [JsonPropertyName("copies")]
        public int Copies { get; set; } = 8;
    }
}
