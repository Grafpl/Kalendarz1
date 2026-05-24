using System;
using System.IO;
using System.Text.Json;

namespace ZPSP.Launcher;

/// <summary>
/// Konfiguracja wyglądu splash — wszystko ładowane z theme.json na QNAP.
/// Sergiusz edytuje JSON, zmiany widoczne przy następnej aktualizacji - BEZ rekompilacji.
/// </summary>
internal sealed class SplashTheme
{
    // === WINDOW ===
    public int WindowWidth { get; set; } = 900;
    public int WindowHeight { get; set; } = 720;
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string BorderColor { get; set; } = "#1F2937";
    public int BorderThickness { get; set; } = 2;

    // === TOP BAR (gradient) ===
    public int TopBarHeight { get; set; } = 120;
    public string TopBarGradientStart { get; set; } = "#22C55E";   // jasny zielony
    public string TopBarGradientEnd { get; set; } = "#15803D";     // ciemny zielony

    // === LOGO ===
    public int LogoSize { get; set; } = 100;
    public int LogoMargin { get; set; } = 24;
    public bool LogoDropShadow { get; set; } = true;

    // === TEKSTY ===
    public string Title { get; set; } = "AKTUALIZACJA ZPSP";
    public int TitleFontSize { get; set; } = 28;
    public string TitleColor { get; set; } = "#FFFFFF";

    public string Subtitle { get; set; } = "Piorkowscy - Mistrz Drobiarstwa od 1996";
    public int SubtitleFontSize { get; set; } = 12;
    public string SubtitleColor { get; set; } = "#DCFCE7";

    // === MEME (carousel) ===
    public int MemeWidth { get; set; } = 560;
    public int MemeHeight { get; set; } = 315;          // 16:9 ratio
    public int MemeRotationSeconds { get; set; } = 5;
    public string MemeBackground { get; set; } = "#F9FAFB";
    public string MemeBorder { get; set; } = "#E5E7EB";
    public bool MemeClickToSkip { get; set; } = true;

    // === DOTS (carousel indicators) ===
    public int DotSize { get; set; } = 10;
    public int DotSpacing { get; set; } = 6;
    public string DotActiveColor { get; set; } = "#16A34A";
    public string DotInactiveColor { get; set; } = "#D1D5DB";

    // === WARNING LABEL ===
    public string WarningText { get; set; } = "PROSZE NIE KLIKAC NICZEGO - aplikacja pojawi sie sama!";
    public int WarningFontSize { get; set; } = 13;
    public string WarningColor { get; set; } = "#DC2626";
    public bool WarningBold { get; set; } = true;

    // === PROGRESS BAR ===
    public int ProgressBarHeight { get; set; } = 42;
    public string ProgressBarBgColor { get; set; } = "#FEE2E2";    // jasny czerwony
    public string ProgressBarBorderColor { get; set; } = "#FCA5A5"; // czerwony border
    public string ProgressBarFgColor { get; set; } = "#16A34A";    // zielony fill
    public string ProgressBarFgColorTop { get; set; } = "#22C55E"; // gradient top
    public bool ProgressBarSmooth { get; set; } = true;            // animowane przejście
    public bool ProgressBarShowEta { get; set; } = true;           // "Pozostalo XX sek"

    // === COUNTER / FILE LABELS ===
    public int CounterFontSize { get; set; } = 11;
    public string CounterColor { get; set; } = "#1F2937";
    public int CurrentFileFontSize { get; set; } = 9;
    public string CurrentFileColor { get; set; } = "#6B7280";

    // === FOOTER (na samym dole) ===
    public string FooterText { get; set; } = "Po zakonczeniu aplikacja uruchomi sie sama";
    public int FooterFontSize { get; set; } = 9;
    public string FooterColor { get; set; } = "#9CA3AF";

    /// <summary>
    /// Ładuje theme z pliku JSON. Jeśli plik nie istnieje lub jest uszkodzony — zwraca default.
    /// </summary>
    public static SplashTheme Load(string assetsDir)
    {
        try
        {
            var path = Path.Combine(assetsDir, "theme.json");
            if (!File.Exists(path)) return new SplashTheme();

            var json = File.ReadAllText(path);
            var theme = JsonSerializer.Deserialize<SplashTheme>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            return theme ?? new SplashTheme();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Nie udalo sie zaladowac theme.json: {ex.Message}. Uzywam defaults.");
            return new SplashTheme();
        }
    }
}
