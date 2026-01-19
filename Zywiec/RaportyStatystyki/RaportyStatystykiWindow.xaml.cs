using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Zywiec.RaportyStatystyki
{
    public partial class RaportyStatystykiWindow : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private List<DostawcaRaport> listaDostawcow;
        private List<DostawaRaport> aktualneDostwy;
        private List<RankingHodowcy> aktualnyRanking;

        // Progi walidacji (domyslne wartosci)
        public static decimal ProgRoznicaWagZolty = 1.0m;
        public static decimal ProgRoznicaWagCzerwony = 2.0m;
        public static decimal ProgOpasieniZolty = 0.5m;
        public static decimal ProgOpasienienCzerwony = 1.0m;
        public static decimal ProgPadleZolty = 0.5m;
        public static decimal ProgPadleCzerwony = 1.0m;
        public static decimal ProgKonfiskatyZolty = 0.3m;
        public static decimal ProgKonfiskatyCzerwony = 0.5m;

        public RaportyStatystykiWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            LoadDostawcy();
            InitializeDatePickers();
            InitializeYearComboBoxes();
            LoadProgiFromDatabase();
        }

        private void InitializeDatePickers()
        {
            // Domyslnie ostatni miesiac
            dpOd.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
            dpDo.SelectedDate = DateTime.Today;

            dpRankingOd.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
            dpRankingDo.SelectedDate = DateTime.Today;
        }

        private void InitializeYearComboBoxes()
        {
            int currentYear = DateTime.Today.Year;
            for (int year = currentYear; year >= currentYear - 5; year--)
            {
                cboRok.Items.Add(year);
                cboRankingRok.Items.Add(year);
            }
            cboRok.SelectedIndex = 0;
            cboRankingRok.SelectedIndex = 0;
        }

        private void LoadDostawcy()
        {
            listaDostawcow = new List<DostawcaRaport>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT ID AS GID, ShortName FROM dbo.Dostawcy WHERE halt = 0 ORDER BY ShortName";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            listaDostawcow.Add(new DostawcaRaport
                            {
                                GID = reader["GID"].ToString(),
                                ShortName = reader["ShortName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
                cboHodowca.ItemsSource = listaDostawcow;
                if (listaDostawcow.Count > 0)
                    cboHodowca.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania dostawcow: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProgiFromDatabase()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT SettingKey, SettingValue FROM dbo.AppSettings WHERE SettingKey LIKE 'Prog%'";
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader["SettingKey"].ToString();
                            decimal value = Convert.ToDecimal(reader["SettingValue"]);

                            switch (key)
                            {
                                case "ProgRoznicaWagZolty": ProgRoznicaWagZolty = value; break;
                                case "ProgRoznicaWagCzerwony": ProgRoznicaWagCzerwony = value; break;
                                case "ProgOpasieniZolty": ProgOpasieniZolty = value; break;
                                case "ProgOpasienienCzerwony": ProgOpasienienCzerwony = value; break;
                                case "ProgPadleZolty": ProgPadleZolty = value; break;
                                case "ProgPadleCzerwony": ProgPadleCzerwony = value; break;
                                case "ProgKonfiskatyZolty": ProgKonfiskatyZolty = value; break;
                                case "ProgKonfiskatyCzerwony": ProgKonfiskatyCzerwony = value; break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Tabela nie istnieje - uzywamy domyslnych wartosci
            }

            // Aktualizuj pola tekstowe
            txtProgRoznicaWagZolty.Text = ProgRoznicaWagZolty.ToString("F1");
            txtProgRoznicaWagCzerwony.Text = ProgRoznicaWagCzerwony.ToString("F1");
            txtProgOpasieniZolty.Text = ProgOpasieniZolty.ToString("F1");
            txtProgOpasienienCzerwony.Text = ProgOpasienienCzerwony.ToString("F1");
            txtProgPadleZolty.Text = ProgPadleZolty.ToString("F1");
            txtProgPadleCzerwony.Text = ProgPadleCzerwony.ToString("F1");
            txtProgKonfiskatyZolty.Text = ProgKonfiskatyZolty.ToString("F1");
            txtProgKonfiskatyCzerwony.Text = ProgKonfiskatyCzerwony.ToString("F1");
        }

        #region Raport Hodowcy

        private void RbZakres_Checked(object sender, RoutedEventArgs e)
        {
            if (rbRok != null && rbRok.IsChecked == true && cboRok != null)
            {
                CboRok_SelectionChanged(null, null);
            }
        }

        private void CboRok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (rbRok != null && rbRok.IsChecked == true && cboRok.SelectedItem != null)
            {
                int rok = (int)cboRok.SelectedItem;
                dpOd.SelectedDate = new DateTime(rok, 1, 1);
                dpDo.SelectedDate = new DateTime(rok, 12, 31);
            }
        }

        private void BtnGenerujRaport_Click(object sender, RoutedEventArgs e)
        {
            if (cboHodowca.SelectedValue == null)
            {
                MessageBox.Show("Wybierz hodowce.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz zakres dat.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string hodowcaGID = cboHodowca.SelectedValue.ToString();
            string hodowcaNazwa = ((DostawcaRaport)cboHodowca.SelectedItem).ShortName;
            DateTime dataOd = dpOd.SelectedDate.Value;
            DateTime dataDo = dpDo.SelectedDate.Value;

            GenerujRaportHodowcy(hodowcaGID, hodowcaNazwa, dataOd, dataDo);
        }

        private void GenerujRaportHodowcy(string hodowcaGID, string hodowcaNazwa, DateTime dataOd, DateTime dataDo)
        {
            aktualneDostwy = new List<DostawaRaport>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT
                        CalcDate,
                        NettoFarmWeight,
                        NettoWeight,
                        Opasienie,
                        DeclI2 as Padle,
                        DeclI3 + DeclI4 + DeclI5 as Konfiskaty,
                        LumQnt as SztukiLumel,
                        Price,
                        KlasaB,
                        IncDeadConf
                    FROM [LibraNet].[dbo].[FarmerCalc]
                    WHERE CustomerRealGID = @GID
                    AND CalcDate >= @DataOd AND CalcDate <= @DataDo
                    ORDER BY CalcDate";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@GID", hodowcaGID);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal nettoH = reader["NettoFarmWeight"] != DBNull.Value ? Convert.ToDecimal(reader["NettoFarmWeight"]) : 0;
                                decimal nettoU = reader["NettoWeight"] != DBNull.Value ? Convert.ToDecimal(reader["NettoWeight"]) : 0;
                                decimal opasienie = reader["Opasienie"] != DBNull.Value ? Convert.ToDecimal(reader["Opasienie"]) : 0;
                                int padle = reader["Padle"] != DBNull.Value ? Convert.ToInt32(reader["Padle"]) : 0;
                                int konfiskaty = reader["Konfiskaty"] != DBNull.Value ? Convert.ToInt32(reader["Konfiskaty"]) : 0;
                                int sztuki = reader["SztukiLumel"] != DBNull.Value ? Convert.ToInt32(reader["SztukiLumel"]) : 0;
                                decimal cena = reader["Price"] != DBNull.Value ? Convert.ToDecimal(reader["Price"]) : 0;
                                decimal klasaB = reader["KlasaB"] != DBNull.Value ? Convert.ToDecimal(reader["KlasaB"]) : 0;
                                bool piK = reader["IncDeadConf"] != DBNull.Value && Convert.ToBoolean(reader["IncDeadConf"]);

                                // Oblicz roznice procentowa
                                decimal roznicaProc = nettoH > 0 ? ((nettoU - nettoH) / nettoH) * 100 : 0;

                                // Oblicz wartosc
                                decimal sredniaWaga = sztuki > 0 ? nettoU / sztuki : 0;
                                decimal padleKG = piK ? 0 : padle * sredniaWaga;
                                decimal konfiskatyKG = piK ? 0 : konfiskaty * sredniaWaga;
                                decimal doZaplaty = nettoU - padleKG - konfiskatyKG - opasienie - klasaB;
                                decimal wartosc = doZaplaty * cena;

                                aktualneDostwy.Add(new DostawaRaport
                                {
                                    Data = Convert.ToDateTime(reader["CalcDate"]),
                                    NettoHodowcy = nettoH,
                                    NettoUbojni = nettoU,
                                    RoznicaProc = roznicaProc,
                                    Opasienie = opasienie,
                                    Padle = padle,
                                    Konfiskaty = konfiskaty,
                                    SztukiLumel = sztuki,
                                    Cena = cena,
                                    Wartosc = wartosc
                                });
                            }
                        }
                    }
                }

                // Wyswietl raport
                WyswietlRaport(hodowcaNazwa, dataOd, dataDo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad generowania raportu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WyswietlRaport(string hodowcaNazwa, DateTime dataOd, DateTime dataDo)
        {
            if (aktualneDostwy.Count == 0)
            {
                MessageBox.Show("Brak dostaw w wybranym okresie.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Ukryj placeholder, pokaz raport
            raportPlaceholder.Visibility = Visibility.Collapsed;
            raportHeader.Visibility = Visibility.Visible;
            raportStatystyki.Visibility = Visibility.Visible;
            raportTabela.Visibility = Visibility.Visible;

            // Naglowek
            lblRaportHodowca.Text = hodowcaNazwa;
            lblRaportOkres.Text = $"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}";
            lblRaportDostawy.Text = $"Dostawy: {aktualneDostwy.Count}";
            lblRaportSumaKG.Text = $"Suma: {aktualneDostwy.Sum(d => d.NettoUbojni):N0} kg";
            lblRaportWartosc.Text = $"Wartosc: {aktualneDostwy.Sum(d => d.Wartosc):N0} zl";

            // Statystyki
            decimal avgRoznica = aktualneDostwy.Average(d => d.RoznicaProc);
            decimal minRoznica = aktualneDostwy.Min(d => d.RoznicaProc);
            decimal maxRoznica = aktualneDostwy.Max(d => d.RoznicaProc);
            lblStatRoznicaWag.Text = $"{avgRoznica:F1}%";
            lblStatRoznicaWagMinMax.Text = $"Min: {minRoznica:F1}% | Max: {maxRoznica:F1}%";

            decimal sumaOpasienie = aktualneDostwy.Sum(d => d.Opasienie);
            decimal sumaNetto = aktualneDostwy.Sum(d => d.NettoUbojni);
            decimal opasienieProc = sumaNetto > 0 ? (sumaOpasienie / sumaNetto) * 100 : 0;
            lblStatOpasienie.Text = $"{sumaOpasienie:N0} kg";
            lblStatOpasienieProcent.Text = $"{opasienieProc:F2}% netto";

            int sumaPadle = aktualneDostwy.Sum(d => d.Padle);
            int sumaSztuki = aktualneDostwy.Sum(d => d.SztukiLumel);
            decimal padleProc = sumaSztuki > 0 ? ((decimal)sumaPadle / sumaSztuki) * 100 : 0;
            lblStatPadle.Text = $"{sumaPadle} szt";
            lblStatPadleProcent.Text = $"{padleProc:F2}% dostarczonych";

            // Tabela
            dgDostawy.ItemsSource = aktualneDostwy;
        }

        private void BtnDrukujRaportPDF_Click(object sender, RoutedEventArgs e)
        {
            if (aktualneDostwy == null || aktualneDostwy.Count == 0)
            {
                MessageBox.Show("Najpierw wygeneruj raport.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var hodowca = cboHodowca.SelectedItem as DostawcaRaport;
            if (hodowca == null) return;

            GenerujPDFRaportHodowcy(hodowca.ShortName, dpOd.SelectedDate.Value, dpDo.SelectedDate.Value);
        }

        private void GenerujPDFRaportHodowcy(string hodowcaNazwa, DateTime dataOd, DateTime dataDo)
        {
            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"Raport_{hodowcaNazwa}_{dataOd:yyyyMMdd}_{dataDo:yyyyMMdd}.pdf"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            Document doc = new Document(PageSize.A4, 40, 40, 40, 40);

            try
            {
                using (FileStream fs = new FileStream(dialog.FileName, FileMode.Create))
                {
                    PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                    Font titleFont = new Font(baseFont, 18, Font.BOLD, new BaseColor(92, 138, 58));
                    Font headerFont = new Font(baseFont, 12, Font.BOLD);
                    Font textFont = new Font(baseFont, 10, Font.NORMAL);
                    Font textFontBold = new Font(baseFont, 10, Font.BOLD);
                    Font smallFont = new Font(baseFont, 8, Font.NORMAL);

                    // Tytul
                    Paragraph title = new Paragraph($"RAPORT HODOWCY: {hodowcaNazwa}", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    doc.Add(title);

                    Paragraph okres = new Paragraph($"Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}", textFont);
                    okres.Alignment = Element.ALIGN_CENTER;
                    okres.SpacingAfter = 20f;
                    doc.Add(okres);

                    // Podsumowanie
                    PdfPTable summaryTable = new PdfPTable(2);
                    summaryTable.WidthPercentage = 60;
                    summaryTable.HorizontalAlignment = Element.ALIGN_LEFT;
                    summaryTable.SpacingAfter = 20f;

                    AddSummaryRow(summaryTable, "Liczba dostaw:", $"{aktualneDostwy.Count}", textFont, textFontBold);
                    AddSummaryRow(summaryTable, "Suma kg netto:", $"{aktualneDostwy.Sum(d => d.NettoUbojni):N0} kg", textFont, textFontBold);
                    AddSummaryRow(summaryTable, "Suma wartosc:", $"{aktualneDostwy.Sum(d => d.Wartosc):N0} zl", textFont, textFontBold);

                    decimal avgRoznica = aktualneDostwy.Average(d => d.RoznicaProc);
                    AddSummaryRow(summaryTable, "Srednia roznica wag:", $"{avgRoznica:F1}%", textFont, textFontBold);

                    decimal sumaOpasienie = aktualneDostwy.Sum(d => d.Opasienie);
                    decimal sumaNetto = aktualneDostwy.Sum(d => d.NettoUbojni);
                    decimal opasienieProc = sumaNetto > 0 ? (sumaOpasienie / sumaNetto) * 100 : 0;
                    AddSummaryRow(summaryTable, "Opasienie:", $"{sumaOpasienie:N0} kg ({opasienieProc:F2}%)", textFont, textFontBold);

                    int sumaPadle = aktualneDostwy.Sum(d => d.Padle);
                    int sumaSztuki = aktualneDostwy.Sum(d => d.SztukiLumel);
                    decimal padleProc = sumaSztuki > 0 ? ((decimal)sumaPadle / sumaSztuki) * 100 : 0;
                    AddSummaryRow(summaryTable, "Padle:", $"{sumaPadle} szt ({padleProc:F2}%)", textFont, textFontBold);

                    doc.Add(summaryTable);

                    // Tabela dostaw
                    Paragraph tableTitle = new Paragraph("SZCZEGOLY DOSTAW", headerFont);
                    tableTitle.SpacingAfter = 10f;
                    doc.Add(tableTitle);

                    PdfPTable dataTable = new PdfPTable(10);
                    dataTable.WidthPercentage = 100;
                    dataTable.SetWidths(new float[] { 1f, 1f, 1f, 0.8f, 0.8f, 0.6f, 0.8f, 0.6f, 0.7f, 1f });

                    // Naglowki
                    string[] headers = { "Data", "Netto H", "Netto U", "Rozn.%", "Opas.", "Padle", "Konf.", "Szt.", "Cena", "Wartosc" };
                    foreach (string header in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(header, new Font(baseFont, 8, Font.BOLD, BaseColor.WHITE)));
                        cell.BackgroundColor = new BaseColor(92, 138, 58);
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 5;
                        dataTable.AddCell(cell);
                    }

                    // Dane
                    foreach (var dostawa in aktualneDostwy)
                    {
                        AddDataCell(dataTable, dostawa.Data.ToString("dd.MM.yy"), smallFont);
                        AddDataCell(dataTable, dostawa.NettoHodowcy.ToString("N0"), smallFont);
                        AddDataCell(dataTable, dostawa.NettoUbojni.ToString("N0"), smallFont);
                        AddDataCell(dataTable, $"{dostawa.RoznicaProc:F1}%", smallFont);
                        AddDataCell(dataTable, dostawa.Opasienie.ToString("N0"), smallFont);
                        AddDataCell(dataTable, dostawa.Padle.ToString(), smallFont);
                        AddDataCell(dataTable, dostawa.Konfiskaty.ToString(), smallFont);
                        AddDataCell(dataTable, dostawa.SztukiLumel.ToString(), smallFont);
                        AddDataCell(dataTable, dostawa.Cena.ToString("F2"), smallFont);
                        AddDataCell(dataTable, dostawa.Wartosc.ToString("N0"), smallFont);
                    }

                    doc.Add(dataTable);

                    doc.Close();
                }

                MessageBox.Show($"Raport PDF zostal zapisany:\n{dialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad generowania PDF: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSummaryRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            PdfPCell labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Border = PdfPCell.NO_BORDER;
            labelCell.PaddingBottom = 5;
            table.AddCell(labelCell);

            PdfPCell valueCell = new PdfPCell(new Phrase(value, valueFont));
            valueCell.Border = PdfPCell.NO_BORDER;
            valueCell.PaddingBottom = 5;
            table.AddCell(valueCell);
        }

        private void AddDataCell(PdfPTable table, string value, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(value, font));
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Padding = 3;
            table.AddCell(cell);
        }

        #endregion

        #region Ranking Hodowcow

        private void RbRankingZakres_Checked(object sender, RoutedEventArgs e)
        {
            if (rbRankingRok != null && rbRankingRok.IsChecked == true && cboRankingRok != null && cboRankingRok.SelectedItem != null)
            {
                int rok = (int)cboRankingRok.SelectedItem;
                dpRankingOd.SelectedDate = new DateTime(rok, 1, 1);
                dpRankingDo.SelectedDate = new DateTime(rok, 12, 31);
            }
        }

        private void BtnGenerujRanking_Click(object sender, RoutedEventArgs e)
        {
            if (!dpRankingOd.SelectedDate.HasValue || !dpRankingDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz zakres dat.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DateTime dataOd = dpRankingOd.SelectedDate.Value;
            DateTime dataDo = dpRankingDo.SelectedDate.Value;
            string sortowanie = ((ComboBoxItem)cboRankingSortowanie.SelectedItem).Content.ToString();

            GenerujRanking(dataOd, dataDo, sortowanie);
        }

        private void GenerujRanking(DateTime dataOd, DateTime dataDo, string sortowanie)
        {
            aktualnyRanking = new List<RankingHodowcy>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT
                        d.ShortName as Hodowca,
                        fc.CustomerRealGID,
                        COUNT(*) as LiczbaDostawF,
                        SUM(fc.NettoWeight) as SumaKG,
                        SUM(fc.NettoFarmWeight) as SumaKGHodowcy,
                        SUM(fc.Opasienie) as SumaOpasienie,
                        SUM(fc.DeclI2) as SumaPadle,
                        SUM(fc.LumQnt) as SumaSztuk,
                        SUM(
                            CASE WHEN fc.IncDeadConf = 1
                            THEN fc.NettoWeight - fc.Opasienie - fc.KlasaB
                            ELSE fc.NettoWeight - (fc.DeclI2 * CASE WHEN fc.LumQnt > 0 THEN fc.NettoWeight / fc.LumQnt ELSE 0 END)
                                 - ((fc.DeclI3 + fc.DeclI4 + fc.DeclI5) * CASE WHEN fc.LumQnt > 0 THEN fc.NettoWeight / fc.LumQnt ELSE 0 END)
                                 - fc.Opasienie - fc.KlasaB
                            END
                        ) * AVG(fc.Price) as SumaWartosc
                    FROM [LibraNet].[dbo].[FarmerCalc] fc
                    JOIN [LibraNet].[dbo].[Dostawcy] d ON fc.CustomerRealGID = d.ID
                    WHERE fc.CalcDate >= @DataOd AND fc.CalcDate <= @DataDo
                    AND d.halt = 0
                    GROUP BY fc.CustomerRealGID, d.ShortName
                    HAVING COUNT(*) > 0";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                decimal sumaKG = reader["SumaKG"] != DBNull.Value ? Convert.ToDecimal(reader["SumaKG"]) : 0;
                                decimal sumaKGH = reader["SumaKGHodowcy"] != DBNull.Value ? Convert.ToDecimal(reader["SumaKGHodowcy"]) : 0;
                                decimal sumaOpasienie = reader["SumaOpasienie"] != DBNull.Value ? Convert.ToDecimal(reader["SumaOpasienie"]) : 0;
                                int sumaPadle = reader["SumaPadle"] != DBNull.Value ? Convert.ToInt32(reader["SumaPadle"]) : 0;
                                int sumaSztuk = reader["SumaSztuk"] != DBNull.Value ? Convert.ToInt32(reader["SumaSztuk"]) : 0;
                                decimal wartosc = reader["SumaWartosc"] != DBNull.Value ? Convert.ToDecimal(reader["SumaWartosc"]) : 0;

                                decimal roznicaWag = sumaKGH > 0 ? ((sumaKG - sumaKGH) / sumaKGH) * 100 : 0;
                                decimal opasienieProc = sumaKG > 0 ? (sumaOpasienie / sumaKG) * 100 : 0;
                                decimal padleProc = sumaSztuk > 0 ? ((decimal)sumaPadle / sumaSztuk) * 100 : 0;

                                aktualnyRanking.Add(new RankingHodowcy
                                {
                                    Hodowca = reader["Hodowca"].ToString(),
                                    LiczbaDostawF = Convert.ToInt32(reader["LiczbaDostawF"]),
                                    SumaKG = sumaKG,
                                    RoznicaWag = roznicaWag,
                                    Opasienie = opasienieProc,
                                    Padle = padleProc,
                                    Wartosc = wartosc
                                });
                            }
                        }
                    }
                }

                // Sortowanie
                switch (sortowanie)
                {
                    case "Roznica wag (najlepsza)":
                        aktualnyRanking = aktualnyRanking.OrderBy(r => Math.Abs(r.RoznicaWag)).ToList();
                        break;
                    case "Opasienie (najnizsza)":
                        aktualnyRanking = aktualnyRanking.OrderBy(r => r.Opasienie).ToList();
                        break;
                    case "Padle % (najnizsza)":
                        aktualnyRanking = aktualnyRanking.OrderBy(r => r.Padle).ToList();
                        break;
                    case "Suma kg (najwieksza)":
                        aktualnyRanking = aktualnyRanking.OrderByDescending(r => r.SumaKG).ToList();
                        break;
                    case "Liczba dostaw":
                        aktualnyRanking = aktualnyRanking.OrderByDescending(r => r.LiczbaDostawF).ToList();
                        break;
                }

                // Numeracja pozycji
                for (int i = 0; i < aktualnyRanking.Count; i++)
                {
                    aktualnyRanking[i].Pozycja = GetPozycjaText(i + 1);
                }

                dgRanking.ItemsSource = aktualnyRanking;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad generowania rankingu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetPozycjaText(int pozycja)
        {
            switch (pozycja)
            {
                case 1: return "1";
                case 2: return "2";
                case 3: return "3";
                default: return pozycja.ToString();
            }
        }

        private void BtnDrukujRankingPDF_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyRanking == null || aktualnyRanking.Count == 0)
            {
                MessageBox.Show("Najpierw wygeneruj ranking.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"Ranking_Hodowcow_{dpRankingOd.SelectedDate:yyyyMMdd}_{dpRankingDo.SelectedDate:yyyyMMdd}.pdf"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            Document doc = new Document(PageSize.A4.Rotate(), 40, 40, 40, 40);

            try
            {
                using (FileStream fs = new FileStream(dialog.FileName, FileMode.Create))
                {
                    PdfWriter.GetInstance(doc, fs);
                    doc.Open();

                    BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1250, BaseFont.EMBEDDED);
                    Font titleFont = new Font(baseFont, 18, Font.BOLD, new BaseColor(92, 138, 58));
                    Font headerFont = new Font(baseFont, 10, Font.BOLD, BaseColor.WHITE);
                    Font textFont = new Font(baseFont, 10, Font.NORMAL);

                    // Tytul
                    Paragraph title = new Paragraph("RANKING HODOWCOW", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    doc.Add(title);

                    Paragraph okres = new Paragraph($"Okres: {dpRankingOd.SelectedDate:dd.MM.yyyy} - {dpRankingDo.SelectedDate:dd.MM.yyyy}", textFont);
                    okres.Alignment = Element.ALIGN_CENTER;
                    okres.SpacingAfter = 20f;
                    doc.Add(okres);

                    // Tabela rankingu
                    PdfPTable table = new PdfPTable(8);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 0.5f, 2f, 0.8f, 1.2f, 1f, 1f, 0.8f, 1.2f });

                    string[] headers = { "Poz.", "Hodowca", "Dostawy", "Suma kg", "Rozn. wag", "Opasienie", "Padle %", "Wartosc" };
                    foreach (string header in headers)
                    {
                        PdfPCell cell = new PdfPCell(new Phrase(header, headerFont));
                        cell.BackgroundColor = new BaseColor(92, 138, 58);
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        cell.Padding = 8;
                        table.AddCell(cell);
                    }

                    foreach (var r in aktualnyRanking)
                    {
                        AddRankingCell(table, r.Pozycja, textFont);
                        AddRankingCell(table, r.Hodowca, textFont, Element.ALIGN_LEFT);
                        AddRankingCell(table, r.LiczbaDostawF.ToString(), textFont);
                        AddRankingCell(table, r.SumaKG.ToString("N0"), textFont);
                        AddRankingCell(table, r.RoznicaWagF, textFont);
                        AddRankingCell(table, r.OpasienieF, textFont);
                        AddRankingCell(table, r.PadleF, textFont);
                        AddRankingCell(table, r.WartoscF, textFont);
                    }

                    doc.Add(table);
                    doc.Close();
                }

                MessageBox.Show($"Ranking PDF zostal zapisany:\n{dialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad generowania PDF: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddRankingCell(PdfPTable table, string value, Font font, int alignment = Element.ALIGN_CENTER)
        {
            PdfPCell cell = new PdfPCell(new Phrase(value, font));
            cell.HorizontalAlignment = alignment;
            cell.Padding = 6;
            table.AddCell(cell);
        }

        #endregion

        #region Ustawienia Progow

        private void BtnZapiszProgi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parsowanie wartosci
                if (!decimal.TryParse(txtProgRoznicaWagZolty.Text, out ProgRoznicaWagZolty) ||
                    !decimal.TryParse(txtProgRoznicaWagCzerwony.Text, out ProgRoznicaWagCzerwony) ||
                    !decimal.TryParse(txtProgOpasieniZolty.Text, out ProgOpasieniZolty) ||
                    !decimal.TryParse(txtProgOpasienienCzerwony.Text, out ProgOpasienienCzerwony) ||
                    !decimal.TryParse(txtProgPadleZolty.Text, out ProgPadleZolty) ||
                    !decimal.TryParse(txtProgPadleCzerwony.Text, out ProgPadleCzerwony) ||
                    !decimal.TryParse(txtProgKonfiskatyZolty.Text, out ProgKonfiskatyZolty) ||
                    !decimal.TryParse(txtProgKonfiskatyCzerwony.Text, out ProgKonfiskatyCzerwony))
                {
                    MessageBox.Show("Wprowadz poprawne wartosci liczbowe.", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Zapis do bazy
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Utworz tabele jesli nie istnieje
                    string createTable = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AppSettings]') AND type in (N'U'))
                        CREATE TABLE [dbo].[AppSettings](
                            [SettingKey] [varchar](100) NOT NULL PRIMARY KEY,
                            [SettingValue] [varchar](500) NULL
                        )";
                    using (SqlCommand cmd = new SqlCommand(createTable, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Zapisz progi
                    var progi = new Dictionary<string, decimal>
                    {
                        { "ProgRoznicaWagZolty", ProgRoznicaWagZolty },
                        { "ProgRoznicaWagCzerwony", ProgRoznicaWagCzerwony },
                        { "ProgOpasieniZolty", ProgOpasieniZolty },
                        { "ProgOpasienienCzerwony", ProgOpasienienCzerwony },
                        { "ProgPadleZolty", ProgPadleZolty },
                        { "ProgPadleCzerwony", ProgPadleCzerwony },
                        { "ProgKonfiskatyZolty", ProgKonfiskatyZolty },
                        { "ProgKonfiskatyCzerwony", ProgKonfiskatyCzerwony }
                    };

                    foreach (var prog in progi)
                    {
                        string upsert = @"IF EXISTS (SELECT 1 FROM AppSettings WHERE SettingKey = @Key)
                            UPDATE AppSettings SET SettingValue = @Value WHERE SettingKey = @Key
                            ELSE
                            INSERT INTO AppSettings (SettingKey, SettingValue) VALUES (@Key, @Value)";
                        using (SqlCommand cmd = new SqlCommand(upsert, connection))
                        {
                            cmd.Parameters.AddWithValue("@Key", prog.Key);
                            cmd.Parameters.AddWithValue("@Value", prog.Value.ToString());
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                MessageBox.Show("Ustawienia progow zostaly zapisane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu ustawien: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPrzywrocDomyslne_Click(object sender, RoutedEventArgs e)
        {
            txtProgRoznicaWagZolty.Text = "1.0";
            txtProgRoznicaWagCzerwony.Text = "2.0";
            txtProgOpasieniZolty.Text = "0.5";
            txtProgOpasienienCzerwony.Text = "1.0";
            txtProgPadleZolty.Text = "0.5";
            txtProgPadleCzerwony.Text = "1.0";
            txtProgKonfiskatyZolty.Text = "0.3";
            txtProgKonfiskatyCzerwony.Text = "0.5";
        }

        #endregion

        #region Termin Zaplaty

        private ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        private void CboHodowca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboHodowca.SelectedValue != null)
            {
                string hodowcaGID = cboHodowca.SelectedValue.ToString();
                int termin = zapytaniasql.GetTerminZaplaty(hodowcaGID);
                txtTerminZaplaty.Text = termin.ToString();
            }
        }

        private void BtnZapiszTermin_Click(object sender, RoutedEventArgs e)
        {
            if (cboHodowca.SelectedValue == null)
            {
                MessageBox.Show("Wybierz hodowce.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(txtTerminZaplaty.Text, out int terminDni) || terminDni < 0)
            {
                MessageBox.Show("Wprowadz poprawna liczbe dni (>= 0).", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string hodowcaGID = cboHodowca.SelectedValue.ToString();
            string hodowcaNazwa = ((DostawcaRaport)cboHodowca.SelectedItem).ShortName;

            if (zapytaniasql.UpdateTerminZaplaty(hodowcaGID, terminDni))
            {
                MessageBox.Show($"Termin zaplaty dla {hodowcaNazwa} zostal ustawiony na {terminDni} dni.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    #region Modele danych

    public class DostawcaRaport
    {
        public string GID { get; set; }
        public string ShortName { get; set; }
    }

    public class DostawaRaport
    {
        public DateTime Data { get; set; }
        public decimal NettoHodowcy { get; set; }
        public decimal NettoUbojni { get; set; }
        public decimal RoznicaProc { get; set; }
        public decimal Opasienie { get; set; }
        public int Padle { get; set; }
        public int Konfiskaty { get; set; }
        public int SztukiLumel { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }
    }

    public class RankingHodowcy
    {
        public string Pozycja { get; set; }
        public string Hodowca { get; set; }
        public int LiczbaDostawF { get; set; }
        public decimal SumaKG { get; set; }
        public decimal RoznicaWag { get; set; }
        public decimal Opasienie { get; set; }
        public decimal Padle { get; set; }
        public decimal Wartosc { get; set; }

        // Formatowane wlasciwosci do wyswietlenia
        public string SumaKGF => SumaKG.ToString("N0");
        public string RoznicaWagF => $"{RoznicaWag:F1}%";
        public string OpasienieF => $"{Opasienie:F2}%";
        public string PadleF => $"{Padle:F2}%";
        public string WartoscF => $"{Wartosc:N0} zl";
    }

    #endregion
}
