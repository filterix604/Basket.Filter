using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Basket.Filter.Models.AIModels;
using Basket.Filter.Services.Interface;
using Google.Apis.Auth.OAuth2;
using Basket.Filter.Models;

namespace Basket.Filter.Services
{
    public class VertexAIService : IVertexAIService
    {
        private readonly HttpClient _httpClient;
        private readonly VertexAIConfig _config;
        private readonly ILogger<VertexAIService> _logger;

        public VertexAIService(IOptions<VertexAIConfig> config, ILogger<VertexAIService> logger, IHttpClientFactory httpClientFactory)
        {
            _config = config.Value;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<AIClassificationResult> ClassifyProductAsync(AIClassificationRequest request)
        {
            if (!_config.EnableAI)
                return GetFallbackResult("AI disabled in config");

            try
            {
                var prompt = BuildPrompt(request);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = _config.Temperature,
                        maxOutputTokens = _config.MaxTokens,
                        topP = 0.8
                    }
                };

                string url = $"https://{_config.Location}-aiplatform.googleapis.com/v1/projects/{_config.ProjectId}/locations/{_config.Location}/publishers/google/models/{_config.ModelName}:generateContent";

                var json = JsonSerializer.Serialize(requestBody);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                // Use proper OAuth scope for Vertex AI
                var credential = await GoogleCredential.GetApplicationDefaultAsync();
                var accessToken = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync("https://www.googleapis.com/auth/cloud-platform");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(httpRequest);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Vertex AI request failed: {Status} {Body}", response.StatusCode, responseContent);
                    return GetFallbackResult($"AI error: {responseContent}");
                }

                return ParseResponse(responseContent, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Vertex AI");
                return GetFallbackResult(ex.Message);
            }
        }

        public async Task<List<AIClassificationResult>> ClassifyBatchAsync(List<AIClassificationRequest> requests)
        {
            var tasks = requests.Select(ClassifyProductAsync);
            var results = await Task.WhenAll(tasks);
            return results.ToList();
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

        public async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                return _config.EnableAI;
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

        private string BuildPrompt(AIClassificationRequest request)
        {
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

        private AIClassificationResult ParseResponse(string jsonResponse, AIClassificationRequest request)
        {
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            var text = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrEmpty(text))
                return GetFallbackResult("Empty AI response");

            try
            {
                var parsed = JsonSerializer.Deserialize<AIClassificationResult>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed == null) return GetFallbackResult("Deserialization failed");

                parsed.ModelVersion = _config.ModelName;
                parsed.DetectedCategory = request.Category;
                return parsed;
            }
            catch
            {
                return GetFallbackResult("Parse error");
            }
        }

        private AIClassificationResult GetFallbackResult(string reason) => new()
        {
            IsEligible = false,
            Confidence = 0,
            Reason = reason,
            ModelVersion = _config.ModelName,
            DetectedCategory = "fallback"
        };
    }
}
