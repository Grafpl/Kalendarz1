using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Kalendarz1.CentrumNagranAI.Services;
using LibVLCSharp.Shared;

namespace Kalendarz1.CentrumNagranAI.Views
{
    /// <summary>
    /// Odtwarzacz klipu dla wybranego hita.
    /// Domyślnie pokazuje samą klatkę JPEG (natychmiast).
    /// Klik "Live z kamery" → LibVLC łączy się z RTSP main-streamem tej kamery.
    ///
    /// PoC: pokazujemy live a nie playback (Hikvision-style ?starttime=&endtime=
    /// na firmware Internec'a wymaga osobnego URL z ONVIF GetReplayUri — dorobimy
    /// jako pełny "playback ±15s" w przyszłej iteracji).
    /// </summary>
    public partial class ClipPlayerWindow : Window
    {
        private readonly SearchHitVm _hit;
        private LibVLC? _libVlc;
        private MediaPlayer? _mediaPlayer;

        public ClipPlayerWindow(SearchHitVm hit)
        {
            InitializeComponent();
            _hit = hit;

            HeaderText.Text = $"#{hit.Rank}  {hit.CameraId}";
            MetaText.Text = $"{hit.TimeLabel}  •  score {hit.Score}";
            ReasonText.Text = hit.Reason;
            StatusText.Text = "Pokazuję klatkę z indeksu. Kliknij ▶ aby uruchomić live z kamery.";

            try
            {
                if (File.Exists(hit.FilePath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(hit.FilePath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    FrameImage.Source = bmp;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Nie mogę załadować klatki: {ex.Message}";
            }

            Closed += OnClosed;
        }

        private void PlayLive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CnaConfig.ZaladujJesliTrzeba();
                var kamera = CnaConfig.Kamery.Find(k => k.Id == _hit.CameraId);
                if (kamera == null)
                {
                    StatusText.Text = $"Brak konfiguracji kamery: {_hit.CameraId}";
                    return;
                }

                // Inicjalizuj LibVLC raz
                if (_libVlc == null)
                {
                    Core.Initialize();
                    _libVlc = new LibVLC(
                        "--no-osd",
                        "--network-caching=300",
                        "--rtsp-tcp"
                    );
                    _mediaPlayer = new MediaPlayer(_libVlc);
                    VideoView.MediaPlayer = _mediaPlayer;
                }

                // Buduj RTSP URL identycznie jak RtspFrameGrabber, ale używamy main streamu
                // (s0) dla lepszej jakości w playerze niż sub.
                string user = Uri.EscapeDataString(kamera.User);
                string pass = Uri.EscapeDataString(kamera.Password);
                string rtsp = $"rtsp://{user}:{pass}@{kamera.Host}:{kamera.RtspPort}/unicast/c{kamera.Channel}/s0/live";

                using var media = new Media(_libVlc, new Uri(rtsp));
                _mediaPlayer!.Play(media);

                FrameImage.Visibility = Visibility.Collapsed;
                VideoView.Visibility = Visibility.Visible;
                StatusText.Text = $"Łączę z {kamera.Host} kanał {kamera.Channel} (live main-stream).";
                PlayLiveBtn.IsEnabled = false;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Błąd LibVLC: {ex.Message}";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OnClosed(object? sender, EventArgs e)
        {
            try { _mediaPlayer?.Stop(); } catch { }
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
        }
    }
}
