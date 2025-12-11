using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LibVLCSharp.Shared;

namespace Kalendarz1.Monitoring
{
    public partial class LiveViewWindow : Window
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private readonly string[] _rtspUrls;
        private readonly string _cameraName;
        private readonly string _channelId;
        private int _currentUrlIndex = 0;
        private bool _isPlaying = false;

        public MediaPlayer MediaPlayer => _mediaPlayer;

        public LiveViewWindow(string cameraName, string rtspUrl, string channelId)
        {
            _cameraName = cameraName;
            _channelId = channelId;

            // Pobierz wszystkie możliwe formaty URL
            var service = new HikvisionService("192.168.0.125", "admin", "terePacja12$");
            _rtspUrls = service.GetAllRtspUrls(channelId, true);
            service.Dispose();

            InitializeComponent();

            CameraNameText.Text = cameraName;
            CameraInfoText.Text = $"Kanał: {channelId}";

            DataContext = this;

            Loaded += LiveViewWindow_Loaded;
        }

        private void LiveViewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Core.Initialize();

                _libVLC = new LibVLC(
                    "--rtsp-tcp",
                    "--network-caching=1000",
                    "--clock-jitter=0",
                    "--no-audio",
                    "--rtsp-user=admin",
                    "--rtsp-pwd=terePacja12$",
                    "--live-caching=300",
                    "--no-video-title-show",
                    "--avcodec-hw=none"  // Wyłącz hardware decoding dla kompatybilności
                );

                _mediaPlayer = new MediaPlayer(_libVLC);
                _mediaPlayer.Playing += MediaPlayer_Playing;
                _mediaPlayer.EncounteredError += MediaPlayer_Error;
                _mediaPlayer.Buffering += MediaPlayer_Buffering;
                _mediaPlayer.EndReached += MediaPlayer_EndReached;

                VideoPlayer.MediaPlayer = _mediaPlayer;

                StartStream();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji VLC:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void StartStream()
        {
            if (_currentUrlIndex >= _rtspUrls.Length)
            {
                // Wypróbowano wszystkie URL-e
                LoadingOverlay.Visibility = Visibility.Collapsed;
                SetErrorStatus("Nie udało się połączyć - sprawdź format RTSP w NVR");
                ShowRtspHelp();
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            var url = _rtspUrls[_currentUrlIndex];
            var displayUrl = url.Replace("terePacja12$", "***");
            LoadingText.Text = $"Próba {_currentUrlIndex + 1}/{_rtspUrls.Length}:\n{displayUrl}";

            try
            {
                var media = new Media(_libVLC, new Uri(url));

                // Opcje dla połączenia RTSP
                media.AddOption(":network-caching=1000");
                media.AddOption(":rtsp-tcp");
                media.AddOption(":rtsp-http=0");
                media.AddOption(":live-caching=300");
                media.AddOption(":clock-jitter=0");

                _mediaPlayer.Play(media);

                // Timeout - jeśli nie połączy w 10 sekund, próbuj następny URL
                Task.Delay(10000).ContinueWith(t =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isPlaying && _currentUrlIndex < _rtspUrls.Length)
                        {
                            _mediaPlayer.Stop();
                            _currentUrlIndex++;
                            StartStream();
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                _currentUrlIndex++;
                StartStream();
            }
        }

        private void MediaPlayer_Playing(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _isPlaying = true;
                PlayPauseButton.Content = "⏸ Pauza";
                SetOnlineStatus();

                // Pokaż który URL zadziałał
                var workingUrl = _rtspUrls[_currentUrlIndex].Replace("terePacja12$", "***");
                CameraInfoText.Text = $"Kanał: {_channelId} | {workingUrl.Split('?')[0].Split('/').Last()}";
            });
        }

        private void MediaPlayer_Error(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isPlaying)
                {
                    _currentUrlIndex++;
                    StartStream();
                }
            });
        }

        private void MediaPlayer_EndReached(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isPlaying)
                {
                    _currentUrlIndex++;
                    StartStream();
                }
            });
        }

        private void MediaPlayer_Buffering(object sender, MediaPlayerBufferingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.Cache < 100)
                {
                    LoadingText.Text = $"Buforowanie... {e.Cache:F0}%";
                }
            });
        }

        private void ShowRtspHelp()
        {
            var urls = string.Join("\n", _rtspUrls.Select(u => u.Replace("terePacja12$", "***")));
            MessageBox.Show(
                $"Nie udało się połączyć z kamerą {_channelId}.\n\n" +
                $"Wypróbowane URL-e:\n{urls}\n\n" +
                $"Sprawdź w VLC Player który URL działa:\n" +
                $"Media -> Otwórz strumień sieciowy\n\n" +
                $"Możesz też sprawdzić format RTSP w konfiguracji NVR.",
                "Błąd połączenia", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SetOnlineStatus()
        {
            StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27AE60"));
            StatusText.Text = "Na żywo";
            StatusText.Foreground = StatusIndicator.Fill;
        }

        private void SetErrorStatus(string message)
        {
            StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E74C3C"));
            StatusText.Text = message;
            StatusText.Foreground = StatusIndicator.Fill;
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null) return;

            if (_isPlaying)
            {
                _mediaPlayer.Pause();
                PlayPauseButton.Content = "▶ Play";
                _isPlaying = false;
            }
            else
            {
                _mediaPlayer.Play();
                PlayPauseButton.Content = "⏸ Pauza";
                _isPlaying = true;
            }
        }

        private void SnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null) return;

            try
            {
                var snapshotPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    $"Kamera_{_channelId}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                );

                _mediaPlayer.TakeSnapshot(0, snapshotPath, 0, 0);

                MessageBox.Show($"Snapshot zapisany:\n{snapshotPath}",
                    "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu snapshot:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Stop();
            _isPlaying = false;
            _currentUrlIndex = 0;
            StartStream();
        }

        private void QualityCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Przebuduj listę URL z nową jakością
            if (QualityCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string streamType)
            {
                var mainStream = streamType == "01";
                var service = new HikvisionService("192.168.0.125", "admin", "terePacja12$");
                var newUrls = service.GetAllRtspUrls(_channelId, mainStream);
                service.Dispose();

                // Zaktualizuj URL-e
                for (int i = 0; i < _rtspUrls.Length && i < newUrls.Length; i++)
                {
                    _rtspUrls[i] = newUrls[i];
                }

                if (_mediaPlayer != null && _libVLC != null)
                {
                    _mediaPlayer.Stop();
                    _isPlaying = false;
                    _currentUrlIndex = 0;
                    StartStream();
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _mediaPlayer?.Stop();
                _mediaPlayer?.Dispose();
                _libVLC?.Dispose();
            }
            catch { }
        }
    }
}
