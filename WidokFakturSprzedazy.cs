using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Kalendarz1
{
    public partial class WidokFakturSprzedazy : Form
    {
        private string connectionString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private bool isDataLoading = true;

        private Chart chartSprzedaz;
        private Chart chartTop10;
        private Chart chartHandlowcy;
        private Chart chartAnalizaCen;

        public string UserID { get; set; }

        private readonly Dictionary<string, string> mapaHandlowcow = new Dictionary<string, string>
{
    { "9991", "Dawid" },
    { "9998", "Daniel" },
    { "871231", "Radek" },
    { "432143", "Ania" }
};

        private string? _docelowyHandlowiec;
        private Button btnWybierzHandlowcow;
        private ComboBox comboBoxRokUdzial;
        private ComboBox comboBoxMiesiacUdzial;
        private ComboBox comboBoxRokSprzedaz;
        private ComboBox comboBoxMiesiacSprzedaz;
        private ComboBox comboBoxRokTop10;
        private ComboBox comboBoxMiesiacTop10;
        private ComboBox comboBoxTowarTop10;

        private RadioButton radioTowarSwieze;
        private RadioButton radioTowarMrozone;
        private ComboBox comboBoxTowarSwieze;
        private ComboBox comboBoxTowarMrozone;
        private int? wybranyTowarSwiezyId = null;
        private int? wybranyTowarMrozonyId = null;

        private DataGridView dataGridViewAnalizaCen;
        private ComboBox comboBoxTowarAnalizaCen;
        private DateTimePicker dateTimePickerAnalizaOd;
        private DateTimePicker dateTimePickerAnalizaDo;
        private Label lblTrendInfo;

        private DataGridView dataGridViewPorownaniaSwiezeMrozone;
        private Label lblStatystykiPorown;

        private List<string> zaznaczeniHandlowcy = new List<string>();

        public WidokFakturSprzedazy()
        {
            InitializeComponent();
            UserID = "11111";
            ApplyModernTheme();
            this.Resize += WidokFakturSprzedazy_Resize;
        }

        private void ApplyModernTheme()
        {
            this.BackColor = ColorTranslator.FromHtml("#f5f7fa");
            this.Font = new Font("Segoe UI", 9F);
        }

        private void WidokFakturSprzedazy_Resize(object? sender, EventArgs e)
        {
            AktualizujRozmiary();
        }

        private void AktualizujRozmiary()
        {
            float baseSize = Math.Min(this.ClientSize.Width / 1600f, this.ClientSize.Height / 900f);
            float fontSize = Math.Max(8f, 9f * baseSize);

            this.Font = new Font("Segoe UI", fontSize);

            foreach (Control ctrl in panelFilters.Controls)
            {
                if (ctrl is Label || ctrl is Button)
                    ctrl.Font = new Font("Segoe UI", fontSize, ctrl is Button ? FontStyle.Bold : FontStyle.Regular);
                else if (ctrl is ComboBox || ctrl is DateTimePicker || ctrl is RadioButton)
                    ctrl.Font = new Font("Segoe UI", fontSize);
            }

            if (dataGridViewOdbiorcy != null)
            {
                dataGridViewOdbiorcy.DefaultCellStyle.Font = new Font("Segoe UI", fontSize);
                dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            }

            if (dataGridViewPozycje != null)
            {
                dataGridViewPozycje.DefaultCellStyle.Font = new Font("Segoe UI", fontSize);
                dataGridViewPozycje.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            }

            if (dataGridViewPlatnosci != null)
            {
                dataGridViewPlatnosci.DefaultCellStyle.Font = new Font("Segoe UI", fontSize);
                dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            }

            if (dataGridViewAnalizaCen != null)
            {
                dataGridViewAnalizaCen.DefaultCellStyle.Font = new Font("Segoe UI", fontSize);
                dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            }
        }

        private void WidokFakturSprzedazy_Load(object? sender, EventArgs e)
        {
            if (UserID == "11111")
            {
                _docelowyHandlowiec = null;
                this.Text = "📊 System Zarządzania Fakturami Sprzedaży - [ADMINISTRATOR]";
            }
            else if (mapaHandlowcow.ContainsKey(UserID))
            {
                _docelowyHandlowiec = mapaHandlowcow[UserID];
                this.Text = $"📊 System Zarządzania Fakturami Sprzedaży - [{_docelowyHandlowiec}]";
            }
            else
            {
                MessageBox.Show("⚠ Nieznany lub nieprawidłowy identyfikator użytkownika.", "Błąd logowania", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _docelowyHandlowiec = "____BRAK_UPRAWNIEN____";
            }

            dateTimePickerDo.Value = DateTime.Today;
            dateTimePickerOd.Value = DateTime.Today.AddMonths(-3);

            KonfigurujDataGridViewDokumenty();
            KonfigurujDataGridViewPozycje();
            KonfigurujDataGridViewPlatnosci();

            KonfigurujMenuKontekstowe();

            StworzZakladkiAnalityczne();

            // INICJALIZUJ FILTRY HANDLOWCÓW PRZED WCZYTANIEM DANYCH
            InicjalizujFiltryHandlowcow();

            WczytajPlatnosciPerKontrahent(null);
            StworzKontroleTowary();
            ZaladujKontrahentow();

            StworzPrzyciskWyboruHandlowcow();

            dataGridViewOdbiorcy.RowHeadersVisible = false;
            dataGridViewPozycje.RowHeadersVisible = false;
            dataGridViewPlatnosci.RowHeadersVisible = false;

            StylizujPrzyciski();
            StylizujKomboBoxes();
            StylizujDataGridViews();

            isDataLoading = false;
            OdswiezDaneGlownejSiatki();
            AktualizujRozmiary();
        }
        private void InicjalizujFiltryHandlowcow()
        {
            zaznaczeniHandlowcy = new List<string>();

            if (UserID == "11111")
            {
                // Administrator - domyślnie wszyscy handlowcy zaznaczeni
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        string query = @"
                    SELECT DISTINCT WYM.CDim_Handlowiec_Val
                    FROM [HANDEL].[SSCommon].[ContractorClassification] WYM
                    WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
                    ORDER BY WYM.CDim_Handlowiec_Val";

                        var cmd = new SqlCommand(query, conn);
                        conn.Open();
                        var reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            zaznaczeniHandlowcy.Add(reader.GetString(0));
                        }
                    }
                }
                catch { }
            }
            else if (mapaHandlowcow.ContainsKey(UserID))
            {
                // Zwykły użytkownik - tylko jego własny handlowiec
                zaznaczeniHandlowcy.Add(mapaHandlowcow[UserID]);
            }
        }
        private void StworzPrzyciskWyboruHandlowcow()
        {
            Label lblHandlowiec = new Label
            {
                Text = "👥 Handlowcy:",
                Location = new Point(675, 15),
                AutoSize = true
            };
            panelFilters.Controls.Add(lblHandlowiec);

            btnWybierzHandlowcow = new Button
            {
                Text = "✓ Wybierz...",
                Location = new Point(760, 12),
                Size = new Size(110, 25),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnWybierzHandlowcow.FlatAppearance.BorderSize = 0;
            btnWybierzHandlowcow.Click += BtnWybierzHandlowcow_Click;

            panelFilters.Controls.Add(btnWybierzHandlowcow);

            label3.Location = new Point(890, 15);
            dateTimePickerOd.Location = new Point(920, 12);

            label4.Location = new Point(1040, 15);
            dateTimePickerDo.Location = new Point(1070, 12);

            btnRefresh.Location = new Point(1190, 11);
        }
        private void BtnWybierzHandlowcow_Click(object? sender, EventArgs e)
        {
            List<string> dozwoleniHandlowcy = new List<string>();

            if (UserID == "11111")
            {
                dozwoleniHandlowcy = null;
            }
            else if (mapaHandlowcow.ContainsKey(UserID))
            {
                dozwoleniHandlowcy = new List<string> { mapaHandlowcow[UserID] };
            }
            else
            {
                MessageBox.Show("⚠ Brak uprawnień do wyboru handlowców.", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var form = new FormWyborHandlowcow(connectionString, zaznaczeniHandlowcy, dozwoleniHandlowcy))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    zaznaczeniHandlowcy = form.WybraniHandlowcy;

                    // Zaktualizuj tekst przycisku
                    if (zaznaczeniHandlowcy.Count == 0)
                        btnWybierzHandlowcow.Text = "✓ Wybierz...";
                    else if (zaznaczeniHandlowcy.Count == 1)
                        btnWybierzHandlowcow.Text = $"✓ {zaznaczeniHandlowcy[0]}";
                    else
                        btnWybierzHandlowcow.Text = $"✓ Wybrano ({zaznaczeniHandlowcy.Count})";

                    OdswiezDaneGlownejSiatki();
                    WczytajPlatnosciPerKontrahent(null);
                    ZaladujKontrahentow();

                    if (tabControl.SelectedIndex == 0)
                        WczytajPlatnosciPerKontrahent(null);
                    else if (tabControl.SelectedIndex == 1)
                        OdswiezWykresSprzedazy();
                    else if (tabControl.SelectedIndex == 2)
                        OdswiezWykresTop10();
                    else if (tabControl.SelectedIndex == 3)
                        OdswiezWykresHandlowcow();
                    else if (tabControl.SelectedIndex == 4)
                        OdswiezAnalizeCen();
                }
            }
        }
        private List<string> PobierzZaznaczonychHandlowcow()
        {
            return zaznaczeniHandlowcy;
        }

        private void StworzKontroleTowary()
        {
            radioTowarSwieze = new RadioButton
            {
                Text = "🥬 Świeże",
                Location = new Point(12, 15),
                AutoSize = true,
                Checked = true
            };
            radioTowarSwieze.CheckedChanged += RadioTowar_CheckedChanged;
            panelFilters.Controls.Add(radioTowarSwieze);

            radioTowarMrozone = new RadioButton
            {
                Text = "❄ Mrożone",
                Location = new Point(100, 15),
                AutoSize = true
            };
            radioTowarMrozone.CheckedChanged += RadioTowar_CheckedChanged;
            panelFilters.Controls.Add(radioTowarMrozone);

            comboBoxTowarSwieze = new ComboBox
            {
                Location = new Point(190, 12),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = true
            };
            comboBoxTowarSwieze.SelectedIndexChanged += ComboBoxTowar_SelectedIndexChanged;
            panelFilters.Controls.Add(comboBoxTowarSwieze);

            comboBoxTowarMrozone = new ComboBox
            {
                Location = new Point(190, 12),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            comboBoxTowarMrozone.SelectedIndexChanged += ComboBoxTowar_SelectedIndexChanged;
            panelFilters.Controls.Add(comboBoxTowarMrozone);

            // TO BYŁO POMINIĘTE - DODAJ:
            ZaladujTowary();

            label2.Location = new Point(410, 15);
            label2.Text = "🏢 Kontrahent:";
            comboBoxKontrahent.Location = new Point(505, 12);
            comboBoxKontrahent.Size = new Size(150, 23);
        }
        private void RadioTowar_CheckedChanged(object? sender, EventArgs e)
        {
            if (isDataLoading) return;

            if (radioTowarSwieze.Checked)
            {
                if (comboBoxTowarMrozone.SelectedValue != null)
                    wybranyTowarMrozonyId = (int)comboBoxTowarMrozone.SelectedValue;

                comboBoxTowarSwieze.Visible = true;
                comboBoxTowarMrozone.Visible = false;

                if (wybranyTowarSwiezyId.HasValue && wybranyTowarSwiezyId > 0)
                    comboBoxTowarSwieze.SelectedValue = wybranyTowarSwiezyId;
            }
            else
            {
                if (comboBoxTowarSwieze.SelectedValue != null)
                    wybranyTowarSwiezyId = (int)comboBoxTowarSwieze.SelectedValue;

                comboBoxTowarSwieze.Visible = false;
                comboBoxTowarMrozone.Visible = true;

                if (wybranyTowarMrozonyId.HasValue && wybranyTowarMrozonyId > 0)
                    comboBoxTowarMrozone.SelectedValue = wybranyTowarMrozonyId;
            }

            OdswiezDaneGlownejSiatki();
        }

        private void ComboBoxTowar_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (isDataLoading) return;

            if (radioTowarSwieze.Checked && comboBoxTowarSwieze.SelectedValue != null)
                wybranyTowarSwiezyId = (int)comboBoxTowarSwieze.SelectedValue;
            else if (radioTowarMrozone.Checked && comboBoxTowarMrozone.SelectedValue != null)
                wybranyTowarMrozonyId = (int)comboBoxTowarMrozone.SelectedValue;

            OdswiezDaneGlownejSiatki();
        }

        private void StworzZakladkiAnalityczne()
        {
            TabPage tabPlatnosci = new TabPage("💳 Płatności");
            dataGridViewPlatnosci.Dock = DockStyle.Fill;
            dataGridViewPlatnosci.Parent = tabPlatnosci;

            TabPage tabWykres = new TabPage("📈 Sprzedaż miesięczna");
            Panel panelSprzedaz = new Panel { Dock = DockStyle.Fill };
            tabWykres.Controls.Add(panelSprzedaz);

            Panel panelSprzedazControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            Label lblRokSprzedaz = new Label { Text = "📅 Rok:", Location = new Point(10, 12), AutoSize = true };
            comboBoxRokSprzedaz = new ComboBox
            {
                Location = new Point(60, 10),
                Size = new Size(80, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijLataComboBox(comboBoxRokSprzedaz);
            comboBoxRokSprzedaz.SelectedIndexChanged += (s, e) => OdswiezWykresSprzedazy();

            Label lblMiesiacSprzedaz = new Label { Text = "📆 Miesiąc:", Location = new Point(160, 12), AutoSize = true };
            comboBoxMiesiacSprzedaz = new ComboBox
            {
                Location = new Point(230, 10),
                Size = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijMiesiaceComboBox(comboBoxMiesiacSprzedaz);
            comboBoxMiesiacSprzedaz.SelectedIndexChanged += (s, e) => OdswiezWykresSprzedazy();

            panelSprzedazControls.Controls.Add(lblRokSprzedaz);
            panelSprzedazControls.Controls.Add(comboBoxRokSprzedaz);
            panelSprzedazControls.Controls.Add(lblMiesiacSprzedaz);
            panelSprzedazControls.Controls.Add(comboBoxMiesiacSprzedaz);

            Panel panelSumaSprzedaz = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Name = "panelSumaSprzedaz"
            };

            Label lblSumaSprzedaz = new Label
            {
                Dock = DockStyle.Fill,
                Text = "",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2c3e50"),
                TextAlign = ContentAlignment.MiddleCenter,
                Name = "lblSumaSprzedaz"
            };
            panelSumaSprzedaz.Controls.Add(lblSumaSprzedaz);

            panelSprzedaz.Controls.Add(panelSprzedazControls);
            panelSprzedaz.Controls.Add(panelSumaSprzedaz);

            chartSprzedaz = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Parent = panelSprzedaz
            };
            KonfigurujWykresSprzedazy();

            TabPage tabTop10 = new TabPage("🏆 Top 10 - odbiorcy");
            Panel panelTop10 = new Panel { Dock = DockStyle.Fill };
            tabTop10.Controls.Add(panelTop10);

            Panel panelTop10Controls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            Label lblRokTop10 = new Label { Text = "📅 Rok:", Location = new Point(10, 12), AutoSize = true };
            comboBoxRokTop10 = new ComboBox
            {
                Location = new Point(60, 10),
                Size = new Size(80, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijLataComboBox(comboBoxRokTop10);
            comboBoxRokTop10.SelectedIndexChanged += (s, e) => OdswiezWykresTop10();

            Label lblMiesiacTop10 = new Label { Text = "📆 Miesiąc:", Location = new Point(160, 12), AutoSize = true };
            comboBoxMiesiacTop10 = new ComboBox
            {
                Location = new Point(230, 10),
                Size = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijMiesiaceComboBox(comboBoxMiesiacTop10);
            comboBoxMiesiacTop10.SelectedIndexChanged += (s, e) => OdswiezWykresTop10();

            Label lblTowarTop10 = new Label { Text = "📦 Towar:", Location = new Point(370, 12), AutoSize = true };
            comboBoxTowarTop10 = new ComboBox
            {
                Location = new Point(430, 10),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijTowaryComboBoxTop10(comboBoxTowarTop10);
            comboBoxTowarTop10.SelectedIndexChanged += (s, e) => OdswiezWykresTop10();

            panelTop10Controls.Controls.Add(lblRokTop10);
            panelTop10Controls.Controls.Add(comboBoxRokTop10);
            panelTop10Controls.Controls.Add(lblMiesiacTop10);
            panelTop10Controls.Controls.Add(comboBoxMiesiacTop10);
            panelTop10Controls.Controls.Add(lblTowarTop10);
            panelTop10Controls.Controls.Add(comboBoxTowarTop10);

            panelTop10.Controls.Add(panelTop10Controls);

            chartTop10 = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Parent = panelTop10
            };
            KonfigurujWykresTop10();

            TabPage tabHandlowcy = new TabPage("👥 Udział handlowców");
            Panel panelHandlowcy = new Panel { Dock = DockStyle.Fill };
            tabHandlowcy.Controls.Add(panelHandlowcy);

            Panel panelHandlowcyControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            Label lblRokUdzial = new Label { Text = "📅 Rok:", Location = new Point(10, 12), AutoSize = true };
            comboBoxRokUdzial = new ComboBox
            {
                Location = new Point(60, 10),
                Size = new Size(80, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijLataComboBox(comboBoxRokUdzial);
            comboBoxRokUdzial.SelectedIndexChanged += (s, e) => OdswiezWykresHandlowcow();

            Label lblMiesiacUdzial = new Label { Text = "📆 Miesiąc:", Location = new Point(160, 12), AutoSize = true };
            comboBoxMiesiacUdzial = new ComboBox
            {
                Location = new Point(230, 10),
                Size = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijMiesiaceComboBox(comboBoxMiesiacUdzial);
            comboBoxMiesiacUdzial.SelectedIndexChanged += (s, e) => OdswiezWykresHandlowcow();

            panelHandlowcyControls.Controls.Add(lblRokUdzial);
            panelHandlowcyControls.Controls.Add(comboBoxRokUdzial);
            panelHandlowcyControls.Controls.Add(lblMiesiacUdzial);
            panelHandlowcyControls.Controls.Add(comboBoxMiesiacUdzial);

            panelHandlowcy.Controls.Add(panelHandlowcyControls);

            chartHandlowcy = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Parent = panelHandlowcy
            };
            KonfigurujWykresHandlowcow();

            TabPage tabAnalizaCen = new TabPage("💰 Analiza cen");
            StworzZakladkeAnalizyCen(tabAnalizaCen);

            TabPage tabPorownanieSwiezeMrozone = new TabPage("⚖ Świeże vs Mrożone");
            StworzZakladkePorownaniaSwiezeMrozone(tabPorownanieSwiezeMrozone);

            tabControl.TabPages.Add(tabPlatnosci);
            tabControl.TabPages.Add(tabWykres);
            tabControl.TabPages.Add(tabTop10);
            tabControl.TabPages.Add(tabHandlowcy);
            tabControl.TabPages.Add(tabAnalizaCen);
            tabControl.TabPages.Add(tabPorownanieSwiezeMrozone);

            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            // Ustaw wartości domyślne
            if (comboBoxRokSprzedaz != null && comboBoxRokSprzedaz.Items.Count > 0)
                comboBoxRokSprzedaz.SelectedItem = DateTime.Now.Year;

            if (comboBoxMiesiacSprzedaz != null && comboBoxMiesiacSprzedaz.Items.Count > 0)
                comboBoxMiesiacSprzedaz.SelectedValue = DateTime.Now.Month;

            if (comboBoxRokTop10 != null && comboBoxRokTop10.Items.Count > 0)
                comboBoxRokTop10.SelectedItem = DateTime.Now.Year;

            if (comboBoxMiesiacTop10 != null && comboBoxMiesiacTop10.Items.Count > 0)
                comboBoxMiesiacTop10.SelectedValue = DateTime.Now.Month;

            if (comboBoxTowarTop10 != null && comboBoxTowarTop10.Items.Count > 0)
                comboBoxTowarTop10.SelectedIndex = 0;

            if (comboBoxRokUdzial != null && comboBoxRokUdzial.Items.Count > 0)
                comboBoxRokUdzial.SelectedItem = DateTime.Now.Year;

            if (comboBoxMiesiacUdzial != null && comboBoxMiesiacUdzial.Items.Count > 0)
                comboBoxMiesiacUdzial.SelectedValue = DateTime.Now.Month;

            if (comboBoxTowarAnalizaCen != null && comboBoxTowarAnalizaCen.Items.Count > 0)
                comboBoxTowarAnalizaCen.SelectedIndex = 0;
        }

        private void StworzZakladkeAnalizyCen(TabPage tab)
        {
            Panel mainPanel = new Panel { Dock = DockStyle.Fill };
            tab.Controls.Add(mainPanel);

            Panel panelControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(10)
            };

            Label lblTowar = new Label { Text = "📦 Towar:", Location = new Point(10, 15), AutoSize = true };
            comboBoxTowarAnalizaCen = new ComboBox
            {
                Location = new Point(75, 12),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijTowaryAnalizaCen(comboBoxTowarAnalizaCen);
            comboBoxTowarAnalizaCen.SelectedIndexChanged += (s, e) => OdswiezAnalizeCen();

            Label lblDataOd = new Label { Text = "📅 Od:", Location = new Point(345, 15), AutoSize = true };
            dateTimePickerAnalizaOd = new DateTimePicker
            {
                Location = new Point(390, 12),
                Size = new Size(110, 23),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today.AddMonths(-3)
            };
            dateTimePickerAnalizaOd.ValueChanged += (s, e) => OdswiezAnalizeCen();

            Label lblDataDo = new Label { Text = "📅 Do:", Location = new Point(515, 15), AutoSize = true };
            dateTimePickerAnalizaDo = new DateTimePicker
            {
                Location = new Point(560, 12),
                Size = new Size(110, 23),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today
            };
            dateTimePickerAnalizaDo.ValueChanged += (s, e) => OdswiezAnalizeCen();

            Button btnEksportuj = new Button
            {
                Text = "📊 Eksportuj CSV",
                Location = new Point(685, 11),
                Size = new Size(140, 25),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnEksportuj.FlatAppearance.BorderSize = 0;
            btnEksportuj.Click += BtnEksportujAnalize_Click;

            Label lblMinTransakcji = new Label { Text = "🔢 Min. transakcji:", Location = new Point(10, 50), AutoSize = true };
            NumericUpDown numMinTransakcji = new NumericUpDown
            {
                Location = new Point(120, 47),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 1000,
                Value = 3,
                Name = "numMinTransakcji"
            };
            numMinTransakcji.ValueChanged += (s, e) => OdswiezAnalizeCen();

            CheckBox chkPokazAlertyCheck = new CheckBox
            {
                Text = "⚠ Tylko alerty cenowe",
                Location = new Point(200, 49),
                AutoSize = true,
                Name = "chkPokazAlertyCheck"
            };
            chkPokazAlertyCheck.CheckedChanged += (s, e) => OdswiezAnalizeCen();

            CheckBox chkPokazRekomendacje = new CheckBox
            {
                Text = "✓ Rekomendacje akcji",
                Location = new Point(380, 49),
                AutoSize = true,
                Checked = true,
                Name = "chkPokazRekomendacje"
            };
            chkPokazRekomendacje.CheckedChanged += (s, e) => OdswiezAnalizeCen();

            panelControls.Controls.Add(lblTowar);
            panelControls.Controls.Add(comboBoxTowarAnalizaCen);
            panelControls.Controls.Add(lblDataOd);
            panelControls.Controls.Add(dateTimePickerAnalizaOd);
            panelControls.Controls.Add(lblDataDo);
            panelControls.Controls.Add(dateTimePickerAnalizaDo);
            panelControls.Controls.Add(btnEksportuj);
            panelControls.Controls.Add(lblMinTransakcji);
            panelControls.Controls.Add(numMinTransakcji);
            panelControls.Controls.Add(chkPokazAlertyCheck);
            panelControls.Controls.Add(chkPokazRekomendacje);

            mainPanel.Controls.Add(panelControls);

            SplitContainer splitAnalizaCen = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300,
                BackColor = ColorTranslator.FromHtml("#bdc3c7")
            };

            Panel panelTrend = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = ColorTranslator.FromHtml("#d5f4e6"),
                Padding = new Padding(10)
            };

            lblTrendInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📊 Wybierz towar aby zobaczyć analizę trendów cenowych",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#27ae60"),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelTrend.Controls.Add(lblTrendInfo);

            splitAnalizaCen.Panel1.Controls.Add(panelTrend);

            dataGridViewAnalizaCen = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false
            };
            KonfigurujDataGridViewAnalizaCen();

            splitAnalizaCen.Panel1.Controls.Add(dataGridViewAnalizaCen);

            chartAnalizaCen = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            KonfigurujWykresAnalizaCen();

            splitAnalizaCen.Panel2.Controls.Add(chartAnalizaCen);

            mainPanel.Controls.Add(splitAnalizaCen);
        }

        private void StworzZakladkePorownaniaSwiezeMrozone(TabPage tab)
        {
            Panel mainPanel = new Panel { Dock = DockStyle.Fill };
            tab.Controls.Add(mainPanel);

            Panel panelControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(10)
            };

            Label lblTowar1 = new Label
            {
                Text = "📦 Towar 1:",
                Location = new Point(10, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            ComboBox comboBoxTowar1 = new ComboBox
            {
                Location = new Point(85, 12),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Name = "comboBoxTowar1"
            };
            WypelnijWszystkieTowaryPorown(comboBoxTowar1);
            comboBoxTowar1.SelectedIndexChanged += (s, e) => OdswiezPorownanieTowarow();

            Label lblTowar2 = new Label
            {
                Text = "📦 Towar 2:",
                Location = new Point(355, 15),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            ComboBox comboBoxTowar2 = new ComboBox
            {
                Location = new Point(430, 12),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Name = "comboBoxTowar2"
            };
            WypelnijWszystkieTowaryPorown(comboBoxTowar2);
            comboBoxTowar2.SelectedIndexChanged += (s, e) => OdswiezPorownanieTowarow();

            Button btnEksportuj = new Button
            {
                Text = "📊 Eksportuj",
                Location = new Point(700, 11),
                Size = new Size(110, 25),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnEksportuj.FlatAppearance.BorderSize = 0;
            btnEksportuj.Click += BtnEksportujPorownanieClick;

            panelControls.Controls.Add(lblTowar1);
            panelControls.Controls.Add(comboBoxTowar1);
            panelControls.Controls.Add(lblTowar2);
            panelControls.Controls.Add(comboBoxTowar2);
            panelControls.Controls.Add(btnEksportuj);

            Panel panelStatystyki = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = ColorTranslator.FromHtml("#d5f4e6"),
                Padding = new Padding(5)
            };

            lblStatystykiPorown = new Label
            {
                Dock = DockStyle.Fill,
                Text = "⚖ Wybierz dwa towary aby zobaczyć porównanie cen",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#27ae60"),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelStatystyki.Controls.Add(lblStatystykiPorown);

            dataGridViewPorownaniaSwiezeMrozone = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                Margin = new Padding(0, 5, 0, 0)
            };
            KonfigurujDataGridViewPorownaniaSwiezeMrozone();

            mainPanel.Controls.Add(dataGridViewPorownaniaSwiezeMrozone);
            mainPanel.Controls.Add(panelStatystyki);
            mainPanel.Controls.Add(panelControls);
        }

        private void KonfigurujDataGridViewAnalizaCen()
        {
            dataGridViewAnalizaCen.Columns.Clear();

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Handlowiec",
                DataPropertyName = "Handlowiec",
                HeaderText = "👤 Handlowiec",
                Width = 120
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SredniaCena",
                DataPropertyName = "SredniaCena",
                HeaderText = "💵 Śr. cena",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MinCena",
                DataPropertyName = "MinCena",
                HeaderText = "⬇ Min",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MaxCena",
                DataPropertyName = "MaxCena",
                HeaderText = "⬆ Max",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LiczbaTransakcji",
                DataPropertyName = "LiczbaTransakcji",
                HeaderText = "🔢 Trans.",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "TrendProcentowy",
                DataPropertyName = "TrendProcentowy",
                HeaderText = "📊 Trend %",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N1",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Trend",
                DataPropertyName = "Trend",
                HeaderText = "↗ Kierunek",
                Width = 100
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OdchylenieOdSredniej",
                DataPropertyName = "OdchylenieOdSredniej",
                HeaderText = "± Odch. %",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N1",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RekomendowanaCena",
                DataPropertyName = "RekomendowanaCena",
                HeaderText = "✓ Rekom.",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = ColorTranslator.FromHtml("#fff3cd"),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "AkcjaRekomendowana",
                DataPropertyName = "AkcjaRekomendowana",
                HeaderText = "⚡ Akcja",
                Width = 150
            });

            dataGridViewAnalizaCen.EnableHeadersVisualStyles = false;
            dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#16a085");
            dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            dataGridViewAnalizaCen.ColumnHeadersHeight = 40;
            dataGridViewAnalizaCen.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#e8f8f5");
            dataGridViewAnalizaCen.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1abc9c");
            dataGridViewAnalizaCen.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewAnalizaCen.RowTemplate.Height = 35;

            dataGridViewAnalizaCen.CellFormatting += DataGridViewAnalizaCen_CellFormatting;
        }

        private void KonfigurujDataGridViewPorownaniaSwiezeMrozone()
        {
            dataGridViewPorownaniaSwiezeMrozone.Columns.Clear();

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Data",
                DataPropertyName = "Data",
                HeaderText = "📅 Data",
                Width = 85,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy" }
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaMrozone",
                DataPropertyName = "CenaMrozone",
                HeaderText = "💵 Cena T1",
                Width = 75,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IloscMrozone",
                DataPropertyName = "IloscMrozone",
                HeaderText = "⚖ kg T1",
                Width = 65,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N1",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaSwieze",
                DataPropertyName = "CenaSwieze",
                HeaderText = "💵 Cena T2",
                Width = 75,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IloscSwieze",
                DataPropertyName = "IloscSwieze",
                HeaderText = "⚖ kg T2",
                Width = 65,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N1",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RoznicaZl",
                DataPropertyName = "RoznicaZl",
                HeaderText = "± Różn. zł",
                Width = 75,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N2",
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RoznicaProcent",
                DataPropertyName = "RoznicaProcent",
                HeaderText = "📊 Różn. %",
                Width = 70,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Format = "N1",
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KtoryDrozszy",
                DataPropertyName = "KtoryDrozszy",
                HeaderText = "⬆ Droższy",
                Width = 90
            });

            dataGridViewPorownaniaSwiezeMrozone.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Kontrahent",
                DataPropertyName = "Kontrahent",
                HeaderText = "🏢 Kontrahenci",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 150
            });

            dataGridViewPorownaniaSwiezeMrozone.EnableHeadersVisualStyles = false;
            dataGridViewPorownaniaSwiezeMrozone.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#8e44ad");
            dataGridViewPorownaniaSwiezeMrozone.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewPorownaniaSwiezeMrozone.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewPorownaniaSwiezeMrozone.ColumnHeadersHeight = 35;
            dataGridViewPorownaniaSwiezeMrozone.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#f4ecf7");
            dataGridViewPorownaniaSwiezeMrozone.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#9b59b6");
            dataGridViewPorownaniaSwiezeMrozone.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewPorownaniaSwiezeMrozone.RowTemplate.Height = 30;

            dataGridViewPorownaniaSwiezeMrozone.CellFormatting += DataGridViewPorownaniaSwiezeMrozone_CellFormatting;
        }

        private void DataGridViewAnalizaCen_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridViewAnalizaCen.Columns[e.ColumnIndex].Name == "Trend" && e.Value != null)
            {
                string trend = e.Value.ToString();
                if (trend.Contains("wzrost"))
                {
                    e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                    e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
                }
                else if (trend.Contains("spadek"))
                {
                    e.CellStyle.ForeColor = ColorTranslator.FromHtml("#27ae60");
                    e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
                }
                else
                {
                    e.CellStyle.ForeColor = ColorTranslator.FromHtml("#95a5a6");
                }
            }

            if (dataGridViewAnalizaCen.Columns[e.ColumnIndex].Name == "TrendProcentowy" && e.Value != null)
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal wartosc))
                {
                    if (wartosc > 0)
                        e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                    else if (wartosc < 0)
                        e.CellStyle.ForeColor = ColorTranslator.FromHtml("#27ae60");
                }
            }
        }

        private void DataGridViewPorownaniaSwiezeMrozone_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridViewPorownaniaSwiezeMrozone.Columns[e.ColumnIndex].Name == "RoznicaProcent" && e.Value != null)
            {
                if (decimal.TryParse(e.Value.ToString(), out decimal procent))
                {
                    if (procent >= 30)
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#d4edda");
                    else if (procent >= 15)
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#fff3cd");
                    else if (procent > 0)
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#f8d7da");
                }
            }
        }

        private void KonfigurujWykresAnalizaCen()
        {
            chartAnalizaCen.ChartAreas.Clear();
            ChartArea area = new ChartArea("MainArea");
            area.AxisX.Title = "📅 Data";
            area.AxisX.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            area.AxisY.Title = "💰 Cena (zł/kg)";
            area.AxisY.TitleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            area.BackColor = Color.WhiteSmoke;
            chartAnalizaCen.ChartAreas.Add(area);

            chartAnalizaCen.Titles.Clear();
            Title title = new Title("📊 Trendy cenowe w czasie");
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            chartAnalizaCen.Titles.Add(title);

            chartAnalizaCen.Legends.Clear();
            Legend legend = new Legend("Legend");
            legend.Docking = Docking.Right;
            legend.Font = new Font("Segoe UI", 9F);
            chartAnalizaCen.Legends.Add(legend);
        }

        private void WypelnijTowaryAnalizaCen(ComboBox combo)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "SELECT [ID], [kod], [katalog] FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY katalog, Kod ASC";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable towary = new DataTable();
                    adapter.Fill(towary);

                    DataRow dr = towary.NewRow();
                    dr["ID"] = 0;
                    dr["kod"] = "--- Wybierz towar ---";
                    dr["katalog"] = DBNull.Value;
                    towary.Rows.InsertAt(dr, 0);

                    combo.DisplayMember = "kod";
                    combo.ValueMember = "ID";
                    combo.DataSource = towary;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd ładowania towarów: " + ex.Message);
            }
        }

        private void WypelnijWszystkieTowaryPorown(ComboBox combo)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "SELECT [ID], [kod], [katalog] FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY katalog, Kod ASC";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable towary = new DataTable();
                    adapter.Fill(towary);

                    DataRow dr = towary.NewRow();
                    dr["ID"] = 0;
                    dr["kod"] = "--- Wybierz towar ---";
                    dr["katalog"] = DBNull.Value;
                    towary.Rows.InsertAt(dr, 0);

                    combo.DisplayMember = "kod";
                    combo.ValueMember = "ID";
                    combo.DataSource = towary;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd ładowania towarów: " + ex.Message);
            }
        }

        private void OdswiezAnalizeCen()
        {
            if (comboBoxTowarAnalizaCen == null || dateTimePickerAnalizaOd == null || dateTimePickerAnalizaDo == null)
                return;

            if (comboBoxTowarAnalizaCen.SelectedValue == null || (int)comboBoxTowarAnalizaCen.SelectedValue == 0)
            {
                lblTrendInfo.Text = "📊 Wybierz towar aby zobaczyć analizę trendów cenowych";
                lblTrendInfo.BackColor = ColorTranslator.FromHtml("#d5f4e6");
                dataGridViewAnalizaCen.DataSource = null;
                chartAnalizaCen.Series.Clear();
                return;
            }

            int towarId = (int)comboBoxTowarAnalizaCen.SelectedValue;
            DateTime dataOd = dateTimePickerAnalizaOd.Value.Date;
            DateTime dataDo = dateTimePickerAnalizaDo.Value.Date;

            string query = @"
        WITH CenyHandlowcow AS (
            SELECT 
                ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                DP.cena AS Cena,
                DK.data AS Data
            FROM [HANDEL].[HM].[DK] DK
            INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
            WHERE DP.idtw = @TowarID
              AND DK.data >= @DataOd
              AND DK.data <= @DataDo
              AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') NOT IN ('Ogólne')
        ),
        CenyPoczatek AS (
            SELECT Handlowiec, AVG(Cena) AS CenaPoczatkowa
            FROM CenyHandlowcow
            WHERE Data < DATEADD(DAY, 14, @DataOd)
            GROUP BY Handlowiec
        ),
        CenyKoniec AS (
            SELECT Handlowiec, AVG(Cena) AS CenaKoncowa
            FROM CenyHandlowcow
            WHERE Data >= DATEADD(DAY, -14, @DataDo)
            GROUP BY Handlowiec
        )
        SELECT 
            CH.Handlowiec,
            CAST(AVG(CH.Cena) AS DECIMAL(18,2)) AS SredniaCena,
            CAST(MIN(CH.Cena) AS DECIMAL(18,2)) AS MinCena,
            CAST(MAX(CH.Cena) AS DECIMAL(18,2)) AS MaxCena,
            COUNT(*) AS LiczbaTransakcji,
            CAST(CASE 
                WHEN CP.CenaPoczatkowa IS NOT NULL AND CK.CenaKoncowa IS NOT NULL AND CP.CenaPoczatkowa > 0
                THEN ((CK.CenaKoncowa - CP.CenaPoczatkowa) / CP.CenaPoczatkowa) * 100
                ELSE 0
            END AS DECIMAL(18,2)) AS TrendProcentowy
        FROM CenyHandlowcow CH
        LEFT JOIN CenyPoczatek CP ON CH.Handlowiec = CP.Handlowiec
        LEFT JOIN CenyKoniec CK ON CH.Handlowiec = CK.Handlowiec
        GROUP BY CH.Handlowiec, CP.CenaPoczatkowa, CK.CenaKoncowa
        ORDER BY SredniaCena DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@TowarID", towarId);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        dt.Columns.Add("Trend", typeof(string));
                        dt.Columns.Add("RekomendowanaCena", typeof(decimal));
                        dt.Columns.Add("OdchylenieOdSredniej", typeof(decimal));
                        dt.Columns.Add("AkcjaRekomendowana", typeof(string));

                        decimal sredniaRynkowa = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            sredniaRynkowa += Convert.ToDecimal(row["SredniaCena"]);
                        }
                        sredniaRynkowa /= dt.Rows.Count;

                        foreach (DataRow row in dt.Rows)
                        {
                            decimal trendProc = Convert.ToDecimal(row["TrendProcentowy"]);
                            decimal sredniaCena = Convert.ToDecimal(row["SredniaCena"]);
                            decimal odchylenie = ((sredniaCena - sredniaRynkowa) / sredniaRynkowa) * 100;
                            row["OdchylenieOdSredniej"] = odchylenie;

                            if (trendProc > 5)
                                row["Trend"] = "↗ Silny wzrost";
                            else if (trendProc > 0)
                                row["Trend"] = "↗ Wzrost";
                            else if (trendProc < -5)
                                row["Trend"] = "↘ Silny spadek";
                            else if (trendProc < 0)
                                row["Trend"] = "↘ Spadek";
                            else
                                row["Trend"] = "→ Stabilny";

                            decimal rekomendacja = sredniaRynkowa * 0.95m;

                            if (trendProc > 5)
                            {
                                rekomendacja = sredniaRynkowa;
                                row["AkcjaRekomendowana"] = "⚠ Utrzymaj cenę";
                            }
                            else if (trendProc < -5)
                            {
                                rekomendacja = sredniaRynkowa * 0.90m;
                                row["AkcjaRekomendowana"] = "✓ Obniż cenę";
                            }
                            else if (odchylenie > 10)
                            {
                                row["AkcjaRekomendowana"] = "⬇ Rozważ obniżkę";
                            }
                            else if (odchylenie < -10)
                            {
                                row["AkcjaRekomendowana"] = "⬆ Możliwa podwyżka";
                            }
                            else
                            {
                                row["AkcjaRekomendowana"] = "✓ Cena OK";
                            }

                            row["RekomendowanaCena"] = Math.Round(rekomendacja, 2);
                        }

                        var sumaRow = dt.NewRow();
                        sumaRow["Handlowiec"] = "📊 ŚREDNIA RYNKOWA";
                        sumaRow["SredniaCena"] = sredniaRynkowa;
                        sumaRow["MinCena"] = dt.AsEnumerable().Min(r => r.Field<decimal>("MinCena"));
                        sumaRow["MaxCena"] = dt.AsEnumerable().Max(r => r.Field<decimal>("MaxCena"));
                        sumaRow["LiczbaTransakcji"] = dt.AsEnumerable().Sum(r => r.Field<int>("LiczbaTransakcji"));

                        decimal avgTrend = dt.AsEnumerable().Average(r => r.Field<decimal>("TrendProcentowy"));
                        sumaRow["TrendProcentowy"] = avgTrend;

                        if (avgTrend > 5)
                            sumaRow["Trend"] = "↗ Silny wzrost";
                        else if (avgTrend > 0)
                            sumaRow["Trend"] = "↗ Wzrost";
                        else if (avgTrend < -5)
                            sumaRow["Trend"] = "↘ Silny spadek";
                        else if (avgTrend < 0)
                            sumaRow["Trend"] = "↘ Spadek";
                        else
                            sumaRow["Trend"] = "→ Stabilny";

                        sumaRow["RekomendowanaCena"] = Math.Round(sredniaRynkowa * 0.95m, 2);
                        sumaRow["OdchylenieOdSredniej"] = 0;
                        sumaRow["AkcjaRekomendowana"] = "—";

                        dt.Rows.InsertAt(sumaRow, 0);
                    }

                    dataGridViewAnalizaCen.DataSource = dt;

                    if (dataGridViewAnalizaCen.Rows.Count > 0)
                    {
                        dataGridViewAnalizaCen.Rows[0].DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#d5dbdb");
                        dataGridViewAnalizaCen.Rows[0].DefaultCellStyle.Font = new Font(dataGridViewAnalizaCen.Font, FontStyle.Bold);
                    }

                    if (dt.Rows.Count > 1)
                    {
                        decimal avgTrendVal = Convert.ToDecimal(dt.Rows[0]["TrendProcentowy"]);
                        string trendTekst = dt.Rows[0]["Trend"].ToString();

                        lblTrendInfo.Text = $"📊 Trend: {trendTekst} ({avgTrendVal:F1}%) | " +
                                          $"Śr. cena: {dt.Rows[0]["SredniaCena"]:N2} zł/kg | " +
                                          $"Rekomendacja: {dt.Rows[0]["RekomendowanaCena"]:N2} zł/kg";

                        if (avgTrendVal > 5)
                            lblTrendInfo.BackColor = ColorTranslator.FromHtml("#ffcccc");
                        else if (avgTrendVal < -5)
                            lblTrendInfo.BackColor = ColorTranslator.FromHtml("#ccffcc");
                        else
                            lblTrendInfo.BackColor = ColorTranslator.FromHtml("#fff3cd");
                    }

                    GenerujWykresAnalizyCen(towarId, dataOd, dataDo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd analizy cen: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GenerujWykresAnalizyCen(int towarId, DateTime dataOd, DateTime dataDo)
        {
            string query = @"
        SELECT 
            CONVERT(date, DK.data) AS Data,
            ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
            CAST(AVG(DP.cena) AS DECIMAL(18,2)) AS SredniaCena
        FROM [HANDEL].[HM].[DK] DK
        INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
        WHERE DP.idtw = @TowarID
          AND DK.data >= @DataOd
          AND DK.data <= @DataDo
          AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') NOT IN ('Ogólne')
        GROUP BY CONVERT(date, DK.data), ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
        ORDER BY Data, Handlowiec;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@TowarID", towarId);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    chartAnalizaCen.Series.Clear();

                    var handlowcy = dt.AsEnumerable()
                        .Select(r => r.Field<string>("Handlowiec"))
                        .Distinct()
                        .OrderBy(h => h)
                        .ToList();

                    Color[] colors = {
                        ColorTranslator.FromHtml("#3498db"),
                        ColorTranslator.FromHtml("#e74c3c"),
                        ColorTranslator.FromHtml("#2ecc71"),
                        ColorTranslator.FromHtml("#f39c12"),
                        ColorTranslator.FromHtml("#9b59b6"),
                        ColorTranslator.FromHtml("#1abc9c"),
                        ColorTranslator.FromHtml("#e67e22"),
                        ColorTranslator.FromHtml("#95a5a6")
                    };

                    int colorIdx = 0;
                    foreach (var handlowiec in handlowcy)
                    {
                        Series series = new Series(handlowiec);
                        series.ChartType = SeriesChartType.Line;
                        series.BorderWidth = 3;
                        series.MarkerStyle = MarkerStyle.Circle;
                        series.MarkerSize = 6;
                        series.Color = colors[colorIdx % colors.Length];

                        var daneHandlowca = dt.AsEnumerable()
                            .Where(r => r.Field<string>("Handlowiec") == handlowiec)
                            .OrderBy(r => r.Field<DateTime>("Data"));

                        foreach (var row in daneHandlowca)
                        {
                            DateTime data = row.Field<DateTime>("Data");
                            decimal cena = row.Field<decimal>("SredniaCena");
                            series.Points.AddXY(data.ToOADate(), cena);
                        }

                        chartAnalizaCen.Series.Add(series);
                        colorIdx++;
                    }

                    chartAnalizaCen.ChartAreas[0].AxisX.LabelStyle.Format = "dd/MM";
                    chartAnalizaCen.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Days;
                    chartAnalizaCen.ChartAreas[0].AxisX.Interval = Math.Max(1, (dataDo - dataOd).Days / 10);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd wykresu: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WypelnijLataComboBox(ComboBox combo)
        {
            var lata = new List<int>();
            for (int rok = DateTime.Now.Year; rok >= DateTime.Now.Year - 5; rok--)
            {
                lata.Add(rok);
            }
            combo.DataSource = lata;
        }

        private void WypelnijMiesiaceComboBox(ComboBox combo)
        {
            var miesiace = new List<KeyValuePair<int, string>>();
            var kultura = new CultureInfo("pl-PL");
            for (int i = 1; i <= 12; i++)
            {
                string nazwa = kultura.DateTimeFormat.GetMonthName(i);
                nazwa = char.ToUpper(nazwa[0]) + nazwa.Substring(1);
                miesiace.Add(new KeyValuePair<int, string>(i, nazwa));
            }
            combo.DisplayMember = "Value";
            combo.ValueMember = "Key";
            combo.DataSource = miesiace;
        }

        private void WypelnijTowaryComboBoxTop10(ComboBox combo)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "SELECT [ID], [kod] FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095', '67153') ORDER BY katalog, Kod ASC";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable towary = new DataTable();
                    adapter.Fill(towary);

                    DataRow dr = towary.NewRow();
                    dr["ID"] = 0;
                    dr["kod"] = "--- Wszystkie towary ---";
                    towary.Rows.InsertAt(dr, 0);

                    combo.DisplayMember = "kod";
                    combo.ValueMember = "ID";
                    combo.DataSource = towary;
                }
            }
            catch { }
        }

        private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (tabControl.SelectedIndex == 0)
                WczytajPlatnosciPerKontrahent(null);
            else if (tabControl.SelectedIndex == 1)
                OdswiezWykresSprzedazy();
            else if (tabControl.SelectedIndex == 2)
                OdswiezWykresTop10();
            else if (tabControl.SelectedIndex == 3)
                OdswiezWykresHandlowcow();
            else if (tabControl.SelectedIndex == 4)
                OdswiezAnalizeCen();
            else if (tabControl.SelectedIndex == 5)
                OdswiezPorownanieTowarow();
        }

        private void KonfigurujWykresSprzedazy()
        {
            chartSprzedaz.ChartAreas.Clear();
            ChartArea area = new ChartArea("MainArea");
            area.BackColor = Color.WhiteSmoke;
            chartSprzedaz.ChartAreas.Add(area);

            chartSprzedaz.Titles.Clear();
            Title title = new Title("📊 Udział kontrahentów w sprzedaży");
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            chartSprzedaz.Titles.Add(title);

            chartSprzedaz.Legends.Clear();
            Legend legend = new Legend("Legend");
            legend.Docking = Docking.Right;
            legend.Font = new Font("Segoe UI", 10F);
            chartSprzedaz.Legends.Add(legend);
        }

        private void KonfigurujWykresTop10()
        {
            chartTop10.ChartAreas.Clear();
            ChartArea area = new ChartArea("MainArea");
            area.BackColor = Color.WhiteSmoke;
            chartTop10.ChartAreas.Add(area);

            chartTop10.Titles.Clear();
            Title title = new Title("🏆 Top 10 odbiorców towaru");
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            chartTop10.Titles.Add(title);

            chartTop10.Legends.Clear();
            Legend legend = new Legend("Legend");
            legend.Docking = Docking.Right;
            legend.Font = new Font("Segoe UI", 10F);
            chartTop10.Legends.Add(legend);
        }

        private void KonfigurujWykresHandlowcow()
        {
            chartHandlowcy.ChartAreas.Clear();
            ChartArea area = new ChartArea("MainArea");
            area.BackColor = Color.WhiteSmoke;
            chartHandlowcy.ChartAreas.Add(area);

            chartHandlowcy.Titles.Clear();
            Title title = new Title("📊 Procentowy udział handlowców w sprzedaży");
            title.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            chartHandlowcy.Titles.Add(title);

            chartHandlowcy.Legends.Clear();
            Legend legend = new Legend("Legend");
            legend.Docking = Docking.Right;
            legend.Font = new Font("Segoe UI", 10F);
            chartHandlowcy.Legends.Add(legend);
        }

        private void OdswiezWykresSprzedazy()
        {
            try
            {
                if (comboBoxRokSprzedaz == null || comboBoxMiesiacSprzedaz == null)
                    return;

                if (comboBoxRokSprzedaz.SelectedItem == null || comboBoxMiesiacSprzedaz.SelectedValue == null)
                    return;

                int rok = (int)comboBoxRokSprzedaz.SelectedItem;
                int miesiac = (int)comboBoxMiesiacSprzedaz.SelectedValue;
                var zaznaczeniHandlowcy = PobierzZaznaczonychHandlowcow();

                var lblSuma = chartSprzedaz?.Parent?.Controls.Find("panelSumaSprzedaz", false)
                    .FirstOrDefault()?.Controls.Find("lblSumaSprzedaz", false)
                    .FirstOrDefault() as Label;

                string query = @"
            SELECT 
                C.shortcut AS Kontrahent,
                SUM(DP.wartNetto) AS WartoscSprzedazy
            FROM [HANDEL].[HM].[DK] DK
            INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
            INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
            WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac";

                if (zaznaczeniHandlowcy.Count > 0)
                {
                    var handlowcyLista = string.Join("','", zaznaczeniHandlowcy.Select(h => h.Replace("'", "''")));
                    query += $" AND WYM.CDim_Handlowiec_Val IN ('{handlowcyLista}')";
                }

                query += @"
            GROUP BY C.shortcut
            ORDER BY WartoscSprzedazy DESC;";

                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Rok", rok);
                    cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    chartSprzedaz.Series.Clear();

                    if (dt.Rows.Count == 0)
                    {
                        chartSprzedaz.Titles[0].Text = "❌ Brak danych dla wybranego okresu";
                        if (lblSuma != null) lblSuma.Text = "";
                        return;
                    }

                    Series series = new Series("Udział");
                    series.ChartType = SeriesChartType.Pie;
                    series.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    series["PieLabelStyle"] = "Outside";
                    series["PieLineColor"] = "Black";

                    decimal suma = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        suma += Convert.ToDecimal(row["WartoscSprzedazy"]);
                    }

                    Color[] colors = {
                    ColorTranslator.FromHtml("#3498db"),
                    ColorTranslator.FromHtml("#2ecc71"),
                    ColorTranslator.FromHtml("#e74c3c"),
                    ColorTranslator.FromHtml("#f39c12"),
                    ColorTranslator.FromHtml("#9b59b6"),
                    ColorTranslator.FromHtml("#1abc9c"),
                    ColorTranslator.FromHtml("#e67e22"),
                    ColorTranslator.FromHtml("#95a5a6"),
                    ColorTranslator.FromHtml("#34495e"),
                    ColorTranslator.FromHtml("#16a085")
                };

                    int colorIdx = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        string kontrahent = row["Kontrahent"].ToString();
                        decimal wartosc = Convert.ToDecimal(row["WartoscSprzedazy"]);
                        decimal procent = suma > 0 ? (wartosc / suma) * 100 : 0;

                        var point = series.Points.AddXY(kontrahent, wartosc);
                        series.Points[point].Color = colors[colorIdx % colors.Length];
                        series.Points[point].Label = $"{procent:F1}%";
                        series.Points[point].LegendText = $"{kontrahent}: {wartosc:N0} zł ({procent:F1}%)";
                        colorIdx++;
                    }

                    chartSprzedaz.Series.Add(series);

                    var kulturaPL = new CultureInfo("pl-PL");
                    string nazwaHandlowcow = zaznaczeniHandlowcy.Count == 0 ? "wszyscy handlowcy" :
                                            zaznaczeniHandlowcy.Count == 1 ? zaznaczeniHandlowcy[0] :
                                            $"{zaznaczeniHandlowcy.Count} handlowców";
                    string tytul = $"📊 Udział kontrahentów - {nazwaHandlowcow} - {kulturaPL.DateTimeFormat.GetMonthName(miesiac)} {rok}";
                    chartSprzedaz.Titles[0].Text = tytul;

                    if (lblSuma != null)
                    {
                        lblSuma.Text = $"💰 CAŁKOWITA WARTOŚĆ SPRZEDAŻY: {suma:N2} zł";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd wykresu sprzedaży:\n{ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdswiezWykresTop10()
        {
            try
            {
                if (comboBoxRokTop10 == null || comboBoxMiesiacTop10 == null || comboBoxTowarTop10 == null)
                    return;

                if (comboBoxRokTop10.SelectedItem == null || comboBoxMiesiacTop10.SelectedValue == null)
                    return;

                int rok = (int)comboBoxRokTop10.SelectedItem;
                int miesiac = (int)comboBoxMiesiacTop10.SelectedValue;
                int? towarId = null;

                if (comboBoxTowarTop10.SelectedValue != null && (int)comboBoxTowarTop10.SelectedValue != 0)
                    towarId = (int)comboBoxTowarTop10.SelectedValue;

                string query = @"
        SELECT TOP 10
            C.shortcut AS Kontrahent,
            ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
            SUM(DP.ilosc) AS SumaIlosci
        FROM [HANDEL].[HM].[DK] DK
        INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
        INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
        LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
        WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
          AND (@TowarID IS NULL OR DP.idtw = @TowarID)
        GROUP BY C.shortcut, ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
        ORDER BY SumaIlosci DESC;";

                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Rok", rok);
                    cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                    cmd.Parameters.AddWithValue("@TowarID", (object)towarId ?? DBNull.Value);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    chartTop10.Series.Clear();

                    if (dt.Rows.Count == 0)
                    {
                        chartTop10.Titles[0].Text = "❌ Brak danych dla wybranego okresu";
                        return;
                    }

                    Series series = new Series("Udział");
                    series.ChartType = SeriesChartType.Pie;
                    series.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    series["PieLabelStyle"] = "Outside";
                    series["PieLineColor"] = "Black";

                    decimal suma = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        suma += Convert.ToDecimal(row["SumaIlosci"]);
                    }

                    Color[] colors = {
                ColorTranslator.FromHtml("#e74c3c"),
                ColorTranslator.FromHtml("#e67e22"),
                ColorTranslator.FromHtml("#f39c12"),
                ColorTranslator.FromHtml("#f1c40f"),
                ColorTranslator.FromHtml("#2ecc71"),
                ColorTranslator.FromHtml("#1abc9c"),
                ColorTranslator.FromHtml("#3498db"),
                ColorTranslator.FromHtml("#9b59b6"),
                ColorTranslator.FromHtml("#95a5a6"),
                ColorTranslator.FromHtml("#7f8c8d")
            };

                    int colorIdx = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        string kontrahent = row["Kontrahent"].ToString();
                        string handlowiec = row["Handlowiec"].ToString();
                        decimal ilosc = Convert.ToDecimal(row["SumaIlosci"]);
                        decimal procent = suma > 0 ? (ilosc / suma) * 100 : 0;

                        var point = series.Points.AddXY(kontrahent, ilosc);
                        series.Points[point].Color = colors[colorIdx % colors.Length];
                        series.Points[point].Label = $"{procent:F1}%";
                        series.Points[point].LegendText = $"{kontrahent} ({handlowiec}) - {procent:F1}%";
                        colorIdx++;
                    }

                    chartTop10.Series.Add(series);

                    var kulturaPL = new CultureInfo("pl-PL");
                    string nazwaTowar = towarId.HasValue ? comboBoxTowarTop10.Text : "wszystkie towary";
                    string tytul = $"🏆 Top 10 - {nazwaTowar} - {kulturaPL.DateTimeFormat.GetMonthName(miesiac)} {rok}";
                    chartTop10.Titles[0].Text = tytul;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd Top 10: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void OdswiezWykresHandlowcow()
        {
            try
            {
                if (comboBoxRokUdzial == null || comboBoxMiesiacUdzial == null)
                    return;

                if (comboBoxRokUdzial.SelectedItem == null || comboBoxMiesiacUdzial.SelectedValue == null)
                    return;

                int rok = (int)comboBoxRokUdzial.SelectedItem;
                int miesiac = (int)comboBoxMiesiacUdzial.SelectedValue;

                string query = @"
            SELECT 
                WYM.CDim_Handlowiec_Val AS Handlowiec,
                SUM(DP.wartNetto) AS WartoscSprzedazy
            FROM [HANDEL].[HM].[DK] DK
            INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
            INNER JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
            WHERE YEAR(DK.data) = @Rok AND MONTH(DK.data) = @Miesiac
              AND WYM.CDim_Handlowiec_Val IS NOT NULL
            GROUP BY WYM.CDim_Handlowiec_Val
            ORDER BY WartoscSprzedazy DESC;";

                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Rok", rok);
                    cmd.Parameters.AddWithValue("@Miesiac", miesiac);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    chartHandlowcy.Series.Clear();

                    if (dt.Rows.Count == 0)
                    {
                        chartHandlowcy.Titles[0].Text = "❌ Brak danych dla wybranego okresu";
                        return;
                    }

                    Series series = new Series("Udział");
                    series.ChartType = SeriesChartType.Pie;
                    series.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    series["PieLabelStyle"] = "Outside";
                    series["PieLineColor"] = "Black";

                    decimal suma = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        suma += Convert.ToDecimal(row["WartoscSprzedazy"]);
                    }

                    Color[] colors = {
                    ColorTranslator.FromHtml("#3498db"),
                    ColorTranslator.FromHtml("#2ecc71"),
                    ColorTranslator.FromHtml("#e74c3c"),
                    ColorTranslator.FromHtml("#f39c12"),
                    ColorTranslator.FromHtml("#9b59b6"),
                    ColorTranslator.FromHtml("#1abc9c"),
                    ColorTranslator.FromHtml("#e67e22"),
                    ColorTranslator.FromHtml("#95a5a6")
                };

                    int colorIdx = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        string handlowiec = row["Handlowiec"].ToString();
                        decimal wartosc = Convert.ToDecimal(row["WartoscSprzedazy"]);
                        decimal procent = suma > 0 ? (wartosc / suma) * 100 : 0;

                        var point = series.Points.AddXY(handlowiec, wartosc);
                        series.Points[point].Color = colors[colorIdx % colors.Length];
                        series.Points[point].Label = $"{procent:F1}%";
                        series.Points[point].LegendText = $"{handlowiec} ({procent:F1}%)";
                        colorIdx++;
                    }

                    chartHandlowcy.Series.Add(series);

                    var kulturaPL = new CultureInfo("pl-PL");
                    string tytul = $"👥 Udział handlowców - {kulturaPL.DateTimeFormat.GetMonthName(miesiac)} {rok}";
                    chartHandlowcy.Titles[0].Text = tytul;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd handlowców: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdswiezPorownanieTowarow()
        {
            var combo1 = dataGridViewPorownaniaSwiezeMrozone?.Parent?.Controls.Find("comboBoxTowar1", true).FirstOrDefault() as ComboBox;
            var combo2 = dataGridViewPorownaniaSwiezeMrozone?.Parent?.Controls.Find("comboBoxTowar2", true).FirstOrDefault() as ComboBox;

            if (combo1 == null || combo2 == null || combo1.SelectedValue == null || combo2.SelectedValue == null)
                return;

            int towar1Id = (int)combo1.SelectedValue;
            int towar2Id = (int)combo2.SelectedValue;

            if (towar1Id == 0 || towar2Id == 0)
            {
                lblStatystykiPorown.Text = "⚖ Wybierz dwa towary aby zobaczyć porównanie cen";
                lblStatystykiPorown.BackColor = ColorTranslator.FromHtml("#d5f4e6");
                dataGridViewPorownaniaSwiezeMrozone.DataSource = null;
                return;
            }

            if (towar1Id == towar2Id)
            {
                lblStatystykiPorown.Text = "⚠ Wybierz różne towary do porównania";
                lblStatystykiPorown.BackColor = ColorTranslator.FromHtml("#ffcccc");
                dataGridViewPorownaniaSwiezeMrozone.DataSource = null;
                return;
            }

            string query = @"
        WITH Towar1 AS (
            SELECT 
                CONVERT(date, DK.data) AS Data,
                AVG(DP.cena) AS CenaTowar1,
                SUM(DP.ilosc) AS IloscTowar1,
                STUFF((
                    SELECT DISTINCT ', ' + C2.shortcut
                    FROM [HANDEL].[HM].[DK] DK2
                    INNER JOIN [HANDEL].[HM].[DP] DP2 ON DK2.id = DP2.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C2 ON DK2.khid = C2.id
                    WHERE DP2.idtw = @Towar1ID
                      AND CONVERT(date, DK2.data) = CONVERT(date, DK.data)
                      AND DP2.cena > 0
                    FOR XML PATH('')
                ), 1, 2, '') AS KontrahenciTowar1
            FROM [HANDEL].[HM].[DK] DK
            INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
            INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
            WHERE DP.idtw = @Towar1ID
              AND DK.data >= DATEADD(MONTH, -6, GETDATE())
              AND DP.cena > 0
            GROUP BY CONVERT(date, DK.data)
        ),
        Towar2 AS (
            SELECT 
                CONVERT(date, DK.data) AS Data,
                AVG(DP.cena) AS CenaTowar2,
                SUM(DP.ilosc) AS IloscTowar2,
                STUFF((
                    SELECT DISTINCT ', ' + C2.shortcut
                    FROM [HANDEL].[HM].[DK] DK2
                    INNER JOIN [HANDEL].[HM].[DP] DP2 ON DK2.id = DP2.super
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C2 ON DK2.khid = C2.id
                    WHERE DP2.idtw = @Towar2ID
                      AND CONVERT(date, DK2.data) = CONVERT(date, DK.data)
                      AND DP2.cena > 0
                    FOR XML PATH('')
                ), 1, 2, '') AS KontrahenciTowar2
            FROM [HANDEL].[HM].[DK] DK
            INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
            INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
            WHERE DP.idtw = @Towar2ID
              AND DK.data >= DATEADD(MONTH, -6, GETDATE())
              AND DP.cena > 0
            GROUP BY CONVERT(date, DK.data)
        )
        SELECT 
            T1.Data,
            T1.CenaTowar1 AS CenaMrozone,
            T1.IloscTowar1 AS IloscMrozone,
            T2.CenaTowar2 AS CenaSwieze,
            T2.IloscTowar2 AS IloscSwieze,
            ISNULL(T1.KontrahenciTowar1, '') + 
            CASE 
                WHEN T1.KontrahenciTowar1 IS NOT NULL AND T2.KontrahenciTowar2 IS NOT NULL 
                THEN ' | ' 
                ELSE '' 
            END + 
            ISNULL(T2.KontrahenciTowar2, '') AS Kontrahent,
            ABS(T1.CenaTowar1 - T2.CenaTowar2) AS RoznicaZl,
            CASE 
                WHEN T1.CenaTowar1 > T2.CenaTowar2
                THEN ((T1.CenaTowar1 - T2.CenaTowar2) / T1.CenaTowar1) * 100
                ELSE ((T2.CenaTowar2 - T1.CenaTowar1) / T2.CenaTowar2) * 100
            END AS RoznicaProcent,
            CASE 
                WHEN T1.CenaTowar1 > T2.CenaTowar2 THEN 'Towar 1'
                WHEN T2.CenaTowar2 > T1.CenaTowar1 THEN 'Towar 2'
                ELSE 'Równe'
            END AS KtoryDrozszy
        FROM Towar1 T1
        INNER JOIN Towar2 T2 ON T1.Data = T2.Data
        WHERE T1.CenaTowar1 > 0 AND T2.CenaTowar2 > 0
        ORDER BY T1.Data DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@Towar1ID", towar1Id);
                    cmd.Parameters.AddWithValue("@Towar2ID", towar2Id);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    dataGridViewPorownaniaSwiezeMrozone.DataSource = dt;

                    if (dt.Rows.Count > 0)
                    {
                        decimal sredniaRoznicaProcent = 0;
                        decimal sredniaRoznicaZl = 0;
                        decimal sumaOszczednosci = 0;

                        foreach (DataRow row in dt.Rows)
                        {
                            decimal roznicaProcent = Convert.ToDecimal(row["RoznicaProcent"]);
                            decimal roznicaZl = Convert.ToDecimal(row["RoznicaZl"]);
                            decimal ilosc1 = Convert.ToDecimal(row["IloscMrozone"]);
                            decimal ilosc2 = Convert.ToDecimal(row["IloscSwieze"]);

                            sredniaRoznicaProcent += roznicaProcent;
                            sredniaRoznicaZl += roznicaZl;
                            sumaOszczednosci += roznicaZl * Math.Min(ilosc1, ilosc2);
                        }

                        sredniaRoznicaProcent /= dt.Rows.Count;
                        sredniaRoznicaZl /= dt.Rows.Count;

                        string t1 = combo1.Text.Length > 30 ? combo1.Text.Substring(0, 27) + "..." : combo1.Text;
                        string t2 = combo2.Text.Length > 30 ? combo2.Text.Substring(0, 27) + "..." : combo2.Text;

                        lblStatystykiPorown.Text = $"📊 {dt.Rows.Count} wspólnych dat | " +
                                                  $"Śr. różnica: {sredniaRoznicaProcent:F1}% ({sredniaRoznicaZl:N2} zł/kg) | " +
                                                  $"💰 Potencjalne oszczędności: {sumaOszczednosci:N2} zł\n" +
                                                  $"T1: {t1} ⚖ T2: {t2}";

                        if (sredniaRoznicaProcent >= 30)
                            lblStatystykiPorown.BackColor = ColorTranslator.FromHtml("#ccffcc");
                        else if (sredniaRoznicaProcent >= 15)
                            lblStatystykiPorown.BackColor = ColorTranslator.FromHtml("#fff3cd");
                        else
                            lblStatystykiPorown.BackColor = ColorTranslator.FromHtml("#ffe6e6");
                    }
                    else
                    {
                        lblStatystykiPorown.Text = "❌ Brak wspólnych dat sprzedaży dla wybranych towarów";
                        lblStatystykiPorown.BackColor = ColorTranslator.FromHtml("#ffcccc");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd porównania: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEksportujAnalize_Click(object? sender, EventArgs e)
        {
            if (dataGridViewAnalizaCen.Rows.Count == 0)
            {
                MessageBox.Show("ℹ Brak danych do eksportu", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV files (*.csv)|*.csv";
                saveDialog.FileName = $"Analiza_Cen_{DateTime.Now:yyyyMMdd}.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var csv = new System.Text.StringBuilder();

                        var headers = dataGridViewAnalizaCen.Columns.Cast<DataGridViewColumn>();
                        csv.AppendLine(string.Join(";", headers.Select(column => column.HeaderText)));

                        foreach (DataGridViewRow row in dataGridViewAnalizaCen.Rows)
                        {
                            var cells = row.Cells.Cast<DataGridViewCell>();
                            csv.AppendLine(string.Join(";", cells.Select(cell => cell.Value?.ToString() ?? "")));
                        }

                        System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString(), System.Text.Encoding.UTF8);
                        MessageBox.Show("✓ Eksport zakończony pomyślnie!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"❌ Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnEksportujPorownanieClick(object? sender, EventArgs e)
        {
            if (dataGridViewPorownaniaSwiezeMrozone.Rows.Count == 0)
            {
                MessageBox.Show("ℹ Brak danych do eksportu", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV files (*.csv)|*.csv";
                saveDialog.FileName = $"Porownanie_Swieze_Mrozone_{DateTime.Now:yyyyMMdd}.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var csv = new System.Text.StringBuilder();

                        var headers = dataGridViewPorownaniaSwiezeMrozone.Columns.Cast<DataGridViewColumn>();
                        csv.AppendLine(string.Join(";", headers.Select(column => column.HeaderText)));

                        foreach (DataGridViewRow row in dataGridViewPorownaniaSwiezeMrozone.Rows)
                        {
                            var cells = row.Cells.Cast<DataGridViewCell>();
                            csv.AppendLine(string.Join(";", cells.Select(cell => cell.Value?.ToString() ?? "")));
                        }

                        System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString(), System.Text.Encoding.UTF8);
                        MessageBox.Show("✓ Eksport zakończony pomyślnie!", "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"❌ Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void StylizujKomboBoxes()
        {
            if (comboBoxTowarSwieze != null)
            {
                comboBoxTowarSwieze.FlatStyle = FlatStyle.Flat;
                comboBoxTowarSwieze.BackColor = Color.White;
            }

            if (comboBoxTowarMrozone != null)
            {
                comboBoxTowarMrozone.FlatStyle = FlatStyle.Flat;
                comboBoxTowarMrozone.BackColor = Color.White;
            }

            comboBoxKontrahent.FlatStyle = FlatStyle.Flat;
            comboBoxKontrahent.BackColor = Color.White;
        }

        private void StylizujDataGridViews()
        {
            dataGridViewOdbiorcy.EnableHeadersVisualStyles = false;
            dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#34495e");
            dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewOdbiorcy.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewOdbiorcy.ColumnHeadersHeight = 35;
            dataGridViewOdbiorcy.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#ecf0f1");
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#3498db");
            dataGridViewOdbiorcy.DefaultCellStyle.SelectionForeColor = Color.White;
            dataGridViewOdbiorcy.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewOdbiorcy.BorderStyle = BorderStyle.None;
            dataGridViewOdbiorcy.AllowUserToResizeRows = false;
            dataGridViewOdbiorcy.RowTemplate.Resizable = DataGridViewTriState.False;

            dataGridViewPozycje.EnableHeadersVisualStyles = false;
            dataGridViewPozycje.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#16a085");
            dataGridViewPozycje.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewPozycje.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewPozycje.ColumnHeadersHeight = 35;
            dataGridViewPozycje.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#e8f8f5");
            dataGridViewPozycje.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1abc9c");
            dataGridViewPozycje.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewPozycje.BorderStyle = BorderStyle.None;
            dataGridViewPozycje.AllowUserToResizeRows = false;
            dataGridViewPozycje.RowTemplate.Resizable = DataGridViewTriState.False;

            dataGridViewPlatnosci.EnableHeadersVisualStyles = false;
            dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#16a085");
            dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewPlatnosci.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewPlatnosci.ColumnHeadersHeight = 35;
            dataGridViewPlatnosci.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#e8f8f5");
            dataGridViewPlatnosci.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1abc9c");
            dataGridViewPlatnosci.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewPlatnosci.BorderStyle = BorderStyle.None;
            dataGridViewPlatnosci.AllowUserToResizeRows = false;
            dataGridViewPlatnosci.RowTemplate.Resizable = DataGridViewTriState.False;
        }

        private void StylizujPrzyciski()
        {
            if (btnRefresh != null)
            {
                btnRefresh.FlatStyle = FlatStyle.Flat;
                btnRefresh.FlatAppearance.BorderSize = 0;
                btnRefresh.Cursor = Cursors.Hand;
                btnRefresh.BackColor = ColorTranslator.FromHtml("#3498db");
                btnRefresh.ForeColor = Color.White;
                btnRefresh.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                btnRefresh.Text = "🔄 Odśwież";
            }
        }

        private void btnRefresh_Click(object? sender, EventArgs e)
        {
            if (dateTimePickerOd.Value > dateTimePickerDo.Value)
            {
                MessageBox.Show("⚠ Data początkowa nie może być późniejsza niż data końcowa!",
                    "Błąd zakresu dat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OdswiezDaneGlownejSiatki();

            if (tabControl.SelectedIndex == 0)
                WczytajPlatnosciPerKontrahent(null);
            else if (tabControl.SelectedIndex == 1)
                OdswiezWykresSprzedazy();
            else if (tabControl.SelectedIndex == 2)
                OdswiezWykresTop10();
            else if (tabControl.SelectedIndex == 3)
                OdswiezWykresHandlowcow();
            else if (tabControl.SelectedIndex == 4)
                OdswiezAnalizeCen();
            else if (tabControl.SelectedIndex == 5)
                OdswiezPorownanieTowarow();
        }

        private void KonfigurujDataGridViewDokumenty()
        {
            dataGridViewOdbiorcy.AutoGenerateColumns = false;
            dataGridViewOdbiorcy.Columns.Clear();
            dataGridViewOdbiorcy.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewOdbiorcy.MultiSelect = false;
            dataGridViewOdbiorcy.ReadOnly = true;
            dataGridViewOdbiorcy.AllowUserToAddRows = false;

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "khid", DataPropertyName = "khid", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", DataPropertyName = "ID", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "IsGroupRow", DataPropertyName = "IsGroupRow", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "SortDate", DataPropertyName = "SortDate", Visible = false });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NumerDokumentu", DataPropertyName = "NumerDokumentu", HeaderText = "📄 Numer Dokumentu", Width = 150 });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "NazwaFirmy", DataPropertyName = "NazwaFirmy", HeaderText = "🏢 Nazwa Firmy", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "IloscKG", DataPropertyName = "IloscKG", HeaderText = "⚖ Ilosc KG", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "SredniaCena", DataPropertyName = "SredniaCena", HeaderText = "💵 Sr. Cena KG", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handlowiec", DataPropertyName = "Handlowiec", HeaderText = "👤 Handlowiec", Width = 120 });

            dataGridViewOdbiorcy.SelectionChanged += DataGridViewDokumenty_SelectionChanged;
            dataGridViewOdbiorcy.RowPrePaint += DataGridViewOdbiorcy_RowPrePaint;
            dataGridViewOdbiorcy.CellFormatting += DataGridViewOdbiorcy_CellFormatting;
        }

        private ContextMenuStrip contextMenuDokumenty;

        private void KonfigurujMenuKontekstowe()
        {
            contextMenuDokumenty = new ContextMenuStrip();
            contextMenuDokumenty.Font = new Font("Segoe UI", 9.5F);

            var menuOdswiez = new ToolStripMenuItem
            {
                Text = "🔄 Odśwież",
                ShortcutKeys = Keys.F5
            };
            menuOdswiez.Click += (s, e) => btnRefresh_Click(s, e);
            contextMenuDokumenty.Items.Add(menuOdswiez);

            contextMenuDokumenty.Items.Add(new ToolStripSeparator());

            var menuPodglad = new ToolStripMenuItem
            {
                Text = "👁 Podgląd faktury",
                ShortcutKeys = Keys.Control | Keys.P
            };
            menuPodglad.Click += MenuPodglad_Click;
            contextMenuDokumenty.Items.Add(menuPodglad);

            var menuAnaliza = new ToolStripMenuItem
            {
                Text = "📊 Tygodniowa analiza",
                ShortcutKeys = Keys.Control | Keys.T
            };
            menuAnaliza.Click += MenuAnaliza_Click;
            contextMenuDokumenty.Items.Add(menuAnaliza);

            contextMenuDokumenty.Items.Add(new ToolStripSeparator());

            var menuReklamacja = new ToolStripMenuItem
            {
                Text = "⚠ Zgłoś reklamację",
                Enabled = false,
                ForeColor = ColorTranslator.FromHtml("#95a5a6")
            };
            menuReklamacja.Click += (s, e) => MessageBox.Show(
                "ℹ Funkcja zgłaszania reklamacji będzie dostępna wkrótce.",
                "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            contextMenuDokumenty.Items.Add(menuReklamacja);

            var menuDrukuj = new ToolStripMenuItem
            {
                Text = "🖨 Drukuj",
                Enabled = false,
                ForeColor = ColorTranslator.FromHtml("#95a5a6")
            };
            menuDrukuj.Click += (s, e) => MessageBox.Show(
                "ℹ Funkcja drukowania będzie dostępna wkrótce.",
                "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            contextMenuDokumenty.Items.Add(menuDrukuj);

            contextMenuDokumenty.Opening += ContextMenuDokumenty_Opening;

            dataGridViewOdbiorcy.ContextMenuStrip = contextMenuDokumenty;
        }

        private void ContextMenuDokumenty_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            bool czyZaznaczono = dataGridViewOdbiorcy.SelectedRows.Count > 0 &&
                                 !Convert.ToBoolean(dataGridViewOdbiorcy.SelectedRows[0].Cells["IsGroupRow"].Value) &&
                                 dataGridViewOdbiorcy.SelectedRows[0].Cells["ID"].Value != DBNull.Value;

            foreach (ToolStripItem item in contextMenuDokumenty.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    if (menuItem.Text.Contains("Podgląd") || menuItem.Text.Contains("analiza"))
                    {
                        menuItem.Enabled = czyZaznaczono;
                    }
                }
            }
        }

        private void MenuPodglad_Click(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewOdbiorcy.SelectedRows[0];

                if (!Convert.ToBoolean(selectedRow.Cells["IsGroupRow"].Value) &&
                    selectedRow.Cells["ID"].Value != DBNull.Value)
                {
                    int idDokumentu = Convert.ToInt32(selectedRow.Cells["ID"].Value);
                    string numerDokumentu = selectedRow.Cells["NumerDokumentu"].Value?.ToString() ?? "Nieznany";

                    using (var szczegoly = new SzczegolyDokumentuForm(connectionString, idDokumentu, numerDokumentu))
                    {
                        szczegoly.ShowDialog(this);
                    }
                }
            }
        }

        private void MenuAnaliza_Click(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewOdbiorcy.SelectedRows[0];
                if (!Convert.ToBoolean(selectedRow.Cells["IsGroupRow"].Value) &&
                    selectedRow.Cells["khid"].Value != DBNull.Value)
                {
                    int idKontrahenta = Convert.ToInt32(selectedRow.Cells["khid"].Value);
                    string nazwaKontrahenta = selectedRow.Cells["NazwaFirmy"].Value?.ToString() ?? "Nieznany";

                    int? towarId = null;
                    if (radioTowarSwieze.Checked && comboBoxTowarSwieze.SelectedValue != null && (int)comboBoxTowarSwieze.SelectedValue != 0)
                        towarId = (int)comboBoxTowarSwieze.SelectedValue;
                    else if (radioTowarMrozone.Checked && comboBoxTowarMrozone.SelectedValue != null && (int)comboBoxTowarMrozone.SelectedValue != 0)
                        towarId = (int)comboBoxTowarMrozone.SelectedValue;

                    string nazwaTowaru = towarId.HasValue ?
                        (radioTowarSwieze.Checked ? comboBoxTowarSwieze.Text : comboBoxTowarMrozone.Text) :
                        "Wszystkie towary";

                    using (var analizaForm = new AnalizaTygodniowaForm(
                        connectionString,
                        idKontrahenta,
                        towarId,
                        nazwaKontrahenta,
                        nazwaTowaru))
                    {
                        analizaForm.ShowDialog(this);
                    }
                }
            }
        }

        private void KonfigurujDataGridViewPozycje()
        {
            dataGridViewPozycje.AutoGenerateColumns = false;
            dataGridViewPozycje.Columns.Clear();
            dataGridViewPozycje.ReadOnly = true;
            dataGridViewPozycje.AllowUserToAddRows = false;
            dataGridViewPozycje.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewPozycje.MultiSelect = false;

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Lp",
                DataPropertyName = "Lp",
                HeaderText = "#",
                Width = 50,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KodTowaru",
                DataPropertyName = "KodTowaru",
                HeaderText = "📦 Kod Towaru",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ilosc",
                DataPropertyName = "Ilosc",
                HeaderText = "⚖ Ilosc",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Cena",
                DataPropertyName = "Cena",
                HeaderText = "💵 Cena Netto",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPozycje.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Wartosc",
                DataPropertyName = "Wartosc",
                HeaderText = "💰 Wartosc Netto",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
        }

        private void KonfigurujDataGridViewPlatnosci()
        {
            dataGridViewPlatnosci.AutoGenerateColumns = false;
            dataGridViewPlatnosci.Columns.Clear();
            dataGridViewPlatnosci.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewPlatnosci.MultiSelect = false;
            dataGridViewPlatnosci.ReadOnly = true;
            dataGridViewPlatnosci.AllowUserToAddRows = false;
            dataGridViewPlatnosci.AllowUserToDeleteRows = false;
            dataGridViewPlatnosci.RowHeadersVisible = false;

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Kontrahent",
                DataPropertyName = "Kontrahent",
                HeaderText = "🏢 Kontrahent",
                Width = 180,
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Limit",
                DataPropertyName = "Limit",
                HeaderText = "💳 Limit",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DoZaplacenia",
                DataPropertyName = "DoZaplacenia",
                HeaderText = "💰 Do zapłacenia",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PrzekroczonyLimit",
                DataPropertyName = "PrzekroczonyLimit",
                HeaderText = "⚠ Przekroczony limit",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Terminowe",
                DataPropertyName = "Terminowe",
                HeaderText = "✓ Terminowe",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Przeterminowane",
                DataPropertyName = "Przeterminowane",
                HeaderText = "⏰ Przeterminowane",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
        }

        private void ZaladujTowary()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "SELECT [ID], [kod] FROM [HANDEL].[HM].[TW] WHERE katalog = 67095 ORDER BY Kod ASC";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable towary = new DataTable();
                    adapter.Fill(towary);
                    DataRow dr = towary.NewRow();
                    dr["ID"] = 0;
                    dr["kod"] = "--- Wszystkie towary ---";
                    towary.Rows.InsertAt(dr, 0);
                    comboBoxTowarSwieze.DisplayMember = "kod";
                    comboBoxTowarSwieze.ValueMember = "ID";
                    comboBoxTowarSwieze.DataSource = towary;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "SELECT [ID], [kod] FROM [HANDEL].[HM].[TW] WHERE katalog = 67153 ORDER BY Kod ASC";
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable towary = new DataTable();
                    adapter.Fill(towary);
                    DataRow dr = towary.NewRow();
                    dr["ID"] = 0;
                    dr["kod"] = "--- Wszystkie towary ---";
                    towary.Rows.InsertAt(dr, 0);
                    comboBoxTowarMrozone.DisplayMember = "kod";
                    comboBoxTowarMrozone.ValueMember = "ID";
                    comboBoxTowarMrozone.DataSource = towary;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd ładowania towarów: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPlatnosciPerKontrahent(string? handlowiec)
        {
            var zaznaczeniHandlowcy = PobierzZaznaczonychHandlowcow();

            string sql = @"
WITH PNAgg AS (
    SELECT PN.dkid,
           SUM(ISNULL(PN.kwotarozl,0)) AS KwotaRozliczona,
           MAX(PN.Termin)              AS TerminPrawdziwy
    FROM [HANDEL].[HM].[PN] PN
    GROUP BY PN.dkid
),
Dokumenty AS (
    SELECT DISTINCT DK.id, DK.khid, DK.walbrutto, DK.plattermin
    FROM [HANDEL].[HM].[DK] DK
    WHERE DK.anulowany = 0";

            if (zaznaczeniHandlowcy.Count > 0)
            {
                var handlowcyLista = string.Join("','", zaznaczeniHandlowcy.Select(h => h.Replace("'", "''")));
                sql += $@"
      AND EXISTS (
          SELECT 1
          FROM [HANDEL].[SSCommon].[ContractorClassification] W
          WHERE W.ElementId = DK.khid
            AND W.CDim_Handlowiec_Val IN ('{handlowcyLista}')
      )";
            }

            sql += @"
),
Saldo AS (
    SELECT D.khid,
           (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) AS DoZaplacenia,
           ISNULL(PA.TerminPrawdziwy, D.plattermin)     AS TerminPlatnosci
    FROM Dokumenty D
    LEFT JOIN PNAgg PA ON PA.dkid = D.id
)
SELECT 
    C.Shortcut AS Kontrahent,
    C.LimitAmount AS Limit,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS DoZaplacenia,
    CAST(C.LimitAmount - SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS PrzekroczonyLimit,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() <= S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Terminowe,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() >  S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane
FROM Saldo S
JOIN [HANDEL].[SSCommon].[STContractors] C ON C.id = S.khid
GROUP BY C.Shortcut, C.LimitAmount
HAVING SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) > 0.01
ORDER BY DoZaplacenia DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@pHandlowiec", DBNull.Value);

                    var dt = new DataTable();
                    new SqlDataAdapter(cmd).Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        decimal sumaLimit = 0m, sumaDoZap = 0m, sumaPrzekr = 0m, sumaTerm = 0m, sumaPrzet = 0m;

                        foreach (DataRow r in dt.Rows)
                        {
                            if (r["Limit"] != DBNull.Value) sumaLimit += Convert.ToDecimal(r["Limit"]);
                            if (r["DoZaplacenia"] != DBNull.Value) sumaDoZap += Convert.ToDecimal(r["DoZaplacenia"]);
                            if (r["PrzekroczonyLimit"] != DBNull.Value) sumaPrzekr += Convert.ToDecimal(r["PrzekroczonyLimit"]);
                            if (r["Terminowe"] != DBNull.Value) sumaTerm += Convert.ToDecimal(r["Terminowe"]);
                            if (r["Przeterminowane"] != DBNull.Value) sumaPrzet += Convert.ToDecimal(r["Przeterminowane"]);
                        }

                        var sumaRow = dt.NewRow();
                        sumaRow["Kontrahent"] = "📊 SUMA";
                        sumaRow["Limit"] = Math.Round(sumaLimit, 2);
                        sumaRow["DoZaplacenia"] = Math.Round(sumaDoZap, 2);
                        sumaRow["PrzekroczonyLimit"] = Math.Round(sumaPrzekr, 2);
                        sumaRow["Terminowe"] = Math.Round(sumaTerm, 2);
                        sumaRow["Przeterminowane"] = Math.Round(sumaPrzet, 2);

                        dt.Rows.InsertAt(sumaRow, 0);
                    }

                    dataGridViewPlatnosci.DataSource = dt;

                    dataGridViewPlatnosci.CellFormatting -= DataGridViewPlatnosci_CellFormatting;
                    dataGridViewPlatnosci.CellFormatting += DataGridViewPlatnosci_CellFormatting;
                    dataGridViewPlatnosci.RowPrePaint -= DataGridViewPlatnosci_RowPrePaint;
                    dataGridViewPlatnosci.RowPrePaint += DataGridViewPlatnosci_RowPrePaint;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd podczas wczytywania płatności: " + ex.Message,
                                "Błąd bazy danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DataGridViewPlatnosci_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridViewPlatnosci.Rows.Count) return;

            var row = dataGridViewPlatnosci.Rows[e.RowIndex];

            if (row.Cells["Kontrahent"].Value?.ToString() == "📊 SUMA")
            {
                row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#d5dbdb");
                row.DefaultCellStyle.Font = new Font(dataGridViewPlatnosci.Font, FontStyle.Bold);
            }
            else if (row.Cells["PrzekroczonyLimit"].Value != null &&
                     row.Cells["PrzekroczonyLimit"].Value != DBNull.Value)
            {
                decimal przekroczony = Convert.ToDecimal(row.Cells["PrzekroczonyLimit"].Value);
                if (przekroczony < 0)
                {
                    row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#ffcccc");
                    row.DefaultCellStyle.ForeColor = Color.DarkRed;
                }
            }
        }

        private void DataGridViewPlatnosci_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value != null && e.ColumnIndex >= 0)
            {
                var colName = dataGridViewPlatnosci.Columns[e.ColumnIndex].Name;
                if (colName == "Limit" || colName == "DoZaplacenia" || colName == "PrzekroczonyLimit" ||
                    colName == "Terminowe" || colName == "Przeterminowane")
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        e.Value = val.ToString("N2") + " zł";
                        e.FormattingApplied = true;
                    }
                }
            }
        }

        private void ZaladujKontrahentow()
        {
            var zaznaczeniHandlowcy = PobierzZaznaczonychHandlowcow();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT DISTINCT C.id, C.shortcut AS nazwa
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId";

                if (zaznaczeniHandlowcy.Count > 0)
                {
                    var handlowcyLista = string.Join("','", zaznaczeniHandlowcy.Select(h => h.Replace("'", "''")));
                    query += $" WHERE WYM.CDim_Handlowiec_Val IN ('{handlowcyLista}')";
                }

                query += " ORDER BY C.shortcut ASC;";

                var cmd = new SqlCommand(query, connection);

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable kontrahenci = new DataTable();
                adapter.Fill(kontrahenci);

                DataRow dr = kontrahenci.NewRow();
                dr["id"] = 0;
                dr["nazwa"] = "--- Wszyscy kontrahenci ---";
                kontrahenci.Rows.InsertAt(dr, 0);
                comboBoxKontrahent.DisplayMember = "nazwa";
                comboBoxKontrahent.ValueMember = "id";
                comboBoxKontrahent.DataSource = kontrahenci;
            }
        }

        private void WczytajDokumentySprzedazy(int? towarId, int? kontrahentId)
        {
            var zaznaczeniHandlowcy = PobierzZaznaczonychHandlowcow();

            string query = @"
DECLARE @TowarID INT = @pTowarID;
DECLARE @KontrahentID INT = @pKontrahentID;
DECLARE @DataOd DATE = @pDataOd;
DECLARE @DataDo DATE = @pDataDo;

WITH AgregatyDokumentu AS (
    SELECT super AS id_dk, SUM(ilosc) AS SumaKG, SUM(wartNetto) / NULLIF(SUM(ilosc), 0) AS SredniaCena
    FROM [HANDEL].[HM].[DP] WHERE @TowarID IS NULL OR idtw = @TowarID GROUP BY super
),
DokumentyFiltrowane AS (
    SELECT DISTINCT DK.*, WYM.CDim_Handlowiec_Val 
    FROM [HANDEL].[HM].[DK] DK
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE (@KontrahentID IS NULL OR DK.khid = @KontrahentID)
        AND (@TowarID IS NULL OR EXISTS (SELECT 1 FROM [HANDEL].[HM].[DP] DP WHERE DP.super = DK.id AND DP.idtw = @TowarID))
        AND DK.data >= @DataOd
        AND DK.data <= @DataDo";

            if (zaznaczeniHandlowcy.Count > 0)
            {
                var handlowcyLista = string.Join("','", zaznaczeniHandlowcy.Select(h => h.Replace("'", "''")));
                query += $" AND WYM.CDim_Handlowiec_Val IN ('{handlowcyLista}')";
            }

            query += @"
)
SELECT 
    CONVERT(date, DF.data) AS SortDate, 1 AS SortOrder, 0 AS IsGroupRow,
    DF.kod AS NumerDokumentu, C.shortcut AS NazwaFirmy,
    ISNULL(AD.SumaKG, 0) AS IloscKG, ISNULL(AD.SredniaCena, 0) AS SredniaCena,
    ISNULL(DF.CDim_Handlowiec_Val, '-') AS Handlowiec, DF.khid, DF.id
FROM DokumentyFiltrowane DF
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DF.khid = C.id
INNER JOIN AgregatyDokumentu AD ON DF.id = AD.id_dk
UNION ALL
SELECT DISTINCT
    CONVERT(date, data) AS SortDate, 0 AS SortOrder, 1 AS IsGroupRow,
    NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM DokumentyFiltrowane
ORDER BY SortDate DESC, SortOrder ASC, SredniaCena DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@pTowarID", (object)towarId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pKontrahentID", (object)kontrahentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@pDataOd", dateTimePickerOd.Value.Date);
                    cmd.Parameters.AddWithValue("@pDataDo", dateTimePickerDo.Value.Date.AddDays(1).AddSeconds(-1));

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    dataGridViewOdbiorcy.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd podczas wczytywania dokumentów: " + ex.Message, "Błąd Bazy Danych", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void WczytajPozycjeDokumentu(int idDokumentu)
        {
            string query = @"
                SELECT 
                    DP.lp AS Lp,
                    DP.kod AS KodTowaru, 
                    DP.ilosc AS Ilosc, 
                    DP.cena AS Cena, 
                    DP.wartNetto AS Wartosc 
                FROM [HANDEL].[HM].[DP] DP 
                WHERE DP.super = @idDokumentu 
                ORDER BY DP.lp;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDokumentu", idDokumentu);
                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        decimal sumaIlosc = 0;
                        decimal sumaWartosc = 0;

                        foreach (DataRow row in dt.Rows)
                        {
                            sumaIlosc += Convert.ToDecimal(row["Ilosc"]);
                            sumaWartosc += Convert.ToDecimal(row["Wartosc"]);
                        }

                        var sumaRow = dt.NewRow();
                        sumaRow["Lp"] = DBNull.Value;
                        sumaRow["KodTowaru"] = "📊 SUMA";
                        sumaRow["Ilosc"] = sumaIlosc;
                        sumaRow["Cena"] = DBNull.Value;
                        sumaRow["Wartosc"] = sumaWartosc;
                        dt.Rows.Add(sumaRow);
                    }

                    dataGridViewPozycje.DataSource = dt;

                    if (dataGridViewPozycje.Rows.Count > 0)
                    {
                        int lastIndex = dataGridViewPozycje.Rows.Count - 1;
                        dataGridViewPozycje.Rows[lastIndex].DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#d5dbdb");
                        dataGridViewPozycje.Rows[lastIndex].DefaultCellStyle.Font = new Font(dataGridViewPozycje.Font, FontStyle.Bold);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd podczas wczytywania pozycji: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdswiezDaneGlownejSiatki()
        {
            if (isDataLoading) return;

            int? selectedTowarId = null;
            if (radioTowarSwieze.Checked && comboBoxTowarSwieze.SelectedValue != null && (int)comboBoxTowarSwieze.SelectedValue != 0)
                selectedTowarId = (int)comboBoxTowarSwieze.SelectedValue;
            else if (radioTowarMrozone.Checked && comboBoxTowarMrozone.SelectedValue != null && (int)comboBoxTowarMrozone.SelectedValue != 0)
                selectedTowarId = (int)comboBoxTowarMrozone.SelectedValue;

            int? selectedKontrahentId = (comboBoxKontrahent.SelectedValue != null && (int)comboBoxKontrahent.SelectedValue != 0) ? (int?)comboBoxKontrahent.SelectedValue : null;
            WczytajDokumentySprzedazy(selectedTowarId, selectedKontrahentId);
        }

        private void comboBoxKontrahent_SelectedIndexChanged(object? sender, EventArgs e)
        {
            OdswiezDaneGlownejSiatki();
        }

        private void DataGridViewDokumenty_SelectionChanged(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count == 0 ||
                Convert.ToBoolean(dataGridViewOdbiorcy.SelectedRows[0].Cells["IsGroupRow"].Value))
            {
                dataGridViewPozycje.DataSource = null;
                return;
            }

            DataGridViewRow selectedRow = dataGridViewOdbiorcy.SelectedRows[0];

            if (selectedRow.Cells["ID"].Value != DBNull.Value)
            {
                int idDokumentu = Convert.ToInt32(selectedRow.Cells["ID"].Value);
                WczytajPozycjeDokumentu(idDokumentu);
            }
        }

        private void DataGridViewOdbiorcy_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (Convert.ToBoolean(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["IsGroupRow"].Value))
            {
                var row = dataGridViewOdbiorcy.Rows[e.RowIndex];
                row.DefaultCellStyle.BackColor = Color.FromArgb(220, 220, 220);
                row.DefaultCellStyle.ForeColor = Color.Black;
                row.DefaultCellStyle.Font = new Font(dataGridViewOdbiorcy.Font, FontStyle.Bold);
                row.Height = 30;
                row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
                row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor;
            }
        }

        private void DataGridViewOdbiorcy_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (Convert.ToBoolean(dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["IsGroupRow"].Value))
            {
                if (e.ColumnIndex == dataGridViewOdbiorcy.Columns["NumerDokumentu"].Index)
                {
                    if (dataGridViewOdbiorcy.Rows[e.RowIndex].Cells["SortDate"].Value is DateTime dateValue)
                    {
                        e.Value = "📅 " + dateValue.ToString("dddd, dd MMMM yyyy", new CultureInfo("pl-PL"));
                    }
                }
                else
                {
                    e.Value = "";
                    e.FormattingApplied = true;
                }
            }
        }
    }

    public class FormWyborHandlowcow : Form
    {
        private CheckedListBox checkedListBox;
        private Button btnOK;
        private Button btnAnuluj;
        private Button btnWszyscy;
        private Button btnZaden;
        public List<string> WybraniHandlowcy { get; private set; }

        public FormWyborHandlowcow(string connectionString, List<string> aktualnieZaznaczeni, List<string> dozwoleniHandlowcy = null)
        {
            this.Text = "👥 Wybierz handlowców";
            this.Size = new Size(350, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lblInfo = new Label
            {
                Text = "✓ Zaznacz handlowców do filtrowania:",
                Location = new Point(10, 10),
                Size = new Size(320, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            this.Controls.Add(lblInfo);

            checkedListBox = new CheckedListBox
            {
                Location = new Point(10, 35),
                Size = new Size(310, 250),
                CheckOnClick = true
            };

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    string query = @"
                    SELECT DISTINCT WYM.CDim_Handlowiec_Val
                    FROM [HANDEL].[SSCommon].[ContractorClassification] WYM
                    WHERE WYM.CDim_Handlowiec_Val IS NOT NULL
                    ORDER BY WYM.CDim_Handlowiec_Val";

                    var cmd = new SqlCommand(query, conn);
                    conn.Open();
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        string nazwa = reader.GetString(0);

                        // Jeśli dozwoleniHandlowcy == null, to admin widzi wszystkich
                        // Jeśli dozwoleniHandlowcy != null, pokazuj tylko dozwolonych
                        if (dozwoleniHandlowcy == null || dozwoleniHandlowcy.Contains(nazwa))
                        {
                            checkedListBox.Items.Add(nazwa);
                        }
                    }
                }
            }
            catch { }

            if (aktualnieZaznaczeni != null && aktualnieZaznaczeni.Count > 0)
            {
                for (int i = 0; i < checkedListBox.Items.Count; i++)
                {
                    if (aktualnieZaznaczeni.Contains(checkedListBox.Items[i].ToString()))
                        checkedListBox.SetItemChecked(i, true);
                }
            }
            else
            {
                for (int i = 0; i < checkedListBox.Items.Count; i++)
                    checkedListBox.SetItemChecked(i, true);
            }

            this.Controls.Add(checkedListBox);

            btnWszyscy = new Button
            {
                Text = "✓ Wszyscy",
                Location = new Point(10, 295),
                Size = new Size(75, 25)
            };
            btnWszyscy.Click += (s, e) => {
                for (int i = 0; i < checkedListBox.Items.Count; i++)
                    checkedListBox.SetItemChecked(i, true);
            };
            this.Controls.Add(btnWszyscy);

            btnZaden = new Button
            {
                Text = "✗ Żaden",
                Location = new Point(95, 295),
                Size = new Size(75, 25)
            };
            btnZaden.Click += (s, e) => {
                for (int i = 0; i < checkedListBox.Items.Count; i++)
                    checkedListBox.SetItemChecked(i, false);
            };
            this.Controls.Add(btnZaden);

            btnOK = new Button
            {
                Text = "✓ OK",
                Location = new Point(165, 330),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK,
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) => {
                WybraniHandlowcy = new List<string>();
                foreach (var item in checkedListBox.CheckedItems)
                    WybraniHandlowcy.Add(item.ToString());
            };
            this.Controls.Add(btnOK);

            btnAnuluj = new Button
            {
                Text = "✗ Anuluj",
                Location = new Point(245, 330),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel,
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAnuluj.FlatAppearance.BorderSize = 0;
            this.Controls.Add(btnAnuluj);

            this.AcceptButton = btnOK;
            this.CancelButton = btnAnuluj;
        }
    }
}