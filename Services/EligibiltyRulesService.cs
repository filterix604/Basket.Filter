using Google.Cloud.Firestore;
using Basket.Filter.Models.Rules;
using Basket.Filter.Services.Interface;

namespace Basket.Filter.Services
{
    public class EligibilityRulesService : IEligibilityRulesService
    {
        private readonly FirestoreDb _firestore;
        private readonly ILogger<EligibilityRulesService> _logger;
        private readonly ICacheService _cacheService;

        public EligibilityRulesService(
            FirestoreDb firestore,
            ILogger<EligibilityRulesService> logger,
            ICacheService cacheService)
        {
            _firestore = firestore;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<MerchantEligibilityRules> GetRulesForMerchantAsync(string merchantId)
        {
            var cacheKey = $"merchant:rules:{merchantId}";

            try
            {
                var cachedRules = await _cacheService.GetAsync<MerchantEligibilityRules>(cacheKey);
                if (cachedRules != null)
                {
                    _logger.LogDebug("Rules cache HIT for merchant: {MerchantId}", merchantId);
                    return cachedRules;
                }

                _logger.LogDebug("Rules cache MISS for merchant: {MerchantId}", merchantId);

                // Try to get merchant-specific rules first
                var merchantDoc = await _firestore
                    .Collection("merchant_rules")
                    .Document(merchantId)
                    .GetSnapshotAsync();

                if (merchantDoc.Exists)
                {
                    var rules = merchantDoc.ConvertTo<MerchantEligibilityRules>();
                    await _cacheService.SetAsync(cacheKey, rules, TimeSpan.FromMinutes(15));
                    return rules;
                }

                // Fallback to default rules
                var defaultRules = await GetDefaultRulesForMerchantAsync(merchantId);
                await _cacheService.SetAsync(cacheKey, defaultRules, TimeSpan.FromMinutes(5));
                return defaultRules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rules for merchant {MerchantId}", merchantId);
                return await GetFallbackRulesAsync();
            }
        }

        public async Task<CountryEligibilityRules> GetRulesForCountryAsync(string countryCode)
        {
            var cacheKey = $"country:rules:{countryCode}";

            try
            {
                var cachedRules = await _cacheService.GetAsync<CountryEligibilityRules>(cacheKey);
                if (cachedRules != null)
                {
                    _logger.LogDebug("Country rules cache HIT for: {CountryCode}", countryCode);
                    return cachedRules;
                }

                _logger.LogDebug("Country rules cache MISS for: {CountryCode}", countryCode);

                var countryDoc = await _firestore
                    .Collection("country_rules")
                    .Document(countryCode)
                    .GetSnapshotAsync();

                if (countryDoc.Exists)
                {
                    var rules = countryDoc.ConvertTo<CountryEligibilityRules>();
                    await _cacheService.SetAsync(cacheKey, rules, TimeSpan.FromHours(1));
                    return rules;
                }

                var defaultRules = GetDefaultCountryRules(countryCode);
                await _cacheService.SetAsync(cacheKey, defaultRules, TimeSpan.FromHours(1));
                return defaultRules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rules for country {CountryCode}", countryCode);
                return GetDefaultCountryRules("FR"); // Fallback to French rules
            }
        }

        private async Task<MerchantEligibilityRules> GetDefaultRulesForMerchantAsync(string merchantId)
        {
            return new MerchantEligibilityRules
            {
                MerchantId = merchantId,
                CountryCode = "FR",
                MaxDailyAmount = 25.00,
                CategoryRules = GetDefaultCategoryRules("FR"),
                AllowAlcoholInCombos = true,
                AllowedDays = new List<DayOfWeek>
                {
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                    DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
                },
                TimeRestrictions = new TimeRestrictions()
            };
        }

        private CountryEligibilityRules GetDefaultCountryRules(string countryCode)
        {
            return countryCode.ToUpper() switch
            {
                "FR" => new CountryEligibilityRules
                {
                    CountryCode = "FR",
                    CountryName = "France",
                    DefaultDailyLimit = 25.00,
                    DefaultCategoryRules = GetFrenchCategoryRules(),
                    DefaultAllowedDays = new List<DayOfWeek>
                    {
                        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday
                    },
                    RequiresEmployeeValidation = true,
                    RegulatoryFramework = "French Labour Code Article L3262-1"
                },
                "BE" => new CountryEligibilityRules
                {
                    CountryCode = "BE",
                    CountryName = "Belgium",
                    DefaultDailyLimit = 8.00,
                    DefaultCategoryRules = GetBelgianCategoryRules(),
                    RequiresEmployeeValidation = true,
                    RegulatoryFramework = "Belgian Social Security Code"
                },
                _ => GetDefaultCountryRules("FR")
            };
        }

        private List<CategoryRule> GetDefaultCategoryRules(string countryCode)
        {
            return countryCode.ToUpper() switch
            {
                "FR" => GetFrenchCategoryRules(),
                "BE" => GetBelgianCategoryRules(),
                _ => GetFrenchCategoryRules()
            };
        }

        private List<CategoryRule> GetFrenchCategoryRules()
        {
            return new List<CategoryRule>
            {
                new()
                {
                    CategoryId = "prepared_meals",
                    CategoryName = "Prepared Meals",
                    IsEligible = true,
                    Keywords = new() { "sandwich", "salade", "pizza", "burger", "menu", "plat" },
                    Description = "Ready-to-eat meals",
                    EligibilityReason = "Eligible under French meal voucher regulations"
                },
                new()
                {
                    CategoryId = "menu_avec_alcool",
                    CategoryName = "Menu with Alcohol",
                    IsEligible = true,
                    Keywords = new() { "menu", "formule" },
                    Description = "Complete meal menus including alcohol",
                    EligibilityReason = "Allowed in France if alcohol < 1/3 of menu price",
                    MaxAlcoholPercentage = 33.33
                },
                new()
                {
                    CategoryId = "alcohol",
                    CategoryName = "Standalone Alcohol",
                    IsEligible = false,
                    Keywords = new() { "bière", "vin", "alcool", "spiritueux" },
                    Description = "Alcoholic beverages purchased alone",
                    EligibilityReason = "Standalone alcohol prohibited"
                },
                new()
                {
                    CategoryId = "delivery_fee",
                    CategoryName = "Delivery Fees",
                    IsEligible = false,
                    Keywords = new() { "livraison", "frais", "delivery" },
                    Description = "Delivery and service charges",
                    EligibilityReason = "Service fees are not eligible"
                }
            };
        }

        private List<CategoryRule> GetBelgianCategoryRules()
        {
            return new List<CategoryRule>
            {
                new()
                {
                    CategoryId = "prepared_meals",
                    CategoryName = "Prepared Meals",
                    IsEligible = true,
                    Keywords = new() { "sandwich", "salade", "pizza" },
                    Description = "Ready-to-eat meals"
                },
                new()
                {
                    CategoryId = "alcohol",
                    CategoryName = "All Alcohol",
                    IsEligible = false,
                    Keywords = new() { "beer", "wine", "alcohol" },
                    Description = "All alcoholic beverages prohibited"
                }
            };
        }

        private async Task<MerchantEligibilityRules> GetFallbackRulesAsync()
        {
            return new MerchantEligibilityRules
            {
                MerchantId = "fallback",
                CountryCode = "FR",
                MaxDailyAmount = 25.00,
                CategoryRules = GetFrenchCategoryRules(),
                AllowAlcoholInCombos = false
            };
        }
    }
}