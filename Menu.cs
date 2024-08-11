using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;

namespace Kalendarz1
{
    public partial class MENU : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private Timer PrzypomnienieWstawien;
        private MojeObliczenia obliczenia = new MojeObliczenia();
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private CenoweMetody CenoweMetody = new CenoweMetody();
        private static ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        public MENU()
        {
            InitializeComponent();
            // Inicjalizacja timera
            PrzypomnienieWstawien = new Timer();
            PrzypomnienieWstawien.Interval = 10800000; // Interwał w milisekundach (tu: co 60 sekund)
            PrzypomnienieWstawien.Tick += PrzypomnienieWstawien_Tick; // Przypisanie zdarzenia
            PrzypomnienieWstawien.Start(); // Rozpoczęcie pracy timera
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void PrzypomnienieWstawien_Tick(object sender, EventArgs e)
        {
            // Sprawdzenie wyników i wyświetlenie w MessageBox
            string results = CheckResults();
            MessageBox.Show(results, "Przypomnienie Wstawien", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string CheckResults()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Wstawienia które trzeba uzupełnić :");

            // Zapytanie SQL
            string query = "SELECT TOP 400 LP, Dostawca, CONVERT(varchar, DataWstawienia, 23) AS DataWstawienia, IloscWstawienia, TypUmowy, Uwagi, [isCheck], [CheckCom] " +
                           "FROM [LibraNet].[dbo].[WstawieniaKurczakow] " +
                           "ORDER BY DataWstawienia DESC";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                int resultCount = 0;

                for (int i = 0; i < table.Rows.Count && resultCount < 20; i++)
                {
                    DataRow row = table.Rows[i];
                    DateTime dataWstawienia;
                    bool isChecked = row["isCheck"] != DBNull.Value && (bool)row["isCheck"];
                    if (DateTime.TryParse(row["DataWstawienia"].ToString(), out dataWstawienia) && !isChecked)
                    {
                        // Oblicz różnicę w dniach między datą wstawienia a dniem obecnym
                        TimeSpan roznicaDni = DateTime.Now.Date - dataWstawienia.Date;

                        // Sprawdź, czy różnica dni wynosi 35
                        if (roznicaDni.Days >= 35)
                        {
                            // Znajdź maksymalną wartość dla dostawcy
                            DateTime maxDataWstawienia = ZnajdzMaxDateDlaDostawcy(row["Dostawca"].ToString());

                            // Sprawdź, czy aktualna data wstawienia jest maksymalną datą dla dostawcy
                            if (dataWstawienia == maxDataWstawienia)
                            {
                                sb.AppendLine($"{row["Dostawca"]}, Data Wstawienia: {row["DataWstawienia"]}");
                                resultCount++;
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }


        private DateTime ZnajdzMaxDateDlaDostawcy(string dostawca)
        {
            DateTime maxDataWstawienia = DateTime.MinValue;

            // Zapytanie SQL
            string query = "SELECT MAX(DataWstawienia) AS MaxDataWstawienia " +
                           "FROM [LibraNet].[dbo].[WstawieniaKurczakow] " +
                           "WHERE Dostawca = @Dostawca";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Dostawca", dostawca);

                connection.Open();
                var result = command.ExecuteScalar();
                if (result != DBNull.Value)
                {
                    maxDataWstawienia = (DateTime)result;
                }
            }

            return maxDataWstawienia;
        }

        // Dodaj tutaj inne metody, takie jak AddEmptyRows, itp.



        private void kalendarzButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            Platnosci platnosci = new Platnosci();

            // Wyświetlanie Form1
            platnosci.Show();
        }

        private void terminyButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji WidokKalendarza
            WidokKalendarza widokKalendarza = new WidokKalendarza();

            // Przypisanie wartości UserID
            widokKalendarza.UserID = App.UserID;

            // Ustawienie formularza w trybie maksymalnym
            widokKalendarza.WindowState = FormWindowState.Maximized;

            // Wyświetlanie formularza
            widokKalendarza.Show();
        }


        private void MENU_Load(object sender, EventArgs e)
        {

        }

        private void buttonWstawienia_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokWstawienia widokWstawienia = new WidokWstawienia();

            // Wyświetlanie Form1
            widokWstawienia.Show();
        }

        private void buttonKontrahenci_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokKontrahenci widokKontrahenci = new WidokKontrahenci();

            // Wyświetlanie Form1
            widokKontrahenci.Show();

        }

        private void mrozniaButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            Mroznia mroznia = new Mroznia();

            // Wyświetlanie Form1
            mroznia.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokSpecyfikacje widokSpecyfikacje = new WidokSpecyfikacje();

            // Wyświetlanie Form1
            widokSpecyfikacje.Show();
        }

        private void sprzedazZakupButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokSprzeZakup widokSprzeZakup = new WidokSprzeZakup();

            // Wyświetlanie Form1
            widokSprzeZakup.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokMatryca Widokmatryca = new WidokMatryca();

            // Wyświetlanie Form1
            Widokmatryca.Show();
        }

        private void button3_Click(object sender, EventArgs e)
        {

        }
    }
}
