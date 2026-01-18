using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Okno diagnostyki wydajności - pokazuje czasy operacji i statystyki cache
    /// </summary>
    public partial class DiagnostykaWindow : Window
    {
        public DiagnostykaWindow()
        {
            InitializeComponent();
            OdswiezRaport();
        }

        private void OdswiezRaport()
        {
            var sb = new StringBuilder();

            // Nagłówek
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              DIAGNOSTYKA WYDAJNOŚCI - MODUŁ OPAKOWAŃ                    ║");
            sb.AppendLine($"║              Wygenerowano: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                         ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // Stan cache
            sb.AppendLine(SaldaService.GetCacheStatus());
            sb.AppendLine();

            // Raport profilera
            sb.AppendLine(PerformanceProfiler.GenerateReport());
            sb.AppendLine();

            // Instrukcje
            sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                         INSTRUKCJE                                      ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ 1. Kliknij 'Test ładowania' aby zmierzyć czas pobierania danych         ║");
            sb.AppendLine("║ 2. Używaj aplikacji normalnie - czasy będą zbierane automatycznie       ║");
            sb.AppendLine("║ 3. Kliknij 'Odśwież raport' aby zobaczyć aktualne statystyki           ║");
            sb.AppendLine("║ 4. 'Kopiuj do schowka' aby skopiować raport i wkleić gdzie potrzeba    ║");
            sb.AppendLine("║                                                                         ║");
            sb.AppendLine("║ CACHE TTL:                                                              ║");
            sb.AppendLine("║   - Salda: 8 godzin (dane ładowane RAZ!)                               ║");
            sb.AppendLine("║   - Potwierdzenia: 4 godziny                                            ║");
            sb.AppendLine("║   - Handlowcy: 24 godziny                                               ║");
            sb.AppendLine("║   - Dokumenty: 1 godzina (per kontrahent)                               ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");

            txtRaport.Text = sb.ToString();
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            OdswiezRaport();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz zresetować wszystkie statystyki?",
                "Potwierdzenie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                PerformanceProfiler.Reset();
                OdswiezRaport();
                MessageBox.Show("Statystyki zostały zresetowane.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnKopiuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtRaport.Text);
                MessageBox.Show("Raport skopiowany do schowka!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd kopiowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnTestLadowania_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n╔══════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                    TEST ŁADOWANIA DANYCH                                 ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════╣");

            txtRaport.Text += sb.ToString();

            try
            {
                var service = new SaldaService();

                // Test 1: Ładowanie sald (z cache lub bazy)
                sb.Clear();
                sb.AppendLine("║ Test 1: Ładowanie sald...                                              ║");
                txtRaport.Text += sb.ToString();

                var sw1 = Stopwatch.StartNew();
                var salda = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw1.Stop();

                sb.Clear();
                sb.AppendLine($"║   Wynik: {salda.Count} rekordów w {sw1.ElapsedMilliseconds}ms                                     ║");
                sb.AppendLine($"║   {(sw1.ElapsedMilliseconds < 100 ? "✓ CACHE HIT - błyskawicznie!" : sw1.ElapsedMilliseconds < 2000 ? "○ OK" : "✗ WOLNO - sprawdź połączenie")}                                                 ║");
                txtRaport.Text += sb.ToString();

                // Test 2: Ponowne ładowanie (powinno być z cache)
                sb.Clear();
                sb.AppendLine("║                                                                         ║");
                sb.AppendLine("║ Test 2: Ponowne ładowanie (powinno być z cache)...                     ║");
                txtRaport.Text += sb.ToString();

                var sw2 = Stopwatch.StartNew();
                var salda2 = await service.PobierzWszystkieSaldaAsync(DateTime.Today);
                sw2.Stop();

                sb.Clear();
                sb.AppendLine($"║   Wynik: {salda2.Count} rekordów w {sw2.ElapsedMilliseconds}ms                                     ║");
                sb.AppendLine($"║   {(sw2.ElapsedMilliseconds < 10 ? "✓ CACHE HIT - super szybko!" : "✗ Powinno być szybsze")}                                         ║");
                txtRaport.Text += sb.ToString();

                // Test 3: Ładowanie dokumentów
                if (salda.Count > 0)
                {
                    var pierwszyKontrahent = salda[0];
                    sb.Clear();
                    sb.AppendLine("║                                                                         ║");
                    sb.AppendLine($"║ Test 3: Dokumenty dla {pierwszyKontrahent.Kontrahent,-10}...                                 ║");
                    txtRaport.Text += sb.ToString();

                    var sw3 = Stopwatch.StartNew();
                    var docs = await service.PobierzDokumentyAsync(pierwszyKontrahent.Id, DateTime.Today.AddMonths(-3), DateTime.Today);
                    sw3.Stop();

                    sb.Clear();
                    sb.AppendLine($"║   Wynik: {docs.Count} dokumentów w {sw3.ElapsedMilliseconds}ms                                   ║");
                    txtRaport.Text += sb.ToString();
                }

                // Podsumowanie
                sb.Clear();
                sb.AppendLine("║                                                                         ║");
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════╣");
                sb.AppendLine("║                         PODSUMOWANIE                                    ║");
                sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════╣");

                if (sw1.ElapsedMilliseconds < 100 && sw2.ElapsedMilliseconds < 10)
                {
                    sb.AppendLine("║ ✓ CACHE DZIAŁA POPRAWNIE!                                              ║");
                    sb.AppendLine("║   Dane są ładowane z pamięci, bez odpytywania bazy.                    ║");
                }
                else if (sw1.ElapsedMilliseconds < 3000)
                {
                    sb.AppendLine("║ ○ PIERWSZE ŁADOWANIE OK                                                 ║");
                    sb.AppendLine("║   Kolejne pobrania będą błyskawiczne (z cache).                        ║");
                }
                else
                {
                    sb.AppendLine("║ ✗ WOLNE ŁADOWANIE                                                       ║");
                    sb.AppendLine("║   Sprawdź połączenie z serwerem 192.168.0.112                          ║");
                }

                sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();

                txtRaport.Text += sb.ToString();

                // Odśwież pełny raport
                await Task.Delay(500);
                OdswiezRaport();
            }
            catch (Exception ex)
            {
                sb.Clear();
                sb.AppendLine($"║ ✗ BŁĄD: {ex.Message,-60} ║");
                sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════╝");
                txtRaport.Text += sb.ToString();
            }
        }
    }
}
