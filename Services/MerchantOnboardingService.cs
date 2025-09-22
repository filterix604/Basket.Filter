using Google.Cloud.Firestore;
using Basket.Filter.Models;
using Basket.Filter.Models.Rules;
using Basket.Filter.Services.Interface;

namespace Basket.Filter.Services
{
    public class MerchantOnboardingService : IMerchantOnboardingService
    {
        private readonly FirestoreDb _firestore;
        private readonly IBusinessRulesEngine _businessRulesEngine;
        private readonly IEligibilityRulesService _rulesService;
        private readonly ILogger<MerchantOnboardingService> _logger;

        public MerchantOnboardingService(
            FirestoreDb firestore,
            IBusinessRulesEngine businessRulesEngine,
            IEligibilityRulesService rulesService,
            ILogger<MerchantOnboardingService> logger)
        {
            _firestore = firestore;
            _businessRulesEngine = businessRulesEngine;
            _rulesService = rulesService;
            _logger = logger;
        }

        public async Task<MerchantEligibilityRules> OnboardMerchantAsync(MerchantOnboardingRequest request)
        {
            try
            {
                _logger.LogInformation("Starting onboarding for merchant {MerchantId}", request.MerchantId);

                // Check if merchant already exists
                var existingMerchant = await _firestore.Collection("merchant_rules").Document(request.MerchantId).GetSnapshotAsync();
                if (existingMerchant.Exists)
                {
                    throw new InvalidOperationException($"Merchant {request.MerchantId} already exists");
                }

                // Get country rules
                var countryRules = await _rulesService.GetRulesForCountryAsync(request.CountryCode);

                // Generate base template from business rules
                var baseTemplate = await _businessRulesEngine.GenerateTemplateAsync(request.MerchantType, request.CountryCode);

                // Apply custom overrides
                var merchantRules = ApplyCustomOverrides(baseTemplate, countryRules, request);

                // Save to Firestore
                await _firestore.Collection("merchant_rules").Document(request.MerchantId).SetAsync(merchantRules);

                _logger.LogInformation("Successfully onboarded merchant {MerchantId}", request.MerchantId);
                return merchantRules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error onboarding merchant {MerchantId}", request.MerchantId);
                throw;
            }
        }

       private MerchantEligibilityRules ApplyCustomOverrides(
    MerchantEligibilityRules baseTemplate,
    CountryEligibilityRules countryRules,
    MerchantOnboardingRequest request)
{
    var merchantRules = new MerchantEligibilityRules
    {
        MerchantId = request.MerchantId,
        MerchantName = request.MerchantName,
        MerchantType = request.MerchantType,
        CountryCode = request.CountryCode,
        MaxDailyAmount = request.CustomDailyLimit ?? countryRules.DefaultDailyLimit,
        CategoryRules = ApplyCategoryOverrides(countryRules.DefaultCategoryRules, request.CategoryOverrides),
        AllowAlcoholInCombos = request.AllowAlcoholInCombos ?? baseTemplate.AllowAlcoholInCombos,
        TimeRestrictions = ApplyTimeRestrictionOverrides(baseTemplate.TimeRestrictions, request.CustomTimeRestrictions),
        AllowedDays = GetAllowedDays(request.CustomAllowedDays, baseTemplate.AllowedDays),
        LastUpdated = Timestamp.FromDateTime(DateTime.UtcNow),
        Status = "active"
    };

    return merchantRules;
}

        private List<CategoryRule> ApplyCategoryOverrides(List<CategoryRule> baseRules, List<CategoryRuleOverride>? overrides)
        {
            var result = new List<CategoryRule>(baseRules);

            // Handle null overrides
            if (overrides == null || !overrides.Any())
            {
                return result;
            }

            foreach (var override_ in overrides)
            {
                var rule = result.FirstOrDefault(r => r.CategoryId == override_.CategoryId);
                if (rule != null)
                {
                    rule.IsEligible = override_.IsEligible;
                    if (!string.IsNullOrEmpty(override_.CustomReason))
                    {
                        rule.EligibilityReason = override_.CustomReason;
                    }
                }
            }

            return result;
        }

        private TimeRestrictions ApplyTimeRestrictionOverrides(TimeRestrictions baseRestrictions, TimeRestrictionsConfig? customRestrictions)
        {
            // If no custom restrictions provided, use base restrictions
            if (customRestrictions == null)
                return baseRestrictions;

            return new TimeRestrictions
            {
                HasTimeRestrictions = customRestrictions.HasTimeRestrictions,
                LunchStartTime = !string.IsNullOrEmpty(customRestrictions.LunchStartTime)
                    ? TimeSpan.Parse(customRestrictions.LunchStartTime)
                    : baseRestrictions.LunchStartTime,
                LunchEndTime = !string.IsNullOrEmpty(customRestrictions.LunchEndTime)
                    ? TimeSpan.Parse(customRestrictions.LunchEndTime)
                    : baseRestrictions.LunchEndTime,
                DinnerStartTime = !string.IsNullOrEmpty(customRestrictions.DinnerStartTime)
                    ? TimeSpan.Parse(customRestrictions.DinnerStartTime)
                    : baseRestrictions.DinnerStartTime,
                DinnerEndTime = !string.IsNullOrEmpty(customRestrictions.DinnerEndTime)
                    ? TimeSpan.Parse(customRestrictions.DinnerEndTime)
                    : baseRestrictions.DinnerEndTime
            };
        }

        private List<DayOfWeek> GetAllowedDays(List<string>? customAllowedDays, List<DayOfWeek> defaultDays)
        {
            // If no custom days provided, use default days
            if (customAllowedDays == null || !customAllowedDays.Any())
                return defaultDays;

            return customAllowedDays.Select(day => Enum.Parse<DayOfWeek>(day)).ToList();
        }
    }
}