using System.Linq;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna.Windows
{
    /// <summary>
    /// Dialog wyświetlający pełen szczegół jednego etapu łańcucha produkcji.
    /// 5 zakładek: Dokumenty, Towary, Per dzień, Per magazyn, Kontrahenci.
    /// </summary>
    public partial class FlowChainEtapDialog : Window
    {
        public FlowChainEtapDialog(FlowChainEtapDetail detail)
        {
            InitializeComponent();
            ZaladujDane(detail);
        }

        private void ZaladujDane(FlowChainEtapDetail det)
        {
            // Header
            Title = $"Szczegóły etapu: {det.EtapNazwa}";
            txtIkona.Text = det.EtapIkona;
            txtNazwa.Text = det.EtapNazwa;
            txtOpis.Text = det.EtapOpis;
            txtZakresDat.Text = $"📅 {det.DataOd:dd.MM.yyyy} – {det.DataDo:dd.MM.yyyy}  ({det.LiczbaDni} dni)";

            // Header background = kolor etapu
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(det.EtapKolor);
                bdHeader.Background = new SolidColorBrush(c);
            }
            catch { }

            // KPI strip
            txtKpiKg.Text = det.SumaKg.ToString("N0");
            txtKpiDok.Text = det.LiczbaDokumentow.ToString("N0");
            txtKpiPozycji.Text = det.LiczbaPozycji.ToString("N0");
            txtKpiDni.Text = det.LiczbaDni.ToString("N0");
            txtKpiSrednia.Text = det.SredniaKgDzien.ToString("N0");

            // Top towary chips (top 10)
            var topTowary = det.Towary
                .OrderByDescending(t => t.Kg)
                .Take(10)
                .ToList();
            icTopTowary.ItemsSource = topTowary;
            bdTopTowary.Visibility = topTowary.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // DataGrids
            dgDokumenty.ItemsSource = det.Dokumenty;
            dgTowary.ItemsSource = det.Towary;
            dgDni.ItemsSource = det.PerDzien;
            dgMagazyny.ItemsSource = det.PerMagazyn;
            dgKontrahenci.ItemsSource = det.Kontrahenci;

            // Footer
            txtFooterInfo.Text = $"📄 {det.LiczbaDokumentow:N0} dok.  •  📋 {det.LiczbaPozycji:N0} poz.  •  🥩 {det.Towary.Count} towarów  •  🏭 {det.PerMagazyn.Count} magazynów  •  👥 {det.Kontrahenci.Count} kontrahentów";
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
