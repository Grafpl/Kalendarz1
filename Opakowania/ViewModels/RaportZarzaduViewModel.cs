using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Kalendarz1.Opakowania.Services;
using Microsoft.Win32;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class RaportZarzaduViewModel : INotifyPropertyChanged
    {
        private readonly SaldaService _saldaService;
        private readonly string _userId;

        // Ceny kaucji
        private const decimal CENA_E2 = 15m;
        private const decimal CENA_H1 = 80m;
        private const decimal CENA_EURO = 60m;
        private const decimal CENA_PCV = 50m;
        private const decimal CENA_DREW = 40m;

        public RaportZarzaduViewModel(string userId)
        {
            _userId = userId;
            _saldaService = new SaldaService();

            Top5Dluznikow = new ObservableCollection<Top5Item>();
            Rekomendacje = new ObservableCollection<string>();

            DataRaportu = DateTime.Today;
            Uzytkownik = userId;

            GenerujPDFCommand = new RelayCommand(async _ => await GenerujPDFAsync());

            _ = LoadDataAsync();
        }

        #region Properties

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private DateTime _dataRaportu;
        public DateTime DataRaportu
        {
            get => _dataRaportu;
            set { _dataRaportu = value; OnPropertyChanged(); }
        }

        private string _uzytkownik;
        public string Uzytkownik
        {
            get => _uzytkownik;
            set { _uzytkownik = value; OnPropertyChanged(); }
        }

        private int _liczbaKontrahentow;
        public int LiczbaKontrahentow
        {
            get => _liczbaKontrahentow;
            set { _liczbaKontrahentow = value; OnPropertyChanged(); }
        }

        private int _sumaE2;
        public int SumaE2
        {
            get => _sumaE2;
            set { _sumaE2 = value; OnPropertyChanged(); }
        }

        private int _sumaH1;
        public int SumaH1
        {
            get => _sumaH1;
            set { _sumaH1 = value; OnPropertyChanged(); }
        }

        private decimal _wartoscKaucji;
        public decimal WartoscKaucji
        {
            get => _wartoscKaucji;
            set { _wartoscKaucji = value; OnPropertyChanged(); }
        }

        private int _procentPotwierdzone;
        public int ProcentPotwierdzone
        {
            get => _procentPotwierdzone;
            set { _procentPotwierdzone = value; OnPropertyChanged(); }
        }

        private int _bezPotwierdzenia30;
        public int BezPotwierdzenia30
        {
            get => _bezPotwierdzenia30;
            set { _bezPotwierdzenia30 = value; OnPropertyChanged(); }
        }

        private int _bezPotwierdzenia90;
        public int BezPotwierdzenia90
        {
            get => _bezPotwierdzenia90;
            set { _bezPotwierdzenia90 = value; OnPropertyChanged(); }
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Top5Item> Top5Dluznikow { get; }
        public ObservableCollection<string> Rekomendacje { get; }

        public ICommand GenerujPDFCommand { get; }

        #endregion

        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Generowanie raportu...";

                var salda = await _saldaService.PobierzWszystkieSaldaAsync(DateTime.Today);
                var zSaldem = salda.Where(s => s.MaSaldo).ToList();

                // Podstawowe statystyki
                LiczbaKontrahentow = zSaldem.Count;
                SumaE2 = zSaldem.Where(s => s.E2 > 0).Sum(s => s.E2);
                SumaH1 = zSaldem.Where(s => s.H1 > 0).Sum(s => s.H1);

                // Wartość kaucji
                var sumaEuro = zSaldem.Where(s => s.EURO > 0).Sum(s => s.EURO);
                var sumaPcv = zSaldem.Where(s => s.PCV > 0).Sum(s => s.PCV);
                var sumaDrew = zSaldem.Where(s => s.DREW > 0).Sum(s => s.DREW);

                WartoscKaucji = (SumaE2 * CENA_E2) + (SumaH1 * CENA_H1) +
                               (sumaEuro * CENA_EURO) + (sumaPcv * CENA_PCV) + (sumaDrew * CENA_DREW);

                // Procent potwierdzonych
                var potwierdzone = zSaldem.Count(s =>
                    (s.E2 == 0 || s.E2Potwierdzone) && (s.H1 == 0 || s.H1Potwierdzone));
                ProcentPotwierdzone = zSaldem.Count > 0 ? (potwierdzone * 100 / zSaldem.Count) : 0;

                // Bez potwierdzenia >30 dni
                BezPotwierdzenia30 = zSaldem.Count(s =>
                {
                    var dniE2 = s.E2DataPotwierdzenia.HasValue ? (DateTime.Today - s.E2DataPotwierdzenia.Value).TotalDays : 999;
                    var dniH1 = s.H1DataPotwierdzenia.HasValue ? (DateTime.Today - s.H1DataPotwierdzenia.Value).TotalDays : 999;
                    return ((s.E2 != 0 && dniE2 > 30 && dniE2 <= 90) || (s.H1 != 0 && dniH1 > 30 && dniH1 <= 90));
                });

                // Bez potwierdzenia >90 dni
                BezPotwierdzenia90 = zSaldem.Count(s =>
                {
                    var dniE2 = s.E2DataPotwierdzenia.HasValue ? (DateTime.Today - s.E2DataPotwierdzenia.Value).TotalDays : 999;
                    var dniH1 = s.H1DataPotwierdzenia.HasValue ? (DateTime.Today - s.H1DataPotwierdzenia.Value).TotalDays : 999;
                    return (s.E2 != 0 && dniE2 > 90) || (s.H1 != 0 && dniH1 > 90);
                });

                // TOP 5 dłużników
                Top5Dluznikow.Clear();
                var top5 = zSaldem
                    .OrderByDescending(s => Math.Abs(s.E2) + Math.Abs(s.H1))
                    .Take(5)
                    .Select((s, i) => new Top5Item
                    {
                        Pozycja = i + 1,
                        NazwaKontrahenta = $"{s.Kontrahent} - {s.Nazwa}",
                        E2 = s.E2,
                        H1 = s.H1
                    });

                foreach (var item in top5)
                    Top5Dluznikow.Add(item);

                // Generuj rekomendacje
                Rekomendacje.Clear();

                if (BezPotwierdzenia90 > 0)
                {
                    Rekomendacje.Add($"PILNE: {BezPotwierdzenia90} kontrahentów bez potwierdzenia salda ponad 90 dni - zalecana natychmiastowa weryfikacja.");
                }

                if (BezPotwierdzenia30 > 5)
                {
                    Rekomendacje.Add($"Wysłać przypomnienia do {BezPotwierdzenia30} kontrahentów bez potwierdzenia 30-90 dni.");
                }

                if (ProcentPotwierdzone < 70)
                {
                    Rekomendacje.Add($"Niski poziom potwierdzeń ({ProcentPotwierdzone}%) - rozważyć kampanię weryfikacji sald.");
                }

                if (WartoscKaucji > 100000)
                {
                    Rekomendacje.Add($"Wartość kaucji w obiegu przekracza 100 000 PLN - monitorować zwroty.");
                }

                var topDluznik = Top5Dluznikow.FirstOrDefault();
                if (topDluznik != null && (topDluznik.E2 + topDluznik.H1) > 100)
                {
                    Rekomendacje.Add($"Największy dłużnik ({topDluznik.NazwaKontrahenta}) posiada {topDluznik.E2 + topDluznik.H1} szt. - rozważyć kontakt.");
                }

                if (Rekomendacje.Count == 0)
                {
                    Rekomendacje.Add("Brak pilnych rekomendacji - system opakowań działa prawidłowo.");
                }

                StatusText = "Raport gotowy";
            }
            catch (Exception ex)
            {
                StatusText = $"Błąd: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task GenerujPDFAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Pliki PDF (*.pdf)|*.pdf",
                    FileName = $"Raport_Zarzadu_{DateTime.Today:yyyy-MM-dd}.pdf",
                    Title = "Zapisz raport PDF"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                IsLoading = true;
                StatusText = "Generowanie PDF...";

                await Task.Run(() =>
                {
                    using (var fs = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        var document = new Document(PageSize.A4, 40, 40, 40, 40);
                        var writer = PdfWriter.GetInstance(document, fs);
                        document.Open();

                        // Fonty
                        var baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                        var baseFontBold = BaseFont.CreateFont(BaseFont.HELVETICA_BOLD, BaseFont.CP1250, BaseFont.EMBEDDED);
                        var fontTitle = new Font(baseFontBold, 18, Font.NORMAL, new BaseColor(230, 81, 0));
                        var fontSubtitle = new Font(baseFont, 12, Font.NORMAL, BaseColor.GRAY);
                        var fontHeader = new Font(baseFontBold, 14, Font.NORMAL, new BaseColor(230, 81, 0));
                        var fontNormal = new Font(baseFont, 11, Font.NORMAL, BaseColor.BLACK);
                        var fontBold = new Font(baseFontBold, 11, Font.NORMAL, BaseColor.BLACK);
                        var fontSmall = new Font(baseFont, 9, Font.NORMAL, BaseColor.GRAY);

                        // Nagłówek
                        var titlePara = new Paragraph("PRONOVA SP. Z O.O.", fontTitle);
                        titlePara.Alignment = Element.ALIGN_CENTER;
                        document.Add(titlePara);

                        var subtitlePara = new Paragraph("RAPORT OPAKOWAN ZWROTNYCH", fontSubtitle);
                        subtitlePara.Alignment = Element.ALIGN_CENTER;
                        subtitlePara.SpacingAfter = 10;
                        document.Add(subtitlePara);

                        var datePara = new Paragraph($"Stan na dzien: {DataRaportu:dd MMMM yyyy}", fontNormal);
                        datePara.Alignment = Element.ALIGN_CENTER;
                        datePara.SpacingAfter = 20;
                        document.Add(datePara);

                        // Sekcja 1: Podsumowanie ogólne
                        document.Add(new Paragraph("1. PODSUMOWANIE OGOLNE", fontHeader));
                        document.Add(new Paragraph(" ", fontSmall));

                        var summaryTable = new PdfPTable(2);
                        summaryTable.WidthPercentage = 100;
                        summaryTable.SetWidths(new float[] { 3, 1 });

                        AddTableRow(summaryTable, "Liczba kontrahentow z saldem:", LiczbaKontrahentow.ToString(), fontNormal, fontBold);
                        AddTableRow(summaryTable, "Suma opakowan E2 w obiegu:", $"{SumaE2:N0} szt.", fontNormal, fontBold);
                        AddTableRow(summaryTable, "Suma palet H1 w obiegu:", $"{SumaH1:N0} szt.", fontNormal, fontBold);
                        AddTableRow(summaryTable, "Szacunkowa wartosc kaucji:", $"{WartoscKaucji:N0} PLN", fontNormal, fontBold);

                        summaryTable.SpacingAfter = 20;
                        document.Add(summaryTable);

                        // Sekcja 2: Status potwierdzeń
                        document.Add(new Paragraph("2. STATUS POTWIERDZERN SALD", fontHeader));
                        document.Add(new Paragraph(" ", fontSmall));

                        var confirmTable = new PdfPTable(3);
                        confirmTable.WidthPercentage = 100;

                        var cell1 = new PdfPCell(new Phrase($"{ProcentPotwierdzone}%\nPotwierdzone", fontBold));
                        cell1.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell1.Border = Rectangle.NO_BORDER;
                        cell1.PaddingBottom = 10;
                        confirmTable.AddCell(cell1);

                        var cell2 = new PdfPCell(new Phrase($"{BezPotwierdzenia30}\nBez potw. >30 dni", fontBold));
                        cell2.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell2.Border = Rectangle.NO_BORDER;
                        cell2.PaddingBottom = 10;
                        confirmTable.AddCell(cell2);

                        var cell3 = new PdfPCell(new Phrase($"{BezPotwierdzenia90}\nBez potw. >90 dni", fontBold));
                        cell3.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell3.Border = Rectangle.NO_BORDER;
                        cell3.PaddingBottom = 10;
                        confirmTable.AddCell(cell3);

                        confirmTable.SpacingAfter = 20;
                        document.Add(confirmTable);

                        // Sekcja 3: TOP 5
                        document.Add(new Paragraph("3. NAJWIEKSI DLUZNICY (TOP 5)", fontHeader));
                        document.Add(new Paragraph(" ", fontSmall));

                        var top5Table = new PdfPTable(4);
                        top5Table.WidthPercentage = 100;
                        top5Table.SetWidths(new float[] { 0.5f, 3, 1, 1 });

                        // Header
                        AddTableHeader(top5Table, "Lp.", fontBold);
                        AddTableHeader(top5Table, "Kontrahent", fontBold);
                        AddTableHeader(top5Table, "E2", fontBold);
                        AddTableHeader(top5Table, "H1", fontBold);

                        foreach (var item in Top5Dluznikow)
                        {
                            AddTableCell(top5Table, $"{item.Pozycja}.", fontNormal);
                            AddTableCell(top5Table, item.NazwaKontrahenta, fontNormal);
                            AddTableCell(top5Table, item.E2.ToString(), fontNormal);
                            AddTableCell(top5Table, item.H1.ToString(), fontNormal);
                        }

                        top5Table.SpacingAfter = 20;
                        document.Add(top5Table);

                        // Sekcja 4: Rekomendacje
                        document.Add(new Paragraph("4. REKOMENDACJE", fontHeader));
                        document.Add(new Paragraph(" ", fontSmall));

                        foreach (var rek in Rekomendacje)
                        {
                            var bullet = new Paragraph($"• {rek}", fontNormal);
                            bullet.SpacingAfter = 5;
                            document.Add(bullet);
                        }

                        document.Add(new Paragraph(" ", fontSmall));
                        document.Add(new Paragraph(" ", fontSmall));

                        // Stopka
                        var footerTable = new PdfPTable(2);
                        footerTable.WidthPercentage = 100;

                        var footerLeft = new PdfPCell();
                        footerLeft.Border = Rectangle.NO_BORDER;
                        footerLeft.AddElement(new Paragraph("Sporzadzil:", fontSmall));
                        footerLeft.AddElement(new Paragraph(Uzytkownik, fontNormal));
                        footerLeft.AddElement(new Paragraph($"{DataRaportu:dd.MM.yyyy}", fontSmall));
                        footerTable.AddCell(footerLeft);

                        var footerRight = new PdfPCell();
                        footerRight.Border = Rectangle.NO_BORDER;
                        footerRight.HorizontalAlignment = Element.ALIGN_RIGHT;
                        footerRight.AddElement(new Paragraph("Wygenerowano automatycznie", fontSmall));
                        footerRight.AddElement(new Paragraph("System Zarzadzania Opakowaniami", fontSmall));
                        footerTable.AddCell(footerRight);

                        document.Add(footerTable);

                        document.Close();
                    }
                });

                StatusText = "PDF zapisany pomyślnie";
                MessageBox.Show($"Raport został zapisany:\n{saveDialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Otwórz plik
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveDialog.FileName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText = $"Błąd: {ex.Message}";
                MessageBox.Show($"Błąd generowania PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AddTableRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var cellLabel = new PdfPCell(new Phrase(label, labelFont));
            cellLabel.Border = Rectangle.NO_BORDER;
            cellLabel.PaddingBottom = 5;
            table.AddCell(cellLabel);

            var cellValue = new PdfPCell(new Phrase(value, valueFont));
            cellValue.Border = Rectangle.NO_BORDER;
            cellValue.PaddingBottom = 5;
            cellValue.HorizontalAlignment = Element.ALIGN_RIGHT;
            table.AddCell(cellValue);
        }

        private void AddTableHeader(PdfPTable table, string text, Font font)
        {
            var cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = new BaseColor(255, 243, 224);
            cell.Padding = 8;
            cell.BorderColor = new BaseColor(238, 238, 238);
            table.AddCell(cell);
        }

        private void AddTableCell(PdfPTable table, string text, Font font)
        {
            var cell = new PdfPCell(new Phrase(text, font));
            cell.Padding = 6;
            cell.BorderColor = new BaseColor(238, 238, 238);
            table.AddCell(cell);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class Top5Item
    {
        public int Pozycja { get; set; }
        public string NazwaKontrahenta { get; set; }
        public int E2 { get; set; }
        public int H1 { get; set; }
    }
}
