using Google.Cloud.Firestore;
using Microsoft.Extensions.Caching.Memory;
using Basket.Filter.Models.Rules;
using Basket.Filter.Services.Interface;

namespace basket_filter_engine.Services
{
    public class EligibilityRulesService : IEligibilityRulesService
    {
        private readonly FirestoreDb _firestore;
        private readonly ILogger<EligibilityRulesService> _logger;
        private readonly IMemoryCache _cache;

        public EligibilityRulesService(
            FirestoreDb firestore,
            ILogger<EligibilityRulesService> logger,
            IMemoryCache cache)
        {
            _firestore = firestore;
            _logger = logger;
            _cache = cache;
        }

        public async Task<MerchantEligibilityRules> GetRulesForMerchantAsync(string merchantId)
        {
            var cacheKey = $"merchant_rules_{merchantId}";

            //if (_cache.TryGetValue(cacheKey, out MerchantEligibilityRules cachedRules))
            //{
            //    return cachedRules;
            //}

            try
            {
                // Try to get merchant-specific rules first
                var merchantDoc = await _firestore
                    .Collection("merchant_rules")
                    .Document(merchantId)
                    .GetSnapshotAsync();

                if (merchantDoc.Exists)
                {
                    var rules = merchantDoc.ConvertTo<MerchantEligibilityRules>();
                    _cache.Set(cacheKey, rules, TimeSpan.FromMinutes(15));
                    return rules;
                }

                // Fallback to default rules based on merchant type and country
                var defaultRules = await GetDefaultRulesForMerchantAsync(merchantId);
                _cache.Set(cacheKey, defaultRules, TimeSpan.FromMinutes(5));
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
            var cacheKey = $"country_rules_{countryCode}";

            if (_cache.TryGetValue(cacheKey, out CountryEligibilityRules cachedRules))
            {
                return cachedRules;
            }

            try
            {
                var countryDoc = await _firestore
                    .Collection("country_rules")
                    .Document(countryCode)
                    .GetSnapshotAsync();

                if (countryDoc.Exists)
                {
                    var rules = countryDoc.ConvertTo<CountryEligibilityRules>();
                    _cache.Set(cacheKey, rules, TimeSpan.FromHours(1));
                    return rules;
                }

                return GetDefaultCountryRules(countryCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving rules for country {CountryCode}", countryCode);
                return GetDefaultCountryRules("FR"); // Fallback to French rules
            }
        }

        public async Task<List<CategoryRule>> GetCategoryRulesAsync(string countryCode, string merchantType)
        {
            var cacheKey = $"category_rules_{countryCode}_{merchantType}";

            if (_cache.TryGetValue(cacheKey, out List<CategoryRule> cachedRules))
            {
                return cachedRules;
            }

            try
            {
                var query = _firestore
                    .Collection("category_rules")
                    .WhereEqualTo("CountryCode", countryCode)
                    .WhereEqualTo("MerchantType", merchantType);

                var snapshot = await query.GetSnapshotAsync();

                var rules = snapshot.Documents
                    .Select(doc => doc.ConvertTo<CategoryRule>())
                    .ToList();

                if (!rules.Any())
                {
                    rules = GetDefaultCategoryRules(countryCode);
                }

                _cache.Set(cacheKey, rules, TimeSpan.FromMinutes(30));
                return rules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category rules for {CountryCode}/{MerchantType}", countryCode, merchantType);
                return GetDefaultCategoryRules(countryCode);
            }
        }

        public async Task UpdateMerchantRulesAsync(string merchantId, MerchantEligibilityRules rules)
        {
            try
            {
                rules.LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow);

                await _firestore
                    .Collection("merchant_rules")
                    .Document(merchantId)
                    .SetAsync(rules);

                // Invalidate cache
                _cache.Remove($"merchant_rules_{merchantId}");

                _logger.LogInformation("Updated rules for merchant {MerchantId}", merchantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rules for merchant {MerchantId}", merchantId);
                throw;
            }
        }

        public async Task<bool> IsCategoryEligibleAsync(string category, string countryCode)
        {
            var rules = await GetCategoryRulesAsync(countryCode, "default");
            var categoryRule = rules.FirstOrDefault(r =>
                r.CategoryName.Equals(category, StringComparison.OrdinalIgnoreCase) ||
                r.Keywords.Any(k => category.Contains(k, StringComparison.OrdinalIgnoreCase)));

            return categoryRule?.IsEligible ?? false;
        }

        public async Task<decimal> GetDailyLimitAsync(string countryCode)
        {
            var countryRules = await GetRulesForCountryAsync(countryCode);
            return (decimal)countryRules.DefaultDailyLimit;
        }

        public async Task<bool> IsTimeRestrictedAsync(string countryCode, DateTime transactionTime)
        {
            var countryRules = await GetRulesForCountryAsync(countryCode);

            if (!countryRules.DefaultTimeRestrictions.HasTimeRestrictions)
                return false;

            var timeOfDay = transactionTime.TimeOfDay;
            var restrictions = countryRules.DefaultTimeRestrictions;

            // Check if within lunch hours
            bool isLunchTime = timeOfDay >= restrictions.LunchStartTime &&
                              timeOfDay <= restrictions.LunchEndTime;

            // Check if within dinner hours  
            bool isDinnerTime = timeOfDay >= restrictions.DinnerStartTime &&
                               timeOfDay <= restrictions.DinnerEndTime;

            return !(isLunchTime || isDinnerTime);
        }

        // Private helper methods
        private async Task<MerchantEligibilityRules> GetDefaultRulesForMerchantAsync(string merchantId)
        {
            // This would typically involve looking up merchant info
            // For now, return French default rules
            return new MerchantEligibilityRules
            {
                MerchantId = merchantId,
                CountryCode = "FR",
                MaxDailyAmount = 25.00,
                CategoryRules = GetDefaultCategoryRules("FR"),
                AllowAlcoholInCombos = true, // French exception
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
                _ => GetDefaultCountryRules("FR") // Fallback to French rules
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
                IsEligible = false, // Stricter than France
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
                AllowAlcoholInCombos = false // Conservative fallback
            };
        }
    }
}
