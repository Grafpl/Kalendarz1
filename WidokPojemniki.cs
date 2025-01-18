using System;
using System.Data;
using System.Drawing;
using System.Net;
using System.Net.Mail;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System.Collections.Generic;
using System.IO; // Dodano dla klasy Path


namespace Kalendarz1
{
    public partial class WidokPojemniki : Form
    {
        private RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        private string connectionString2 = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        public WidokPojemniki()
        {
            InitializeComponent();
            RozwijanieComboBox.RozwijanieKontrPoKatalogu(comboBoxKontrahent, "Odbiorcy Drobiu");
        }

        private void LoadKontrahenci()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString2))
                {
                    connection.Open();
                    string query = "SELECT id, nazwa FROM kontrahenci";
                    SqlCommand command = new SqlCommand(query, connection);
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        comboBoxKontrahent.Items.Add(new
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                    reader.Close();
                }

                comboBoxKontrahent.DisplayMember = "Name";
                comboBoxKontrahent.ValueMember = "Id";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania kontrahentów: {ex.Message}");
            }
        }


        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (comboBoxKontrahent.SelectedItem == null || dateTimePickerOd.Value == null || dateTimePickerDo.Value == null)
            {
                MessageBox.Show("Wybierz kontrahenta i określ zakres dat.");
                return;
            }

            // Pobranie ID kontrahenta
            string kontrahentId = ((KeyValuePair<string, string>)comboBoxKontrahent.SelectedItem).Key;

            DateTime dataOd = dateTimePickerOd.Value;
            DateTime dataDo = dateTimePickerDo.Value;

            LoadData(kontrahentId, dataOd, dataDo);
        }


        private void LoadData(string kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            string query = @"
        SELECT 
            d.id AS dokument_id,
            d.kod AS nr_dokumentu,
            d.data AS data_dokumentu,
            DATENAME(WEEKDAY, d.data) AS dzien_tygodnia, -- Dodano dzień tygodnia
            d.opis AS opis_dokumentu,
            SUM(CASE WHEN z.kod = 'Pojemnik drobiowy E2' THEN z.ilosc ELSE 0 END) AS E2,
            SUM(CASE WHEN z.kod = 'PALETA H1' THEN z.ilosc ELSE 0 END) AS H1,
            SUM(CASE WHEN z.kod = 'PALETA EURO' THEN z.ilosc ELSE 0 END) AS EURO,
            SUM(CASE WHEN z.kod = 'PALETA PLASTIKOWA' THEN z.ilosc ELSE 0 END) AS Plastik,
            SUM(CASE WHEN z.kod = 'Paleta Drewniana' THEN z.ilosc ELSE 0 END) AS Drewno
        FROM 
            hm.MG AS d
        JOIN 
            hm.MZ AS z ON d.id = z.super
        WHERE 
            d.khid = @kontrahentId
            AND d.magazyn = 65559
            AND d.typ_dk IN ('MW1', 'MP')
            AND d.anulowany = 0
            AND d.data BETWEEN @dataOd AND @dataDo
        GROUP BY 
            d.id, d.kod, d.data, d.opis
        ORDER BY 
            d.data DESC";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString2))
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@kontrahentId", kontrahentId);
                    command.Parameters.AddWithValue("@dataOd", dataOd);
                    command.Parameters.AddWithValue("@dataDo", dataDo);

                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    // Dodaj wiersz sumy na końcu tabeli
                    if (table.Rows.Count > 0)
                    {
                        DataRow sumRow = table.NewRow();
                        sumRow["nr_dokumentu"] = "SUMA";
                        sumRow["E2"] = table.Compute("SUM(E2)", string.Empty);
                        sumRow["H1"] = table.Compute("SUM(H1)", string.Empty);
                        sumRow["EURO"] = table.Compute("SUM(EURO)", string.Empty);
                        sumRow["Plastik"] = table.Compute("SUM(Plastik)", string.Empty);
                        sumRow["Drewno"] = table.Compute("SUM(Drewno)", string.Empty);
                        table.Rows.Add(sumRow);
                    }

                    dataGridViewZestawienie.DataSource = table;

                    // Automatyczne dopasowanie szerokości kolumn
                    dataGridViewZestawienie.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}");
            }
        }

        private void btnGeneratePDF_Click(object sender, EventArgs e)
        {
            if (dataGridViewZestawienie.Rows.Count == 0)
            {
                MessageBox.Show("Brak danych do wygenerowania PDF.");
                return;
            }

            // Ścieżka do pliku w folderze tymczasowym
            string filePath = Path.Combine(Path.GetTempPath(), "ZestawieniePojemników.pdf");

            try
            {
                using (PdfWriter writer = new PdfWriter(filePath))
                {
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    document.Add(new Paragraph("Zestawienie Pojemników")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20));

                    Table table = new Table(dataGridViewZestawienie.Columns.Count);

                    // Nagłówki kolumn
                    foreach (DataGridViewColumn column in dataGridViewZestawienie.Columns)
                    {
                        table.AddHeaderCell(new Cell().Add(new Paragraph(column.HeaderText)));
                    }

                    // Dane wierszy
                    foreach (DataGridViewRow row in dataGridViewZestawienie.Rows)
                    {
                        if (row.IsNewRow) continue; // Pomijamy nowy wiersz

                        foreach (DataGridViewCell cell in row.Cells)
                        {
                            string cellValue = cell.Value?.ToString() ?? string.Empty; // Obsługa null
                            table.AddCell(new Cell().Add(new Paragraph(cellValue)));
                        }
                    }

                    document.Add(table);
                    document.Close();
                }

                MessageBox.Show($"PDF został wygenerowany w: {filePath}");
                SendEmailWithAttachment(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas generowania PDF: {ex.Message}\n{ex.StackTrace}");
            }
        }



        private void SendEmailWithAttachment(string filePath)
        {
            try
            {
                MailMessage mail = new MailMessage
                {
                    From = new MailAddress("your_email@example.com"),
                    Subject = "Prośba o potwierdzenie salda opakowań",
                    Body = @"
Szanowni Państwo,

W załączniku przesyłamy saldo, w którym wyszczególnione zostały dni z wydaniem i przyjęciem poszczególnych asortymentów.

Uprzejmie prosimy o potwierdzenie salda dotyczącego opakowań E2, palet H1 oraz pozostałych pozycji.

W przypadku jakichkolwiek pytań lub wątpliwości pozostajemy do Państwa dyspozycji.

Z poważaniem,
Ubojnia Drobiu Piórkowscy"
                };

                mail.To.Add("recipient@example.com");
                mail.Attachments.Add(new Attachment(filePath));

                SmtpClient client = new SmtpClient("smtp.example.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("your_email@example.com", "your_password"),
                    EnableSsl = true
                };

                client.Send(mail);
                MessageBox.Show("E-mail został wysłany.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wysyłania e-maila: {ex.Message}");
            }
        }
    }
}
