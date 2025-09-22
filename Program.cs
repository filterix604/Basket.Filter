using Basket.Filter.Services.Interface;
using Basket.Filter.Services;
using Google.Cloud.Firestore;
using Basket.Filter.Infrastructure.Services;
using Basket.Filter.Services.Interfaces;
using Basket.Filter.Mappers;

var builder = WebApplication.CreateBuilder(args);
var projectId = "basket-filter-engine"; // Replace with your project ID
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "firestore-key.json");
builder.Services.AddSingleton(FirestoreDb.Create(projectId));

// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddScoped<IEligibilityRulesService, EligibilityRulesService>();
builder.Services.AddScoped<IBasketFilteringService, BasketFilteringService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IDataStorageService, DataStorageService>();

builder.Services.AddScoped<IEligibilityRulesService, EligibilityRulesService>();
builder.Services.AddScoped<IDataSeedingService, DataSeedingService>();
builder.Services.AddScoped<IBusinessRulesEngine, BusinessRulesEngine>();
builder.Services.AddScoped<IMerchantOnboardingService, MerchantOnboardingService>();
builder.Services.AddScoped<IBasketRequestMapper, BasketRequestMapper>();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
	var seedingService = scope.ServiceProvider.GetRequiredService<IDataSeedingService>();
	try
	{
		await seedingService.SeedInitialDataAsync();
	}
	catch (Exception ex)
	{
		var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
		logger.LogError(ex, "Error during data seeding");

	}
}


app.Run();
