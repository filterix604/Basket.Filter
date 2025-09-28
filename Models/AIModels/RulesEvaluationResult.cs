namespace Basket.Filter.Models.AIModels
{
    public class RulesEvaluationResult
    {
        public bool HasDefinitiveResult { get; set; }
        public bool IsEligible { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string DetectedCategory { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}