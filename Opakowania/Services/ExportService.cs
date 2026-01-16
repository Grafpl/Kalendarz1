using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do eksportu danych do PDF i Excel
    /// </summary>
    public class ExportService
    {
        private readonly string _sciezkaPDF1 = @"\\192.168.0.170\Public\Salda Opakowan";
        private readonly string _sciezkaPDF2 = @"\\192.168.0.171\Public\Salda Opakowan";
        private readonly PdfReportService _pdfService;

        public ExportService()
        {
            _pdfService = new PdfReportService();
        }

        public ExportService(string sciezkaPDF1, string sciezkaPDF2)
        {
            _sciezkaPDF1 = sciezkaPDF1;
            _sciezkaPDF2 = sciezkaPDF2;
            _pdfService = new PdfReportService();
        }

        #region Ścieżki zapisu

        /// <summary>
        /// Pobiera dostępną ścieżkę do zapisu PDF
        /// </summary>
        public string GetSciezkaZapisu()
        {
            // Najpierw próbuj główną ścieżkę
            try
            {
                if (Directory.Exists(_sciezkaPDF1))
                {
                    // Sprawdź czy możemy zapisywać
                    string testFile = Path.Combine(_sciezkaPDF1, $"test_{Guid.NewGuid()}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    return _sciezkaPDF1;
                }
            }
            catch { }

            // Próbuj zapasową ścieżkę
            try
            {
                if (Directory.Exists(_sciezkaPDF2))
                {
                    string testFile = Path.Combine(_sciezkaPDF2, $"test_{Guid.NewGuid()}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    return _sciezkaPDF2;
                }
            }
            catch { }

            // Fallback do lokalnego folderu
            string localPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Salda Opakowan");
            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }
            return localPath;
        }

        /// <summary>
        /// Generuje nazwę pliku na podstawie kontrahenta i daty
        /// </summary>
        public string GenerujNazwePliku(string kontrahent, string typ = "Saldo")
        {
            string bezpiecznaNazwa = string.Join("_", kontrahent.Split(Path.GetInvalidFileNameChars()));
            return $"{typ}_{bezpiecznaNazwa}_{DateTime.Now:yyyy-MM-dd_HH-mm}.pdf";
        }

        #endregion

        #region Eksport PDF

        /// <summary>
        /// Eksportuje saldo kontrahenta do PDF
        /// </summary>
        public async Task<string> EksportujSaldoKontrahentaDoPDFAsync(
            string kontrahent,
            int kontrahentId,
            SaldoOpakowania saldo,
            List<DokumentOpakowania> dokumenty,
            DateTime dataOd,
            DateTime dataDo)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Użyj PdfReportService do wygenerowania prawdziwego PDF
                    string pdfPath = _pdfService.GenerujRaportKontrahenta(
                        kontrahentId,
                        kontrahent,
                        saldo,
                        dokumenty,
                        null, // potwierdzenia - można dodać później
                        dataOd,
                        dataDo);

                    // Przenieś plik do właściwego folderu
                    string sciezkaDocelowa = GetSciezkaZapisu();
                    string nazwaPliku = GenerujNazwePliku(kontrahent, "Saldo");
                    string pelnaSciezkaDocelowa = Path.Combine(sciezkaDocelowa, nazwaPliku);

                    // Skopiuj plik z lokalizacji tymczasowej do docelowej
                    if (File.Exists(pdfPath))
                    {
                        File.Copy(pdfPath, pelnaSciezkaDocelowa, true);
                        File.Delete(pdfPath); // Usuń plik tymczasowy
                        return pelnaSciezkaDocelowa;
                    }

                    return pdfPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd podczas eksportu do PDF: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Eksportuje listę sald wszystkich kontrahentów do PDF
        /// </summary>
        public async Task<string> EksportujSaldaWszystkichDoPDFAsync(
            List<SaldoOpakowania> salda,
            DateTime dataDo,
            string handlowiec = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string sciezka = GetSciezkaZapisu();
                    string nazwaPliku = $"Salda_Wszystkich_{DateTime.Now:yyyy-MM-dd_HH-mm}.html";
                    string pelnaSciezka = Path.Combine(sciezka, nazwaPliku);

                    string html = GenerujHTMLSaldaWszystkich(salda, dataDo, handlowiec);
                    File.WriteAllText(pelnaSciezka, html, Encoding.UTF8);

                    return pelnaSciezka;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd podczas eksportu do PDF: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Eksportuje zestawienie sald do prawdziwego PDF
        /// </summary>
        public async Task<string> EksportujZestawienieDoPDFAsync(
            List<ZestawienieSalda> zestawienie,
            TypOpakowania typOpakowania,
            DateTime dataOd,
            DateTime dataDo,
            string handlowiec = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Użyj PdfReportService do wygenerowania prawdziwego PDF
                    string pdfPath = _pdfService.GenerujRaportZestawienia(
                        zestawienie,
                        typOpakowania,
                        dataOd,
                        dataDo,
                        handlowiec);

                    // Przenieś plik do właściwego folderu
                    string sciezkaDocelowa = GetSciezkaZapisu();
                    string nazwaPliku = $"Zestawienie_{typOpakowania.Kod}_{DateTime.Now:yyyy-MM-dd_HH-mm}.pdf";
                    string pelnaSciezkaDocelowa = Path.Combine(sciezkaDocelowa, nazwaPliku);

                    // Skopiuj plik z lokalizacji tymczasowej do docelowej
                    if (File.Exists(pdfPath))
                    {
                        File.Copy(pdfPath, pelnaSciezkaDocelowa, true);
                        File.Delete(pdfPath); // Usuń plik tymczasowy
                        return pelnaSciezkaDocelowa;
                    }

                    return pdfPath;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd podczas eksportu zestawienia do PDF: {ex.Message}", ex);
                }
            });
        }

        private string GenerujHTMLSaldaKontrahenta(
            string kontrahent,
            SaldoOpakowania saldo,
            List<DokumentOpakowania> dokumenty,
            DateTime dataOd,
            DateTime dataDo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Saldo opakowań - " + kontrahent + "</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #1e1e1e; color: #ffffff; }
                h1 { color: #4CAF50; margin-bottom: 5px; }
                h2 { color: #cccccc; font-weight: normal; margin-top: 0; }
                .info { margin-bottom: 20px; color: #9d9d9d; }
                table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
                th { background: #333337; color: #cccccc; padding: 12px; text-align: left; border: 1px solid #3f3f46; }
                td { padding: 10px 12px; border: 1px solid #3f3f46; }
                tr:nth-child(even) { background: #2a2a2e; }
                tr:nth-child(odd) { background: #333337; }
                .saldo-row { background: #4a3f00 !important; font-weight: bold; }
                .wydane { color: #f44336; }
                .zwrot { color: #4CAF50; }
                .suma-box { display: inline-block; padding: 10px 20px; margin: 5px; border-radius: 8px; text-align: center; }
                .suma-e2 { background: #1e3a5f; border: 1px solid #3B82F6; }
                .suma-h1 { background: #4a2c17; border: 1px solid #F97316; }
                .suma-euro { background: #1a3d1c; border: 1px solid #10B981; }
                .suma-pcv { background: #2d1f4a; border: 1px solid #8B5CF6; }
                .suma-drew { background: #4a3517; border: 1px solid #F59E0B; }
                .footer { margin-top: 30px; padding-top: 20px; border-top: 1px solid #3f3f46; color: #9d9d9d; font-size: 12px; }
                @media print { 
                    body { background: white; color: black; }
                    th { background: #f0f0f0; color: black; }
                    td { color: black; }
                    tr:nth-child(even) { background: #f9f9f9; }
                    tr:nth-child(odd) { background: white; }
                    .saldo-row { background: #fff3cd !important; }
                }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            
            sb.AppendLine($"<h1>Saldo opakowań</h1>");
            sb.AppendLine($"<h2>{kontrahent}</h2>");
            sb.AppendLine($"<p class='info'>Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy} | Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");

            // Podsumowanie sald
            sb.AppendLine("<div style='margin-bottom: 20px;'>");
            if (saldo != null)
            {
                sb.AppendLine($"<div class='suma-box suma-e2'><div style='font-size:12px;color:#9d9d9d;'>E2</div><div style='font-size:20px;font-weight:bold;'>{saldo.SaldoE2Tekst}</div></div>");
                sb.AppendLine($"<div class='suma-box suma-h1'><div style='font-size:12px;color:#9d9d9d;'>H1</div><div style='font-size:20px;font-weight:bold;'>{saldo.SaldoH1Tekst}</div></div>");
                sb.AppendLine($"<div class='suma-box suma-euro'><div style='font-size:12px;color:#9d9d9d;'>EURO</div><div style='font-size:20px;font-weight:bold;'>{saldo.SaldoEUROTekst}</div></div>");
                sb.AppendLine($"<div class='suma-box suma-pcv'><div style='font-size:12px;color:#9d9d9d;'>PCV</div><div style='font-size:20px;font-weight:bold;'>{saldo.SaldoPCVTekst}</div></div>");
                sb.AppendLine($"<div class='suma-box suma-drew'><div style='font-size:12px;color:#9d9d9d;'>DREW</div><div style='font-size:20px;font-weight:bold;'>{saldo.SaldoDREWTekst}</div></div>");
            }
            sb.AppendLine("</div>");

            // Tabela dokumentów
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>Data</th><th>Nr dokumentu</th><th>E2</th><th>H1</th><th>EURO</th><th>PCV</th><th>DREW</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var dok in dokumenty ?? new List<DokumentOpakowania>())
            {
                string rowClass = dok.JestSaldem ? " class='saldo-row'" : "";
                sb.AppendLine($"<tr{rowClass}>");
                sb.AppendLine($"<td>{dok.DataText}</td>");
                sb.AppendLine($"<td>{dok.NrDok}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(dok.E2)}'>{dok.E2Tekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(dok.H1)}'>{dok.H1Tekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(dok.EURO)}'>{dok.EUROTekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(dok.PCV)}'>{dok.PCVTekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(dok.DREW)}'>{dok.DREWTekst}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");

            // Legenda
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p><strong>Legenda:</strong></p>");
            sb.AppendLine("<p><span class='wydane'>●</span> Czerwony = kontrahent winny nam opakowania (wydane)</p>");
            sb.AppendLine("<p><span class='zwrot'>●</span> Zielony = my winni kontrahentowi (do zwrotu)</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GenerujHTMLSaldaWszystkich(List<SaldoOpakowania> salda, DateTime dataDo, string handlowiec)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Salda wszystkich opakowań</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #1e1e1e; color: #ffffff; }
                h1 { color: #4CAF50; }
                .info { margin-bottom: 20px; color: #9d9d9d; }
                table { border-collapse: collapse; width: 100%; }
                th { background: #333337; color: #cccccc; padding: 10px; text-align: left; border: 1px solid #3f3f46; font-size: 12px; }
                td { padding: 8px 10px; border: 1px solid #3f3f46; font-size: 13px; }
                tr:nth-child(even) { background: #2a2a2e; }
                tr:nth-child(odd) { background: #333337; }
                .wydane { color: #f44336; }
                .zwrot { color: #4CAF50; }
                .warning-row { background: rgba(255,152,0,0.2) !important; }
                .critical-row { background: rgba(244,67,54,0.2) !important; }
                .suma { font-weight: bold; background: #252526 !important; }
                @media print { 
                    body { background: white; color: black; }
                    th { background: #f0f0f0; color: black; }
                    td { color: black; }
                    tr:nth-child(even) { background: #f9f9f9; }
                    tr:nth-child(odd) { background: white; }
                }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("<h1>Salda opakowań wszystkich kontrahentów</h1>");
            sb.AppendLine($"<p class='info'>Stan na dzień: {dataDo:dd.MM.yyyy} | Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}");
            if (!string.IsNullOrEmpty(handlowiec))
                sb.AppendLine($" | Handlowiec: {handlowiec}");
            sb.AppendLine("</p>");

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>Kontrahent</th><th>Handlowiec</th><th>E2</th><th>H1</th><th>EURO</th><th>PCV</th><th>DREW</th><th>RAZEM</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            int sumaE2 = 0, sumaH1 = 0, sumaEURO = 0, sumaPCV = 0, sumaDREW = 0;

            foreach (var s in salda ?? new List<SaldoOpakowania>())
            {
                string rowClass = "";
                if (s.MaxSaldoDodatnie >= SaldoOpakowania.ProgKrytyczny)
                    rowClass = " class='critical-row'";
                else if (s.MaxSaldoDodatnie >= SaldoOpakowania.ProgOstrzezenia)
                    rowClass = " class='warning-row'";

                sb.AppendLine($"<tr{rowClass}>");
                sb.AppendLine($"<td>{s.Kontrahent}</td>");
                sb.AppendLine($"<td>{s.Handlowiec ?? "-"}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(s.SaldoE2)}'>{s.SaldoE2Tekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(s.SaldoH1)}'>{s.SaldoH1Tekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(s.SaldoEURO)}'>{s.SaldoEUROTekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(s.SaldoPCV)}'>{s.SaldoPCVTekst}</td>");
                sb.AppendLine($"<td class='{GetKlasaKoloru(s.SaldoDREW)}'>{s.SaldoDREWTekst}</td>");
                sb.AppendLine($"<td>{s.SaldoCalkowite}</td>");
                sb.AppendLine("</tr>");

                sumaE2 += s.SaldoE2;
                sumaH1 += s.SaldoH1;
                sumaEURO += s.SaldoEURO;
                sumaPCV += s.SaldoPCV;
                sumaDREW += s.SaldoDREW;
            }

            // Wiersz sumy
            sb.AppendLine("<tr class='suma'>");
            sb.AppendLine("<td colspan='2'><strong>SUMA</strong></td>");
            sb.AppendLine($"<td class='{GetKlasaKoloru(sumaE2)}'><strong>{FormatujSaldo(sumaE2)}</strong></td>");
            sb.AppendLine($"<td class='{GetKlasaKoloru(sumaH1)}'><strong>{FormatujSaldo(sumaH1)}</strong></td>");
            sb.AppendLine($"<td class='{GetKlasaKoloru(sumaEURO)}'><strong>{FormatujSaldo(sumaEURO)}</strong></td>");
            sb.AppendLine($"<td class='{GetKlasaKoloru(sumaPCV)}'><strong>{FormatujSaldo(sumaPCV)}</strong></td>");
            sb.AppendLine($"<td class='{GetKlasaKoloru(sumaDREW)}'><strong>{FormatujSaldo(sumaDREW)}</strong></td>");
            sb.AppendLine($"<td><strong>{Math.Abs(sumaE2) + Math.Abs(sumaH1) + Math.Abs(sumaEURO) + Math.Abs(sumaPCV) + Math.Abs(sumaDREW)}</strong></td>");
            sb.AppendLine("</tr>");

            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GetKlasaKoloru(int wartosc)
        {
            if (wartosc > 0) return "wydane";
            if (wartosc < 0) return "zwrot";
            return "";
        }

        private string FormatujSaldo(int saldo)
        {
            if (saldo == 0) return "0";
            if (saldo > 0) return $"{saldo} (wydane)";
            return $"{Math.Abs(saldo)} (zwrot)";
        }

        #endregion

        #region Eksport Excel (CSV)

        /// <summary>
        /// Eksportuje salda wszystkich kontrahentów do Excel (CSV)
        /// </summary>
        public async Task<string> EksportujDoExcelAsync(List<SaldoOpakowania> salda, DateTime dataDo, string handlowiec = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string sciezka = GetSciezkaZapisu();
                    string nazwaPliku = $"Salda_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv";
                    string pelnaSciezka = Path.Combine(sciezka, nazwaPliku);

                    var sb = new StringBuilder();
                    
                    // Nagłówek z BOM dla Excel
                    sb.AppendLine("Kontrahent;Handlowiec;E2;H1;EURO;PCV;DREW;RAZEM");

                    foreach (var s in salda)
                    {
                        sb.AppendLine($"{s.Kontrahent};{s.Handlowiec ?? "-"};{s.SaldoE2};{s.SaldoH1};{s.SaldoEURO};{s.SaldoPCV};{s.SaldoDREW};{s.SaldoCalkowite}");
                    }

                    // Suma
                    int sumaE2 = salda.Sum(x => x.SaldoE2);
                    int sumaH1 = salda.Sum(x => x.SaldoH1);
                    int sumaEURO = salda.Sum(x => x.SaldoEURO);
                    int sumaPCV = salda.Sum(x => x.SaldoPCV);
                    int sumaDREW = salda.Sum(x => x.SaldoDREW);
                    int sumaRazem = salda.Sum(x => x.SaldoCalkowite);

                    sb.AppendLine($"SUMA;;{sumaE2};{sumaH1};{sumaEURO};{sumaPCV};{sumaDREW};{sumaRazem}");

                    // Zapisz z BOM dla poprawnego kodowania w Excel
                    File.WriteAllText(pelnaSciezka, sb.ToString(), new UTF8Encoding(true));

                    return pelnaSciezka;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd podczas eksportu do Excel: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Eksportuje dokumenty kontrahenta do Excel (CSV)
        /// </summary>
        public async Task<string> EksportujDokumentyDoExcelAsync(
            string kontrahent,
            List<DokumentOpakowania> dokumenty,
            DateTime dataOd,
            DateTime dataDo)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string sciezka = GetSciezkaZapisu();
                    string bezpiecznaNazwa = string.Join("_", kontrahent.Split(Path.GetInvalidFileNameChars()));
                    string nazwaPliku = $"Dokumenty_{bezpiecznaNazwa}_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv";
                    string pelnaSciezka = Path.Combine(sciezka, nazwaPliku);

                    var sb = new StringBuilder();
                    sb.AppendLine("Data;Nr dokumentu;Typ;E2;H1;EURO;PCV;DREW");

                    foreach (var d in dokumenty.Where(x => !x.JestSaldem))
                    {
                        sb.AppendLine($"{d.DataText};{d.NrDok};{d.TypDokumentuText};{d.E2};{d.H1};{d.EURO};{d.PCV};{d.DREW}");
                    }

                    File.WriteAllText(pelnaSciezka, sb.ToString(), new UTF8Encoding(true));
                    return pelnaSciezka;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd podczas eksportu do Excel: {ex.Message}", ex);
                }
            });
        }

        #endregion

        #region Drukowanie

        /// <summary>
        /// Otwiera plik w domyślnej aplikacji (do drukowania)
        /// </summary>
        public void OtworzPlik(string sciezka)
        {
            try
            {
                if (File.Exists(sciezka))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = sciezka,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"Plik nie istnieje: {sciezka}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć pliku: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Otwiera folder z plikiem
        /// </summary>
        public void OtworzFolder(string sciezkaPliku)
        {
            try
            {
                if (File.Exists(sciezkaPliku))
                {
                    Process.Start("explorer.exe", $"/select,\"{sciezkaPliku}\"");
                }
                else
                {
                    string folder = Path.GetDirectoryName(sciezkaPliku);
                    if (Directory.Exists(folder))
                    {
                        Process.Start("explorer.exe", folder);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć folderu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Kontakt

        /// <summary>
        /// Otwiera domyślny program do dzwonienia (jeśli dostępny)
        /// </summary>
        public void Zadzwon(string numerTelefonu)
        {
            if (string.IsNullOrWhiteSpace(numerTelefonu) || numerTelefonu == "-")
            {
                MessageBox.Show("Brak numeru telefonu", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Usuń spacje i inne znaki
                string numer = new string(numerTelefonu.Where(c => char.IsDigit(c) || c == '+').ToArray());
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"tel:{numer}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Skopiuj do schowka jako fallback
                try
                {
                    Clipboard.SetText(numerTelefonu);
                    MessageBox.Show($"Numer {numerTelefonu} skopiowany do schowka", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {
                    MessageBox.Show($"Nie można zadzwonić: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Otwiera domyślny program email
        /// </summary>
        public void WyslijEmail(string adresEmail, string temat = "", string tresc = "")
        {
            if (string.IsNullOrWhiteSpace(adresEmail) || adresEmail == "-")
            {
                MessageBox.Show("Brak adresu email", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string mailto = $"mailto:{adresEmail}";
                if (!string.IsNullOrEmpty(temat))
                    mailto += $"?subject={Uri.EscapeDataString(temat)}";
                if (!string.IsNullOrEmpty(tresc))
                    mailto += (mailto.Contains("?") ? "&" : "?") + $"body={Uri.EscapeDataString(tresc)}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = mailto,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Skopiuj do schowka jako fallback
                try
                {
                    Clipboard.SetText(adresEmail);
                    MessageBox.Show($"Adres {adresEmail} skopiowany do schowka", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {
                    MessageBox.Show($"Nie można otworzyć programu email: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Generuje PDF salda kontrahenta i otwiera email z załącznikiem
        /// </summary>
        public async Task<string> GenerujPDFiWyslijEmailAsync(
            string kontrahent,
            int kontrahentId,
            SaldoOpakowania saldo,
            List<DokumentOpakowania> dokumenty,
            DateTime dataOd,
            DateTime dataDo,
            string emailKontrahenta = null)
        {
            // 1. Generuj PDF
            string pdfPath = await EksportujSaldoKontrahentaDoPDFAsync(
                kontrahent, kontrahentId, saldo, dokumenty, dataOd, dataDo);

            // 2. Przygotuj treść emaila
            string dataSalda = dataDo.ToString("dd.MM.yyyy");
            string temat = $"Uzgodnienie salda opakowań zwrotnych na dzień {dataSalda} - {kontrahent}";

            // Buduj treść emaila z konkretnymi danymi
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Szanowni Państwo,");
            sb.AppendLine();
            sb.AppendLine($"W załączeniu przesyłamy zestawienie opakowań zwrotnych na dzień {dataSalda}.");
            sb.AppendLine();
            sb.AppendLine("Stan opakowań według naszej ewidencji:");
            sb.AppendLine();

            // Dodaj konkretne salda
            if (saldo != null)
            {
                if (saldo.SaldoE2 != 0)
                {
                    string statusE2 = saldo.SaldoE2 > 0
                        ? $"Kontrahent winny: {saldo.SaldoE2} szt."
                        : $"Ubojnia winna: {Math.Abs(saldo.SaldoE2)} szt.";
                    sb.AppendLine($"• Pojemniki E2: {statusE2}");
                }
                if (saldo.SaldoH1 != 0)
                {
                    string statusH1 = saldo.SaldoH1 > 0
                        ? $"Kontrahent winny: {saldo.SaldoH1} szt."
                        : $"Ubojnia winna: {Math.Abs(saldo.SaldoH1)} szt.";
                    sb.AppendLine($"• Palety H1: {statusH1}");
                }
                if (saldo.SaldoEURO != 0)
                {
                    string statusEURO = saldo.SaldoEURO > 0
                        ? $"Kontrahent winny: {saldo.SaldoEURO} szt."
                        : $"Ubojnia winna: {Math.Abs(saldo.SaldoEURO)} szt.";
                    sb.AppendLine($"• Palety EURO: {statusEURO}");
                }
                if (saldo.SaldoPCV != 0)
                {
                    string statusPCV = saldo.SaldoPCV > 0
                        ? $"Kontrahent winny: {saldo.SaldoPCV} szt."
                        : $"Ubojnia winna: {Math.Abs(saldo.SaldoPCV)} szt.";
                    sb.AppendLine($"• Palety plastikowe: {statusPCV}");
                }
                if (saldo.SaldoDREW != 0)
                {
                    string statusDREW = saldo.SaldoDREW > 0
                        ? $"Kontrahent winny: {saldo.SaldoDREW} szt."
                        : $"Ubojnia winna: {Math.Abs(saldo.SaldoDREW)} szt.";
                    sb.AppendLine($"• Palety drewniane: {statusDREW}");
                }

                // Jeśli wszystkie salda są zerowe
                if (saldo.SaldoE2 == 0 && saldo.SaldoH1 == 0 && saldo.SaldoEURO == 0 && saldo.SaldoPCV == 0 && saldo.SaldoDREW == 0)
                {
                    sb.AppendLine("• Wszystkie salda są rozliczone (0)");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Prosimy o weryfikację powyższych danych i potwierdzenie ich zgodności.");
            sb.AppendLine("W przypadku rozbieżności prosimy o kontakt.");
            sb.AppendLine();
            sb.AppendLine("W razie braku odpowiedzi w ciągu 7 dni od otrzymania niniejszej wiadomości,");
            sb.AppendLine("saldo zostanie uznane za zgodne z naszą ewidencją.");
            sb.AppendLine();
            sb.AppendLine("Z poważaniem,");
            sb.AppendLine("Magazyn Opakowań");
            sb.AppendLine("Ubojnia Drobiu \"Piórkowscy\"");
            sb.AppendLine("tel. 46 874 71 70, wew. 122");
            sb.AppendLine("email: opakowania@piorkowscy.com.pl");

            string tresc = sb.ToString();

            // 3. Skopiuj ścieżkę PDF do schowka
            try
            {
                Clipboard.SetText(pdfPath);
            }
            catch { }

            // 4. Otwórz Outlook z załącznikiem (jeśli dostępny)
            bool outlookOtworzony = false;
            try
            {
                // Próba otwarcia Outlook z załącznikiem
                string outlookPath = GetOutlookPath();
                if (!string.IsNullOrEmpty(outlookPath) && File.Exists(outlookPath))
                {
                    string args = $"/c ipm.note /m \"{emailKontrahenta ?? ""}\" /a \"{pdfPath}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = outlookPath,
                        Arguments = args,
                        UseShellExecute = false
                    });
                    outlookOtworzony = true;
                }
            }
            catch { }

            // 5. Jeśli Outlook nie zadziałał, użyj mailto
            if (!outlookOtworzony)
            {
                try
                {
                    string mailto = $"mailto:{emailKontrahenta ?? ""}?subject={Uri.EscapeDataString(temat)}&body={Uri.EscapeDataString(tresc)}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = mailto,
                        UseShellExecute = true
                    });

                    // Pokaż informację o załączniku
                    MessageBox.Show(
                        $"Ścieżka do pliku PDF została skopiowana do schowka.\n\n" +
                        $"Proszę załączyć plik:\n{pdfPath}\n\n" +
                        $"(Ctrl+V w oknie wyboru pliku)",
                        "Załącz plik PDF",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można otworzyć programu email: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            return pdfPath;
        }

        /// <summary>
        /// Próbuje znaleźć ścieżkę do Outlook
        /// </summary>
        private string GetOutlookPath()
        {
            string[] possiblePaths = new[]
            {
                @"C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE",
                @"C:\Program Files (x86)\Microsoft Office\root\Office16\OUTLOOK.EXE",
                @"C:\Program Files\Microsoft Office\Office16\OUTLOOK.EXE",
                @"C:\Program Files (x86)\Microsoft Office\Office16\OUTLOOK.EXE",
                @"C:\Program Files\Microsoft Office\root\Office15\OUTLOOK.EXE",
                @"C:\Program Files (x86)\Microsoft Office\root\Office15\OUTLOOK.EXE"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        #endregion

        #region Eksport dla nowego Dashboard

        /// <summary>
        /// Eksportuje saldo kontrahenta do PDF - wersja dla nowego widoku
        /// </summary>
        public void EksportujPdf(SaldoKontrahenta kontrahent, IEnumerable<DokumentSalda> dokumenty, DateTime dataOd, DateTime dataDo)
        {
            try
            {
                string sciezka = GetSciezkaZapisu();
                string nazwaPliku = GenerujNazwePliku(kontrahent.Kontrahent, "Saldo");
                string pelnaSciezka = Path.Combine(sciezka, nazwaPliku.Replace(".pdf", ".html"));

                string html = GenerujHTMLSaldaKontrahentaNowy(kontrahent, dokumenty, dataOd, dataDo);
                File.WriteAllText(pelnaSciezka, html, Encoding.UTF8);

                OtworzPlik(pelnaSciezka);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas eksportu PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Eksportuje saldo kontrahenta do Excel - wersja dla nowego widoku
        /// </summary>
        public void EksportujExcel(SaldoKontrahenta kontrahent, IEnumerable<DokumentSalda> dokumenty, DateTime dataOd, DateTime dataDo)
        {
            try
            {
                string sciezka = GetSciezkaZapisu();
                string bezpiecznaNazwa = string.Join("_", kontrahent.Kontrahent.Split(Path.GetInvalidFileNameChars()));
                string nazwaPliku = $"Saldo_{bezpiecznaNazwa}_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv";
                string pelnaSciezka = Path.Combine(sciezka, nazwaPliku);

                var sb = new StringBuilder();
                sb.AppendLine($"Kontrahent: {kontrahent.Kontrahent} - {kontrahent.Nazwa}");
                sb.AppendLine($"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}");
                sb.AppendLine();
                sb.AppendLine("Data;Nr dokumentu;E2;H1;EURO;PCV;DREW;Opis");

                foreach (var dok in dokumenty)
                {
                    sb.AppendLine($"{dok.DataTekst};{dok.NrDokumentu};{dok.E2};{dok.H1};{dok.EURO};{dok.PCV};{dok.DREW};{dok.Opis}");
                }

                File.WriteAllText(pelnaSciezka, sb.ToString(), new UTF8Encoding(true));
                OtworzPlik(pelnaSciezka);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas eksportu Excel: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Drukuje saldo kontrahenta - wersja dla nowego widoku
        /// </summary>
        public void Drukuj(SaldoKontrahenta kontrahent, IEnumerable<DokumentSalda> dokumenty, DateTime dataOd, DateTime dataDo)
        {
            // Eksportuj do HTML i otwórz do druku
            EksportujPdf(kontrahent, dokumenty, dataOd, dataDo);
        }

        private string GenerujHTMLSaldaKontrahentaNowy(SaldoKontrahenta kontrahent, IEnumerable<DokumentSalda> dokumenty, DateTime dataOd, DateTime dataDo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pl'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine($"<title>Saldo opakowań - {kontrahent.Kontrahent}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: white; color: #1f2937; }
                h1 { color: #1f2937; margin-bottom: 5px; font-size: 24px; }
                h2 { color: #6b7280; font-weight: normal; margin-top: 0; font-size: 16px; }
                .info { margin-bottom: 20px; color: #6b7280; font-size: 14px; }
                .suma-container { display: flex; gap: 15px; margin-bottom: 20px; }
                .suma-box { padding: 15px 20px; border-radius: 10px; text-align: center; min-width: 100px; }
                .suma-e2 { background: #EBF5FF; border: 2px solid #3B82F6; }
                .suma-h1 { background: #FFF7ED; border: 2px solid #F97316; }
                .suma-euro { background: #ECFDF5; border: 2px solid #10B981; }
                .suma-pcv { background: #F5F3FF; border: 2px solid #8B5CF6; }
                .suma-drew { background: #FEF3C7; border: 2px solid #D97706; }
                .suma-label { font-size: 12px; color: #6b7280; font-weight: bold; }
                .suma-value { font-size: 28px; font-weight: bold; margin-top: 5px; }
                .positive { color: #DC2626; }
                .negative { color: #16A34A; }
                .zero { color: #9CA3AF; }
                table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
                th { background: #f9fafb; color: #374151; padding: 12px; text-align: left; border-bottom: 2px solid #e5e7eb; font-size: 12px; text-transform: uppercase; }
                td { padding: 10px 12px; border-bottom: 1px solid #e5e7eb; font-size: 13px; }
                tr:hover { background: #f3f4f6; }
                .saldo-row { background: #FEF3C7 !important; font-weight: bold; }
                .footer { margin-top: 30px; padding-top: 20px; border-top: 1px solid #e5e7eb; color: #9ca3af; font-size: 12px; }
                @media print {
                    body { margin: 10px; }
                    .suma-container { flex-wrap: wrap; }
                }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine($"<h1>{kontrahent.Kontrahent}</h1>");
            sb.AppendLine($"<h2>{kontrahent.Nazwa}</h2>");
            sb.AppendLine($"<p class='info'>Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy} | Handlowiec: {kontrahent.Handlowiec} | Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}</p>");

            // Podsumowanie sald
            sb.AppendLine("<div class='suma-container'>");
            sb.AppendLine($"<div class='suma-box suma-e2'><div class='suma-label'>E2</div><div class='suma-value {GetKlasaKoloruNowy(kontrahent.E2)}'>{kontrahent.E2}</div></div>");
            sb.AppendLine($"<div class='suma-box suma-h1'><div class='suma-label'>H1</div><div class='suma-value {GetKlasaKoloruNowy(kontrahent.H1)}'>{kontrahent.H1}</div></div>");
            sb.AppendLine($"<div class='suma-box suma-euro'><div class='suma-label'>EURO</div><div class='suma-value {GetKlasaKoloruNowy(kontrahent.EURO)}'>{kontrahent.EURO}</div></div>");
            sb.AppendLine($"<div class='suma-box suma-pcv'><div class='suma-label'>PCV</div><div class='suma-value {GetKlasaKoloruNowy(kontrahent.PCV)}'>{kontrahent.PCV}</div></div>");
            sb.AppendLine($"<div class='suma-box suma-drew'><div class='suma-label'>DREW</div><div class='suma-value {GetKlasaKoloruNowy(kontrahent.DREW)}'>{kontrahent.DREW}</div></div>");
            sb.AppendLine("</div>");

            // Tabela dokumentów
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>Data</th><th>Nr dokumentu</th><th style='text-align:right'>E2</th><th style='text-align:right'>H1</th><th style='text-align:right'>EURO</th><th style='text-align:right'>PCV</th><th style='text-align:right'>DREW</th><th>Opis</th>");
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var dok in dokumenty)
            {
                string rowClass = dok.JestSaldem ? " class='saldo-row'" : "";
                sb.AppendLine($"<tr{rowClass}>");
                sb.AppendLine($"<td>{dok.DataTekst}</td>");
                sb.AppendLine($"<td>{dok.NrDokumentu}</td>");
                sb.AppendLine($"<td style='text-align:right' class='{GetKlasaKoloruNowy(dok.E2)}'>{FormatujWartosc(dok.E2)}</td>");
                sb.AppendLine($"<td style='text-align:right' class='{GetKlasaKoloruNowy(dok.H1)}'>{FormatujWartosc(dok.H1)}</td>");
                sb.AppendLine($"<td style='text-align:right' class='{GetKlasaKoloruNowy(dok.EURO)}'>{FormatujWartosc(dok.EURO)}</td>");
                sb.AppendLine($"<td style='text-align:right' class='{GetKlasaKoloruNowy(dok.PCV)}'>{FormatujWartosc(dok.PCV)}</td>");
                sb.AppendLine($"<td style='text-align:right' class='{GetKlasaKoloruNowy(dok.DREW)}'>{FormatujWartosc(dok.DREW)}</td>");
                sb.AppendLine($"<td>{dok.Opis}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");

            // Legenda
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p><strong>Legenda:</strong></p>");
            sb.AppendLine("<p><span class='positive'>■</span> Czerwony = kontrahent winny nam opakowania</p>");
            sb.AppendLine("<p><span class='negative'>■</span> Zielony = my winni kontrahentowi</p>");
            sb.AppendLine("<p>Ubojnia Drobiu \"Piórkowscy\" | tel. 46 874 71 70</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private string GetKlasaKoloruNowy(int wartosc)
        {
            if (wartosc > 0) return "positive";
            if (wartosc < 0) return "negative";
            return "zero";
        }

        private string FormatujWartosc(int wartosc)
        {
            if (wartosc == 0) return "-";
            return wartosc.ToString();
        }

        #endregion
    }
}
