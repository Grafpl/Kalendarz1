using Microsoft.Data.SqlClient;
using System;
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
        private TextBox textBoxPKD;
        private Button buttonZapisz;
        private Button buttonAnuluj;

        public FormDodajOdbiorce(string connString, string opID)
        {
            connectionString = connString;
            operatorID = opID;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Dodaj nowego odbiorcę";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterParent;

            int x = 20, y = 20, labelWidth = 100, controlWidth = 300;

            var lblNazwa = new Label { Text = "Nazwa:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxNazwa = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

            y += 35;
            var lblKod = new Label { Text = "Kod pocztowy:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxKod = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

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
            var lblPKD = new Label { Text = "PKD Opis:", Location = new Point(x, y), Size = new Size(labelWidth, 20) };
            textBoxPKD = new TextBox { Location = new Point(x + labelWidth, y), Size = new Size(controlWidth, 25) };

            y += 50;
            buttonZapisz = new Button
            {
                Text = "Zapisz",
                Location = new Point(x + labelWidth, y),
                Size = new Size(140, 35),
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            buttonZapisz.Click += ButtonZapisz_Click;

            buttonAnuluj = new Button
            {
                Text = "Anuluj",
                Location = new Point(x + labelWidth + 160, y),
                Size = new Size(140, 35),
                BackColor = Color.Gray,
                ForeColor = Color.White
            };
            buttonAnuluj.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                lblNazwa, textBoxNazwa,
                lblKod, textBoxKod,
                lblMiasto, textBoxMiasto,
                lblUlica, textBoxUlica,
                lblTelefon, textBoxTelefon,
                lblWoj, comboBoxWoj,
                lblPowiat, textBoxPowiat,
                lblPKD, textBoxPKD,
                buttonZapisz, buttonAnuluj
            });
        }

        private void ButtonZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxNazwa.Text))
            {
                MessageBox.Show("Podaj nazwę firmy");
                return;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        var cmdOdbiorca = new SqlCommand(@"
                            INSERT INTO OdbiorcyCRM 
                            (Nazwa, KOD, MIASTO, Ulica, Telefon_K, Wojewodztwo, Powiat, PKD_Opis, Status)
                            OUTPUT INSERTED.ID
                            VALUES 
                            (@nazwa, @kod, @miasto, @ulica, @tel, @woj, @pow, @pkd, 'Nowy')",
                            conn, transaction);

                        cmdOdbiorca.Parameters.AddWithValue("@nazwa", textBoxNazwa.Text);
                        cmdOdbiorca.Parameters.AddWithValue("@kod", textBoxKod.Text);
                        cmdOdbiorca.Parameters.AddWithValue("@miasto", textBoxMiasto.Text);
                        cmdOdbiorca.Parameters.AddWithValue("@ulica", textBoxUlica.Text);
                        cmdOdbiorca.Parameters.AddWithValue("@tel", textBoxTelefon.Text);
                        cmdOdbiorca.Parameters.AddWithValue("@woj", comboBoxWoj.Text);
                        cmdOdbiorca.Parameters.AddWithValue("@pow", textBoxPowiat.Text);
                        cmdOdbiorca.Parameters.AddWithValue("@pkd", textBoxPKD.Text);

                        int nowyID = (int)cmdOdbiorca.ExecuteScalar();

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

                        MessageBox.Show($"Dodano odbiorcę ID: {nowyID}");
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Błąd: " + ex.Message);
                    }
                }
            }
        }
    }
}