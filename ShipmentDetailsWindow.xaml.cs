using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Kalendarz1
{
    public partial class ShipmentDetailsWindow : Window
    {
        private readonly string _connHandel;
        private readonly int _clientId;
        private readonly DateTime _date;
        private ObservableCollection<ShipmentDetail> _details = new();

        public ShipmentDetailsWindow(string connHandel, int clientId, DateTime date)
        {
            _connHandel = connHandel;
            _clientId = clientId;
            _date = date.Date;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            lblTitle.Text = $"Wydania bez zamówienia – KH {_clientId}";
            dgvData.ItemsSource = _details;

            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                var cmd = new SqlCommand(@"
                    SELECT MG.id AS DocId, MZ.idtw, TW.kod, SUM(ABS(MZ.ilosc)) AS Qty 
                    FROM HANDEL.HM.MZ MZ 
                    JOIN HANDEL.HM.MG ON MZ.super=MG.id 
                    JOIN HANDEL.HM.TW ON MZ.idtw=TW.id 
                    WHERE MG.seria IN ('sWZ','sWZ-W') AND MG.aktywny=1 
                    AND MG.data=@D AND MG.khid=@Kh 
                    GROUP BY MG.id, MZ.idtw, TW.kod 
                    ORDER BY MG.id DESC", cn);

                cmd.Parameters.AddWithValue("@D", _date);
                cmd.Parameters.AddWithValue("@Kh", _clientId);

                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    _details.Add(new ShipmentDetail
                    {
                        Dokument = rd.GetInt32(0).ToString(),
                        TowarID = rd.GetInt32(1),
                        Kod = rd.IsDBNull(2) ? string.Empty : rd.GetString(2),
                        Ilosc = rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3))
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class ShipmentDetail
        {
            public string Dokument { get; set; }
            public int TowarID { get; set; }
            public string Kod { get; set; }
            public decimal Ilosc { get; set; }
        }
    }
}