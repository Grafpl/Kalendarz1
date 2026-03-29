using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1.MapaFloty
{
    public partial class SyncKursowWindow : Window
    {
        private readonly WebfleetOrderService _svc = new();
        private List<WebfleetOrderService.SyncStatus> _kursy = new();

        public SyncKursowWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            DatePick.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await LoadKursy();
        }

        private void DatePick_Changed(object? s, SelectionChangedEventArgs e) => _ = LoadKursy();
        private void BtnRefresh_Click(object s, RoutedEventArgs e) => _ = LoadKursy();

        private async Task LoadKursy()
        {
            try
            {
                StatusText.Text = "Ładowanie kursów...";
                var date = DatePick.SelectedDate ?? DateTime.Today;
                _kursy = await _svc.PobierzStatusySyncAsync(date);
                BuildList();
                TotalCount.Text = _kursy.Count.ToString();
                SentCount.Text = _kursy.Count(k => k.SyncStatusText == "Wyslany").ToString();
                PendingCount.Text = _kursy.Count(k => k.SyncStatusText == "Nie wysłany").ToString();
                ErrorCount.Text = _kursy.Count(k => k.SyncStatusText == "Blad").ToString();
                StatusText.Text = $"Załadowano {_kursy.Count} kursów z {date:dd.MM.yyyy}";
            }
            catch (Exception ex) { StatusText.Text = $"Błąd: {ex.Message}"; }
        }

        private void BuildList()
        {
            KursyList.Children.Clear();
            // Nagłówek
            var header = MakeRow("Godz.", "Trasa", "Kierowca", "Pojazd", "Przystanki", "Status Webfleet", "", true);
            KursyList.Children.Add(header);

            foreach (var k in _kursy)
            {
                var row = new Border
                {
                    Padding = new Thickness(14, 10, 14, 10),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 244)),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                row.MouseEnter += (_, _) => row.Background = new SolidColorBrush(Color.FromRgb(248, 249, 253));
                row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });    // Godz
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Trasa
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });   // Kierowca
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });    // Pojazd
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });    // Przystanki
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });   // Status
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });   // Akcje

                void AddCell(int col, string text, Color? color = null, FontWeight? fw = null)
                {
                    var tb = new TextBlock
                    {
                        Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = new SolidColorBrush(color ?? Color.FromRgb(38, 50, 56)),
                        FontWeight = fw ?? FontWeights.Normal
                    };
                    Grid.SetColumn(tb, col);
                    grid.Children.Add(tb);
                }

                AddCell(0, k.GodzWyjazdu, Color.FromRgb(84, 110, 122), FontWeights.SemiBold);
                AddCell(1, k.Trasa, null, FontWeights.SemiBold);
                AddCell(2, k.Kierowca, Color.FromRgb(96, 125, 139));
                AddCell(3, k.Rejestracja, Color.FromRgb(96, 125, 139));
                AddCell(4, k.LadunkiCount.ToString(), Color.FromRgb(57, 73, 171), FontWeights.Bold);

                // Status pill
                var statusColor = k.SyncStatusText switch
                {
                    "Wyslany" => (bg: Color.FromRgb(232, 245, 233), fg: Color.FromRgb(46, 125, 50)),
                    "Blad" => (bg: Color.FromRgb(255, 235, 238), fg: Color.FromRgb(198, 40, 40)),
                    "Anulowany" => (bg: Color.FromRgb(245, 245, 245), fg: Color.FromRgb(120, 120, 120)),
                    _ => (bg: Color.FromRgb(255, 243, 224), fg: Color.FromRgb(230, 81, 0))
                };
                var statusLabel = k.SyncStatusText switch
                {
                    "Wyslany" => "Wysłany do Webfleet",
                    "Blad" => $"Błąd: {k.Blad?[..Math.Min(k.Blad?.Length ?? 0, 30)]}",
                    "Anulowany" => "Anulowany",
                    _ => "Nie wysłany"
                };
                var pillBorder = new Border
                {
                    Background = new SolidColorBrush(statusColor.bg),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                pillBorder.Child = new TextBlock
                {
                    Text = statusLabel, FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(statusColor.fg)
                };
                Grid.SetColumn(pillBorder, 5);
                grid.Children.Add(pillBorder);

                // Przyciski akcji
                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var kursId = k.KursID;

                if (k.SyncStatusText == "Nie wysłany" || k.SyncStatusText == "Blad")
                {
                    var btnSend = MakeBtn("Wyślij", "#1565c0", Colors.White);
                    btnSend.Click += async (_, _) => await SendKurs(kursId);
                    btnPanel.Children.Add(btnSend);
                }

                if (k.SyncStatusText == "Wyslany")
                {
                    var wfOrderId = k.WebfleetOrderId;

                    var btnStatus = MakeBtn("Status", "#00695c", Colors.White);
                    btnStatus.Click += async (_, _) => await ShowOrderStatus(wfOrderId);
                    btnPanel.Children.Add(btnStatus);

                    var btnUpdate = MakeBtn("Aktualizuj", "#e65100", Colors.White);
                    btnUpdate.Click += async (_, _) => await UpdateKurs(kursId);
                    btnPanel.Children.Add(btnUpdate);

                    var btnCancel = MakeBtn("Anuluj", "#c62828", Colors.White);
                    btnCancel.Click += async (_, _) => await CancelKurs(kursId);
                    btnPanel.Children.Add(btnCancel);
                }

                Grid.SetColumn(btnPanel, 6);
                grid.Children.Add(btnPanel);

                row.Child = grid;
                KursyList.Children.Add(row);
            }
        }

        private async Task SendKurs(long kursId)
        {
            try
            {
                StatusText.Text = $"Wysyłanie kursu {kursId}...";
                var result = await _svc.WyslijKursAsync(kursId, App.UserFullName ?? "system");
                if (result.Success)
                {
                    StatusText.Text = $"Kurs {kursId} wysłany do Webfleet ({result.StopsCount} przystanków) — ID: {result.OrderId}";
                    MessageBox.Show($"Kurs wysłany pomyślnie!\n\nWebfleet Order ID: {result.OrderId}\nPrzystanki: {result.StopsCount}",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var errDetail = $"Błąd wysyłania kursu {kursId} do Webfleet\n\n" +
                        $"Kod błędu: {result.ErrorCode}\n" +
                        $"Komunikat: {result.ErrorMessage}\n" +
                        $"Order ID: {result.OrderId}\n" +
                        $"Czas: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    StatusText.Text = $"Błąd: {result.ErrorCode} — {result.ErrorMessage}";
                    var resp = MessageBox.Show(errDetail + "\n\nSkopiować do schowka?",
                        "Błąd Webfleet", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (resp == MessageBoxResult.Yes)
                        Clipboard.SetText(errDetail);
                }
                await LoadKursy();
            }
            catch (WebfleetOrderService.NeedAddressException nae)
            {
                StatusText.Text = "Brak adresów klientów — uzupełnij adresy";
                var resp = MessageBox.Show(
                    "Następujący klienci nie mają adresów:\n\n" +
                    string.Join("\n", nae.BrakujaceKody.Select(k => $"  • {k}")) +
                    "\n\nCzy chcesz teraz uzupełnić adresy?",
                    "Brak adresów", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (resp == MessageBoxResult.Yes)
                {
                    var dlg = new AdresyKlientowWindow(nae.BrakujaceKody) { Owner = this };
                    if (dlg.ShowDialog() == true)
                        await SendKurs(kursId); // ponów automatycznie po uzupełnieniu adresów
                }
            }
            catch (Exception ex)
            {
                var errDetail = $"Błąd wysyłania kursu {kursId}\n\n{ex.GetType().Name}: {ex.Message}\n\nStack:\n{ex.StackTrace}";
                StatusText.Text = $"Błąd: {ex.Message}";
                var resp = MessageBox.Show(errDetail + "\n\nSkopiować do schowka?",
                    "Błąd", MessageBoxButton.YesNo, MessageBoxImage.Error);
                if (resp == MessageBoxResult.Yes)
                    Clipboard.SetText(errDetail);
            }
        }

        private async Task UpdateKurs(long kursId)
        {
            try
            {
                StatusText.Text = $"Aktualizacja kursu {kursId} w Webfleet...";
                var result = await _svc.AktualizujZlecenieAsync(kursId, App.UserFullName ?? "system");
                if (result.Success)
                {
                    StatusText.Text = $"Kurs {kursId} zaktualizowany w Webfleet";
                    MessageBox.Show($"Zlecenie zaktualizowane w Webfleet.\n\nOrder ID: {result.OrderId}\nPrzystanki: {result.StopsCount}",
                        "Zaktualizowano", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var err = $"Błąd aktualizacji: {result.ErrorCode}: {result.ErrorMessage}";
                    StatusText.Text = err;
                    var resp = MessageBox.Show(err + "\n\nSkopiować do schowka?", "Błąd", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (resp == MessageBoxResult.Yes) Clipboard.SetText(err);
                }
                await LoadKursy();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
                MessageBox.Show(ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ShowOrderStatus(string? orderId)
        {
            if (string.IsNullOrEmpty(orderId)) { StatusText.Text = "Brak Order ID"; return; }
            try
            {
                StatusText.Text = $"Pobieranie statusu {orderId}...";
                var info = await _svc.PobierzStatusZleceniaAsync(orderId);
                if (info == null) { MessageBox.Show("Nie znaleziono zlecenia w Webfleet.", "Brak danych"); return; }

                var detail = $"ZLECENIE WEBFLEET: {info.OrderId}\n" +
                    $"{'='}\n\n" +
                    $"Status: {info.StatusNazwa} (kod {info.OrderState})\n" +
                    $"Czas zmiany statusu: {info.OrderStateTime}\n\n" +
                    $"Pojazd: {info.ObjectName}\n" +
                    $"Kierowca: {info.DriverName}\n\n" +
                    $"Adres dostawy: {info.Street}, {info.City}\n" +
                    $"ETA: {(string.IsNullOrEmpty(info.EstimatedArrival) ? "brak" : info.EstimatedArrival)}\n" +
                    $"Opóźnienie: {info.OpoznienieNazwa}\n\n" +
                    $"Treść zlecenia:\n{info.OrderText}";

                var resp = MessageBox.Show(detail + "\n\nSkopiować do schowka?",
                    $"Status zlecenia — {info.StatusNazwa}", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (resp == MessageBoxResult.Yes) Clipboard.SetText(detail);
                StatusText.Text = $"Status: {info.StatusNazwa} | ETA: {info.EstimatedArrival} | Opóźnienie: {info.OpoznienieNazwa}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd: {ex.Message}";
                MessageBox.Show(ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CancelKurs(long kursId)
        {
            if (MessageBox.Show("Anulować zlecenie w Webfleet?", "Potwierdzenie",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                await _svc.AnulujZlecenieAsync(kursId);
                StatusText.Text = $"Kurs {kursId} anulowany w Webfleet";
                await LoadKursy();
            }
            catch (Exception ex) { StatusText.Text = $"Błąd: {ex.Message}"; }
        }

        // ── UI Helpers ──────────────────────────────────────────────────

        private Border MakeRow(string c1, string c2, string c3, string c4, string c5, string c6, string c7, bool isHeader)
        {
            var border = new Border
            {
                Padding = new Thickness(14, 8, 14, 8),
                Background = new SolidColorBrush(isHeader ? Color.FromRgb(247, 248, 250) : Colors.Transparent),
                BorderBrush = new SolidColorBrush(Color.FromRgb(228, 230, 234)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            var texts = new[] { c1, c2, c3, c4, c5, c6 };
            for (int i = 0; i < texts.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text = texts[i], FontSize = isHeader ? 10.5 : 12,
                    Foreground = new SolidColorBrush(isHeader ? Color.FromRgb(84, 110, 122) : Color.FromRgb(38, 50, 56)),
                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(tb, i);
                grid.Children.Add(tb);
            }
            border.Child = grid;
            return border;
        }

        private Button MakeBtn(string text, string bgHex, Color fg)
        {
            var c = (Color)ColorConverter.ConvertFromString(bgHex);
            var btn = new Button
            {
                Content = text, Background = new SolidColorBrush(c), Foreground = new SolidColorBrush(fg),
                BorderThickness = new Thickness(0), Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand, FontSize = 10.5, FontWeight = FontWeights.SemiBold
            };
            btn.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>" +
                "<Border Background='{TemplateBinding Background}' CornerRadius='5' Padding='{TemplateBinding Padding}'>" +
                "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>" +
                "</Border></ControlTemplate>");
            return btn;
        }
    }
}
