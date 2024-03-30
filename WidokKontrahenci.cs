using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokKontrahenci : Form
    {
        // Connection string do bazy danych
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        public WidokKontrahenci()
        {
            InitializeComponent();
            DisplayDataInDataGridView();
        }
        private void DisplayDataInDataGridView()
        {
            // Zapytanie SQL
            string query = @"
    SELECT 
        D.[ID],
        D.[ShortName], 
        D.[Name], 
        D.[Address], 
        D.[PostalCode], 
        D.[Halt], 
        D.[City], 
        D.[Distance] AS 'KM', 
        P.[Name] AS 'Typ Ceny', 
        D.[Addition] AS 'Dodatek', 
        D.[Loss] AS 'Ubytek', 
        (
            SELECT MAX(CreateData) 
            FROM [LibraNet].[dbo].[PartiaDostawca] 
            WHERE CustomerID = D.ID
        ) AS OstatnieZdanie,
        D.[Phone1], 
        D.[Phone2], 
        D.[Phone3], 
        D.[AnimNo], 
        D.[IRZPlus], 
        D.[Nip], 
        D.[Regon], 
        D.[Pesel], 
        D.[Email]
    FROM 
        [LibraNet].[dbo].[Dostawcy] D
    LEFT JOIN
        [LibraNet].[dbo].[PriceType] P ON D.PriceTypeID = P.ID
    ORDER BY 
        D.ID DESC;
    ";

            // Utworzenie połączenia z bazą danych
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Utworzenie adaptera danych
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);

                // Utworzenie tabeli danych
                DataTable table = new DataTable();

                // Wypełnienie tabeli danymi z adaptera
                adapter.Fill(table);

                // Ustawienie źródła danych dla DataGridView
                dataGridView1.DataSource = table;

                dataGridView1.Columns["Halt"].Visible = false;

                // Ustawienie automatycznego dopasowywania szerokości kolumn do zawartości
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            }
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // Pobierz nazwy kolumn, w których edycja może spowodować aktualizację w bazie danych
            List<string> editableColumns = new List<string> { "ShortName", "Name", "Address", "PostalCode", "Skrót", "City", "Phone1", "Phone2", "Phone3", "IRZPlus", "Nip", "Regon", "Pesel", "Email" };

            // Sprawdź, czy edycja odbyła się w jednej z wybranych kolumn
            if (editableColumns.Contains(dataGridView1.Columns[e.ColumnIndex].HeaderText))
            {
                // Pobierz nową wartość z edytowanej komórki
                string newValue = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value.ToString();

                // Pobierz ID związane z edytowanym wierszem
                string id = dataGridView1.Rows[e.RowIndex].Cells["ID"].Value.ToString();

                // Zaktualizuj wartość w bazie danych na podstawie ID
                UpdateDatabaseValue(id, dataGridView1.Columns[e.ColumnIndex].Name, newValue);
            }
        }

        private void UpdateDatabaseValue(string id, string columnName, string newValue)
        {
            // Zapytanie SQL do aktualizacji wartości w bazie danych
            string query = $"UPDATE [LibraNet].[dbo].[Dostawcy] SET [{columnName}] = @NewValue WHERE [ID] = @ID";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Dodaj parametry do zapytania SQL
                    command.Parameters.AddWithValue("@NewValue", newValue);
                    command.Parameters.AddWithValue("@ID", id);

                    // Otwórz połączenie i wykonaj zapytanie SQL
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            {
                string filterText = textBox1.Text.Trim().ToLower();

                // Sprawdzenie, czy istnieje źródło danych dla DataGridView
                if (dataGridView1.DataSource is DataTable dataTable)
                {
                    // Ustawienie filtra dla kolumny "Dostawca"
                    dataTable.DefaultView.RowFilter = $"Name LIKE '%{filterText}%'";

                    // Przywrócenie pozycji kursora po zastosowaniu filtra
                    int currentPosition = dataGridView1.FirstDisplayedScrollingRowIndex;
                    if (currentPosition >= 0 && currentPosition < dataGridView1.RowCount)
                    {
                        dataGridView1.FirstDisplayedScrollingRowIndex = currentPosition;
                    }
                }
            }
        }

        private void buttonSprawdzDuplikaty_Click(object sender, EventArgs e)
        {
            SprawdzDuplikaty();
        }
        private void SprawdzDuplikaty()
        {
            HashSet<string> unikalneNazwy = new HashSet<string>();
            List<string> zduplikowaneNazwy = new List<string>();

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.Cells["Name"].Value != null && !string.IsNullOrWhiteSpace(row.Cells["Name"].Value.ToString()))
                {
                    string nazwa = row.Cells["Name"].Value.ToString().Trim().ToLower();

                    if (unikalneNazwy.Contains(nazwa))
                    {
                        zduplikowaneNazwy.Add(nazwa);
                    }
                    else
                    {
                        unikalneNazwy.Add(nazwa);
                    }
                }
            }

            // Wyświetlenie informacji o duplikatach
            if (zduplikowaneNazwy.Count > 0)
            {
                string komunikat = "Znaleziono duplikaty dla następujących nazw:\n\n";
                komunikat += string.Join("\n", zduplikowaneNazwy);
                MessageBox.Show(komunikat, "Duplikaty", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Nie znaleziono duplikatów.", "Duplikaty", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                FormatujWierszeZgodnieZStatus(e.RowIndex);
            }
        }

        private void FormatujWierszeZgodnieZStatus(int rowIndex)
        {
            
            if (dataGridView1.Columns.Contains("Typ Ceny")) // Sprawdź, czy istnieje kolumna "Halt"
            {
                if (rowIndex >= 0)
                {
                    var typCenyCellValue = dataGridView1.Rows[rowIndex].Cells["Typ Ceny"].Value;

                    if (typCenyCellValue != null)
                    {
                        string typCeny = typCenyCellValue.ToString();

                        switch (typCeny)
                        {
                            case "rolnicza":
                                dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                                break;
                            case "ministerialna":
                                dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightBlue;
                                break;
                            case "wolnorynkowa":
                                dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightYellow; // Przykładowy kolor
                                break;
                            case "łączona":
                                dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.PaleVioletRed;
                                break;
                            default:
                                dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.White; // Domyślny kolor tła
                                break;
                        }
                    }
                }
                if (rowIndex >= 0)
                {
                    var haltCellValue = dataGridView1.Rows[rowIndex].Cells["Halt"].Value;
                    if (haltCellValue != null && haltCellValue.ToString() == "-1")
                    {
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.LightGray;
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Red; // Kolor skreślenia
                        dataGridView1.Rows[rowIndex].DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Strikeout); // Ustawienie skreślenia
                    }
                }
            }
        }
    }
}
