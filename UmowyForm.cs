using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class UmowyForm : Form
    {
        private readonly string _connString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Parametry wejściowe (opcjonalne)
        private readonly string? _initialLp;
        private readonly string? _initialIdLibra;

        public UmowyForm() : this(null, null) { }

        public UmowyForm(string? initialLp, string? initialIdLibra)
        {
            _initialLp = initialLp;
            _initialIdLibra = initialIdLibra;

            InitializeComponent();

            // Zdarzenia
            Load += UmowyForm_Load;

            dtpData.ValueChanged += DtpData_ValueChanged; // przelicza datę podpisania

            ComboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
            Dostawca.TextChanged += Dostawca_TextChanged;

            CommandButton_Update.Click += CommandButton_Update_Click;
        }

        #region Load / init

        private void UmowyForm_Load(object? sender, EventArgs e)
        {
            try
            {
                // Załaduj Lp do ComboBox1
                ComboBox1.Items.Clear();
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand("SELECT DISTINCT Lp FROM dbo.HarmonogramDostaw ORDER BY Lp", conn))
                {
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                            ComboBox1.Items.Add(rd["Lp"]?.ToString());
                    }
                }

                // Słowniki (jak w VBA UserForm_Initialize)
                AddComboValue(typCeny, "Wolnorynkowa");
                AddComboValue(typCeny, "Rolnicza");
                AddComboValue(typCeny, "Ministerialna");
                AddComboValue(typCeny, "Łączona");

                AddComboValue(PaszaPisklak, "Tak");
                AddComboValue(PaszaPisklak, "Nie");

                AddComboValue(CzyjaWaga, "Ubojnia");
                AddComboValue(CzyjaWaga, "Hodowca");

                AddComboValue(KonfPadl, "Sprzedającego");
                AddComboValue(KonfPadl, "Odbiorcę");

                // Jeśli przyszły parametry z zewnątrz – zastosuj
                if (!string.IsNullOrWhiteSpace(_initialLp))
                {
                    // wybierz LP (jeśli istnieje na liście)
                    var idx = -1;
                    for (int i = 0; i < ComboBox1.Items.Count; i++)
                    {
                        if (string.Equals(ComboBox1.Items[i]?.ToString(), _initialLp, StringComparison.OrdinalIgnoreCase))
                        {
                            idx = i; break;
                        }
                    }
                    if (idx >= 0) ComboBox1.SelectedIndex = idx;
                }

                if (!string.IsNullOrWhiteSpace(_initialIdLibra))
                {
                    // Załaduj dostawcę po ID (nie po nazwie)
                    LoadSupplierById(_initialIdLibra!);
                }

                // Pierwsze przeliczenie daty podpisania na podstawie dtpData
                DtpData_ValueChanged(dtpData, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd inicjalizacji: " + ex.Message);
            }
        }

        private static void AddComboValue(ComboBox cb, string value)
        {
            if (!cb.Items.Cast<object>().Any(x => string.Equals(x?.ToString(), value, StringComparison.Ordinal)))
                cb.Items.Add(value);
        }

        #endregion

        #region Date logic (dtpData -> dtpDataPodpisania)

        private void DtpData_ValueChanged(object? sender, EventArgs e)
        {
            var selectedDate = dtpData.Value;
            var adjusted = selectedDate.AddDays(-2);

            // Niedziela -> piątek (–2), Sobota -> piątek (–1)
            if (adjusted.DayOfWeek == DayOfWeek.Sunday)
                adjusted = adjusted.AddDays(-2);
            else if (adjusted.DayOfWeek == DayOfWeek.Saturday)
                adjusted = adjusted.AddDays(-1);

            dtpDataPodpisania.Value = adjusted;
        }

        #endregion

        #region ComboBox1 (Lp) -> HarmonogramDostaw

        private void ComboBox1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (ComboBox1.SelectedItem == null) return;

            var lp = ComboBox1.SelectedItem.ToString();

            const string sql = @"SELECT TOP(1) * FROM dbo.HarmonogramDostaw WHERE Lp = @lp;";
            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@lp", lp ?? (object)DBNull.Value);
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return;

                        SetText(Dostawca, rd, "Dostawca");

                        // DataOdbioru -> dtpData
                        var v = rd["DataOdbioru"];
                        if (v != DBNull.Value && DateTime.TryParse(v.ToString(), out var dt))
                            dtpData.Value = dt;

                        SetText(sztuki, rd, "SztukiDek");
                        SetText(srednia, rd, "WagaDek");
                        SetText(Cena, rd, "Cena");
                        SetTextComboOrText(typCeny, rd, "typCeny");
                        SetText(Ubytek, rd, "Ubytek");
                    }
                }

                // Po zmianie daty odbioru – przelicz podpisanie
                DtpData_ValueChanged(dtpData, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd odczytu HarmonogramDostaw: " + ex.Message);
            }
        }

        private static void SetText(TextBox tb, IDataRecord rec, string col)
        {
            var v = rec[col];
            tb.Text = v == DBNull.Value ? "" : v.ToString();
        }

        private static void SetTextComboOrText(ComboBox cb, IDataRecord rec, string col)
        {
            var v = rec[col];
            var text = v == DBNull.Value ? "" : v.ToString();
            cb.Text = text;
        }

        #endregion

        #region Dostawca -> pobierz szczegóły z dbo.Dostawcy

        private void Dostawca_TextChanged(object? sender, EventArgs e)
        {
            var name = Dostawca.Text?.Trim();
            if (string.IsNullOrEmpty(name)) return;

            const string sql = @"SELECT TOP(1) * FROM dbo.Dostawcy WHERE Name = @name;";
            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            ClearSupplierFields();
                            return;
                        }

                        FillSupplierFieldsFromReader(rd);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd odczytu Dostawcy: " + ex.Message);
            }
        }

        private void LoadSupplierById(string idLibra)
        {
            const string sql = @"SELECT TOP(1) * FROM dbo.Dostawcy WHERE ID = @id;";
            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idLibra);
                    conn.Open();
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read())
                        {
                            ClearSupplierFields();
                            return;
                        }

                        // Ustaw także nazwę do TextBoxa 'Dostawca' dla spójności z resztą logiki:
                        var nameObj = rd["Name"];
                        if (nameObj != DBNull.Value) Dostawca.Text = nameObj.ToString();

                        FillSupplierFieldsFromReader(rd);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd odczytu Dostawcy po ID: " + ex.Message);
            }
        }

        private void FillSupplierFieldsFromReader(IDataRecord rd)
        {
            SetText(IDLibra, rd, "ID");
            SetText(Address1, rd, "Address1");
            SetText(Address2, rd, "Address2");
            SetText(NIP, rd, "NIP");
            SetText(REGON, rd, "REGON");
            SetText(PESEL, rd, "PESEL");
            SetText(Phone1, rd, "Phone1");
            SetText(Phone2, rd, "Phone2");
            SetText(Info1, rd, "Info1");
            SetText(Info2, rd, "Info2");
            SetText(Email, rd, "Email");
            SetText(NrGosp, rd, "AnimNo");
            SetText(IRZPlus, rd, "IRZPlus");
            SetText(PostalCode, rd, "PostalCode");
            SetText(Address, rd, "Address");
            SetText(City, rd, "City");
        }

        private void ClearSupplierFields()
        {
            TextBox[] tbs =
            {
                IDLibra, Address1, Address2, NIP, REGON, PESEL, Phone1, Phone2,
                Info1, Info2, Email, NrGosp, IRZPlus, PostalCode, Address, City
            };

            foreach (var tb in tbs)
                tb.Text = string.Empty;
        }

        #endregion

        #region Update -> SQL UPDATE + DOCX (OpenXML)

        private void CommandButton_Update_Click(object? sender, EventArgs e)
        {
            // 1) Update Utworzone = 1 dla wybranego LP
            if (ComboBox1.SelectedItem == null)
            {
                MessageBox.Show("Wybierz Lp.");
                return;
            }

            var selectedLp = ComboBox1.SelectedItem.ToString();

            const string updateSql = @"UPDATE dbo.HarmonogramDostaw SET Utworzone = 1 WHERE Lp = @lp;";
            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("@lp", selectedLp);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji Utworzone: " + ex.Message);
                return;
            }

            // 2) Word: wstaw zamienniki i zapisz DOCX
            try
            {
                GenerateWordDocx();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd generowania dokumentu: " + ex.Message);
                return;
            }

            // 3) Zamknij okno (jak w VBA Unload Me)
            Close();
        }

        private void GenerateWordDocx()
        {
            // Ścieżki
            var root = @"\\192.168.0.170\Install\UmowyZakupu";
            var templatePath = Path.Combine(root, "UmowaZakupu.docx"); // MUSI być .docx
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Nie znaleziono szablonu Word: " + templatePath);

            var dt = dtpData.Value;

            string baseFileName = $"Umowa Zakupu {Dostawca.Text} {dt.Day}-{dt.Month}-{dt.Year}";
            string docxPath = Path.Combine(root, baseFileName + ".docx");

            // 1) Skopiuj szablon
            File.Copy(templatePath, docxPath, overwrite: true);

            // 2) Mapowanie znaczników -> wartości (daty z DTP)
            var repl = new Dictionary<string, string?>
            {
                ["[NAZWA]"] = Dostawca.Text,
                ["[AdresHodowcy]"] = Address1.Text,
                ["[KodPocztowyHodowcy]"] = Address2.Text,
                ["[NIP]"] = NIP.Text,
                ["[WAGA]"] = srednia.Text,
                ["[DataZawarciaUmowy]"] = dtpDataPodpisania.Value.ToString("yyyy-MM-dd"),
                ["[AdresKurnika]"] = Address.Text,
                ["[KodPocztowyKurnika]"] = PostalCode.Text,
                ["[SZTUKI]"] = sztuki.Text,
                ["[DataOdbioru]"] = dtpData.Value.ToString("yyyy-MM-dd"),
                ["[CzyjaWaga]"] = CzyjaWaga.Text,
                ["[Obciążenie]"] = KonfPadl.Text,
                ["[Ubytek]"] = BuildUbytekText(Ubytek.Text),
                ["[Cena]"] = BuildCenaText(typCeny.Text, Cena.Text),

                // Pasza: jeśli „Tak” → dopiska, jeśli „Nie” albo puste → pusty string (znacznik znika)
                ["[PaszaPisklak]"] = BuildPaszaText(PaszaPisklak.Text),

                ["[Odeslanie]"] = Vatowiec.Checked
                    ? "Brak odesłania podpisanej faktury VAT spowoduje wstrzymanie płatności"
                    : "Brak odesłania podpisanej faktury VAT RR spowoduje wstrzymanie płatności",
                ["[Rolnik]"] = Vatowiec.Checked
                    ? "Sprzedawca oświadcza, że nie jest rolnikiem ryczałtowym zwolnionym od podatku od towaru i usług na podstawie art. 43 ust. 1 pkt. 3 "
                    : "Sprzedawca oświadcza, że jest rolnikiem ryczałtowym zwolnionym od podatku od towaru i usług na podstawie art. 43 ust. 1 pkt. 3 ustawy o podatku od towaru i usług i nie prowadzi działalności gospodarczej."
            };

            // 3) Podmień znaczniki w kopii pliku (wersja odporna na rozbijanie runów)
            ReplacePlaceholdersInDocx_ParagraphWise(docxPath, repl);

            // 4) Otwórz utworzony plik DOCX
            System.Diagnostics.Process.Start("explorer.exe", $"\"{docxPath}\"");
        }

        private static void ReplacePlaceholdersInDocx_ParagraphWise(
            string docxPath,
            IDictionary<string, string?> replacements)
        {
            using (var doc = WordprocessingDocument.Open(docxPath, true))
            {
                void ReplaceInParagraphs(IEnumerable<Paragraph> paragraphs)
                {
                    foreach (var p in paragraphs)
                    {
                        var original = p.InnerText ?? string.Empty;
                        var replaced = original;

                        foreach (var kv in replacements)
                        {
                            if (string.IsNullOrEmpty(kv.Key)) continue;
                            replaced = replaced.Replace(kv.Key, kv.Value ?? string.Empty);
                        }

                        if (!string.Equals(original, replaced, StringComparison.Ordinal))
                        {
                            // Zachowaj formatowanie pierwszego runa w akapicie
                            var firstRunProps = p.Elements<Run>()
                                .FirstOrDefault()?.RunProperties?
                                .CloneNode(true) as RunProperties;

                            // wyczyść runy i wstaw nowy z odziedziczonym formatowaniem
                            p.RemoveAllChildren<Run>();

                            var newRun = new Run(new Text(replaced)
                            {
                                Space = SpaceProcessingModeValues.Preserve
                            });

                            if (firstRunProps != null)
                                newRun.RunProperties = firstRunProps;

                            p.Append(newRun);
                        }
                    }
                }

                var body = doc.MainDocumentPart!.Document.Body!;
                ReplaceInParagraphs(body.Descendants<Paragraph>());

                foreach (var hp in doc.MainDocumentPart.HeaderParts)
                    ReplaceInParagraphs(hp.Header.Descendants<Paragraph>());

                foreach (var fp in doc.MainDocumentPart.FooterParts)
                    ReplaceInParagraphs(fp.Footer.Descendants<Paragraph>());

                doc.MainDocumentPart.Document.Save();
            }
        }
        #endregion

        #region Teksty pomocnicze i OpenXML replace

        private static string BuildUbytekText(string? ubytekText)
        {
            if (decimal.TryParse(ubytekText, out var u))
                return u > 0 ? $"Pomniejszona o {u}% ubytków transportowych" : "";
            return "";
        }
        private static string BuildPaszaText(string? pasza)
        {
            return string.Equals(pasza?.Trim(), "Tak", StringComparison.OrdinalIgnoreCase)
                    ? " + 0.03 zł/kg dodatku"
                    : string.Empty; // „Nie” lub puste → usuń znacznik
        }
        private static string BuildCenaText(string? typCeny, string? cenaVal)
        {
            if (string.IsNullOrWhiteSpace(typCeny))
                return "";

            // Usuwamy polskie znaki i bierzemy 3 pierwsze litery (uppercase)
            string normalized = RemovePolishChars(typCeny.Trim()).ToUpperInvariant();
            string prefix = normalized.Length >= 3 ? normalized.Substring(0, 3) : normalized;

            switch (prefix)
            {
                case "WOL": // Wolnorynkowa
                    return $"to {cenaVal} zł/kg";

                case "ROL": // Rolnicza
                    return "jest ustalana na podstawie ceny rolniczej, ogłaszanej na stronie cenyrolnicze.pl";

                case "MIN": // Ministerialna
                    return "jest ustalana na podstawie ceny ministerialnej, ogłaszanej ze strony ministerstwa";

                case "LAC": // Łączona / Lączona
                    return "jest ustalana na podstawie ceny łączonej, czyli połowa ilorazu ceny rolniczej i ceny ministerialnej";

                default:
                    return "";
            }
        }

        // Pomocnicza metoda do usuwania polskich znaków
        private static string RemovePolishChars(string text)
        {
            return text
                .Replace("ą", "a").Replace("Ą", "A")
                .Replace("ć", "c").Replace("Ć", "C")
                .Replace("ę", "e").Replace("Ę", "E")
                .Replace("ł", "l").Replace("Ł", "L")
                .Replace("ń", "n").Replace("Ń", "N")
                .Replace("ó", "o").Replace("Ó", "O")
                .Replace("ś", "s").Replace("Ś", "S")
                .Replace("ż", "z").Replace("Ż", "Z")
                .Replace("ź", "z").Replace("Ź", "Z");
        }

        #endregion

        private void CommandButton_Update_Click_1(object sender, EventArgs e)
        {

        }
    }
}
