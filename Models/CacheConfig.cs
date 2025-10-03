namespace Basket.Filter.Models
{
    public class CacheConfig
    {
        public bool EnableCaching { get; set; } = true;
        public bool UseRedis { get; set; } = false;
        public InMemoryCacheConfig InMemory { get; set; } = new();
        public RedisCacheConfig Redis { get; set; } = new();
    }

    public class InMemoryCacheConfig
    {
        public int MaxSizeInMB { get; set; } = 100;
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan CatalogItemExpiration { get; set; } = TimeSpan.FromHours(2);
        public TimeSpan AIClassificationExpiration { get; set; } = TimeSpan.FromHours(24);
        public long SizeLimit => MaxSizeInMB * 1024 * 1024;
    }

    public class RedisCacheConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string InstanceName { get; set; } = "basket-filter-cache";
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan CatalogItemExpiration { get; set; } = TimeSpan.FromHours(6);
        public TimeSpan AIClassificationExpiration { get; set; } = TimeSpan.FromHours(48);
    }
}