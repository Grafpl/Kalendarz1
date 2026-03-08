using System;
using System.Windows;
using Kalendarz1.Partie.Services;

namespace Kalendarz1.Partie.Windows
{
    public partial class DostawcaComparisonWindow : Window
    {
        private readonly PartiaService _service;
        private readonly string _dataOd;
        private readonly string _dataDo;

        public DostawcaComparisonWindow(string dataOd, string dataDo)
        {
            InitializeComponent();
            _service = new PartiaService();
            _dataOd = dataOd;
            _dataDo = dataDo;

            TxtOkres.Text = $"Okres: {dataOd} - {dataDo}";
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                var data = await _service.GetDostawcaComparisonAsync(_dataOd, _dataDo);
                gridComparison.ItemsSource = data;
                TxtFooter.Text = $"Dostawcow: {data.Count} | Okres: {_dataOd} - {_dataDo}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}
