using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Partie.Models;
using Kalendarz1.Partie.Services;

namespace Kalendarz1.Partie.Windows
{
    public partial class ZamknijPartieDialog : Window
    {
        private readonly PartiaService _service;
        private readonly PartiaModel _partia;
        private List<ChecklistItem> _checklist;
        private List<QCNormaModel> _normy;
        private bool _qcComplete;

        public ZamknijPartieDialog(PartiaModel partia)
        {
            InitializeComponent();
            _service = new PartiaService();
            _partia = partia;

            PopulateData();
            Loaded += async (s, e) => await LoadNormyAndChecklist();
        }

        private void PopulateData()
        {
            txtHeader.Text = $"ZAMKNIECIE PARTII {_partia.Partia}";
            txtSubHeader.Text = $"Dzial: {_partia.DirID}  |  Status: {_partia.StatusText}";
            txtDostawca.Text = $"{_partia.CustomerName} ({_partia.CustomerID})";
            txtDataOtwarcia.Text = $"{_partia.CreateData} {_partia.CreateGodzina}";
            txtOtworzyl.Text = _partia.OtworzylNazwa;

            txtWydano.Text = $"{_partia.WydanoKg:N1} kg ({_partia.WydanoSzt} szt)";
            txtPrzyjeto.Text = $"{_partia.PrzyjetoKg:N1} kg ({_partia.PrzyjetoSzt} szt)";
            txtNaStanie.Text = $"{_partia.NaStanieKg:N1} kg";
            txtWydajnosc.Text = _partia.WydajnoscProc.HasValue
                ? $"{_partia.WydajnoscProc:N1}%"
                : "- (brak danych skupu)";

            // Warnings
            if (_partia.NaStanieKg > 10)
            {
                borderWarning.Visibility = Visibility.Visible;
                txtWarning.Text = $"Na stanie zostalo {_partia.NaStanieKg:N1} kg produktu! Czy na pewno chcesz zamknac?";
            }
        }

        private async System.Threading.Tasks.Task LoadNormyAndChecklist()
        {
            try
            {
                _normy = await _service.GetNormyAsync();
            }
            catch
            {
                _normy = new List<QCNormaModel>();
            }

            _checklist = _service.BuildQCChecklist(_partia, _normy);
            ChecklistItems.ItemsSource = _checklist;

            // Evaluate QC completeness
            int total = _checklist.Count;
            int ok = _checklist.Count(c => c.IsChecked);
            int warnings = _checklist.Count(c => c.IsWarning);
            _qcComplete = ok == total && warnings == 0;

            if (_qcComplete)
            {
                borderQCSummary.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x3D, 0x1A));
                txtQCSummary.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
                txtQCSummary.Text = $"QC KOMPLETNE ({ok}/{total})  - partia zostanie zamknieta jako 'Zamknieta'";
            }
            else if (ok > 0)
            {
                borderQCSummary.Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x3A, 0x1A));
                txtQCSummary.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xAF, 0x37));
                txtQCSummary.Text = $"QC NIEKOMPLETNE ({ok}/{total})" +
                    (warnings > 0 ? $", {warnings} ostrzezen" : "") +
                    " - partia zostanie zamknieta jako 'Zamknieta z brakami'";
            }
            else
            {
                borderQCSummary.Background = new SolidColorBrush(Color.FromRgb(0x3D, 0x1A, 0x1A));
                txtQCSummary.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));
                txtQCSummary.Text = $"QC BRAK (0/{total}) - partia zostanie zamknieta jako 'Zamknieta z brakami'";
            }
        }

        private async void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            // Confirm if QC incomplete
            if (!_qcComplete)
            {
                var result = MessageBox.Show(
                    "Kontrola jakosci nie jest kompletna. Partia zostanie zamknieta z brakami.\n\nCzy kontynuowac?",
                    "QC niekompletne",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            BtnZamknij.IsEnabled = false;
            try
            {
                bool ok = await _service.ClosePartiaV2Async(
                    _partia.Partia, App.UserID, txtKomentarz.Text?.Trim(), _qcComplete);

                if (ok)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Nie udalo sie zamknac partii. Moze juz jest zamknieta.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zamykania partii:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZamknij.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
