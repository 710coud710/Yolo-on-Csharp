using System.Text.Json.Serialization;

namespace Client.Models
{
    public class DetectResponse
    {
        [JsonPropertyName("image_id")]
        public int ImageId { get; set; }

        [JsonPropertyName("machine_name")]
        public string MachineName { get; set; }

        [JsonPropertyName("total_detections")]
        public int TotalDetections { get; set; }

        [JsonPropertyName("processing_time_ms")]
        public double ProcessingTimeMs { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("input_url")]
        public string InputUrl { get; set; }

        [JsonPropertyName("output_url")]
        public string OutputUrl { get; set; }

        [JsonPropertyName("input_image_url")]
        public string InputImageUrl { get; set; }

        [JsonPropertyName("output_image_url")]
        public string OutputImageUrl { get; set; }

        [JsonPropertyName("detections")]
        public List<DetectItem> Detections { get; set; }

        [JsonIgnore]
        public string EffectiveOutputImageUrl =>
            !string.IsNullOrWhiteSpace(OutputImageUrl) ? OutputImageUrl :
            !string.IsNullOrWhiteSpace(OutputUrl) ? OutputUrl :
            string.Empty;

        public DetectResponse()
        {
            MachineName = string.Empty;
            InputUrl = string.Empty;
            OutputUrl = string.Empty;
            InputImageUrl = string.Empty;
            OutputImageUrl = string.Empty;
            Detections = new List<DetectItem>();
        }
    }

    public class DetectItem
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("x1")]
        public double X1 { get; set; }

        [JsonPropertyName("y1")]
        public double Y1 { get; set; }

        [JsonPropertyName("x2")]
        public double X2 { get; set; }

        [JsonPropertyName("y2")]
        public double Y2 { get; set; }
    }
}

