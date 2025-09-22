namespace Basket.Filter.Models.Rules
{
    public class BusinessRulesConfig
    {
        public Dictionary<string, MerchantTypeRules> MerchantTypes { get; set; } = new();
        public Dictionary<string, CountrySpecificRules> Countries { get; set; } = new();
    }
}
