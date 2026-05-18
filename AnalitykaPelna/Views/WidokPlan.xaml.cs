using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna.Views
{
    public partial class WidokPlan : UserControl
    {
        private readonly PrognozaService _service = new();
        private List<SuroweDanePrognozy> _surowe = new();
        private List<PrognozaWiersz> _aktualne = new();
        private FiltryAnaliz _filtry = new();

        public WidokPlan()
        {
            InitializeComponent();
        }

        public async Task ZastosujFiltryAsync(FiltryAnaliz f)
        {
            _filtry = f;
            try
            {
                _surowe = await _service.LoadSurowePrognozyAsync(f);
                OdswiezWidok();
            }
            catch (System.Exception ex)
            {
                _surowe.Clear();
                _aktualne.Clear();
                dgPrognoza.ItemsSource = null;
                txtInfo.Text = "Błąd ładowania: " + ex.Message;
                throw;  // główne okno ładnie obsłuży i pokaże w pasku statusu
            }
        }

        private void WidokRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (dgPrognoza == null) return;
            OdswiezWidok();
        }

        private void OdswiezWidok()
        {
            var widok = AktualnyWidok();
            _aktualne = _service.AgregujPrognoze(_surowe, widok, _filtry.LiczbaTygodniPrognozy);
            dgPrognoza.ItemsSource = _aktualne;

            var p = _service.BudujPodsumowanie(_aktualne, _filtry.LiczbaTygodniPrognozy);
            kpiSumaTygodnia.Wartosc = p.SredniaTygodniowa.ToString("N0");
            kpiDzienMax.Wartosc = string.IsNullOrEmpty(p.DzienMaxNazwa) ? "—" : p.DzienMaxNazwa;
            kpiDzienMax.Jednostka = p.DzienMaxKg > 0 ? $"{p.DzienMaxKg:N0} kg" : "";
            kpiDzienMin.Wartosc = string.IsNullOrEmpty(p.DzienMinNazwa) ? "—" : p.DzienMinNazwa;
            kpiDzienMin.Jednostka = p.DzienMinKg > 0 ? $"{p.DzienMinKg:N0} kg" : "";
            kpiTygodni.Wartosc = p.LiczbaTygodni.ToString();
            kpiLiczbaPozycji.Wartosc = _aktualne.Count.ToString();

            string nazwaWidoku = NazwaWidoku(widok);
            txtInfo.Text = _aktualne.Count == 0
                ? "Brak danych w wybranym zakresie"
                : $"Próbka: {p.DataOdAnaliza:dd.MM.yyyy} – {p.DataDoAnaliza:dd.MM.yyyy}  •  " +
                  $"{_aktualne.Count} pozycji w widoku '{nazwaWidoku}'";
        }

        private WidokPrognozy AktualnyWidok()
        {
            if (rbOdbiorcy.IsChecked == true) return WidokPrognozy.Odbiorcy;
            if (rbHandlowcy.IsChecked == true) return WidokPrognozy.Handlowcy;
            return WidokPrognozy.Towary;
        }

        private static string NazwaWidoku(WidokPrognozy w) => w switch
        {
            WidokPrognozy.Towary => "Towary",
            WidokPrognozy.Odbiorcy => "Odbiorcy",
            WidokPrognozy.Handlowcy => "Handlowcy",
            _ => "?"
        };

        private void BtnEksport_Click(object sender, RoutedEventArgs e) => EksportujCsv();

        public void EksportujCsv()
        {
            CsvExporter.Eksportuj(_aktualne, $"Plan_{NazwaWidoku(AktualnyWidok())}",
                owner: Window.GetWindow(this));
        }
    }
}
