using System.Text.Json;
using Google.Cloud.Firestore;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Basket.Filter.Models.Rules;

namespace Basket.Filter.Services
{
    public class DataSeedingService : IDataSeedingService
    {
        private readonly FirestoreDb _firestore;
        private readonly ILogger<DataSeedingService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IBusinessRulesEngine _businessRulesEngine;

        public DataSeedingService(
            FirestoreDb firestore,
            ILogger<DataSeedingService> logger,
            IWebHostEnvironment environment,
            IBusinessRulesEngine businessRulesEngine)
        {
            _firestore = firestore;
            _logger = logger;
            _environment = environment;
            _businessRulesEngine = businessRulesEngine;
        }

        public async Task SeedInitialDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting configuration-driven data seeding...");

                await SeedCountryRulesFromConfigAsync();
                await SeedMerchantTemplatesFromBusinessRulesAsync();

                _logger.LogInformation("Initial data seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data seeding");
                throw;
            }
        }

        public async Task SeedCountryRulesFromConfigAsync()
        {
            try
            {
                var configPath = Path.Combine(_environment.ContentRootPath, "Configuration", "CountryRules.json");

                if (!File.Exists(configPath))
                {
                    _logger.LogWarning("Country rules configuration file not found at {ConfigPath}", configPath);
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(configPath);
                var countryConfigs = JsonSerializer.Deserialize<List<CountryRuleConfig>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                foreach (var config in countryConfigs)
                {
                    var existingDoc = await _firestore.Collection("country_rules").Document(config.CountryCode).GetSnapshotAsync();
                    if (existingDoc.Exists)
                    {
                        _logger.LogInformation("Country rules for {CountryCode} already exist, skipping", config.CountryCode);
                        continue;
                    }

                    var countryRules = MapConfigToCountryRules(config);
                    await _firestore.Collection("country_rules").Document(config.CountryCode).SetAsync(countryRules);

                    _logger.LogInformation("Seeded country rules for {CountryCode} from configuration", config.CountryCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding country rules from configuration");
                throw;
            }
        }

        public async Task SeedMerchantTemplatesFromBusinessRulesAsync()
        {
            try
            {
                _logger.LogInformation("Generating merchant templates using business rules engine...");

                var businessRules = await _businessRulesEngine.GetBusinessRulesAsync();
                var countries = businessRules.Countries.Keys;
                var merchantTypes = businessRules.MerchantTypes.Keys;

                foreach (var country in countries)
                {
                    foreach (var merchantType in merchantTypes)
                    {
                        var templateId = $"{merchantType}_{country}";
                        var existingTemplate = await _firestore.Collection("merchant_templates").Document(templateId).GetSnapshotAsync();

                        if (existingTemplate.Exists)
                        {
                            _logger.LogInformation("Template {TemplateId} already exists, skipping", templateId);
                            continue;
                        }

                        // Generate template using business rules engine
                        var template = await _businessRulesEngine.GenerateTemplateAsync(merchantType, country);

                        // Get country rules for additional data
                        var countryDoc = await _firestore.Collection("country_rules").Document(country).GetSnapshotAsync();
                        if (countryDoc.Exists)
                        {
                            var countryRules = countryDoc.ConvertTo<CountryEligibilityRules>();
                            template.MaxDailyAmount = countryRules.DefaultDailyLimit;
                            template.CategoryRules = _businessRulesEngine.ApplyBusinessRulesToCategories(
                                countryRules.DefaultCategoryRules, merchantType, country);
                        }

                        await _firestore.Collection("merchant_templates").Document(templateId).SetAsync(template);
                        _logger.LogInformation("Generated template {TemplateId} using business rules", templateId);
                    }
                }

                _logger.LogInformation("Merchant templates generated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating merchant templates from business rules");
                throw;
            }
        }

        public async Task SeedCategoryRulesAsync()
        {
            // Category rules are now part of country rules
            await Task.CompletedTask;
        }

        public async Task CheckAndCreateIndexesAsync()
        {
            _logger.LogInformation("Required Firestore indexes:");
            _logger.LogInformation("1. Collection: merchant_rules, Fields: countryCode (ASC), status (ASC)");
            _logger.LogInformation("2. Collection: merchant_templates, Fields: merchantType (ASC), countryCode (ASC)");
            await Task.CompletedTask;
        }

        private CountryEligibilityRules MapConfigToCountryRules(CountryRuleConfig config)
        {
            return new CountryEligibilityRules
            {
                CountryCode = config.CountryCode,
                CountryName = config.CountryName,
                DefaultDailyLimit = config.DefaultDailyLimit,
                RequiresEmployeeValidation = config.RequiresEmployeeValidation,
                RegulatoryFramework = config.RegulatoryFramework,
                DefaultTimeRestrictions = MapTimeRestrictions(config.DefaultTimeRestrictions),
                DefaultAllowedDays = config.DefaultAllowedDays?.Select(day => Enum.Parse<DayOfWeek>(day)).ToList() ?? new(),
                DefaultCategoryRules = config.CategoryRules?.Select(MapCategoryRule).ToList() ?? new(),
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                UpdatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            };
        }

        private TimeRestrictions MapTimeRestrictions(TimeRestrictionsConfig config)
        {
            if (config == null) return new TimeRestrictions { HasTimeRestrictions = false };

            return new TimeRestrictions
            {
                HasTimeRestrictions = config.HasTimeRestrictions,
                LunchStartTime = !string.IsNullOrEmpty(config.LunchStartTime) ? TimeSpan.Parse(config.LunchStartTime) : new(11, 30, 0),
                LunchEndTime = !string.IsNullOrEmpty(config.LunchEndTime) ? TimeSpan.Parse(config.LunchEndTime) : new(14, 30, 0),
                DinnerStartTime = !string.IsNullOrEmpty(config.DinnerStartTime) ? TimeSpan.Parse(config.DinnerStartTime) : new(19, 0, 0),
                DinnerEndTime = !string.IsNullOrEmpty(config.DinnerEndTime) ? TimeSpan.Parse(config.DinnerEndTime) : new(22, 0, 0)
            };
        }

        private CategoryRule MapCategoryRule(CategoryRuleConfig config)
        {
            return new CategoryRule
            {
                CategoryId = config.CategoryId,
                CategoryName = config.CategoryName,
                IsEligible = config.IsEligible,
                Keywords = config.Keywords ?? new(),
                ExcludedKeywords = config.ExcludedKeywords ?? new(),
                Description = config.Description,
                EligibilityReason = config.EligibilityReason,
                MaxAlcoholPercentage = config.MaxAlcoholPercentage,
                RequiresAccompanyingFood = config.RequiresAccompanyingFood,
                RequiresImmediateConsumption = config.RequiresImmediateConsumption
            };
        }
    }
}