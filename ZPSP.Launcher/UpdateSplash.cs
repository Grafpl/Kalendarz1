using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZPSP.Launcher;

/// <summary>
/// Splash aktualizacji ZPSP — theme-driven (theme.json na QNAP), bulletproof.
/// Funkcje:
///  - Carousel memów z preload do RAM (zero I/O lag)
///  - Smooth progress bar z gradient + ETA "Pozostalo XX sek"
///  - Logo z drop-shadow placeholder gdy brak
///  - Wszystko (kolory, rozmiary, teksty) konfigurowane przez theme.json
///  - Robust error handling - splash nigdy nie crashuje (best-effort)
/// </summary>
internal sealed class UpdateSplash : Form
{
    private readonly SplashTheme _theme;

    // Controls
    private readonly PictureBox _logoBox;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly PictureBox _memeBox;
    private readonly Panel _indicatorPanel;
    private readonly Label _heroLabel;
    private readonly Label _warnLabel;
    private readonly Panel _progressBg;
    private readonly Panel _progressFg;
    private readonly Label _percentLabel;
    private readonly Label _counterLabel;
    private readonly Label _currentFileLabel;
    private readonly Label _etaLabel;
    private readonly Label _footerLabel;

    // State
    private List<Image> _memeImages = new();
    private int _currentMemeIndex = -1;
    private System.Windows.Forms.Timer? _rotationTimer;
    private System.Windows.Forms.Timer? _smoothProgressTimer;

    private int _total = 1;
    private int _currentCount;
    private double _displayedPercent;     // dla smooth animation
    private double _targetPercent;
    private readonly Stopwatch _stopwatch = new();

    public UpdateSplash(string? assetsDir = null)
    {
        // ════════════════ THEME LOAD ════════════════
        _theme = string.IsNullOrEmpty(assetsDir)
            ? new SplashTheme()
            : SplashTheme.Load(assetsDir);

        // ════════════════ FORM SETUP ════════════════
        Text = "ZPSP - Aktualizacja";
        Size = new Size(_theme.WindowWidth, _theme.WindowHeight);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = ParseColor(_theme.BackgroundColor, Color.White);
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;

        // Border dookoła
        Paint += (s, e) =>
        {
            using var pen = new Pen(ParseColor(_theme.BorderColor, Color.Black), _theme.BorderThickness);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        // ════════════════ TOP BAR (gradient) ════════════════
        var topBar = new Panel
        {
            Location = new Point(_theme.BorderThickness, _theme.BorderThickness),
            Size = new Size(_theme.WindowWidth - 2 * _theme.BorderThickness, _theme.TopBarHeight)
        };
        topBar.Paint += (s, e) =>
        {
            try
            {
                using var brush = new LinearGradientBrush(
                    topBar.ClientRectangle,
                    ParseColor(_theme.TopBarGradientStart, Color.Green),
                    ParseColor(_theme.TopBarGradientEnd, Color.DarkGreen),
                    LinearGradientMode.Vertical);
                e.Graphics.FillRectangle(brush, topBar.ClientRectangle);
            }
            catch (Exception ex) { Logger.Warn($"TopBar paint: {ex.Message}"); }
        };
        Controls.Add(topBar);

        // Logo
        _logoBox = new PictureBox
        {
            Location = new Point(_theme.LogoMargin, (topBar.Height - _theme.LogoSize) / 2),
            Size = new Size(_theme.LogoSize, _theme.LogoSize),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(_logoBox);

        // Title + subtitle
        var textX = _theme.LogoMargin + _theme.LogoSize + 20;
        var textWidth = topBar.Width - textX - _theme.LogoMargin;

        _titleLabel = new Label
        {
            Text = _theme.Title,
            Font = new Font("Segoe UI", _theme.TitleFontSize, FontStyle.Bold),
            ForeColor = ParseColor(_theme.TitleColor, Color.White),
            Location = new Point(textX, (topBar.Height - _theme.TitleFontSize * 2 - 24) / 2),
            Size = new Size(textWidth, _theme.TitleFontSize + 14),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(_titleLabel);

        _subtitleLabel = new Label
        {
            Text = _theme.Subtitle,
            Font = new Font("Segoe UI", _theme.SubtitleFontSize, FontStyle.Italic),
            ForeColor = ParseColor(_theme.SubtitleColor, Color.LightGreen),
            Location = new Point(textX, _titleLabel.Top + _titleLabel.Height + 2),
            Size = new Size(textWidth, _theme.SubtitleFontSize + 8),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(_subtitleLabel);

        // ════════════════ MEME (carousel center) ════════════════
        var contentY = _theme.TopBarHeight + 28;
        var memeX = (_theme.WindowWidth - _theme.MemeWidth) / 2;

        _memeBox = new PictureBox
        {
            Location = new Point(memeX, contentY),
            Size = new Size(_theme.MemeWidth, _theme.MemeHeight),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = ParseColor(_theme.MemeBackground, Color.WhiteSmoke),
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = _theme.MemeClickToSkip ? Cursors.Hand : Cursors.Default
        };
        if (_theme.MemeClickToSkip)
        {
            _memeBox.Click += (s, e) =>
            {
                if (_memeImages.Count > 1) ShowNextMeme();
            };
        }
        Controls.Add(_memeBox);

        // Placeholder gdy brak memów
        var memePlaceholder = new Label
        {
            Text = "🐔\nDodaj memy do:\n\\\\192.168.0.170\\Install\\Kalendarz1L\\Launcher\\Assets\\memes\\",
            Font = new Font("Segoe UI", 11, FontStyle.Italic),
            ForeColor = Color.FromArgb(156, 163, 175),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        _memeBox.Controls.Add(memePlaceholder);

        // Carousel dots
        _indicatorPanel = new Panel
        {
            Location = new Point(memeX, contentY + _theme.MemeHeight + 8),
            Size = new Size(_theme.MemeWidth, 18),
            BackColor = Color.Transparent
        };
        Controls.Add(_indicatorPanel);

        // ════════════════ HERO TEXT (wielki napis) ════════════════
        var heroY = _indicatorPanel.Top + _indicatorPanel.Height + 14;
        _heroLabel = new Label
        {
            Text = _theme.HeroText,
            Font = new Font("Segoe UI", _theme.HeroFontSize, _theme.HeroBold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = ParseColor(_theme.HeroColor, Color.Green),
            Location = new Point(20, heroY),
            Size = new Size(_theme.WindowWidth - 40, _theme.HeroFontSize + 18),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = false
        };
        Controls.Add(_heroLabel);

        // ════════════════ WARNING ════════════════
        var warnY = heroY + _heroLabel.Height + 8;
        _warnLabel = new Label
        {
            Text = _theme.WarningText,
            Font = new Font("Segoe UI", _theme.WarningFontSize, _theme.WarningBold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = ParseColor(_theme.WarningColor, Color.Red),
            Location = new Point(32, warnY),
            Size = new Size(_theme.WindowWidth - 64, _theme.WarningFontSize + 14),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_warnLabel);

        // ════════════════ PROGRESS BAR ════════════════
        var progY = warnY + _warnLabel.Height + 12;
        _progressBg = new Panel
        {
            Location = new Point(32, progY),
            Size = new Size(_theme.WindowWidth - 64, _theme.ProgressBarHeight),
            BackColor = ParseColor(_theme.ProgressBarBgColor, Color.MistyRose)
        };
        _progressBg.Paint += (s, e) =>
        {
            try
            {
                using var pen = new Pen(ParseColor(_theme.ProgressBarBorderColor, Color.Pink), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, _progressBg.Width - 1, _progressBg.Height - 1);
            }
            catch { }
        };
        Controls.Add(_progressBg);

        _progressFg = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(0, _theme.ProgressBarHeight),
            BorderStyle = BorderStyle.None
        };
        _progressFg.Paint += (s, e) =>
        {
            try
            {
                if (_progressFg.Width <= 0) return;
                using var brush = new LinearGradientBrush(
                    _progressFg.ClientRectangle,
                    ParseColor(_theme.ProgressBarFgColorTop, Color.LightGreen),
                    ParseColor(_theme.ProgressBarFgColor, Color.Green),
                    LinearGradientMode.Vertical);
                e.Graphics.FillRectangle(brush, _progressFg.ClientRectangle);
            }
            catch { }
        };
        _progressBg.Controls.Add(_progressFg);

        _percentLabel = new Label
        {
            Text = "0 %",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Location = new Point(0, 0),
            Size = new Size(_progressBg.Width, _theme.ProgressBarHeight),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        _progressBg.Controls.Add(_percentLabel);

        // ════════════════ COUNTER + ETA + FILE ════════════════
        var counterY = progY + _theme.ProgressBarHeight + 10;
        _counterLabel = new Label
        {
            Text = "Pliki: - / -",
            Font = new Font("Segoe UI", _theme.CounterFontSize),
            ForeColor = ParseColor(_theme.CounterColor, Color.Black),
            Location = new Point(32, counterY),
            Size = new Size((_theme.WindowWidth - 64) / 2, _theme.CounterFontSize + 10),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_counterLabel);

        _etaLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", _theme.CounterFontSize),
            ForeColor = ParseColor(_theme.CounterColor, Color.Black),
            Location = new Point(32 + (_theme.WindowWidth - 64) / 2, counterY),
            Size = new Size((_theme.WindowWidth - 64) / 2, _theme.CounterFontSize + 10),
            TextAlign = ContentAlignment.MiddleRight
        };
        Controls.Add(_etaLabel);

        var fileY = counterY + _theme.CounterFontSize + 12;
        _currentFileLabel = new Label
        {
            Text = "Przygotowanie...",
            Font = new Font("Segoe UI", _theme.CurrentFileFontSize),
            ForeColor = ParseColor(_theme.CurrentFileColor, Color.Gray),
            Location = new Point(32, fileY),
            Size = new Size(_theme.WindowWidth - 64, _theme.CurrentFileFontSize + 10),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        Controls.Add(_currentFileLabel);

        // ════════════════ FOOTER ════════════════
        _footerLabel = new Label
        {
            Text = _theme.FooterText,
            Font = new Font("Segoe UI", _theme.FooterFontSize, FontStyle.Italic),
            ForeColor = ParseColor(_theme.FooterColor, Color.LightGray),
            Location = new Point(32, _theme.WindowHeight - _theme.FooterFontSize - 22),
            Size = new Size(_theme.WindowWidth - 64, _theme.FooterFontSize + 12),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_footerLabel);

        // ════════════════ LOAD ASSETS ════════════════
        if (!string.IsNullOrEmpty(assetsDir))
        {
            LoadAssetsSafe(assetsDir);
        }

        // ════════════════ START CAROUSEL ════════════════
        if (_memeImages.Count > 0)
        {
            _memeBox.Controls.Clear();
            ShowNextMeme();
            BuildIndicators();

            if (_memeImages.Count > 1)
            {
                _rotationTimer = new System.Windows.Forms.Timer
                {
                    Interval = Math.Max(2, _theme.MemeRotationSeconds) * 1000
                };
                _rotationTimer.Tick += (s, e) => ShowNextMeme();
                _rotationTimer.Start();
            }
        }

        // ════════════════ SMOOTH PROGRESS TIMER (60 FPS) ════════════════
        if (_theme.ProgressBarSmooth)
        {
            _smoothProgressTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS
            _smoothProgressTimer.Tick += SmoothProgressTick;
            _smoothProgressTimer.Start();
        }

        _stopwatch.Start();
        Logger.Info($"UpdateSplash otwarty (theme: {_theme.WindowWidth}x{_theme.WindowHeight}, memy: {_memeImages.Count})");
    }

    // ════════════════════════════════════════════════════════════════════
    // ASSETS LOADING
    // ════════════════════════════════════════════════════════════════════

    private void LoadAssetsSafe(string assetsDir)
    {
        // Logo
        try
        {
            var logoPath = Path.Combine(assetsDir, "logo.png");
            if (File.Exists(logoPath))
            {
                var bytes = File.ReadAllBytes(logoPath);
                using var ms = new MemoryStream(bytes);
                _logoBox.Image = Image.FromStream(ms);
                Logger.Info($"Logo zaladowane ({bytes.Length / 1024} KB)");
            }
            else
            {
                Logger.Warn($"Logo nie znalezione: {logoPath}");
                CreateFallbackLogo();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LoadLogo");
            CreateFallbackLogo();
        }

        // Memy
        try
        {
            var memesDir = Path.Combine(assetsDir, "memes");
            if (!Directory.Exists(memesDir))
            {
                Logger.Warn($"Folder memow nie istnieje: {memesDir}");
                return;
            }

            var memeFiles = Directory.EnumerateFiles(memesDir)
                .Where(f =>
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                .OrderBy(_ => Guid.NewGuid())
                .ToList();

            int skipped = 0;
            foreach (var file in memeFiles)
            {
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.Length > 10 * 1024 * 1024) // > 10 MB
                    {
                        Logger.Warn($"Mem {fi.Name} pominiety (> 10 MB)");
                        skipped++;
                        continue;
                    }

                    var bytes = File.ReadAllBytes(file);
                    using var ms = new MemoryStream(bytes);
                    var img = Image.FromStream(ms);
                    _memeImages.Add(img);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Mem {Path.GetFileName(file)} pominiety: {ex.Message}");
                    skipped++;
                }
            }

            Logger.Info($"Memy zaladowane: {_memeImages.Count} (pominiete: {skipped})");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "LoadMemes");
        }
    }

    private void CreateFallbackLogo()
    {
        try
        {
            // Generuj proste logo: kółko z literą P
            var bmp = new Bitmap(_theme.LogoSize, _theme.LogoSize);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using var bgBrush = new SolidBrush(Color.White);
            g.FillEllipse(bgBrush, 4, 4, _theme.LogoSize - 8, _theme.LogoSize - 8);

            using var pen = new Pen(Color.FromArgb(22, 163, 74), 4);
            g.DrawEllipse(pen, 4, 4, _theme.LogoSize - 8, _theme.LogoSize - 8);

            using var font = new Font("Segoe UI", _theme.LogoSize / 2.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(22, 163, 74));
            var size = g.MeasureString("P", font);
            g.DrawString("P", font,
                textBrush,
                (_theme.LogoSize - size.Width) / 2,
                (_theme.LogoSize - size.Height) / 2 - 2);

            _logoBox.Image = bmp;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Fallback logo: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CAROUSEL
    // ════════════════════════════════════════════════════════════════════

    private void BuildIndicators()
    {
        _indicatorPanel.Controls.Clear();
        if (_memeImages.Count <= 1) return;

        var totalWidth = _memeImages.Count * (_theme.DotSize + _theme.DotSpacing) - _theme.DotSpacing;
        var startX = (_indicatorPanel.Width - totalWidth) / 2;

        for (int i = 0; i < _memeImages.Count; i++)
        {
            var dot = new Panel
            {
                Location = new Point(startX + i * (_theme.DotSize + _theme.DotSpacing), 4),
                Size = new Size(_theme.DotSize, _theme.DotSize),
                BackColor = ParseColor(_theme.DotInactiveColor, Color.LightGray),
                Tag = i
            };
            try
            {
                using var gp = new GraphicsPath();
                gp.AddEllipse(0, 0, _theme.DotSize, _theme.DotSize);
                dot.Region = new Region(gp);
            }
            catch { }
            _indicatorPanel.Controls.Add(dot);
        }
    }

    private void ShowNextMeme()
    {
        if (_memeImages.Count == 0) return;

        try
        {
            _currentMemeIndex = (_currentMemeIndex + 1) % _memeImages.Count;
            _memeBox.Image = _memeImages[_currentMemeIndex];

            for (int i = 0; i < _indicatorPanel.Controls.Count; i++)
            {
                _indicatorPanel.Controls[i].BackColor = (i == _currentMemeIndex)
                    ? ParseColor(_theme.DotActiveColor, Color.Green)
                    : ParseColor(_theme.DotInactiveColor, Color.LightGray);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"ShowNextMeme: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PROGRESS API (called from CopyDirectoryWithProgress)
    // ════════════════════════════════════════════════════════════════════

    public void SetTotal(int total)
    {
        _total = total > 0 ? total : 1;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => SetTotal(total))); } catch { }
            return;
        }
        _counterLabel.Text = $"Pliki: 0 / {_total}";
        Application.DoEvents();
    }

    public void UpdateProgress(int current, string? currentFileName = null)
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(() => UpdateProgress(current, currentFileName))); } catch { }
            return;
        }

        if (current > _total) current = _total;
        _currentCount = current;
        _targetPercent = 100.0 * current / _total;

        _counterLabel.Text = $"Pliki: {current} / {_total}";
        if (!string.IsNullOrEmpty(currentFileName))
        {
            _currentFileLabel.Text = "Kopiuje: " + Path.GetFileName(currentFileName);
        }

        // ETA
        if (_theme.ProgressBarShowEta && current > 5 && current < _total)
        {
            var elapsed = _stopwatch.Elapsed.TotalSeconds;
            var rate = current / elapsed;
            var remaining = (_total - current) / Math.Max(rate, 0.1);
            _etaLabel.Text = remaining > 60
                ? $"Pozostalo ~{(int)(remaining / 60)} min {(int)(remaining % 60)} sek"
                : $"Pozostalo ~{(int)remaining} sek";
        }

        // Jeśli smooth wyłączony — natychmiast ustaw
        if (!_theme.ProgressBarSmooth)
        {
            _displayedPercent = _targetPercent;
            ApplyProgress();
        }

        Application.DoEvents();
    }

    public void SetCompleting()
    {
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(SetCompleting)); } catch { }
            return;
        }
        _titleLabel.Text = "GOTOWE - uruchamiam ZPSP...";
        _heroLabel.Text = "ARCYDZIELO ZAKTUALIZOWANE!";
        _heroLabel.ForeColor = ParseColor(_theme.ProgressBarFgColor, Color.Green);
        _warnLabel.Text = "✓ Aktualizacja zakonczona pomyslnie";
        _warnLabel.ForeColor = ParseColor(_theme.ProgressBarFgColor, Color.Green);
        _currentFileLabel.Text = "";
        _etaLabel.Text = "";
        _counterLabel.Text = $"Pliki: {_total} / {_total}";
        _targetPercent = 100.0;
        _displayedPercent = 100.0;
        ApplyProgress();
        Application.DoEvents();
    }

    // ════════════════════════════════════════════════════════════════════
    // SMOOTH PROGRESS ANIMATION (60 FPS interpolation)
    // ════════════════════════════════════════════════════════════════════

    private void SmoothProgressTick(object? sender, EventArgs e)
    {
        try
        {
            var diff = _targetPercent - _displayedPercent;
            if (Math.Abs(diff) < 0.1) return;

            // Ease towards target (15% per frame)
            _displayedPercent += diff * 0.15;
            ApplyProgress();
        }
        catch (Exception ex) { Logger.Warn($"SmoothTick: {ex.Message}"); }
    }

    private void ApplyProgress()
    {
        try
        {
            var pct = Math.Clamp(_displayedPercent, 0, 100);
            _percentLabel.Text = $"{(int)pct} %";
            _progressFg.Width = (int)(_progressBg.Width * pct / 100.0);
            _progressFg.Height = _theme.ProgressBarHeight;
            _progressFg.Invalidate();
        }
        catch { }
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private static Color ParseColor(string hex, Color fallback)
    {
        if (string.IsNullOrEmpty(hex)) return fallback;
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                return Color.FromArgb(
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            if (hex.Length == 8)
            {
                return Color.FromArgb(
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16),
                    Convert.ToInt32(hex.Substring(6, 2), 16));
            }
        }
        catch { }
        return fallback;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            _rotationTimer?.Stop();
            _rotationTimer?.Dispose();
            _smoothProgressTimer?.Stop();
            _smoothProgressTimer?.Dispose();
            _stopwatch.Stop();

            _memeBox.Image = null;
            foreach (var img in _memeImages)
            {
                try { img.Dispose(); } catch { }
            }
            _memeImages.Clear();

            _logoBox.Image?.Dispose();

            Logger.Info($"UpdateSplash zamkniety (czas: {_stopwatch.Elapsed.TotalSeconds:F1}s)");
        }
        catch (Exception ex) { Logger.Warn($"OnFormClosed: {ex.Message}"); }
        base.OnFormClosed(e);
    }
}
