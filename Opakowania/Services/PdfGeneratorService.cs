using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis generowania raportów PDF dla opakowań
    /// Wykorzystuje bibliotekę iTextSharp lub QuestPDF (do wyboru)
    /// Ten plik zawiera podstawową implementację z QuestPDF
    /// </summary>
    public class PdfGeneratorService
    {
        private readonly string _outputDirectory;

        public PdfGeneratorService()
        {
            _outputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "OpakowaniaRaporty");

            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        /// <summary>
        /// Generuje raport zestawienia sald dla wybranego typu opakowania
        /// </summary>
        public string GenerujZestawienieSald(
            IEnumerable<ZestawienieSalda> zestawienie,
            TypOpakowania typOpakowania,
            DateTime dataOd,
            DateTime dataDo)
        {
            var fileName = $"Zestawienie_{typOpakowania.Kod}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_outputDirectory, fileName);

            // Generowanie HTML do PDF (prostsze rozwiązanie bez zewnętrznych bibliotek)
            var html = GenerujHtmlZestawienia(zestawienie, typOpakowania, dataOd, dataDo);

            // Zapisz jako HTML (można później skonwertować do PDF)
            var htmlPath = Path.ChangeExtension(filePath, ".html");
            File.WriteAllText(htmlPath, html, Encoding.UTF8);

            return htmlPath;
        }

        /// <summary>
        /// Generuje raport szczegółowy salda dla kontrahenta
        /// </summary>
        public string GenerujRaportKontrahenta(
            string kontrahentNazwa,
            SaldoOpakowania saldoAktualne,
            IEnumerable<DokumentOpakowania> dokumenty,
            IEnumerable<PotwierdzenieSalda> potwierdzenia,
            DateTime dataOd,
            DateTime dataDo)
        {
            var fileName = $"Saldo_{SanitizeFileName(kontrahentNazwa)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_outputDirectory, fileName);

            var html = GenerujHtmlRaportuKontrahenta(
                kontrahentNazwa, saldoAktualne, dokumenty, potwierdzenia, dataOd, dataDo);

            var htmlPath = Path.ChangeExtension(filePath, ".html");
            File.WriteAllText(htmlPath, html, Encoding.UTF8);

            return htmlPath;
        }

        /// <summary>
        /// Generuje potwierdzenie salda do wydruku/wysłania
        /// </summary>
        public string GenerujPotwierdzenieSalda(
            string kontrahentNazwa,
            TypOpakowania typOpakowania,
            int saldoSystemowe,
            DateTime dataPotwierdzenia)
        {
            var fileName = $"Potwierdzenie_{typOpakowania.Kod}_{SanitizeFileName(kontrahentNazwa)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(_outputDirectory, fileName);

            var html = GenerujHtmlPotwierdzenia(kontrahentNazwa, typOpakowania, saldoSystemowe, dataPotwierdzenia);

            var htmlPath = Path.ChangeExtension(filePath, ".html");
            File.WriteAllText(htmlPath, html, Encoding.UTF8);

            return htmlPath;
        }

        #region Generatory HTML

        private string GenerujHtmlZestawienia(
            IEnumerable<ZestawienieSalda> zestawienie,
            TypOpakowania typOpakowania,
            DateTime dataOd,
            DateTime dataDo)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <title>Zestawienie sald opakowań</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCommonStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Nagłówek
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("    <div class='logo'>PRONOVA SP. Z O.O.</div>");
            sb.AppendLine($"    <h1>Zestawienie sald opakowań - {typOpakowania.Nazwa}</h1>");
            sb.AppendLine($"    <p class='date-range'>Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}</p>");
            sb.AppendLine($"    <p class='generated'>Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.AppendLine("</div>");

            // Podsumowanie
            var lista = zestawienie.ToList();
            var sumaDodatnich = lista.Where(x => x.IloscDrugiZakres > 0).Sum(x => x.IloscDrugiZakres);
            var sumaUjemnych = lista.Where(x => x.IloscDrugiZakres < 0).Sum(x => x.IloscDrugiZakres);
            var liczbaPotwierdzen = lista.Count(x => x.JestPotwierdzone);

            sb.AppendLine("<div class='summary'>");
            sb.AppendLine($"    <div class='summary-item'><span>Liczba kontrahentów:</span> <strong>{lista.Count}</strong></div>");
            sb.AppendLine($"    <div class='summary-item positive'><span>Winni nam (suma):</span> <strong>+{sumaDodatnich}</strong></div>");
            sb.AppendLine($"    <div class='summary-item negative'><span>My winni (suma):</span> <strong>{sumaUjemnych}</strong></div>");
            sb.AppendLine($"    <div class='summary-item'><span>Potwierdzone:</span> <strong>{liczbaPotwierdzen}/{lista.Count}</strong></div>");
            sb.AppendLine("</div>");

            // Tabela
            sb.AppendLine("<table>");
            sb.AppendLine("<thead>");
            sb.AppendLine("    <tr>");
            sb.AppendLine("        <th>Lp.</th>");
            sb.AppendLine("        <th>Kontrahent</th>");
            sb.AppendLine("        <th>Handlowiec</th>");
            sb.AppendLine("        <th class='number'>Saldo początkowe</th>");
            sb.AppendLine("        <th class='number'>Saldo końcowe</th>");
            sb.AppendLine("        <th class='number'>Zmiana</th>");
            sb.AppendLine("        <th>Ostatni dok.</th>");
            sb.AppendLine("        <th>Potwierdzenie</th>");
            sb.AppendLine("    </tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            int lp = 1;
            foreach (var item in lista.OrderByDescending(x => Math.Abs(x.IloscDrugiZakres)))
            {
                var saldoClass = item.IloscDrugiZakres > 0 ? "positive" : (item.IloscDrugiZakres < 0 ? "negative" : "");
                var potwierdzenieText = item.JestPotwierdzone ? $"✓ {item.DataPotwierdzeniaTekst}" : "-";

                sb.AppendLine("    <tr>");
                sb.AppendLine($"        <td>{lp++}</td>");
                sb.AppendLine($"        <td>{EscapeHtml(item.Kontrahent)}</td>");
                sb.AppendLine($"        <td>{EscapeHtml(item.Handlowiec)}</td>");
                sb.AppendLine($"        <td class='number'>{item.IloscPierwszyZakresTekst}</td>");
                sb.AppendLine($"        <td class='number {saldoClass}'><strong>{item.IloscDrugiZakresTekst}</strong></td>");
                sb.AppendLine($"        <td class='number'>{item.RoznicaTekst}</td>");
                sb.AppendLine($"        <td>{item.DataOstatniegoDokumentuTekst}</td>");
                sb.AppendLine($"        <td class='center'>{potwierdzenieText}</td>");
                sb.AppendLine("    </tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            // Stopka
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("    <p>Dokument wygenerowany automatycznie z systemu zarządzania opakowaniami</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GenerujHtmlRaportuKontrahenta(
            string kontrahentNazwa,
            SaldoOpakowania saldoAktualne,
            IEnumerable<DokumentOpakowania> dokumenty,
            IEnumerable<PotwierdzenieSalda> potwierdzenia,
            DateTime dataOd,
            DateTime dataDo)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <title>Raport salda opakowań</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCommonStyles());
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Nagłówek
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("    <div class='logo'>PRONOVA SP. Z O.O.</div>");
            sb.AppendLine($"    <h1>Raport salda opakowań</h1>");
            sb.AppendLine($"    <h2>{EscapeHtml(kontrahentNazwa)}</h2>");
            sb.AppendLine($"    <p class='date-range'>Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}</p>");
            sb.AppendLine($"    <p class='generated'>Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.AppendLine("</div>");

            // Aktualne salda
            if (saldoAktualne != null)
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Aktualne salda opakowań</h3>");
                sb.AppendLine("    <div class='cards'>");

                AppendSaldoCard(sb, "Pojemnik E2", saldoAktualne.SaldoE2);
                AppendSaldoCard(sb, "Paleta H1", saldoAktualne.SaldoH1);
                AppendSaldoCard(sb, "Paleta EURO", saldoAktualne.SaldoEURO);
                AppendSaldoCard(sb, "Paleta PCV", saldoAktualne.SaldoPCV);
                AppendSaldoCard(sb, "Paleta DREW", saldoAktualne.SaldoDREW);

                sb.AppendLine("    </div>");
                sb.AppendLine("</div>");
            }

            // Historia dokumentów
            var dokumentyList = dokumenty?.ToList() ?? new List<DokumentOpakowania>();
            if (dokumentyList.Any())
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Historia dokumentów</h3>");
                sb.AppendLine("    <table>");
                sb.AppendLine("    <thead>");
                sb.AppendLine("        <tr>");
                sb.AppendLine("            <th>Data</th>");
                sb.AppendLine("            <th>Nr dokumentu</th>");
                sb.AppendLine("            <th>Opis</th>");
                sb.AppendLine("            <th class='number'>E2</th>");
                sb.AppendLine("            <th class='number'>H1</th>");
                sb.AppendLine("            <th class='number'>EURO</th>");
                sb.AppendLine("            <th class='number'>PCV</th>");
                sb.AppendLine("            <th class='number'>DREW</th>");
                sb.AppendLine("        </tr>");
                sb.AppendLine("    </thead>");
                sb.AppendLine("    <tbody>");

                foreach (var dok in dokumentyList)
                {
                    sb.AppendLine("        <tr>");
                    sb.AppendLine($"            <td>{dok.DataText}</td>");
                    sb.AppendLine($"            <td>{EscapeHtml(dok.NrDok)}</td>");
                    sb.AppendLine($"            <td>{EscapeHtml(dok.Dokumenty)}</td>");
                    sb.AppendLine($"            <td class='number {GetSaldoClass(dok.E2)}'>{FormatSaldo(dok.E2)}</td>");
                    sb.AppendLine($"            <td class='number {GetSaldoClass(dok.H1)}'>{FormatSaldo(dok.H1)}</td>");
                    sb.AppendLine($"            <td class='number {GetSaldoClass(dok.EURO)}'>{FormatSaldo(dok.EURO)}</td>");
                    sb.AppendLine($"            <td class='number {GetSaldoClass(dok.PCV)}'>{FormatSaldo(dok.PCV)}</td>");
                    sb.AppendLine($"            <td class='number {GetSaldoClass(dok.DREW)}'>{FormatSaldo(dok.DREW)}</td>");
                    sb.AppendLine("        </tr>");
                }

                sb.AppendLine("    </tbody>");
                sb.AppendLine("    </table>");
                sb.AppendLine("</div>");
            }

            // Historia potwierdzeń
            var potwierdzeniList = potwierdzenia?.ToList() ?? new List<PotwierdzenieSalda>();
            if (potwierdzeniList.Any())
            {
                sb.AppendLine("<div class='section'>");
                sb.AppendLine("    <h3>Historia potwierdzeń sald</h3>");
                sb.AppendLine("    <table>");
                sb.AppendLine("    <thead>");
                sb.AppendLine("        <tr>");
                sb.AppendLine("            <th>Data</th>");
                sb.AppendLine("            <th>Typ opakowania</th>");
                sb.AppendLine("            <th class='number'>Potwierdzone</th>");
                sb.AppendLine("            <th class='number'>W systemie</th>");
                sb.AppendLine("            <th class='number'>Różnica</th>");
                sb.AppendLine("            <th>Status</th>");
                sb.AppendLine("            <th>Wprowadził</th>");
                sb.AppendLine("        </tr>");
                sb.AppendLine("    </thead>");
                sb.AppendLine("    <tbody>");

                foreach (var pot in potwierdzeniList)
                {
                    sb.AppendLine("        <tr>");
                    sb.AppendLine($"            <td>{pot.DataPotwierdzeniaText}</td>");
                    sb.AppendLine($"            <td>{EscapeHtml(pot.TypOpakowania)}</td>");
                    sb.AppendLine($"            <td class='number'>{pot.IloscPotwierdzona}</td>");
                    sb.AppendLine($"            <td class='number'>{pot.SaldoSystemowe}</td>");
                    sb.AppendLine($"            <td class='number {GetSaldoClass(pot.Roznica)}'>{FormatSaldo(pot.Roznica)}</td>");
                    sb.AppendLine($"            <td>{EscapeHtml(pot.StatusPotwierdzenia)}</td>");
                    sb.AppendLine($"            <td>{EscapeHtml(pot.UzytkownikNazwa)}</td>");
                    sb.AppendLine("        </tr>");
                }

                sb.AppendLine("    </tbody>");
                sb.AppendLine("    </table>");
                sb.AppendLine("</div>");
            }

            // Stopka
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("    <p>Dokument wygenerowany automatycznie z systemu zarządzania opakowaniami</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GenerujHtmlPotwierdzenia(
            string kontrahentNazwa,
            TypOpakowania typOpakowania,
            int saldoSystemowe,
            DateTime dataPotwierdzenia)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <title>Potwierdzenie salda opakowań</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine(GetCommonStyles());
            sb.AppendLine(@"
        .confirmation-box {
            border: 2px solid #4B833C;
            border-radius: 8px;
            padding: 30px;
            margin: 30px auto;
            max-width: 600px;
            background: #f8fff8;
        }
        .confirmation-title {
            font-size: 24px;
            font-weight: bold;
            text-align: center;
            color: #4B833C;
            margin-bottom: 30px;
        }
        .confirmation-field {
            display: flex;
            justify-content: space-between;
            padding: 12px 0;
            border-bottom: 1px solid #e0e0e0;
        }
        .confirmation-field:last-child {
            border-bottom: none;
        }
        .field-label {
            color: #666;
        }
        .field-value {
            font-weight: bold;
        }
        .saldo-box {
            text-align: center;
            padding: 20px;
            background: white;
            border-radius: 8px;
            margin: 20px 0;
        }
        .saldo-label {
            font-size: 14px;
            color: #666;
            margin-bottom: 10px;
        }
        .saldo-value {
            font-size: 48px;
            font-weight: bold;
        }
        .saldo-positive { color: #CC2F37; }
        .saldo-negative { color: #4B833C; }
        .signature-section {
            margin-top: 50px;
            display: flex;
            justify-content: space-between;
        }
        .signature-box {
            text-align: center;
            width: 45%;
        }
        .signature-line {
            border-top: 1px solid #333;
            margin-top: 60px;
            padding-top: 10px;
            font-size: 12px;
            color: #666;
        }
            ");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Nagłówek
            sb.AppendLine("<div class='header'>");
            sb.AppendLine("    <div class='logo'>PRONOVA SP. Z O.O.</div>");
            sb.AppendLine("    <p>ul. Przykładowa 123, 00-000 Miasto</p>");
            sb.AppendLine("    <p>NIP: 000-000-00-00</p>");
            sb.AppendLine("</div>");

            // Treść potwierdzenia
            sb.AppendLine("<div class='confirmation-box'>");
            sb.AppendLine("    <div class='confirmation-title'>POTWIERDZENIE SALDA OPAKOWAŃ</div>");

            sb.AppendLine("    <div class='confirmation-field'>");
            sb.AppendLine("        <span class='field-label'>Kontrahent:</span>");
            sb.AppendLine($"        <span class='field-value'>{EscapeHtml(kontrahentNazwa)}</span>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <div class='confirmation-field'>");
            sb.AppendLine("        <span class='field-label'>Typ opakowania:</span>");
            sb.AppendLine($"        <span class='field-value'>{EscapeHtml(typOpakowania.Nazwa)}</span>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <div class='confirmation-field'>");
            sb.AppendLine("        <span class='field-label'>Stan na dzień:</span>");
            sb.AppendLine($"        <span class='field-value'>{dataPotwierdzenia:dd.MM.yyyy}</span>");
            sb.AppendLine("    </div>");

            var saldoClass = saldoSystemowe > 0 ? "saldo-positive" : "saldo-negative";
            var saldoText = saldoSystemowe > 0 ? $"+{saldoSystemowe}" : saldoSystemowe.ToString();
            var saldoOpis = saldoSystemowe > 0 
                ? "opakowań do zwrotu przez kontrahenta" 
                : (saldoSystemowe < 0 ? "opakowań do wydania kontrahentowi" : "brak należności");

            sb.AppendLine("    <div class='saldo-box'>");
            sb.AppendLine("        <div class='saldo-label'>SALDO OPAKOWAŃ</div>");
            sb.AppendLine($"        <div class='saldo-value {saldoClass}'>{saldoText}</div>");
            sb.AppendLine($"        <div class='saldo-label'>{saldoOpis}</div>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <p style='text-align: center; margin-top: 20px; color: #666; font-size: 14px;'>");
            sb.AppendLine("        Prosimy o potwierdzenie powyższego salda opakowań.<br/>");
            sb.AppendLine("        W przypadku rozbieżności prosimy o kontakt.");
            sb.AppendLine("    </p>");

            sb.AppendLine("    <div class='signature-section'>");
            sb.AppendLine("        <div class='signature-box'>");
            sb.AppendLine("            <div class='signature-line'>Podpis i pieczęć wystawcy</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class='signature-box'>");
            sb.AppendLine("            <div class='signature-line'>Podpis i pieczęć kontrahenta</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");

            sb.AppendLine("</div>");

            // Stopka
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine($"    <p>Dokument wygenerowany: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        #endregion

        #region Helpers

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
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 2px solid #4B833C;
        }
        .logo {
            font-size: 24px;
            font-weight: bold;
            color: #4B833C;
            margin-bottom: 10px;
        }
        h1 {
            margin: 10px 0;
            color: #333;
            font-size: 20px;
        }
        h2 {
            margin: 5px 0;
            color: #666;
            font-size: 16px;
            font-weight: normal;
        }
        h3 {
            color: #4B833C;
            border-bottom: 1px solid #ddd;
            padding-bottom: 10px;
            margin-top: 30px;
        }
        .date-range {
            color: #666;
            font-size: 14px;
        }
        .generated {
            color: #999;
            font-size: 11px;
        }
        .summary {
            display: flex;
            justify-content: space-around;
            background: #f5f5f5;
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 20px;
        }
        .summary-item {
            text-align: center;
        }
        .summary-item span {
            display: block;
            color: #666;
            font-size: 11px;
        }
        .summary-item strong {
            font-size: 18px;
        }
        .summary-item.positive strong { color: #CC2F37; }
        .summary-item.negative strong { color: #4B833C; }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
        }
        th {
            background: #4B833C;
            color: white;
            padding: 10px 8px;
            text-align: left;
            font-weight: 600;
            font-size: 11px;
        }
        th.number {
            text-align: right;
        }
        td {
            padding: 8px;
            border-bottom: 1px solid #eee;
        }
        td.number {
            text-align: right;
            font-family: 'Consolas', monospace;
        }
        td.center {
            text-align: center;
        }
        tr:nth-child(even) {
            background: #fafafa;
        }
        tr:hover {
            background: #f0f0f0;
        }
        .positive { color: #CC2F37; }
        .negative { color: #4B833C; }
        .section {
            margin-bottom: 30px;
        }
        .cards {
            display: flex;
            justify-content: space-between;
            gap: 15px;
            flex-wrap: wrap;
        }
        .card {
            flex: 1;
            min-width: 120px;
            background: #f8f8f8;
            border-radius: 8px;
            padding: 15px;
            text-align: center;
        }
        .card-label {
            font-size: 11px;
            color: #666;
            margin-bottom: 8px;
        }
        .card-value {
            font-size: 24px;
            font-weight: bold;
        }
        .footer {
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #ddd;
            text-align: center;
            color: #999;
            font-size: 10px;
        }
        @media print {
            body { padding: 0; }
            .no-print { display: none; }
        }
            ";
        }

        private void AppendSaldoCard(StringBuilder sb, string label, int value)
        {
            var colorClass = value > 0 ? "positive" : (value < 0 ? "negative" : "");
            var valueText = value == 0 ? "0" : (value > 0 ? $"+{value}" : value.ToString());

            sb.AppendLine($"        <div class='card'>");
            sb.AppendLine($"            <div class='card-label'>{label}</div>");
            sb.AppendLine($"            <div class='card-value {colorClass}'>{valueText}</div>");
            sb.AppendLine($"        </div>");
        }

        private string GetSaldoClass(int value)
        {
            return value > 0 ? "positive" : (value < 0 ? "negative" : "");
        }

        private string FormatSaldo(int value)
        {
            if (value == 0) return "-";
            return value > 0 ? $"+{value}" : value.ToString();
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return System.Net.WebUtility.HtmlEncode(text);
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        #endregion
    }
}
