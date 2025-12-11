using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;

namespace Kalendarz1.Monitoring
{
    public partial class LiveViewWindow : Window
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private readonly string _baseRtspUrl;
        private readonly string _cameraName;
        private readonly string _channelId;
        private bool _isPlaying = false;
        private string _currentStreamType = "01"; // 01 = main, 02 = sub

        public MediaPlayer MediaPlayer => _mediaPlayer;

        public LiveViewWindow(string cameraName, string rtspUrl, string channelId)
        {
            _cameraName = cameraName;
            _baseRtspUrl = rtspUrl;
            _channelId = channelId;

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
                // Inicjalizacja LibVLC
                Core.Initialize();

                _libVLC = new LibVLC(
                    "--rtsp-tcp",           // Użyj TCP zamiast UDP (stabilniejsze)
                    "--network-caching=300", // Buforowanie sieci (ms)
                    "--no-audio"            // Bez dźwięku (kamery zwykle nie mają)
                );

                _mediaPlayer = new MediaPlayer(_libVLC);

                // Event handlers
                _mediaPlayer.Playing += MediaPlayer_Playing;
                _mediaPlayer.EncounteredError += MediaPlayer_Error;
                _mediaPlayer.Buffering += MediaPlayer_Buffering;

                VideoPlayer.MediaPlayer = _mediaPlayer;

                // Rozpocznij odtwarzanie
                StartStream();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji VLC:\n{ex.Message}\n\nUpewnij się że pakiety LibVLC są zainstalowane.",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void StartStream()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Łączenie ze strumieniem...";

                // Zbuduj URL z wybraną jakością
                var rtspUrl = _baseRtspUrl.Replace("01\"", $"{_currentStreamType}\"")
                                          .Replace("/101", $"/1{_currentStreamType}")
                                          .Replace("/201", $"/2{_currentStreamType}");

                // Jeśli URL kończy się na 01 lub 02, zamień
                if (rtspUrl.EndsWith("01"))
                    rtspUrl = rtspUrl.Substring(0, rtspUrl.Length - 2) + _currentStreamType;
                else if (rtspUrl.EndsWith("02"))
                    rtspUrl = rtspUrl.Substring(0, rtspUrl.Length - 2) + _currentStreamType;

                var media = new Media(_libVLC, new Uri(rtspUrl));
                _mediaPlayer.Play(media);
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                SetErrorStatus($"Błąd: {ex.Message}");
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
            });
        }

        private void MediaPlayer_Error(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                SetErrorStatus("Błąd strumienia - sprawdź połączenie");
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
            StartStream();
        }

        private void QualityCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (QualityCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string streamType)
            {
                if (_currentStreamType != streamType)
                {
                    _currentStreamType = streamType;
                    if (_mediaPlayer != null && _libVLC != null)
                    {
                        _mediaPlayer.Stop();
                        StartStream();
                    }
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
