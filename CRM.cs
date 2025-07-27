using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Kalendarz1
{
    public partial class CRM : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private int handlowiecID = 0;
        private string przypisanyPowiat = "";
        private int aktualnyOdbiorcaID = 0;


        public string UserID { get; set; }

        public CRM()
        {
            InitializeComponent();
            //przypisanyPowiat = PobierzPowiatDlaHandlowca(handlowiecID);
            WczytajOdbiorcow();
        }


        private string PobierzPowiatDlaHandlowca(int idHandlowca)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT Powiat FROM Operators WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", idHandlowca);
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? "";
            }
        }

        private void WczytajOdbiorcow()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = @"
            SELECT 
                O.ID,
                O.Nazwa,
                O.KOD AS KodPocztowy,
                O.MIASTO,
                O.Ulica,
                O.Telefon_K,
                O.Wojewodztwo,
                O.Powiat,
                O.Gmina,
                MAX(N.DataUtworzenia) AS DataOstatniejNotatki
            FROM OdbiorcyCRM O
            LEFT JOIN NotatkiCRM N ON O.ID = N.IDOdbiorcy
            GROUP BY 
                O.ID, O.Nazwa, O.KOD, O.MIASTO, O.Ulica, O.Telefon_K,
                O.Wojewodztwo, O.Powiat, O.Gmina
            ORDER BY O.Nazwa";

                var cmd = new SqlCommand(query, conn);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewOdbiorcy.DataSource = dt;
            }
        }



        private void WczytajNotatki(int idOdbiorcy)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var query = "SELECT Tresc, DataUtworzenia FROM NotatkiCRM WHERE IDOdbiorcy = @id ORDER BY DataUtworzenia DESC";
                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", idOdbiorcy);

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewNotatki.DataSource = dt;
            }
        }

        private void DodajNotatke(int idOdbiorcy, string tresc)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @tresc, @kto)", conn);
                cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                cmd.Parameters.AddWithValue("@tresc", tresc);
                cmd.Parameters.AddWithValue("@kto", handlowiecID);
                cmd.ExecuteNonQuery();
            }
            WczytajNotatki(idOdbiorcy);
        }

        private void dataGridViewOdbiorcy_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                int idOdbiorcy = Convert.ToInt32(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["ID"].Value);
                WczytajNotatki(idOdbiorcy);
                aktualnyOdbiorcaID = idOdbiorcy;
            }
        }

        private void buttonDodajNotatke_Click(object sender, EventArgs e)
        {
            if (aktualnyOdbiorcaID > 0 && !string.IsNullOrWhiteSpace(textBoxNotatka.Text))
            {
                DodajNotatke(aktualnyOdbiorcaID, textBoxNotatka.Text);
                textBoxNotatka.Clear();
            }
            else
            {
                MessageBox.Show("Wybierz odbiorcę i wpisz treść notatki.");
            }
        }

        private void CRM_Load(object sender, EventArgs e)
        {

        }
    }
}
