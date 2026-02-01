using System;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Scoring
{
    public partial class ScoringPanel : Window
    {
        private readonly ScoringService _service;
        private readonly int _klientId;
        private readonly string _nazwaKlienta;

        public ScoringPanel(string connLibra, string connHandel, int klientId, string nazwaKlienta)
        {
            InitializeComponent();

            _service = new ScoringService(connLibra, connHandel);
            _klientId = klientId;
            _nazwaKlienta = nazwaKlienta;

            txtNaglowek.Text = $"📊 Scoring kredytowy";
            txtKlient.Text = $"{nazwaKlienta} (ID: {klientId})";

            Loaded += async (s, e) =>
            {
                try
                {
                    await _service.EnsureTableExistsAsync();
                    var ostatni = await _service.PobierzOstatniScoringAsync(_klientId);
                    if (ostatni != null)
                        WyswietlScoring(ostatni);
                    else
                        txtStatus.Text = "Brak danych scoringowych. Kliknij 'Przelicz scoring'.";
                }
                catch (Exception ex)
                {
                    txtStatus.Text = $"Błąd: {ex.Message}";
                }
            };
        }

        private async void BtnPrzelicz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnPrzelicz.IsEnabled = false;
                txtStatus.Text = "Obliczanie scoringu...";

                var result = await _service.ObliczScoringAsync(_klientId);
                WyswietlScoring(result);

                txtStatus.Text = "Scoring obliczony pomyślnie.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd obliczania scoringu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Błąd: {ex.Message}";
            }
            finally
            {
                btnPrzelicz.IsEnabled = true;
            }
        }

        private void WyswietlScoring(ScoringResult result)
        {
            txtScore.Text = result.ScoreTotal.ToString();
            txtKategoriaBadge.Text = result.Kategoria;

            // Kolor wskaźnika
            var kolor = ParseKolor(result.KategoriaKolor);
            txtScore.Foreground = new SolidColorBrush(kolor);
            scoreRing.Stroke = new SolidColorBrush(kolor);

            // Postęp koła (circumference ~377 for 120px diameter ellipse)
            double circumference = Math.PI * 110; // approx for stroke thickness 10
            double progress = result.ScoreTotal / 100.0 * circumference;
            scoreRing.StrokeDashArray = new DoubleCollection { progress / 10, circumference / 10 };

            // Składniki
            txtTerminowosc.Text = $"{result.TerminowoscPkt}/40";
            pbTerminowosc.Value = result.TerminowoscPkt;

            txtHistoria.Text = $"{result.HistoriaPkt}/20";
            pbHistoria.Value = result.HistoriaPkt;

            txtRegularnosc.Text = $"{result.RegularnoscPkt}/20";
            pbRegularnosc.Value = result.RegularnoscPkt;

            txtTrend.Text = $"{result.TrendPkt}/10";
            pbTrend.Value = result.TrendPkt;

            txtLimit.Text = $"{result.LimitPkt}/10";
            pbLimit.Value = result.LimitPkt;

            // Rekomendacja
            txtRekomendacja.Text = result.RekomendacjaOpis ?? "Brak rekomendacji.";
            txtDataObliczenia.Text = $"Ostatnie obliczenie: {result.DataObliczenia:dd.MM.yyyy HH:mm}";

            // Kolor tła rekomendacji
            var bgKolor = result.ScoreTotal switch
            {
                >= 70 => Color.FromRgb(220, 252, 231), // #DCFCE7 green
                >= 50 => Color.FromRgb(254, 249, 195), // #FEF9C3 yellow
                _ => Color.FromRgb(254, 226, 226)       // #FEE2E2 red
            };
            borderRekomendacja.Background = new SolidColorBrush(bgKolor);
        }

        private Color ParseKolor(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                return Color.FromRgb(
                    Convert.ToByte(hex.Substring(0, 2), 16),
                    Convert.ToByte(hex.Substring(2, 2), 16),
                    Convert.ToByte(hex.Substring(4, 2), 16));
            }
            catch
            {
                return Color.FromRgb(107, 114, 128);
            }
        }
    }
}
