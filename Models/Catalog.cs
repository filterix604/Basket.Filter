using Basket.Filter.Models.AIModels;
using Google.Cloud.Firestore;
using System.Text.Json.Serialization;

namespace Basket.Filter.Models
{
    [FirestoreData]
    public class CatalogItem
    {
        [FirestoreProperty]
        public string Sku { get; set; }

        [FirestoreProperty]
        public string Name { get; set; }

        [FirestoreProperty]
        public string Description { get; set; }

        [FirestoreProperty]
        public string NormalizedCategory { get; set; }

        [FirestoreProperty]
        public string OriginalCategory { get; set; }

        [FirestoreProperty]
        public string Brand { get; set; }

        [FirestoreProperty]
        public double Price { get; set; } // ← CHANGED FROM decimal TO double

        [FirestoreProperty]
        public bool ContainsAlcohol { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }

        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; }

        [FirestoreProperty("aiClassification")]
        public AIClassificationData? AIClassification { get; set; }

        // Additional metadata
        [FirestoreProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [FirestoreProperty("attributes")]
        public Dictionary<string, object> Attributes { get; set; } = new();

    }

    public class CatalogUploadRequest
    {
        public IFormFile File { get; set; }
    }

    public class CatalogUploadResponse
    {
        public int TotalItems { get; set; }
        public int SuccessfulItems { get; set; }
        public int FailedItems { get; set; }
        public List<string> Errors { get; set; } = new();
        public string Message { get; set; }
    }
}