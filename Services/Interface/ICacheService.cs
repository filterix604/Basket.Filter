using Basket.Filter.Models;
using Basket.Filter.Models.AIModels;

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
       
        // AI classification cache
        Task<AIClassificationData?> GetAIClassificationAsync(string sku);
        Task SetAIClassificationAsync(string sku, AIClassificationData classification, TimeSpan? expiration = null);

        // Batch operations
        Task<Dictionary<string, CatalogItem>> GetCatalogItemsBatchAsync(List<string> skus);
    }
}