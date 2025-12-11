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
        // Konfiguracja NVR - można przenieść do ustawień
        private readonly string _nvrIp = "192.168.0.128";
        private readonly string _username = "Admin";
        private readonly string _password = "terePacja12$";

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
                LoadingText.Text = "Łączenie z NVR...";

                _hikvisionService = new HikvisionService(_nvrIp, _username, _password);

                // Test połączenia
                LoadingText.Text = "Testowanie połączenia...";
                var isConnected = await _hikvisionService.TestConnectionAsync();

                if (!isConnected)
                {
                    SetOfflineStatus("Brak połączenia z NVR");
                    return;
                }

                // Pobierz informacje o urządzeniu
                LoadingText.Text = "Pobieranie informacji o urządzeniu...";
                var deviceInfo = await _hikvisionService.GetDeviceInfoAsync();
                UpdateDeviceInfo(deviceInfo);

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
                MessageBox.Show($"Błąd połączenia z NVR:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
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
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = channel.Status,
                FontSize = 9,
                Foreground = channel.Status == "Online" ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888")),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var button = new Button
            {
                Style = (Style)FindResource("CameraButtonStyle"),
                Content = stack,
                Tag = channel
            };

            button.Click += CameraButton_Click;
            return button;
        }

        private async void CameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CameraChannel channel)
            {
                try
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    LoadingText.Text = $"Pobieranie obrazu z {channel.Name}...";

                    // Pobierz snapshot z kamery
                    var imageBytes = await _hikvisionService.GetSnapshotAsync(channel.Id);

                    // Wyświetl w nowym oknie
                    ShowSnapshot(channel.Name, imageBytes);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd pobierania obrazu:\n{ex.Message}",
                        "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ShowSnapshot(string cameraName, byte[] imageBytes)
        {
            var window = new Window
            {
                Title = $"Podgląd - {cameraName}",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Colors.Black)
            };

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

            window.Content = image;
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
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Info o konfiguracji
            var info = $"Konfiguracja NVR:\n\n" +
                       $"IP: {_nvrIp}\n" +
                       $"Użytkownik: {_username}\n" +
                       $"URL RTSP: rtsp://{_username}:***@{_nvrIp}:554/Streaming/Channels/101\n\n" +
                       $"Aby zmienić konfigurację, edytuj plik:\n" +
                       $"Monitoring/MonitoringWindow.xaml.cs";

            MessageBox.Show(info, "Ustawienia", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _hikvisionService?.Dispose();
        }
    }
}
