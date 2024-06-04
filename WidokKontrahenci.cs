using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokKontrahenci : Form
    {
        // Connection string do bazy danych
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Lista dla PriceType
        private List<KeyValuePair<int, string>> priceTypeList;

        public WidokKontrahenci()
        {
            InitializeComponent();
            LoadPriceTypes();
            DisplayDataInDataGridView();
        }

        private void LoadPriceTypes()
        {
            string query = "SELECT [ID], [Name] FROM [LibraNet].[dbo].[PriceType]";
            priceTypeList = new List<KeyValuePair<int, string>>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            priceTypeList.Add(new KeyValuePair<int, string>(reader.GetInt32(0), reader.GetString(1)));
                        }
                    }
                }
            }
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
        D.[PriceTypeID], 
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

                // Ukrycie kolumny Halt
               // dataGridView1.Columns["Halt"].Visible = false;

                // Ustawienie ComboBox dla kolumny PriceTypeID
                DataGridViewComboBoxColumn comboBoxColumn = new DataGridViewComboBoxColumn();
                comboBoxColumn.HeaderText = "Typ Ceny";
                comboBoxColumn.Name = "PriceTypeID";
                comboBoxColumn.DataSource = priceTypeList;
                comboBoxColumn.DisplayMember = "Value";
                comboBoxColumn.ValueMember = "Key";
                comboBoxColumn.DataPropertyName = "PriceTypeID";
                comboBoxColumn.FlatStyle = FlatStyle.Flat;

                int columnIndex = dataGridView1.Columns["PriceTypeID"].Index;
                dataGridView1.Columns.Remove("PriceTypeID");
                dataGridView1.Columns.Insert(columnIndex, comboBoxColumn);

                // Ustawienie automatycznego dopasowywania szerokości kolumn do zawartości
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            }
        }

        private void dataGridView1_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dataGridView1.CurrentCell.ColumnIndex == dataGridView1.Columns["PriceTypeID"].Index && e.Control is ComboBox comboBox)
            {
                comboBox.SelectedIndexChanged -= ComboBox_SelectedIndexChanged;
                comboBox.SelectedIndexChanged += ComboBox_SelectedIndexChanged;
            }
        }

        private void ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue != null && dataGridView1.CurrentCell.RowIndex >= 0)
            {
                if (int.TryParse(comboBox.SelectedValue.ToString(), out int selectedPriceTypeId))
                {
                    int currentRowIndex = dataGridView1.CurrentCell.RowIndex;
                    string id = dataGridView1.Rows[currentRowIndex].Cells["ID"].Value.ToString();

                    // Zaktualizuj wartość PriceTypeID w bazie danych
                    UpdateDatabaseValue(id, "PriceTypeID", selectedPriceTypeId.ToString());

                    // Aktualizuj wartość w DataGridView
                    dataGridView1.Rows[currentRowIndex].Cells["PriceTypeID"].Value = selectedPriceTypeId;
                }
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

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            // Pobierz nazwy kolumn, w których edycja może spowodować aktualizację w bazie danych
            List<string> editableColumns = new List<string> { "ShortName", "Name", "Halt", "Address", "PostalCode", "Skrót", "City", "Phone1", "Phone2", "Phone3", "IRZPlus", "Nip", "Regon", "Pesel", "Email" };

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

        private void FormatujWierszeZgodnieZStatus(int rowIndex)
        {
            if (dataGridView1.Columns.Contains("Typ Ceny")) // Sprawdź, czy istnieje kolumna "Typ Ceny"
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
