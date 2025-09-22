using Basket.Filter.Models;

namespace Basket.Filter.Services.Interfaces
{
	public interface IDataStorageService
	{
		Task StoreBasketRequestAsync(BasketRequest request, CancellationToken cancellationToken = default);
		Task StoreBasketResponseAsync(BasketFilteringResponse response, CancellationToken cancellationToken = default);
		Task StoreTransactionAsync(BasketRequest request, BasketFilteringResponse response);
	}
}