using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
// UWAGA: nie używaj 'using Microsoft.Office.Interop.Word;' żeby nie kolidować z Windows.Forms.CheckBox
using Word = Microsoft.Office.Interop.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Collections.Generic;

namespace Kalendarz1
{
    public partial class UmowyForm : Form
    {
        private readonly string _connString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Do toggle DatePicker (może być null na starcie)
        private TextBox? _dateTargetTextBox;

        public UmowyForm()
        {
            InitializeComponent();

            // Ukryj kalendarz na starcie
            frameDatePicker.Visible = false;

            // Zdarzenia
            this.Load += UmowyForm_Load;

            dateButton0.Click += (s, e) => ToggleDatePicker(data);
            dateButton1.Click += (s, e) => ToggleDatePicker(dataPodpisania);

            monthCalendar1.DateSelected += MonthCalendar1_DateSelected;

            data.TextChanged += Data_TextChanged_AdjustSignedDate;

            ComboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;

            Dostawca.TextChanged += Dostawca_TextChanged;

            CommandButton_Update.Click += CommandButton_Update_Click;
        }

        #region Load / init

        private void UmowyForm_Load(object sender, EventArgs e)
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

        #region DatePicker (toggle + wybór dnia)

        private void ToggleDatePicker(TextBox target)
        {
            if (frameDatePicker.Visible && _dateTargetTextBox == target)
            {
                frameDatePicker.Visible = false;
                _dateTargetTextBox = null;
                return;
            }

            _dateTargetTextBox = target;

            if (DateTime.TryParse(target.Text, out var parsed))
                monthCalendar1.SetDate(parsed);
            else
                monthCalendar1.SetDate(DateTime.Today);

            // Pozycja panelu
            frameDatePicker.Top = target.Bottom + 6;
            frameDatePicker.Left = target.Left;
            frameDatePicker.Visible = true;
            frameDatePicker.BringToFront();
        }

        private void MonthCalendar1_DateSelected(object sender, DateRangeEventArgs e)
        {
            if (_dateTargetTextBox != null)
            {
                _dateTargetTextBox.Text = e.Start.ToString("yyyy-MM-dd");
            }
            frameDatePicker.Visible = false;
            _dateTargetTextBox = null;
        }

        #endregion

        #region data -> dataPodpisania (–2 dni, weekend -> piątek)

        private void Data_TextChanged_AdjustSignedDate(object sender, EventArgs e)
        {
            if (!DateTime.TryParse(data.Text, out var selectedDate))
                return;

            var adjusted = selectedDate.AddDays(-2);

            // Niedziela -> piątek (–2), Sobota -> piątek (–1)
            if (adjusted.DayOfWeek == DayOfWeek.Sunday)
                adjusted = adjusted.AddDays(-2);
            else if (adjusted.DayOfWeek == DayOfWeek.Saturday)
                adjusted = adjusted.AddDays(-1);

            dataPodpisania.Text = adjusted.ToString("yyyy-MM-dd");
        }

        #endregion

        #region ComboBox1 (Lp) -> HarmonogramDostaw

        private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
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
                        SetDateText(data, rd, "DataOdbioru"); // yyyy-MM-dd
                        SetText(sztuki, rd, "SztukiDek");
                        SetText(srednia, rd, "WagaDek");
                        SetText(Cena, rd, "Cena");
                        SetTextComboOrText(typCeny, rd, "typCeny");
                        SetText(Ubytek, rd, "Ubytek");
                    }
                }
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

        private static void SetDateText(TextBox tb, IDataRecord rec, string col)
        {
            var v = rec[col];
            if (v == DBNull.Value) { tb.Text = ""; return; }
            if (DateTime.TryParse(v.ToString(), out var dt))
                tb.Text = dt.ToString("yyyy-MM-dd");
            else
                tb.Text = v.ToString();
        }

        #endregion

        #region Dostawca -> pobierz szczegóły z dbo.Dostawcy

        private void Dostawca_TextChanged(object sender, EventArgs e)
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
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd odczytu Dostawcy: " + ex.Message);
            }
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

        #region Update -> SQL UPDATE + Word (DOCX + PDF)

        private void CommandButton_Update_Click(object sender, EventArgs e)
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

            // 2) Word: wstaw zamienniki, zapisz DOCX i PDF, otwórz PDF
            try
            {
                GenerateWordAndPdf();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd generowania dokumentu: " + ex.Message);
                return;
            }

            // 3) Zamknij okno (jak w VBA Unload Me)
            this.Close();
        }

        private void GenerateWordAndPdf()
        {
            // Ścieżki
            var root = @"\\192.168.0.170\Install\UmowyZakupu";
            var templatePath = Path.Combine(root, "UmowaZakupu.docx"); // UPEWNIJ SIĘ, że to .docx (nie .doc)
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Nie znaleziono szablonu Word: " + templatePath);

            if (!DateTime.TryParse(data.Text, out var dt))
                throw new InvalidOperationException("Pole DataOdbioru jest niepoprawne.");

            string rok = dt.Year.ToString();
            string miesiac = dt.Month.ToString();
            string dzien = dt.Day.ToString();

            string baseFileName = $"Umowa Zakupu {Dostawca.Text} {dzien}-{miesiac}-{rok}";
            string docxPath = Path.Combine(root, baseFileName + ".docx");

            // 1) Skopiuj szablon
            File.Copy(templatePath, docxPath, overwrite: true);

            // 2) Mapowanie znaczników -> wartości
            var repl = new Dictionary<string, string?>
            {
                ["[NAZWA]"] = Dostawca.Text,
                ["[AdresHodowcy]"] = Address1.Text,
                ["[KodPocztowyHodowcy]"] = Address2.Text,
                ["[NIP]"] = NIP.Text,
                ["[WAGA]"] = srednia.Text,
                ["[DataZawarciaUmowy]"] = dataPodpisania.Text,
                ["[AdresKurnika]"] = Address.Text,
                ["[KodPocztowyKurnika]"] = PostalCode.Text,
                ["[SZTUKI]"] = sztuki.Text,
                ["[DataOdbioru]"] = data.Text,
                ["[CzyjaWaga]"] = CzyjaWaga.Text,
                ["[Obciążenie]"] = KonfPadl.Text,
                ["[Ubytek]"] = BuildUbytekText(Ubytek.Text),
                ["[Cena]"] = BuildCenaText(typCeny.Text, Cena.Text),
                ["[PaszaPisklak]"] = BuildPaszaText(PaszaPisklak.Text),
                ["[Odeslanie]"] = Vatowiec.Checked
                    ? "Brak odesłania podpisanej faktury VAT spowoduje wstrzymanie płatności"
                    : "Brak odesłania podpisanej faktury VAT RR spowoduje wstrzymanie płatności",
                ["[Rolnik]"] = Vatowiec.Checked
                    ? "Sprzedawca oświadcza, że nie jest rolnikiem ryczałtowym zwolnionym od podatku od towaru i usług na podstawie art. 43 ust. 1 pkt. 3 "
                    : "Sprzedawca oświadcza, że jest rolnikiem ryczałtowym zwolnionym od podatku od towaru i usług na podstawie art. 43 ust. 1 pkt. 3 ustawy o podatku od towaru i usług i nie prowadzi działalności gospodarczej."
            };

            // 3) Podmień znaczniki w kopii pliku
            ReplacePlaceholdersInDocx(docxPath, repl);

            // 4) Otwórz utworzony plik DOCX
            System.Diagnostics.Process.Start("explorer.exe", $"\"{docxPath}\"");
        }
        private static string BuildUbytekText(string? ubytekText)
        {
            if (decimal.TryParse(ubytekText, out var u))
                return u > 0 ? $"Pomniejszona o {u}% ubytków transportowych" : "";
            return "";
        }

        private static string BuildCenaText(string? typCeny, string? cenaVal)
        {
            var typ = (typCeny ?? "").Trim().ToUpperInvariant();
            if (typ == "WOLNORYNKOWA") return $"to {cenaVal} zł/kg";
            if (typ == "ROLNICZA") return "jest ustalana na podstawie ceny rolniczej, ogłaszanej na stronie cenyrolnicze.pl";
            if (typ == "MINISTERIALNA") return "jest ustalana na podstawie ceny ministerialnej, ogłaszanej ze strony ministerstwa";
            if (typ == "ŁĄCZONA" || typ == "LĄCZONA" || typ == "LACZONA")
                return "jest ustalana na podstawie ceny łączonej, czyli połowa ilorazu ceny rolniczej i ceny ministerialnej";
            return "";
        }

        private static string BuildPaszaText(string? pasza)
        {
            return string.Equals(pasza, "Tak", StringComparison.OrdinalIgnoreCase)
                ? " + 0.03 zł/kg dodatku"
                : "";
        }

        private static void ReplaceAll(Word.Range rng, string findText, string replaceText)
        {
            var find = rng.Find;
            find.ClearFormatting();
            find.Text = findText;
            find.Replacement.ClearFormatting();
            find.Replacement.Text = replaceText ?? "";
            object replaceAll = Word.WdReplace.wdReplaceAll;
            find.Execute(FindText: Type.Missing, MatchCase: false, MatchWholeWord: false,
                         MatchWildcards: false, MatchSoundsLike: false, MatchAllWordForms: false,
                         Forward: true, Wrap: Word.WdFindWrap.wdFindContinue, Format: false,
                         ReplaceWith: Type.Missing, Replace: replaceAll);
        }

        #endregion
        private static void ReplacePlaceholdersInDocx(string docxPath, IDictionary<string, string?> replacements)
        {
            using (var doc = WordprocessingDocument.Open(docxPath, true))
            {
                var body = doc.MainDocumentPart!.Document.Body!;
                // Uwaga: w DOCX tekst bywa pocięty na wiele "runów".
                // Prosty, ale skuteczny sposób: lecieć po wszystkich Text i replace.
                foreach (var text in body.Descendants<Text>())
                {
                    if (string.IsNullOrEmpty(text.Text)) continue;

                    foreach (var kv in replacements)
                    {
                        if (string.IsNullOrEmpty(kv.Key)) continue;
                        var newVal = kv.Value ?? string.Empty;
                        if (text.Text.Contains(kv.Key))
                            text.Text = text.Text.Replace(kv.Key, newVal);
                    }
                }
                doc.MainDocumentPart.Document.Save();
            }
        }
        private void CommandButton_Update_Click_1(object sender, EventArgs e)
        {

        }
    }
}
