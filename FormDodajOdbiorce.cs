using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class FormDodajOdbiorce : Form
    {
        private string connectionString;
        private string operatorID;

        private TextBox textBoxNazwa;
        private TextBox textBoxKod;
        private TextBox textBoxMiasto;
        private TextBox textBoxUlica;
        private TextBox textBoxTelefon;
        private ComboBox comboBoxWoj;
        private TextBox textBoxPowiat;
        private ComboBox comboBoxPKD;
        private Button buttonZapisz;
        private Button buttonAnuluj;
        private CheckBox checkBoxTylkoMoje;
        private Label lblAutoInfo;

        // Mapowanie prefixów kodu pocztowego do województw
        private static readonly Dictionary<string, string> kodDoWojewodztwa = new Dictionary<string, string>
        {
            {"00", "Mazowieckie"}, {"01", "Mazowieckie"}, {"02", "Mazowieckie"}, {"03", "Mazowieckie"}, {"04", "Mazowieckie"}, {"05", "Mazowieckie"},
            {"06", "Mazowieckie"}, {"07", "Mazowieckie"}, {"08", "Mazowieckie"}, {"09", "Mazowieckie"},
            {"10", "Warmińsko-Mazurskie"}, {"11", "Warmińsko-Mazurskie"}, {"12", "Warmińsko-Mazurskie"}, {"13", "Warmińsko-Mazurskie"}, {"14", "Warmińsko-Mazurskie"},
            {"15", "Podlaskie"}, {"16", "Podlaskie"}, {"17", "Podlaskie"}, {"18", "Podlaskie"}, {"19", "Podlaskie"},
            {"20", "Lubelskie"}, {"21", "Lubelskie"}, {"22", "Lubelskie"}, {"23", "Lubelskie"}, {"24", "Lubelskie"},
            {"25", "Świętokrzyskie"}, {"26", "Świętokrzyskie"}, {"27", "Świętokrzyskie"}, {"28", "Świętokrzyskie"}, {"29", "Świętokrzyskie"},
            {"30", "Małopolskie"}, {"31", "Małopolskie"}, {"32", "Małopolskie"}, {"33", "Małopolskie"}, {"34", "Małopolskie"},
            {"35", "Podkarpackie"}, {"36", "Podkarpackie"}, {"37", "Podkarpackie"}, {"38", "Podkarpackie"}, {"39", "Podkarpackie"},
            {"40", "Śląskie"}, {"41", "Śląskie"}, {"42", "Śląskie"}, {"43", "Śląskie"}, {"44", "Śląskie"},
            {"45", "Opolskie"}, {"46", "Opolskie"}, {"47", "Opolskie"}, {"48", "Opolskie"}, {"49", "Opolskie"},
            {"50", "Dolnośląskie"}, {"51", "Dolnośląskie"}, {"52", "Dolnośląskie"}, {"53", "Dolnośląskie"}, {"54", "Dolnośląskie"},
            {"55", "Dolnośląskie"}, {"56", "Dolnośląskie"}, {"57", "Dolnośląskie"}, {"58", "Dolnośląskie"}, {"59", "Dolnośląskie"},
            {"60", "Wielkopolskie"}, {"61", "Wielkopolskie"}, {"62", "Wielkopolskie"}, {"63", "Wielkopolskie"}, {"64", "Wielkopolskie"},
            {"65", "Lubuskie"}, {"66", "Lubuskie"}, {"67", "Lubuskie"}, {"68", "Lubuskie"}, {"69", "Lubuskie"},
            {"70", "Zachodniopomorskie"}, {"71", "Zachodniopomorskie"}, {"72", "Zachodniopomorskie"}, {"73", "Zachodniopomorskie"}, {"74", "Zachodniopomorskie"},
            {"75", "Zachodniopomorskie"}, {"76", "Zachodniopomorskie"}, {"77", "Pomorskie"}, {"78", "Zachodniopomorskie"},
            {"80", "Pomorskie"}, {"81", "Pomorskie"}, {"82", "Pomorskie"}, {"83", "Pomorskie"}, {"84", "Pomorskie"},
            {"85", "Kujawsko-Pomorskie"}, {"86", "Kujawsko-Pomorskie"}, {"87", "Kujawsko-Pomorskie"}, {"88", "Kujawsko-Pomorskie"}, {"89", "Kujawsko-Pomorskie"},
            {"90", "Łódzkie"}, {"91", "Łódzkie"}, {"92", "Łódzkie"}, {"93", "Łódzkie"}, {"94", "Łódzkie"},
            {"95", "Łódzkie"}, {"96", "Łódzkie"}, {"97", "Łódzkie"}, {"98", "Łódzkie"}, {"99", "Łódzkie"}
        };

        public FormDodajOdbiorce(string connString, string opID)
        {
            connectionString = connString;
            operatorID = opID;
            InitializeComponent();
            WczytajPKD();
        }

        private void InitializeComponent()
        {
            this.Text = "Dodaj nowego odbiorcę";
            this.Size = new Size(520, 520);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int x = 20, y = 20, labelWidth = 110, controlWidth = 340;

            var lblNazwa = new Label { Text = "Nazwa firmy:*", Location = new Point(x, y), Size = new Size(labelWidth, 20), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            textBoxNazwa = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

            y += 35;
            var lblKod = new Label { Text = "Kod pocztowy:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxKod = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(100, 25) };
            textBoxKod.TextChanged += TextBoxKod_TextChanged;
            lblAutoInfo = new Label { Text = "(auto-uzupełni woj./miasto)", Location = new Point(x + labelWidth + 110, y + 3), Size = new Size(200, 20), ForeColor = Color.Gray, Font = new Font("Segoe UI", 8) };

            y += 35;
            var lblMiasto = new Label { Text = "Miasto:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxMiasto = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

            y += 35;
            var lblUlica = new Label { Text = "Ulica:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxUlica = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

            y += 35;
            var lblTelefon = new Label { Text = "Telefon:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxTelefon = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

            y += 35;
            var lblWoj = new Label { Text = "Województwo:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            comboBoxWoj = new ComboBox
            {
                Location = new Point(x + labelWidth, y),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxWoj.Items.Add(""); // Pusty element
            comboBoxWoj.Items.AddRange(new string[] {
                "Dolnośląskie", "Kujawsko-Pomorskie", "Lubelskie", "Lubuskie",
                "Łódzkie", "Małopolskie", "Mazowieckie", "Opolskie",
                "Podkarpackie", "Podlaskie", "Pomorskie", "Śląskie",
                "Świętokrzyskie", "Warmińsko-Mazurskie", "Wielkopolskie", "Zachodniopomorskie"
            });

            y += 35;
            var lblPowiat = new Label { Text = "Powiat:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxPowiat = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

            y += 35;
            var lblPKD = new Label { Text = "Branża (PKD):", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            comboBoxPKD = new ComboBox
            {
                Location = new Point(x + labelWidth, y),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDown, // Pozwala na wpisywanie
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            y += 45;
            checkBoxTylkoMoje = new CheckBox
            {
                Text = "Po dodaniu pokaż tylko moich klientów",
                Location = new Point(x + labelWidth, y),
                Size = new Size(controlWidth, 20),
                Checked = true
            };

            y += 40;
            buttonZapisz = new Button
            {
                Text = "Zapisz",
                Location = new Point(x + labelWidth, y),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(22, 163, 74),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            buttonZapisz.FlatAppearance.BorderSize = 0;
            buttonZapisz.Click += ButtonZapisz_Click;

            buttonAnuluj = new Button
            {
                Text = "Anuluj",
                Location = new Point(x + labelWidth + 160, y),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(107, 114, 128),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            buttonAnuluj.FlatAppearance.BorderSize = 0;
            buttonAnuluj.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                lblNazwa, textBoxNazwa,
                lblKod, textBoxKod, lblAutoInfo,
                lblMiasto, textBoxMiasto,
                lblUlica, textBoxUlica,
                lblTelefon, textBoxTelefon,
                lblWoj, comboBoxWoj,
                lblPowiat, textBoxPowiat,
                lblPKD, comboBoxPKD,
                checkBoxTylkoMoje,
                buttonZapisz, buttonAnuluj
            });
        }

        private void WczytajPKD()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT DISTINCT PKD_Opis FROM OdbiorcyCRM WHERE PKD_Opis IS NOT NULL AND PKD_Opis <> '' ORDER BY PKD_Opis", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            comboBoxPKD.Items.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch { }
        }

        private void TextBoxKod_TextChanged(object sender, EventArgs e)
        {
            string kod = textBoxKod.Text.Replace("-", "").Trim();

            if (kod.Length >= 2)
            {
                string prefix = kod.Substring(0, 2);

                // Auto-uzupełnij województwo na podstawie prefixu kodu
                if (kodDoWojewodztwa.TryGetValue(prefix, out string woj))
                {
                    int index = comboBoxWoj.Items.IndexOf(woj);
                    if (index >= 0)
                        comboBoxWoj.SelectedIndex = index;
                }

                // Spróbuj znaleźć miasto w bazie na podstawie kodu pocztowego
                if (kod.Length >= 5)
                {
                    try
                    {
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            // Sprawdź w KodyPocztowe
                            var cmd = new SqlCommand(@"
                                SELECT TOP 1 miej FROM KodyPocztowe
                                WHERE REPLACE(Kod, '-', '') = @kod", conn);
                            cmd.Parameters.AddWithValue("@kod", kod);
                            var miasto = cmd.ExecuteScalar() as string;

                            if (!string.IsNullOrEmpty(miasto) && string.IsNullOrEmpty(textBoxMiasto.Text))
                            {
                                textBoxMiasto.Text = miasto;
                            }

                            // Sprawdź też w OdbiorcyCRM dla powiatu i miasta
                            var cmd2 = new SqlCommand(@"
                                SELECT TOP 1 MIASTO, Powiat, Wojewodztwo
                                FROM OdbiorcyCRM
                                WHERE REPLACE(KOD, '-', '') = @kod", conn);
                            cmd2.Parameters.AddWithValue("@kod", kod);
                            using (var reader = cmd2.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    if (string.IsNullOrEmpty(textBoxMiasto.Text) && !reader.IsDBNull(0))
                                        textBoxMiasto.Text = reader.GetString(0);

                                    if (string.IsNullOrEmpty(textBoxPowiat.Text) && !reader.IsDBNull(1))
                                        textBoxPowiat.Text = reader.GetString(1);

                                    // Nadpisz województwo jeśli mamy dokładniejsze dane
                                    if (!reader.IsDBNull(2))
                                    {
                                        string wojDB = reader.GetString(2);
                                        int idx = comboBoxWoj.Items.IndexOf(wojDB);
                                        if (idx >= 0)
                                            comboBoxWoj.SelectedIndex = idx;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        // Właściwość publiczna do przekazania informacji o filtrowaniu
        public bool FiltrujTylkoMoje { get; private set; }

        private void ButtonZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxNazwa.Text))
            {
                MessageBox.Show("Podaj nazwę firmy!", "Brak nazwy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBoxNazwa.Focus();
                return;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Najpierw sprawdź czy tabela WlascicieleOdbiorcow istnieje
                        var cmdCheckTable = new SqlCommand(@"
                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WlascicieleOdbiorcow')
                            CREATE TABLE WlascicieleOdbiorcow (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                IDOdbiorcy INT NOT NULL,
                                OperatorID NVARCHAR(50) NOT NULL,
                                DataDodania DATETIME DEFAULT GETDATE()
                            )", conn, transaction);
                        cmdCheckTable.ExecuteNonQuery();

                        // Pobierz następne dostępne ID (tabela nie ma IDENTITY)
                        var cmdMaxId = new SqlCommand("SELECT ISNULL(MAX(ID), 0) + 1 FROM OdbiorcyCRM", conn, transaction);
                        int nowyID = (int)cmdMaxId.ExecuteScalar();

                        var cmdOdbiorca = new SqlCommand(@"
                            INSERT INTO OdbiorcyCRM
                            (ID, Nazwa, KOD, MIASTO, Ulica, Telefon_K, Wojewodztwo, Powiat, PKD_Opis, Status)
                            VALUES
                            (@id, @nazwa, @kod, @miasto, @ulica, @tel, @woj, @pow, @pkd, 'Do zadzwonienia')",
                            conn, transaction);

                        cmdOdbiorca.Parameters.AddWithValue("@id", nowyID);
                        cmdOdbiorca.Parameters.AddWithValue("@nazwa", textBoxNazwa.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@kod", textBoxKod.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@miasto", textBoxMiasto.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@ulica", textBoxUlica.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@tel", textBoxTelefon.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@woj", comboBoxWoj.Text ?? "");
                        cmdOdbiorca.Parameters.AddWithValue("@pow", textBoxPowiat.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@pkd", comboBoxPKD.Text.Trim());

                        cmdOdbiorca.ExecuteNonQuery();

                        var cmdWlasciciel = new SqlCommand(@"
                            INSERT INTO WlascicieleOdbiorcow (IDOdbiorcy, OperatorID)
                            VALUES (@odbiorca, @operator)",
                            conn, transaction);

                        cmdWlasciciel.Parameters.AddWithValue("@odbiorca", nowyID);
                        cmdWlasciciel.Parameters.AddWithValue("@operator", operatorID);
                        cmdWlasciciel.ExecuteNonQuery();

                        var cmdLog = new SqlCommand(@"
                            INSERT INTO HistoriaZmianCRM
                            (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal)
                            VALUES (@id, 'Utworzenie kontaktu', 'Dodany ręcznie', @kto)",
                            conn, transaction);

                        cmdLog.Parameters.AddWithValue("@id", nowyID);
                        cmdLog.Parameters.AddWithValue("@kto", operatorID);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();

                        FiltrujTylkoMoje = checkBoxTylkoMoje.Checked;
                        MessageBox.Show($"Dodano odbiorcę: {textBoxNazwa.Text}\nID: {nowyID}", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Błąd przy zapisie:\n" + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}