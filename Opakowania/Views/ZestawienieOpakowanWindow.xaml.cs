using System;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.ViewModels;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Główne okno zestawienia opakowań dla wszystkich kontrahentów
    /// </summary>
    public partial class ZestawienieOpakowanWindow : Window
    {
        private readonly ZestawienieOpakowanViewModel _viewModel;

        public ZestawienieOpakowanWindow()
        {
            InitializeComponent();

            // Pobierz UserID z aplikacji
            string userId = App.UserID ?? "11111"; // Domyślnie admin dla testów

            _viewModel = new ZestawienieOpakowanViewModel(userId);
            DataContext = _viewModel;

            // Subskrybuj eventy
            _viewModel.OtworzSzczegolyRequested += OnOtworzSzczegoly;
            _viewModel.DodajPotwierdzenieRequested += OnDodajPotwierdzenie;

            // Aktualizuj wygląd przycisków przy zmianie typu
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.WybranyTypOpakowania))
                {
                    AktualizujWygladPrzyciskowOpakowan();
                }
            };
        }

        #region Event Handlers

        private void OnOtworzSzczegoly(ZestawienieSalda kontrahent)
        {
            if (kontrahent == null || kontrahent.KontrahentId <= 0) return;

            var szczegoly = new SaldoOdbiorcyWindow(
                kontrahent.KontrahentId,
                kontrahent.Kontrahent,
                App.UserID ?? "11111");

            szczegoly.Owner = this;
            szczegoly.ShowDialog();

            // Odśwież dane po powrocie
            _viewModel.OdswiezCommand.Execute(null);
        }

        private void OnDodajPotwierdzenie(ZestawienieSalda kontrahent, TypOpakowania typOpakowania)
        {
            if (kontrahent == null || kontrahent.KontrahentId <= 0) return;

            var okno = new DodajPotwierdzenieWindow(
                kontrahent.KontrahentId,
                kontrahent.Kontrahent,
                kontrahent.Kontrahent,
                typOpakowania,
                kontrahent.IloscDrugiZakres,
                App.UserID ?? "11111");

            okno.Owner = this;
            if (okno.ShowDialog() == true)
            {
                _viewModel.OdswiezCommand.Execute(null);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                OnOtworzSzczegoly(_viewModel.WybranyKontrahent);
            }
        }

        private void DataGridZestawienie_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                OnOtworzSzczegoly(kontrahent);
            }
        }

        private void BtnSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ZestawienieSalda kontrahent)
            {
                OnOtworzSzczegoly(kontrahent);
            }
        }

        private void BtnPotwierdzenie_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ZestawienieSalda kontrahent)
            {
                OnDodajPotwierdzenie(kontrahent, _viewModel.WybranyTypOpakowania);
            }
        }

        private void MenuSzczegoly_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                OnOtworzSzczegoly(kontrahent);
            }
        }

        private void MenuDodajPotwierdzenie_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                OnDodajPotwierdzenie(kontrahent, _viewModel.WybranyTypOpakowania);
            }
        }

        private void MenuZadzwon_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                // TODO: Implementacja dzwonienia
                MessageBox.Show($"Dzwonienie do: {kontrahent.Kontrahent}", "Zadzwoń", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuWyslijEmail_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridZestawienie.SelectedItem is ZestawienieSalda kontrahent)
            {
                // TODO: Implementacja emaila
                MessageBox.Show($"Wysyłanie emaila do: {kontrahent.Kontrahent}", "Email", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuEksportExcel_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EksportExcelCommand?.Execute(null);
        }

        private void MenuEksportPDF_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EksportPDFCommand?.Execute(null);
        }

        #endregion

        #region Window Chrome

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region UI Helpers

        private void AktualizujWygladPrzyciskowOpakowan()
        {
            // Reset wszystkich przycisków
            var buttons = new[] { btnE2, btnH1, btnEURO, btnPCV, btnDREW };
            foreach (var btn in buttons)
            {
                btn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
                btn.Background = System.Windows.Media.Brushes.White;
            }

            // Podświetl wybrany
            var wybranyKod = _viewModel.WybranyTypOpakowania?.Kod;
            System.Windows.Controls.Button wybranyBtn = wybranyKod switch
            {
                "E2" => btnE2,
                "H1" => btnH1,
                "EURO" => btnEURO,
                "PCV" => btnPCV,
                "DREW" => btnDREW,
                _ => null
            };

            if (wybranyBtn != null)
            {
                wybranyBtn.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryGreenBrush");
                wybranyBtn.Background = (System.Windows.Media.Brush)FindResource("PrimaryGreenLightBrush");
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.OtworzSzczegolyRequested -= OnOtworzSzczegoly;
            _viewModel.DodajPotwierdzenieRequested -= OnDodajPotwierdzenie;
            base.OnClosed(e);
        }
    }
}
