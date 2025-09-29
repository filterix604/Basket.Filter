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
using Google.Cloud.AIPlatform.V1;
using Google.Api.Gax.Grpc;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore.V1;

var builder = WebApplication.CreateBuilder(args);

//CLOUD RUN: Configure Kestrel for PORT
builder.WebHost.ConfigureKestrel(options =>
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    options.ListenAnyIP(int.Parse(port));
});

// CLOUD RUN: Enhanced Google Cloud setup
var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "basket-filter-engine";

// Separate credentials for Firestore and Vertex AI
var firestoreCredentialsPath = Environment.GetEnvironmentVariable("FIRESTORE_CREDENTIALS_PATH") ?? "firestore-key.json";
var vertexAICredentialsPath = Environment.GetEnvironmentVariable("VERTEX_AI_CREDENTIALS_PATH") ?? "vertex-ai-key.json";

// Set up Firestore with its specific credentials
GoogleCredential firestoreCredential;
try
{
    if (File.Exists(firestoreCredentialsPath))
    {
        firestoreCredential = GoogleCredential.FromFile(firestoreCredentialsPath);
        Console.WriteLine($"Using Firestore credentials from file: {firestoreCredentialsPath}");
    }
    else
    {
        firestoreCredential = GoogleCredential.GetApplicationDefault();
        Console.WriteLine("Using default Firestore credentials");
    }
}
catch (Exception ex)
{
    throw new InvalidOperationException($"Failed to load Firestore credentials: {ex.Message}", ex);
}

// Set up Vertex AI with its specific credentials
GoogleCredential vertexAICredential;
try
{
    if (File.Exists(vertexAICredentialsPath))
    {
        vertexAICredential = GoogleCredential.FromFile(vertexAICredentialsPath);
        // Set this as the default for other Google Cloud services
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", vertexAICredentialsPath);
        Console.WriteLine($"Using Vertex AI credentials from file: {vertexAICredentialsPath}");
    }
    else
    {
        vertexAICredential = GoogleCredential.GetApplicationDefault();
        Console.WriteLine("Using default Vertex AI credentials");
    }
}
catch (Exception ex)
{
    throw new InvalidOperationException($"Failed to load Vertex AI credentials: {ex.Message}", ex);
}

// Firestore setup with specific credentials
builder.Services.AddSingleton<FirestoreDb>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<FirestoreDb>>();
    try
    {
        // Create Firestore client builder with specific credentials
        var firestoreClientBuilder = new FirestoreClientBuilder
        {
            Credential = firestoreCredential
        };

        var firestoreClient = firestoreClientBuilder.Build();
        var db = FirestoreDb.Create(projectId, firestoreClient);

        logger.LogInformation("Firestore initialized successfully with project: {ProjectId}", projectId);
        return db;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize Firestore");
        throw;
    }
});

// Vertex AI Client setup with specific credentials
builder.Services.AddSingleton<PredictionServiceClient>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<PredictionServiceClient>>();
    try
    {
        var clientBuilder = new PredictionServiceClientBuilder
        {
            Credential = vertexAICredential,
            Endpoint = "us-central1-aiplatform.googleapis.com"
        };

        var client = clientBuilder.Build();
        logger.LogInformation("Vertex AI client initialized successfully");
        return client;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize Vertex AI client");
        throw;
    }
});

// Cache configuration
builder.Services.Configure<CacheConfig>(builder.Configuration.GetSection("Cache"));

// AI and Business Rules configuration
builder.Services.Configure<VertexAIConfig>(builder.Configuration.GetSection("VertexAI"));
builder.Services.AddHttpClient<VertexAIService>();

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

// Services registration
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
    })
    .AddCheck("credentials", () =>
    {
        var results = new List<string>();
        if (File.Exists(firestoreCredentialsPath)) results.Add("Firestore: OK");
        if (File.Exists(vertexAICredentialsPath)) results.Add("VertexAI: OK");
        return HealthCheckResult.Healthy($"Credentials: {string.Join(", ", results)}");
    });

var app = builder.Build();

// CLOUD RUN: Always enable Swagger for demo
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Startup validation with proper logger access
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var aiConfig = app.Configuration.GetSection("VertexAI").Get<VertexAIConfig>();

logger.LogInformation("Starting Basket Filter API");
logger.LogInformation("Project ID: {ProjectId}", projectId);
logger.LogInformation("Firestore Credentials: {FirestorePath}", firestoreCredentialsPath);
logger.LogInformation("Vertex AI Credentials: {VertexPath}", vertexAICredentialsPath);
logger.LogInformation("Cache: Redis={RedisEnabled}", cacheConfig?.UseRedis ?? false);

if (aiConfig?.EnableAI == true)
{
    logger.LogInformation("AI Service: ENABLED (Model: {Model})", aiConfig.ModelName);

    // Test both services at startup
    try
    {
        var vertexClient = app.Services.GetRequiredService<PredictionServiceClient>();
        var firestoreDb = app.Services.GetRequiredService<FirestoreDb>();
        logger.LogInformation("Both Vertex AI and Firestore clients ready");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Service initialization failed");
    }
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