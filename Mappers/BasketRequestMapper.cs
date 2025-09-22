
using Basket.Filter.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;

namespace Basket.Filter.Mappers
{
	public class BasketRequestMapper : IBasketRequestMapper
	{
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILogger<BasketRequestMapper> _logger;

		public BasketRequestMapper(
			IHttpContextAccessor httpContextAccessor,
			ILogger<BasketRequestMapper> logger)
		{
			_httpContextAccessor = httpContextAccessor;
			_logger = logger;
		}

		public BasketRequest MapBasketRequestToFirestore(BasketRequest request)
		{
			try
			{
				var now = Timestamp.FromDateTime(DateTime.UtcNow);

				var firestoreModel = new BasketRequest
				{
					TransactionData = MapTransactionData(request.TransactionData),
					MerchantData = MapMerchantData(request.MerchantData),
					CustomerData = MapCustomerData(request.CustomerData),
					BasketItems = request.BasketItems?.Select(MapBasketItem).ToList() ?? new(),
					AdditionalCharges = request.AdditionalCharges?.Select(MapAdditionalCharge).ToList() ?? new(),
					BasketTotals = MapBasketTotals(request.BasketTotals),
					ComplianceData = MapComplianceData(request.ComplianceData)
				};

				return firestoreModel;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error mapping BasketRequest to Firestore model");
				throw;
			}
		}

		private string GenerateDocumentId(BasketRequest request)
		{
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			return $"{request.TransactionData.TransactionId}_{timestamp}";
		}

		private TransactionData MapTransactionData(TransactionData data)
		{
			return new TransactionData
			{
				BasketId = data.BasketId,
				TransactionId = data.TransactionId,
				Timestamp = data.Timestamp,
				CurrencyCode = data.CurrencyCode,
				CountryCode = data.CountryCode
			};
		}

		private MerchantData MapMerchantData(MerchantData data)
		{
			return new MerchantData
			{
				MerchantId = data.MerchantId,
				MerchantName = data.MerchantName,
				MerchantCategoryCode = data.MerchantCategoryCode,
				MerchantType = data.MerchantType,
				MerchantAddress = data.MerchantAddress != null ? new MerchantAddress
				{
					CountryCode = data.MerchantAddress.CountryCode,
					City = data.MerchantAddress.City,
					PostalCode = data.MerchantAddress.PostalCode
				} : null
			};
		}

		private CustomerData MapCustomerData(CustomerData data)
		{
			return new CustomerData
			{
				CustomerId = data?.CustomerId,
				EmployeeId = data?.EmployeeId
			};
		}

		private BasketItem MapBasketItem(BasketItem item)
		{
			return new BasketItem
			{
				ItemData = new ItemData
				{
					ItemId = item.ItemData.ItemId,
					Sku = item.ItemData.Sku,
					Gtin = item.ItemData.Gtin,
					ItemName = item.ItemData.ItemName,
					ItemDescription = item.ItemData.ItemDescription
				},
				CategoryData = new CategoryData
				{
					PrimaryCategory = item.CategoryData.PrimaryCategory,
					SubCategory = item.CategoryData.SubCategory,
					TaxonomyCode = item.CategoryData.TaxonomyCode
				},
				PricingData = new PricingData
				{
					Quantity = item.PricingData.Quantity,
					UnitPriceAmount = item.PricingData.UnitPriceAmount,
					TotalPriceAmount = item.PricingData.TotalPriceAmount,
					CurrencyCode = item.PricingData.CurrencyCode,
					VatRate = item.PricingData.VatRate,
					VatAmount = item.PricingData.VatAmount
				},
				ItemAttributes = new ItemAttributes
				{
					IsComboItem = item.ItemAttributes.IsComboItem,
					ParentComboId = item.ItemAttributes.ParentComboId,
					ContainsAlcohol = item.ItemAttributes.ContainsAlcohol,
					AlcoholByVolume = item.ItemAttributes.AlcoholByVolume,
					AllergenInfo = item.ItemAttributes.AllergenInfo ?? new List<string>()
				}
			};
		}

		private AdditionalCharge MapAdditionalCharge(AdditionalCharge charge)
		{
			return new AdditionalCharge
			{
				ChargeId = charge.ChargeId,
				ChargeType = charge.ChargeType,
				ChargeName = charge.ChargeName,
				ChargeAmount = charge.ChargeAmount,
				CurrencyCode = charge.CurrencyCode,
				VatRate = charge.VatRate
			};
		}

		private BasketTotals MapBasketTotals(BasketTotals totals)
		{
			return new BasketTotals
			{
				SubtotalAmount = totals.SubtotalAmount,
				TotalChargesAmount = totals.TotalChargesAmount,
				TotalVatAmount = totals.TotalVatAmount,
				TotalAmount = totals.TotalAmount,
				CurrencyCode = totals.CurrencyCode
			};
		}

		private ComplianceData MapComplianceData(ComplianceData compliance)
		{
			return new ComplianceData
			{
				PsiVersion = compliance.PsiVersion,
				DataClassification = compliance.DataClassification,
				GDPRConsent = compliance.GDPRConsent,
				RetentionPeriod = compliance.RetentionPeriod
			};
		}
	}
}