using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.Zywiec.Kalendarz.Dialogs
{
    // Dialog testowy do wysłania SMS-a przez webhook na telefonie (MacroDroid).
    // ŻADEN zapis do bazy danych — tylko test komunikacji HTTP → telefon → SMS.
    // Ustawienia (IP, port, token, ostatni numer testowy) zapisywane w
    // %LOCALAPPDATA%\Kalendarz1\sms-telefon.json.
    public class TestSmsDialog : Window
    {
        private TextBox _txtIp = null!;
        private TextBox _txtPort = null!;
        private TextBox _txtToken = null!;
        private TextBox _txtNumer = null!;
        private TextBox _txtTresc = null!;
        private TextBlock _lblWynik = null!;
        private Button _btnWyslij = null!;

        // Per-user settings — każdy operator ma własny plik z IP swojego telefonu
        public static string GetSettingsPath(string? userId)
        {
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Kalendarz1");
            // userId pusty → wspólny plik (legacy / fallback dla niezalogowanych)
            string fileName = string.IsNullOrWhiteSpace(userId)
                ? "sms-telefon.json"
                : $"sms-telefon-{userId}.json";
            return Path.Combine(baseDir, fileName);
        }

        private readonly string _userId;
        private SmsTelefonSettings _settings;

        public TestSmsDialog(string? dostawaInfo = null, string? sugerowanaTresc = null, string? userId = null)
        {
            _userId = userId ?? "";
            _settings = WczytajUstawienia(_userId);

            Title = "🧪 TEST SMS przez telefon (bez zapisu do bazy)";
            Width = 540;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9));
            ResizeMode = ResizeMode.NoResize;

            Content = BudujUI(dostawaInfo, sugerowanaTresc);
        }

        private FrameworkElement BudujUI(string? dostawaInfo, string? sugerowanaTresc)
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Banner ostrzegawczy
            var banner = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };
            string userInfo = string.IsNullOrEmpty(_userId)
                ? ""
                : $"\n👤 Konfiguracja dla użytkownika: ID = {_userId} (każdy user ma własne IP telefonu)";
            banner.Child = new TextBlock
            {
                Text = "🧪 TRYB TESTOWY — żadna wiadomość NIE zostanie zapisana w bazie ani w historii notatek. " +
                       "Sprawdzasz tylko czy webhook telefonu działa." + userInfo,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x35, 0x0F)),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(banner, 0);
            root.Children.Add(banner);

            // Konfiguracja webhook
            var grpConfig = NowyGroupBox("⚙️ Konfiguracja webhook (MacroDroid)");
            var configGrid = new Grid();
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            configGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 3; i++)
                configGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            DodajPole(configGrid, 0, "IP telefonu:", out _txtIp, _settings.Ip);
            _txtIp.ToolTip = "Stały IP telefonu w sieci biurowej (DHCP Reservation w UniFi)";
            DodajPole(configGrid, 1, "Port:", out _txtPort, _settings.Port.ToString());
            _txtPort.ToolTip = "Port nasłuchu MacroDroid (domyślnie 8765)";
            DodajPole(configGrid, 2, "Token (opc.):", out _txtToken, _settings.AuthToken);
            _txtToken.ToolTip = "Opcjonalny klucz autoryzacji w nagłówku X-Auth-Token";

            grpConfig.Content = configGrid;
            grpConfig.Margin = new Thickness(0, 0, 0, 10);
            Grid.SetRow(grpConfig, 1);
            root.Children.Add(grpConfig);

            // Treść SMS-a
            var grpSms = NowyGroupBox("📱 Treść SMS-a testowego");
            var smsStack = new StackPanel();
            var smsGrid = new Grid();
            smsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            smsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            smsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DodajPole(smsGrid, 0, "Numer testowy:", out _txtNumer, _settings.LastTestPhone);
            _txtNumer.ToolTip = "Numer na który wyślemy testowy SMS (najlepiej swój własny)";
            smsStack.Children.Add(smsGrid);

            _txtTresc = new TextBox
            {
                Text = sugerowanaTresc ?? "🧪 Test SMS z aplikacji ZPSP " + DateTime.Now.ToString("HH:mm:ss"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 100,
                Padding = new Thickness(6),
                Margin = new Thickness(0, 6, 0, 0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            smsStack.Children.Add(new TextBlock { Text = "Treść:", Margin = new Thickness(0, 6, 0, 2), FontWeight = FontWeights.SemiBold, FontSize = 11 });
            smsStack.Children.Add(_txtTresc);

            if (!string.IsNullOrEmpty(dostawaInfo))
            {
                smsStack.Children.Add(new TextBlock
                {
                    Text = "ℹ️ Wybrana dostawa: " + dostawaInfo,
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                    Margin = new Thickness(0, 6, 0, 0)
                });
            }

            grpSms.Content = smsStack;
            Grid.SetRow(grpSms, 2);
            root.Children.Add(grpSms);

            // Pasek wyniku
            _lblWynik = new TextBlock
            {
                Text = "",
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11
            };
            Grid.SetRow(_lblWynik, 3);
            root.Children.Add(_lblWynik);

            // Akcje
            var akcje = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            _btnWyslij = NowyPrzycisk("📲 Wyślij testowy SMS", "#10B981");
            _btnWyslij.Click += async (_, _) => await WyslijAsync();
            akcje.Children.Add(_btnWyslij);

            var btnZamknij = NowyPrzycisk("✖ Zamknij", "#64748B");
            btnZamknij.Margin = new Thickness(8, 0, 0, 0);
            btnZamknij.Click += (_, _) => Close();
            akcje.Children.Add(btnZamknij);

            Grid.SetRow(akcje, 4);
            root.Children.Add(akcje);

            return root;
        }

        private static GroupBox NowyGroupBox(string header) => new GroupBox
        {
            Header = header,
            Padding = new Thickness(10),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12
        };

        private static void DodajPole(Grid grid, int row, string etykieta, out TextBox txt, string wartosc)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var label = new TextBlock
            {
                Text = etykieta,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 8, 4),
                FontSize = 11
            };
            Grid.SetRow(label, row); Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            txt = new TextBox
            {
                Text = wartosc ?? "",
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 4, 0, 4),
                FontSize = 11
            };
            Grid.SetRow(txt, row); Grid.SetColumn(txt, 1);
            grid.Children.Add(txt);
        }

        private static Button NowyPrzycisk(string tekst, string kolorHex)
        {
            return new Button
            {
                Content = tekst,
                Padding = new Thickness(14, 6, 14, 6),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorHex)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        // ===== WYSŁANIE TESTOWEGO HTTP POST =====
        private async Task WyslijAsync()
        {
            string ip = _txtIp.Text.Trim();
            string portStr = _txtPort.Text.Trim();
            string token = _txtToken.Text.Trim();
            string numer = _txtNumer.Text.Trim();
            string tresc = _txtTresc.Text.Trim();

            if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(portStr) ||
                string.IsNullOrEmpty(numer) || string.IsNullOrEmpty(tresc))
            {
                PokazWynik("⚠ Wypełnij wszystkie pola (token opcjonalny).", "#DC2626");
                return;
            }
            if (!int.TryParse(portStr, out int port) || port < 1 || port > 65535)
            {
                PokazWynik("⚠ Port musi być liczbą 1-65535.", "#DC2626");
                return;
            }

            // Zapisz ustawienia per-user żeby się nie powtarzało
            _settings.Ip = ip;
            _settings.Port = port;
            _settings.AuthToken = token;
            _settings.LastTestPhone = numer;
            ZapiszUstawienia(_settings, _userId);

            _btnWyslij.IsEnabled = false;
            PokazWynik("⏳ Wysyłam request...", "#1F2937");

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                if (!string.IsNullOrEmpty(token))
                    http.DefaultRequestHeaders.Add("X-Auth-Token", token);

                // Format JSON kompatybilny ze standardowymi makrami MacroDroid
                var json = JsonSerializer.Serialize(new { phone = numer, text = tresc });
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                string url = $"http://{ip}:{port}/sms";
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var resp = await http.PostAsync(url, content);
                sw.Stop();

                string body = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode)
                {
                    PokazWynik(
                        $"✅ HTTP {(int)resp.StatusCode} {resp.StatusCode}  •  {sw.ElapsedMilliseconds} ms\n" +
                        $"Telefon przyjął request. Sprawdź na docelowym numerze czy SMS dotarł.\n\n" +
                        $"Odpowiedź: {(string.IsNullOrEmpty(body) ? "(pusta)" : body)}",
                        "#15803D");
                }
                else
                {
                    PokazWynik(
                        $"⚠ HTTP {(int)resp.StatusCode} {resp.StatusCode}  •  {sw.ElapsedMilliseconds} ms\n" +
                        $"Telefon odpowiedział błędem.\n\n" +
                        $"Odpowiedź: {(string.IsNullOrEmpty(body) ? "(pusta)" : body)}",
                        "#B45309");
                }
            }
            catch (TaskCanceledException)
            {
                PokazWynik(
                    "❌ TIMEOUT (8s) — telefon nie odpowiada.\n\n" +
                    $"Sprawdź:\n" +
                    $"• Czy IP {ip} jest prawidłowy?\n" +
                    $"• Czy port {portStr} jest otwarty w MacroDroid?\n" +
                    $"• Czy jesteście w tej samej sieci (Wi-Fi lub Teleport)?\n" +
                    $"• Czy MacroDroid działa w tle?",
                    "#DC2626");
            }
            catch (HttpRequestException ex)
            {
                PokazWynik(
                    $"❌ Błąd połączenia: {ex.Message}\n\n" +
                    $"Sprawdź czy adres http://{ip}:{port}/sms jest osiągalny z tego komputera.",
                    "#DC2626");
            }
            catch (Exception ex)
            {
                PokazWynik($"❌ Błąd: {ex.GetType().Name}\n{ex.Message}", "#DC2626");
            }
            finally
            {
                _btnWyslij.IsEnabled = true;
            }
        }

        private void PokazWynik(string tekst, string kolorHex)
        {
            _lblWynik.Text = tekst;
            _lblWynik.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorHex));
        }

        // ===== USTAWIENIA — zapisywane lokalnie =====
        public class SmsTelefonSettings
        {
            public string Ip { get; set; } = "192.168.1.50";
            public int Port { get; set; } = 8765;
            public string AuthToken { get; set; } = "";
            public string LastTestPhone { get; set; } = "";
        }

        public static SmsTelefonSettings WczytajUstawienia(string? userId)
        {
            string path = GetSettingsPath(userId);
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var s = JsonSerializer.Deserialize<SmsTelefonSettings>(json);
                    if (s != null) return s;
                }
                // Fallback do wspólnego pliku jeśli per-user nie istnieje (pierwsze uruchomienie)
                string fallback = GetSettingsPath(null);
                if (!string.IsNullOrEmpty(userId) && File.Exists(fallback))
                {
                    string json = File.ReadAllText(fallback);
                    var s = JsonSerializer.Deserialize<SmsTelefonSettings>(json);
                    if (s != null) return s;
                }
            }
            catch { }
            return new SmsTelefonSettings();
        }

        public static void ZapiszUstawienia(SmsTelefonSettings s, string? userId)
        {
            string path = GetSettingsPath(userId);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
