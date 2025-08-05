using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class CRM : Form
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private int handlowiecID = 1; // Przykładowe ID, powinno być ustawiane dynamicznie po logowaniu
        private int aktualnyOdbiorcaID = 0;
        private bool isDataLoading = false;

        public string UserID { get; set; }

        public CRM()
        {
            InitializeComponent();
            dataGridViewOdbiorcy.EditMode = DataGridViewEditMode.EditOnEnter;

            // Inicjalizacja filtra statusu
            comboBoxStatusFilter.Items.Add("Wszystkie statusy");
            comboBoxStatusFilter.Items.AddRange(new object[] {
        "Nowy", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt",
        "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)"
    });
            comboBoxStatusFilter.SelectedIndex = 0;

            // Inicjalizacja filtra powiatu
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.SelectedIndex = 0;

            // Podpięcie ZDARZEŃ do kontrolek
            // Oba ComboBoxy wskazują na TĘ SAMĄ metodę filtrującą
            this.comboBoxStatusFilter.SelectedIndexChanged += new System.EventHandler(this.ZastosujFiltry);
            this.comboBoxPowiatFilter.SelectedIndexChanged += new System.EventHandler(this.ZastosujFiltry);

            // Pozostałe zdarzenia DataGridView
            this.dataGridViewOdbiorcy.CurrentCellDirtyStateChanged += new System.EventHandler(this.dataGridViewOdbiorcy_CurrentCellDirtyStateChanged);
            this.dataGridViewOdbiorcy.RowPrePaint += new System.Windows.Forms.DataGridViewRowPrePaintEventHandler(this.dataGridViewOdbiorcy_RowPrePaint);
            this.dataGridViewOdbiorcy.CellEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridViewOdbiorcy_CellEnter);


        }
        // NOWA METODA DO WYPEŁNIANIA FILTRA POWIATÓW
        private void WypelnijFiltrPowiatow()
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            // Używamy LINQ do wybrania unikalnych, niepustych wartości z kolumny "Powiat"
            var powiaty = dt.AsEnumerable()
                            .Select(row => row.Field<string>("Powiat"))
                            .Where(p => !string.IsNullOrEmpty(p))
                            .Distinct()
                            .OrderBy(p => p)
                            .ToArray();

            // Czyścimy stare wartości (oprócz "Wszystkie powiaty") i dodajemy nowe
            comboBoxPowiatFilter.Items.Clear();
            comboBoxPowiatFilter.Items.Add("Wszystkie powiaty");
            comboBoxPowiatFilter.Items.AddRange(powiaty);
            comboBoxPowiatFilter.SelectedIndex = 0; // Ustawiamy domyślną wartość
        }
        // NOWA METODA DO FILTROWANIA
        private void comboBoxStatusFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Pobieramy źródło danych z siatki i rzutujemy je na DataTable
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;

            if (dt == null) return; // Zabezpieczenie, jeśli dane nie są jeszcze załadowane

            // Pobieramy wybrany status z ComboBoxa
            string wybranyStatus = comboBoxStatusFilter.SelectedItem.ToString();

            // Sprawdzamy, czy wybrano opcję "Wszystkie statusy"
            if (wybranyStatus == "Wszystkie statusy")
            {
                // Jeśli tak, czyścimy filtr, aby pokazać wszystkie wiersze
                dt.DefaultView.RowFilter = string.Empty;
            }
            else
            {
                // Jeśli wybrano konkretny status, tworzymy odpowiedni filtr
                // Składnia [NazwaKolumny] = 'Wartość' jest podobna do SQL WHERE
                dt.DefaultView.RowFilter = $"Status = '{wybranyStatus}'";
            }
        }
        // ZASTĄP TĘ METODĘ W SWOIM KODZIE
        private void KonfigurujDataGridView()
        {
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();

            // ... (inne kolumny bez zmian) ...
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", DataPropertyName = "ID", HeaderText = "ID", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nazwa", DataPropertyName = "Nazwa", HeaderText = "Nazwa", Width = 200, ReadOnly = true });

            var statusColumn = new DataGridViewComboBoxColumn
            {
                // POPRAWKA JEST TUTAJ
                Name = "StatusColumn", // Używamy nowej, spójnej nazwy
                DataPropertyName = "Status",
                HeaderText = "Status",
                FlatStyle = FlatStyle.Flat,
                Width = 150
            };
            statusColumn.Items.AddRange("Nowy", "Próba kontaktu", "Nawiązano kontakt", "Zgoda na dalszy kontakt", "Do wysłania oferta", "Nie zainteresowany", "Poprosił o usunięcie", "Błędny rekord (do raportu)");
            dataGridViewOdbiorcy.Columns.Add(statusColumn);

            // ... (reszta kolumn bez zmian) ...
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "KodPocztowy", DataPropertyName = "KodPocztowy", HeaderText = "Kod Pocztowy", ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "MIASTO", DataPropertyName = "MIASTO", HeaderText = "Miasto", ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ulica", DataPropertyName = "Ulica", HeaderText = "Ulica", Width = 150, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Telefon_K", DataPropertyName = "Telefon_K", HeaderText = "Telefon", Width = 100, ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Wojewodztwo", DataPropertyName = "Wojewodztwo", HeaderText = "Województwo", ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Powiat", DataPropertyName = "Powiat", HeaderText = "Powiat", ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Gmina", DataPropertyName = "Gmina", HeaderText = "Gmina", ReadOnly = true });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "DataOstatniejNotatki", DataPropertyName = "DataOstatniejNotatki", HeaderText = "Ost. Notatka", Width = 120, ReadOnly = true });
        }
        private readonly Dictionary<string, List<string>> mapaWojewodztw = new()
{
    { "9991", new List<string> { "Kujawsko-Pomorskie" } },
    { "9998", new List<string> { "Mazowieckie", "Wielkopolskie" } },
    { "871231", new List<string> { "Opolskie", "Śląskie" } },
    { "432143", new List<string> { "Łódzkie", "Świętokrzyskie" } },
};

        private void WczytajOdbiorcow()
        {
            isDataLoading = true;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query;
                SqlCommand cmd;

                if (UserID == "11111")
                {
                    // Użytkownik 11111 widzi wszystkich odbiorców
                    query = @"
            SELECT 
                O.ID, 
                O.Nazwa, 
                ISNULL(O.Status, 'Nowy') AS Status, 
                O.KOD AS KodPocztowy, 
                O.MIASTO, 
                O.Ulica, 
                O.Telefon_K, 
                O.Wojewodztwo, 
                O.Powiat, 
                O.Gmina, 
                MAX(N.DataUtworzenia) AS DataOstatniejNotatki 
            FROM OdbiorcyCRM O 
            LEFT JOIN NotatkiCRM N ON O.ID = N.IDOdbiorcy 
            GROUP BY 
                O.ID, O.Nazwa, O.Status, O.KOD, O.MIASTO, O.Ulica, O.Telefon_K, 
                O.Wojewodztwo, O.Powiat, O.Gmina 
            ORDER BY O.Nazwa";
                    cmd = new SqlCommand(query, conn);
                }
                else if (!string.IsNullOrEmpty(UserID) && mapaWojewodztw.TryGetValue(UserID, out var wojewodztwa))
                {
                    // Filtrowanie po województwach przypisanych do handlowca
                    var likeClauses = wojewodztwa
                        .Select((w, i) => $"LOWER(O.Wojewodztwo) LIKE @Wojewodztwo{i}")
                        .ToList();
                    var whereClause = string.Join(" OR ", likeClauses);

                    query = $@"
            SELECT 
                O.ID, 
                O.Nazwa, 
                ISNULL(O.Status, 'Nowy') AS Status, 
                O.KOD AS KodPocztowy, 
                O.MIASTO, 
                O.Ulica, 
                O.Telefon_K, 
                O.Wojewodztwo, 
                O.Powiat, 
                O.Gmina, 
                MAX(N.DataUtworzenia) AS DataOstatniejNotatki 
            FROM OdbiorcyCRM O 
            LEFT JOIN NotatkiCRM N ON O.ID = N.IDOdbiorcy 
            WHERE {whereClause} 
            GROUP BY 
                O.ID, O.Nazwa, O.Status, O.KOD, O.MIASTO, O.Ulica, O.Telefon_K, 
                O.Wojewodztwo, O.Powiat, O.Gmina 
            ORDER BY O.Nazwa";

                    cmd = new SqlCommand(query, conn);
                    for (int i = 0; i < wojewodztwa.Count; i++)
                    {
                        string likeParam = "%" + wojewodztwa[i].Substring(0, 5).ToLower() + "%";
                        cmd.Parameters.AddWithValue($"@Wojewodztwo{i}", likeParam);
                    }
                }
                else
                {
                    // Brak przypisanego województwa – nie pokazuj nic
                    dataGridViewOdbiorcy.DataSource = null;
                    return;
                }

                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewOdbiorcy.DataSource = dt;
            }
            isDataLoading = false;
            dataGridViewOdbiorcy.Refresh();

            // Po załadowaniu danych wypełnij filtry
            WypelnijFiltrPowiatow();
        }
        // ZASTĄP STARĄ METODĘ FILTRUJĄCĄ TĄ PONIŻEJ
        private void ZastosujFiltry(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridViewOdbiorcy.DataSource;
            if (dt == null) return;

            // Tworzymy listę aktywnych filtrów
            var filtry = new System.Collections.Generic.List<string>();

            // Sprawdzamy filtr statusu
            if (comboBoxStatusFilter.SelectedIndex > 0) // > 0, bo na indeksie 0 jest "Wszystkie statusy"
            {
                string wybranyStatus = comboBoxStatusFilter.SelectedItem.ToString();
                filtry.Add($"Status = '{wybranyStatus}'");
            }

            // Sprawdzamy filtr powiatu
            if (comboBoxPowiatFilter.SelectedIndex > 0) // > 0, bo na indeksie 0 jest "Wszystkie powiaty"
            {
                string wybranyPowiat = comboBoxPowiatFilter.SelectedItem.ToString();
                filtry.Add($"Powiat = '{wybranyPowiat}'");
            }

            // Łączymy wszystkie aktywne filtry za pomocą operatora AND
            dt.DefaultView.RowFilter = string.Join(" AND ", filtry);
        }
        private void AktualizujStatusWBazie(int idOdbiorcy, string nowyStatus)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                cmd.Parameters.AddWithValue("@status", (object)nowyStatus ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private void WczytajNotatki(int idOdbiorcy)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var query = "SELECT Tresc, DataUtworzenia FROM NotatkiCRM WHERE IDOdbiorcy = @id ORDER BY DataUtworzenia DESC";
                var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);
                dataGridViewNotatki.DataSource = dt;
            }
        }

        private void DodajNotatke(int idOdbiorcy, string tresc)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @tresc, @kto)", conn);
                cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                cmd.Parameters.AddWithValue("@tresc", tresc);
                cmd.Parameters.AddWithValue("@kto", handlowiecID);
                cmd.ExecuteNonQuery();
            }
            WczytajNotatki(idOdbiorcy);
            WczytajOdbiorcow();
        }

        private void buttonDodajNotatke_Click(object sender, EventArgs e)
        {
            if (aktualnyOdbiorcaID > 0 && !string.IsNullOrWhiteSpace(textBoxNotatka.Text))
            {
                DodajNotatke(aktualnyOdbiorcaID, textBoxNotatka.Text);
                textBoxNotatka.Clear();
            }
            else
            {
                MessageBox.Show("Wybierz odbiorcę i wpisz treść notatki.");
            }
        }

        // ZASTĄP TĘ METODĘ W SWOIM KODZIE
        private void dataGridViewOdbiorcy_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // Sprawdzamy, czy nazwa kolumny to "StatusColumn" - to jest nasz test
            if (!isDataLoading && e.RowIndex >= 0 && dataGridViewOdbiorcy.Columns[e.ColumnIndex].Name == "StatusColumn")
            {
                // Jeśli ten komunikat się pojawi, to znaczy, że warunek jest spełniony
                MessageBox.Show("Warunek spełniony! Rozpoczynam zapis do bazy.");

                int idOdbiorcy = Convert.ToInt32(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["ID"].Value);
                string nowyStatus = dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["StatusColumn"].Value?.ToString() ?? "";

                AktualizujStatusWBazie(idOdbiorcy, nowyStatus);

                dataGridViewOdbiorcy.InvalidateRow(e.RowIndex);
            }
        }

        // ZASTĄP ISTNIEJĄCĄ METODĘ TĄ PONIŻEJ

        private void dataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridViewOdbiorcy.Rows.Count)
            {
                return;
            }

            DataGridViewRow row = dataGridViewOdbiorcy.Rows[e.RowIndex];

            // POPRAWKA JEST TUTAJ
            string status = row.Cells["StatusColumn"].Value?.ToString() ?? "Nowy";

            Color kolorWiersza;
            switch (status)
            {
                case "Nowy": kolorWiersza = Color.WhiteSmoke; break;
                case "Próba kontaktu": kolorWiersza = Color.LightSkyBlue; break;
                case "Nawiązano kontakt": kolorWiersza = Color.CornflowerBlue; break;
                case "Zgoda na dalszy kontakt": kolorWiersza = Color.LightGreen; break;
                case "Do wysłania oferta": kolorWiersza = Color.LightYellow; break;
                case "Nie zainteresowany": kolorWiersza = Color.MistyRose; break;
                case "Poprosił o usunięcie": kolorWiersza = Color.Salmon; break;
                case "Błędny rekord (do raportu)": kolorWiersza = Color.Orange; break;
                default: kolorWiersza = Color.White; break;
            }

            row.DefaultCellStyle.BackColor = kolorWiersza;
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(kolorWiersza.A, Math.Max(0, kolorWiersza.R - 25), Math.Max(0, kolorWiersza.G - 25), Math.Max(0, kolorWiersza.B - 25));
        }
        private void dataGridViewOdbiorcy_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.IsCurrentCellDirty)
            {
                dataGridViewOdbiorcy.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        // ZASTĄP ISTNIEJĄCĄ METODĘ TĄ PONIŻEJ
        // ZASTĄP ISTNIEJĄCĄ METODĘ TĄ PONIŻEJ
        private void dataGridViewOdbiorcy_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            // Sprawdzenie, czy weszliśmy do prawidłowego wiersza
            if (e.RowIndex >= 0)
            {
                var row = dataGridViewOdbiorcy.Rows[e.RowIndex];

                // --- DODANE ZABEZPIECZENIE ---
                // Sprawdzamy, czy to jest specjalny "nowy wiersz" na końcu tabeli.
                if (row.IsNewRow)
                {
                    // Jeśli tak, to nic nie robimy i po prostu wychodzimy z metody,
                    // aby uniknąć próby odczytania wartości z pustych komórek.
                    return;
                }
                // -----------------------------

                // Ten kod wykona się teraz tylko dla istniejących wierszy z danymi
                int idOdbiorcy = Convert.ToInt32(row.Cells["ID"].Value);
                if (aktualnyOdbiorcaID != idOdbiorcy)
                {
                    WczytajNotatki(idOdbiorcy);
                    aktualnyOdbiorcaID = idOdbiorcy;
                }
            }
        }

        private void WczytajRankingHandlowcow()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var query = @"
WITH WojHandlowcy AS (
    SELECT 
        V.UserID,
        V.NazwaHandlowca,
        O.Status
    FROM OdbiorcyCRM O
    JOIN (
        VALUES 
            ('9991', 'Dawid'),
            ('9998', 'Daniel'),
            ('871231', 'Radek'),
            ('321143', 'Ania')
        -- Dodaj więcej jeśli trzeba
    ) AS V(UserID, NazwaHandlowca)
    ON (
    (V.UserID = '9991' AND LOWER(O.Wojewodztwo) = 'kujawsko-pomorskie') OR
    (V.UserID = '9998' AND (LOWER(O.Wojewodztwo) = 'mazowieckie' OR LOWER(O.Wojewodztwo) = 'wielkopolskie')) OR
    (V.UserID = '871231' AND (LOWER(O.Wojewodztwo) = 'opolskie' OR LOWER(O.Wojewodztwo) = 'śląskie')) OR
    (V.UserID = '432143' AND (LOWER(O.Wojewodztwo) = 'łódzkie' OR LOWER(O.Wojewodztwo) = 'świętokrzyskie'))
)

)

SELECT 
    NazwaHandlowca,
    COUNT(CASE WHEN Status IS NULL OR Status = 'Nowy' THEN 1 END) AS [Nowy],
    COUNT(CASE WHEN Status = 'Próba kontaktu' THEN 1 END) AS [Próba kontaktu],
    COUNT(CASE WHEN Status = 'Nawiązano kontakt' THEN 1 END) AS [Nawiązano kontakt],
    COUNT(CASE WHEN Status = 'Zgoda na dalszy kontakt' THEN 1 END) AS [Zgoda],
    COUNT(CASE WHEN Status = 'Do wysłania oferta' THEN 1 END) AS [Do wysłania],
    COUNT(CASE WHEN Status = 'Nie zainteresowany' THEN 1 END) AS [Nie zainteresowany],
    COUNT(CASE WHEN LOWER(Status) LIKE '%poprosił o usunięcie%' THEN 1 END) AS [Usunąć],
    COUNT(CASE WHEN Status = 'Błędny rekord (do raportu)' THEN 1 END) AS [Błędny]
FROM WojHandlowcy
GROUP BY NazwaHandlowca
ORDER BY NazwaHandlowca";

                var cmd = new SqlCommand(query, conn);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                adapter.Fill(dt);

                dataGridViewRanking.DataSource = dt;
            }
        }

        private void CRM_Load(object sender, EventArgs e)
        {
            // Wywołanie metod konfigurujących i wczytujących dane
            KonfigurujDataGridView();
            WczytajOdbiorcow();
            WczytajRankingHandlowcow();

        }
    }
}