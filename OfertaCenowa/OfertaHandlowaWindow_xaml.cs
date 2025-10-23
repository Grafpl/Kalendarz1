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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kalendarz1.OfertaCenowa
{
    public partial class OfertaHandlowaWindow : Window, INotifyPropertyChanged
    {
        private bool _isInitializing = true;
        private bool _trybReczny = false;
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public ObservableCollection<KlientOferta> Klienci { get; set; } = new ObservableCollection<KlientOferta>();
        public ObservableCollection<TowarOferta> Towary { get; set; } = new ObservableCollection<TowarOferta>();
        public ObservableCollection<TowarOferta> TowaryWOfercie { get; set; } = new ObservableCollection<TowarOferta>();
        public ObservableCollection<TowarOferta> FiltrowaneTowary { get; set; } = new ObservableCollection<TowarOferta>();

        private string _aktywnyKatalog = "67095";
        private KlientOferta? _wybranyKlient;
        
        // Manager szablonów
        private readonly SzablonyManager _szablonyManager = new SzablonyManager();

        public OfertaHandlowaWindow()
        {
            _isInitializing = true;
            InitializeComponent();
            this.DataContext = this;

            dpSredniaCenaData.SelectedDate = DateTime.Today;

            LoadData();

            dgTowary.ItemsSource = TowaryWOfercie;
            cboSzybkieDodawanieProduktu.ItemsSource = FiltrowaneTowary;

            txtPelnaNazwaProduktu.Text = "-";

            TowaryWOfercie.CollectionChanged += (s, e) => ObliczWartoscCalkowita();

            // Inicjalizuj przykładowe szablony
            InicjalizujSzablonyPrzykladowe();

            _isInitializing = false;
        }

        public OfertaHandlowaWindow(KlientOferta klient) : this()
        {
            if (klient != null)
            {
                UstawKlientaRecznie(klient);
            }
        }

        private void UstawKlientaRecznie(KlientOferta klient)
        {
            _trybReczny = true;
            panelRecznyKlient.Visibility = Visibility.Visible;
            cmbKlienci.IsEnabled = false;
            btnReczneWprowadzenie.Content = "📋 Z bazy";
            txtKlientInfo.Text = "Dane wczytane z CRM. Możesz je edytować.";

            txtRecznyNazwa.Text = klient.Nazwa;
            txtRecznyNIP.Text = klient.NIP;
            txtRecznyAdres.Text = klient.Adres;
            txtRecznyKodPocztowy.Text = klient.KodPocztowy;
            txtRecznyMiejscowosc.Text = klient.Miejscowosc;
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

        private async Task LoadKlienci()
        {
            const string sql = @"
                SELECT c.Id, c.Name AS Nazwa, ISNULL(c.NIP, '') AS NIP, ISNULL(poa.Street, '') AS Adres,
                       ISNULL(poa.PostCode, '') AS KodPocztowy, ISNULL(poa.Place, '') AS Miejscowosc
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                ORDER BY c.Name";

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
                        Id = rd[0].ToString() ?? "",
                        Nazwa = rd[1].ToString() ?? "",
                        NIP = rd[2].ToString() ?? "",
                        Adres = rd[3].ToString() ?? "",
                        KodPocztowy = rd[4].ToString() ?? "",
                        Miejscowosc = rd[5].ToString() ?? "",
                        CzyReczny = false
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

        private async Task LoadTowary()
        {
            Towary.Clear();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var excludedProducts = new[] { "KURCZAK B", "FILET C" };
                await using var cmd = new SqlCommand("SELECT Id, Kod, Nazwa, katalog FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY Kod ASC", cn);
                await using var rd = await cmd.ExecuteReaderAsync();

                while (await rd.ReadAsync())
                {
                    string kod = rd[1]?.ToString() ?? "";
                    if (excludedProducts.Any(excluded => kod.ToUpper().Contains(excluded)))
                        continue;

                    string pelna_nazwa = rd[2]?.ToString() ?? kod;

                    Towary.Add(new TowarOferta
                    {
                        Id = Convert.ToInt32(rd[0]),
                        Kod = kod,
                        Nazwa = pelna_nazwa,
                        Katalog = rd[3]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania towarów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbKlienci_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_trybReczny) return;

            if (cmbKlienci.SelectedItem is KlientOferta klient)
            {
                _wybranyKlient = klient;
                txtKlientInfo.Text = $"NIP: {klient.NIP} | {klient.Adres}, {klient.KodPocztowy} {klient.Miejscowosc}";
            }
        }

        private void BtnReczneWprowadzenie_Click(object sender, RoutedEventArgs e)
        {
            _trybReczny = !_trybReczny;

            if (_trybReczny)
            {
                panelRecznyKlient.Visibility = Visibility.Visible;
                cmbKlienci.IsEnabled = false;
                btnReczneWprowadzenie.Content = "📋 Z bazy";

                txtRecznyNazwa.Clear();
                txtRecznyNIP.Clear();
                txtRecznyAdres.Clear();
                txtRecznyKodPocztowy.Clear();
                txtRecznyMiejscowosc.Clear();
                txtKlientInfo.Text = "Tryb ręcznego wprowadzania odbiorcy";
            }
            else
            {
                panelRecznyKlient.Visibility = Visibility.Collapsed;
                cmbKlienci.IsEnabled = true;
                btnReczneWprowadzenie.Content = "✏️ Ręcznie";

                if (cmbKlienci.SelectedItem is KlientOferta klient)
                {
                    txtKlientInfo.Text = $"NIP: {klient.NIP} | {klient.Adres}, {klient.KodPocztowy} {klient.Miejscowosc}";
                }
            }
        }

        private async void BtnSprawdzNIP_Click(object sender, RoutedEventArgs e)
        {
            string nip = txtRecznyNIP.Text.Trim().Replace("-", "").Replace(" ", "");

            if (string.IsNullOrEmpty(nip) || nip.Length != 10)
            {
                MessageBox.Show("Wprowadź poprawny numer NIP (10 cyfr)", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                btnSprawdzNIP.IsEnabled = false;
                btnSprawdzNIP.Content = "⏳";

                await Task.Delay(1000);
                MessageBox.Show("Funkcja weryfikacji NIP będzie dostępna po integracji z API GUS.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas pobierania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSprawdzNIP.IsEnabled = true;
                btnSprawdzNIP.Content = "🔍";
            }
        }

        private KlientOferta? GetRecznyKlient()
        {
            if (!_trybReczny) return _wybranyKlient;

            if (string.IsNullOrWhiteSpace(txtRecznyNazwa.Text))
            {
                MessageBox.Show("Wprowadź nazwę firmy!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            return new KlientOferta
            {
                Id = "RECZNY",
                Nazwa = txtRecznyNazwa.Text.Trim(),
                NIP = txtRecznyNIP.Text.Trim(),
                Adres = txtRecznyAdres.Text.Trim(),
                KodPocztowy = txtRecznyKodPocztowy.Text.Trim(),
                Miejscowosc = txtRecznyMiejscowosc.Text.Trim(),
                CzyReczny = true
            };
        }

        private void RbTypProduktu_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            _aktywnyKatalog = rbSwiezy?.IsChecked == true ? "67095" : "67153";
            FiltrujTowary();
        }

        private void CboSzybkieDodawanieProduktu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboSzybkieDodawanieProduktu.SelectedItem is TowarOferta towar)
            {
                txtPelnaNazwaProduktu.Text = towar.Nazwa;
            }
            else
            {
                txtPelnaNazwaProduktu.Text = "-";
            }
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

        private void ChkTylkoCena_Changed(object sender, RoutedEventArgs e)
        {
            bool tylkoCena = chkTylkoCena.IsChecked == true;

            if (dgTowary.Columns.Count > 2)
            {
                dgTowary.Columns[2].Visibility = tylkoCena ? Visibility.Collapsed : Visibility.Visible;
                dgTowary.Columns[5].Visibility = tylkoCena ? Visibility.Collapsed : Visibility.Visible;
            }

            txtSzybkieDodawanieIlosc.IsEnabled = !tylkoCena;

            if (tylkoCena)
            {
                txtSzybkieDodawanieIlosc.Text = "1";
            }
        }

        private void BtnSzybkieDodawanie_Click(object sender, RoutedEventArgs e)
        {
            if (cboSzybkieDodawanieProduktu.SelectedItem is not TowarOferta wybranyTowar)
            {
                MessageBox.Show("Wybierz produkt z listy.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal ilosc = 0;
            bool tylkoCena = chkTylkoCena.IsChecked == true;

            if (!tylkoCena)
            {
                if (string.IsNullOrWhiteSpace(txtSzybkieDodawanieIlosc.Text))
                {
                    ilosc = 0;
                }
                else if (!decimal.TryParse(txtSzybkieDodawanieIlosc.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out ilosc) || ilosc < 0)
                {
                    MessageBox.Show("Wprowadź poprawną ilość (lub zostaw puste).", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            decimal cena = 0;
            if (string.IsNullOrWhiteSpace(txtSzybkieDodawanieCena.Text))
            {
                cena = 0;
            }
            else if (!decimal.TryParse(txtSzybkieDodawanieCena.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out cena) || cena < 0)
            {
                MessageBox.Show("Wprowadź poprawną cenę (lub zostaw puste).", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string opakowanie = (cboSzybkieDodawanieOpakowanie.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "E2";

            TowaryWOfercie.Add(new TowarOferta
            {
                Id = wybranyTowar.Id,
                Kod = wybranyTowar.Kod,
                Nazwa = wybranyTowar.Nazwa,
                Katalog = wybranyTowar.Katalog,
                Ilosc = ilosc,
                CenaJednostkowa = cena,
                Opakowanie = opakowanie
            });

            cboSzybkieDodawanieProduktu.SelectedIndex = -1;
            txtPelnaNazwaProduktu.Text = "-";
            txtSzybkieDodawanieIlosc.Clear();
            txtSzybkieDodawanieCena.Clear();
            cboSzybkieDodawanieOpakowanie.SelectedIndex = 0;
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

        private async void BtnPobierzSrednieCeny_Click(object sender, RoutedEventArgs e)
        {
            if (dpSredniaCenaData.SelectedDate == null)
            {
                MessageBox.Show("Wybierz datę, z której mają zostać pobrane ceny.", "Brak daty", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtProcentNarzuconejCeny.Text, out decimal procentMarzy) || procentMarzy < 0)
            {
                MessageBox.Show("Wprowadź poprawną wartość procentową marży (np. 20).", "Błędna marża", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (TowaryWOfercie.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jeden produkt do oferty, aby pobrać dla niego cenę.", "Brak towarów", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DateTime data = dpSredniaCenaData.SelectedDate.Value;
            var towaryIds = TowaryWOfercie.Select(t => t.Id).ToList();
            var cenySrednie = new Dictionary<int, decimal>();
            int zaktualizowano = 0;

            btnPobierzSrednieCeny.IsEnabled = false;
            btnPobierzSrednieCeny.Content = "Pobieranie...";

            try
            {
                var parametryZapytania = new List<string>();
                var sqlParameters = new List<SqlParameter> { new SqlParameter("@Data", data) };
                for (int i = 0; i < towaryIds.Count; i++)
                {
                    var paramName = $"@p{i}";
                    parametryZapytania.Add(paramName);
                    sqlParameters.Add(new SqlParameter(paramName, towaryIds[i]));
                }

                string sql = $@"
                    SELECT 
                        DP.idtw AS TowarId,
                        CAST(SUM(DP.wartNetto) / NULLIF(SUM(DP.ilosc), 0) AS DECIMAL(18,2)) AS SredniaCena
                    FROM [HANDEL].[HM].[DK] DK
                    JOIN [HANDEL].[HM].[DP] DP ON DP.super = DK.id
                    WHERE CONVERT(date, DK.data) = @Data
                      AND DP.idtw IN ({string.Join(",", parametryZapytania)})
                      AND DK.anulowany = 0
                    GROUP BY DP.idtw
                    HAVING SUM(DP.ilosc) > 0";

                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddRange(sqlParameters.ToArray());

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    cenySrednie.Add(rd.GetInt32(0), Convert.ToDecimal(rd[1]));
                }

                foreach (var towar in TowaryWOfercie)
                {
                    if (cenySrednie.TryGetValue(towar.Id, out decimal sredniaCena))
                    {
                        towar.CenaJednostkowa = Math.Round(sredniaCena * (1 + (procentMarzy / 100M)), 2);
                        zaktualizowano++;
                    }
                }

                if (chkPdfPokazIlosc.IsChecked == false)
                {
                    string notatka = "Cena zależna od ilości kupionej.";
                    if (!txtNotatki.Text.Contains(notatka))
                    {
                        if (string.IsNullOrWhiteSpace(txtNotatki.Text))
                        {
                            txtNotatki.Text = notatka;
                        }
                        else
                        {
                            txtNotatki.Text += $"\n{notatka}";
                        }
                    }
                }

                MessageBox.Show($"Pomyślnie zaktualizowano ceny dla {zaktualizowano} z {TowaryWOfercie.Count} towarów.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas pobierania cen: {ex.Message}", "Błąd bazy danych", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnPobierzSrednieCeny.IsEnabled = true;
                btnPobierzSrednieCeny.Content = "Pobierz i oblicz ceny";
            }
        }

        private void BtnGenerujPDF_Click(object sender, RoutedEventArgs e)
        {
            var klient = GetRecznyKlient();

            if (klient == null)
            {
                MessageBox.Show("Wybierz lub wprowadź dane klienta.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                FileName = $"Oferta_{klient.Nazwa.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var generator = new OfertaPDFGenerator();
                    string transport = rbTransportWlasny.IsChecked == true ? "Transport własny" : "Transport klienta";

                    string terminPlatnosci = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "7 dni";
                    string tagTermin = (cboTerminPlatnosci.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "7";

                    string walutaKonta = (cboKontoBankowe.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "PLN";
                    bool tylkoCena = chkTylkoCena.IsChecked == true;

                    string jezykTag = (cboJezykPDF.SelectedItem as ComboBoxItem)?.Tag.ToString() ?? "Polski";
                    JezykOferty jezyk = jezykTag == "English" ? JezykOferty.English : JezykOferty.Polski;

                    TypLogo typLogo = rbLogoOkragle.IsChecked == true ? TypLogo.Okragle : TypLogo.Dlugie;

                    var parametry = new ParametryOferty
                    {
                        TerminPlatnosci = terminPlatnosci,
                        DniPlatnosci = int.Parse(tagTermin),
                        WalutaKonta = walutaKonta,
                        PokazTylkoCeny = tylkoCena,
                        Jezyk = jezyk,
                        TypLogo = typLogo,
                        PokazOpakowanie = chkPdfPokazOpakowanie.IsChecked == true,
                        PokazCene = chkPdfPokazCene.IsChecked == true,
                        PokazIlosc = chkPdfPokazIlosc.IsChecked == true,
                        PokazTerminPlatnosci = chkPdfPokazTermin.IsChecked == true
                    };

                    generator.GenerujPDF(saveDialog.FileName, klient, TowaryWOfercie.ToList(), txtNotatki.Text, transport, parametry);

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

        // ===================================================================================
        // NOWE METODY DO OBSŁUGI SZABLONÓW
        // ===================================================================================

        /// <summary>
        /// Otwiera okno zarządzania szablonami towarów
        /// </summary>
        private void BtnZarzadzajSzablonami_TowarowClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var okno = new SzablonTowarowWindow(Towary.ToList());
                okno.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania okna szablonów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Otwiera okno zarządzania szablonami parametrów
        /// </summary>
        private void BtnZarzadzajSzablonami_ParametrowClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var okno = new SzablonParametrowWindow();
                okno.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas otwierania okna szablonów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Wczytuje szablon towarów i dodaje towary do oferty
        /// </summary>
        private void BtnWczytajSzablonTowarow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var szablony = _szablonyManager.WczytajSzablonyTowarow();
                
                if (!szablony.Any())
                {
                    var wynik = MessageBox.Show(
                        "Nie masz jeszcze żadnych zapisanych szablonów towarów.\n\nCzy chcesz utworzyć nowy szablon?",
                        "Brak szablonów",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );
                    
                    if (wynik == MessageBoxResult.Yes)
                    {
                        BtnZarzadzajSzablonami_TowarowClick(sender, e);
                    }
                    return;
                }

                // Okno wyboru szablonu
                var oknoWyboru = new WyborSzablonuWindow(szablony.Cast<object>().ToList(), "Wybierz szablon towarów");
                if (oknoWyboru.ShowDialog() == true && oknoWyboru.WybranyIndex >= 0)
                {
                    var wybrany = szablony[oknoWyboru.WybranyIndex];
                    WczytajSzablonTowarowDoOferty(wybrany);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania szablonu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Wczytuje szablon parametrów i ustawia wszystkie opcje
        /// </summary>
        private void BtnWczytajSzablonParametrow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var szablony = _szablonyManager.WczytajSzablonyParametrow();
                
                if (!szablony.Any())
                {
                    var wynik = MessageBox.Show(
                        "Nie masz jeszcze żadnych zapisanych szablonów parametrów.\n\nCzy chcesz utworzyć nowy szablon?",
                        "Brak szablonów",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );
                    
                    if (wynik == MessageBoxResult.Yes)
                    {
                        BtnZarzadzajSzablonami_ParametrowClick(sender, e);
                    }
                    return;
                }

                // Okno wyboru szablonu
                var oknoWyboru = new WyborSzablonuWindow(szablony.Cast<object>().ToList(), "Wybierz szablon parametrów");
                if (oknoWyboru.ShowDialog() == true && oknoWyboru.WybranyIndex >= 0)
                {
                    var wybrany = szablony[oknoWyboru.WybranyIndex];
                    UstawParametryZSzablonu(wybrany);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania szablonu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Wczytuje towary z szablonu do oferty
        /// </summary>
        private void WczytajSzablonTowarowDoOferty(SzablonTowarow szablon)
        {
            int dodano = 0;
            int pominięto = 0;

            foreach (var towarSzablonu in szablon.Towary)
            {
                // Sprawdź czy towar już jest w ofercie
                if (TowaryWOfercie.Any(t => t.Id == towarSzablonu.TowarId))
                {
                    pominięto++;
                    continue;
                }

                // Znajdź pełne dane towaru
                var towarPelny = Towary.FirstOrDefault(t => t.Id == towarSzablonu.TowarId);
                if (towarPelny == null)
                {
                    pominięto++;
                    continue;
                }

                // Dodaj towar do oferty
                TowaryWOfercie.Add(new TowarOferta
                {
                    Id = towarPelny.Id,
                    Kod = towarPelny.Kod,
                    Nazwa = towarPelny.Nazwa,
                    Katalog = towarPelny.Katalog,
                    Opakowanie = towarSzablonu.Opakowanie,
                    Ilosc = towarSzablonu.DomyslnaIlosc,
                    CenaJednostkowa = towarSzablonu.DomyslnaCena
                });
                dodano++;
            }

            if (dodano > 0)
            {
                MessageBox.Show(
                    $"Wczytano szablon \"{szablon.Nazwa}\":\n✓ Dodano {dodano} produktów" + 
                    (pominięto > 0 ? $"\n⚠ Pominięto {pominięto} (już w ofercie lub niedostępne)" : ""),
                    "Szablon wczytany",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                MessageBox.Show(
                    "Nie dodano żadnych produktów. Wszystkie towary z szablonu już są w ofercie lub są niedostępne.",
                    "Informacja",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        /// <summary>
        /// Ustawia parametry oferty na podstawie szablonu
        /// </summary>
        private void UstawParametryZSzablonu(SzablonParametrow szablon)
        {
            try
            {
                // Termin płatności
                foreach (ComboBoxItem item in cboTerminPlatnosci.Items)
                {
                    if (item.Tag.ToString() == szablon.DniPlatnosci.ToString())
                    {
                        cboTerminPlatnosci.SelectedItem = item;
                        break;
                    }
                }

                // Konto bankowe
                foreach (ComboBoxItem item in cboKontoBankowe.Items)
                {
                    if (item.Tag.ToString() == szablon.WalutaKonta)
                    {
                        cboKontoBankowe.SelectedItem = item;
                        break;
                    }
                }

                // Transport
                rbTransportWlasny.IsChecked = szablon.TransportTyp == "wlasny";
                rbTransportKlienta.IsChecked = szablon.TransportTyp == "klienta";

                // Język
                foreach (ComboBoxItem item in cboJezykPDF.Items)
                {
                    if (item.Tag.ToString() == szablon.Jezyk.ToString())
                    {
                        cboJezykPDF.SelectedItem = item;
                        break;
                    }
                }

                // Logo
                rbLogoOkragle.IsChecked = szablon.TypLogo == TypLogo.Okragle;
                rbLogoDlugie.IsChecked = szablon.TypLogo == TypLogo.Dlugie;

                // Widoczność w PDF
                chkPdfPokazOpakowanie.IsChecked = szablon.PokazOpakowanie;
                chkPdfPokazCene.IsChecked = szablon.PokazCene;
                chkPdfPokazIlosc.IsChecked = szablon.PokazIlosc;
                chkPdfPokazTermin.IsChecked = szablon.PokazTerminPlatnosci;

                // Notatki
                if (szablon.DodajNotkeOCenach)
                {
                    string notatka = "Cena zależna od ilości kupionej.";
                    if (!txtNotatki.Text.Contains(notatka))
                    {
                        if (string.IsNullOrWhiteSpace(txtNotatki.Text))
                        {
                            txtNotatki.Text = notatka;
                        }
                        else
                        {
                            txtNotatki.Text += $"\n{notatka}";
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(szablon.NotatkaCustom))
                {
                    if (!txtNotatki.Text.Contains(szablon.NotatkaCustom))
                    {
                        if (string.IsNullOrWhiteSpace(txtNotatki.Text))
                        {
                            txtNotatki.Text = szablon.NotatkaCustom;
                        }
                        else
                        {
                            txtNotatki.Text += $"\n{szablon.NotatkaCustom}";
                        }
                    }
                }

                MessageBox.Show(
                    $"Wczytano szablon parametrów \"{szablon.Nazwa}\".\nWszystkie ustawienia zostały zaktualizowane.",
                    "Szablon wczytany",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ustawiania parametrów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Inicjalizuje przykładowe szablony przy pierwszym uruchomieniu
        /// </summary>
        private void InicjalizujSzablonyPrzykladowe()
        {
            try
            {
                _szablonyManager.UtworzSzablonyPrzykladowe();
            }
            catch
            {
                // Ignoruj błędy inicjalizacji
            }
        }

        /// <summary>
        /// Obsługa checkboxa automatycznej notatki o cenach zależnych od kilogramów
        /// </summary>
        private void ChkDodajNotkeKilogramy_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            string notatka = "Cena zależna od ilości kupionej.";
            
            if (chkDodajNotkeKilogramy.IsChecked == true)
            {
                // Dodaj notatkę jeśli jej nie ma
                if (!txtNotatki.Text.Contains(notatka))
                {
                    if (string.IsNullOrWhiteSpace(txtNotatki.Text))
                    {
                        txtNotatki.Text = notatka;
                    }
                    else
                    {
                        txtNotatki.Text += $"\n{notatka}";
                    }
                }
            }
            else
            {
                // Usuń notatkę
                txtNotatki.Text = txtNotatki.Text.Replace(notatka, "").Trim();
                // Usuń podwójne puste linie
                while (txtNotatki.Text.Contains("\n\n\n"))
                {
                    txtNotatki.Text = txtNotatki.Text.Replace("\n\n\n", "\n\n");
                }
            }
        }
    }
}
