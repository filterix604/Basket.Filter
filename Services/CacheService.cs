using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using StackExchange.Redis;
using Basket.Filter.Models.AIModels;

namespace Basket.Filter.Services
{
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CacheService> _logger;
        private readonly CacheConfig _config;
        private readonly CacheStatistics _statistics;
        private readonly IDatabase? _redisDatabase;

        public CacheService(
            IMemoryCache memoryCache,
            ILogger<CacheService> logger,
            IOptions<CacheConfig> config,
            IConnectionMultiplexer? redis = null)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _config = config.Value;
            _statistics = new CacheStatistics();

            if (_config.UseRedis && redis != null)
            {
                _redisDatabase = redis.GetDatabase();
                _logger.LogInformation("Redis connected: {InstanceName}", _config.Redis.InstanceName);
            }
            else
            {
                _logger.LogInformation("Using memory cache only (MaxSize: {MaxSize}MB)",
                    _config.InMemory.MaxSizeInMB);
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (!_config.EnableCaching) return null;

            try
            {
                // L1: Check memory cache
                if (_memoryCache.TryGetValue(key, out T? cachedItem))
                {
                    _statistics.TotalHits++;
                    _statistics.MemoryHits++;
                    _logger.LogDebug("Memory cache HIT: {Key}", key);
                    return cachedItem;
                }

                // L2: Check Redis
                if (_config.UseRedis && _redisDatabase != null)
                {
                    var redisItem = await GetFromRedisAsync<T>(key);
                    if (redisItem != null)
                    {
                        _statistics.TotalHits++;
                        _statistics.RedisHits++;
                        _logger.LogDebug("Redis cache HIT: {Key}", key);

                        // Promote to L1 cache
                        await SetInMemoryAsync(key, redisItem, _config.InMemory.DefaultExpiration);
                        return redisItem;
                    }
                }

                _statistics.TotalMisses++;
                _logger.LogDebug("Cache MISS: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache GET error: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (!_config.EnableCaching || value == null) return;

            try
            {
                var exp = expiration ?? _config.InMemory.DefaultExpiration;

                // L1: Store in memory
                await SetInMemoryAsync(key, value, exp);

                // L2: Store in Redis
                if (_config.UseRedis && _redisDatabase != null)
                {
                    var redisExp = expiration ?? _config.Redis.DefaultExpiration;
                    await SetInRedisAsync(key, value, redisExp);
                }

                _logger.LogDebug("Cache SET: {Key} (TTL: {Expiration})", key, exp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache SET error: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);

                if (_config.UseRedis && _redisDatabase != null)
                {
                    await _redisDatabase.KeyDeleteAsync(key);
                }

                _logger.LogDebug("Cache REMOVE: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache REMOVE error: {Key}", key);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            var existsInMemory = _memoryCache.TryGetValue(key, out _);
            if (existsInMemory) return true;

            if (_config.UseRedis && _redisDatabase != null)
            {
                return await _redisDatabase.KeyExistsAsync(key);
            }

            return false;
        }

        // Catalog-specific methods (aligned with your existing interface)
        public async Task<CatalogItem?> GetCatalogItemAsync(string sku)
        {
            var key = $"catalog:item:{sku}";
            return await GetAsync<CatalogItem>(key);
        }

        public async Task SetCatalogItemAsync(string sku, CatalogItem item, TimeSpan? expiration = null)
        {
            var key = $"catalog:item:{sku}";
            var exp = expiration ?? _config.InMemory.CatalogItemExpiration;
            await SetAsync(key, item, exp);
        }

        public async Task RemoveCatalogItemAsync(string sku)
        {
            var key = $"catalog:item:{sku}";
            await RemoveAsync(key);
        }

        public async Task<AIClassificationData?> GetAIClassificationAsync(string sku)
        {
            var key = $"ai:classification:{sku}";
            return await GetAsync<AIClassificationData>(key);
        }

        public async Task SetAIClassificationAsync(string sku, AIClassificationData classification, TimeSpan? expiration = null)
        {
            var key = $"ai:classification:{sku}";
            var exp = expiration ?? _config.InMemory.AIClassificationExpiration;
            await SetAsync(key, classification, exp);
        }

        // Batch operations
        public async Task<Dictionary<string, CatalogItem>> GetCatalogItemsBatchAsync(List<string> skus)
        {
            var results = new Dictionary<string, CatalogItem>();

            foreach (var sku in skus)
            {
                var item = await GetCatalogItemAsync(sku);
                if (item != null)
                {
                    results[sku] = item;
                }
            }

            return results;
        }

        public CacheStatistics GetStatistics() => _statistics;

        public async Task ClearAsync()
        {
            try
            {
                if (_config.UseRedis && _redisDatabase != null)
                {
                    var server = _redisDatabase.Multiplexer.GetServer(_redisDatabase.Multiplexer.GetEndPoints().First());
                    await server.FlushDatabaseAsync();
                }

                // Reset statistics
                _statistics.TotalHits = 0;
                _statistics.TotalMisses = 0;
                _statistics.MemoryHits = 0;
                _statistics.RedisHits = 0;
                _statistics.LastReset = DateTime.UtcNow;

                _logger.LogWarning("Cache cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache clear error");
            }
        }

        // Private helper methods
        private async Task SetInMemoryAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration,
                Priority = CacheItemPriority.Normal,
                Size = EstimateSize(value)
            };

            _memoryCache.Set(key, value, options);
            await Task.CompletedTask;
        }

        private int EstimateSize<T>(T value)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                return json.Length * 2; // UTF-16 approximation
            }
            catch
            {
                return 1000; // Default estimate
            }
        }

        private async Task<T?> GetFromRedisAsync<T>(string key) where T : class
        {
            try
            {
                if (_redisDatabase == null) return null;

                var value = await _redisDatabase.StringGetAsync(key);
                if (!value.HasValue) return null;

                return JsonSerializer.Deserialize<T>(value!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis GET error: {Key}", key);
                return null;
            }
        }

        private async Task SetInRedisAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            try
            {
                if (_redisDatabase == null) return;

                var json = JsonSerializer.Serialize(value);
                await _redisDatabase.StringSetAsync(key, json, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis SET error: {Key}", key);
            }
        }
    }
}