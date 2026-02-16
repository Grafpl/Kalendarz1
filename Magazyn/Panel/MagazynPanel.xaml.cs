using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1
{
    public partial class MagazynPanel : Window, INotifyPropertyChanged
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private static bool? _dataAkceptacjiMagazynColumnExists = null;
        private static bool? _strefaColumnExists = null;
        private static bool? _czyModMagazynuColumnExists = null;
        private readonly Dictionary<int, System.Windows.Media.Imaging.BitmapImage?> _productImages = new();

        public string UserID { get; set; } = "Magazynier";
        private DateTime _selectedDate = DateTime.Today;
        private DispatcherTimer refreshTimer;
        private readonly Dictionary<int, ZamowienieInfo> _zamowienia = new();
        private int? _filteredProductId = null;
        private Dictionary<int, string> _produktLookup = new();
        private Button _selectedProductButton = null;

        public ObservableCollection<ZamowienieViewModel> ZamowieniaList1 { get; set; } = new();
        public ObservableCollection<ZamowienieViewModel> ZamowieniaList2 { get; set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MagazynPanel()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            this.DataContext = this;
            InitializeAsync();
            KeyDown += MagazynPanel_KeyDown;
            StartAutoRefresh();
        }

        private List<HistoriaWydaniaItem> _historiaWydanAll = new(); // Pełna lista bez filtrów

        private async void InitializeAsync()
        {
            // Ustaw domyślny zakres dat dla Historii wydań - ostatnie 30 dni
            dpHistoriaOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpHistoriaDo.SelectedDate = DateTime.Today;

            await LoadProductImagesAsync();
            await ReloadAllAsync();
        }

        private void MagazynPanel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        #region Event Handlers
        private async void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-1);
            await ReloadAllAsync();
        }

        private async void btnNext_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(1);
            await ReloadAllAsync();
        }

        private async void btnToday_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            await ReloadAllAsync();
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await ReloadAllAsync();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnTabZamowienia_Click(object sender, RoutedEventArgs e)
        {
            pnlZamowienia.Visibility = Visibility.Visible;
            pnlHistoria.Visibility = Visibility.Collapsed;
            btnTabZamowienia.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x7A, 0x5A));
            btnTabZamowienia.Foreground = Brushes.White;
            btnTabHistoria.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x48));
            btnTabHistoria.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        }

        private async void btnTabHistoria_Click(object sender, RoutedEventArgs e)
        {
            pnlZamowienia.Visibility = Visibility.Collapsed;
            pnlHistoria.Visibility = Visibility.Visible;
            btnTabHistoria.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x7A, 0x5A));
            btnTabHistoria.Foreground = Brushes.White;
            btnTabZamowienia.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x48));
            btnTabZamowienia.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
            // Załaduj historię jeśli daty ustawione
            if (dpHistoriaOd.SelectedDate.HasValue && dpHistoriaDo.SelectedDate.HasValue)
                await LoadHistoriaWydanAsync(dpHistoriaOd.SelectedDate.Value, dpHistoriaDo.SelectedDate.Value);
        }

        private async void chkShowShipmentsOnly_CheckedChanged(object sender, RoutedEventArgs e)
        {
            await ReloadAllAsync();
        }

        private async void btnMarkAsShipped_Click(object sender, RoutedEventArgs e)
        {
            var selected = (dgvZamowienia1.SelectedItem ?? dgvZamowienia2.SelectedItem) as ZamowienieViewModel;

            if (selected == null)
            {
                MessageBox.Show("Nie wybrano żadnego zamówienia!", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selected.Status == "Wydany")
            {
                MessageBox.Show("To zamówienie jest już oznaczone jako wydane!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Pobierz pozycje zamówienia do dialogu (filtrowane jeśli wybrany konkretny towar)
            var pozycje = await LoadOrderPositionsForDialogAsync(selected.Info.Id, _filteredProductId);

            if (!pozycje.Any())
            {
                MessageBox.Show("Brak pozycji w zamówieniu!", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pokaż dialog wydania
            string tytulKlienta = _filteredProductId.HasValue && _produktLookup.ContainsKey(_filteredProductId.Value)
                ? $"{selected.Info.Klient} ({_produktLookup[_filteredProductId.Value]})"
                : selected.Info.Klient;
            var dialog = new WydanieDialog(tytulKlienta, pozycje);
            dialog.Owner = this;
            var dialogResult = dialog.ShowDialog();

            if (dialogResult != true || !dialog.Zatwierdzone)
            {
                return; // Anulowano
            }

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Jeśli nie wszystko wydane, zapisz różnice
                if (!dialog.WszystkoWydane)
                {
                    await SaveWydanieRozniczeAsync(cn, selected.Info.Id, dialog.Pozycje.ToList(), dialog.UwagiWydania);
                }

                // Ustaw CzyWydane + DataWydania + KtoWydal + Status (dla kompatybilności)
                var cmd = new SqlCommand(@"UPDATE dbo.ZamowieniaMieso
                                           SET CzyWydane = 1,
                                               DataWydania = GETDATE(),
                                               KtoWydal = @UserID,
                                               Status = 'Wydany',
                                               CzyWszystkoWydane = @CzyWszystko,
                                               UwagiWydania = @Uwagi
                                           WHERE Id = @Id", cn);
                cmd.Parameters.AddWithValue("@Id", selected.Info.Id);
                cmd.Parameters.AddWithValue("@UserID", UserID);
                cmd.Parameters.AddWithValue("@CzyWszystko", dialog.WszystkoWydane);
                cmd.Parameters.AddWithValue("@Uwagi", string.IsNullOrEmpty(dialog.UwagiWydania) ? DBNull.Value : dialog.UwagiWydania);

                await cmd.ExecuteNonQueryAsync();

                string msg = dialog.WszystkoWydane
                    ? "Zamówienie zostało oznaczone jako WYDANE (wszystko zgodnie z zamówieniem)!"
                    : "Zamówienie zostało oznaczone jako WYDANE (z różnicami - zapisano szczegóły)!";
                MessageBox.Show(msg, "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Odśwież listę
                await ReloadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zmiany statusu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<List<(int TowarId, string Nazwa, decimal Zamowiono)>> LoadOrderPositionsForDialogAsync(int zamowienieId, int? filteredProductId = null)
        {
            var pozycje = new List<(int, string, decimal)>();
            var towarIds = new List<int>();
            var ilosciMap = new Dictionary<int, decimal>();

            // Pobierz pozycje zamówienia z LibraNet (z opcjonalnym filtrem produktu)
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = "SELECT KodTowaru, Ilosc FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @Id";
                if (filteredProductId.HasValue)
                    sql += " AND KodTowaru = @ProductId";

                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", zamowienieId);
                if (filteredProductId.HasValue)
                    cmd.Parameters.AddWithValue("@ProductId", filteredProductId.Value);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int towarId = rd.GetInt32(0);
                    decimal ilosc = rd.GetDecimal(1);
                    towarIds.Add(towarId);
                    ilosciMap[towarId] = ilosc;
                }
            }

            if (!towarIds.Any()) return pozycje;

            // Pobierz nazwy towarów z Handel
            var nazwyMap = new Dictionary<int, string>();
            using (var cn = new SqlConnection(_connHandel))
            {
                await cn.OpenAsync();
                var cmd = new SqlCommand($"SELECT ID, kod FROM HM.TW WHERE ID IN ({string.Join(",", towarIds)})", cn);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    nazwyMap[rd.GetInt32(0)] = rd.GetString(1);
                }
            }

            // Połącz dane
            foreach (var towarId in towarIds)
            {
                string nazwa = nazwyMap.ContainsKey(towarId) ? nazwyMap[towarId] : $"Towar {towarId}";
                pozycje.Add((towarId, nazwa, ilosciMap[towarId]));
            }

            return pozycje.OrderBy(p => p.Item2).ToList();
        }

        private async Task SaveWydanieRozniczeAsync(SqlConnection cn, int zamowienieId, List<WydanieItem> pozycje, string uwagi)
        {
            // Sprawdź czy tabela istnieje, jeśli nie - utwórz
            var checkCmd = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE name='ZamowienieWydanieRoznice' AND type='U'", cn);
            if ((int)await checkCmd.ExecuteScalarAsync() == 0)
            {
                var createCmd = new SqlCommand(@"
                    CREATE TABLE dbo.ZamowienieWydanieRoznice (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        ZamowienieId INT NOT NULL,
                        KodTowaru INT NOT NULL,
                        IloscZamowiona DECIMAL(18,3) NOT NULL,
                        IloscWydana DECIMAL(18,3) NOT NULL,
                        Roznica DECIMAL(18,3) NOT NULL,
                        DataWpisu DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE INDEX IX_WydanieRoznice_ZamowienieId ON dbo.ZamowienieWydanieRoznice(ZamowienieId);", cn);
                await createCmd.ExecuteNonQueryAsync();
            }

            // Sprawdź/dodaj kolumny do ZamowieniaMieso jeśli nie istnieją
            var checkCol1 = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'CzyWszystkoWydane'", cn);
            if ((int)await checkCol1.ExecuteScalarAsync() == 0)
            {
                await new SqlCommand("ALTER TABLE dbo.ZamowieniaMieso ADD CzyWszystkoWydane BIT NULL", cn).ExecuteNonQueryAsync();
            }

            var checkCol2 = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'UwagiWydania'", cn);
            if ((int)await checkCol2.ExecuteScalarAsync() == 0)
            {
                await new SqlCommand("ALTER TABLE dbo.ZamowieniaMieso ADD UwagiWydania NVARCHAR(500) NULL", cn).ExecuteNonQueryAsync();
            }

            // Usuń stare różnice dla tego zamówienia
            var delCmd = new SqlCommand("DELETE FROM dbo.ZamowienieWydanieRoznice WHERE ZamowienieId = @ZamId", cn);
            delCmd.Parameters.AddWithValue("@ZamId", zamowienieId);
            await delCmd.ExecuteNonQueryAsync();

            // Zapisz nowe różnice (tylko te które mają różnicę)
            foreach (var poz in pozycje.Where(p => p.Zamowiono != p.Wydano))
            {
                var insCmd = new SqlCommand(@"
                    INSERT INTO dbo.ZamowienieWydanieRoznice (ZamowienieId, KodTowaru, IloscZamowiona, IloscWydana, Roznica)
                    VALUES (@ZamId, @TowarId, @Zamowiono, @Wydano, @Roznica)", cn);
                insCmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                insCmd.Parameters.AddWithValue("@TowarId", poz.TowarId);
                insCmd.Parameters.AddWithValue("@Zamowiono", poz.Zamowiono);
                insCmd.Parameters.AddWithValue("@Wydano", poz.Wydano);
                insCmd.Parameters.AddWithValue("@Roznica", poz.Roznica);
                await insCmd.ExecuteNonQueryAsync();
            }
        }

        private async void btnUndoShipment_Click(object sender, RoutedEventArgs e)
        {
            var selected = (dgvZamowienia1.SelectedItem ?? dgvZamowienia2.SelectedItem) as ZamowienieViewModel;

            if (selected == null)
            {
                MessageBox.Show("Nie wybrano żadnego zamówienia!", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selected.Status != "Wydany")
            {
                MessageBox.Show("To zamówienie nie jest oznaczone jako wydane!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Czy na pewno chcesz COFNĄĆ status 'Wydany' dla zamówienia klienta '{selected.Klient}'?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    // Cofnij tylko CzyWydane (nie ruszaj CzyZrealizowane)
                    var cmd = new SqlCommand(@"UPDATE dbo.ZamowieniaMieso
                                               SET CzyWydane = 0,
                                                   DataWydania = NULL,
                                                   KtoWydal = NULL,
                                                   Status = CASE WHEN CzyZrealizowane = 1 THEN 'Zrealizowane' ELSE 'Nowe' END
                                               WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", selected.Info.Id);

                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Status zamówienia został cofnięty do 'Nowe'!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Odśwież listę
                    await ReloadAllAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas cofania statusu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void dgvZamowienia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == dgvZamowienia1 && dgvZamowienia1.SelectedItem != null)
            {
                dgvZamowienia2.SelectedItem = null;
            }
            else if (sender == dgvZamowienia2 && dgvZamowienia2.SelectedItem != null)
            {
                dgvZamowienia1.SelectedItem = null;
            }
            await LoadPozycjeForSelectedAsync();
        }

        // ============ HISTORIA WYDAŃ ============
        private async void btnLoadHistoria_Click(object sender, RoutedEventArgs e)
        {
            if (dpHistoriaOd.SelectedDate == null || dpHistoriaDo.SelectedDate == null)
            {
                MessageBox.Show("Proszę wybrać zakres dat!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await LoadHistoriaWydanAsync(dpHistoriaOd.SelectedDate.Value, dpHistoriaDo.SelectedDate.Value);
        }

        private async void btnHistoriaToday_Click(object sender, RoutedEventArgs e)
        {
            dpHistoriaOd.SelectedDate = DateTime.Today;
            dpHistoriaDo.SelectedDate = DateTime.Today;
            await LoadHistoriaWydanAsync(DateTime.Today, DateTime.Today);
        }

        private async void btnHistoriaWeek_Click(object sender, RoutedEventArgs e)
        {
            var start = DateTime.Today.AddDays(-7);
            var end = DateTime.Today;
            dpHistoriaOd.SelectedDate = start;
            dpHistoriaDo.SelectedDate = end;
            await LoadHistoriaWydanAsync(start, end);
        }

        private async void btnHistoriaMonth_Click(object sender, RoutedEventArgs e)
        {
            var start = DateTime.Today.AddDays(-30);
            var end = DateTime.Today;
            dpHistoriaOd.SelectedDate = start;
            dpHistoriaDo.SelectedDate = end;
            await LoadHistoriaWydanAsync(start, end);
        }

        private void cmbHistoriaFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyHistoriaFilters();
        }

        private void btnHistoriaClearFilters_Click(object sender, RoutedEventArgs e)
        {
            cmbHistoriaUzytkownik.SelectedIndex = 0;
            cmbHistoriaKlient.SelectedIndex = 0;
            cmbHistoriaStatus.SelectedIndex = 0;
            ApplyHistoriaFilters();
        }

        private void ApplyHistoriaFilters()
        {
            if (_historiaWydanAll == null || !_historiaWydanAll.Any())
            {
                return;
            }

            var filtered = _historiaWydanAll.AsEnumerable();

            // Filtr użytkownika
            if (cmbHistoriaUzytkownik.SelectedIndex > 0 && cmbHistoriaUzytkownik.SelectedItem is string selectedUser)
            {
                filtered = filtered.Where(h => h.KtoWydal == selectedUser);
            }

            // Filtr klienta
            if (cmbHistoriaKlient.SelectedIndex > 0 && cmbHistoriaKlient.SelectedItem is string selectedKlient)
            {
                filtered = filtered.Where(h => h.Klient == selectedKlient);
            }

            // Filtr statusu
            if (cmbHistoriaStatus.SelectedIndex > 0)
            {
                var statusItem = cmbHistoriaStatus.SelectedItem as ComboBoxItem;
                string statusFilter = statusItem?.Content?.ToString() ?? "";
                if (statusFilter.Contains("Pełne"))
                    filtered = filtered.Where(h => h.StatusWydania.Contains("Pełne"));
                else if (statusFilter.Contains("różnicami"))
                    filtered = filtered.Where(h => h.StatusWydania.Contains("różnicami"));
            }

            var result = filtered.ToList();
            dgvHistoriaWydan.ItemsSource = result;

            // Podsumowanie (dla przefiltrowanych)
            lblHistoriaCount.Text = result.Count.ToString();
            lblHistoriaSumaKg.Text = result.Sum(h => h.IloscKg).ToString("N0");
            lblHistoriaPelne.Text = result.Count(h => h.StatusWydania.Contains("Pełne")).ToString();
            lblHistoriaRoznice.Text = result.Count(h => h.StatusWydania.Contains("różnicami")).ToString();
        }

        private async Task LoadHistoriaWydanAsync(DateTime dataOd, DateTime dataDo)
        {
            _historiaWydanAll.Clear();

            try
            {
                // Pobierz zamówienia z wydaniami z LibraNet
                var zamowienia = new List<(int Id, int KlientId, DateTime DataWydania, string KtoWydal, decimal Ilosc, bool CzyWszystkoWydane, string Uwagi)>();

                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    // Sprawdź czy kolumny istnieją
                    var checkCol = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'CzyWszystkoWydane'", cn);
                    bool hasCzyWszystko = (int)await checkCol.ExecuteScalarAsync() > 0;

                    var checkCol2 = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'UwagiWydania'", cn);
                    bool hasUwagiWydania = (int)await checkCol2.ExecuteScalarAsync() > 0;

                    string czyWszystkoCol = hasCzyWszystko ? "ISNULL(z.CzyWszystkoWydane, 1)" : "1";
                    string uwagiWydaniaCol = hasUwagiWydania ? "ISNULL(z.UwagiWydania, '')" : "''";

                    string sql = $@"
                        SELECT z.Id, z.KlientId, z.DataWydania, ISNULL(z.KtoWydal, '') AS KtoWydal,
                               (SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id) AS TotalIlosc,
                               {czyWszystkoCol} AS CzyWszystkoWydane,
                               {uwagiWydaniaCol} AS UwagiWydania
                        FROM dbo.ZamowieniaMieso z
                        WHERE z.CzyWydane = 1
                          AND z.DataWydania >= @Od AND z.DataWydania < @DoPlus
                        ORDER BY z.DataWydania DESC";

                    var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@Od", dataOd.Date);
                    cmd.Parameters.AddWithValue("@DoPlus", dataDo.Date.AddDays(1)); // Do końca dnia

                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        zamowienia.Add((
                            rd.GetInt32(0),
                            rd.GetInt32(1),
                            rd.GetDateTime(2),
                            rd.GetString(3),
                            rd.IsDBNull(4) ? 0 : rd.GetDecimal(4),
                            rd.GetBoolean(5),
                            rd.GetString(6)
                        ));
                    }
                }

                if (!zamowienia.Any())
                {
                    dgvHistoriaWydan.ItemsSource = null;
                    cmbHistoriaUzytkownik.ItemsSource = new[] { "Wszyscy" };
                    cmbHistoriaUzytkownik.SelectedIndex = 0;
                    cmbHistoriaKlient.ItemsSource = new[] { "Wszyscy" };
                    cmbHistoriaKlient.SelectedIndex = 0;
                    lblHistoriaCount.Text = "0";
                    lblHistoriaSumaKg.Text = "0";
                    lblHistoriaPelne.Text = "0";
                    lblHistoriaRoznice.Text = "0";
                    return;
                }

                // Pobierz nazwy operatorów (kto wydał)
                var operatorIds = zamowienia
                    .Where(z => !string.IsNullOrEmpty(z.KtoWydal) && int.TryParse(z.KtoWydal, out _))
                    .Select(z => int.Parse(z.KtoWydal))
                    .Distinct()
                    .ToList();
                var operatorNames = await LoadOperatorNamesAsync(operatorIds);

                // Pobierz nazwy klientów
                var klientIds = zamowienia.Select(z => z.KlientId).Distinct().ToList();
                var klienci = await LoadContractorsAsync(klientIds);

                // Połącz dane
                foreach (var z in zamowienia)
                {
                    string klientNazwa = klienci.ContainsKey(z.KlientId) ? klienci[z.KlientId].Shortcut : $"KH {z.KlientId}";
                    string ktoWydalNazwa = z.KtoWydal;
                    if (!string.IsNullOrEmpty(z.KtoWydal) && int.TryParse(z.KtoWydal, out int opId) && operatorNames.ContainsKey(opId))
                    {
                        ktoWydalNazwa = operatorNames[opId];
                    }

                    _historiaWydanAll.Add(new HistoriaWydaniaItem
                    {
                        DataWydania = z.DataWydania,
                        Klient = klientNazwa,
                        IloscKg = z.Ilosc,
                        KtoWydal = ktoWydalNazwa,
                        StatusWydania = z.CzyWszystkoWydane ? "✅ Pełne" : "⚠️ Z różnicami",
                        Uwagi = z.Uwagi
                    });
                }

                // Wypełnij filtry
                var uzytkownicy = new List<string> { "Wszyscy" };
                uzytkownicy.AddRange(_historiaWydanAll.Select(h => h.KtoWydal).Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k));
                cmbHistoriaUzytkownik.ItemsSource = uzytkownicy;
                cmbHistoriaUzytkownik.SelectedIndex = 0;

                var klienciList = new List<string> { "Wszyscy" };
                klienciList.AddRange(_historiaWydanAll.Select(h => h.Klient).Where(k => !string.IsNullOrEmpty(k)).Distinct().OrderBy(k => k));
                cmbHistoriaKlient.ItemsSource = klienciList;
                cmbHistoriaKlient.SelectedIndex = 0;

                cmbHistoriaStatus.SelectedIndex = 0;

                // Wyświetl wszystkie
                dgvHistoriaWydan.ItemsSource = _historiaWydanAll;

                // Podsumowanie
                lblHistoriaCount.Text = _historiaWydanAll.Count.ToString();
                lblHistoriaSumaKg.Text = _historiaWydanAll.Sum(h => h.IloscKg).ToString("N0");
                lblHistoriaPelne.Text = _historiaWydanAll.Count(h => h.StatusWydania.Contains("Pełne")).ToString();
                lblHistoriaRoznice.Text = _historiaWydanAll.Count(h => h.StatusWydania.Contains("różnicami")).ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania historii wydań:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Data Loading
        private async Task ReloadAllAsync()
        {
            lblData.Text = _selectedDate.ToString("yyyy-MM-dd dddd", new CultureInfo("pl-PL"));
            await PopulateProductFilterAsync();
            await LoadOrdersAsync();
        }

        private async Task PopulateProductFilterAsync()
        {
            var ids = new HashSet<int>();

            try
            {
                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();
                    string sql = "SELECT DISTINCT zmt.KodTowaru FROM dbo.ZamowieniaMieso z " +
                                "JOIN dbo.ZamowieniaMiesoTowar zmt ON z.Id=zmt.ZamowienieId " +
                                "WHERE z.DataUboju=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')";
                    var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                        if (!rd.IsDBNull(0)) ids.Add(rd.GetInt32(0));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą LibraNet:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _produktLookup.Clear();
            if (ids.Count > 0)
            {
                await LoadProductLookupAsync(ids);
            }

            // Utwórz przyciski towarów
            pnlProductButtons.Children.Clear();

            // Przycisk "Wszystkie"
            var btnAll = CreateProductButton(0, "Wszystkie");
            pnlProductButtons.Children.Add(btnAll);
            if (!_filteredProductId.HasValue)
            {
                SetProductButtonSelected(btnAll);
            }

            // Przyciski dla poszczególnych towarów
            foreach (var product in _produktLookup.OrderBy(k => k.Value))
            {
                var btn = CreateProductButton(product.Key, product.Value);
                pnlProductButtons.Children.Add(btn);
                if (_filteredProductId == product.Key)
                {
                    SetProductButtonSelected(btn);
                }
            }
        }

        private Button CreateProductButton(int productId, string productName)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Dodaj obrazek produktu jeśli dostępny
            if (productId != 0 && _productImages.TryGetValue(productId, out var img) && img != null)
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = img,
                    Width = 28,
                    Height = 28,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                sp.Children.Add(image);
            }

            sp.Children.Add(new TextBlock
            {
                Text = productName,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            });

            var btn = new Button
            {
                Content = sp,
                Tag = productId,
                Height = 40,
                MinWidth = 70,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(3),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x48)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                Cursor = Cursors.Hand
            };

            btn.Click += ProductButton_Click;
            return btn;
        }

        private void SetProductButtonSelected(Button btn)
        {
            // Reset poprzednio zaznaczonego przycisku
            if (_selectedProductButton != null)
            {
                _selectedProductButton.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x48));
                _selectedProductButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            }

            // Zaznacz nowy przycisk
            _selectedProductButton = btn;
            btn.Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x7A, 0x5A));
            btn.BorderBrush = Brushes.LimeGreen;
        }

        private async void ProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                int productId = 0;
                if (btn.Tag is int tagInt)
                {
                    productId = tagInt;
                }
                else if (btn.Tag != null && int.TryParse(btn.Tag.ToString(), out int parsed))
                {
                    productId = parsed;
                }

                _filteredProductId = productId == 0 ? null : productId;
                SetProductButtonSelected(btn);
                await LoadOrdersAsync();

                // DEBUG: pokaż info o filtrze (usuń po naprawieniu problemu)
                string nazwaFiltra = productId == 0 ? "Wszystkie" : (_produktLookup.ContainsKey(productId) ? _produktLookup[productId] : $"ID:{productId}");
                this.Title = $"Panel Magazynier - Filtr: {nazwaFiltra} ({ZamowieniaList1.Count + ZamowieniaList2.Count} zamówień)";
            }
        }

        private async Task LoadProductLookupAsync(HashSet<int> ids)
        {
            var list = ids.ToList();
            const int batch = 400;

            for (int i = 0; i < list.Count; i += batch)
            {
                try
                {
                    using var cn = new SqlConnection(_connHandel);
                    await cn.OpenAsync();
                    var slice = list.Skip(i).Take(batch).ToList();
                    var cmd = cn.CreateCommand();
                    var paramNames = new List<string>();

                    for (int k = 0; k < slice.Count; k++)
                    {
                        var pn = "@p" + k;
                        cmd.Parameters.AddWithValue(pn, slice[k]);
                        paramNames.Add(pn);
                    }

                    cmd.CommandText = $"SELECT ID, kod FROM HM.TW WHERE ID IN ({string.Join(",", paramNames)}) AND katalog=67095";
                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        _produktLookup[rd.GetInt32(0)] = rd.GetString(1);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania produktów:\n{ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }

        private async Task<bool> CheckDataAkceptacjiMagazynColumnExistsAsync(SqlConnection cn)
        {
            if (_dataAkceptacjiMagazynColumnExists.HasValue)
                return _dataAkceptacjiMagazynColumnExists.Value;

            string checkSql = @"SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataAkceptacjiMagazyn'";
            using var cmd = new SqlCommand(checkSql, cn);
            var result = await cmd.ExecuteScalarAsync();
            _dataAkceptacjiMagazynColumnExists = result != null;
            return _dataAkceptacjiMagazynColumnExists.Value;
        }

        private async Task<bool> CheckCzyModMagazynuColumnExistsAsync(SqlConnection cn)
        {
            if (_czyModMagazynuColumnExists.HasValue)
                return _czyModMagazynuColumnExists.Value;

            string checkSql = @"SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'CzyZmodyfikowaneDlaMagazynu'";
            using var cmd = new SqlCommand(checkSql, cn);
            var result = await cmd.ExecuteScalarAsync();
            if (result == null)
            {
                try
                {
                    using var alter = new SqlCommand("ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZmodyfikowaneDlaMagazynu BIT NULL DEFAULT 0", cn);
                    await alter.ExecuteNonQueryAsync();
                }
                catch { }
            }
            _czyModMagazynuColumnExists = true;
            return true;
        }

        private async Task<bool> CheckStrefaColumnExistsAsync(SqlConnection cn)
        {
            if (_strefaColumnExists.HasValue)
                return _strefaColumnExists.Value;

            string checkSql = @"SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMiesoTowar') AND name = 'Strefa'";
            using var cmd = new SqlCommand(checkSql, cn);
            var result = await cmd.ExecuteScalarAsync();
            bool exists = result != null;
            if (!exists)
            {
                try
                {
                    using var alter = new SqlCommand("ALTER TABLE [dbo].[ZamowieniaMiesoTowar] ADD Strefa BIT NULL DEFAULT 0", cn);
                    await alter.ExecuteNonQueryAsync();
                    exists = true;
                }
                catch { }
            }
            _strefaColumnExists = exists;
            return _strefaColumnExists.Value;
        }

        private async Task LoadProductImagesAsync()
        {
            if (_productImages.Count > 0) return;
            try
            {
                using var cn = new SqlConnection(_connLibra);
                cn.Open();

                using var cmdCheck = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TowarZdjecia') THEN 1 ELSE 0 END", cn);
                if ((int)(await cmdCheck.ExecuteScalarAsync())! == 0) return;

                using var cmd = new SqlCommand("SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1", cn);
                cmd.CommandTimeout = 30;
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int towarId = rdr.GetInt32(0);
                    if (!rdr.IsDBNull(1))
                    {
                        byte[] data = (byte[])rdr[1];
                        try
                        {
                            using var ms = new System.IO.MemoryStream(data);
                            var bi = new System.Windows.Media.Imaging.BitmapImage();
                            bi.BeginInit();
                            bi.StreamSource = ms;
                            bi.DecodePixelWidth = 140;
                            bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bi.EndInit();
                            bi.Freeze();
                            _productImages[towarId] = bi;
                        }
                        catch (Exception exImg)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MagazynPanel] Błąd dekodowania obrazka TowarId={towarId}: {exImg.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MagazynPanel] Błąd ładowania obrazków: {ex.Message}");
            }
        }

        private BitmapImage? GetProductImage(int towarId)
        {
            return _productImages.TryGetValue(towarId, out var img) ? img : null;
        }

        private async Task LoadOrdersAsync()
        {
            _zamowienia.Clear();
            ZamowieniaList1.Clear();
            ZamowieniaList2.Clear();
            var orderListForGrid = new List<ZamowienieViewModel>();

            try
            {
                // KROK 1: Pobierz zamówienia z LibraNet
                using (var cn = new SqlConnection(_connLibra))
                {
                    await cn.OpenAsync();

                    // Sprawdź czy kolumny opcjonalne istnieją
                    bool hasAkceptacjaColumn = await CheckDataAkceptacjiMagazynColumnExistsAsync(cn);
                    bool hasStrefaColumn = await CheckStrefaColumnExistsAsync(cn);
                    bool hasCzyModMagazynu = await CheckCzyModMagazynuColumnExistsAsync(cn);

                    string akceptacjaColumn = hasAkceptacjaColumn ? "z.DataAkceptacjiMagazyn" : "NULL AS DataAkceptacjiMagazyn";
                    string strefaColumn = hasStrefaColumn
                        ? ", CAST((SELECT MAX(CAST(ISNULL(ts.Strefa, 0) AS INT)) FROM dbo.ZamowieniaMiesoTowar ts WHERE ts.ZamowienieId = z.Id) AS BIT) AS Strefa"
                        : ", CAST(0 AS BIT) AS Strefa";

                    // Buduj zapytanie z filtrem towaru
                    var sqlBuilder = new System.Text.StringBuilder();
                    sqlBuilder.Append($@"SELECT z.Id, z.KlientId, ISNULL(z.Uwagi,'') AS Uwagi, ISNULL(z.Status,'Nowe') AS Status,
                                  (SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id");
                    if (_filteredProductId.HasValue) sqlBuilder.Append(" AND t.KodTowaru=@P");
                    sqlBuilder.Append(@") AS TotalIlosc,
                                  z.DataUtworzenia, z.TransportKursID, z.DataWydania, ISNULL(z.KtoWydal, '') AS KtoWydal,
                                  CAST(CASE WHEN z.TransportStatus = 'Wlasny' THEN 1 ELSE 0 END AS BIT) AS WlasnyTransport,
                                  z.DataPrzyjazdu,
                                  z.DataOstatniejModyfikacji, z.DataRealizacji, ISNULL(z.CzyZrealizowane, 0) AS CzyZrealizowane,
                                  ");
                    sqlBuilder.Append(akceptacjaColumn);
                    sqlBuilder.Append(strefaColumn);
                    sqlBuilder.Append(", CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id AND t.Folia = 1) THEN 1 ELSE 0 END AS BIT) AS MaFolie");
                    sqlBuilder.Append(", CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id AND ISNULL(t.Hallal, 0) = 1) THEN 1 ELSE 0 END AS BIT) AS MaHalal");
                    sqlBuilder.Append(", CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id AND ISNULL(t.E2, 0) = 1) THEN 1 ELSE 0 END AS BIT) AS MaE2");
                    sqlBuilder.Append(hasCzyModMagazynu ? ", ISNULL(z.CzyZmodyfikowaneDlaMagazynu, 0) AS CzyZmodyfikowaneDlaMagazynuFlag" : ", CAST(0 AS BIT) AS CzyZmodyfikowaneDlaMagazynuFlag");
                    sqlBuilder.Append(" FROM dbo.ZamowieniaMieso z WHERE z.DataUboju=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')");
                    if (_filteredProductId.HasValue)
                        sqlBuilder.Append(" AND EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId=z.Id AND t.KodTowaru=@P)");

                    var cmd = new SqlCommand(sqlBuilder.ToString(), cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                    if (_filteredProductId.HasValue) cmd.Parameters.AddWithValue("@P", _filteredProductId.Value);

                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var dataOstatniejModyfikacji = rd.IsDBNull(11) ? (DateTime?)null : rd.GetDateTime(11);
                        var dataRealizacji = rd.IsDBNull(12) ? (DateTime?)null : rd.GetDateTime(12);
                        var czyZrealizowane = rd.GetBoolean(13);
                        var dataAkceptacjiMagazyn = rd.IsDBNull(14) ? (DateTime?)null : rd.GetDateTime(14);

                        // Sprawdź czy zamówienie zostało zmodyfikowane od czasu realizacji (dla produkcji)
                        bool czyZmodyfikowaneProdukcja = false;
                        if (dataRealizacji.HasValue && dataOstatniejModyfikacji.HasValue)
                        {
                            czyZmodyfikowaneProdukcja = dataOstatniejModyfikacji.Value > dataRealizacji.Value;
                        }

                        // Sprawdź czy zamówienie zostało zmodyfikowane dla magazynu
                        // 1) Flaga boolean ustawiana przez WidokZamowienia przy edycji
                        bool czyZmodyfikowaneMagazynFlag = rd.GetBoolean(19);
                        // 2) Timestamp comparison jako fallback (dla starszych danych)
                        bool czyZmodyfikowaneMagazyn = czyZmodyfikowaneMagazynFlag;
                        if (!czyZmodyfikowaneMagazyn && czyZrealizowane && dataOstatniejModyfikacji.HasValue)
                        {
                            if (dataAkceptacjiMagazyn.HasValue)
                            {
                                czyZmodyfikowaneMagazyn = dataOstatniejModyfikacji.Value > dataAkceptacjiMagazyn.Value;
                            }
                            else if (dataRealizacji.HasValue)
                            {
                                czyZmodyfikowaneMagazyn = dataOstatniejModyfikacji.Value > dataRealizacji.Value;
                            }
                        }

                        var info = new ZamowienieInfo
                        {
                            Id = rd.GetInt32(0),
                            KlientId = rd.GetInt32(1),
                            Uwagi = rd.GetString(2),
                            Status = rd.GetString(3),
                            TotalIlosc = rd.IsDBNull(4) ? 0 : rd.GetDecimal(4),
                            DataUtworzenia = rd.IsDBNull(5) ? (DateTime?)null : rd.GetDateTime(5),
                            TransportKursId = rd.IsDBNull(6) ? null : rd.GetInt64(6),
                            DataWydania = rd.IsDBNull(7) ? null : rd.GetDateTime(7),
                            KtoWydal = rd.GetString(8),
                            WlasnyTransport = rd.GetBoolean(9),
                            DataPrzyjazdu = rd.IsDBNull(10) ? null : rd.GetDateTime(10),
                            // Nowe pola do wykrywania zmian
                            DataOstatniejModyfikacji = dataOstatniejModyfikacji,
                            DataRealizacji = dataRealizacji,
                            DataAkceptacjiMagazyn = dataAkceptacjiMagazyn,
                            CzyZrealizowane = czyZrealizowane,
                            CzyZmodyfikowaneOdRealizacji = czyZmodyfikowaneProdukcja,
                            CzyZmodyfikowaneDlaMagazynu = czyZmodyfikowaneMagazyn,
                            Strefa = rd.IsDBNull(15) ? false : rd.GetBoolean(15),
                            MaFolie = rd.GetBoolean(16),
                            MaHalal = rd.GetBoolean(17),
                            MaE2 = rd.GetBoolean(18)
                        };

                        _zamowienia[info.Id] = info;
                    }
                }

                // KROK 2: Pobierz dane kursów z TransportPL (kierowca, pojazd, godzina)
                using var cnTransport = new SqlConnection(_connTransport);
                await cnTransport.OpenAsync();

                // Pobierz wszystkie kursy
                var kursData = new Dictionary<long, (DateTime DataKursu, TimeSpan? GodzWyjazdu, string Kierowca, string Rejestracja, int? PaletyH1)>();
                string sqlKursy = @"
                    SELECT
                        k.KursID,
                        k.DataKursu,
                        k.GodzWyjazdu,
                        ISNULL(kie.Imie + ' ' + kie.Nazwisko, 'Nie przypisano') as Kierowca,
                        p.Rejestracja,
                        p.PaletyH1
                    FROM dbo.Kurs k
                    LEFT JOIN dbo.Kierowca kie ON k.KierowcaID = kie.KierowcaID
                    LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID";

                using (var cmd = new SqlCommand(sqlKursy, cnTransport))
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        long kursId = rd.GetInt64(0);
                        DateTime dataKursu = rd.GetDateTime(1);
                        TimeSpan? godzWyjazdu = rd.IsDBNull(2) ? null : rd.GetTimeSpan(2);
                        string kierowca = rd.GetString(3);
                        string rejestracja = rd.IsDBNull(4) ? null : rd.GetString(4);
                        int? paletyH1 = rd.IsDBNull(5) ? null : rd.GetInt32(5);

                        kursData[kursId] = (dataKursu, godzWyjazdu, kierowca, rejestracja, paletyH1);
                    }
                }

                // Pobierz mapowanie zamówienie -> kurs z tabeli Ladunek (dla łączonych kursów)
                var zamowienieToKurs = new Dictionary<int, long>();
                using (var cmd = new SqlCommand("SELECT KursID, KodKlienta FROM dbo.Ladunek WHERE KodKlienta LIKE 'ZAM_%'", cnTransport))
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    while (await rd.ReadAsync())
                    {
                        var kursId = rd.GetInt64(0);
                        var kodKlienta = rd.GetString(1);
                        if (kodKlienta.StartsWith("ZAM_") && int.TryParse(kodKlienta.Substring(4), out int zamId))
                        {
                            zamowienieToKurs[zamId] = kursId;
                        }
                    }
                }

                // Przypisz dane kursów do WSZYSTKICH zamówień
                foreach (var zamowienie in _zamowienia.Values)
                {
                    long? kursId = zamowienie.TransportKursId;

                    // Jeśli nie ma TransportKursId, sprawdź tabelę Ladunek
                    if (!kursId.HasValue && zamowienieToKurs.TryGetValue(zamowienie.Id, out var ladunekKursId))
                    {
                        kursId = ladunekKursId;
                    }

                    // Przypisz dane kursu
                    if (kursId.HasValue && kursData.TryGetValue(kursId.Value, out var kurs))
                    {
                        zamowienie.DataKursu = kurs.DataKursu;
                        zamowienie.CzasWyjazdu = kurs.GodzWyjazdu;
                        zamowienie.Kierowca = kurs.Kierowca;
                        zamowienie.NumerRejestracyjny = kurs.Rejestracja;
                        zamowienie.PaletyH1 = kurs.PaletyH1;
                    }
                }

                // KROK 3: Załaduj nazwy operatorów (kto wydał) z LibraNet
                var operatorIds = _zamowienia.Values
                    .Where(z => !string.IsNullOrEmpty(z.KtoWydal) && int.TryParse(z.KtoWydal, out _))
                    .Select(z => int.Parse(z.KtoWydal))
                    .Distinct()
                    .ToList();
                var operatorNames = await LoadOperatorNamesAsync(operatorIds);
                foreach (var zam in _zamowienia.Values)
                {
                    if (!string.IsNullOrEmpty(zam.KtoWydal) && int.TryParse(zam.KtoWydal, out int opId) && operatorNames.TryGetValue(opId, out string nazwa))
                    {
                        zam.KtoWydalNazwa = nazwa;
                    }
                    else
                    {
                        zam.KtoWydalNazwa = zam.KtoWydal;
                    }
                }

                // KROK 4: Załaduj dane klientów z Handel
                var orderClientIds = _zamowienia.Values.Select(o => o.KlientId).Distinct().ToList();
                if (orderClientIds.Any())
                {
                    var contractors = await LoadContractorsAsync(orderClientIds);
                    foreach (var orderInfo in _zamowienia.Values)
                    {
                        if (contractors.TryGetValue(orderInfo.KlientId, out var cinfo))
                        {
                            orderInfo.Klient = Normalize(cinfo.Shortcut);
                            orderInfo.Handlowiec = Normalize(cinfo.Handlowiec);
                        }
                        else
                        {
                            orderInfo.Klient = $"KH {orderInfo.KlientId}";
                        }
                        orderListForGrid.Add(new ZamowienieViewModel(orderInfo));
                    }
                }

                // KROK 4: Sortuj i podziel na dwie kolumny
                var sorted = orderListForGrid
                    .OrderBy(o => o.SortDateTime)
                    .ThenBy(o => o.Info.Klient)
                    .ToList();

                int midpoint = (int)Math.Ceiling(sorted.Count / 2.0);
                sorted.Take(midpoint).ToList().ForEach(ZamowieniaList1.Add);
                sorted.Skip(midpoint).ToList().ForEach(ZamowieniaList2.Add);

                // Automatycznie wybierz pierwsze
                if (ZamowieniaList1.Count > 0)
                {
                    dgvZamowienia1.SelectedIndex = 0;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania zamówień:\n{ex.Message}",
                    "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Normalize(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

        private async Task<Dictionary<int, string>> LoadOperatorNamesAsync(List<int> ids)
        {
            var dict = new Dictionary<int, string>();
            if (!ids.Any()) return dict;

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand($"SELECT ID, Name FROM dbo.operators WHERE ID IN ({string.Join(',', ids)})", cn);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    dict[rd.GetInt32(0)] = rd.IsDBNull(1) ? "" : rd.GetString(1);
                }
            }
            catch { /* Ignore errors, fallback to ID */ }
            return dict;
        }

        private async Task<Dictionary<int, ContractorInfo>> LoadContractorsAsync(List<int> ids)
        {
            var dict = new Dictionary<int, ContractorInfo>();
            if (!ids.Any()) return dict;

            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // DOKŁADNIE JAK W PRODUKCJAPANEL
                var cmd = new SqlCommand($@"SELECT c.Id, ISNULL(c.Shortcut,'KH '+CAST(c.Id AS varchar(10))), ISNULL(w.CDim_Handlowiec_Val,'(Brak)') 
                                            FROM SSCommon.STContractors c 
                                            LEFT JOIN SSCommon.ContractorClassification w ON c.Id=w.ElementId 
                                            WHERE c.Id IN ({string.Join(',', ids)})", cn);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    dict[rd.GetInt32(0)] = new ContractorInfo
                    {
                        Id = rd.GetInt32(0),
                        Shortcut = rd.GetString(1).Trim(),
                        Handlowiec = Normalize(rd.GetString(2))
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania kontrahentów:\n{ex.Message}",
                    "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return dict;
        }

        private async Task LoadPozycjeForSelectedAsync()
        {
            var selected = (dgvZamowienia1.SelectedItem ?? dgvZamowienia2.SelectedItem) as ZamowienieViewModel;

            if (selected == null)
            {
                dgvPozycje.ItemsSource = null;
                txtKlient.Text = "--";
                txtHandlowiec.Text = "--";
                txtCzasWyjazdu.Text = "--";
                txtPojazd.Text = "--";
                txtKierowca.Text = "--";
                txtUwagi.Text = "";
                btnMarkAsShipped.Visibility = Visibility.Collapsed;
                btnUndoShipment.Visibility = Visibility.Collapsed;
                txtShipmentInfo.Text = "";
                return;
            }

            var info = selected.Info;

            // Wypełnij dane podstawowe
            txtKlient.Text = info.Klient;
            txtHandlowiec.Text = string.IsNullOrEmpty(info.Handlowiec) ? "--" : info.Handlowiec;
            txtCzasWyjazdu.Text = selected.CzasWyjazdDisplay;
            txtPojazd.Text = info.WlasnyTransport ? "Własny" : (info.NumerRejestracyjny ?? "Nie przypisano");
            txtKierowca.Text = info.WlasnyTransport ? "Własny odbiór" : (info.Kierowca ?? "Nie przypisano");
            txtUwagi.Text = info.Uwagi;

            // Pokaż/ukryj przyciski w zależności od statusu
            if (info.Status == "Wydany")
            {
                btnMarkAsShipped.Visibility = Visibility.Collapsed;
                btnUndoShipment.Visibility = Visibility.Visible;

                // Pokaż informację kto i kiedy wydał
                if (info.DataWydania.HasValue)
                {
                    string wydawca = !string.IsNullOrEmpty(info.KtoWydalNazwa) ? info.KtoWydalNazwa : info.KtoWydal;
                    txtShipmentInfo.Text = $"✅ Wydano: {info.DataWydania.Value:yyyy-MM-dd HH:mm} przez {wydawca}";
                    txtShipmentInfo.Foreground = Brushes.LimeGreen;
                }
                else
                {
                    txtShipmentInfo.Text = "✅ Wydano (brak szczegółów)";
                    txtShipmentInfo.Foreground = Brushes.LimeGreen;
                }
            }
            else
            {
                btnMarkAsShipped.Visibility = Visibility.Visible;
                btnUndoShipment.Visibility = Visibility.Collapsed;
                txtShipmentInfo.Text = "";
            }

            // Pobierz pozycje zamówienia
            var orderPositions = new List<(int TowarId, decimal Ilosc, bool Folia, bool E2, bool Hallal, bool Strefa)>();
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                bool hasStrefa = await CheckStrefaColumnExistsAsync(cn);
                string strefaSel = hasStrefa ? ", ISNULL(zmt.Strefa, 0) as Strefa" : "";
                string sql = $@"SELECT zmt.KodTowaru, zmt.Ilosc, ISNULL(zmt.Folia, 0) AS Folia, ISNULL(zmt.E2, 0) AS E2, ISNULL(zmt.Hallal, 0) AS Hallal{strefaSel}
                               FROM dbo.ZamowieniaMiesoTowar zmt
                               WHERE zmt.ZamowienieId=@Id";
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", info.Id);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    orderPositions.Add((rd.GetInt32(0), rd.GetDecimal(1), rd.GetBoolean(2), rd.GetBoolean(3), rd.GetBoolean(4), hasStrefa ? rd.GetBoolean(5) : false));
                }
            }

            // Pobierz wydania dla klienta - DOKŁADNIE JAK W PRODUKCJAPANEL
            var shipments = await GetShipmentsForClientAsync(info.KlientId);

            // Pobierz snapshot (jeśli zamówienie było realizowane)
            var snapshot = info.CzyZrealizowane ? await GetOrderSnapshotAsync(info.Id, "Realizacja") : new Dictionary<int, decimal>();

            // Połącz wszystkie ID towarów
            var ids = orderPositions.Select(p => p.TowarId).Union(shipments.Keys).Union(snapshot.Keys).Where(i => i > 0).Distinct().ToList();

            // Pobierz nazwy towarów - DOKŁADNIE JAK W PRODUKCJAPANEL
            var towary = await LoadTowaryAsync(ids);

            // Stwórz tabelę z pozycjami - DOKŁADNIE JAK W PRODUKCJAPANEL
            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("ProduktImg", typeof(BitmapImage));
            dt.Columns.Add("Zamówiono (kg)", typeof(decimal));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));
            dt.Columns.Add("Różnica (kg)", typeof(decimal));
            // Kolumna zmian - pokazuje różnicę między aktualnym stanem a snapshotem
            dt.Columns.Add("Zmiana", typeof(string));
            // Kolumny checkbox per-produkt
            dt.Columns.Add("Halal", typeof(bool));
            dt.Columns.Add("Folia", typeof(bool));
            dt.Columns.Add("E2", typeof(bool));
            dt.Columns.Add("Strefa", typeof(bool));

            var mapOrd = orderPositions.ToDictionary(p => p.TowarId, p => (p.Ilosc, p.Folia, p.E2, p.Hallal, p.Strefa));

            foreach (var id in ids.Where(i => mapOrd.ContainsKey(i) || !snapshot.ContainsKey(i)))
            {
                mapOrd.TryGetValue(id, out var ord);
                shipments.TryGetValue(id, out var wyd);
                snapshot.TryGetValue(id, out var snap);

                string produktNazwa = towary.ContainsKey(id) ? towary[id].Kod : $"Towar {id}";

                // Oblicz zmianę od snapshotu
                string zmiana = "";
                if (info.CzyZrealizowane && snapshot.Count > 0)
                {
                    if (!snapshot.ContainsKey(id))
                    {
                        // Nowa pozycja dodana po realizacji
                        zmiana = "🆕 NOWE";
                    }
                    else if (ord.Ilosc != snap)
                    {
                        // Zmieniona ilość
                        decimal diff = ord.Ilosc - snap;
                        zmiana = diff > 0 ? $"+{diff:N0} kg" : $"{diff:N0} kg";
                    }
                }

                var row = dt.NewRow();
                row["Produkt"] = produktNazwa;
                row["ProduktImg"] = (object?)GetProductImage(id) ?? DBNull.Value;
                row["Zamówiono (kg)"] = ord.Ilosc;
                row["Wydano (kg)"] = wyd;
                row["Różnica (kg)"] = ord.Ilosc - wyd;
                row["Zmiana"] = zmiana;
                row["Halal"] = ord.Hallal;
                row["Folia"] = ord.Folia;
                row["E2"] = ord.E2;
                row["Strefa"] = ord.Strefa;
                dt.Rows.Add(row);
            }

            // Sprawdź czy są pozycje usunięte (były w snapshocie, ale nie ma w aktualnym zamówieniu)
            if (info.CzyZrealizowane && snapshot.Count > 0)
            {
                foreach (var snapItem in snapshot.Where(s => !mapOrd.ContainsKey(s.Key)))
                {
                    string produktNazwa = towary.ContainsKey(snapItem.Key) ? towary[snapItem.Key].Kod : $"Towar {snapItem.Key}";
                    // produktNazwa bez ikony - zmiana widoczna w kolumnie "Zmiana"
                    var row = dt.NewRow();
                    row["Produkt"] = produktNazwa;
                    row["ProduktImg"] = (object?)GetProductImage(snapItem.Key) ?? DBNull.Value;
                    row["Zamówiono (kg)"] = 0m;
                    row["Wydano (kg)"] = 0m;
                    row["Różnica (kg)"] = 0m;
                    row["Zmiana"] = $"USUNIĘTO ({snapItem.Value:N0} kg)";
                    row["Halal"] = false;
                    row["Folia"] = false;
                    row["E2"] = false;
                    row["Strefa"] = false;
                    dt.Rows.Add(row);
                }
            }

            dgvPozycje.ItemsSource = dt.DefaultView;

            // Pokaż/ukryj przycisk "Przyjmuję zmianę" (dla magazynu - osobna flaga)
            btnAcceptChange.Visibility = info.CzyZmodyfikowaneDlaMagazynu ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void btnAcceptChange_Click(object sender, RoutedEventArgs e)
        {
            var selected = (dgvZamowienia1.SelectedItem ?? dgvZamowienia2.SelectedItem) as ZamowienieViewModel;
            if (selected == null || !selected.Info.CzyZmodyfikowaneDlaMagazynu) return;

            var result = MessageBox.Show(
                $"Czy potwierdzasz, że wiesz o zmianach w zamówieniu '{selected.Info.Klient}'?\n\n" +
                "Ikona ⚠️ zniknie dopóki zamówienie nie zostanie ponownie zmodyfikowane.\n" +
                "(Produkcja ma swoją osobną akceptację)",
                "Potwierdzenie przyjęcia zmiany - Magazyn",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Upewnij się że kolumna DataAkceptacjiMagazyn istnieje
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ZamowieniaMieso') AND name = 'DataAkceptacjiMagazyn'", cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0)
                {
                    var addCmd = new SqlCommand("ALTER TABLE dbo.ZamowieniaMieso ADD DataAkceptacjiMagazyn DATETIME NULL", cn);
                    await addCmd.ExecuteNonQueryAsync();
                    // Zresetuj cache po utworzeniu kolumny
                    _dataAkceptacjiMagazynColumnExists = true;
                }

                // Zaktualizuj DataAkceptacjiMagazyn na teraz + resetuj flagę boolean (osobna akceptacja dla magazynu)
                var cmd = new SqlCommand("UPDATE dbo.ZamowieniaMieso SET DataAkceptacjiMagazyn = GETDATE(), CzyZmodyfikowaneDlaMagazynu = 0 WHERE Id = @Id", cn);
                cmd.Parameters.AddWithValue("@Id", selected.Info.Id);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show("Zmiana została przyjęta przez magazyn.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                // Odśwież dane
                await ReloadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas akceptacji zmiany:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveOrderSnapshotAsync(SqlConnection cn, int zamowienieId, string typSnapshotu)
        {
            try
            {
                // Sprawdź czy tabela snapshotów istnieje
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE name='ZamowieniaMiesoSnapshot' AND type='U'", cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0)
                {
                    // Utwórz tabelę jeśli nie istnieje
                    var createCmd = new SqlCommand(@"
                        CREATE TABLE dbo.ZamowieniaMiesoSnapshot (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ZamowienieId INT NOT NULL,
                            KodTowaru INT NOT NULL,
                            Ilosc DECIMAL(18,3) NOT NULL,
                            Folia BIT NULL,
                            Hallal BIT NULL,
                            DataSnapshotu DATETIME NOT NULL DEFAULT GETDATE(),
                            TypSnapshotu NVARCHAR(20) NOT NULL
                        );
                        CREATE INDEX IX_Snapshot_ZamowienieId ON dbo.ZamowieniaMiesoSnapshot(ZamowienieId);", cn);
                    await createCmd.ExecuteNonQueryAsync();
                }

                // Usuń stary snapshot tego samego typu
                var cmdDelete = new SqlCommand(@"DELETE FROM dbo.ZamowieniaMiesoSnapshot WHERE ZamowienieId = @ZamId AND TypSnapshotu = @Typ", cn);
                cmdDelete.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmdDelete.Parameters.AddWithValue("@Typ", typSnapshotu);
                await cmdDelete.ExecuteNonQueryAsync();

                // Zapisz nowy snapshot
                var cmdInsert = new SqlCommand(@"
                    INSERT INTO dbo.ZamowieniaMiesoSnapshot (ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, TypSnapshotu)
                    SELECT ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, @Typ
                    FROM dbo.ZamowieniaMiesoTowar
                    WHERE ZamowienieId = @ZamId", cn);
                cmdInsert.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmdInsert.Parameters.AddWithValue("@Typ", typSnapshotu);
                await cmdInsert.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Błąd zapisywania snapshotu: {ex.Message}"); }
        }

        private async Task<Dictionary<int, decimal>> GetOrderSnapshotAsync(int zamowienieId, string typSnapshotu)
        {
            var snapshot = new Dictionary<int, decimal>();
            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdź czy tabela snapshotów istnieje
                var checkCmd = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE name='ZamowieniaMiesoSnapshot' AND type='U'", cn);
                if ((int)await checkCmd.ExecuteScalarAsync() == 0)
                    return snapshot;

                var cmd = new SqlCommand(@"SELECT KodTowaru, Ilosc
                                           FROM dbo.ZamowieniaMiesoSnapshot
                                           WHERE ZamowienieId = @ZamId AND TypSnapshotu = @Typ", cn);
                cmd.Parameters.AddWithValue("@ZamId", zamowienieId);
                cmd.Parameters.AddWithValue("@Typ", typSnapshotu);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    snapshot[rd.GetInt32(0)] = rd.GetDecimal(1);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Błąd pobierania snapshotu: {ex.Message}"); }
            return snapshot;
        }

        private async Task<Dictionary<int, TowarInfo>> LoadTowaryAsync(List<int> ids)
        {
            var dict = new Dictionary<int, TowarInfo>();
            if (!ids.Any()) return dict;

            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // DOKŁADNIE JAK W PRODUKCJAPANEL
                var cmd = new SqlCommand($"SELECT ID,kod FROM HM.TW WHERE ID IN ({string.Join(',', ids)})", cn);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    dict[rd.GetInt32(0)] = new TowarInfo { Id = rd.GetInt32(0), Kod = rd.GetString(1) };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania towarów:\n{ex.Message}",
                    "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return dict;
        }

        private async Task<Dictionary<int, decimal>> GetShipmentsForClientAsync(int klientId)
        {
            var dict = new Dictionary<int, decimal>();

            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // DOKŁADNIE JAK W PRODUKCJAPANEL
                string sql = @"SELECT MZ.idtw, SUM(ABS(MZ.ilosc)) 
                               FROM HANDEL.HM.MZ MZ 
                               JOIN HANDEL.HM.MG ON MZ.super=MG.id 
                               JOIN HANDEL.HM.TW ON MZ.idtw=TW.id 
                               WHERE MG.seria IN ('sWZ','sWZ-W') 
                                 AND MG.aktywny=1 
                                 AND MG.data=@D 
                                 AND MG.khid=@K 
                                 AND TW.katalog=67095
                               GROUP BY MZ.idtw";

                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@D", _selectedDate.Date);
                cmd.Parameters.AddWithValue("@K", klientId);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    dict[rd.GetInt32(0)] = Convert.ToDecimal(rd.GetValue(1));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z bazą Handel podczas pobierania wydań:\n{ex.Message}",
                    "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return dict;
        }
        #endregion

        #region Auto Refresh
        private void StartAutoRefresh()
        {
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            refreshTimer.Tick += async (s, e) => await ReloadAllAsync();
            refreshTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            refreshTimer?.Stop();
        }
        #endregion

        #region Data Classes
        public class ZamowienieInfo
        {
            public int Id { get; set; }
            public int KlientId { get; set; }
            public string Klient { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public string Uwagi { get; set; } = "";
            public string Status { get; set; } = "";
            public decimal TotalIlosc { get; set; }
            public DateTime? DataUtworzenia { get; set; }
            public TimeSpan? CzasWyjazdu { get; set; }
            public DateTime? DataKursu { get; set; }
            public string NumerRejestracyjny { get; set; } = "";
            public string Kierowca { get; set; } = "";
            public long? TransportKursId { get; set; }
            public int? PaletyH1 { get; set; } // Ile palet mieści pojazd
            public DateTime? DataWydania { get; set; } // Kiedy wydano
            public string KtoWydal { get; set; } = ""; // Kto wydał
            public string KtoWydalNazwa { get; set; } = ""; // Pełna nazwa użytkownika
            public bool WlasnyTransport { get; set; } // Własny transport
            public bool MaFolie { get; set; }
            public bool MaHalal { get; set; }
            public bool MaE2 { get; set; }
            public DateTime? DataPrzyjazdu { get; set; } // Godzina odbioru dla własnego transportu
            // Nowe pola do wykrywania zmian
            public DateTime? DataOstatniejModyfikacji { get; set; }
            public DateTime? DataRealizacji { get; set; }
            public DateTime? DataAkceptacjiMagazyn { get; set; } // Osobna akceptacja magazynu
            public bool CzyZrealizowane { get; set; }
            public bool CzyZmodyfikowaneOdRealizacji { get; set; } // Dla produkcji
            public bool CzyZmodyfikowaneDlaMagazynu { get; set; } // Dla magazynu - osobna flaga
            public bool Strefa { get; set; } // Strefa ptasiej grypy/pomoru
        }

        public class ContractorInfo
        {
            public int Id { get; set; }
            public string Shortcut { get; set; } = "";
            public string Handlowiec { get; set; } = "(Brak)";
        }

        public class TowarInfo
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
        }

        public class HistoriaWydaniaItem
        {
            public DateTime DataWydania { get; set; }
            public string Klient { get; set; } = "";
            public decimal IloscKg { get; set; }
            public string KtoWydal { get; set; } = "";
            public string StatusWydania { get; set; } = "";
            public string Uwagi { get; set; } = "";
        }

        public class ZamowienieViewModel : INotifyPropertyChanged
        {
            public ZamowienieInfo Info { get; }
            public ZamowienieViewModel(ZamowienieInfo info) { Info = info; }

            public string Klient => Info.Klient;

            // Flagi statusów do ikon
            public bool HasStrefa => Info.Strefa;
            public bool HasHalal => Info.MaHalal;
            public bool HasFolia => Info.MaFolie;
            public bool HasE2 => Info.MaE2;

            // Kolor nazwy klienta - żółty gdy zmodyfikowane, czerwony gdy strefa
            public Brush KlientColor => Info.Strefa ? new SolidColorBrush(Color.FromRgb(255, 200, 200)) :
                                        Info.CzyZmodyfikowaneDlaMagazynu ? Brushes.Yellow : Brushes.White;
            public decimal TotalIlosc => Info.TotalIlosc;
            public string Handlowiec => Info.Handlowiec;

            // Status z informacją o niezaakceptowanej zmianie
            public string Status
            {
                get
                {
                    if (Info.CzyZmodyfikowaneDlaMagazynu) return "⚠ Do zaakceptowania";
                    return Info.Status;
                }
            }

            // Kolor statusu
            public Brush StatusColor => Info.CzyZmodyfikowaneDlaMagazynu ? Brushes.Yellow : Brushes.White;
            public string NumerRejestracyjny => Info.NumerRejestracyjny ?? "Brak";
            public string Kierowca => Info.Kierowca ?? "Brak";

            // Wyświetlanie pojazdu z ilością palet z bazy danych
            public string PojazdDisplay
            {
                get
                {
                    if (Info.WlasnyTransport)
                        return "Własny";
                    if (string.IsNullOrEmpty(Info.NumerRejestracyjny))
                        return "Brak";

                    if (Info.PaletyH1.HasValue && Info.PaletyH1.Value > 0)
                        return $"{Info.NumerRejestracyjny} ({Info.PaletyH1}p)";

                    return Info.NumerRejestracyjny;
                }
            }

            // Wyświetlanie kierowcy - samo imię i nazwisko
            public string KierowcaDisplay => Info.WlasnyTransport ? "Własny odbiór" : (string.IsNullOrEmpty(Info.Kierowca) ? "Brak" : Info.Kierowca);

            // Wyjazd: ikona + czas + skrócony dzień tygodnia
            public string CzasWyjazdDisplay
            {
                get
                {
                    // Własny transport - ikona auta + czas odbioru + dzień
                    if (Info.WlasnyTransport && Info.DataPrzyjazdu.HasValue)
                    {
                        string dzien = GetShortDayName(Info.DataPrzyjazdu.Value);
                        return $"🚗 {Info.DataPrzyjazdu.Value:HH:mm} {dzien}";
                    }
                    if (Info.WlasnyTransport)
                        return "🚗 Własny";

                    // Zwykły transport - ikona auta + czas wyjazdu + dzień
                    if (Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue)
                    {
                        string dzien = GetShortDayName(Info.DataKursu.Value);
                        return $"🚗 {Info.CzasWyjazdu.Value:hh\\:mm} {dzien}";
                    }
                    return "Brak kursu";
                }
            }

            private static string GetShortDayName(DateTime date)
            {
                return date.DayOfWeek switch
                {
                    DayOfWeek.Monday => "pon.",
                    DayOfWeek.Tuesday => "wt.",
                    DayOfWeek.Wednesday => "śr.",
                    DayOfWeek.Thursday => "czw.",
                    DayOfWeek.Friday => "pt.",
                    DayOfWeek.Saturday => "sob.",
                    DayOfWeek.Sunday => "niedz.",
                    _ => ""
                };
            }

            public DateTime SortDateTime
            {
                get
                {
                    if (Info.WlasnyTransport && Info.DataPrzyjazdu.HasValue)
                        return Info.DataPrzyjazdu.Value;
                    if (Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue)
                        return Info.DataKursu.Value.Add(Info.CzasWyjazdu.Value);
                    return DateTime.MaxValue;
                }
            }

            // Wyświetlanie ostatniej zmiany (dla magazynu - osobna logika)
            public string OstatniaZmianaDisplay
            {
                get
                {
                    if (!Info.CzyZrealizowane) return "-";
                    if (!Info.CzyZmodyfikowaneDlaMagazynu) return "✓ OK";
                    if (Info.DataOstatniejModyfikacji.HasValue)
                        return $"⚠️ {Info.DataOstatniejModyfikacji.Value:HH:mm}";
                    return "⚠️ Zmiana";
                }
            }

            public Brush ZmianaColor => Info.CzyZmodyfikowaneDlaMagazynu ? Brushes.Orange : Brushes.LimeGreen;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}