using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.CentrumNagranAI.Services;

namespace Kalendarz1.CentrumNagranAI.Views
{
    public partial class NvrEditorWindow : Window
    {
        public class NvrVm : INotifyPropertyChanged
        {
            public string Name { get; set; } = "NVR";
            public string Host { get; set; } = string.Empty;
            public int RtspPort { get; set; } = 554;
            public string User { get; set; } = "admin";
            public string Password { get; set; } = string.Empty;
            public List<int> Channels { get; set; } = new();
            public int DefaultStreamType { get; set; }
            public string ChannelsLabel => Channels.Count == 0 ? "brak kanałów" :
                                           Channels.Count <= 6 ? string.Join(",", Channels) :
                                           $"{Channels.Count} kanałów";
            public event PropertyChangedEventHandler? PropertyChanged;
            public void Refresh()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Host)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RtspPort)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ChannelsLabel)));
            }
        }

        public bool ChangesSaved { get; private set; }

        private readonly ObservableCollection<NvrVm> _nvry = new();
        private NvrVm? _editing;

        public NvrEditorWindow()
        {
            InitializeComponent();
            CnaConfig.ZaladujJesliTrzeba();

            foreach (var def in CnaConfig.WczytajNvry())
            {
                _nvry.Add(new NvrVm
                {
                    Name = def.Name,
                    Host = def.Host,
                    RtspPort = def.RtspPort,
                    User = def.User,
                    Password = def.Password,
                    Channels = new List<int>(def.Channels),
                    DefaultStreamType = def.DefaultStreamType
                });
            }
            NvrList.ItemsSource = _nvry;
            if (_nvry.Count > 0) NvrList.SelectedIndex = 0;
        }

        private void NvrList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _editing = NvrList.SelectedItem as NvrVm;
            if (_editing == null) { EditorPanel.IsEnabled = false; return; }
            EditorPanel.IsEnabled = true;
            NameBox.Text = _editing.Name;
            HostBox.Text = _editing.Host;
            PortBox.Text = _editing.RtspPort.ToString();
            UserBox.Text = _editing.User;
            PasswordBox.Password = _editing.Password;
            ChannelsBox.Text = FormatChannels(_editing.Channels);
            StreamCombo.SelectedIndex = Math.Max(0, Math.Min(1, _editing.DefaultStreamType));
            TestStatus.Text = string.Empty;
        }

        private void NewNvr_Click(object sender, RoutedEventArgs e)
        {
            var nv = new NvrVm
            {
                Name = $"NVR{_nvry.Count + 1}",
                Host = "192.168.0.",
                RtspPort = 554,
                User = "admin",
                Password = string.Empty,
                Channels = new List<int> { 1, 2, 3, 4 },
                DefaultStreamType = 0
            };
            _nvry.Add(nv);
            NvrList.SelectedItem = nv;
        }

        private void DeleteNvr_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) return;
            if (MessageBox.Show($"Usunąć NVR '{_editing.Name}'?", "Potwierdź",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _nvry.Remove(_editing);
            _editing = null;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) return;
            if (!TryReadEditor(out string err)) { TestStatus.Text = "❌ " + err; return; }
            _editing.Refresh();
            BottomStatus.Text = $"✓ Zmiany w '{_editing.Name}' zastosowane (kliknij 'Zapisz wszystko' żeby zapisać do pliku)";
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadEditor(out string err)) { TestStatus.Text = "❌ " + err; return; }
            if (_editing == null) return;

            TestBtn.IsEnabled = false;
            TestStatus.Text = "🔌 Łączę z NVR...";

            try
            {
                bool onvifOk = await TestOnvifAsync(_editing.Host);
                TestStatus.Text = onvifOk ? "✓ ONVIF na :80 odpowiada" : "⚠ ONVIF na :80 nie odpowiada (NVR może nie wspierać)";

                int firstChannel = _editing.Channels.FirstOrDefault();
                if (firstChannel == 0) { TestStatus.Text += "\n⚠ Brak kanałów do testu RTSP"; return; }

                var kamera = new CnaCameraEndpoint
                {
                    Id = $"{_editing.Name}-CH{firstChannel:D2}",
                    Host = _editing.Host,
                    RtspPort = _editing.RtspPort,
                    User = _editing.User,
                    Password = _editing.Password,
                    Channel = firstChannel,
                    StreamType = _editing.DefaultStreamType
                };

                string testFile = Path.Combine(CnaConfig.FramesDir, "_test", $"{_editing.Name}_ch{firstChannel}.jpg");
                await RtspFrameGrabber.ZapiszKlatkeAsync(kamera, testFile, timeoutSekund: 15);
                long size = new FileInfo(testFile).Length;
                TestStatus.Text += $"\n✓ RTSP kanał {firstChannel}: pobrano klatkę {size / 1024} KB";
            }
            catch (Exception ex)
            {
                TestStatus.Text += $"\n❌ {ex.Message.Split('\n')[0]}";
            }
            finally { TestBtn.IsEnabled = true; }
        }

        private static async Task<bool> TestOnvifAsync(string host)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var resp = await http.GetAsync($"http://{host}/onvif/device_service");
                return resp.IsSuccessStatusCode || (int)resp.StatusCode == 405; // 405 = method not allowed (POST only) = endpoint istnieje
            }
            catch { return false; }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            // Zastosuj edytor jeśli były zmiany
            if (_editing != null) { TryReadEditor(out _); }

            // Walidacja: każdy NVR musi mieć Host, User, Password, min 1 kanał.
            foreach (var n in _nvry)
            {
                if (string.IsNullOrWhiteSpace(n.Host) || string.IsNullOrWhiteSpace(n.User)
                    || string.IsNullOrEmpty(n.Password) || n.Channels.Count == 0)
                {
                    MessageBox.Show($"NVR '{n.Name}' niekompletny - host/login/hasło/kanały muszą być uzupełnione.",
                        "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NvrList.SelectedItem = n;
                    return;
                }
            }

            try
            {
                var defs = _nvry.Select(n => new CnaConfig.NvrDefinition
                {
                    Name = n.Name, Host = n.Host, RtspPort = n.RtspPort,
                    User = n.User, Password = n.Password,
                    Channels = new List<int>(n.Channels),
                    DefaultStreamType = n.DefaultStreamType
                }).ToList();
                CnaConfig.ZapiszNvry(defs);
                ChangesSaved = true;
                BottomStatus.Text = "✓ Zapisane do secrets.json. Restartuj indekser żeby nowe kamery weszły.";
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryReadEditor(out string err)
        {
            err = string.Empty;
            if (_editing == null) { err = "Nic nie zaznaczone"; return false; }

            var name = (NameBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) { err = "Nazwa pusta"; return false; }
            if (!Regex.IsMatch(name, @"^[A-Za-z0-9_\-]+$")) { err = "Nazwa: tylko litery/cyfry/_/- (używana jako prefix Id kamery)"; return false; }
            var host = (HostBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(host)) { err = "Host pusty"; return false; }
            if (!int.TryParse(PortBox.Text, out int port) || port < 1 || port > 65535) { err = "Port niepoprawny (1-65535)"; return false; }
            var user = (UserBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(user)) { err = "Login pusty"; return false; }
            var pass = PasswordBox.Password;
            if (string.IsNullOrEmpty(pass)) { err = "Hasło puste"; return false; }
            var channels = ParseChannels(ChannelsBox.Text);
            if (channels.Count == 0) { err = "Brak kanałów (np. '1,2,3' albo '1-4')"; return false; }

            _editing.Name = name;
            _editing.Host = host;
            _editing.RtspPort = port;
            _editing.User = user;
            _editing.Password = pass;
            _editing.Channels = channels;
            _editing.DefaultStreamType = StreamCombo.SelectedIndex;
            return true;
        }

        // Parsowanie "1,2,5-8" → [1,2,5,6,7,8]
        private static List<int> ParseChannels(string s)
        {
            var result = new SortedSet<int>();
            if (string.IsNullOrWhiteSpace(s)) return new List<int>();
            foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Contains('-'))
                {
                    var range = part.Split('-', 2);
                    if (range.Length == 2 && int.TryParse(range[0].Trim(), out int a) && int.TryParse(range[1].Trim(), out int b))
                        for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++)
                            if (i >= 1 && i <= 256) result.Add(i);
                }
                else if (int.TryParse(part, out int n) && n >= 1 && n <= 256)
                {
                    result.Add(n);
                }
            }
            return result.ToList();
        }

        private static string FormatChannels(List<int> channels)
        {
            // [1,2,3,4,7,8,9] → "1-4,7-9"
            if (channels.Count == 0) return string.Empty;
            var sorted = channels.OrderBy(x => x).ToList();
            var groups = new List<string>();
            int start = sorted[0]; int prev = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == prev + 1) { prev = sorted[i]; continue; }
                groups.Add(start == prev ? start.ToString() : $"{start}-{prev}");
                start = sorted[i]; prev = sorted[i];
            }
            groups.Add(start == prev ? start.ToString() : $"{start}-{prev}");
            return string.Join(",", groups);
        }
    }
}
