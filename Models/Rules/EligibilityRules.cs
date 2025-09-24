using Google.Cloud.Firestore;

namespace Basket.Filter.Models.Rules
{
    [FirestoreData]
    public class MerchantEligibilityRules
    {
        [FirestoreProperty("merchantId")]
        public string MerchantId { get; set; }

        [FirestoreProperty("merchantName")]
        public string MerchantName { get; set; }

        [FirestoreProperty("merchantType")]
        public string MerchantType { get; set; }

        [FirestoreProperty("countryCode")]
        public string CountryCode { get; set; }

        [FirestoreProperty("maxDailyAmount")]
        public double MaxDailyAmount { get; set; } = 25.00;

        [FirestoreProperty("categoryRules")]
        public List<CategoryRule> CategoryRules { get; set; } = new();

        [FirestoreProperty("allowAlcoholInCombos")]
        public bool AllowAlcoholInCombos { get; set; } = false;

        [FirestoreProperty("timeRestrictions")]
        public TimeRestrictions TimeRestrictions { get; set; } = new();

        [FirestoreProperty("allowedDays")]
        public List<string> AllowedDaysFirestore { get; set; } = new();

        [FirestoreProperty("lastUpdated")]
        public Timestamp LastUpdated { get; set; }

        [FirestoreProperty("status")]
        public string Status { get; set; } = "active";

        public List<DayOfWeek> AllowedDays
        {
            get => AllowedDaysFirestore?.Select(day => Enum.Parse<DayOfWeek>(day)).ToList() ?? new();
            set => AllowedDaysFirestore = value?.Select(day => day.ToString()).ToList() ?? new();
        }
    }

    [FirestoreData]
    public class CountryEligibilityRules
    {
        [FirestoreProperty("countryCode")]
        public string CountryCode { get; set; }

        [FirestoreProperty("countryName")]
        public string CountryName { get; set; }

        [FirestoreProperty("defaultDailyLimit")]
        public double DefaultDailyLimit { get; set; }

        [FirestoreProperty("defaultCategoryRules")]
        public List<CategoryRule> DefaultCategoryRules { get; set; } = new();

        [FirestoreProperty("defaultTimeRestrictions")]
        public TimeRestrictions DefaultTimeRestrictions { get; set; } = new();

        [FirestoreProperty("defaultAllowedDays")]
        public List<string> DefaultAllowedDaysFirestore { get; set; } = new();

        [FirestoreProperty("requiresEmployeeValidation")]
        public bool RequiresEmployeeValidation { get; set; } = true;

        [FirestoreProperty("regulatoryFramework")]
        public string RegulatoryFramework { get; set; }

        [FirestoreProperty("createdAt")]
        public Timestamp CreatedAt { get; set; }

        [FirestoreProperty("updatedAt")]
        public Timestamp UpdatedAt { get; set; }

        public List<DayOfWeek> DefaultAllowedDays
        {
            get => DefaultAllowedDaysFirestore?.Select(day => Enum.Parse<DayOfWeek>(day)).ToList() ?? new();
            set => DefaultAllowedDaysFirestore = value?.Select(day => day.ToString()).ToList() ?? new();
        }
    }

    [FirestoreData]
    public class CategoryRule
    {
        [FirestoreProperty("categoryId")]
        public string CategoryId { get; set; }

        [FirestoreProperty("categoryName")]
        public string CategoryName { get; set; }

        [FirestoreProperty("isEligible")]
        public bool IsEligible { get; set; }

        [FirestoreProperty("keywords")]
        public List<string> Keywords { get; set; } = new();

        [FirestoreProperty("excludedKeywords")]
        public List<string> ExcludedKeywords { get; set; } = new();

        [FirestoreProperty("description")]
        public string Description { get; set; }

        [FirestoreProperty("eligibilityReason")]
        public string EligibilityReason { get; set; }

        [FirestoreProperty("maxAlcoholPercentage")]
        public double? MaxAlcoholPercentage { get; set; }

        [FirestoreProperty("requiresAccompanyingFood")]
        public bool RequiresAccompanyingFood { get; set; }

        [FirestoreProperty("requiresImmediateConsumption")]
        public bool RequiresImmediateConsumption { get; set; }
    }

    [FirestoreData]
    public class TimeRestrictions
    {
        [FirestoreProperty("lunchStartTime")]
        public string LunchStartTimeString { get; set; } = "11:30:00";

        [FirestoreProperty("lunchEndTime")]
        public string LunchEndTimeString { get; set; } = "14:30:00";

        [FirestoreProperty("dinnerStartTime")]
        public string DinnerStartTimeString { get; set; } = "19:00:00";

        [FirestoreProperty("dinnerEndTime")]
        public string DinnerEndTimeString { get; set; } = "22:00:00";

        [FirestoreProperty("hasTimeRestrictions")]
        public bool HasTimeRestrictions { get; set; } = true;

        public TimeSpan LunchStartTime
        {
            get => !string.IsNullOrEmpty(LunchStartTimeString) ? TimeSpan.Parse(LunchStartTimeString) : new(11, 30, 0);
            set => LunchStartTimeString = value.ToString(@"hh\:mm\:ss");
        }

        public TimeSpan LunchEndTime
        {
            get => !string.IsNullOrEmpty(LunchEndTimeString) ? TimeSpan.Parse(LunchEndTimeString) : new(14, 30, 0);
            set => LunchEndTimeString = value.ToString(@"hh\:mm\:ss");
        }

        public TimeSpan DinnerStartTime
        {
            get => !string.IsNullOrEmpty(DinnerStartTimeString) ? TimeSpan.Parse(DinnerStartTimeString) : new(19, 0, 0);
            set => DinnerStartTimeString = value.ToString(@"hh\:mm\:ss");
        }

        public TimeSpan DinnerEndTime
        {
            get => !string.IsNullOrEmpty(DinnerEndTimeString) ? TimeSpan.Parse(DinnerEndTimeString) : new(22, 0, 0);
            set => DinnerEndTimeString = value.ToString(@"hh\:mm\:ss");
        }
    }
}