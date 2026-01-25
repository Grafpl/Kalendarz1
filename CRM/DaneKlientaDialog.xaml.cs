using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.CRM
{
    public partial class DaneKlientaDialog : Window
    {
        private readonly string connectionString;
        private readonly int idOdbiorcy;
        private readonly string operatorId;

        public DaneKlientaDialog(string connStr, int idOdbiorcy, string nazwaFirmy, string operatorId)
        {
            InitializeComponent();
            connectionString = connStr;
            this.idOdbiorcy = idOdbiorcy;
            this.operatorId = operatorId;
            txtNazwaFirmy.Text = nazwaFirmy;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Zbierz produkty
                var produkty = new List<string>();
                if (chkTuszki.IsChecked == true) produkty.Add("Tuszki");
                if (chkFilet.IsChecked == true) produkty.Add("Filet");
                if (chkCwiartki.IsChecked == true) produkty.Add("Cwiartki");
                if (chkSkrzydla.IsChecked == true) produkty.Add("Skrzydla");
                if (chkUdka.IsChecked == true) produkty.Add("Udka");
                if (chkPodroby.IsChecked == true) produkty.Add("Podroby");

                // Parsuj wolumen
                int? wolumen = null;
                if (int.TryParse(txtWolumen.Text, out int w))
                    wolumen = w;

                // Szacuj wartosc (przykladowo 15 zl/kg * 4 tygodnie)
                decimal? wartoscSzacowana = wolumen.HasValue ? wolumen.Value * 15 * 4 : null;

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        UPDATE OdbiorcyCRM SET
                            OsobaKontaktowa = @osoba,
                            StanowiskoOsoby = @stanowisko,
                            TelefonBezposredni = @telefon,
                            InteresujaceProdukty = @produkty,
                            SzacowanyWolumenKg = @wolumen,
                            SzacowanaWartoscMiesieczna = @wartosc,
                            CzestotliwoscDostaw = @czestotliwosc,
                            AktualnyDostawca = @dostawca,
                            PowodZmianyDostawcy = @powod,
                            Status = CASE
                                WHEN @nastepnyKrok = 'oferta' THEN 'Do wyslania oferta'
                                WHEN @nastepnyKrok = 'spotkanie' THEN 'Zgoda na dalszy kontakt'
                                WHEN @nastepnyKrok = 'probki' THEN 'Zgoda na dalszy kontakt'
                                ELSE Status
                            END
                        WHERE ID = @id", conn);

                    cmd.Parameters.AddWithValue("@osoba",
                        string.IsNullOrWhiteSpace(txtOsoba.Text) ? (object)DBNull.Value : txtOsoba.Text);
                    cmd.Parameters.AddWithValue("@stanowisko",
                        cmbStanowisko.SelectedItem != null ?
                        ((ComboBoxItem)cmbStanowisko.SelectedItem).Content.ToString() :
                        (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@telefon",
                        string.IsNullOrWhiteSpace(txtTelefonBezposredni.Text) ? (object)DBNull.Value : txtTelefonBezposredni.Text);
                    cmd.Parameters.AddWithValue("@produkty",
                        produkty.Count > 0 ? string.Join(", ", produkty) : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@wolumen", wolumen.HasValue ? (object)wolumen.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@wartosc", wartoscSzacowana.HasValue ? (object)wartoscSzacowana.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@czestotliwosc",
                        cmbCzestotliwosc.SelectedItem != null ?
                        ((ComboBoxItem)cmbCzestotliwosc.SelectedItem).Content.ToString() :
                        (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@dostawca",
                        string.IsNullOrWhiteSpace(txtAktualnyDostawca.Text) ? (object)DBNull.Value : txtAktualnyDostawca.Text);
                    cmd.Parameters.AddWithValue("@powod",
                        string.IsNullOrWhiteSpace(txtPowodZmiany.Text) ? (object)DBNull.Value : txtPowodZmiany.Text);

                    string nastepnyKrok = rbWyslacOferte.IsChecked == true ? "oferta" :
                                          rbUmowicSpotkanie.IsChecked == true ? "spotkanie" : "probki";
                    cmd.Parameters.AddWithValue("@nastepnyKrok", nastepnyKrok);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);

                    cmd.ExecuteNonQuery();

                    // Dodaj do historii
                    var cmdHist = new SqlCommand(@"
                        INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal)
                        VALUES (@id, 'Zmiana statusu',
                            CASE @nastepnyKrok
                                WHEN 'oferta' THEN 'Do wyslania oferta'
                                ELSE 'Zgoda na dalszy kontakt'
                            END, @op)", conn);
                    cmdHist.Parameters.AddWithValue("@id", idOdbiorcy);
                    cmdHist.Parameters.AddWithValue("@nastepnyKrok", nastepnyKrok);
                    cmdHist.Parameters.AddWithValue("@op", operatorId);
                    cmdHist.ExecuteNonQuery();

                    // Aktualizuj engagement
                    var cmdEng = new SqlCommand("sp_AktualizujEngagement", conn);
                    cmdEng.CommandType = System.Data.CommandType.StoredProcedure;
                    cmdEng.Parameters.AddWithValue("@IDOdbiorcy", idOdbiorcy);
                    cmdEng.Parameters.AddWithValue("@Akcja", nastepnyKrok == "oferta" ? "oferta" : "spotkanie");
                    cmdEng.ExecuteNonQuery();
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisywania: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
