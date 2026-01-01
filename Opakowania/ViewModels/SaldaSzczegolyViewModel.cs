using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class SaldaSzczegolyViewModel : INotifyPropertyChanged
    {
        private readonly SaldaService _saldaService;
        private readonly PdfReportService _pdfService;
        private readonly string _userId;

        public event Action CloseRequested;

        public SaldaSzczegolyViewModel(SaldoKontrahenta kontrahent, DateTime dataDo, string userId)
        {
            Kontrahent = kontrahent;
            _userId = userId;
            _dataDo = dataDo;
            _dataOd = dataDo.AddMonths(-2);

            _saldaService = new SaldaService();
            _pdfService = new PdfReportService();

            Dokumenty = new ObservableCollection<DokumentSalda>();

            // Komendy
            WrocCommand = new RelayCommand(_ => CloseRequested?.Invoke());
            GenerujPDFCommand = new RelayCommand(async _ => await GenerujPDFAsync());
            PDFiEmailCommand = new RelayCommand(async _ => await PDFiEmailAsync());
            FiltrTenMiesiacCommand = new RelayCommand(_ => UstawFiltr(0));
            Filtr3MiesCommand = new RelayCommand(_ => UstawFiltr(3));
            Filtr6MiesCommand = new RelayCommand(_ => UstawFiltr(6));

            // Załaduj dane
            _ = PobierzDokumentyAsync();
        }

        #region Properties

        public SaldoKontrahenta Kontrahent { get; }

        public string Tytul => $"Saldo - {Kontrahent.Kontrahent}";

        private DateTime _dataOd;
        public DateTime DataOd
        {
            get => _dataOd;
            set
            {
                if (_dataOd != value)
                {
                    _dataOd = value;
                    OnPropertyChanged();
                    _ = PobierzDokumentyAsync();
                }
            }
        }

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
                    _ = PobierzDokumentyAsync();
                }
            }
        }

        public ObservableCollection<DokumentSalda> Dokumenty { get; }

        public int LiczbaDokumentow => Dokumenty.Count(d => !d.JestSaldem);

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        // Opisy sald
        public string E2Opis => GetOpis(Kontrahent.E2);
        public string H1Opis => GetOpis(Kontrahent.H1);
        public string EUROOpis => GetOpis(Kontrahent.EURO);
        public string PCVOpis => GetOpis(Kontrahent.PCV);
        public string DREWOpis => GetOpis(Kontrahent.DREW);

        private string GetOpis(int saldo)
        {
            if (saldo == 0) return "brak salda";
            return saldo > 0 ? "kontrahent winny" : "my winni";
        }

        #endregion

        #region Commands

        public ICommand WrocCommand { get; }
        public ICommand GenerujPDFCommand { get; }
        public ICommand PDFiEmailCommand { get; }
        public ICommand FiltrTenMiesiacCommand { get; }
        public ICommand Filtr3MiesCommand { get; }
        public ICommand Filtr6MiesCommand { get; }

        #endregion

        #region Methods

        private void UstawFiltr(int miesiecy)
        {
            var dzisiaj = DateTime.Today;
            if (miesiecy == 0)
            {
                _dataOd = new DateTime(dzisiaj.Year, dzisiaj.Month, 1);
            }
            else
            {
                _dataOd = dzisiaj.AddMonths(-miesiecy);
            }
            _dataDo = dzisiaj;

            OnPropertyChanged(nameof(DataOd));
            OnPropertyChanged(nameof(DataDo));
            _ = PobierzDokumentyAsync();
        }

        private async Task PobierzDokumentyAsync()
        {
            try
            {
                IsLoading = true;

                var docs = await _saldaService.PobierzDokumentyAsync(Kontrahent.Id, DataOd, DataDo);

                Dokumenty.Clear();
                foreach (var doc in docs)
                {
                    Dokumenty.Add(doc);
                }

                OnPropertyChanged(nameof(LiczbaDokumentow));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
                IsLoading = true;

                // Konwertuj dokumenty do formatu wymaganego przez PdfReportService
                var dokumentyPdf = Dokumenty.Select(d => new DokumentOpakowania
                {
                    NrDok = d.NrDokumentu,
                    Data = d.Data,
                    Dokumenty = d.JestSaldem ? d.NrDokumentu : d.Opis,
                    E2 = d.E2,
                    H1 = d.H1,
                    EURO = d.EURO,
                    PCV = d.PCV,
                    DREW = d.DREW,
                    JestSaldem = d.JestSaldem
                }).ToList();

                // Konwertuj saldo
                var saldo = new SaldoOpakowania
                {
                    Kontrahent = Kontrahent.Kontrahent,
                    KontrahentId = Kontrahent.Id,
                    SaldoE2 = Kontrahent.E2,
                    SaldoH1 = Kontrahent.H1,
                    SaldoEURO = Kontrahent.EURO,
                    SaldoPCV = Kontrahent.PCV,
                    SaldoDREW = Kontrahent.DREW
                };

                var sciezka = await Task.Run(() =>
                    _pdfService.GenerujRaportKontrahenta(
                        Kontrahent.Id,
                        Kontrahent.Kontrahent,
                        saldo,
                        dokumentyPdf,
                        new System.Collections.Generic.List<PotwierdzenieSalda>(),
                        DataOd,
                        DataDo));

                var result = MessageBox.Show(
                    $"Raport PDF zapisany:\n{sciezka}\n\nCzy chcesz otworzyć plik?",
                    "PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = sciezka,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task PDFiEmailAsync()
        {
            try
            {
                IsLoading = true;

                // Generuj PDF
                var dokumentyPdf = Dokumenty.Select(d => new DokumentOpakowania
                {
                    NrDok = d.NrDokumentu,
                    Data = d.Data,
                    Dokumenty = d.JestSaldem ? d.NrDokumentu : d.Opis,
                    E2 = d.E2,
                    H1 = d.H1,
                    EURO = d.EURO,
                    PCV = d.PCV,
                    DREW = d.DREW,
                    JestSaldem = d.JestSaldem
                }).ToList();

                var saldo = new SaldoOpakowania
                {
                    Kontrahent = Kontrahent.Kontrahent,
                    KontrahentId = Kontrahent.Id,
                    SaldoE2 = Kontrahent.E2,
                    SaldoH1 = Kontrahent.H1,
                    SaldoEURO = Kontrahent.EURO,
                    SaldoPCV = Kontrahent.PCV,
                    SaldoDREW = Kontrahent.DREW
                };

                var sciezka = await Task.Run(() =>
                    _pdfService.GenerujRaportKontrahenta(
                        Kontrahent.Id,
                        Kontrahent.Kontrahent,
                        saldo,
                        dokumentyPdf,
                        new System.Collections.Generic.List<PotwierdzenieSalda>(),
                        DataOd,
                        DataDo));

                // Przygotuj treść emaila
                var temat = $"Saldo opakowań - {Kontrahent.Kontrahent} - stan na {DataDo:dd.MM.yyyy}";

                var tresc = $"Szanowni Państwo,\n\n" +
                    $"W załączeniu przesyłam zestawienie sald opakowań.\n\n" +
                    $"Stan na dzień {DataDo:dd.MM.yyyy}:\n";

                if (Kontrahent.E2 != 0)
                    tresc += $"  - Pojemnik E2: {(Kontrahent.E2 > 0 ? $"Państwo winni {Kontrahent.E2} szt." : $"My winni {Math.Abs(Kontrahent.E2)} szt.")}\n";
                if (Kontrahent.H1 != 0)
                    tresc += $"  - Paleta H1: {(Kontrahent.H1 > 0 ? $"Państwo winni {Kontrahent.H1} szt." : $"My winni {Math.Abs(Kontrahent.H1)} szt.")}\n";
                if (Kontrahent.EURO != 0)
                    tresc += $"  - Paleta EURO: {(Kontrahent.EURO > 0 ? $"Państwo winni {Kontrahent.EURO} szt." : $"My winni {Math.Abs(Kontrahent.EURO)} szt.")}\n";
                if (Kontrahent.PCV != 0)
                    tresc += $"  - Paleta PCV: {(Kontrahent.PCV > 0 ? $"Państwo winni {Kontrahent.PCV} szt." : $"My winni {Math.Abs(Kontrahent.PCV)} szt.")}\n";
                if (Kontrahent.DREW != 0)
                    tresc += $"  - Paleta DREW: {(Kontrahent.DREW > 0 ? $"Państwo winni {Kontrahent.DREW} szt." : $"My winni {Math.Abs(Kontrahent.DREW)} szt.")}\n";

                tresc += "\nProszę o potwierdzenie zgodności salda.\n\nZ poważaniem,\nDział Opakowań";

                // Otwórz Outlook z załącznikiem
                try
                {
                    var outlookPath = @"C:\Program Files\Microsoft Office\root\Office16\OUTLOOK.EXE";
                    if (!System.IO.File.Exists(outlookPath))
                        outlookPath = @"C:\Program Files (x86)\Microsoft Office\root\Office16\OUTLOOK.EXE";

                    if (System.IO.File.Exists(outlookPath))
                    {
                        var args = $"/c ipm.note /a \"{sciezka}\" /m \"?subject={Uri.EscapeDataString(temat)}&body={Uri.EscapeDataString(tresc)}\"";
                        System.Diagnostics.Process.Start(outlookPath, args);
                    }
                    else
                    {
                        // Fallback - otwórz mailto
                        var mailto = $"mailto:?subject={Uri.EscapeDataString(temat)}&body={Uri.EscapeDataString(tresc)}";
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = mailto,
                            UseShellExecute = true
                        });

                        MessageBox.Show($"PDF zapisany w:\n{sciezka}\n\nDodaj ręcznie jako załącznik.", "Email", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch
                {
                    // Otwórz plik PDF
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = sciezka,
                        UseShellExecute = true
                    });
                    MessageBox.Show("Nie udało się otworzyć Outlook. PDF został wygenerowany.", "Email", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
