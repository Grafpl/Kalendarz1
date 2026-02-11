using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.DyrektorDashboard.Models;

namespace Kalendarz1.DyrektorDashboard.Services
{
    /// <summary>
    /// Serwis agregujący dane ze wszystkich działów zakładu dla Panelu Dyrektora.
    /// Łączy się z 3 bazami danych: LibraNet, Handel, TransportPL.
    /// </summary>
    public class DyrektorDashboardService : IDisposable
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly string _connTransport;

        private SqlConnection _poolLibra;
        private SqlConnection _poolHandel;
        private SqlConnection _poolTransport;

        private readonly SemaphoreSlim _lockLibra = new(1, 1);
        private readonly SemaphoreSlim _lockHandel = new(1, 1);
        private readonly SemaphoreSlim _lockTransport = new(1, 1);

        private bool _disposed;
        private const int CMD_TIMEOUT = 60;

        // Stałe magazynowe (z Mroznia.cs)
        private const int MAGAZYN_MROZNIA = 65552;
        private const string DATA_STARTOWA = "2020-01-07";
        private const string KATALOG_SWIEZY = "67095";
        private const string KATALOG_MROZONY = "67153";

        private static readonly string[] _nazwyDni = { "Nd", "Pn", "Wt", "Śr", "Cz", "Pt", "Sb" };

        public DyrektorDashboardService(string connLibra, string connHandel, string connTransport)
        {
            _connLibra = connLibra;
            _connHandel = connHandel;
            _connTransport = connTransport;
        }

        #region Connection Pooling

        private async Task<SqlConnection> GetLibraAsync(CancellationToken ct = default)
        {
            await _lockLibra.WaitAsync(ct);
            try
            {
                if (_poolLibra == null || _poolLibra.State != ConnectionState.Open)
                {
                    _poolLibra?.Dispose();
                    _poolLibra = new SqlConnection(_connLibra);
                    await _poolLibra.OpenAsync(ct);
                }
                return _poolLibra;
            }
            finally { _lockLibra.Release(); }
        }

        private async Task<SqlConnection> GetHandelAsync(CancellationToken ct = default)
        {
            await _lockHandel.WaitAsync(ct);
            try
            {
                if (_poolHandel == null || _poolHandel.State != ConnectionState.Open)
                {
                    _poolHandel?.Dispose();
                    _poolHandel = new SqlConnection(_connHandel);
                    await _poolHandel.OpenAsync(ct);
                }
                return _poolHandel;
            }
            finally { _lockHandel.Release(); }
        }

        private async Task<SqlConnection> GetTransportAsync(CancellationToken ct = default)
        {
            await _lockTransport.WaitAsync(ct);
            try
            {
                if (_poolTransport == null || _poolTransport.State != ConnectionState.Open)
                {
                    _poolTransport?.Dispose();
                    _poolTransport = new SqlConnection(_connTransport);
                    await _poolTransport.OpenAsync(ct);
                }
                return _poolTransport;
            }
            finally { _lockTransport.Release(); }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════
        // KPI KARTY - szybkie zapytania, ładowane jako pierwsze
        // ════════════════════════════════════════════════════════════════════

        public async Task<KpiKartyDyrektora> GetKpiKartyAsync(CancellationToken ct = default)
        {
            var kpi = new KpiKartyDyrektora();

            // 3 grupy równoległych zapytań (po jednym na bazę)
            var taskLibra = Task.Run(async () =>
            {
                try
                {
                    using var conn = new SqlConnection(_connLibra);
                    await conn.OpenAsync(ct);

                    // Żywiec dzisiaj
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) as Dostawy, ISNULL(SUM(LumQnt),0) as Sztuki,
                               ISNULL(SUM(NettoWeight),0) as WagaKg
                        FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                        WHERE CAST(DataPrzyjecia AS DATE) = CAST(GETDATE() AS DATE)", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ZywiecDzisDostawy = r.GetInt32(0);
                            kpi.ZywiecSztukiDzis = Convert.ToInt32(r["Sztuki"]);
                            kpi.ZywiecDzisKg = Convert.ToDecimal(r["WagaKg"]);
                        }
                    }

                    // Zamówienia dzisiaj
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(DISTINCT z.ID) as Liczba,
                               ISNULL(SUM(z.IloscKg),0) as SumaKg,
                               ISNULL(SUM(z.IloscKg * z.CenaNetto),0) as SumaWartosc
                        FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                        WHERE CAST(z.DataOdbioru AS DATE) = CAST(GETDATE() AS DATE)", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ZamowieniaDzisLiczba = r.GetInt32(0);
                            kpi.ZamowieniaDzisKg = Convert.ToDecimal(r["SumaKg"]);
                            kpi.ZamowieniaDzisWartosc = Convert.ToDecimal(r["SumaWartosc"]);
                        }
                    }

                    // Zamówienia jutro
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(DISTINCT z.ID) as Liczba, ISNULL(SUM(z.IloscKg),0) as SumaKg
                        FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                        WHERE CAST(z.DataOdbioru AS DATE) = CAST(DATEADD(DAY,1,GETDATE()) AS DATE)", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ZamowieniaJutroLiczba = r.GetInt32(0);
                            kpi.ZamowieniaJutroKg = Convert.ToDecimal(r["SumaKg"]);
                        }
                    }

                    // Reklamacje otwarte
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            SUM(CASE WHEN Status = 'Nowa' THEN 1 ELSE 0 END) as Nowe,
                            SUM(CASE WHEN Status IN ('Nowa','W trakcie') THEN 1 ELSE 0 END) as Otwarte,
                            ISNULL(SUM(CASE WHEN Status IN ('Nowa','W trakcie') THEN SumaKg ELSE 0 END),0) as SumaKg
                        FROM [dbo].[Reklamacje] WITH (NOLOCK)", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ReklamacjeNowe = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                            kpi.ReklamacjeOtwarte = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                            kpi.ReklamacjeSumaKg = Convert.ToDecimal(r["SumaKg"]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"KPI LibraNet error: {ex.Message}");
                }
            }, ct);

            var taskHandel = Task.Run(async () =>
            {
                try
                {
                    using var conn = new SqlConnection(_connHandel);
                    await conn.OpenAsync(ct);

                    // Produkcja dzisiaj (sPWU = przyjęcie wewnętrzne uboju, LWP = likwidacja wyrobu)
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            ISNULL(SUM(CASE WHEN MG.seria = 'sPWU' THEN ABS(MZ.iloscwp) ELSE 0 END),0) as UbojKg,
                            ISNULL(SUM(CASE WHEN MG.seria = 'LWP' THEN ABS(MZ.iloscwp) ELSE 0 END),0) as LWPKg
                        FROM [HM].[MG] MG WITH (NOLOCK)
                        JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MG.GIDNumer = MZ.GIDNumer AND MG.GIDTyp = MZ.GIDTyp
                        WHERE CAST(MG.data_ AS DATE) = CAST(GETDATE() AS DATE)
                          AND MG.seria IN ('sPWU','LWP','RWP')", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ProdukcjaDzisKg = Convert.ToDecimal(r["UbojKg"]);
                            kpi.ProdukcjaLWPKg = Convert.ToDecimal(r["LWPKg"]);
                        }
                    }

                    // Magazyn mroźni - stan aktualny
                    using (var cmd = new SqlCommand($@"
                        SELECT
                            ISNULL(SUM(ABS(MZ.iloscwp)),0) as StanKg,
                            ISNULL(SUM(ABS(MZ.wartNetto)),0) as StanWartosc
                        FROM [HM].[MZ] MZ WITH (NOLOCK)
                        WHERE MZ.data_ >= '{DATA_STARTOWA}'
                          AND MZ.data_ <= GETDATE()
                          AND MZ.magazyn = {MAGAZYN_MROZNIA}
                          AND MZ.typ = '0'
                        HAVING ABS(SUM(MZ.iloscwp)) > 0", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.MagazynStanKg = Convert.ToDecimal(r["StanKg"]);
                            kpi.MagazynStanWartosc = Convert.ToDecimal(r["StanWartosc"]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"KPI Handel error: {ex.Message}");
                }
            }, ct);

            var taskTransport = Task.Run(async () =>
            {
                try
                {
                    using var conn = new SqlConnection(_connTransport);
                    await conn.OpenAsync(ct);

                    using var cmd = new SqlCommand(@"
                        SELECT
                            COUNT(*) as KursyDzis,
                            SUM(CASE WHEN Status IN ('Przypisany','Wlasny') THEN 1 ELSE 0 END) as Aktywne,
                            SUM(CASE WHEN Status = 'Zakonczony' THEN 1 ELSE 0 END) as Zakonczone,
                            COUNT(DISTINCT KierowcaID) as Kierowcy
                        FROM [dbo].[Kurs] WITH (NOLOCK)
                        WHERE CAST(DataKursu AS DATE) = CAST(GETDATE() AS DATE)", conn);
                    cmd.CommandTimeout = CMD_TIMEOUT;

                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        kpi.TransportDzisKursy = r.GetInt32(0);
                        kpi.TransportAktywneKursy = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                        kpi.TransportZakonczoneKursy = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                        kpi.TransportKierowcyAktywni = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"KPI Transport error: {ex.Message}");
                }
            }, ct);

            await Task.WhenAll(taskLibra, taskHandel, taskTransport);
            return kpi;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: ŻYWIEC
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneZywiec> GetDaneZywiecAsync(CancellationToken ct = default)
        {
            var dane = new DaneZywiec();
            try
            {
                using var conn = new SqlConnection(_connLibra);
                await conn.OpenAsync(ct);

                // Dzisiaj - szczegóły
                using (var cmd = new SqlCommand(@"
                    SELECT COUNT(*) as Dostawy, ISNULL(SUM(LumQnt),0) as Sztuki,
                           ISNULL(SUM(NettoWeight),0) as WagaKg,
                           ISNULL(SUM((Price + ISNULL(Addition,0)) * NettoWeight * (1 - ISNULL(Loss,0)/100.0)),0) as Wartosc,
                           ISNULL(AVG(Price + ISNULL(Addition,0)),0) as SredniaCena,
                           ISNULL(AVG(ISNULL(Loss,0)),0) as SredniUbytek,
                           ISNULL(SUM(IncDeadConf),0) as Padniete
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE CAST(DataPrzyjecia AS DATE) = CAST(GETDATE() AS DATE)", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.DzisDostawy = r.GetInt32(0);
                        dane.DzisSztuki = Convert.ToInt32(r["Sztuki"]);
                        dane.DzisKg = Convert.ToDecimal(r["WagaKg"]);
                        dane.DzisWartosc = Convert.ToDecimal(r["Wartosc"]);
                        dane.SredniaCenaDzis = Convert.ToDecimal(r["SredniaCena"]);
                        dane.SredniUbytekDzis = Convert.ToDecimal(r["SredniUbytek"]);
                        dane.PadnieteDzis = Convert.ToInt32(r["Padniete"]);
                    }
                }

                // Tydzień
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(NettoWeight),0) as Waga,
                           ISNULL(SUM((Price + ISNULL(Addition,0)) * NettoWeight * (1 - ISNULL(Loss,0)/100.0)),0) as Wartosc
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE DataPrzyjecia >= DATEADD(DAY,-7,GETDATE())", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.TydzienKg = Convert.ToDecimal(r["Waga"]);
                        dane.TydzienWartosc = Convert.ToDecimal(r["Wartosc"]);
                    }
                }

                // Miesiąc
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(NettoWeight),0) as Waga,
                           ISNULL(SUM((Price + ISNULL(Addition,0)) * NettoWeight * (1 - ISNULL(Loss,0)/100.0)),0) as Wartosc
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE DataPrzyjecia >= DATEADD(MONTH,-1,GETDATE())", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.MiesiacKg = Convert.ToDecimal(r["Waga"]);
                        dane.MiesiacWartosc = Convert.ToDecimal(r["Wartosc"]);
                    }
                }

                // Top 5 hodowców (miesiąc)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 c.Name as Nazwa, c.City as Miasto,
                           SUM(fc.NettoWeight) as Waga,
                           SUM((fc.Price + ISNULL(fc.Addition,0)) * fc.NettoWeight) as Wartosc,
                           COUNT(*) as Dostawy
                    FROM [dbo].[FarmerCalc] fc WITH (NOLOCK)
                    JOIN [dbo].[Customer] c ON fc.CustomerGID = c.GID
                    WHERE fc.DataPrzyjecia >= DATEADD(MONTH,-1,GETDATE())
                    GROUP BY c.Name, c.City
                    ORDER BY SUM(fc.NettoWeight) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    int poz = 1;
                    while (await r.ReadAsync(ct))
                    {
                        dane.TopHodowcy.Add(new TopHodowcaItem
                        {
                            Pozycja = poz++,
                            Nazwa = r["Nazwa"]?.ToString(),
                            Miasto = r["Miasto"]?.ToString(),
                            WagaKg = Convert.ToDecimal(r["Waga"]),
                            Wartosc = Convert.ToDecimal(r["Wartosc"]),
                            LiczbaDostaw = r.GetInt32(4)
                        });
                    }
                }

                // Trend 8 tygodni
                using (var cmd = new SqlCommand(@"
                    SELECT DATEPART(WEEK, DataPrzyjecia) as Tydzien,
                           MIN(DataPrzyjecia) as Poczatek,
                           SUM(NettoWeight) as Waga,
                           SUM((Price + ISNULL(Addition,0)) * NettoWeight * (1 - ISNULL(Loss,0)/100.0)) as Wartosc,
                           AVG(Price + ISNULL(Addition,0)) as SredniaCena,
                           COUNT(*) as Dostawy
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE DataPrzyjecia >= DATEADD(WEEK,-8,GETDATE())
                    GROUP BY DATEPART(YEAR, DataPrzyjecia), DATEPART(WEEK, DataPrzyjecia)
                    ORDER BY MIN(DataPrzyjecia)", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.Trend8Tygodni.Add(new TrendTygodniowyItem
                        {
                            NumerTygodnia = r.GetInt32(0),
                            PoczatekTygodnia = r.GetDateTime(1),
                            WagaKg = Convert.ToDecimal(r["Waga"]),
                            Wartosc = Convert.ToDecimal(r["Wartosc"]),
                            SredniaCena = Convert.ToDecimal(r["SredniaCena"]),
                            LiczbaDostaw = r.GetInt32(5)
                        });
                    }
                }

                // Dostawy dzisiejsze (lista)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 20 fc.DataPrzyjecia, c.Name as Hodowca, fc.LumQnt as Sztuki,
                           fc.NettoWeight as Waga, (fc.Price + ISNULL(fc.Addition,0)) as Cena,
                           d.Name as Kierowca
                    FROM [dbo].[FarmerCalc] fc WITH (NOLOCK)
                    JOIN [dbo].[Customer] c ON fc.CustomerGID = c.GID
                    LEFT JOIN [dbo].[Drivers] d ON fc.DriverGID = d.GID
                    WHERE CAST(fc.DataPrzyjecia AS DATE) = CAST(GETDATE() AS DATE)
                    ORDER BY fc.DataPrzyjecia DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.DostawyDzis.Add(new DostawaDzisItem
                        {
                            Godzina = r.GetDateTime(0),
                            Hodowca = r["Hodowca"]?.ToString(),
                            Sztuki = Convert.ToInt32(r["Sztuki"]),
                            WagaKg = Convert.ToDecimal(r["Waga"]),
                            Cena = Convert.ToDecimal(r["Cena"]),
                            Kierowca = r["Kierowca"]?.ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Żywiec error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: ZAMÓWIENIA
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneZamowienia> GetDaneZamowieniaAsync(CancellationToken ct = default)
        {
            var dane = new DaneZamowienia();
            try
            {
                using var conn = new SqlConnection(_connLibra);
                await conn.OpenAsync(ct);

                // Zamówienia dziś
                using (var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT ID) as Liczba, ISNULL(SUM(IloscKg),0) as SumaKg,
                           ISNULL(SUM(IloscKg * CenaNetto),0) as Wartosc
                    FROM [dbo].[ZamowieniaMieso] WITH (NOLOCK)
                    WHERE CAST(DataOdbioru AS DATE) = CAST(GETDATE() AS DATE)", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.LiczbaZamowienDzis = r.GetInt32(0);
                        dane.SumaKgDzis = Convert.ToDecimal(r["SumaKg"]);
                        dane.SumaWartoscDzis = Convert.ToDecimal(r["Wartosc"]);
                    }
                }

                // Zamówienia jutro
                using (var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT ID) as Liczba, ISNULL(SUM(IloscKg),0) as SumaKg,
                           ISNULL(SUM(IloscKg * CenaNetto),0) as Wartosc
                    FROM [dbo].[ZamowieniaMieso] WITH (NOLOCK)
                    WHERE CAST(DataOdbioru AS DATE) = CAST(DATEADD(DAY,1,GETDATE()) AS DATE)", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.LiczbaZamowienJutro = r.GetInt32(0);
                        dane.SumaKgJutro = Convert.ToDecimal(r["SumaKg"]);
                        dane.SumaWartoscJutro = Convert.ToDecimal(r["Wartosc"]);
                    }
                }

                // Trend dzienny (ostatnie 14 dni)
                using (var cmd = new SqlCommand(@"
                    SELECT CAST(DataOdbioru AS DATE) as Dzien, COUNT(DISTINCT ID) as Liczba,
                           ISNULL(SUM(IloscKg),0) as SumaKg,
                           ISNULL(SUM(IloscKg * CenaNetto),0) as Wartosc
                    FROM [dbo].[ZamowieniaMieso] WITH (NOLOCK)
                    WHERE DataOdbioru >= DATEADD(DAY,-14,GETDATE())
                      AND DataOdbioru <= DATEADD(DAY,1,GETDATE())
                    GROUP BY CAST(DataOdbioru AS DATE)
                    ORDER BY CAST(DataOdbioru AS DATE)", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.TrendDzienny.Add(new ZamowienieDzienneItem
                        {
                            Data = r.GetDateTime(0),
                            Liczba = r.GetInt32(1),
                            SumaKg = Convert.ToDecimal(r["SumaKg"]),
                            SumaWartosc = Convert.ToDecimal(r["Wartosc"])
                        });
                    }
                }

                // Top 10 klientów (miesiąc)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 10 z.Kontrahent as Nazwa,
                           SUM(z.IloscKg) as SumaKg,
                           SUM(z.IloscKg * z.CenaNetto) as Wartosc,
                           COUNT(DISTINCT z.ID) as Zamowienia
                    FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                    WHERE z.DataOdbioru >= DATEADD(MONTH,-1,GETDATE())
                    GROUP BY z.Kontrahent
                    ORDER BY SUM(z.IloscKg) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.TopKlienci.Add(new TopKlientItem
                        {
                            Nazwa = r["Nazwa"]?.ToString(),
                            SumaKg = Convert.ToDecimal(r["SumaKg"]),
                            SumaWartosc = Convert.ToDecimal(r["Wartosc"]),
                            LiczbaZamowien = r.GetInt32(3)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Zamówienia error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: PRODUKCJA (sPWU = ubój, LWP = krojenie, RWP = rozchód)
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneProdukcja> GetDaneProdukcjaAsync(CancellationToken ct = default)
        {
            var dane = new DaneProdukcja();
            try
            {
                using var conn = new SqlConnection(_connHandel);
                await conn.OpenAsync(ct);

                // Produkcja dzisiaj wg typu dokumentu
                using (var cmd = new SqlCommand(@"
                    SELECT MG.seria,
                           ISNULL(SUM(ABS(MZ.iloscwp)),0) as SumaKg
                    FROM [HM].[MG] MG WITH (NOLOCK)
                    JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MG.GIDNumer = MZ.GIDNumer AND MG.GIDTyp = MZ.GIDTyp
                    WHERE CAST(MG.data_ AS DATE) = CAST(GETDATE() AS DATE)
                      AND MG.seria IN ('sPWU','LWP','RWP')
                    GROUP BY MG.seria", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        var seria = r.GetString(0).Trim();
                        var kg = Convert.ToDecimal(r["SumaKg"]);
                        switch (seria)
                        {
                            case "sPWU": dane.UbojDzisKg = kg; break;
                            case "LWP": dane.KrojenieDzisKg = kg; break;
                            case "RWP": dane.RWPDzisKg = kg; break;
                        }
                    }
                }

                // Top 15 produktów dzisiaj (LWP - krojenie)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 15 TW.kod, TW.nazwa,
                           ABS(SUM(MZ.iloscwp)) as IloscKg, MG.seria
                    FROM [HM].[MG] MG WITH (NOLOCK)
                    JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MG.GIDNumer = MZ.GIDNumer AND MG.GIDTyp = MZ.GIDTyp
                    JOIN [HM].[TW] TW WITH (NOLOCK) ON MZ.twr = TW.GIDNumer
                    WHERE CAST(MG.data_ AS DATE) = CAST(GETDATE() AS DATE)
                      AND MG.seria IN ('sPWU','LWP')
                    GROUP BY TW.kod, TW.nazwa, MG.seria
                    ORDER BY ABS(SUM(MZ.iloscwp)) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.TopProdukty.Add(new ProduktProdukcjiItem
                        {
                            Kod = r["kod"]?.ToString()?.Trim(),
                            Nazwa = r["nazwa"]?.ToString()?.Trim(),
                            IloscKg = Convert.ToDecimal(r["IloscKg"]),
                            TypDokumentu = r["seria"]?.ToString()?.Trim()
                        });
                    }
                }

                // Trend produkcji (ostatnie 7 dni roboczych)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 7 CAST(MG.data_ AS DATE) as Dzien,
                           ISNULL(SUM(CASE WHEN MG.seria = 'sPWU' THEN ABS(MZ.iloscwp) ELSE 0 END),0) as UbojKg,
                           ISNULL(SUM(CASE WHEN MG.seria = 'LWP' THEN ABS(MZ.iloscwp) ELSE 0 END),0) as LWPKg,
                           ISNULL(SUM(CASE WHEN MG.seria = 'RWP' THEN ABS(MZ.iloscwp) ELSE 0 END),0) as RWPKg
                    FROM [HM].[MG] MG WITH (NOLOCK)
                    JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MG.GIDNumer = MZ.GIDNumer AND MG.GIDTyp = MZ.GIDTyp
                    WHERE MG.data_ >= DATEADD(DAY,-14,GETDATE())
                      AND MG.seria IN ('sPWU','LWP','RWP')
                    GROUP BY CAST(MG.data_ AS DATE)
                    HAVING SUM(ABS(MZ.iloscwp)) > 0
                    ORDER BY CAST(MG.data_ AS DATE) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        var dt = r.GetDateTime(0);
                        dane.TrendTygodniowy.Add(new ProdukcjaDziennaItem
                        {
                            Data = dt,
                            DzienNazwa = _nazwyDni[(int)dt.DayOfWeek],
                            UbojKg = Convert.ToDecimal(r["UbojKg"]),
                            KrojenieKg = Convert.ToDecimal(r["LWPKg"]),
                            LWPKg = Convert.ToDecimal(r["LWPKg"])
                        });
                    }
                }
                dane.TrendTygodniowy.Reverse();

                // Wydajność krojenia: LWP / sPWU * 100
                if (dane.UbojDzisKg > 0)
                    dane.WydajnoscKrojeniaProcent = dane.KrojenieDzisKg / dane.UbojDzisKg * 100;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Produkcja error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: MAGAZYN MROŹNI
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneMagazyn> GetDaneMagazynAsync(CancellationToken ct = default)
        {
            var dane = new DaneMagazyn();
            try
            {
                using var conn = new SqlConnection(_connHandel);
                await conn.OpenAsync(ct);

                // Top produkty w mroźni
                using var cmd = new SqlCommand($@"
                    SELECT TW.kod, TW.nazwa,
                           ABS(SUM(MZ.iloscwp)) as IloscKg,
                           ABS(SUM(MZ.wartNetto)) as WartoscZl,
                           CASE WHEN TW.katalog = {KATALOG_SWIEZY} THEN 'Świeży'
                                WHEN TW.katalog = {KATALOG_MROZONY} THEN 'Mrożony'
                                ELSE 'Inny' END as Katalog
                    FROM [HM].[MZ] MZ WITH (NOLOCK)
                    JOIN [HM].[TW] TW WITH (NOLOCK) ON MZ.twr = TW.GIDNumer
                    WHERE MZ.data_ >= '{DATA_STARTOWA}'
                      AND MZ.data_ <= GETDATE()
                      AND MZ.magazyn = {MAGAZYN_MROZNIA}
                      AND MZ.typ = '0'
                    GROUP BY TW.kod, TW.nazwa, TW.katalog
                    HAVING ABS(SUM(MZ.iloscwp)) > 1
                    ORDER BY ABS(SUM(MZ.iloscwp)) DESC", conn);
                cmd.CommandTimeout = CMD_TIMEOUT;

                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var item = new StanProduktItem
                    {
                        Kod = r["kod"]?.ToString()?.Trim(),
                        Nazwa = r["nazwa"]?.ToString()?.Trim(),
                        IloscKg = Convert.ToDecimal(r["IloscKg"]),
                        WartoscZl = Convert.ToDecimal(r["WartoscZl"]),
                        Katalog = r["Katalog"]?.ToString()
                    };
                    dane.TopProdukty.Add(item);

                    dane.StanCaloscKg += item.IloscKg;
                    dane.StanWartoscZl += item.WartoscZl;
                    dane.LiczbaPozycji++;

                    if (item.Katalog == "Świeży") dane.StanSwiezyKg += item.IloscKg;
                    else if (item.Katalog == "Mrożony") dane.StanMrozonyKg += item.IloscKg;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magazyn error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: TRANSPORT
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneTransport> GetDaneTransportAsync(CancellationToken ct = default)
        {
            var dane = new DaneTransport();
            try
            {
                using var conn = new SqlConnection(_connTransport);
                await conn.OpenAsync(ct);

                using var cmd = new SqlCommand(@"
                    SELECT k.KursID, ki.Imie + ' ' + ki.Nazwisko as Kierowca,
                           p.NumerRejestracyjny as Pojazd,
                           k.Status,
                           (SELECT COUNT(*) FROM [dbo].[Ladunek] l WHERE l.KursID = k.KursID) as Ladunki
                    FROM [dbo].[Kurs] k WITH (NOLOCK)
                    LEFT JOIN [dbo].[Kierowca] ki WITH (NOLOCK) ON k.KierowcaID = ki.KierowcaID
                    LEFT JOIN [dbo].[Pojazd] p WITH (NOLOCK) ON k.PojazdID = p.PojazdID
                    WHERE CAST(k.DataKursu AS DATE) = CAST(GETDATE() AS DATE)
                    ORDER BY k.KursID", conn);
                cmd.CommandTimeout = CMD_TIMEOUT;

                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var kurs = new KursItem
                    {
                        KursID = r.GetInt64(0),
                        Kierowca = r["Kierowca"]?.ToString()?.Trim(),
                        Pojazd = r["Pojazd"]?.ToString()?.Trim(),
                        Status = r["Status"]?.ToString()?.Trim(),
                        LiczbaLadunkow = r.GetInt32(4)
                    };
                    dane.Kursy.Add(kurs);

                    switch (kurs.Status)
                    {
                        case "Planowany": case "Oczekuje": dane.KursyPlanowane++; break;
                        case "Przypisany": case "Wlasny": dane.KursyWTrasie++; break;
                        case "Zakonczony": dane.KursyZakonczone++; break;
                    }
                }
                dane.KursyDzis = dane.Kursy.Count;
                dane.KierowcyAktywni = dane.Kursy.Select(k => k.Kierowca).Distinct().Count();
                dane.PojazdyWUzyciu = dane.Kursy.Select(k => k.Pojazd).Where(p => !string.IsNullOrEmpty(p)).Distinct().Count();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Transport error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: REKLAMACJE
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneReklamacje> GetDaneReklamacjeAsync(CancellationToken ct = default)
        {
            var dane = new DaneReklamacje();
            try
            {
                using var conn = new SqlConnection(_connLibra);
                await conn.OpenAsync(ct);

                // Statusy
                using (var cmd = new SqlCommand(@"
                    SELECT Status, COUNT(*) as Liczba, ISNULL(SUM(SumaKg),0) as SumaKg
                    FROM [dbo].[Reklamacje] WITH (NOLOCK)
                    GROUP BY Status", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        var status = r["Status"]?.ToString()?.Trim();
                        var liczba = r.GetInt32(1);
                        var kg = Convert.ToDecimal(r["SumaKg"]);

                        switch (status)
                        {
                            case "Nowa": dane.NoweCount = liczba; dane.SumaKgOtwarte += kg; break;
                            case "W trakcie": dane.WTrakcieCount = liczba; dane.SumaKgOtwarte += kg; break;
                            case "Zaakceptowana": dane.ZaakceptowaneCount = liczba; break;
                            case "Odrzucona": dane.OdrzuconeCount = liczba; break;
                            case "Zamknieta": case "Zamknięta": dane.ZamknieteCount = liczba; break;
                        }
                    }
                }

                // Ostatnie 15 reklamacji
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 15 ID, DataZgloszenia, Kontrahent, Status, Opis,
                           ISNULL(SumaKg,0) as SumaKg
                    FROM [dbo].[Reklamacje] WITH (NOLOCK)
                    ORDER BY DataZgloszenia DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.OstatnieReklamacje.Add(new ReklamacjaItem
                        {
                            Id = r.GetInt32(0),
                            Data = r.IsDBNull(1) ? DateTime.MinValue : r.GetDateTime(1),
                            Kontrahent = r["Kontrahent"]?.ToString(),
                            Status = r["Status"]?.ToString()?.Trim(),
                            Opis = r["Opis"]?.ToString(),
                            IloscKg = Convert.ToDecimal(r["SumaKg"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Reklamacje error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: OPAKOWANIA
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneOpakowania> GetDaneOpakowaniaAsync(CancellationToken ct = default)
        {
            var dane = new DaneOpakowania();
            try
            {
                using var conn = new SqlConnection(_connHandel);
                await conn.OpenAsync(ct);

                using var cmd = new SqlCommand(@"
                    SELECT TW.nazwa as TypOpakowania,
                           ISNULL(SUM(CASE WHEN MZ.iloscwp > 0 THEN MZ.iloscwp ELSE 0 END),0) as Wydane,
                           ISNULL(SUM(CASE WHEN MZ.iloscwp < 0 THEN ABS(MZ.iloscwp) ELSE 0 END),0) as Przyjete,
                           ISNULL(SUM(MZ.iloscwp),0) as Saldo
                    FROM [HM].[MZ] MZ WITH (NOLOCK)
                    JOIN [HM].[TW] TW WITH (NOLOCK) ON MZ.twr = TW.GIDNumer
                    WHERE TW.katalog IN (SELECT GIDNumer FROM [HM].[TW] WHERE nazwa LIKE '%opakow%' OR nazwa LIKE '%E2%' OR nazwa LIKE '%H1%')
                      AND MZ.data_ >= DATEADD(YEAR,-1,GETDATE())
                    GROUP BY TW.nazwa
                    HAVING ABS(SUM(MZ.iloscwp)) > 0
                    ORDER BY ABS(SUM(MZ.iloscwp)) DESC", conn);
                cmd.CommandTimeout = CMD_TIMEOUT;

                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var item = new OpakowanieSaldoItem
                    {
                        TypOpakowania = r["TypOpakowania"]?.ToString()?.Trim(),
                        Wydane = Convert.ToDecimal(r["Wydane"]),
                        Przyjete = Convert.ToDecimal(r["Przyjete"]),
                        Saldo = Convert.ToDecimal(r["Saldo"])
                    };
                    dane.SaldaWgTypu.Add(item);

                    var nazwa = item.TypOpakowania?.ToUpper() ?? "";
                    if (nazwa.Contains("E2")) dane.SaldoE2 += item.Saldo;
                    else if (nazwa.Contains("H1")) dane.SaldoH1 += item.Saldo;
                    else dane.SaldoInne += item.Saldo;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Opakowania error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: PLAN TYGODNIOWY
        // ════════════════════════════════════════════════════════════════════

        public async Task<DanePlanTygodniowy> GetPlanTygodniowyAsync(CancellationToken ct = default)
        {
            var dane = new DanePlanTygodniowy();
            try
            {
                // Oblicz poniedziałek bieżącego tygodnia
                var today = DateTime.Today;
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                var monday = today.AddDays(-diff);

                using var conn = new SqlConnection(_connLibra);
                await conn.OpenAsync(ct);

                // Plan i realizacja per dzień tygodnia
                for (int i = 0; i < 5; i++) // Pon-Pt
                {
                    var dzien = monday.AddDays(i);
                    var planDzien = new PlanDzienItem
                    {
                        Data = dzien,
                        DzienTygodnia = _nazwyDni[(int)dzien.DayOfWeek],
                        CzyDzisiaj = dzien.Date == today
                    };

                    // Realizacja (rzeczywiste dostawy)
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM(NettoWeight),0) as Waga
                        FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                        WHERE CAST(DataPrzyjecia AS DATE) = @dzien", conn))
                    {
                        cmd.Parameters.AddWithValue("@dzien", dzien.Date);
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        var result = await cmd.ExecuteScalarAsync(ct);
                        planDzien.RealizacjaKg = result != null && result != DBNull.Value
                            ? Convert.ToDecimal(result) : 0;
                    }

                    // Plan (planowane dostawy z harmonogramu - DeclI1 = planowana ilość)
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM(ISNULL(DeclI1,0)),0) as Plan
                        FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                        WHERE CAST(PlanDate AS DATE) = @dzien", conn))
                    {
                        cmd.Parameters.AddWithValue("@dzien", dzien.Date);
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        var result = await cmd.ExecuteScalarAsync(ct);
                        planDzien.PlanKg = result != null && result != DBNull.Value
                            ? Convert.ToDecimal(result) : 0;
                    }

                    dane.Dni.Add(planDzien);
                    dane.PlanTygodniaSumaKg += planDzien.PlanKg;
                    dane.RealizacjaTygodniaSumaKg += planDzien.RealizacjaKg;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Plan tygodniowy error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: ALERTY OPERACYJNE
        // ════════════════════════════════════════════════════════════════════

        public async Task<List<AlertItem>> GetAlertyAsync(CancellationToken ct = default)
        {
            var alerty = new List<AlertItem>();
            try
            {
                using var conn = new SqlConnection(_connLibra);
                await conn.OpenAsync(ct);

                // Wysoki ubytek (>5%)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 c.Name as Hodowca, fc.DataPrzyjecia, fc.Loss as Ubytek
                    FROM [dbo].[FarmerCalc] fc WITH (NOLOCK)
                    JOIN [dbo].[Customer] c ON fc.CustomerGID = c.GID
                    WHERE fc.Loss > 5 AND fc.DataPrzyjecia >= DATEADD(DAY,-7,GETDATE())
                    ORDER BY fc.Loss DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        alerty.Add(new AlertItem
                        {
                            Typ = "Ubytek",
                            Priorytet = "Wysoki",
                            Tytul = "Wysoki ubytek transportowy",
                            Opis = $"{r["Hodowca"]} - ubytek {Convert.ToDecimal(r["Ubytek"]):F1}%",
                            Data = r.GetDateTime(1),
                            Ikona = "⚠️"
                        });
                    }
                }

                // Padnięcia powyżej 1%
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 c.Name as Hodowca, fc.DataPrzyjecia,
                           fc.IncDeadConf as Padniete, fc.LumQnt as Sztuki
                    FROM [dbo].[FarmerCalc] fc WITH (NOLOCK)
                    JOIN [dbo].[Customer] c ON fc.CustomerGID = c.GID
                    WHERE fc.IncDeadConf > 0 AND fc.DataPrzyjecia >= DATEADD(DAY,-7,GETDATE())
                      AND CAST(fc.IncDeadConf AS FLOAT) / NULLIF(fc.LumQnt,0) > 0.01
                    ORDER BY CAST(fc.IncDeadConf AS FLOAT) / NULLIF(fc.LumQnt,0) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        var padniete = Convert.ToInt32(r["Padniete"]);
                        var sztuki = Convert.ToInt32(r["Sztuki"]);
                        var proc = sztuki > 0 ? (decimal)padniete / sztuki * 100 : 0;
                        alerty.Add(new AlertItem
                        {
                            Typ = "Padnięcia",
                            Priorytet = proc > 3 ? "Krytyczny" : "Wysoki",
                            Tytul = "Padnięcia w dostawie",
                            Opis = $"{r["Hodowca"]} - {padniete} szt ({proc:F1}%)",
                            Data = r.GetDateTime(1),
                            Ikona = "💀"
                        });
                    }
                }

                // Nowe reklamacje (ostatnie 7 dni)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 Kontrahent, DataZgloszenia, Opis
                    FROM [dbo].[Reklamacje] WITH (NOLOCK)
                    WHERE Status = 'Nowa' AND DataZgloszenia >= DATEADD(DAY,-7,GETDATE())
                    ORDER BY DataZgloszenia DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        alerty.Add(new AlertItem
                        {
                            Typ = "Reklamacja",
                            Priorytet = "Sredni",
                            Tytul = "Nowa reklamacja",
                            Opis = $"{r["Kontrahent"]} - {r["Opis"]?.ToString()?.Substring(0, Math.Min(80, (r["Opis"]?.ToString()?.Length ?? 0)))}",
                            Data = r.IsDBNull(1) ? DateTime.MinValue : r.GetDateTime(1),
                            Ikona = "📋"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Alerty error: {ex.Message}");
            }

            return alerty.OrderByDescending(a => a.Priorytet switch
            {
                "Krytyczny" => 4,
                "Wysoki" => 3,
                "Sredni" => 2,
                _ => 1
            }).ThenByDescending(a => a.Data).ToList();
        }

        #region IDisposable

        public void CloseConnections()
        {
            _poolLibra?.Dispose(); _poolLibra = null;
            _poolHandel?.Dispose(); _poolHandel = null;
            _poolTransport?.Dispose(); _poolTransport = null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                CloseConnections();
                _lockLibra.Dispose();
                _lockHandel.Dispose();
                _lockTransport.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
