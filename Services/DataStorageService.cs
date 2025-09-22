using Google.Cloud.Firestore;
using Basket.Filter.Models;
using Basket.Filter.Services.Interfaces;
using Basket.Filter.Services;
using Google.Cloud.Firestore.V1;
using System.Text.Json;
using Basket.Filter.Mappers;
using static Google.Rpc.Context.AttributeContext.Types;

namespace Basket.Filter.Infrastructure.Services;

public class DataStorageService : IDataStorageService
{
	private readonly FirestoreDb _firestoreDb;
	private readonly ILogger<CatalogService> _logger;
	private readonly IBasketRequestMapper _mapper;

	private const string REQUESTS_COLLECTION = "basket-requests";
	private const string RESPONSES_COLLECTION = "basket-responses";

	public DataStorageService(FirestoreDb firestore, ILogger<CatalogService> logger, IBasketRequestMapper basketRequestMapper)
	{
		_firestoreDb = firestore;
		_logger = logger;
		_mapper = basketRequestMapper;
	}

	public async Task StoreTransactionAsync(BasketRequest request, BasketFilteringResponse response)
	{
		if (_firestoreDb == null)
		{
			_logger.LogWarning("Firestore not available, cannot store transaction {BasketId}",
				request.TransactionData.BasketId);
			return;
		}

		try
		{
			var collection = _firestoreDb.Collection("basket_transactions");

			var summary = new
			{
				basketId = request.TransactionData.BasketId,
				transactionId = request.TransactionData.TransactionId,
				merchantId = request.MerchantData.MerchantId,
				merchantName = request.MerchantData.MerchantName,
				merchantType = request.MerchantData.MerchantType,
				timestamp = request.TransactionData.Timestamp,
				processedAt = DateTimeOffset.UtcNow,
				totalAmount = (double)response.TotalAmount,
				eligibleAmount = (double)response.EligibleAmount,
				ineligibleAmount = (double)response.IneligibleAmount,
				isFullyEligible = response.IsFullyEligible,
				itemCount = request.BasketItems.Count,
				//eligibleItemCount = response.CategorizedItems.Count(i => i.Eligible),
				currencyCode = request.TransactionData.CurrencyCode,
				countryCode = request.TransactionData.CountryCode,
				reasonIfNotEligible = response.ReasonIfNotEligible ?? ""
			};
			// Store transaction data
			await collection.Document(request.TransactionData.BasketId).SetAsync(summary);

			_logger.LogDebug("Stored transaction summary {BasketId} in Firestore", request.TransactionData.BasketId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to store transaction {BasketId}", request.TransactionData.BasketId);
			// Don't throw - this is not critical for the response
		}
	}

	public async Task StoreBasketRequestAsync(BasketRequest request, CancellationToken cancellationToken = default)
	{
		try
		{
			var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				WriteIndented = true
			});
			// Check if basket already exists
			var existingBasket = await _firestoreDb.Collection(REQUESTS_COLLECTION)
				.Document(request.TransactionData.BasketId)
				.GetSnapshotAsync();

			if (existingBasket.Exists)
			{
				_logger.LogError("Basket {BasketId} already exists, cannot overwrite", request.TransactionData.BasketId);
				throw new InvalidOperationException($"Basket with ID {request.TransactionData.BasketId} already exists.");
			}
			var firestoreModel = _mapper.MapBasketRequestToFirestore(request);

			await _firestoreDb.Collection(REQUESTS_COLLECTION)
					.Document(request.TransactionData.BasketId)
					.SetAsync(firestoreModel);

			_logger.LogInformation("Saved basket request with ID: {DocumentId}", firestoreModel.TransactionData.BasketId);


		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error saving basket request to Firestore");
			throw;
		}
	}

	public async Task StoreBasketResponseAsync(BasketFilteringResponse response, CancellationToken cancellationToken = default)
	{
		try
		{

			//var responseId = Guid.NewGuid().ToString();
			await _firestoreDb.Collection(RESPONSES_COLLECTION)
					.Document(response.BasketId)
					.SetAsync(response);

			_logger.LogInformation("Saved basket response with ID: {DocumentId}", response.BasketId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error saving basket response to Firestore");
			throw;
		}
	}
}
