using Basket.Filter.Models.Rules;
using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Basket.Filter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MerchantController : ControllerBase
    {
        private readonly IMerchantOnboardingService _onboardingService;
        private readonly IBusinessRulesEngine _businessRulesEngine;

        public MerchantController(
            IMerchantOnboardingService onboardingService,
            IBusinessRulesEngine businessRulesEngine)
        {
            _onboardingService = onboardingService;
            _businessRulesEngine = businessRulesEngine;
        }

        [HttpPost("onboard")]
        public async Task<ActionResult<object>> OnboardMerchant([FromBody] MerchantOnboardingRequest request)
        {
            try
            {
                await _onboardingService.OnboardMerchantAsync(request);
                return Ok(new
                {
                    message = "Merchant onboarded successfully",
                    merchantId = request.MerchantId,
                    status = "success"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = "Merchant onboarding failed",
                    error = ex.Message,
                    status = "failed"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Merchant onboarding failed due to server error",
                    error = ex.Message,
                    status = "error"
                });
            }
        }

        [HttpGet("template/{merchantType}/{countryCode}")]
        public async Task<ActionResult<MerchantEligibilityRules>> GetMerchantTemplate(string merchantType, string countryCode)
        {
            try
            {
                var template = await _businessRulesEngine.GenerateTemplateAsync(merchantType, countryCode);
                return Ok(template);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("business-rules")]
        public async Task<ActionResult<BusinessRulesConfig>> GetBusinessRules()
        {
            try
            {
                var rules = await _businessRulesEngine.GetBusinessRulesAsync();
                return Ok(rules);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
