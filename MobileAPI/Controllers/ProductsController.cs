using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileAPI.DTOs;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active poultry products (Kurczak A + Kurczak B).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts([FromQuery] string? search = null)
    {
        try
        {
            List<ProductDto> products;

            if (!string.IsNullOrWhiteSpace(search))
            {
                products = await _productService.SearchProductsAsync(search);
            }
            else
            {
                products = await _productService.GetProductsAsync();
            }

            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products.");
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania produktów." });
        }
    }

    /// <summary>
    /// Gets a single product by its code.
    /// </summary>
    [HttpGet("{kod}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(string kod)
    {
        try
        {
            var product = await _productService.GetProductByCodeAsync(kod);

            if (product == null)
                return NotFound(new { message = $"Produkt o kodzie '{kod}' nie został znaleziony." });

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product '{Kod}'.", kod);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania produktu." });
        }
    }
}
