using Basket.Filter.Models.Rules;

namespace Basket.Filter.Services.Interface
{
    public interface IEligibilityRulesService
    {
        Task<MerchantEligibilityRules> GetRulesForMerchantAsync(string merchantId);
        Task<CountryEligibilityRules> GetRulesForCountryAsync(string countryCode);
    }
}