using Basket.Filter.Services.Interface;
using Basket.Filter.Services;
using Google.Cloud.Firestore;
using Basket.Filter.Infrastructure.Services;
using Basket.Filter.Services.Interfaces;
using Basket.Filter.Mappers;
using Basket.Filter.Models;
using Basket.Filter.Health;
using StackExchange.Redis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Basket.Filter.Models.Rules;

var builder = WebApplication.CreateBuilder(args);

//CLOUD RUN: Configure Kestrel for PORT
builder.WebHost.ConfigureKestrel(options =>
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    options.ListenAnyIP(int.Parse(port));
});

// CLOUD RUN: Enhanced Firestore setup
var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "basket-filter-engine";
var credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") ?? "firestore-key.json";
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
builder.Services.AddSingleton(FirestoreDb.Create(projectId));

// Cache configuration
builder.Services.Configure<CacheConfig>(builder.Configuration.GetSection("Cache"));

// AI and Business Rules configuration
builder.Services.Configure<VertexAIConfig>(builder.Configuration.GetSection("VertexAI"));
// Memory cache (increased for production)
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 512 * 1024 * 1024; // 512MB for Cloud Run
});

// Add Redis connection (only if UseRedis is true)
var cacheConfig = builder.Configuration.GetSection("Cache").Get<CacheConfig>();
if (cacheConfig?.UseRedis == true)
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
    {
        var logger = provider.GetRequiredService<ILogger<IConnectionMultiplexer>>();
        try
        {
            var connection = ConnectionMultiplexer.Connect(cacheConfig.Redis.ConnectionString);
            logger.LogInformation("Connected to Google Cloud Redis");
            return connection;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Google Cloud Redis");
            throw;
        }
    });
}
else
{
    // Register null for services that expect IConnectionMultiplexer
    builder.Services.AddSingleton<IConnectionMultiplexer?>(provider => null);
}

// Services registration (cleaned up duplicates)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

// Business services
builder.Services.AddScoped<IEligibilityRulesService, EligibilityRulesService>();
builder.Services.AddScoped<IBasketFilteringService, BasketFilteringService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IDataStorageService, DataStorageService>();
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddScoped<IDataSeedingService, DataSeedingService>();
builder.Services.AddScoped<IBusinessRulesEngine, BusinessRulesEngine>();
builder.Services.AddScoped<IMerchantOnboardingService, MerchantOnboardingService>();
builder.Services.AddScoped<IBasketRequestMapper, BasketRequestMapper>();

// AI Service
builder.Services.AddScoped<IVertexAIService, VertexAIService>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<VertexAIHealthCheck>("vertex-ai")
    .AddCheck("firestore", () => HealthCheckResult.Healthy($"Firestore: {projectId}"))
    .AddCheck("redis-cache", () =>
    {
        if (cacheConfig?.UseRedis == true)
        {
            return HealthCheckResult.Healthy($"Redis: {cacheConfig.Redis.InstanceName}");
        }
        return HealthCheckResult.Healthy("Redis cache disabled");
    });

var app = builder.Build();

// CLOUD RUN: Always enable Swagger for demo
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Startup validation
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var aiConfig = app.Configuration.GetSection("VertexAI").Get<VertexAIConfig>();

logger.LogInformation("Starting Basket Filter API");
logger.LogInformation("Project ID: {ProjectId}", projectId);
logger.LogInformation("Cache: Redis={RedisEnabled}", cacheConfig?.UseRedis ?? false);

if (aiConfig?.EnableAI == true)
{
    logger.LogInformation("AI Service: ENABLED (Model: {Model})", aiConfig.ModelName);
}
else
{
    logger.LogInformation("AI Service: DISABLED");
}

// CLOUD RUN: Async data seeding (non-blocking)
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var seedingService = scope.ServiceProvider.GetRequiredService<IDataSeedingService>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        seedLogger.LogInformation("Starting data seeding...");
        await seedingService.SeedInitialDataAsync();
        seedLogger.LogInformation("Data seeding completed successfully");
    }
    catch (Exception ex)
    {
        seedLogger.LogError(ex, "Error during data seeding");
    }
});

app.Run();