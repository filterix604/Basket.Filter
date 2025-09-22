using Basket.Filter.Models; 

namespace Basket.Filter.Services.Interface
{
    public interface IBasketFilteringService
    {
        Task<BasketFilteringResponse> FilterBasketAsync(BasketRequest request);
    }
}
