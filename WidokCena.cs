using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Kalendarz1
{
    public partial class WidokCena : Form
    {
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public WidokCena()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            SetupStatus();

            // Ustawienie początkowej wartości TypCeny na jakiś domyślny element
            TypCeny.SelectedIndex = 0; // lub TypCeny.SelectedItem = "Rolnicza" / "Ministerialna"

            // Wywołanie metody Data1_ValueChanged na początku
            Data1_ValueChanged(this, EventArgs.Empty);
        }

        private void Data1_ValueChanged(object sender, EventArgs e)
        {
            DateTime data1Value = Data1.Value;
            int dzienTygodnia = (int)data1Value.DayOfWeek;

            if (TypCeny.SelectedItem.ToString() == "Rolnicza")
            {
                DateTime data2Value = DateTime.MinValue;
                DateTime data3Value = DateTime.MinValue;

                switch (dzienTygodnia)
                {
                    case 1: // Poniedziałek
                        data2Value = data1Value.AddDays(2); // Środa
                        data3Value = data1Value.AddDays(1); // Wtorek
                        break;
                    case 3: // Środa
                        data2Value = data1Value.AddDays(2); // Piątek
                        data3Value = data1Value.AddDays(1); // Wtorek
                        break;
                    case 5: // Piątek
                        data2Value = data1Value.AddDays(3); // Poniedziałek
                        data3Value = data1Value.AddDays(1); // Wtorek
                        break;
                    default:
                        MessageBox.Show("Dla wprowadzonej daty nie ma określonej daty docelowej.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        break;
                }

                if (data2Value != DateTime.MinValue && data3Value != DateTime.MinValue)
                {
                    Data2.Value = data2Value;
                    Data3.Value = data3Value;
                }
            }
            if (TypCeny.SelectedItem.ToString() == "Tuszka")
            {
                DateTime data2Value = DateTime.MinValue;
                DateTime data3Value = DateTime.MinValue;

                switch (dzienTygodnia)
                {
                    case 1: // Poniedziałek
                        data2Value = data1Value.AddDays(1); // Środa
                        data3Value = data1Value.AddDays(0); // Wtorek
                        break;
                    case 3: // Środa
                        data2Value = data1Value.AddDays(1); // Piątek
                        data3Value = data1Value.AddDays(0); // Wtorek
                        break;
                    case 5: // Piątek
                        data2Value = data1Value.AddDays(2); // Poniedziałek
                        data3Value = data1Value.AddDays(0); // Wtorek
                        break;
                    default:
                        MessageBox.Show("Dla wprowadzonej daty nie ma określonej daty docelowej.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        break;
                }

                if (data2Value != DateTime.MinValue && data3Value != DateTime.MinValue)
                {
                    Data2.Value = data2Value;
                    Data3.Value = data3Value;
                }
            }
            else if (TypCeny.SelectedItem.ToString() == "Ministerialna")
            {
                DateTime data3Value = data1Value.AddDays(8 - dzienTygodnia); // Początek następnego tygodnia (poniedziałek)
                DateTime data2Value = data1Value.AddDays(14 - dzienTygodnia); // Koniec następnego tygodnia (niedziela)

                if (data2Value != DateTime.MinValue && data3Value != DateTime.MinValue)
                {
                    Data2.Value = data2Value;
                    Data3.Value = data3Value;
                }
            }
        }


        private void SetupStatus()
        {

            // Dodaj opcje do comboBox2
            TypCeny.Items.AddRange(new string[] { "Ministerialna", "Rolnicza", "Tuszka" });

            // Opcjonalnie ustaw domyślną opcję wybraną
            TypCeny.SelectedIndex = 0; // Wybierz pierwszą opcję

        }
        private void UtworzCena_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(TypCeny.Text))
            {
                using (SqlConnection cnn = new SqlConnection(connectionPermission))
                {
                    cnn.Open();

                    DateTime data1Value = Data1.Value;
                    DateTime data2Value = Data2.Value;
                    DateTime data3Value = Data3.Value;
                    int maxLP = 0;

                    string tabela = "";
                    if (TypCeny.SelectedItem.ToString() == "Rolnicza") tabela = "dbo.CenaRolnicza";
                    else if (TypCeny.SelectedItem.ToString() == "Tuszka") tabela = "dbo.CenaTuszki";
                    else if (TypCeny.SelectedItem.ToString() == "Ministerialna") tabela = "dbo.CenaMinisterialna";
                    else
                    {
                        MessageBox.Show("Niepoprawny typ ceny.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string strSQL2 = $"SELECT MAX(Lp) AS MaxLP FROM {tabela};";
                    using (SqlCommand cmd = new SqlCommand(strSQL2, cnn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                maxLP = reader["MaxLP"] != DBNull.Value ? Convert.ToInt32(reader["MaxLP"]) : 1;
                            }
                        }
                    }

                    maxLP++;

                    // Sprawdzenie istniejących rekordów w zakresie dat
                    List<(DateTime Data, decimal Cena)> existingRecords = new List<(DateTime, decimal)>();
                    string selectQuery = $"SELECT Data, Cena FROM {tabela} WHERE Data BETWEEN @start AND @end";
                    using (SqlCommand cmd = new SqlCommand(selectQuery, cnn))
                    {
                        cmd.Parameters.AddWithValue("@start", data3Value.Date);
                        cmd.Parameters.AddWithValue("@end", data2Value.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime data = reader.GetDateTime(0);
                                decimal cena = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                                existingRecords.Add((data, cena));
                            }
                        }
                    }

                    // Jeśli istnieją jakiekolwiek ceny w tym zakresie — zapytaj użytkownika
                    if (existingRecords.Any())
                    {
                        string lista = string.Join(Environment.NewLine, existingRecords.Select(r => $"{r.Data:yyyy-MM-dd}: {r.Cena} zł"));
                        DialogResult result = MessageBox.Show(
                            $"W bazie istnieją już ceny w dniach od {data3Value:yyyy-MM-dd} do {data2Value:yyyy-MM-dd}:\n\n{lista}\n\nCzy chcesz je nadpisać?",
                            "Ceny już istnieją",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );

                        if (result == DialogResult.No)
                            return;

                        // Usuń istniejące rekordy z zakresu
                        string deleteQuery = $"DELETE FROM {tabela} WHERE Data BETWEEN @start AND @end";
                        using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, cnn))
                        {
                            deleteCmd.Parameters.AddWithValue("@start", data3Value.Date);
                            deleteCmd.Parameters.AddWithValue("@end", data2Value.Date);
                            deleteCmd.ExecuteNonQuery();
                        }
                    }

                    // Wstaw nowe rekordy
                    while (data3Value <= data2Value)
                    {
                        string insertSQL = $"INSERT INTO {tabela} (Lp, Data, Cena) VALUES (@Lp, @Data, @Cena)";
                        using (SqlCommand insertCmd = new SqlCommand(insertSQL, cnn))
                        {
                            insertCmd.Parameters.AddWithValue("@Data", data3Value.Date);
                            insertCmd.Parameters.AddWithValue("@Lp", maxLP);
                            insertCmd.Parameters.AddWithValue("@Cena", string.IsNullOrEmpty(Cena.Text) ? (object)DBNull.Value : Convert.ToDecimal(Cena.Text));
                            insertCmd.ExecuteNonQuery();
                        }

                        maxLP++;
                        data3Value = data3Value.AddDays(1);
                    }

                    cnn.Close();
                    MessageBox.Show("Dane zostały pomyślnie zapisane do bazy danych LibraNet.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                MessageBox.Show("Proszę wybrać Typ Ceny przed wykonaniem operacji.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }


        private void button1_Click(object sender, EventArgs e)
        {
            // Zamknij bieżący formularz
            this.Close();
        }

        private void TypCeny_SelectedIndexChanged(object sender, EventArgs e)
        {
            Data1_ValueChanged(this, EventArgs.Empty);
        }
    }
}
