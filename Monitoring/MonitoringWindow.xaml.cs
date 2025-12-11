using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Monitoring
{
    public partial class MonitoringWindow : Window
    {
        // ═══════════════════════════════════════════════════════════════════
        // KONFIGURACJA NVR - Zmień te wartości dla swojego urządzenia
        // ═══════════════════════════════════════════════════════════════════
        private readonly string _nvrIp = "192.168.0.125";    // IP rejestratora NVR
        private readonly string _username = "admin";          // Login (zazwyczaj małymi literami)
        private readonly string _password = "terePacja12$";   // Hasło
        // ═══════════════════════════════════════════════════════════════════

        private HikvisionService _hikvisionService;

        public MonitoringWindow()
        {
            InitializeComponent();
            Loaded += MonitoringWindow_Loaded;
        }

        private async void MonitoringWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = $"Łączenie z NVR ({_nvrIp})...";

                _hikvisionService = new HikvisionService(_nvrIp, _username, _password);

                // Test połączenia z diagnostyką
                LoadingText.Text = "Testowanie połączenia...";
                var (success, message) = await _hikvisionService.TestConnectionAsync();

                if (!success)
                {
                    SetOfflineStatus(message);
                    ShowConnectionHelp(message);
                    return;
                }

                // Pobierz informacje o urządzeniu
                LoadingText.Text = "Pobieranie informacji o NVR...";
                try
                {
                    var deviceInfo = await _hikvisionService.GetDeviceInfoAsync();
                    UpdateDeviceInfo(deviceInfo);
                }
                catch (Exception ex)
                {
                    DeviceNameText.Text = "NVR Połączony";
                    DeviceModelText.Text = $"(nie udało się pobrać szczegółów: {ex.Message})";
                }

                // Pobierz listę kamer
                LoadingText.Text = "Pobieranie listy kamer...";
                var channels = await _hikvisionService.GetChannelsAsync();
                UpdateCamerasList(channels);

                // Ustaw status online
                SetOnlineStatus();
            }
            catch (Exception ex)
            {
                SetOfflineStatus($"Błąd: {ex.Message}");
                ShowConnectionHelp(ex.Message);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowConnectionHelp(string errorMessage)
        {
            var help = "Sprawdź:\n" +
                      $"1. Czy NVR jest dostępny pod adresem {_nvrIp}\n" +
                      $"2. Czy login ({_username}) i hasło są poprawne\n" +
                      "3. Czy ISAPI jest włączone w NVR\n" +
                      "4. Czy firewall nie blokuje połączenia\n\n" +
                      $"Błąd: {errorMessage}";

            MessageBox.Show(help, "Problem z połączeniem", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void UpdateDeviceInfo(DeviceInfo info)
        {
            DeviceNameText.Text = info.DeviceName;
            DeviceModelText.Text = $"Model: {info.Model}";
            FirmwareText.Text = info.FirmwareVersion;
            MacAddressText.Text = info.MacAddress;
        }

        private void UpdateCamerasList(System.Collections.Generic.List<CameraChannel> channels)
        {
            CamerasPanel.Children.Clear();

            foreach (var channel in channels)
            {
                var button = CreateCameraButton(channel);
                CamerasPanel.Children.Add(button);
            }
        }

        private Button CreateCameraButton(CameraChannel channel)
        {
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(new TextBlock
            {
                Text = "📷",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = channel.Name,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 130
            });

            // Pokaż IP kamery
            stack.Children.Add(new TextBlock
            {
                Text = channel.IpAddress ?? "",
                FontSize = 8,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var button = new Button
            {
                Style = (Style)FindResource("CameraButtonStyle"),
                Content = stack,
                Tag = channel,
                ToolTip = $"Kliknij aby pobrać snapshot\nKanał: {channel.Id}\nIP: {channel.IpAddress}"
            };

            button.Click += CameraButton_Click;
            return button;
        }

        private Brush GetStatusBrush(string status)
        {
            return status switch
            {
                "Online" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                "Aktywny" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                "Offline" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"))
            };
        }

        private async void CameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CameraChannel channel)
            {
                try
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    LoadingText.Text = $"Pobieranie obrazu z {channel.Name}...";

                    var imageBytes = await _hikvisionService.GetSnapshotAsync(channel.Id);
                    ShowSnapshot(channel.Name, imageBytes, channel.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Nie udało się pobrać obrazu z kamery.\n\n" +
                        $"Kamera: {channel.Name} (kanał {channel.Id})\n" +
                        $"Błąd: {ex.Message}\n\n" +
                        $"Spróbuj otworzyć stream RTSP w VLC:\n" +
                        $"{_hikvisionService.GetRtspUrl(channel.Id)}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ShowSnapshot(string cameraName, byte[] imageBytes, string channelId)
        {
            var window = new Window
            {
                Title = $"Podgląd - {cameraName}",
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Colors.Black)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var image = new Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(10)
            };

            using (var ms = new MemoryStream(imageBytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                image.Source = bitmap;
            }

            Grid.SetRow(image, 0);
            grid.Children.Add(image);

            // Panel z informacjami i przyciskami
            var infoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var rtspUrl = _hikvisionService.GetRtspUrl(channelId);
            var copyButton = new Button
            {
                Content = "📋 Kopiuj URL RTSP",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            copyButton.Click += (s, ev) =>
            {
                Clipboard.SetText(rtspUrl);
                MessageBox.Show($"Skopiowano URL RTSP:\n{rtspUrl}", "Skopiowano", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            infoPanel.Children.Add(copyButton);

            Grid.SetRow(infoPanel, 1);
            grid.Children.Add(infoPanel);

            window.Content = grid;
            window.ShowDialog();
        }

        private void SetOnlineStatus()
        {
            StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            StatusText.Text = "Online";
        }

        private void SetOfflineStatus(string message)
        {
            StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            StatusText.Text = "Offline";
            DeviceNameText.Text = message;
            DeviceModelText.Text = "";
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            CamerasPanel.Children.Clear();
            await LoadDataAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var rtspExample = _hikvisionService?.GetRtspUrl("1") ?? $"rtsp://{_username}:***@{_nvrIp}:554/Streaming/Channels/101";

            var info = $"Konfiguracja NVR:\n\n" +
                       $"IP: {_nvrIp}\n" +
                       $"Użytkownik: {_username}\n\n" +
                       $"URL RTSP (przykład dla kanału 1):\n{rtspExample}\n\n" +
                       $"Format kanałów RTSP:\n" +
                       $"  X01 = główny strumień (HD)\n" +
                       $"  X02 = substream (niższa jakość)\n\n" +
                       $"Aby zmienić konfigurację, edytuj plik:\n" +
                       $"Monitoring/MonitoringWindow.xaml.cs";

            MessageBox.Show(info, "Ustawienia NVR", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _hikvisionService?.Dispose();
        }
    }
}
