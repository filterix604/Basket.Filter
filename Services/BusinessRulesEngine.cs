using System.Text.Json;
using Basket.Filter.Models.Rules;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Google.Cloud.Firestore;

namespace Basket.Filter.Services
{
    public class BusinessRulesEngine : IBusinessRulesEngine
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<BusinessRulesEngine> _logger;
        private BusinessRulesConfig _cachedRules;
        private DateTime _lastLoadTime;

        public BusinessRulesEngine(IWebHostEnvironment environment, ILogger<BusinessRulesEngine> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<MerchantEligibilityRules> GenerateTemplateAsync(string merchantType, string countryCode)
        {
            try
            {
                var businessRules = await GetBusinessRulesAsync();
                var merchantTypeRules = GetMerchantTypeRules(businessRules, merchantType);
                var countryRules = GetCountrySpecificRules(businessRules, countryCode);

                return new MerchantEligibilityRules
                {
                    MerchantId = $"template_{merchantType}_{countryCode}",
                    MerchantName = $"{merchantTypeRules.TypeName} Template ({countryCode})",
                    MerchantType = merchantType,
                    CountryCode = countryCode,
                    AllowAlcoholInCombos = ShouldAllowAlcoholInCombos(merchantType, countryCode),
                    TimeRestrictions = GenerateTimeRestrictions(merchantType, countryCode),
                    AllowedDays = GetOperatingDays(merchantType, countryCode),
                    LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow),
                    Status = "template"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating template for {MerchantType}/{CountryCode}", merchantType, countryCode);
                throw;
            }
        }

        public async Task<BusinessRulesConfig> GetBusinessRulesAsync()
        {
            if (_cachedRules != null && DateTime.UtcNow - _lastLoadTime < TimeSpan.FromHours(1))
            {
                return _cachedRules;
            }

            try
            {
                var filePath = Path.Combine(_environment.ContentRootPath, "Configuration", "BusinessRules.json");

                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Business rules file not found, using defaults");
                    return GetDefaultBusinessRules();
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var rules = JsonSerializer.Deserialize<BusinessRulesConfig>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _cachedRules = rules;
                _lastLoadTime = DateTime.UtcNow;

                return rules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading business rules, using defaults");
                return GetDefaultBusinessRules();
            }
        }

        public bool ShouldAllowAlcoholInCombos(string merchantType, string countryCode)
        {
            var rules = GetBusinessRulesAsync().Result;
            var merchantRules = GetMerchantTypeRules(rules, merchantType);
            var countryRules = GetCountrySpecificRules(rules, countryCode);

            return merchantRules.DefaultAllowAlcoholInCombos && countryRules.AllowAlcoholInMenus;
        }

        public bool ShouldRequireTimeRestrictions(string merchantType, string countryCode)
        {
            var rules = GetBusinessRulesAsync().Result;
            var merchantRules = GetMerchantTypeRules(rules, merchantType);
            var countryRules = GetCountrySpecificRules(rules, countryCode);

            return merchantRules.RequiresTimeRestrictions || countryRules.HasStrictTimeRestrictions;
        }

        public List<DayOfWeek> GetOperatingDays(string merchantType, string countryCode)
        {
            var rules = GetBusinessRulesAsync().Result;
            var merchantRules = GetMerchantTypeRules(rules, merchantType);

            var baseDays = new List<DayOfWeek>
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday
            };

            if (merchantRules.OperatesWeekends)
            {
                baseDays.Add(DayOfWeek.Saturday);
                if (merchantType.ToLower() == "grocery")
                {
                    baseDays.Add(DayOfWeek.Sunday);
                }
            }

            return baseDays;
        }

        public List<CategoryRule> ApplyBusinessRulesToCategories(List<CategoryRule> baseRules, string merchantType, string countryCode)
        {
            var rules = GetBusinessRulesAsync().Result;
            var countryRules = GetCountrySpecificRules(rules, countryCode);
            var result = new List<CategoryRule>(baseRules);

            foreach (var rule in result)
            {
                if (countryRules.ProhibitedCategories.Contains(rule.CategoryId))
                {
                    rule.IsEligible = false;
                    rule.EligibilityReason = $"Prohibited in {countryCode}";
                }
            }

            return result;
        }

        private TimeRestrictions GenerateTimeRestrictions(string merchantType, string countryCode)
        {
            if (!ShouldRequireTimeRestrictions(merchantType, countryCode))
            {
                return new TimeRestrictions { HasTimeRestrictions = false };
            }

            return new TimeRestrictions
            {
                HasTimeRestrictions = true,
                LunchStartTime = new TimeSpan(11, 30, 0),
                LunchEndTime = new TimeSpan(14, 30, 0),
                DinnerStartTime = new TimeSpan(19, 0, 0),
                DinnerEndTime = new TimeSpan(22, 0, 0)
            };
        }

        private MerchantTypeRules GetMerchantTypeRules(BusinessRulesConfig config, string merchantType)
        {
            return config.MerchantTypes.TryGetValue(merchantType.ToLower(), out var rules)
                ? rules
                : GetDefaultMerchantTypeRules();
        }

        private CountrySpecificRules GetCountrySpecificRules(BusinessRulesConfig config, string countryCode)
        {
            return config.Countries.TryGetValue(countryCode.ToUpper(), out var rules)
                ? rules
                : GetDefaultCountrySpecificRules();
        }

        private BusinessRulesConfig GetDefaultBusinessRules()
        {
            return new BusinessRulesConfig
            {
                MerchantTypes = new Dictionary<string, MerchantTypeRules>
                {
                    ["quick_service"] = new()
                    {
                        TypeName = "Quick Service",
                        DefaultAllowAlcoholInCombos = true,
                        RequiresTimeRestrictions = true,
                        OperatesWeekends = true
                    },
                    ["grocery"] = new()
                    {
                        TypeName = "Grocery",
                        DefaultAllowAlcoholInCombos = false,
                        RequiresTimeRestrictions = false,
                        OperatesWeekends = true
                    }
                },
                Countries = new Dictionary<string, CountrySpecificRules>
                {
                    ["FR"] = new()
                    {
                        CountryCode = "FR",
                        AllowAlcoholInMenus = true,
                        MaxAlcoholPercentageInMenu = 33.33,
                        HasStrictTimeRestrictions = true
                    }
                }
            };
        }

        private MerchantTypeRules GetDefaultMerchantTypeRules()
        {
            return new MerchantTypeRules
            {
                TypeName = "Default",
                DefaultAllowAlcoholInCombos = false,
                RequiresTimeRestrictions = true,
                OperatesWeekends = false
            };
        }

        private CountrySpecificRules GetDefaultCountrySpecificRules()
        {
            return new CountrySpecificRules
            {
                AllowAlcoholInMenus = false,
                MaxAlcoholPercentageInMenu = 0,
                HasStrictTimeRestrictions = true
            };
        }
    }
}