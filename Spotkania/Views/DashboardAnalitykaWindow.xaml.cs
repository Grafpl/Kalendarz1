using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class DashboardAnalitykaWindow : Window
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly FirefliesService _firefliesService;
        private List<SpotkanieStat> _spotkania = new();

        public DashboardAnalitykaWindow(FirefliesService firefliesService)
        {
            InitializeComponent();
            _firefliesService = firefliesService;

            // Domyslny zakres - ostatnie 30 dni
            DpDoDaty.SelectedDate = DateTime.Today;
            DpOdDaty.SelectedDate = DateTime.Today.AddDays(-30);

            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (!DpOdDaty.SelectedDate.HasValue || !DpDoDaty.SelectedDate.HasValue)
                return;

            ShowLoading("Ladowanie danych...");

            try
            {
                var odDaty = DpOdDaty.SelectedDate.Value;
                var doDaty = DpDoDaty.SelectedDate.Value.AddDays(1);

                // Pobierz dane z bazy
                _spotkania = await PobierzStatystykiSpotkań(odDaty, doDaty);

                // Oblicz i wyswietl statystyki
                await Task.Run(() => ObliczStatystyki());

                Dispatcher.Invoke(() =>
                {
                    WyswietlStatystykiGlowne();
                    WyswietlWykresAktywnosci();
                    WyswietlTopMowcow();
                    WyswietlSentyment();
                    WyswietlTony();
                    WyswietlListeSpotkań();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task<List<SpotkanieStat>> PobierzStatystykiSpotkań(DateTime odDaty, DateTime doDaty)
        {
            var lista = new List<SpotkanieStat>();

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"SELECT TranskrypcjaID, FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy,
                                  Uczestnicy, Transkrypcja, Podsumowanie
                           FROM FirefliesTranskrypcje
                           WHERE DataSpotkania >= @OdDaty AND DataSpotkania < @DoDaty
                           ORDER BY DataSpotkania DESC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@OdDaty", odDaty);
            cmd.Parameters.AddWithValue("@DoDaty", doDaty);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var stat = new SpotkanieStat
                {
                    TranskrypcjaID = reader.GetInt64(0),
                    FirefliesID = reader.GetString(1),
                    Tytul = reader.IsDBNull(2) ? "Bez tytulu" : reader.GetString(2),
                    DataSpotkania = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                    CzasTrwaniaSekundy = reader.GetInt32(4),
                    Transkrypcja = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Podsumowanie = reader.IsDBNull(7) ? null : reader.GetString(7)
                };

                // Parse uczestnicy
                if (!reader.IsDBNull(5))
                {
                    try
                    {
                        var json = reader.GetString(5);
                        stat.Uczestnicy = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    }
                    catch
                    {
                        stat.Uczestnicy = new List<string>();
                    }
                }

                // Analizuj transkrypcje
                if (!string.IsNullOrEmpty(stat.Transkrypcja))
                {
                    AnalizujTranskrypcje(stat);
                }

                lista.Add(stat);
            }

            return lista;
        }

        private void AnalizujTranskrypcje(SpotkanieStat stat)
        {
            var linie = stat.Transkrypcja!.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var linia in linie)
            {
                // Format: [Mowca]: tekst
                var match = System.Text.RegularExpressions.Regex.Match(linia, @"^\[([^\]]+)\]:\s*(.*)$");
                if (match.Success)
                {
                    var mowca = match.Groups[1].Value.Trim();
                    var tekst = match.Groups[2].Value.Trim();

                    if (!stat.CzasMowcow.ContainsKey(mowca))
                        stat.CzasMowcow[mowca] = 0;

                    // Szacuj czas na podstawie dlugosci tekstu (ok 150 slow/min = 2.5 slowa/sek)
                    var slowa = tekst.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    var szacowanyCzas = slowa / 2.5;
                    stat.CzasMowcow[mowca] += (int)szacowanyCzas;

                    // Analiza sentymentu tekstu
                    AnalizujSentymentTekstu(stat, tekst);
                }
            }
        }

        private void AnalizujSentymentTekstu(SpotkanieStat stat, string tekst)
        {
            var lower = tekst.ToLowerInvariant();

            // Slowa pozytywne
            var pozytywne = new[] { "swietnie", "super", "doskonale", "fantastycznie", "gratulacje", "sukces",
                "zgadzam", "dobrze", "excellent", "great", "perfect", "amazing", "thanks", "dzieki", "dziekuje",
                "ciesze", "podoba", "wspaniale" };

            // Slowa negatywne
            var negatywne = new[] { "problem", "blad", "zle", "niestety", "nie dziala", "awaria", "opoznienie",
                "trudnosc", "nie zgadzam", "sprzeciwiam", "unfortunately", "issue", "bug", "failed", "error",
                "martwi", "obawiam", "szkoda" };

            // Tony
            var entuzjastyczne = new[] { "!", "swietnie", "super", "wow", "amazing", "excited", "fantastic" };
            var profesjonalne = new[] { "proponuje", "sugeruje", "rekomenduje", "analiza", "strategia", "plan" };
            var zestresowane = new[] { "pilne", "asap", "natychmiast", "deadline", "opoznienie", "problem", "kryzys" };

            foreach (var slowo in pozytywne)
                if (lower.Contains(slowo)) stat.SentymentPozytywny++;

            foreach (var slowo in negatywne)
                if (lower.Contains(slowo)) stat.SentymentNegatywny++;

            foreach (var slowo in entuzjastyczne)
                if (lower.Contains(slowo)) stat.TonEntuzjastyczny++;

            foreach (var slowo in profesjonalne)
                if (lower.Contains(slowo)) stat.TonProfesjonalny++;

            foreach (var slowo in zestresowane)
                if (lower.Contains(slowo)) stat.TonZestresowany++;

            stat.TonNeutralny++;
        }

        private void ObliczStatystyki()
        {
            // Statystyki sa obliczane w trakcie parsowania
        }

        private void WyswietlStatystykiGlowne()
        {
            if (_spotkania.Count == 0)
            {
                TxtLiczbaSpotkań.Text = "0";
                TxtLacznyCzas.Text = "0h";
                TxtSredniCzas.Text = "0m";
                TxtUczestnicy.Text = "0";
                TxtSentyment.Text = "-";
                return;
            }

            // Liczba spotkan
            TxtLiczbaSpotkań.Text = _spotkania.Count.ToString();

            // Laczny czas
            var lacznyCzas = _spotkania.Sum(s => s.CzasTrwaniaSekundy);
            var godziny = lacznyCzas / 3600;
            var minuty = (lacznyCzas % 3600) / 60;
            TxtLacznyCzas.Text = godziny > 0 ? $"{godziny}h {minuty}m" : $"{minuty}m";

            // Sredni czas
            var sredniCzas = lacznyCzas / _spotkania.Count;
            TxtSredniCzas.Text = $"{sredniCzas / 60}m";

            // Unikalni uczestnicy
            var unikalni = _spotkania.SelectMany(s => s.Uczestnicy).Distinct().Count();
            TxtUczestnicy.Text = unikalni.ToString();

            // Sredni sentyment
            var pozytywne = _spotkania.Sum(s => s.SentymentPozytywny);
            var negatywne = _spotkania.Sum(s => s.SentymentNegatywny);
            var total = pozytywne + negatywne;

            if (total > 0)
            {
                var sentymentScore = (double)pozytywne / total * 100;
                if (sentymentScore >= 70)
                {
                    TxtSentyment.Text = "Pozyt.";
                    TxtSentyment.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    TxtSentymentOpis.Text = $"{sentymentScore:F0}% pozytywnych";
                }
                else if (sentymentScore >= 40)
                {
                    TxtSentyment.Text = "Neutr.";
                    TxtSentyment.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                    TxtSentymentOpis.Text = "Zrownowazony";
                }
                else
                {
                    TxtSentyment.Text = "Negat.";
                    TxtSentyment.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                    TxtSentymentOpis.Text = $"{100 - sentymentScore:F0}% negatywnych";
                }
            }
        }

        private void WyswietlWykresAktywnosci()
        {
            var grupyTygodniowe = _spotkania
                .GroupBy(s => GetWeekStart(s.DataSpotkania))
                .OrderBy(g => g.Key)
                .Take(8)
                .ToList();

            var maxWartosc = grupyTygodniowe.Any() ? grupyTygodniowe.Max(g => g.Count()) : 1;

            var dane = grupyTygodniowe.Select(g => new WykresItem
            {
                Okres = g.Key.ToString("dd.MM"),
                Wartosc = g.Count().ToString(),
                SzerokoscPaska = (double)g.Count() / maxWartosc * 400
            }).ToList();

            WykresAktywnosci.ItemsSource = dane;
        }

        private DateTime GetWeekStart(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private void WyswietlTopMowcow()
        {
            var wszyscyMowcy = _spotkania
                .SelectMany(s => s.CzasMowcow)
                .GroupBy(kv => kv.Key)
                .Select(g => new { Nazwa = g.Key, Czas = g.Sum(x => x.Value) })
                .OrderByDescending(x => x.Czas)
                .Take(10)
                .ToList();

            var maxCzas = wszyscyMowcy.Any() ? wszyscyMowcy.Max(m => m.Czas) : 1;
            var suma = wszyscyMowcy.Sum(m => m.Czas);

            var kolory = new[] { "#FFD700", "#C0C0C0", "#CD7F32", "#1976D2", "#1976D2" };

            var lista = wszyscyMowcy.Select((m, i) => new TopMowca
            {
                Pozycja = (i + 1).ToString(),
                Nazwa = m.Nazwa,
                CzasSekundy = m.Czas,
                CzasDisplay = FormatujCzas(m.Czas),
                Procent = suma > 0 ? (double)m.Czas / suma * 100 : 0,
                ProcentDisplay = suma > 0 ? $"{(double)m.Czas / suma * 100:F1}%" : "0%",
                KolorPozycji = new SolidColorBrush((Color)ColorConverter.ConvertFromString(i < kolory.Length ? kolory[i] : "#666")),
                KolorPaska = new SolidColorBrush((Color)ColorConverter.ConvertFromString(i < 3 ? kolory[i] : "#1976D2"))
            }).ToList();

            ListaTopMowcow.ItemsSource = lista;
        }

        private void WyswietlSentyment()
        {
            int pozytywne = 0, neutralne = 0, negatywne = 0;

            var listaSentyment = new List<SpotkanieSentymentItem>();

            foreach (var s in _spotkania.Take(10))
            {
                var total = s.SentymentPozytywny + s.SentymentNegatywny;
                string sentyment;
                string kolor;

                if (total == 0)
                {
                    sentyment = "Neutralne";
                    kolor = "#FF9800";
                    neutralne++;
                }
                else
                {
                    var score = (double)s.SentymentPozytywny / total;
                    if (score >= 0.6)
                    {
                        sentyment = "Pozytywne";
                        kolor = "#4CAF50";
                        pozytywne++;
                    }
                    else if (score >= 0.4)
                    {
                        sentyment = "Neutralne";
                        kolor = "#FF9800";
                        neutralne++;
                    }
                    else
                    {
                        sentyment = "Negatywne";
                        kolor = "#F44336";
                        negatywne++;
                    }
                }

                listaSentyment.Add(new SpotkanieSentymentItem
                {
                    Tytul = s.Tytul.Length > 40 ? s.Tytul.Substring(0, 40) + "..." : s.Tytul,
                    SentymentDisplay = sentyment,
                    SentymentKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor))
                });
            }

            TxtPozytywne.Text = pozytywne.ToString();
            TxtNeutralne.Text = neutralne.ToString();
            TxtNegatywne.Text = negatywne.ToString();

            ListaSentymentSpotkania.ItemsSource = listaSentyment;
        }

        private void WyswietlTony()
        {
            var entuzjastyczny = _spotkania.Sum(s => s.TonEntuzjastyczny);
            var profesjonalny = _spotkania.Sum(s => s.TonProfesjonalny);
            var zestresowany = _spotkania.Sum(s => s.TonZestresowany);
            var neutralny = _spotkania.Sum(s => s.TonNeutralny);

            var total = entuzjastyczny + profesjonalny + zestresowany + neutralny;
            if (total == 0) total = 1;

            var tony = new List<TonItem>
            {
                new TonItem { Nazwa = "Entuzjastyczny", Procent = (double)entuzjastyczny / total * 100,
                    Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")) },
                new TonItem { Nazwa = "Profesjonalny", Procent = (double)profesjonalny / total * 100,
                    Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2")) },
                new TonItem { Nazwa = "Zestresowany", Procent = (double)zestresowany / total * 100,
                    Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")) },
                new TonItem { Nazwa = "Neutralny", Procent = (double)neutralny / total * 100,
                    Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E")) }
            };

            foreach (var t in tony)
                t.ProcentDisplay = $"{t.Procent:F1}%";

            ListaTonow.ItemsSource = tony;

            // Dominujacy ton
            var dominujacy = tony.OrderByDescending(t => t.Procent).First();
            TxtDominujacyTon.Text = dominujacy.Nazwa;
        }

        private void WyswietlListeSpotkań()
        {
            var lista = _spotkania.Select(s =>
            {
                var total = s.SentymentPozytywny + s.SentymentNegatywny;
                string sentyment = "Neutralny";
                if (total > 0)
                {
                    var score = (double)s.SentymentPozytywny / total;
                    sentyment = score >= 0.6 ? "Pozytywny" : score >= 0.4 ? "Neutralny" : "Negatywny";
                }

                var maxTon = new[] {
                    ("Entuzjastyczny", s.TonEntuzjastyczny),
                    ("Profesjonalny", s.TonProfesjonalny),
                    ("Zestresowany", s.TonZestresowany),
                    ("Neutralny", s.TonNeutralny)
                }.OrderByDescending(x => x.Item2).First().Item1;

                return new SzczegolySpotkania
                {
                    DataDisplay = s.DataSpotkania.ToString("dd.MM.yyyy"),
                    Tytul = s.Tytul,
                    CzasDisplay = FormatujCzas(s.CzasTrwaniaSekundy),
                    LiczbaUczestnikow = s.Uczestnicy.Count,
                    SentymentDisplay = sentyment,
                    TonDisplay = maxTon
                };
            }).ToList();

            GridSpotkania.ItemsSource = lista;
        }

        private string FormatujCzas(int sekundy)
        {
            var ts = TimeSpan.FromSeconds(sekundy);
            if (ts.Hours > 0)
                return $"{ts.Hours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }

        private async void BtnGenerujRaport_Click(object sender, RoutedEventArgs e)
        {
            if (_spotkania.Count == 0)
            {
                MessageBox.Show("Brak danych do wygenerowania raportu.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Plik tekstowy (*.txt)|*.txt|Plik CSV (*.csv)|*.csv",
                FileName = $"Raport_Spotkania_{DateTime.Now:yyyy-MM-dd}"
            };

            if (dialog.ShowDialog() == true)
            {
                ShowLoading("Generowanie raportu...");

                try
                {
                    await GenerujRaport(dialog.FileName);
                    MessageBox.Show($"Raport zostal zapisany:\n{dialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad generowania raportu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    HideLoading();
                }
            }
        }

        private async Task GenerujRaport(string sciezka)
        {
            var sb = new StringBuilder();

            sb.AppendLine("================================================================================");
            sb.AppendLine("                    RAPORT ANALITYCZNY SPOTKAN");
            sb.AppendLine($"                    {DpOdDaty.SelectedDate:dd.MM.yyyy} - {DpDoDaty.SelectedDate:dd.MM.yyyy}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            // Statystyki ogolne
            sb.AppendLine("STATYSTYKI OGOLNE");
            sb.AppendLine("-----------------");
            sb.AppendLine($"Liczba spotkan:        {_spotkania.Count}");
            var lacznyCzas = _spotkania.Sum(s => s.CzasTrwaniaSekundy);
            sb.AppendLine($"Laczny czas:           {FormatujCzas(lacznyCzas)}");
            sb.AppendLine($"Sredni czas spotkania: {FormatujCzas(lacznyCzas / Math.Max(1, _spotkania.Count))}");
            sb.AppendLine($"Unikalnych uczestnikow: {_spotkania.SelectMany(s => s.Uczestnicy).Distinct().Count()}");
            sb.AppendLine();

            // Top mowcy
            sb.AppendLine("TOP 10 MOWCOW (wg czasu)");
            sb.AppendLine("------------------------");
            var topMowcy = _spotkania
                .SelectMany(s => s.CzasMowcow)
                .GroupBy(kv => kv.Key)
                .Select(g => new { Nazwa = g.Key, Czas = g.Sum(x => x.Value) })
                .OrderByDescending(x => x.Czas)
                .Take(10);

            int pozycja = 1;
            foreach (var m in topMowcy)
            {
                sb.AppendLine($"  {pozycja}. {m.Nazwa,-30} {FormatujCzas(m.Czas)}");
                pozycja++;
            }
            sb.AppendLine();

            // Sentyment
            sb.AppendLine("ANALIZA SENTYMENTU");
            sb.AppendLine("------------------");
            var pozytywne = _spotkania.Count(s => s.SentymentPozytywny > s.SentymentNegatywny);
            var negatywne = _spotkania.Count(s => s.SentymentNegatywny > s.SentymentPozytywny);
            var neutralne = _spotkania.Count - pozytywne - negatywne;
            sb.AppendLine($"Spotkania pozytywne:  {pozytywne}");
            sb.AppendLine($"Spotkania neutralne:  {neutralne}");
            sb.AppendLine($"Spotkania negatywne:  {negatywne}");
            sb.AppendLine();

            // Lista spotkan
            sb.AppendLine("LISTA SPOTKAN");
            sb.AppendLine("-------------");
            foreach (var s in _spotkania)
            {
                sb.AppendLine($"  {s.DataSpotkania:dd.MM.yyyy} | {s.Tytul}");
                sb.AppendLine($"             Czas: {FormatujCzas(s.CzasTrwaniaSekundy)}, Uczestnicy: {s.Uczestnicy.Count}");
            }
            sb.AppendLine();

            sb.AppendLine("================================================================================");
            sb.AppendLine($"Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("================================================================================");

            await File.WriteAllTextAsync(sciezka, sb.ToString(), Encoding.UTF8);
        }

        private async void BtnEksportCSV_Click(object sender, RoutedEventArgs e)
        {
            if (_spotkania.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Plik CSV (*.csv)|*.csv",
                FileName = $"Spotkania_{DateTime.Now:yyyy-MM-dd}"
            };

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Data;Tytul;Czas (min);Uczestnicy;Sentyment;Dominujacy ton");

                foreach (var s in _spotkania)
                {
                    var total = s.SentymentPozytywny + s.SentymentNegatywny;
                    var sentyment = total == 0 ? "Neutralny" :
                        (double)s.SentymentPozytywny / total >= 0.6 ? "Pozytywny" :
                        (double)s.SentymentPozytywny / total >= 0.4 ? "Neutralny" : "Negatywny";

                    var maxTon = new[] {
                        ("Entuzjastyczny", s.TonEntuzjastyczny),
                        ("Profesjonalny", s.TonProfesjonalny),
                        ("Zestresowany", s.TonZestresowany),
                        ("Neutralny", s.TonNeutralny)
                    }.OrderByDescending(x => x.Item2).First().Item1;

                    sb.AppendLine($"{s.DataSpotkania:yyyy-MM-dd};\"{s.Tytul}\";{s.CzasTrwaniaSekundy / 60};{s.Uczestnicy.Count};{sentyment};{maxTon}");
                }

                await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Dane wyeksportowane do:\n{dialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowLoading(string message)
        {
            TxtLoading.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        #region Helper Classes

        private class SpotkanieStat
        {
            public long TranskrypcjaID { get; set; }
            public string FirefliesID { get; set; } = "";
            public string Tytul { get; set; } = "";
            public DateTime DataSpotkania { get; set; }
            public int CzasTrwaniaSekundy { get; set; }
            public List<string> Uczestnicy { get; set; } = new();
            public string? Transkrypcja { get; set; }
            public string? Podsumowanie { get; set; }

            public Dictionary<string, int> CzasMowcow { get; set; } = new();
            public int SentymentPozytywny { get; set; }
            public int SentymentNegatywny { get; set; }
            public int TonEntuzjastyczny { get; set; }
            public int TonProfesjonalny { get; set; }
            public int TonZestresowany { get; set; }
            public int TonNeutralny { get; set; }
        }

        private class WykresItem
        {
            public string Okres { get; set; } = "";
            public string Wartosc { get; set; } = "";
            public double SzerokoscPaska { get; set; }
        }

        private class TopMowca
        {
            public string Pozycja { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public int CzasSekundy { get; set; }
            public string CzasDisplay { get; set; } = "";
            public double Procent { get; set; }
            public string ProcentDisplay { get; set; } = "";
            public SolidColorBrush KolorPozycji { get; set; } = Brushes.Gray;
            public SolidColorBrush KolorPaska { get; set; } = Brushes.Blue;
        }

        private class SpotkanieSentymentItem
        {
            public string Tytul { get; set; } = "";
            public string SentymentDisplay { get; set; } = "";
            public SolidColorBrush SentymentKolor { get; set; } = Brushes.Gray;
        }

        private class TonItem
        {
            public string Nazwa { get; set; } = "";
            public double Procent { get; set; }
            public string ProcentDisplay { get; set; } = "";
            public SolidColorBrush Kolor { get; set; } = Brushes.Gray;
        }

        private class SzczegolySpotkania
        {
            public string DataDisplay { get; set; } = "";
            public string Tytul { get; set; } = "";
            public string CzasDisplay { get; set; } = "";
            public int LiczbaUczestnikow { get; set; }
            public string SentymentDisplay { get; set; } = "";
            public string TonDisplay { get; set; } = "";
        }

        #endregion
    }
}
