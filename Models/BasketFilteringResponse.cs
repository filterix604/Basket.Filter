using Basket.Filter.Models.Rules;

namespace Basket.Filter.Models;
public class BasketFilteringResponse
{
    public string BasketId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal EligibleAmount { get; set; }
    public decimal IneligibleAmount { get; set; }
    public List<CategorizedItem> CategorizedItems { get; set; } = new();
    public List<Fee> IneligibleFees { get; set; } = new();
    public bool IsFullyEligible { get; set; }
    public string ReasonIfNotEligible { get; set; }
}

public class CategorizedItem
{
    public ItemData ItemData { get; set; }
    public CategoryData CategoryData { get; set; }
    public PricingData PricingData { get; set; }
    public ItemAttributes ItemAttributes { get; set; }

    public bool IsEligible { get; set; }
    public string EligibilityReason { get; set; }
    public string DetectedCategory { get; set; }
}

public class Fee
{
    public string Type { get; set; }
    public string Name { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
}