using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class WalidacjaDialog : Window
    {
        public bool MoznaKontynuowac { get; private set; }

        public WalidacjaDialog(List<P02Validator.ValidationIssue> issues)
        {
            InitializeComponent();

            int errors = issues.Count(i => i.Severity == P02Validator.Severity.Error);
            int warnings = issues.Count(i => i.Severity == P02Validator.Severity.Warning);
            int infos = issues.Count(i => i.Severity == P02Validator.Severity.Info);

            // Status header
            if (errors > 0)
            {
                ikonaStatus.Text = "✗";
                ikonaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                lblTytul.Text = "Walidacja nie przeszła — eksport zablokowany";
                btnKontynuuj.IsEnabled = false;
                btnKontynuuj.Opacity = 0.5;
                btnKontynuuj.ToolTip = "Najpierw popraw błędy w danych";
            }
            else if (warnings > 0)
            {
                ikonaStatus.Text = "⚠";
                ikonaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D97706"));
                lblTytul.Text = "Walidacja OK — z ostrzeżeniami";
            }
            else if (infos > 0)
            {
                ikonaStatus.Text = "ℹ";
                ikonaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB"));
                lblTytul.Text = "Walidacja OK — sprawdź informacje";
            }
            else
            {
                ikonaStatus.Text = "✓";
                ikonaStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
                lblTytul.Text = "Walidacja przeszła — wszystko OK";
            }

            lblPodsumowanie.Text = $"{errors} błędów  ·  {warnings} ostrzeżeń  ·  {infos} info";

            // Pre-build view models
            dg.ItemsSource = issues.Select(i => new IssueVm(i)).ToList();
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            MoznaKontynuowac = true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            MoznaKontynuowac = false;
            Close();
        }
    }

    public class IssueVm
    {
        public string TypLabel { get; }
        public Brush TypBg { get; }
        public Brush TypFg { get; }
        public string Field { get; }
        public string Message { get; }

        public IssueVm(P02Validator.ValidationIssue i)
        {
            Field = i.Field;
            Message = i.Message;
            (TypLabel, string bg, string fg) = i.Severity switch
            {
                P02Validator.Severity.Error => ("✗ BŁĄD", "#FEE2E2", "#B91C1C"),
                P02Validator.Severity.Warning => ("⚠ OSTRZEŻ.", "#FEF3C7", "#92400E"),
                P02Validator.Severity.Info => ("ℹ INFO", "#DBEAFE", "#1E40AF"),
                _ => ("?", "#F0F2F5", "#374151")
            };
            TypBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            TypFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
        }
    }
}
