using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
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

        // Kolekcje do wyswietlania
        private ObservableCollection<MowcaMapowanieDisplay> _mowcy = new();
        private ObservableCollection<ZdanieDisplay> _zdania = new();
        private List<PracownikItem> _pracownicy = new();

        // Kolory dla mowcow
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
            WindowIconHelper.SetIcon(this);
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

                // 1. Pobierz liste pracownikow
                await PobierzPracownikow();

                // 2. Pobierz dane transkrypcji z bazy (jesli istnieje)
                if (_transkrypcjaId > 0)
                {
                    _transkrypcja = await _firefliesService.PobierzTranskrypcjeZBazyPoId(_transkrypcjaId);
                }

                // 3. Pobierz zdania z API (dla mapowania mowcow)
                UstawStatus("Pobieranie zdań z Fireflies...");
                try
                {
                    _zdaniaApi = await _firefliesService.PobierzZdaniaTranskrypcji(_firefliesId);
                    _transkrypcjaApi = await _firefliesService.PobierzSzczegolyTranskrypcji(_firefliesId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Blad pobierania z API: {ex.Message}");
                }

                // 4. Wypelnij formularz
                WypelnijFormularz();

                // 5. Wykryj mowcow z zdan
                WykryjMowcowZZdan();

                // 6. Wypelnij zdania (transkrypcje)
                WypelnijZdania();

                // 7. Pobierz spotkania i notatki do comboboxow
                await PobierzSpotkaniaINotatki();

                // 8. Zaladuj zapisane mapowania
                await ZaladujZapisaneMapowania();

                UstawStatus("");
                UstawSyncStatus(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                UstawStatus($"Blad: {ex.Message}");
            }
        }

        private async Task PobierzPracownikow()
        {
            _pracownicy.Clear();
            _pracownicy.Add(new PracownikItem { UserID = "", DisplayName = "(Nie przypisano)" });

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            // Tabela operators ma kolumny ID i Name
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
            // Najpierw probuj dane z API, potem z bazy
            if (_transkrypcjaApi != null)
            {
                TxtTytul.Text = _transkrypcjaApi.Title ?? "";
                TxtData.Text = _transkrypcjaApi.DateAsDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "---";
                TxtCzasTrwania.Text = FormatujCzas(_transkrypcjaApi.Duration);
                TxtOrganizator.Text = _transkrypcjaApi.EmailOrganizatora ?? "Nieznany";
                TxtPodsumowanie.Text = _transkrypcjaApi.Summary?.Overview ?? "";
                TxtSlowaKluczowe.Text = _transkrypcjaApi.Summary?.Keywords != null
                    ? string.Join(", ", _transkrypcjaApi.Summary.Keywords) : "";
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

            // Grupuj po speaker_id i speaker_name
            var grupyMowcow = _zdaniaApi
                .GroupBy(z => new { z.SpeakerId, z.SpeakerName })
                .Select((g, idx) => new
                {
                    SpeakerId = g.Key.SpeakerId,
                    SpeakerName = g.Key.SpeakerName,
                    LiczbaWypowiedzi = g.Count(),
                    PrzykladowaWypowiedz = g.FirstOrDefault()?.Text ?? "",
                    KolorIndex = idx
                })
                .OrderByDescending(m => m.LiczbaWypowiedzi)
                .ToList();

            foreach (var g in grupyMowcow)
            {
                var kolorIdx = g.KolorIndex % KoloryMowcow.Length;

                _mowcy.Add(new MowcaMapowanieDisplay
                {
                    SpeakerId = g.SpeakerId,
                    SpeakerNameFireflies = g.SpeakerName ?? $"Mowca {g.SpeakerId}",
                    LiczbaWypowiedzi = g.LiczbaWypowiedzi,
                    PrzykladowaWypowiedz = g.PrzykladowaWypowiedz.Length > 100
                        ? g.PrzykladowaWypowiedz.Substring(0, 100) + "..."
                        : g.PrzykladowaWypowiedz,
                    KolorMowcy = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryMowcow[kolorIdx])),
                    TloKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryTla[kolorIdx])),
                    DostepniPracownicy = _pracownicy
                });
            }

            TxtLiczbaMowcow.Text = $"({_mowcy.Count})";
        }

        private void WypelnijZdania()
        {
            _zdania.Clear();

            if (_zdaniaApi != null && _zdaniaApi.Count > 0)
            {
                // Wypelnij z API
                foreach (var z in _zdaniaApi.OrderBy(x => x.Index))
                {
                    var mowca = _mowcy.FirstOrDefault(m =>
                        m.SpeakerId == z.SpeakerId && m.SpeakerNameFireflies == (z.SpeakerName ?? $"Mowca {z.SpeakerId}"));

                    _zdania.Add(new ZdanieDisplay
                    {
                        Index = z.Index,
                        SpeakerId = z.SpeakerId,
                        Mowca = z.SpeakerName ?? $"Mowca {z.SpeakerId}",
                        Tekst = z.Text ?? "",
                        StartTime = z.StartTime,
                        TloKolor = mowca?.TloKolor ?? new SolidColorBrush(Colors.White),
                        MowcaKolor = mowca?.KolorMowcy ?? new SolidColorBrush(Colors.Gray)
                    });
                }
            }
            else if (_transkrypcja?.Transkrypcja != null)
            {
                // Wypelnij z bazy (stary format tekstowy)
                var lines = _transkrypcja.Transkrypcja.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                int idx = 0;

                foreach (var line in lines)
                {
                    var zdanie = new ZdanieDisplay { Index = idx++ };

                    // Parse format: [Speaker]: Text
                    var colonIndex = line.IndexOf(']');
                    if (colonIndex > 0 && line.StartsWith("["))
                    {
                        zdanie.Mowca = line.Substring(1, colonIndex - 1);
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

                // Aktualizuj zdania z mapowaniami
                AktualizujZdaniazMapowaniami();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad ladowania mapowan: {ex.Message}");
            }
        }

        private void AktualizujZdaniazMapowaniami()
        {
            foreach (var zdanie in _zdania)
            {
                var mowca = _mowcy.FirstOrDefault(m =>
                    m.SpeakerId == zdanie.SpeakerId ||
                    m.SpeakerNameFireflies == zdanie.Mowca);

                if (mowca != null && !string.IsNullOrEmpty(mowca.PrzypisanyUserID))
                {
                    var pracownik = _pracownicy.FirstOrDefault(p => p.UserID == mowca.PrzypisanyUserID);
                    zdanie.MowcaSystemowy = pracownik?.DisplayName ?? "";
                }
            }
        }

        private async Task PobierzSpotkaniaINotatki()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                // Spotkania
                var spotkania = new List<SpotkanieComboItem> { new() { SpotkaniID = 0, TytulDisplay = "(Brak powiazania)" } };

                string sqlS = @"SELECT TOP 100 SpotkaniID, Tytul, DataRozpoczecia
                               FROM Spotkania ORDER BY DataRozpoczecia DESC";
                using (var cmdS = new SqlCommand(sqlS, conn))
                using (var readerS = await cmdS.ExecuteReaderAsync())
                {
                    while (await readerS.ReadAsync())
                    {
                        spotkania.Add(new SpotkanieComboItem
                        {
                            SpotkaniID = readerS.GetInt64(0),
                            TytulDisplay = $"{readerS.GetDateTime(2):dd.MM.yyyy} - {(readerS.IsDBNull(1) ? "Bez tytulu" : readerS.GetString(1))}"
                        });
                    }
                }

                CmbSpotkanie.ItemsSource = spotkania;
                CmbSpotkanie.SelectedIndex = 0;

                // Notatki
                var notatki = new List<NotatkaComboItem> { new() { NotatkaID = 0, TematDisplay = "(Brak powiazania)" } };

                string sqlN = @"SELECT TOP 100 NotatkaID, Temat, DataSpotkania
                               FROM NotatkiZeSpotkan ORDER BY DataSpotkania DESC";
                using (var cmdN = new SqlCommand(sqlN, conn))
                using (var readerN = await cmdN.ExecuteReaderAsync())
                {
                    while (await readerN.ReadAsync())
                    {
                        var dataSpotkania = readerN.IsDBNull(2) ? "" : readerN.GetDateTime(2).ToString("dd.MM.yyyy");
                        notatki.Add(new NotatkaComboItem
                        {
                            NotatkaID = readerN.GetInt64(0),
                            TematDisplay = $"{dataSpotkania} - {(readerN.IsDBNull(1) ? "Bez tematu" : readerN.GetString(1))}"
                        });
                    }
                }

                CmbNotatka.ItemsSource = notatki;
                CmbNotatka.SelectedIndex = 0;

                // Ustaw aktualne powiazania
                if (_transkrypcja?.SpotkaniID.HasValue == true)
                {
                    var spotkanie = spotkania.FirstOrDefault(s => s.SpotkaniID == _transkrypcja.SpotkaniID);
                    if (spotkanie != null) CmbSpotkanie.SelectedItem = spotkanie;
                }

                if (_transkrypcja?.NotatkaID.HasValue == true)
                {
                    var notatka = notatki.FirstOrDefault(n => n.NotatkaID == _transkrypcja.NotatkaID);
                    if (notatka != null) CmbNotatka.SelectedItem = notatka;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad pobierania powiazan: {ex.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void BtnOtworzFireflies_Click(object sender, RoutedEventArgs e)
        {
            var url = _transkrypcja?.TranskrypcjaUrl ?? $"https://app.fireflies.ai/view/{_firefliesId}";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udalo sie otworzyc przegladarki: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            BtnOdswiez.IsEnabled = false;
            BtnOdswiez.Content = "Ladowanie...";

            try
            {
                _zdaniaApi = await _firefliesService.PobierzZdaniaTranskrypcji(_firefliesId);
                _transkrypcjaApi = await _firefliesService.PobierzSzczegolyTranskrypcji(_firefliesId);

                WypelnijFormularz();
                WykryjMowcowZZdan();
                WypelnijZdania();
                await ZaladujZapisaneMapowania();

                UstawSyncStatus(true);
                MessageBox.Show("Dane odswieżone z Fireflies.ai", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad pobierania z API: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnOdswiez.IsEnabled = true;
                BtnOdswiez.Content = "Odswiez z API";
            }
        }

        private async void BtnSyncTytul_Click(object sender, RoutedEventArgs e)
        {
            var nowyTytul = TxtTytul.Text.Trim();
            if (string.IsNullOrEmpty(nowyTytul))
            {
                MessageBox.Show("Podaj tytul spotkania", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnSyncTytul.IsEnabled = false;
            UstawStatus("Synchronizacja tytulu z Fireflies...");

            try
            {
                var (success, message) = await _firefliesService.AktualizujTytulWFireflies(_firefliesId, nowyTytul);

                if (success)
                {
                    UstawSyncStatus(true);
                    MessageBox.Show("Tytul zaktualizowany w Fireflies.ai!", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    UstawSyncStatus(false, message);
                    MessageBox.Show($"Blad synchronizacji: {message}", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                UstawSyncStatus(false, ex.Message);
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSyncTytul.IsEnabled = true;
                UstawStatus("");
            }
        }

        private async void BtnZapiszMapowanie_Click(object sender, RoutedEventArgs e)
        {
            if (_transkrypcjaId <= 0)
            {
                // Najpierw zapisz transkrypcje do bazy
                await ZapiszDoLocalnejBazy();
            }

            try
            {
                UstawStatus("Zapisywanie mapowania mowcow...");

                var mapowania = _mowcy.Select(m => new MowcaMapowanie
                {
                    SpeakerId = m.SpeakerId,
                    SpeakerNameFireflies = m.SpeakerNameFireflies,
                    PrzypisanyUserID = m.PrzypisanyUserID,
                    PrzypisanyUserName = _pracownicy.FirstOrDefault(p => p.UserID == m.PrzypisanyUserID)?.DisplayName
                }).ToList();

                await _firefliesService.ZapiszMapowanieMowcow(_transkrypcjaId, mapowania);

                // Aktualizuj wyswietlanie zdan
                AktualizujZdaniazMapowaniami();
                ListaZdan.Items.Refresh();

                UstawStatus("Mapowanie zapisane");
                MessageBox.Show("Mapowanie mowcow zostalo zapisane.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisywania mapowania: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                UstawStatus($"Blad: {ex.Message}");
            }
        }

        private void ChkPokazNazwySystemowe_Changed(object sender, RoutedEventArgs e)
        {
            var pokazSystemowe = ChkPokazNazwySystemowe.IsChecked == true;

            foreach (var zdanie in _zdania)
            {
                zdanie.PokazNazweSystemowa = pokazSystemowe;
            }

            ListaZdan.Items.Refresh();
        }

        private async void BtnPowiazSpotkanie_Click(object sender, RoutedEventArgs e)
        {
            var spotkanie = CmbSpotkanie.SelectedItem as SpotkanieComboItem;
            if (spotkanie == null || spotkanie.SpotkaniID == 0)
            {
                MessageBox.Show("Wybierz spotkanie do powiazania.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_transkrypcjaId > 0)
            {
                await _firefliesService.PowiazTranskrypcjeZeSpotkaniem(_transkrypcjaId, spotkanie.SpotkaniID);
                MessageBox.Show("Powiazano ze spotkaniem.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnPowiazNotatke_Click(object sender, RoutedEventArgs e)
        {
            var notatka = CmbNotatka.SelectedItem as NotatkaComboItem;
            if (notatka == null || notatka.NotatkaID == 0)
            {
                MessageBox.Show("Wybierz notatke do powiazania.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_transkrypcjaId > 0)
            {
                await _firefliesService.PowiazTranskrypcjeZNotatka(_transkrypcjaId, notatka.NotatkaID);
                MessageBox.Show("Powiazano z notatka.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Czy na pewno chcesz usunac te transkrypcje z bazy danych?\n\nNie wplynie to na dane w Fireflies.ai.",
                "Potwierdz usuniecie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = "DELETE FROM FirefliesTranskrypcje WHERE TranskrypcjaID = @ID";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ID", _transkrypcjaId);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show("Transkrypcja zostala usunieta.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad usuwania: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnZapiszWszystko_Click(object sender, RoutedEventArgs e)
        {
            BtnZapiszWszystko.IsEnabled = false;
            UstawStatus("Zapisywanie i synchronizacja...");

            try
            {
                // 1. Zapisz lokalnie
                await ZapiszDoLocalnejBazy();

                // 2. Sync tytul do Fireflies
                var nowyTytul = TxtTytul.Text.Trim();
                if (!string.IsNullOrEmpty(nowyTytul))
                {
                    var (success, message) = await _firefliesService.AktualizujTytulWFireflies(_firefliesId, nowyTytul);
                    if (!success)
                    {
                        MessageBox.Show($"Dane zapisane lokalnie, ale blad sync z Fireflies: {message}",
                            "Ostrzezenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                // 3. Zapisz mapowanie
                var mapowania = _mowcy.Select(m => new MowcaMapowanie
                {
                    SpeakerId = m.SpeakerId,
                    SpeakerNameFireflies = m.SpeakerNameFireflies,
                    PrzypisanyUserID = m.PrzypisanyUserID,
                    PrzypisanyUserName = _pracownicy.FirstOrDefault(p => p.UserID == m.PrzypisanyUserID)?.DisplayName
                }).ToList();

                await _firefliesService.ZapiszMapowanieMowcow(_transkrypcjaId, mapowania);

                UstawSyncStatus(true);
                UstawStatus("Wszystko zapisane!");
                MessageBox.Show("Wszystkie zmiany zostaly zapisane i zsynchronizowane.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                UstawSyncStatus(false, ex.Message);
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapiszWszystko.IsEnabled = true;
            }
        }

        private async void BtnZapiszLokalnie_Click(object sender, RoutedEventArgs e)
        {
            BtnZapiszLokalnie.IsEnabled = false;
            UstawStatus("Zapisywanie lokalnie...");

            try
            {
                await ZapiszDoLocalnejBazy();

                // Zapisz mapowanie
                var mapowania = _mowcy.Select(m => new MowcaMapowanie
                {
                    SpeakerId = m.SpeakerId,
                    SpeakerNameFireflies = m.SpeakerNameFireflies,
                    PrzypisanyUserID = m.PrzypisanyUserID,
                    PrzypisanyUserName = _pracownicy.FirstOrDefault(p => p.UserID == m.PrzypisanyUserID)?.DisplayName
                }).ToList();

                await _firefliesService.ZapiszMapowanieMowcow(_transkrypcjaId, mapowania);

                UstawStatus("Zapisano lokalnie");
                MessageBox.Show("Zmiany zostaly zapisane lokalnie.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisywania: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapiszLokalnie.IsEnabled = true;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Pomocnicze

        private async Task ZapiszDoLocalnejBazy()
        {
            var slowa = TxtSlowaKluczowe.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList();

            // Przygotuj uczestnikow JSON
            var uczestnicyJson = JsonSerializer.Serialize(_mowcy.Select(m => new
            {
                nazwa = m.SpeakerNameFireflies,
                speakerId = m.SpeakerId?.ToString(),
                userId = m.PrzypisanyUserID,
                userName = _pracownicy.FirstOrDefault(p => p.UserID == m.PrzypisanyUserID)?.DisplayName
            }).ToList());

            if (_transkrypcjaId > 0)
            {
                await _firefliesService.AktualizujTranskrypcjeWBazie(
                    _transkrypcjaId,
                    TxtTytul.Text,
                    TxtPodsumowanie.Text,
                    slowa,
                    uczestnicyJson);
            }
            else
            {
                // Wstaw nowa transkrypcje
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                // Zbuduj transkrypcje tekstowa
                var transkrypcjaTekst = string.Join("\n",
                    _zdania.Select(z => $"[{z.Mowca}]: {z.Tekst}"));

                string sql = @"INSERT INTO FirefliesTranskrypcje
                    (FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy, Uczestnicy, HostEmail,
                     Transkrypcja, TranskrypcjaUrl, Podsumowanie, SlowKluczowe, StatusImportu, DataImportu)
                VALUES
                    (@FirefliesID, @Tytul, @DataSpotkania, @CzasTrwania, @Uczestnicy, @HostEmail,
                     @Transkrypcja, @TranskrypcjaUrl, @Podsumowanie, @Slowa, 'Reczny', GETDATE());
                SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FirefliesID", _firefliesId);
                cmd.Parameters.AddWithValue("@Tytul", TxtTytul.Text);
                cmd.Parameters.AddWithValue("@DataSpotkania", (object?)_transkrypcjaApi?.DateAsDateTime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CzasTrwania", _transkrypcjaApi?.Duration ?? 0);
                cmd.Parameters.AddWithValue("@Uczestnicy", uczestnicyJson);
                cmd.Parameters.AddWithValue("@HostEmail", (object?)_transkrypcjaApi?.EmailOrganizatora ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Transkrypcja", transkrypcjaTekst);
                cmd.Parameters.AddWithValue("@TranskrypcjaUrl", (object?)_transkrypcjaApi?.TranscriptUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Podsumowanie", (object?)TxtPodsumowanie.Text ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Slowa", JsonSerializer.Serialize(slowa));

                var newId = await cmd.ExecuteScalarAsync();
                _transkrypcjaId = Convert.ToInt64(newId);
            }
        }

        private void UstawStatus(string tekst)
        {
            TxtStatusZapisu.Text = tekst;
        }

        private void UstawSyncStatus(bool zsynchronizowane, string? blad = null)
        {
            if (zsynchronizowane)
            {
                BadgeSync.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                TxtSyncStatus.Text = "Zsynchronizowane";
            }
            else
            {
                BadgeSync.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                TxtSyncStatus.Text = blad != null ? $"Blad: {blad}" : "Niezapisane";
            }
        }

        private string FormatujCzas(double? sekundy)
        {
            if (!sekundy.HasValue) return "---";
            var ts = TimeSpan.FromSeconds(sekundy.Value);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}min";
            return $"{ts.Minutes}min {ts.Seconds}s";
        }

        #endregion
    }

    #region Helper Classes

    public class MowcaMapowanieDisplay : INotifyPropertyChanged
    {
        private string? _przypisanyUserID;

        public int? SpeakerId { get; set; }
        public string? SpeakerNameFireflies { get; set; }
        public int LiczbaWypowiedzi { get; set; }
        public string? PrzykladowaWypowiedz { get; set; }

        public string? PrzypisanyUserID
        {
            get => _przypisanyUserID;
            set { _przypisanyUserID = value; OnPropertyChanged(); }
        }

        public string? PrzypisanyUserName { get; set; }

        public List<PracownikItem> DostepniPracownicy { get; set; } = new();

        public SolidColorBrush KolorMowcy { get; set; } = new SolidColorBrush(Colors.Gray);
        public SolidColorBrush TloKolor { get; set; } = new SolidColorBrush(Colors.White);

        public string DisplayFireflies => SpeakerNameFireflies ?? $"Mowca {SpeakerId}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PracownikItem
    {
        public string UserID { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Email { get; set; }
    }

    public class ZdanieDisplay : INotifyPropertyChanged
    {
        private bool _pokazNazweSystemowa;

        public int Index { get; set; }
        public int? SpeakerId { get; set; }
        public string? Mowca { get; set; }
        public string? Tekst { get; set; }
        public double StartTime { get; set; }
        public string? MowcaSystemowy { get; set; }

        public bool PokazNazweSystemowa
        {
            get => _pokazNazweSystemowa;
            set { _pokazNazweSystemowa = value; OnPropertyChanged(); OnPropertyChanged(nameof(MowcaSystemowyVisibility)); }
        }

        public string CzasDisplay => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss");
        public string MowcaDisplay => Mowca ?? "?";
        public Visibility MowcaSystemowyVisibility =>
            PokazNazweSystemowa && !string.IsNullOrEmpty(MowcaSystemowy) ? Visibility.Visible : Visibility.Collapsed;

        public SolidColorBrush TloKolor { get; set; } = new SolidColorBrush(Colors.White);
        public SolidColorBrush MowcaKolor { get; set; } = new SolidColorBrush(Colors.Gray);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
