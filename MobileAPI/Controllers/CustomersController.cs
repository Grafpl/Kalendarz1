using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileAPI.DTOs;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(CustomerService customerService, ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active customers for the logged-in handlowiec.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomers([FromQuery] string? search = null)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        try
        {
            List<CustomerDto> customers;

            if (!string.IsNullOrWhiteSpace(search))
            {
                customers = await _customerService.SearchCustomersAsync(handlowiec, search);
            }
            else
            {
                customers = await _customerService.GetCustomersByHandlowiecAsync(handlowiec);
            }

            return Ok(customers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customers for handlowiec '{Handlowiec}'.", handlowiec);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania klientów." });
        }
    }

    /// <summary>
    /// Gets a single customer by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomer(int id)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        try
        {
            var customer = await _customerService.GetCustomerByIdAsync(id, handlowiec);

            if (customer == null)
                return NotFound(new { message = $"Klient o ID {id} nie został znaleziony." });

            return Ok(customer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer {CustomerId}.", id);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania klienta." });
        }
    }

    private string GetHandlowiec()
    {
        return User.FindFirst("handlowiec")?.Value ?? string.Empty;
    }
}
