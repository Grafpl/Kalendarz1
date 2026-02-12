using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Kalendarz1.PulpitZarzadu.Services;

namespace Kalendarz1.PulpitZarzadu.Views
{
    public partial class PulpitZarzaduWindow : Window
    {
        private DispatcherTimer _autoRefreshTimer;

        // Track error state per section for footer dots
        private bool _magazynOk, _zamowieniaOk, _sprzedazOk, _produkcjaOk, _transportOk, _hrOk;

        public PulpitZarzaduWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            SetupCardHoverEffects();
        }

        private void SetupCardHoverEffects()
        {
            var cards = new[] { cardMagazyn, cardZamowienia, cardSprzedaz, cardProdukcja, cardTransport, cardHr };
            var accents = new[] { "#2196F3", "#FF9800", "#4CAF50", "#9C27B0", "#00BCD4", "#FF5722" };

            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                var accentColor = (Color)ColorConverter.ConvertFromString(accents[i]);

                card.MouseEnter += (s, e) =>
                {
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4D, 0x6A, 0x8A));
                    card.Effect = new DropShadowEffect
                    {
                        ShadowDepth = 4,
                        BlurRadius = 20,
                        Opacity = 0.4,
                        Color = accentColor
                    };
                };

                card.MouseLeave += (s, e) =>
                {
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x36, 0x3D));
                    card.Effect = new DropShadowEffect
                    {
                        ShadowDepth = 4,
                        BlurRadius = 12,
                        Opacity = 0.3,
                        Color = Colors.Black
                    };
                };
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtHeaderDate.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy  HH:mm");

            // Auto-refresh co 5 minut
            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _autoRefreshTimer.Tick += async (s, ev) => await LoadAllKpisAsync();
            _autoRefreshTimer.Start();

            await LoadAllKpisAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadAllKpisAsync();
        }

        private async Task LoadAllKpisAsync()
        {
            loadingOverlay.Show("Ladowanie danych KPI...");
            txtHeaderDate.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy  HH:mm");

            try
            {
                // Load all sections in parallel
                var taskMagazyn = PulpitDataService.LoadMagazynMrozniAsync();
                var taskZamowienia = PulpitDataService.LoadZamowieniaAsync();
                var taskSprzedaz = PulpitDataService.LoadSprzedazAsync();
                var taskProdukcja = PulpitDataService.LoadProdukcjaAsync();
                var taskTransport = PulpitDataService.LoadTransportAsync();
                var taskHr = PulpitDataService.LoadHrFrekwencjaAsync();

                await Task.WhenAll(taskMagazyn, taskZamowienia, taskSprzedaz, taskProdukcja, taskTransport, taskHr);

                // Update UI on dispatcher thread
                UpdateMagazyn(taskMagazyn.Result);
                UpdateZamowienia(taskZamowienia.Result);
                UpdateSprzedaz(taskSprzedaz.Result);
                UpdateProdukcja(taskProdukcja.Result);
                UpdateTransport(taskTransport.Result);
                UpdateHr(taskHr.Result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad KPI: {ex.Message}");
            }
            finally
            {
                loadingOverlay.Hide();
                UpdateFooter();
            }
        }

        private void UpdateFooter()
        {
            txtLastUpdate.Text = $"Ostatnia aktualizacja: {DateTime.Now:HH:mm:ss}";

            var green = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            var red = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

            // Handel = Magazyn + Zamowienia + Sprzedaz
            dotHandel.Fill = (_magazynOk && _zamowieniaOk && _sprzedazOk) ? green : red;
            // LibraNet = Produkcja
            dotLibraNet.Fill = _produkcjaOk ? green : red;
            // Transport
            dotTransport.Fill = _transportOk ? green : red;
            // Unicard = HR
            dotUnicard.Fill = _hrOk ? green : red;
        }

        private void UpdateMagazyn(KpiSection section)
        {
            _magazynOk = !section.HasError;
            if (section.HasError)
            {
                txtMagazynError.Text = section.ErrorMessage;
                txtMagazynError.Visibility = Visibility.Visible;
                return;
            }
            txtMagazynError.Visibility = Visibility.Collapsed;

            if (section.Items.Count >= 3)
            {
                txtMagazynStan.Text = section.Items[0].Value;
                txtMagazynWydania.Text = section.Items[1].Value;
                txtMagazynPrzyjecia.Text = section.Items[2].Value;
            }

            // Mini bar chart with gradient bars
            if (section.ChartData.Count > 0)
            {
                chartMagazyn.Items.Clear();
                double maxVal = 1;
                foreach (var (_, val) in section.ChartData)
                    if (val > maxVal) maxVal = val;

                foreach (var (label, val) in section.ChartData)
                {
                    double height = maxVal > 0 ? (val / maxVal) * 40 : 0;
                    var bar = new Border
                    {
                        Background = new LinearGradientBrush(
                            Color.FromRgb(33, 150, 243),
                            Color.FromRgb(21, 101, 170),
                            90),
                        Height = Math.Max(height, 2),
                        CornerRadius = new CornerRadius(3, 3, 0, 0),
                        Margin = new Thickness(1, 0, 1, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        ToolTip = $"{label}: {val:#,##0} kg"
                    };
                    chartMagazyn.Items.Add(bar);
                }
            }
        }

        private void UpdateZamowienia(KpiSection section)
        {
            _zamowieniaOk = !section.HasError;
            if (section.HasError)
            {
                txtZamError.Text = section.ErrorMessage;
                txtZamError.Visibility = Visibility.Visible;
                return;
            }
            txtZamError.Visibility = Visibility.Collapsed;

            if (section.Items.Count >= 1)
            {
                txtZamDzis.Text = section.Items[0].Value;
                txtZamDzisKg.Text = section.Items[0].SubText;
            }
            if (section.Items.Count >= 2)
            {
                txtZamJutro.Text = section.Items[1].Value;
            }
        }

        private void UpdateSprzedaz(KpiSection section)
        {
            _sprzedazOk = !section.HasError;
            if (section.HasError)
            {
                txtSprzedazError.Text = section.ErrorMessage;
                txtSprzedazError.Visibility = Visibility.Visible;
                return;
            }
            txtSprzedazError.Visibility = Visibility.Collapsed;

            if (section.Items.Count >= 1)
            {
                txtSprzedazMiesiac.Text = section.Items[0].Value;
                txtSprzedazPln.Text = section.Items[0].SubText;
            }
            if (section.Items.Count >= 2)
            {
                txtSprzedazZmiana.Text = section.Items[1].Value;
                var change = section.Items[1].ChangePercent ?? 0;
                bool isPositive = change >= 0;
                txtSprzedazZmiana.Foreground = new SolidColorBrush(
                    isPositive ? Color.FromRgb(46, 204, 113) : Color.FromRgb(231, 76, 60));

                // Trend arrow
                txtSprzedazArrow.Text = isPositive ? "\u25B2" : "\u25BC";
                txtSprzedazArrow.Foreground = new SolidColorBrush(
                    isPositive ? Color.FromRgb(46, 204, 113) : Color.FromRgb(231, 76, 60));
            }
        }

        private void UpdateProdukcja(KpiSection section)
        {
            _produkcjaOk = !section.HasError;
            if (section.HasError)
            {
                txtProdukcjaError.Text = section.ErrorMessage;
                txtProdukcjaError.Visibility = Visibility.Visible;
                return;
            }
            txtProdukcjaError.Visibility = Visibility.Collapsed;

            if (section.Items.Count >= 1)
            {
                txtProdukcjaZywiec.Text = section.Items[0].Value;
            }
        }

        private void UpdateTransport(KpiSection section)
        {
            _transportOk = !section.HasError;
            if (section.HasError)
            {
                txtTransportError.Text = section.ErrorMessage;
                txtTransportError.Visibility = Visibility.Visible;
                return;
            }
            txtTransportError.Visibility = Visibility.Collapsed;

            if (section.Items.Count >= 1)
            {
                txtTransportKursy.Text = section.Items[0].Value;
            }
            if (section.Items.Count >= 2)
            {
                txtTransportKlienci.Text = section.Items[1].Value;
            }
        }

        private void UpdateHr(KpiSection section)
        {
            _hrOk = !section.HasError;
            if (section.HasError)
            {
                txtHrError.Text = section.ErrorMessage;
                txtHrError.Visibility = Visibility.Visible;
                return;
            }
            txtHrError.Visibility = Visibility.Collapsed;

            if (section.Items.Count >= 1)
            {
                txtHrObecnych.Text = section.Items[0].Value;
            }
            if (section.Items.Count >= 2)
            {
                txtHrAktywnych.Text = section.Items[1].Value;
            }
            if (section.Items.Count >= 3)
            {
                txtHrFrekwencja.Text = section.Items[2].Value;

                // Color frekwencja dot based on value
                if (double.TryParse(section.Items[2].Value?.TrimEnd('%'), out double frekVal))
                {
                    Color dotColor;
                    if (frekVal >= 90)
                        dotColor = Color.FromRgb(0x2E, 0xCC, 0x71); // green
                    else if (frekVal >= 75)
                        dotColor = Color.FromRgb(0xFF, 0x98, 0x00); // orange
                    else
                        dotColor = Color.FromRgb(0xE7, 0x4C, 0x3C); // red

                    var brush = new SolidColorBrush(dotColor);
                    dotFrekwencja.Fill = brush;
                    txtHrFrekwencja.Foreground = brush;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoRefreshTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
