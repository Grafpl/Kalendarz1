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

        // Przechowywanie oryginalnych wartości do logowania zmian
        private Dictionary<string, string> _originalValues = new Dictionary<string, string>();
        private string _dostawcaNazwa = "";
        private int _nr = 0;
        private string _carId = "";
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

            // === NAWIGACJA TAB ===
            // Ustaw kolejność TabIndex dla kontrolek po DateTimePickers
            kmPowrot.TabIndex = 100;
            kmWyjazd.TabIndex = 101;
            hBrutto.TabIndex = 102;
            hTara.TabIndex = 103;

            // Ustaw własną nawigację Tab dla DateTimePickers (HH -> MM -> następny HH)
            SetupDateTimePickerTabNavigation();
        }

        // Słownik do śledzenia czy jesteśmy na minutach
        private Dictionary<DateTimePicker, bool> _isOnMinutes = new Dictionary<DateTimePicker, bool>();

        private void SetupDateTimePickerTabNavigation()
        {
            // Lista DateTimePickers w kolejności nawigacji
            var pickers = new DateTimePicker[] {
                poczatekUslugiData, wyjazdZakladData, dojazdHodowcaData,
                poczatekZaladunekData, koniecZaladunekData, wyjazdHodowcaData,
                powrotZakladData, koniecUslugiData
            };

            for (int i = 0; i < pickers.Length; i++)
            {
                var picker = pickers[i];
                int index = i; // Capture for closure
                var nextPicker = (i < pickers.Length - 1) ? pickers[i + 1] : null;
                var prevPicker = (i > 0) ? pickers[i - 1] : null;

                _isOnMinutes[picker] = false;

                // Obsługa Enter - reset stanu minut
                picker.Enter += (s, e) => { _isOnMinutes[picker] = false; };

                // Obsługa KeyDown dla Tab
                picker.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Tab && !e.Shift)
                    {
                        e.SuppressKeyPress = true;
                        e.Handled = true;

                        if (!_isOnMinutes[picker])
                        {
                            // Jesteśmy na godzinach - przejdź na minuty
                            _isOnMinutes[picker] = true;
                            SendKeys.Send("{RIGHT}");
                        }
                        else
                        {
                            // Jesteśmy na minutach - przejdź do następnej kontrolki
                            _isOnMinutes[picker] = false;

                            if (nextPicker != null)
                            {
                                nextPicker.Focus();
                            }
                            else
                            {
                                // Ostatni picker - przejdź do kmPowrot
                                kmPowrot.Focus();
                            }
                        }
                    }
                    else if (e.KeyCode == Keys.Tab && e.Shift)
                    {
                        e.SuppressKeyPress = true;
                        e.Handled = true;

                        if (_isOnMinutes[picker])
                        {
                            // Z minut na godziny
                            _isOnMinutes[picker] = false;
                            SendKeys.Send("{LEFT}");
                        }
                        else
                        {
                            // Z godzin - cofnij do poprzedniego pickera (na minuty)
                            if (prevPicker != null)
                            {
                                _isOnMinutes[prevPicker] = true;
                                prevPicker.Focus();
                                SendKeys.Send("{RIGHT}"); // Przejdź na minuty
                            }
                        }
                    }
                };
            }
        }
        public WidokAvilog(int idSpecyfikacji) : this()
        {
            id2Specyfikacji = idSpecyfikacji;
            idDostawcy = zapytaniasql.PobierzInformacjeZBazyDanychKonkretneJakiejkolwiek(idSpecyfikacji, "FarmerCalc", "CustomerGID");
            idrealDostawcy = zapytaniasql.PobierzInformacjeZBazyDanychKonkretneJakiejkolwiek(idSpecyfikacji, "FarmerCalc", "CustomerRealGID");
            UstawDostawce(idSpecyfikacji, Dostawca, RealDostawca);
            UstawgodzinyAviloga(idSpecyfikacji, poczatekUslugiData,  wyjazdZakladData,  dojazdHodowcaData,  poczatekZaladunekData,  koniecZaladunekData,  wyjazdHodowcaData,  powrotZakladData, koniecUslugiData);
            UstawKierowceAuta(idSpecyfikacji, Naczepa, Auto, Kierowca);
            UstawRozliczenia(idSpecyfikacji, hBrutto, hTara, uBrutto, uTara, hLiczbaSztuk, uLiczbaSztuk, buforhLiczbaSztuk, hSrednia, buforhSrednia, hSumaSztuk, buforSumaSztuk, uSumaSztuk);
            UstawKilometry(idSpecyfikacji, kmWyjazd, kmPowrot);

            // Zapisz oryginalne wartości do logowania zmian
            ZapiszOryginalne(idSpecyfikacji);
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
                            if (Dostawca != null)
                            {
                                Dostawca.Text = dostawcaNazwa ?? string.Empty;
                            }
                        }

                        if (!reader.IsDBNull(reader.GetOrdinal("CustomerRealGID")))
                        {
                            string dostawcaInfo = reader["CustomerRealGID"]?.ToString();
                            string dostawcaNazwa = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(dostawcaInfo, "ShortName");
                            if (RealDostawca != null)
                            {
                                RealDostawca.Text = dostawcaNazwa ?? string.Empty;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Brak danych dla określonego identyfikatora.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Wystąpił błąd podczas pobierania danych: " + ex.Message);
            }
        }
        private static void UstawRozliczenia(int id, TextBox hBrutto, TextBox hTara, TextBox uBrutto, TextBox uTara, TextBox hLiczbaSztuk, TextBox uLiczbaSztuk, TextBox buforhLiczbaSztuk, TextBox hSrednia, TextBox buforhSrednia, TextBox hSumaSztuk, TextBox buforSumaSztuk, TextBox uSumaSztuk)
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
                        // SztPoj to całkowita liczba sztuk - wpisujemy do SumaSztuk, nie do LiczbaSztuk (per szuflada)
                        if (!reader.IsDBNull(reader.GetOrdinal("SztPoj")))
                        {
                            int sztuki = (int)reader.GetDecimal(reader.GetOrdinal("SztPoj"));
                            hSumaSztuk.Text = sztuki.ToString();
                            buforSumaSztuk.Text = sztuki.ToString();
                            uSumaSztuk.Text = sztuki.ToString();
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

            // Loguj zmiany przed zapisem
            LogujZmianyPrzedZapisem();

            zapytaniasql.UpdateDaneHodowowAvilog(id2Specyfikacji, dostawcaIdString, realDostawcaIdString);
            zapytaniasql.UpdateDaneAdresoweDostawcy(dostawcaIdString, UlicaH, KodPocztowyH, MiejscH, KmH);
            zapytaniasql.UpdateDaneAdresoweDostawcy(realDostawcaIdString, UlicaR, KodPocztowyR, MiejscR, KmR);
            zapytaniasql.UpdateDaneKontaktowe(dostawcaIdString, Tel1, info1, Tel2, info2, Tel3, info3, Email, null, null);
            zapytaniasql.UpdateDaneKontaktowe(realDostawcaIdString, Tel1R, Info1R, Tel2R, Info2R, Tel3R, Info3R, EmailR, null, null);
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

            // Zamknij okno po zapisaniu
            this.Close();
        }

        /// <summary>
        /// Zapisuje oryginalne wartości z bazy do późniejszego porównania
        /// </summary>
        private void ZapiszOryginalne(int id)
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
                        // Wagi
                        _originalValues["FullFarmWeight"] = reader.IsDBNull(reader.GetOrdinal("FullFarmWeight")) ? "" : reader.GetDecimal(reader.GetOrdinal("FullFarmWeight")).ToString();
                        _originalValues["EmptyFarmWeight"] = reader.IsDBNull(reader.GetOrdinal("EmptyFarmWeight")) ? "" : reader.GetDecimal(reader.GetOrdinal("EmptyFarmWeight")).ToString();
                        _originalValues["FullWeight"] = reader.IsDBNull(reader.GetOrdinal("FullWeight")) ? "" : reader.GetDecimal(reader.GetOrdinal("FullWeight")).ToString();
                        _originalValues["EmptyWeight"] = reader.IsDBNull(reader.GetOrdinal("EmptyWeight")) ? "" : reader.GetDecimal(reader.GetOrdinal("EmptyWeight")).ToString();

                        // Sztuki
                        _originalValues["SztPoj"] = reader.IsDBNull(reader.GetOrdinal("SztPoj")) ? "" : reader.GetDecimal(reader.GetOrdinal("SztPoj")).ToString();
                        _originalValues["WagaDek"] = reader.IsDBNull(reader.GetOrdinal("WagaDek")) ? "" : reader.GetDecimal(reader.GetOrdinal("WagaDek")).ToString();

                        // Kilometry
                        _originalValues["StartKM"] = reader.IsDBNull(reader.GetOrdinal("StartKM")) ? "" : reader.GetInt32(reader.GetOrdinal("StartKM")).ToString();
                        _originalValues["StopKM"] = reader.IsDBNull(reader.GetOrdinal("StopKM")) ? "" : reader.GetInt32(reader.GetOrdinal("StopKM")).ToString();

                        // Auto/Kierowca
                        _originalValues["CarID"] = reader.IsDBNull(reader.GetOrdinal("CarID")) ? "" : reader.GetString(reader.GetOrdinal("CarID"));
                        _originalValues["TrailerID"] = reader.IsDBNull(reader.GetOrdinal("TrailerID")) ? "" : reader.GetString(reader.GetOrdinal("TrailerID"));
                        _originalValues["DriverGID"] = reader.IsDBNull(reader.GetOrdinal("DriverGID")) ? "" : reader.GetInt32(reader.GetOrdinal("DriverGID")).ToString();

                        // Czasy
                        _originalValues["PoczatekUslugi"] = reader.IsDBNull(reader.GetOrdinal("PoczatekUslugi")) ? "" : reader.GetDateTime(reader.GetOrdinal("PoczatekUslugi")).ToString("HH:mm");
                        _originalValues["Wyjazd"] = reader.IsDBNull(reader.GetOrdinal("Wyjazd")) ? "" : reader.GetDateTime(reader.GetOrdinal("Wyjazd")).ToString("HH:mm");
                        _originalValues["DojazdHodowca"] = reader.IsDBNull(reader.GetOrdinal("DojazdHodowca")) ? "" : reader.GetDateTime(reader.GetOrdinal("DojazdHodowca")).ToString("HH:mm");
                        _originalValues["Zaladunek"] = reader.IsDBNull(reader.GetOrdinal("Zaladunek")) ? "" : reader.GetDateTime(reader.GetOrdinal("Zaladunek")).ToString("HH:mm");
                        _originalValues["ZaladunekKoniec"] = reader.IsDBNull(reader.GetOrdinal("ZaladunekKoniec")) ? "" : reader.GetDateTime(reader.GetOrdinal("ZaladunekKoniec")).ToString("HH:mm");
                        _originalValues["WyjazdHodowca"] = reader.IsDBNull(reader.GetOrdinal("WyjazdHodowca")) ? "" : reader.GetDateTime(reader.GetOrdinal("WyjazdHodowca")).ToString("HH:mm");
                        _originalValues["Przyjazd"] = reader.IsDBNull(reader.GetOrdinal("Przyjazd")) ? "" : reader.GetDateTime(reader.GetOrdinal("Przyjazd")).ToString("HH:mm");
                        _originalValues["KoniecUslugi"] = reader.IsDBNull(reader.GetOrdinal("KoniecUslugi")) ? "" : reader.GetDateTime(reader.GetOrdinal("KoniecUslugi")).ToString("HH:mm");

                        // Nr i CarID do logowania
                        _nr = reader.IsDBNull(reader.GetOrdinal("CarLp")) ? 0 : reader.GetInt32(reader.GetOrdinal("CarLp"));
                        _carId = reader.IsDBNull(reader.GetOrdinal("CarID")) ? "" : reader.GetString(reader.GetOrdinal("CarID"));

                        // Nazwa dostawcy
                        if (!reader.IsDBNull(reader.GetOrdinal("CustomerRealGID")))
                        {
                            string dostawcaInfo = reader["CustomerRealGID"]?.ToString();
                            _dostawcaNazwa = zapytaniasql.PobierzInformacjeZBazyDanychHodowcowString(dostawcaInfo, "ShortName") ?? "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd podczas zapisywania oryginalnych wartości: " + ex.Message);
            }
        }

        /// <summary>
        /// Loguje zmiany przed zapisem
        /// </summary>
        private void LogujZmianyPrzedZapisem()
        {
            try
            {
                // Wagi hodowca
                LogujZmianeJesliInna("FullFarmWeight", "Waga Brutto Hodowca", _originalValues.GetValueOrDefault("FullFarmWeight", ""), hBrutto.Text);
                LogujZmianeJesliInna("EmptyFarmWeight", "Waga Tara Hodowca", _originalValues.GetValueOrDefault("EmptyFarmWeight", ""), hTara.Text);

                // Wagi ubojnia
                LogujZmianeJesliInna("FullWeight", "Waga Brutto Ubojnia", _originalValues.GetValueOrDefault("FullWeight", ""), uBrutto.Text);
                LogujZmianeJesliInna("EmptyWeight", "Waga Tara Ubojnia", _originalValues.GetValueOrDefault("EmptyWeight", ""), uTara.Text);

                // Kilometry
                LogujZmianeJesliInna("StartKM", "KM Wyjazd", _originalValues.GetValueOrDefault("StartKM", ""), kmWyjazd.Text);
                LogujZmianeJesliInna("StopKM", "KM Powrót", _originalValues.GetValueOrDefault("StopKM", ""), kmPowrot.Text);

                // Auto/Naczepa
                LogujZmianeJesliInna("CarID", "Auto", _originalValues.GetValueOrDefault("CarID", ""), Auto.Text);
                LogujZmianeJesliInna("TrailerID", "Naczepa", _originalValues.GetValueOrDefault("TrailerID", ""), Naczepa.Text);

                // Czasy
                LogujZmianeJesliInna("PoczatekUslugi", "Początek Usługi", _originalValues.GetValueOrDefault("PoczatekUslugi", ""), poczatekUslugiData.Value.ToString("HH:mm"));
                LogujZmianeJesliInna("Wyjazd", "Wyjazd Zakład", _originalValues.GetValueOrDefault("Wyjazd", ""), wyjazdZakladData.Value.ToString("HH:mm"));
                LogujZmianeJesliInna("DojazdHodowca", "Dojazd Hodowca", _originalValues.GetValueOrDefault("DojazdHodowca", ""), dojazdHodowcaData.Value.ToString("HH:mm"));
                LogujZmianeJesliInna("Zaladunek", "Początek Załadunku", _originalValues.GetValueOrDefault("Zaladunek", ""), poczatekZaladunekData.Value.ToString("HH:mm"));
                LogujZmianeJesliInna("ZaladunekKoniec", "Koniec Załadunku", _originalValues.GetValueOrDefault("ZaladunekKoniec", ""), koniecZaladunekData.Value.ToString("HH:mm"));
                LogujZmianeJesliInna("WyjazdHodowca", "Wyjazd Hodowca", _originalValues.GetValueOrDefault("WyjazdHodowca", ""), wyjazdHodowcaData.Value.ToString("HH:mm"));
                LogujZmianeJesliInna("Przyjazd", "Powrót Zakład", _originalValues.GetValueOrDefault("Przyjazd", ""), powrotZakladData.Value.ToString("HH:mm"));
                LogujZmianeJesliInna("KoniecUslugi", "Koniec Usługi", _originalValues.GetValueOrDefault("KoniecUslugi", ""), koniecUslugiData.Value.ToString("HH:mm"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd podczas logowania zmian: " + ex.Message);
            }
        }

        /// <summary>
        /// Loguje zmianę jeśli wartość jest inna
        /// </summary>
        private void LogujZmianeJesliInna(string fieldName, string displayName, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(oldValue) && string.IsNullOrEmpty(newValue)) return;
            if (oldValue == newValue) return;

            // Nie loguj początkowych wartości (gdy stare jest puste)
            if (string.IsNullOrEmpty(oldValue) || oldValue == "0" || oldValue == "00:00") return;

            LogChangeToDatabase(id2Specyfikacji, displayName, oldValue, newValue, _dostawcaNazwa, _nr, _carId);
        }

        /// <summary>
        /// Zapisuje zmianę do bazy danych FarmerCalcChangeLog
        /// </summary>
        private void LogChangeToDatabase(int recordId, string fieldName, string oldValue, string newValue, string dostawca, int nr, string carId)
        {
            try
            {
                if (oldValue == newValue) return;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz datę specyfikacji
                    DateTime? calcDate = null;
                    string getDateSql = "SELECT CalcDate FROM [dbo].[FarmerCalc] WHERE ID = @ID";
                    using (SqlCommand dateCmd = new SqlCommand(getDateSql, conn))
                    {
                        dateCmd.Parameters.AddWithValue("@ID", recordId);
                        var result = dateCmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            calcDate = (DateTime)result;
                    }

                    // Pobierz nazwę użytkownika
                    string userName = Environment.UserName;
                    string userId = App.UserID ?? "";
                    if (!string.IsNullOrEmpty(userId))
                    {
                        try
                        {
                            NazwaZiD nazwaZiD = new NazwaZiD();
                            userName = nazwaZiD.GetNameById(userId) ?? userName;
                        }
                        catch { }
                    }

                    string sql = @"INSERT INTO [dbo].[FarmerCalcChangeLog]
                        (FarmerCalcID, FieldName, OldValue, NewValue, Dostawca, ChangedBy, UserID, Nr, CarID, ChangeDate, CalcDate)
                        VALUES (@FarmerCalcID, @FieldName, @OldValue, @NewValue, @Dostawca, @ChangedBy, @UserID, @Nr, @CarID, GETDATE(), @CalcDate)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@FarmerCalcID", recordId);
                        cmd.Parameters.AddWithValue("@FieldName", fieldName ?? "");
                        cmd.Parameters.AddWithValue("@OldValue", oldValue ?? "");
                        cmd.Parameters.AddWithValue("@NewValue", newValue ?? "");
                        cmd.Parameters.AddWithValue("@Dostawca", dostawca ?? "");
                        cmd.Parameters.AddWithValue("@ChangedBy", userName ?? "system");
                        cmd.Parameters.AddWithValue("@UserID", userId ?? "");
                        cmd.Parameters.AddWithValue("@Nr", nr);
                        cmd.Parameters.AddWithValue("@CarID", carId ?? "");
                        cmd.Parameters.AddWithValue("@CalcDate", (object)calcDate ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd logowania zmiany: " + ex.Message);
            }
        }
    }
}
