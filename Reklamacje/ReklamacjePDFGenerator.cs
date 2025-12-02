using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace Kalendarz1.Reklamacje
{
    /// <summary>
    /// Generator raportów PDF dla reklamacji
    /// </summary>
    public class ReklamacjePDFGenerator
    {
        private readonly string _connectionString;
        private readonly string _outputDirectory;

        public ReklamacjePDFGenerator(string connectionString)
        {
            _connectionString = connectionString;
            _outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ReklamacjeRaporty");

            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        /// <summary>
        /// Generuje raport PDF ze szczegółami reklamacji
        /// </summary>
        public string GenerujRaportReklamacji(int idReklamacji)
        {
            var reklamacja = PobierzDaneReklamacji(idReklamacji);
            if (reklamacja == null)
                throw new Exception($"Nie znaleziono reklamacji o ID: {idReklamacji}");

            var towary = PobierzTowary(idReklamacji);
            var partie = PobierzPartie(idReklamacji);
            var historia = PobierzHistorie(idReklamacji);

            var html = GenerujHtmlReklamacji(reklamacja, towary, partie, historia);

            var fileName = $"Reklamacja_{idReklamacji}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            var filePath = Path.Combine(_outputDirectory, fileName);
            File.WriteAllText(filePath, html, Encoding.UTF8);

            return filePath;
        }

        /// <summary>
        /// Generuje zestawienie reklamacji w okresie
        /// </summary>
        public string GenerujZestawienie(DateTime dataOd, DateTime dataDo, string status = null)
        {
            var reklamacje = PobierzListeReklamacji(dataOd, dataDo, status);
            var html = GenerujHtmlZestawienia(reklamacje, dataOd, dataDo, status);

            var fileName = $"Zestawienie_Reklamacji_{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}.html";
            var filePath = Path.Combine(_outputDirectory, fileName);
            File.WriteAllText(filePath, html, Encoding.UTF8);

            return filePath;
        }

        /// <summary>
        /// Otwiera wygenerowany raport w przeglądarce
        /// </summary>
        public void OtworzRaport(string sciezka)
        {
            if (File.Exists(sciezka))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = sciezka,
                    UseShellExecute = true
                });
            }
        }

        #region Pobieranie danych

        private ReklamacjaData PobierzDaneReklamacji(int id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var query = @"SELECT * FROM [dbo].[Reklamacje] WHERE Id = @Id";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ReklamacjaData
                            {
                                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                                DataZgloszenia = reader["DataZgloszenia"] != DBNull.Value ? Convert.ToDateTime(reader["DataZgloszenia"]) : DateTime.MinValue,
                                NumerDokumentu = reader["NumerDokumentu"]?.ToString() ?? "",
                                NazwaKontrahenta = reader["NazwaKontrahenta"]?.ToString() ?? "",
                                IdKontrahenta = reader["IdKontrahenta"]?.ToString() ?? "",
                                Opis = reader["Opis"]?.ToString() ?? "",
                                Status = reader["Status"]?.ToString() ?? "",
                                SumaKg = reader["SumaKg"] != DBNull.Value ? Convert.ToDecimal(reader["SumaKg"]) : 0,
                                UserID = reader["UserID"]?.ToString() ?? "",
                                OsobaRozpatrujaca = reader["OsobaRozpatrujaca"]?.ToString() ?? "",
                                Komentarz = reader["Komentarz"]?.ToString() ?? "",
                                Rozwiazanie = reader["Rozwiazanie"]?.ToString() ?? "",
                                DataZamkniecia = reader["DataZamkniecia"] != DBNull.Value ? Convert.ToDateTime(reader["DataZamkniecia"]) : (DateTime?)null
                            };
                        }
                    }
                }
            }
            return null;
        }

        private List<TowarData> PobierzTowary(int idReklamacji)
        {
            var lista = new List<TowarData>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var query = @"SELECT * FROM [dbo].[ReklamacjeTowary] WHERE IdReklamacji = @Id ORDER BY Id";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new TowarData
                            {
                                NazwaTowaru = GetSafeString(reader, "NazwaTowaru"),
                                IloscKg = GetSafeDecimal(reader, "IloscKg"),
                                IdTowaru = GetSafeString(reader, "IdTowaru")
                            });
                        }
                    }
                }
            }

            return lista;
        }

        private List<string> PobierzPartie(int idReklamacji)
        {
            var lista = new List<string>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var query = @"SELECT * FROM [dbo].[ReklamacjePartie] WHERE IdReklamacji = @Id ORDER BY DataDodania";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string partia = "";
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var colName = reader.GetName(i);
                                if ((colName == "Partia" || colName == "NumerPartii") && !reader.IsDBNull(i))
                                {
                                    partia = reader[i].ToString();
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(partia))
                                lista.Add(partia);
                        }
                    }
                }
            }

            return lista;
        }

        private List<HistoriaData> PobierzHistorie(int idReklamacji)
        {
            var lista = new List<HistoriaData>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var query = @"SELECT * FROM [dbo].[ReklamacjeHistoria] WHERE ReklamacjaId = @Id OR IdReklamacji = @Id ORDER BY DataZmiany DESC";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    try
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new HistoriaData
                                {
                                    DataZmiany = GetSafeDateTime(reader, "DataZmiany"),
                                    StatusPoprzedni = GetSafeString(reader, "StatusPoprzedni"),
                                    StatusNowy = GetSafeString(reader, "StatusNowy"),
                                    ZmienionePrzez = GetSafeString(reader, "ZmienionePrzez"),
                                    Komentarz = GetSafeString(reader, "Komentarz")
                                });
                            }
                        }
                    }
                    catch { }
                }
            }

            return lista;
        }

        private List<ReklamacjaData> PobierzListeReklamacji(DateTime dataOd, DateTime dataDo, string status)
        {
            var lista = new List<ReklamacjaData>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var query = @"
                    SELECT * FROM [dbo].[Reklamacje]
                    WHERE DataZgloszenia BETWEEN @DataOd AND @DataDo";

                if (!string.IsNullOrEmpty(status))
                    query += " AND Status = @Status";

                query += " ORDER BY DataZgloszenia DESC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo.Date.AddDays(1).AddSeconds(-1));
                    if (!string.IsNullOrEmpty(status))
                        cmd.Parameters.AddWithValue("@Status", status);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lista.Add(new ReklamacjaData
                            {
                                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                                DataZgloszenia = reader["DataZgloszenia"] != DBNull.Value ? Convert.ToDateTime(reader["DataZgloszenia"]) : DateTime.MinValue,
                                NumerDokumentu = reader["NumerDokumentu"]?.ToString() ?? "",
                                NazwaKontrahenta = reader["NazwaKontrahenta"]?.ToString() ?? "",
                                Opis = reader["Opis"]?.ToString() ?? "",
                                Status = reader["Status"]?.ToString() ?? "",
                                SumaKg = reader["SumaKg"] != DBNull.Value ? Convert.ToDecimal(reader["SumaKg"]) : 0,
                                UserID = reader["UserID"]?.ToString() ?? "",
                                OsobaRozpatrujaca = reader["OsobaRozpatrujaca"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            return lista;
        }

        #endregion

        #region Generowanie HTML

        private string GenerujHtmlReklamacji(ReklamacjaData reklamacja, List<TowarData> towary, List<string> partie, List<HistoriaData> historia)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine($"    <title>Reklamacja #{reklamacja.Id}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCommonStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Nagłówek
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("    <div class='logo'>UBOJNIA DROBIU PIÓRKOWSCY</div>");
            sb.AppendLine($"    <h1>REKLAMACJA #{reklamacja.Id}</h1>");
            sb.AppendLine($"    <p class='generated'>Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.AppendLine("</div>");

            // Status
            var statusClass = GetStatusClass(reklamacja.Status);
            sb.AppendLine($"<div class='status-banner {statusClass}'>");
            sb.AppendLine($"    <span class='status-label'>STATUS:</span>");
            sb.AppendLine($"    <span class='status-value'>{EscapeHtml(reklamacja.Status)}</span>");
            sb.AppendLine("</div>");

            // Informacje podstawowe
            sb.AppendLine("<div class='section'>");
            sb.AppendLine("    <h3>Informacje podstawowe</h3>");
            sb.AppendLine("    <div class='info-grid'>");

            sb.AppendLine($"        <div class='info-row'><span class='label'>Data zgłoszenia:</span><span class='value'>{reklamacja.DataZgloszenia:dd.MM.yyyy HH:mm}</span></div>");
            sb.AppendLine($"        <div class='info-row'><span class='label'>Zgłosił:</span><span class='value'>{EscapeHtml(reklamacja.UserID)}</span></div>");
            sb.AppendLine($"        <div class='info-row'><span class='label'>Nr dokumentu:</span><span class='value'>{EscapeHtml(reklamacja.NumerDokumentu)}</span></div>");
            sb.AppendLine($"        <div class='info-row'><span class='label'>Kontrahent:</span><span class='value'>{EscapeHtml(reklamacja.NazwaKontrahenta)}</span></div>");
            sb.AppendLine($"        <div class='info-row'><span class='label'>Suma kg:</span><span class='value'>{reklamacja.SumaKg:N2} kg</span></div>");
            sb.AppendLine($"        <div class='info-row'><span class='label'>Osoba rozpatrująca:</span><span class='value'>{EscapeHtml(reklamacja.OsobaRozpatrujaca)}</span></div>");

            if (reklamacja.DataZamkniecia.HasValue)
                sb.AppendLine($"        <div class='info-row'><span class='label'>Data zamknięcia:</span><span class='value'>{reklamacja.DataZamkniecia:dd.MM.yyyy HH:mm}</span></div>");

            sb.AppendLine("    </div>");
            sb.AppendLine("</div>");

            // Opis problemu
            sb.AppendLine("<div class='section'>");
            sb.AppendLine("    <h3>Opis problemu</h3>");
            sb.AppendLine($"    <div class='text-box'>{EscapeHtml(reklamacja.Opis).Replace("\n", "<br/>")}</div>");
            sb.AppendLine("</div>");

            // Towary
            if (towary.Count > 0)
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Reklamowane towary</h3>");
                sb.AppendLine("    <table>");
                sb.AppendLine("    <thead><tr><th>Lp.</th><th>Nazwa towaru</th><th>ID towaru</th><th class='number'>Ilość [kg]</th></tr></thead>");
                sb.AppendLine("    <tbody>");

                int lp = 1;
                decimal suma = 0;
                foreach (var towar in towary)
                {
                    sb.AppendLine($"        <tr><td>{lp++}</td><td>{EscapeHtml(towar.NazwaTowaru)}</td><td>{EscapeHtml(towar.IdTowaru)}</td><td class='number'>{towar.IloscKg:N2}</td></tr>");
                    suma += towar.IloscKg;
                }

                sb.AppendLine($"        <tr class='summary-row'><td colspan='3'><strong>RAZEM:</strong></td><td class='number'><strong>{suma:N2}</strong></td></tr>");
                sb.AppendLine("    </tbody>");
                sb.AppendLine("    </table>");
                sb.AppendLine("</div>");
            }

            // Partie
            if (partie.Count > 0)
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Partie produkcyjne</h3>");
                sb.AppendLine("    <ul class='party-list'>");
                foreach (var partia in partie)
                {
                    sb.AppendLine($"        <li>{EscapeHtml(partia)}</li>");
                }
                sb.AppendLine("    </ul>");
                sb.AppendLine("</div>");
            }

            // Komentarz i rozwiązanie
            if (!string.IsNullOrWhiteSpace(reklamacja.Komentarz))
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Komentarze</h3>");
                sb.AppendLine($"    <div class='text-box'>{EscapeHtml(reklamacja.Komentarz).Replace("\n", "<br/>")}</div>");
                sb.AppendLine("</div>");
            }

            if (!string.IsNullOrWhiteSpace(reklamacja.Rozwiazanie))
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Rozwiązanie</h3>");
                sb.AppendLine($"    <div class='text-box resolution'>{EscapeHtml(reklamacja.Rozwiazanie).Replace("\n", "<br/>")}</div>");
                sb.AppendLine("</div>");
            }

            // Historia
            if (historia.Count > 0)
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Historia zmian statusu</h3>");
                sb.AppendLine("    <table>");
                sb.AppendLine("    <thead><tr><th>Data</th><th>Zmiana statusu</th><th>Przez</th><th>Komentarz</th></tr></thead>");
                sb.AppendLine("    <tbody>");

                foreach (var h in historia)
                {
                    sb.AppendLine($"        <tr>");
                    sb.AppendLine($"            <td>{h.DataZmiany:dd.MM.yyyy HH:mm}</td>");
                    sb.AppendLine($"            <td><span class='{GetStatusClass(h.StatusPoprzedni)}'>{EscapeHtml(h.StatusPoprzedni)}</span> → <span class='{GetStatusClass(h.StatusNowy)}'>{EscapeHtml(h.StatusNowy)}</span></td>");
                    sb.AppendLine($"            <td>{EscapeHtml(h.ZmienionePrzez)}</td>");
                    sb.AppendLine($"            <td>{EscapeHtml(h.Komentarz)}</td>");
                    sb.AppendLine($"        </tr>");
                }

                sb.AppendLine("    </tbody>");
                sb.AppendLine("    </table>");
                sb.AppendLine("</div>");
            }

            // Stopka
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("    <p>Dokument wygenerowany automatycznie z systemu zarządzania reklamacjami</p>");
            sb.AppendLine("    <p class='no-print'>Aby wydrukować do PDF, użyj funkcji Drukuj (Ctrl+P) i wybierz \"Zapisz jako PDF\"</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GenerujHtmlZestawienia(List<ReklamacjaData> reklamacje, DateTime dataOd, DateTime dataDo, string status)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <title>Zestawienie reklamacji</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCommonStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Nagłówek
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("    <div class='logo'>UBOJNIA DROBIU PIÓRKOWSCY</div>");
            sb.AppendLine("    <h1>ZESTAWIENIE REKLAMACJI</h1>");
            sb.AppendLine($"    <p class='date-range'>Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}</p>");
            if (!string.IsNullOrEmpty(status))
                sb.AppendLine($"    <p class='filter'>Filtr: {status}</p>");
            sb.AppendLine($"    <p class='generated'>Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.AppendLine("</div>");

            // Podsumowanie
            var sumaKg = 0m;
            foreach (var r in reklamacje) sumaKg += r.SumaKg;

            var statusy = new Dictionary<string, int>();
            foreach (var r in reklamacje)
            {
                if (!statusy.ContainsKey(r.Status)) statusy[r.Status] = 0;
                statusy[r.Status]++;
            }

            sb.AppendLine("<div class='summary'>");
            sb.AppendLine($"    <div class='summary-item'><span>Liczba reklamacji:</span> <strong>{reklamacje.Count}</strong></div>");
            sb.AppendLine($"    <div class='summary-item'><span>Suma kg:</span> <strong>{sumaKg:N2}</strong></div>");
            foreach (var s in statusy)
            {
                sb.AppendLine($"    <div class='summary-item'><span>{s.Key}:</span> <strong>{s.Value}</strong></div>");
            }
            sb.AppendLine("</div>");

            // Tabela
            sb.AppendLine("<table>");
            sb.AppendLine("<thead>");
            sb.AppendLine("    <tr>");
            sb.AppendLine("        <th>ID</th>");
            sb.AppendLine("        <th>Data</th>");
            sb.AppendLine("        <th>Nr dokumentu</th>");
            sb.AppendLine("        <th>Kontrahent</th>");
            sb.AppendLine("        <th class='number'>Suma kg</th>");
            sb.AppendLine("        <th>Status</th>");
            sb.AppendLine("        <th>Zgłosił</th>");
            sb.AppendLine("        <th>Rozpatruje</th>");
            sb.AppendLine("    </tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            foreach (var r in reklamacje)
            {
                sb.AppendLine("    <tr>");
                sb.AppendLine($"        <td>{r.Id}</td>");
                sb.AppendLine($"        <td>{r.DataZgloszenia:dd.MM.yyyy}</td>");
                sb.AppendLine($"        <td>{EscapeHtml(r.NumerDokumentu)}</td>");
                sb.AppendLine($"        <td>{EscapeHtml(r.NazwaKontrahenta)}</td>");
                sb.AppendLine($"        <td class='number'>{r.SumaKg:N2}</td>");
                sb.AppendLine($"        <td><span class='{GetStatusClass(r.Status)}'>{EscapeHtml(r.Status)}</span></td>");
                sb.AppendLine($"        <td>{EscapeHtml(r.UserID)}</td>");
                sb.AppendLine($"        <td>{EscapeHtml(r.OsobaRozpatrujaca)}</td>");
                sb.AppendLine("    </tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            // Stopka
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("    <p>Dokument wygenerowany automatycznie z systemu zarządzania reklamacjami</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GetCommonStyles()
        {
            return @"
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            margin: 0;
            padding: 20px;
            color: #333;
            font-size: 12px;
        }
        .header {
            text-align: center;
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid #2c3e50;
        }
        .logo {
            font-size: 24px;
            font-weight: bold;
            color: #2c3e50;
            margin-bottom: 10px;
        }
        h1 { margin: 10px 0; color: #333; font-size: 20px; }
        h3 { color: #2c3e50; border-bottom: 1px solid #ddd; padding-bottom: 8px; margin-top: 25px; font-size: 14px; }
        .date-range { color: #666; font-size: 13px; }
        .filter { color: #3498db; font-size: 12px; }
        .generated { color: #999; font-size: 11px; }

        .status-banner {
            padding: 15px 20px;
            margin: 15px 0;
            border-radius: 6px;
            text-align: center;
        }
        .status-banner .status-label { font-size: 11px; opacity: 0.8; }
        .status-banner .status-value { font-size: 18px; font-weight: bold; margin-left: 10px; }
        .status-nowa { background: #3498db; color: white; }
        .status-wtrakcie { background: #f39c12; color: white; }
        .status-zaakceptowana { background: #27ae60; color: white; }
        .status-odrzucona { background: #e74c3c; color: white; }
        .status-zamknieta { background: #7f8c8d; color: white; }

        .section { margin-bottom: 20px; }
        .info-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
        .info-row { display: flex; padding: 6px 0; border-bottom: 1px solid #f0f0f0; }
        .info-row .label { color: #666; width: 140px; flex-shrink: 0; }
        .info-row .value { font-weight: 500; }

        .text-box {
            background: #f8f9fa;
            padding: 15px;
            border-radius: 6px;
            border-left: 4px solid #3498db;
            line-height: 1.6;
        }
        .text-box.resolution { border-left-color: #27ae60; }

        .party-list { margin: 0; padding-left: 20px; }
        .party-list li { padding: 5px 0; }

        .summary {
            display: flex;
            justify-content: space-around;
            flex-wrap: wrap;
            background: #f5f5f5;
            padding: 15px;
            border-radius: 8px;
            margin: 15px 0;
        }
        .summary-item { text-align: center; padding: 5px 15px; }
        .summary-item span { display: block; color: #666; font-size: 11px; }
        .summary-item strong { font-size: 16px; }

        table { width: 100%; border-collapse: collapse; margin-top: 10px; }
        th { background: #2c3e50; color: white; padding: 10px 8px; text-align: left; font-weight: 600; font-size: 11px; }
        th.number { text-align: right; }
        td { padding: 8px; border-bottom: 1px solid #eee; }
        td.number { text-align: right; font-family: 'Consolas', monospace; }
        tr:nth-child(even) { background: #fafafa; }
        tr:hover { background: #f0f0f0; }
        .summary-row { background: #e8e8e8 !important; font-weight: bold; }

        .footer {
            margin-top: 30px;
            padding-top: 15px;
            border-top: 1px solid #ddd;
            text-align: center;
            color: #999;
            font-size: 10px;
        }
        .no-print { color: #3498db; font-style: italic; }

        @media print {
            body { padding: 0; }
            .no-print { display: none; }
            .status-banner { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        }
            ";
        }

        private string GetStatusClass(string status)
        {
            if (string.IsNullOrEmpty(status)) return "";
            var s = status.ToLower().Replace(" ", "");
            switch (s)
            {
                case "nowa": return "status-nowa";
                case "wtrakcie": return "status-wtrakcie";
                case "zaakceptowana": return "status-zaakceptowana";
                case "odrzucona": return "status-odrzucona";
                case "zamknieta": case "zamknięta": return "status-zamknieta";
                default: return "";
            }
        }

        #endregion

        #region Helpers

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return WebUtility.HtmlEncode(text);
        }

        private string GetSafeString(SqlDataReader reader, string columnName)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(i))
                        return reader[i].ToString();
                }
            }
            catch { }
            return "";
        }

        private decimal GetSafeDecimal(SqlDataReader reader, string columnName)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(i))
                        return Convert.ToDecimal(reader[i]);
                }
            }
            catch { }
            return 0;
        }

        private DateTime GetSafeDateTime(SqlDataReader reader, string columnName)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(i))
                        return Convert.ToDateTime(reader[i]);
                }
            }
            catch { }
            return DateTime.MinValue;
        }

        #endregion

        #region Data classes

        private class ReklamacjaData
        {
            public int Id { get; set; }
            public DateTime DataZgloszenia { get; set; }
            public string NumerDokumentu { get; set; }
            public string NazwaKontrahenta { get; set; }
            public string IdKontrahenta { get; set; }
            public string Opis { get; set; }
            public string Status { get; set; }
            public decimal SumaKg { get; set; }
            public string UserID { get; set; }
            public string OsobaRozpatrujaca { get; set; }
            public string Komentarz { get; set; }
            public string Rozwiazanie { get; set; }
            public DateTime? DataZamkniecia { get; set; }
        }

        private class TowarData
        {
            public string NazwaTowaru { get; set; }
            public decimal IloscKg { get; set; }
            public string IdTowaru { get; set; }
        }

        private class HistoriaData
        {
            public DateTime DataZmiany { get; set; }
            public string StatusPoprzedni { get; set; }
            public string StatusNowy { get; set; }
            public string ZmienionePrzez { get; set; }
            public string Komentarz { get; set; }
        }

        #endregion
    }
}
