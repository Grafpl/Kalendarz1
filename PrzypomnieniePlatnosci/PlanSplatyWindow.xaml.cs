using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace Kalendarz1.PrzypomnieniePlatnosci
{
    public partial class PlanSplatyWindow : Window
    {
        private decimal _kwotaDlugu;
        private string _nazwaKontrahenta;
        private DaneKontrahenta _daneKontrahenta;
        private List<DokumentPlatnosci> _dokumenty;

        public PlanSplatyWindow(decimal kwotaDlugu, string nazwaKontrahenta, DaneKontrahenta daneKontrahenta, List<DokumentPlatnosci> dokumenty)
        {
            InitializeComponent();

            _kwotaDlugu = kwotaDlugu;
            _nazwaKontrahenta = nazwaKontrahenta;
            _daneKontrahenta = daneKontrahenta;
            _dokumenty = dokumenty;

            txtKontrahent.Text = nazwaKontrahenta;
            txtKwotaDlugu.Text = $"{kwotaDlugu:N2} zł";
            dpDataPierwszejRaty.SelectedDate = DateTime.Now.AddDays(7);

            // Wywołaj PrzeliczRaty dopiero po pełnym załadowaniu okna
            this.Loaded += (s, e) => PrzeliczRaty(null, null);
        }

        private void PrzeliczRaty(object sender, RoutedEventArgs e)
        {
            // Zabezpieczenie przed wywołaniem przed inicjalizacją kontrolek
            if (txtLiczbaRat == null || dpDataPierwszejRaty == null ||
                cbCzestotliwosc == null || icRaty == null)
                return;

            if (!int.TryParse(txtLiczbaRat.Text, out int liczbaRat) || liczbaRat < 1)
            {
                liczbaRat = 6;
                txtLiczbaRat.Text = "6";
            }

            if (dpDataPierwszejRaty.SelectedDate == null)
                return;

            DateTime dataPierwszejRaty = dpDataPierwszejRaty.SelectedDate.Value;
            decimal kwotaRaty = Math.Round(_kwotaDlugu / liczbaRat, 2);
            decimal sumaRat = kwotaRaty * (liczbaRat - 1);
            decimal ostatniaRata = _kwotaDlugu - sumaRat;

            int dniPomiedzy = cbCzestotliwosc.SelectedIndex switch
            {
                1 => 4,   // Dwa razy w tygodniu
                2 => 7,   // Tygodniowe
                3 => 14,  // Dwutygodniowe
                _ => 30   // Miesięczne
            };

            var raty = new List<RataInfo>();
            DateTime dataRaty = dataPierwszejRaty;

            for (int i = 1; i <= liczbaRat; i++)
            {
                decimal kwota = i == liczbaRat ? ostatniaRata : kwotaRaty;
                raty.Add(new RataInfo
                {
                    Numer = $"Rata {i}/{liczbaRat}:",
                    Data = dataRaty.ToString("dd.MM.yyyy"),
                    Kwota = $"{kwota:N2} zł"
                });

                dataRaty = dataRaty.AddDays(dniPomiedzy);
            }

            icRaty.ItemsSource = raty;
        }

        private void BtnGenerujPDF_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtLiczbaRat.Text, out int liczbaRat) || liczbaRat < 1)
            {
                MessageBox.Show("Podaj prawidłową liczbę rat (minimum 1).", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpDataPierwszejRaty.SelectedDate == null)
            {
                MessageBox.Show("Wybierz datę pierwszej raty.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Plan_Splaty_{_nazwaKontrahenta}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    DateTime dataPierwszejRaty = dpDataPierwszejRaty.SelectedDate.Value;

                    int dniPomiedzy = cbCzestotliwosc.SelectedIndex switch
                    {
                        1 => 4,
                        2 => 7,
                        3 => 14,
                        _ => 30
                    };

                    var generator = new PlanSplatyPDFGenerator();
                    generator.GenerujPDF(
                        saveDialog.FileName,
                        _daneKontrahenta,
                        _dokumenty,
                        _kwotaDlugu,
                        liczbaRat,
                        dataPierwszejRaty,
                        dniPomiedzy);

                    MessageBox.Show("✓ Plan spłaty został pomyślnie wygenerowany!", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Błąd generowania PDF: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class RataInfo
    {
        public string Numer { get; set; }
        public string Data { get; set; }
        public string Kwota { get; set; }
    }
}