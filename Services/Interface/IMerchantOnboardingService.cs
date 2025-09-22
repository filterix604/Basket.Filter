using Basket.Filter.Models;
using Basket.Filter.Models.Rules;

namespace Basket.Filter.Services.Interface
{
    public interface IMerchantOnboardingService
    {
        Task<MerchantEligibilityRules> OnboardMerchantAsync(MerchantOnboardingRequest request);
    }
}
