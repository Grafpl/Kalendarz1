using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileAPI.DTOs;
using MobileAPI.Services;

namespace MobileAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(DashboardService dashboardService, ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    /// <summary>
    /// Gets dashboard summary for a single day (defaults to today).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? date = null)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        try
        {
            var dashboard = await _dashboardService.GetDashboardAsync(handlowiec, date);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard for handlowiec '{Handlowiec}'.", handlowiec);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania podsumowania." });
        }
    }

    /// <summary>
    /// Gets dashboard summaries for a date range (daily breakdown).
    /// </summary>
    [HttpGet("range")]
    [ProducesResponseType(typeof(List<DashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDashboardRange(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo)
    {
        var handlowiec = GetHandlowiec();
        if (string.IsNullOrEmpty(handlowiec))
            return Forbid();

        if (dateFrom > dateTo)
            return BadRequest(new { message = "Data początkowa nie może być późniejsza niż data końcowa." });

        if ((dateTo - dateFrom).TotalDays > 90)
            return BadRequest(new { message = "Zakres dat nie może przekraczać 90 dni." });

        try
        {
            var dashboards = await _dashboardService.GetDashboardRangeAsync(handlowiec, dateFrom, dateTo);
            return Ok(dashboards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error getting dashboard range for handlowiec '{Handlowiec}'.", handlowiec);
            return StatusCode(500, new { message = "Wystąpił błąd podczas pobierania podsumowania." });
        }
    }

    private string GetHandlowiec()
    {
        return User.FindFirst("handlowiec")?.Value ?? string.Empty;
    }
}
