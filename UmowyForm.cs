using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Globalization;

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

        // Statyczny cache - oba SELECT-y są ciężkie (Hodowcy z 'Dane hodowców$', Kontrahenci z Handel z window function).
        // Pozostają w pamięci między otwarciami okna; TTL 30 min wystarczy bo zmiany są rzadkie.
        private static DataTable _kontrahenciCache;
        private static DateTime _kontrahenciCacheTime = DateTime.MinValue;
        private static DataTable _hodowcyCache;
        private static DateTime _hodowcyCacheTime = DateTime.MinValue;
        private const int CACHE_TTL_MIN = 30;

        private readonly Timer _filterTimer = new Timer { Interval = 250 };
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private static ZapytaniaSQL zapytaniasql = new ZapytaniaSQL();

        // Panel "Sugerowane warunki" (#B) - karty z danymi z FarmerCalc (specyfikacja przyjętego żywca)
        private System.Windows.Forms.Label _lblNaglowek;
        private System.Windows.Forms.Button _btnZastosujSugerowane;
        private readonly Timer _historiaDebounce = new Timer { Interval = 300 };
        private string _ostatnioPobranyDostawca = "";

        // 5 kart sugerowanych (Cena usunięta - pokazujemy ją tylko w tabeli specyfikacji)
        private SugCard _cardTypCeny;
        private SugCard _cardDodatek;
        private SugCard _cardUbytek;
        private SugCard _cardCzyjaWaga;
        private SugCard _cardPiK;

        // Tabela "Specyfikacje z dnia DataOdbioru" - wszyscy dostawcy przyjęci tego dnia
        private System.Windows.Forms.Label _lblDzien;
        private DataGridView _dgvDzien;
        private DateTime? _ostatnioPobranaData;

        // Sugerowane wartości (najczęstsze z historii) - wypełniane po wczytaniu danych,
        // używane przez przycisk "Zastosuj sugerowane".
        private string _sugTypCeny;
        private decimal? _sugDodatek;
        private decimal? _sugUbytek;
        private bool? _sugPiK;
        private string _sugCzyjaWaga;   // pochodne od Ubytek: >0 → "Hodowca", =0 → "Ubojnia"

        // Helper - wszystkie kontrolki jednej karty w jednym obiekcie
        private class SugCard
        {
            public Panel Panel;
            public System.Windows.Forms.Label Value;
            public System.Windows.Forms.Label Hint;
            public System.Windows.Forms.Label Freq;   // badge "8/10" w prawym górnym rogu
            public CardKind Kind;                     // jakie pole reprezentuje (do drill-down)
        }

        private enum CardKind { TypCeny, Dodatek, Ubytek, CzyjaWaga, PiK }

        public UmowyForm() : this(null, null) { }

        public UmowyForm(string? initialLp, string? initialIdLibra)
        {
            _initialLp = initialLp;
            _initialIdLibra = initialIdLibra;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

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

            // Panel historii (#B): zbuduj UI + podpiąć debounce
            BudujPanelHistorii();
            // Reorganizacja dolnej części: dgvHodowcy + dgvKontrahenci obok siebie do dołu
            RearrangeBottomGrids();
            _historiaDebounce.Tick += async (s, e) =>
            {
                _historiaDebounce.Stop();
                await WczytajHistorieDostawcyAsync(Dostawca1.Text?.Trim() ?? "");
                _dgvDzien?.Invalidate();  // odśwież highlight wiersza dostawcy
            };
            Dostawca1.TextChanged += (s, e) =>
            {
                _historiaDebounce.Stop();
                _historiaDebounce.Start();
            };

            // Przy zmianie daty odbioru - przeładuj specyfikacje z tego dnia
            dtpData.ValueChanged += async (s, e) =>
            {
                await WczytajSpecyfikacjeZDniaAsync(dtpData.Value);
            };
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
                // Ograniczenie do ostatnich 6 miesięcy - bez tego ComboBox ładuje 10000+ Lp od początku tabeli.
                // Najnowsze pozycje na górze (DESC) - typowo edytujesz świeże umowy.
                const string sqlLp = @"
SELECT DISTINCT Lp FROM dbo.HarmonogramDostaw
WHERE DataOdbioru >= DATEADD(MONTH, -6, GETDATE())
ORDER BY Lp DESC";
                using (var conn = new SqlConnection(_connString))
                using (var cmd = new SqlCommand(sqlLp, conn))
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

                // 5b) Specyfikacje z dnia DataOdbioru
                await WczytajSpecyfikacjeZDniaAsync(dtpData.Value);

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

                // Wymuś odświeżenie tablicy specyfikacji dla DataOdbioru tego LP.
                // Bez tego, jeśli nowy LP ma tę samą DataOdbioru co poprzedni, ValueChanged się nie odpala
                // i tablica nie reaguje na zmianę LP.
                _ostatnioPobranaData = null;
                _ = WczytajSpecyfikacjeZDniaAsync(dtpData.Value);
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

            // Cache: jeśli świeży (<30min) - nie odpytuj 'Dane hodowców$' (ciężka tabela importowana z Excel)
            bool cacheSwiezy = _hodowcyCache != null
                && (DateTime.Now - _hodowcyCacheTime).TotalMinutes < CACHE_TTL_MIN;

            if (cacheSwiezy)
            {
                _hodowcyTable = _hodowcyCache;
                _hodowcyBS.DataSource = _hodowcyTable.DefaultView;
                dataGridViewHodowcy.AutoGenerateColumns = true;
                dataGridViewHodowcy.DataSource = _hodowcyBS;
                return;
            }

            try
            {
                using var conn = new SqlConnection(_connString);
                using var cmd = new SqlCommand(sql, conn);
                await conn.OpenAsync();

                using var rdr = await cmd.ExecuteReaderAsync();
                var dt = new DataTable { CaseSensitive = false };
                dt.Load(rdr);

                _hodowcyTable = dt;
                _hodowcyCache = dt;
                _hodowcyCacheTime = DateTime.Now;
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
            // Cache: jeśli dane są świeże (<30 min) - nie odpytuj Handelu (ciężki query z window function)
            bool cacheSwiezy = _kontrahenciCache != null
                && (DateTime.Now - _kontrahenciCacheTime).TotalMinutes < CACHE_TTL_MIN;

            if (cacheSwiezy)
            {
                _kontrahenciTable = _kontrahenciCache;
                _kontrahenciBS.DataSource = _kontrahenciTable.DefaultView;
                dataGridViewKontrahenci.DataSource = _kontrahenciBS;
                return;
            }

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
                _kontrahenciCache = dt;
                _kontrahenciCacheTime = DateTime.Now;
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
                ["[Dodatek]"] = dodatek.Text,
                ["[Obciążenie]"] = KonfPadl.Text,
                ["[Ubytek]"] = BuildUbytekText(Ubytek.Text),
                ["[Cena]"] = BuildCenaText(typCeny.Text, Cena.Text, dodatek.Text),
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
                    ? " + 0.03 zł/kg dodatku paszowego"
                    : string.Empty;
        }

        private static string BuildCenaText(string? typCeny, string? cenaVal, string? dodatekVal)
        {
            if (string.IsNullOrWhiteSpace(typCeny))
                return string.Empty;

            string normalized = RemovePolishChars(typCeny.Trim()).ToUpperInvariant();
            string prefix = normalized.Length >= 3 ? normalized.Substring(0, 3) : normalized;

            // przygotuj sufiks dodatku, tylko jeśli coś wpisano i > 0
            string dodatekSuffix = string.Empty;
            if (TryParseDecimalFlexible(dodatekVal, out var dodatek) && dodatek > 0)
                dodatekSuffix = " + " + FormatZl(dodatek) + " dodatku uznaniowego";

            switch (prefix)
            {
                case "WOL":
                    // WOLNA cena: masz już kwotę w polu "Cena"
                    // Jeżeli chcesz także tu doliczać dodatek – dopisz "+ dodatekSuffix"
                    return $"to {cenaVal} zł/kg";

                case "ROL":
                    return "jest ustalana na podstawie ceny rolniczej, ogłaszanej na stronie cenyrolnicze.pl"
                           + dodatekSuffix;

                case "MIN":
                    return "jest ustalana na podstawie ceny ministerialnej, ogłaszanej ze strony ministerstwa"
                           + dodatekSuffix;

                case "LAC":
                    return "jest ustalana na podstawie ceny łączonej, czyli połowa ilorazu ceny rolniczej i ceny ministerialnej"
                           + dodatekSuffix;

                default:
                    return string.Empty;
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
        private static bool TryParseDecimalFlexible(string? input, out decimal value)
        {
            // akceptuj przecinek lub kropkę
            var styles = NumberStyles.Number;
            return decimal.TryParse(input, styles, new CultureInfo("pl-PL"), out value)
                || decimal.TryParse(input, styles, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatZl(decimal kwota)
        {
            // bez wymuszonego formatu walutowego, tylko liczba + " zł"
            // 2 miejsca, separator wg PL
            return kwota.ToString("#,0.##", new CultureInfo("pl-PL")) + " zł";
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

                    if (rows > 0)
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

        private void CommandButton_Update_Click_1(object sender, EventArgs e)
        {

        }

        // ==================================================================
        // PANEL "HISTORIA WARUNKÓW TEGO HODOWCY" (#B)
        // ==================================================================

        private void BudujPanelHistorii()
        {
            // groupBox2 przeniesiony na x=1260, więc panel zajmuje teraz miejsce gdzie był groupBox2.
            // Dzięki temu jest widoczny tuż obok groupBox1 (Dane Hodowcy) - mocno w lewo.
            const int x = 640;
            const int width = 600;
            const int yStart = 12;
            const int cardHeight = 64;
            const int cardSpacing = 4;

            // === TABLICA "Specyfikacje z dnia DataOdbioru" - GÓRA (ważniejsza) ===
            _lblDzien = new System.Windows.Forms.Label
            {
                Text = "📅  Specyfikacje z dnia (wybierz datę odbioru)",
                Location = new System.Drawing.Point(x, yStart),
                Size = new System.Drawing.Size(width, 22),
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(44, 62, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(_lblDzien);

            _dgvDzien = new DataGridView
            {
                Location = new System.Drawing.Point(x, yStart + 26),
                Size = new System.Drawing.Size(width, 320),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = true,
                ReadOnly = true,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                BackgroundColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ColumnHeadersHeight = 30,
                RowTemplate = { Height = 24 },
                AlternatingRowsDefaultCellStyle = { BackColor = System.Drawing.Color.FromArgb(248, 249, 250) }
            };
            _dgvDzien.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(38, 50, 56);
            _dgvDzien.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
            _dgvDzien.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
            _dgvDzien.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            _dgvDzien.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 8.75F);
            _dgvDzien.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(197, 225, 165);
            _dgvDzien.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(33, 33, 33);
            _dgvDzien.RowTemplate.Height = 28;
            _dgvDzien.GridColor = System.Drawing.Color.FromArgb(238, 238, 238);

            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "LP", DataPropertyName = "LP", HeaderText = "LP", Width = 35,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dostawca", DataPropertyName = "Dostawca", HeaderText = "Dostawca", Width = 130 });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "TypCeny", DataPropertyName = "TypCeny", HeaderText = "Typ", Width = 75 });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cena", DataPropertyName = "Cena", HeaderText = "Cena", Width = 55,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00",
                    Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold) } });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dodatek", DataPropertyName = "Dodatek", HeaderText = "Dod.", Width = 55,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" } });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ubytek", DataPropertyName = "Ubytek", HeaderText = "Ub%", Width = 45,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.0" } });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "PiK", DataPropertyName = "PiK", HeaderText = "PiK", Width = 40,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "WagaHod", DataPropertyName = "WagaHod", HeaderText = "Hod (kg)", Width = 70,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" } });
            _dgvDzien.Columns.Add(new DataGridViewTextBoxColumn { Name = "WagaUbojnia", DataPropertyName = "WagaUbojnia", HeaderText = "Uboj (kg)", Width = 70,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" } });

            // PiK bool → TAK/NIE
            _dgvDzien.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (_dgvDzien.Columns[e.ColumnIndex].Name != "PiK") return;
                if (e.Value == null || e.Value == DBNull.Value) { e.Value = "—"; e.FormattingApplied = true; return; }
                bool b = Convert.ToBoolean(e.Value);
                e.Value = b ? "TAK" : "NIE";
                e.CellStyle.ForeColor = b ? System.Drawing.Color.FromArgb(46, 125, 50) : System.Drawing.Color.FromArgb(127, 140, 141);
                e.CellStyle.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
                e.FormattingApplied = true;
            };

            // Wyróżnij wiersz bieżącego dostawcy (na żółto)
            _dgvDzien.RowPrePaint += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _dgvDzien.RowCount) return;
                var r = _dgvDzien.Rows[e.RowIndex];
                var d = r.Cells["Dostawca"]?.Value?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(d)
                    && !string.IsNullOrEmpty(Dostawca1?.Text)
                    && string.Equals(d, Dostawca1.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    r.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(255, 245, 157);  // żółty highlight
                    r.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold);
                }
            };

            Controls.Add(_dgvDzien);

            // === SUGEROWANE WARUNKI - układ 2-kolumnowy + colored icon boxes ===
            int yNagl = yStart + 26 + 320 + 18;

            // Pasek nagłówka z ikoną (granatowy gradient look)
            var naglowekTlo = new Panel
            {
                Location = new System.Drawing.Point(x, yNagl),
                Size = new System.Drawing.Size(width, 36),
                BackColor = System.Drawing.Color.FromArgb(38, 50, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Controls.Add(naglowekTlo);

            _lblNaglowek = new System.Windows.Forms.Label
            {
                Text = "💡  SUGEROWANE WARUNKI",
                Location = new System.Drawing.Point(12, 0),
                Size = new System.Drawing.Size(width - 24, 36),
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            naglowekTlo.Controls.Add(_lblNaglowek);

            // 2-kolumnowy układ: 2 cols × 295px + 10px spacing = 600 width
            int cardY = yNagl + 36 + 8;
            int cardWidth = (width - cardSpacing) / 2;     // 298px
            int xCol1 = x;
            int xCol2 = x + cardWidth + cardSpacing;

            // Każda karta ma swój kolor (Material Design palette)
            var colTypCeny    = System.Drawing.Color.FromArgb(63, 81, 181);   // indigo
            var colDodatek    = System.Drawing.Color.FromArgb(76, 175, 80);   // green
            var colUbytek     = System.Drawing.Color.FromArgb(255, 152, 0);   // orange
            var colCzyjaWaga  = System.Drawing.Color.FromArgb(156, 39, 176);  // purple
            var colPiK        = System.Drawing.Color.FromArgb(244, 67, 54);   // red

            // Row 1: TypCeny + Dodatek
            _cardTypCeny    = BudujKarte(xCol1, cardY, cardWidth, cardHeight, "💰", "TYP CENY", colTypCeny);
            _cardTypCeny.Kind = CardKind.TypCeny;
            _cardDodatek    = BudujKarte(xCol2, cardY, cardWidth, cardHeight, "➕", "DODATEK", colDodatek);
            _cardDodatek.Kind = CardKind.Dodatek;
            cardY += cardHeight + cardSpacing;

            // Row 2: Ubytek + CzyjaWaga
            _cardUbytek     = BudujKarte(xCol1, cardY, cardWidth, cardHeight, "📉", "UBYTEK", colUbytek);
            _cardUbytek.Kind = CardKind.Ubytek;
            _cardCzyjaWaga  = BudujKarte(xCol2, cardY, cardWidth, cardHeight, "⚖", "CZYJA WAGA", colCzyjaWaga);
            _cardCzyjaWaga.Kind = CardKind.CzyjaWaga;
            cardY += cardHeight + cardSpacing;

            // Row 3: PiK (full width - bo ma długi tekst "TAK → Sprzedającego")
            _cardPiK        = BudujKarte(xCol1, cardY, width, cardHeight, "🐔", "PiK · PADŁE I KONFISKATY", colPiK);
            _cardPiK.Kind = CardKind.PiK;
            cardY += cardHeight + cardSpacing;

            // Hook click na każdą kartę → drill-down do historycznych dostaw
            foreach (var c in new[] { _cardTypCeny, _cardDodatek, _cardUbytek, _cardCzyjaWaga, _cardPiK })
            {
                var card = c;  // capture for closure
                HookClickRekurencyjnie(card.Panel, async (s, e) => await PokazHistorieKartyAsync(card));
            }

            // Apply button - większy, gradient look
            _btnZastosujSugerowane = new System.Windows.Forms.Button
            {
                Text = "✓   ZASTOSUJ WSZYSTKIE SUGESTIE",
                Location = new System.Drawing.Point(x, cardY + 8),
                Size = new System.Drawing.Size(width, 48),
                Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold),
                BackColor = System.Drawing.Color.FromArgb(76, 175, 80),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _btnZastosujSugerowane.FlatAppearance.BorderSize = 0;
            _btnZastosujSugerowane.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(56, 142, 60);
            _btnZastosujSugerowane.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(46, 125, 50);
            _btnZastosujSugerowane.Click += (s, e) => ZastosujSugerowane();
            Controls.Add(_btnZastosujSugerowane);
        }

        // Wczytaj specyfikacje (FarmerCalc) z konkretnego dnia - wszyscy dostawcy przyjęci tego dnia.
        // Pomocne do porównania warunków - "Co dziś dostali inni hodowcy?".
        private async Task WczytajSpecyfikacjeZDniaAsync(DateTime data)
        {
            // Skip jeśli ta sama data już pobrana
            if (_ostatnioPobranaData.HasValue && _ostatnioPobranaData.Value.Date == data.Date)
            {
                _dgvDzien?.Invalidate();  // odśwież podświetlenie wiersza (bo Dostawca1 mógł się zmienić)
                return;
            }

            _lblDzien.Text = $"📅  Specyfikacje z dnia {data:yyyy-MM-dd} — ładuję...";

            const string sql = @"
SELECT
    fc.CarLp                                          AS LP,
    LTRIM(RTRIM(ISNULL(custReal.ShortName, custReal.Name))) AS Dostawca,
    pt.Name                                            AS TypCeny,
    fc.Price                                           AS Cena,
    fc.Addition                                        AS Dodatek,
    fc.Loss * 100                                      AS Ubytek,
    fc.IncDeadConf                                     AS PiK,
    fc.NettoFarmWeight                                 AS WagaHod,
    fc.NettoWeight                                     AS WagaUbojnia
FROM [LibraNet].[dbo].[FarmerCalc] fc
LEFT JOIN [LibraNet].[dbo].[Dostawcy] custReal
       ON custReal.ID = ISNULL(fc.CustomerRealGID, fc.CustomerGID)
LEFT JOIN [LibraNet].[dbo].[PriceType] pt ON fc.PriceTypeID = pt.ID
WHERE CAST(fc.CalcDate AS DATE) = CAST(@data AS DATE)
ORDER BY fc.CarLp;";

            try
            {
                using var conn = new SqlConnection(_connString);
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@data", data.Date);
                await conn.OpenAsync();
                using var rd = await cmd.ExecuteReaderAsync();
                var dt = new DataTable();
                dt.Load(rd);

                _dgvDzien.DataSource = dt;
                _ostatnioPobranaData = data.Date;

                if (dt.Rows.Count == 0)
                {
                    _lblDzien.Text = $"📭  Brak specyfikacji z dnia {data:yyyy-MM-dd}";
                }
                else
                {
                    // Statystyka dnia: ile dostawców, łączna waga
                    decimal sumaHod = dt.AsEnumerable()
                        .Where(r => r["WagaHod"] != DBNull.Value)
                        .Sum(r => Convert.ToDecimal(r["WagaHod"]));
                    _lblDzien.Text = $"📅  Specyfikacje z dnia {data:yyyy-MM-dd} — {dt.Rows.Count} dostawców, łącznie {sumaHod:N0} kg";
                }
            }
            catch (Exception ex)
            {
                _lblDzien.Text = "❌  Błąd: " + ex.Message;
                _dgvDzien.DataSource = null;
            }
        }

        // Karta: kolorowy icon-box (lewa) + title/value (środek) + freq badge (prawy górny róg)
        private SugCard BudujKarte(int x, int y, int width, int height, string icon, string title, System.Drawing.Color iconColor)
        {
            // Główny panel - białe tło, subtelny border (efekt "kafla")
            var panel = new Panel
            {
                Location = new System.Drawing.Point(x, y),
                Size = new System.Drawing.Size(width, height),
                BackColor = System.Drawing.Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // ICON BOX - kolorowy kwadrat z lewej strony (jak avatar)
            int iconSize = height - 2;
            var iconBox = new Panel
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(iconSize, iconSize),
                BackColor = iconColor
            };
            var lblIcon = new System.Windows.Forms.Label
            {
                Text = icon,
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI Emoji", 18F, System.Drawing.FontStyle.Regular),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Transparent,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            iconBox.Controls.Add(lblIcon);
            panel.Controls.Add(iconBox);

            // TITLE - mała, uppercase, kolor w tonie ikony (subtle)
            var lblTitle = new System.Windows.Forms.Label
            {
                Text = title,
                Location = new System.Drawing.Point(iconSize + 10, 6),
                Size = new System.Drawing.Size(width - iconSize - 70, 16),
                Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold),
                ForeColor = iconColor,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(lblTitle);

            // VALUE - duża, bold, ciemna
            var lblValue = new System.Windows.Forms.Label
            {
                Text = "—",
                Location = new System.Drawing.Point(iconSize + 10, 22),
                Size = new System.Drawing.Size(width - iconSize - 20, 22),
                Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(33, 33, 33),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(lblValue);

            // HINT - drobna, italic, na dole
            var lblHint = new System.Windows.Forms.Label
            {
                Text = "",
                Location = new System.Drawing.Point(iconSize + 10, 44),
                Size = new System.Drawing.Size(width - iconSize - 20, 16),
                Font = new System.Drawing.Font("Segoe UI", 7.5F, System.Drawing.FontStyle.Italic),
                ForeColor = System.Drawing.Color.FromArgb(140, 140, 140),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(lblHint);

            // FREQ BADGE - prawy górny róg, kolorowe tło
            var lblFreq = new System.Windows.Forms.Label
            {
                Text = "",
                Location = new System.Drawing.Point(width - 60, 6),
                Size = new System.Drawing.Size(54, 16),
                Font = new System.Drawing.Font("Segoe UI", 7.5F, System.Drawing.FontStyle.Bold),
                ForeColor = iconColor,
                BackColor = System.Drawing.Color.FromArgb(245, 245, 245),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.None,
                Visible = false
            };
            panel.Controls.Add(lblFreq);
            lblFreq.BringToFront();

            Controls.Add(panel);
            return new SugCard { Panel = panel, Value = lblValue, Hint = lblHint, Freq = lblFreq };
        }

        private async Task WczytajHistorieDostawcyAsync(string dostawca)
        {
            if (string.IsNullOrWhiteSpace(dostawca))
            {
                _lblNaglowek.Text = "💡  Sugerowane warunki - wybierz hodowcę";
                ResetujKarty();
                _btnZastosujSugerowane.Enabled = false;
                return;
            }

            if (string.Equals(dostawca, _ostatnioPobranyDostawca, StringComparison.OrdinalIgnoreCase)) return;

            _lblNaglowek.Text = "💡  Ładuję dane...";

            // Źródło prawdy: dbo.FarmerCalc (specyfikacja przyjętego żywca).
            // Tu są RZECZYWIŚCIE zastosowane warunki - cena/dodatek/ubytek/PiK z faktycznych przyjęć.
            const string sql = @"
SELECT TOP 30
    fc.CalcDate    AS DataOdbioru,
    pt.Name        AS TypCeny,
    fc.Price       AS Cena,
    fc.Addition    AS Dodatek,
    fc.Loss * 100  AS Ubytek,
    fc.IncDeadConf AS PiK
FROM [LibraNet].[dbo].[FarmerCalc] fc
LEFT JOIN [LibraNet].[dbo].[Dostawcy] d
       ON fc.CustomerRealGID = d.ID OR fc.CustomerGID = d.ID
LEFT JOIN [LibraNet].[dbo].[PriceType] pt ON fc.PriceTypeID = pt.ID
WHERE (LTRIM(RTRIM(d.Name)) = @dostawca OR LTRIM(RTRIM(d.ShortName)) = @dostawca)
  AND fc.Price > 0
ORDER BY fc.CalcDate DESC;";

            try
            {
                using var conn = new SqlConnection(_connString);
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@dostawca", dostawca);
                await conn.OpenAsync();
                using var rd = await cmd.ExecuteReaderAsync();
                var dt = new DataTable();
                dt.Load(rd);

                _ostatnioPobranyDostawca = dostawca;

                if (dt.Rows.Count == 0)
                {
                    _lblNaglowek.Text = $"📭  Brak specyfikacji dla \"{dostawca}\"";
                    ResetujKarty();
                    _btnZastosujSugerowane.Enabled = false;
                    _sugTypCeny = null; _sugDodatek = null; _sugUbytek = null; _sugPiK = null; _sugCzyjaWaga = null;
                }
                else
                {
                    _lblNaglowek.Text = $"💡  Sugerowane dla {dostawca} — na podstawie {Math.Min(dt.Rows.Count, 10)} ostatnich przyjęć";
                    _sugTypCeny = null; _sugDodatek = null; _sugUbytek = null; _sugPiK = null; _sugCzyjaWaga = null;
                    ObliczSugerowane(dt);
                }
            }
            catch (Exception ex)
            {
                _lblNaglowek.Text = "❌  Błąd: " + ex.Message;
                ResetujKarty();
                _btnZastosujSugerowane.Enabled = false;
            }
        }

        // Przenosi dgvHodowcy + dgvKontrahenci do TableLayoutPanel (50/50, anchor Top|Bottom|Left|Right).
        // Dzięki temu obie tabele są obok siebie i rosną do dołu okna gdy formularz zostanie powiększony.
        private void RearrangeBottomGrids()
        {
            // Odepnij oryginalne pozycje (były w Form.Controls z InitializeComponent)
            Controls.Remove(label29);
            Controls.Remove(label30);
            Controls.Remove(dataGridViewHodowcy);
            Controls.Remove(dataGridViewKontrahenci);

            // Reset styli labeli - mają być ładne nagłówki nad gridami (a nie pionowe etykiety obok)
            label29.AutoSize = false;
            label29.Dock = DockStyle.Fill;
            label29.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label29.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            label29.Padding = new Padding(8, 0, 0, 0);
            label29.BackColor = System.Drawing.Color.FromArgb(238, 242, 245);
            label29.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);
            label29.Text = "📋  TABELA · DANE HODOWCY";

            label30.AutoSize = false;
            label30.Dock = DockStyle.Fill;
            label30.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            label30.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            label30.Padding = new Padding(8, 0, 0, 0);
            label30.BackColor = System.Drawing.Color.FromArgb(238, 242, 245);
            label30.ForeColor = System.Drawing.Color.FromArgb(44, 62, 80);
            label30.Text = "📋  TABELA · BAZA SYMFONIA";

            // Gridy mają wypełniać komórki TLP
            dataGridViewHodowcy.Dock = DockStyle.Fill;
            dataGridViewKontrahenci.Dock = DockStyle.Fill;

            // TableLayoutPanel: 2 kolumny 50/50 + 2 wiersze (label 24px + grid wypełnia resztę)
            // Anchor wszystkie 4 strony - rośnie razem z formą.
            // Designer ClientSize = 1500x1000. Pozycja y=695, więc do dołu mamy 1000-695-12=293px.
            var tlp = new TableLayoutPanel
            {
                Location = new System.Drawing.Point(12, 695),
                Size = new System.Drawing.Size(1476, 293),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = System.Drawing.Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tlp.ColumnStyles.Clear();
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tlp.RowStyles.Clear();
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Padding w komórkach żeby labels/gridy nie kleiły się do siebie
            tlp.Padding = new Padding(0);

            tlp.Controls.Add(label29, 0, 0);
            tlp.Controls.Add(label30, 1, 0);
            tlp.Controls.Add(dataGridViewHodowcy, 0, 1);
            tlp.Controls.Add(dataGridViewKontrahenci, 1, 1);

            Controls.Add(tlp);
        }

        // Rekurencyjnie podpina handler Click do panelu i wszystkich jego dzieci.
        // Bez tego klik na Label/iconBox nie wywoła handlera ustawionego na samym Panelu.
        private static void HookClickRekurencyjnie(System.Windows.Forms.Control parent, EventHandler handler)
        {
            parent.Click += handler;
            parent.Cursor = Cursors.Hand;
            foreach (System.Windows.Forms.Control c in parent.Controls)
                HookClickRekurencyjnie(c, handler);
        }

        // Klik na kartę → otwórz dialog z listą historycznych dostaw spełniających dany warunek.
        private async System.Threading.Tasks.Task PokazHistorieKartyAsync(SugCard card)
        {
            string dostawca = Dostawca1.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(dostawca))
            {
                MessageBox.Show("Najpierw wybierz hodowcę.", "Brak dostawcy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Dla każdego rodzaju karty: zbuduj warunek SQL + opis nagłówka + co podświetlić
            string warunek = "1=1";
            string opisPola = "";
            string podswietl = "";
            var paramy = new System.Collections.Generic.List<Microsoft.Data.SqlClient.SqlParameter>();

            switch (card.Kind)
            {
                case CardKind.TypCeny:
                    if (string.IsNullOrEmpty(_sugTypCeny)) return;
                    warunek = "LOWER(LTRIM(RTRIM(pt.Name))) = LOWER(@val)";
                    paramy.Add(new Microsoft.Data.SqlClient.SqlParameter("@val", _sugTypCeny));
                    opisPola = "Typ Ceny = " + _sugTypCeny;
                    podswietl = _sugTypCeny;
                    break;

                case CardKind.Dodatek:
                    if (!_sugDodatek.HasValue) return;
                    warunek = "ROUND(ISNULL(fc.Addition, 0), 2) = @val";
                    paramy.Add(new Microsoft.Data.SqlClient.SqlParameter("@val", _sugDodatek.Value));
                    opisPola = "Dodatek = " + _sugDodatek.Value.ToString("0.00") + " zł";
                    podswietl = _sugDodatek.Value.ToString("0.00");
                    break;

                case CardKind.Ubytek:
                    if (!_sugUbytek.HasValue) return;
                    warunek = "ROUND(fc.Loss * 100, 1) = @val";
                    paramy.Add(new Microsoft.Data.SqlClient.SqlParameter("@val", _sugUbytek.Value));
                    opisPola = "Ubytek = " + _sugUbytek.Value.ToString("0.0") + "%";
                    podswietl = _sugUbytek.Value.ToString("0.0");
                    break;

                case CardKind.CzyjaWaga:
                    if (string.IsNullOrEmpty(_sugCzyjaWaga)) return;
                    // Hodowca: Ubytek > 0  /  Ubojnia: Ubytek = 0
                    warunek = _sugCzyjaWaga == "Hodowca" ? "fc.Loss > 0" : "ISNULL(fc.Loss, 0) = 0";
                    opisPola = "Waga loco " + _sugCzyjaWaga;
                    break;

                case CardKind.PiK:
                    if (!_sugPiK.HasValue) return;
                    warunek = "ISNULL(fc.IncDeadConf, 0) = @val";
                    paramy.Add(new Microsoft.Data.SqlClient.SqlParameter("@val", _sugPiK.Value));
                    opisPola = "PiK = " + (_sugPiK.Value ? "TAK" : "NIE");
                    podswietl = _sugPiK.Value ? "TAK" : "NIE";
                    break;
            }

            try
            {
                var dt = await PobierzHistorieZWarunkiemAsync(dostawca, warunek, paramy);
                using var dlg = new HistoriaWartosciDialog(opisPola, dostawca, podswietl, dt);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Pobierz historyczne dostawy z FarmerCalc dla danego dostawcy z dodatkowym warunkiem (np. Loss>0).
        private async System.Threading.Tasks.Task<DataTable> PobierzHistorieZWarunkiemAsync(
            string dostawca, string warunek, System.Collections.Generic.List<Microsoft.Data.SqlClient.SqlParameter> paramy)
        {
            string sql = @"
SELECT TOP 50
    fc.CalcDate            AS Data,
    pt.Name                AS TypCeny,
    fc.Price               AS Cena,
    fc.Addition            AS Dodatek,
    fc.Loss * 100          AS Ubytek,
    fc.IncDeadConf         AS PiK,
    fc.NettoFarmWeight     AS WagaHod,
    fc.NettoWeight         AS WagaUbojnia
FROM [LibraNet].[dbo].[FarmerCalc] fc
LEFT JOIN [LibraNet].[dbo].[Dostawcy] d
       ON d.ID = ISNULL(fc.CustomerRealGID, fc.CustomerGID)
LEFT JOIN [LibraNet].[dbo].[PriceType] pt ON fc.PriceTypeID = pt.ID
WHERE (LTRIM(RTRIM(d.Name)) = @dostawca OR LTRIM(RTRIM(d.ShortName)) = @dostawca)
  AND fc.Price > 0
  AND (" + warunek + @")
ORDER BY fc.CalcDate DESC;";

            using var conn = new Microsoft.Data.SqlClient.SqlConnection(_connString);
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dostawca", dostawca);
            foreach (var p in paramy) cmd.Parameters.Add(p);

            await conn.OpenAsync();
            using var rd = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(rd);
            return dt;
        }

        private void ResetujKarty()
        {
            foreach (var c in new[] { _cardTypCeny, _cardDodatek, _cardUbytek, _cardCzyjaWaga, _cardPiK })
            {
                if (c == null) continue;
                c.Value.Text = "—";
                c.Hint.Text = "";
                if (c.Freq != null) c.Freq.Visible = false;
            }
        }

        // Oblicz NAJCZĘSTSZE wartości dla każdego pola - to są domyślne warunki tego hodowcy.
        // Używamy tylko ostatnich 10 dostaw (mniej "starości"), priorytet dla aktualnych warunków.
        private void ObliczSugerowane(DataTable dt)
        {
            var ostatnie = dt.AsEnumerable().Take(10).ToList();
            if (ostatnie.Count == 0)
            {
                _btnZastosujSugerowane.Enabled = false;
                return;
            }
            int total = ostatnie.Count;

            // ─── TypCeny ─────────────────────────────────────────
            var typG = ostatnie
                .Select(r => r["TypCeny"]?.ToString()?.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            _sugTypCeny = typG?.Key;
            UstawKarte(_cardTypCeny,
                _sugTypCeny != null ? char.ToUpper(_sugTypCeny[0]) + _sugTypCeny.Substring(1) : "—",
                typG != null ? $"najczęstszy typ ceny dla tego hodowcy" : "brak danych",
                typG != null ? $"{typG.Count()}/{total}" : "");

            // ─── Dodatek ─────────────────────────────────────────
            var dodAll = ostatnie
                .Where(r => r["Dodatek"] != DBNull.Value)
                .Select(r => Math.Round(Convert.ToDecimal(r["Dodatek"]), 2))
                .ToList();
            var dodG = dodAll
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            _sugDodatek = dodG?.Key;
            string dodHint;
            if (dodAll.Count == 0)
                dodHint = "brak danych";
            else if (_sugDodatek == 0)
                dodHint = "zazwyczaj bez dodatku";
            else
                dodHint = $"zakres: {dodAll.Min():0.00}–{dodAll.Max():0.00} zł";
            UstawKarte(_cardDodatek,
                _sugDodatek.HasValue ? $"{_sugDodatek:0.00} zł" : "—",
                dodHint,
                dodG != null ? $"{dodG.Count()}/{total}" : "");

            // ─── Ubytek ──────────────────────────────────────────
            var ubAll = ostatnie
                .Where(r => r["Ubytek"] != DBNull.Value)
                .Select(r => Math.Round(Convert.ToDecimal(r["Ubytek"]), 1))
                .ToList();
            var ubG = ubAll
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            _sugUbytek = ubG?.Key;
            string ubHint = ubAll.Count > 0
                ? $"zakres: {ubAll.Min():0.0}–{ubAll.Max():0.0} %"
                : "brak danych";
            UstawKarte(_cardUbytek,
                _sugUbytek.HasValue ? $"{_sugUbytek:0.0} %" : "—",
                ubHint,
                ubG != null ? $"{ubG.Count()}/{total}" : "");

            // ─── CzyjaWaga (pochodna od Ubytek) ─────────────────
            // Zasada z PDF specyfikacji (WidokSpecyfikacje.xaml.cs:6524):
            //   Ubytek > 0  → "Waga loco Hodowca" (waży u hodowcy, ubytek transportowy)
            //   Ubytek == 0 → "Waga loco Ubojnia" (waży dopiero ubojnia)
            if (_sugUbytek.HasValue)
            {
                _sugCzyjaWaga = _sugUbytek.Value > 0 ? "Hodowca" : "Ubojnia";
                string opis = _sugUbytek.Value > 0
                    ? $"Ubytek > 0 → loco hodowca"
                    : $"Ubytek = 0 → loco ubojnia";
                UstawKarte(_cardCzyjaWaga, _sugCzyjaWaga.ToUpper(), opis, "");
            }
            else
            {
                _sugCzyjaWaga = null;
                UstawKarte(_cardCzyjaWaga, "—", "brak danych", "");
            }

            // ─── PiK ─────────────────────────────────────────────
            // IncDeadConf=TRUE → padłe wliczone w wagę → Odbiorcę obciążony
            // IncDeadConf=FALSE → padłe niewliczone → Sprzedającego obciążony
            var pikG = ostatnie
                .Where(r => r["PiK"] != DBNull.Value)
                .Select(r => Convert.ToBoolean(r["PiK"]))
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count()).FirstOrDefault();
            _sugPiK = pikG?.Key;
            if (_sugPiK.HasValue)
            {
                string val = _sugPiK.Value ? "TAK" : "NIE";
                string kogoObciazyc = _sugPiK.Value ? "Odbiorcę" : "Sprzedającego";
                UstawKarte(_cardPiK,
                    $"{val}   →   obciążenie: {kogoObciazyc}",
                    "wliczanie padłych i konfiskat do wagi",
                    $"{pikG.Count()}/{total}");
            }
            else
            {
                UstawKarte(_cardPiK, "—", "brak danych", "");
            }

            _btnZastosujSugerowane.Enabled = true;
        }

        private void UstawKarte(SugCard card, string value, string hint, string freq)
        {
            if (card == null) return;
            card.Value.Text = value;
            card.Hint.Text = hint;
            if (card.Freq != null)
            {
                if (string.IsNullOrEmpty(freq))
                {
                    card.Freq.Visible = false;
                }
                else
                {
                    card.Freq.Text = freq;
                    card.Freq.Visible = true;
                }
            }
        }

        // Wypełnij pola formularza sugerowanymi wartościami (po kliknięciu przycisku).
        private void ZastosujSugerowane()
        {
            int zmieniono = 0;

            if (!string.IsNullOrEmpty(_sugTypCeny))
            {
                // Case-insensitive match w ComboBox typCeny ("wolnorynkowa" w bazie ↔ "Wolnorynkowa" w combo)
                for (int i = 0; i < typCeny.Items.Count; i++)
                {
                    if (string.Equals(typCeny.Items[i]?.ToString(), _sugTypCeny, StringComparison.OrdinalIgnoreCase))
                    {
                        typCeny.SelectedIndex = i;
                        zmieniono++;
                        break;
                    }
                }
            }

            if (_sugDodatek.HasValue)
            {
                dodatek.Text = _sugDodatek.Value.ToString("0.00", CultureInfo.GetCultureInfo("pl-PL"));
                zmieniono++;
            }

            if (_sugUbytek.HasValue)
            {
                Ubytek.Text = _sugUbytek.Value.ToString("0.0", CultureInfo.GetCultureInfo("pl-PL"));
                zmieniono++;
            }

            if (_sugPiK.HasValue)
            {
                // PiK=TRUE → "Odbiorcę"; PiK=FALSE → "Sprzedającego"
                string konfText = _sugPiK.Value ? "Odbiorcę" : "Sprzedającego";
                for (int i = 0; i < KonfPadl.Items.Count; i++)
                {
                    if (string.Equals(KonfPadl.Items[i]?.ToString(), konfText, StringComparison.OrdinalIgnoreCase))
                    {
                        KonfPadl.SelectedIndex = i;
                        zmieniono++;
                        break;
                    }
                }
            }

            // CzyjaWaga - z reguły o ubytku (Ubytek > 0 → "Hodowca", = 0 → "Ubojnia")
            if (!string.IsNullOrEmpty(_sugCzyjaWaga))
            {
                for (int i = 0; i < CzyjaWaga.Items.Count; i++)
                {
                    if (string.Equals(CzyjaWaga.Items[i]?.ToString(), _sugCzyjaWaga, StringComparison.OrdinalIgnoreCase))
                    {
                        CzyjaWaga.SelectedIndex = i;
                        zmieniono++;
                        break;
                    }
                }
            }

            MessageBox.Show(
                $"Zastosowano {zmieniono} sugerowanych wartości.\n\nSprawdź pola formularza i koryguj jeśli trzeba.",
                "Sugerowane warunki", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _historiaDebounce?.Stop();
            _historiaDebounce?.Dispose();
            _filterTimer?.Stop();
            _filterTimer?.Dispose();
            base.OnFormClosed(e);
        }

        private void comboBoxDostawcaS_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
#endregion