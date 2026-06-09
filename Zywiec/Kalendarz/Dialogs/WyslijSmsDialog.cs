using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Zywiec.Kalendarz.Services;

namespace Kalendarz1.Zywiec.Kalendarz.Dialogs
{
    // Dialog produkcyjny: podgląd numeru + treści PRZED wysłaniem SMS-a.
    // Funkcje:
    //  - Edycja numeru i treści
    //  - Historia ostatnich 3 SMS-ów do tego hodowcy (z bazy: SmsDostawySnapshot + ContactHistory)
    //  - Ostrzeżenie anti-spam gdy treść podobna do wcześniej wysłanej
    //  - Wysyłka przez telefon (MacroDroid /sms) lub fallback do schowka
    //
    // Sprawdzanie kontaktów telefonu (/check-contact) i historia SMS-ów z telefonu (/last-sms)
    // zostały usunięte — MacroDroid nie dawał na to wiarygodnej obsługi, a dane mamy w bazie.
    public class WyslijSmsDialog : Window
    {
        public bool SmsWyslanyPrzezTelefon { get; private set; }
        public bool TylkoSchowek { get; private set; }
        public string FinalNumer { get; private set; } = "";
        public string FinalTresc { get; private set; } = "";

        private TextBox _txtNumer = null!;
        private TextBox _txtTresc = null!;
        private TextBlock _lblZnaki = null!;
        private TextBlock _lblWynik = null!;
        private StackPanel _panelHistoria = null!;
        private Button _btnWyslij = null!;
        private Button _btnTylkoSchowek = null!;

        private readonly string _dostawcaInfo;
        private readonly string _dostawcaNazwa;
        private readonly string _connectionString;
        private readonly string _userId;

        public WyslijSmsDialog(string dostawcaInfo, string dostawcaNazwa, string poczatkowyTelefon,
                               string poczatkowaTresc, string connectionString, string? userId = null)
        {
            _dostawcaInfo = dostawcaInfo ?? "";
            _dostawcaNazwa = dostawcaNazwa ?? "";
            _connectionString = connectionString ?? "";
            _userId = userId ?? "";

            Title = "📱 Wyślij SMS o szczegółach dostawy";
            Width = 620;
            Height = 680;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            ResizeMode = ResizeMode.NoResize;

            Content = BudujUI(poczatkowyTelefon, poczatkowaTresc);

            // Historia wysłanych SMS-ów (z bazy, równolegle z otwarciem)
            Loaded += async (_, _) => await LadujHistorieAsync();
        }

        private FrameworkElement BudujUI(string poczatkowyTelefon, string poczatkowaTresc)
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 1. Header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xF2, 0xFE)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x0E, 0xA5, 0xE9)),
                BorderThickness = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            header.Child = new TextBlock
            {
                Text = "📦 " + _dostawcaInfo,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x07, 0x54, 0x85)),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // 2. Historia (wypełniona asynchronicznie)
            _panelHistoria = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(_panelHistoria, 1);
            root.Children.Add(_panelHistoria);

            // 3. Numer
            var numerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            numerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            numerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            numerGrid.Children.Add(new TextBlock
            {
                Text = "📞 Numer:",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            });

            _txtNumer = new TextBox
            {
                Text = poczatkowyTelefon ?? "",
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(_txtNumer, 1);
            numerGrid.Children.Add(_txtNumer);

            Grid.SetRow(numerGrid, 2);
            root.Children.Add(numerGrid);

            // 4. Treść SMS
            var trescPanel = new StackPanel();
            var treScHeader = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            treScHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            treScHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            treScHeader.Children.Add(new TextBlock
            {
                Text = "💬 Treść SMS-a:",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
            _lblZnaki = new TextBlock
            {
                Text = "",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
            };
            Grid.SetColumn(_lblZnaki, 1);
            treScHeader.Children.Add(_lblZnaki);

            trescPanel.Children.Add(treScHeader);
            _txtTresc = new TextBox
            {
                Text = poczatkowaTresc ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(8),
                FontSize = 12,
                MinHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            _txtTresc.TextChanged += (_, _) => OdswiezLicznikZnakow();
            trescPanel.Children.Add(_txtTresc);

            Grid.SetRow(trescPanel, 3);
            root.Children.Add(trescPanel);
            OdswiezLicznikZnakow();

            // 5. Pasek wyniku
            _lblWynik = new TextBlock
            {
                Text = "",
                FontSize = 11,
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(_lblWynik, 4);
            root.Children.Add(_lblWynik);

            // 6. Akcje
            var akcje = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnAnuluj = NowyPrzycisk("✖ Anuluj", "#94A3B8", false);
            btnAnuluj.Click += (_, _) => { DialogResult = false; Close(); };
            akcje.Children.Add(btnAnuluj);

            _btnTylkoSchowek = NowyPrzycisk("📋 Tylko schowek", "#64748B", false);
            _btnTylkoSchowek.Margin = new Thickness(8, 0, 0, 0);
            _btnTylkoSchowek.Click += (_, _) => SkopiujDoSchowka();
            akcje.Children.Add(_btnTylkoSchowek);

            _btnWyslij = NowyPrzycisk("📲 Wyślij przez telefon", "#10B981", true);
            _btnWyslij.Margin = new Thickness(8, 0, 0, 0);
            _btnWyslij.Click += async (_, _) => await WyslijAsync();
            akcje.Children.Add(_btnWyslij);

            Grid.SetRow(akcje, 5);
            root.Children.Add(akcje);

            return root;
        }

        private void OdswiezLicznikZnakow()
        {
            int len = _txtTresc.Text.Length;
            int sms = len == 0 ? 0 : (len <= 70 ? 1 : 1 + (int)Math.Ceiling((len - 70) / 67.0));
            _lblZnaki.Text = $"{len} znaków  •  ~{sms} SMS (UCS-2)";
        }

        // ===== HISTORIA WYSŁANYCH SMS-ÓW (TYLKO Z LOKALNEJ BAZY) =====
        private sealed class HistoriaWpis
        {
            public DateTime Kiedy { get; set; }
            public string Opis { get; set; } = "";
            public string Tresc { get; set; } = "";
            public string Zrodlo { get; set; } = "";
        }

        private async Task LadujHistorieAsync()
        {
            if (string.IsNullOrWhiteSpace(_dostawcaNazwa) || string.IsNullOrWhiteSpace(_connectionString)) return;
            var wpisy = new List<HistoriaWpis>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 5 CreatedAt,
                           CASE WHEN Wariant = 'aktualizacja' THEN 'SMS aktualizujący' ELSE 'SMS pierwszy' END AS Opis,
                           ISNULL(SmsText, '') AS Tresc,
                           'snapshot' AS Zrodlo
                    FROM dbo.SmsDostawySnapshot
                    WHERE Dostawca = @n

                    UNION ALL

                    SELECT TOP 5 CreatedAt,
                           ISNULL(Reason, 'SMS') AS Opis,
                           '' AS Tresc,
                           'contact' AS Zrodlo
                    FROM dbo.ContactHistory
                    WHERE Dostawca = @n
                      AND (Reason LIKE 'Auto SMS%' OR Reason LIKE '%SMS%' OR Reason LIKE 'Potwierdzenie%')

                    ORDER BY CreatedAt DESC", conn) { CommandTimeout = 5 };
                cmd.Parameters.AddWithValue("@n", _dostawcaNazwa);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    wpisy.Add(new HistoriaWpis
                    {
                        Kiedy = r.GetDateTime(0),
                        Opis = r.IsDBNull(1) ? "" : r.GetString(1),
                        Tresc = r.IsDBNull(2) ? "" : r.GetString(2),
                        Zrodlo = r.IsDBNull(3) ? "" : r.GetString(3)
                    });
                }
            }
            catch { return; }

            var top3 = wpisy.OrderByDescending(w => w.Kiedy).Take(3).ToList();
            if (top3.Count == 0) return;

            string aktualnaTresc = _txtTresc.Text.Trim();
            string aktualnaTresc50 = NormalizujDoPorownania(aktualnaTresc);
            if (aktualnaTresc50.Length > 50) aktualnaTresc50 = aktualnaTresc50.Substring(0, 50);

            await Dispatcher.InvokeAsync(() =>
            {
                bool jestDuplikat = top3.Any(w =>
                {
                    if (string.IsNullOrEmpty(aktualnaTresc50)) return false;
                    string norm = NormalizujDoPorownania(w.Tresc);
                    return norm.Length >= aktualnaTresc50.Length
                        && norm.Substring(0, aktualnaTresc50.Length) == aktualnaTresc50;
                });

                var box = new Border
                {
                    Background = jestDuplikat
                        ? new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2))
                        : new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB)),
                    BorderBrush = jestDuplikat
                        ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                        : new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)),
                    BorderThickness = new Thickness(jestDuplikat ? 2 : 1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 6, 8, 6)
                };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock
                {
                    Text = jestDuplikat
                        ? $"⚠ UWAGA — podobny SMS już wysłano do {_dostawcaNazwa}!"
                        : $"📜 Ostatnie {top3.Count} SMS-y do {_dostawcaNazwa}:",
                    FontSize = jestDuplikat ? 11 : 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = jestDuplikat
                        ? new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B))
                        : new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63)),
                    Margin = new Thickness(0, 0, 0, 4),
                    TextWrapping = TextWrapping.Wrap
                });

                foreach (var w in top3)
                {
                    string ikona = w.Zrodlo switch
                    {
                        "snapshot" => "📲",
                        "contact" => "📝",
                        _ => "•"
                    };
                    string trescSkrocona = string.IsNullOrEmpty(w.Tresc)
                        ? w.Opis
                        : (w.Tresc.Length > 60 ? w.Tresc.Substring(0, 57) + "..." : w.Tresc).Replace("\n", " ").Replace("\r", "");
                    var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                    line.Children.Add(new TextBlock
                    {
                        Text = $"{ikona} {w.Kiedy:dd.MM HH:mm}",
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)),
                        Width = 90
                    });
                    line.Children.Add(new TextBlock
                    {
                        Text = trescSkrocona,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                        TextWrapping = TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 460,
                        ToolTip = w.Tresc
                    });
                    sp.Children.Add(line);
                }

                box.Child = sp;
                _panelHistoria.Children.Add(box);
            });
        }

        private static string NormalizujDoPorownania(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            bool lastWasSpace = false;
            foreach (char c in s.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) { sb.Append(c); lastWasSpace = false; }
                else if (char.IsWhiteSpace(c) || c == ',' || c == '.' || c == '-')
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            return sb.ToString().Trim();
        }

        // ===== WYSŁANIE SMS-A =====
        private async Task WyslijAsync()
        {
            string numer = _txtNumer.Text.Trim();
            string tresc = _txtTresc.Text.Trim();

            if (string.IsNullOrEmpty(numer)) { PokazWynik("⚠ Wpisz numer telefonu hodowcy.", "#DC2626"); return; }
            if (string.IsNullOrEmpty(tresc)) { PokazWynik("⚠ Treść SMS-a nie może być pusta.", "#DC2626"); return; }

            FinalNumer = numer;
            FinalTresc = tresc;

            _btnWyslij.IsEnabled = false;
            _btnTylkoSchowek.IsEnabled = false;
            PokazWynik("⏳ Wysyłam przez telefon...", "#1F2937");

            var wynik = await MacroDroidClient.WyslijSmsAsync(_userId, numer, tresc);
            if (wynik.Sukces)
            {
                SmsWyslanyPrzezTelefon = true;
                PokazWynik($"✅ SMS wysłany przez telefon do {numer}", "#15803D");
                await Task.Delay(700);
                DialogResult = true;
                Close();
            }
            else
            {
                PokazWynik($"❌ {wynik.Komunikat}\nUżyj 'Tylko schowek' jako fallback.", "#DC2626");
                _btnWyslij.IsEnabled = true;
                _btnTylkoSchowek.IsEnabled = true;
            }
        }

        private void SkopiujDoSchowka()
        {
            FinalNumer = _txtNumer.Text.Trim();
            FinalTresc = _txtTresc.Text.Trim();
            if (string.IsNullOrEmpty(FinalTresc)) { PokazWynik("⚠ Treść pusta — nic do skopiowania.", "#DC2626"); return; }
            try
            {
                Clipboard.SetText(FinalTresc);
                TylkoSchowek = true;
                PokazWynik("📋 Skopiowano do schowka. Wklej w aplikacji telefonu.", "#1E40AF");
                Task.Delay(700).ContinueWith(_ => Dispatcher.Invoke(() => { DialogResult = true; Close(); }));
            }
            catch (Exception ex) { PokazWynik($"❌ Błąd schowka: {ex.Message}", "#DC2626"); }
        }

        private void PokazWynik(string tekst, string kolorHex)
        {
            _lblWynik.Text = tekst;
            _lblWynik.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorHex));
        }

        private static Button NowyPrzycisk(string tekst, string kolorHex, bool primary)
        {
            return new Button
            {
                Content = tekst,
                Padding = new Thickness(14, 6, 14, 6),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorHex)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = primary ? FontWeights.Bold : FontWeights.SemiBold,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                MinWidth = primary ? 180 : 120
            };
        }

        // Delegacja do MacroDroidClient — kompatybilność z istniejącymi wywołaniami
        public static bool CzyTelefonSkonfigurowany(string? userId)
            => MacroDroidClient.CzyTelefonSkonfigurowany(userId);
    }
}
