using Kalendarz1.Customer360.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Agregator wszystkich negatywnych sygnalow odbiorcy z 3 baz:
    /// - LibraNet.Reklamacje + Towary + Partie
    /// - LibraNet.ZamowieniaMieso (anulowane, niedotrzymane)
    /// - HANDEL.HM.DK (korekty, anulowane faktury, zmiany terminow w PN)
    /// </summary>
    public class Customer360TransparentnoscService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public async Task<TransparentnoscDane> GetTransparentnoscAsync(int klientId, decimal obrot12M)
        {
            var dane = new TransparentnoscDane();
            try
            {
                // Rownolegle: 5 osobnych zapytan z 2 baz
                var tAnul = LoadAnulowaneStatystykiAsync(klientId);
                var tRek = LoadReklamacjePeneAsync(klientId);
                var tKor = LoadKorektyMinusAsync(klientId);
                var tTerm = LoadZmianyTerminowAsync(klientId);
                var tWzor = LoadAnulacjeWgMiesiacaAsync(klientId);

                await Task.WhenAll(tAnul, tRek, tKor, tTerm, tWzor);

                var anul = await tAnul;
                dane.LiczbaAnulowanych = anul.liczba;
                dane.SumaKgAnulowanych = anul.kg;
                dane.SumaWartoscAnulowanych = anul.wartosc;
                dane.ProcAnulowanych = anul.proc;

                dane.Reklamacje = await tRek;
                dane.LiczbaReklamacji = dane.Reklamacje.Count;
                dane.WartoscReklamacji = dane.Reklamacje.Sum(r => r.Kwota);
                dane.LiczbaReklamacjiOtwartych = dane.Reklamacje.Count(r => r.Otwarta);
                dane.ProcReklamacjiObrotu = obrot12M > 0 ? dane.WartoscReklamacji / obrot12M * 100m : 0m;

                dane.Korekty = await tKor;
                dane.LiczbaKorektMinus = dane.Korekty.Count(k => k.JestMinus);
                dane.SumaKorektMinus = dane.Korekty.Where(k => k.JestMinus).Sum(k => Math.Abs(k.Walbrutto));

                dane.ZmianyTerminow = await tTerm;
                dane.LiczbaZmianTerminow = dane.ZmianyTerminow.Count;
                dane.SredniePrzesuniecieTerminowDni = dane.ZmianyTerminow.Count > 0
                    ? (decimal)dane.ZmianyTerminow.Average(z => z.PrzesunieceDni)
                    : 0m;

                dane.AnulacjeWgMiesiaca = await tWzor;

                // Niedotrzymanie z istniejacego serwisu Customer360Service (re-use)
                // (Wezwiemy z UI bo tu byloby cykliczne)

                dane.Timeline = BudujTimeline(dane);
                dane.TopProblematyczne = BudujTopProblematyczne(dane);
                dane.TrendReklamacji = ObliczTrendReklamacji(dane.Reklamacje);

                // KLASYFIKACJA RYZYKA
                dane.Klasyfikacja = KlasyfikujRyzyko(dane, obrot12M);

                // REKOMENDACJA AI (algorytmiczna, czytelna po polsku)
                (dane.RekomendacjaTekst, dane.RekomendacjaPoziom) = GenerujRekomendacje(dane, obrot12M);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[C360 transparentnosc] {ex.Message}");
            }
            return dane;
        }

        // ============= LADOWANIE Z BAZY =============

        private async Task<(int liczba, decimal kg, decimal wartosc, decimal proc)> LoadAnulowaneStatystykiAsync(int klientId)
        {
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(@"
                    DECLARE @anulIle INT = 0, @anulKg DECIMAL(18,2) = 0, @anulWart DECIMAL(18,2) = 0;
                    DECLARE @wszIle INT = 0;
                    SELECT @anulIle = COUNT(DISTINCT z.Id),
                           @anulKg = ISNULL(SUM(zt.Ilosc),0),
                           @anulWart = ISNULL(SUM(zt.Ilosc * TRY_CAST(zt.Cena AS DECIMAL(18,2))),0)
                    FROM dbo.ZamowieniaMieso z
                    LEFT JOIN dbo.ZamowieniaMiesoTowar zt ON zt.ZamowienieId = z.Id
                    WHERE z.KlientId = @kid
                      AND ISNULL(z.Status,'') IN ('Anulowane','Anulowano')
                      AND z.DataPrzyjazdu >= DATEADD(MONTH,-12,GETDATE());

                    SELECT @wszIle = COUNT(DISTINCT z.Id)
                    FROM dbo.ZamowieniaMieso z
                    WHERE z.KlientId = @kid
                      AND z.DataPrzyjazdu >= DATEADD(MONTH,-12,GETDATE())
                      AND CAST(z.DataPrzyjazdu AS DATE) <= CAST(GETDATE() AS DATE);

                    SELECT @anulIle, @anulKg, @anulWart, @wszIle;", cn) { CommandTimeout = 8 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    int anulIle = rd.GetInt32(0);
                    decimal anulKg = rd.GetDecimal(1);
                    decimal anulWart = rd.GetDecimal(2);
                    int wszIle = rd.GetInt32(3);
                    decimal proc = wszIle > 0 ? (decimal)anulIle / (wszIle + anulIle) * 100m : 0m;
                    return (anulIle, anulKg, anulWart, proc);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 anul stat] " + ex.Message); }
            return (0, 0, 0, 0);
        }

        private async Task<List<ReklamacjaSzczegoly>> LoadReklamacjePeneAsync(int klientId)
        {
            var lista = new List<ReklamacjaSzczegoly>();
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();

                // Sprawdz czy tabela istnieje
                await using (var chk = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.tables WHERE name='Reklamacje'", cn) { CommandTimeout = 3 })
                {
                    if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0) return lista;
                }

                // Pelne wczytanie reklamacji 12M
                const string sql = @"
                    SELECT TOP 100
                        r.Id,
                        r.DataZgloszenia,
                        ISNULL(r.NumerDokumentu,'') AS NumerDokumentu,
                        ISNULL(r.Opis,'') AS Opis,
                        ISNULL(r.SumaKg, 0) AS SumaKg,
                        ISNULL(r.Kwota, 0) AS Kwota,
                        ISNULL(r.Status,'') AS Status,
                        ISNULL(r.StatusV2,'') AS StatusV2,
                        ISNULL(r.TypReklamacji,'') AS TypReklamacji,
                        ISNULL(r.Priorytet,'') AS Priorytet,
                        ISNULL(r.ZrodloZgloszenia,'') AS ZrodloZgloszenia,
                        ISNULL(r.PrzyczynaGlowna,'') AS PrzyczynaGlowna,
                        ISNULL(r.AkcjeNaprawcze,'') AS AkcjeNaprawcze,
                        ISNULL(r.OsobaRozpatrujaca,'') AS OsobaRozpatrujaca,
                        r.DataZakonczenia
                    FROM dbo.Reklamacje r
                    WHERE r.IdKontrahenta = @kid
                      AND r.DataZgloszenia >= DATEADD(MONTH,-12,GETDATE())
                    ORDER BY r.DataZgloszenia DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var r = new ReklamacjaSzczegoly
                    {
                        Id = rd.GetInt32(0),
                        DataZgloszenia = rd.GetDateTime(1),
                        NumerDokumentu = rd.GetString(2),
                        Opis = rd.GetString(3),
                        SumaKg = rd.GetDecimal(4),
                        Kwota = rd.GetDecimal(5),
                        Status = rd.GetString(6),
                        StatusV2 = rd.GetString(7),
                        TypReklamacji = rd.GetString(8),
                        Priorytet = rd.GetString(9),
                        ZrodloZgloszenia = rd.GetString(10),
                        PrzyczynaGlowna = rd.GetString(11),
                        AkcjeNaprawcze = rd.GetString(12),
                        OsobaRozpatrujaca = rd.GetString(13),
                        DataZakonczenia = rd.IsDBNull(14) ? null : (DateTime?)rd.GetDateTime(14)
                    };
                    r.StatusV2Etykieta = r.StatusV2 switch
                    {
                        "ZGLOSZONA" => "Nowa",
                        "W_ANALIZIE" => "Rozpatrywana",
                        "ZASADNA" => "Uznana",
                        "POWIAZANA" => "Polaczona",
                        "ZAMKNIETA" => "Zamknieta",
                        "ODRZUCONA" => "Odrzucona",
                        _ => r.Status
                    };
                    lista.Add(r);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 reklamacje] " + ex.Message); }
            return lista;
        }

        private async Task<List<KorektaSygnalu>> LoadKorektyMinusAsync(int klientId)
        {
            var lista = new List<KorektaSygnalu>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                // Korekty FKS/FKR z minusem + powiazania do oryginalu
                const string sql = @"
                    SELECT TOP 50
                        DK.id,
                        ISNULL(DK.numer,'') AS Numer,
                        DK.data,
                        ISNULL(DK.typ_dk,'') AS TypDk,
                        ISNULL(DK.walbrutto, 0) AS Walbrutto,
                        ISNULL(DK.iddokkoryg, 0) AS IdOryginalu,
                        ISNULL(O.numer,'') AS NumerOryginalu
                    FROM [HANDEL].[HM].[DK] DK
                    LEFT JOIN [HANDEL].[HM].[DK] O ON O.id = DK.iddokkoryg
                    WHERE DK.khid = @kid
                      AND DK.anulowany = 0
                      AND DK.data >= DATEADD(MONTH,-12,GETDATE())
                      AND DK.typ_dk IN ('FKS','FKR','FKZ')
                    ORDER BY DK.data DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new KorektaSygnalu
                    {
                        Id = rd.GetInt32(0),
                        NumerDokumentu = rd.GetString(1),
                        Data = rd.GetDateTime(2),
                        TypDk = rd.GetString(3),
                        Walbrutto = rd.GetDecimal(4),
                        IdDokumentuOryginalnego = rd.GetInt32(5),
                        NumerOryginalu = rd.GetString(6)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 korekty] " + ex.Message); }
            return lista;
        }

        private async Task<List<ZmianaTerminu>> LoadZmianyTerminowAsync(int klientId)
        {
            var lista = new List<ZmianaTerminu>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();
                // Faktury gdzie PN.MAX(Termin) > DK.plattermin = ktos zmienil termin w PN
                const string sql = @"
                    SELECT TOP 30
                        DK.id,
                        ISNULL(DK.numer,'') AS Numer,
                        DK.data,
                        DK.plattermin,
                        MAX(PN.Termin) AS TerminAktualny,
                        ISNULL(DK.walbrutto,0) AS Wal
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[PN] PN ON PN.dkid = DK.id
                    WHERE DK.khid = @kid
                      AND DK.anulowany = 0
                      AND DK.data >= DATEADD(MONTH,-12,GETDATE())
                      AND DK.typ_dk IN ('FVS','FVR','FVZ')
                      AND PN.Termin IS NOT NULL
                      AND DK.plattermin IS NOT NULL
                    GROUP BY DK.id, DK.numer, DK.data, DK.plattermin, DK.walbrutto
                    HAVING MAX(PN.Termin) > DK.plattermin
                    ORDER BY DK.data DESC";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 15 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new ZmianaTerminu
                    {
                        IdFaktury = rd.GetInt32(0),
                        NumerFaktury = rd.GetString(1),
                        DataFaktury = rd.GetDateTime(2),
                        TerminPierwotny = rd.GetDateTime(3),
                        TerminAktualny = rd.GetDateTime(4),
                        KwotaFaktury = rd.GetDecimal(5)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 zmiany term] " + ex.Message); }
            return lista;
        }

        private async Task<Dictionary<int, int>> LoadAnulacjeWgMiesiacaAsync(int klientId)
        {
            var mapa = new Dictionary<int, int>();
            for (int i = 1; i <= 12; i++) mapa[i] = 0;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                const string sql = @"
                    SELECT MONTH(z.DataPrzyjazdu) AS M, COUNT(*) AS Ile
                    FROM dbo.ZamowieniaMieso z
                    WHERE z.KlientId = @kid
                      AND ISNULL(z.Status,'') IN ('Anulowane','Anulowano')
                      AND z.DataPrzyjazdu >= DATEADD(YEAR,-3,GETDATE())
                    GROUP BY MONTH(z.DataPrzyjazdu)";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 8 };
                cmd.Parameters.AddWithValue("@kid", klientId);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int m = rd.GetInt32(0);
                    int ile = rd.GetInt32(1);
                    if (m >= 1 && m <= 12) mapa[m] = ile;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 anulw mies] " + ex.Message); }
            return mapa;
        }

        // ============= AGREGACJA POMOCNICZA =============

        private List<IncydentTransparentnosci> BudujTimeline(TransparentnoscDane d)
        {
            var t = new List<IncydentTransparentnosci>();
            foreach (var r in d.Reklamacje.Take(15))
            {
                t.Add(new IncydentTransparentnosci
                {
                    Data = r.DataZgloszenia,
                    Typ = "Reklamacja",
                    Ikona = "🔧",
                    Opis = $"{r.TypReklamacji} — {r.NumerDokumentu} ({r.StatusV2Etykieta})",
                    Kwota = r.Kwota,
                    KolorHex = r.Otwarta ? "#DC2626" : "#64748B"
                });
            }
            foreach (var k in d.Korekty.Where(k => k.JestMinus).Take(10))
            {
                t.Add(new IncydentTransparentnosci
                {
                    Data = k.Data,
                    Typ = "Korekta minus",
                    Ikona = "💸",
                    Opis = $"Korekta {k.TypDk} do {k.NumerOryginalu}",
                    Kwota = k.Walbrutto,
                    KolorHex = "#F59E0B"
                });
            }
            foreach (var z in d.ZmianyTerminow.Take(10))
            {
                t.Add(new IncydentTransparentnosci
                {
                    Data = z.DataFaktury,
                    Typ = "Zmiana terminu",
                    Ikona = "⏰",
                    Opis = $"FV {z.NumerFaktury} — przesuniety o {z.PrzesunieceDni} dni",
                    Kwota = z.KwotaFaktury,
                    KolorHex = "#F59E0B"
                });
            }
            return t.OrderByDescending(x => x.Data).Take(20).ToList();
        }

        private List<TopProblematycznyTowar> BudujTopProblematyczne(TransparentnoscDane d)
        {
            // Agregacja per kod towaru z reklamacji (bo Reklamacje to glowne zrodlo informacji per-towar)
            // Tu uproszczenie: grupowanie reklamacji po opisie/numerze dok (bez ReklamacjeTowary join)
            // Pelne rozszerzenie w przyszlosci po dolaczeniu ReklamacjeTowary
            return new List<TopProblematycznyTowar>();
        }

        private string ObliczTrendReklamacji(List<ReklamacjaSzczegoly> rek)
        {
            if (rek.Count < 4) return "▬ za malo danych";
            var teraz = DateTime.Today;
            int polowa = rek.Count(r => (teraz - r.DataZgloszenia).TotalDays <= 180);
            int wczesniej = rek.Count - polowa;
            if (polowa > wczesniej * 1.3) return "▲ rosnie";
            if (polowa < wczesniej * 0.7) return "▼ spada";
            return "▬ stabilnie";
        }

        // ============= KLASYFIKACJA RYZYKA =============

        private KlasyfikacjaRyzyka KlasyfikujRyzyko(TransparentnoscDane d, decimal obrot12M)
        {
            var k = new KlasyfikacjaRyzyka();

            // 4 sub-wskazniki, kazdy 0-100 gdzie 100 = krytyczne
            // 1) REPUTACYJNY — reklamacje + ich SLA + priorytet
            int rep = 0;
            rep += Math.Min(40, d.LiczbaReklamacji * 5);                        // do 40 pkt za samo wystepowanie
            rep += Math.Min(30, d.LiczbaReklamacjiOtwartych * 10);              // do 30 za otwarte
            rep += Math.Min(20, d.Reklamacje.Count(r => r.SlaPrzekroczone) * 7);// do 20 za przekroczone SLA
            rep += Math.Min(10, d.Reklamacje.Count(r => r.Priorytet == "Krytyczny") * 5);
            k.RiskReputacyjny = Math.Min(100, rep);
            k.OpisReputacyjny = d.LiczbaReklamacji == 0
                ? "Brak reklamacji w 12M — czysta historia"
                : $"{d.LiczbaReklamacji} reklamacji ({d.LiczbaReklamacjiOtwartych} otwartych)";

            // 2) FINANSOWY — przeterminowane + korekty minus + zmiany terminow
            int fin = 0;
            if (obrot12M > 0)
                fin += Math.Min(40, (int)(d.Przeterminowane / obrot12M * 1000));   // do 40 za % przeterminowanych
            fin += Math.Min(30, d.LiczbaKorektMinus * 5);                          // do 30 za korekty minus
            fin += Math.Min(20, d.LiczbaZmianTerminow * 4);                        // do 20 za przekladania
            fin += Math.Min(10, d.MaxDniOpoznienia / 6);                           // do 10 za max dni
            k.RiskFinansowy = Math.Min(100, fin);
            k.OpisFinansowy = (d.Przeterminowane <= 0 && d.LiczbaKorektMinus == 0 && d.LiczbaZmianTerminow == 0)
                ? "Bez problemow finansowych"
                : $"{d.Przeterminowane:N0} zl przeterminowane, {d.LiczbaKorektMinus} korekt minus, {d.LiczbaZmianTerminow} przekladan";

            // 3) OPERACYJNY — anulacje + niedotrzymanie
            int op = 0;
            op += Math.Min(50, (int)d.ProcAnulowanych * 3);                        // do 50 za % anulacji
            if (d.SredniaRealizacjaProc > 0 && d.SredniaRealizacjaProc < 100)
                op += Math.Min(50, (int)(100 - d.SredniaRealizacjaProc) * 2);      // do 50 za niedotrzymanie
            k.RiskOperacyjny = Math.Min(100, op);
            k.OpisOperacyjny = (d.LiczbaAnulowanych == 0 && d.SredniaRealizacjaProc >= 98)
                ? "Plynna realizacja"
                : $"{d.LiczbaAnulowanych} anulacji ({d.ProcAnulowanych:N1}%), realizacja {d.SredniaRealizacjaProc:N0}%";

            // 4) KOMUNIKACYJNY — narazie placeholder (do rozszerzenia o parsing notatek)
            k.RiskKomunikacyjny = 0;
            k.OpisKomunikacyjny = "Brak analizy notatek (placeholder)";

            // TOTAL — srednia wazona
            k.TotalScore = (int)((k.RiskReputacyjny * 0.30 + k.RiskFinansowy * 0.35 + k.RiskOperacyjny * 0.30 + k.RiskKomunikacyjny * 0.05));

            // Litera + kolor
            if (k.TotalScore < 15) { k.Litera = "A"; k.Kategoria = "Niskie ryzyko"; k.KolorHex = "#16A34A"; }
            else if (k.TotalScore < 35) { k.Litera = "B"; k.Kategoria = "Srednie ryzyko"; k.KolorHex = "#EAB308"; }
            else if (k.TotalScore < 60) { k.Litera = "C"; k.Kategoria = "Wysokie ryzyko"; k.KolorHex = "#F97316"; }
            else { k.Litera = "D"; k.Kategoria = "Krytyczne ryzyko"; k.KolorHex = "#DC2626"; }

            return k;
        }

        private (string tekst, string poziom) GenerujRekomendacje(TransparentnoscDane d, decimal obrot12M)
        {
            var k = d.Klasyfikacja;
            if (k.TotalScore < 15)
                return ("✅ Klient bez istotnych sygnalow ryzyka. Kontynuuj standardowa obsluge.", "INFO");

            var sygnaly = new List<string>();
            if (d.LiczbaReklamacjiOtwartych >= 3)
                sygnaly.Add($"{d.LiczbaReklamacjiOtwartych} otwartych reklamacji — wymaga eskalacji do jakosci");
            if (d.LiczbaReklamacji > 0 && d.WartoscReklamacji / Math.Max(obrot12M, 1) > 0.02m)
                sygnaly.Add($"Reklamacje > 2% obrotu ({d.ProcReklamacjiObrotu:N1}%) — toksyczny stosunek wartosci do problemow");
            if (d.ProcAnulowanych > 10m)
                sygnaly.Add($"{d.ProcAnulowanych:N0}% zamowien anulowanych — nieregularny wzor operacyjny");
            if (d.LiczbaKorektMinus >= 3)
                sygnaly.Add($"{d.LiczbaKorektMinus} korekt na minus ({d.SumaKorektMinus:N0} zl) — spor cenowy / niezgodnosci");
            if (d.LiczbaZmianTerminow >= 3)
                sygnaly.Add($"{d.LiczbaZmianTerminow} przekladan terminow platnosci — problemy z plynnoscia klienta");
            if (d.SredniaRealizacjaProc > 0 && d.SredniaRealizacjaProc < 90m)
                sygnaly.Add($"Realizacja zamowien {d.SredniaRealizacjaProc:N0}% — mocne niedotrzymanie ze strony produkcji LUB zlece");

            string poziom = k.TotalScore >= 60 ? "CRITICAL" : k.TotalScore >= 35 ? "WARNING" : "INFO";
            string preamble = poziom switch
            {
                "CRITICAL" => "🚨 KRYTYCZNE RYZYKO — wymaga decyzji strategicznej (blokada / windykacja / rezygnacja)",
                "WARNING" => "⚠ WYSOKIE RYZYKO — wymaga proaktywnej obslugi i nadzoru",
                _ => "ℹ Srednie sygnaly — obserwuj i rozmawiaj"
            };

            string rec = preamble + ":\n" + string.Join("\n", sygnaly.Select(s => "• " + s));
            return (rec, poziom);
        }
    }
}
