
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Basket.Filter.Models;
public class BasketRequest
{
    [Required][JsonPropertyName("transactionData")] public TransactionData TransactionData { get; set; }
    [JsonPropertyName("merchantData")]
    public MerchantData MerchantData { get; set; }
    [JsonPropertyName("customerData")]
    public CustomerData CustomerData { get; set; }
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    [JsonPropertyName("basketItems")]
    public List<BasketItem> BasketItems { get; set; }
    [JsonPropertyName("additionalCharges")]
    public List<AdditionalCharge> AdditionalCharges { get; set; }
    [Required]
    [JsonPropertyName("basketTotals")]
    public BasketTotals BasketTotals { get; set; }
    [Required]
    [JsonPropertyName("complianceData")]
    public ComplianceData ComplianceData { get; set; }
}
public class TransactionData
{
    [Required]
    [RegularExpression(@"^[A-Za-z0-9_-]{8,50}$")]
    [JsonPropertyName("basketId")]
    public string BasketId { get; set; }
    [Required]
    [RegularExpression(@"^TXN_[A-Za-z0-9_-]{12,30}$")]
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; }
    [Required]
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } // ISO 8601 date-time
    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; }
    [Required]
    [RegularExpression(@"^[A-Z]{2}$")]
    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; }
}
public class MerchantData
{
    [Required]
    [RegularExpression(@"^[A-Za-z0-9_-]{8,20}$")]
    [JsonPropertyName("merchantId")]
    public string MerchantId { get; set; }
    [Required]
    [MaxLength(50)]
    [JsonPropertyName("merchantName")]
    public string MerchantName { get; set; }
    [Required]
    [RegularExpression(@"^[0-9]{4}$")]
    [JsonPropertyName("merchantCategoryCode")]
    public string MerchantCategoryCode { get; set; }
    // Enumerated as string to avoid mapping issues; validate with regex    [Required]
    [RegularExpression(@"^(restaurant|quick_service|grocery|delivery_platform|vending)$")]
    [JsonPropertyName("merchantType")]
    public string MerchantType { get; set; }
    [JsonPropertyName("merchantAddress")]
    public MerchantAddress MerchantAddress { get; set; }
}
public class MerchantAddress
{
    [RegularExpression(@"^[A-Z]{2}$")]
    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; }
    [MaxLength(50)]
    [JsonPropertyName("city")]
    public string City { get; set; }
    [MaxLength(10)]
    [JsonPropertyName("postalCode")]
    public string PostalCode { get; set; }
}
public class CustomerData
{
    [RegularExpression(@"^CUST_[A-Za-z0-9_-]{8,30}$")]
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }
    [RegularExpression(@"^EMP_[A-Za-z0-9_-]{8,20}$")]
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; }
}
public class BasketItem
{
    [Required]
    [JsonPropertyName("itemData")]
    public ItemData ItemData { get; set; }
    [Required]
    [JsonPropertyName("categoryData")]
    public CategoryData CategoryData { get; set; }
    [Required]
    [JsonPropertyName("pricingData")]
    public PricingData PricingData { get; set; }
    [Required]
    [JsonPropertyName("itemAttributes")]
    public ItemAttributes ItemAttributes { get; set; }
}
public class ItemData
{
    [Required]
    [RegularExpression(@"^ITEM_[A-Za-z0-9_-]{8,30}$")]
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; }
    [Required]
    [JsonPropertyName("sku")]
    public string Sku { get; set; }
    [RegularExpression(@"^[0-9]{8,14}$")]
    [JsonPropertyName("gtin")]
    public string Gtin { get; set; }
    [Required]
    [MaxLength(100)]
    [JsonPropertyName("itemName")]
    public string ItemName { get; set; }
    [MaxLength(255)]
    [JsonPropertyName("itemDescription")]
    public string ItemDescription { get; set; }
}
public class CategoryData
{
    [Required]
    [MaxLength(50)]
    [JsonPropertyName("primaryCategory")]
    public string PrimaryCategory { get; set; }
    [MaxLength(50)]
    [JsonPropertyName("subCategory")]
    public string SubCategory { get; set; }
    [RegularExpression(@"^[A-Z0-9]{4,10}$")]
    [JsonPropertyName("taxonomyCode")]
    public string TaxonomyCode { get; set; }
}
public class PricingData
{
    [Required]
    [Range(1, 99)]
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    [Required]
    [JsonPropertyName("unitPriceAmount")]
    [Range(0, int.MaxValue)]
    public int UnitPriceAmount { get; set; }
    [Required]
    [JsonPropertyName("totalPriceAmount")]
    [Range(0, int.MaxValue)]
    public int TotalPriceAmount { get; set; }
    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; }
    [Range(0, 100)]
    [JsonPropertyName("vatRate")]
    public decimal? VatRate { get; set; }
    [Range(0, int.MaxValue)]
    [JsonPropertyName("vatAmount")]
    public int? VatAmount { get; set; }
}
public class ItemAttributes
{
    [JsonPropertyName("isComboItem")]
    public bool IsComboItem { get; set; } = false;
    [JsonPropertyName("parentComboId")]
    public string ParentComboId { get; set; }
    [JsonPropertyName("containsAlcohol")]
    public bool ContainsAlcohol { get; set; } = false;
    [JsonPropertyName("alcoholByVolume")]
    [Range(0, 100)]
    public decimal? AlcoholByVolume { get; set; }
    [JsonPropertyName("allergenInfo")]
    public List<string> AllergenInfo { get; set; }
    // Allowed values per schema: gluten, dairy, nuts, eggs, soy, shellfish// Consider custom validation if needed at runtime.
}
public class AdditionalCharge
{
    [RegularExpression(@"^CHARGE_[A-Za-z0-9_-]{8,20}$")]
    [JsonPropertyName("chargeId")]
    public string ChargeId { get; set; }
    [Required]
    [RegularExpression(@"^(delivery_fee|service_charge|processing_fee|tip|packaging_fee)$")]
    [JsonPropertyName("chargeType")]
    public string ChargeType { get; set; }
    [MaxLength(50)]
    [JsonPropertyName("chargeName")]
    public string ChargeName { get; set; }
    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("chargeAmount")]
    public int ChargeAmount { get; set; }
    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; }
    [Range(0, 100)]
    [JsonPropertyName("vatRate")]
    public decimal? VatRate { get; set; }
}
public class BasketTotals
{
    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("subtotalAmount")]
    public int SubtotalAmount { get; set; }
    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("totalChargesAmount")]
    public int TotalChargesAmount { get; set; }
    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("totalVatAmount")]
    public int TotalVatAmount { get; set; }
    [Required]
    [Range(0, int.MaxValue)]
    [JsonPropertyName("totalAmount")]
    public int TotalAmount { get; set; }
    [Required]
    [RegularExpression(@"^(EUR|USD|GBP|CHF)$")]
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; }
}
public class ComplianceData
{
    [Required]
    [RegularExpression(@"^(2\.0|2\.1)$")]
    [JsonPropertyName("psiVersion")]
    public string PsiVersion { get; set; }
    [RegularExpression(@"^(public|internal|confidential)$")]
    [JsonPropertyName("dataClassification")]
    public string DataClassification { get; set; } = "internal";
    [Required]
    [JsonPropertyName("gdprConsent")]
    public bool GDPRConsent { get; set; }
    [RegularExpression(@"^P[0-9]+[YMD]$")]
    [JsonPropertyName("retentionPeriod")]
    public string RetentionPeriod { get; set; }
}