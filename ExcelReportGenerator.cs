using Microsoft.Data.SqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using OfficeOpenXml;
public class ExcelReportGenerator
{
    private string connectionString;

    public ExcelReportGenerator(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public void GenerateExcelReport(string customerGID, DateTime date, string outputPath)
    {
        try
        {
            // Pobierz dane z bazy danych dla określonego dostawcy i daty
            string query = @"SELECT [CalcDate], [CustomerGID], [FullWeight], [EmptyWeight], [NettoWeight]
                             FROM [LibraNet].[dbo].[FarmerCalc]
                             WHERE [CustomerGID] = @CustomerGID AND CONVERT(date, [CalcDate]) = @Date";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CustomerGID", customerGID);
                command.Parameters.AddWithValue("@Date", date.Date);
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                DataTable dataTable = new DataTable();
                adapter.Fill(dataTable);

                // Utwórz nowy plik Excel
                using (ExcelPackage excelPackage = new ExcelPackage())
                {
                    // Dodaj arkusz do pliku Excel
                    ExcelWorksheet worksheet = excelPackage.Workbook.Worksheets.Add("Report");

                    // Ustaw nagłówki kolumn w arkuszu Excel
                    worksheet.Cells[1, 1].Value = "CalcDate";
                    worksheet.Cells[1, 2].Value = "CustomerGID";
                    worksheet.Cells[1, 3].Value = "FullWeight";
                    worksheet.Cells[1, 4].Value = "EmptyWeight";
                    worksheet.Cells[1, 5].Value = "NettoWeight";

                    // Wstaw dane z DataTable do arkusza Excel
                    int row = 2;
                    foreach (DataRow dataRow in dataTable.Rows)
                    {
                        worksheet.Cells[row, 1].Value = dataRow["CalcDate"];
                        worksheet.Cells[row, 2].Value = dataRow["CustomerGID"];
                        worksheet.Cells[row, 3].Value = dataRow["FullWeight"];
                        worksheet.Cells[row, 4].Value = dataRow["EmptyWeight"];
                        worksheet.Cells[row, 5].Value = dataRow["NettoWeight"];
                        row++;
                    }

                    // Zapisz plik Excel na dysku
                    FileInfo excelFile = new FileInfo(outputPath);
                    excelPackage.SaveAs(excelFile);
                }

                Console.WriteLine("Raport został wygenerowany pomyślnie.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Wystąpił błąd: " + ex.Message);
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True"; // Zastąp właściwym połączeniem do bazy danych
        string customerGID = "104"; // Zastąp właściwym CustomerGID
        DateTime date = new DateTime(DateTime.Now.Year, 4, 20);// Tutaj ustaw datę, dla której chcesz wygenerować raport
        string outputPath = @"J:\\Dokumenty\Report.xlsx"; // Ścieżka do zapisania raportu Excel

        ExcelReportGenerator reportGenerator = new ExcelReportGenerator(connectionString);
        reportGenerator.GenerateExcelReport(customerGID, date, outputPath);
    }
}
