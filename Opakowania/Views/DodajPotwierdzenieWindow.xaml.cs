using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Okno dialogowe do dodawania potwierdzenia salda opakowania
    /// </summary>
    public partial class DodajPotwierdzenieWindow : Window
    {
        private readonly DodajPotwierdzenieViewModel _viewModel;

        public DodajPotwierdzenieWindow(
            int kontrahentId,
            string kontrahentNazwa,
            string kontrahentShortcut,
            TypOpakowania typOpakowania,
            int saldoSystemowe,
            string userId)
        {
            InitializeComponent();

            _viewModel = new DodajPotwierdzenieViewModel(
                kontrahentId,
                kontrahentNazwa,
                kontrahentShortcut,
                typOpakowania,
                saldoSystemowe,
                userId);

            DataContext = _viewModel;

            // Subskrybuj event zamknięcia
            _viewModel.RequestClose += OnRequestClose;
            _viewModel.WybierzZalacznikRequested += OnWybierzZalacznik;
        }

        private void OnRequestClose(bool? dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        private void OnWybierzZalacznik()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Wybierz załącznik",
                Filter = "Wszystkie pliki|*.*|Obrazy|*.jpg;*.jpeg;*.png;*.bmp|Dokumenty PDF|*.pdf|Dokumenty Word|*.doc;*.docx",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.SciezkaZalacznika = dialog.FileName;
            }
        }

        private async void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = await _viewModel.ZapiszAsync();
                if (success)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Nie pozwalaj na maksymalizację dla okna dialogowego
                return;
            }
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.RequestClose -= OnRequestClose;
            _viewModel.WybierzZalacznikRequested -= OnWybierzZalacznik;
            base.OnClosed(e);
        }
    }
}
