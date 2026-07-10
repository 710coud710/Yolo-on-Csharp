using System;
using System.Text.Json.Serialization;

namespace Client.Models
{
    public class ModelClass
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("model_code")]
        public string ModelCode { get; set; }

        [JsonPropertyName("model_name")]
        public string ModelName { get; set; }

        [JsonPropertyName("model_path")]
        public string ModelPath { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        public ModelClass()
        {
            ModelCode = string.Empty;
            ModelName = string.Empty;
            ModelPath = string.Empty;
            Description = string.Empty;
        }
    }
}
