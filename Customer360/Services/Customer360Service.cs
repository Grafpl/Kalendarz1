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
                            AND CAST(DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)   -- aktywny = zrealizowany, nie zaplanowany
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

        // ── KPI klienta — agregat za ostatnie 12 mies (canonical) + opcjonalnie inny okres dla UI ──
        // monthsBack: 12 (default) = tylko 12M, 0/3/6 = dodatkowo policz wartosci dla wybranego okresu.
        // 12M sluzy scoringowi/churnowi/PDF/porownaniu — stabilna kotwica. ObrotOkres etc. tylko dla hero tile.
        public async Task<KlientKpi> GetKpiAsync(int klientId, bool forceScoreRefresh = false, int monthsBack = 12)
        {
            var kpi = new KlientKpi { OkresMiesiacy = monthsBack };
            try
            {
                // Paralelnie: zamówienia 12M (LibraNet) + finanse (HANDEL) + reklamacje (LibraNet)
                var taskZam12 = LoadZamowieniaSummaryAsync(klientId, 12);
                var taskFin = LoadFinanseSummaryAsync(klientId);
                var taskRek = LoadReklamacjeSummaryAsync(klientId);
                await Task.WhenAll(taskZam12, taskFin, taskRek);

                var (obrot, liczba, sumaKg, ostatnie, sredniDni) = await taskZam12;
                var (limit, doZap, term, przeterm, maxDni, faktur) = await taskFin;
                var (liczbaRek, wartoscRek) = await taskRek;

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

                // Obrót 12M + YoY + liczba faktur 12M = Z FAKTUR (zamówienia×cena mają pozycje dopiero od ~10/2025)
                var (obrotFak12, obrotFakPrev, faktur12) = await GetObrotFakturyAsync(klientId, 12);
                kpi.Obrot12M = obrotFak12 > 0 ? obrotFak12 : obrot;       // fallback na zamówienia gdy brak faktur
                kpi.Obrot12MPrev = obrotFakPrev > 0 ? obrotFakPrev : await LoadObrotPrevYearAsync(klientId, 12);
                kpi.LiczbaFaktur12M = faktur12;

                // Okres wybrany w UI — dolicz wartosci dla tile hero (jesli ten sam okres co 12M, reuse).
                if (monthsBack == 12)
                {
                    kpi.ObrotOkres = kpi.Obrot12M;
                    kpi.ObrotOkresPrev = kpi.Obrot12MPrev;
                    kpi.LiczbaFakturOkres = kpi.LiczbaFaktur12M;
                    kpi.LiczbaZamowienOkres = kpi.LiczbaZamowien12M;
                    kpi.SumaKgOkres = kpi.SumaKg12M;
                }
                else
                {
                    var taskZamN = LoadZamowieniaSummaryAsync(klientId, monthsBack);
                    var taskFakN = GetObrotFakturyAsync(klientId, monthsBack);
                    await Task.WhenAll(taskZamN, taskFakN);
                    var (obrotN, liczbaN, sumaKgN, _, _) = await taskZamN;
                    var (obrotFakN, obrotFakPrevN, fakturN) = await taskFakN;
                    kpi.ObrotOkres = obrotFakN > 0 ? obrotFakN : obrotN;
                    kpi.ObrotOkresPrev = monthsBack > 0
                        ? (obrotFakPrevN > 0 ? obrotFakPrevN : await LoadObrotPrevYearAsync(klientId, monthsBack))
                        : 0m;
                    kpi.LiczbaFakturOkres = fakturN;
                    kpi.LiczbaZamowienOkres = liczbaN;
                    kpi.SumaKgOkres = sumaKgN;
                }

                // Churn risk — z 12M canonical (stabilny niezaleznie od UI)
                var (level, reason) = Customer360KpiCalculator.ObliczChurn(kpi);
                kpi.ChurnRiskLevel = level;
                kpi.ChurnRiskReason = reason;

                // Scoring 4-składnikowy — z cache (pamięć→DB, TTL 7 dni); liczenie tylko na cache-miss / force
                kpi.Score = await Customer360ScoringService.PobierzLubObliczAsync(klientId, forceScoreRefresh, async () =>
                {
                    var cfg = await Customer360ScoringConfigStore.WczytajAsync();
                    var pierwszaFak = await PobierzPierwszaFakturaAsync(klientId);
                    return Customer360Scorer.BudujScore(kpi, pierwszaFak, cfg);
                });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 KPI] {ex.Message}"); }
            return kpi;
        }

        /// <summary>Data pierwszej faktury sprzedaży (do długości relacji w scoringu).</summary>
        private async Task<DateTime?> PobierzPierwszaFakturaAsync(int klientId)
        {
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    "SELECT MIN(data) FROM [HANDEL].[HM].[DK] WHERE khid=@kid AND anulowany=0 AND typ_dk IN ('FVS','FVR','FVZ')", cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 pierwszaFaktura] {ex.Message}"); return null; }
        }

        /// <summary>Scoring klienta (4 składniki) — liczy KPI i zwraca jego Score.</summary>
        public async Task<Customer360Score?> GetScoringAsync(int klientId)
        {
            var kpi = await GetKpiAsync(klientId);
            return kpi.Score;
        }

        // monthsBack: 0 = cala historia, >0 = N miesiecy wstecz
        private async Task<(decimal obrot, int liczba, decimal sumaKg, DateTime? ostatnie, decimal sredniDni)> LoadZamowieniaSummaryAsync(int klientId, int monthsBack)
        {
            decimal obrot = 0, sumaKg = 0;
            int liczba = 0;
            DateTime? ostatnie = null;
            var daty = new List<DateTime>();

            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();

                // Summary: liczba zamówień, suma kg, suma wartości — stan na DZIS (bez przyszlosci)
                const string sqlAgg = @"
                    SELECT COUNT(DISTINCT z.Id) AS Liczba,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc,
                           MAX(z.DataPrzyjazdu) AS Ostatnie
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')";
                await using (var cmd = new SqlCommand(sqlAgg, cn) { CommandTimeout = 8 })
                {
                    cmd.Parameters.AddWithValue("@kid", klientId);
                    cmd.Parameters.AddWithValue("@months", monthsBack);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        liczba = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                        sumaKg = rd.IsDBNull(1) ? 0m : rd.GetDecimal(1);
                        obrot = rd.IsDBNull(2) ? 0m : rd.GetDecimal(2);
                        ostatnie = rd.IsDBNull(3) ? null : (DateTime?)rd.GetDateTime(3);
                    }
                }

                // Daty zamówień — do liczenia średniego odstępu (bez przyszlosci)
                const string sqlDni = @"
                    SELECT DISTINCT CAST(z.DataPrzyjazdu AS DATE) AS Data
                    FROM dbo.ZamowieniaMieso z
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
                    ORDER BY Data";
                await using (var cmd = new SqlCommand(sqlDni, cn) { CommandTimeout = 5 })
                {
                    cmd.Parameters.AddWithValue("@kid", klientId);
                    cmd.Parameters.AddWithValue("@months", monthsBack);
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

            // Marża usunięta (była zmyślona: obrot*0.12 — brak kosztu per zamówienie w danych).
            return (obrot, liczba, sumaKg, ostatnie, sredniDni);
        }

        // Poprzedni okres tej samej dlugosci: [-2N..-N) miesiecy. Brak dla monthsBack<=0.
        private async Task<decimal> LoadObrotPrevYearAsync(int klientId, int monthsBack)
        {
            if (monthsBack <= 0) return 0m;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0)
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND z.DataPrzyjazdu >= DATEADD(MONTH, -(@months*2), GETDATE())
                      AND z.DataPrzyjazdu <  DATEADD(MONTH, -@months,    GETDATE())
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')", cn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
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

        // Churn przeniesiony do Customer360KpiCalculator.ObliczChurn (Faza 7).

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
                           ISNULL(z.Status,'') AS Status, ISNULL(CAST(z.IdUser AS NVARCHAR(20)),'') AS Handlowiec,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           COUNT(zt.KodTowaru) AS LiczbaPozycji,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc
                    FROM dbo.ZamowieniaMieso z
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)   -- historia = co bylo, nie co zaplanowane
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')   -- bez anulowanych (są w zakładce Anulowane)
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

        // ── Porównanie miesięczne: zamówione kg (LibraNet) vs zafakturowane kg (HANDEL) ──
        public async Task<List<PorownanieMiesiac>> GetPorownanieMiesiaceAsync(int klientId, int monthsBack = 12)
        {
            var mapa = new Dictionary<(int, int), PorownanieMiesiac>();
            PorownanieMiesiac Get(int y, int m)
            {
                if (!mapa.TryGetValue((y, m), out var pm)) { pm = new PorownanieMiesiac { Year = y, Month = m }; mapa[(y, m)] = pm; }
                return pm;
            }

            // 1) Zamówione kg per miesiąc (data zamówienia = DataPrzyjazdu)
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT YEAR(z.DataPrzyjazdu) AS Rok, MONTH(z.DataPrzyjazdu) AS Mies, ISNULL(SUM(zt.Ilosc),0) AS Kg
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)   -- bez przyszlosci: zam. ktore jeszcze nie wyjechaly
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
                    GROUP BY YEAR(z.DataPrzyjazdu), MONTH(z.DataPrzyjazdu)";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    Get(rd.GetInt32(0), rd.GetInt32(1)).ZamowioneKg = rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 porownanie zam] {ex.Message}"); }

            // 2) Zafakturowane kg per miesiąc (data faktury), sprzedaż+korekty
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT YEAR(DK.data) AS Rok, MONTH(DK.data) AS Mies,
                           ISNULL(SUM(CASE WHEN DP.ilosc < 0 THEN -DP.ilosc ELSE DP.ilosc END),0) AS Kg
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DP.super = DK.id AND DP.idtw IS NOT NULL
                    WHERE DK.khid = @kid AND DK.anulowany = 0
                      AND (@months <= 0 OR DK.data >= DATEADD(MONTH, -@months, GETDATE()))
                      AND DK.typ_dk IN ('FVS','FVR','FVZ')   -- weryfikacja: BEZ korekt
                    GROUP BY YEAR(DK.data), MONTH(DK.data)";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    Get(rd.GetInt32(0), rd.GetInt32(1)).ZafakturowaneKg = rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 porownanie fak] {ex.Message}"); }

            return mapa.Values.OrderBy(p => p.Year).ThenBy(p => p.Month).ToList();
        }

        // ── Obrót z FAKTUR: okres + poprzedni okres (YoY) + liczba faktur ──
        // monthsBack: 0 = cala historia (oPrev=0, f12 = wszystkie); >0 = [-N..0) + [-2N..-N) na YoY.
        public async Task<(decimal obrotOkres, decimal obrotPrev, int fakturOkres)> GetObrotFakturyAsync(int klientId, int monthsBack = 12)
        {
            decimal oOkres = 0, oPrev = 0; int fOkres = 0;
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT
                      ISNULL(SUM(CASE WHEN (@months <= 0 OR DK.data >= DATEADD(MONTH,-@months,GETDATE())) THEN DK.walbrutto ELSE 0 END),0) AS OOkres,
                      ISNULL(SUM(CASE WHEN @months > 0 AND DK.data >= DATEADD(MONTH,-(@months*2),GETDATE()) AND DK.data < DATEADD(MONTH,-@months,GETDATE()) THEN DK.walbrutto ELSE 0 END),0) AS OPrev,
                      SUM(CASE WHEN (@months <= 0 OR DK.data >= DATEADD(MONTH,-@months,GETDATE())) THEN 1 ELSE 0 END) AS FOkres
                    FROM [HANDEL].[HM].[DK] DK
                    WHERE DK.khid = @kid AND DK.anulowany = 0
                      AND (@months <= 0 OR DK.data >= DATEADD(MONTH,-(@months*2),GETDATE()))
                      AND (DK.typ_dk IN ('FVS','FVR','FVZ')
                           OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o WHERE o.id = DK.iddokkoryg AND o.typ_dk IN ('FVS','FVR','FVZ')))";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    oOkres = rd.IsDBNull(0) ? 0m : Convert.ToDecimal(rd.GetValue(0));
                    oPrev = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                    fOkres = rd.IsDBNull(2) ? 0 : Convert.ToInt32(rd.GetValue(2));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 obrot faktury] {ex.Message}"); }
            return (oOkres, oPrev, fOkres);
        }

        // ── Obrót miesięczny Z FAKTUR (HANDEL) — realny obrót, PEŁNA historia ──
        // Zamówienia (ZamowieniaMiesoTowar) mają pozycje dopiero od ~10/2025, więc wykres oparty
        // na zamówieniach urywał się. Faktury sięgają pełnej historii klienta.
        public async Task<List<MonthlyStats>> GetMonthlyObrotFakturyAsync(int klientId, int monthsBack = 0)
        {
            var lista = new List<MonthlyStats>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                // Proste GROUP BY na DK — bez kosztownych podzapytań (wykres używa tylko Wartosc + Liczba).
                string sql = @"
                    SELECT YEAR(DK.data) AS Rok, MONTH(DK.data) AS Miesiac,
                           ISNULL(SUM(DK.walbrutto), 0) AS Wartosc,
                           COUNT(*) AS Liczba
                    FROM [HANDEL].[HM].[DK] DK
                    WHERE DK.khid = @kid
                      AND DK.anulowany = 0
                      AND (@months <= 0 OR DK.data >= DATEADD(MONTH, -@months, GETDATE()))
                      AND (DK.typ_dk IN ('FVS','FVR','FVZ')
                           OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o WHERE o.id = DK.iddokkoryg AND o.typ_dk IN ('FVS','FVR','FVZ')))
                    GROUP BY YEAR(DK.data), MONTH(DK.data)
                    ORDER BY Rok, Miesiac";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new MonthlyStats
                    {
                        Year = rd.GetInt32(0),
                        Month = rd.GetInt32(1),
                        Wartosc = rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2)),
                        LiczbaZamowien = rd.IsDBNull(3) ? 0 : Convert.ToInt32(rd.GetValue(3))
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[C360 monthly faktury] {ex.Message}"); }
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
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)   -- top towary z historii, nie z planu
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

                // Poprawna liczba zamówień (osobno, bo poniższy GROUP BY KodTowaru liczy per-towar)
                await using (var cmdLZ = new SqlCommand(@"
                    SELECT COUNT(DISTINCT z.Id) FROM dbo.ZamowieniaMieso z
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)   -- bez przyszlosci: zam. ktore jeszcze nie wyjechaly nie mogly byc zafakturowane
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')", cn) { CommandTimeout = 8 })
                {
                    cmdLZ.Parameters.AddWithValue("@kid", klientId);
                    cmdLZ.Parameters.AddWithValue("@months", monthsBack);
                    var r = await cmdLZ.ExecuteScalarAsync();
                    liczbaZam = r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
                }

                const string sqlZ = @"
                    SELECT zt.KodTowaru,
                           ISNULL(SUM(zt.Ilosc), 0) AS Kg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc
                    FROM dbo.ZamowieniaMieso z
                    INNER JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE)   -- bez przyszlosci: zam. ktore jeszcze nie wyjechaly nie mogly byc zafakturowane
                      AND ISNULL(z.Status,'') NOT IN ('Anulowane','Anulowano')
                    GROUP BY zt.KodTowaru";
                await using var cmd = new SqlCommand(sqlZ, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                cmd.Parameters.AddWithValue("@months", monthsBack);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int kod = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                    if (kod == 0) continue;
                    if (!byKod.TryGetValue(kod, out var wt))
                    {
                        wt = new WeryfikacjaTowar { KodTowaru = kod };
                        byKod[kod] = wt;
                    }
                    wt.ZamowioneKg = rd.IsDBNull(1) ? 0m : rd.GetDecimal(1);
                    wt.ZamowionaWartosc = rd.IsDBNull(2) ? 0m : rd.GetDecimal(2);
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
                      AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')   -- weryfikacja: BEZ korekt", cnF) { CommandTimeout = 8 })
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
                      AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')   -- weryfikacja: BEZ korekt
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
                // PrzyczynaAnulowania (nvarchar 200) i AnulowanePrzez sa OSOBNYMI polami od Uwagi (uwagi do zamowienia).
                // LEFT JOIN operators dla nazw zamiast ID — spojnie z innymi modulami (ZSRIR, Listapartii, …).
                const string sql = @"
                    SELECT z.Id, z.DataZamowienia, z.DataPrzyjazdu,
                           ISNULL(SUM(zt.Ilosc), 0) AS SumaKg,
                           ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))), 0) AS Wartosc,
                           CASE WHEN COL_LENGTH('dbo.ZamowieniaMieso','PrzyczynaAnulowania') IS NOT NULL
                                THEN ISNULL(z.PrzyczynaAnulowania,'') ELSE '' END AS Przyczyna,
                           ISNULL(z.Uwagi, '') AS Uwagi,
                           ISNULL(opHand.Name, CAST(z.IdUser AS NVARCHAR(20))) AS HandlowiecNazwa,
                           CASE WHEN COL_LENGTH('dbo.ZamowieniaMieso','AnulowanePrzez') IS NOT NULL
                                THEN ISNULL(opAnul.Name, ISNULL(z.AnulowanePrzez,'')) ELSE '' END AS AnulPrzezNazwa,
                           CASE WHEN COL_LENGTH('dbo.ZamowieniaMieso','DataAnulowania') IS NOT NULL
                                THEN z.DataAnulowania ELSE NULL END AS DataAnul
                    FROM dbo.ZamowieniaMieso z
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    LEFT JOIN dbo.operators opHand ON opHand.ID = CAST(z.IdUser AS NVARCHAR(50))
                    LEFT JOIN dbo.operators opAnul ON opAnul.ID = z.AnulowanePrzez
                    WHERE z.KlientId = @kid
                      AND (@months <= 0 OR z.DataPrzyjazdu >= DATEADD(MONTH, -@months, GETDATE()))
                      AND ISNULL(z.Status,'') IN ('Anulowane','Anulowano')
                    GROUP BY z.Id, z.DataZamowienia, z.DataPrzyjazdu, z.PrzyczynaAnulowania, z.Uwagi,
                             z.IdUser, opHand.Name, z.AnulowanePrzez, opAnul.Name, z.DataAnulowania
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
                        Przyczyna = rd.GetString(5),
                        Uwagi = rd.GetString(6),
                        Handlowiec = rd.GetString(7),
                        AnulowanePrzez = rd.IsDBNull(8) ? "" : rd.GetString(8),
                        DataAnulowania = rd.IsDBNull(9) ? null : (DateTime?)rd.GetDateTime(9)
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

        // ════════════════════════════════════════════════════════════════════
        // DIAGNOSTYKA — pełny zrzut stanu dla danego klienta (do debugowania)
        // ════════════════════════════════════════════════════════════════════
        public async Task<string> BuildDiagnosticReportAsync(int klientId)
        {
            var sb = new System.Text.StringBuilder();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            void H(string t) { sb.AppendLine(); sb.AppendLine("══════════════════════════════════════════════════════════"); sb.AppendLine(t); sb.AppendLine("══════════════════════════════════════════════════════════"); }
            void L(string t = "") => sb.AppendLine(t);

            // Przechwyć logi Debug.WriteLine które emitują metody serwisu ([Wer fak] itp.)
            var capture = new CaptureTraceListener();
            System.Diagnostics.Trace.Listeners.Add(capture);

            // Bufory na auto-werdykt
            string? dllBuilt = null;
            int rawFakAll = -1, rawFakZ12M = -1, rawZamAll = -1;
            int methodFakCount = -1, methodZamCount = -1;
            int minFakRok = 0, maxFakRok = 0;
            bool khidIstnieje = false;
            var bledy = new List<string>();

            // Nagłówek pliku
            L("╔══════════════════════════════════════════════════════════╗");
            L("║   CUSTOMER 360 — RAPORT DIAGNOSTYCZNY                     ║");
            L($"║   Klient khid={klientId,-10}  {DateTime.Now:yyyy-MM-dd HH:mm:ss}            ║");
            L("╚══════════════════════════════════════════════════════════╝");
            L("(WERDYKT na dole — sekcja 9)");

            // ── 0. ŚRODOWISKO / WERSJA BINARKI ──
            H("0. ŚRODOWISKO / WERSJA BINARKI (wykrywa stary kod w pamięci)");
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var loc = asm.Location;
                L($"Assembly:        {asm.GetName().Name} v{asm.GetName().Version}");
                L($"Proces PID:      {System.Diagnostics.Process.GetCurrentProcess().Id}");
                L($"Proces start:    {System.Diagnostics.Process.GetCurrentProcess().StartTime:yyyy-MM-dd HH:mm:ss}  <-- kiedy uruchomiono aplikację");
                L($"Lokalizacja DLL: {loc}");
                if (!string.IsNullOrEmpty(loc) && System.IO.File.Exists(loc))
                {
                    var bt = System.IO.File.GetLastWriteTime(loc);
                    dllBuilt = bt.ToString("yyyy-MM-dd HH:mm:ss");
                    L($"DLL zbudowany:   {dllBuilt}  <-- jeśli STARSZE niż proces start, OK; jeśli świeży kod nie zadziałał = stary proces");
                    try
                    {
                        var bytes = System.IO.File.ReadAllBytes(loc);
                        int peOffset = BitConverter.ToInt32(bytes, 60);
                        int secs = BitConverter.ToInt32(bytes, peOffset + 8);
                        var linkTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(secs).ToLocalTime();
                        if (linkTime.Year >= 2000 && linkTime.Year < 2100) L($"PE link time:    {linkTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    catch { }
                }
                L($"Czas teraz:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                L($"GETDATE()-12M =  {DateTime.Now.AddMonths(-12):yyyy-MM-dd}  (stary filtr -12M odcinał starsze niż to)");
                L($"Conn LibraNet:   Server=192.168.0.109;Database=LibraNet");
                L($"Conn HANDEL:     Server=192.168.0.112;Database=Handel");
            }
            catch (Exception ex) { L("BŁĄD środowiska: " + ex); bledy.Add("env: " + ex.Message); }

            // ── 1. TEST POŁĄCZEŃ (latencja) ──
            H("1. TEST POŁĄCZEŃ");
            L("LibraNet  : " + await DiagPing(ConnLibra));
            L("HANDEL    : " + await DiagPing(ConnHandel));

            // ── 2. MAPOWANIE khid → HANDEL.STContractors ──
            H("2. MAPOWANIE klienta — czy khid istnieje w HANDEL?");
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(
                    $"SELECT Id, ISNULL(Shortcut,''), ISNULL(Name,''), ISNULL(NIP,'') FROM [HANDEL].[SSCommon].[STContractors] WHERE Id = {klientId}", cn) { CommandTimeout = 10 };
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    khidIstnieje = true;
                    L($"✅ ZNALEZIONO: Id={rd.GetInt32(0)} | {rd.GetString(1)} | {rd.GetString(2)} | NIP {rd.GetString(3)}");
                }
                else L($"❌ khid {klientId} NIE ISTNIEJE w STContractors — to nie jest poprawny khid HANDEL!");
            }
            catch (Exception ex) { L("❌ BŁĄD: " + ex.Message); bledy.Add("mapowanie: " + ex.Message); }

            // ── 3. ZAMÓWIENIA (LibraNet) — surowe liczby per rok + porównanie 12M ──
            H("3. ZAMÓWIENIA — LibraNet dbo.ZamowieniaMieso per rok");
            await DiagQuery(sb, ConnLibra, $@"
                SELECT YEAR(DataPrzyjazdu) AS Rok, COUNT(*) AS Zamowien,
                       MIN(DataPrzyjazdu) AS Najwcz, MAX(DataPrzyjazdu) AS Najpoz
                FROM dbo.ZamowieniaMieso WHERE KlientId = {klientId}
                GROUP BY YEAR(DataPrzyjazdu) ORDER BY Rok");
            rawZamAll = await DiagScalar(ConnLibra, $"SELECT COUNT(*) FROM dbo.ZamowieniaMieso WHERE KlientId={klientId}");

            // ── 4. FAKTURY (HANDEL) — per rok (sprzedaż+korekty) + porównanie 12M vs ALL ──
            H("4. FAKTURY — HANDEL HM.DK per rok (FVS/FVR/FVZ + korekty via iddokkoryg)");
            await DiagQuery(sb, ConnHandel, $@"
                SELECT YEAR(DK.data) AS Rok, COUNT(*) AS Faktur,
                       MIN(DK.data) AS Najwcz, MAX(DK.data) AS Najpoz
                FROM [HANDEL].[HM].[DK] DK
                WHERE DK.khid = {klientId} AND DK.anulowany = 0
                  AND (DK.typ_dk IN ('FVS','FVR','FVZ')
                       OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o WHERE o.id = DK.iddokkoryg AND o.typ_dk IN ('FVS','FVR','FVZ')))
                GROUP BY YEAR(DK.data) ORDER BY Rok");
            string fakFilter = $@"DK.khid={klientId} AND DK.anulowany=0
                AND (DK.typ_dk IN ('FVS','FVR','FVZ') OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DK] o WHERE o.id=DK.iddokkoryg AND o.typ_dk IN ('FVS','FVR','FVZ')))";
            rawFakAll = await DiagScalar(ConnHandel, $"SELECT COUNT(*) FROM [HANDEL].[HM].[DK] DK WHERE {fakFilter}");
            rawFakZ12M = await DiagScalar(ConnHandel, $"SELECT COUNT(*) FROM [HANDEL].[HM].[DK] DK WHERE {fakFilter} AND DK.data >= DATEADD(MONTH,-12,GETDATE())");
            L("");
            L($"  ➤ PORÓWNANIE FILTRA:  WSZYSTKO={rawFakAll} faktur   |   ostatnie 12M={rawFakZ12M} faktur");
            L($"    (różnica {rawFakAll - rawFakZ12M} faktur to te, które stary filtr -12M UKRYWAŁ)");

            // ── 5. typ_dk klienta (bez filtra) ──
            H("5. FAKTURY — wszystkie typ_dk klienta (diagnoza nazw typów)");
            await DiagQuery(sb, ConnHandel, $@"
                SELECT typ_dk, COUNT(*) AS Cnt, MIN(data) AS Najwcz, MAX(data) AS Najpoz
                FROM [HANDEL].[HM].[DK] WHERE khid={klientId} AND anulowany=0
                GROUP BY typ_dk ORDER BY Cnt DESC");

            // ── 6. REALNE METODY SERWISU (monthsBack=0) ──
            H("6. REALNE METODY SERWISU (monthsBack=0 → cała historia)");
            try
            {
                var hist = await GetOrderHistoryAsync(klientId, 0);
                methodZamCount = hist.Count;
                L($"GetOrderHistoryAsync(0):   {hist.Count} zamówień" +
                  (hist.Count > 0 ? $" | {hist.Min(h => h.DataZamowienia):dd.MM.yyyy} – {hist.Max(h => h.DataZamowienia):dd.MM.yyyy}" : ""));
            }
            catch (Exception ex) { L("GetOrderHistoryAsync BŁĄD: " + ex.Message); bledy.Add("history: " + ex.Message); }
            try
            {
                var fak = await GetFakturyDetailAsync(klientId, 0);
                methodFakCount = fak.Count;
                L($"GetFakturyDetailAsync(0):  {fak.Count} faktur" +
                  (fak.Count > 0 ? $" | {fak.Min(f => f.DataWystawienia):dd.MM.yyyy} – {fak.Max(f => f.DataWystawienia):dd.MM.yyyy}" : ""));
                if (fak.Count > 0)
                {
                    minFakRok = fak.Min(f => f.DataWystawienia.Year);
                    maxFakRok = fak.Max(f => f.DataWystawienia.Year);
                    foreach (var g in fak.GroupBy(f => f.DataWystawienia.Year).OrderBy(g => g.Key))
                        L($"     rok {g.Key}: {g.Count()} faktur");
                }
            }
            catch (Exception ex) { L("GetFakturyDetailAsync BŁĄD: " + ex.Message); bledy.Add("faktury: " + ex.Message); }
            try
            {
                var (ws, wt) = await GetWeryfikacjaAsync(klientId, 0);
                L($"GetWeryfikacjaAsync(0):    zam={ws.LiczbaZamowien} fak={ws.LiczbaFaktur} towarów={wt.Count} | ZamKg={ws.ZamowioneKg:N0} FakKg={ws.ZafakturowaneKg:N0}");
            }
            catch (Exception ex) { L("GetWeryfikacjaAsync BŁĄD: " + ex.Message); bledy.Add("weryfikacja: " + ex.Message); }
            try
            {
                var anul = await GetAnulowaneZamowieniaAsync(klientId, 0);
                L($"GetAnulowaneAsync(0):      {anul.Count} anulowanych");
            }
            catch (Exception ex) { L("GetAnulowaneAsync BŁĄD: " + ex.Message); bledy.Add("anulowane: " + ex.Message); }

            // ── 7. SAMPLE faktur (najstarsze + najnowsze) ──
            H("7. SAMPLE — 10 NAJSTARSZYCH faktur");
            await DiagQuery(sb, ConnHandel, $@"
                SELECT TOP 10 DK.id, DK.typ_dk, DK.kod, DK.data
                FROM [HANDEL].[HM].[DK] DK WHERE DK.khid={klientId} AND DK.anulowany=0
                  AND DK.typ_dk IN ('FVS','FVR','FVZ') ORDER BY DK.data ASC");
            H("7b. SAMPLE — 10 NAJNOWSZYCH faktur");
            await DiagQuery(sb, ConnHandel, $@"
                SELECT TOP 10 DK.id, DK.typ_dk, DK.kod, DK.data
                FROM [HANDEL].[HM].[DK] DK WHERE DK.khid={klientId} AND DK.anulowany=0
                  AND DK.typ_dk IN ('FVS','FVR','FVZ') ORDER BY DK.data DESC");

            // ── 8. PRZECHWYCONE LOGI Debug.WriteLine ──
            H("8. PRZECHWYCONE LOGI (Debug.WriteLine z metod serwisu)");
            System.Diagnostics.Trace.Listeners.Remove(capture);
            var logi = capture.Lines;
            if (logi.Count == 0) L("(brak logów — metody nie zgłosiły problemów)");
            else foreach (var ln in logi) L(ln);

            // ── 9. AUTO-WERDYKT ──
            H("9. ⚖ AUTO-WERDYKT");
            if (!khidIstnieje)
                L("🔴 PROBLEM: khid nie istnieje w HANDEL → wybrany klient ma zły identyfikator. To nie jest bug zapytań.");
            else if (rawFakAll <= 0)
                L($"🟡 Ten klient NIE MA faktur sprzedaży (FVS/FVR/FVZ) w HANDEL. Pusto = poprawnie, nie ma czego pokazać.");
            else if (methodFakCount < 0)
                L("🔴 Metoda GetFakturyDetailAsync rzuciła wyjątek (patrz sekcja 6/8). To bug w zapytaniu.");
            else if (methodFakCount >= rawFakAll && minFakRok < DateTime.Now.Year - 1)
                L($"🟢 OK! Metoda zwraca {methodFakCount} faktur od roku {minFakRok} do {maxFakRok} = CAŁA historia ({rawFakAll} w bazie). " +
                  "Jeśli w oknie wciąż widzisz tylko 2025 → masz STARĄ binarkę (zamknij apkę, przebuduj, uruchom).");
            else if (methodFakCount < rawFakAll)
                L($"🟠 UWAGA: metoda zwraca {methodFakCount}, a w bazie jest {rawFakAll}. Coś jeszcze filtruje — przekaż ten raport.");
            else
                L($"🟢 Metoda zwraca {methodFakCount} faktur (zakres {minFakRok}-{maxFakRok}).");

            if (rawFakAll > rawFakZ12M && rawFakZ12M >= 0)
                L($"ℹ Stary filtr -12M pokazywał {rawFakZ12M} z {rawFakAll} faktur (ukrywał {rawFakAll - rawFakZ12M}). Po fixie powinno być {rawFakAll}.");
            if (dllBuilt != null)
                L($"ℹ DLL zbudowany {dllBuilt}. Jeśli to PRZED Twoją ostatnią przebudową = uruchomiona binarka jest stara.");
            if (bledy.Count > 0)
            {
                L("");
                L("🔴 WYJĄTKI: " + string.Join(" || ", bledy));
            }

            sw.Stop();
            L("");
            L($"═══ KONIEC ({sw.ElapsedMilliseconds} ms) ═══");
            return sb.ToString();
        }

        /// <summary>Test połączenia z latencją.</summary>
        private static async Task<string> DiagPing(string connStr)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await using var cn = new SqlConnection(connStr);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT @@VERSION", cn) { CommandTimeout = 10 };
                var v = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
                sw.Stop();
                var firstLine = v.Split('\n')[0].Trim();
                return $"✅ OK ({sw.ElapsedMilliseconds} ms) — {firstLine}";
            }
            catch (Exception ex) { sw.Stop(); return $"❌ BŁĄD ({sw.ElapsedMilliseconds} ms): {ex.Message}"; }
        }

        /// <summary>Pojedyncza wartość skalarna (int), -1 przy błędzie.</summary>
        private static async Task<int> DiagScalar(string connStr, string sql)
        {
            try
            {
                await using var cn = new SqlConnection(connStr);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }
            catch { return -1; }
        }

        /// <summary>Wykonuje surowe query i dopisuje wynik tabelaryczny do raportu.</summary>
        private static async Task DiagQuery(System.Text.StringBuilder sb, string connStr, string sql)
        {
            try
            {
                await using var cn = new SqlConnection(connStr);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
                await using var rd = await cmd.ExecuteReaderAsync();
                int cols = rd.FieldCount;
                var header = new List<string>();
                for (int i = 0; i < cols; i++) header.Add(rd.GetName(i));
                sb.AppendLine(string.Join(" | ", header));
                sb.AppendLine(new string('-', Math.Min(80, header.Sum(h => h.Length + 3))));
                int rows = 0;
                while (await rd.ReadAsync())
                {
                    rows++;
                    var vals = new List<string>();
                    for (int i = 0; i < cols; i++)
                    {
                        var v = rd.IsDBNull(i) ? "(null)" : rd.GetValue(i);
                        if (v is DateTime dt) vals.Add(dt.ToString("yyyy-MM-dd"));
                        else vals.Add(v?.ToString() ?? "(null)");
                    }
                    sb.AppendLine(string.Join(" | ", vals));
                }
                if (rows == 0) sb.AppendLine("(brak wierszy)");
            }
            catch (Exception ex)
            {
                sb.AppendLine("❌ BŁĄD SQL: " + ex.Message);
            }
        }
    }

    /// <summary>Przechwytuje Debug.WriteLine/Trace podczas diagnostyki.</summary>
    internal sealed class CaptureTraceListener : System.Diagnostics.TraceListener
    {
        public readonly List<string> Lines = new();
        private readonly System.Text.StringBuilder _partial = new();
        public override void Write(string? message) { if (message != null) _partial.Append(message); }
        public override void WriteLine(string? message)
        {
            _partial.Append(message);
            Lines.Add(_partial.ToString());
            _partial.Clear();
        }
    }
}
