namespace Basket.Filter.Models.Rules;

public class CountrySpecificRules
{
    public string CountryCode { get; set; }
    public bool AllowAlcoholInMenus { get; set; }
    public double MaxAlcoholPercentageInMenu { get; set; }
    public bool HasStrictTimeRestrictions { get; set; }
    public List<string> ProhibitedCategories { get; set; } = new();
}
