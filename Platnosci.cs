using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class Platnosci : Form
    {
        static string connectionPermission = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public Platnosci()
        {
            InitializeComponent();
            dataGridView1.RowPrePaint += dataGridView1_RowPrePaint;
            textBox1.TextChanged += textBox1_TextChanged;
        }
        private void refreshButton_Click(object sender, EventArgs e)
        {
            RefreshData();
            FormatujKolumny();
            UkryjKolumny();
        }

        private void RefreshData()
        {
            using (SqlConnection connection = new SqlConnection(connectionPermission))
            {
                try
                {
                    SqlCommand command = new SqlCommand("SELECT DISTINCT " +
                    "C.Shortcut AS Hodowca, " +
                    "DK.kod, " +
                    "DK.walbrutto AS Kwota, " +
                    "PN.kwotarozl AS Rozliczone, " +
                    "PN.wartosc AS DoZaplacenia, " +
                    "DK.data AS DataOdbioru, " +
                    "DK.plattermin AS DataTermin, " +
                    "PN.Termin AS TerminPrawdziwy, " +
                    "DATEDIFF(day, DK.data, PN.Termin) AS Termin, " +
                    "DATEDIFF(day, DK.data, GETDATE()) AS Obecny, " +
                    "(DATEDIFF(day, DK.data, PN.Termin) - DATEDIFF(day, DK.data, GETDATE())) AS Roznica " +
                    "FROM [HANDEL].[HM].[DK] DK " +
                    "JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super " +
                    "JOIN [HANDEL].[HM].[PN] PN ON DK.id = PN.dkid " +
                    "JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id " +
                    "WHERE DK.aktywny = 1 " +
                    "AND DK.ok = 0 " +
                    "AND (DK.typ_dk = 'FVR' OR DK.typ_dk = 'FVZ') " +
                    "AND DK.anulowany = 0 " +
                    "AND (DP.kod = 'Kurczak żywy - 8' OR DP.kod = 'Kurczak żywy -7') " +
                    "ORDER BY roznica ASC", connection);
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    dataGridView1.DataSource = dataTable;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd podczas pobierania danych: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            // Ustaw AutoSizeMode na AllCells dla wszystkich kolumn
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            ObliczISumujDoZaplacenia(); // Oblicz i sumuj wartości w kolumnie "DoZaplacenia"

        }
        private void dataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            int roznica = Convert.ToInt32(row.Cells["Roznica"].Value);

            if (roznica <= 0)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(244, 176, 132); // Kolor pomarańczowy
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.LightGreen;
            }
        }
        private void FormatujKolumny()
        {
            // Pętla po kolumnach DataGridView
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                // Sprawdzenie, czy kolumna zawiera nazwy kolumn "Kwota", "Rozliczone" lub "DoZaplacenia"
                if (column.Name == "Kwota" || column.Name == "Rozliczone" || column.Name == "DoZaplacenia")
                {
                    // Ustawienie formatowania dla kolumny "Kwota" i "Rozliczone"
                    column.DefaultCellStyle.Format = "N2";
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }

                // Sprawdzenie, czy kolumna zawiera nazwę "DoZaplacenia"
                if (column.Name == "DoZaplacenia")
                {
                    // Ustawienie formatowania dla kolumny "DoZaplacenia"
                    column.DefaultCellStyle.Format = "N2";
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    column.DefaultCellStyle.Font = new Font(dataGridView1.Font, FontStyle.Bold); // Pogrubienie czcionki
                    column.DefaultCellStyle.Font = new Font(column.DefaultCellStyle.Font.FontFamily, column.DefaultCellStyle.Font.Size + 1); // Zwiększenie rozmiaru czcionki o jedną jednostkę
                }
            }
        }
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string filterText = textBox1.Text.Trim().ToLower();

            // Sprawdzenie, czy istnieje źródło danych dla DataGridView
            if (dataGridView1.DataSource is DataTable dataTable)
            {
                // Ustawienie filtra dla kolumny "Hodowca"
                dataTable.DefaultView.RowFilter = $"Hodowca LIKE '%{filterText}%'";
            }
            ObliczISumujDoZaplacenia();
        }
        private void UkryjKolumny()
        {
            // Sprawdź, czy CheckBox jest zaznaczony
            if (!showAllCheckBox.Checked)
            {
                // Iteruj przez wszystkie kolumny i ukryj je, o ile nie są Hodowca, DoZaplacenia lub Roznica
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    if (column.Name != "Hodowca" && column.Name != "DoZaplacenia" && column.Name != "Roznica")
                    {
                        column.Visible = false;
                    }
                }
            }
            else
            {
                // Pokaż wszystkie kolumny
                foreach (DataGridViewColumn column in dataGridView1.Columns)
                {
                    column.Visible = true;
                }
            }
        }
        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            ObliczISumujDoZaplacenia(); // Po zmianie wartości w komórce DataGridView, ponownie oblicz i sumuj wartości w kolumnie "DoZaplacenia"
        }
        private void ObliczISumujDoZaplacenia()
        {
            decimal sumaDoZaplacenia = 0;

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                // Sprawdź, czy komórka "Roznica" zawiera liczbę mniejszą lub równą 0
                if (row.Cells["Roznica"].Value != null && int.TryParse(row.Cells["Roznica"].Value.ToString(), out int roznica) && roznica <= 0)
                {
                    // Sprawdź, czy komórka "DoZaplacenia" nie jest nullem
                    if (row.Cells["DoZaplacenia"].Value != null && decimal.TryParse(row.Cells["DoZaplacenia"].Value.ToString(), out decimal wartoscDoZaplacenia))
                    {
                        // Dodaj wartość kolumny "DoZaplacenia" do sumy
                        sumaDoZaplacenia += wartoscDoZaplacenia;
                    }
                }
            }

            // Wyświetl sumę w TextBox2
            textBox2.Text = sumaDoZaplacenia.ToString("N2") + " zł";
        }

        private void showAllCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UkryjKolumny();
            RefreshData();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            // Tworzenie zrzutu ekranu tylko dla formularza
            Bitmap screenshot = new Bitmap(this.Width, this.Height);
            this.DrawToBitmap(screenshot, new Rectangle(0, 0, this.Width, this.Height));

            // Umieszczanie zrzutu ekranu w schowku
            Clipboard.SetImage(screenshot);

            // Informacja dla użytkownika
            MessageBox.Show("Zrzut ekranu widoku formularza został umieszczony w schowku systemowym. Możesz teraz wkleić go w dowolne miejsce.", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
