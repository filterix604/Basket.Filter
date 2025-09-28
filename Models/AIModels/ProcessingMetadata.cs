namespace Basket.Filter.Models.AIModels
{
    public class ProcessingMetadata
    {
        public string Source { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string Version { get; set; } = string.Empty;
    }
}