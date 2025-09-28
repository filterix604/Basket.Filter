namespace Basket.Filter.Models.AIModels
{
    public class AIClassificationRequest
    {
        public string ProductName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool ContainsAlcohol { get; set; }
        public double? AlcoholByVolume { get; set; }
        public List<string> Allergens { get; set; } = new();
        public string MerchantType { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
    }
}