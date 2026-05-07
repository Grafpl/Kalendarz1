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

            HeaderText.Text = $"#{hit.Rank}  {hit.CameraDisplayName}";
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

        private void EnsureVlcInitialized()
        {
            if (_libVlc != null) return;
            Core.Initialize();
            _libVlc = new LibVLC(
                "--no-osd",
                "--network-caching=500",
                "--rtsp-tcp"
            );
            _mediaPlayer = new MediaPlayer(_libVlc);
            VideoView.MediaPlayer = _mediaPlayer;
        }

        private CnaCameraEndpoint? KameraDlaHita()
        {
            CnaConfig.ZaladujJesliTrzeba();
            return CnaConfig.Kamery.Find(k => k.Id == _hit.CameraId);
        }

        private void PlayClip_Click(object sender, RoutedEventArgs e)
        {
            var kamera = KameraDlaHita();
            if (kamera == null) { StatusText.Text = $"Brak konfiguracji kamery: {_hit.CameraId}"; return; }

            PlayClipBtn.IsEnabled = false;
            StatusText.Text = "Pytam NVR o URL nagrania (ONVIF GetReplayUri)...";

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var from = _hit.TsUtc.AddSeconds(-15);
                    var to = _hit.TsUtc.AddSeconds(15);
                    string? replayUrl = await OnvifClient.GetReplayUriAsync(kamera, from, to);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (string.IsNullOrEmpty(replayUrl))
                        {
                            StatusText.Text = "NVR nie zwrócił URL nagrania. Spróbuj ▶ Live.";
                            PlayClipBtn.IsEnabled = true;
                            return;
                        }

                        EnsureVlcInitialized();

                        // Wstrzyknij creds do URL (ONVIF zwraca URL bez user/pass).
                        string user = Uri.EscapeDataString(kamera.User);
                        string pass = Uri.EscapeDataString(kamera.Password);
                        string urlWithAuth = replayUrl.Replace("rtsp://", $"rtsp://{user}:{pass}@");

                        using var media = new Media(_libVlc!, new Uri(urlWithAuth));
                        _mediaPlayer!.Play(media);

                        FrameImage.Visibility = Visibility.Collapsed;
                        VideoView.Visibility = Visibility.Visible;
                        StatusText.Text = $"Odtwarzam klip ±15s z {_hit.TsUtc.ToLocalTime():HH:mm:ss}";
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text = $"Błąd: {ex.Message}";
                        PlayClipBtn.IsEnabled = true;
                    });
                }
            });
        }

        private void PlayLive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var kamera = KameraDlaHita();
                if (kamera == null) { StatusText.Text = $"Brak konfiguracji kamery: {_hit.CameraId}"; return; }

                EnsureVlcInitialized();

                string user = Uri.EscapeDataString(kamera.User);
                string pass = Uri.EscapeDataString(kamera.Password);
                string rtsp = $"rtsp://{user}:{pass}@{kamera.Host}:{kamera.RtspPort}/unicast/c{kamera.Channel}/s0/live";

                using var media = new Media(_libVlc!, new Uri(rtsp));
                _mediaPlayer!.Play(media);

                FrameImage.Visibility = Visibility.Collapsed;
                VideoView.Visibility = Visibility.Visible;
                StatusText.Text = $"Łączę z {kamera.Host} kanał {kamera.Channel} (live).";
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
