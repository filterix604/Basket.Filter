using Basket.Filter.Models;
using Basket.Filter.Models.AIModels;

namespace Basket.Filter.Services.Interface
{
    public interface ICatalogService
    {
        Task<CatalogUploadResponse> UploadCatalogJsonAsync(IFormFile file);
        Task<CatalogItem> GetItemBySkuAsync(string sku);
        Task<List<CatalogItem>> GetItemsByCategoryAsync(string category);
        Task<List<CatalogItem>> SearchItemsAsync(string searchTerm);
        Task<bool> DeleteCatalogAsync();
        Task<int> GetCatalogCountAsync();
        Task<CatalogItem> SaveOrUpdateCatalogItemAsync(CatalogItem item);
        Task UpdateAIClassificationAsync(string sku, AIClassificationData classification);
        Task<Dictionary<string, CatalogItem>> GetItemsBatchAsync(List<string> skus);
    }
}