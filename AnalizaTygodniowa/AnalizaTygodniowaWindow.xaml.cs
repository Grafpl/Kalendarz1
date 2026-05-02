using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kalendarz1.AnalizaTygodniowa.Models;
using Kalendarz1.AnalizaTygodniowa.Services;
using Kalendarz1.PrognozyUboju;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Microsoft.Win32;

namespace Kalendarz1.AnalizaTygodniowa
{
    public partial class AnalizaTygodniowaWindow : Window, INotifyPropertyChanged
    {
        private const string CONN = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private readonly AnalizaTygodniowaService _service = new(CONN);

        private List<int> _wybraniOdbiorcy = new();
        private List<string> _wybraniHandlowcy = new();
        private Dictionary<int, string> _odbiorcyNazwy = new();

        private List<SuroweDaneSQl> _suroweDane = new();
        private List<SuroweDaneSQl> _suroweDaneYoY = new();           // dane sprzed roku
        private Dictionary<DayOfWeek, decimal> _prognoza = new();
        private List<PodsumowanieDniaModel> _pelneDane = new();
        private List<PodsumowanieDniaModel> _filtrowaneDane;          // null = pokaż wszystko

        private bool _isLoading;
        private bool _isFullscreen;
        private WindowState _prevState;
        private WindowStyle _prevStyle;

        private readonly DispatcherTimer _autoRefreshTimer = new();
        private int _topN = 10;

        // Chart binding
        private List<string> _labelsBilans = new();
        public List<string> LabelsBilans
        {
            get => _labelsBilans;
            set { _labelsBilans = value; OnPropertyChanged(); }
        }
        public Func<double, string> KgFormatter { get; set; } = v => v.ToString("N0") + " kg";

        // Heatmap binding
        private List<string> _heatLabelsX = new();
        private List<string> _heatLabelsY = new();
        public List<string> HeatLabelsX { get => _heatLabelsX; set { _heatLabelsX = value; OnPropertyChanged(); } }
        public List<string> HeatLabelsY { get => _heatLabelsY; set { _heatLabelsY = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public AnalizaTygodniowaWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DataContext = this;

            ApplyPresetPoprzTydz();

            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;

            Loaded += (_, _) => _ = LoadTowaryAsync();
        }

        // ═════════════════════════════════════════════
        //  Filters / commands
        // ═════════════════════════════════════════════

        private async Task LoadTowaryAsync()
        {
            try
            {
                cmbTowar.ItemsSource = await _service.LoadTowaryAsync();
                cmbTowar.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania towarów:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private AnalizaTygodniowaFilter BuildFilter() => new()
        {
            DataOd = dpDataOd.SelectedDate ?? DateTime.Today,
            DataDo = dpDataDo.SelectedDate ?? DateTime.Today,
            TowarId = (cmbTowar.SelectedValue is int id && id > 0) ? id : (int?)null,
            Handlowcy = new List<string>(_wybraniHandlowcy),
            OdbiorcyIds = new List<int>(_wybraniOdbiorcy),
            UkryjKorekty = chkUkryjKorekty.IsChecked == true
        };

        private async void BtnAnalizuj_Click(object sender, RoutedEventArgs e) => await AnalizujAsync();

        private async Task AnalizujAsync()
        {
            if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz prawidłowy zakres dat.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "Pobieram surowe dane");
            btnAnalizuj.IsEnabled = false;
            txtStatus.Text = "Analizowanie…";

            try
            {
                var filter = BuildFilter();

                var prognozaTask = _service.LoadPrognozaAsync(filter.DataOd, filter.TowarId);
                var daneTask = _service.LoadAnalitykaAsync(filter);

                Task<List<SuroweDaneSQl>> daneYoYTask = null;
                if (chkPorownajYoY.IsChecked == true)
                {
                    var yoYFilter = new AnalizaTygodniowaFilter
                    {
                        DataOd = filter.DataOd.AddYears(-1),
                        DataDo = filter.DataDo.AddYears(-1),
                        TowarId = filter.TowarId,
                        Handlowcy = filter.Handlowcy,
                        OdbiorcyIds = filter.OdbiorcyIds,
                        UkryjKorekty = filter.UkryjKorekty
                    };
                    daneYoYTask = _service.LoadAnalitykaAsync(yoYFilter);
                }

                if (daneYoYTask != null)
                    await Task.WhenAll(prognozaTask, daneTask, daneYoYTask);
                else
                    await Task.WhenAll(prognozaTask, daneTask);

                _prognoza = prognozaTask.Result;
                _suroweDane = daneTask.Result;
                _suroweDaneYoY = daneYoYTask?.Result ?? new List<SuroweDaneSQl>();

                _filtrowaneDane = null;
                Przelicz();

                txtStatus.Text = $"Załadowano {_pelneDane.Count} dni · {_suroweDane.Count} pozycji"
                                 + (chkPorownajYoY.IsChecked == true ? $" · YoY: {_suroweDaneYoY.Count}" : "");
                txtLastRefresh.Text = $"Aktualizacja: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd podczas analizy:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Błąd analizy";
            }
            finally
            {
                btnAnalizuj.IsEnabled = true;
                SetBusy(false);
            }
        }

        private void Przelicz()
        {
            bool ukryjKorekty = chkUkryjKorekty.IsChecked == true;

            var grupy = _suroweDane.GroupBy(d => d.Data.Date).OrderBy(g => g.Key);
            var dni = new List<PodsumowanieDniaModel>();

            foreach (var grupa in grupy)
            {
                var dzien = new PodsumowanieDniaModel { Data = grupa.Key };

                foreach (var w in grupa)
                {
                    if (w.TypOperacji == "PRODUKCJA")
                    {
                        dzien.IloscWyprodukowana += w.Ilosc;
                        dzien.SzczegolyProdukcji.Add(new SzczegolProdukcjiModel
                        {
                            NumerDokumentu = w.NumerDokumentu,
                            KodTowaru = w.KodTowaru,
                            NazwaTowaru = w.NazwaTowaru,
                            Ilosc = w.Ilosc
                        });
                    }
                    else
                    {
                        dzien.SzczegolySprzedazy.Add(new SzczegolSprzedazyModel
                        {
                            NazwaKontrahenta = w.NazwaKontrahenta,
                            Handlowiec = w.Handlowiec,
                            KodTowaru = w.KodTowaru,
                            NazwaTowaru = w.NazwaTowaru,
                            Ilosc = w.Ilosc,
                            Cena = w.Cena,
                            NumerDokumentu = w.NumerDokumentu
                        });

                        if (ukryjKorekty && w.Ilosc < 0) continue;
                        dzien.IloscSprzedana += w.Ilosc;
                        dzien.WartoscSprzedazy += w.Ilosc * w.Cena;
                    }
                }

                dzien.PrognozaSprzedazy = _prognoza.TryGetValue(grupa.Key.DayOfWeek, out var p) ? p : 0;
                dni.Add(dzien);
            }

            _pelneDane = dni;
            WyznaczAnomalie(_pelneDane);

            _filtrowaneDane = null;
            ZastosujLokalnyFiltr();

            AktualizujKPI();
            AktualizujWykres();
            AktualizujRankingi();
            AktualizujHeatmape();
            OdswiezSzczegolySprzedazy();
        }

        private void ZastosujLokalnyFiltr()
        {
            dgPodsumowanieDni.ItemsSource = _filtrowaneDane ?? _pelneDane;
        }

        // 2σ — proste wyznaczenie anomalii statystycznych
        private static void WyznaczAnomalie(List<PodsumowanieDniaModel> dni)
        {
            if (dni.Count < 3) return;
            decimal[] wartosci = dni.Select(d => d.Wariancja).ToArray();
            decimal mean = wartosci.Average();
            double sumSq = wartosci.Sum(v => Math.Pow((double)(v - mean), 2));
            decimal sigma = (decimal)Math.Sqrt(sumSq / wartosci.Length);
            if (sigma == 0) return;
            decimal prog = sigma * 2;
            foreach (var d in dni)
                d.Anomalia = Math.Abs(d.Wariancja - mean) > prog;
        }

        private void AktualizujKPI()
        {
            if (_pelneDane.Count == 0)
            {
                kpiProdukcja.Text = "0 kg";
                kpiSprzedaz.Text = "0 kg";
                kpiProcent.Text = "0%";
                kpiWariancja.Text = "0 kg";
                kpiMape.Text = "—";
                kpiProdukcjaInfo.Text = kpiSprzedazInfo.Text = kpiProcentInfo.Text =
                    kpiWariancjaInfo.Text = kpiMapeInfo.Text = "";
                return;
            }

            decimal totalProd = _pelneDane.Sum(d => d.IloscWyprodukowana);
            decimal totalSprz = _pelneDane.Sum(d => d.IloscSprzedana);
            decimal totalWart = _pelneDane.Sum(d => d.WartoscSprzedazy);
            decimal wariancja = totalProd - totalSprz;
            decimal procent = totalProd > 0 ? totalSprz / totalProd * 100 : 0;
            int anomalii = _pelneDane.Count(d => d.Anomalia);

            kpiProdukcja.Text = $"{totalProd:N0} kg";
            kpiProdukcjaInfo.Text = $"{_pelneDane.Count} dni";

            kpiSprzedaz.Text = $"{totalSprz:N0} kg";
            kpiSprzedazInfo.Text = totalWart > 0 ? $"≈ {totalWart:N0} zł" : "";

            kpiProcent.Text = $"{procent:N1}%";
            kpiProcentInfo.Text = procent >= 100 ? "wyprzedaż" : (procent >= 80 ? "dobra rotacja" : "niska rotacja");

            kpiWariancja.Text = $"{wariancja:N0} kg";
            kpiWariancjaInfo.Text = wariancja > 0
                ? $"↑ nadwyżka · {anomalii} anomalii"
                : (wariancja < 0 ? $"↓ niedobór · {anomalii} anomalii" : "balans");

            // MAPE — średni z dni gdzie jest prognoza > 0
            var mapeDays = _pelneDane.Where(d => d.PrognozaSprzedazy > 0).ToList();
            if (mapeDays.Count == 0)
            {
                kpiMape.Text = "—";
                kpiMapeInfo.Text = "brak prognozy historycznej";
            }
            else
            {
                decimal avgMape = mapeDays.Average(d => d.Mape);
                kpiMape.Text = $"{100 - Math.Min(100, avgMape):N0}%";
                kpiMapeInfo.Text = avgMape switch
                {
                    < 10 => "doskonała",
                    < 25 => "dobra",
                    < 50 => "umiarkowana",
                    _ => "słaba — zaktualizuj prognozę"
                };
            }
        }

        private void AktualizujWykres()
        {
            chartBilans.Series.Clear();

            if (_pelneDane.Count == 0)
            {
                LabelsBilans = new List<string>();
                chartEmpty.Visibility = Visibility.Visible;
                chartHint.Text = "";
                return;
            }

            chartEmpty.Visibility = Visibility.Collapsed;
            chartHint.Text = $"({_pelneDane.Count} dni)" + (chkPorownajYoY.IsChecked == true ? " · YoY włączony" : "");

            LabelsBilans = _pelneDane.Select(d => d.Data.ToString("MM-dd ddd",
                new CultureInfo("pl-PL"))).ToList();

            chartBilans.Series.Add(new ColumnSeries
            {
                Title = "Produkcja",
                Values = new ChartValues<double>(_pelneDane.Select(d => (double)d.IloscWyprodukowana)),
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x25, 0x63, 0xEB)),
                MaxColumnWidth = 40,
                ColumnPadding = 4
            });
            chartBilans.Series.Add(new ColumnSeries
            {
                Title = "Sprzedaż",
                Values = new ChartValues<double>(_pelneDane.Select(d => (double)d.IloscSprzedana)),
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A)),
                MaxColumnWidth = 40,
                ColumnPadding = 4
            });

            if (_prognoza.Values.Any(v => v > 0))
            {
                chartBilans.Series.Add(new LineSeries
                {
                    Title = "Prognoza",
                    Values = new ChartValues<double>(_pelneDane.Select(d => (double)d.PrognozaSprzedazy)),
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06)),
                    Fill = System.Windows.Media.Brushes.Transparent,
                    StrokeThickness = 2,
                    PointGeometrySize = 6,
                    LineSmoothness = 0.3
                });
            }

            // YoY: sprzedaż rok temu — ułóż wg dnia (offset -1 rok)
            if (chkPorownajYoY.IsChecked == true && _suroweDaneYoY.Count > 0)
            {
                var yoYDict = _suroweDaneYoY
                    .Where(d => d.TypOperacji == "SPRZEDAZ")
                    .GroupBy(d => d.Data.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Ilosc));

                var yoYValues = _pelneDane
                    .Select(d => yoYDict.TryGetValue(d.Data.AddYears(-1), out var v) ? (double)v : 0d)
                    .ToList();

                chartBilans.Series.Add(new LineSeries
                {
                    Title = "Sprzedaż rok temu",
                    Values = new ChartValues<double>(yoYValues),
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED)),
                    Fill = System.Windows.Media.Brushes.Transparent,
                    StrokeThickness = 2,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 3 },
                    PointGeometrySize = 5,
                    LineSmoothness = 0.3
                });
            }
        }

        private void AktualizujRankingi()
        {
            decimal totalKg = Math.Max(1m, _pelneDane.Sum(d => d.IloscSprzedana));

            // Top odbiorcy
            var odb = _pelneDane.SelectMany(d => d.SzczegolySprzedazy)
                .Where(s => s.Ilosc > 0)
                .GroupBy(s => s.NazwaKontrahenta ?? "—")
                .Select(g => new RankingItem
                {
                    Klucz = g.Key,
                    Nazwa = g.Key,
                    Ilosc = g.Sum(x => x.Ilosc),
                    Wartosc = g.Sum(x => x.Wartosc),
                    ProcentOgolu = g.Sum(x => x.Ilosc) / totalKg * 100
                })
                .OrderByDescending(x => x.Ilosc)
                .Take(_topN)
                .Select((x, i) => { x.Pozycja = i + 1; return x; })
                .ToList();
            dgTopOdbiorcy.ItemsSource = odb;

            // Top handlowcy
            var hdl = _pelneDane.SelectMany(d => d.SzczegolySprzedazy)
                .Where(s => s.Ilosc > 0)
                .GroupBy(s => s.Handlowiec ?? "—")
                .Select(g => new RankingItem
                {
                    Klucz = g.Key,
                    Nazwa = g.Key,
                    Ilosc = g.Sum(x => x.Ilosc),
                    Wartosc = g.Sum(x => x.Wartosc),
                    ProcentOgolu = g.Sum(x => x.Ilosc) / totalKg * 100
                })
                .OrderByDescending(x => x.Ilosc)
                .Take(_topN)
                .Select((x, i) => { x.Pozycja = i + 1; return x; })
                .ToList();
            dgTopHandlowcy.ItemsSource = hdl;

            // Top produkty
            var prd = _pelneDane.SelectMany(d => d.SzczegolySprzedazy)
                .Where(s => s.Ilosc > 0)
                .GroupBy(s => new { s.KodTowaru, s.NazwaTowaru })
                .Select(g => new RankingItem
                {
                    Klucz = g.Key.KodTowaru ?? "—",
                    Nazwa = g.Key.NazwaTowaru ?? "—",
                    Ilosc = g.Sum(x => x.Ilosc),
                    Wartosc = g.Sum(x => x.Wartosc),
                    ProcentOgolu = g.Sum(x => x.Ilosc) / totalKg * 100
                })
                .OrderByDescending(x => x.Ilosc)
                .Take(_topN)
                .Select((x, i) => { x.Pozycja = i + 1; return x; })
                .ToList();
            dgTopProdukty.ItemsSource = prd;
        }

        private void AktualizujHeatmape()
        {
            chartHeatmapa.Series.Clear();

            if (_pelneDane.Count == 0)
            {
                HeatLabelsX = new List<string>();
                HeatLabelsY = new List<string>();
                return;
            }

            var dni = _pelneDane.Select(d => d.Data.Date).Distinct().OrderBy(d => d).ToList();

            // Top 15 produktów po sprzedaży, żeby heatmapa była czytelna
            var produkty = _pelneDane.SelectMany(d => d.SzczegolySprzedazy)
                .Where(s => s.Ilosc > 0)
                .GroupBy(s => new { s.KodTowaru, s.NazwaTowaru })
                .Select(g => new { g.Key.KodTowaru, g.Key.NazwaTowaru, Suma = g.Sum(x => x.Ilosc) })
                .OrderByDescending(x => x.Suma)
                .Take(15)
                .ToList();

            HeatLabelsX = dni.Select(d => d.ToString("MM-dd ddd", new CultureInfo("pl-PL"))).ToList();
            HeatLabelsY = produkty.Select(p => string.IsNullOrWhiteSpace(p.KodTowaru) ? p.NazwaTowaru : p.KodTowaru).ToList();

            var values = new ChartValues<HeatPoint>();
            for (int yi = 0; yi < produkty.Count; yi++)
            {
                var p = produkty[yi];
                for (int xi = 0; xi < dni.Count; xi++)
                {
                    var data = dni[xi];
                    var dzien = _pelneDane.FirstOrDefault(d => d.Data.Date == data);
                    decimal ilosc = dzien?.SzczegolySprzedazy
                        .Where(s => s.KodTowaru == p.KodTowaru && s.NazwaTowaru == p.NazwaTowaru && s.Ilosc > 0)
                        .Sum(s => s.Ilosc) ?? 0;
                    values.Add(new HeatPoint(xi, yi, (double)ilosc));
                }
            }

            chartHeatmapa.Series.Add(new HeatSeries
            {
                Values = values,
                DataLabels = false,
                GradientStopCollection = new System.Windows.Media.GradientStopCollection
                {
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0xF8, 0xFA, 0xFC), 0),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0xBB, 0xF7, 0xD0), 0.4),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A), 1)
                }
            });
        }

        // ═════════════════════════════════════════════
        //  Master-detail
        // ═════════════════════════════════════════════

        private void DgPodsumowanieDni_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgPodsumowanieDni.SelectedItem is PodsumowanieDniaModel dzien)
            {
                dgSzczegolyProdukcji.ItemsSource = dzien.SzczegolyProdukcji.OrderByDescending(p => p.Ilosc).ToList();
                hdrSzczegolyProdukcji.Text = $"Szczegóły produkcji ({dzien.SzczegolyProdukcji.Count} • {dzien.IloscWyprodukowana:N0} kg)";
                OdswiezSzczegolySprzedazy();

                dgSzczegolySprzedazy.MouseDoubleClick -= DgSzczegolySprzedazy_MouseDoubleClick;
                dgSzczegolySprzedazy.MouseDoubleClick += DgSzczegolySprzedazy_MouseDoubleClick;
            }
            else
            {
                dgSzczegolyProdukcji.ItemsSource = null;
                dgSzczegolySprzedazy.ItemsSource = null;
                hdrSzczegolySprzedazy.Text = "Szczegóły sprzedaży";
                hdrSzczegolyProdukcji.Text = "Szczegóły produkcji";
            }
        }

        private void OdswiezSzczegolySprzedazy()
        {
            if (dgPodsumowanieDni.SelectedItem is not PodsumowanieDniaModel dzien)
            {
                dgSzczegolySprzedazy.ItemsSource = null;
                hdrSzczegolySprzedazy.Text = "Szczegóły sprzedaży";
                return;
            }

            var szczegoly = dzien.SzczegolySprzedazy.AsEnumerable();
            if (chkUkryjKorekty.IsChecked == true)
                szczegoly = szczegoly.Where(s => s.Ilosc > 0);

            var lista = szczegoly.OrderByDescending(s => s.Ilosc).ToList();
            dgSzczegolySprzedazy.ItemsSource = lista;

            decimal suma = lista.Sum(s => s.Ilosc);
            decimal wartosc = lista.Sum(s => s.Wartosc);
            hdrSzczegolySprzedazy.Text = wartosc > 0
                ? $"Szczegóły sprzedaży ({lista.Count} • {suma:N0} kg • {wartosc:N0} zł)"
                : $"Szczegóły sprzedaży ({lista.Count} • {suma:N0} kg)";
        }

        private async void DgSzczegolySprzedazy_MouseDoubleClick(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            await OtworzWybranyDokumentAsync();
        }

        private async Task OtworzWybranyDokumentAsync()
        {
            if (dgSzczegolySprzedazy.SelectedItem is not SzczegolSprzedazyModel s) return;
            try
            {
                int? id = await _service.GetIdDokumentuAsync(s.NumerDokumentu);
                if (id.HasValue)
                {
                    new SzczegolyDokumentuWindow(CONN, id.Value, s.NumerDokumentu).ShowDialog();
                }
                else
                {
                    MessageBox.Show($"Nie znaleziono dokumentu: {s.NumerDokumentu}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania dokumentu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════
        //  Drill-down z wykresu
        // ═════════════════════════════════════════════

        private void ChartBilans_DataClick(object sender, LiveCharts.ChartPoint chartPoint)
        {
            int idx = (int)chartPoint.X;
            if (idx < 0 || idx >= _pelneDane.Count) return;
            var dzien = _pelneDane[idx];

            // Zaznacz odpowiedni wiersz w głównej tabeli
            dgPodsumowanieDni.SelectedItem = dzien;
            dgPodsumowanieDni.ScrollIntoView(dzien);

            // Przejdź na zakładkę Szczegóły sprzedaży
            if (tabSzczegoly.Items.Count > 0) tabSzczegoly.SelectedIndex = 0;
        }

        // ═════════════════════════════════════════════
        //  KPI cards click — drill
        // ═════════════════════════════════════════════

        private void KpiCardProdukcja_Click(object sender, MouseButtonEventArgs e)
        {
            if (tabSzczegoly.Items.Count > 1) tabSzczegoly.SelectedIndex = 1;
        }

        private void KpiCardSprzedaz_Click(object sender, MouseButtonEventArgs e)
        {
            if (tabSzczegoly.Items.Count > 0) tabSzczegoly.SelectedIndex = 0;
        }

        private void KpiCardRyzyko_Click(object sender, MouseButtonEventArgs e)
        {
            if (_pelneDane.Count == 0) return;
            _filtrowaneDane = _pelneDane.Where(d => d.Wariancja > 0).ToList();
            ZastosujLokalnyFiltr();
            txtStatus.Text = $"🎯 Filtr: dni z ryzykiem ({_filtrowaneDane.Count}) — kliknij wiersz → szczegóły";
        }

        // ═════════════════════════════════════════════
        //  Date presets
        // ═════════════════════════════════════════════

        private void BtnPresetWczoraj_Click(object sender, RoutedEventArgs e)
        {
            var d = DateTime.Today.AddDays(-1);
            dpDataOd.SelectedDate = d;
            dpDataDo.SelectedDate = d;
        }

        private void BtnPresetTenTydz_Click(object sender, RoutedEventArgs e)
        {
            int offset = ((int)DateTime.Today.DayOfWeek + 6) % 7;
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-offset);
            dpDataDo.SelectedDate = DateTime.Today;
        }

        private void BtnPresetPoprzTydz_Click(object sender, RoutedEventArgs e) => ApplyPresetPoprzTydz();

        private void ApplyPresetPoprzTydz()
        {
            int offset = ((int)DateTime.Today.DayOfWeek + 6) % 7;
            var poniedzialekTen = DateTime.Today.AddDays(-offset);
            dpDataOd.SelectedDate = poniedzialekTen.AddDays(-7);
            dpDataDo.SelectedDate = poniedzialekTen.AddDays(-1);
        }

        private void BtnPreset7Dni_Click(object sender, RoutedEventArgs e)
        {
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-6);
            dpDataDo.SelectedDate = DateTime.Today;
        }

        private void BtnPreset30Dni_Click(object sender, RoutedEventArgs e)
        {
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-29);
            dpDataDo.SelectedDate = DateTime.Today;
        }

        private void BtnPresetMiesiac_Click(object sender, RoutedEventArgs e)
        {
            dpDataOd.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpDataDo.SelectedDate = DateTime.Today;
        }

        // ═════════════════════════════════════════════
        //  Multi-select dialogs + badges
        // ═════════════════════════════════════════════

        private void BtnWybierzHandlowcow_Click(object sender, RoutedEventArgs e)
        {
            var form = new FormWyborHandlowcow(CONN, _wybraniHandlowcy);
            if (form.ShowDialog() == true)
            {
                _wybraniHandlowcy = form.WybraniHandlowcy ?? new List<string>();
                btnWybierzHandlowcow.Content = _wybraniHandlowcy.Count > 0
                    ? $"{_wybraniHandlowcy.Count} wybr."
                    : "Wybierz…";
                txtHandlowcyBadge.Text = _wybraniHandlowcy.Count > 0
                    ? string.Join(", ", _wybraniHandlowcy.Take(3)) + (_wybraniHandlowcy.Count > 3 ? $" (+{_wybraniHandlowcy.Count - 3})" : "")
                    : "";
                txtHandlowcyBadge.ToolTip = string.Join(", ", _wybraniHandlowcy);
            }
        }

        private void BtnWybierzOdbiorcow_Click(object sender, RoutedEventArgs e)
        {
            var form = new FormWyborKontrahentow(CONN, _wybraniOdbiorcy);
            if (form.ShowDialog() == true)
            {
                _wybraniOdbiorcy = form.WybraniKontrahenci ?? new List<int>();
                btnWybierzOdbiorcow.Content = _wybraniOdbiorcy.Count > 0
                    ? $"{_wybraniOdbiorcy.Count} wybr."
                    : "Wybierz…";

                txtOdbiorcyBadge.Text = _wybraniOdbiorcy.Count > 0
                    ? $"{_wybraniOdbiorcy.Count} odbiorców"
                    : "";
                txtOdbiorcyBadge.ToolTip = _wybraniOdbiorcy.Count > 0
                    ? "ID: " + string.Join(", ", _wybraniOdbiorcy)
                    : null;
            }
        }

        private void BtnResetFiltry_Click(object sender, RoutedEventArgs e)
        {
            _wybraniHandlowcy.Clear();
            _wybraniOdbiorcy.Clear();
            btnWybierzHandlowcow.Content = "Wybierz…";
            btnWybierzOdbiorcow.Content = "Wybierz…";
            txtHandlowcyBadge.Text = "";
            txtOdbiorcyBadge.Text = "";
            cmbTowar.SelectedIndex = 0;
            chkUkryjKorekty.IsChecked = false;
            _filtrowaneDane = null;
            ZastosujLokalnyFiltr();
        }

        // ═════════════════════════════════════════════
        //  Korekty toggle (LOCAL, no DB re-query)
        // ═════════════════════════════════════════════

        private void ChkUkryjKorekty_Click(object sender, RoutedEventArgs e)
        {
            if (_suroweDane.Count == 0) return;
            Przelicz();
        }

        // ═════════════════════════════════════════════
        //  Auto-refresh
        // ═════════════════════════════════════════════

        private void CmbAutoRefresh_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _autoRefreshTimer.Stop();
            if (cmbAutoRefresh.SelectedItem is not ComboBoxItem item) return;
            int? minutes = (item.Content as string) switch
            {
                "5 min" => 5,
                "15 min" => 15,
                "30 min" => 30,
                _ => null
            };
            if (minutes.HasValue)
            {
                _autoRefreshTimer.Interval = TimeSpan.FromMinutes(minutes.Value);
                _autoRefreshTimer.Start();
                txtStatus.Text = $"Auto-refresh co {minutes.Value} min — aktywny";
            }
        }

        private async void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_isLoading) return;
            await AnalizujAsync();
        }

        // ═════════════════════════════════════════════
        //  Top-N
        // ═════════════════════════════════════════════

        private void CmbTopN_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTopN?.SelectedItem is ComboBoxItem item
                && int.TryParse(item.Content?.ToString(), out var n))
            {
                _topN = n;
                if (_pelneDane.Count > 0) AktualizujRankingi();
            }
        }

        // ═════════════════════════════════════════════
        //  Window keyboard shortcuts (F5 / F11 / Esc)
        // ═════════════════════════════════════════════

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F5:
                    await AnalizujAsync();
                    e.Handled = true;
                    break;
                case Key.F11:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                case Key.Escape when _isFullscreen:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
            }
        }

        private void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                WindowStyle = _prevStyle;
                WindowState = _prevState;
                _isFullscreen = false;
            }
            else
            {
                _prevStyle = WindowStyle;
                _prevState = WindowState;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Normal;     // wymagane do prawidłowego pełnego ekranu
                WindowState = WindowState.Maximized;
                _isFullscreen = true;
            }
        }

        // ═════════════════════════════════════════════
        //  Context menu — bilans
        // ═════════════════════════════════════════════

        private void MenuKopiujWiersz_Click(object sender, RoutedEventArgs e)
        {
            if (dgPodsumowanieDni.SelectedItem is not PodsumowanieDniaModel d) return;
            var line = $"{d.Data:yyyy-MM-dd}\t{d.DzienTygodnia}\t{d.IloscWyprodukowana:N0}\t" +
                       $"{d.IloscSprzedana:N0}\t{d.PrognozaSprzedazy:N0}\t{d.Wariancja:N0}\t" +
                       $"{d.Mape:N1}\t{d.ProcentSprzedazy:N1}";
            try { Clipboard.SetText(line); txtStatus.Text = "Skopiowano do schowka"; } catch { }
        }

        private void MenuFiltrujDzien_Click(object sender, RoutedEventArgs e)
        {
            if (dgPodsumowanieDni.SelectedItem is not PodsumowanieDniaModel d) return;
            _filtrowaneDane = _pelneDane.Where(x => x.Data == d.Data).ToList();
            ZastosujLokalnyFiltr();
            txtStatus.Text = $"🔍 Filtr lokalny: {d.Data:yyyy-MM-dd} — kliknij Wyczyść lokalny filtr aby cofnąć";
        }

        private void MenuZakresDzien_Click(object sender, RoutedEventArgs e)
        {
            if (dgPodsumowanieDni.SelectedItem is not PodsumowanieDniaModel d) return;
            dpDataOd.SelectedDate = d.Data;
            dpDataDo.SelectedDate = d.Data;
        }

        private void MenuFiltrAnomalie_Click(object sender, RoutedEventArgs e)
        {
            if (_pelneDane.Count == 0) return;
            _filtrowaneDane = _pelneDane.Where(d => d.Anomalia).ToList();
            ZastosujLokalnyFiltr();
            txtStatus.Text = $"⚡ Filtr lokalny: anomalie ({_filtrowaneDane.Count})";
        }

        private void MenuFiltrRyzyko_Click(object sender, RoutedEventArgs e)
        {
            if (_pelneDane.Count == 0) return;
            _filtrowaneDane = _pelneDane.Where(d => d.Wariancja > 0).ToList();
            ZastosujLokalnyFiltr();
            txtStatus.Text = $"⚠️ Filtr lokalny: ryzyko ({_filtrowaneDane.Count})";
        }

        private void MenuFiltrCzysc_Click(object sender, RoutedEventArgs e)
        {
            _filtrowaneDane = null;
            ZastosujLokalnyFiltr();
            txtStatus.Text = "Filtr lokalny wyczyszczony";
        }

        // ═════════════════════════════════════════════
        //  Context menu — szczegóły sprzedaży
        // ═════════════════════════════════════════════

        private async void MenuOtworzDokument_Click(object sender, RoutedEventArgs e)
        {
            await OtworzWybranyDokumentAsync();
        }

        private void MenuFiltrujOdbiorce_Click(object sender, RoutedEventArgs e)
        {
            if (dgSzczegolySprzedazy.SelectedItem is not SzczegolSprzedazyModel s) return;
            // Użyjemy lokalnego filtra po nazwie (nie ID — nie mamy id w SuroweDaneSQl)
            txtStatus.Text = $"💡 Aby trwale filtrować '{s.NazwaKontrahenta}', wybierz odbiorcę przez przycisk Wybierz… i kliknij Analizuj";
        }

        private void MenuFiltrujHandlowca_Click(object sender, RoutedEventArgs e)
        {
            if (dgSzczegolySprzedazy.SelectedItem is not SzczegolSprzedazyModel s) return;
            if (string.IsNullOrWhiteSpace(s.Handlowiec)) return;
            _wybraniHandlowcy = new List<string> { s.Handlowiec };
            btnWybierzHandlowcow.Content = "1 wybr.";
            txtHandlowcyBadge.Text = s.Handlowiec;
            txtStatus.Text = $"🎯 Wybrano handlowca: {s.Handlowiec} — kliknij Analizuj aby pobrać";
        }

        private void MenuFiltrujTowar_Click(object sender, RoutedEventArgs e)
        {
            if (dgSzczegolySprzedazy.SelectedItem is not SzczegolSprzedazyModel s) return;
            // Wybierz pasujący towar w combo (po kodzie)
            if (cmbTowar.ItemsSource is IEnumerable<TowarComboItem> tow)
            {
                var match = tow.FirstOrDefault(t => t.Kod == s.KodTowaru);
                if (match != null)
                {
                    cmbTowar.SelectedValue = match.Id;
                    txtStatus.Text = $"🎯 Wybrano towar: {match.Kod} — kliknij Analizuj";
                }
            }
        }

        private void MenuKopiujSprzedazRow_Click(object sender, RoutedEventArgs e)
        {
            if (dgSzczegolySprzedazy.SelectedItem is not SzczegolSprzedazyModel s) return;
            var line = $"{s.NumerDokumentu}\t{s.NazwaKontrahenta}\t{s.KodTowaru}\t{s.NazwaTowaru}\t" +
                       $"{s.Handlowiec}\t{s.Ilosc:N2}\t{s.Cena:N2}\t{s.Wartosc:N2}";
            try { Clipboard.SetText(line); txtStatus.Text = "Skopiowano do schowka"; } catch { }
        }

        // ═════════════════════════════════════════════
        //  Header buttons
        // ═════════════════════════════════════════════

        private async void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_pelneDane.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu. Najpierw kliknij Analizuj.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Plik CSV|*.csv",
                FileName = $"DashboardAnalityczny_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dialog.ShowDialog() != true) return;

            var snapshot = _pelneDane.OrderBy(d => d.Data).ToList();
            var sumProd = snapshot.Sum(d => d.IloscWyprodukowana);
            var sumSprz = snapshot.Sum(d => d.IloscSprzedana);
            var sumWart = snapshot.Sum(d => d.WartoscSprzedazy);
            var dataOd = dpDataOd.SelectedDate;
            var dataDo = dpDataDo.SelectedDate;
            var path = dialog.FileName;

            SetBusy(true, "Eksport CSV…");
            try
            {
                await Task.Run(() =>
                {
                    using var w = new StreamWriter(path, false, System.Text.Encoding.UTF8);
                    w.WriteLine("Data;Dzien;Produkcja_kg;Sprzedaz_kg;Prognoza_kg;Wariancja_kg;MAPE_proc;ProcentSprzedazy;Wartosc_zl;Anomalia");
                    foreach (var d in snapshot)
                    {
                        w.WriteLine($"{d.Data:yyyy-MM-dd};{d.DzienTygodnia};" +
                                    $"{d.IloscWyprodukowana:N2};{d.IloscSprzedana:N2};{d.PrognozaSprzedazy:N2};" +
                                    $"{d.Wariancja:N2};{d.Mape:N1};{d.ProcentSprzedazy:N1};{d.WartoscSprzedazy:N2};" +
                                    $"{(d.Anomalia ? "TAK" : "")}");
                    }
                    w.WriteLine();
                    w.WriteLine("=== PODSUMOWANIE ===");
                    w.WriteLine($"Suma produkcja kg;{sumProd:N2}");
                    w.WriteLine($"Suma sprzedaz kg;{sumSprz:N2}");
                    w.WriteLine($"Suma wartosc zl;{sumWart:N2}");
                    w.WriteLine($"Zakres dat;{dataOd:yyyy-MM-dd} - {dataDo:yyyy-MM-dd}");
                });

                MessageBox.Show($"Eksport zakończony.\n\n{path}", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnInstrukcja_Click(object sender, RoutedEventArgs e)
        {
            const string instrukcja = """
                📖 INSTRUKCJA — DASHBOARD ANALITYCZNY

                CEL:
                Analiza bilansu produkcji vs sprzedaży świeżych towarów (katalog 67095).

                JAK UŻYWAĆ:
                1. Wybierz zakres dat (preset: Wczoraj / Ten tydzień / Poprz. tydzień / 7/30 dni / Bieżący mc)
                2. Opcjonalnie zawęź: Towar / Handlowiec / Odbiorca
                3. Włącz „Ukryj korekty" / „Porównaj YoY"
                4. Kliknij „🔄 Analizuj" lub naciśnij F5

                CO ZOBACZYSZ:
                • 5 KPI: produkcja, sprzedaż, % sprzedaży, ryzyko, dokładność prognozy (MAPE)
                • Wykres: produkcja vs sprzedaż + linia prognozy (8 tygodni) + opcjonalnie YoY
                • Tabela bilansu: zielone = niedobór, czerwone = nadwyżka, ⚡ = anomalia statystyczna
                • Klik na słupek wykresu → automatyczne przejście do szczegółów dnia
                • Klik na kartę KPI „Pozostało" → filtr dni z ryzykiem
                • Klik na kartę KPI „Produkcja" / „Sprzedaż" → odpowiednia zakładka
                • Right-click na wierszu → menu kontekstowe (kopiuj, filtruj, anomalie)
                • Tab „Rankingi" → Top-N odbiorców / handlowców / produktów
                • Tab „Heatmapa" → mapa cieplna sprzedaży produkt × dzień

                SKRÓTY:
                • F5 — Analizuj
                • F11 — Pełny ekran (Esc — wyjście)

                AUTO-REFRESH:
                Wybierz interwał (5/15/30 min) — okno samo odświeży dane.
                """;
            MessageBox.Show(instrukcja, "Instrukcja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ═════════════════════════════════════════════
        //  Loading overlay
        // ═════════════════════════════════════════════

        private void SetBusy(bool busy, string details = null)
        {
            _isLoading = busy;
            if (loadingOverlay == null) return;
            loadingOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (busy && details != null && txtLoadingDetails != null)
                txtLoadingDetails.Text = details;
        }
    }
}
