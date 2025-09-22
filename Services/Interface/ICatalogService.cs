using Basket.Filter.Models;

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
    }
}