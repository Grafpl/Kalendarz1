using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Kalendarz1.ZSRIR.Services;

namespace Kalendarz1.ZSRIR.Views
{
    public partial class ZsrirHistoryDialog : Window
    {
        private readonly ZsrirSubmissionsRepo _repo = new();
        public ObservableCollection<HistRow> Rows { get; } = new();

        public ZsrirHistoryDialog()
        {
            InitializeComponent();
            dgHistory.ItemsSource = Rows;
            Loaded += async (s, e) =>
            {
                var rows = await _repo.GetRecentAsync(200);
                foreach (var r in rows) Rows.Add(HistRow.From(r));
                lblCount.Text = $"· {rows.Count} wysyłek";
            };
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
