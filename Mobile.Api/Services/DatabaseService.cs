using Microsoft.Data.SqlClient;
using KalendarzMobile.Api.Models;

namespace KalendarzMobile.Api.Services;

public interface IDatabaseService
{
    Task<List<Zamowienie>> GetZamowieniaAsync(ZamowieniaFilter filter);
    Task<Zamowienie?> GetZamowienieAsync(int id);
    Task<List<Kontrahent>> GetKontrahenciAsync(string? szukaj = null);
    Task<DzienneStatystyki> GetStatystykiDniaAsync(DateTime data);
}

public class DatabaseService : IDatabaseService
{
    private readonly string _connLibra;
    private readonly string _connHandel;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _logger = logger;

        // Połączenia do baz danych - w produkcji użyć appsettings.json
        _connLibra = configuration.GetConnectionString("LibraNet")
            ?? "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        _connHandel = configuration.GetConnectionString("Handel")
            ?? "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
    }

    public async Task<List<Zamowienie>> GetZamowieniaAsync(ZamowieniaFilter filter)
    {
        var zamowienia = new List<Zamowienie>();

        try
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            var sql = @"
                SELECT TOP (@Limit)
                    z.Id,
                    z.DataZamowienia,
                    z.DataPrzyjazdu,
                    z.DataProdukcji,
                    z.KlientId,
                    z.Uwagi,
                    z.IdUser,
                    z.DataUtworzenia,
                    z.LiczbaPojemnikow,
                    z.LiczbaPalet,
                    z.TrybE2,
                    z.TransportStatus,
                    z.Waluta,
                    z.Status,
                    z.ProcentRealizacji,
                    z.CzyCzesciowoZrealizowane
                FROM [dbo].[ZamowieniaMieso] z
                WHERE 1=1";

            if (filter.DataOd.HasValue)
                sql += " AND z.DataPrzyjazdu >= @DataOd";
            if (filter.DataDo.HasValue)
                sql += " AND z.DataPrzyjazdu <= @DataDo";
            if (filter.KlientId.HasValue)
                sql += " AND z.KlientId = @KlientId";
            if (!string.IsNullOrEmpty(filter.Status) && filter.Status != "Wszystkie")
                sql += " AND z.Status = @Status";

            sql += " ORDER BY z.DataPrzyjazdu DESC OFFSET @Offset ROWS";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Limit", filter.Limit);
            cmd.Parameters.AddWithValue("@Offset", filter.Offset);

            if (filter.DataOd.HasValue)
                cmd.Parameters.AddWithValue("@DataOd", filter.DataOd.Value);
            if (filter.DataDo.HasValue)
                cmd.Parameters.AddWithValue("@DataDo", filter.DataDo.Value);
            if (filter.KlientId.HasValue)
                cmd.Parameters.AddWithValue("@KlientId", filter.KlientId.Value);
            if (!string.IsNullOrEmpty(filter.Status) && filter.Status != "Wszystkie")
                cmd.Parameters.AddWithValue("@Status", filter.Status);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var zamowienie = MapZamowienie(reader);
                zamowienia.Add(zamowienie);
            }

            // Pobierz nazwy klientów z bazy Handel
            await LoadKlientInfo(zamowienia);

            // Pobierz towary dla każdego zamówienia
            foreach (var z in zamowienia)
            {
                z.Towary = await GetTowaryZamowieniaAsync(z.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania zamówień");
        }

        return zamowienia;
    }

    public async Task<Zamowienie?> GetZamowienieAsync(int id)
    {
        try
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            var sql = @"
                SELECT
                    z.Id,
                    z.DataZamowienia,
                    z.DataPrzyjazdu,
                    z.DataProdukcji,
                    z.KlientId,
                    z.Uwagi,
                    z.IdUser,
                    z.DataUtworzenia,
                    z.LiczbaPojemnikow,
                    z.LiczbaPalet,
                    z.TrybE2,
                    z.TransportStatus,
                    z.Waluta,
                    z.Status,
                    z.ProcentRealizacji,
                    z.CzyCzesciowoZrealizowane
                FROM [dbo].[ZamowieniaMieso] z
                WHERE z.Id = @Id";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Id", id);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var zamowienie = MapZamowienie(reader);
                await LoadKlientInfo(new List<Zamowienie> { zamowienie });
                zamowienie.Towary = await GetTowaryZamowieniaAsync(id);
                return zamowienie;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania zamówienia {Id}", id);
        }

        return null;
    }

    private async Task<List<ZamowienieTowa>> GetTowaryZamowieniaAsync(int zamowienieId)
    {
        var towary = new List<ZamowienieTowa>();

        try
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            var sql = @"
                SELECT
                    t.Id,
                    t.ZamowienieId,
                    t.KodTowaru,
                    t.Ilosc,
                    t.Cena,
                    t.Pojemniki,
                    t.Palety,
                    t.E2,
                    t.Folia,
                    t.Hallal,
                    t.IloscZrealizowana,
                    t.PowodBraku
                FROM [dbo].[ZamowieniaMiesoTowar] t
                WHERE t.ZamowienieId = @ZamowienieId";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@ZamowienieId", zamowienieId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                towary.Add(new ZamowienieTowa
                {
                    Id = reader.GetInt32(0),
                    ZamowienieId = reader.GetInt32(1),
                    KodTowaru = reader.GetInt32(2),
                    Ilosc = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                    Cena = ParseCena(reader.IsDBNull(4) ? "0" : reader.GetString(4)),
                    Pojemniki = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    Palety = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    E2 = !reader.IsDBNull(7) && reader.GetBoolean(7),
                    Folia = !reader.IsDBNull(8) && reader.GetBoolean(8),
                    Hallal = !reader.IsDBNull(9) && reader.GetBoolean(9),
                    IloscZrealizowana = reader.IsDBNull(10) ? 0 : reader.GetDecimal(10),
                    PowodBraku = reader.IsDBNull(11) ? null : reader.GetString(11)
                });
            }

            // Pobierz nazwy towarów z bazy Handel
            await LoadTowarNames(towary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania towarów zamówienia {Id}", zamowienieId);
        }

        return towary;
    }

    private async Task LoadKlientInfo(List<Zamowienie> zamowienia)
    {
        if (!zamowienia.Any()) return;

        try
        {
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            var klientIds = string.Join(",", zamowienia.Select(z => z.KlientId).Distinct());
            var sql = $@"
                SELECT Id, Shortcut, City, PostalCode
                FROM [dbo].[Contractors]
                WHERE Id IN ({klientIds})";

            await using var cmd = new SqlCommand(sql, cn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var klientDict = new Dictionary<int, (string Nazwa, string? Miasto)>();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var miasto = reader.IsDBNull(2) ? null : reader.GetString(2);
                klientDict[id] = (nazwa, miasto);
            }

            foreach (var z in zamowienia)
            {
                if (klientDict.TryGetValue(z.KlientId, out var info))
                {
                    z.KlientNazwa = info.Nazwa;
                    z.KlientMiasto = info.Miasto;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania informacji o klientach");
        }
    }

    private async Task LoadTowarNames(List<ZamowienieTowa> towary)
    {
        if (!towary.Any()) return;

        try
        {
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            var towarIds = string.Join(",", towary.Select(t => t.KodTowaru).Distinct());
            var sql = $@"
                SELECT Id, Name
                FROM [dbo].[Products]
                WHERE Id IN ({towarIds})";

            await using var cmd = new SqlCommand(sql, cn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var towarDict = new Dictionary<int, string>();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                var nazwa = reader.IsDBNull(1) ? $"Towar #{id}" : reader.GetString(1);
                towarDict[id] = nazwa;
            }

            foreach (var t in towary)
            {
                if (towarDict.TryGetValue(t.KodTowaru, out var nazwa))
                {
                    t.NazwaTowaru = nazwa;
                }
                else
                {
                    t.NazwaTowaru = $"Towar #{t.KodTowaru}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania nazw towarów");
        }
    }

    public async Task<List<Kontrahent>> GetKontrahenciAsync(string? szukaj = null)
    {
        var kontrahenci = new List<Kontrahent>();

        try
        {
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            var sql = @"
                SELECT TOP 100
                    Id, Shortcut, Name, City, PostalCode, Phone, Email, NIP
                FROM [dbo].[Contractors]
                WHERE 1=1";

            if (!string.IsNullOrEmpty(szukaj))
            {
                sql += " AND (Shortcut LIKE @Szukaj OR Name LIKE @Szukaj OR City LIKE @Szukaj)";
            }

            sql += " ORDER BY Shortcut";

            await using var cmd = new SqlCommand(sql, cn);
            if (!string.IsNullOrEmpty(szukaj))
            {
                cmd.Parameters.AddWithValue("@Szukaj", $"%{szukaj}%");
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                kontrahenci.Add(new Kontrahent
                {
                    Id = reader.GetInt32(0),
                    Shortcut = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Miasto = reader.IsDBNull(3) ? null : reader.GetString(3),
                    KodPocztowy = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Telefon = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Email = reader.IsDBNull(6) ? null : reader.GetString(6),
                    NIP = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania kontrahentów");
        }

        return kontrahenci;
    }

    public async Task<DzienneStatystyki> GetStatystykiDniaAsync(DateTime data)
    {
        var stats = new DzienneStatystyki { Data = data };

        try
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            var sql = @"
                SELECT
                    COUNT(*) as LiczbaZamowien,
                    COUNT(DISTINCT KlientId) as LiczbaKlientow,
                    ISNULL(SUM(LiczbaPojemnikow), 0) as SumaPojemnikow,
                    ISNULL(SUM(LiczbaPalet), 0) as SumaPalet,
                    SUM(CASE WHEN Status = 'Oczekuje' THEN 1 ELSE 0 END) as Oczekujace,
                    SUM(CASE WHEN Status = 'Zrealizowane' THEN 1 ELSE 0 END) as Zrealizowane
                FROM [dbo].[ZamowieniaMieso]
                WHERE CAST(DataPrzyjazdu AS DATE) = @Data";

            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@Data", data.Date);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                stats.LiczbaZamowien = reader.GetInt32(0);
                stats.LiczbaKlientow = reader.GetInt32(1);
                stats.SumaPojemnikow = reader.GetInt32(2);
                stats.SumaPalet = reader.GetDecimal(3);
                stats.ZamowieniaOczekujace = reader.GetInt32(4);
                stats.ZamowieniaZrealizowane = reader.GetInt32(5);
            }

            // Oblicz sumę kg z towarów
            var sqlKg = @"
                SELECT ISNULL(SUM(t.Ilosc), 0)
                FROM [dbo].[ZamowieniaMiesoTowar] t
                INNER JOIN [dbo].[ZamowieniaMieso] z ON t.ZamowienieId = z.Id
                WHERE CAST(z.DataPrzyjazdu AS DATE) = @Data";

            await using var cmdKg = new SqlCommand(sqlKg, cn);
            cmdKg.Parameters.AddWithValue("@Data", data.Date);
            var result = await cmdKg.ExecuteScalarAsync();
            stats.SumaKg = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania statystyk dnia {Data}", data);
        }

        return stats;
    }

    private Zamowienie MapZamowienie(SqlDataReader reader)
    {
        return new Zamowienie
        {
            Id = reader.GetInt32(0),
            DataZamowienia = reader.GetDateTime(1),
            DataPrzyjazdu = reader.GetDateTime(2),
            DataProdukcji = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            KlientId = reader.GetInt32(4),
            Uwagi = reader.IsDBNull(5) ? null : reader.GetString(5),
            IdUser = reader.IsDBNull(6) ? null : reader.GetString(6),
            DataUtworzenia = reader.IsDBNull(7) ? DateTime.Now : reader.GetDateTime(7),
            LiczbaPojemnikow = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
            LiczbaPalet = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
            TrybE2 = !reader.IsDBNull(10) && reader.GetBoolean(10),
            TransportStatus = reader.IsDBNull(11) ? null : reader.GetString(11),
            Waluta = reader.IsDBNull(12) ? "PLN" : reader.GetString(12),
            Status = reader.IsDBNull(13) ? "Nowe" : reader.GetString(13),
            ProcentRealizacji = reader.IsDBNull(14) ? 0 : reader.GetDecimal(14),
            CzyCzesciowoZrealizowane = !reader.IsDBNull(15) && reader.GetBoolean(15)
        };
    }

    private decimal ParseCena(string cenaStr)
    {
        if (string.IsNullOrEmpty(cenaStr)) return 0;
        cenaStr = cenaStr.Replace(",", ".").Trim();
        return decimal.TryParse(cenaStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
    }
}
