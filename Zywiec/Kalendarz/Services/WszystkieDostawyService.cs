using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.Zywiec.Kalendarz.Models;

namespace Kalendarz1.Zywiec.Kalendarz.Services
{
    // Service warstwy danych dla widoku "Wszystkie dostawy" (WPF).
    // - Async SQL z filtrowaniem po stronie bazy
    // - Hurtowe pobieranie SMS snapshotów + statystyk stałych klientów
    // - Eksport do Excel (ClosedXML) i PDF (QuestPDF)
    public sealed class WszystkieDostawyService
    {
        private readonly string _connectionString;
        public WszystkieDostawyService(string connectionString) => _connectionString = connectionString;

        // === FILTRY ===
        public sealed class Filtry
        {
            public DateTime? DataOd { get; set; }
            public DateTime? DataDo { get; set; }
            public string? Dostawca { get; set; }
            public string? Bufor { get; set; }   // "Wszystkie" lub konkretny status
            public string? Szukaj { get; set; }  // tekst pełnotekstowy
            public bool TylkoZeSmsem { get; set; }
            public bool TylkoWymagajaAktualizacjiSms { get; set; }
            public bool TylkoStaliKlienci { get; set; }
        }

        // Pobiera dostawy z filtrami. WSZYSTKO po stronie SQL — żeby filtrowanie 100k wierszy
        // nie wiało pamięcią klienta.
        public async Task<List<WszystkieDostawyRekord>> PobierzAsync(Filtry f, CancellationToken ct = default)
        {
            var lista = new List<WszystkieDostawyRekord>();

            // Najpierw spróbuj utworzyć tabelę SMS (idempotent — jest w SmsDostawySnapshotService)
            try { await SmsDostawySnapshotService.PobierzWszystkieNajnowszeAsync(_connectionString); } catch { }

            // Główne zapytanie — dynamiczna kompozycja WHERE
            var where = new List<string>();
            var p = new Dictionary<string, object>();

            if (f.DataOd.HasValue)
            {
                where.Add("HD.DataOdbioru >= @dataOd");
                p["@dataOd"] = f.DataOd.Value.Date;
            }
            if (f.DataDo.HasValue)
            {
                where.Add("HD.DataOdbioru <= @dataDo");
                p["@dataDo"] = f.DataDo.Value.Date;
            }
            if (!string.IsNullOrWhiteSpace(f.Dostawca))
            {
                where.Add("HD.Dostawca = @dostawca");
                p["@dostawca"] = f.Dostawca;
            }
            if (!string.IsNullOrWhiteSpace(f.Bufor) && f.Bufor != "Wszystkie")
            {
                where.Add("HD.Bufor = @bufor");
                p["@bufor"] = f.Bufor;
            }
            if (!string.IsNullOrWhiteSpace(f.Szukaj))
            {
                where.Add("(HD.Dostawca LIKE @sz OR HD.UWAGI LIKE @sz OR CAST(HD.LP AS NVARCHAR) LIKE @sz)");
                p["@sz"] = "%" + f.Szukaj + "%";
            }
            string whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            string sql = $@"
                SELECT HD.LP, HD.LpW, HD.DostawcaID, HD.Dostawca,
                       HD.DataOdbioru, WK.DataWstawienia,
                       ISNULL(HD.Auta, 0) AS Auta,
                       ISNULL(HD.SztukiDek, 0) AS SztukiDek,
                       ISNULL(HD.WagaDek, 0) AS WagaDek,
                       ISNULL(HD.TypUmowy, '') AS TypUmowy,
                       ISNULL(HD.TypCeny, '') AS TypCeny,
                       ISNULL(HD.Cena, 0) AS Cena,
                       ISNULL(HD.Bufor, '') AS Bufor,
                       ISNULL(HD.Ubytek, 0) AS Ubytek,
                       ISNULL(HD.UWAGI, '') AS UWAGI,
                       HD.DataUtw, ISNULL(O1.Name, '') AS KtoStwo,
                       HD.DataMod, ISNULL(O2.Name, '') AS KtoMod
                FROM [LibraNet].[dbo].[HarmonogramDostaw] HD
                LEFT JOIN [LibraNet].[dbo].[WstawieniaKurczakow] WK ON HD.LpW = WK.Lp
                LEFT JOIN [LibraNet].[dbo].[operators] O1 ON HD.KtoStwo = O1.ID
                LEFT JOIN [LibraNet].[dbo].[operators] O2 ON HD.KtoMod = O2.ID
                {whereClause}
                ORDER BY HD.DataOdbioru DESC, HD.LP DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync(ct);
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                foreach (var kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value);

                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    // Defensywne odczyty — Auta/SztukiDek/Ubytek mogą być w bazie DECIMAL,
                    // dlatego używamy Convert.ToInt32 (tolerancja na różne typy SQL)
                    lista.Add(new WszystkieDostawyRekord
                    {
                        LP = Convert.ToInt32(r.GetValue(0)),
                        LpW = r.IsDBNull(1) ? null : (int?)Convert.ToInt32(r.GetValue(1)),
                        DostawcaID = r.IsDBNull(2) ? "" : r.GetValue(2).ToString() ?? "",
                        Dostawca = r.IsDBNull(3) ? "" : r.GetString(3),
                        DataOdbioru = r.GetDateTime(4),
                        DataWstawienia = r.IsDBNull(5) ? null : (DateTime?)r.GetDateTime(5),
                        Auta = Convert.ToInt32(r.GetValue(6)),
                        SztukiDek = Convert.ToInt32(r.GetValue(7)),
                        WagaDek = Convert.ToDecimal(r.GetValue(8)),
                        TypUmowy = r.GetString(9),
                        TypCeny = r.GetString(10),
                        Cena = Convert.ToDecimal(r.GetValue(11)),
                        Bufor = r.GetString(12),
                        Ubytek = Convert.ToInt32(r.GetValue(13)),
                        Uwagi = r.GetString(14),
                        DataUtw = r.IsDBNull(15) ? null : (DateTime?)r.GetDateTime(15),
                        KtoStwo = r.GetString(16),
                        DataMod = r.IsDBNull(17) ? null : (DateTime?)r.GetDateTime(17),
                        KtoMod = r.GetString(18)
                    });
                }
            }

            // Wzbogać o SMS snapshoty (hurtowo, jeden round-trip)
            var snapshoty = await SmsDostawySnapshotService.PobierzWszystkieNajnowszeAsync(_connectionString);
            foreach (var rekord in lista)
            {
                if (snapshoty.TryGetValue(rekord.LP, out var snap))
                {
                    rekord.BylSMS = true;
                    rekord.SmsCreatedAt = snap.CreatedAt;
                    rekord.SmsWymagaAktualizacji =
                        SmsDostawySnapshotService.WymagaAktualizacji(snap, rekord.DataOdbioru, rekord.Auta);
                }
            }

            // Wylicz stałych klientów (4+ Potwierdzony w 12 m-cach)
            var staliKlienci = await PobierzStalichKlientowAsync(ct);
            foreach (var rekord in lista)
                rekord.StalyKlient = staliKlienci.Contains(rekord.Dostawca);

            // Post-filtry (te które nie da się wyrazić w SQL bo zależą od snapshotów/stałych)
            if (f.TylkoZeSmsem) lista.RemoveAll(x => !x.BylSMS);
            if (f.TylkoWymagajaAktualizacjiSms) lista.RemoveAll(x => !x.SmsWymagaAktualizacji);
            if (f.TylkoStaliKlienci) lista.RemoveAll(x => !x.StalyKlient);

            return lista;
        }

        private async Task<HashSet<string>> PobierzStalichKlientowAsync(CancellationToken ct)
        {
            var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(ct);
                using var cmd = new SqlCommand(@"
                    SELECT Dostawca
                    FROM dbo.HarmonogramDostaw
                    WHERE DataOdbioru >= DATEADD(MONTH, -12, GETDATE())
                      AND Bufor IN ('Potwierdzony','Sprzedany','B.Wolny','B.Wolny.','B.Kontr.')
                    GROUP BY Dostawca
                    HAVING COUNT(*) >= 4", conn) { CommandTimeout = 30 };
                using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) hs.Add(r.GetString(0));
            }
            catch { }
            return hs;
        }

        // === KPI dla pasa nagłówka ===
        public sealed class KpiSummary
        {
            public int LiczbaDostaw { get; set; }
            public int SumaAut { get; set; }
            public long SumaSztuk { get; set; }
            public decimal SredniaCena { get; set; }
            public int UnikalnychHodowcow { get; set; }
        }

        public KpiSummary WyliczKpi(List<WszystkieDostawyRekord> dostawy)
        {
            if (dostawy.Count == 0) return new KpiSummary();
            return new KpiSummary
            {
                LiczbaDostaw = dostawy.Count,
                SumaAut = dostawy.Sum(d => d.Auta),
                SumaSztuk = dostawy.Sum(d => (long)d.SztukiDek),
                SredniaCena = dostawy.Where(d => d.Cena > 0).Count() > 0
                    ? dostawy.Where(d => d.Cena > 0).Average(d => d.Cena)
                    : 0,
                UnikalnychHodowcow = dostawy.Select(d => d.Dostawca).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            };
        }

        // === EKSPORT EXCEL (ClosedXML) ===
        public void EksportujExcel(List<WszystkieDostawyRekord> dostawy, string path)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Dostawy");

            string[] headers = { "LP", "Data odbioru", "Dostawca", "★", "📱", "Auta", "Sztuki",
                "Waga", "Doba dni", "Typ ceny", "Cena", "Bufor", "Ubytek", "Uwagi",
                "Utworzone", "Kto stworzył", "Modyfikowane", "Kto modyfikował" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];
            ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
            ws.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGreen;

            int row = 2;
            foreach (var d in dostawy)
            {
                ws.Cell(row, 1).Value = d.LP;
                ws.Cell(row, 2).Value = d.DataOdbioru;
                ws.Cell(row, 2).Style.DateFormat.Format = "yyyy-MM-dd";
                ws.Cell(row, 3).Value = d.Dostawca;
                ws.Cell(row, 4).Value = d.StalyKlient ? "★" : "";
                ws.Cell(row, 5).Value = d.SmsStatus;
                ws.Cell(row, 6).Value = d.Auta;
                ws.Cell(row, 7).Value = d.SztukiDek;
                ws.Cell(row, 8).Value = d.WagaDek;
                ws.Cell(row, 9).Value = d.RoznicaDni;
                ws.Cell(row, 10).Value = d.TypCeny;
                ws.Cell(row, 11).Value = d.Cena;
                ws.Cell(row, 12).Value = d.Bufor;
                ws.Cell(row, 13).Value = d.Ubytek;
                ws.Cell(row, 14).Value = d.Uwagi;
                ws.Cell(row, 15).Value = d.DataUtw;
                ws.Cell(row, 16).Value = d.KtoStwo;
                ws.Cell(row, 17).Value = d.DataMod;
                ws.Cell(row, 18).Value = d.KtoMod;
                row++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
            wb.SaveAs(path);
        }

        // === EKSPORT PDF (QuestPDF) ===
        public byte[] EksportujPdf(List<WszystkieDostawyRekord> dostawy, KpiSummary kpi, Filtry filtry)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            return Document.Create(c =>
            {
                c.Page(p =>
                {
                    p.Size(PageSizes.A4.Landscape());
                    p.Margin(20);
                    p.DefaultTextStyle(t => t.FontSize(9));

                    p.Header().Column(col =>
                    {
                        col.Item().Text("📦 Raport dostaw żywca")
                            .FontSize(16).Bold().FontColor("#166534");
                        col.Item().Text($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}   •   Wierszy: {dostawy.Count}")
                            .FontSize(8).FontColor("#6B7280");
                        if (filtry.DataOd.HasValue || filtry.DataDo.HasValue)
                            col.Item().Text($"Okres: {filtry.DataOd:dd.MM.yyyy} — {filtry.DataDo:dd.MM.yyyy}")
                                .FontSize(8).FontColor("#6B7280");
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#166534");
                    });

                    p.Content().PaddingVertical(8).Column(col =>
                    {
                        // KPI bar
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Background("#F0FDF4").Padding(6).Column(c =>
                            {
                                c.Item().Text("Dostaw").FontSize(8).FontColor("#166534");
                                c.Item().Text($"{kpi.LiczbaDostaw}").FontSize(14).Bold();
                            });
                            r.RelativeItem().Background("#EFF6FF").Padding(6).Column(c =>
                            {
                                c.Item().Text("Aut razem").FontSize(8).FontColor("#1E40AF");
                                c.Item().Text($"{kpi.SumaAut}").FontSize(14).Bold();
                            });
                            r.RelativeItem().Background("#FEF3C7").Padding(6).Column(c =>
                            {
                                c.Item().Text("Sztuk razem").FontSize(8).FontColor("#92400E");
                                c.Item().Text($"{kpi.SumaSztuk:#,0}").FontSize(14).Bold();
                            });
                            r.RelativeItem().Background("#FDF2F8").Padding(6).Column(c =>
                            {
                                c.Item().Text("Hodowców").FontSize(8).FontColor("#9D174D");
                                c.Item().Text($"{kpi.UnikalnychHodowcow}").FontSize(14).Bold();
                            });
                        });

                        col.Item().PaddingTop(8).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(0.6f); // LP
                                cd.RelativeColumn(1.0f); // Data
                                cd.RelativeColumn(2.5f); // Dostawca
                                cd.RelativeColumn(0.4f); // Auta
                                cd.RelativeColumn(1.0f); // Sztuki
                                cd.RelativeColumn(0.8f); // Waga
                                cd.RelativeColumn(0.8f); // Cena
                                cd.RelativeColumn(1.2f); // Bufor
                            });
                            t.Header(h =>
                            {
                                string[] hd = { "LP", "Data", "Dostawca", "A", "Sztuk", "Waga", "Cena", "Status" };
                                foreach (var x in hd)
                                    h.Cell().Background("#166534").Padding(3)
                                        .Text(x).FontColor("#FFFFFF").Bold().FontSize(8);
                            });
                            int idx = 0;
                            foreach (var d in dostawy)
                            {
                                string bg = (idx++ % 2 == 0) ? "#FFFFFF" : "#F9FAFB";
                                t.Cell().Background(bg).Padding(2).Text($"{d.LP}").FontSize(8);
                                t.Cell().Background(bg).Padding(2).Text($"{d.DataOdbioru:dd.MM.yyyy}").FontSize(8);
                                t.Cell().Background(bg).Padding(2)
                                    .Text((d.StalyKlient ? "★ " : "") + d.Dostawca).FontSize(8);
                                t.Cell().Background(bg).Padding(2).AlignRight().Text($"{d.Auta}").FontSize(8);
                                t.Cell().Background(bg).Padding(2).AlignRight().Text($"{d.SztukiDek:#,0}").FontSize(8);
                                t.Cell().Background(bg).Padding(2).AlignRight().Text($"{d.WagaDek:0.00}").FontSize(8);
                                t.Cell().Background(bg).Padding(2).AlignRight().Text($"{d.Cena:0.00}").FontSize(8);
                                t.Cell().Background(bg).Padding(2).Text(d.Bufor).FontSize(8);
                            }
                        });
                    });

                    p.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Strona ").FontSize(8).FontColor("#6B7280");
                        x.CurrentPageNumber().FontSize(8).FontColor("#6B7280");
                        x.Span(" z ").FontSize(8).FontColor("#6B7280");
                        x.TotalPages().FontSize(8).FontColor("#6B7280");
                        x.Span("   •   Ubojnia Drobiu Piórkowscy").FontSize(8).FontColor("#6B7280");
                    });
                });
            }).GeneratePdf();
        }
    }
}
