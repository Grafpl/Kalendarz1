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
using System.Windows.Threading;

namespace Kalendarz1
{
    public partial class MagazynPanel : Window, INotifyPropertyChanged
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public string UserID { get; set; } = "Magazynier";
        private DateTime _selectedDate = DateTime.Today;
        private DispatcherTimer refreshTimer;
        private readonly Dictionary<int, ZamowienieInfo> _zamowienia = new();

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
            this.DataContext = this;
            InitializeAsync();
            KeyDown += MagazynPanel_KeyDown;
            StartAutoRefresh();
        }

        private async void InitializeAsync()
        {
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

            var result = MessageBox.Show($"Czy na pewno chcesz oznaczyć zamówienie dla klienta '{selected.Klient}' jako WYDANE?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();

                    var cmd = new SqlCommand(@"UPDATE dbo.ZamowieniaMieso 
                                               SET Status = 'Wydany', 
                                                   DataWydania = GETDATE(), 
                                                   KtoWydal = @UserID 
                                               WHERE Id = @Id", cn);
                    cmd.Parameters.AddWithValue("@Id", selected.Info.Id);
                    cmd.Parameters.AddWithValue("@UserID", UserID);

                    await cmd.ExecuteNonQueryAsync();

                    MessageBox.Show("Status zamówienia został zmieniony na 'Wydany'!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Odśwież listę
                    await ReloadAllAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas zmiany statusu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

                    var cmd = new SqlCommand(@"UPDATE dbo.ZamowieniaMieso 
                                               SET Status = 'Nowe', 
                                                   DataWydania = NULL, 
                                                   KtoWydal = NULL 
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
        #endregion

        #region Data Loading
        private async Task ReloadAllAsync()
        {
            lblData.Text = _selectedDate.ToString("yyyy-MM-dd dddd", new CultureInfo("pl-PL"));
            await LoadOrdersAsync();
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

                    string sql = @"SELECT z.Id, z.KlientId, ISNULL(z.Uwagi,'') AS Uwagi, ISNULL(z.Status,'Nowe') AS Status, 
                                  (SELECT SUM(ISNULL(t.Ilosc, 0)) FROM dbo.ZamowieniaMiesoTowar t WHERE t.ZamowienieId = z.Id) AS TotalIlosc,
                                  z.DataUtworzenia, z.TransportKursID, z.DataWydania, ISNULL(z.KtoWydal, '') AS KtoWydal
                                  FROM dbo.ZamowieniaMieso z 
                                  WHERE z.DataUboju=@D AND ISNULL(z.Status,'Nowe') NOT IN ('Anulowane')";

                    var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@D", _selectedDate.Date);

                    using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
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
                            KtoWydal = rd.GetString(8)
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

                // KROK 3: Załaduj dane klientów z Handel
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
            txtCzasWyjazdu.Text = selected.CzasWyjazdDisplay;
            txtPojazd.Text = info.NumerRejestracyjny ?? "Nie przypisano";
            txtKierowca.Text = info.Kierowca ?? "Nie przypisano";
            txtUwagi.Text = info.Uwagi;

            // Pokaż/ukryj przyciski w zależności od statusu
            if (info.Status == "Wydany")
            {
                btnMarkAsShipped.Visibility = Visibility.Collapsed;
                btnUndoShipment.Visibility = Visibility.Visible;

                // Pokaż informację kto i kiedy wydał
                if (info.DataWydania.HasValue)
                {
                    txtShipmentInfo.Text = $"✅ Wydano: {info.DataWydania.Value:yyyy-MM-dd HH:mm} przez {info.KtoWydal}";
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
            var orderPositions = new List<(int TowarId, decimal Ilosc)>();
            using (var cn = new SqlConnection(_connLibra))
            {
                await cn.OpenAsync();
                string sql = @"SELECT zmt.KodTowaru, zmt.Ilosc 
                               FROM dbo.ZamowieniaMiesoTowar zmt 
                               WHERE zmt.ZamowienieId=@Id";
                var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Id", info.Id);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    orderPositions.Add((rd.GetInt32(0), rd.GetDecimal(1)));
                }
            }

            // Pobierz wydania dla klienta - DOKŁADNIE JAK W PRODUKCJAPANEL
            var shipments = await GetShipmentsForClientAsync(info.KlientId);

            // Połącz wszystkie ID towarów
            var ids = orderPositions.Select(p => p.TowarId).Union(shipments.Keys).Where(i => i > 0).Distinct().ToList();

            // Pobierz nazwy towarów - DOKŁADNIE JAK W PRODUKCJAPANEL
            var towary = await LoadTowaryAsync(ids);

            // Stwórz tabelę z pozycjami - DOKŁADNIE JAK W PRODUKCJAPANEL
            var dt = new DataTable();
            dt.Columns.Add("Produkt", typeof(string));
            dt.Columns.Add("Zamówiono (kg)", typeof(decimal));
            dt.Columns.Add("Wydano (kg)", typeof(decimal));
            dt.Columns.Add("Różnica (kg)", typeof(decimal));

            foreach (var pos in orderPositions)
            {
                string produktNazwa = towary.ContainsKey(pos.TowarId) ? towary[pos.TowarId].Kod : $"Towar {pos.TowarId}";
                decimal wydano = shipments.ContainsKey(pos.TowarId) ? shipments[pos.TowarId] : 0;
                decimal roznica = pos.Ilosc - wydano;

                dt.Rows.Add(produktNazwa, pos.Ilosc, wydano, roznica);
            }

            dgvPozycje.ItemsSource = dt.DefaultView;
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

        public class ZamowienieViewModel : INotifyPropertyChanged
        {
            public ZamowienieInfo Info { get; }
            public ZamowienieViewModel(ZamowienieInfo info) { Info = info; }

            public string Klient => Info.Klient;
            public decimal TotalIlosc => Info.TotalIlosc;
            public string Handlowiec => Info.Handlowiec;
            public string Status => Info.Status;
            public string NumerRejestracyjny => Info.NumerRejestracyjny ?? "Brak";
            public string Kierowca => Info.Kierowca ?? "Brak";

            // Wyświetlanie pojazdu z ilością palet z bazy danych
            public string PojazdDisplay
            {
                get
                {
                    if (string.IsNullOrEmpty(Info.NumerRejestracyjny))
                        return "Brak";

                    if (Info.PaletyH1.HasValue && Info.PaletyH1.Value > 0)
                        return $"{Info.NumerRejestracyjny} ({Info.PaletyH1}p)";

                    return Info.NumerRejestracyjny;
                }
            }

            // Wyświetlanie kierowcy - samo imię i nazwisko
            public string KierowcaDisplay => string.IsNullOrEmpty(Info.Kierowca) ? "Brak" : Info.Kierowca;

            public string CzasWyjazdDisplay => Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue
                ? $"{Info.CzasWyjazdu.Value:hh\\:mm}"
                : "Brak";

            public DateTime SortDateTime => Info.CzasWyjazdu.HasValue && Info.DataKursu.HasValue
                ? Info.DataKursu.Value.Add(Info.CzasWyjazdu.Value)
                : DateTime.MaxValue;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}