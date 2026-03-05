using System;
using System.Windows;
using Kalendarz1.Partie.Models;
using Kalendarz1.Partie.Services;

namespace Kalendarz1.Partie.Windows
{
    public partial class OtworzPartieDialog : Window
    {
        private readonly PartiaService _service;
        private readonly PartiaModel _partia;

        public OtworzPartieDialog(PartiaModel partia)
        {
            InitializeComponent();
            _service = new PartiaService();
            _partia = partia;

            txtHeader.Text = $"PONOWNE OTWARCIE PARTII {_partia.Partia}";
            txtSubHeader.Text = $"Dostawca: {_partia.CustomerName}";
            runZamknieta.Text = $"{_partia.CloseData} {_partia.CloseGodzina}";
            runPrzez.Text = _partia.ZamknalNazwa;

            txtPowod.Focus();
        }

        private async void BtnOtworz_Click(object sender, RoutedEventArgs e)
        {
            string powod = txtPowod.Text?.Trim();
            if (string.IsNullOrEmpty(powod))
            {
                MessageBox.Show("Podaj powod ponownego otwarcia.", "Wymagane pole",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPowod.Focus();
                return;
            }

            BtnOtworz.IsEnabled = false;
            try
            {
                bool ok = await _service.ReopenPartiaAsync(_partia.Partia, App.UserID, powod);

                if (ok)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Nie udalo sie otworzyc partii. Moze juz jest otwarta.",
                        "Informacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwierania partii:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnOtworz.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
