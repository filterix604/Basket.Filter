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
    }

    public class RedisCacheConfig
    {
        public string ConnectionString { get; set; } = "localhost:6379";
        public string InstanceName { get; set; } = "BasketFilter";
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan CatalogItemExpiration { get; set; } = TimeSpan.FromHours(6);
    }
}