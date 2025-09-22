namespace Basket.Filter.Services.Interface
{
    public interface IDataSeedingService
    {
        Task SeedInitialDataAsync();
        Task SeedCategoryRulesAsync();
        Task CheckAndCreateIndexesAsync();
    }
}
