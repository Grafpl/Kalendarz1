using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DevExpress.Xpf.Grid;
using Kalendarz1.Partie.Models;
using Kalendarz1.Partie.Services;
using Kalendarz1.Partie.Windows;

namespace Kalendarz1.Partie.Views
{
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrEmpty(hex))
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class WidokPartie : UserControl
    {
        private readonly PartiaService _service;
        private ObservableCollection<PartiaModel> _partieCollection;
        private List<PartiaModel> _allPartie;
        private DispatcherTimer _autoRefreshTimer;

        // Cache detail data to avoid re-loading on tab switch
        private readonly Dictionary<string, List<WazenieModel>> _cachedWazenia = new();
        private readonly Dictionary<string, List<ProduktPartiiModel>> _cachedProdukty = new();
        private readonly Dictionary<string, QCDataModel> _cachedQC = new();
        private readonly Dictionary<string, SkupDataModel> _cachedSkup = new();
        private readonly Dictionary<string, List<HaccpModel>> _cachedHaccp = new();
        private readonly Dictionary<string, List<TimelineEvent>> _cachedTimeline = new();

        public WidokPartie()
        {
            InitializeComponent();

            _service = new PartiaService();
            _partieCollection = new ObservableCollection<PartiaModel>();
            _allPartie = new List<PartiaModel>();

            gridPartie.ItemsSource = _partieCollection;

            dpOd.SelectedDate = DateTime.Today.AddDays(-7);
            dpDo.SelectedDate = DateTime.Today;

            this.PreviewKeyDown += OnPreviewKeyDown;

            // Auto-refresh every 30 seconds
            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoRefreshTimer.Tick += async (s, e) => await LoadDataAsync(silent: true);
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            _autoRefreshTimer.Start();
        }

        // ═══════════════════════════════════════════════════════════════
        // DATA LOADING
        // ═══════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadDataAsync(bool silent = false)
        {
            if (!dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue) return;

            try
            {
                if (!silent) ShowLoading(true, "Ladowanie partii...");

                string dataOd = dpOd.SelectedDate.Value.ToString("yyyy-MM-dd");
                string dataDo = dpDo.SelectedDate.Value.ToString("yyyy-MM-dd");
                string dzialFilter = GetDzialFilter();
                int? statusFilter = GetStatusFilter();
                string statusV2Filter = GetStatusV2Filter();
                string szukaj = TxtSzukaj.Text?.Trim();
                if (string.IsNullOrEmpty(szukaj)) szukaj = null;

                var partie = await _service.GetPartieAsync(dataOd, dataDo, dzialFilter, statusFilter, szukaj, statusV2Filter);
                _allPartie = partie;

                _partieCollection.Clear();
                foreach (var p in partie)
                    _partieCollection.Add(p);

                ClearDetailCache();
                await UpdateStatsAsync();
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show($"Blad ladowania danych:\n{ex.Message}", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!silent) ShowLoading(false);
            }
        }

        private async System.Threading.Tasks.Task UpdateStatsAsync()
        {
            var stats = await _service.GetStatsAsync(_allPartie);
            TxtStats.Text = $"Partii: {stats.LiczbaPartii} (otwartych: {stats.Otwartych}, zamknietych: {stats.Zamknietych})" +
                $"  |  Dzis: {stats.DzisPartii} partii, {stats.DzisKg:N0} kg" +
                $"  |  Sr. wydajnosc: {stats.SrWydajnosc:N1}%" +
                $"  |  Sr. klasa B: {stats.SrKlasaB:N1}%" +
                $"  |  Sr. temp rampa: {stats.SrTempRampa:N1} C";
        }

        // ═══════════════════════════════════════════════════════════════
        // DETAIL LOADING (lazy, on expand)
        // ═══════════════════════════════════════════════════════════════

        private PartiaModel GetPartiaFromDetail(DependencyObject element)
        {
            var border = FindParent<Border>(element);
            while (border != null)
            {
                if (border.Tag is PartiaModel pm) return pm;
                // Also check DataContext in case Tag binding uses wrapper
                if (border.DataContext is PartiaModel dc) return dc;
                border = FindParent<Border>(VisualTreeHelper.GetParent(border));
            }
            return null;
        }

        private async void DetailTabs_Loaded(object sender, RoutedEventArgs e)
        {
            // When detail expands, load the first tab (wazenia) automatically
            if (sender is not TabControl tabs) return;
            var partia = GetPartiaFromDetail(tabs);
            if (partia == null) return;

            try { await LoadDetailWazeniaForTabAsync(partia.Partia, tabs); }
            catch { /* detail load failure should not crash */ }
        }

        private async void DetailTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl tabs) return;
            if (tabs.SelectedItem is not TabItem tab) return;

            var partia = GetPartiaFromDetail(tabs);
            if (partia == null) return;

            string tag = tab.Tag?.ToString();
            try
            {
                switch (tag)
                {
                    case "wazenia":
                        await LoadDetailWazeniaForTabAsync(partia.Partia, tabs);
                        break;
                    case "produkty":
                        await LoadDetailProduktyForTabAsync(partia.Partia, tabs);
                        break;
                    case "qc":
                        await LoadDetailQCForTabAsync(partia.Partia, tabs);
                        break;
                    case "skup":
                        await LoadDetailSkupForTabAsync(partia.Partia, tabs);
                        break;
                    case "haccp":
                        await LoadDetailHaccpForTabAsync(partia.Partia, tabs);
                        break;
                    case "timeline":
                        await LoadDetailTimelineForTabAsync(partia.Partia, tabs);
                        break;
                }
            }
            catch { /* detail tab load failure should not crash */ }
        }

        private async System.Threading.Tasks.Task LoadDetailWazeniaForTabAsync(string partia, TabControl tabs)
        {
            if (!_cachedWazenia.ContainsKey(partia))
            {
                var data = await _service.GetWazeniaAsync(partia);
                _cachedWazenia[partia] = data;
            }

            var grid = FindChildByName<GridControl>(tabs, "gridWazenia");
            if (grid != null)
                grid.ItemsSource = _cachedWazenia[partia];
        }

        private async System.Threading.Tasks.Task LoadDetailProduktyForTabAsync(string partia, TabControl tabs)
        {
            if (!_cachedProdukty.ContainsKey(partia))
            {
                var data = await _service.GetProduktyAsync(partia);
                _cachedProdukty[partia] = data;
            }

            var grid = FindChildByName<GridControl>(tabs, "gridProdukty");
            if (grid != null)
                grid.ItemsSource = _cachedProdukty[partia];
        }

        private async System.Threading.Tasks.Task LoadDetailQCForTabAsync(string partia, TabControl tabs)
        {
            if (!_cachedQC.ContainsKey(partia))
            {
                var data = await _service.GetQCDataAsync(partia);
                _cachedQC[partia] = data;
            }

            var panel = FindChildByName<StackPanel>(tabs, "panelQC");
            if (panel == null) return;

            var qc = _cachedQC[partia];
            panel.Children.Clear();

            // Temperatury
            panel.Children.Add(new TextBlock
            {
                Text = "Temperatury",
                Style = (Style)FindResource("SectionHeader")
            });

            if (qc.Temperatury.Any())
            {
                var tempGrid = new Grid();
                tempGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(100) });
                for (int i = 0; i < 5; i++)
                    tempGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(80) });

                string[] headers = { "Miejsce", "Proba 1", "Proba 2", "Proba 3", "Proba 4", "Srednia" };
                for (int c = 0; c < headers.Length; c++)
                {
                    var hdr = new TextBlock
                    {
                        Text = headers[c],
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D)),
                        Margin = new Thickness(4, 2, 4, 2)
                    };
                    Grid.SetColumn(hdr, c);
                    Grid.SetRow(hdr, 0);
                    tempGrid.Children.Add(hdr);
                }

                tempGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
                for (int i = 0; i < qc.Temperatury.Count; i++)
                {
                    tempGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition());
                    var t = qc.Temperatury[i];
                    string[] vals = { t.Miejsce, FormatTemp(t.Proba1), FormatTemp(t.Proba2),
                                      FormatTemp(t.Proba3), FormatTemp(t.Proba4), FormatTemp(t.Srednia) };
                    for (int c = 0; c < vals.Length; c++)
                    {
                        var tb = new TextBlock
                        {
                            Text = vals[c],
                            FontSize = 12,
                            Margin = new Thickness(4, 2, 4, 2),
                            Foreground = c == 5 && t.Srednia > 4
                                ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
                                : new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50))
                        };
                        if (c == 5) tb.FontWeight = FontWeights.Bold;
                        Grid.SetColumn(tb, c);
                        Grid.SetRow(tb, i + 1);
                        tempGrid.Children.Add(tb);
                    }
                }
                panel.Children.Add(tempGrid);
            }
            else
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Brak pomiarow temperatury",
                    Style = (Style)FindResource("DetailLabel"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12))
                });
            }

            // Wady
            panel.Children.Add(new TextBlock
            {
                Text = "Ocena wad",
                Style = (Style)FindResource("SectionHeader")
            });

            if (qc.SkrzydlaOcena.HasValue)
            {
                panel.Children.Add(CreateRatingRow("Skrzydla:", qc.SkrzydlaOcena.Value));
                panel.Children.Add(CreateRatingRow("Nogi:", qc.NogiOcena ?? 0));
                panel.Children.Add(CreateRatingRow("Oparzenia:", qc.OparzeniaOcena ?? 0));
            }
            else
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Brak oceny wad",
                    Style = (Style)FindResource("DetailLabel"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12))
                });
            }

            // Podsumowanie
            if (qc.KlasaBProc.HasValue || qc.PrzekarmienieKg.HasValue)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Podsumowanie QC",
                    Style = (Style)FindResource("SectionHeader")
                });
                if (qc.KlasaBProc.HasValue)
                {
                    var klBColor = qc.KlasaBProc > 20
                        ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
                        : new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50));
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"Klasa B: {qc.KlasaBProc:N1}%",
                        FontSize = 13, FontWeight = FontWeights.SemiBold,
                        Foreground = klBColor, Margin = new Thickness(0, 2, 0, 2)
                    });
                }
                if (qc.PrzekarmienieKg.HasValue)
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"Przekarmienie: {qc.PrzekarmienieKg:N1} kg",
                        Style = (Style)FindResource("DetailValue")
                    });
                if (!string.IsNullOrEmpty(qc.Notatka))
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"Notatka: {qc.Notatka}",
                        Style = (Style)FindResource("DetailLabel"),
                        TextWrapping = TextWrapping.Wrap
                    });
            }

            // Zdjecia
            if (qc.Zdjecia.Any())
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"Zdjecia ({qc.Zdjecia.Count} szt.)",
                    Style = (Style)FindResource("SectionHeader")
                });
                foreach (var z in qc.Zdjecia)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"  {z.WadaTyp}: {z.Opis} ({z.Wykonal})",
                        Style = (Style)FindResource("DetailLabel")
                    });
                }
            }
        }

        private async System.Threading.Tasks.Task LoadDetailSkupForTabAsync(string partia, TabControl tabs)
        {
            if (!_cachedSkup.ContainsKey(partia))
            {
                var data = await _service.GetSkupDataAsync(partia);
                _cachedSkup[partia] = data;
            }

            var panel = FindChildByName<StackPanel>(tabs, "panelSkup");
            if (panel == null) return;

            panel.Children.Clear();
            var skup = _cachedSkup[partia];
            if (skup == null)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Brak danych skupu (FarmerCalc)",
                    Style = (Style)FindResource("DetailLabel")
                });
                return;
            }

            panel.Children.Add(new TextBlock { Text = "Dane skupu", Style = (Style)FindResource("SectionHeader") });
            AddLabelValue(panel, "Dostawca:", $"{skup.CustomerName} ({skup.CustomerID})");
            AddLabelValue(panel, "Data skupu:", skup.CalcDate?.ToString("yyyy-MM-dd") ?? "-");
            AddLabelValue(panel, "Kierowca:", skup.KierowcaNazwa);
            AddLabelValue(panel, "Pojazd:", $"{skup.CarID} + {skup.TrailerID}");

            panel.Children.Add(new TextBlock { Text = "Wagi", Style = (Style)FindResource("SectionHeader") });
            AddLabelValue(panel, "Brutto:", $"{skup.BruttoWeight:N0} kg");
            AddLabelValue(panel, "Tara:", $"{skup.EmptyWeight:N0} kg");
            AddLabelValue(panel, "Netto:", $"{skup.NettoWeight:N0} kg");

            panel.Children.Add(new TextBlock { Text = "Sztuki", Style = (Style)FindResource("SectionHeader") });
            AddLabelValue(panel, "Deklarowane:", $"{skup.DeclI1:N0} szt");
            AddLabelValue(panel, "Padle:", $"{skup.DeclI2} szt");

            panel.Children.Add(new TextBlock { Text = "Rozliczenie", Style = (Style)FindResource("SectionHeader") });
            AddLabelValue(panel, "Cena:", $"{skup.Price:N2} PLN/kg");
            AddLabelValue(panel, "Wartosc netto:", $"{skup.WartoscNetto:N2} PLN");

            if (skup.KmTrasy > 0)
                AddLabelValue(panel, "Km trasy:", $"{skup.KmTrasy} km");

            if (skup.Wyjazd.HasValue)
                AddLabelValue(panel, "Wyjazd:", skup.Wyjazd.Value.ToString("HH:mm"));
            if (skup.Zaladunek.HasValue)
                AddLabelValue(panel, "Zaladunek:", skup.Zaladunek.Value.ToString("HH:mm"));
            if (skup.Przyjazd.HasValue)
                AddLabelValue(panel, "Przyjazd:", skup.Przyjazd.Value.ToString("HH:mm"));
        }

        private async System.Threading.Tasks.Task LoadDetailHaccpForTabAsync(string partia, TabControl tabs)
        {
            if (!_cachedHaccp.ContainsKey(partia))
            {
                var data = await _service.GetHaccpAsync(partia);
                _cachedHaccp[partia] = data;
            }

            var grid = FindChildByName<GridControl>(tabs, "gridHaccp");
            if (grid != null)
                grid.ItemsSource = _cachedHaccp[partia];
        }

        private async System.Threading.Tasks.Task LoadDetailTimelineForTabAsync(string partia, TabControl tabs)
        {
            if (!_cachedTimeline.ContainsKey(partia))
            {
                var data = await _service.GetTimelineAsync(partia);
                _cachedTimeline[partia] = data;
            }

            var list = FindChildByName<ItemsControl>(tabs, "timelineList");
            if (list != null)
                list.ItemsSource = _cachedTimeline[partia];
        }

        // ═══════════════════════════════════════════════════════════════
        // ACTIONS
        // ═══════════════════════════════════════════════════════════════

        private async void BtnNowaPartia_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NowaPartiaDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        private async void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            var partia = GetSelectedPartia();
            if (partia == null) { ShowSelectMessage(); return; }
            if (partia.IsClose == 1)
            {
                MessageBox.Show("Ta partia jest juz zamknieta.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new ZamknijPartieDialog(partia);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        private async void BtnOtworz_Click(object sender, RoutedEventArgs e)
        {
            var partia = GetSelectedPartia();
            if (partia == null) { ShowSelectMessage(); return; }
            if (partia.IsClose != 1)
            {
                MessageBox.Show("Ta partia jest juz otwarta.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OtworzPartieDialog(partia);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                await LoadDataAsync();
            }
        }

        private void BtnExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Partie_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };

                if (dlg.ShowDialog() == true)
                {
                    using (var workbook = new ClosedXML.Excel.XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Partie");
                        string[] headers = { "Partia", "Data", "Dostawca", "Dzial", "Status",
                            "Szt. dekl.", "Netto skup", "Wydano kg", "Przyjeto kg", "Na stanie",
                            "Wydajn.%", "Kl.B %", "Temp rampa" };
                        for (int c = 0; c < headers.Length; c++)
                            ws.Cell(1, c + 1).Value = headers[c];

                        int row = 2;
                        foreach (var p in _allPartie)
                        {
                            ws.Cell(row, 1).Value = p.Partia;
                            ws.Cell(row, 2).Value = p.CreateData;
                            ws.Cell(row, 3).Value = p.CustomerName;
                            ws.Cell(row, 4).Value = p.DirID;
                            ws.Cell(row, 5).Value = p.StatusText;
                            ws.Cell(row, 6).Value = p.SztDekl;
                            ws.Cell(row, 7).Value = (double)p.NettoSkup;
                            ws.Cell(row, 8).Value = (double)p.WydanoKg;
                            ws.Cell(row, 9).Value = (double)p.PrzyjetoKg;
                            ws.Cell(row, 10).Value = (double)p.NaStanieKg;
                            ws.Cell(row, 11).Value = p.WydajnoscProc.HasValue ? (double)p.WydajnoscProc.Value : 0;
                            ws.Cell(row, 12).Value = p.KlasaBProc.HasValue ? (double)p.KlasaBProc.Value : 0;
                            ws.Cell(row, 13).Value = p.TempRampa.HasValue ? (double)p.TempRampa.Value : 0;
                            row++;
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(dlg.FileName);
                    }

                    MessageBox.Show("Eksport zakonczony.", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad eksportu:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void Filter_Changed(object sender, EventArgs e)
        {
            if (IsLoaded)
                await LoadDataAsync();
        }

        private async void TxtSzukaj_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LoadDataAsync();
        }

        private void TableView_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            // Optional: update status bar with selected partia info
        }

        // ═══════════════════════════════════════════════════════════════
        // KEYBOARD SHORTCUTS
        // ═══════════════════════════════════════════════════════════════

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                BtnOdswiez_Click(sender, e);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                BtnNowaPartia_Click(sender, e);
                e.Handled = true;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private PartiaModel GetSelectedPartia()
        {
            if (tableView.FocusedRowHandle < 0) return null;
            return gridPartie.GetRow(tableView.FocusedRowHandle) as PartiaModel;
        }

        private PartiaModel GetPartiaFromRow(int rowHandle)
        {
            if (rowHandle < 0) return null;
            return gridPartie.GetRow(rowHandle) as PartiaModel;
        }

        private string GetDzialFilter()
        {
            if (CmbDzial.SelectedItem is ComboBoxItem item && item.Tag != null)
                return item.Tag.ToString();
            return null;
        }

        private int? GetStatusFilter()
        {
            if (CmbStatus.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string tagStr = item.Tag.ToString();
                if (tagStr.StartsWith("V2:")) return null; // handled by V2 filter
                return int.Parse(tagStr);
            }
            return null;
        }

        private string GetStatusV2Filter()
        {
            if (CmbStatus.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string tagStr = item.Tag.ToString();
                if (tagStr.StartsWith("V2:")) return tagStr.Substring(3);
            }
            return null;
        }

        private void BtnPorownanie_Click(object sender, RoutedEventArgs e)
        {
            string dataOd = dpOd.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
            string dataDo = dpDo.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");

            var win = new DostawcaComparisonWindow(dataOd, dataDo);
            win.Owner = Window.GetWindow(this);
            win.Show();
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            var win = new Window
            {
                Title = "Produkcja Dzis - ZPSP",
                Width = 1400,
                Height = 900,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowState = WindowState.Maximized,
                Content = new ProdukcjaDzisWidok()
            };
            try { WindowIconHelper.SetIcon(win); } catch { }
            win.Show();
        }

        private void ShowLoading(bool visible, string text = "Ladowanie...")
        {
            LoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            LoadingText.Text = text;
        }

        private void ShowSelectMessage()
        {
            MessageBox.Show("Zaznacz partie w tabeli.", "Informacja",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearDetailCache()
        {
            _cachedWazenia.Clear();
            _cachedProdukty.Clear();
            _cachedQC.Clear();
            _cachedSkup.Clear();
            _cachedHaccp.Clear();
            _cachedTimeline.Clear();
        }

        private static string FormatTemp(decimal? val)
        {
            return val.HasValue ? $"{val.Value:N1}" : "-";
        }

        private StackPanel CreateRatingRow(string label, int rating)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                Width = 90,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50))
            });
            for (int i = 1; i <= 5; i++)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = i <= rating ? "\u2605" : "\u2606", // filled/empty star
                    FontSize = 16,
                    Foreground = i <= rating
                        ? new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37))
                        : new SolidColorBrush(Color.FromRgb(0xBD, 0xC3, 0xC7)),
                    Margin = new Thickness(1, 0, 1, 0)
                });
            }
            sp.Children.Add(new TextBlock
            {
                Text = $" ({rating}/5)",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x7F, 0x8C, 0x8D)),
                VerticalAlignment = VerticalAlignment.Center
            });
            return sp;
        }

        private void AddLabelValue(StackPanel panel, string label, string value)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                Width = 130,
                Style = (Style)FindResource("DetailLabel")
            });
            sp.Children.Add(new TextBlock
            {
                Text = value,
                Style = (Style)FindResource("DetailValue")
            });
            panel.Children.Add(sp);
        }

        // Visual tree helpers
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T found) return found;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private static T FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && t.Name == name) return t;
                var result = FindChildByName<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
