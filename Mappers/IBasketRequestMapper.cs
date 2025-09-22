using Basket.Filter.Models;

namespace Basket.Filter.Mappers
{
	public interface IBasketRequestMapper
	{
		BasketRequest MapBasketRequestToFirestore(BasketRequest json);
	}
}
