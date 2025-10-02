using Basket.Filter.Models;
using Basket.Filter.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Basket.Filter.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CatalogController : ControllerBase
    {
        private readonly ICatalogService _catalogService;
        private readonly ILogger<CatalogController> _logger;

        public CatalogController(ICatalogService catalogService, ILogger<CatalogController> logger)
        {
            _catalogService = catalogService;
            _logger = logger;
        }

        // Upload catalog items from JSON file
        [HttpPost("upload")]
        public async Task<ActionResult<CatalogUploadResponse>> UploadCatalog(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded" });

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only JSON files are supported" });

            if (file.Length > 10 * 1024 * 1024) // 10MB limit
                return BadRequest(new { error = "File size must be less than 10MB" });

            try
            {
                var result = await _catalogService.UploadCatalogJsonAsync(file);

                if (result.SuccessfulItems == 0)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading catalog");
                return StatusCode(500, new { error = "Internal server error during upload" });
            }
        }

        //Get catalog item by SKU
        [HttpGet("item/{sku}")]
        public async Task<ActionResult<CatalogItem>> GetItemBySku(string sku)
        {
            if (string.IsNullOrEmpty(sku))
                return BadRequest(new { error = "SKU is required" });

            try
            {
                var item = await _catalogService.GetItemBySkuAsync(sku);

                if (item == null)
                    return NotFound(new { error = $"Item with SKU '{sku}' not found" });

                return Ok(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item by SKU: {Sku}", sku);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Delete entire catalog (for testing purposes)
        [HttpDelete]
        public async Task<ActionResult> DeleteCatalog()
        {
            try
            {
                var success = await _catalogService.DeleteCatalogAsync();

                if (success)
                    return Ok(new { message = "Catalog deleted successfully" });
                else
                    return StatusCode(500, new { error = "Failed to delete catalog" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting catalog");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

    }
}
