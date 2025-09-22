using Basket.Filter.Models.Rules;
using System.ComponentModel.DataAnnotations;

namespace Basket.Filter.Models
{
    public class MerchantOnboardingRequest
    {
        [Required(ErrorMessage = "MerchantId is required")]
        public string MerchantId { get; set; }

        [Required(ErrorMessage = "MerchantName is required")]
        public string MerchantName { get; set; }

        [Required(ErrorMessage = "MerchantType is required")]
        public string MerchantType { get; set; }

        [Required(ErrorMessage = "CountryCode is required")]
        public string CountryCode { get; set; }

        public double? CustomDailyLimit { get; set; }

        public bool? AllowAlcoholInCombos { get; set; }

        public TimeRestrictionsConfig? CustomTimeRestrictions { get; set; }

        public List<string>? CustomAllowedDays { get; set; }

        public List<CategoryRuleOverride>? CategoryOverrides { get; set; }
    }
}
