using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>Podgląd treści umowy — ekstrakcja tekstu z wygenerowanego docx (OpenXML), bez Worda.</summary>
    public partial class KontraktPodgladWindow : Window
    {
        private readonly string _docxPath;

        public KontraktPodgladWindow(string docxPath, string? podtytul = null)
        {
            InitializeComponent();
            _docxPath = docxPath;
            if (!string.IsNullOrWhiteSpace(podtytul)) txtPodtytul.Text = podtytul;
            Loaded += (_, _) => Renderuj();
        }

        private void Renderuj()
        {
            try
            {
                var linie = WordTemplateService.WyciagnijTekst(_docxPath);
                int wyswietlone = 0;
                foreach (var l in linie)
                {
                    if (string.IsNullOrWhiteSpace(l)) { icTresc.Items.Add(new TextBlock { Height = 8 }); continue; }
                    bool tabela = l.StartsWith("    ");
                    bool naglowek = !tabela && l.Length < 60 && l == l.ToUpperInvariant() && l.Length > 2;
                    icTresc.Items.Add(new TextBlock
                    {
                        Text = l,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, naglowek ? 8 : 1, 0, 1),
                        FontSize = naglowek ? 13.5 : 12.5,
                        FontWeight = naglowek ? FontWeights.Bold : FontWeights.Normal,
                        FontFamily = tabela ? new FontFamily("Consolas") : new FontFamily("Segoe UI"),
                        Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A))
                    });
                    wyswietlone++;
                }
                txtInfo.Text = wyswietlone == 0
                    ? "Brak treści do wyświetlenia (pusty dokument)."
                    : $"{wyswietlone} wierszy treści";
            }
            catch (Exception ex)
            {
                icTresc.Items.Add(new TextBlock
                {
                    Text = "Nie udało się odczytać treści: " + ex.Message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                });
            }
        }

        private void BtnWord_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(_docxPath) { UseShellExecute = true }); } catch { }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
