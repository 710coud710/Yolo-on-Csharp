using System.Text.Json.Serialization;

namespace Client.Models
{
    public class HistoryLog
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("machine_name")]
        public string MachineName { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [JsonPropertyName("item_name")]
        public string ItemName { get; set; } = string.Empty;

        [JsonPropertyName("total_objects")]
        public int TotalObjects { get; set; }

        [JsonPropertyName("image_path")]
        public string? ImagePath { get; set; }

        [JsonPropertyName("result_image_path")]
        public string? ResultImagePath { get; set; }
    }
}

