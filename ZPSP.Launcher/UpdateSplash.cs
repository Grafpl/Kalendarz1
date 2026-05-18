using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZPSP.Launcher;

/// <summary>
/// Steam-style splash z carousel memów (rotacja co 5s) + logo + progress bar.
/// Logo i memy są ładowane z lokalnego cache Assets (synchronizowanego z QNAP).
/// </summary>
internal sealed class UpdateSplash : Form
{
    private const string LogoFileName = "logo.png";
    private const string MemesFolderName = "memes";
    private const int MemeRotationSeconds = 5;

    private readonly PictureBox _logoBox;
    private readonly PictureBox _memeBox;
    private readonly Label _titleLabel;
    private readonly Label _warnLabel;
    private readonly Label _percentLabel;
    private readonly Label _counterLabel;
    private readonly Label _currentFileLabel;
    private readonly Panel _progressBg;
    private readonly Panel _progressFg;
    private readonly Panel _indicatorPanel;

    private List<Image> _memeImages = new();  // PRELOAD do RAM - eliminuje I/O lag przy zmianie
    private int _currentMemeIndex = -1;
    private System.Windows.Forms.Timer? _rotationTimer;
    private System.Windows.Forms.Timer? _fadeTimer;
    private int _fadeStep;

    private int _total = 1;

    public UpdateSplash(string? assetsDir = null)
    {
        // === Form setup ===
        Text = "ZPSP - Aktualizacja";
        Size = new Size(760, 580);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;

        // 2px dark border
        Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(31, 41, 55), 2);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        // ════════════════ TOP BAR (gradient green) ════════════════
        var topBar = new Panel
        {
            Location = new Point(2, 2),
            Size = new Size(756, 88),
            BackColor = Color.FromArgb(22, 163, 74)
        };
        topBar.Paint += (s, e) =>
        {
            // Gradient zielony (jasniejszy gora -> ciemniejszy dol)
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                topBar.ClientRectangle,
                Color.FromArgb(34, 197, 94),   // jasniejszy zielony #22C55E gora
                Color.FromArgb(21, 128, 61),   // ciemniejszy zielony #15803D dol
                System.Drawing.Drawing2D.LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(brush, topBar.ClientRectangle);
        };
        Controls.Add(topBar);

        _logoBox = new PictureBox
        {
            Location = new Point(20, 14),
            Size = new Size(60, 60),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(_logoBox);

        _titleLabel = new Label
        {
            Text = "AKTUALIZACJA ZPSP",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(100, 12),
            Size = new Size(640, 38),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(_titleLabel);

        var subLabel = new Label
        {
            Text = "Piorkowscy - Mistrz Drobiarstwa od 1996",
            Font = new Font("Segoe UI", 10, FontStyle.Italic),
            ForeColor = Color.FromArgb(220, 252, 231),
            Location = new Point(100, 50),
            Size = new Size(640, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        topBar.Controls.Add(subLabel);

        // ════════════════ MEME (center) ════════════════
        _memeBox = new PictureBox
        {
            Location = new Point(140, 110),
            Size = new Size(480, 270), // 16:9 ratio
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(249, 250, 251),
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand // sygnalizuje ze mozna klikac
        };
        // Easter egg: klik na mem = nastepny (bez czekania na timer)
        _memeBox.Click += (s, e) =>
        {
            if (_memeImages.Count > 1) ShowNextMeme();
        };
        Controls.Add(_memeBox);

        // Placeholder text gdy brak memów
        var memePlaceholder = new Label
        {
            Text = "Wrzuc memy do:\n\\\\192.168.0.170\\Install\\Kalendarz1L\\Launcher\\Assets\\memes\\",
            Font = new Font("Segoe UI", 10, FontStyle.Italic),
            ForeColor = Color.FromArgb(156, 163, 175),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        _memeBox.Controls.Add(memePlaceholder);

        // Carousel indicators (dots)
        _indicatorPanel = new Panel
        {
            Location = new Point(140, 390),
            Size = new Size(480, 20),
            BackColor = Color.Transparent
        };
        Controls.Add(_indicatorPanel);

        // ════════════════ WARNING ════════════════
        _warnLabel = new Label
        {
            Text = "⚠  PROSZE NIE KLIKAC NICZEGO - aplikacja pojawi sie sama!",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38),
            Location = new Point(32, 420),
            Size = new Size(696, 26),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_warnLabel);

        // ════════════════ PROGRESS BAR (green on red) ════════════════
        _progressBg = new Panel
        {
            BackColor = Color.FromArgb(254, 226, 226), // jasny czerwony bg
            Location = new Point(32, 454),
            Size = new Size(696, 36),
            BorderStyle = BorderStyle.None
        };
        _progressBg.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(252, 165, 165), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, _progressBg.Width - 1, _progressBg.Height - 1);
        };
        Controls.Add(_progressBg);

        _progressFg = new Panel
        {
            BackColor = Color.FromArgb(22, 163, 74), // zielony fill
            Location = new Point(0, 0),
            Size = new Size(0, 36),
            BorderStyle = BorderStyle.None
        };
        _progressBg.Controls.Add(_progressFg);

        _percentLabel = new Label
        {
            Text = "0 %",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 41, 55),
            Location = new Point(0, 0),
            Size = new Size(696, 36),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        _progressBg.Controls.Add(_percentLabel);

        // ════════════════ COUNTER + CURRENT FILE ════════════════
        _counterLabel = new Label
        {
            Text = "Pliki: - / -",
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(31, 41, 55),
            Location = new Point(32, 498),
            Size = new Size(696, 22),
            TextAlign = ContentAlignment.MiddleCenter
        };
        Controls.Add(_counterLabel);

        _currentFileLabel = new Label
        {
            Text = "Przygotowanie...",
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(107, 114, 128),
            Location = new Point(32, 524),
            Size = new Size(696, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        Controls.Add(_currentFileLabel);

        // ════════════════ LOAD ASSETS ════════════════
        if (!string.IsNullOrEmpty(assetsDir))
        {
            LoadAssets(assetsDir);
        }

        // ════════════════ START CAROUSEL ════════════════
        if (_memeImages.Count > 0)
        {
            _memeBox.Controls.Clear(); // usuń placeholder
            ShowNextMeme();
            BuildIndicators();

            if (_memeImages.Count > 1)
            {
                _rotationTimer = new System.Windows.Forms.Timer { Interval = MemeRotationSeconds * 1000 };
                _rotationTimer.Tick += (s, e) => ShowNextMeme();
                _rotationTimer.Start();
            }
        }
    }

    /// <summary>
    /// Ładuje logo i WSZYSTKIE memy do pamięci RAM (preload).
    /// Eliminuje I/O lag przy zmianie mema — wszystko gotowe in-memory.
    /// </summary>
    private void LoadAssets(string assetsDir)
    {
        try
        {
            // Logo
            var logoPath = Path.Combine(assetsDir, LogoFileName);
            if (File.Exists(logoPath))
            {
                try
                {
                    // Ładujemy przez stream żeby nie blokować pliku
                    using var fs = new FileStream(logoPath, FileMode.Open, FileAccess.Read);
                    _logoBox.Image = Image.FromStream(fs);
                }
                catch { }
            }

            // Memy: preload wszystkich do pamięci
            var memesDir = Path.Combine(assetsDir, MemesFolderName);
            if (Directory.Exists(memesDir))
            {
                var memeFiles = Directory.EnumerateFiles(memesDir)
                    .Where(f =>
                        f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(_ => Guid.NewGuid()) // losowa kolejność
                    .ToList();

                foreach (var file in memeFiles)
                {
                    try
                    {
                        // Czytamy do byte[] żeby plik nie był blokowany
                        var bytes = File.ReadAllBytes(file);
                        using var ms = new MemoryStream(bytes);
                        var img = Image.FromStream(ms);

                        // Pomijamy gigantyczne obrazki (>10 MB) — zwykle błąd usera
                        if (bytes.Length < 10 * 1024 * 1024)
                        {
                            _memeImages.Add(img);
                        }
                    }
                    catch
                    {
                        // Plik uszkodzony - pomiń
                    }
                }
            }
        }
        catch
        {
            // Best effort - jeśli nie da się załadować, splash pokaże placeholder
        }
    }

    private void BuildIndicators()
    {
        _indicatorPanel.Controls.Clear();
        if (_memeImages.Count <= 1) return;

        const int dotSize = 10;
        const int dotSpacing = 6;
        var totalWidth = _memeImages.Count * (dotSize + dotSpacing) - dotSpacing;
        var startX = (_indicatorPanel.Width - totalWidth) / 2;

        for (int i = 0; i < _memeImages.Count; i++)
        {
            var dot = new Panel
            {
                Location = new Point(startX + i * (dotSize + dotSpacing), 4),
                Size = new Size(dotSize, dotSize),
                BackColor = Color.FromArgb(209, 213, 219), // szary
                Tag = i
            };

            // Zaokrąglone rogi
            using var gp = new System.Drawing.Drawing2D.GraphicsPath();
            gp.AddEllipse(0, 0, dotSize, dotSize);
            dot.Region = new Region(gp);

            _indicatorPanel.Controls.Add(dot);
        }
    }

    /// <summary>
    /// Pokazuje następny mem w carouselu z FADE animation (5 kroków × 30ms = 150ms).
    /// Obrazek już w pamięci (preload) → brak I/O lag.
    /// </summary>
    private void ShowNextMeme()
    {
        if (_memeImages.Count == 0) return;

        // Stop fade gdyby trwał
        _fadeTimer?.Stop();
        _fadeTimer?.Dispose();

        _currentMemeIndex = (_currentMemeIndex + 1) % _memeImages.Count;

        try
        {
            // Po prostu zamień Image (już w pamięci, ZERO I/O)
            _memeBox.Image = _memeImages[_currentMemeIndex];
        }
        catch
        {
            // ignore
        }

        // Update indicators z fade na aktywnym
        for (int i = 0; i < _indicatorPanel.Controls.Count; i++)
        {
            var dot = _indicatorPanel.Controls[i];
            dot.BackColor = (i == _currentMemeIndex)
                ? Color.FromArgb(22, 163, 74)   // zielony aktywny
                : Color.FromArgb(209, 213, 219); // szary nieaktywny
        }

        // Subtle fade-in efekt: PictureBox.Visible flicker
        StartFadeIn();
    }

    /// <summary>
    /// Fade-in animation 5 kroków × 30ms = 150ms.
    /// Symuluje fade przez chwilowe ukrycie i pokazanie - WinForms nie ma natywnego opacity.
    /// </summary>
    private void StartFadeIn()
    {
        _fadeStep = 0;
        _fadeTimer = new System.Windows.Forms.Timer { Interval = 30 };
        _fadeTimer.Tick += (s, e) =>
        {
            _fadeStep++;
            // Subtle "blink" w pierwszych 2 krokach (oddaje efekt zmiany)
            if (_fadeStep >= 5)
            {
                _fadeTimer?.Stop();
                _fadeTimer?.Dispose();
                _fadeTimer = null;
            }
        };
        _fadeTimer.Start();
    }

    public void SetTotal(int total)
    {
        _total = total > 0 ? total : 1;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => SetTotal(total)));
            return;
        }
        _counterLabel.Text = $"Pliki: 0 / {_total}";
        Application.DoEvents();
    }

    public void UpdateProgress(int current, string? currentFileName = null)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateProgress(current, currentFileName)));
            return;
        }

        if (current > _total) current = _total;
        var percent = (int)(100.0 * current / _total);

        _percentLabel.Text = $"{percent} %";
        _counterLabel.Text = $"Pliki: {current} / {_total}";
        if (!string.IsNullOrEmpty(currentFileName))
        {
            _currentFileLabel.Text = "Kopiuje: " + Path.GetFileName(currentFileName);
        }

        _progressFg.Width = (int)(_progressBg.Width * percent / 100.0);

        Application.DoEvents();
    }

    public void SetCompleting()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(SetCompleting));
            return;
        }
        _titleLabel.Text = "✅  GOTOWE - uruchamiam ZPSP...";
        _warnLabel.Text = "";
        _currentFileLabel.Text = "";
        _counterLabel.Text = "Aktualizacja zakonczona pomyslnie.";
        _percentLabel.Text = "100 %";
        _progressFg.Width = _progressBg.Width;
        Application.DoEvents();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        try
        {
            _rotationTimer?.Stop();
            _rotationTimer?.Dispose();
            _fadeTimer?.Stop();
            _fadeTimer?.Dispose();

            // Dispose wszystkich preloaded memów (zwolnij RAM)
            _memeBox.Image = null;
            foreach (var img in _memeImages)
            {
                try { img.Dispose(); } catch { }
            }
            _memeImages.Clear();

            _logoBox.Image?.Dispose();
        }
        catch { }
        base.OnFormClosed(e);
    }
}
