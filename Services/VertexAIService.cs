using Basket.Filter.Models.AIModels;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class VertexAIService : IVertexAIService
{
    private readonly HttpClient _httpClient;
    private readonly VertexAIConfig _config;
    private readonly ILogger<VertexAIService> _logger;
    private readonly GoogleCredential _credential; // Cache credential

    public VertexAIService(
        IOptions<VertexAIConfig> config,
        ILogger<VertexAIService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();

        // Initialize credential once
        _credential = InitializeCredential();
    }

    private GoogleCredential InitializeCredential()
    {
        try
        {
            var vertexCredPath = Environment.GetEnvironmentVariable("VERTEX_AI_CREDENTIALS_PATH")
                               ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
                               ?? "/app/vertexai-key.json";

            if (File.Exists(vertexCredPath))
            {
                _logger.LogInformation("Using Vertex AI credentials from: {Path}", vertexCredPath);
                return GoogleCredential.FromFile(vertexCredPath)
                    .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            }
            else
            {
                _logger.LogInformation("Using default application credentials for Vertex AI");
                return GoogleCredential.GetApplicationDefault()
                    .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Vertex AI credentials");
            throw;
        }
    }

    public async Task<AIClassificationResult> ClassifyProductAsync(AIClassificationRequest request)
    {
        if (!_config.EnableAI)
            return GetFallbackResult("AI disabled in config");

        try
        {
            // ADD DETAILED LOGGING
            _logger.LogInformation("AI Config - Model: {Model}, Location: {Location}, Project: {Project}",
                _config.ModelName, _config.Location, _config.ProjectId);

            var prompt = BuildPrompt(request);
            var startTime = DateTime.UtcNow;

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

            // LOG THE FULL URL
            _logger.LogInformation("Vertex AI URL: {Url}", url);

            var json = JsonSerializer.Serialize(requestBody);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Get access token from cached credential
            var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(httpRequest);
            var responseContent = await response.Content.ReadAsStringAsync();
            var processingTime = DateTime.UtcNow - startTime;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Vertex AI request failed: {Status} {Body}", response.StatusCode, responseContent);
                return GetFallbackResult($"AI error: {responseContent} [Source: vertex_ai_fresh, Confidence: 0.00, Time: {processingTime.TotalMilliseconds}ms]");
            }

            var result = ParseResponse(responseContent, request);
            _logger.LogInformation("AI Success: {ProductName} → {IsEligible} (Confidence: {Confidence})",
                request.ProductName, result.IsEligible, result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vertex AI for: {ProductName}", request.ProductName);
            return GetFallbackResult($"AI error: {ex.Message}");
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
            return _config.EnableAI;
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

IMPORTANT: Respond with ONLY valid JSON in this exact format (no extra text, no markdown):
{{""isEligible"": true, ""confidence"": 0.95, ""reason"": ""eligible food item""}}

or

{{""isEligible"": false, ""confidence"": 0.90, ""reason"": ""contains alcohol""}}";
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

