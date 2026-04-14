using System;
using System.Windows;
using Microsoft.Win32;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    public partial class DodajPotwierdzenieWindow : Window
    {
        private readonly DodajPotwierdzenieViewModel _vm;

        public DodajPotwierdzenieWindow(int kontrahentId, string kontrahentNazwa, SaldoOpakowania saldo, string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _vm = new DodajPotwierdzenieViewModel(kontrahentId, kontrahentNazwa, kontrahentNazwa, userId,
                saldo?.SaldoE2 ?? 0, saldo?.SaldoH1 ?? 0, saldo?.SaldoEURO ?? 0, saldo?.SaldoPCV ?? 0, saldo?.SaldoDREW ?? 0);
            DataContext = _vm;
        }

        public DodajPotwierdzenieWindow(int kontrahentId, string kontrahentNazwa, string kontrahentShortcut,
            TypOpakowania typOpakowania, int saldoSystemowe, string userId)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            int e2 = 0, h1 = 0, euro = 0, pcv = 0, drew = 0;
            switch (typOpakowania?.Kod)
            {
                case "E2": e2 = saldoSystemowe; break;
                case "H1": h1 = saldoSystemowe; break;
                case "EURO": euro = saldoSystemowe; break;
                case "PCV": pcv = saldoSystemowe; break;
                case "DREW": drew = saldoSystemowe; break;
            }
            _vm = new DodajPotwierdzenieViewModel(kontrahentId, kontrahentNazwa, kontrahentShortcut, userId,
                e2, h1, euro, pcv, drew);
            DataContext = _vm;
        }

        private void BtnWybierzSkan_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Wybierz skan potwierdzenia",
                Filter = "Obrazy i PDF|*.jpg;*.jpeg;*.png;*.bmp;*.pdf;*.tif;*.tiff|Wszystkie pliki|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true)
                _vm.SkanSciezka = dlg.FileName;
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (await _vm.ZapiszAsync())
            {
                DialogResult = true;
                Close();
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
