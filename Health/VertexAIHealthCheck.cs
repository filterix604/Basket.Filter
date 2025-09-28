using Microsoft.Extensions.Diagnostics.HealthChecks;
using Basket.Filter.Services.Interface;

namespace Basket.Filter.Health
{
    public class VertexAIHealthCheck : IHealthCheck
    {
        private readonly IVertexAIService _vertexAIService;
        private readonly ILogger<VertexAIHealthCheck> _logger;

        public VertexAIHealthCheck(
            IVertexAIService vertexAIService,
            ILogger<VertexAIHealthCheck> logger)
        {
            _vertexAIService = vertexAIService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var isHealthy = await _vertexAIService.IsServiceHealthyAsync();

                if (isHealthy)
                {
                    var modelVersion = await _vertexAIService.GetModelVersionAsync();
                    return HealthCheckResult.Healthy($"Vertex AI: {modelVersion}");
                }

                return HealthCheckResult.Unhealthy("Vertex AI health check failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Vertex AI health check exception");
                return HealthCheckResult.Unhealthy($"Vertex AI exception: {ex.Message}");
            }
        }
    }
}