namespace Basket.Filter.Models.Rules
{
    public class CategoryRuleConfig
    {
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public bool IsEligible { get; set; }
        public List<string> Keywords { get; set; } = new();
        public List<string> ExcludedKeywords { get; set; } = new();
        public string Description { get; set; }
        public string EligibilityReason { get; set; }
        public double? MaxAlcoholPercentage { get; set; }
        public bool RequiresAccompanyingFood { get; set; }
        public bool RequiresImmediateConsumption { get; set; }
    }
}
