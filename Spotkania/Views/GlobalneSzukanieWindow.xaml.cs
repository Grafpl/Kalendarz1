using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class GlobalneSzukanieWindow : Window
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly FirefliesService _firefliesService;
        private List<WynikWyszukiwania> _wyniki = new();

        public event Action<string, long>? TranskrypcjaOtwarta;

        private static readonly string[] KoloryMowcow = {
            "#2196F3", "#4CAF50", "#FF9800", "#9C27B0", "#F44336",
            "#00BCD4", "#795548", "#607D8B", "#E91E63", "#3F51B5"
        };

        public GlobalneSzukanieWindow(FirefliesService firefliesService)
        {
            InitializeComponent();
            _firefliesService = firefliesService;

            Loaded += async (s, e) =>
            {
                await ZaladujStatystyki();
                TxtSzukaj.Focus();
            };

            ListaWynikow.SelectionChanged += (s, e) =>
            {
                BtnOtworzWybrana.IsEnabled = ListaWynikow.SelectedItem != null;
            };
        }

        private async Task ZaladujStatystyki()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                // Policz transkrypcje i zdania
                string sql = @"
                    SELECT
                        (SELECT COUNT(*) FROM FirefliesTranskrypcje) as LiczbaTranskrypcji,
                        (SELECT COUNT(*) FROM FirefliesMowcyMapowania) as LiczbaMapowan";

                using var cmd = new SqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    int liczbaTranskrypcji = reader.GetInt32(0);
                    int liczbaMapowan = reader.GetInt32(1);
                    TxtStatystyki.Text = $"Przeszukuje {liczbaTranskrypcji} transkrypcji • {liczbaMapowan} zmapowanych mowcow";
                }
            }
            catch
            {
                TxtStatystyki.Text = "Przeszukuje wszystkie zapisane transkrypcje";
            }
        }

        private void TxtSzukaj_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSzukaj_Click(sender, e);
            }
        }

        private async void BtnSzukaj_Click(object sender, RoutedEventArgs e)
        {
            var fraza = TxtSzukaj.Text.Trim();
            if (string.IsNullOrEmpty(fraza) || fraza.Length < 2)
            {
                MessageBox.Show("Wpisz co najmniej 2 znaki do wyszukania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await WyszukajAsync(fraza);
        }

        private async Task WyszukajAsync(string fraza)
        {
            BtnSzukaj.IsEnabled = false;
            PanelLadowanie.Visibility = Visibility.Visible;
            PanelBrakWynikow.Visibility = Visibility.Collapsed;
            ListaWynikow.ItemsSource = null;

            try
            {
                _wyniki = await PrzeszukajTranskrypcjeAsync(fraza);

                if (_wyniki.Count == 0)
                {
                    PanelBrakWynikow.Visibility = Visibility.Visible;
                    TxtLiczbaWynikow.Text = "";
                }
                else
                {
                    ListaWynikow.ItemsSource = _wyniki;
                    TxtLiczbaWynikow.Text = $"({_wyniki.Count} wynikow w {_wyniki.Select(w => w.FirefliesId).Distinct().Count()} transkrypcjach)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wyszukiwania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PanelLadowanie.Visibility = Visibility.Collapsed;
                BtnSzukaj.IsEnabled = true;
            }
        }

        private async Task<List<WynikWyszukiwania>> PrzeszukajTranskrypcjeAsync(string fraza)
        {
            var wyniki = new List<WynikWyszukiwania>();
            bool caseSensitive = ChkCaseSensitive.IsChecked == true;
            bool tylkoMowca = ChkTylkoMowca.IsChecked == true;

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            // Pobierz wszystkie transkrypcje z pelnym tekstem
            string sql = @"
                SELECT
                    t.TranskrypcjaID,
                    t.FirefliesID,
                    t.Tytul,
                    t.DataSpotkania,
                    t.Transkrypcja,
                    t.Uczestnicy
                FROM FirefliesTranskrypcje t
                WHERE t.Transkrypcja IS NOT NULL
                  AND LEN(t.Transkrypcja) > 0
                ORDER BY t.DataSpotkania DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var frazaDoSzukania = caseSensitive ? fraza : fraza.ToLower();
            var kolorIndex = 0;
            var mowcaKolory = new Dictionary<string, string>();

            while (await reader.ReadAsync())
            {
                var transkrypcjaId = reader.GetInt64(0);
                var firefliesId = reader.GetString(1);
                var tytul = reader.IsDBNull(2) ? "Bez tytulu" : reader.GetString(2);
                var dataSpotkania = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                var transkrypcja = reader.IsDBNull(4) ? "" : reader.GetString(4);
                var uczestnicy = reader.IsDBNull(5) ? "" : reader.GetString(5);

                if (string.IsNullOrEmpty(transkrypcja)) continue;

                // Parsuj transkrypcje - format: [Mowca]: Tekst
                var linie = transkrypcja.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var czasCounter = 0.0;

                foreach (var linia in linie)
                {
                    var tekstDoSzukania = caseSensitive ? linia : linia.ToLower();
                    var pozycja = tekstDoSzukania.IndexOf(frazaDoSzukania);

                    if (pozycja >= 0)
                    {
                        // Parsuj mowce
                        string mowca = "Nieznany";
                        string tekst = linia;

                        var dwukropek = linia.IndexOf("]:");
                        if (linia.StartsWith("[") && dwukropek > 0)
                        {
                            mowca = linia.Substring(1, dwukropek - 1);
                            tekst = linia.Substring(dwukropek + 2).Trim();
                        }

                        // Jesli tylko mowca - sprawdz czy fraza jest w nazwie mowcy
                        if (tylkoMowca)
                        {
                            var mowcaDoSzukania = caseSensitive ? mowca : mowca.ToLower();
                            if (!mowcaDoSzukania.Contains(frazaDoSzukania))
                                continue;
                        }

                        // Przypisz kolor mowcy
                        if (!mowcaKolory.ContainsKey(mowca))
                        {
                            mowcaKolory[mowca] = KoloryMowcow[kolorIndex % KoloryMowcow.Length];
                            kolorIndex++;
                        }

                        // Wyodrebnij fragment z kontekstem
                        var pozycjaWTekscie = caseSensitive
                            ? tekst.IndexOf(fraza)
                            : tekst.ToLower().IndexOf(fraza.ToLower());

                        string tekstPrzed = "";
                        string tekstZnaleziony = "";
                        string tekstPo = "";

                        if (pozycjaWTekscie >= 0)
                        {
                            int start = Math.Max(0, pozycjaWTekscie - 50);
                            int koniec = Math.Min(tekst.Length, pozycjaWTekscie + fraza.Length + 50);

                            tekstPrzed = (start > 0 ? "..." : "") + tekst.Substring(start, pozycjaWTekscie - start);
                            tekstZnaleziony = tekst.Substring(pozycjaWTekscie, fraza.Length);
                            tekstPo = tekst.Substring(pozycjaWTekscie + fraza.Length, koniec - pozycjaWTekscie - fraza.Length) + (koniec < tekst.Length ? "..." : "");
                        }
                        else
                        {
                            // Fraza w nazwie mowcy
                            tekstPrzed = tekst.Length > 100 ? tekst.Substring(0, 100) + "..." : tekst;
                        }

                        wyniki.Add(new WynikWyszukiwania
                        {
                            TranskrypcjaId = transkrypcjaId,
                            FirefliesId = firefliesId,
                            TytulSpotkania = tytul,
                            DataSpotkania = dataSpotkania?.ToString("dd.MM.yyyy HH:mm") ?? "Brak daty",
                            NazwaMowcy = mowca,
                            CzasWSekundach = czasCounter,
                            TekstPrzed = tekstPrzed,
                            TekstZnaleziony = tekstZnaleziony,
                            TekstPo = tekstPo,
                            MowcaKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(mowcaKolory[mowca]))
                        });
                    }

                    czasCounter += 5; // Przyblizony czas - 5 sekund na linie
                }
            }

            return wyniki.Take(200).ToList(); // Limit wynikow
        }

        private void ListaWynikow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            OtworzWybranaTranskrypcje();
        }

        private void BtnOtworzWybrana_Click(object sender, RoutedEventArgs e)
        {
            OtworzWybranaTranskrypcje();
        }

        private void OtworzWybranaTranskrypcje()
        {
            if (ListaWynikow.SelectedItem is WynikWyszukiwania wynik)
            {
                TranskrypcjaOtwarta?.Invoke(wynik.FirefliesId, wynik.TranskrypcjaId);
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class WynikWyszukiwania
    {
        public long TranskrypcjaId { get; set; }
        public string FirefliesId { get; set; } = "";
        public string TytulSpotkania { get; set; } = "";
        public string DataSpotkania { get; set; } = "";
        public string NazwaMowcy { get; set; } = "";
        public double CzasWSekundach { get; set; }
        public string TekstPrzed { get; set; } = "";
        public string TekstZnaleziony { get; set; } = "";
        public string TekstPo { get; set; } = "";

        public string CzasDisplay => TimeSpan.FromSeconds(CzasWSekundach).ToString(@"mm\:ss");

        public SolidColorBrush TloKolor => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA"));
        public SolidColorBrush MowcaKolor { get; set; } = new SolidColorBrush(Colors.Gray);
    }
}
