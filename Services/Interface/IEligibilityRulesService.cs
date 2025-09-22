using Basket.Filter.Models.Rules;

namespace Basket.Filter.Services.Interface
{
    public interface IEligibilityRulesService
    {
        Task<MerchantEligibilityRules> GetRulesForMerchantAsync(string merchantId);
        Task<CountryEligibilityRules> GetRulesForCountryAsync(string countryCode);
        Task<List<CategoryRule>> GetCategoryRulesAsync(string countryCode, string merchantType);
        Task UpdateMerchantRulesAsync(string merchantId, MerchantEligibilityRules rules);
        Task<bool> IsCategoryEligibleAsync(string category, string countryCode);
        Task<decimal> GetDailyLimitAsync(string countryCode);
        Task<bool> IsTimeRestrictedAsync(string countryCode, DateTime transactionTime);
    }
}