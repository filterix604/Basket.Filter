namespace Basket.Filter.Models
{
    public class TimeRestrictionsConfig
    {
        public bool HasTimeRestrictions { get; set; }
        public string LunchStartTime { get; set; }
        public string LunchEndTime { get; set; }
        public string DinnerStartTime { get; set; }
        public string DinnerEndTime { get; set; }
    }
}
