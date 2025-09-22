using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Http;
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

        public BasketController(IBasketFilteringService filteringService, IEligibilityRulesService rulesService, FirestoreDb firestore)
        {
            _filteringService = filteringService;
            _rulesService = rulesService;
            _firestore = firestore;
        }

        [HttpPost("filter")]
        public async Task<BasketFilteringResponse> FilterBasket([FromBody] BasketRequest request)
        {
            return await _filteringService.FilterBasketAsync(request);
        }
    }
}
