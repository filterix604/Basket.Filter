using Google.Cloud.Firestore;
using System.Text.Json.Serialization;

namespace Basket.Filter.Models.AIModels
{
    [FirestoreData]
    public class AIClassificationData
    {
        [FirestoreProperty("isEligible")]
        [JsonPropertyName("isEligible")]
        public bool IsEligible { get; set; }

        [FirestoreProperty("confidence")]
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [FirestoreProperty("reason")]
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [FirestoreProperty("source")]
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty; // "rules", "vertex_ai", "manual"

        [FirestoreProperty("modelVersion")]
        [JsonPropertyName("modelVersion")]
        public string ModelVersion { get; set; } = string.Empty;

        [FirestoreProperty("classifiedAt")]
        [JsonPropertyName("classifiedAt")]
        public DateTime ClassifiedAt { get; set; }

        [FirestoreProperty("metadata")]
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}