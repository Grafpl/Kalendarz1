using System;
using System.Diagnostics;
using System.Windows;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Szczegóły kontrahenta - salda i dokumenty
    /// </summary>
    public partial class SzczegolyKontrahentaWindow : Window
    {
        private readonly SzczegolyKontrahentaViewModel _viewModel;

        public SzczegolyKontrahentaWindow(SaldoKontrahenta kontrahent, DateTime dataDo, string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _viewModel = new SzczegolyKontrahentaViewModel(kontrahent, dataDo, userId);
            _viewModel.RequestOpenPotwierdzenieDialog = OtworzDialogPotwierdzenia;
            DataContext = _viewModel;
        }

        /// <summary>
        /// Konstruktor alternatywny dla SaldoKontrahentaOpakowania
        /// </summary>
        public SzczegolyKontrahentaWindow(SaldoKontrahentaOpakowania kontrahent, DateTime dataDo, string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Konwertuj na SaldoKontrahenta
            var saldo = new SaldoKontrahenta
            {
                Id = kontrahent.Id,
                Kontrahent = kontrahent.Kontrahent,
                Nazwa = kontrahent.Nazwa,
                Handlowiec = kontrahent.Handlowiec,
                E2 = kontrahent.E2,
                H1 = kontrahent.H1,
                EURO = kontrahent.EURO,
                PCV = kontrahent.PCV,
                DREW = kontrahent.DREW,
                OstatniDokument = kontrahent.OstatniDokument,
                Telefon = kontrahent.Telefon,
                Email = kontrahent.Email
            };

            _viewModel = new SzczegolyKontrahentaViewModel(saldo, dataDo, userId);
            _viewModel.RequestOpenPotwierdzenieDialog = OtworzDialogPotwierdzenia;
            DataContext = _viewModel;
        }

        private void BtnWstecz_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnEmail_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_viewModel.Kontrahent?.Email))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"mailto:{_viewModel.Kontrahent.Email}?subject=Saldo opakowań",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można otworzyć klienta email: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Brak adresu email kontrahenta.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnZadzwon_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_viewModel.Kontrahent?.Telefon))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"tel:{_viewModel.Kontrahent.Telefon}",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można wykonać połączenia: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("Brak numeru telefonu kontrahenta.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Otwiera dialog potwierdzenia salda
        /// </summary>
        private void OtworzDialogPotwierdzenia(string typOpakowania, int saldoSystemowe)
        {
            var dialog = new DodajPotwierdzenieWindow(
                _viewModel.Kontrahent.Id,
                _viewModel.Kontrahent.Kontrahent,
                _viewModel.Kontrahent.Nazwa,
                typOpakowania,
                saldoSystemowe,
                _viewModel.UserId);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Odśwież dane po dodaniu potwierdzenia
                _viewModel.OdswiezPotwierdzeniaCommand.Execute(null);
                MessageBox.Show("Potwierdzenie salda zostało zapisane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
