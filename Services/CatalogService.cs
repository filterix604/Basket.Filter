using Basket.Filter.Services.Interface;
using Basket.Filter.Models;
using Google.Cloud.Firestore;
using System.Text.Json;

namespace Basket.Filter.Services
{
    public class CatalogService : ICatalogService
    {
        private readonly FirestoreDb _firestore;
        private readonly ILogger<CatalogService> _logger;
        private readonly ICacheService _cacheService; 
        private const string CATALOG_COLLECTION = "catalog_items";

        public CatalogService(FirestoreDb firestore, ILogger<CatalogService> logger, ICacheService cacheService)
        {
            _firestore = firestore;
            _logger = logger;
            _cacheService = cacheService;
        }

        public async Task<CatalogUploadResponse> UploadCatalogJsonAsync(IFormFile file)
        {
            var response = new CatalogUploadResponse();

            try
            {
                var items = await ParseJsonFileAsync(file);
                response.TotalItems = items.Count;

                if (items.Count == 0)
                {
                    response.Errors.Add("No valid items found in JSON file");
                    return response;
                }

                var batch = _firestore.StartBatch();
                var batchCount = 0;

                foreach (var item in items)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(item.Sku))
                        {
                            response.FailedItems++;
                            response.Errors.Add("Item missing SKU - skipped");
                            continue;
                        }

                        item.NormalizedCategory = NormalizeCategory(item.OriginalCategory);
                        item.CreatedAt = DateTime.UtcNow;
                        item.UpdatedAt = DateTime.UtcNow;

                        var docRef = _firestore.Collection(CATALOG_COLLECTION).Document(item.Sku);
                        batch.Set(docRef, item);

                        await _cacheService.SetCatalogItemAsync(item.Sku, item);

                        batchCount++;
                        response.SuccessfulItems++;

                        if (batchCount >= 500)
                        {
                            await batch.CommitAsync();
                            batch = _firestore.StartBatch();
                            batchCount = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        response.FailedItems++;
                        response.Errors.Add($"Failed to upload SKU {item.Sku}: {ex.Message}");
                        _logger.LogError(ex, "Error uploading item {Sku}", item.Sku);
                    }
                }

                if (batchCount > 0)
                {
                    await batch.CommitAsync();
                }

                response.Message = $"Catalog upload completed. {response.SuccessfulItems}/{response.TotalItems} items uploaded successfully.";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading catalog");
                response.Errors.Add($"Upload failed: {ex.Message}");
                return response;
            }
        }

        public async Task<CatalogItem?> GetItemBySkuAsync(string sku)
        {
            try
            {
                // Step 1: Check cache first
                var cachedItem = await _cacheService.GetCatalogItemAsync(sku);
                if (cachedItem != null)
                {
                    return cachedItem;
                }

                // Step 2: Cache miss - get from Firestore
                var doc = await _firestore.Collection(CATALOG_COLLECTION).Document(sku).GetSnapshotAsync();

                if (!doc.Exists)
                {
                    return null;
                }

                var item = doc.ConvertTo<CatalogItem>();

                // Step 3: Store in cache
                await _cacheService.SetCatalogItemAsync(sku, item);

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item by SKU: {Sku}", sku);
                return null;
            }
        }

        public async Task<List<CatalogItem>> GetItemsByCategoryAsync(string category)
        {
            try
            {
                var query = _firestore.Collection(CATALOG_COLLECTION)
                    .WhereEqualTo("NormalizedCategory", category);
                var snapshot = await query.GetSnapshotAsync();

                return snapshot.Documents.Select(doc => doc.ConvertTo<CatalogItem>()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting items by category: {Category}", category);
                return new List<CatalogItem>();
            }
        }

        public async Task<List<CatalogItem>> SearchItemsAsync(string searchTerm)
        {
            try
            {
                var query = _firestore.Collection(CATALOG_COLLECTION);
                var snapshot = await query.GetSnapshotAsync();

                return snapshot.Documents
                    .Select(doc => doc.ConvertTo<CatalogItem>())
                    .Where(item => item.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                  item.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                  item.Brand.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching items: {SearchTerm}", searchTerm);
                return new List<CatalogItem>();
            }
        }

        public async Task<bool> DeleteCatalogAsync()
        {
            try
            {
                var query = _firestore.Collection(CATALOG_COLLECTION);
                var snapshot = await query.GetSnapshotAsync();

                if (snapshot.Count == 0)
                {
                    _logger.LogInformation("No catalog items to delete");
                    return true;
                }

                var batch = _firestore.StartBatch();
                var batchCount = 0;

                foreach (var doc in snapshot.Documents)
                {
                    batch.Delete(doc.Reference);
                    batchCount++;

                    // Commit batch every 500 items (Firestore limit)
                    if (batchCount >= 500)
                    {
                        await batch.CommitAsync();
                        batch = _firestore.StartBatch();
                        batchCount = 0;
                    }
                }

                // Commit remaining items
                if (batchCount > 0)
                {
                    await batch.CommitAsync();
                }

                _logger.LogInformation("Deleted {Count} catalog items", snapshot.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting catalog");
                return false;
            }
        }

        public async Task<int> GetCatalogCountAsync()
        {
            try
            {
                var snapshot = await _firestore.Collection(CATALOG_COLLECTION).GetSnapshotAsync();
                return snapshot.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting catalog count");
                return 0;
            }
        }

        private async Task<List<CatalogItem>> ParseJsonFileAsync(IFormFile file)
        {
            var items = new List<CatalogItem>();

            using var reader = new StreamReader(file.OpenReadStream());
            var json = await reader.ReadToEndAsync();

            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var rawItems = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, jsonOptions);

                if (rawItems == null)
                {
                    _logger.LogWarning("JSON deserialization returned null");
                    return items;
                }

                foreach (var rawItem in rawItems)
                {
                    try
                    {
                        var item = new CatalogItem
                        {
                            Sku = GetStringValue(rawItem, "sku"),
                            Name = GetStringValue(rawItem, "name"),
                            Description = GetStringValue(rawItem, "description"),
                            OriginalCategory = GetStringValue(rawItem, "category"),
                            Brand = GetStringValue(rawItem, "brand"),
                            Price = GetDoubleValue(rawItem, "price"),
                            ContainsAlcohol = GetBoolValue(rawItem, "contains_alcohol")
                        };

                        // Skip items with missing required fields
                        if (string.IsNullOrEmpty(item.Sku) || string.IsNullOrEmpty(item.Name))
                        {
                            _logger.LogWarning("Skipping item with missing SKU or Name");
                            continue;
                        }

                        items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error parsing individual item, skipping");
                        continue;
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON format");
                throw new ArgumentException("Invalid JSON format", ex);
            }

            return items;
        }

        private string GetStringValue(Dictionary<string, JsonElement> dict, string key)
        {
            if (dict.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString() ?? "";
                if (element.ValueKind == JsonValueKind.Number)
                    return element.ToString();
            }
            return "";
        }

        private double GetDoubleValue(Dictionary<string, JsonElement> dict, string key)
        {
            if (dict.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.Number)
                    return element.GetDouble();
                if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), out var result))
                    return result;
            }
            return 0.0;
        }

        private bool GetBoolValue(Dictionary<string, JsonElement> dict, string key)
        {
            if (dict.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.True) return true;
                if (element.ValueKind == JsonValueKind.False) return false;
                if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var result))
                    return result;
            }
            return false;
        }

        private string NormalizeCategory(string originalCategory)
        {
            if (string.IsNullOrEmpty(originalCategory))
                return "non_food";

            var category = originalCategory.ToLowerInvariant();

            // Normalize to standard categories
            if (category.Contains("meal") || category.Contains("pizza") || category.Contains("burger") ||
                category.Contains("sandwich") || category.Contains("prepared") || category.Contains("ready"))
                return "prepared_meal";

            if (category.Contains("fruit") || category.Contains("vegetable") || category.Contains("fresh") ||
                category.Contains("produce"))
                return "fresh_fruit";

            if (category.Contains("alcohol") || category.Contains("beer") || category.Contains("wine") ||
                category.Contains("spirit") || category.Contains("liquor"))
                return "alcoholic";

            if (category.Contains("beverage") || category.Contains("drink") || category.Contains("juice") ||
                category.Contains("soda") || category.Contains("water"))
                return "beverage";

            if (category.Contains("dairy") || category.Contains("milk") || category.Contains("cheese") ||
                category.Contains("yogurt"))
                return "beverage"; // Dairy often eligible like beverages

            if (category.Contains("bakery") || category.Contains("bread") || category.Contains("pastry"))
                return "prepared_meal"; // Bakery items often treated as prepared meals

            if (category.Contains("hygiene") || category.Contains("personal_care") || category.Contains("soap") ||
                category.Contains("shampoo") || category.Contains("cleaning"))
                return "non_food";

            return "non_food"; // Default for unknown categories
        }
    }
}