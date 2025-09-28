namespace Basket.Filter.Models
{
    public class VertexAIConfig
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Location { get; set; } = "asia-south1";
        public string ModelName { get; set; } = "gemini-1.5-flash-002";       
        public int MaxTokens { get; set; } = 1000;
        public double Temperature { get; set; } = 0.1;
        public bool EnableAI { get; set; } = true;
        public int MaxRetries { get; set; } = 3;
        public int TimeoutSeconds { get; set; } = 30;
    }
}