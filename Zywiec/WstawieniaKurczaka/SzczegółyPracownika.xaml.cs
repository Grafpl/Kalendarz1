using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Kalendarz1
{
    public class SzczegolyWstawienie
    {
        public int Lp { get; set; }
        public string Dostawca { get; set; }
        public DateTime? DataWstawienia { get; set; }
        public int? IloscWstawienia { get; set; }
        public DateTime? DataUtw { get; set; }
        public string Status { get; set; }
        public string KtoStworzyl { get; set; }
        public DateTime? DataConf { get; set; }
    }

    public partial class SzczegółyPracownika : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string nazwaPracownika;
        private DateTime startDate;
        private DateTime endDate;

        public SzczegółyPracownika(string pracownik, DateTime dataOd, DateTime dataDo)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            nazwaPracownika = pracownik;
            startDate = dataOd;
            endDate = dataDo;

            txtNazwaPracownika.Text = $"Szczegoly: {pracownik}";
            txtOkres.Text = $"Okres: {dataOd:dd.MM.yyyy} - {dataDo.AddDays(-1):dd.MM.yyyy}";

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // Zaladuj co utworzyl
                var stworzone = LoadStworzonePrzezPracownika();
                dgStworzone.ItemsSource = stworzone;
                txtLiczbaStworzone.Text = stworzone.Count.ToString();

                // Zaladuj co potwierdzil
                var potwierdzone = LoadPotwierdzonePrzezPracownika();
                dgPotwierdzone.ItemsSource = potwierdzone;
                txtLiczbaPotwierdzone.Text = potwierdzone.Count.ToString();

                // Razem
                txtLiczbaRazem.Text = (stworzone.Count + potwierdzone.Count).ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<SzczegolyWstawienie> LoadStworzonePrzezPracownika()
        {
            var lista = new List<SzczegolyWstawienie>();

            string query = @"
                SELECT 
                    w.Lp,
                    w.Dostawca,
                    w.DataWstawienia,
                    w.IloscWstawienia,
                    w.DataUtw,
                    CASE WHEN w.isConf = 1 THEN 'Potwierdzone' ELSE 'Oczekujace' END as Status
                FROM dbo.WstawieniaKurczakow w
                LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                WHERE o.Name = @Pracownik 
                    AND w.DataUtw >= @StartDate 
                    AND w.DataUtw < @EndDate
                ORDER BY w.DataUtw DESC";

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@Pracownik", nazwaPracownika);
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new SzczegolyWstawienie
                        {
                            Lp = Convert.ToInt32(reader["Lp"]),
                            Dostawca = reader["Dostawca"]?.ToString() ?? "",
                            DataWstawienia = reader["DataWstawienia"] as DateTime?,
                            IloscWstawienia = reader["IloscWstawienia"] as int?,
                            DataUtw = reader["DataUtw"] as DateTime?,
                            Status = reader["Status"]?.ToString() ?? ""
                        });
                    }
                }
            }

            return lista;
        }

        private List<SzczegolyWstawienie> LoadPotwierdzonePrzezPracownika()
        {
            var lista = new List<SzczegolyWstawienie>();

            string query = @"
                SELECT 
                    w.Lp,
                    w.Dostawca,
                    w.DataWstawienia,
                    w.IloscWstawienia,
                    os.Name as KtoStworzyl,
                    w.DataConf
                FROM dbo.WstawieniaKurczakow w
                LEFT JOIN dbo.operators oc ON w.KtoConf = oc.ID
                LEFT JOIN dbo.operators os ON w.KtoStwo = os.ID
                WHERE oc.Name = @Pracownik 
                    AND w.isConf = 1
                    AND w.DataConf >= @StartDate 
                    AND w.DataConf < @EndDate
                ORDER BY w.DataConf DESC";

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@Pracownik", nazwaPracownika);
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new SzczegolyWstawienie
                        {
                            Lp = Convert.ToInt32(reader["Lp"]),
                            Dostawca = reader["Dostawca"]?.ToString() ?? "",
                            DataWstawienia = reader["DataWstawienia"] as DateTime?,
                            IloscWstawienia = reader["IloscWstawienia"] as int?,
                            KtoStworzyl = reader["KtoStworzyl"]?.ToString() ?? "Nieznany",
                            DataConf = reader["DataConf"] as DateTime?
                        });
                    }
                }
            }

            return lista;
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
