using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class UmowyForm : Form
    {
        private readonly string _connString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string connectionString2 =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Parametry wejściowe (opcjonalne)
        private readonly string? _initialLp;
        private readonly string? _initialIdLibra;
        public string UserID { get; set; }

        private DataTable _hodowcyTable;
        private readonly BindingSource _hodowcyBS = new BindingSource();

        private DataTable _kontrahenciTable;
        private readonly BindingSource _kontrahenciBS = new BindingSource();

        private readonly Timer _filterTimer = new Timer { Interval = 250 };
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private static ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        public UmowyForm() : this(null, null) { }

        public UmowyForm(string? initialLp, string? initialIdLibra)
        {
            _initialLp = initialLp;
            _initialIdLibra = initialIdLibra;

            InitializeComponent();

            // listy wyboru
            zapytaniasql.UzupelnijComboBoxHodowcami3(comboBoxDostawca);
            UzupelnijComboDostawcy(comboBoxDostawcaS);

            // Zdarzenia
            Load += UmowyForm_Load;

            dtpData.ValueChanged += DtpData_ValueChanged;

            ComboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
            Dostawca1.TextChanged += Dostawca_TextChanged;

            dataGridViewKontrahenci.DataBindingComplete += DataGridViewKontrahenci_DataBindingComplete;
            dataGridViewKontrahenci.RowHeadersVisible = false;

            CommandButton_Update.Click += CommandButton_Update_Click;

            // Debounce filtrowania
            _filterTimer.Tick += (s, e) =>
            {
                _filterTimer.Stop();
                ApplyFilter(textBoxFiltrKontrahent.Text);
            };
            textBoxFiltrKontrahent.TextChanged += (s, e) =>
            {
                _filterTimer.Stop();
                _filterTimer.Start();
            };

            comboBoxDostawcaS.SelectionChangeCommitted += comboBoxDostawcaS_SelectionChangeCommitted;
            comboBoxDostawca.SelectedIndexChanged += comboBoxDostawca_SelectedIndexChanged;

            buttonZapisz.Click += buttonZapisz_Click;
        }

        #region Load / init

        private void DataGridViewKontrahenci_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
        {
            var g = dataGridViewKontrahenci;
            if (g.Columns.Count == 0) return;

            foreach (DataGridViewColumn c in g.Columns)
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                c.MinimumWidth = 60;
            }

            var colKontr = g.Columns["Kontrahent"];
            if (colKontr != null)
            {
                colKontr.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                colKontr.FillWeight = 300;
                colKontr.MinimumWidth = 250;
            }

            if (g.Columns.Contains("DataOstatniegoDokumentu"))
                g.Columns["DataOstatniegoDokumentu"].DefaultCellStyle.Format = "yyyy-MM-dd";
        }

        private async void UmowyForm_Load(object? sender, EventArgs e)
        {
            try
            {
                // 1) Załaduj LP do ComboBox1
                ComboBox1.Items.Clear();
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand("SELECT DISTINCT Lp FROM dbo.HarmonogramDostaw ORDER BY Lp", conn))
                {
                    await conn.OpenAsync();
                    using (var rd = await cmd.ExecuteReaderAsync())
                    {
                        while (await rd.ReadAsync())
                            ComboBox1.Items.Add(rd["Lp"]?.ToString());
                    }
                }
                // jeżeli nie znaleziono IdSymf dla aktualnego dostawcy
                if (string.IsNullOrWhiteSpace(IDLibraS.Text))
                {
                    ClearSymfoniaFields();
                }

                // 2) Słowniki (listy wyboru)
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

                // 3) Parametry startowe
                if (!string.IsNullOrWhiteSpace(_initialLp))
                {
                    var idx = -1;
                    for (int i = 0; i < ComboBox1.Items.Count; i++)
                    {
                        if (string.Equals(ComboBox1.Items[i]?.ToString(), _initialLp, StringComparison.OrdinalIgnoreCase))
                        { idx = i; break; }
                    }
                    if (idx >= 0) ComboBox1.SelectedIndex = idx;
                }

                if (!string.IsNullOrWhiteSpace(_initialIdLibra))
                {
                    LoadSupplierById(_initialIdLibra!);
                }

                // 4) Przelicz datę podpisania od razu
                DtpData_ValueChanged(dtpData, EventArgs.Empty);

                // 5) Jednorazowe załadowanie tabel
                await WczytajKontrahentowAsync();
                await WczytajHodowcowAsync();
                ApplyFilter(string.Empty);

                // 6) AUTO-POWIĄZANIE: jeżeli w LibraNet.Dostawcy dla bieżącego ID jest IdSymf -> ustaw combobox i wczytaj z Symfonii
                if (!string.IsNullOrWhiteSpace(IDLibra.Text))
                {
                    var idSymf = PobierzIdSymfDlaLibraPoId(IDLibra.Text);
                    if (!string.IsNullOrWhiteSpace(idSymf))
                    {
                        // Ustaw wskazaną pozycję w comboboxie Symfonii
                        comboBoxDostawcaS.SelectedValue = idSymf;
                        // Ręcznie wczytaj pólka (SelectionChangeCommitted nie odpala się przy zmianie programowej)
                        WczytajKontrahentaSymfoniaDoFormularza(idSymf);
                    }
                }
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

                        SetText(Dostawca1, rd, "Dostawca");

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

        #region Dostawca -> pobierz szczegóły z dbo.Dostawcy (Libra)

        private void Dostawca_TextChanged(object? sender, EventArgs e)
        {
            var name = Dostawca1.Text?.Trim();
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

                        var nameObj = rd["Name"];
                        if (nameObj != DBNull.Value) Dostawca1.Text = nameObj.ToString();

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
            SetText(Dostawca, rd, "Name");
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
            if (ComboBox1.SelectedItem == null)
            {
                MessageBox.Show("Wybierz Lp.");
                return;
            }
            var selectedLp = ComboBox1.SelectedItem.ToString();

            if (!int.TryParse(UserID, out var userIdInt))
            {
                MessageBox.Show("UserID musi być liczbą całkowitą (potrzebne do zapisania KtoUtw).");
                return;
            }

            const string updateSql = @"
UPDATE dbo.HarmonogramDostaw
SET
    Utworzone = 1,
    KtoUtw    = CASE WHEN ISNULL(KtoUtw, 0) = 0 THEN @kto ELSE KtoUtw END,
    KiedyUtw  = CASE WHEN KiedyUtw IS NULL     THEN GETDATE() ELSE KiedyUtw END
WHERE Lp = @lp;";

            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand(updateSql, conn))
                {
                    cmd.Parameters.AddWithValue("@lp", selectedLp ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@kto", userIdInt);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji Utworzone/KtoUtw/KiedyUtw: " + ex.Message);
                return;
            }

            try
            {
                GenerateWordDocx();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd generowania dokumentu: " + ex.Message);
                return;
            }

            Close();
        }

        private async Task WczytajHodowcowAsync()
        {
            string sql = @"
SELECT
    [Lp#]                                   AS Lp,
    [Dostawca Drobiu_Nazwisko i Imie]       AS Dostawca,
    [Typ ceny]                              AS TypCeny,
    [Dodatek]                               AS Dodatek,
    [Ubytek]                                AS Ubytek,
    [Dane]                                  AS Dane,
    [Kod Pocztowy]                          AS KodPocztowy,
    [Kontakt]                               AS Kontakt,
    [F9]                                    AS F9,
    [F10]                                   AS F10,
    [Adres zamieszkania Hodowcy]            AS AdresZamHodowcy,
    [F12]                                   AS F12,
    [Adres Fermy (opcjonalne)]              AS AdresFermy,
    [F14]                                   AS F14,
    [Województwo]                           AS Wojewodztwo,
    [KM]                                    AS KM,
    [archiwalne ceny]                       AS ArchiwalneCeny,
    [ID_Na prdukcji]                        AS IDNaProdukcji,
    [IRZPLUS]                               AS IRZPLUS,
    [Kontrola urzędowa , badanie urzędowe , wynik ujemny, dodatni] AS KontrolaUrz,
    [Test PCR]                              AS TestPCR,
    [F22]                                   AS F22,
    [F23]                                   AS F23
  FROM [LibraNet].[dbo].['Dane hodowców$']";

            try
            {
                using var conn = new SqlConnection(_connString);
                using var cmd = new SqlCommand(sql, conn);
                await conn.OpenAsync();

                using var rdr = await cmd.ExecuteReaderAsync();
                var dt = new DataTable { CaseSensitive = false };
                dt.Load(rdr);

                _hodowcyTable = dt;
                _hodowcyBS.DataSource = _hodowcyTable.DefaultView;
                dataGridViewHodowcy.AutoGenerateColumns = true;
                dataGridViewHodowcy.DataSource = _hodowcyBS;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd wczytywania hodowców: " + ex.Message);
            }
        }

        #region Kontrahenci – ostatnie dokumenty (Handel)

        private async Task WczytajKontrahentowAsync()
        {
            const string sql = @"
SELECT
    COALESCE(C.Name,'') AS Kontrahent,
    CASE 
        WHEN LastDocs.seria IN ('sFVS', 'sFVZ') THEN 'VATowiec'
        WHEN LastDocs.seria = 'sFVR' THEN 'Rolnik'
        ELSE LastDocs.seria
    END AS TypKontrahenta,
    LastDocs.data AS DataOstatniegoDokumentu,
    LastDocs.DK_kod AS OstatniKodDokumentu
FROM [HANDEL].[SSCommon].[STContractors] C
INNER JOIN (
    SELECT 
        DK.khid,
        DK.seria,
        DP.data,
        DK.kod AS DK_kod,
        ROW_NUMBER() OVER (PARTITION BY DK.khid ORDER BY DP.data DESC) AS rn
    FROM [HANDEL].[HM].[DP] DP
    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
    INNER JOIN [HANDEL].[HM].[DK] DK ON DP.super = DK.id
    WHERE DP.data >= '2023-01-01'
      AND TW.kod LIKE '%Kurczak żywy%'
) LastDocs ON LastDocs.khid = C.id AND LastDocs.rn = 1
ORDER BY C.Shortcut;";

            try
            {
                using var conn = new SqlConnection(connectionString2);
                using var cmd = new SqlCommand(sql, conn);
                await conn.OpenAsync();

                using var rdr = await cmd.ExecuteReaderAsync();
                var dt = new DataTable { CaseSensitive = false };
                dt.Load(rdr);

                _kontrahenciTable = dt;
                _kontrahenciBS.DataSource = _kontrahenciTable.DefaultView;
                dataGridViewKontrahenci.DataSource = _kontrahenciBS;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd wczytywania kontrahentów: " + ex.Message);
            }
        }

        // --- FILTROWANIE LOKALNE ---
        private void ApplyFilter(string rawText)
        {
            string pattern = EscapeLikeValue((rawText ?? string.Empty).Trim());

            if (_kontrahenciBS.DataSource is DataView)
                _kontrahenciBS.Filter = string.IsNullOrEmpty(pattern)
                    ? string.Empty
                    : $"Convert([Kontrahent],'System.String') LIKE '%{pattern}%'";

            if (_hodowcyBS.DataSource is DataView)
                _hodowcyBS.Filter = string.IsNullOrEmpty(pattern)
                    ? string.Empty
                    : $"Convert([Dostawca],'System.String') LIKE '%{pattern}%'";
        }

        private static string EscapeLikeValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]")
                .Replace("'", "''");
        }

        #endregion

        #region Word generator

        private void GenerateWordDocx()
        {
            var root = @"\\192.168.0.170\Install\UmowyZakupu";
            var templatePath = Path.Combine(root, "UmowaZakupu.docx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Nie znaleziono szablonu Word: " + templatePath);

            var dt = dtpData.Value;

            string baseFileName = $"Umowa Zakupu {Dostawca1.Text} {dt.Day}-{dt.Month}-{dt.Year}";
            string docxPath = Path.Combine(root, baseFileName + ".docx");

            File.Copy(templatePath, docxPath, overwrite: true);

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
                ["[PaszaPisklak]"] = BuildPaszaText(PaszaPisklak.Text),
                ["[Odeslanie]"] = Vatowiec.Checked
                    ? "Brak odesłania podpisanej faktury VAT spowoduje wstrzymanie płatności"
                    : "Brak odesłania podpisanej faktury VAT RR spowoduje wstrzymanie płatności",
                ["[Rolnik]"] = Vatowiec.Checked
                    ? "Sprzedawca oświadcza, że nie jest rolnikiem ryczałtowym zwolnionym od podatku od towaru i usług na podstawie art. 43 ust. 1 pkt. 3 "
                    : "Sprzedawca oświadcza, że jest rolnikiem ryczałtowym zwolnionym od podatku od towaru i usług na podstawie art. 43 ust. 1 pkt. 3 ustawy o podatku od towaru i usług i nie prowadzi działalności gospodarczej."
            };

            ReplacePlaceholdersInDocx_ParagraphWise(docxPath, repl);
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
                            var firstRunProps = p.Elements<Run>()
                                .FirstOrDefault()?.RunProperties?
                                .CloneNode(true) as RunProperties;

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

        #region Teksty pomocnicze

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
                    : string.Empty;
        }

        private static string BuildCenaText(string? typCeny, string? cenaVal)
        {
            if (string.IsNullOrWhiteSpace(typCeny))
                return "";

            string normalized = RemovePolishChars(typCeny.Trim()).ToUpperInvariant();
            string prefix = normalized.Length >= 3 ? normalized.Substring(0, 3) : normalized;

            switch (prefix)
            {
                case "WOL": return $"to {cenaVal} zł/kg";
                case "ROL": return "jest ustalana na podstawie ceny rolniczej, ogłaszanej na stronie cenyrolnicze.pl";
                case "MIN": return "jest ustalana na podstawie ceny ministerialnej, ogłaszanej ze strony ministerstwa";
                case "LAC": return "jest ustalana na podstawie ceny łączonej, czyli połowa ilorazu ceny rolniczej i ceny ministerialnej";
                default: return "";
            }
        }

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

        #region Handlery GUI

        private void comboBoxDostawca_SelectedIndexChanged(object sender, EventArgs e)
        {
            // dotychczasowe mapowanie nazwy
            Dostawca1.Text = comboBoxDostawca.Text;
            Dostawca.Text = comboBoxDostawca.Text;

            // spróbuj pobrać IdSymf
            string? idSymf = null;

            if (!string.IsNullOrWhiteSpace(IDLibra.Text))
                idSymf = PobierzIdSymfDlaLibraPoId(IDLibra.Text);

            if (string.IsNullOrWhiteSpace(idSymf) && !string.IsNullOrWhiteSpace(Dostawca1.Text))
                idSymf = PobierzIdSymfDlaLibraPoNazwie(Dostawca1.Text);

            // jeśli jest powiązanie – ustaw wybór w Symfonii i doładuj textboxy
            if (!string.IsNullOrWhiteSpace(idSymf))
            {
                comboBoxDostawcaS.SelectedValue = idSymf;              // ustawia konkretny kontrahent w comboboxie
                WczytajKontrahentaSymfoniaDoFormularza(idSymf);       // wczytuje NIP/adres/itd.
            }
            // else: nic nie robimy; możesz tu wyczyścić pola z prawej, jeśli tak wolisz
        }


        private void UzupelnijComboDostawcy(ComboBox comboBox)
        {
            const string sql = "SELECT Id, Shortcut FROM [HANDEL].[SSCommon].[STContractors] ORDER BY Shortcut";

            using (var conn = new SqlConnection(connectionString2))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    var lista = new List<KeyValuePair<string, string>>();
                    while (r.Read())
                    {
                        lista.Add(new KeyValuePair<string, string>(
                            r["Id"].ToString(),
                            r["Shortcut"].ToString()
                        ));
                    }
                    comboBox.DataSource = lista;
                    comboBox.DisplayMember = "Value";
                    comboBox.ValueMember = "Key";
                }
            }
        }

        private void comboBoxDostawcaS_SelectionChangeCommitted(object sender, EventArgs e)
        {
            string id = comboBoxDostawcaS.SelectedValue?.ToString();
            if (string.IsNullOrEmpty(id))
            {
                ClearSymfoniaFields();
                return;
            }

            const string sql = @"
SELECT 
    c.Shortcut AS Kontrahent,
    c.NIP,
    c.ID AS IDS,
    poa.Street AS Ulica,
    poa.HouseNo AS numer,
    poa.Postcode AS kod,
    c.regon As REGON,
    c.PESEL AS PESEL
FROM [HANDEL].[SSCommon].[STContractors] c
LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa
       ON poa.ContactGuid = c.ContactGuid
      AND poa.AddressName = N'adres domyślny'
WHERE c.Id = @Id";

            using (var conn = new SqlConnection(connectionString2))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        IDLibraS.Text = r["IDS"] == DBNull.Value ? "" : r["IDS"].ToString();
                        DostawcaS.Text = r["Kontrahent"] == DBNull.Value ? "" : r["Kontrahent"].ToString();
                        NIPS.Text = r["NIP"] == DBNull.Value ? "" : r["NIP"].ToString();
                        REGONS.Text = r["REGON"] == DBNull.Value ? "" : r["REGON"].ToString();
                        PESELS.Text = r["PESEL"] == DBNull.Value ? "" : r["PESEL"].ToString();
                        Address1S.Text = r["Ulica"] == DBNull.Value ? "" : r["Ulica"].ToString();
                        numer.Text = r["numer"] == DBNull.Value ? "" : r["numer"].ToString();
                        Address2S.Text = r["kod"] == DBNull.Value ? "" : r["kod"].ToString();
                    }
                    else
                    {
                        ClearSymfoniaFields();
                    }
                }
            }
        }


        private void WczytajKontrahentaSymfoniaDoFormularza(string idSymf)
        {
            const string sql = @"
SELECT 
    c.Shortcut AS Kontrahent,
    c.NIP,
    c.ID AS IDS,
    poa.Street AS Ulica,
    poa.HouseNo AS numer,
    poa.Postcode AS kod,
    c.regon As REGON,
    c.PESEL AS PESEL
FROM [HANDEL].[SSCommon].[STContractors] c
LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa
       ON poa.ContactGuid = c.ContactGuid
      AND poa.AddressName = N'adres domyślny'
WHERE c.Id = @Id";

            using (var conn = new SqlConnection(connectionString2))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@Id", SqlDbType.VarChar, 50).Value = idSymf;
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        IDLibraS.Text = r["IDS"] == DBNull.Value ? "" : r["IDS"].ToString();
                        DostawcaS.Text = r["Kontrahent"] == DBNull.Value ? "" : r["Kontrahent"].ToString();
                        NIPS.Text = r["NIP"] == DBNull.Value ? "" : r["NIP"].ToString();
                        REGONS.Text = r["REGON"] == DBNull.Value ? "" : r["REGON"].ToString();
                        PESELS.Text = r["PESEL"] == DBNull.Value ? "" : r["PESEL"].ToString();
                        Address1S.Text = r["Ulica"] == DBNull.Value ? "" : r["Ulica"].ToString();
                        numer.Text = r["numer"] == DBNull.Value ? "" : r["numer"].ToString();
                        Address2S.Text = r["kod"] == DBNull.Value ? "" : r["kod"].ToString();
                    }
                    else
                    {
                        IDLibraS.Text = DostawcaS.Text = NIPS.Text = REGONS.Text = PESELS.Text =
                        Address1S.Text = numer.Text = Address2S.Text = "";
                    }
                }
            }
        }

        private string? PobierzIdSymfDlaLibraPoId(string idLibra)
        {
            const string sql = @"SELECT IdSymf FROM [LibraNet].[dbo].[Dostawcy] WHERE ID = @ID;";

            using (var conn = new SqlConnection(_connString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@ID", SqlDbType.VarChar, 50).Value = idLibra.Trim();
                conn.Open();
                var val = cmd.ExecuteScalar();
                if (val == null || val == DBNull.Value) return null;
                return Convert.ToInt32(val).ToString(); // combobox trzyma string
            }
        }

        private void buttonZapisz_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IDLibra.Text) || string.IsNullOrWhiteSpace(IDLibraS.Text))
            {
                MessageBox.Show("Brak wartości w polach IDLibra lub IDLibraS.");
                return;
            }

            if (!int.TryParse(IDLibraS.Text, out int idSymf))
            {
                MessageBox.Show("Pole 'Id Symfonia' musi być liczbą całkowitą.");
                return;
            }

            const string sql = @"
UPDATE [LibraNet].[dbo].[Dostawcy]
SET IdSymf = @IdSymf
WHERE ID = @Id;";

            try
            {
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@IdSymf", SqlDbType.Int).Value = idSymf;
                    // ID w Twojej bazie jest varchar (np. '0-1')
                    cmd.Parameters.Add("@Id", SqlDbType.VarChar, 50).Value = IDLibra.Text.Trim();

                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();

                    if (rows > 1)
                    {
                        string komunikat =
                            $"Powiązano dostawcę z Libry: \"{Dostawca1.Text}\" (ID: {IDLibra.Text})\n" +
                            $"z kontrahentem z Symfonii: \"{DostawcaS.Text}\" (ID: {IDLibraS.Text}).";

                        MessageBox.Show(komunikat, "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Nie znaleziono rekordu o podanym ID.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message);
            }
        }
        private void ClearSymfoniaFields()
        {
            comboBoxDostawcaS.SelectedIndex = -1; // odznacza combobox
            IDLibraS.Text = "";
            DostawcaS.Text = "";
            NIPS.Text = "";
            REGONS.Text = "";
            PESELS.Text = "";
            Address1S.Text = "";
            numer.Text = "";
            Address2S.Text = "";
        }
        private string? PobierzIdSymfDlaLibraPoNazwie(string nazwa)
        {
            const string sql = @"SELECT TOP(1) IdSymf FROM [LibraNet].[dbo].[Dostawcy] WHERE Name = @Name;";
            using (var conn = new SqlConnection(_connString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = nazwa?.Trim() ?? "";
                conn.Open();
                var val = cmd.ExecuteScalar();
                if (val == null || val == DBNull.Value) return null;
                return Convert.ToInt32(val).ToString(); // combobox trzyma string
            }
        }

        #endregion
        // helper: gdy pusto → DBNull
        private static object DbNullIfEmpty(string? s)
            => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();

        // helper: tylko cyfry (do NIP/REGON/PESEL jeśli chcesz trzymać „czyste”)
        private static string Digits(string? s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return Regex.Replace(s, "[^0-9]", "");
        }

        private void buttonZapiszDaneLibra_Click(object sender, EventArgs e)
        {
            // klucz rekordu (w Twojej bazie ID jest VARCHAR, np. '0-1')
            var idLibra = IDLibra.Text?.Trim();
            if (string.IsNullOrWhiteSpace(idLibra))
            {
                MessageBox.Show("Brak wartości w polu Id Libra.");
                return;
            }

            // Przygotuj wartości — zostawiłem „oczyszczanie” cyfrowe dla NIP/REGON/PESEL
            var address1 = Address1.Text;
            var address2 = Address2.Text;
            var nip = Digits(NIP.Text);
            var pesel = Digits(PESEL.Text);
            var regon = Digits(REGON.Text);
            var phone1 = Phone1.Text;
            var phone2 = Phone2.Text;
            var email = Email.Text;
            var animNo = NrGosp.Text;     // Nr Gospodarstwa (AnimNo)
            var irzPlus = IRZPlus.Text;
            var postalCode = PostalCode.Text; // kod pocztowy kurnika (u Ciebie)
            var address = Address.Text;    // adres kurnika
            var city = City.Text;
            var info1 = Info1.Text;
            var info2 = Info2.Text;

            const string sql = @"
UPDATE [LibraNet].[dbo].[Dostawcy]
SET
    Address1   = @Address1,
    Address2   = @Address2,
    NIP        = @NIP,
    PESEL      = @PESEL,
    REGON      = @REGON,
    Phone1     = @Phone1,
    Phone2     = @Phone2,
    Email      = @Email,
    AnimNo     = @AnimNo,
    IRZPlus    = @IRZPlus,
    PostalCode = @PostalCode,
    [Address]  = @Address,
    City       = @City,
    Info1      = @Info1,
    Info2      = @Info2
WHERE ID = @ID;";

            try
            {
                using (var conn = new SqlConnection(_connString)) // <-- connection do LibraNet
                using (var cmd = new SqlCommand(sql, conn))
                {
                    // Parametry NVARCHAR – dobierz długości do swoich kolumn (tu bezpieczne „górki”)
                    cmd.Parameters.Add("@Address1", SqlDbType.NVarChar, 200).Value = DbNullIfEmpty(address1);
                    cmd.Parameters.Add("@Address2", SqlDbType.NVarChar, 200).Value = DbNullIfEmpty(address2);
                    cmd.Parameters.Add("@NIP", SqlDbType.NVarChar, 20).Value = DbNullIfEmpty(nip);
                    cmd.Parameters.Add("@PESEL", SqlDbType.NVarChar, 20).Value = DbNullIfEmpty(pesel);
                    cmd.Parameters.Add("@REGON", SqlDbType.NVarChar, 20).Value = DbNullIfEmpty(regon);
                    cmd.Parameters.Add("@Phone1", SqlDbType.NVarChar, 50).Value = DbNullIfEmpty(phone1);
                    cmd.Parameters.Add("@Phone2", SqlDbType.NVarChar, 50).Value = DbNullIfEmpty(phone2);
                    cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 200).Value = DbNullIfEmpty(email);
                    cmd.Parameters.Add("@AnimNo", SqlDbType.NVarChar, 50).Value = DbNullIfEmpty(animNo);
                    cmd.Parameters.Add("@IRZPlus", SqlDbType.NVarChar, 50).Value = DbNullIfEmpty(irzPlus);
                    cmd.Parameters.Add("@PostalCode", SqlDbType.NVarChar, 20).Value = DbNullIfEmpty(postalCode);
                    cmd.Parameters.Add("@Address", SqlDbType.NVarChar, 200).Value = DbNullIfEmpty(address);
                    cmd.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = DbNullIfEmpty(city);
                    cmd.Parameters.Add("@Info1", SqlDbType.NVarChar, 200).Value = DbNullIfEmpty(info1);
                    cmd.Parameters.Add("@Info2", SqlDbType.NVarChar, 200).Value = DbNullIfEmpty(info2);

                    // klucz (VARCHAR!)
                    cmd.Parameters.Add("@ID", SqlDbType.VarChar, 50).Value = idLibra;

                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();

                    MessageBox.Show(rows > 0 ? "Zaktualizowano dane dostawcy." : "Nie znaleziono rekordu o podanym ID.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd aktualizacji: " + ex.Message);
            }
        }
    }
}
#endregion