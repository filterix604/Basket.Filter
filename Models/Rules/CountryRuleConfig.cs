using Basket.Filter.Models.Rules;

namespace Basket.Filter.Models.Rules
{
    public class CountryRuleConfig
    {
        public string CountryCode { get; set; }
        public string CountryName { get; set; }
        public double DefaultDailyLimit { get; set; }
        public string RegulatoryFramework { get; set; }
        public bool RequiresEmployeeValidation { get; set; }
        public TimeRestrictionsConfig DefaultTimeRestrictions { get; set; }
        public List<string> DefaultAllowedDays { get; set; }
        public List<CategoryRuleConfig> CategoryRules { get; set; } = new();
    }
}
