using Microsoft.AspNetCore.Mvc;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Basket.Filter.Models.AIModels;

namespace Basket.Filter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BasketFilterController : ControllerBase
    {
        private readonly IBasketFilteringService _basketFilteringService;
        private readonly ICacheService _cacheService;
        private readonly IVertexAIService _vertexAIService;
        private readonly ILogger<BasketFilterController> _logger;

        public BasketFilterController(
            IBasketFilteringService basketFilteringService,
            ICacheService cacheService,
            IVertexAIService vertexAIService,
            ILogger<BasketFilterController> logger)
        {
            _basketFilteringService = basketFilteringService;
            _cacheService = cacheService;
            _vertexAIService = vertexAIService;
            _logger = logger;
        }

        [HttpPost("filter")]
        public async Task<ActionResult<BasketFilteringResponse>> FilterBasket([FromBody] BasketRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _logger.LogInformation("Received basket filter request: {BasketId} with {ItemCount} items",
                    request.TransactionData.BasketId, request.BasketItems.Count);

                var result = await _basketFilteringService.FilterBasketAsync(request);

                _logger.LogInformation("Basket filter response: {BasketId} - Eligible: €{EligibleAmount}/€{TotalAmount}",
                    result.BasketId, result.EligibleAmount, result.TotalAmount);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering basket: {BasketId}",
                    request?.TransactionData?.BasketId ?? "unknown");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        [HttpGet("cache/stats")]
        public ActionResult<CacheStatistics> GetCacheStats()
        {
            try
            {
                var stats = _cacheService.GetStatistics();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache stats");
                return StatusCode(500, new { error = "Failed to get cache statistics" });
            }
        }

        [HttpDelete("cache/clear")]
        public async Task<ActionResult> ClearCache()
        {
            try
            {
                await _cacheService.ClearAsync();
                _logger.LogInformation("Cache cleared by request");
                return Ok(new { message = "Cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, new { error = "Failed to clear cache" });
            }
        }

        [HttpGet("ai/health")]
        public async Task<ActionResult> CheckAIHealth()
        {
            try
            {
                var isHealthy = await _vertexAIService.IsServiceHealthyAsync();
                var modelVersion = await _vertexAIService.GetModelVersionAsync();

                return Ok(new
                {
                    healthy = isHealthy,
                    model = modelVersion,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI health check failed");
                return StatusCode(500, new { healthy = false, error = ex.Message });
            }
        }

        [HttpPost("ai/classify")]
        public async Task<ActionResult<AIClassificationResult>> ClassifyProduct([FromBody] AIClassificationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ProductName))
                {
                    return BadRequest("ProductName is required");
                }

                var result = await _vertexAIService.ClassifyProductAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying product: {ProductName}", request?.ProductName);
                return StatusCode(500, new { error = "Classification failed", message = ex.Message });
            }
        }
    }
}