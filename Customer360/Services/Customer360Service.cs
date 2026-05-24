using Kalendarz1.Customer360.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Agregator danych klienta z 3 baz (HANDEL kontrahenci + faktury, LibraNet zamówienia + reklamacje).
    /// Wszystkie zapytania paralelnie (Task.WhenAll), cache na poziomie window (nie static — dane się zmieniają).
    /// </summary>
    public class Customer360Service
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Cache zbioru "aktywni klienci" (KlientId z zamówień ostatnich 12 mies) — odświeżany co 10 min.
        // Pozwala sortować search results: najpierw aktywni klienci, potem reszta. Lepsze UX dla dużych baz (>5000).
        private static HashSet<int>? _aktywniCache;
        private static DateTime _aktywniCacheAt = DateTime.MinValue;
        private static readonly System.Threading.SemaphoreSlim _aktywniLock = new(1, 1);

        private async Task<HashSet<int>> GetAktywniKlienciAsync()
        {
            if (_aktywniCache != null && (DateTime.Now - _aktywniCacheAt).TotalMinutes < 10)
                return _aktywniCache;
            await _aktywniLock.WaitAsync();
            try
            {
                if (_aktywniCache != null && (DateTime.Now - _aktywniCacheAt).TotalMinutes < 10)
                    return _aktywniCache;
                var set = new HashSet<int>();
                try
                {
                    await using var cn = new SqlConnection(ConnLibra);
                    await cn.OpenAsync();
                    await using var cmd = new SqlCommand(
                        @"SELECT DISTINCT KlientId FROM dbo.ZamowieniaMieso
                          WHERE KlientId IS NOT NULL
                            AND DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())
                            AND ISNULL(Status,'') NOT IN ('Anulowane','Anulowano')", cn) { CommandTimeout = 8 };
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync()) set.Add(rd.GetInt32(0));
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Aktywni cache] {ex.Message}"); }
                _aktywniCache = set;
                _aktywniCacheAt = DateTime.Now;
                return set;
            }
            finally { _aktywniLock.Release(); }
        }

        /// <summary>Lista wszystkich handlowców (dystynktnych) — do chip-ów filtra w pickerze.</summary>
        public async Task<List<string>> GetHandlowcyDistinctAsync()
        {
            var lista = new List<string>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    @"SELECT DISTINCT CDim_Handlowiec_Val
                      FROM [HANDEL].[SSCommon].[ContractorClassification]
                      WHERE CDim_Handlowiec_Val IS NOT NULL AND LTRIM(CDim_Handlowiec_Val) <> ''
                      ORDER BY CDim_Handlowiec_Val", cn) { CommandTimeout = 5 };
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync()) lista.Add(rd.GetString(0));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Handlowcy distinct] {ex.Message}"); }
            return lista;
        }

        /// <summary>Ostatni błąd SQL — wystawione żeby UI mógł pokazać.</summary>
        public string? LastError { get; private set; }

        // ── Wyszukiwanie klientów — uproszczony query, bez OUTER APPLY ──
        public async Task<List<KlientSearchItem>> SearchKlienciAsync(string searchText, int limit = 100, string? handlowiecFilter = null)
        {
            LastError = null;
            var lista = new List<KlientSearchItem>();

            // Krok 1: tylko STContractors (bez JOIN-ów) — żeby gwarantowanie coś zwrócić
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();

                string whereClause = string.IsNullOrWhiteSpace(searchText)
                    ? "((C.Shortcut IS NOT NULL AND LTRIM(C.Shortcut) <> '') OR (C.Name IS NOT NULL AND LTRIM(C.Name) <> ''))"
                    : "(C.Shortcut LIKE @q OR C.Name LIKE @q OR C.NIP LIKE @q)";

                int serverLimit = Math.Min(limit * 3, 1500);

                string sql = $@"
                    SELECT TOP (@serverLimit) C.Id,
                          ISNULL(NULLIF(LTRIM(RTRIM(C.Shortcut)),''), ISNULL(C.Name,'')) AS Nazwa,
                          ISNULL(C.NIP,'') AS NIP
                    FROM [HANDEL].[SSCommon].[STContractors] C
                    WHERE {whereClause}
                    ORDER BY Nazwa";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@serverLimit", serverLimit);
                if (!string.IsNullOrWhiteSpace(searchText))
                    cmd.Parameters.AddWithValue("@q", "%" + searchText.Trim() + "%");

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new KlientSearchItem
                    {
                        Id = rd.GetInt32(0),
                        Nazwa = rd.GetString(1),
                        NIP = rd.GetString(2)
                    });
                }
                System.Diagnostics.Debug.WriteLine($"[C360 search] q='{searchText}' → {lista.Count} klientów");
            }
            catch (Exception ex)
            {
                LastError = $"Błąd SQL wyszukiwania klientów: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[C360 search] {ex}");
            }

            if (lista.Count == 0) return lista;

            // Krok 2: dociągnij miasto + handlowca per klient (osobne zapytania, każde z fallbackiem)
            await EnrichClientsAsync(lista);

            // Filtr handlowca (po wzbogaceniu)
            if (!string.IsNullOrWhiteSpace(handlowiecFilter))
                lista = lista.Where(k => string.Equals(k.Handlowiec, handlowiecFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            // Sortowanie: aktywni na górze
            var aktywni = await GetAktywniKlienciAsync();
            return lista
                .OrderByDescending(k => aktywni.Contains(k.Id))
                .ThenBy(k => k.Nazwa, StringComparer.CurrentCultureIgnoreCase)
                .Take(limit)
                .ToList();
        }

        /// <summary>Wzbogaca listę klientów o Miasto i Handlowca — best-effort, błąd nie psuje listy.</summary>
        private async Task EnrichClientsAsync(List<KlientSearchItem> klienci)
        {
            if (klienci.Count == 0) return;
            string idList = string.Join(",", klienci.Select(k => k.Id));

            // Handlowcy
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    $@"SELECT ElementId, MAX(ISNULL(CDim_Handlowiec_Val,'')) AS H
                       FROM [HANDEL].[SSCommon].[ContractorClassification]
                       WHERE ElementId IN ({idList})
                       GROUP BY ElementId", cn) { CommandTimeout = 5 };
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    var k = klienci.FirstOrDefault(x => x.Id == id);
                    if (k != null) k.Handlowiec = rd.IsDBNull(1) ? "" : rd.GetString(1);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 enrich handlowca] {ex.Message}"); }

            // Miasta — przez JOIN z STContractors po ContactGuid
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    $@"SELECT C.Id, MAX(ISNULL(POA.Place,'')) AS Miasto
                       FROM [HANDEL].[SSCommon].[STContractors] C
                       LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] POA ON POA.ContactGuid = C.ContactGuid
                       WHERE C.Id IN ({idList})
                       GROUP BY C.Id", cn) { CommandTimeout = 5 };
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    var k = klienci.FirstOrDefault(x => x.Id == id);
                    if (k != null) k.Miasto = rd.IsDBNull(1) ? "" : rd.GetString(1);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 enrich miasto] {ex.Message}"); }
        }

        /// <summary>Liczba wszystkich aktywnych klientów (do badge w pickerze).</summary>
        public async Task<int> GetAktywniCountAsync()
        {
            try { return (await GetAktywniKlienciAsync()).Count; }
            catch { return 0; }
        }

        // ── Header klienta — pełne dane, 3 osobne kroki (każdy może paść osobno) ──
        public async Task<KlientHeader?> GetKlientHeaderAsync(int klientId)
        {
            LastError = null;
            KlientHeader? hdr = null;

            // KROK 1: STContractors (must-have)
            try
            {
                await using var cnH = new SqlConnection(ConnHandel);
                await cnH.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT TOP 1 C.Id,
                           ISNULL(NULLIF(LTRIM(RTRIM(C.Shortcut)),''), ISNULL(C.Name,'')) AS Nazwa,
                           ISNULL(C.NIP,'') AS NIP
                    FROM [HANDEL].[SSCommon].[STContractors] C
                    WHERE C.Id = @id", cnH) { CommandTimeout = 8 };
                cmd.Parameters.AddWithValue("@id", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    hdr = new KlientHeader
                    {
                        Id = rd.GetInt32(0),
                        Nazwa = rd.GetString(1),
                        NIP = rd.GetString(2)
                    };
                }
            }
            catch (Exception ex)
            {
                LastError = $"Nie udało się pobrać klienta z HANDEL: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[C360 header step1] {ex}");
                return null;
            }
            if (hdr == null) return null;

            // KROK 2: adres z STPostOfficeAddresses (best-effort)
            try
            {
                await using var cnH = new SqlConnection(ConnHandel);
                await cnH.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT TOP 1 ISNULL(POA.Street,'') AS Adres,
                                 ISNULL(POA.PostCode,'') AS KodPocztowy,
                                 ISNULL(POA.Place,'') AS Miasto
                    FROM [HANDEL].[SSCommon].[STContractors] C
                    INNER JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] POA ON POA.ContactGuid = C.ContactGuid
                    WHERE C.Id = @id", cnH) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@id", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    hdr.Adres = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    hdr.KodPocztowy = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    hdr.Miasto = rd.IsDBNull(2) ? "" : rd.GetString(2);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 header step2 addr] {ex.Message}"); }

            // KROK 3: handlowiec z ContractorClassification (best-effort)
            try
            {
                await using var cnH = new SqlConnection(ConnHandel);
                await cnH.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT TOP 1 ISNULL(CDim_Handlowiec_Val,'')
                    FROM [HANDEL].[SSCommon].[ContractorClassification]
                    WHERE ElementId = @id", cnH) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@id", klientId);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value) hdr.Handlowiec = r.ToString() ?? "";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 header step3 handl] {ex.Message}"); }

            // KROK 4: Kategoria z KartotekaOdbiorcyDane (LibraNet)
            try
            {
                await using var cnL = new SqlConnection(ConnLibra);
                await cnL.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT TOP 1 ISNULL(KategoriaHandlowca,'') FROM dbo.KartotekaOdbiorcyDane WHERE IdSymfonia = @id", cnL);
                cmd.Parameters.AddWithValue("@id", klientId);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value) hdr.Kategoria = r.ToString() ?? "";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 header step4 kategoria] {ex.Message}"); }

            return hdr;
        }

        // ── KPI klienta — agregat za ostatnie 12 miesięcy ──
        public async Task<KlientKpi> GetKpiAsync(int klientId)
        {
            var kpi = new KlientKpi();
            try
            {
                // Paralelnie: zamówienia (LibraNet) + finanse (HANDEL) + reklamacje (LibraNet)
                var taskZam = LoadZamowieniaSummaryAsync(klientId);
                var taskFin = LoadFinanseSummaryAsync(klientId);
                var taskRek = LoadReklamacjeSummaryAsync(klientId);
                await Task.WhenAll(taskZam, taskFin, taskRek);

                var (obrot, marza, sredniaMarza, liczba, sumaKg, ostatnie, sredniDni) = await taskZam;
                var (limit, doZap, term, przeterm, maxDni, faktur) = await taskFin;
                var (liczbaRek, wartoscRek) = await taskRek;

                kpi.Obrot12M = obrot;
                kpi.Marza12M = marza;
                kpi.SredniaMarzaKg = sredniaMarza;
                kpi.LiczbaZamowien12M = liczba;
                kpi.SumaKg12M = sumaKg;
                kpi.OstatnieZamowienie = ostatnie;
                kpi.SredniCzasMiedzyZamowieniami = sredniDni;

                kpi.LimitKredytowy = limit;
                kpi.DoZaplaty = doZap;
                kpi.Terminowe = term;
                kpi.Przeterminowane = przeterm;
                kpi.MaxDniOpoznienia = maxDni;
                kpi.LiczbaFaktur = faktur;

                kpi.LiczbaReklamacji12M = liczbaRek;
                kpi.WartoscReklamacji12M = wartoscRek;

                // Obrót 12-24 mies temu (YoY)
                kpi.Obrot12MPrev = await LoadObrotPrevYearAsync(klientId);

                // Churn risk
                var (level, reason) = CalculateChurnRisk(kpi);
                kpi.ChurnRiskLevel = level;
                kpi.ChurnRiskReason = reason;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 KPI] {ex.Message}"); }
            return kpi;
        }

        private async Task<(decimal obrot, decimal marza, decimal sredniaMarzaKg, int liczba, decimal sumaKg, DateTime? ostatnie, decimal sredniDni)> LoadZamowieniaSummaryAsync(int klientId)
        {
            decimal obrot = 0, marza = 0, sumaKg = 0;
            int liczba = 0;
            DateTime? ostatnie = null;
            var daty = new List<DateTime>();

            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();

                // Summary: liczba zamówień, suma kg, suma wartości
                const string sqlAgg = @"
                    SELECT COUNT(DISTINCT z.Id) AS Liczba,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc,
                           MAX(z.DataPrzyjazdu) AS Ostatnie
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND z.DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')";
                await using (var cmd = new SqlCommand(sqlAgg, cn) { CommandTimeout = 8 })
                {
                    cmd.Parameters.AddWithValue("@kid", klientId);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        liczba = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        sumaKg = rd.IsDBNull(1) ? 0m : rd.GetDecimal(1);
                        obrot = rd.IsDBNull(2) ? 0m : rd.GetDecimal(2);
                        ostatnie = rd.IsDBNull(3) ? null : (DateTime?)rd.GetDateTime(3);
                    }
                }

                // Daty zamówień — do liczenia średniego odstępu
                const string sqlDni = @"
                    SELECT DISTINCT CAST(z.DataPrzyjazdu AS DATE) AS Data
                    FROM dbo.ZamowieniaMieso z
                    WHERE z.KlientId = @kid
                      AND z.DataPrzyjazdu >= DATEADD(MONTH, -12, GETDATE())
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
                    ORDER BY Data";
                await using (var cmd = new SqlCommand(sqlDni, cn) { CommandTimeout = 5 })
                {
                    cmd.Parameters.AddWithValue("@kid", klientId);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync()) daty.Add(rd.GetDateTime(0));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 zam agg] {ex.Message}"); }

            // Średni odstęp dni
            decimal sredniDni = 0;
            if (daty.Count > 1)
            {
                var roznice = new List<double>();
                for (int i = 1; i < daty.Count; i++) roznice.Add((daty[i] - daty[i - 1]).TotalDays);
                sredniDni = (decimal)roznice.Average();
            }

            // Marża — uproszczone: zakładamy że ostatnia ~12-15% wartości (bo nie mamy konkretnego kosztu per zamówienie)
            // W docelowym wersji: koszt = sumaKg × cena żywca z dnia × yield. Tu placeholder 12%.
            marza = obrot * 0.12m;
            decimal sredniaMarzaKg = sumaKg > 0 ? marza / sumaKg : 0m;

            return (obrot, marza, sredniaMarzaKg, liczba, sumaKg, ostatnie, sredniDni);
        }

        private async Task<decimal> LoadObrotPrevYearAsync(int klientId)
        {
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0)
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND z.DataPrzyjazdu >= DATEADD(MONTH, -24, GETDATE())
                      AND z.DataPrzyjazdu < DATEADD(MONTH, -12, GETDATE())
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')", cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? 0m : Convert.ToDecimal(r);
            }
            catch { return 0m; }
        }

        private async Task<(decimal limit, decimal doZap, decimal term, decimal przeterm, int maxDni, int faktur)> LoadFinanseSummaryAsync(int klientId)
        {
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT C.LimitAmount,
                           SUM(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 THEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) ELSE 0 END) AS DoZap,
                           SUM(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 AND GETDATE() <= ISNULL(PN.TerminPrawdziwy, DK.plattermin) THEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) ELSE 0 END) AS Term,
                           SUM(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 AND GETDATE() > ISNULL(PN.TerminPrawdziwy, DK.plattermin) THEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) ELSE 0 END) AS Przeterm,
                           MAX(CASE WHEN DK.walbrutto - ISNULL(PN.KwotaRozliczona, 0) > 0.01 AND GETDATE() > ISNULL(PN.TerminPrawdziwy, DK.plattermin) THEN DATEDIFF(day, ISNULL(PN.TerminPrawdziwy, DK.plattermin), GETDATE()) ELSE 0 END) AS MaxDni,
                           COUNT(DISTINCT DK.id) AS Faktur
                    FROM [HANDEL].[SSCommon].[STContractors] C
                    LEFT JOIN [HANDEL].[HM].[DK] DK ON DK.khid = C.id AND DK.anulowany = 0
                    LEFT JOIN (
                        SELECT dkid, SUM(ISNULL(kwotarozl, 0)) AS KwotaRozliczona, MAX(Termin) AS TerminPrawdziwy
                        FROM [HANDEL].[HM].[PN] GROUP BY dkid
                    ) PN ON PN.dkid = DK.id
                    WHERE C.Id = @kid
                    GROUP BY C.LimitAmount";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 8 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    return (
                        rd.IsDBNull(0) ? 0m : Convert.ToDecimal(rd.GetValue(0)),
                        rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1)),
                        rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2)),
                        rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3)),
                        rd.IsDBNull(4) ? 0 : Convert.ToInt32(rd.GetValue(4)),
                        rd.IsDBNull(5) ? 0 : Convert.ToInt32(rd.GetValue(5))
                    );
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 fin] {ex.Message}"); }
            return (0m, 0m, 0m, 0m, 0, 0);
        }

        private async Task<(int liczba, decimal wartosc)> LoadReklamacjeSummaryAsync(int klientId)
        {
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                // Sprawdź czy tabela istnieje
                var existsCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Reklamacje','ReklamacjeZamowien','PanelReklamacji')", cn);
                int existsCount = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());
                if (existsCount == 0) return (0, 0m);

                // Próba uniwersalnego query — zachowaj graceful fail
                const string sql = @"
                    SELECT COUNT(*) AS Liczba,
                           ISNULL(SUM(CASE WHEN ISNULL(Kwota, 0) > 0 THEN Kwota ELSE 0 END), 0) AS Wartosc
                    FROM dbo.Reklamacje
                    WHERE KlientId = @kid AND DataZgloszenia >= DATEADD(MONTH, -12, GETDATE())";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 3 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    return (rd.GetInt32(0), rd.IsDBNull(1) ? 0m : rd.GetDecimal(1));
                }
            }
            catch { /* tabela może nie istnieć lub mieć inny schemat — return 0 */ }
            return (0, 0m);
        }

        private (string level, string reason) CalculateChurnRisk(KlientKpi kpi)
        {
            // Jeśli brak danych
            if (kpi.LiczbaZamowien12M == 0) return ("UNKNOWN", "Brak zamówień w ostatnich 12 mies");
            if (!kpi.OstatnieZamowienie.HasValue) return ("UNKNOWN", "Brak daty ostatniego zamówienia");

            int dniOd = kpi.DniOdOstatniegoZamowienia;
            decimal sredniOdstep = kpi.SredniCzasMiedzyZamowieniami;

            if (sredniOdstep == 0) sredniOdstep = 30; // fallback
            double ratio = (double)dniOd / (double)sredniOdstep;

            // Sygnały dodatkowe
            decimal yoyChange = kpi.Obrot12MPrev > 0 ? (kpi.Obrot12M - kpi.Obrot12MPrev) / kpi.Obrot12MPrev : 0m;
            bool yoyMocnoSpadl = yoyChange < -0.3m;  // -30% YoY
            bool ratioPrzekroczony = ratio > 2.5;
            bool ratioMocnoPrzekroczony = ratio > 4.0;

            if (ratioMocnoPrzekroczony && yoyMocnoSpadl)
                return ("CRITICAL", $"Brak zamówienia {dniOd} dni (norma {sredniOdstep:N0}) + obrót YoY {yoyChange:P0}");
            if (ratioMocnoPrzekroczony)
                return ("WARNING", $"Brak zamówienia {dniOd} dni (norma {sredniOdstep:N0})");
            if (ratioPrzekroczony)
                return ("WATCH", $"Opóźnione zamówienie ({dniOd} dni vs norma {sredniOdstep:N0})");
            if (yoyMocnoSpadl)
                return ("WATCH", $"Obrót YoY {yoyChange:P0}");

            return ("OK", $"Aktywny ({dniOd} dni od ostatniego, norma {sredniOdstep:N0})");
        }

        // ── Historia zamówień ──
        public async Task<List<OrderHistoryItem>> GetOrderHistoryAsync(int klientId, int monthsBack = 12)
        {
            var lista = new List<OrderHistoryItem>();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                string sql = $@"
                    SELECT z.Id, z.DataZamowienia, z.DataPrzyjazdu, z.DataUboju, z.DataWydania,
                           ISNULL(z.Status,'') AS Status, ISNULL(z.IdUser,'') AS Handlowiec,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           COUNT(zt.KodTowaru) AS LiczbaPozycji,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc
                    FROM dbo.ZamowieniaMieso z
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                    GROUP BY z.Id, z.DataZamowienia, z.DataPrzyjazdu, z.DataUboju, z.DataWydania, z.Status, z.IdUser
                    ORDER BY z.DataPrzyjazdu DESC, z.Id DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new OrderHistoryItem
                    {
                        Id = rd.GetInt32(0),
                        DataZamowienia = rd.IsDBNull(2) ? rd.GetDateTime(1) : rd.GetDateTime(2),
                        DataUboju = rd.IsDBNull(3) ? null : (DateTime?)rd.GetDateTime(3),
                        DataWydania = rd.IsDBNull(4) ? null : (DateTime?)rd.GetDateTime(4),
                        Status = rd.GetString(5),
                        Handlowiec = rd.GetString(6),
                        SumaKg = rd.IsDBNull(7) ? 0m : rd.GetDecimal(7),
                        LiczbaPozycji = rd.IsDBNull(8) ? 0 : rd.GetInt32(8),
                        Wartosc = rd.IsDBNull(9) ? 0m : rd.GetDecimal(9)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 history] {ex.Message}"); }
            return lista;
        }

        // ── Statystyki miesięczne (do wykresu) ──
        public async Task<List<MonthlyStats>> GetMonthlyStatsAsync(int klientId, int monthsBack = 12)
        {
            var lista = new List<MonthlyStats>();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                string sql = $@"
                    SELECT YEAR(z.DataPrzyjazdu) AS Rok,
                           MONTH(z.DataPrzyjazdu) AS Miesiac,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc,
                           COUNT(DISTINCT z.Id) AS Liczba
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
                    GROUP BY YEAR(z.DataPrzyjazdu), MONTH(z.DataPrzyjazdu)
                    ORDER BY Rok, Miesiac";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new MonthlyStats
                    {
                        Year = rd.GetInt32(0),
                        Month = rd.GetInt32(1),
                        SumaKg = rd.IsDBNull(2) ? 0m : rd.GetDecimal(2),
                        Wartosc = rd.IsDBNull(3) ? 0m : rd.GetDecimal(3),
                        LiczbaZamowien = rd.IsDBNull(4) ? 0 : rd.GetInt32(4)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 monthly] {ex.Message}"); }
            return lista;
        }

        // ── Top kupowane towary ──
        public async Task<List<TopTowarItem>> GetTopTowaryAsync(int klientId, int monthsBack = 12, int topN = 10)
        {
            var lista = new List<TopTowarItem>();
            try
            {
                // Pobierz nazwy towarów z HANDEL
                var towarNames = new Dictionary<int, string>();
                try
                {
                    await using var cnH = new SqlConnection(ConnHandel);
                    await cnH.OpenAsync();
                    await using var cmdN = new SqlCommand(
                        "SELECT id, ISNULL(kod,'') FROM [HANDEL].[HM].[TW]", cnH) { CommandTimeout = 5 };
                    await using var rdN = await cmdN.ExecuteReaderAsync();
                    while (await rdN.ReadAsync()) towarNames[rdN.GetInt32(0)] = rdN.GetString(1);
                }
                catch { }

                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                string sql = $@"
                    SELECT TOP (@topN) zt.KodTowaru,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc,
                           COUNT(DISTINCT z.Id) AS Liczba
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
                    GROUP BY zt.KodTowaru
                    ORDER BY SumaKg DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                cmd.Parameters.AddWithValue("@topN", topN);
                await using var rd = await cmd.ExecuteReaderAsync();
                int poz = 1;
                while (await rd.ReadAsync())
                {
                    int kod = rd.GetInt32(0);
                    lista.Add(new TopTowarItem
                    {
                        Pozycja = poz++,
                        KodTowaru = kod,
                        Nazwa = towarNames.TryGetValue(kod, out var n) ? n : $"Towar #{kod}",
                        SumaKg = rd.IsDBNull(1) ? 0m : rd.GetDecimal(1),
                        Wartosc = rd.IsDBNull(2) ? 0m : rd.GetDecimal(2),
                        LiczbaZamowien = rd.IsDBNull(3) ? 0 : rd.GetInt32(3)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 top towar] {ex.Message}"); }
            return lista;
        }

        // ── WERYFIKACJA: porównanie zamówień (LibraNet) vs faktur (HANDEL) per towar ──
        public async Task<(WeryfikacjaSumarum summa, List<WeryfikacjaTowar> towary)> GetWeryfikacjaAsync(int klientId, int monthsBack = 12)
        {
            var byKod = new Dictionary<int, WeryfikacjaTowar>();
            int liczbaZam = 0, liczbaFak = 0;

            // 1) Zamówienia z LibraNet (NIE anulowane) — agregacja kg+wartość per KodTowaru
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                const string sqlZ = @"
                    SELECT COUNT(DISTINCT z.Id) AS LZam, zt.KodTowaru,
                           ISNULL(SUM(zt.Ilosc), 0) AS Kg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
                    GROUP BY zt.KodTowaru";
                await using var cmd = new SqlCommand(sqlZ, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    if (liczbaZam == 0) liczbaZam = rd.GetInt32(0);
                    int kod = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                    if (kod == 0) continue;
                    if (!byKod.TryGetValue(kod, out var wt))
                    {
                        wt = new WeryfikacjaTowar { KodTowaru = kod };
                        byKod[kod] = wt;
                    }
                    wt.ZamowioneKg = rd.IsDBNull(2) ? 0m : rd.GetDecimal(2);
                    wt.ZamowionaWartosc = rd.IsDBNull(3) ? 0m : rd.GetDecimal(3);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Wer zam] {ex.Message}"); }

            // 2) Faktury z HANDEL — agregacja per idtw (KodTowaru)
            // Liberalna lista typów (Sage różnie nazywa: FVS=Faktura VAT Sprzedaży, FVK=korekta, WZ=wydanie, PA=paragon)
            // Korekty są UJEMNE w DP.ilosc — zachowujemy znak żeby suma była prawidłowa (faktura − korekta = netto sprzedaży)
            // Wartość: DP.cena * DP.ilosc (bezpieczniejsze niż wartNetto, które bywa NULL)
            try
            {
                // Najpierw osobne query: globalna liczba faktur (bez group by)
                await using var cnF = new SqlConnection(ConnHandel);
                await cnF.OpenAsync();
                await using (var cmdC = new SqlCommand(@"
                    SELECT COUNT(DISTINCT DK.id)
                    FROM [HANDEL].[HM].[DK] DK
                    WHERE DK.khid = @kid
                      AND DK.anulowany = 0
                      AND (@months <= 0 OR DK.data >= DATEADD(MONTH, -@months, GETDATE()))
                      AND (DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                           OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o
                                      WHERE o.id = DK.iddokkoryg AND o.typ_dk IN ('FVS', 'FVR', 'FVZ')))", cnF) { CommandTimeout = 8 })
                {
                    cmdC.Parameters.AddWithValue("@kid", klientId);
                    cmdC.Parameters.AddWithValue("@months", monthsBack);
                    var rC = await cmdC.ExecuteScalarAsync();
                    liczbaFak = rC == null || rC == DBNull.Value ? 0 : Convert.ToInt32(rC);
                }

                const string sqlF = @"
                    SELECT DP.idtw,
                           ISNULL(SUM(CASE WHEN DP.ilosc < 0 THEN -DP.ilosc ELSE DP.ilosc END), 0) AS KgAbs,
                           ISNULL(SUM(DP.ilosc), 0) AS KgNet,
                           ISNULL(SUM(DP.cena * DP.ilosc), 0) AS Wartosc
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DP.super = DK.id
                    WHERE DK.khid = @kid
                      AND DK.anulowany = 0
                      AND (@months <= 0 OR DK.data >= DATEADD(MONTH, -@months, GETDATE()))
                      AND (DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                           OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o
                                      WHERE o.id = DK.iddokkoryg AND o.typ_dk IN ('FVS', 'FVR', 'FVZ')))
                      AND DP.idtw IS NOT NULL
                    GROUP BY DP.idtw";
                await using var cmd = new SqlCommand(sqlF, cnF) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                int liczbaWierszy = 0;
                while (await rd.ReadAsync())
                {
                    liczbaWierszy++;
                    int kod = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                    if (kod == 0) continue;
                    if (!byKod.TryGetValue(kod, out var wt))
                    {
                        wt = new WeryfikacjaTowar { KodTowaru = kod };
                        byKod[kod] = wt;
                    }
                    // KgNet/Wartosc = suma ZE ZNAKIEM (faktury + korekty). Korekty mają ujemne linie,
                    // więc suma = realna sprzedaż NETTO po korektach. NIE bierzemy Math.Abs.
                    decimal kgNet = rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2));
                    wt.ZafakturowaneKg = kgNet;
                    wt.ZafakturowanaWartosc = rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3));
                }
                System.Diagnostics.Debug.WriteLine($"[Wer fak] Klient={klientId} miesiace={monthsBack} → faktur={liczbaFak}, pozycji towarów={liczbaWierszy}");

                // Diagnostyka: gdy 0 wyników, sprawdź jakie typy dokumentów ma ten klient w HANDEL
                if (liczbaFak == 0 && liczbaWierszy == 0)
                {
                    try
                    {
                        await using var cmdD = new SqlCommand(@"
                            SELECT TOP 20 typ_dk, COUNT(*) AS Cnt
                            FROM [HANDEL].[HM].[DK]
                            WHERE khid = @kid AND ISNULL(anulowany,0)=0
                              AND (@months <= 0 OR data >= DATEADD(MONTH, -@months, GETDATE()))
                            GROUP BY typ_dk ORDER BY Cnt DESC", cnF) { CommandTimeout = 5 };
                        cmdD.Parameters.AddWithValue("@kid", klientId);
                        cmdD.Parameters.AddWithValue("@months", monthsBack);
                        await using var rdD = await cmdD.ExecuteReaderAsync();
                        var listaTypow = new List<string>();
                        while (await rdD.ReadAsync())
                            listaTypow.Add($"{(rdD.IsDBNull(0) ? "(null)" : rdD.GetString(0))}:{rdD.GetInt32(1)}");
                        if (listaTypow.Count > 0)
                            System.Diagnostics.Debug.WriteLine($"[Wer fak DIAG] Klient {klientId} ma w DK typy: {string.Join(", ", listaTypow)}");
                        else
                            System.Diagnostics.Debug.WriteLine($"[Wer fak DIAG] Klient {klientId} NIE MA ŻADNYCH dokumentów w HM.DK (sprawdź KlientId vs HANDEL.khid)");
                    }
                    catch (Exception exD) { System.Diagnostics.Debug.WriteLine($"[Wer fak DIAG] {exD.Message}"); }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Wer fak] {ex.Message}"); }

            // 3) Nazwy towarów z HM.TW
            if (byKod.Count > 0)
            {
                try
                {
                    string ids = string.Join(",", byKod.Keys);
                    await using var cnH = new SqlConnection(ConnHandel);
                    await cnH.OpenAsync();
                    await using var cmd = new SqlCommand(
                        $"SELECT id, ISNULL(kod,'') FROM [HANDEL].[HM].[TW] WHERE id IN ({ids})", cnH) { CommandTimeout = 5 };
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        int kod = rd.GetInt32(0);
                        if (byKod.TryGetValue(kod, out var wt)) wt.Nazwa = rd.GetString(1);
                    }
                }
                catch { }
            }

            var towary = byKod.Values
                .OrderByDescending(w => Math.Max(w.ZamowioneKg, w.ZafakturowaneKg))
                .ToList();

            var summa = new WeryfikacjaSumarum
            {
                ZamowioneKg = towary.Sum(t => t.ZamowioneKg),
                ZafakturowaneKg = towary.Sum(t => t.ZafakturowaneKg),
                ZamowionaWartosc = towary.Sum(t => t.ZamowionaWartosc),
                ZafakturowanaWartosc = towary.Sum(t => t.ZafakturowanaWartosc),
                LiczbaZamowien = liczbaZam,
                LiczbaFaktur = liczbaFak,
                LiczbaTowarow = towary.Count,
                LiczbaTowarowUcietych = towary.Count(t => t.Status == "✂ Ucięte"),
                LiczbaTowarowDodanych = towary.Count(t => t.Status == "➕ Więcej"),
                LiczbaTowarowBrakFaktury = towary.Count(t => t.Status == "⚠ Brak faktury")
            };

            return (summa, towary);
        }

        // ── Anulowane zamówienia ──
        public async Task<List<AnulowaneZam>> GetAnulowaneZamowieniaAsync(int klientId, int monthsBack = 12)
        {
            var lista = new List<AnulowaneZam>();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                // AnulowanePrzez i DataAnulowania mogą istnieć (sprawdzaliśmy w schema fast check)
                const string sql = @"
                    SELECT z.Id, z.DataZamowienia, z.DataPrzyjazdu,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc,
                           ISNULL(z.Uwagi, '') AS Uwagi,
                           ISNULL(z.IdUser, '') AS Handlowiec,
                           CASE WHEN COL_LENGTH('dbo.ZamowieniaMieso','AnulowanePrzez') IS NOT NULL THEN ISNULL(z.AnulowanePrzez,'') ELSE '' END AS AnulPrzez,
                           CASE WHEN COL_LENGTH('dbo.ZamowieniaMieso','DataAnulowania') IS NOT NULL THEN z.DataAnulowania ELSE NULL END AS DataAnul
                    FROM dbo.ZamowieniaMieso z
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND ISNULL(z.Status,'') IN ('Anulowane','Anulowano')
                    GROUP BY z.Id, z.DataZamowienia, z.DataPrzyjazdu, z.Uwagi, z.IdUser, z.AnulowanePrzez, z.DataAnulowania
                    ORDER BY z.DataPrzyjazdu DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 8 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new AnulowaneZam
                    {
                        Id = rd.GetInt32(0),
                        DataZamowienia = rd.GetDateTime(1),
                        DataPrzyjazdu = rd.IsDBNull(2) ? null : (DateTime?)rd.GetDateTime(2),
                        SumaKg = rd.IsDBNull(3) ? 0m : rd.GetDecimal(3),
                        Wartosc = rd.IsDBNull(4) ? 0m : rd.GetDecimal(4),
                        Powod = rd.GetString(5),
                        Handlowiec = rd.GetString(6),
                        AnulowanePrzez = rd.IsDBNull(7) ? "" : rd.GetString(7),
                        DataAnulowania = rd.IsDBNull(8) ? null : (DateTime?)rd.GetDateTime(8)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Anulowane] {ex.Message}"); }
            return lista;
        }

        // ── Pełne faktury z HANDEL (rozszerzone — typ, brutto/netto, pozycje) ──
        public async Task<List<FakturaDetail>> GetFakturyDetailAsync(int klientId, int monthsBack = 12)
        {
            var lista = new List<FakturaDetail>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT DK.kod, DK.typ_dk, DK.data, DK.plattermin,
                           DK.walbrutto, ISNULL(DK.walnetto, DK.walbrutto) AS Netto,
                           ISNULL(PN.KwotaRozliczona, 0) AS Zaplacone,
                           ISNULL(PN.TerminPrawdziwy, DK.plattermin) AS TerminPrawdziwy,
                           ISNULL((SELECT SUM(ABS(ilosc)) FROM [HANDEL].[HM].[DP] WHERE super = DK.id), 0) AS SumaKg,
                           ISNULL((SELECT COUNT(*) FROM [HANDEL].[HM].[DP] WHERE super = DK.id), 0) AS Pozycji
                    FROM [HANDEL].[HM].[DK] DK
                    LEFT JOIN (
                        SELECT dkid, SUM(ISNULL(kwotarozl, 0)) AS KwotaRozliczona, MAX(Termin) AS TerminPrawdziwy
                        FROM [HANDEL].[HM].[PN] GROUP BY dkid
                    ) PN ON PN.dkid = DK.id
                    WHERE DK.khid = @kid
                      AND DK.anulowany = 0
                      AND (@months <= 0 OR DK.data >= DATEADD(MONTH, -@months, GETDATE()))
                      AND (DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                           OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o
                                      WHERE o.id = DK.iddokkoryg AND o.typ_dk IN ('FVS', 'FVR', 'FVZ')))
                    ORDER BY DK.data DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new FakturaDetail
                    {
                        NumerFaktury = rd.IsDBNull(0) ? "" : rd.GetString(0),
                        TypDk = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        DataWystawienia = rd.GetDateTime(2),
                        TerminPlatnosci = rd.IsDBNull(7) ? null : (DateTime?)rd.GetDateTime(7),
                        Brutto = rd.IsDBNull(4) ? 0m : Convert.ToDecimal(rd.GetValue(4)),
                        Netto = rd.IsDBNull(5) ? 0m : Convert.ToDecimal(rd.GetValue(5)),
                        Zaplacone = rd.IsDBNull(6) ? 0m : Convert.ToDecimal(rd.GetValue(6)),
                        SumaKg = rd.IsDBNull(8) ? 0m : Convert.ToDecimal(rd.GetValue(8)),
                        LiczbaPozycji = rd.IsDBNull(9) ? 0 : Convert.ToInt32(rd.GetValue(9))
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Fak detail] {ex.Message}"); }
            return lista;
        }

        // ── Faktury klienta ──
        public async Task<List<KlientFaktura>> GetFakturyAsync(int klientId, int monthsBack = 12)
        {
            var lista = new List<KlientFaktura>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT DK.kod, DK.data, DK.plattermin, DK.walbrutto,
                           ISNULL(PN.KwotaRozliczona, 0) AS Zaplacone,
                           ISNULL(PN.TerminPrawdziwy, DK.plattermin) AS TerminPrawdziwy
                    FROM [HANDEL].[HM].[DK] DK
                    LEFT JOIN (
                        SELECT dkid, SUM(ISNULL(kwotarozl, 0)) AS KwotaRozliczona, MAX(Termin) AS TerminPrawdziwy
                        FROM [HANDEL].[HM].[PN] GROUP BY dkid
                    ) PN ON PN.dkid = DK.id
                    WHERE DK.khid = @kid
                      AND DK.anulowany = 0
                      AND (@months <= 0 OR DK.data >= DATEADD(MONTH, -@months, GETDATE()))
                      AND (DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                           OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o
                                      WHERE o.id = DK.iddokkoryg AND o.typ_dk IN ('FVS', 'FVR', 'FVZ')))
                    ORDER BY DK.data DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new KlientFaktura
                    {
                        NumerFaktury = rd.IsDBNull(0) ? "" : rd.GetString(0),
                        DataWystawienia = rd.GetDateTime(1),
                        TerminPlatnosci = rd.IsDBNull(5) ? null : (DateTime?)rd.GetDateTime(5),
                        Kwota = rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3)),
                        Zaplacone = rd.IsDBNull(4) ? 0m : Convert.ToDecimal(rd.GetValue(4))
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 faktury] {ex.Message}"); }
            return lista;
        }
    }
}
