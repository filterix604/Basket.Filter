using Google.Cloud.AIPlatform.V1;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Basket.Filter.Models.AIModels;
using ProtobufValue = Google.Protobuf.WellKnownTypes.Value;  // For building requests
using AIPlatformValue = Google.Cloud.AIPlatform.V1.Value;    // For parsing responses (if needed)

namespace Basket.Filter.Services
{
    public class VertexAIService : IVertexAIService
    {
        private readonly PredictionServiceClient? _predictionClient;
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
                try
                {
                    // Use default Google Cloud credentials (from Cloud Run service account)
                    _predictionClient = new PredictionServiceClientBuilder().Build();
                    _logger.LogInformation("Vertex AI initialized: {Model} in {Location}",
                        _config.ModelName, _config.Location);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Vertex AI");
                    _predictionClient = null;
                }
            }
            else
            {
                _logger.LogInformation("Vertex AI disabled in configuration");
            }
        }

        public async Task<AIClassificationResult> ClassifyProductAsync(AIClassificationRequest request)
        {
            if (!_config.EnableAI || _predictionClient == null)
            {
                return GetFallbackResult("AI service not available");
            }

            try
            {
                _logger.LogDebug("Classifying: {ProductName}", request.ProductName);

                var result = await CallVertexAIWithRetryAsync(request);

                _logger.LogInformation("AI Result: {ProductName} → {IsEligible} ({Confidence:F2})",
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

            if (result.Confidence < 0.7)
            {
                _logger.LogWarning("Low confidence ({Confidence:F2}), applying conservative fallback", result.Confidence);
                return new AIClassificationResult
                {
                    IsEligible = false,
                    Confidence = 0.5,
                    Reason = $"Low confidence result: {result.Reason}",
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
            if (!_config.EnableAI) return true;

            try
            {
                // Test basic connectivity without expensive API call
                return _predictionClient != null;
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

        private async Task<AIClassificationResult> CallVertexAIWithRetryAsync(AIClassificationRequest request)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
            {
                try
                {
                    return await CallVertexAIAsync(request);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning("Attempt {Attempt}/{MaxRetries} failed: {Error}",
                        attempt, _config.MaxRetries, ex.Message);

                    if (attempt < _config.MaxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                    }
                }
            }

            throw lastException ?? new Exception("AI service failed after retries");
        }

        private async Task<AIClassificationResult> CallVertexAIAsync(AIClassificationRequest request)
        {
            // Use Gemini API directly (no custom endpoint)
            var model = $"projects/{_config.ProjectId}/locations/{_config.Location}/publishers/google/models/{_config.ModelName}";

            var prompt = BuildOptimizedPrompt(request);

            // FIXED: Use ProtobufValue alias for building request
            var content = new Struct();
            content.Fields.Add("content", ProtobufValue.ForString(prompt));

            var instance = ProtobufValue.ForStruct(content);

            var parameters = new Struct();
            parameters.Fields.Add("maxOutputTokens", ProtobufValue.ForNumber(_config.MaxTokens));
            parameters.Fields.Add("temperature", ProtobufValue.ForNumber(_config.Temperature));
            parameters.Fields.Add("topP", ProtobufValue.ForNumber(0.8));

            var predictRequest = new PredictRequest
            {
                Endpoint = model,
                Instances = { instance },
                Parameters = ProtobufValue.ForStruct(parameters)
            };

            var response = await _predictionClient!.PredictAsync(predictRequest);
            return ParseResponse(response, request);
        }

        private string BuildOptimizedPrompt(AIClassificationRequest request)
        {
            // Shorter, more focused prompt to reduce token usage
            return $@"Analyze this product for meal voucher eligibility in {request.CountryCode}:

Product: {request.ProductName}
Description: {request.Description ?? "N/A"}
Category: {request.Category}
Price: €{request.Price:F2}
Contains Alcohol: {request.ContainsAlcohol}
Merchant: {request.MerchantType}

{GetCountryRules(request.CountryCode)}

Respond with ONLY this JSON:
{{""isEligible"": true/false, ""confidence"": 0.0-1.0, ""reason"": ""brief explanation""}}";
        }

        private string GetCountryRules(string countryCode)
        {
            return countryCode.ToUpper() switch
            {
                "FR" => "RULES: Food/drinks OK. Alcohol in menus <33% OK. Standalone alcohol/non-food NOT OK.",
                "BE" => "RULES: Food/drinks OK. NO alcohol allowed. Non-food NOT OK.",
                _ => "RULES: Food/drinks for consumption OK. Alcohol/non-food NOT OK."
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
                if (string.IsNullOrEmpty(content))
                {
                    throw new Exception("Empty response from AI");
                }

                var jsonContent = ExtractJsonFromContent(content);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<AIClassificationResult>(jsonContent, options);

                if (parsed == null)
                {
                    throw new Exception("Failed to deserialize AI response");
                }

                // Enrich result
                parsed.ModelVersion = _config.ModelName;
                parsed.DetectedCategory = DetermineCategory(parsed, request.Category);
                parsed.Confidence = Math.Max(0.0, Math.Min(1.0, parsed.Confidence));

                return parsed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse AI response");
                return GetFallbackResult($"Parse error: {ex.Message}");
            }
        }

        // FIXED: Use ProtobufValue for response parsing (this is what response.Predictions returns)
        private string ExtractContent(ProtobufValue prediction)
        {
            if (prediction.KindCase == ProtobufValue.KindOneofCase.StructValue)
            {
                var fields = prediction.StructValue.Fields;

                // Try common response fields
                foreach (var field in new[] { "content", "text", "output" })
                {
                    if (fields.ContainsKey(field) &&
                        fields[field].KindCase == ProtobufValue.KindOneofCase.StringValue)
                    {
                        return fields[field].StringValue;
                    }
                }

                // Try candidates structure
                if (fields.ContainsKey("candidates") &&
                    fields["candidates"].KindCase == ProtobufValue.KindOneofCase.ListValue &&
                    fields["candidates"].ListValue.Values.Count > 0)
                {
                    var candidate = fields["candidates"].ListValue.Values[0];
                    if (candidate.KindCase == ProtobufValue.KindOneofCase.StructValue &&
                        candidate.StructValue.Fields.ContainsKey("content"))
                    {
                        return candidate.StructValue.Fields["content"].StringValue;
                    }
                }
            }

            if (prediction.KindCase == ProtobufValue.KindOneofCase.StringValue)
            {
                return prediction.StringValue;
            }

            return prediction.ToString();
        }

        private string ExtractJsonFromContent(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                throw new Exception("Empty content from AI");
            }

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                return content.Substring(jsonStart, jsonEnd - jsonStart);
            }

            // Create fallback response if no JSON found
            var fallback = new
            {
                isEligible = false,
                confidence = 0.3,
                reason = "Could not parse AI response"
            };

            return JsonSerializer.Serialize(fallback);
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

            return result.Reason.ToLowerInvariant().Contains("meal") ? "prepared_meal" : originalCategory;
        }

        private AIClassificationResult GetFallbackResult(string reason)
        {
            return new AIClassificationResult
            {
                IsEligible = false,
                Confidence = 0.0,
                Reason = reason,
                ModelVersion = _config.ModelName,
                DetectedCategory = "fallback"
            };
        }
    }
}