using Microsoft.Data.SqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokAvilog : Form
    {
        private int id2Specyfikacji;
        static string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        static string sqlDostawy = "SELECT * FROM [LibraNet].[dbo].[FarmerCalc] WHERE ID = @id";
        static string sqlDostawcy = "SELECT * FROM [LibraNet].[dbo].[Dostawcy] WHERE ID = @id";
        static string idDostawcy, idrealDostawcy, idKurnika, idDostawcyZmienione;
        private static ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();
        private RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        public WidokAvilog()
        {
            InitializeComponent();
            zapytaniasql.UzupelnijComboBoxHodowcami3(Dostawca); ; zapytaniasql.UzupelnijComboBoxHodowcami3(RealDostawca);
            zapytaniasql.UzupelnijComboBoxCiagnikami(Auto); zapytaniasql.UzupelnijComboBoxNaczepami(Naczepa); zapytaniasql.UzupelnijComboBoxKierowcami(Kierowca);
            // Tablica zawierająca wszystkie kontrolki
            DateTimePicker[] dateTimePickers = { wyjazdZakladData, dojazdHodowcaData, poczatekZaladunekData, koniecZaladunekData, wyjazdHodowcaData, poczatekUslugiData, powrotZakladData, koniecUslugiData };
            // Pętla ustawiająca właściwości dla każdej kontrolki w tablicy
            foreach (DateTimePicker dateTimePicker in dateTimePickers)
            {
                dateTimePicker.Format = DateTimePickerFormat.Custom;
                dateTimePicker.CustomFormat = "HH:mm";
                dateTimePicker.ShowUpDown = true;
                dateTimePicker.Value = DateTime.Today.Date;
            }
            RozwijanieComboBox.RozwijanieKontrPoKatalogu(comboBoxSymfonia, "Dostawcy Drobiu");

        }
        public WidokAvilog(int idSpecyfikacji) : this()
        {
            id2Specyfikacji = idSpecyfikacji;
            idDostawcy = zapytaniasql.PobierzInformacjeZBazyDanychKonkretneJakiejkolwiek(idSpecyfikacji, "FarmerCalc", "CustomerGID");
            idrealDostawcy = zapytaniasql.PobierzInformacjeZBazyDanychKonkretneJakiejkolwiek(idSpecyfikacji, "FarmerCalc", "CustomerRealGID");
            UstawDostawce(idSpecyfikacji, Dostawca, RealDostawca);
            UstawgodzinyAviloga(idSpecyfikacji, poczatekUslugiData,  wyjazdZakladData,  dojazdHodowcaData,  poczatekZaladunekData,  koniecZaladunekData,  wyjazdHodowcaData,  powrotZakladData, koniecUslugiData);
            UstawKierowceAuta(idSpecyfikacji, Naczepa, Auto, Kierowca);
            UstawRozliczenia(idSpecyfikacji, hBrutto, hTara, uBrutto, uTara, hLiczbaSztuk, uLiczbaSztuk, buforhLiczbaSztuk, hSrednia, buforhSrednia);
            UstawKilometry(idSpecyfikacji, kmWyjazd, kmPowrot);
            UstawKilometry(idSpecyfikacji, kmWyjazd, kmPowrot);
            //ZczytajDane(idSpecyfikacji, Dostawca, RealDostawca);
        }
        private static void UstawgodzinyAviloga(int id, DateTimePicker poczatekUslugiData, DateTimePicker wyjazdZakladData, DateTimePicker dojazdHodowcaData, DateTimePicker poczatekZaladunekData, DateTimePicker koniecZaladunekData, DateTimePicker wyjazdHodowcaData, DateTimePicker powrotZakladData, DateTimePicker koniecUslugiData)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(sqlDostawy, connection);
                    command.Parameters.AddWithValue("@id", id);

                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        // Przypisz wartości dat do odpowiednich DateTimePicker
                        if (!reader.IsDBNull(reader.GetOrdinal("PoczatekUslugi")))
                        {
                            poczatekUslugiData.Value = reader.GetDateTime(reader.GetOrdinal("PoczatekUslugi"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("Wyjazd")))
                        {
                            wyjazdZakladData.Value = reader.GetDateTime(reader.GetOrdinal("Wyjazd"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("DojazdHodowca")))
                        {
                            dojazdHodowcaData.Value = reader.GetDateTime(reader.GetOrdinal("DojazdHodowca"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("Zaladunek")))
                        {
                            poczatekZaladunekData.Value = reader.GetDateTime(reader.GetOrdinal("Zaladunek"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("ZaladunekKoniec")))
                        {
                            koniecZaladunekData.Value = reader.GetDateTime(reader.GetOrdinal("ZaladunekKoniec"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("WyjazdHodowca")))
                        {
                            wyjazdHodowcaData.Value = reader.GetDateTime(reader.GetOrdinal("WyjazdHodowca"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("Przyjazd")))
                        {
                            powrotZakladData.Value = reader.GetDateTime(reader.GetOrdinal("Przyjazd"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("KoniecUslugi")))
                        {
                            koniecUslugiData.Value = reader.GetDateTime(reader.GetOrdinal("KoniecUslugi"));
                        }
                    }
                    else
                    {
                        // Obsłuż przypadki, gdy brak danych dla określonego identyfikatora
                        Console.WriteLine("Brak danych dla określonego identyfikatora.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas pobierania danych: " + ex.Message);
            }
        }
        private static void UstawKierowceAuta(int id, ComboBox Naczepa, ComboBox Auto, ComboBox Kierowca)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(sqlDostawy, connection);
                    command.Parameters.AddWithValue("@id", id);

                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {

                        if (!reader.IsDBNull(reader.GetOrdinal("TrailerID")))
                        {
                            Naczepa.Text = reader.GetString(reader.GetOrdinal("TrailerID"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("CarID")))
                        {
                            Auto.Text = reader.GetString(reader.GetOrdinal("CarID"));
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("DriverGID")))
                        {
                            int startKM = reader.GetInt32(reader.GetOrdinal("DriverGID"));
                            string kierowcaNazwa = zapytaniasql.ZnajdzNazweKierowcy(startKM);
                            Kierowca.Text = kierowcaNazwa.ToString(); // Konwersja wartości int na string
                        }
                    }
                    else
                    {
                        // Obsłuż przypadki, gdy brak danych dla określonego identyfikatora
                        Console.WriteLine("Brak danych dla określonego identyfikatora.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas pobierania danych: " + ex.Message);
            }
        }
        private static void UstawDostawce(int id, ComboBox Dostawca, ComboBox RealDostawca)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(sqlDostawy, connection);
                    command.Parameters.AddWithValue("@id", id);

                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {

                        if (!reader.IsDBNull(reader.GetOrdinal("CustomerGID")))
                        {
                            string dostawcaInfo = reader["CustomerGID"]?.ToString();
                            string dostawcaNazwa = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(dostawcaInfo, "ShortName");
                            Dostawca.Text = dostawcaNazwa.ToString(); // Konwersja wartości int na string
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("CustomerRealGID")))
                        {
                            string dostawcaInfo = reader["CustomerRealGID"]?.ToString();
                            string dostawcaNazwa = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(dostawcaInfo, "ShortName");
                            RealDostawca.Text = dostawcaNazwa.ToString(); // Konwersja wartości int na string
                        }
                    }
                    else
                    {
                        // Obsłuż przypadki, gdy brak danych dla określonego identyfikatora
                        Console.WriteLine("Brak danych dla określonego identyfikatora.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas pobierania danych: " + ex.Message);
            }
        }
        private static void UstawRozliczenia(int id, TextBox hBrutto, TextBox hTara, TextBox uBrutto, TextBox uTara, TextBox hLiczbaSztuk, TextBox uLiczbaSztuk, TextBox buforhLiczbaSztuk, TextBox hSrednia, TextBox buforhSrednia)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(sqlDostawy, connection);
                    command.Parameters.AddWithValue("@id", id);

                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {


                        if (!reader.IsDBNull(reader.GetOrdinal("FullFarmWeight")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("FullFarmWeight"));
                            hBrutto.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("EmptyFarmWeight")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("EmptyFarmWeight"));
                            hTara.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("EmptyWeight")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("EmptyWeight"));
                            uTara.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("FullWeight")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("FullWeight"));
                            uBrutto.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }
                        if (!reader.IsDBNull(reader.GetOrdinal("WagaDek")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("WagaDek"));
                            hSrednia.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }
                        if (!reader.IsDBNull(reader.GetOrdinal("WagaDek")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("WagaDek"));
                            buforhSrednia.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }
                        if (!reader.IsDBNull(reader.GetOrdinal("SztPoj")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("SztPoj"));
                            hLiczbaSztuk.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }
                        if (!reader.IsDBNull(reader.GetOrdinal("SztPoj")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("SztPoj"));
                            buforhLiczbaSztuk.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }
                        if (!reader.IsDBNull(reader.GetOrdinal("SztPoj")))
                        {
                            decimal waga = reader.GetDecimal(reader.GetOrdinal("SztPoj"));
                            uLiczbaSztuk.Text = waga.ToString(); // Konwersja wartości decimal na string
                        }
                    }
                    else
                    {
                        // Obsłuż przypadki, gdy brak danych dla określonego identyfikatora
                        Console.WriteLine("Brak danych dla określonego identyfikatora.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas pobierania danych: " + ex.Message);
            }
        }
        private static void UstawKilometry(int id, TextBox kmWyjazd, TextBox kmPowrot)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(sqlDostawy, connection);
                    command.Parameters.AddWithValue("@id", id);

                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        if (!reader.IsDBNull(reader.GetOrdinal("StartKM")))
                        {
                            int startKM = reader.GetInt32(reader.GetOrdinal("StartKM"));
                            kmWyjazd.Text = startKM.ToString(); // Konwersja wartości int na string
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("StopKM")))
                        {
                            int stopKM = reader.GetInt32(reader.GetOrdinal("StopKM"));
                            kmPowrot.Text = stopKM.ToString();
                        }

                    }
                    else
                    {
                        // Obsłuż przypadki, gdy brak danych dla określonego identyfikatora
                        Console.WriteLine("Brak danych dla określonego identyfikatora.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas pobierania danych: " + ex.Message);
            }
        }
        private void Odejmowanie(TextBox textBox1, TextBox textBox2, TextBox resultTextBox)
        {
            // Sprawdzenie, czy wartości w textBox1 i textBox2 są liczbami
            if (double.TryParse(textBox1.Text, out double value1) && double.TryParse(textBox2.Text, out double value2))
            {
                // Odejmowanie wartości i zapisanie wyniku w textBox3
                double result = value1 - value2;
                resultTextBox.Text = result.ToString();
            }
        }
        private void Mnożenie(TextBox textBox1, TextBox textBox2, TextBox resultTextBox)
        {
            // Sprawdzenie, czy wartości w textBox1 i textBox2 są liczbami
            if (double.TryParse(textBox1.Text, out double value1) && double.TryParse(textBox2.Text, out double value2))
            {
                // Odejmowanie wartości i zapisanie wyniku w textBox3
                double result = value1 * value2;
                resultTextBox.Text = result.ToString();
            }
        }
        private void Dzielenie(TextBox textBox1, TextBox textBox2, TextBox resultTextBox)
        {
            // Sprawdzenie, czy wartości w textBox1 i textBox2 są liczbami
            if (double.TryParse(textBox1.Text, out double value1) && double.TryParse(textBox2.Text, out double value2))
            {
                // Sprawdzenie, czy druga wartość nie jest zerem
                if (value2 != 0)
                {
                    // Dzielenie wartości i zapisanie wyniku w textBox3 z dwoma miejscami po przecinku
                    double result = value1 / value2;
                    resultTextBox.Text = result.ToString("F2");
                }
            }
        }
        private void WidokAvilog_Load(object sender, EventArgs e)
        {

        }
        private void hBrutto_TextChanged(object sender, EventArgs e)
        {
            Odejmowanie(hBrutto, hTara, hNetto);
        }
        private void hTara_TextChanged(object sender, EventArgs e)
        {
            Odejmowanie(hBrutto, hTara, hNetto);
        }
        private void kmWyjazd_TextChanged(object sender, EventArgs e)
        {
            Odejmowanie(kmPowrot, kmWyjazd, Dystans);
        }
        private void kmPowrot_TextChanged(object sender, EventArgs e)
        {
            Odejmowanie(kmPowrot, kmWyjazd, Dystans);
        }
        private void uBrutto_TextChanged(object sender, EventArgs e)
        {
            Odejmowanie(uBrutto, uTara, uNetto);
        }
        private void uTara_TextChanged(object sender, EventArgs e)
        {
            Odejmowanie(uBrutto, uTara, uNetto);
        }
        private void uLiczbaSztuk_TextChanged(object sender, EventArgs e)
        {
            Mnożenie(uLiczbaSzuflad, uLiczbaSztuk, uSumaSztuk);
        }
        private void uLiczbaSzuflad_TextChanged(object sender, EventArgs e)
        {
            Mnożenie(uLiczbaSzuflad, uLiczbaSztuk, uSumaSztuk);
        }
        private void hLiczbaSzuflad_TextChanged(object sender, EventArgs e)
        {
            Mnożenie(hLiczbaSzuflad, hLiczbaSztuk, hSumaSztuk);
        }
        private void hLiczbaSztuk_TextChanged(object sender, EventArgs e)
        {
            Mnożenie(hLiczbaSzuflad, hLiczbaSztuk, hSumaSztuk);
        }
        private void buforhLiczbaSztuk_TextChanged(object sender, EventArgs e)
        {
            Mnożenie(hLiczbaSzuflad, buforhLiczbaSztuk, buforSumaSztuk);
        }
        private void uSumaSztuk_TextChanged(object sender, EventArgs e)
        {
            Dzielenie(uNetto, uSumaSztuk, uSrednia);
        }
        private void uNetto_TextChanged(object sender, EventArgs e)
        {
            Dzielenie(uNetto, uSumaSztuk, uSrednia);
        }
        private void hNetto_TextChanged(object sender, EventArgs e)
        {
            Dzielenie(hNetto, hSumaSztuk, hSrednia);
        }
        private void hSumaSztuk_TextChanged(object sender, EventArgs e)
        {
            Dzielenie(hNetto, hSumaSztuk, hSrednia);
        }
        private void label38_Click(object sender, EventArgs e)
        {

        }
        private void Dostawca_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Dostawca?.SelectedItem is KeyValuePair<string, string> selectedItem)
            {
                string selectedId = selectedItem.Key; // Pobieramy tylko ID

                // Wywołanie metody z wybranym ID
                zapytaniasql.UzupełnienieDanychHodowcydoTextBoxow(selectedId, UlicaH, KodPocztowyH, MiejscH, KmH, Tel1, Tel2, Tel3);
            }
        }
        private void RealDostawca_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (RealDostawca?.SelectedItem is KeyValuePair<string, string> selectedItem)
            {
                string selectedId = selectedItem.Key; // Pobieramy tylko ID

                // Wywołanie metody z wybranym ID
                zapytaniasql.UzupełnienieDanychHodowcydoTextBoxow(selectedId, UlicaR, KodPocztowyR, MiejscR, KmR, Tel1R, Tel2R, Tel3R);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string dostawcaIdString = null;
            string realDostawcaIdString = null;

            if (Dostawca?.SelectedItem is KeyValuePair<string, string> DostawcaIdlista)
            {
                dostawcaIdString = DostawcaIdlista.Key; 
            }
            if (RealDostawca?.SelectedItem is KeyValuePair<string, string> RealDostawcaIdlista)
            {
                realDostawcaIdString = RealDostawcaIdlista.Key;
            }
            zapytaniasql.UpdateDaneHodowowAvilog(id2Specyfikacji, dostawcaIdString, realDostawcaIdString);
            zapytaniasql.UpdateDaneAdresoweDostawcy(dostawcaIdString, UlicaH, KodPocztowyH, MiejscH, KmH);
            zapytaniasql.UpdateDaneAdresoweDostawcy(realDostawcaIdString, UlicaR, KodPocztowyR, MiejscR, KmR);
            zapytaniasql.UpdateDaneKontaktowe(dostawcaIdString, Tel1, info1, Tel2, info2, Tel3, info3, Email);
            zapytaniasql.UpdateDaneKontaktowe(realDostawcaIdString, Tel1R, Info1R, Tel2R, Info2R, Tel3R, Info3R, EmailR);
            zapytaniasql.UpdateDaneRozliczenioweAvilogHodowca(id2Specyfikacji, hBrutto, hTara, hNetto, hSrednia, hSumaSztuk, hLiczbaSztuk);
            zapytaniasql.UpdateDaneRozliczenioweAvilogUbojnia(id2Specyfikacji, uBrutto, uTara, uNetto, uSrednia, uSumaSztuk, uLiczbaSztuk);
            zapytaniasql.UpdateDaneAutKierowcy(id2Specyfikacji, Kierowca, Auto, Naczepa);
            zapytaniasql.UpdateDaneDystansu(id2Specyfikacji, kmWyjazd, kmPowrot, Dystans);


            



            zapytaniasql.UpdateCzas(id2Specyfikacji, "PoczatekUslugi", poczatekUslugiData);
            zapytaniasql.UpdateCzas(id2Specyfikacji, "Wyjazd", wyjazdZakladData);
            zapytaniasql.UpdateCzas(id2Specyfikacji, "DojazdHodowca", dojazdHodowcaData);
            zapytaniasql.UpdateCzas(id2Specyfikacji, "Zaladunek", poczatekZaladunekData);
            zapytaniasql.UpdateCzas(id2Specyfikacji, "ZaladunekKoniec", koniecZaladunekData);
            zapytaniasql.UpdateCzas(id2Specyfikacji, "WyjazdHodowca", wyjazdHodowcaData);
            zapytaniasql.UpdateCzas(id2Specyfikacji, "Przyjazd", powrotZakladData);
            zapytaniasql.UpdateCzas(id2Specyfikacji, "KoniecUslugi", koniecUslugiData);
        }
    }
}
