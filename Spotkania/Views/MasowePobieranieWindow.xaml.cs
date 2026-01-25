using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;
using Microsoft.Win32;

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
        private int _bledy;
        private readonly ObservableCollection<string> _logEntries = new();
        private string? _folderDocelowy;

        public MasowePobieranieWindow(FirefliesService firefliesService)
        {
            InitializeComponent();
            _firefliesService = firefliesService;

            // Ustaw domyslne daty
            DpOdDaty.SelectedDate = DateTime.Today.AddMonths(-6);
            DpDoDaty.SelectedDate = DateTime.Today;

            // Domyslny folder - Dokumenty/Fireflies
            var dokumenty = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _folderDocelowy = Path.Combine(dokumenty, "Fireflies");
            TxtFolderDocelowy.Text = _folderDocelowy;

            ListaLog.ItemsSource = _logEntries;
        }

        private void BtnWybierzFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisania transkrypcji",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(_folderDocelowy) && Directory.Exists(_folderDocelowy))
            {
                dialog.SelectedPath = _folderDocelowy;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _folderDocelowy = dialog.SelectedPath;
                TxtFolderDocelowy.Text = _folderDocelowy;
            }
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

            // Walidacja folderu
            if (string.IsNullOrWhiteSpace(_folderDocelowy))
            {
                MessageBox.Show("Wybierz folder docelowy.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Walidacja dat
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

            // Utworz folder jesli nie istnieje
            try
            {
                if (!Directory.Exists(_folderDocelowy))
                {
                    Directory.CreateDirectory(_folderDocelowy);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie mozna utworzyc folderu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
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
            _bledy = 0;
            _logEntries.Clear();

            bool zapiszTxt = RbFormatTxt.IsChecked == true || RbFormatOba.IsChecked == true;
            bool zapiszJson = RbFormatJson.IsChecked == true || RbFormatOba.IsChecked == true;
            bool zapiszDoBazy = ChkZapiszDoBazy.IsChecked == true;
            bool nadpiszPliki = ChkNadpiszPliki.IsChecked == true;
            bool dodajDate = ChkDodajDate.IsChecked == true;

            try
            {
                // Krok 1: Pobierz liste transkrypcji z Fireflies
                DodajLog("Pobieranie listy transkrypcji z Fireflies...");
                TxtStatus.Text = "Pobieranie listy transkrypcji...";

                DateTime? odDaty = RbZakres.IsChecked == true ? DpOdDaty.SelectedDate : null;
                DateTime? doDaty = RbZakres.IsChecked == true ? DpDoDaty.SelectedDate : null;

                var listaTranskrypcji = await _firefliesService.PobierzListeTranskrypcji(1000, odDaty);

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
                int i = 0;
                foreach (var t in listaTranskrypcji)
                {
                    if (token.IsCancellationRequested) break;

                    i++;
                    ProgressPobieranie.Value = i;
                    TxtStatus.Text = $"Przetwarzanie {i}/{_znalezione}: {t.Title ?? t.Id}";

                    try
                    {
                        // Pobierz pelne szczegoly z Fireflies
                        var szczegoly = await _firefliesService.PobierzSzczegolyTranskrypcji(t.Id!);
                        if (szczegoly == null)
                        {
                            _bledy++;
                            AktualizujLiczniki();
                            DodajLog($"Blad pobierania szczegolow: {t.Title ?? t.Id}");
                            continue;
                        }

                        // Przygotuj nazwe pliku
                        string nazwaPliku = PrzygotujNazwePliku(szczegoly, dodajDate);

                        // Zapisz do pliku TXT
                        if (zapiszTxt)
                        {
                            string sciezkaTxt = Path.Combine(_folderDocelowy!, nazwaPliku + ".txt");
                            if (nadpiszPliki || !File.Exists(sciezkaTxt))
                            {
                                await ZapiszJakoTxt(szczegoly, sciezkaTxt);
                            }
                        }

                        // Zapisz do pliku JSON
                        if (zapiszJson)
                        {
                            string sciezkaJson = Path.Combine(_folderDocelowy!, nazwaPliku + ".json");
                            if (nadpiszPliki || !File.Exists(sciezkaJson))
                            {
                                await ZapiszJakoJson(szczegoly, sciezkaJson);
                            }
                        }

                        // Zapisz do bazy danych
                        if (zapiszDoBazy)
                        {
                            bool istnieje = await CzyTranskrypcjaIstnieje(t.Id!);
                            if (!istnieje)
                            {
                                await ZapiszTranskrypcjeDoBazy(szczegoly);
                            }
                        }

                        _pobrane++;
                        AktualizujLiczniki();
                        DodajLog($"Pobrano: {t.Title ?? t.Id}");
                    }
                    catch (Exception ex)
                    {
                        _bledy++;
                        AktualizujLiczniki();
                        DodajLog($"Blad: {t.Title ?? t.Id} - {ex.Message}");
                    }

                    // Maly delay zeby nie przeciazyc API
                    await Task.Delay(300, token);
                }

                if (token.IsCancellationRequested)
                {
                    TxtStatus.Text = "Pobieranie anulowane";
                    DodajLog("Pobieranie zostalo anulowane przez uzytkownika");
                }
                else
                {
                    TxtStatus.Text = $"Zakonczone! Pobrano {_pobrane} transkrypcji";
                    DodajLog($"=== ZAKONCZONO: Pobrano {_pobrane}, bledy: {_bledy} ===");
                    DodajLog($"Pliki zapisane w: {_folderDocelowy}");

                    MessageBox.Show($"Pobrano {_pobrane} transkrypcji.\n\nPliki zapisane w:\n{_folderDocelowy}",
                        "Pobieranie zakonczone", MessageBoxButton.OK, MessageBoxImage.Information);

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

        private string PrzygotujNazwePliku(FirefliesTranscriptDto dto, bool dodajDate)
        {
            string nazwa = dto.Title ?? dto.Id ?? "transkrypcja";

            // Usun niedozwolone znaki z nazwy pliku
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                nazwa = nazwa.Replace(c, '_');
            }

            // Skroc jesli za dluga
            if (nazwa.Length > 100)
            {
                nazwa = nazwa.Substring(0, 100);
            }

            // Dodaj date jesli wybrano
            if (dodajDate && dto.DateAsDateTime.HasValue)
            {
                nazwa = $"{dto.DateAsDateTime.Value:yyyy-MM-dd}_{nazwa}";
            }

            return nazwa;
        }

        private async Task ZapiszJakoTxt(FirefliesTranscriptDto dto, string sciezka)
        {
            var sb = new StringBuilder();

            // Naglowek
            sb.AppendLine("================================================================================");
            sb.AppendLine($"TRANSKRYPCJA: {dto.Title ?? "Brak tytulu"}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();
            sb.AppendLine($"Data:          {dto.DateAsDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "Brak daty"}");
            sb.AppendLine($"Czas trwania:  {FormatujCzas((int)(dto.Duration ?? 0))}");
            sb.AppendLine($"Organizator:   {dto.HostEmail ?? "Nieznany"}");

            if (dto.Participants != null && dto.Participants.Count > 0)
            {
                sb.AppendLine($"Uczestnicy:    {string.Join(", ", dto.Participants)}");
            }

            sb.AppendLine();

            // Podsumowanie
            if (!string.IsNullOrEmpty(dto.Summary?.Overview))
            {
                sb.AppendLine("--- PODSUMOWANIE ---");
                sb.AppendLine(dto.Summary.Overview);
                sb.AppendLine();
            }

            // Slowa kluczowe
            if (dto.Summary?.Keywords != null && dto.Summary.Keywords.Count > 0)
            {
                sb.AppendLine($"Slowa kluczowe: {string.Join(", ", dto.Summary.Keywords)}");
                sb.AppendLine();
            }

            // Transkrypcja
            sb.AppendLine("--- TRANSKRYPCJA ---");
            sb.AppendLine();

            if (dto.Sentences != null && dto.Sentences.Count > 0)
            {
                string? aktualnyMowca = null;
                foreach (var s in dto.Sentences)
                {
                    string mowca = s.SpeakerName ?? $"Mowca {s.SpeakerId}";

                    if (mowca != aktualnyMowca)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"[{mowca}]:");
                        aktualnyMowca = mowca;
                    }

                    sb.AppendLine($"  {s.Text}");
                }
            }
            else
            {
                sb.AppendLine("(Brak transkrypcji)");
            }

            sb.AppendLine();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Pobrano z Fireflies.ai - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("================================================================================");

            await File.WriteAllTextAsync(sciezka, sb.ToString(), Encoding.UTF8);
        }

        private async Task ZapiszJakoJson(FirefliesTranscriptDto dto, string sciezka)
        {
            var obiekt = new
            {
                id = dto.Id,
                title = dto.Title,
                date = dto.DateAsDateTime,
                duration_seconds = dto.Duration,
                host_email = dto.HostEmail,
                participants = dto.Participants,
                transcript_url = dto.TranscriptUrl,
                audio_url = dto.AudioUrl,
                summary = new
                {
                    overview = dto.Summary?.Overview,
                    keywords = dto.Summary?.Keywords
                },
                sentences = dto.Sentences?.ConvertAll(s => new
                {
                    index = s.Index,
                    speaker_id = s.SpeakerId,
                    speaker_name = s.SpeakerName,
                    text = s.Text,
                    start_time = s.StartTime,
                    end_time = s.EndTime
                }),
                exported_at = DateTime.Now
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(obiekt, options);
            await File.WriteAllTextAsync(sciezka, json, Encoding.UTF8);
        }

        private string FormatujCzas(int sekundy)
        {
            var ts = TimeSpan.FromSeconds(sekundy);
            if (ts.Hours > 0)
                return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
            else if (ts.Minutes > 0)
                return $"{ts.Minutes}m {ts.Seconds}s";
            else
                return $"{ts.Seconds}s";
        }

        private void AktualizujLiczniki()
        {
            TxtZnalezione.Text = _znalezione.ToString();
            TxtPobrane.Text = _pobrane.ToString();
            TxtBledy.Text = _bledy.ToString();
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

        private async Task ZapiszTranskrypcjeDoBazy(FirefliesTranscriptDto dto)
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
                var result = MessageBox.Show("Czy na pewno chcesz anulowac pobieranie?\nJuz pobrane pliki zostana zachowane.",
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
