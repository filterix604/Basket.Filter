using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using StackExchange.Redis;

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
				_logger.LogInformation("Redis connected successfully");
			}
		}

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            if (!_config.EnableCaching)
                return null;

            try
            {
                // L1: Check in-memory cache
                if (_memoryCache.TryGetValue(key, out T? cachedItem))
                {
                    _statistics.TotalHits++;
                    _statistics.MemoryHits++;
                    _logger.LogInformation("Cache HIT (Memory): {Key}", key);
                    return cachedItem;
                }

                // L2: Check Redis (when enabled)
                if (_config.UseRedis)
                {
                    var redisItem = await GetFromRedisAsync<T>(key);
                    if (redisItem != null)
                    {
                        _statistics.TotalHits++;
                        _statistics.RedisHits++;
                        _logger.LogInformation("Cache HIT (Redis): {Key}", key);

                        // Store in L1 for faster access
                        await SetInMemoryAsync(key, redisItem, _config.InMemory.DefaultExpiration);
                        return redisItem;
                    }
                }

                _statistics.TotalMisses++;
                _logger.LogInformation("Cache MISS: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting from cache: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            if (!_config.EnableCaching || value == null)
                return;

            try
            {
                var exp = expiration ?? _config.InMemory.DefaultExpiration;

                // L1: Store in memory
                await SetInMemoryAsync(key, value, exp);

                // L2: Store in Redis (when enabled)
                if (_config.UseRedis)
                {
                    await SetInRedisAsync(key, value, exp);
                }

                _logger.LogInformation("Cache SET: {Key} (TTL: {Expiration})", key, exp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                // L1: Remove from memory
                _memoryCache.Remove(key);

                // L2: Remove from Redis (when enabled)
                if (_config.UseRedis)
                {
                    await RemoveFromRedisAsync(key);
                }

                _logger.LogInformation("Cache REMOVE: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from cache: {Key}", key);
            }
        }

        public async Task ClearAsync()
        {
            try
            {
                _logger.LogWarning("Cache clear requested");

                if (_config.UseRedis)
                {
                    await ClearRedisAsync();
                }

                _logger.LogInformation("Cache clear completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }
        }

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

        public async Task<bool> ExistsAsync(string key)
        {
            var existsInMemory = _memoryCache.TryGetValue(key, out _);

            if (!existsInMemory && _config.UseRedis)
            {
                var redisItem = await GetFromRedisAsync<object>(key);
                return redisItem != null;
            }

            return existsInMemory;
        }

        public CacheStatistics GetStatistics()
        {
            return _statistics;
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
                return json.Length * 2;
            }
            catch
            {
                return 1000;
            }
        }

		// Redis methods 
		// Redis methods implementation
		private async Task<T?> GetFromRedisAsync<T>(string key) where T : class
		{
			try
			{
                _logger.LogInformation("Getting Key from Redis");
				if (_redisDatabase == null) return null;
                _logger.LogInformation("Connecting to db...");
                var value = await _redisDatabase.StringGetAsync(key);
				if (!value.HasValue) return null;
                _logger.LogInformation("Started deserializing");

                var redisValue = JsonSerializer.Deserialize<T>(value!);
                _logger.LogInformation("Deserialize Value: {Value}", redisValue);
                return redisValue;
            }
            catch (Exception ex)
			{
				_logger.LogError(ex, "Redis GET error for key: {Key}", key);
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
				_logger.LogError(ex, "Redis SET error for key: {Key}", key);
			}
		}

		private async Task RemoveFromRedisAsync(string key)
		{
			try
			{
				if (_redisDatabase == null) return;

				await _redisDatabase.KeyDeleteAsync(key);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Redis REMOVE error for key: {Key}", key);
			}
		}

		private async Task ClearRedisAsync()
		{
			try
			{
				if (_redisDatabase == null) return;

				var server = _redisDatabase.Multiplexer.GetServer(_redisDatabase.Multiplexer.GetEndPoints().First());
				await server.FlushDatabaseAsync();
				_logger.LogWarning("Redis cache cleared");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Redis CLEAR error");
			}
		}
	}
}