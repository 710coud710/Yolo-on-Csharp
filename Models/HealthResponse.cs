using System.Text.Json.Serialization;

namespace Client.Models
{
    public class HealthResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("database")]
        public bool Database { get; set; }

        [JsonPropertyName("ai_models")]
        public bool AiModels { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        public HealthResponse()
        {
            Status = string.Empty;
            Version = string.Empty;
        }
    }
}

