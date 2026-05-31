using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>Masowe generowanie dokumentów Word dla wielu kontraktów naraz (3.4).</summary>
    public partial class KontraktyAneksyWindow : Window
    {
        private readonly KontraktyService _svc = new();
        private readonly WordTemplateService _word = new();
        private string? _ostatniFolder;

        public KontraktyAneksyWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ZaladujAsync();
        }

        private async System.Threading.Tasks.Task ZaladujAsync()
        {
            string status = (cbStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "AKTYWNE";
            string typ = (cbTyp.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "WSZYSTKIE";
            var dane = await _svc.GetKontraktyAsync(null, status, typ, false, null);
            dgList.ItemsSource = dane;
            txtLicznik.Text = $"{dane.Count} kontraktów";
            AktualizujLicznik();
        }

        private async void Filtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            await ZaladujAsync();
        }

        private void BtnZaznaczWsz_Click(object sender, RoutedEventArgs e) => dgList.SelectAll();
        private void BtnOdznacz_Click(object sender, RoutedEventArgs e) => dgList.UnselectAll();
        private void Dg_SelChanged(object sender, SelectionChangedEventArgs e) => AktualizujLicznik();

        private void AktualizujLicznik()
        {
            int n = dgList.SelectedItems.Count;
            btnGeneruj.IsEnabled = n > 0;
            btnGeneruj.Content = n > 0 ? $"📄 Generuj zaznaczone ({n})" : "📄 Generuj zaznaczone";
        }

        private async void BtnGeneruj_Click(object sender, RoutedEventArgs e)
        {
            var wybrane = dgList.SelectedItems.OfType<KontraktListItem>().ToList();
            if (wybrane.Count == 0) return;

            if (MessageBox.Show($"Wygenerować dokumenty Word dla {wybrane.Count} kontraktów?\n" +
                "Pliki trafią do folderu rocznika; ścieżka zostanie zapisana przy kontrakcie.",
                "Masowe generowanie", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            btnGeneruj.IsEnabled = false;
            int ok = 0; var bledy = new List<string>();
            foreach (var k in wybrane)
            {
                txtStatus.Text = $"Generuję {k.NumerKontraktu} ({k.Hodowca})…";
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                try
                {
                    string? folder = await GenerujJedenAsync(k.Id);
                    if (folder != null) { ok++; _ostatniFolder = folder; }
                }
                catch (Exception ex) { bledy.Add($"{k.NumerKontraktu}: {ex.Message}"); }
            }

            txtStatus.Text = $"✔ Wygenerowano {ok} z {wybrane.Count}." +
                (bledy.Count > 0 ? $"  Błędy ({bledy.Count}): {string.Join("; ", bledy.Take(3))}" +
                    (bledy.Count > 3 ? " …" : "") : "");
            btnFolder.Visibility = _ostatniFolder != null ? Visibility.Visible : Visibility.Collapsed;
            btnGeneruj.IsEnabled = true;
            AktualizujLicznik();
            if (bledy.Count > 0)
                MessageBox.Show(string.Join("\n", bledy), "Błędy generowania",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>Generuje Word dla jednego kontraktu (bieżąca wersja). Zwraca folder wyjściowy lub rzuca.</summary>
        private async System.Threading.Tasks.Task<string?> GenerujJedenAsync(int kontraktId)
        {
            var det = await _svc.GetDetailAsync(kontraktId);
            var wersje = await _svc.GetWersjeAsync(kontraktId);
            var w = wersje.FirstOrDefault(x => x.IsAktualna) ?? wersje.FirstOrDefault();
            if (det == null || w == null) throw new InvalidOperationException("brak wersji");

            string? szablon = await _svc.GetTemplatePathAsync(det.TypKontraktu);
            if (string.IsNullOrWhiteSpace(szablon) || !File.Exists(szablon))
                throw new FileNotFoundException("brak szablonu dla typu " + det.TypLabel);

            var cykle = await _svc.GetHarmonogramAsync(w.Id);
            var tokeny = WordTemplateService.BuildKontraktacjaTokens(det, w, det.NumerKontraktu);

            string folder = Path.Combine(ZalacznikiHelper.Root, det.Rok.ToString());
            string nazwa = $"Umowa_{ZalacznikiHelper.SanitizeNumer(det.NumerKontraktu)}_{Bezpieczne(det.NazwaHodowcySnapshot ?? "hodowca")}.docx";
            string output = Path.Combine(folder, nazwa);

            _word.GenerujKontraktacja(szablon!, output, tokeny, cykle);
            await _svc.SetSciezkiAsync(w.Id, output, null);
            return folder;
        }

        private void BtnFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_ostatniFolder == null) return;
            try { Process.Start(new ProcessStartInfo(_ostatniFolder) { UseShellExecute = true }); } catch { }
        }

        private static string Bezpieczne(string s)
        { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace('/', '-').Trim(); }
    }
}
