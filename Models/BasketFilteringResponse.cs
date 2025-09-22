using Google.Cloud.Firestore;

namespace Basket.Filter.Models;

[FirestoreData]
public class BasketFilteringResponse
{
	[FirestoreProperty]
	public string BasketId { get; set; }
	[FirestoreProperty]
	public double TotalAmount { get; set; }

	[FirestoreProperty]
	public double EligibleAmount { get; set; }

	[FirestoreProperty]
	public double IneligibleAmount { get; set; }

	[FirestoreProperty]
	public List<CategorizedItem> CategorizedItems { get; set; } = new();

	[FirestoreProperty]
	public List<Fee> IneligibleFees { get; set; } = new();

	[FirestoreProperty]
	public bool IsFullyEligible { get; set; }

	[FirestoreProperty]
	public string ReasonIfNotEligible { get; set; }
}

[FirestoreData]
public class CategorizedItem
{
	[FirestoreProperty]
	public ItemData ItemData { get; set; }

	[FirestoreProperty]
	public CategoryData CategoryData { get; set; }

	[FirestoreProperty]
	public PricingData PricingData { get; set; }

	[FirestoreProperty]
	public ItemAttributes ItemAttributes { get; set; }

	[FirestoreProperty]
	public bool IsEligible { get; set; }

	[FirestoreProperty]
	public string EligibilityReason { get; set; }

	[FirestoreProperty]
	public string DetectedCategory { get; set; }
}

[FirestoreData]
public class Fee
{
	[FirestoreProperty]
	public string Type { get; set; }

	[FirestoreProperty]
	public string Name { get; set; }

	[FirestoreProperty]
	public double Amount { get; set; }

	[FirestoreProperty]
	public string CurrencyCode { get; set; }
}
