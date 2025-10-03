using Basket.Filter.Models.AIModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Basket.Filter.Services.Interface
{
    public interface IVertexAIService
    {
        Task<AIClassificationResult> ClassifyProductAsync(AIClassificationRequest request);
        Task<bool> IsServiceHealthyAsync();
        Task<string> GetModelVersionAsync();
        Task<AIClassificationResult> ClassifyWithFallbackAsync(AIClassificationRequest request);
    }
}
