using Microsoft.Data.SqlClient;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.CRM
{
    public partial class WynikRozmowyDialog : Window
    {
        private readonly string connectionString;
        private readonly int idOdbiorcy;
        private readonly string operatorId;
        private string wybranyWynik = null;
        private DateTime? wybranaData = null;

        public bool ZapiszINastepny { get; private set; } = false;
        public bool Zapisano { get; private set; } = false;
        public string WybranyWynik => wybranyWynik;

        public WynikRozmowyDialog(string connStr, int idOdbiorcy, string nazwaFirmy,
                                   string telefon, string operatorId)
        {
            InitializeComponent();
            connectionString = connStr;
            this.idOdbiorcy = idOdbiorcy;
            this.operatorId = operatorId;

            txtNazwaFirmy.Text = nazwaFirmy;
            txtTelefon.Text = telefon;
        }

        private void BtnWynik_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                wybranyWynik = btn.Tag.ToString();

                // Reset wszystkich przyciskow
                foreach (var child in panelWyniki.Children)
                {
                    if (child is Button b && b.Tag != null)
                    {
                        b.BorderThickness = new Thickness(0);
                        b.BorderBrush = null;
                    }
                }

                // Zaznacz wybrany
                btn.BorderThickness = new Thickness(2);
                btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));

                // Pokaz panel daty dla niektorych wynikow
                if (wybranyWynik == "rozmowa_neutralna" || wybranyWynik == "rozmowa_pozytywna")
                {
                    panelData.Visibility = Visibility.Visible;
                }
                else
                {
                    panelData.Visibility = Visibility.Collapsed;
                }

                // Aktywuj przyciski zapisu
                btnZapisz.IsEnabled = true;
                btnZapiszNastepny.IsEnabled = true;

                // Automatyczne daty dla niektorych wynikow
                if (wybranyWynik == "nie_odebral")
                    wybranaData = DateTime.Today.AddDays(1);
                else if (wybranyWynik == "zajety")
                    wybranaData = DateTime.Now.AddHours(2);
            }
        }

        private void BtnData_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag.ToString(), out int dni))
            {
                wybranaData = DateTime.Today.AddDays(dni);
                dpData.SelectedDate = wybranaData;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (ZapiszWynik())
            {
                Zapisano = true;
                ZapiszINastepny = false;
                DialogResult = true;
                Close();
            }
        }

        private void BtnZapiszNastepny_Click(object sender, RoutedEventArgs e)
        {
            if (ZapiszWynik())
            {
                Zapisano = true;
                ZapiszINastepny = true;
                DialogResult = true;
                Close();
            }
        }

        private bool ZapiszWynik()
        {
            if (string.IsNullOrEmpty(wybranyWynik))
            {
                MessageBox.Show("Wybierz wynik rozmowy.", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Data z DatePicker ma priorytet
                    if (dpData.SelectedDate.HasValue)
                        wybranaData = dpData.SelectedDate.Value;

                    // Wywolaj procedure
                    var cmd = new SqlCommand("sp_RejestrujProbeKontaktu", conn);
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@IDOdbiorcy", idOdbiorcy);
                    cmd.Parameters.AddWithValue("@TypWyniku", wybranyWynik);
                    cmd.Parameters.AddWithValue("@Notatka",
                        string.IsNullOrWhiteSpace(txtNotatka.Text) ? (object)DBNull.Value : txtNotatka.Text);
                    cmd.Parameters.AddWithValue("@KtoWykonal", operatorId);
                    cmd.Parameters.AddWithValue("@DataNastepnejAkcji",
                        wybranaData.HasValue ? (object)wybranaData.Value : DBNull.Value);
                    cmd.ExecuteNonQuery();

                    // Aktualizuj engagement score
                    string akcjaEngagement = wybranyWynik switch
                    {
                        "rozmowa_pozytywna" => "zainteresowany",
                        "rozmowa_neutralna" => "moze_pozniej",
                        "rozmowa_negatywna" => "odmowa",
                        _ => "rozmowa"
                    };

                    var cmdEng = new SqlCommand("sp_AktualizujEngagement", conn);
                    cmdEng.CommandType = System.Data.CommandType.StoredProcedure;
                    cmdEng.Parameters.AddWithValue("@IDOdbiorcy", idOdbiorcy);
                    cmdEng.Parameters.AddWithValue("@Akcja", akcjaEngagement);
                    cmdEng.ExecuteNonQuery();

                    // Zmiana statusu dla pozytywnej rozmowy
                    if (wybranyWynik == "rozmowa_pozytywna")
                    {
                        var cmdStatus = new SqlCommand(@"
                            UPDATE OdbiorcyCRM SET Status = 'Nawiazano kontakt' WHERE ID = @id;
                            INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal)
                            VALUES (@id, 'Zmiana statusu', 'Nawiazano kontakt', @op);", conn);
                        cmdStatus.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdStatus.Parameters.AddWithValue("@op", operatorId);
                        cmdStatus.ExecuteNonQuery();
                    }
                    else if (wybranyWynik == "rozmowa_negatywna")
                    {
                        var cmdStatus = new SqlCommand(@"
                            UPDATE OdbiorcyCRM SET Status = 'Nie zainteresowany' WHERE ID = @id;
                            INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal)
                            VALUES (@id, 'Zmiana statusu', 'Nie zainteresowany', @op);", conn);
                        cmdStatus.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdStatus.Parameters.AddWithValue("@op", operatorId);
                        cmdStatus.ExecuteNonQuery();
                    }
                    else if (wybranyWynik == "nie_odebral" || wybranyWynik == "zajety" ||
                             wybranyWynik == "poczta_glosowa")
                    {
                        // Sprawdz czy pierwszy raz - jesli tak, zmien status
                        var cmdCheck = new SqlCommand(
                            "SELECT Status FROM OdbiorcyCRM WHERE ID = @id", conn);
                        cmdCheck.Parameters.AddWithValue("@id", idOdbiorcy);
                        var status = cmdCheck.ExecuteScalar()?.ToString();

                        if (status == "Do zadzwonienia" || string.IsNullOrEmpty(status))
                        {
                            var cmdStatus = new SqlCommand(@"
                                UPDATE OdbiorcyCRM SET Status = 'Proba kontaktu' WHERE ID = @id;
                                INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal)
                                VALUES (@id, 'Zmiana statusu', 'Proba kontaktu', @op);", conn);
                            cmdStatus.Parameters.AddWithValue("@id", idOdbiorcy);
                            cmdStatus.Parameters.AddWithValue("@op", operatorId);
                            cmdStatus.ExecuteNonQuery();
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisywania: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
