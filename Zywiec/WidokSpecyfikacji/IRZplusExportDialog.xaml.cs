using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kalendarz1.Models.IRZplus;
using Kalendarz1.Services;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    public partial class IRZplusExportDialog : Window
    {
        private readonly IRZplusExportService _exportService;
        private readonly DateTime _dataUboju;
        private readonly List<PozycjaEksportuIRZ> _pozycje;
        private readonly string _numerProducenta;
        private readonly string _numerRzezni;

        public string WyeksportowanyPlik { get; private set; }
        public bool Sukces { get; private set; }

        /// <summary>
        /// Konstruktor okna eksportu
        /// </summary>
        /// <param name="dataUboju">Data uboju</param>
        /// <param name="pozycje">Lista pozycji do eksportu</param>
        /// <param name="numerProducenta">Numer producenta (rzezni)</param>
        /// <param name="numerRzezni">Numer rzezni</param>
        public IRZplusExportDialog(
            DateTime dataUboju,
            List<PozycjaEksportuIRZ> pozycje,
            string numerProducenta = "039806095",
            string numerRzezni = "039806095-001")
        {
            InitializeComponent();

            _exportService = new IRZplusExportService();
            _dataUboju = dataUboju;
            _pozycje = pozycje ?? new List<PozycjaEksportuIRZ>();
            _numerProducenta = numerProducenta;
            _numerRzezni = numerRzezni;

            WypelnijPodsumowanie();
        }

        /// <summary>
        /// Alternatywny konstruktor przyjmujacy dowolna kolekcje z mapowaniem
        /// </summary>
        public static IRZplusExportDialog UtworzZDanych<T>(
            DateTime dataUboju,
            IEnumerable<T> items,
            Func<T, int, PozycjaEksportuIRZ> mapper,
            string numerProducenta = "039806095",
            string numerRzezni = "039806095-001")
        {
            var pozycje = items.Select((item, idx) => mapper(item, idx + 1)).ToList();
            return new IRZplusExportDialog(dataUboju, pozycje, numerProducenta, numerRzezni);
        }

        private void WypelnijPodsumowanie()
        {
            txtDataUboju.Text = _dataUboju.ToString("yyyy-MM-dd (dddd)");
            txtLiczbaPozycji.Text = _pozycje.Count.ToString("N0");
            txtSumaSztuk.Text = _pozycje.Sum(p => p.LiczbaSztuk).ToString("N0");

            var sumaWagi = _pozycje.Sum(p => p.WagaKg ?? 0);
            txtSumaWagi.Text = sumaWagi > 0 ? $"{sumaWagi:N2} kg" : "brak danych";

            var hodowcy = _pozycje
                .Select(p => p.NumerPartiiDrobiu?.Split('-').FirstOrDefault())
                .Where(h => !string.IsNullOrEmpty(h))
                .Distinct()
                .ToList();
            txtHodowcy.Text = hodowcy.Count > 0
                ? $"{hodowcy.Count} ({string.Join(", ", hodowcy.Take(3))}{(hodowcy.Count > 3 ? "..." : "")})"
                : "brak danych";
        }

        private void BtnEksportuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filePath;
                string typDokumentu;

                if (rbZURD.IsChecked == true)
                {
                    // Eksport ZURD
                    typDokumentu = "Zgloszenie Uboju Drobiu w Rzezni (ZURD)";
                    var dane = new EksportZURD
                    {
                        NumerProducenta = _numerProducenta,
                        NumerRzezni = _numerRzezni,
                        NumerPartiiUboju = EksportZURD.GenerujNumerPartiiUboju(_dataUboju),
                        GatunekKod = GatunekDrobiu.Kury,
                        DataUboju = _dataUboju,
                        Pozycje = _pozycje
                    };

                    filePath = rbXML.IsChecked == true
                        ? _exportService.EksportujZURD_XML(dane)
                        : _exportService.EksportujZURD_CSV(dane);
                }
                else
                {
                    // Eksport ZSSD - grupuj po hodowcach
                    typDokumentu = "Zgloszenie Zmiany Stanu Stada Drobiu (ZSSD)";

                    // Dla ZSSD zmieniamy typ zdarzenia na RU (Rozchod - przekazanie do uboju)
                    var pozycjeZSSD = _pozycje.Select(p => new PozycjaEksportuIRZ
                    {
                        Lp = p.Lp,
                        NumerPartiiDrobiu = p.NumerPartiiDrobiu,
                        TypZdarzenia = TypZdarzeniaZSSD.RozchodUboj,
                        LiczbaSztuk = p.LiczbaSztuk,
                        DataZdarzenia = p.DataZdarzenia,
                        PrzyjeteZDzialalnosci = _numerRzezni, // Dla ZSSD to numer rzezni do ktorej przekazano
                        WagaKg = p.WagaKg,
                        Uwagi = $"Przekazane do uboju w rzezni {_numerRzezni}"
                    }).ToList();

                    // Pobierz pierwszy numer siedliska jako glowny
                    var numerSiedliska = _pozycje.FirstOrDefault()?.NumerPartiiDrobiu?.Split('-').FirstOrDefault() ?? "NIEZNANY";

                    var dane = new EksportZSSD
                    {
                        NumerSiedliskaHodowcy = numerSiedliska,
                        GatunekKod = GatunekDrobiu.Kury,
                        DataZdarzenia = _dataUboju,
                        Pozycje = pozycjeZSSD
                    };

                    filePath = rbXML.IsChecked == true
                        ? _exportService.EksportujZSSD_XML(dane)
                        : _exportService.EksportujZSSD_CSV(dane);
                }

                WyeksportowanyPlik = filePath;
                Sukces = true;

                // Pokaz raport
                var raport = _exportService.GenerujRaport(
                    typDokumentu,
                    filePath,
                    _pozycje.Count,
                    _pozycje.Sum(p => p.LiczbaSztuk),
                    _dataUboju);

                MessageBox.Show(raport, "Eksport zakonczony", MessageBoxButton.OK, MessageBoxImage.Information);

                // Otworz folder z zaznaczonym plikiem
                _exportService.OtworzFolderZPlikiem(filePath);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Blad podczas eksportu:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Blad eksportu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnOtworzFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _exportService.OtworzFolderEksportu();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
