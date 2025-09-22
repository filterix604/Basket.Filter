namespace Basket.Filter.Models.Rules
{
    public class MerchantTypeRules
    {
        public string TypeName { get; set; }
        public string Description { get; set; }
        public bool DefaultAllowAlcoholInCombos { get; set; }
        public bool RequiresTimeRestrictions { get; set; }
        public bool OperatesWeekends { get; set; }
        public bool AllowsBulkPurchases { get; set; }
        public int MaxItemsPerOrder { get; set; } = 50;
        public List<string> TypicalCategories { get; set; } = new();
    }
}
