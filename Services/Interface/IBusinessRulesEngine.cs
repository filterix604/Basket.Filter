using Basket.Filter.Models.Rules;

namespace Basket.Filter.Services.Interface
{
    public interface IBusinessRulesEngine
    {
        Task<MerchantEligibilityRules> GenerateTemplateAsync(string merchantType, string countryCode);
        Task<BusinessRulesConfig> GetBusinessRulesAsync();
        bool ShouldAllowAlcoholInCombos(string merchantType, string countryCode);
        bool ShouldRequireTimeRestrictions(string merchantType, string countryCode);
        List<DayOfWeek> GetOperatingDays(string merchantType, string countryCode);
        List<CategoryRule> ApplyBusinessRulesToCategories(List<CategoryRule> baseRules, string merchantType, string countryCode);
    }
}
