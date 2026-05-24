// ════════════════════════════════════════════════════════════════════════════
// KontraktyListaWindow.xaml.cs — szkielet code-behind
// Część 4 audytu (2026-05-23)
// Target: Kontrakty/Windows/KontraktyListaWindow.xaml.cs
//
// UWAGA: To jest SZKIELET — wiele metod ma "TODO Faza 2/3" placeholder.
// Ser kończy implementację po wdrożeniu Fazy 1 (CRUD + lista).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Windows
{
    public partial class KontraktyListaWindow : Window
    {
        private readonly KontraktyService _svc = new();
        private readonly WordTemplateService _wordSvc = new();
        private readonly ObservableCollection<KontraktDto> _items = new();
        private string _activeFilter = "ACTIVE"; // ACTIVE / EXPIRING / EXPIRED / ALL
        private bool _onlyArimr = false;

        public KontraktyListaWindow()
        {
            InitializeComponent();
            dgKontrakty.ItemsSource = _items;
            Loaded += async (_, _) => await LoadAsync();
        }

        // ────────────────────────────────────────────────────────────────────
        // LOAD + FILTROWANIE
        // ────────────────────────────────────────────────────────────────────

        private async Task LoadAsync()
        {
            try
            {
                _items.Clear();
                var statusFilter = _activeFilter == "ALL" ? null : _activeFilter;
                var lista = await _svc.GetAllAsync(statusFilter);

                if (_activeFilter == "EXPIRING")
                    lista = lista.Where(k => k.Status == "EXPIRING"
                        || (k.DataObowiazujeDo.HasValue && (k.DataObowiazujeDo.Value - DateTime.Today).Days <= 90))
                        .ToList();

                if (_onlyArimr)
                    lista = lista.Where(k => k.LiczySieDoArimr).ToList();

                var search = txtSearch?.Text?.Trim();
                if (!string.IsNullOrEmpty(search))
                    lista = lista.Where(k =>
                        (k.NumerKontraktu?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (k.NazwaHodowcySnapshot?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                        .ToList();

                foreach (var k in lista) _items.Add(k);
                txtStats.Text = $"{_items.Count} kontraktów";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania kontraktów:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => _ = LoadAsync();

        private async void ChipActive_Click(object sender, RoutedEventArgs e)   { _activeFilter = "ACTIVE";   await LoadAsync(); }
        private async void ChipExpiring_Click(object sender, RoutedEventArgs e) { _activeFilter = "EXPIRING"; await LoadAsync(); }
        private async void ChipExpired_Click(object sender, RoutedEventArgs e)  { _activeFilter = "EXPIRED";  await LoadAsync(); }
        private async void ChipArimr_Click(object sender, RoutedEventArgs e)    { _onlyArimr = !_onlyArimr;    await LoadAsync(); }

        // ────────────────────────────────────────────────────────────────────
        // AKCJE TOOLBAR + CONTEXT MENU
        // ────────────────────────────────────────────────────────────────────

        private void BtnNowy_Click(object sender, RoutedEventArgs e)
        {
            // TODO Faza 1: otwórz KontraktyEditorWindow(null) → po zapisie LoadAsync()
            MessageBox.Show("Otwarcie KontraktyEditorWindow (TODO Faza 1).");
        }

        private void BtnPrzedluz_Click(object sender, RoutedEventArgs e)
        {
            // TODO Faza 2: weź zaznaczony wiersz → utworz nowy kontrakt z pre-wypełnionymi polami
            if (dgKontrakty.SelectedItem is not KontraktDto selected)
            {
                MessageBox.Show("Wybierz kontrakt do przedłużenia.");
                return;
            }
            MessageBox.Show($"Przedłużenie kontraktu {selected.NumerKontraktu} (TODO Faza 2).");
        }

        private async void BtnGenerujWord_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontrakty.SelectedItem is not KontraktDto k) return;

            // 1. Wybierz szablon (uproszczone: ARIMR_3LAT_v1 dla typu ARIMR_3LAT)
            var templatePath = k.TypKontraktu switch
            {
                "ARIMR_3LAT" => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_ARIMR_3LAT.docx",
                "WIECZNY"    => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Wieczna.docx",
                "ROCZNY"     => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Roczna.docx",
                _ => @"\\192.168.0.170\Install\UmowyZakupu\_SZABLON\Umowa_Spot.docx"
            };

            var nazwiskoSan = SanitizeForFilename(k.NazwaHodowcySnapshot ?? "Hodowca");
            var outputPath = $@"\\192.168.0.170\Install\UmowyZakupu\{k.Rok}\Umowa_{nazwiskoSan}_{k.NumerKontraktu.Replace('/', '_')}.docx";

            try
            {
                var values = _wordSvc.BuildValuesFromKontrakt(k);
                _wordSvc.GenerateContract(templatePath, outputPath, values);

                // Zaktualizuj ścieżkę + status DRAFT → PRINTED
                k.SciezkaWord = outputPath;
                if (k.Status == "DRAFT")
                    await _svc.ChangeStatusAsync(k.Id, "PRINTED", App.UserID ?? "?");
                await _svc.UpdateAsync(k, App.UserID ?? "?");

                // Otwórz Word
                System.Diagnostics.Process.Start("explorer", $"\"{outputPath}\"");

                MessageBox.Show($"Wygenerowano: {outputPath}\nAsia: skoryguj niestandardowe zapisy → Ctrl+S w Wordzie.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generacji Worda:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDodajSkan_Click(object sender, RoutedEventArgs e)
        {
            // TODO Faza 2: OpenFileDialog → kopiuj PDF do \\server\UmowyZakupu\{Rok}\ → wpis do KontraktyZalaczniki
            MessageBox.Show("Dodaj skan PDF (TODO Faza 2).");
        }

        private void BtnDashboardArimr_Click(object sender, RoutedEventArgs e)
        {
            // TODO Faza 3: otwórz DashboardArimrWindow
            MessageBox.Show("Dashboard ARiMR (TODO Faza 3).");
        }

        private void BtnExportArimr_Click(object sender, RoutedEventArgs e)
        {
            // TODO Faza 3: generuj PDF raport (iTextSharp) z listą aktywnych ARiMR
            MessageBox.Show("Export PDF dla audytu ARiMR (TODO Faza 3).");
        }

        private void DgKontrakty_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgKontrakty.SelectedItem is KontraktDto k)
            {
                // TODO Faza 1: otwórz KontraktyDetailsWindow(k.Id) (read-only widok)
                MessageBox.Show($"Otwarcie szczegółów {k.NumerKontraktu} (TODO Faza 1).");
            }
        }

        private void MenuEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontrakty.SelectedItem is KontraktDto k)
                MessageBox.Show($"Edycja {k.NumerKontraktu} (TODO Faza 1).");
        }

        private void MenuExtend_Click(object sender, RoutedEventArgs e) => BtnPrzedluz_Click(sender, e);
        private void MenuGenerateWord_Click(object sender, RoutedEventArgs e) => BtnGenerujWord_Click(sender, e);
        private void MenuAddScan_Click(object sender, RoutedEventArgs e) => BtnDodajSkan_Click(sender, e);

        private async void MenuStatusSent_Click(object sender, RoutedEventArgs e)
            => await ZmienStatus("SENT");

        private async void MenuStatusSigned_Click(object sender, RoutedEventArgs e)
            => await ZmienStatus("SIGNED");

        private async void MenuStatusActive_Click(object sender, RoutedEventArgs e)
            => await ZmienStatus("ACTIVE");

        private async Task ZmienStatus(string nowy)
        {
            if (dgKontrakty.SelectedItem is not KontraktDto k) return;
            try
            {
                await _svc.ChangeStatusAsync(k.Id, nowy, App.UserID ?? "?");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuTerminate_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontrakty.SelectedItem is not KontraktDto k) return;

            var conf = MessageBox.Show(
                $"Czy na pewno wypowiedzieć kontrakt {k.NumerKontraktu} ({k.NazwaHodowcySnapshot})?\n\n" +
                "Status zmieni się na TERMINATED. Tej operacji nie da się cofnąć przez UI (tylko SQL).",
                "Wypowiedzenie kontraktu",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (conf != MessageBoxResult.Yes) return;

            // TODO Faza 2: dialog z polem powodu + daty wypowiedzenia + okresu wypowiedzenia
            await _svc.ChangeStatusAsync(k.Id, "TERMINATED", App.UserID ?? "?", "Wypowiedzenie z UI");
            await LoadAsync();
        }

        // ────────────────────────────────────────────────────────────────────
        // Helpers

        private static string SanitizeForFilename(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
