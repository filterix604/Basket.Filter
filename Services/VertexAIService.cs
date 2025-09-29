using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Basket.Filter.Models.AIModels;

namespace Basket.Filter.Services
{
    public class VertexAIService : IVertexAIService, IDisposable
    {
        private readonly VertexAIConfig _config;
        private readonly ILogger<VertexAIService> _logger;
        private readonly HttpClient _httpClient;
        private string? _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public VertexAIService(
            IOptions<VertexAIConfig> config,
            ILogger<VertexAIService> logger,
            HttpClient httpClient)
        {
            _config = config.Value;
            _logger = logger;
            _httpClient = httpClient;

            if (_config.EnableAI)
            {
                _logger.LogInformation("Vertex AI HTTP client initialized: {Model} in {Location}",
                    _config.ModelName, _config.Location);
            }
        }

        public async Task<AIClassificationResult> ClassifyProductAsync(AIClassificationRequest request)
        {
            if (!_config.EnableAI)
                return GetFallbackResult("AI service not available");

            try
            {
                _logger.LogDebug("Classifying: {ProductName}", request.ProductName);
                var result = await CallVertexAIWithRetryAsync(request);
                _logger.LogInformation("AI Result: {ProductName} -> {IsEligible} ({Confidence:F2})",
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
                var token = await GetAccessTokenAsync();
                return !string.IsNullOrEmpty(token);
            }
            catch { return false; }
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
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }
            throw lastException ?? new Exception("AI service failed after retries");
        }

        private async Task<AIClassificationResult> CallVertexAIAsync(AIClassificationRequest request)
        {
            var accessToken = await GetAccessTokenAsync();
            var prompt = BuildOptimizedPrompt(request);

            // Try multiple model formats and endpoints
            var endpoints = new[]
            {
                $"https://{_config.Location}-aiplatform.googleapis.com/v1/projects/{_config.ProjectId}/locations/{_config.Location}/publishers/google/models/{_config.ModelName}:generateContent",
                $"https://{_config.Location}-aiplatform.googleapis.com/v1/projects/{_config.ProjectId}/locations/{_config.Location}/publishers/google/models/text-bison:generateContent",
                $"https://{_config.Location}-aiplatform.googleapis.com/v1/projects/{_config.ProjectId}/locations/{_config.Location}/publishers/google/models/text-bison-001:generateContent"
            };

            Exception? lastException = null;

            foreach (var url in endpoints)
            {
                try
                {
                    _logger.LogDebug("Trying endpoint: {Url}", url);

                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                role = "user",
                                parts = new[] { new { text = prompt } }
                            }
                        },
                        generationConfig = new
                        {
                            maxOutputTokens = _config.MaxTokens,
                            temperature = _config.Temperature,
                            topP = 0.8
                        }
                    };

                    var jsonContent = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        Encoding.UTF8,
                        "application/json");

                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                    var response = await _httpClient.PostAsync(url, jsonContent);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return ParseGeminiResponse(responseContent, request);
                    }

                    _logger.LogWarning("Endpoint failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning("Endpoint failed with exception: {Error}", ex.Message);
                }
            }

            throw lastException ?? new Exception("All AI endpoints failed");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
                return _accessToken;

            var credential = await Google.Apis.Auth.OAuth2.GoogleCredential.GetApplicationDefaultAsync();
            var scoped = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            _accessToken = await scoped.UnderlyingCredential.GetAccessTokenForRequestAsync();
            _tokenExpiry = DateTime.UtcNow.AddMinutes(50);
            return _accessToken;
        }

        private AIClassificationResult ParseGeminiResponse(string responseContent, AIClassificationRequest request)
        {
            try
            {
                using var document = JsonDocument.Parse(responseContent);
                var root = document.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    if (candidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        var part = parts[0];
                        if (part.TryGetProperty("text", out var textElement))
                        {
                            var text = textElement.GetString();
                            return ParseAIResponse(text, request);
                        }
                    }
                }

                throw new Exception("No valid response content found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse response");
                return GetFallbackResult($"Parse error: {ex.Message}");
            }
        }

        private AIClassificationResult ParseAIResponse(string content, AIClassificationRequest request)
        {
            try
            {
                var jsonContent = ExtractJsonFromContent(content);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<AIClassificationResult>(jsonContent, options);

                if (parsed == null)
                    throw new Exception("Failed to deserialize AI response");

                parsed.ModelVersion = _config.ModelName;
                parsed.DetectedCategory = DetermineCategory(parsed, request.Category);
                parsed.Confidence = Math.Clamp(parsed.Confidence, 0.0, 1.0);

                return parsed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse AI response");
                return GetFallbackResult($"Parse error: {ex.Message}");
            }
        }

        private string BuildOptimizedPrompt(AIClassificationRequest request)
        {
            return $@"Analyze this product for meal voucher eligibility in {request.CountryCode}:

Product: {request.ProductName}
Description: {request.Description ?? "N/A"}
Category: {request.Category}
Price: €{request.Price:F2}
Contains Alcohol: {request.ContainsAlcohol}
Merchant: {request.MerchantType}

RULES for {request.CountryCode}: Food/drinks for consumption OK. Alcohol/non-food NOT OK.

Respond with ONLY this JSON:
{{""isEligible"": true/false, ""confidence"": 0.0-1.0, ""reason"": ""brief explanation""}}";
        }

        private string ExtractJsonFromContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                throw new Exception("Empty content from AI");

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
                return content.Substring(jsonStart, jsonEnd - jsonStart);

            return JsonSerializer.Serialize(new
            {
                isEligible = false,
                confidence = 0.3,
                reason = "Could not parse AI response"
            });
        }

        private string DetermineCategory(AIClassificationResult result, string originalCategory)
        {
            if (!result.IsEligible)
                return result.Reason?.ToLowerInvariant().Contains("alcohol") == true ? "alcoholic" : "ineligible";
            return result.Reason?.ToLowerInvariant().Contains("meal") == true ? "prepared_meal" : originalCategory;
        }

        private AIClassificationResult GetFallbackResult(string reason)
        {
            return new AIClassificationResult
            {
                IsEligible = false,
                Confidence = 0.0,
                Reason = reason,
                ModelVersion = _config.ModelName ?? "unknown",
                DetectedCategory = "fallback"
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}