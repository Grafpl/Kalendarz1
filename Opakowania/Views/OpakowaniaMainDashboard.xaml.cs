using System.Windows;
using System.Windows.Input;
using Kalendarz1.Opakowania.ViewModels;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.Views
{
    /// <summary>
    /// Dashboard główny modułu opakowań zwrotnych
    /// </summary>
    public partial class OpakowaniaMainDashboard : Window
    {
        private readonly DashboardViewModel _viewModel;

        public OpakowaniaMainDashboard(string userId)
        {
            InitializeComponent();
            _viewModel = new DashboardViewModel(userId);
            DataContext = _viewModel;
        }

        /// <summary>
        /// Otwiera okno diagnostyki wydajności
        /// </summary>
        private void BtnDiagnostyka_Click(object sender, RoutedEventArgs e)
        {
            var diagWindow = new DiagnostykaWindow();
            diagWindow.Owner = this;
            diagWindow.ShowDialog();
        }

        /// <summary>
        /// Wymusza pełne odświeżenie danych (invaliduje cache)
        /// </summary>
        private void BtnForceRefresh_Click(object sender, RoutedEventArgs e)
        {
            SaldaService.InvalidateAllCaches();
            _viewModel.OdswiezCommand.Execute(null);
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla E2
        /// </summary>
        private void TileE2_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("E2", "Pojemnik Drobiowy E2");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla H1
        /// </summary>
        private void TileH1_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("H1", "Paleta H1");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla EURO
        /// </summary>
        private void TileEURO_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("EURO", "Paleta EURO");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla PCV
        /// </summary>
        private void TilePCV_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("PCV", "Paleta plastikowa");
        }

        /// <summary>
        /// Otwiera listę kontrahentów dla DREW
        /// </summary>
        private void TileDREW_Click(object sender, MouseButtonEventArgs e)
        {
            OtworzListeOpakowania("DREW", "Paleta Drewniana");
        }

        /// <summary>
        /// Otwiera okno listy kontrahentów dla wybranego typu opakowania
        /// </summary>
        private void OtworzListeOpakowania(string kodOpakowania, string nazwaOpakowania)
        {
            var listaWindow = new ListaOpakowanWindow(kodOpakowania, nazwaOpakowania, _viewModel.DataDo, _viewModel.UserId);
            listaWindow.Owner = this;
            listaWindow.ShowDialog();

            // Po zamknięciu odśwież dane
            _viewModel.OdswiezCommand.Execute(null);
        }

        /// <summary>
        /// Dwuklik na wierszu - otwiera szczegóły kontrahenta
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.WybranyKontrahent != null)
            {
                var szczegolyWindow = new SzczegolyKontrahentaWindow(
                    _viewModel.WybranyKontrahent,
                    _viewModel.DataDo,
                    _viewModel.UserId);
                szczegolyWindow.Owner = this;
                szczegolyWindow.ShowDialog();

                // Po zamknięciu odśwież dane
                _viewModel.OdswiezCommand.Execute(null);
            }
        }
    }
}
