using Basket.Filter.Models;
using Basket.Filter.Models.Rules;
using Basket.Filter.Services.Interface;
using Basket.Filter.Services.Interfaces;

namespace Basket.Filter.Services
{
	public class BasketFilteringService : IBasketFilteringService
	{
		private readonly IEligibilityRulesService _rulesService;
		private readonly ICatalogService _catalogService;
		private readonly IDataStorageService _storageService;

		public BasketFilteringService(
			IEligibilityRulesService rulesService,
			ICatalogService catalogService,
			IDataStorageService storageService)
		{
			_rulesService = rulesService;
			_catalogService = catalogService;
			_storageService = storageService;
		}

		public async Task<BasketFilteringResponse> FilterBasketAsync(BasketRequest request)
		{
			var rules = await _rulesService.GetRulesForMerchantAsync(request.MerchantData.MerchantId);
			var categorizedItems = new List<CategorizedItem>();
			decimal eligibleAmount = 0;

			foreach (var item in request.BasketItems)
			{
				var categorizedItem = await CategorizeItemAsync(item, rules);
				categorizedItems.Add(categorizedItem);

				if (categorizedItem.IsEligible)
				{
					eligibleAmount += (decimal)categorizedItem.PricingData.TotalPriceAmount / 100; // Convert from cents
				}
			}

			// Apply daily limit
			if (eligibleAmount > (decimal)rules.MaxDailyAmount)
			{
				eligibleAmount = (decimal)rules.MaxDailyAmount;
			}

			var fees = request.AdditionalCharges?.Select(c => new Fee
			{
				Type = c.ChargeType,
				Name = c.ChargeName,
				Amount = c.ChargeAmount / 100,
				CurrencyCode = c.CurrencyCode
			}).ToList() ?? new List<Fee>();

			var totalFeesAmount = fees.Sum(f => f.Amount);
			var totalAmount = (decimal)request.BasketTotals.TotalAmount / 100;
			var ineligibleAmount = totalAmount - eligibleAmount;

			var response = new BasketFilteringResponse
			{
				BasketId = request.TransactionData.BasketId,
				TotalAmount = (double)totalAmount,
				EligibleAmount = (double)eligibleAmount,
				IneligibleAmount = (double)ineligibleAmount,
				CategorizedItems = categorizedItems,
				IneligibleFees = fees, // All fees are ineligible
				IsFullyEligible = ineligibleAmount == (decimal)totalFeesAmount,
				ReasonIfNotEligible = ineligibleAmount > (decimal)totalFeesAmount ? "Contains non-food items or alcohol" : "Delivery fees excluded"
			};
			await _storageService.StoreTransactionAsync(request, response);
			await _storageService.StoreBasketResponseAsync(response);
			return response;
		}
		private async Task<CategorizedItem> CategorizeItemAsync(BasketItem item, MerchantEligibilityRules rules)
		{
			var categorizedItem = new CategorizedItem
			{
				ItemData = item.ItemData,
				CategoryData = item.CategoryData,
				PricingData = item.PricingData,
				ItemAttributes = item.ItemAttributes
			};

			// Step 1: Try to get item from catalog first
			var catalogItem = await _catalogService.GetItemBySkuAsync(item.ItemData.Sku);

			if (catalogItem != null)
			{
				// PRIORITIZE CATALOG DATA - Use catalog category and create catalog-based response
				Console.WriteLine($"CATALOG FOUND: SKU={item.ItemData.Sku}, Category={catalogItem.NormalizedCategory}");

				categorizedItem.DetectedCategory = catalogItem.NormalizedCategory; // Use catalog category
				categorizedItem.IsEligible = catalogItem.NormalizedCategory != "alcoholic" && catalogItem.NormalizedCategory != "non_food";
				categorizedItem.EligibilityReason = $"Catalog match: {catalogItem.NormalizedCategory}";

				return categorizedItem; // ← RETURN EARLY - Skip rules engine completely
			}

			// Step 2: Only use rules engine if NO catalog data found
			Console.WriteLine($"CATALOG NOT FOUND: SKU={item.ItemData.Sku} - Using rules engine");

			string categoryToCheck = item.CategoryData.PrimaryCategory;

			var matchingRule = rules.CategoryRules.FirstOrDefault(r =>
				r.CategoryName == categoryToCheck ||
				r.Keywords.Any(k => item.ItemData.ItemName.ToLowerInvariant().Contains(k.ToLowerInvariant()) ||
								   (item.ItemData.ItemDescription?.ToLowerInvariant().Contains(k.ToLowerInvariant()) ?? false)));

			if (matchingRule != null)
			{
				categorizedItem.IsEligible = matchingRule.IsEligible;
				categorizedItem.EligibilityReason = matchingRule.IsEligible ?
					$"Eligible {matchingRule.CategoryName}" :
					$"Not eligible: {matchingRule.CategoryName}";
				categorizedItem.DetectedCategory = matchingRule.CategoryName;
			}
			else
			{
				categorizedItem.IsEligible = true;
				categorizedItem.EligibilityReason = "Eligible by default";
				categorizedItem.DetectedCategory = categoryToCheck;
			}

			return categorizedItem;
		}


	}
}