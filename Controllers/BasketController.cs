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

		public BasketController(IBasketFilteringService filteringService, IEligibilityRulesService rulesService, FirestoreDb firestore, IDataStorageService dataStorageService)
		{
			_filteringService = filteringService;
			_rulesService = rulesService;
			_firestore = firestore;
			_dataStorageService = dataStorageService;
		}

		[HttpPost("filter")]
		public async Task<BasketFilteringResponse> FilterBasket([FromBody] BasketRequest request)
		{
			await _dataStorageService.StoreBasketRequestAsync(request);

			return await _filteringService.FilterBasketAsync(request);
		}
	}
}
