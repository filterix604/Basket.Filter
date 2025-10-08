using Google.Cloud.Firestore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Basket.Filter.Models;

[FirestoreData]
public class BasketRequest
{
    [Required]
    [JsonPropertyName("transactionData")]
    [FirestoreProperty("transactionData")]
    public TransactionData TransactionData { get; set; }

    [JsonPropertyName("merchantData")]
    [FirestoreProperty("merchantData")]
    public MerchantData MerchantData { get; set; }

    [JsonPropertyName("customerData")]
    [FirestoreProperty("customerData")]
    public CustomerData CustomerData { get; set; }

    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    [JsonPropertyName("basketItems")]
    [FirestoreProperty("basketItems")]
    public List<BasketItem> BasketItems { get; set; }

    [JsonPropertyName("additionalCharges")]
    [FirestoreProperty("additionalCharges")]
    public List<AdditionalCharge> AdditionalCharges { get; set; }

    [Required]
    [JsonPropertyName("basketTotals")]
    [FirestoreProperty("basketTotals")]
    public BasketTotals BasketTotals { get; set; }

    [Required]
    [JsonPropertyName("complianceData")]
    [FirestoreProperty("complianceData")]
    public ComplianceData ComplianceData { get; set; }
}

[FirestoreData]
public class TransactionData
{
    [Required]
    [JsonPropertyName("basketId")]
    [FirestoreProperty("basketId")]
    public string BasketId { get; set; }

    [Required]
    [JsonPropertyName("transactionId")]
    [FirestoreProperty("transactionId")]
    public string TransactionId { get; set; }

    [Required]
    [JsonPropertyName("timestamp")]
    [FirestoreProperty("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    [FirestoreProperty("currencyCode")]
    public string CurrencyCode { get; set; }

    [Required]
    [RegularExpression(@"^[A-Z]{2}$")]
    [JsonPropertyName("countryCode")]
    [FirestoreProperty("countryCode")]
    public string CountryCode { get; set; }
}

[FirestoreData]
public class MerchantData
{
    [Required]
    [JsonPropertyName("merchantId")]
    [FirestoreProperty("merchantId")]
    public string MerchantId { get; set; }

    [Required]
    [MaxLength(50)]
    [JsonPropertyName("merchantName")]
    [FirestoreProperty("merchantName")]
    public string MerchantName { get; set; }

    [Required]
    [JsonPropertyName("merchantCategoryCode")]
    [FirestoreProperty("merchantCategoryCode")]
    public string MerchantCategoryCode { get; set; }

    [Required]
    [JsonPropertyName("merchantType")]
    [FirestoreProperty("merchantType")]
    public string MerchantType { get; set; }

    [JsonPropertyName("merchantAddress")]
    [FirestoreProperty("merchantAddress")]
    public MerchantAddress MerchantAddress { get; set; }
}

[FirestoreData]
public class MerchantAddress
{
    [RegularExpression(@"^[A-Z]{2}$")]
    [JsonPropertyName("countryCode")]
    [FirestoreProperty("countryCode")]
    public string CountryCode { get; set; }

    [MaxLength(50)]
    [JsonPropertyName("city")]
    [FirestoreProperty("city")]
    public string City { get; set; }

    [MaxLength(10)]
    [JsonPropertyName("postalCode")]
    [FirestoreProperty("postalCode")]
    public string PostalCode { get; set; }
}

[FirestoreData]
public class CustomerData
{
    [JsonPropertyName("customerId")]
    [FirestoreProperty("customerId")]
    public string CustomerId { get; set; }

    [JsonPropertyName("employeeId")]
    [FirestoreProperty("employeeId")]
    public string EmployeeId { get; set; }
}

[FirestoreData]
public class BasketItem
{
    [Required]
    [JsonPropertyName("itemData")]
    [FirestoreProperty("itemData")]
    public ItemData ItemData { get; set; }

    [Required]
    [JsonPropertyName("categoryData")]
    [FirestoreProperty("categoryData")]
    public CategoryData CategoryData { get; set; }

    [Required]
    [JsonPropertyName("pricingData")]
    [FirestoreProperty("pricingData")]
    public PricingData PricingData { get; set; }

    [Required]
    [JsonPropertyName("itemAttributes")]
    [FirestoreProperty("itemAttributes")]
    public ItemAttributes ItemAttributes { get; set; }
}

[FirestoreData]
public class ItemData
{
    [Required]
    [JsonPropertyName("itemId")]
    [FirestoreProperty("itemId")]
    public string ItemId { get; set; }

    [Required]
    [JsonPropertyName("sku")]
    [FirestoreProperty("sku")]
    public string Sku { get; set; }

    [JsonPropertyName("gtin")]
    [FirestoreProperty("gtin")]
    public string Gtin { get; set; }

    [Required]
    [MaxLength(100)]
    [JsonPropertyName("itemName")]
    [FirestoreProperty("itemName")]
    public string ItemName { get; set; }

    [MaxLength(255)]
    [JsonPropertyName("itemDescription")]
    [FirestoreProperty("itemDescription")]
    public string ItemDescription { get; set; }
}

[FirestoreData]
public class CategoryData
{
    [Required]
    [MaxLength(50)]
    [JsonPropertyName("primaryCategory")]
    [FirestoreProperty("primaryCategory")]
    public string PrimaryCategory { get; set; }

    [MaxLength(50)]
    [JsonPropertyName("subCategory")]
    [FirestoreProperty("subCategory")]
    public string SubCategory { get; set; }

    [JsonPropertyName("taxonomyCode")]
    [FirestoreProperty("taxonomyCode")]
    public string TaxonomyCode { get; set; }
}

[FirestoreData]
public class PricingData
{
    [Required]
    [Range(1, 99)]
    [JsonPropertyName("quantity")]
    [FirestoreProperty("quantity")]
    public int Quantity { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("unitPriceAmount")]
    [FirestoreProperty("unitPriceAmount")]
    public int UnitPriceAmount { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("totalPriceAmount")]
    [FirestoreProperty("totalPriceAmount")]
    public int TotalPriceAmount { get; set; }

    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    [FirestoreProperty("currencyCode")]
    public string CurrencyCode { get; set; }

    [Range(0, 100)]
    [JsonPropertyName("vatRate")]
    [FirestoreProperty("vatRate")]
    public double? VatRate { get; set; }

    [Range(0, int.MaxValue)]
    [JsonPropertyName("vatAmount")]
    [FirestoreProperty("vatAmount")]
    public int? VatAmount { get; set; }
}

[FirestoreData]
public class ItemAttributes
{
    [JsonPropertyName("isComboItem")]
    [FirestoreProperty("isComboItem")]
    public bool IsComboItem { get; set; } = false;

    [JsonPropertyName("parentComboId")]
    [FirestoreProperty("parentComboId")]
    public string ParentComboId { get; set; }

    [JsonPropertyName("containsAlcohol")]
    [FirestoreProperty("containsAlcohol")]
    public bool ContainsAlcohol { get; set; } = false;

    [Range(0, 100)]
    [JsonPropertyName("alcoholByVolume")]
    [FirestoreProperty("alcoholByVolume")]
    public double? AlcoholByVolume { get; set; }

    [JsonPropertyName("allergenInfo")]
    [FirestoreProperty("allergenInfo")]
    public List<string> AllergenInfo { get; set; }
}

[FirestoreData]
public class AdditionalCharge
{
    [JsonPropertyName("chargeId")]
    [FirestoreProperty("chargeId")]
    public string ChargeId { get; set; }

    [Required]
    [RegularExpression(@"^(delivery_fee|service_charge|processing_fee|tip|packaging_fee)$")]
    [JsonPropertyName("chargeType")]
    [FirestoreProperty("chargeType")]
    public string ChargeType { get; set; }

    [MaxLength(50)]
    [JsonPropertyName("chargeName")]
    [FirestoreProperty("chargeName")]
    public string ChargeName { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("chargeAmount")]
    [FirestoreProperty("chargeAmount")]
    public int ChargeAmount { get; set; }

    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    [FirestoreProperty("currencyCode")]
    public string CurrencyCode { get; set; }

    [Range(0, 100)]
    [JsonPropertyName("vatRate")]
    [FirestoreProperty("vatRate")]
    public double? VatRate { get; set; }
}

[FirestoreData]
public class BasketTotals
{
    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("subtotalAmount")]
    [FirestoreProperty("subtotalAmount")]
    public int SubtotalAmount { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("totalChargesAmount")]
    [FirestoreProperty("totalChargesAmount")]
    public int TotalChargesAmount { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("totalVatAmount")]
    [FirestoreProperty("totalVatAmount")]
    public int TotalVatAmount { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("totalAmount")]
    [FirestoreProperty("totalAmount")]
    public int TotalAmount { get; set; }

    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    [FirestoreProperty("currencyCode")]
    public string CurrencyCode { get; set; }
}

[FirestoreData]
public class ComplianceData
{
    [JsonPropertyName("psiVersion")]
    [FirestoreProperty("psiVersion")]
    public string PsiVersion { get; set; }

    [RegularExpression(@"^(public|internal|confidential)$")]
    [JsonPropertyName("dataClassification")]
    [FirestoreProperty("dataClassification")]
    public string DataClassification { get; set; } = "internal";

    [JsonPropertyName("gdprConsent")]
    [FirestoreProperty("gdprConsent")]
    public bool GDPRConsent { get; set; }

    [JsonPropertyName("retentionPeriod")]
    [FirestoreProperty("retentionPeriod")]
    public string RetentionPeriod { get; set; }
}