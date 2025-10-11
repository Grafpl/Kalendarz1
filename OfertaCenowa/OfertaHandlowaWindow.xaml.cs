using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;

namespace Kalendarz1.OfertaCenowa
{
    public partial class OfertaHandlowaWindow : Window, INotifyPropertyChanged
    {
        private bool _isInitializing = true;
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public ObservableCollection<KlientOferta> Klienci { get; set; } = new ObservableCollection<KlientOferta>();
        public ObservableCollection<TowarOferta> Towary { get; set; } = new ObservableCollection<TowarOferta>();
        public ObservableCollection<TowarOferta> TowaryWOfercie { get; set; } = new ObservableCollection<TowarOferta>();
        public ObservableCollection<TowarOferta> FiltrowaneTowary { get; set; } = new ObservableCollection<TowarOferta>();

        private string _aktywnyKatalog = "67095";
        private KlientOferta? _wybranyKlient;

        public OfertaHandlowaWindow()
        {
            _isInitializing = true;
            InitializeComponent();
            this.DataContext = this;

            LoadData();

            dgTowary.ItemsSource = TowaryWOfercie;
            cboSzybkieDodawanieProduktu.ItemsSource = FiltrowaneTowary;

            TowaryWOfercie.CollectionChanged += (s, e) => ObliczWartoscCalkowita();

            _isInitializing = false;
        }

        private async void LoadData()
        {
            try
            {
                await LoadKlienci();
                await LoadTowary();
                FiltrujTowary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadKlienci()
        {
            const string sql = @"
                SELECT c.Id, c.Shortcut AS Nazwa, ISNULL(c.NIP, '') AS NIP, ISNULL(poa.Street, '') AS Adres,
                       ISNULL(poa.PostCode, '') AS KodPocztowy, ISNULL(poa.Place, '') AS Miejscowosc
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                ORDER BY c.Shortcut";

            Klienci.Clear();

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    Klienci.Add(new KlientOferta
                    {
                        Id = rd[0].ToString(),
                        Nazwa = rd[1].ToString() ?? "",
                        NIP = rd[2].ToString() ?? "",
                        Adres = rd[3].ToString() ?? "",
                        KodPocztowy = rd[4].ToString() ?? "",
                        Miejscowosc = rd[5].ToString() ?? ""
                    });
                }
                cmbKlienci.ItemsSource = Klienci;
                cmbKlienci.DisplayMemberPath = "Nazwa";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania klientów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadTowary()
        {
            Towary.Clear();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var excludedProducts = new[] { "KURCZAK B", "FILET C" };
                await using var cmd = new SqlCommand("SELECT Id, Kod, katalog FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY Kod ASC", cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string kod = rd[1]?.ToString() ?? "";
                    if (excludedProducts.Any(excluded => kod.ToUpper().Contains(excluded)))
                        continue;

                    Towary.Add(new TowarOferta { Id = Convert.ToInt32(rd[0]), Nazwa = kod, Katalog = rd[2]?.ToString() ?? "" });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania towarów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbKlienci_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbKlienci.SelectedItem is KlientOferta klient)
            {
                _wybranyKlient = klient;
                txtKlientInfo.Text = $"NIP: {klient.NIP} | {klient.Adres}";
            }
        }

        private void RbTypProduktu_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _aktywnyKatalog = rbSwiezy?.IsChecked == true ? "67095" : "67153";
            FiltrujTowary();
        }

        private void FiltrujTowary()
        {
            FiltrowaneTowary.Clear();
            var towaryDoDodania = Towary.Where(t => t.Katalog == _aktywnyKatalog);
            foreach (var towar in towaryDoDodania)
            {
                FiltrowaneTowary.Add(towar);
            }
        }

        private void BtnSzybkieDodawanie_Click(object sender, RoutedEventArgs e)
        {
            if (cboSzybkieDodawanieProduktu.SelectedItem is not TowarOferta wybranyTowar)
            {
                MessageBox.Show("Wybierz produkt z listy.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtSzybkieDodawanieIlosc.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal ilosc) || ilosc <= 0)
            {
                MessageBox.Show("Wprowadź poprawną ilość.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtSzybkieDodawanieCena.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cena) || cena <= 0)
            {
                MessageBox.Show("Wprowadź poprawną cenę.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TowaryWOfercie.Add(new TowarOferta
            {
                Id = wybranyTowar.Id,
                Nazwa = wybranyTowar.Nazwa,
                Katalog = wybranyTowar.Katalog,
                Ilosc = ilosc,
                CenaJednostkowa = cena
            });

            cboSzybkieDodawanieProduktu.SelectedIndex = -1;
            txtSzybkieDodawanieIlosc.Clear();
            txtSzybkieDodawanieCena.Clear();
        }

        private void BtnUsunTowar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TowarOferta towar)
            {
                TowaryWOfercie.Remove(towar);
            }
        }

        private void ObliczWartoscCalkowita()
        {
            decimal suma = TowaryWOfercie.Sum(t => t.Wartosc);
            txtWartoscCalkowita.Text = $"{suma:N2} zł";
        }

        private void BtnGenerujPDF_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranyKlient == null)
            {
                MessageBox.Show("Wybierz klienta z listy.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!TowaryWOfercie.Any())
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt do oferty.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Plik PDF (*.pdf)|*.pdf",
                FileName = $"Oferta_{_wybranyKlient.Nazwa.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var generator = new OfertaPDFGenerator();
                    string transport = rbTransportWlasny.IsChecked == true ? "Transport własny" : "Transport klienta";

                    generator.GenerujPDF(saveDialog.FileName, _wybranyKlient, TowaryWOfercie.ToList(), txtNotatki.Text, transport);

                    MessageBox.Show("PDF został wygenerowany pomyślnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = saveDialog.FileName, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd generowania PDF: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcjonalność zapisu zostanie zaimplementowana w przyszłości.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // ### PEŁNE DEFINICJE KLAS POMOCNICZYCH ###
    public class KlientOferta : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string NIP { get; set; } = "";
        public string KodPocztowy { get; set; } = "";
        public string Miejscowosc { get; set; } = "";
        public string Adres { get; set; } = "";
        public string OsobaKontaktowa { get; set; } = "";
        public string Telefon { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TowarOferta : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string Katalog { get; set; } = "";

        private decimal _ilosc;
        public decimal Ilosc
        {
            get => _ilosc;
            set { _ilosc = value; OnPropertyChanged(nameof(Ilosc)); OnPropertyChanged(nameof(Wartosc)); }
        }

        private decimal _cenaJednostkowa;
        public decimal CenaJednostkowa
        {
            get => _cenaJednostkowa;
            set { _cenaJednostkowa = value; OnPropertyChanged(nameof(CenaJednostkowa)); OnPropertyChanged(nameof(Wartosc)); }
        }

        public decimal Wartosc => Ilosc * CenaJednostkowa;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}