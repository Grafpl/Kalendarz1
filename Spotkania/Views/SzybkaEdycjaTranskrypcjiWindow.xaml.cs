using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class SzybkaEdycjaTranskrypcjiWindow : Window
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly FirefliesService _firefliesService;
        private readonly long _transkrypcjaId;
        private readonly string _firefliesId;

        private string? _wybranaKategoria;
        private readonly ObservableCollection<UczestnikEdycja> _uczestnicy = new();
        private readonly List<PracownikItem> _pracownicy = new();
        private readonly List<string> _kategorie = new()
        {
            "Zespol", "Klient", "Sprzedaz", "Support", "Planowanie",
            "Szkolenie", "Interview", "1:1", "Standup", "Inne"
        };

        public bool ZapianoZmiany { get; private set; }

        public SzybkaEdycjaTranskrypcjiWindow(FirefliesService firefliesService, long transkrypcjaId, string firefliesId)
        {
            InitializeComponent();
            _firefliesService = firefliesService;
            _transkrypcjaId = transkrypcjaId;
            _firefliesId = firefliesId;

            ListaUczestnikow.ItemsSource = _uczestnicy;

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Pobierz dane transkrypcji
                var transkrypcja = await _firefliesService.PobierzTranskrypcjeZBazyPoId(_transkrypcjaId);
                if (transkrypcja == null)
                {
                    MessageBox.Show("Nie znaleziono transkrypcji.", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                // Wypelnij dane
                TxtTytul.Text = transkrypcja.Tytul ?? "";
                TxtDataSpotkania.Text = transkrypcja.DataSpotkania?.ToString("dd.MM.yyyy HH:mm") ?? "";

                // Pobierz kategorie z bazy (jesli istnieje kolumna)
                _wybranaKategoria = await PobierzKategorie();
                TxtWybranaKategoria.Text = string.IsNullOrEmpty(_wybranaKategoria) ? "(brak)" : _wybranaKategoria;

                // Pobierz liste kategorii uzywanych w systemie
                await PobierzKategorieZBazy();

                // Wyswietl przyciski kategorii
                WyswietlKategorie();

                // Pobierz pracownikow
                await PobierzPracownikow();

                // Pobierz uczestnikow z transkrypcji
                foreach (var u in transkrypcja.Uczestnicy)
                {
                    var uczestnik = new UczestnikEdycja
                    {
                        NazwaFireflies = u.DisplayName ?? "Nieznany",
                        SpeakerId = u.SpeakerId,
                        PrzypisanyUserId = u.PrzypisanyUserID,
                        DostepniPracownicy = _pracownicy.ToList()
                    };

                    if (!string.IsNullOrEmpty(u.PrzypisanyUserID))
                    {
                        uczestnik.WybranyPracownik = _pracownicy.FirstOrDefault(p => p.UserID == u.PrzypisanyUserID);
                    }

                    _uczestnicy.Add(uczestnik);
                }

                TxtBrakUczestnikow.Visibility = _uczestnicy.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string?> PobierzKategorie()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                // Sprawdz czy kolumna istnieje
                await UpewnijSieZeKolumnaIstnieje(conn);

                string sql = "SELECT Kategoria FROM FirefliesTranskrypcje WHERE TranskrypcjaID = @ID";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ID", _transkrypcjaId);

                var result = await cmd.ExecuteScalarAsync();
                return result as string;
            }
            catch
            {
                return null;
            }
        }

        private async Task UpewnijSieZeKolumnaIstnieje(SqlConnection conn)
        {
            try
            {
                string checkSql = @"IF NOT EXISTS (SELECT * FROM sys.columns
                                    WHERE object_id = OBJECT_ID('FirefliesTranskrypcje') AND name = 'Kategoria')
                                    ALTER TABLE FirefliesTranskrypcje ADD Kategoria NVARCHAR(100)";
                using var cmd = new SqlCommand(checkSql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { }
        }

        private async Task PobierzKategorieZBazy()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = @"SELECT DISTINCT Kategoria FROM FirefliesTranskrypcje
                              WHERE Kategoria IS NOT NULL AND Kategoria != ''";
                using var cmd = new SqlCommand(sql, conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kat = reader.GetString(0);
                    if (!_kategorie.Contains(kat))
                    {
                        _kategorie.Add(kat);
                    }
                }
            }
            catch { }
        }

        private void WyswietlKategorie()
        {
            PanelKategorie.Children.Clear();

            foreach (var kat in _kategorie)
            {
                var btn = new Button
                {
                    Content = kat,
                    Style = (Style)FindResource("BtnKategoria"),
                    Tag = kat
                };

                if (kat == _wybranaKategoria)
                {
                    btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2"));
                    btn.Foreground = Brushes.White;
                }

                btn.Click += BtnKategoria_Click;
                PanelKategorie.Children.Add(btn);
            }
        }

        private void BtnKategoria_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string kategoria)
            {
                _wybranaKategoria = kategoria;
                TxtWybranaKategoria.Text = kategoria;
                WyswietlKategorie();
            }
        }

        private void BtnDodajKategorie_Click(object sender, RoutedEventArgs e)
        {
            var nowaKategoria = TxtNowaKategoria.Text.Trim();
            if (string.IsNullOrEmpty(nowaKategoria))
                return;

            if (!_kategorie.Contains(nowaKategoria))
            {
                _kategorie.Add(nowaKategoria);
            }

            _wybranaKategoria = nowaKategoria;
            TxtWybranaKategoria.Text = nowaKategoria;
            TxtNowaKategoria.Text = "";
            WyswietlKategorie();
        }

        private async Task PobierzPracownikow()
        {
            _pracownicy.Clear();
            _pracownicy.Add(new PracownikItem { UserID = "", DisplayName = "(Nie przypisano)" });

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                string sql = @"SELECT UserID, Imie, Nazwisko, Email
                              FROM Uzytkownicy
                              WHERE Aktywny = 1
                              ORDER BY Nazwisko, Imie";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    _pracownicy.Add(new PracownikItem
                    {
                        UserID = reader.GetString(0),
                        DisplayName = $"{reader.GetString(1)} {reader.GetString(2)}",
                        Email = reader.IsDBNull(3) ? null : reader.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad pobierania pracownikow: {ex.Message}");
            }
        }

        private void BtnDodajUczestnika_Click(object sender, RoutedEventArgs e)
        {
            var uczestnik = new UczestnikEdycja
            {
                NazwaFireflies = "Nowy uczestnik",
                DostepniPracownicy = _pracownicy.ToList(),
                IsNew = true
            };

            _uczestnicy.Add(uczestnik);
            TxtBrakUczestnikow.Visibility = Visibility.Collapsed;
        }

        private void BtnUsunUczestnika_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UczestnikEdycja uczestnik)
            {
                _uczestnicy.Remove(uczestnik);
                TxtBrakUczestnikow.Visibility = _uczestnicy.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            var nowyTytul = TxtTytul.Text.Trim();
            if (string.IsNullOrEmpty(nowyTytul))
            {
                MessageBox.Show("Podaj nazwe spotkania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                BtnZapisz.IsEnabled = false;
                BtnZapisz.Content = "Zapisywanie...";

                // Zapisz do bazy lokalnej
                await ZapiszDoBazy(nowyTytul);

                // Aktualizuj w Fireflies jesli zaznaczono
                if (ChkAktualizujFireflies.IsChecked == true && !string.IsNullOrEmpty(_firefliesId))
                {
                    var (success, message) = await _firefliesService.AktualizujTytulWFireflies(_firefliesId, nowyTytul);
                    if (!success)
                    {
                        MessageBox.Show($"Zapisano lokalnie, ale nie udalo sie zaktualizowac w Fireflies:\n{message}",
                            "Ostrzezenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                ZapianoZmiany = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisywania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapisz.IsEnabled = true;
                BtnZapisz.Content = "Zapisz zmiany";
            }
        }

        private async Task ZapiszDoBazy(string nowyTytul)
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            // Przygotuj JSON uczestnikow
            var uczestnicyJson = JsonSerializer.Serialize(_uczestnicy.Select(u => new
            {
                nazwa = u.NazwaFireflies,
                speakerId = u.SpeakerId,
                userId = u.WybranyPracownik?.UserID,
                email = u.WybranyPracownik?.Email
            }).ToList());

            string sql = @"UPDATE FirefliesTranskrypcje SET
                          Tytul = @Tytul,
                          Kategoria = @Kategoria,
                          Uczestnicy = @Uczestnicy,
                          DataModyfikacji = GETDATE()
                          WHERE TranskrypcjaID = @ID";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ID", _transkrypcjaId);
            cmd.Parameters.AddWithValue("@Tytul", nowyTytul);
            cmd.Parameters.AddWithValue("@Kategoria", (object?)_wybranaKategoria ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Uczestnicy", uczestnicyJson);

            await cmd.ExecuteNonQueryAsync();

            // Zapisz mapowania globalne
            foreach (var u in _uczestnicy.Where(x => x.WybranyPracownik != null && !string.IsNullOrEmpty(x.WybranyPracownik.UserID)))
            {
                await _firefliesService.ZapiszGlobalneMapowanie(
                    u.NazwaFireflies,
                    u.WybranyPracownik!.UserID,
                    u.WybranyPracownik.DisplayName,
                    u.WybranyPracownik.Email);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #region Helper Classes

        public class UczestnikEdycja : INotifyPropertyChanged
        {
            public string NazwaFireflies { get; set; } = "";
            public string? SpeakerId { get; set; }
            public string? PrzypisanyUserId { get; set; }
            public bool IsNew { get; set; }

            private PracownikItem? _wybranyPracownik;
            public PracownikItem? WybranyPracownik
            {
                get => _wybranyPracownik;
                set
                {
                    _wybranyPracownik = value;
                    OnPropertyChanged(nameof(WybranyPracownik));
                    OnPropertyChanged(nameof(PrzypisanyDisplay));
                }
            }

            public List<PracownikItem> DostepniPracownicy { get; set; } = new();

            public string PrzypisanyDisplay =>
                WybranyPracownik != null && !string.IsNullOrEmpty(WybranyPracownik.UserID)
                    ? $"→ {WybranyPracownik.DisplayName}"
                    : "(nie przypisano)";

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public class PracownikItem
        {
            public string UserID { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public string? Email { get; set; }
        }

        #endregion
    }
}
