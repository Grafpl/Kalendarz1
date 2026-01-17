using Microsoft.AspNetCore.Mvc;
using KalendarzMobile.Api.Models;
using KalendarzMobile.Api.Services;

namespace KalendarzMobile.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZamowieniaController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly ILogger<ZamowieniaController> _logger;

    public ZamowieniaController(IDatabaseService db, ILogger<ZamowieniaController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Pobiera listę zamówień z filtrowaniem
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ZamowieniaResponse>> GetZamowienia(
        [FromQuery] DateTime? dataOd,
        [FromQuery] DateTime? dataDo,
        [FromQuery] int? klientId,
        [FromQuery] string? status,
        [FromQuery] string? handlowiec,
        [FromQuery] string? szukaj,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var filter = new ZamowieniaFilter
        {
            DataOd = dataOd,
            DataDo = dataDo,
            KlientId = klientId,
            Status = status,
            Handlowiec = handlowiec,
            Szukaj = szukaj,
            Limit = Math.Min(limit, 100), // Max 100
            Offset = offset
        };

        var zamowienia = await _db.GetZamowieniaAsync(filter);

        return Ok(new ZamowieniaResponse
        {
            Zamowienia = zamowienia,
            Total = zamowienia.Count,
            HasMore = zamowienia.Count == filter.Limit
        });
    }

    /// <summary>
    /// Pobiera szczegóły zamówienia
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Zamowienie>> GetZamowienie(int id)
    {
        var zamowienie = await _db.GetZamowienieAsync(id);

        if (zamowienie == null)
        {
            return NotFound(new { message = $"Zamówienie o id {id} nie zostało znalezione" });
        }

        return Ok(zamowienie);
    }
}

[ApiController]
[Route("api/[controller]")]
public class KontrahenciController : ControllerBase
{
    private readonly IDatabaseService _db;

    public KontrahenciController(IDatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Pobiera listę kontrahentów
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Kontrahent>>> GetKontrahenci([FromQuery] string? szukaj)
    {
        var kontrahenci = await _db.GetKontrahenciAsync(szukaj);
        return Ok(kontrahenci);
    }
}

[ApiController]
[Route("api/[controller]")]
public class StatystykiController : ControllerBase
{
    private readonly IDatabaseService _db;

    public StatystykiController(IDatabaseService db)
    {
        _db = db;
    }

    /// <summary>
    /// Pobiera statystyki dnia
    /// </summary>
    [HttpGet("{data}")]
    public async Task<ActionResult<DzienneStatystyki>> GetStatystykiDnia(DateTime data)
    {
        var stats = await _db.GetStatystykiDniaAsync(data);
        return Ok(stats);
    }
}
