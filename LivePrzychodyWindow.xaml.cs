using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Kalendarz1
{
    public partial class LivePrzychodyWindow : Window
    {
        private readonly string _connLibra;
        private readonly DateTime _date;
        private readonly Func<string, bool> _isContainer;
        private readonly Func<string, decimal, (int, decimal)> _calcPack;
        private DispatcherTimer _timer;

        public LivePrzychodyWindow(string connLibra, DateTime date,
            Func<string, bool> isContainer,
            Func<string, decimal, (int, decimal)> calcPack)
        {
            _connLibra = connLibra;
            _date = date;
            _isContainer = isContainer;
            _calcPack = calcPack;

            InitializeComponent();
            Loaded += async (s, e) => await RefreshDataAsync();
            StartTimer();
        }

        private void StartTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(20);
            _timer.Tick += async (s, e) => await RefreshDataAsync();
            _timer.Start();
        }

        private async Task RefreshDataAsync()
        {
            var dt = new DataTable();
            dt.Columns.Add("Towar", typeof(string));
            dt.Columns.Add("Kg", typeof(decimal));
            dt.Columns.Add("Pojemniki", typeof(int));

            try
            {
                using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"
                    SELECT ArticleName, CAST(Weight AS float) AS W 
                    FROM dbo.In0E 
                    WHERE Data=@D AND ISNULL(ArticleName,'')<>'' 
                    ORDER BY 
                        CASE WHEN COL_LENGTH('dbo.In0E','CreatedAt') IS NOT NULL THEN CreatedAt END DESC, 
                        CASE WHEN COL_LENGTH('dbo.In0E','Id') IS NOT NULL THEN Id END DESC", cn);
                cmd.Parameters.AddWithValue("@D", _date.ToString("yyyy-MM-dd"));

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string name = rd.IsDBNull(0) ? "(Brak)" : rd.GetString(0);
                    decimal w = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                    var (c, _) = _calcPack(name, w);
                    dt.Rows.Add(name, w, c);
                }
            }
            catch (Exception ex)
            {
                dt.Rows.Add($"Błąd: {ex.Message}", 0m, 0);
            }

            dgvData.ItemsSource = dt.DefaultView;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _timer?.Stop();
        }
    }
}