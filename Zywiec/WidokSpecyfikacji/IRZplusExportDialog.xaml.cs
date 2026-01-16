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
        private readonly ZgloszenieZURD _zgloszenie;

        public string WyeksportowanyPlik { get; private set; }
        public bool Sukces { get; private set; }

        /// <summary>
        /// Konstruktor - przyjmuje JEDNO zgloszenie z WIELOMA pozycjami
        /// </summary>
        public IRZplusExportDialog(ZgloszenieZURD zgloszenie)
        {
            InitializeComponent();

            _exportService = new IRZplusExportService();
            _zgloszenie = zgloszenie ?? throw new ArgumentNullException(nameof(zgloszenie));

            WypelnijPodsumowanie();
            dgPodglad.ItemsSource = _zgloszenie.Pozycje.OrderBy(p => p.Lp).ToList();
        }

        /// <summary>
        /// Statyczna metoda fabrykujaca - tworzy zgloszenie z listy danych
        /// Kazdy element listy (transport/aut) staje sie POZYCJA w jednym zgloszeniu
        /// </summary>
        public static IRZplusExportDialog UtworzZDanych<T>(
            DateTime dataUboju,
            IEnumerable<T> transporty,
            Func<T, int, PozycjaZgloszeniaIRZ> mapujNaPozycje,
            string numerRzezni = "039806095-001",
            string numerProducenta = "039806095",
            string gatunek = "KURY")
        {
            var pozycje = transporty.Select((t, idx) => mapujNaPozycje(t, idx + 1)).ToList();

            var zgloszenie = new ZgloszenieZURD
            {
                Gatunek = gatunek,
                NumerRzezni = numerRzezni,
                NumerProducenta = numerProducenta,
                NumerPartiiUboju = ZgloszenieZURD.GenerujNumerPartiiUboju(dataUboju),
                DataUboju = dataUboju,
                Pozycje = pozycje
            };

            return new IRZplusExportDialog(zgloszenie);
        }

        private void WypelnijPodsumowanie()
        {
            txtDataUboju.Text = _zgloszenie.DataUboju.ToString("yyyy-MM-dd (dddd)");
            txtNumerRzezni.Text = _zgloszenie.NumerRzezni;
            txtNumerPartii.Text = _zgloszenie.NumerPartiiUboju;
            txtGatunek.Text = _zgloszenie.Gatunek;

            txtLiczbaPozycji.Text = _zgloszenie.LiczbaPozycji.ToString("N0");
            txtLiczbaHodowcow.Text = _zgloszenie.LiczbaHodowcow.ToString();
            txtSumaSztuk.Text = _zgloszenie.SumaLiczbaSztuk.ToString("N0");
            txtSumaMasa.Text = $"{_zgloszenie.SumaMasaKg:N2} kg";
        }

        private void BtnEksportuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pobierz sciezke eksportu z ustawien IRZplus
                var irzService = new IRZplusService();
                var settings = irzService.GetSettings();
                var bazowaScieszka = settings.LocalExportPath;

                var formatPliku = rbCSV.IsChecked == true ? "CSV" : "XML";
                var trybEksportu = rbPojedynczo.IsChecked == true ? "POJEDYNCZO" : "RAZEM";
                string typDokumentu;

                if (rbZURD.IsChecked == true)
                {
                    typDokumentu = "ZURD";

                    if (rbPojedynczo.IsChecked == true)
                    {
                        // EKSPORT POJEDYNCZO - kazdy transport do osobnego pliku
                        List<ExportResult> wyniki;

                        if (rbCSV.IsChecked == true)
                        {
                            wyniki = _exportService.EksportujTransportyPojedynczoZStruktura_CSV(
                                _zgloszenie.DataUboju,
                                _zgloszenie.Pozycje,
                                bazowaScieszka);
                        }
                        else
                        {
                            wyniki = _exportService.EksportujTransportyPojedynczoZStruktura_XML(
                                _zgloszenie.DataUboju,
                                _zgloszenie.Pozycje,
                                bazowaScieszka);
                        }

                        var sukcesy = wyniki.Count(r => r.Success);
                        var bledy = wyniki.Count(r => !r.Success);

                        if (sukcesy > 0)
                        {
                            WyeksportowanyPlik = wyniki.First(r => r.Success).FilePath;
                            Sukces = true;

                            var folder = System.IO.Path.GetDirectoryName(WyeksportowanyPlik);

                            var msg = $"EKSPORT POJEDYNCZY ZAKONCZONY\n\n" +
                                $"Wyeksportowano: {sukcesy} plikow\n" +
                                (bledy > 0 ? $"Bledy: {bledy}\n" : "") +
                                $"\nFormat: {formatPliku}\n" +
                                $"Folder: {folder}\n\n" +
                                $"Struktura folderow:\n" +
                                $"  Rok/Miesiac/Dzien-DzienTygodnia/\n" +
                                $"    1-Hodowca1-data.{formatPliku.ToLower()}\n" +
                                $"    2-Hodowca2-data.{formatPliku.ToLower()}\n" +
                                $"    ...\n\n" +
                                $"Kazdy plik to osobne zgloszenie w portalu IRZplus.";

                            if (bledy > 0)
                            {
                                msg += "\n\nBLEDY:\n" + string.Join("\n", wyniki.Where(r => !r.Success).Select(r => r.Message));
                            }

                            MessageBox.Show(msg, "Eksport zakonczony", MessageBoxButton.OK,
                                bledy > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                            _exportService.OtworzFolderZPlikiem(WyeksportowanyPlik);

                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show($"Blad eksportu - zadne pliki nie zostaly utworzone:\n\n" +
                                string.Join("\n", wyniki.Select(r => r.Message)),
                                "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        // EKSPORT RAZEM - wszystko w jednym pliku
                        ExportResult result;

                        if (rbCSV.IsChecked == true)
                        {
                            result = _exportService.EksportujTransportyRazem_CSV(_zgloszenie, bazowaScieszka);
                        }
                        else
                        {
                            result = _exportService.EksportujTransportyRazem_XML(_zgloszenie, bazowaScieszka);
                        }

                        if (result.Success)
                        {
                            WyeksportowanyPlik = result.FilePath;
                            Sukces = true;

                            MessageBox.Show(
                                $"EKSPORT RAZEM ZAKONCZONY POMYSLNIE\n\n" +
                                $"Typ: {typDokumentu}\n" +
                                $"Format: {formatPliku}\n" +
                                $"Plik: {result.FileName}\n\n" +
                                $"Pozycji: {_zgloszenie.LiczbaPozycji}\n" +
                                $"Suma sztuk: {_zgloszenie.SumaLiczbaSztuk:N0}\n" +
                                $"Suma masa: {_zgloszenie.SumaMasaKg:N2} kg\n\n" +
                                $"INSTRUKCJA IMPORTU W PORTALU IRZPLUS:\n" +
                                $"1. Zaloguj sie do portalu IRZplus\n" +
                                $"2. Przejdz do: Zgloszenie uboju drobiu w rzezni\n" +
                                $"3. Uzupelnij NAGLOWEK:\n" +
                                $"   - Gatunek: {_zgloszenie.Gatunek}\n" +
                                $"   - Numer rzezni: {_zgloszenie.NumerRzezni}\n" +
                                $"   - Numer partii uboju: {_zgloszenie.NumerPartiiUboju}\n" +
                                $"4. Kliknij: 'Wczytaj dane z pliku {formatPliku}'\n" +
                                $"5. Wybierz wygenerowany plik\n" +
                                $"6. Sprawdz pozycje i zatwierdz zgloszenie",
                                "Eksport zakonczony",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            _exportService.OtworzFolderZPlikiem(result.FilePath);

                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show($"Blad eksportu:\n\n{result.Message}", "Blad",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    // Dla ZSSD - grupujemy po hodowcach (stara logika)
                    typDokumentu = "ZSSD";

                    var hodowcy = _zgloszenie.Pozycje
                        .GroupBy(p => p.NumerPartiiDrobiu?.Split('-').FirstOrDefault() ?? "NIEZNANY")
                        .ToList();

                    if (hodowcy.Count > 1)
                    {
                        MessageBox.Show(
                            $"Uwaga: W zgloszeniu jest {hodowcy.Count} roznych hodowcow.\n" +
                            $"Zostanie wygenerowany plik dla pierwszego hodowcy: {hodowcy.First().Key}\n\n" +
                            $"Dla pozostalych hodowcow nalezy wygenerowac osobne pliki.",
                            "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    var pierwszyHodowca = hodowcy.First();
                    var zssd = new ZgloszenieZSSD
                    {
                        NumerSiedliska = pierwszyHodowca.Key,
                        Gatunek = _zgloszenie.Gatunek,
                        DataZdarzenia = _zgloszenie.DataUboju,
                        Pozycje = pierwszyHodowca.Select((p, idx) => new PozycjaZgloszeniaIRZ
                        {
                            Lp = idx + 1,
                            TypZdarzenia = TypZdarzeniaZSSD.RozchodUboj,
                            LiczbaSztuk = p.LiczbaSztuk,
                            MasaKg = p.MasaKg,
                            DataZdarzenia = p.DataZdarzenia,
                            PrzyjeteZDzialalnosci = _zgloszenie.NumerRzezni,
                            Uwagi = $"Uboj w rzezni {_zgloszenie.NumerRzezni}"
                        }).ToList()
                    };

                    var result = rbCSV.IsChecked == true
                        ? _exportService.EksportujZSSD_CSV(zssd)
                        : _exportService.EksportujZSSD_XML(zssd);

                    if (result.Success)
                    {
                        WyeksportowanyPlik = result.FilePath;
                        Sukces = true;

                        MessageBox.Show(
                            $"EKSPORT ZSSD ZAKONCZONY POMYSLNIE\n\n" +
                            $"Typ: {typDokumentu}\n" +
                            $"Format: {formatPliku}\n" +
                            $"Plik: {result.FileName}",
                            "Eksport zakonczony",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        _exportService.OtworzFolderZPlikiem(result.FilePath);

                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show($"Blad eksportu:\n\n{result.Message}", "Blad",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad:\n\n{ex.Message}\n\n{ex.StackTrace}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOtworzFolder_Click(object sender, RoutedEventArgs e)
        {
            _exportService.OtworzFolderEksportu();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
