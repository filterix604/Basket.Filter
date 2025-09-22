// Services/Interface/ICacheService.cs
using Basket.Filter.Models;

namespace Basket.Filter.Services.Interface
{
    public interface ICacheService
    {
        // Generic operations
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task ClearAsync();

        // Catalog specific
        Task<CatalogItem?> GetCatalogItemAsync(string sku);
        Task SetCatalogItemAsync(string sku, CatalogItem item, TimeSpan? expiration = null);
        Task RemoveCatalogItemAsync(string sku);

        // Utilities
        Task<bool> ExistsAsync(string key);
        CacheStatistics GetStatistics();
    }
}