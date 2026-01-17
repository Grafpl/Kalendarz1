using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zadania
{
    public partial class ZadanieDialog : Window
    {
        private readonly string connectionString;
        private readonly string operatorId;
        private readonly ZadanieViewModel existingTask;
        private readonly bool isEditMode;
        private List<FirmaItem> firmy = new List<FirmaItem>();

        public ZadanieDialog(string connString, string opId, ZadanieViewModel task = null)
        {
            InitializeComponent();
            connectionString = connString;
            operatorId = opId;
            existingTask = task;
            isEditMode = task != null;

            if (isEditMode)
            {
                txtHeader.Text = "Edytuj Zadanie";
                btnZapisz.Content = "Aktualizuj";
            }

            LoadFirmy();
            InitializeForm();
        }

        private void LoadFirmy()
        {
            firmy.Clear();
            firmy.Add(new FirmaItem { Id = 0, Nazwa = "(Brak firmy)" });

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(
                        "SELECT ID, Nazwa FROM OdbiorcyCRM WHERE Nazwa IS NOT NULL ORDER BY Nazwa", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            firmy.Add(new FirmaItem
                            {
                                Id = reader.GetInt64(0),
                                Nazwa = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore errors loading companies
            }

            cmbFirma.ItemsSource = firmy;
            cmbFirma.DisplayMemberPath = "Nazwa";
            cmbFirma.SelectedValuePath = "Id";
            cmbFirma.SelectedIndex = 0;
        }

        private void InitializeForm()
        {
            dpTermin.SelectedDate = DateTime.Today;

            if (isEditMode && existingTask != null)
            {
                txtTypZadania.Text = existingTask.TypZadania;
                txtOpis.Text = existingTask.Opis;
                dpTermin.SelectedDate = existingTask.TerminWykonania.Date;
                txtGodzina.Text = existingTask.TerminWykonania.ToString("HH:mm");

                // Set priority
                rbNiski.IsChecked = existingTask.Priorytet == 1;
                rbSredni.IsChecked = existingTask.Priorytet == 2;
                rbWysoki.IsChecked = existingTask.Priorytet == 3;

                // Set company
                if (existingTask.IDOdbiorcy > 0)
                {
                    cmbFirma.SelectedValue = existingTask.IDOdbiorcy;
                }
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(txtTypZadania.Text))
            {
                MessageBox.Show("Wprowadź typ zadania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTypZadania.Focus();
                return;
            }

            if (!dpTermin.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz termin wykonania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse time
            DateTime termin = dpTermin.SelectedDate.Value;
            if (TimeSpan.TryParse(txtGodzina.Text, out TimeSpan time))
            {
                termin = termin.Add(time);
            }
            else
            {
                termin = termin.AddHours(12);
            }

            // Get priority
            int priorytet = rbWysoki.IsChecked == true ? 3 :
                           rbSredni.IsChecked == true ? 2 : 1;

            // Get company ID
            long? firmaId = null;
            if (cmbFirma.SelectedValue is long id && id > 0)
            {
                firmaId = id;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    if (isEditMode)
                    {
                        var cmd = new SqlCommand(@"
                            UPDATE Zadania SET
                                TypZadania = @typ,
                                Opis = @opis,
                                TerminWykonania = @termin,
                                Priorytet = @priorytet,
                                IDOdbiorcy = @firma
                            WHERE ID = @id", conn);

                        cmd.Parameters.AddWithValue("@typ", txtTypZadania.Text);
                        cmd.Parameters.AddWithValue("@opis", (object)txtOpis.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@termin", termin);
                        cmd.Parameters.AddWithValue("@priorytet", priorytet);
                        cmd.Parameters.AddWithValue("@firma", (object)firmaId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@id", existingTask.Id);
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        var cmd = new SqlCommand(@"
                            INSERT INTO Zadania (OperatorID, TypZadania, Opis, TerminWykonania, Priorytet, IDOdbiorcy, Wykonane)
                            VALUES (@operator, @typ, @opis, @termin, @priorytet, @firma, 0)", conn);

                        cmd.Parameters.AddWithValue("@operator", operatorId);
                        cmd.Parameters.AddWithValue("@typ", txtTypZadania.Text);
                        cmd.Parameters.AddWithValue("@opis", (object)txtOpis.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@termin", termin);
                        cmd.Parameters.AddWithValue("@priorytet", priorytet);
                        cmd.Parameters.AddWithValue("@firma", (object)firmaId ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class FirmaItem
    {
        public long Id { get; set; }
        public string Nazwa { get; set; }
    }
}
