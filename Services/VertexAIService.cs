using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Basket.Filter.Models.AIModels;

namespace Basket.Filter.Services
{
    public class VertexAIService : IVertexAIService
    {
        private readonly PredictionServiceClient _predictionClient;
        private readonly VertexAIConfig _config;
        private readonly ILogger<VertexAIService> _logger;

        public VertexAIService(
            IOptions<VertexAIConfig> config,
            ILogger<VertexAIService> logger)
        {
            _config = config.Value;
            _logger = logger;

            if (_config.EnableAI)
            {
                var clientBuilder = new PredictionServiceClientBuilder
                {
                    CredentialsPath = _config.CredentialsPath
                };
                _predictionClient = clientBuilder.Build();
                _logger.LogInformation("Vertex AI service initialized");
            }
            else
            {
                _logger.LogWarning("Vertex AI service disabled in configuration");
            }
        }

        public async Task<AIClassificationResult> ClassifyProductAsync(AIClassificationRequest request)
        {
            if (!_config.EnableAI)
            {
                return GetFallbackResult("AI service disabled");
            }

            try
            {
                _logger.LogInformation("Calling Vertex AI for: {ProductName}", request.ProductName);

                var result = await CallVertexAIWithRetryAsync(request);

                _logger.LogInformation("AI Classification: {ProductName} → Eligible: {IsEligible} (Confidence: {Confidence})",
                    request.ProductName, result.IsEligible, result.Confidence);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI Classification failed: {ProductName}", request.ProductName);
                return GetFallbackResult($"AI error: {ex.Message}");
            }
        }

        public async Task<AIClassificationResult> ClassifyWithFallbackAsync(AIClassificationRequest request)
        {
            var result = await ClassifyProductAsync(request);

            // If confidence is too low, apply conservative fallback
            if (result.Confidence < 0.7)
            {
                _logger.LogWarning("Low confidence AI result ({Confidence}), applying conservative fallback", result.Confidence);
                return new AIClassificationResult
                {
                    IsEligible = false, // Conservative: reject uncertain items
                    Confidence = 0.5,
                    Reason = $"Low confidence AI result: {result.Reason}",
                    ModelVersion = result.ModelVersion,
                    DetectedCategory = "uncertain"
                };
            }

            return result;
        }

        public async Task<List<AIClassificationResult>> ClassifyBatchAsync(List<AIClassificationRequest> requests)
        {
            var tasks = requests.Select(ClassifyProductAsync);
            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        public async Task<bool> IsServiceHealthyAsync()
        {
            if (!_config.EnableAI) return true; // Disabled = healthy

            try
            {
                var testRequest = new AIClassificationRequest
                {
                    ProductName = "Health Check Product",
                    Description = "Test product for service health verification",
                    Category = "test"
                };

                var result = await CallVertexAIAsync(testRequest);
                return result != null && !string.IsNullOrEmpty(result.Reason);
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetModelVersionAsync()
        {
            return await Task.FromResult(_config.ModelName);
        }

        // Private implementation methods
        private async Task<AIClassificationResult> CallVertexAIWithRetryAsync(AIClassificationRequest request)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
            {
                try
                {
                    return await CallVertexAIAsync(request);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning("AI call attempt {Attempt}/{MaxRetries} failed: {Error}",
                        attempt, _config.MaxRetries, ex.Message);

                    if (attempt < _config.MaxRetries)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                        await Task.Delay(delay);
                    }
                }
            }

            throw lastException ?? new Exception("AI service failed after retries");
        }

        private async Task<AIClassificationResult> CallVertexAIAsync(AIClassificationRequest request)
        {
            var endpoint = EndpointName.FromProjectLocationEndpoint(
                _config.ProjectId,
                _config.Location,
                _config.EndpointId);

            var prompt = BuildPrompt(request);

            // Use fully qualified Protobuf Value for building request
            var instance = Google.Protobuf.WellKnownTypes.Value.ForStruct(new Struct
            {
                Fields =
                {
                    ["prompt"] = Google.Protobuf.WellKnownTypes.Value.ForString(prompt),
                    ["max_tokens"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(_config.MaxTokens),
                    ["temperature"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(_config.Temperature)
                }
            });

            var predictRequest = new PredictRequest
            {
                Endpoint = endpoint.ToString(),
                Instances = { instance }
            };

            var response = await _predictionClient.PredictAsync(predictRequest);
            return ParseResponse(response, request);
        }

        private string BuildPrompt(AIClassificationRequest request)
        {
            return $@"
You are an AI assistant specialized in determining meal voucher eligibility for products in {request.CountryCode}.

Product Information:
- Name: {request.ProductName}
- Description: {request.Description}
- Category: {request.Category}
- Subcategory: {request.SubCategory}
- Price: €{request.Price:F2}
- Contains Alcohol: {request.ContainsAlcohol}
- Alcohol Volume: {request.AlcoholByVolume?.ToString() ?? "N/A"}%
- Allergens: {string.Join(", ", request.Allergens)}
- Merchant Type: {request.MerchantType}

Context for {request.CountryCode}:
{GetCountrySpecificGuidance(request.CountryCode)}

CRITICAL: Respond with ONLY a JSON object:
{{
    ""isEligible"": true/false,
    ""confidence"": 0.0-1.0,
    ""reason"": ""clear explanation""
}}";
        }

        private string GetCountrySpecificGuidance(string countryCode)
        {
            return countryCode.ToUpper() switch
            {
                "FR" => @"
French Meal Voucher Rules:
ELIGIBLE: Fresh food, prepared meals, beverages (non-alcoholic), bread, dairy
MENUS WITH ALCOHOL: Allowed if alcohol < 33% of menu value
NOT ELIGIBLE: Standalone alcohol, tobacco, non-food items, services",

                "BE" => @"
Belgian Meal Voucher Rules:
ELIGIBLE: Prepared meals, fresh food, non-alcoholic beverages
NOT ELIGIBLE: ALL alcohol products, tobacco, non-food items",

                _ => @"
General Rules:
ELIGIBLE: Food and beverages for immediate consumption
NOT ELIGIBLE: Non-food items, alcohol, tobacco, services"
            };
        }

        private AIClassificationResult ParseResponse(PredictResponse response, AIClassificationRequest request)
        {
            try
            {
                var prediction = response.Predictions.FirstOrDefault();
                if (prediction == null)
                {
                    throw new Exception("No predictions in response");
                }

                string content = ExtractContent(prediction);
                var jsonContent = ExtractJsonFromContent(content);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var parsed = JsonSerializer.Deserialize<AIClassificationResult>(jsonContent, options);
                if (parsed == null)
                {
                    throw new Exception("Failed to deserialize AI response");
                }

                // Enrich the result
                parsed.ModelVersion = _config.ModelName;
                parsed.DetectedCategory = DetermineCategory(parsed, request.Category);
                parsed.Metadata = new Dictionary<string, object>
                {
                    ["rawResponse"] = content,
                    ["requestId"] = Guid.NewGuid().ToString(),
                    ["processedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                };

                // Validate and normalize
                parsed.Confidence = Math.Max(0.0, Math.Min(1.0, parsed.Confidence));

                return parsed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse AI response");
                return GetFallbackResult($"Parse error: {ex.Message}");
            }
        }

        // FIXED: Use Protobuf Value (the correct type from response.Predictions)
        private string ExtractContent(Google.Protobuf.WellKnownTypes.Value prediction)
        {
            // Check for struct value with fields
            if (prediction.KindCase == Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StructValue)
            {
                var structValue = prediction.StructValue;
                if (structValue.Fields.ContainsKey("content"))
                    return structValue.Fields["content"].StringValue;
                if (structValue.Fields.ContainsKey("text"))
                    return structValue.Fields["text"].StringValue;
                if (structValue.Fields.ContainsKey("candidates"))
                {
                    var candidates = structValue.Fields["candidates"];
                    if (candidates.KindCase == Google.Protobuf.WellKnownTypes.Value.KindOneofCase.ListValue &&
                        candidates.ListValue.Values.Count > 0)
                    {
                        var firstCandidate = candidates.ListValue.Values[0];
                        if (firstCandidate.KindCase == Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StructValue &&
                            firstCandidate.StructValue.Fields.ContainsKey("content"))
                        {
                            return firstCandidate.StructValue.Fields["content"].StringValue;
                        }
                    }
                }
            }

            // Check for direct string value
            if (prediction.KindCase == Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StringValue)
            {
                return prediction.StringValue;
            }

            // Fallback: try to get any string representation
            return prediction.ToString();
        }

        private string ExtractJsonFromContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                throw new Exception("Empty content received from AI");
            }

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                return content.Substring(jsonStart, jsonEnd - jsonStart);
            }

            throw new Exception($"No valid JSON found in response: {content}");
        }

        private string DetermineCategory(AIClassificationResult result, string originalCategory)
        {
            if (!result.IsEligible)
            {
                if (result.Reason.ToLowerInvariant().Contains("alcohol"))
                    return "alcoholic";
                if (result.Reason.ToLowerInvariant().Contains("non-food"))
                    return "non_food";
                return "ineligible";
            }

            if (result.Reason.ToLowerInvariant().Contains("meal") ||
                result.Reason.ToLowerInvariant().Contains("food"))
                return "prepared_meal";

            return originalCategory;
        }

        private AIClassificationResult GetFallbackResult(string reason)
        {
            return new AIClassificationResult
            {
                IsEligible = false, // Conservative fallback
                Confidence = 0.0,
                Reason = reason,
                ModelVersion = _config.ModelName,
                DetectedCategory = "fallback"
            };
        }
    }
}