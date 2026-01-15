using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class TranskrypcjaSzczegolyWindow : Window
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly FirefliesService _firefliesService;
        private readonly string _firefliesId;
        private long _transkrypcjaId;

        // Dane
        private FirefliesTranskrypcja? _transkrypcja;
        private FirefliesTranscriptDto? _transkrypcjaApi;
        private List<FirefliesSentenceDto> _zdaniaApi = new();

        // Kolekcje
        private ObservableCollection<MowcaMapowanieDisplay> _mowcy = new();
        private ObservableCollection<ZdanieDisplay> _zdania = new();
        private List<PracownikItem> _pracownicy = new();

        // Ustawienia
        private bool _autoZapisMapowan = true;
        private bool _uzyjNazwSystemowych = true;
        private bool _pokazCzasy = true;
        private string _filtrMowcy = "";
        private string _filtrTekst = "";
        private double _calkowityCzas = 0;

        // Kolory
        private static readonly string[] KoloryMowcow = {
            "#2196F3", "#4CAF50", "#FF9800", "#9C27B0", "#F44336",
            "#00BCD4", "#795548", "#607D8B", "#E91E63", "#3F51B5"
        };
        private static readonly string[] KoloryTla = {
            "#E3F2FD", "#E8F5E9", "#FFF3E0", "#F3E5F5", "#FFEBEE",
            "#E0F7FA", "#EFEBE9", "#ECEFF1", "#FCE4EC", "#E8EAF6"
        };

        public TranskrypcjaSzczegolyWindow(string firefliesId, long transkrypcjaId = 0)
        {
            InitializeComponent();
            _firefliesService = new FirefliesService();
            _firefliesId = firefliesId;
            _transkrypcjaId = transkrypcjaId;

            TxtFirefliesId.Text = $"Fireflies ID: {firefliesId}";
            ListaMowcow.ItemsSource = _mowcy;
            ListaZdan.ItemsSource = _zdania;

            Loaded += async (s, e) => await LoadDataAsync();
        }

        #region Ladowanie danych

        private async Task LoadDataAsync()
        {
            try
            {
                UstawStatus("Ladowanie danych...");

                // 1. Pobierz pracownikow
                await PobierzPracownikow();

                // 2. Pobierz transkrypcje z bazy
                if (_transkrypcjaId > 0)
                {
                    _transkrypcja = await _firefliesService.PobierzTranskrypcjeZBazyPoId(_transkrypcjaId);
                }

                // 3. Pobierz z API
                UstawStatus("Pobieranie z Fireflies...");
                try
                {
                    _zdaniaApi = await _firefliesService.PobierzZdaniaTranskrypcji(_firefliesId);
                    _transkrypcjaApi = await _firefliesService.PobierzSzczegolyTranskrypcji(_firefliesId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Blad API: {ex.Message}");
                }

                // 4. Wypelnij formularz
                WypelnijFormularz();

                // 5. Wykryj mowcow
                WykryjMowcowZZdan();

                // 6. Zaladuj zapisane mapowania PRZED wypelnieniem zdan
                await ZaladujZapisaneMapowania();

                // 7. Wypelnij zdania
                WypelnijZdania();

                // 8. Pobierz powiazania
                await PobierzSpotkaniaINotatki();

                UstawStatus("");
                UstawSyncStatus(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                UstawStatus($"Blad: {ex.Message}");
            }
        }

        private async Task PobierzPracownikow()
        {
            _pracownicy.Clear();
            _pracownicy.Add(new PracownikItem { UserID = "", DisplayName = "(Nie przypisano)" });

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"SELECT ID, Name FROM operators ORDER BY Name";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var name = reader.IsDBNull(1) ? id : reader.GetString(1);
                _pracownicy.Add(new PracownikItem
                {
                    UserID = id,
                    DisplayName = string.IsNullOrEmpty(name) ? id : name
                });
            }
        }

        private void WypelnijFormularz()
        {
            if (_transkrypcjaApi != null)
            {
                TxtTytul.Text = _transkrypcjaApi.Title ?? "";
                TxtData.Text = _transkrypcjaApi.DateAsDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "---";
                TxtCzasTrwania.Text = FormatujCzas(_transkrypcjaApi.Duration);
                TxtOrganizator.Text = _transkrypcjaApi.EmailOrganizatora ?? "Nieznany";
                TxtPodsumowanie.Text = _transkrypcjaApi.Summary?.Overview ?? "";
                TxtSlowaKluczowe.Text = _transkrypcjaApi.Summary?.Keywords != null
                    ? string.Join(", ", _transkrypcjaApi.Summary.Keywords) : "";
                _calkowityCzas = _transkrypcjaApi.Duration ?? 0;
            }
            else if (_transkrypcja != null)
            {
                TxtTytul.Text = _transkrypcja.Tytul ?? "";
                TxtData.Text = _transkrypcja.DataSpotkaniaDisplay;
                TxtCzasTrwania.Text = _transkrypcja.CzasTrwaniaDisplay;
                TxtOrganizator.Text = _transkrypcja.HostEmail ?? "Nieznany";
                TxtPodsumowanie.Text = _transkrypcja.Podsumowanie ?? "";
                TxtSlowaKluczowe.Text = _transkrypcja.SlowKluczowe != null
                    ? string.Join(", ", _transkrypcja.SlowKluczowe) : "";
                _calkowityCzas = _transkrypcja.CzasTrwaniaSekundy;
            }
        }

        private void WykryjMowcowZZdan()
        {
            _mowcy.Clear();

            if (_zdaniaApi == null || _zdaniaApi.Count == 0)
            {
                TxtLiczbaMowcow.Text = "(0)";
                return;
            }

            // Grupuj i oblicz statystyki
            var grupyMowcow = _zdaniaApi
                .GroupBy(z => new { z.SpeakerId, z.SpeakerName })
                .Select((g, idx) => {
                    var zdania = g.ToList();
                    double czasMowienia = 0;
                    foreach (var z in zdania)
                    {
                        czasMowienia += (z.EndTime - z.StartTime);
                    }

                    return new
                    {
                        SpeakerId = g.Key.SpeakerId,
                        SpeakerName = g.Key.SpeakerName,
                        LiczbaWypowiedzi = zdania.Count,
                        CzasMowienia = czasMowienia,
                        PrzykladowaWypowiedz = zdania.FirstOrDefault()?.Text ?? "",
                        KolorIndex = idx
                    };
                })
                .OrderByDescending(m => m.CzasMowienia)
                .ToList();

            foreach (var g in grupyMowcow)
            {
                var kolorIdx = g.KolorIndex % KoloryMowcow.Length;
                var procentCzasu = _calkowityCzas > 0 ? (g.CzasMowienia / _calkowityCzas * 100) : 0;

                _mowcy.Add(new MowcaMapowanieDisplay
                {
                    SpeakerId = g.SpeakerId,
                    SpeakerNameFireflies = g.SpeakerName ?? $"Mowca {g.SpeakerId}",
                    LiczbaWypowiedzi = g.LiczbaWypowiedzi,
                    CzasMowienia = g.CzasMowienia,
                    ProcentCzasu = procentCzasu,
                    PrzykladowaWypowiedz = g.PrzykladowaWypowiedz.Length > 120
                        ? g.PrzykladowaWypowiedz.Substring(0, 120) + "..."
                        : g.PrzykladowaWypowiedz,
                    KolorMowcy = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryMowcow[kolorIdx])),
                    TloKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryTla[kolorIdx])),
                    DostepniPracownicy = _pracownicy
                });
            }

            TxtLiczbaMowcow.Text = $"({_mowcy.Count})";
        }

        private async Task ZaladujZapisaneMapowania()
        {
            if (_transkrypcjaId <= 0) return;

            try
            {
                var mapowania = await _firefliesService.PobierzMapowanieMowcow(_transkrypcjaId);

                foreach (var m in mapowania)
                {
                    var mowca = _mowcy.FirstOrDefault(x =>
                        x.SpeakerId == m.SpeakerId ||
                        x.SpeakerNameFireflies == m.SpeakerNameFireflies);

                    if (mowca != null && !string.IsNullOrEmpty(m.PrzypisanyUserID))
                    {
                        mowca.PrzypisanyUserID = m.PrzypisanyUserID;
                        mowca.PrzypisanyUserName = m.PrzypisanyUserName;
                    }
                }

                // Odswiez liste mowcow
                ListaMowcow.Items.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad mapowan: {ex.Message}");
            }
        }

        private void WypelnijZdania()
        {
            _zdania.Clear();

            if (_zdaniaApi != null && _zdaniaApi.Count > 0)
            {
                foreach (var z in _zdaniaApi.OrderBy(x => x.Index))
                {
                    var mowca = _mowcy.FirstOrDefault(m =>
                        m.SpeakerId == z.SpeakerId &&
                        (m.SpeakerNameFireflies == z.SpeakerName || m.SpeakerNameFireflies == $"Mowca {z.SpeakerId}"));

                    var zdanie = new ZdanieDisplay
                    {
                        Index = z.Index,
                        SpeakerId = z.SpeakerId,
                        MowcaFireflies = z.SpeakerName ?? $"Mowca {z.SpeakerId}",
                        Tekst = z.Text ?? "",
                        StartTime = z.StartTime,
                        TloKolor = mowca?.TloKolor ?? new SolidColorBrush(Colors.White),
                        MowcaKolor = mowca?.KolorMowcy ?? new SolidColorBrush(Colors.Gray),
                        UzyjNazwySystemowej = _uzyjNazwSystemowych,
                        PokazCzas = _pokazCzasy
                    };

                    // Przypisz nazwe systemowa jesli jest mapowanie
                    if (mowca != null && !string.IsNullOrEmpty(mowca.PrzypisanyUserID))
                    {
                        var pracownik = _pracownicy.FirstOrDefault(p => p.UserID == mowca.PrzypisanyUserID);
                        zdanie.MowcaSystemowy = pracownik?.DisplayName;
                    }

                    _zdania.Add(zdanie);
                }
            }
            else if (_transkrypcja?.Transkrypcja != null)
            {
                var lines = _transkrypcja.Transkrypcja.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                int idx = 0;

                foreach (var line in lines)
                {
                    var zdanie = new ZdanieDisplay { Index = idx++, UzyjNazwySystemowej = _uzyjNazwSystemowych, PokazCzas = _pokazCzasy };

                    var colonIndex = line.IndexOf(']');
                    if (colonIndex > 0 && line.StartsWith("["))
                    {
                        zdanie.MowcaFireflies = line.Substring(1, colonIndex - 1);
                        zdanie.Tekst = line.Substring(colonIndex + 2).Trim();
                    }
                    else
                    {
                        zdanie.Tekst = line;
                    }

                    _zdania.Add(zdanie);
                }
            }

            TxtLiczbaZdan.Text = $"({_zdania.Count} wypowiedzi)";
        }

        private async Task PobierzSpotkaniaINotatki()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                var spotkania = new List<SpotkanieComboItem> { new() { SpotkaniID = 0, TytulDisplay = "(Brak)" } };
                string sqlS = @"SELECT TOP 100 SpotkaniID, Tytul, DataRozpoczecia FROM Spotkania ORDER BY DataRozpoczecia DESC";
                using (var cmdS = new SqlCommand(sqlS, conn))
                using (var readerS = await cmdS.ExecuteReaderAsync())
                {
                    while (await readerS.ReadAsync())
                    {
                        spotkania.Add(new SpotkanieComboItem
                        {
                            SpotkaniID = readerS.GetInt64(0),
                            TytulDisplay = $"{readerS.GetDateTime(2):dd.MM} - {(readerS.IsDBNull(1) ? "Bez tytulu" : readerS.GetString(1))}"
                        });
                    }
                }
                CmbSpotkanie.ItemsSource = spotkania;
                CmbSpotkanie.SelectedIndex = 0;

                var notatki = new List<NotatkaComboItem> { new() { NotatkaID = 0, TematDisplay = "(Brak)" } };
                string sqlN = @"SELECT TOP 100 NotatkaID, Temat, DataSpotkania FROM NotatkiZeSpotkan ORDER BY DataSpotkania DESC";
                using (var cmdN = new SqlCommand(sqlN, conn))
                using (var readerN = await cmdN.ExecuteReaderAsync())
                {
                    while (await readerN.ReadAsync())
                    {
                        var data = readerN.IsDBNull(2) ? "" : readerN.GetDateTime(2).ToString("dd.MM");
                        notatki.Add(new NotatkaComboItem
                        {
                            NotatkaID = readerN.GetInt64(0),
                            TematDisplay = $"{data} - {(readerN.IsDBNull(1) ? "Bez tematu" : readerN.GetString(1))}"
                        });
                    }
                }
                CmbNotatka.ItemsSource = notatki;
                CmbNotatka.SelectedIndex = 0;

                if (_transkrypcja?.SpotkaniID.HasValue == true)
                {
                    var s = spotkania.FirstOrDefault(x => x.SpotkaniID == _transkrypcja.SpotkaniID);
                    if (s != null) CmbSpotkanie.SelectedItem = s;
                }
                if (_transkrypcja?.NotatkaID.HasValue == true)
                {
                    var n = notatki.FirstOrDefault(x => x.NotatkaID == _transkrypcja.NotatkaID);
                    if (n != null) CmbNotatka.SelectedItem = n;
                }
            }
            catch { }
        }

        #endregion

        #region Event Handlers - Mapowanie

        private async void CmbMowca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_autoZapisMapowan) return;

            var cmb = sender as ComboBox;
            var mowca = cmb?.Tag as MowcaMapowanieDisplay;
            if (mowca == null) return;

            // Pobierz wybrana wartosc
            var selectedItem = cmb?.SelectedItem as PracownikItem;
            if (selectedItem != null)
            {
                mowca.PrzypisanyUserName = selectedItem.DisplayName;
            }

            // Aktualizuj zdania
            AktualizujZdaniaMowcy(mowca);

            // Auto-zapisz jesli wlaczone
            if (_autoZapisMapowan && _transkrypcjaId > 0)
            {
                await ZapiszMapowaniaDoDb();
                UstawStatus("Mapowanie zapisane automatycznie");
                await Task.Delay(1500);
                UstawStatus("");
            }
        }

        private void AktualizujZdaniaMowcy(MowcaMapowanieDisplay mowca)
        {
            foreach (var z in _zdania.Where(x =>
                x.SpeakerId == mowca.SpeakerId ||
                x.MowcaFireflies == mowca.SpeakerNameFireflies))
            {
                if (!string.IsNullOrEmpty(mowca.PrzypisanyUserID))
                {
                    var pracownik = _pracownicy.FirstOrDefault(p => p.UserID == mowca.PrzypisanyUserID);
                    z.MowcaSystemowy = pracownik?.DisplayName;
                }
                else
                {
                    z.MowcaSystemowy = null;
                }
            }

            ListaZdan.Items.Refresh();
        }

        private void BtnAutoZapisz_Click(object sender, RoutedEventArgs e)
        {
            _autoZapisMapowan = !_autoZapisMapowan;
            BtnAutoZapisz.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(_autoZapisMapowan ? "#4CAF50" : "#9E9E9E"));
            BtnAutoZapisz.Content = _autoZapisMapowan ? "Auto-zapisz" : "Reczny zapis";
        }

        #endregion

        #region Event Handlers - Transkrypcja

        private void ChkUzyjNazwSystemowych_Changed(object sender, RoutedEventArgs e)
        {
            _uzyjNazwSystemowych = ChkUzyjNazwSystemowych.IsChecked == true;
            foreach (var z in _zdania) z.UzyjNazwySystemowej = _uzyjNazwSystemowych;
            ListaZdan.Items.Refresh();
        }

        private void ChkPokazCzasy_Changed(object sender, RoutedEventArgs e)
        {
            _pokazCzasy = ChkPokazCzasy.IsChecked == true;
            foreach (var z in _zdania) z.PokazCzas = _pokazCzasy;
            ListaZdan.Items.Refresh();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filtrTekst = TxtSzukaj.Text.ToLower();
            FiltrujZdania();
        }

        private void BtnWyczyscSzukaj_Click(object sender, RoutedEventArgs e)
        {
            TxtSzukaj.Text = "";
            _filtrTekst = "";
            _filtrMowcy = "";
            FiltrujZdania();
        }

        private void BtnFiltruj_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Wszyscy mowcy", Tag = "" });
            menu.Items.Add(new Separator());

            foreach (var m in _mowcy)
            {
                var nazwa = !string.IsNullOrEmpty(m.PrzypisanyUserName) ? m.PrzypisanyUserName : m.DisplayFireflies;
                menu.Items.Add(new MenuItem { Header = nazwa, Tag = m.SpeakerNameFireflies });
            }

            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                item.Click += (s, ev) =>
                {
                    _filtrMowcy = (s as MenuItem)?.Tag?.ToString() ?? "";
                    FiltrujZdania();
                };
            }

            menu.IsOpen = true;
        }

        private void FiltrujZdania()
        {
            foreach (var z in _zdania)
            {
                bool pasujeTekst = string.IsNullOrEmpty(_filtrTekst) ||
                    z.Tekst?.ToLower().Contains(_filtrTekst) == true ||
                    z.MowcaWyswietlany?.ToLower().Contains(_filtrTekst) == true;

                bool pasujeMowca = string.IsNullOrEmpty(_filtrMowcy) ||
                    z.MowcaFireflies == _filtrMowcy;

                z.Widocznosc = pasujeTekst && pasujeMowca ? Visibility.Visible : Visibility.Collapsed;
            }

            ListaZdan.Items.Refresh();
        }

        private void BtnKopiujTranskrypcje_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"TRANSKRYPCJA: {TxtTytul.Text}");
            sb.AppendLine($"Data: {TxtData.Text}");
            sb.AppendLine($"Czas trwania: {TxtCzasTrwania.Text}");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();

            foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
            {
                var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                    ? z.MowcaSystemowy : z.MowcaFireflies;
                sb.AppendLine($"[{z.CzasDisplay}] {mowca}:");
                sb.AppendLine($"  {z.Tekst}");
                sb.AppendLine();
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Transkrypcja skopiowana do schowka!", "Skopiowano", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEksportuj_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Kopiuj jako tekst", Tag = "txt" });
            menu.Items.Add(new MenuItem { Header = "Kopiuj jako CSV", Tag = "csv" });
            menu.Items.Add(new MenuItem { Header = "Kopiuj jako HTML", Tag = "html" });

            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                item.Click += (s, ev) => EksportujJako((s as MenuItem)?.Tag?.ToString() ?? "txt");
            }

            menu.IsOpen = true;
        }

        private void EksportujJako(string format)
        {
            var sb = new StringBuilder();

            switch (format)
            {
                case "csv":
                    sb.AppendLine("Czas,Mowca,Tekst");
                    foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
                    {
                        var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                            ? z.MowcaSystemowy : z.MowcaFireflies;
                        sb.AppendLine($"\"{z.CzasDisplay}\",\"{mowca}\",\"{z.Tekst?.Replace("\"", "\"\"")}\"");
                    }
                    break;

                case "html":
                    sb.AppendLine("<html><body>");
                    sb.AppendLine($"<h1>{TxtTytul.Text}</h1>");
                    sb.AppendLine($"<p>Data: {TxtData.Text} | Czas: {TxtCzasTrwania.Text}</p>");
                    sb.AppendLine("<table border='1'><tr><th>Czas</th><th>Mowca</th><th>Tekst</th></tr>");
                    foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
                    {
                        var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                            ? z.MowcaSystemowy : z.MowcaFireflies;
                        sb.AppendLine($"<tr><td>{z.CzasDisplay}</td><td><b>{mowca}</b></td><td>{z.Tekst}</td></tr>");
                    }
                    sb.AppendLine("</table></body></html>");
                    break;

                default:
                    BtnKopiujTranskrypcje_Click(null, null);
                    return;
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"Eksport {format.ToUpper()} skopiowany do schowka!", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Event Handlers - Glowne

        private void BtnOtworzFireflies_Click(object sender, RoutedEventArgs e)
        {
            var url = _transkrypcja?.TranskrypcjaUrl ?? $"https://app.fireflies.ai/view/{_firefliesId}";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            BtnOdswiez.IsEnabled = false;
            try
            {
                _zdaniaApi = await _firefliesService.PobierzZdaniaTranskrypcji(_firefliesId);
                _transkrypcjaApi = await _firefliesService.PobierzSzczegolyTranskrypcji(_firefliesId);
                WypelnijFormularz();
                WykryjMowcowZZdan();
                await ZaladujZapisaneMapowania();
                WypelnijZdania();
                UstawSyncStatus(true);
                MessageBox.Show("Odswiezono!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnOdswiez.IsEnabled = true;
            }
        }

        private async void BtnSyncTytul_Click(object sender, RoutedEventArgs e)
        {
            var nowyTytul = TxtTytul.Text.Trim();
            if (string.IsNullOrEmpty(nowyTytul)) return;

            BtnSyncTytul.IsEnabled = false;
            UstawStatus("Synchronizacja tytulu...");

            try
            {
                var (success, message) = await _firefliesService.AktualizujTytulWFireflies(_firefliesId, nowyTytul);
                if (success)
                {
                    UstawSyncStatus(true);
                    MessageBox.Show("Tytul zaktualizowany w Fireflies!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Blad: {message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                BtnSyncTytul.IsEnabled = true;
                UstawStatus("");
            }
        }

        private async void BtnPowiazSpotkanie_Click(object sender, RoutedEventArgs e)
        {
            var spotkanie = CmbSpotkanie.SelectedItem as SpotkanieComboItem;
            if (spotkanie == null || spotkanie.SpotkaniID == 0 || _transkrypcjaId <= 0) return;

            await _firefliesService.PowiazTranskrypcjeZeSpotkaniem(_transkrypcjaId, spotkanie.SpotkaniID);
            MessageBox.Show("Powiazano!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnPowiazNotatke_Click(object sender, RoutedEventArgs e)
        {
            var notatka = CmbNotatka.SelectedItem as NotatkaComboItem;
            if (notatka == null || notatka.NotatkaID == 0 || _transkrypcjaId <= 0) return;

            await _firefliesService.PowiazTranskrypcjeZNotatka(_transkrypcjaId, notatka.NotatkaID);
            MessageBox.Show("Powiazano!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Usunac transkrypcje?", "Potwierdz", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM FirefliesTranskrypcje WHERE TranskrypcjaID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", _transkrypcjaId);
            await cmd.ExecuteNonQueryAsync();

            DialogResult = true;
            Close();
        }

        private async void BtnZapiszWszystko_Click(object sender, RoutedEventArgs e)
        {
            BtnZapiszWszystko.IsEnabled = false;
            UstawStatus("Zapisywanie...");

            try
            {
                await ZapiszDoDb();
                await ZapiszMapowaniaDoDb();

                var nowyTytul = TxtTytul.Text.Trim();
                if (!string.IsNullOrEmpty(nowyTytul))
                {
                    await _firefliesService.AktualizujTytulWFireflies(_firefliesId, nowyTytul);
                }

                UstawSyncStatus(true);
                MessageBox.Show("Zapisano i zsynchronizowano!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapiszWszystko.IsEnabled = true;
            }
        }

        private async void BtnZapiszLokalnie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ZapiszDoDb();
                await ZapiszMapowaniaDoDb();
                MessageBox.Show("Zapisano lokalnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Zapis do DB

        private async Task ZapiszDoDb()
        {
            var slowa = TxtSlowaKluczowe.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList();

            var uczestnicyJson = JsonSerializer.Serialize(_mowcy.Select(m => new
            {
                nazwa = m.SpeakerNameFireflies,
                speakerId = m.SpeakerId?.ToString(),
                userId = m.PrzypisanyUserID,
                userName = m.PrzypisanyUserName
            }).ToList());

            if (_transkrypcjaId > 0)
            {
                await _firefliesService.AktualizujTranskrypcjeWBazie(_transkrypcjaId, TxtTytul.Text, TxtPodsumowanie.Text, slowa, uczestnicyJson);
            }
            else
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                var transkrypcjaTekst = string.Join("\n", _zdania.Select(z => $"[{z.MowcaFireflies}]: {z.Tekst}"));

                string sql = @"INSERT INTO FirefliesTranskrypcje
                    (FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy, Uczestnicy, HostEmail,
                     Transkrypcja, TranskrypcjaUrl, Podsumowanie, SlowKluczowe, StatusImportu, DataImportu)
                VALUES (@FirefliesID, @Tytul, @Data, @Czas, @Ucz, @Host, @Trans, @Url, @Pod, @Slowa, 'Reczny', GETDATE());
                SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FirefliesID", _firefliesId);
                cmd.Parameters.AddWithValue("@Tytul", TxtTytul.Text);
                cmd.Parameters.AddWithValue("@Data", (object?)_transkrypcjaApi?.DateAsDateTime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Czas", _transkrypcjaApi?.Duration ?? 0);
                cmd.Parameters.AddWithValue("@Ucz", uczestnicyJson);
                cmd.Parameters.AddWithValue("@Host", (object?)_transkrypcjaApi?.EmailOrganizatora ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Trans", transkrypcjaTekst);
                cmd.Parameters.AddWithValue("@Url", (object?)_transkrypcjaApi?.TranscriptUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Pod", (object?)TxtPodsumowanie.Text ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Slowa", JsonSerializer.Serialize(slowa));

                var newId = await cmd.ExecuteScalarAsync();
                _transkrypcjaId = Convert.ToInt64(newId);
            }
        }

        private async Task ZapiszMapowaniaDoDb()
        {
            if (_transkrypcjaId <= 0)
            {
                await ZapiszDoDb();
            }

            var mapowania = _mowcy.Select(m => new MowcaMapowanie
            {
                SpeakerId = m.SpeakerId,
                SpeakerNameFireflies = m.SpeakerNameFireflies,
                PrzypisanyUserID = m.PrzypisanyUserID,
                PrzypisanyUserName = m.PrzypisanyUserName
            }).ToList();

            await _firefliesService.ZapiszMapowanieMowcow(_transkrypcjaId, mapowania);
        }

        #endregion

        #region Pomocnicze

        private void UstawStatus(string tekst) => TxtStatusZapisu.Text = tekst;

        private void UstawSyncStatus(bool ok, string? blad = null)
        {
            BadgeSync.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ok ? "#4CAF50" : "#FF9800"));
            TxtSyncStatus.Text = ok ? "Zsynchronizowane" : (blad ?? "Niezapisane");
        }

        private string FormatujCzas(double? sek)
        {
            if (!sek.HasValue) return "---";
            var ts = TimeSpan.FromSeconds(sek.Value);
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}min" : $"{ts.Minutes}min {ts.Seconds}s";
        }

        #endregion
    }

    #region Helper Classes

    public class MowcaMapowanieDisplay : INotifyPropertyChanged
    {
        private string? _przypisanyUserID;
        private string? _przypisanyUserName;

        public int? SpeakerId { get; set; }
        public string? SpeakerNameFireflies { get; set; }
        public int LiczbaWypowiedzi { get; set; }
        public double CzasMowienia { get; set; }
        public double ProcentCzasu { get; set; }
        public string? PrzykladowaWypowiedz { get; set; }

        public string? PrzypisanyUserID
        {
            get => _przypisanyUserID;
            set { _przypisanyUserID = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusPrzypisania)); OnPropertyChanged(nameof(StatusTlo)); OnPropertyChanged(nameof(StatusKolor)); }
        }

        public string? PrzypisanyUserName
        {
            get => _przypisanyUserName;
            set { _przypisanyUserName = value; OnPropertyChanged(); }
        }

        public List<PracownikItem> DostepniPracownicy { get; set; } = new();

        public SolidColorBrush KolorMowcy { get; set; } = new SolidColorBrush(Colors.Gray);
        public SolidColorBrush TloKolor { get; set; } = new SolidColorBrush(Colors.White);

        public string DisplayFireflies => SpeakerNameFireflies ?? $"Mowca {SpeakerId}";
        public string CzasMowieniaDisplay => TimeSpan.FromSeconds(CzasMowienia).ToString(@"mm\:ss");
        public string ProcentCzasuDisplay => $"({ProcentCzasu:F0}%)";

        public string StatusPrzypisania => string.IsNullOrEmpty(PrzypisanyUserID)
            ? "Nie przypisano"
            : $"Przypisano: {PrzypisanyUserName}";

        public SolidColorBrush StatusTlo => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(PrzypisanyUserID) ? "#FFF3E0" : "#E8F5E9"));

        public SolidColorBrush StatusKolor => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(PrzypisanyUserID) ? "#E65100" : "#2E7D32"));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PracownikItem
    {
        public string UserID { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    public class ZdanieDisplay : INotifyPropertyChanged
    {
        private bool _uzyjNazwySystemowej = true;
        private bool _pokazCzas = true;
        private Visibility _widocznosc = Visibility.Visible;

        public int Index { get; set; }
        public int? SpeakerId { get; set; }
        public string? MowcaFireflies { get; set; }
        public string? MowcaSystemowy { get; set; }
        public string? Tekst { get; set; }
        public double StartTime { get; set; }

        public bool UzyjNazwySystemowej
        {
            get => _uzyjNazwySystemowej;
            set { _uzyjNazwySystemowej = value; OnPropertyChanged(nameof(MowcaWyswietlany)); }
        }

        public bool PokazCzas
        {
            get => _pokazCzas;
            set { _pokazCzas = value; OnPropertyChanged(nameof(CzasWidocznosc)); }
        }

        public Visibility Widocznosc
        {
            get => _widocznosc;
            set { _widocznosc = value; OnPropertyChanged(); }
        }

        public string CzasDisplay => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss");
        public Visibility CzasWidocznosc => PokazCzas ? Visibility.Visible : Visibility.Collapsed;

        public string MowcaWyswietlany =>
            UzyjNazwySystemowej && !string.IsNullOrEmpty(MowcaSystemowy)
                ? MowcaSystemowy
                : MowcaFireflies ?? "?";

        public SolidColorBrush TloKolor { get; set; } = new SolidColorBrush(Colors.White);
        public SolidColorBrush MowcaKolor { get; set; } = new SolidColorBrush(Colors.Gray);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SpotkanieComboItem
    {
        public long SpotkaniID { get; set; }
        public string TytulDisplay { get; set; } = "";
    }

    public class NotatkaComboItem
    {
        public long NotatkaID { get; set; }
        public string TematDisplay { get; set; } = "";
    }

    #endregion
}
