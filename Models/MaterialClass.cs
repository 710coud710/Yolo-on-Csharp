using System.Text.Json.Serialization;

namespace Client.Models
{
    public class MaterialClass
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("label_id")]
        public int LabelId { get; set; }

        [JsonPropertyName("material_code")]
        public string MaterialCode { get; set; }

        [JsonPropertyName("material_name")]
        public string MaterialName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        public MaterialClass()
        {
            Label = string.Empty;
            MaterialCode = string.Empty;
            MaterialName = string.Empty;
            Description = string.Empty;
        }
    }
}
