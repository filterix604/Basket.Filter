using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Basket.Filter.Services.Interfaces;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace Basket.Filter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BasketController : ControllerBase
    {
        private readonly IBasketFilteringService _filteringService;
        private readonly IEligibilityRulesService _rulesService;
        private readonly FirestoreDb _firestore;
        private readonly IDataStorageService _dataStorageService;
        private readonly ICacheService _cacheService; //ADD THIS - For cache stats testing

        public BasketController(
            IBasketFilteringService filteringService,
            IEligibilityRulesService rulesService,
            FirestoreDb firestore,
            IDataStorageService dataStorageService,
            ICacheService cacheService) // ADD THIS - For cache stats testing
        {
            _filteringService = filteringService;
            _rulesService = rulesService;
            _firestore = firestore;
            _dataStorageService = dataStorageService;
            _cacheService = cacheService; // ADD THIS - For cache stats testing
        }

        [HttpPost("filter")]
        public async Task<BasketFilteringResponse> FilterBasket([FromBody] BasketRequest request)
        {
            await _dataStorageService.StoreBasketRequestAsync(request);

            return await _filteringService.FilterBasketAsync(request);
        }

        //TEMPORARY TESTING ENDPOINT - REMOVE AFTER CACHE TESTING
        [HttpGet("cache-stats")]
        public IActionResult GetCacheStats()
        {
            var stats = _cacheService.GetStatistics();
            return Ok(new
            {
                stats.TotalHits,
                stats.TotalMisses,
                stats.MemoryHits,
                stats.RedisHits,
                stats.HitRatio,
                stats.LastReset,
                Message = "Cache statistics - this is a temporary testing endpoint"
            });
        }
        // 🧪 END TEMPORARY TESTING CODE

    }
}