using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;
using Microsoft.Win32;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class SaldaMainViewModel : INotifyPropertyChanged
    {
        private readonly SaldaService _service;
        private string _handlowiecFilter;

        public string UserId { get; }

        public SaldaMainViewModel(string userId)
        {
            UserId = userId;
            _service = new SaldaService();

            _dataDo = DateTime.Today;
            _wszystkieSalda = new List<SaldoKontrahenta>();

            // Wszystkie typy opakowań
            ListaE2 = new ObservableCollection<SaldoKontrahenta>();
            ListaH1 = new ObservableCollection<SaldoKontrahenta>();
            ListaEURO = new ObservableCollection<SaldoKontrahenta>();
            ListaPCV = new ObservableCollection<SaldoKontrahenta>();
            ListaDREW = new ObservableCollection<SaldoKontrahenta>();

            OdswiezCommand = new RelayCommand(async _ => await OdswiezAsync());

            // Start
            _ = InitAsync();
        }

        #region Properties

        private DateTime _dataDo;
        public DateTime DataDo
        {
            get => _dataDo;
            set
            {
                if (_dataDo != value)
                {
                    _dataDo = value;
                    OnPropertyChanged();
                    SaldaService.InvalidateCache();
                    _ = OdswiezAsync();
                }
            }
        }

        private string _filtrTekst;
        public string FiltrTekst
        {
            get => _filtrTekst;
            set
            {
                if (_filtrTekst != value)
                {
                    _filtrTekst = value;
                    OnPropertyChanged();
                    FiltrujListy();
                }
            }
        }

        private int _wybranaZakladka;
        public int WybranaZakladka
        {
            get => _wybranaZakladka;
            set
            {
                if (_wybranaZakladka != value)
                {
                    _wybranaZakladka = value;
                    OnPropertyChanged();
                    ObliczStatystyki();
                }
            }
        }

        private SaldoKontrahenta _wybranyKontrahent;
        public SaldoKontrahenta WybranyKontrahent
        {
            get => _wybranyKontrahent;
            set
            {
                _wybranyKontrahent = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private string _statusLadowania;
        public string StatusLadowania
        {
            get => _statusLadowania;
            set
            {
                _statusLadowania = value;
                OnPropertyChanged();
            }
        }

        public string NazwaHandlowca => string.IsNullOrEmpty(_handlowiecFilter) ? "Wszyscy" : _handlowiecFilter;

        // Listy per zakładka - wszystkie typy opakowań
        public ObservableCollection<SaldoKontrahenta> ListaE2 { get; }
        public ObservableCollection<SaldoKontrahenta> ListaH1 { get; }
        public ObservableCollection<SaldoKontrahenta> ListaEURO { get; }
        public ObservableCollection<SaldoKontrahenta> ListaPCV { get; }
        public ObservableCollection<SaldoKontrahenta> ListaDREW { get; }

        private List<SaldoKontrahenta> _wszystkieSalda;

        // Statystyki
        private int _liczbaKontrahentow;
        public int LiczbaKontrahentow
        {
            get => _liczbaKontrahentow;
            set { _liczbaKontrahentow = value; OnPropertyChanged(); }
        }

        private int _sumaWydane;
        public int SumaWydane
        {
            get => _sumaWydane;
            set { _sumaWydane = value; OnPropertyChanged(); }
        }

        private int _sumaZwroty;
        public int SumaZwroty
        {
            get => _sumaZwroty;
            set { _sumaZwroty = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand OdswiezCommand { get; }

        #endregion

        #region Methods

        private async Task InitAsync()
        {
            // Pobierz filtr handlowca
            _handlowiecFilter = await _service.PobierzHandlowcaAsync(UserId);
            OnPropertyChanged(nameof(NazwaHandlowca));

            await OdswiezAsync();
        }

        private async Task OdswiezAsync()
        {
            try
            {
                IsLoading = true;
                StatusLadowania = "Pobieranie danych...";

                _wszystkieSalda = await _service.PobierzWszystkieSaldaAsync(DataDo, _handlowiecFilter);

                FiltrujListy();

                StatusLadowania = $"Załadowano {_wszystkieSalda.Count} kontrahentów";
            }
            catch (Exception ex)
            {
                StatusLadowania = $"Błąd: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FiltrujListy()
        {
            var filtr = FiltrTekst?.ToLower() ?? "";

            var przefiltrowane = _wszystkieSalda
                .Where(s => string.IsNullOrEmpty(filtr) ||
                            s.Kontrahent.ToLower().Contains(filtr) ||
                            s.Nazwa?.ToLower().Contains(filtr) == true ||
                            s.Handlowiec?.ToLower().Contains(filtr) == true)
                .ToList();

            // E2 - tylko kontrahenci z saldem E2
            ListaE2.Clear();
            foreach (var s in przefiltrowane.Where(x => x.E2 != 0).OrderByDescending(x => x.E2))
                ListaE2.Add(s);

            // H1
            ListaH1.Clear();
            foreach (var s in przefiltrowane.Where(x => x.H1 != 0).OrderByDescending(x => x.H1))
                ListaH1.Add(s);

            // EURO
            ListaEURO.Clear();
            foreach (var s in przefiltrowane.Where(x => x.EURO != 0).OrderByDescending(x => x.EURO))
                ListaEURO.Add(s);

            // PCV
            ListaPCV.Clear();
            foreach (var s in przefiltrowane.Where(x => x.PCV != 0).OrderByDescending(x => x.PCV))
                ListaPCV.Add(s);

            // DREW
            ListaDREW.Clear();
            foreach (var s in przefiltrowane.Where(x => x.DREW != 0).OrderByDescending(x => x.DREW))
                ListaDREW.Add(s);

            ObliczStatystyki();
        }

        private void ObliczStatystyki()
        {
            // Wszystkie typy opakowań
            var aktualnaLista = WybranaZakladka switch
            {
                0 => ListaE2,
                1 => ListaH1,
                2 => ListaEURO,
                3 => ListaPCV,
                4 => ListaDREW,
                _ => ListaE2
            };

            var typSalda = WybranaZakladka switch
            {
                0 => "E2",
                1 => "H1",
                2 => "EURO",
                3 => "PCV",
                4 => "DREW",
                _ => "E2"
            };

            LiczbaKontrahentow = aktualnaLista.Count;
            SumaWydane = aktualnaLista.Where(s => s.GetSaldo(typSalda) > 0).Sum(s => s.GetSaldo(typSalda));
            SumaZwroty = aktualnaLista.Where(s => s.GetSaldo(typSalda) < 0).Sum(s => Math.Abs(s.GetSaldo(typSalda)));
        }

        public void EksportujDoExcel()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Pliki Excel (*.xlsx)|*.xlsx",
                    FileName = $"Salda_Opakowan_{DataDo:yyyy-MM-dd}.xlsx",
                    Title = "Zapisz zestawienie sald"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                using (var workbook = new XLWorkbook())
                {
                    // Arkusz E2
                    var wsE2 = workbook.Worksheets.Add("E2 - Pojemniki");
                    DodajDaneDoArkusza(wsE2, ListaE2.ToList(), "E2");

                    // Arkusz H1
                    var wsH1 = workbook.Worksheets.Add("H1 - Palety");
                    DodajDaneDoArkusza(wsH1, ListaH1.ToList(), "H1");

                    // Arkusz EURO
                    var wsEURO = workbook.Worksheets.Add("EURO - Palety");
                    DodajDaneDoArkusza(wsEURO, ListaEURO.ToList(), "EURO");

                    // Arkusz PCV
                    var wsPCV = workbook.Worksheets.Add("PCV - Pojemniki");
                    DodajDaneDoArkusza(wsPCV, ListaPCV.ToList(), "PCV");

                    // Arkusz DREW
                    var wsDREW = workbook.Worksheets.Add("DREW - Drewniane");
                    DodajDaneDoArkusza(wsDREW, ListaDREW.ToList(), "DREW");

                    // Arkusz podsumowanie
                    var wsSummary = workbook.Worksheets.Add("Podsumowanie");
                    DodajPodsumowanie(wsSummary);

                    workbook.SaveAs(saveDialog.FileName);
                }

                MessageBox.Show($"Plik zapisany pomyslnie:\n{saveDialog.FileName}", "Eksport Excel", MessageBoxButton.OK, MessageBoxImage.Information);

                // Otwórz plik
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = saveDialog.FileName,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DodajDaneDoArkusza(IXLWorksheet ws, List<SaldoKontrahenta> dane, string typSalda)
        {
            // Nagłówek tytułowy
            ws.Cell(1, 1).Value = $"ZESTAWIENIE SALD OPAKOWAN - {typSalda}";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 7).Merge();

            ws.Cell(2, 1).Value = $"Stan na dzien: {DataDo:dd.MM.yyyy}";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Range(2, 1, 2, 7).Merge();

            // Nagłówki kolumn
            var headerRow = 4;
            var headers = new[] { "Lp.", "Symbol", "Nazwa", "Handlowiec", "Saldo", "Status", "Data potw." };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE0B2");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            // Dane
            var row = headerRow + 1;
            var lp = 1;

            foreach (var s in dane)
            {
                var saldo = typSalda == "E2" ? s.E2 : s.H1;
                var potwierdzone = typSalda == "E2" ? s.E2Potwierdzone : s.H1Potwierdzone;
                var dataPotw = typSalda == "E2" ? s.E2DataPotwierdzenia : s.H1DataPotwierdzenia;

                ws.Cell(row, 1).Value = lp++;
                ws.Cell(row, 2).Value = s.Kontrahent;
                ws.Cell(row, 3).Value = s.Nazwa;
                ws.Cell(row, 4).Value = s.Handlowiec ?? "-";
                ws.Cell(row, 5).Value = saldo;
                ws.Cell(row, 6).Value = potwierdzone ? "Potwierdzone" : "Brak potw.";
                ws.Cell(row, 7).Value = dataPotw?.ToString("dd.MM.yyyy") ?? "-";

                // Kolorowanie
                if (saldo > 0)
                    ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#E53935");
                else if (saldo < 0)
                    ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#43A047");

                if (!potwierdzone)
                    ws.Cell(row, 6).Style.Font.FontColor = XLColor.FromHtml("#FB8C00");

                // Ramki
                for (int col = 1; col <= 7; col++)
                    ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                row++;
            }

            // Podsumowanie
            row++;
            ws.Cell(row, 4).Value = "RAZEM:";
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 5).Value = dane.Sum(s => typSalda == "E2" ? s.E2 : s.H1);
            ws.Cell(row, 5).Style.Font.Bold = true;

            // Autofit
            ws.Columns().AdjustToContents();
        }

        private void DodajPodsumowanie(IXLWorksheet ws)
        {
            ws.Cell(1, 1).Value = "PODSUMOWANIE SALD OPAKOWAN";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 3).Merge();

            ws.Cell(2, 1).Value = $"Stan na dzien: {DataDo:dd.MM.yyyy}";
            ws.Cell(2, 1).Style.Font.Italic = true;

            var row = 4;

            // E2
            ws.Cell(row, 1).Value = "Pojemniki E2";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Liczba kontrahentow:";
            ws.Cell(row, 2).Value = ListaE2.Count;
            row++;

            ws.Cell(row, 1).Value = "Suma u kontrahentow:";
            ws.Cell(row, 2).Value = ListaE2.Where(s => s.E2 > 0).Sum(s => s.E2);
            row++;

            ws.Cell(row, 1).Value = "Suma nadwyzek (u nas):";
            ws.Cell(row, 2).Value = Math.Abs(ListaE2.Where(s => s.E2 < 0).Sum(s => s.E2));
            row++;

            ws.Cell(row, 1).Value = "Szacunkowa wartosc (15 PLN/szt):";
            ws.Cell(row, 2).Value = ListaE2.Where(s => s.E2 > 0).Sum(s => s.E2) * 15;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0 \"PLN\"";
            row += 2;

            // H1
            ws.Cell(row, 1).Value = "Palety H1";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Liczba kontrahentow:";
            ws.Cell(row, 2).Value = ListaH1.Count;
            row++;

            ws.Cell(row, 1).Value = "Suma u kontrahentow:";
            ws.Cell(row, 2).Value = ListaH1.Where(s => s.H1 > 0).Sum(s => s.H1);
            row++;

            ws.Cell(row, 1).Value = "Suma nadwyzek (u nas):";
            ws.Cell(row, 2).Value = Math.Abs(ListaH1.Where(s => s.H1 < 0).Sum(s => s.H1));
            row++;

            ws.Cell(row, 1).Value = "Szacunkowa wartosc (80 PLN/szt):";
            ws.Cell(row, 2).Value = ListaH1.Where(s => s.H1 > 0).Sum(s => s.H1) * 80;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0 \"PLN\"";
            row += 2;

            // EURO
            ws.Cell(row, 1).Value = "Palety EURO";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Liczba kontrahentow:";
            ws.Cell(row, 2).Value = ListaEURO.Count;
            row++;

            ws.Cell(row, 1).Value = "Suma u kontrahentow:";
            ws.Cell(row, 2).Value = ListaEURO.Where(s => s.EURO > 0).Sum(s => s.EURO);
            row++;

            ws.Cell(row, 1).Value = "Szacunkowa wartosc (60 PLN/szt):";
            ws.Cell(row, 2).Value = ListaEURO.Where(s => s.EURO > 0).Sum(s => s.EURO) * 60;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0 \"PLN\"";
            row += 2;

            // PCV
            ws.Cell(row, 1).Value = "Pojemniki PCV";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Liczba kontrahentow:";
            ws.Cell(row, 2).Value = ListaPCV.Count;
            row++;

            ws.Cell(row, 1).Value = "Suma u kontrahentow:";
            ws.Cell(row, 2).Value = ListaPCV.Where(s => s.PCV > 0).Sum(s => s.PCV);
            row++;

            ws.Cell(row, 1).Value = "Szacunkowa wartosc (50 PLN/szt):";
            ws.Cell(row, 2).Value = ListaPCV.Where(s => s.PCV > 0).Sum(s => s.PCV) * 50;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0 \"PLN\"";
            row += 2;

            // DREW
            ws.Cell(row, 1).Value = "Opakowania drewniane (DREW)";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 1).Value = "Liczba kontrahentow:";
            ws.Cell(row, 2).Value = ListaDREW.Count;
            row++;

            ws.Cell(row, 1).Value = "Suma u kontrahentow:";
            ws.Cell(row, 2).Value = ListaDREW.Where(s => s.DREW > 0).Sum(s => s.DREW);
            row++;

            ws.Cell(row, 1).Value = "Szacunkowa wartosc (40 PLN/szt):";
            ws.Cell(row, 2).Value = ListaDREW.Where(s => s.DREW > 0).Sum(s => s.DREW) * 40;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0 \"PLN\"";
            row += 2;

            // Łącznie
            ws.Cell(row, 1).Value = "LACZNIE WARTOSC KAUCJI:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            var lacznaWartosc = (ListaE2.Where(s => s.E2 > 0).Sum(s => s.E2) * 15) +
                                (ListaH1.Where(s => s.H1 > 0).Sum(s => s.H1) * 80) +
                                (ListaEURO.Where(s => s.EURO > 0).Sum(s => s.EURO) * 60) +
                                (ListaPCV.Where(s => s.PCV > 0).Sum(s => s.PCV) * 50) +
                                (ListaDREW.Where(s => s.DREW > 0).Sum(s => s.DREW) * 40);
            ws.Cell(row, 2).Value = lacznaWartosc;
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0 \"PLN\"";

            ws.Columns().AdjustToContents();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
