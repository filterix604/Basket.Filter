using Basket.Filter.Models;
using Basket.Filter.Models.AIModels;
using Basket.Filter.Models.Rules;
using Basket.Filter.Services.Interface;
using Basket.Filter.Services.Interfaces;

namespace Basket.Filter.Services
{
    public class BasketFilteringService : IBasketFilteringService
    {
        private readonly IEligibilityRulesService _rulesService;
        private readonly ICatalogService _catalogService;
        private readonly IDataStorageService _storageService;
        private readonly IVertexAIService _vertexAIService;
        private readonly ILogger<BasketFilteringService> _logger;

        public BasketFilteringService(
            IEligibilityRulesService rulesService,
            ICatalogService catalogService,
            IDataStorageService storageService,
            IVertexAIService vertexAIService,
            ILogger<BasketFilteringService> logger)
        {
            _rulesService = rulesService;
            _catalogService = catalogService;
            _storageService = storageService;
            _vertexAIService = vertexAIService;
            _logger = logger;
        }

        public async Task<BasketFilteringResponse> FilterBasketAsync(BasketRequest request)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Processing basket: {BasketId} with {ItemCount} items",
                request.TransactionData.BasketId, request.BasketItems.Count);

            try
            {
                var rules = await _rulesService.GetRulesForMerchantAsync(request.MerchantData.MerchantId);
                var categorizedItems = new List<CategorizedItem>();
                decimal eligibleAmount = 0;

                var tasks = request.BasketItems.Select(item => CategorizeItemAsync(item, rules, request));
                var results = await Task.WhenAll(tasks);
                categorizedItems.AddRange(results);

                // Calculate eligible amount
                foreach (var categorizedItem in categorizedItems)
                {
                    if (categorizedItem.IsEligible)
                    {
                        eligibleAmount += (decimal)categorizedItem.PricingData.TotalPriceAmount / 100;
                    }
                }

                // Fix: Cast to decimal BEFORE division to avoid integer division
                var fees = request.AdditionalCharges?.Select(c => new Fee
                {
                    Type = c.ChargeType,
                    Name = c.ChargeName,
                    Amount = (double)((decimal)c.ChargeAmount / 100), // Cast to decimal for division, then to double
                    CurrencyCode = c.CurrencyCode
                }).ToList() ?? new List<Fee>();

                var totalFeesAmount = fees.Sum(f => f.Amount);
                var totalAmount = (decimal)request.BasketTotals.TotalAmount / 100;

                decimal ineligibleItemsAmount = 0;
                foreach (var categorizedItem in categorizedItems)
                {
                    if (!categorizedItem.IsEligible)
                    {
                        ineligibleItemsAmount += (decimal)categorizedItem.PricingData.TotalPriceAmount / 100;
                    }
                }

                // Fix: Calculate ineligible amount to ensure totals always balance
                var ineligibleAmount = totalAmount - eligibleAmount;

                // Basket is fully eligible if:
                // 1. No ineligible items (all items are food/beverage)
                // 2. Fees are allowed to be excluded (they don't count against eligibility)
                var hasIneligibleItems = categorizedItems.Any(i => !i.IsEligible);
				var isFullyEligible = !hasIneligibleItems;

				string reasonIfNotEligible = null;
				if (!isFullyEligible)
				{
					if (eligibleAmount == 0)
					{
						reasonIfNotEligible = "Basket contains no eligible items";
					}
					else if (ineligibleItemsAmount > 0 && (decimal)totalFeesAmount > 0)
					{
						reasonIfNotEligible = "Contains non-food items and delivery fees";
					}
					else if (ineligibleItemsAmount > 0)
					{
						var ineligibleItems = categorizedItems.Where(i => !i.IsEligible).ToList();
						var categories = string.Join(", ", ineligibleItems.Select(i => i.DetectedCategory).Distinct());
						reasonIfNotEligible = $"Contains ineligible items: {categories}";
					}
					else if ((decimal)totalFeesAmount > 0)
					{
						reasonIfNotEligible = $"Delivery/service fees excluded (€{totalFeesAmount:F2})";
					}
				}

				if (eligibleAmount > (decimal)rules.MaxDailyAmount)
				{
					var originalAmount = eligibleAmount;
					var exceededAmount = eligibleAmount - (decimal)rules.MaxDailyAmount;
					eligibleAmount = (decimal)rules.MaxDailyAmount;

					ineligibleAmount += exceededAmount;

					_logger.LogInformation("Daily limit applied: {Original} → {Limited}, Excess: {Excess}",
						originalAmount, eligibleAmount, exceededAmount);

					if (isFullyEligible)
					{
						isFullyEligible = false;
						reasonIfNotEligible = $"Daily limit exceeded by €{exceededAmount:F2}";
					}
				}

				var response = new BasketFilteringResponse
				{
					BasketId = request.TransactionData.BasketId,
					TotalAmount = (double)totalAmount,
					EligibleAmount = (double)eligibleAmount,
					IneligibleAmount = (double)ineligibleAmount,
					CategorizedItems = categorizedItems,
					IneligibleFees = fees,
					IsFullyEligible = isFullyEligible,
					ReasonIfNotEligible = reasonIfNotEligible
				};

				var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation("Basket processed: {BasketId} in {ProcessingTime}ms - Eligible: €{EligibleAmount}/€{TotalAmount}",
                    request.TransactionData.BasketId, processingTime.TotalMilliseconds, eligibleAmount, totalAmount);

                await _storageService.StoreTransactionAsync(request, response);
                await _storageService.StoreBasketResponseAsync(response);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing basket: {BasketId}", request.TransactionData.BasketId);
                throw;
            }
        }

        private async Task<CategorizedItem> CategorizeItemAsync(BasketItem item, MerchantEligibilityRules rules, BasketRequest request)
        {
            var startTime = DateTime.UtcNow;
            var sku = item.ItemData.Sku;

            var categorizedItem = new CategorizedItem
            {
                ItemData = item.ItemData,
                CategoryData = item.CategoryData,
                PricingData = item.PricingData,
                ItemAttributes = item.ItemAttributes
            };

            try
            {
                //  STEP 1: Check Catalog (with AI data) - HIGHEST PRIORITY
                _logger.LogDebug("Step 1 - Checking catalog for: {Sku}", sku);
                var catalogItem = await _catalogService.GetItemBySkuAsync(sku);

                if (catalogItem?.AIClassification != null)
                {
                    _logger.LogInformation("CACHE HIT (AI): SKU={Sku}, Confidence={Confidence}",
                        sku, catalogItem.AIClassification.Confidence);

                    categorizedItem.IsEligible = catalogItem.AIClassification.IsEligible;
                    categorizedItem.EligibilityReason = catalogItem.AIClassification.Reason;
                    categorizedItem.DetectedCategory = catalogItem.NormalizedCategory;

                    AddProcessingMetadata(categorizedItem, "cache", startTime, catalogItem.AIClassification.Confidence);
                    return categorizedItem;
                }

                if (catalogItem != null)
                {
                    // Found in catalog but no AI classification - use catalog rules
                    _logger.LogInformation("CATALOG HIT (Rules): SKU={Sku}, Category={Category}",
                        sku, catalogItem.NormalizedCategory);

                    categorizedItem.DetectedCategory = catalogItem.NormalizedCategory;
                    categorizedItem.IsEligible = catalogItem.NormalizedCategory != "alcoholic" &&
                                                catalogItem.NormalizedCategory != "non_food";
                    categorizedItem.EligibilityReason = $"Catalog match: {catalogItem.NormalizedCategory}";

                    AddProcessingMetadata(categorizedItem, "catalog", startTime, 0.9);
                    return categorizedItem;
                }

                // STEP 2: Catalog Miss - Try Rules Engine
                _logger.LogDebug("Step 2 - Rules engine evaluation for: {Sku}", sku);
                var rulesResult = await EvaluateWithRulesEngine(item, rules);

                if (rulesResult.HasDefinitiveResult)
                {
                    _logger.LogInformation("RULES HIT: SKU={Sku}, Result={Result}, Confidence={Confidence}",
                        sku, rulesResult.IsEligible, rulesResult.Confidence);

                    categorizedItem.IsEligible = rulesResult.IsEligible;
                    categorizedItem.EligibilityReason = rulesResult.Reason;
                    categorizedItem.DetectedCategory = rulesResult.DetectedCategory;

                    AddProcessingMetadata(categorizedItem, "rules", startTime, rulesResult.Confidence);
                    return categorizedItem;
                }

                // STEP 3: Rules Engine Uncertain - Call AI
                _logger.LogInformation("Step 3 - AI classification for: {Sku}", sku);
                var aiResult = await ClassifyWithAI(item, request);

                // STEP 4: Apply Business Rules to AI Result (Safety Net)
                var finalResult = await ApplyBusinessRulesToAIResult(aiResult, item, rules);

                categorizedItem.IsEligible = finalResult.IsEligible;
                categorizedItem.EligibilityReason = finalResult.Reason;
                categorizedItem.DetectedCategory = finalResult.DetectedCategory;

                // STEP 5: Store AI result in catalog for future use (Learning)
                await StoreAIResultInCatalog(sku, finalResult, item);

                AddProcessingMetadata(categorizedItem, "vertex_ai", startTime, finalResult.Confidence);
                return categorizedItem;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR processing item: {Sku}", sku);

                categorizedItem.IsEligible = false;
                categorizedItem.EligibilityReason = $"Processing error: {ex.Message}";
                categorizedItem.DetectedCategory = "error";

                AddProcessingMetadata(categorizedItem, "error", startTime, 0.0);
                return categorizedItem;
            }
        }

        private async Task<RulesEvaluationResult> EvaluateWithRulesEngine(BasketItem item, MerchantEligibilityRules rules)
        {
            try
            {
                string categoryToCheck = item.CategoryData.PrimaryCategory;

                var matchingRule = rules.CategoryRules.FirstOrDefault(r =>
                    r.CategoryName == categoryToCheck ||
                    r.Keywords.Any(k => item.ItemData.ItemName.ToLowerInvariant().Contains(k.ToLowerInvariant()) ||
                                       (item.ItemData.ItemDescription?.ToLowerInvariant().Contains(k.ToLowerInvariant()) ?? false)));

                if (matchingRule != null)
                {
                    return new RulesEvaluationResult
                    {
                        HasDefinitiveResult = true,
                        IsEligible = matchingRule.IsEligible,
                        Confidence = 0.95,
                        Reason = matchingRule.IsEligible ?
                            $"Eligible {matchingRule.CategoryName}" :
                            $"Not eligible: {matchingRule.CategoryName}",
                        DetectedCategory = matchingRule.CategoryName,
                        RuleName = matchingRule.CategoryName
                    };
                }

                var prohibitedKeywords = new[] { "alcohol", "wine", "beer", "tobacco", "cigarette", "vodka", "whiskey", "rum", "gin" };
                var itemText = $"{item.ItemData.ItemName} {item.ItemData.ItemDescription}".ToLowerInvariant();

                foreach (var keyword in prohibitedKeywords)
                {
                    if (itemText.Contains(keyword))
                    {
                        return new RulesEvaluationResult
                        {
                            HasDefinitiveResult = true,
                            IsEligible = false,
                            Confidence = 1.0,
                            Reason = $"Contains prohibited keyword: {keyword}",
                            DetectedCategory = "prohibited",
                            RuleName = "prohibition_rule"
                        };
                    }
                }

                // Check alcohol attributes
                if (item.ItemAttributes.ContainsAlcohol && !rules.AllowAlcoholInCombos)
                {
                    return new RulesEvaluationResult
                    {
                        HasDefinitiveResult = true,
                        IsEligible = false,
                        Confidence = 1.0,
                        Reason = "Alcohol not allowed for this merchant type",
                        DetectedCategory = "alcohol_prohibited",
                        RuleName = "merchant_alcohol_rule"
                    };
                }

                // Rules engine uncertain - let AI decide
                return new RulesEvaluationResult
                {
                    HasDefinitiveResult = false,
                    Reason = "No matching rules found - requires AI analysis",
                    Confidence = 0.0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rules evaluation for: {Sku}", item.ItemData.Sku);
                return new RulesEvaluationResult
                {
                    HasDefinitiveResult = false,
                    Reason = $"Rules evaluation error: {ex.Message}",
                    Confidence = 0.0
                };
            }
        }

        private async Task<AIClassificationResult> ClassifyWithAI(BasketItem item, BasketRequest request)
        {
            try
            {
                var aiRequest = new AIClassificationRequest
                {
                    ProductName = item.ItemData.ItemName,
                    Description = item.ItemData.ItemDescription ?? "",
                    Category = item.CategoryData.PrimaryCategory,
                    SubCategory = item.CategoryData.SubCategory ?? "",
                    Price = (decimal)item.PricingData.UnitPriceAmount / 100,
                    ContainsAlcohol = item.ItemAttributes.ContainsAlcohol,
                    AlcoholByVolume = item.ItemAttributes.AlcoholByVolume,
                    Allergens = item.ItemAttributes.AllergenInfo ?? new List<string>(),
                    MerchantType = request.MerchantData.MerchantType,
                    CountryCode = request.TransactionData.CountryCode
                };

                var aiResult = await _vertexAIService.ClassifyWithFallbackAsync(aiRequest);

                return new AIClassificationResult
                {
                    IsEligible = aiResult.IsEligible,
                    Confidence = aiResult.Confidence,
                    Reason = aiResult.Reason,
                    ModelVersion = aiResult.ModelVersion,
                    DetectedCategory = DetermineCategory(aiResult, item.CategoryData.PrimaryCategory),
                    Metadata = aiResult.Metadata
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI classification failed for: {Sku}", item.ItemData.Sku);
                return new AIClassificationResult
                {
                    IsEligible = false,
                    Confidence = 0.0,
                    Reason = $"AI classification error: {ex.Message}",
                    DetectedCategory = "error"
                };
            }
        }

        private async Task<AIClassificationResult> ApplyBusinessRulesToAIResult(
            AIClassificationResult aiResult,
            BasketItem item,
            MerchantEligibilityRules rules)
        {
            try
            {
                // Even if AI says eligible, check absolute business prohibitions
                if (aiResult.IsEligible)
                {
                    // Check alcohol rules
                    if (item.ItemAttributes.ContainsAlcohol && !rules.AllowAlcoholInCombos)
                    {
                        _logger.LogInformation("Business rule override: Alcohol not allowed for merchant");
                        return new AIClassificationResult
                        {
                            IsEligible = false,
                            Confidence = Math.Max(aiResult.Confidence, 0.9),
                            Reason = "Business rule override: Alcohol not allowed for this merchant",
                            DetectedCategory = "alcohol_prohibited",
                            ModelVersion = aiResult.ModelVersion
                        };
                    }

                    // Check combo alcohol percentage rules (French 33% rule)
                    if (item.ItemAttributes.IsComboItem && item.ItemAttributes.ContainsAlcohol)
                    {
                        var alcoholRule = rules.CategoryRules.FirstOrDefault(r => r.CategoryId == "menu_avec_alcool");
                        if (alcoholRule != null && item.ItemAttributes.AlcoholByVolume > alcoholRule.MaxAlcoholPercentage)
                        {
                            _logger.LogInformation("Business rule override: Alcohol percentage too high ({AlcoholPercent}% > {MaxPercent}%)",
                                item.ItemAttributes.AlcoholByVolume, alcoholRule.MaxAlcoholPercentage);

                            return new AIClassificationResult
                            {
                                IsEligible = false,
                                Confidence = 1.0,
                                Reason = $"Alcohol percentage ({item.ItemAttributes.AlcoholByVolume}%) exceeds limit ({alcoholRule.MaxAlcoholPercentage}%)",
                                DetectedCategory = "alcohol_limit_exceeded",
                                ModelVersion = aiResult.ModelVersion
                            };
                        }
                    }
                }

                return aiResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying business rules to AI result");
                return new AIClassificationResult
                {
                    IsEligible = false,
                    Confidence = 0.0,
                    Reason = $"Business rules validation error: {ex.Message}",
                    DetectedCategory = "error"
                };
            }
        }

        // Store AI results in catalog for learning
        private async Task StoreAIResultInCatalog(string sku, AIClassificationResult aiResult, BasketItem item)
        {
            try
            {
                var catalogItem = await _catalogService.GetItemBySkuAsync(sku);

                if (catalogItem == null)
                {
                    catalogItem = new CatalogItem
                    {
                        Sku = sku,
                        Name = item.ItemData.ItemName,
                        Description = item.ItemData.ItemDescription ?? "",
                        OriginalCategory = item.CategoryData.PrimaryCategory,
                        NormalizedCategory = aiResult.DetectedCategory,
                        Price = (double)item.PricingData.UnitPriceAmount / 100,
                        ContainsAlcohol = item.ItemAttributes.ContainsAlcohol,
                        CreatedAt = DateTime.UtcNow
                    };
                }

                catalogItem.AIClassification = new AIClassificationData
                {
                    IsEligible = aiResult.IsEligible,
                    Confidence = aiResult.Confidence,
                    Reason = aiResult.Reason,
                    Source = "vertex_ai",
                    ModelVersion = aiResult.ModelVersion,
                    ClassifiedAt = DateTime.UtcNow,
                    Metadata = aiResult.Metadata
                };

                catalogItem.UpdatedAt = DateTime.UtcNow;

                await _catalogService.SaveOrUpdateCatalogItemAsync(catalogItem);

                _logger.LogInformation("Stored AI result: {Sku} → Eligible: {IsEligible} (Confidence: {Confidence})",
                    sku, aiResult.IsEligible, aiResult.Confidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store AI result for: {Sku}", sku);
            }
        }

        private void AddProcessingMetadata(CategorizedItem item, string source, DateTime startTime, double confidence)
        {
            var processingTime = DateTime.UtcNow - startTime;

            item.EligibilityReason += $" [Source: {source}, Confidence: {confidence:F2}, Time: {processingTime.TotalMilliseconds:F0}ms]";
        }

        private string DetermineCategory(AIClassificationResult aiResult, string originalCategory)
        {
            if (!aiResult.IsEligible)
            {
                if (aiResult.Reason.ToLowerInvariant().Contains("alcohol"))
                    return "alcoholic";
                if (aiResult.Reason.ToLowerInvariant().Contains("non-food") ||
                    aiResult.Reason.ToLowerInvariant().Contains("cosmetic"))
                    return "non_food";
                return "ineligible";
            }

            if (aiResult.Reason.ToLowerInvariant().Contains("meal") ||
                aiResult.Reason.ToLowerInvariant().Contains("food"))
                return "prepared_meal";

            return originalCategory;
        }
    }
}