using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileAPI.DTOs;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(OrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Gets orders for the logged-in handlowiec, with optional date and status filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? status = null)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        try
        {
            var orders = await _orderService.GetOrdersAsync(handlowiec, dateFrom, dateTo, status);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders for handlowiec '{Handlowiec}'.", handlowiec);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania zamówień." });
        }
    }

    /// <summary>
    /// Gets a single order by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(int id)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        try
        {
            var order = await _orderService.GetOrderByIdAsync(id, handlowiec);

            if (order == null)
                return NotFound(new { message = $"Zamówienie o ID {id} nie zostało znalezione." });

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId}.", id);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania zamówienia." });
        }
    }

    /// <summary>
    /// Gets items for a specific order.
    /// </summary>
    [HttpGet("{id:int}/items")]
    [ProducesResponseType(typeof(List<OrderItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrderItems(int id)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        try
        {
            var items = await _orderService.GetOrderItemsAsync(id, handlowiec);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting items for order {OrderId}.", id);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania pozycji zamówienia." });
        }
    }

    /// <summary>
    /// Creates a new order.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] OrderCreateDto dto)
    {
        var handlowiec = GetHandlowiec();
        var login = GetLogin();

        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        // Ensure the order is created for the logged-in handlowiec
        dto.Handlowiec = handlowiec;

        if (dto.KlientId <= 0)
            return BadRequest(new { message = "KlientId jest wymagany." });

        if (string.IsNullOrWhiteSpace(dto.Odbiorca))
            return BadRequest(new { message = "Odbiorca jest wymagany." });

        if (dto.Pozycje == null || dto.Pozycje.Count == 0)
            return BadRequest(new { message = "Zamówienie musi zawierać co najmniej jedną pozycję." });

        try
        {
            var order = await _orderService.CreateOrderAsync(dto, login);

            if (order == null)
                return StatusCode(500, new { message = "Nie udało się utworzyć zamówienia." });

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for handlowiec '{Handlowiec}'.", handlowiec);
            return StatusCode(500, new { message = "Wystąpił błąd podczas tworzenia zamówienia." });
        }
    }

    /// <summary>
    /// Updates an existing order (only if status is 'Nowe').
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrder(int id, [FromBody] OrderCreateDto dto)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        dto.Handlowiec = handlowiec;

        if (dto.KlientId <= 0)
            return BadRequest(new { message = "KlientId jest wymagany." });

        if (dto.Pozycje == null || dto.Pozycje.Count == 0)
            return BadRequest(new { message = "Zamówienie musi zawierać co najmniej jedną pozycję." });

        try
        {
            var updated = await _orderService.UpdateOrderAsync(id, dto, handlowiec);

            if (!updated)
                return NotFound(new
                {
                    message = $"Zamówienie {id} nie istnieje, nie należy do Ciebie lub ma status inny niż 'Nowe'."
                });

            var order = await _orderService.GetOrderByIdAsync(id, handlowiec);
            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId}.", id);
            return StatusCode(500, new { message = "Wystąpił błąd podczas aktualizacji zamówienia." });
        }
    }

    /// <summary>
    /// Cancels an order (sets status to 'Anulowane'). Only works for orders with status 'Nowe'.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        try
        {
            var cancelled = await _orderService.CancelOrderAsync(id, handlowiec);

            if (!cancelled)
                return NotFound(new
                {
                    message = $"Zamówienie {id} nie istnieje, nie należy do Ciebie lub ma status inny niż 'Nowe'."
                });

            return Ok(new { message = $"Zamówienie {id} zostało anulowane." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}.", id);
            return StatusCode(500, new { message = "Wystąpił błąd podczas anulowania zamówienia." });
        }
    }

    // ---------- Helpers ----------

    private string GetHandlowiec()
    {
        return User.FindFirst("handlowiec")?.Value ?? string.Empty;
    }

    private string GetLogin()
    {
        return User.Identity?.Name ?? string.Empty;
    }
}
