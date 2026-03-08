using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kalendarz1.Partie.Models;
using Kalendarz1.Partie.Services;
using Kalendarz1.Partie.Windows;

namespace Kalendarz1.Partie.Views
{
    public partial class ProdukcjaDzisWidok : UserControl
    {
        private readonly PartiaService _service;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _clockTimer;
        private List<PartiaModel> _dzisPartie = new();
        private List<HarmonogramItem> _harmonogram = new();
        private List<QCNormaModel> _normy = new();
        private List<AlertModel> _alerts = new();
        private PartiaModel _flyoutPartia;

        public ProdukcjaDzisWidok()
        {
            InitializeComponent();
            _service = new PartiaService();

            TxtDzisData.Text = DateTime.Today.ToString("dddd, yyyy-MM-dd");
            TxtCzas.Text = DateTime.Now.ToString("HH:mm:ss");

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => TxtCzas.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += async (s, e) => await LoadDataAsync(silent: true);

            this.PreviewKeyDown += OnPreviewKeyDown;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            _refreshTimer.Start();
        }

        private async System.Threading.Tasks.Task LoadDataAsync(bool silent = false)
        {
            try
            {
                if (!silent) LoadingOverlay.Visibility = Visibility.Visible;

                var partieTask = _service.GetPartieDzisAsync();
                var harmTask = _service.GetDzisHarmonogramAsync();
                var normyTask = _service.GetNormyAsync();
                var sparklineTask = _service.GetHourlyProductionBulkAsync();

                await System.Threading.Tasks.Task.WhenAll(partieTask, harmTask, normyTask, sparklineTask);

                _dzisPartie = partieTask.Result;
                _harmonogram = harmTask.Result;
                _normy = normyTask.Result;
                var hourlyData = sparklineTask.Result;

                // Feature 10: Auto status detection
                await _service.RunAutoStatusDetectionAsync(_dzisPartie);

                // Feature 7: Alerts
                _alerts = _service.GetAlerts(_dzisPartie, _normy);

                // Feature 4: Assign sparkline points
                AssignSparklines(hourlyData);

                UpdateCards();
                UpdateStats();
                UpdateHarmonogram();
                UpdateAlerts();
            }
            catch (Exception ex)
            {
                if (!silent)
                    MessageBox.Show($"Blad ladowania:\n{ex.Message}", "Blad",
                        MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!silent) LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SPARKLINE (Feature 4)
        // ═══════════════════════════════════════════════════════════════

        private void AssignSparklines(Dictionary<string, List<HourlyProductionPoint>> hourlyData)
        {
            foreach (var p in _dzisPartie)
            {
                if (!hourlyData.TryGetValue(p.Partia, out var points) || points.Count < 2)
                {
                    p.SparklinePoints = null;
                    continue;
                }

                var ordered = points.OrderBy(x => x.Hour).ToList();
                decimal maxKg = ordered.Max(pt => pt.CumulativeKg);
                int minHour = ordered.Min(pt => pt.Hour);
                int maxHour = ordered.Max(pt => pt.Hour);
                int hourRange = Math.Max(maxHour - minHour, 1);

                var pc = new PointCollection();
                foreach (var pt in ordered)
                {
                    double x = (pt.Hour - minHour) / (double)hourRange;
                    double y = 1.0 - (maxKg > 0 ? (double)(pt.CumulativeKg / maxKg) : 0);
                    pc.Add(new Point(x, y));
                }
                p.SparklinePoints = pc;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UI UPDATE
        // ═══════════════════════════════════════════════════════════════

        private void UpdateCards()
        {
            var active = _dzisPartie.Where(p => p.IsActive).OrderByDescending(p => p.CreateGodzina).ToList();
            var closed = _dzisPartie.Where(p => !p.IsActive).OrderByDescending(p => p.CloseGodzina).ToList();

            CardsActive.ItemsSource = active;
            CardsClosed.ItemsSource = closed;
        }

        private void UpdateStats()
        {
            int total = _dzisPartie.Count;
            int open = _dzisPartie.Count(p => p.IsActive);
            decimal totalKg = _dzisPartie.Sum(p => p.WydanoKg);

            var wydList = _dzisPartie.Where(p => p.WydajnoscProc.HasValue).ToList();
            decimal avgWyd = wydList.Any() ? wydList.Average(p => p.WydajnoscProc.Value) : 0;

            var tempList = _dzisPartie.Where(p => p.TempRampa.HasValue).ToList();
            decimal avgTemp = tempList.Any() ? tempList.Average(p => p.TempRampa.Value) : 0;

            TxtStatPartii.Text = total.ToString();
            TxtStatOtwartych.Text = open.ToString();
            TxtStatKg.Text = $"{totalKg:N0}";
            TxtStatWydajnosc.Text = avgWyd > 0 ? $"{avgWyd:N1}%" : "-";
            TxtStatTemp.Text = avgTemp != 0 ? $"{avgTemp:N1} C" : "-";
            TxtStatHarmonogram.Text = _harmonogram.Count.ToString();

            TxtFooter.Text = $"Partii dzis: {total} | Wydano: {totalKg:N0} kg | Alerty: {_alerts.Count} | Ostatnie odsw.: {DateTime.Now:HH:mm:ss}";
        }

        private void UpdateHarmonogram()
        {
            ListHarmonogram.ItemsSource = _harmonogram;
        }

        private void UpdateAlerts()
        {
            if (_alerts.Count > 0)
            {
                AlertsPanel.Visibility = Visibility.Visible;
                ListAlerts.ItemsSource = _alerts;
                TxtAlertCount.Text = $"({_alerts.Count})";
            }
            else
            {
                AlertsPanel.Visibility = Visibility.Collapsed;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DETAIL FLYOUT (Feature 3)
        // ═══════════════════════════════════════════════════════════════

        private void ShowFlyout(PartiaModel partia)
        {
            _flyoutPartia = partia;
            FlyoutPartia.Text = partia.Partia;
            FlyoutStatus.Text = $"{partia.StatusText}  |  {partia.CustomerName}";

            FlyoutContent.Children.Clear();

            // Info section
            AddFlyoutSection("Informacje");
            AddFlyoutRow("Dostawca:", $"{partia.CustomerName} ({partia.CustomerID})");
            AddFlyoutRow("Dzial:", partia.DirID);
            AddFlyoutRow("Otwarcie:", $"{partia.CreateData} {partia.CreateGodzina}");
            AddFlyoutRow("Otworzyl:", partia.OtworzylNazwa);
            if (!string.IsNullOrEmpty(partia.VetNo))
                AddFlyoutRow("Swiad. wet.:", partia.VetNo);

            // Metrics
            AddFlyoutSection("Metryki");
            AddFlyoutRow("Szt. deklarowane:", $"{partia.SztDekl:N0}");
            AddFlyoutRow("Netto skup:", $"{partia.NettoSkup:N1} kg");
            AddFlyoutRow("Wydano:", $"{partia.WydanoKg:N1} kg ({partia.WydanoSzt} szt)");
            AddFlyoutRow("Przyjeto:", $"{partia.PrzyjetoKg:N1} kg ({partia.PrzyjetoSzt} szt)");
            AddFlyoutRow("Na stanie:", $"{partia.NaStanieKg:N1} kg");
            AddFlyoutRow("Wydajnosc:", partia.WydajnoscProc.HasValue
                ? $"{partia.WydajnoscProc:N1}%" : "- (brak danych)");

            // QC
            AddFlyoutSection("Kontrola jakosci");
            AddFlyoutRow("QC:", partia.QCBadge);
            if (partia.KlasaBProc.HasValue)
                AddFlyoutRow("Klasa B:", $"{partia.KlasaBProc:N1}%");
            if (partia.TempRampa.HasValue)
            {
                var normTemp = _normy?.Find(n => n.Nazwa == "TempRampa");
                string tempColor = (normTemp != null && !normTemp.IsInNorm(partia.TempRampa)) ? "#E74C3C" : "#2C3E50";
                AddFlyoutRow("Temp rampa:", $"{partia.TempRampa:N1} C", tempColor);
            }
            if (partia.MaWady)
                AddFlyoutRow("Wady:", $"S:{partia.SkrzydlaOcena} N:{partia.NogiOcena} O:{partia.OparzeniaOcena}");
            AddFlyoutRow("Zdjecia:", $"{partia.IloscZdjec} szt.");
            if (partia.Padle > 0)
                AddFlyoutRow("Padle:", $"{partia.Padle} szt");

            // Sparkline (larger)
            if (partia.SparklinePoints != null && partia.SparklinePoints.Count >= 2)
            {
                AddFlyoutSection("Produkcja (godz.)");
                var polyline = new System.Windows.Shapes.Polyline
                {
                    Points = partia.SparklinePoints,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E44AD")),
                    StrokeThickness = 2,
                    Stretch = Stretch.Fill,
                    Height = 60,
                    Margin = new Thickness(0, 4, 0, 8)
                };
                FlyoutContent.Children.Add(polyline);
            }

            // Action buttons
            AddFlyoutSection("Akcje");
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            if (partia.IsActive)
            {
                if (partia.CanAdvanceStatus)
                {
                    var btnAdvance = CreateFlyoutButton($">> {partia.NextStatusText}", "#2980B9");
                    btnAdvance.Click += async (s, ev) =>
                    {
                        var next = PartiaStatusHelper.GetNextStatus(partia.StatusV2);
                        if (next.HasValue)
                        {
                            await _service.UpdateStatusV2Async(partia.Partia, next.Value.ToString(),
                                partia.StatusV2.ToString(), App.UserID, App.UserFullName, "Szybka zmiana");
                            await LoadDataAsync();
                            CloseFlyout();
                        }
                    };
                    btnPanel.Children.Add(btnAdvance);
                }

                var btnZamknij = CreateFlyoutButton("Zamknij", "#E67E22");
                btnZamknij.Click += async (s, ev) =>
                {
                    var dialog = new ZamknijPartieDialog(partia);
                    dialog.Owner = Window.GetWindow(this);
                    if (dialog.ShowDialog() == true)
                    {
                        await LoadDataAsync();
                        CloseFlyout();
                    }
                };
                btnPanel.Children.Add(btnZamknij);
            }
            else
            {
                var btnOtworz = CreateFlyoutButton("Otworz ponownie", "#8E44AD");
                btnOtworz.Click += async (s, ev) =>
                {
                    var dialog = new OtworzPartieDialog(partia);
                    dialog.Owner = Window.GetWindow(this);
                    if (dialog.ShowDialog() == true)
                    {
                        await LoadDataAsync();
                        CloseFlyout();
                    }
                };
                btnPanel.Children.Add(btnOtworz);
            }

            FlyoutContent.Children.Add(btnPanel);

            // Show flyout, hide cards
            CardsScrollViewer.Visibility = Visibility.Collapsed;
            DetailFlyout.Visibility = Visibility.Visible;
        }

        private void CloseFlyout()
        {
            _flyoutPartia = null;
            DetailFlyout.Visibility = Visibility.Collapsed;
            CardsScrollViewer.Visibility = Visibility.Visible;
        }

        private void CloseFlyout_Click(object sender, RoutedEventArgs e)
        {
            CloseFlyout();
        }

        private void AddFlyoutSection(string title)
        {
            FlyoutContent.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F")),
                Margin = new Thickness(0, 12, 0, 4),
                FontFamily = new FontFamily("Segoe UI")
            });
        }

        private void AddFlyoutRow(string label, string value, string valueColor = "#2C3E50")
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            sp.Children.Add(new TextBlock
            {
                Text = label,
                Width = 120,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                FontFamily = new FontFamily("Segoe UI")
            });
            sp.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(valueColor)),
                FontFamily = new FontFamily("Segoe UI")
            });
            FlyoutContent.Children.Add(sp);
        }

        private Button CreateFlyoutButton(string text, string bgColor)
        {
            var btn = new Button
            {
                Content = text,
                Foreground = Brushes.White,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            return btn;
        }

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is PartiaModel partia)
            {
                ShowFlyout(partia);
            }
        }

        private async void QuickStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PartiaModel partia)
            {
                var next = PartiaStatusHelper.GetNextStatus(partia.StatusV2);
                if (next.HasValue)
                {
                    try
                    {
                        await _service.UpdateStatusV2Async(partia.Partia, next.Value.ToString(),
                            partia.StatusV2.ToString(), App.UserID, App.UserFullName, "Szybka zmiana z dashboard");
                        await LoadDataAsync(silent: true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Blad zmiany statusu:\n{ex.Message}", "Blad",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void Harmonogram_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is HarmonogramItem harm)
            {
                if (harm.MaPartie)
                {
                    MessageBox.Show($"Pozycja harmonogramu Lp={harm.Lp} ({harm.Dostawca}) ma juz przypisana partie.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new NowaPartiaDialog(harm);
                dialog.Owner = Window.GetWindow(this);
                if (dialog.ShowDialog() == true)
                    await LoadDataAsync();
            }
        }

        private async void BtnNowaPartia_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NowaPartiaDialog();
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
                await LoadDataAsync();
        }

        private void BtnListaPartii_Click(object sender, RoutedEventArgs e)
        {
            var parent = Window.GetWindow(this);
            if (parent != null)
            {
                var content = parent.Content;
                if (content is TabControl tc)
                {
                    foreach (TabItem tab in tc.Items)
                    {
                        if (tab.Content is WidokPartie)
                        {
                            tc.SelectedItem = tab;
                            return;
                        }
                    }
                }
            }

            var win = new ListaPartiiWindow();
            win.Show();
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

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
            else if (e.Key == Key.Escape && DetailFlyout.Visibility == Visibility.Visible)
            {
                CloseFlyout();
                e.Handled = true;
            }
        }
    }
}
