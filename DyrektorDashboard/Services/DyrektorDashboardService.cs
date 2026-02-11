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

            var taskLibra = Task.Run(async () =>
            {
                try
                {
                    using var conn = new SqlConnection(_connLibra);
                    await conn.OpenAsync(ct);

                    // Żywiec dzisiaj (FarmerCalc: CalcDate, NettoWeight, LumQnt)
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(*) as Dostawy,
                               ISNULL(SUM(ISNULL(LumQnt,0)),0) as Sztuki,
                               ISNULL(SUM(ISNULL(NettoWeight,0)),0) as WagaKg
                        FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                        WHERE CAST(CalcDate AS DATE) = CAST(GETDATE() AS DATE)
                          AND ISNULL(Deleted,0) = 0", conn))
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

                    // Zamówienia dzisiaj (ZamowieniaMieso: DataUboju, ilości w ZamowieniaMiesoTowar)
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(DISTINCT z.Id) as Liczba,
                               ISNULL(SUM(ISNULL(t.Ilosc,0)),0) as SumaKg
                        FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                        LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t WITH (NOLOCK) ON z.Id = t.ZamowienieId
                        WHERE z.DataUboju = CAST(GETDATE() AS DATE)
                          AND ISNULL(z.Status,'Nowe') <> 'Anulowane'", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ZamowieniaDzisLiczba = r.GetInt32(0);
                            kpi.ZamowieniaDzisKg = Convert.ToDecimal(r["SumaKg"]);
                        }
                    }

                    // Zamówienia jutro
                    using (var cmd = new SqlCommand(@"
                        SELECT COUNT(DISTINCT z.Id) as Liczba,
                               ISNULL(SUM(ISNULL(t.Ilosc,0)),0) as SumaKg
                        FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                        LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t WITH (NOLOCK) ON z.Id = t.ZamowienieId
                        WHERE z.DataUboju = CAST(DATEADD(DAY,1,GETDATE()) AS DATE)
                          AND ISNULL(z.Status,'Nowe') <> 'Anulowane'", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ZamowieniaJutroLiczba = r.GetInt32(0);
                            kpi.ZamowieniaJutroKg = Convert.ToDecimal(r["SumaKg"]);
                        }
                    }

                    // Reklamacje otwarte (NazwaKontrahenta, Status, SumaKg)
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            ISNULL(SUM(CASE WHEN Status = 'Nowa' THEN 1 ELSE 0 END),0) as Nowe,
                            ISNULL(SUM(CASE WHEN Status IN ('Nowa','W trakcie') THEN 1 ELSE 0 END),0) as Otwarte,
                            ISNULL(SUM(CASE WHEN Status IN ('Nowa','W trakcie') THEN ISNULL(SumaKg,0) ELSE 0 END),0) as SumaKg
                        FROM [dbo].[Reklamacje] WITH (NOLOCK)", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ReklamacjeNowe = Convert.ToInt32(r["Nowe"]);
                            kpi.ReklamacjeOtwarte = Convert.ToInt32(r["Otwarte"]);
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

                    // Produkcja dzisiaj (JOIN: MZ.super=MG.id, kolumna: MZ.ilosc, data: MG.data)
                    using (var cmd = new SqlCommand(@"
                        SELECT
                            ISNULL(SUM(CASE WHEN MG.seria IN ('sPWU','sPWP','PWP') THEN ABS(MZ.ilosc) ELSE 0 END),0) as UbojKg,
                            ISNULL(SUM(CASE WHEN MG.seria IN ('sWZ','sWZ-W') THEN ABS(MZ.ilosc) ELSE 0 END),0) as WydaniaKg
                        FROM [HM].[MG] MG WITH (NOLOCK)
                        JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
                        WHERE MG.data = CAST(GETDATE() AS DATE)
                          AND MG.aktywny = 1
                          AND MG.seria IN ('sPWU','sPWP','PWP','sWZ','sWZ-W')", conn))
                    {
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        using var r = await cmd.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            kpi.ProdukcjaDzisKg = Convert.ToDecimal(r["UbojKg"]);
                            kpi.ProdukcjaLWPKg = Convert.ToDecimal(r["WydaniaKg"]);
                        }
                    }

                    // Magazyn mroźni - stan aktualny (MZ.iloscwp dla stanu kumulatywnego)
                    using (var cmd = new SqlCommand($@"
                        SELECT
                            ISNULL(SUM(ABS([iloscwp])),0) as StanKg,
                            ISNULL(SUM(ABS([wartNetto])),0) as StanWartosc
                        FROM [HM].[MZ] WITH (NOLOCK)
                        WHERE [data] >= '{DATA_STARTOWA}'
                          AND [data] <= GETDATE()
                          AND [magazyn] = {MAGAZYN_MROZNIA}
                          AND typ = '0'
                        HAVING ABS(SUM([iloscwp])) > 0", conn))
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
                            ISNULL(SUM(CASE WHEN Status IN ('Przypisany','Wlasny') THEN 1 ELSE 0 END),0) as Aktywne,
                            ISNULL(SUM(CASE WHEN Status = 'Zakonczony' THEN 1 ELSE 0 END),0) as Zakonczone,
                            COUNT(DISTINCT KierowcaID) as Kierowcy
                        FROM [dbo].[Kurs] WITH (NOLOCK)
                        WHERE CAST(DataKursu AS DATE) = CAST(GETDATE() AS DATE)", conn);
                    cmd.CommandTimeout = CMD_TIMEOUT;

                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        kpi.TransportDzisKursy = r.GetInt32(0);
                        kpi.TransportAktywneKursy = Convert.ToInt32(r["Aktywne"]);
                        kpi.TransportZakonczoneKursy = Convert.ToInt32(r["Zakonczone"]);
                        kpi.TransportKierowcyAktywni = Convert.ToInt32(r["Kierowcy"]);
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
        // FarmerCalc: CalcDate (data), NettoWeight (waga), LumQnt (sztuki),
        //   DeclI1 (plan szt.), CustomerGID → Dostawcy.ID, Deleted
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
                    SELECT COUNT(*) as Dostawy,
                           ISNULL(SUM(ISNULL(LumQnt,0)),0) as Sztuki,
                           ISNULL(SUM(ISNULL(NettoWeight,0)),0) as WagaKg,
                           ISNULL(SUM(ISNULL(NettoWeight,0)),0) as Wartosc,
                           CASE WHEN SUM(ISNULL(LumQnt,0)) > 0
                                THEN SUM(ISNULL(NettoWeight,0)) / SUM(ISNULL(LumQnt,0))
                                ELSE 0 END as SredniaWagaSzt,
                           CASE WHEN SUM(ISNULL(NettoWeight,0)) > 0 AND SUM(COALESCE(NettoFarmWeight, WagaDek, 0)) > 0
                                THEN (1.0 - SUM(ISNULL(NettoWeight,0)) / NULLIF(SUM(COALESCE(NettoFarmWeight, WagaDek, 0)),0)) * 100
                                ELSE 0 END as SredniUbytek
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE CAST(CalcDate AS DATE) = CAST(GETDATE() AS DATE)
                      AND ISNULL(Deleted,0) = 0", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.DzisDostawy = r.GetInt32(0);
                        dane.DzisSztuki = Convert.ToInt32(r["Sztuki"]);
                        dane.DzisKg = Convert.ToDecimal(r["WagaKg"]);
                        dane.DzisWartosc = Convert.ToDecimal(r["Wartosc"]);
                        dane.SredniaCenaDzis = Convert.ToDecimal(r["SredniaWagaSzt"]);
                        dane.SredniUbytekDzis = Convert.ToDecimal(r["SredniUbytek"]);
                    }
                }

                // Tydzień
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(ISNULL(NettoWeight,0)),0) as Waga
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE CalcDate >= DATEADD(DAY,-7,GETDATE())
                      AND ISNULL(Deleted,0) = 0", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.TydzienKg = Convert.ToDecimal(r["Waga"]);
                    }
                }

                // Miesiąc
                using (var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(ISNULL(NettoWeight,0)),0) as Waga
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE CalcDate >= DATEADD(MONTH,-1,GETDATE())
                      AND ISNULL(Deleted,0) = 0", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.MiesiacKg = Convert.ToDecimal(r["Waga"]);
                    }
                }

                // Top 5 hodowców (miesiąc) - Dostawcy table
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 d.Nazwa as Nazwa, d.Miejscowosc as Miasto,
                           SUM(ISNULL(fc.NettoWeight,0)) as Waga,
                           SUM(ISNULL(fc.NettoWeight,0)) as Wartosc,
                           COUNT(*) as Dostawy
                    FROM [dbo].[FarmerCalc] fc WITH (NOLOCK)
                    LEFT JOIN [dbo].[Dostawcy] d ON LTRIM(RTRIM(CAST(d.ID AS NVARCHAR(20)))) = LTRIM(RTRIM(fc.CustomerGID))
                    WHERE fc.CalcDate >= DATEADD(MONTH,-1,GETDATE())
                      AND ISNULL(fc.Deleted,0) = 0
                    GROUP BY d.Nazwa, d.Miejscowosc
                    ORDER BY SUM(ISNULL(fc.NettoWeight,0)) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    int poz = 1;
                    while (await r.ReadAsync(ct))
                    {
                        dane.TopHodowcy.Add(new TopHodowcaItem
                        {
                            Pozycja = poz++,
                            Nazwa = r["Nazwa"]?.ToString() ?? "(brak)",
                            Miasto = r["Miasto"]?.ToString(),
                            WagaKg = Convert.ToDecimal(r["Waga"]),
                            Wartosc = Convert.ToDecimal(r["Wartosc"]),
                            LiczbaDostaw = r.GetInt32(4)
                        });
                    }
                }

                // Trend 8 tygodni
                using (var cmd = new SqlCommand(@"
                    SELECT DATEPART(WEEK, CalcDate) as Tydzien,
                           MIN(CalcDate) as Poczatek,
                           SUM(ISNULL(NettoWeight,0)) as Waga,
                           SUM(ISNULL(NettoWeight,0)) as Wartosc,
                           CASE WHEN SUM(ISNULL(LumQnt,0)) > 0
                                THEN SUM(ISNULL(NettoWeight,0)) / SUM(ISNULL(LumQnt,0))
                                ELSE 0 END as SredniaCena,
                           COUNT(*) as Dostawy
                    FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                    WHERE CalcDate >= DATEADD(WEEK,-8,GETDATE())
                      AND ISNULL(Deleted,0) = 0
                    GROUP BY DATEPART(YEAR, CalcDate), DATEPART(WEEK, CalcDate)
                    ORDER BY MIN(CalcDate)", conn))
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
                    SELECT TOP 20 fc.CalcDate as Godzina,
                           d.Nazwa as Hodowca,
                           ISNULL(fc.LumQnt,0) as Sztuki,
                           ISNULL(fc.NettoWeight,0) as Waga,
                           CASE WHEN ISNULL(fc.LumQnt,0) > 0
                                THEN fc.NettoWeight / fc.LumQnt
                                ELSE 0 END as SrWagaSzt,
                           '' as Kierowca
                    FROM [dbo].[FarmerCalc] fc WITH (NOLOCK)
                    LEFT JOIN [dbo].[Dostawcy] d ON LTRIM(RTRIM(CAST(d.ID AS NVARCHAR(20)))) = LTRIM(RTRIM(fc.CustomerGID))
                    WHERE CAST(fc.CalcDate AS DATE) = CAST(GETDATE() AS DATE)
                      AND ISNULL(fc.Deleted,0) = 0
                    ORDER BY fc.CalcDate DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.DostawyDzis.Add(new DostawaDzisItem
                        {
                            Godzina = r.GetDateTime(0),
                            Hodowca = r["Hodowca"]?.ToString() ?? "(brak)",
                            Sztuki = Convert.ToInt32(r["Sztuki"]),
                            WagaKg = Convert.ToDecimal(r["Waga"]),
                            Cena = Convert.ToDecimal(r["SrWagaSzt"]),
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
        // ZamowieniaMieso: DataUboju (data uboju), KlientId (FK do Handel.SSCommon.STContractors)
        // ZamowieniaMiesoTowar: ZamowienieId, KodTowaru, Ilosc, Cena (varchar!)
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
                    SELECT COUNT(DISTINCT z.Id) as Liczba,
                           ISNULL(SUM(ISNULL(t.Ilosc,0)),0) as SumaKg
                    FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t WITH (NOLOCK) ON z.Id = t.ZamowienieId
                    WHERE z.DataUboju = CAST(GETDATE() AS DATE)
                      AND ISNULL(z.Status,'Nowe') <> 'Anulowane'", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.LiczbaZamowienDzis = r.GetInt32(0);
                        dane.SumaKgDzis = Convert.ToDecimal(r["SumaKg"]);
                    }
                }

                // Zamówienia jutro
                using (var cmd = new SqlCommand(@"
                    SELECT COUNT(DISTINCT z.Id) as Liczba,
                           ISNULL(SUM(ISNULL(t.Ilosc,0)),0) as SumaKg
                    FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t WITH (NOLOCK) ON z.Id = t.ZamowienieId
                    WHERE z.DataUboju = CAST(DATEADD(DAY,1,GETDATE()) AS DATE)
                      AND ISNULL(z.Status,'Nowe') <> 'Anulowane'", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        dane.LiczbaZamowienJutro = r.GetInt32(0);
                        dane.SumaKgJutro = Convert.ToDecimal(r["SumaKg"]);
                    }
                }

                // Trend dzienny (ostatnie 14 dni)
                using (var cmd = new SqlCommand(@"
                    SELECT z.DataUboju as Dzien,
                           COUNT(DISTINCT z.Id) as Liczba,
                           ISNULL(SUM(ISNULL(t.Ilosc,0)),0) as SumaKg,
                           0 as Wartosc
                    FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t WITH (NOLOCK) ON z.Id = t.ZamowienieId
                    WHERE z.DataUboju >= DATEADD(DAY,-14,GETDATE())
                      AND z.DataUboju <= DATEADD(DAY,1,GETDATE())
                      AND ISNULL(z.Status,'Nowe') <> 'Anulowane'
                    GROUP BY z.DataUboju
                    ORDER BY z.DataUboju", conn))
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

                // Top 10 klientów (miesiąc) - KlientId, nazwy z HANDEL
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 10 z.KlientId,
                           ISNULL(SUM(ISNULL(t.Ilosc,0)),0) as SumaKg,
                           COUNT(DISTINCT z.Id) as Zamowienia
                    FROM [dbo].[ZamowieniaMieso] z WITH (NOLOCK)
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] t WITH (NOLOCK) ON z.Id = t.ZamowienieId
                    WHERE z.DataUboju >= DATEADD(MONTH,-1,GETDATE())
                      AND ISNULL(z.Status,'Nowe') <> 'Anulowane'
                    GROUP BY z.KlientId
                    ORDER BY ISNULL(SUM(ISNULL(t.Ilosc,0)),0) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        dane.TopKlienci.Add(new TopKlientItem
                        {
                            Nazwa = $"Klient #{r["KlientId"]}",
                            SumaKg = Convert.ToDecimal(r["SumaKg"]),
                            SumaWartosc = 0,
                            LiczbaZamowien = r.GetInt32(2)
                        });
                    }
                }

                // Rozwiąż nazwy klientów z HANDEL
                if (dane.TopKlienci.Any())
                {
                    try
                    {
                        using var connH = new SqlConnection(_connHandel);
                        await connH.OpenAsync(ct);
                        foreach (var klient in dane.TopKlienci)
                        {
                            var idStr = klient.Nazwa.Replace("Klient #", "");
                            if (int.TryParse(idStr, out int klientId))
                            {
                                using var cmdH = new SqlCommand(@"
                                    SELECT Name1 FROM [SSCommon].[STContractors] WHERE Id = @id", connH);
                                cmdH.Parameters.AddWithValue("@id", klientId);
                                cmdH.CommandTimeout = CMD_TIMEOUT;
                                var nazwa = await cmdH.ExecuteScalarAsync(ct);
                                if (nazwa != null && nazwa != DBNull.Value)
                                    klient.Nazwa = nazwa.ToString();
                            }
                        }
                    }
                    catch { /* Handel niedostępny - zostaw ID */ }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Zamówienia error: {ex.Message}");
            }
            return dane;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKA: PRODUKCJA
        // HM.MG JOIN HM.MZ ON MZ.super = MG.id
        // MZ.ilosc (ruch), MG.data (data), MG.aktywny=1, MG.seria
        // Serie: sPWU/sPWP/PWP (przyjęcia), sWZ/sWZ-W (wydania)
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
                           ISNULL(SUM(ABS(MZ.ilosc)),0) as SumaKg
                    FROM [HM].[MG] MG WITH (NOLOCK)
                    JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
                    WHERE MG.data = CAST(GETDATE() AS DATE)
                      AND MG.aktywny = 1
                      AND MG.seria IN ('sPWU','sPWP','PWP','sWZ','sWZ-W')
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
                            case "sPWU": case "sPWP": case "PWP":
                                dane.UbojDzisKg += kg; break;
                            case "sWZ": case "sWZ-W":
                                dane.KrojenieDzisKg += kg; break;
                        }
                    }
                }

                // Top 15 produktów dzisiaj
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 15 TW.kod, TW.nazwa,
                           SUM(ABS(MZ.ilosc)) as IloscKg, MG.seria
                    FROM [HM].[MG] MG WITH (NOLOCK)
                    JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
                    JOIN [HM].[TW] TW WITH (NOLOCK) ON MZ.idtw = TW.id
                    WHERE MG.data = CAST(GETDATE() AS DATE)
                      AND MG.aktywny = 1
                      AND MG.seria IN ('sPWU','sPWP','PWP','sWZ','sWZ-W')
                    GROUP BY TW.kod, TW.nazwa, MG.seria
                    ORDER BY SUM(ABS(MZ.ilosc)) DESC", conn))
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
                    SELECT TOP 7 MG.data as Dzien,
                           ISNULL(SUM(CASE WHEN MG.seria IN ('sPWU','sPWP','PWP') THEN ABS(MZ.ilosc) ELSE 0 END),0) as UbojKg,
                           ISNULL(SUM(CASE WHEN MG.seria IN ('sWZ','sWZ-W') THEN ABS(MZ.ilosc) ELSE 0 END),0) as WydaniaKg
                    FROM [HM].[MG] MG WITH (NOLOCK)
                    JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
                    WHERE MG.data >= DATEADD(DAY,-14,GETDATE())
                      AND MG.aktywny = 1
                      AND MG.seria IN ('sPWU','sPWP','PWP','sWZ','sWZ-W')
                    GROUP BY MG.data
                    HAVING SUM(ABS(MZ.ilosc)) > 0
                    ORDER BY MG.data DESC", conn))
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
                            KrojenieKg = Convert.ToDecimal(r["WydaniaKg"]),
                            LWPKg = Convert.ToDecimal(r["WydaniaKg"])
                        });
                    }
                }
                dane.TrendTygodniowy.Reverse();

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
        // MZ.kod (kod produktu), MZ.iloscwp (stan kumulat.), MZ.wartNetto
        // MZ.[data], MZ.magazyn=65552, MZ.typ='0'
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneMagazyn> GetDaneMagazynAsync(CancellationToken ct = default)
        {
            var dane = new DaneMagazyn();
            try
            {
                using var conn = new SqlConnection(_connHandel);
                await conn.OpenAsync(ct);

                // Stan magazynowy wg produktu (wzorzec z Mroznia.cs linii 2416-2427)
                using var cmd = new SqlCommand($@"
                    SELECT kod,
                           CAST(ABS(SUM([iloscwp])) AS DECIMAL(18,3)) AS IloscKg,
                           CAST(ABS(SUM([wartNetto])) AS DECIMAL(18,2)) AS WartoscZl
                    FROM [HM].[MZ] WITH (NOLOCK)
                    WHERE [data] >= '{DATA_STARTOWA}'
                      AND [data] <= GETDATE()
                      AND [magazyn] = {MAGAZYN_MROZNIA}
                      AND typ = '0'
                    GROUP BY kod
                    HAVING ABS(SUM([iloscwp])) > 1
                    ORDER BY ABS(SUM([iloscwp])) DESC", conn);
                cmd.CommandTimeout = CMD_TIMEOUT;

                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var kodProduktu = r["kod"]?.ToString()?.Trim() ?? "";
                    var item = new StanProduktItem
                    {
                        Kod = kodProduktu,
                        Nazwa = kodProduktu,
                        IloscKg = Convert.ToDecimal(r["IloscKg"]),
                        WartoscZl = Convert.ToDecimal(r["WartoscZl"]),
                        Katalog = "Inny"
                    };
                    dane.TopProdukty.Add(item);

                    dane.StanCaloscKg += item.IloscKg;
                    dane.StanWartoscZl += item.WartoscZl;
                    dane.LiczbaPozycji++;
                }

                // Rozdziel świeży/mrożony po TW.katalog
                try
                {
                    using var cmd2 = new SqlCommand($@"
                        SELECT kod, katalog
                        FROM [HM].[TW] WITH (NOLOCK)
                        WHERE katalog IN ('{KATALOG_SWIEZY}', '{KATALOG_MROZONY}')", conn);
                    cmd2.CommandTimeout = CMD_TIMEOUT;
                    var katalogMap = new Dictionary<string, string>();
                    using var r2 = await cmd2.ExecuteReaderAsync(ct);
                    while (await r2.ReadAsync(ct))
                    {
                        var kod = r2["kod"]?.ToString()?.Trim() ?? "";
                        var kat = r2["katalog"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(kod))
                            katalogMap[kod] = kat == KATALOG_SWIEZY ? "Świeży" : "Mrożony";
                    }

                    foreach (var item in dane.TopProdukty)
                    {
                        if (katalogMap.TryGetValue(item.Kod, out var kat))
                        {
                            item.Katalog = kat;
                            if (kat == "Świeży") dane.StanSwiezyKg += item.IloscKg;
                            else dane.StanMrozonyKg += item.IloscKg;
                        }
                    }
                }
                catch { /* TW lookup opcjonalny */ }
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
                    SELECT k.KursID, k.Status, k.GodzWyjazdu,
                           k.KierowcaID, k.PojazdID
                    FROM [dbo].[Kurs] k WITH (NOLOCK)
                    WHERE CAST(k.DataKursu AS DATE) = CAST(GETDATE() AS DATE)
                    ORDER BY k.KursID", conn);
                cmd.CommandTimeout = CMD_TIMEOUT;

                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var kurs = new KursItem
                    {
                        KursID = r.GetInt64(0),
                        Kierowca = $"Kier. #{r["KierowcaID"]}",
                        Pojazd = r["PojazdID"]?.ToString(),
                        Status = r["Status"]?.ToString()?.Trim(),
                        LiczbaLadunkow = 0
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
        // Reklamacje: NazwaKontrahenta, Status, SumaKg, DataZgloszenia, Opis
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
                    SELECT Status, COUNT(*) as Liczba, ISNULL(SUM(ISNULL(SumaKg,0)),0) as SumaKg
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

                // Ostatnie 15 reklamacji (NazwaKontrahenta - prawidłowa kolumna)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 15 Id, DataZgloszenia, NazwaKontrahenta, Status, Opis,
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
                            Kontrahent = r["NazwaKontrahenta"]?.ToString(),
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
        // Wg serii sMM+/sMM-/sMK+/sMK- z mroźni
        // ════════════════════════════════════════════════════════════════════

        public async Task<DaneOpakowania> GetDaneOpakowaniaAsync(CancellationToken ct = default)
        {
            var dane = new DaneOpakowania();
            try
            {
                using var conn = new SqlConnection(_connHandel);
                await conn.OpenAsync(ct);

                using var cmd = new SqlCommand($@"
                    SELECT MZ.kod as TypOpakowania,
                           ISNULL(SUM(CASE WHEN MZ.ilosc > 0 THEN MZ.ilosc ELSE 0 END),0) as Wydane,
                           ISNULL(SUM(CASE WHEN MZ.ilosc < 0 THEN ABS(MZ.ilosc) ELSE 0 END),0) as Przyjete,
                           ISNULL(SUM(MZ.ilosc),0) as Saldo
                    FROM [HM].[MG] MG WITH (NOLOCK)
                    JOIN [HM].[MZ] MZ WITH (NOLOCK) ON MZ.super = MG.id
                    WHERE MG.magazyn = {MAGAZYN_MROZNIA}
                      AND (MG.seria = 'sMM+' OR MG.seria = 'sMM-' OR MG.seria = 'sMK-' OR MG.seria = 'sMK+')
                      AND MG.[Data] >= DATEADD(YEAR,-1,GETDATE())
                    GROUP BY MZ.kod
                    HAVING ABS(SUM(MZ.ilosc)) > 0
                    ORDER BY ABS(SUM(MZ.ilosc)) DESC", conn);
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
        // FarmerCalc: CalcDate (realizacja), DeclI1 (plan sztuki)
        // ════════════════════════════════════════════════════════════════════

        public async Task<DanePlanTygodniowy> GetPlanTygodniowyAsync(CancellationToken ct = default)
        {
            var dane = new DanePlanTygodniowy();
            try
            {
                var today = DateTime.Today;
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                var monday = today.AddDays(-diff);

                using var conn = new SqlConnection(_connLibra);
                await conn.OpenAsync(ct);

                for (int i = 0; i < 5; i++)
                {
                    var dzien = monday.AddDays(i);
                    var planDzien = new PlanDzienItem
                    {
                        Data = dzien,
                        DzienTygodnia = _nazwyDni[(int)dzien.DayOfWeek],
                        CzyDzisiaj = dzien.Date == today
                    };

                    // Realizacja (rzeczywiste dostawy - CalcDate)
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM(ISNULL(NettoWeight,0)),0) as Waga
                        FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                        WHERE CAST(CalcDate AS DATE) = @dzien
                          AND ISNULL(Deleted,0) = 0", conn))
                    {
                        cmd.Parameters.AddWithValue("@dzien", dzien.Date);
                        cmd.CommandTimeout = CMD_TIMEOUT;
                        var result = await cmd.ExecuteScalarAsync(ct);
                        planDzien.RealizacjaKg = result != null && result != DBNull.Value
                            ? Convert.ToDecimal(result) : 0;
                    }

                    // Plan (DeclI1 = planowana ilość sztuk)
                    using (var cmd = new SqlCommand(@"
                        SELECT ISNULL(SUM(ISNULL(DeclI1,0)),0) as Planowane
                        FROM [dbo].[FarmerCalc] WITH (NOLOCK)
                        WHERE CAST(CalcDate AS DATE) = @dzien
                          AND ISNULL(Deleted,0) = 0", conn))
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

                // Duże odchylenia wagi (>5% różnicy między wagą hodowcy a ubojową)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 d.Nazwa as Hodowca, fc.CalcDate as Data,
                           CASE WHEN COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                                THEN ABS((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0))
                                     / COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) * 100)
                                ELSE 0 END as OdchylenieProcent
                    FROM [dbo].[FarmerCalc] fc WITH (NOLOCK)
                    LEFT JOIN [dbo].[Dostawcy] d ON LTRIM(RTRIM(CAST(d.ID AS NVARCHAR(20)))) = LTRIM(RTRIM(fc.CustomerGID))
                    WHERE fc.CalcDate >= DATEADD(DAY,-7,GETDATE())
                      AND ISNULL(fc.Deleted,0) = 0
                      AND COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) > 0
                      AND fc.NettoWeight > 0
                      AND ABS((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0))
                          / COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) * 100) > 5
                    ORDER BY ABS((fc.NettoWeight - COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0))
                             / COALESCE(fc.NettoFarmWeight, fc.WagaDek, 0) * 100) DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        alerty.Add(new AlertItem
                        {
                            Typ = "Odchylenie wagi",
                            Priorytet = "Wysoki",
                            Tytul = "Duże odchylenie wagi",
                            Opis = $"{r["Hodowca"] ?? "?"} - odchylenie {Convert.ToDecimal(r["OdchylenieProcent"]):F1}%",
                            Data = r.GetDateTime(1),
                            Ikona = "⚠️"
                        });
                    }
                }

                // Nowe reklamacje (ostatnie 7 dni)
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 5 NazwaKontrahenta, DataZgloszenia, Opis
                    FROM [dbo].[Reklamacje] WITH (NOLOCK)
                    WHERE Status = 'Nowa' AND DataZgloszenia >= DATEADD(DAY,-7,GETDATE())
                    ORDER BY DataZgloszenia DESC", conn))
                {
                    cmd.CommandTimeout = CMD_TIMEOUT;
                    using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        var opis = r["Opis"]?.ToString() ?? "";
                        if (opis.Length > 80) opis = opis.Substring(0, 80) + "...";
                        alerty.Add(new AlertItem
                        {
                            Typ = "Reklamacja",
                            Priorytet = "Sredni",
                            Tytul = "Nowa reklamacja",
                            Opis = $"{r["NazwaKontrahenta"]} - {opis}",
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
