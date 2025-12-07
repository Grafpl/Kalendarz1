using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Windows;

namespace Kalendarz1.CRM
{
    public partial class PanelManageraWindow : Window
    {
        string connStr;
        public PanelManageraWindow(string c)
        {
            InitializeComponent();
            connStr = c;
            dpOd.SelectedDate = DateTime.Today.AddDays(-30);
            dpDo.SelectedDate = DateTime.Today;
            Wczytaj();
        }

        private void Dp_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => Wczytaj();
        private void BtnGeneruj_Click(object sender, RoutedEventArgs e) => Wczytaj();

        void Wczytaj()
        {
            if (dpOd.SelectedDate == null || dpDo.SelectedDate == null) return;

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                    SELECT ISNULL(o.Name, h.KtoWykonal) as Handlowiec,
                    COUNT(*) as [Wszystkie Akcje],
                    SUM(CASE WHEN TypZmiany='Zmiana statusu' THEN 1 ELSE 0 END) as [Zmiany Statusów],
                    SUM(CASE WHEN WartoscNowa='Próba kontaktu' THEN 1 ELSE 0 END) as [Próby Tel],
                    SUM(CASE WHEN WartoscNowa LIKE '%oferta%' THEN 1 ELSE 0 END) as [Wysłane Oferty],
                    SUM(CASE WHEN WartoscNowa LIKE '%Zgoda%' THEN 1 ELSE 0 END) as [Pozytywne]
                    FROM HistoriaZmianCRM h LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                    WHERE DataZmiany >= @od AND DataZmiany < DATEADD(day, 1, @do)
                    GROUP BY o.Name, h.KtoWykonal
                    ORDER BY [Wszystkie Akcje] DESC", conn);

                cmd.Parameters.AddWithValue("@od", dpOd.SelectedDate.Value);
                cmd.Parameters.AddWithValue("@do", dpDo.SelectedDate.Value);

                var dt = new DataTable(); new SqlDataAdapter(cmd).Fill(dt);
                dgRaport.ItemsSource = dt.DefaultView;
            }
        }
    }
}