using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.AnalitykaPelna.Models;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.AnalitykaPelna.Windows
{
    public partial class WazeniaPartiiDialog : Window
    {
        private readonly List<WazenieRekord> _wazenia;
        private readonly string _partia;

        public WazeniaPartiiDialog(string partia, string hodowca, List<WazenieRekord> wazenia)
        {
            InitializeComponent();
            _partia = partia;
            _wazenia = wazenia ?? new List<WazenieRekord>();

            txtTytul.Text = $"Partia: {partia}";
            txtPodtytul.Text = string.IsNullOrEmpty(hodowca)
                ? $"{_wazenia.Count} ważeń"
                : $"Hodowca: {hodowca}  •  {_wazenia.Count} ważeń";

            BudujStatystyki();
            dgWazenia.ItemsSource = _wazenia.OrderBy(w => w.Godzina).ToList();

            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        }

        private void BudujStatystyki()
        {
            int liczba = _wazenia.Count(w => w.ActWeight > 0);
            int anulacje = _wazenia.Count(w => w.ActWeight < 0);
            decimal suma = _wazenia.Where(w => w.ActWeight > 0).Sum(w => w.ActWeight);
            int towary = _wazenia.Select(w => w.NazwaTowaru).Where(s => !string.IsNullOrEmpty(s)).Distinct().Count();
            int operatorzy = _wazenia.Select(w => w.OperatorID).Where(s => !string.IsNullOrEmpty(s)).Distinct().Count();

            txtLiczbaWazen.Text = liczba.ToString("N0");
            txtAnulacje.Text = anulacje.ToString("N0");
            txtSumaKg.Text = suma.ToString("N0");
            txtTowarow.Text = towary.ToString();
            txtOperatorow.Text = operatorzy.ToString();
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
            => CsvExporter.Eksportuj(_wazenia.OrderBy(w => w.Godzina), $"Wazenia_Partia_{_partia}",
                owner: this);

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();
    }
}
