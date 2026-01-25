using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class MasowePobieranieWindow : Window
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly FirefliesService _firefliesService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _pobieranieTrwa;
        private int _znalezione;
        private int _pobrane;
        private int _pominiete;
        private readonly ObservableCollection<string> _logEntries = new();

        public MasowePobieranieWindow(FirefliesService firefliesService)
        {
            InitializeComponent();
            _firefliesService = firefliesService;

            // Ustaw domyslne daty
            DpOdDaty.SelectedDate = DateTime.Today.AddMonths(-6);
            DpDoDaty.SelectedDate = DateTime.Today;

            ListaLog.ItemsSource = _logEntries;
        }

        private void ZakresZmieniony(object sender, RoutedEventArgs e)
        {
            if (PanelZakresDat != null)
            {
                PanelZakresDat.IsEnabled = RbZakres.IsChecked == true;
            }
        }

        private async void BtnRozpocznij_Click(object sender, RoutedEventArgs e)
        {
            if (_pobieranieTrwa)
                return;

            // Walidacja
            if (RbZakres.IsChecked == true)
            {
                if (!DpOdDaty.SelectedDate.HasValue || !DpDoDaty.SelectedDate.HasValue)
                {
                    MessageBox.Show("Wybierz zakres dat.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (DpOdDaty.SelectedDate > DpDoDaty.SelectedDate)
                {
                    MessageBox.Show("Data 'Od' musi byc wczesniejsza niz data 'Do'.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Sprawdz konfiguracje API
            var config = await _firefliesService.PobierzKonfiguracje();
            if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
            {
                MessageBox.Show("Brak skonfigurowanego klucza API Fireflies.\nPrzejdz do ustawien Fireflies i skonfiguruj klucz API.",
                    "Blad konfiguracji", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await RozpocznijPobieranie();
        }

        private async Task RozpocznijPobieranie()
        {
            _pobieranieTrwa = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // UI
            BtnRozpocznij.IsEnabled = false;
            BtnAnuluj.Visibility = Visibility.Visible;
            PanelStatus.Visibility = Visibility.Visible;
            PanelInfo.Visibility = Visibility.Collapsed;

            _znalezione = 0;
            _pobrane = 0;
            _pominiete = 0;
            _logEntries.Clear();

            try
            {
                // Krok 1: Pobierz liste transkrypcji z Fireflies
                DodajLog("Pobieranie listy transkrypcji z Fireflies...");
                TxtStatus.Text = "Pobieranie listy transkrypcji...";

                DateTime? odDaty = RbZakres.IsChecked == true ? DpOdDaty.SelectedDate : null;
                DateTime? doDaty = RbZakres.IsChecked == true ? DpDoDaty.SelectedDate : null;

                var listaTranskrypcji = await _firefliesService.PobierzListeTranskrypcji(500, odDaty);

                if (token.IsCancellationRequested) return;

                // Filtruj po dacie "do" jesli podano
                if (doDaty.HasValue)
                {
                    listaTranskrypcji = listaTranskrypcji.FindAll(t =>
                        !t.DateAsDateTime.HasValue || t.DateAsDateTime.Value <= doDaty.Value.AddDays(1));
                }

                _znalezione = listaTranskrypcji.Count;
                AktualizujLiczniki();
                DodajLog($"Znaleziono {_znalezione} transkrypcji w Fireflies");

                if (_znalezione == 0)
                {
                    TxtStatus.Text = "Brak transkrypcji do pobrania";
                    DodajLog("Brak transkrypcji spelniajacych kryteria");
                    return;
                }

                ProgressPobieranie.Maximum = _znalezione;
                ProgressPobieranie.Value = 0;

                // Krok 2: Przetwarzaj kazda transkrypcje
                bool nadpiszIstniejace = ChkNadpiszIstniejace.IsChecked == true;
                bool pominKrotkie = ChkPominKrotkie.IsChecked == true;
                bool pobierzSzczegoly = ChkPobierzSzczegoly.IsChecked == true;
                int minCzas = pominKrotkie ? 60 : 0;

                int i = 0;
                foreach (var t in listaTranskrypcji)
                {
                    if (token.IsCancellationRequested) break;

                    i++;
                    ProgressPobieranie.Value = i;
                    TxtStatus.Text = $"Przetwarzanie {i}/{_znalezione}: {t.Title ?? t.Id}";

                    try
                    {
                        // Sprawdz czy istnieje
                        bool istnieje = await CzyTranskrypcjaIstnieje(t.Id!);
                        if (istnieje && !nadpiszIstniejace)
                        {
                            _pominiete++;
                            AktualizujLiczniki();
                            continue;
                        }

                        // Sprawdz czas trwania
                        if (t.Duration.HasValue && t.Duration.Value < minCzas)
                        {
                            _pominiete++;
                            AktualizujLiczniki();
                            DodajLog($"Pominieto (krotkie): {t.Title ?? t.Id}");
                            continue;
                        }

                        // Pobierz szczegoly jesli potrzeba
                        FirefliesTranscriptDto? szczegoly = t;
                        if (pobierzSzczegoly)
                        {
                            szczegoly = await _firefliesService.PobierzSzczegolyTranskrypcji(t.Id!);
                            if (szczegoly == null)
                            {
                                _pominiete++;
                                AktualizujLiczniki();
                                DodajLog($"Blad pobierania szczegolow: {t.Title ?? t.Id}");
                                continue;
                            }
                        }

                        // Zapisz do bazy
                        if (istnieje && nadpiszIstniejace)
                        {
                            await UsunTranskrypcje(t.Id!);
                        }

                        await ZapiszTranskrypcje(szczegoly);
                        _pobrane++;
                        AktualizujLiczniki();
                        DodajLog($"Pobrano: {t.Title ?? t.Id}");
                    }
                    catch (Exception ex)
                    {
                        _pominiete++;
                        AktualizujLiczniki();
                        DodajLog($"Blad: {t.Title ?? t.Id} - {ex.Message}");
                    }

                    // Maly delay zeby nie przeciazyc API
                    await Task.Delay(200, token);
                }

                if (token.IsCancellationRequested)
                {
                    TxtStatus.Text = "Pobieranie anulowane";
                    DodajLog("Pobieranie zostalo anulowane przez uzytkownika");
                }
                else
                {
                    TxtStatus.Text = $"Zakonczone! Pobrano {_pobrane} transkrypcji";
                    DodajLog($"=== ZAKONCZONO: Pobrano {_pobrane}, pominieto {_pominiete} ===");
                    DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Blad: {ex.Message}";
                DodajLog($"BLAD KRYTYCZNY: {ex.Message}");
                MessageBox.Show($"Blad podczas pobierania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _pobieranieTrwa = false;
                BtnRozpocznij.IsEnabled = true;
                BtnAnuluj.Visibility = Visibility.Collapsed;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void AktualizujLiczniki()
        {
            TxtZnalezione.Text = _znalezione.ToString();
            TxtPobrane.Text = _pobrane.ToString();
            TxtPominiete.Text = _pominiete.ToString();
        }

        private void DodajLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logEntries.Add($"[{timestamp}] {message}");

            // Scrolluj na dol
            if (ListaLog.Items.Count > 0)
            {
                ListaLog.ScrollIntoView(ListaLog.Items[ListaLog.Items.Count - 1]);
            }
        }

        private async Task<bool> CzyTranskrypcjaIstnieje(string firefliesId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "SELECT COUNT(*) FROM FirefliesTranskrypcje WHERE FirefliesID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", firefliesId);

            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        private async Task UsunTranskrypcje(string firefliesId)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = "DELETE FROM FirefliesTranskrypcje WHERE FirefliesID = @ID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", firefliesId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task ZapiszTranskrypcje(FirefliesTranscriptDto dto)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"INSERT INTO FirefliesTranskrypcje
                (FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy, Uczestnicy, HostEmail,
                 Transkrypcja, TranskrypcjaUrl, Podsumowanie, AkcjeDoDziałania, SlowKluczowe,
                 NastepneKroki, StatusImportu, DataImportu)
            VALUES
                (@FirefliesID, @Tytul, @DataSpotkania, @CzasTrwania, @Uczestnicy, @HostEmail,
                 @Transkrypcja, @TranskrypcjaUrl, @Podsumowanie, @Akcje, @Slowa,
                 @Kroki, 'Zaimportowane', GETDATE())";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@FirefliesID", dto.Id);
            cmd.Parameters.AddWithValue("@Tytul", (object?)dto.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataSpotkania", (object?)dto.DateAsDateTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CzasTrwania", dto.Duration ?? 0);

            // Uczestnicy jako JSON
            var uczestnicy = dto.Participants != null ? JsonSerializer.Serialize(dto.Participants) : null;
            cmd.Parameters.AddWithValue("@Uczestnicy", (object?)uczestnicy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@HostEmail", (object?)dto.HostEmail ?? DBNull.Value);

            // Transkrypcja - zlacz zdania
            string? transkrypcjaTekst = null;
            if (dto.Sentences != null && dto.Sentences.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var s in dto.Sentences)
                {
                    sb.AppendLine($"[{s.SpeakerName ?? s.SpeakerId?.ToString() ?? "?"}]: {s.Text}");
                }
                transkrypcjaTekst = sb.ToString();
            }
            cmd.Parameters.AddWithValue("@Transkrypcja", (object?)transkrypcjaTekst ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TranskrypcjaUrl", (object?)dto.TranscriptUrl ?? DBNull.Value);

            // Summary
            cmd.Parameters.AddWithValue("@Podsumowanie", (object?)dto.Summary?.Overview ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Akcje", DBNull.Value);
            cmd.Parameters.AddWithValue("@Slowa", dto.Summary?.Keywords != null
                ? JsonSerializer.Serialize(dto.Summary.Keywords) : DBNull.Value);
            cmd.Parameters.AddWithValue("@Kroki", DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                var result = MessageBox.Show("Czy na pewno chcesz anulowac pobieranie?\nJuz pobrane transkrypcje zostana zachowane.",
                    "Anuluj pobieranie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource.Cancel();
                    TxtStatus.Text = "Anulowanie...";
                }
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            if (_pobieranieTrwa)
            {
                var result = MessageBox.Show("Pobieranie jest w toku. Czy na pewno chcesz zamknac okno?\nPobieranie zostanie anulowane.",
                    "Zamknij", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource?.Cancel();
                    Close();
                }
            }
            else
            {
                Close();
            }
        }
    }
}
