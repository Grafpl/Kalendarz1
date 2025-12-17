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

        // Kolory ciemnego motywu
        private readonly Color bgColor = Color.FromArgb(15, 23, 42);
        private readonly Color panelColor = Color.FromArgb(30, 41, 59);
        private readonly Color inputBgColor = Color.FromArgb(51, 65, 85);
        private readonly Color textColor = Color.FromArgb(226, 232, 240);
        private readonly Color labelColor = Color.FromArgb(180, 190, 210);
        private readonly Color accentColor = Color.FromArgb(34, 197, 94);
        private readonly Color warningColor = Color.FromArgb(239, 68, 68);

        // Kontrolki - Dane firmy
        private TextBox textBoxNazwa;
        private ComboBox comboBoxPKD;

        // Kontrolki - Adres
        private TextBox textBoxKod;
        private TextBox textBoxMiasto;
        private TextBox textBoxUlica;
        private ComboBox comboBoxWoj;
        private TextBox textBoxPowiat;

        // Kontrolki - Kontakt
        private TextBox textBoxTelefon;
        private TextBox textBoxEmail;
        private TextBox textBoxOsobaKontaktowa;
        private TextBox textBoxNotatki;

        // Przyciski i inne
        private Button buttonZapisz;
        private Button buttonAnuluj;
        private CheckBox checkBoxTylkoMoje;

        // Panel duplikatów
        private Panel panelDuplikaty;
        private Label lblPodobniKlienci;
        private ListBox listBoxPodobni;
        private System.Windows.Forms.Timer timerSzukaj;

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
            this.Text = "Dodaj nowego kontrahenta";
            this.Size = new Size(1300, 780);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = bgColor;

            // Timer do opóźnionego wyszukiwania
            timerSzukaj = new System.Windows.Forms.Timer { Interval = 300 };
            timerSzukaj.Tick += TimerSzukaj_Tick;

            int leftColX = 30;
            int rightColX = 520;
            int controlHeight = 36;
            int rowSpacing = 52;
            int labelWidth = 145;
            int inputWidth = 300;

            // ========== SEKCJA: DANE FIRMY ==========
            var panelFirma = CreateSection("DANE FIRMY", leftColX, 20, 460, 160);
            int y = 50;

            AddLabel(panelFirma, "Nazwa firmy: *", 20, y, labelWidth);
            textBoxNazwa = AddTextBox(panelFirma, labelWidth + 25, y, inputWidth, controlHeight);
            textBoxNazwa.TextChanged += TextBoxNazwa_TextChanged;

            y += rowSpacing;
            AddLabel(panelFirma, "Branża (PKD):", 20, y, labelWidth);
            comboBoxPKD = new ComboBox
            {
                Location = new Point(labelWidth + 25, y),
                Size = new Size(inputWidth, controlHeight),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                BackColor = inputBgColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12)
            };
            panelFirma.Controls.Add(comboBoxPKD);

            this.Controls.Add(panelFirma);

            // ========== SEKCJA: ADRES ==========
            var panelAdres = CreateSection("ADRES", leftColX, 195, 460, 285);
            y = 50;

            AddLabel(panelAdres, "Kod pocztowy:", 20, y, labelWidth);
            textBoxKod = AddTextBox(panelAdres, labelWidth + 25, y, 130, controlHeight);
            textBoxKod.TextChanged += TextBoxKod_TextChanged;
            var lblKodInfo = new Label
            {
                Text = "(auto-uzupełnia)",
                Location = new Point(labelWidth + 165, y + 8),
                Size = new Size(130, 24),
                ForeColor = Color.FromArgb(100, 130, 160),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.Transparent
            };
            panelAdres.Controls.Add(lblKodInfo);

            y += rowSpacing;
            AddLabel(panelAdres, "Miasto:", 20, y, labelWidth);
            textBoxMiasto = AddTextBox(panelAdres, labelWidth + 25, y, inputWidth, controlHeight);

            y += rowSpacing;
            AddLabel(panelAdres, "Ulica:", 20, y, labelWidth);
            textBoxUlica = AddTextBox(panelAdres, labelWidth + 25, y, inputWidth, controlHeight);

            y += rowSpacing;
            AddLabel(panelAdres, "Województwo:", 20, y, labelWidth);
            comboBoxWoj = new ComboBox
            {
                Location = new Point(labelWidth + 25, y),
                Size = new Size(inputWidth, controlHeight),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBgColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12)
            };
            comboBoxWoj.Items.Add("");
            comboBoxWoj.Items.AddRange(new string[] {
                "Dolnośląskie", "Kujawsko-Pomorskie", "Lubelskie", "Lubuskie",
                "Łódzkie", "Małopolskie", "Mazowieckie", "Opolskie",
                "Podkarpackie", "Podlaskie", "Pomorskie", "Śląskie",
                "Świętokrzyskie", "Warmińsko-Mazurskie", "Wielkopolskie", "Zachodniopomorskie"
            });
            panelAdres.Controls.Add(comboBoxWoj);

            y += rowSpacing;
            AddLabel(panelAdres, "Powiat:", 20, y, labelWidth);
            textBoxPowiat = AddTextBox(panelAdres, labelWidth + 25, y, inputWidth, controlHeight);

            this.Controls.Add(panelAdres);

            // ========== SEKCJA: KONTAKT ==========
            var panelKontakt = CreateSection("KONTAKT", leftColX, 495, 460, 220);
            y = 50;

            AddLabel(panelKontakt, "Telefon:", 20, y, labelWidth);
            textBoxTelefon = AddTextBox(panelKontakt, labelWidth + 25, y, 200, controlHeight);

            y += rowSpacing;
            AddLabel(panelKontakt, "Email:", 20, y, labelWidth);
            textBoxEmail = AddTextBox(panelKontakt, labelWidth + 25, y, inputWidth, controlHeight);

            y += rowSpacing;
            AddLabel(panelKontakt, "Osoba kontaktowa:", 20, y, labelWidth);
            textBoxOsobaKontaktowa = AddTextBox(panelKontakt, labelWidth + 25, y, inputWidth, controlHeight);

            this.Controls.Add(panelKontakt);

            // ========== PANEL DUPLIKATÓW (po prawej - SZERSZY) ==========
            panelDuplikaty = CreateSection("PODOBNI KLIENCI W BAZIE", rightColX, 20, 730, 630);

            var lblInfo = new Label
            {
                Text = "Podczas wpisywania nazwy firmy automatycznie wyszukiwani są podobni klienci w bazie CRM.\nPozwala to uniknąć dodawania duplikatów.",
                Location = new Point(25, 50),
                Size = new Size(680, 55),
                ForeColor = labelColor,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.Transparent
            };
            panelDuplikaty.Controls.Add(lblInfo);

            lblPodobniKlienci = new Label
            {
                Text = "Znalezione dopasowania:",
                Location = new Point(25, 115),
                Size = new Size(680, 30),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = warningColor,
                BackColor = Color.Transparent,
                Visible = false
            };
            panelDuplikaty.Controls.Add(lblPodobniKlienci);

            listBoxPodobni = new ListBox
            {
                Location = new Point(25, 150),
                Size = new Size(680, 450),
                Font = new Font("Segoe UI", 12),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(80, 20, 20),
                ForeColor = Color.FromArgb(255, 200, 200),
                ItemHeight = 32,
                Visible = false
            };
            listBoxPodobni.DoubleClick += ListBoxPodobni_DoubleClick;
            panelDuplikaty.Controls.Add(listBoxPodobni);

            // Placeholder gdy brak wyników
            var lblBrakWynikow = new Label
            {
                Name = "lblBrakWynikow",
                Text = "Zacznij wpisywać nazwę firmy\naby sprawdzić czy klient już istnieje w bazie.\n\n(minimum 3 znaki)",
                Location = new Point(25, 200),
                Size = new Size(680, 150),
                ForeColor = labelColor,
                Font = new Font("Segoe UI", 14),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelDuplikaty.Controls.Add(lblBrakWynikow);

            this.Controls.Add(panelDuplikaty);

            // ========== NOTATKI (pod sekcją Kontakt) ==========
            var lblNotatki = new Label
            {
                Text = "Notatki:",
                Location = new Point(leftColX + 20, 725),
                Size = new Size(100, 28),
                ForeColor = labelColor,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblNotatki);

            textBoxNotatki = new TextBox
            {
                Location = new Point(leftColX + 130, 720),
                Size = new Size(350, 40),
                BackColor = inputBgColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                Multiline = true
            };
            this.Controls.Add(textBoxNotatki);

            // ========== PRZYCISKI ==========
            buttonZapisz = new Button
            {
                Text = "ZAPISZ KONTRAHENTA",
                Location = new Point(rightColX + 25, 670),
                Size = new Size(280, 60),
                BackColor = accentColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            buttonZapisz.FlatAppearance.BorderSize = 0;
            buttonZapisz.Click += ButtonZapisz_Click;
            this.Controls.Add(buttonZapisz);

            buttonAnuluj = new Button
            {
                Text = "Anuluj",
                Location = new Point(rightColX + 330, 670),
                Size = new Size(160, 60),
                BackColor = Color.FromArgb(71, 85, 105),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 13),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            buttonAnuluj.FlatAppearance.BorderSize = 0;
            buttonAnuluj.Click += (s, e) => this.Close();
            this.Controls.Add(buttonAnuluj);

            checkBoxTylkoMoje = new CheckBox
            {
                Text = "Po dodaniu pokaż tylko moich klientów",
                Location = new Point(rightColX + 520, 690),
                Size = new Size(200, 40),
                Checked = true,
                ForeColor = textColor,
                Font = new Font("Segoe UI", 9),
                AutoSize = false
            };
            this.Controls.Add(checkBoxTylkoMoje);
        }

        private Panel CreateSection(string title, int x, int y, int width, int height)
        {
            var panel = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = panelColor
            };

            var lblTitle = new Label
            {
                Text = title,
                Location = new Point(20, 12),
                Size = new Size(width - 40, 30),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = accentColor,
                BackColor = Color.Transparent
            };
            panel.Controls.Add(lblTitle);

            // Linia pod tytułem
            var line = new Panel
            {
                Location = new Point(20, 42),
                Size = new Size(width - 40, 2),
                BackColor = Color.FromArgb(51, 65, 85)
            };
            panel.Controls.Add(line);

            return panel;
        }

        private void AddLabel(Panel parent, string text, int x, int y, int width)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y + 6),
                Size = new Size(width, 28),
                ForeColor = labelColor,
                Font = new Font("Segoe UI", 11),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(lbl);
        }

        private TextBox AddTextBox(Panel parent, int x, int y, int width, int height)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = inputBgColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 12)
            };
            parent.Controls.Add(txt);
            return txt;
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

                if (kodDoWojewodztwa.TryGetValue(prefix, out string woj))
                {
                    int index = comboBoxWoj.Items.IndexOf(woj);
                    if (index >= 0)
                        comboBoxWoj.SelectedIndex = index;
                }

                if (kod.Length >= 5)
                {
                    try
                    {
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            var cmd = new SqlCommand(@"
                                SELECT TOP 1 miej FROM KodyPocztowe
                                WHERE REPLACE(Kod, '-', '') = @kod", conn);
                            cmd.Parameters.AddWithValue("@kod", kod);
                            var miasto = cmd.ExecuteScalar() as string;

                            if (!string.IsNullOrEmpty(miasto) && string.IsNullOrEmpty(textBoxMiasto.Text))
                            {
                                textBoxMiasto.Text = miasto;
                            }

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

        public bool FiltrujTylkoMoje { get; private set; }

        private void ButtonZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxNazwa.Text))
            {
                MessageBox.Show("Podaj nazwę firmy!", "Brak nazwy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBoxNazwa.Focus();
                return;
            }

            // Ostrzeżenie jeśli są duplikaty
            if (listBoxPodobni.Visible && listBoxPodobni.Items.Count > 0)
            {
                var result = MessageBox.Show(
                    $"Znaleziono {listBoxPodobni.Items.Count} podobnych klientów w bazie.\n\nCzy na pewno chcesz dodać nowego kontrahenta?",
                    "Możliwy duplikat",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result == DialogResult.No)
                    return;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Sprawdź i utwórz tabele/kolumny jeśli nie istnieją
                        var cmdCheckTable = new SqlCommand(@"
                            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WlascicieleOdbiorcow')
                            CREATE TABLE WlascicieleOdbiorcow (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                IDOdbiorcy INT NOT NULL,
                                OperatorID NVARCHAR(50) NOT NULL,
                                DataDodania DATETIME DEFAULT GETDATE()
                            )", conn, transaction);
                        cmdCheckTable.ExecuteNonQuery();

                        // Sprawdź i utwórz brakujące kolumny (tak jak w EdycjaKontaktuWindow)
                        string[] kolumny = { "Imie", "Nazwisko", "Stanowisko", "TelefonDodatkowy" };
                        foreach (var kol in kolumny)
                        {
                            var cmdKol = new SqlCommand($@"
                                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = '{kol}')
                                ALTER TABLE OdbiorcyCRM ADD {kol} NVARCHAR(200) NULL", conn, transaction);
                            cmdKol.ExecuteNonQuery();
                        }

                        var cmdMaxId = new SqlCommand("SELECT ISNULL(MAX(ID), 0) + 1 FROM OdbiorcyCRM", conn, transaction);
                        int nowyID = (int)cmdMaxId.ExecuteScalar();

                        // Rozdziel osobę kontaktową na Imię i Nazwisko
                        string osobaKontaktowa = textBoxOsobaKontaktowa.Text.Trim();
                        string imie = "", nazwisko = "";
                        if (!string.IsNullOrEmpty(osobaKontaktowa))
                        {
                            var czesci = osobaKontaktowa.Split(new[] { ' ' }, 2);
                            imie = czesci[0];
                            nazwisko = czesci.Length > 1 ? czesci[1] : "";
                        }

                        var cmdOdbiorca = new SqlCommand(@"
                            INSERT INTO OdbiorcyCRM
                            (ID, Nazwa, KOD, MIASTO, Ulica, Telefon_K, Email, Wojewodztwo, PKD_Opis, Status, Imie, Nazwisko)
                            VALUES
                            (@id, @nazwa, @kod, @miasto, @ulica, @tel, @email, @woj, @pkd, 'Do zadzwonienia', @imie, @nazwisko)",
                            conn, transaction);

                        cmdOdbiorca.Parameters.AddWithValue("@id", nowyID);
                        cmdOdbiorca.Parameters.AddWithValue("@nazwa", textBoxNazwa.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@kod", textBoxKod.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@miasto", textBoxMiasto.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@ulica", textBoxUlica.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@tel", textBoxTelefon.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@email", textBoxEmail.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@woj", comboBoxWoj.Text ?? "");
                        cmdOdbiorca.Parameters.AddWithValue("@pkd", comboBoxPKD.Text.Trim());
                        cmdOdbiorca.Parameters.AddWithValue("@imie", imie);
                        cmdOdbiorca.Parameters.AddWithValue("@nazwisko", nazwisko);

                        cmdOdbiorca.ExecuteNonQuery();

                        // Dodaj notatkę jeśli jest
                        if (!string.IsNullOrWhiteSpace(textBoxNotatki.Text))
                        {
                            var cmdNotatka = new SqlCommand(@"
                                INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal)
                                VALUES (@id, @tresc, @kto)", conn, transaction);
                            cmdNotatka.Parameters.AddWithValue("@id", nowyID);
                            cmdNotatka.Parameters.AddWithValue("@tresc", textBoxNotatki.Text.Trim());
                            cmdNotatka.Parameters.AddWithValue("@kto", operatorID);
                            cmdNotatka.ExecuteNonQuery();
                        }

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
                        MessageBox.Show($"Dodano kontrahenta: {textBoxNazwa.Text}\nID: {nowyID}", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void TextBoxNazwa_TextChanged(object sender, EventArgs e)
        {
            timerSzukaj.Stop();
            if (textBoxNazwa.Text.Length >= 3)
            {
                timerSzukaj.Start();
            }
            else
            {
                UkryjWyniki();
            }
        }

        private void TimerSzukaj_Tick(object sender, EventArgs e)
        {
            timerSzukaj.Stop();
            SzukajPodobnychKlientow();
        }

        private void UkryjWyniki()
        {
            lblPodobniKlienci.Visible = false;
            listBoxPodobni.Visible = false;
            var lblBrak = panelDuplikaty.Controls["lblBrakWynikow"];
            if (lblBrak != null) lblBrak.Visible = true;
        }

        private void PokazWyniki(int count)
        {
            bool maWyniki = count > 0;
            lblPodobniKlienci.Visible = maWyniki;
            listBoxPodobni.Visible = maWyniki;
            var lblBrak = panelDuplikaty.Controls["lblBrakWynikow"];
            if (lblBrak != null) lblBrak.Visible = !maWyniki;

            if (maWyniki)
            {
                lblPodobniKlienci.Text = $"UWAGA! Znaleziono {count} podobnych klientów:";
            }
        }

        private void SzukajPodobnychKlientow()
        {
            string szukany = textBoxNazwa.Text.Trim();
            if (szukany.Length < 3) return;

            listBoxPodobni.Items.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT TOP 50 ID, Nazwa, MIASTO, Status
                        FROM OdbiorcyCRM
                        WHERE Nazwa LIKE '%' + @szukany + '%'
                        ORDER BY
                            CASE
                                WHEN Nazwa LIKE @szukany + '%' THEN 1
                                WHEN Nazwa LIKE '%' + @szukany + '%' THEN 2
                                ELSE 3
                            END,
                            Nazwa", conn);
                    cmd.Parameters.AddWithValue("@szukany", szukany);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            string miasto = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            string status = reader.IsDBNull(3) ? "" : reader.GetString(3);

                            string info = $"[{id}]  {nazwa}";
                            if (!string.IsNullOrEmpty(miasto))
                                info += $"   •   {miasto}";
                            if (!string.IsNullOrEmpty(status))
                                info += $"   ({status})";

                            listBoxPodobni.Items.Add(info);
                        }
                    }
                }

                PokazWyniki(listBoxPodobni.Items.Count);
            }
            catch { }
        }

        private void ListBoxPodobni_DoubleClick(object sender, EventArgs e)
        {
            if (listBoxPodobni.SelectedItem == null) return;

            string wybrany = listBoxPodobni.SelectedItem.ToString();
            int startIdx = wybrany.IndexOf('[');
            int endIdx = wybrany.IndexOf(']');
            if (startIdx >= 0 && endIdx > startIdx)
            {
                string idStr = wybrany.Substring(startIdx + 1, endIdx - startIdx - 1);
                if (int.TryParse(idStr, out int id))
                {
                    var result = MessageBox.Show(
                        $"Wybrany klient już istnieje w CRM:\n\n{wybrany}\n\nCzy na pewno chcesz dodać nowego kontrahenta?",
                        "Klient już istnieje",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.No)
                    {
                        this.DialogResult = DialogResult.Cancel;
                        this.Close();
                    }
                }
            }
        }
    }
}
