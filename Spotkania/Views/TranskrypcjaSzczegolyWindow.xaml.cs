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
        private FirefliesTranskrypcja? _transkrypcja;

        private ObservableCollection<UczestnikMapowanie> _uczestnicy = new();
        private ObservableCollection<ZdanieDisplay> _zdania = new();
        private List<PracownikItem> _pracownicy = new();

        public TranskrypcjaSzczegolyWindow(string firefliesId, long transkrypcjaId = 0)
        {
            InitializeComponent();
            _firefliesService = new FirefliesService();
            _firefliesId = firefliesId;
            _transkrypcjaId = transkrypcjaId;

            TxtFirefliesId.Text = $"Fireflies ID: {firefliesId}";
            ListaUczestnikow.ItemsSource = _uczestnicy;
            ListaZdan.ItemsSource = _zdania;

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Pobierz listę pracowników
                await PobierzPracownikow();

                // Pobierz dane transkrypcji
                if (_transkrypcjaId > 0)
                {
                    _transkrypcja = await _firefliesService.PobierzTranskrypcjeZBazyPoId(_transkrypcjaId);
                }

                if (_transkrypcja == null)
                {
                    // Pobierz z API jeśli nie ma w bazie
                    await OdswiezZApi();
                }
                else
                {
                    WypelnijFormularz();
                }

                // Pobierz spotkania i notatki do comboboxów
                await PobierzSpotkaniaINotatki();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PobierzPracownikow()
        {
            _pracownicy.Clear();
            _pracownicy.Add(new PracownikItem { UserID = "", DisplayName = "(Nie przypisano)" });

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"SELECT UserID, imie, nazwisko, email
                          FROM operators
                          WHERE aktywny = 1
                          ORDER BY nazwisko, imie";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var userId = reader.GetString(0);
                var imie = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var nazwisko = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var email = reader.IsDBNull(3) ? "" : reader.GetString(3);

                _pracownicy.Add(new PracownikItem
                {
                    UserID = userId,
                    DisplayName = $"{imie} {nazwisko}".Trim(),
                    Email = email
                });
            }
        }

        private void WypelnijFormularz()
        {
            if (_transkrypcja == null) return;

            TxtTytul.Text = _transkrypcja.Tytul ?? "";
            TxtData.Text = _transkrypcja.DataSpotkaniaDisplay;
            TxtCzasTrwania.Text = _transkrypcja.CzasTrwaniaDisplay;
            TxtOrganizator.Text = _transkrypcja.HostEmail ?? "Nieznany";
            TxtPodsumowanie.Text = _transkrypcja.Podsumowanie ?? "";
            TxtSlowaKluczowe.Text = _transkrypcja.SlowKluczowe != null
                ? string.Join(", ", _transkrypcja.SlowKluczowe)
                : "";

            // Wypełnij uczestników
            _uczestnicy.Clear();
            foreach (var u in _transkrypcja.Uczestnicy)
            {
                _uczestnicy.Add(new UczestnikMapowanie
                {
                    NazwaWFireflies = u.Nazwa,
                    EmailWFireflies = u.Email,
                    SpeakerId = u.SpeakerId,
                    PrzypisanyUserID = u.PrzypisanyUserID,
                    DostepniPracownicy = _pracownicy
                });
            }

            // Jeśli brak uczestników, dodaj z emaili
            if (_uczestnicy.Count == 0 && !string.IsNullOrEmpty(_transkrypcja.HostEmail))
            {
                _uczestnicy.Add(new UczestnikMapowanie
                {
                    NazwaWFireflies = _transkrypcja.HostEmail,
                    EmailWFireflies = _transkrypcja.HostEmail,
                    DostepniPracownicy = _pracownicy
                });
            }

            // Wypełnij zdania transkrypcji
            WypelnijZdania();
        }

        private void WypelnijZdania()
        {
            _zdania.Clear();

            if (_transkrypcja?.Transkrypcja == null) return;

            // Parse transkrypcji
            var lines = _transkrypcja.Transkrypcja.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int index = 0;

            // Kolory dla różnych mówców
            var kolory = new[] { "#E3F2FD", "#FFF3E0", "#E8F5E9", "#FCE4EC", "#F3E5F5" };
            var kolorMowcy = new Dictionary<string, (string Tlo, string Tekst)>();

            foreach (var line in lines)
            {
                var zdanie = new ZdanieDisplay { Index = index++ };

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

                // Przypisz kolor dla mówcy
                if (!string.IsNullOrEmpty(zdanie.Mowca))
                {
                    if (!kolorMowcy.ContainsKey(zdanie.Mowca))
                    {
                        var idx = kolorMowcy.Count % kolory.Length;
                        kolorMowcy[zdanie.Mowca] = (kolory[idx], "#1976D2");
                    }

                    zdanie.TloKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorMowcy[zdanie.Mowca].Tlo));
                    zdanie.MowcaKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorMowcy[zdanie.Mowca].Tekst));
                }

                _zdania.Add(zdanie);
            }

            TxtLiczbaZdan.Text = $"({_zdania.Count} wypowiedzi)";
        }

        private async Task OdswiezZApi()
        {
            try
            {
                BtnOdswiez.IsEnabled = false;
                BtnOdswiez.Content = "⏳ Pobieranie...";

                var dto = await _firefliesService.PobierzSzczegolyTranskrypcji(_firefliesId);
                if (dto == null)
                {
                    MessageBox.Show("Nie udało się pobrać danych z API.", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Konwertuj DTO na model
                _transkrypcja = new FirefliesTranskrypcja
                {
                    FirefliesID = dto.Id ?? _firefliesId,
                    Tytul = dto.Title,
                    DataSpotkania = dto.DateAsDateTime,
                    CzasTrwaniaSekundy = (int)(dto.Duration ?? 0),
                    HostEmail = dto.EmailOrganizatora,
                    TranskrypcjaUrl = dto.TranscriptUrl,
                    Podsumowanie = dto.Summary?.Overview
                };

                // Uczestnicy z sentences
                if (dto.Sentences != null)
                {
                    var mowcy = dto.Sentences
                        .Where(s => !string.IsNullOrEmpty(s.SpeakerName) || s.SpeakerId.HasValue)
                        .Select(s => new { s.SpeakerName, s.SpeakerId })
                        .Distinct()
                        .ToList();

                    foreach (var m in mowcy)
                    {
                        _transkrypcja.Uczestnicy.Add(new FirefliesUczestnik
                        {
                            SpeakerId = m.SpeakerId?.ToString(),
                            DisplayName = m.SpeakerName
                        });
                    }

                    // Zbuduj transkrypcję
                    var sb = new System.Text.StringBuilder();
                    foreach (var s in dto.Sentences)
                    {
                        sb.AppendLine($"[{s.SpeakerName ?? s.SpeakerId?.ToString() ?? "?"}]: {s.Text}");
                    }
                    _transkrypcja.Transkrypcja = sb.ToString();
                }

                // Słowa kluczowe
                if (dto.Summary?.Keywords != null)
                {
                    _transkrypcja.SlowKluczowe = dto.Summary.Keywords;
                }

                WypelnijFormularz();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania z API: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnOdswiez.IsEnabled = true;
                BtnOdswiez.Content = "🔄 Odśwież z API";
            }
        }

        private async Task PobierzSpotkaniaINotatki()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                // Spotkania
                var spotkania = new List<SpotkanieComboItem> { new() { SpotkaniID = 0, TytulDisplay = "(Brak powiązania)" } };

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
                            TytulDisplay = $"{readerS.GetDateTime(2):dd.MM.yyyy} - {(readerS.IsDBNull(1) ? "Bez tytułu" : readerS.GetString(1))}"
                        });
                    }
                }

                CmbSpotkanie.ItemsSource = spotkania;
                CmbSpotkanie.SelectedIndex = 0;

                // Notatki
                var notatki = new List<NotatkaComboItem> { new() { NotatkaID = 0, TematDisplay = "(Brak powiązania)" } };

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

                // Ustaw aktualne powiązania
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
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania powiązań: {ex.Message}");
            }
        }

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
                MessageBox.Show($"Nie udało się otworzyć przeglądarki: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await OdswiezZApi();
        }

        private void BtnZapiszUczestnika_Click(object sender, RoutedEventArgs e)
        {
            // Zapisywane będzie razem z całym formularzem
            MessageBox.Show("Przypisanie zostanie zapisane po kliknięciu 'Zapisz zmiany'.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDodajUczestnika_Click(object sender, RoutedEventArgs e)
        {
            _uczestnicy.Add(new UczestnikMapowanie
            {
                NazwaWFireflies = "Nowy uczestnik",
                DostepniPracownicy = _pracownicy
            });
        }

        private async void BtnPowiazSpotkanie_Click(object sender, RoutedEventArgs e)
        {
            var spotkanie = CmbSpotkanie.SelectedItem as SpotkanieComboItem;
            if (spotkanie == null || spotkanie.SpotkaniID == 0)
            {
                MessageBox.Show("Wybierz spotkanie do powiązania.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_transkrypcjaId > 0)
            {
                await _firefliesService.PowiazTranskrypcjeZeSpotkaniem(_transkrypcjaId, spotkanie.SpotkaniID);
                MessageBox.Show("Powiązano ze spotkaniem.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnPowiazNotatke_Click(object sender, RoutedEventArgs e)
        {
            var notatka = CmbNotatka.SelectedItem as NotatkaComboItem;
            if (notatka == null || notatka.NotatkaID == 0)
            {
                MessageBox.Show("Wybierz notatkę do powiązania.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_transkrypcjaId > 0)
            {
                await _firefliesService.PowiazTranskrypcjeZNotatka(_transkrypcjaId, notatka.NotatkaID);
                MessageBox.Show("Powiązano z notatką.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnUtworzNotatke_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implementacja tworzenia notatki
            MessageBox.Show("Funkcja tworzenia notatki z transkrypcji będzie dostępna wkrótce.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Czy na pewno chcesz usunąć tę transkrypcję z bazy danych?\n\nNie wpłynie to na dane w Fireflies.ai.",
                "Potwierdź usunięcie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = "DELETE FROM FirefliesTranskrypcje WHERE TranskrypcjaID = @ID";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ID", _transkrypcjaId);
                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show("Transkrypcja została usunięta.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                // Przygotuj dane uczestników do JSON
                var uczestnicyJson = JsonSerializer.Serialize(_uczestnicy.Select(u => new
                {
                    nazwa = u.NazwaWFireflies,
                    email = u.EmailWFireflies,
                    speakerId = u.SpeakerId,
                    userId = u.PrzypisanyUserID
                }).ToList());

                // Słowa kluczowe
                var slowa = TxtSlowaKluczowe.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToList();
                var slowaJson = JsonSerializer.Serialize(slowa);

                if (_transkrypcjaId > 0)
                {
                    // Aktualizuj istniejący rekord
                    string sql = @"UPDATE FirefliesTranskrypcje SET
                        Tytul = @Tytul,
                        Podsumowanie = @Podsumowanie,
                        SlowKluczowe = @Slowa,
                        Uczestnicy = @Uczestnicy,
                        DataModyfikacji = GETDATE()
                    WHERE TranskrypcjaID = @ID";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ID", _transkrypcjaId);
                    cmd.Parameters.AddWithValue("@Tytul", TxtTytul.Text);
                    cmd.Parameters.AddWithValue("@Podsumowanie", (object?)TxtPodsumowanie.Text ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Slowa", slowaJson);
                    cmd.Parameters.AddWithValue("@Uczestnicy", uczestnicyJson);

                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    // Wstaw nowy rekord
                    string sql = @"INSERT INTO FirefliesTranskrypcje
                        (FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy, Uczestnicy, HostEmail,
                         Transkrypcja, TranskrypcjaUrl, Podsumowanie, SlowKluczowe, StatusImportu, DataImportu)
                    VALUES
                        (@FirefliesID, @Tytul, @DataSpotkania, @CzasTrwania, @Uczestnicy, @HostEmail,
                         @Transkrypcja, @TranskrypcjaUrl, @Podsumowanie, @Slowa, 'Ręczny', GETDATE());
                    SELECT SCOPE_IDENTITY();";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@FirefliesID", _firefliesId);
                    cmd.Parameters.AddWithValue("@Tytul", TxtTytul.Text);
                    cmd.Parameters.AddWithValue("@DataSpotkania", (object?)_transkrypcja?.DataSpotkania ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CzasTrwania", _transkrypcja?.CzasTrwaniaSekundy ?? 0);
                    cmd.Parameters.AddWithValue("@Uczestnicy", uczestnicyJson);
                    cmd.Parameters.AddWithValue("@HostEmail", (object?)_transkrypcja?.HostEmail ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Transkrypcja", (object?)_transkrypcja?.Transkrypcja ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TranskrypcjaUrl", (object?)_transkrypcja?.TranskrypcjaUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Podsumowanie", (object?)TxtPodsumowanie.Text ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Slowa", slowaJson);

                    var newId = await cmd.ExecuteScalarAsync();
                    _transkrypcjaId = Convert.ToInt64(newId);
                }

                MessageBox.Show("Zmiany zostały zapisane.", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #region Helper Classes

    public class UczestnikMapowanie : INotifyPropertyChanged
    {
        private string? _przypisanyUserID;

        public string? NazwaWFireflies { get; set; }
        public string? EmailWFireflies { get; set; }
        public string? SpeakerId { get; set; }

        public string? PrzypisanyUserID
        {
            get => _przypisanyUserID;
            set { _przypisanyUserID = value; OnPropertyChanged(); }
        }

        public List<PracownikItem> DostepniPracownicy { get; set; } = new();

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

    public class ZdanieDisplay
    {
        public int Index { get; set; }
        public string? Mowca { get; set; }
        public string? Tekst { get; set; }
        public double StartTime { get; set; }

        public string CzasDisplay => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss");
        public string MowcaDisplay => Mowca ?? "?";

        public SolidColorBrush TloKolor { get; set; } = new SolidColorBrush(Colors.White);
        public SolidColorBrush MowcaKolor { get; set; } = new SolidColorBrush(Colors.Gray);
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
