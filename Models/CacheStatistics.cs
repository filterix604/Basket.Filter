namespace Basket.Filter.Models
{
    public class CacheStatistics
    {
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public long MemoryHits { get; set; }
        public long RedisHits { get; set; }
        public double HitRatio => TotalHits + TotalMisses > 0 ? (double)TotalHits / (TotalHits + TotalMisses) * 100 : 0;
        public DateTime LastReset { get; set; } = DateTime.UtcNow;
    }
}