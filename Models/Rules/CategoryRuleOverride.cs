using System.ComponentModel.DataAnnotations;

namespace Basket.Filter.Models.Rules
{
    public class CategoryRuleOverride
    {
        [Required]
        public string CategoryId { get; set; }

        [Required]
        public bool IsEligible { get; set; }

        public string? CustomReason { get; set; }
    }
}
