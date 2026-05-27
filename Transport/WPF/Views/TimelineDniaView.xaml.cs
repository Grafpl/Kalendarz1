// ════════════════════════════════════════════════════════════════════════════
// Transport/WPF/Views/TimelineDniaView.xaml.cs — oś czasu dnia (Gantt).
// Wiersze per kierowca, paski kursów po godzinach, linia „teraz", drag&drop:
//  • na pasek kursu → przypisanie zamówienia
//  • na pusty obszar wiersza → utworzenie nowego kursu (preselekcja kierowcy)
// Faza T zdegradowana: bez pending-bell (Faza 2) i hatch niedostępności.
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Kalendarz1.Transport.WPF.Controls;
using Kalendarz1.Transport.WPF.Models;
using Kalendarz1.Transport.WPF.Services;

namespace Kalendarz1.Transport.WPF.Views
{
    public partial class TimelineDniaView : UserControl
    {
        // ── stałe czasowe ──
        private const int HourStart = 6;
        private const int HourEnd = 22;
        private const double RowHeight = 50;
        private int HoursVisible => HourEnd - HourStart;
        private double _pph = 38;   // pixels per hour (zoom)

        public TransportWpfService? Svc { get; set; }
        public string Uzytkownik { get; set; } = "system";

        private DateTime _data = DateTime.Today;
        private bool _ukryjWolnych;
        private List<KierowcaWierszTimeline> _wiersze = new();
        private bool _renderowanie;

        private Line? _nowLine;
        private Border? _nowLabel;
        private Rectangle? _dropHi;
        private readonly DispatcherTimer _nowTimer;

        public event Action<long>? KursOtwarty;   // klik na pasek → otwórz edytor
        public event Action? Zmieniono;            // po drop/utworzeniu → odśwież wolne w rodzicu

        public TimelineDniaView()
        {
            InitializeComponent();
            TimelineCanvas.DragOver += Canvas_DragOver;
            TimelineCanvas.Drop += Canvas_Drop;
            TimelineCanvas.DragLeave += (_, _) => UsunDropHighlight();
            _nowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _nowTimer.Tick += (_, _) => UpdateNowLine();
            Loaded += (_, _) => _nowTimer.Start();
            Unloaded += (_, _) => _nowTimer.Stop();
        }

        private double TimeToX(TimeSpan t) => (t.TotalHours - HourStart) * _pph;
        private TimeSpan XToTime(double x) => TimeSpan.FromMinutes((x / _pph) * 60 + HourStart * 60);

        public void UstawDate(DateTime d) => _data = d.Date;

        // ════════════════════════════════════════════════════════════════════
        public async Task RenderAsync()
        {
            if (Svc == null || _renderowanie) return;
            _renderowanie = true;
            try
            {
                NazwaDnia.Text = _data.ToString("dddd, dd.MM.yyyy", new CultureInfo("pl-PL"));
                _wiersze = await Svc.LoadKierowcyZKursamiAsync(_data, _ukryjWolnych);

                double totalW = HoursVisible * _pph;

                // ── nagłówek godzin ──
                HeaderCanvas.Children.Clear();
                HeaderCanvas.Width = totalW;
                for (int h = HourStart; h <= HourEnd; h++)
                {
                    double x = (h - HourStart) * _pph;
                    if (h < HourEnd)
                    {
                        var tb = new TextBlock { Text = $"{h:00}:00", FontSize = 10.5, Foreground = B("#8A95A3") };
                        Canvas.SetLeft(tb, x + 3); Canvas.SetTop(tb, 5);
                        HeaderCanvas.Children.Add(tb);
                    }
                    HeaderCanvas.Children.Add(new Line { X1 = x, X2 = x, Y1 = 19, Y2 = 26, Stroke = B("#DCE1E6"), StrokeThickness = 1 });
                }

                // ── body ──
                TimelineCanvas.Children.Clear();
                _dropHi = null; _nowLine = null; _nowLabel = null;
                LeftRows.ItemsSource = null;
                LeftRows.ItemsSource = _wiersze;
                TimelineCanvas.Width = totalW;
                TimelineCanvas.Height = Math.Max(RowHeight, _wiersze.Count * RowHeight) + 20;

                for (int i = 0; i < _wiersze.Count; i++)
                {
                    double yTop = i * RowHeight;
                    if (i % 2 == 1)
                    {
                        var bg = new Rectangle { Width = totalW, Height = RowHeight, Fill = B("#F8FAFB") };
                        Canvas.SetLeft(bg, 0); Canvas.SetTop(bg, yTop); Panel.SetZIndex(bg, 0);
                        TimelineCanvas.Children.Add(bg);
                    }
                    for (int h = HourStart; h <= HourEnd; h++)
                    {
                        double x = (h - HourStart) * _pph;
                        var ln = new Line { X1 = x, X2 = x, Y1 = yTop, Y2 = yTop + RowHeight, Stroke = B("#EAEDF0"), StrokeThickness = 1 };
                        Panel.SetZIndex(ln, 1);
                        TimelineCanvas.Children.Add(ln);
                    }
                    TimelineCanvas.Children.Add(new Line { X1 = 0, X2 = totalW, Y1 = yTop + RowHeight, Y2 = yTop + RowHeight, Stroke = B("#EAEDF0"), StrokeThickness = 1 });

                    foreach (var kurs in _wiersze[i].Kursy)
                    {
                        var bar = new KursBarControl();
                        bar.Bind(kurs);
                        double left = TimeToX(kurs.Wyjazd);
                        double right = TimeToX(kurs.Powrot);
                        Canvas.SetLeft(bar, left);
                        Canvas.SetTop(bar, yTop + 5);
                        bar.Width = Math.Max(22, right - left);
                        bar.Height = RowHeight - 12;
                        Panel.SetZIndex(bar, kurs.Konflikt ? 15 : 10);
                        var b = bar;
                        b.MouseLeftButtonUp += (_, _) => { if (b.Kurs != null) KursOtwarty?.Invoke(b.Kurs.KursID); };
                        b.AllowDrop = true;
                        b.Drop += OnDropOnBar;
                        b.DragEnter += (_, e) => { if (e.Data.GetDataPresent(WpfDragHelper.FmtWolne)) b.Opacity = 0.72; };
                        b.DragLeave += (_, _) => b.Opacity = 1.0;
                        TimelineCanvas.Children.Add(b);
                    }
                }

                UpdateNowLine();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Timeline] Render: {ex.Message}");
            }
            finally { _renderowanie = false; }
        }

        // ── linia „teraz" (tylko dziś), aktualizowana co 60s ──
        private void UpdateNowLine()
        {
            if (_nowLine != null) { TimelineCanvas.Children.Remove(_nowLine); _nowLine = null; }
            if (_nowLabel != null) { TimelineCanvas.Children.Remove(_nowLabel); _nowLabel = null; }
            if (_data.Date != DateTime.Today) return;
            var now = DateTime.Now.TimeOfDay;
            if (now.TotalHours < HourStart || now.TotalHours > HourEnd) return;

            double x = TimeToX(now);
            double hgt = Math.Max(RowHeight, _wiersze.Count * RowHeight);
            _nowLine = new Line { X1 = x, X2 = x, Y1 = 0, Y2 = hgt, Stroke = Brushes.Red, StrokeThickness = 1.5, IsHitTestVisible = false };
            Panel.SetZIndex(_nowLine, 100);
            TimelineCanvas.Children.Add(_nowLine);

            _nowLabel = new Border
            {
                Background = Brushes.Red,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                IsHitTestVisible = false,
                Child = new TextBlock { Text = now.ToString(@"hh\:mm"), Foreground = Brushes.White, FontSize = 10, FontWeight = FontWeights.SemiBold }
            };
            Panel.SetZIndex(_nowLabel, 101);
            Canvas.SetLeft(_nowLabel, x - 16);
            Canvas.SetTop(_nowLabel, 0);
            TimelineCanvas.Children.Add(_nowLabel);
        }

        // ════════════════════════════════════════════════════════════════════
        // DRAG & DROP
        // ════════════════════════════════════════════════════════════════════
        private async void OnDropOnBar(object sender, DragEventArgs e)
        {
            e.Handled = true;
            UsunDropHighlight();
            if (sender is not KursBarControl bar || bar.Kurs == null || Svc == null) return;
            bar.Opacity = 1.0;
            var zam = Przeciagane(e);
            if (zam.Count == 0) return;
            try
            {
                await Svc.DodajWolneDoKursuAsync(bar.Kurs.KursID, zam, Uzytkownik);
                Zmieniono?.Invoke();
                await RenderAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Canvas_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(WpfDragHelper.FmtWolne)) { e.Effects = DragDropEffects.None; return; }
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            var p = e.GetPosition(TimelineCanvas);
            int i = (int)(p.Y / RowHeight);
            if (i < 0 || i >= _wiersze.Count) { UsunDropHighlight(); return; }
            PokazDropHighlight(i);
            var sugg = ZaokraglijDo15(XToTime(p.X));
            DragStatusBar.Visibility = Visibility.Visible;
            DragStatusText.Text = $"Upuść, aby utworzyć kurs dla: {_wiersze[i].PelneNazwisko}   ·   ~{sugg:hh\\:mm}";
        }

        private async void Canvas_Drop(object sender, DragEventArgs e)
        {
            UsunDropHighlight();
            if (!e.Data.GetDataPresent(WpfDragHelper.FmtWolne) || Svc == null) return;
            var zam = Przeciagane(e);
            if (zam.Count == 0) return;
            var p = e.GetPosition(TimelineCanvas);
            int i = (int)(p.Y / RowHeight);
            if (i < 0 || i >= _wiersze.Count) return;
            var w = _wiersze[i];
            var godz = ZaokraglijDo15(XToTime(p.X));
            if (MessageBox.Show($"Utworzyć nowy kurs dla „{w.PelneNazwisko}\" o {godz:hh\\:mm}?  ({zam.Count} zam.)",
                "Nowy kurs", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                int? kierId = w.BrakKierowcy ? (int?)null : w.KierowcaID;
                await Svc.UtworzKursIDodajAsync(_data, kierId, godz, zam, Uzytkownik);
                Zmieniono?.Invoke();
                await RenderAsync();
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void PokazDropHighlight(int i)
        {
            if (_dropHi == null)
            {
                _dropHi = new Rectangle
                {
                    Stroke = B("#00838F"),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    Fill = B("#1400838F"),
                    RadiusX = 6,
                    RadiusY = 6,
                    IsHitTestVisible = false
                };
                Panel.SetZIndex(_dropHi, 200);
                TimelineCanvas.Children.Add(_dropHi);
            }
            _dropHi.Width = HoursVisible * _pph;
            _dropHi.Height = RowHeight;
            Canvas.SetLeft(_dropHi, 0);
            Canvas.SetTop(_dropHi, i * RowHeight);
            _dropHi.Visibility = Visibility.Visible;
        }

        private void UsunDropHighlight()
        {
            if (_dropHi != null) _dropHi.Visibility = Visibility.Collapsed;
            DragStatusBar.Visibility = Visibility.Collapsed;
        }

        private static List<Models.WolneZamowienieWpf> Przeciagane(DragEventArgs e)
            => e.Data.GetData(WpfDragHelper.FmtWolne) as List<Models.WolneZamowienieWpf> ?? new();

        private static TimeSpan ZaokraglijDo15(TimeSpan t)
        {
            int total = (int)Math.Round(t.TotalMinutes / 15.0) * 15;
            total = Math.Max(0, Math.Min(23 * 60 + 45, total));
            return TimeSpan.FromMinutes(total);
        }

        // ── sterowanie / scroll ──
        private void MainScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0) HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
            if (e.VerticalChange != 0) LeftScroll.ScrollToVerticalOffset(e.VerticalOffset);
        }

        private async void ZoomSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _pph = e.NewValue;
            if (Svc != null && IsLoaded) await RenderAsync();
        }

        private async void ChkUkryjWolnych_Click(object sender, RoutedEventArgs e)
        {
            _ukryjWolnych = ChkUkryjWolnych.IsChecked == true;
            if (Svc != null) await RenderAsync();
        }

        private static SolidColorBrush B(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    }
}
