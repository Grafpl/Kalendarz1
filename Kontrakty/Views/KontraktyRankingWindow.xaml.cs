using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>Ranking hodowców wg wolumenu (12 mies.) z flagą kontrakt/ARiMR — dwuklik → akcja.</summary>
    public partial class KontraktyRankingWindow : Window
    {
        private readonly KontraktyService _svc = new();

        public KontraktyRankingWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await ZaladujAsync();
        }

        private async System.Threading.Tasks.Task ZaladujAsync()
        {
            var dane = await _svc.GetRankingHodowcowAsync(80);
            dgRanking.ItemsSource = dane;
            int zUmowa = dane.Count(d => d.MaKontrakt);
            int arimr = dane.Count(d => d.MaArimr);
            txtPodtytul.Text = $"TOP {dane.Count} hodowców • z umową: {zUmowa} • w tym ARiMR: {arimr} • bez umowy: {dane.Count - zUmowa}";
        }

        private async void Dg_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgRanking.SelectedItem is not RankingHodowca h) return;

            if (h.MaKontrakt)
            {
                // otwórz najbliższy aktywny kontrakt tego hodowcy
                var kontrakty = await _svc.GetAktywneKontraktyHodowcyAsync(h.DostawcaId);
                var k = kontrakty.FirstOrDefault();
                if (k != null) { new KontraktyKartaWindow(k.Id) { Owner = this }.ShowDialog(); return; }
            }
            // bez umowy (lub nie znaleziono) → kreator z prefillem hodowcy
            new KontraktKreatorWindow(h.DostawcaId) { Owner = this }.ShowDialog();
            await ZaladujAsync();
        }
    }
}
