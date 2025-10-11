using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Globalization;
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

        private Chart? chartRoznicaCen;


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

            // ===== DODAJ TO NA KOŃCU =====
            // Zmniejsz szerokość lewego panelu z fakturami
            splitContainerMain.SplitterDistance = 900;  // Zmień z 850 na 600 (lub inną wartość)
                                                        // ==============================
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

            // Panel z kontrolkami filtrów - ZWIĘKSZONA WYSOKOŚĆ dla 2 rzędów
            Panel panelControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,  // ZMIANA: zwiększone dla 2 rzędów
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(10)
            };

            // PIERWSZY RZĄD - Filtry
            Label lblTowar = new Label
            {
                Text = "📦 Towar:",
                Location = new Point(10, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            comboBoxTowarAnalizaCen = new ComboBox
            {
                Location = new Point(85, 10),
                Size = new Size(250, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            WypelnijTowaryAnalizaCen(comboBoxTowarAnalizaCen);
            comboBoxTowarAnalizaCen.SelectedIndexChanged += (s, e) => OdswiezAnalizeCen();

            Label lblDataOd = new Label
            {
                Text = "📅 Od:",
                Location = new Point(355, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            dateTimePickerAnalizaOd = new DateTimePicker
            {
                Location = new Point(430, 10),
                Size = new Size(110, 23),
                Format = DateTimePickerFormat.Short,
                Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                Font = new Font("Segoe UI", 9F)
            };
            dateTimePickerAnalizaOd.ValueChanged += (s, e) => OdswiezAnalizeCen();

            Label lblDataDo = new Label
            {
                Text = "📅 Do:",
                Location = new Point(555, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            dateTimePickerAnalizaDo = new DateTimePicker
            {
                Location = new Point(630, 10),
                Size = new Size(110, 23),
                Format = DateTimePickerFormat.Short,
                Value = DateTime.Today,
                Font = new Font("Segoe UI", 9F)
            };
            dateTimePickerAnalizaDo.ValueChanged += (s, e) => OdswiezAnalizeCen();

            // DRUGI RZĄD - Przyciski
            Button btnOdswiez = new Button
            {
                Text = "🔄 Odśwież",
                Location = new Point(10, 45),  // ZMIANA: drugi rząd
                Size = new Size(100, 28),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnOdswiez.FlatAppearance.BorderSize = 0;
            btnOdswiez.Click += (s, e) => OdswiezAnalizeCen();

            Button btnEksportuj = new Button
            {
                Text = "📊 Eksportuj",
                Location = new Point(120, 45),  // ZMIANA: drugi rząd
                Size = new Size(110, 28),
                BackColor = ColorTranslator.FromHtml("#27ae60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnEksportuj.FlatAppearance.BorderSize = 0;
            btnEksportuj.Click += BtnEksportujAnalize_Click;

            Button btnInstrukcja = new Button
            {
                Text = "📖 Instrukcja",
                Location = new Point(240, 45),  // ZMIANA: drugi rząd
                Size = new Size(110, 28),
                BackColor = ColorTranslator.FromHtml("#9b59b6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnInstrukcja.FlatAppearance.BorderSize = 0;
            btnInstrukcja.Click += BtnInstrukcja_Click;

            panelControls.Controls.Add(lblTowar);
            panelControls.Controls.Add(comboBoxTowarAnalizaCen);
            panelControls.Controls.Add(lblDataOd);
            panelControls.Controls.Add(dateTimePickerAnalizaOd);
            panelControls.Controls.Add(lblDataDo);
            panelControls.Controls.Add(dateTimePickerAnalizaDo);
            panelControls.Controls.Add(btnOdswiez);
            panelControls.Controls.Add(btnEksportuj);
            panelControls.Controls.Add(btnInstrukcja);

            // Panel z informacją - ŻÓŁTY, porównanie dzień do dnia
            Panel panelTrend = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,  // ZMIANA: zwiększona wysokość
                BackColor = ColorTranslator.FromHtml("#fff9c4"),  // ZMIANA: żółty kolor
                Padding = new Padding(10)
            };

            lblTrendInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "📊 Wybierz towar aby zobaczyć porównanie cen dzień do dnia",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),  // ZMIANA: większa czcionka
                ForeColor = ColorTranslator.FromHtml("#f57f17"),  // ZMIANA: ciemny pomarańczowy
                TextAlign = ContentAlignment.MiddleLeft  // ZMIANA: wyrównanie do lewej
            };
            panelTrend.Controls.Add(lblTrendInfo);

            dataGridViewAnalizaCen = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 32 }
            };
            KonfigurujDataGridViewAnalizaCen();

            dataGridViewAnalizaCen.CellDoubleClick += DataGridViewAnalizaCen_CellDoubleClick;

            mainPanel.Controls.Add(dataGridViewAnalizaCen);
            mainPanel.Controls.Add(panelTrend);
            mainPanel.Controls.Add(panelControls);
        }        // NOWA METODA: Obsługa przycisku instrukcji
        private void BtnInstrukcja_Click(object? sender, EventArgs e)
        {
            string instrukcja = @"📖 INSTRUKCJA ODCZYTU ANALIZY CEN

👤 HANDLOWIEC
   Nazwa handlowca odpowiedzialnego za sprzedaż

📅 WCZORAJSZA ŚR. CENA
   Średnia cena z wczorajszego dnia handlowego (zł/kg)

📅 DZISIEJSZA ŚR. CENA
   Średnia cena z dzisiejszego dnia (zł/kg)

± ZMIANA
   Różnica między dzisiejszą a wczorajszą ceną
   🟢 Zielony = cena wzrosła (dobrze dla sprzedawcy)
   🔴 Czerwony = cena spadła

± ZMIANA %
   Procentowa zmiana ceny względem wczoraj
   Pokazuje skalę zmiany w procentach

💵 ŚR. OKRES
   Średnia cena w całym wybranym okresie

⬇ MIN
   Najniższa cena w okresie

⬆ MAX
   Najwyższa cena w okresie

🔢 TRANS.
   Liczba transakcji w okresie

📊 TREND
   Trend długoterminowy w okresie
   🟢 Dodatni = ceny rosną
   🔴 Ujemny = ceny spadają

± ODCH.
   Odchylenie od średniej rynkowej
   >0% = drożej niż konkurencja
   <0% = taniej niż konkurencja

📊 ŚREDNIA RYNKOWA (pierwszy wiersz)
   Zagregowane dane ze wszystkich handlowców

💡 WSKAZÓWKI:
   • Dwukrotnie kliknij wiersz aby zobaczyć szczegóły
   • Żółty panel pokazuje porównanie dzień do dnia
   • Zielone liczby = wzrost cen (korzystne)
   • Czerwone liczby = spadek cen (uwaga!)";

            MessageBox.Show(instrukcja, "📖 Instrukcja analizy cen",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void DataGridViewAnalizaCen_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || comboBoxTowarAnalizaCen.SelectedValue == null)
                return;

            var row = dataGridViewAnalizaCen.Rows[e.RowIndex];

            // Sprawdź czy to nie wiersz sumy
            if (row.Cells["Handlowiec"].Value?.ToString() == "📊 ŚREDNIA RYNKOWA")
                return;

            string handlowiec = row.Cells["Handlowiec"].Value?.ToString();
            if (string.IsNullOrEmpty(handlowiec))
                return;

            int towarId = (int)comboBoxTowarAnalizaCen.SelectedValue;
            string nazwaTowaru = comboBoxTowarAnalizaCen.Text;

            using (var historiaForm = new FormHistoriaCen(
                connectionString,
                towarId,
                nazwaTowaru,
                handlowiec,
                dateTimePickerAnalizaOd.Value,
                dateTimePickerAnalizaDo.Value))
            {
                historiaForm.ShowDialog(this);
            }
        }
        // === FRAGMENT TWORZĄCY ZAKŁADKĘ (JEŚLI JUŻ MASZ - ZAMIEŃ NA TO) ===
        private void StworzZakladkePorownaniaSwiezeMrozone(TabPage tab)
        {
            Panel mainPanel = new Panel { Dock = DockStyle.Fill };
            tab.Controls.Add(mainPanel);

            Panel panelControls = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(8)
            };

            var lblTowar1 = new Label { Text = "📦 Towar 1:", AutoSize = true, Location = new Point(8, 12) };
            var comboBoxTowar1 = new ComboBox
            {
                Name = "comboBoxTowar1",
                Location = new Point(80, 8),
                Size = new Size(230, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijWszystkieTowaryPorown(comboBoxTowar1);
            comboBoxTowar1.SelectedIndexChanged += (s, e) => OdswiezPorownanieTowarow();

            var lblTowar2 = new Label { Text = "📦 Towar 2:", AutoSize = true, Location = new Point(324, 12) };
            var comboBoxTowar2 = new ComboBox
            {
                Name = "comboBoxTowar2",
                Location = new Point(396, 8),
                Size = new Size(230, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            WypelnijWszystkieTowaryPorown(comboBoxTowar2);
            comboBoxTowar2.SelectedIndexChanged += (s, e) => OdswiezPorownanieTowarow();

            var btnEksport = new Button
            {
                Text = "📊 Eksportuj",
                Location = new Point(640, 8),
                Size = new Size(110, 24),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnEksport.FlatAppearance.BorderSize = 0;
            btnEksport.Click += BtnEksportujPorownanieClick;

            panelControls.Controls.AddRange(new Control[] { lblTowar1, comboBoxTowar1, lblTowar2, comboBoxTowar2, btnEksport });

            Panel panelStat = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = ColorTranslator.FromHtml("#d5f4e6"),
                Padding = new Padding(6)
            };
            lblStatystykiPorown = new Label
            {
                Dock = DockStyle.Fill,
                Text = "⚖ Wybierz dwa towary aby zobaczyć porównanie cen",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#27ae60"),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panelStat.Controls.Add(lblStatystykiPorown);

            // ✅ CheckBox: ukryj dni z zerową sprzedażą
            var chkUkryjZerowe = new CheckBox
            {
                Name = "chkUkryjZerowePorownanie",
                Text = "Ukryj dni z zerową sprzedażą",
                AutoSize = true,
                Location = new Point(760, 12),
                Checked = true,
                Cursor = Cursors.Hand
            };

            var chkUkryj = new CheckBox
            {
                Name = "chkUkryjZerowePorownanie",
                Text = "Ukryj wiersze z 0 kg",
                AutoSize = true,
                Location = new Point(760, 12)
            };
            chkUkryj.CheckedChanged += (s, e) => ZastosujFiltrZerowychWPorownaniu();
            chkUkryjZerowe.CheckedChanged += (s, e) =>
            {
                ZastosujFiltrZerowychWPorownaniu();   // filtruj dane siatki
                AktualizujWykresRoznicy();            // przerysuj wykres
            };
            panelControls.Controls.Add(chkUkryjZerowe);


            dataGridViewPorownaniaSwiezeMrozone = new DataGridView { Dock = DockStyle.Fill };
            KonfigurujDataGridViewPorownaniaSwiezeMrozone();

            // === WYKRES RÓŻNICY CEN (zł/kg) ===
            var panelChart = new Panel { Dock = DockStyle.Bottom, Height = 220, Padding = new Padding(8, 4, 8, 8), BackColor = Color.White };
            chartRoznicaCen = new Chart { Dock = DockStyle.Fill, BorderlineDashStyle = ChartDashStyle.Solid, BorderlineColor = Color.Gainsboro };

            var ca = new ChartArea("ca");
            ca.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            ca.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            ca.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            ca.AxisX.LabelStyle.Angle = -15;
            ca.AxisX.LabelStyle.Format = "yyyy-MMMM-dd dddd"; // 👈 format jak w siatce
            ca.AxisX.IsMarginVisible = true;
            ca.AxisY.Title = "Różnica (zł/kg)";

            var zero = new StripLine { Interval = 0, StripWidth = 0, BorderWidth = 1, BorderColor = Color.Gray, BorderDashStyle = ChartDashStyle.Dash };
            ca.AxisY.StripLines.Add(zero);

            chartRoznicaCen.ChartAreas.Add(ca);

            var s = new Series("Różnica zł/kg")
            {
                ChartType = SeriesChartType.Line,
                XValueType = ChartValueType.DateTime,
                YValueType = ChartValueType.Double,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = 5,
                IsValueShownAsLabel = false,
            };
            chartRoznicaCen.Series.Add(s);

            panelChart.Controls.Add(chartRoznicaCen);
            tab.Controls.Add(panelChart);

            mainPanel.Controls.Add(dataGridViewPorownaniaSwiezeMrozone);
            mainPanel.Controls.Add(panelStat);
            mainPanel.Controls.Add(panelControls);
        }

        private void AktualizujWykresRoznicy()
        {
            if (chartRoznicaCen == null) return;

            // Pobierz aktualne dane z grida (po filtrze)
            DataTable? dt = null;

            // Grid może trzymać DataTable po ToTable(); zrób bezpieczny cast
            if (dataGridViewPorownaniaSwiezeMrozone.DataSource is DataTable t)
                dt = t;
            else if (dataGridViewPorownaniaSwiezeMrozone.DataSource is DataView dv)
                dt = dv.ToTable();

            var series = chartRoznicaCen.Series[0];
            series.Points.Clear();

            if (dt == null || dt.Rows.Count == 0) return;

            // Upewnij się o sortowaniu rosnącym
            var rows = dt.AsEnumerable()
                         .Where(r => r["Data"] != DBNull.Value && r["RoznicaZl"] != DBNull.Value)
                         .OrderBy(r => r.Field<DateTime>("Data"));

            foreach (var r in rows)
            {
                var d = r.Field<DateTime>("Data");
                var y = Convert.ToDouble(r.Field<decimal>("RoznicaZl"));
                series.Points.AddXY(d, y);
            }

            // Auto-zoom osi Y według danych
            var ca = chartRoznicaCen.ChartAreas[0];
            if (series.Points.Count > 0)
            {
                var ys = series.Points.Select(p => p.YValues[0]);
                double min = ys.Min(), max = ys.Max();
                if (Math.Abs(max - min) < 0.01) { min -= 0.5; max += 0.5; }
                ca.AxisY.Minimum = Math.Floor(min * 10) / 10.0;
                ca.AxisY.Maximum = Math.Ceiling(max * 10) / 10.0;
            }
            else
            {
                ca.AxisY.Minimum = double.NaN;
                ca.AxisY.Maximum = double.NaN;
            }
        }

        private void BtnEksportujPorownanieClick(object? sender, EventArgs e)
        {
            if (dataGridViewPorownaniaSwiezeMrozone.DataSource is DataTable dt && dt.Rows.Count > 0)
            {
                using var sfd = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"Porownanie_{DateTime.Now:yyyyMMdd}.csv" };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var lines = new List<string>();
                    var headers = string.Join(";", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                    lines.Add(headers);
                    foreach (DataRow r in dt.Rows)
                        lines.Add(string.Join(";", r.ItemArray.Select(v => v?.ToString())));
                    System.IO.File.WriteAllLines(sfd.FileName, lines, System.Text.Encoding.UTF8);
                    MessageBox.Show("Zapisano plik CSV.");
                }
            }
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

            // NOWE KOLUMNY - wczoraj i dziś
            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaWczoraj",
                DataPropertyName = "CenaWczoraj",
                HeaderText = "📅 Wczoraj\nśr. cena",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = ColorTranslator.FromHtml("#fff3e0")
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaDzisiaj",
                DataPropertyName = "CenaDzisiaj",
                HeaderText = "📅 Dziś\nśr. cena",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = ColorTranslator.FromHtml("#e8f5e9")
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ZmianaZl",
                DataPropertyName = "ZmianaZl",
                HeaderText = "± Zmiana",
                Width = 85,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ZmianaProcent",
                DataPropertyName = "ZmianaProcent",
                HeaderText = "± Zmiana",
                Width = 75,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SredniaCena",
                DataPropertyName = "SredniaCena",
                HeaderText = "💵 Śr. okres",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MinCena",
                DataPropertyName = "MinCena",
                HeaderText = "⬇ Min",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "MaxCena",
                DataPropertyName = "MaxCena",
                HeaderText = "⬆ Max",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LiczbaTransakcji",
                DataPropertyName = "LiczbaTransakcji",
                HeaderText = "🔢 Trans.",
                Width = 70,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "TrendProcentowy",
                DataPropertyName = "TrendProcentowy",
                HeaderText = "📊 Trend",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OdchylenieOdSredniej",
                DataPropertyName = "OdchylenieOdSredniej",
                HeaderText = "± Odch.",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            dataGridViewAnalizaCen.EnableHeadersVisualStyles = false;
            dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#16a085");
            dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dataGridViewAnalizaCen.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;  // DODANE: zawijanie tekstu
            dataGridViewAnalizaCen.ColumnHeadersHeight = 45;  // ZMIANA: wyższe nagłówki
            dataGridViewAnalizaCen.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#e8f8f5");
            dataGridViewAnalizaCen.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1abc9c");
            dataGridViewAnalizaCen.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridViewAnalizaCen.RowTemplate.Height = 32;

            dataGridViewAnalizaCen.CellFormatting += DataGridViewAnalizaCen_CellFormatting;
        }
        // === KOMPAKTOWE KOLUMNY I WYGLĄD (ZAMIANA DOTYCHCZASOWEJ METODY) ===
        // === PORÓWNANIE ŚWIEŻE vs MROŻONE — KONFIGURACJA GRIDU (KOMPAKT) ===
        private void KonfigurujDataGridViewPorownaniaSwiezeMrozone()
        {
            var dgv = dataGridViewPorownaniaSwiezeMrozone;
            dgv.Columns.Clear();

            dgv.RowHeadersVisible = false;
            dgv.AutoGenerateColumns = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.ReadOnly = true;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;

            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dgv.DataBindingComplete -= Dgv_DataBindingComplete_AutoSize;
            dgv.DataBindingComplete += Dgv_DataBindingComplete_AutoSize;

            dgv.RowTemplate.Height = 26;
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 34;
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#6c5ce7");
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#f4f6fa");

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Data",
                DataPropertyName = "Data",
                HeaderText = "📅 Data",
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy" }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaT1",
                DataPropertyName = "CenaT1",
                HeaderText = "Cena T1 (zł/kg)",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KgT1",
                DataPropertyName = "KgT1",
                HeaderText = "Ilość T1 (kg)",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaT2",
                DataPropertyName = "CenaT2",
                HeaderText = "Cena T2 (zł/kg)",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KgT2",
                DataPropertyName = "KgT2",
                HeaderText = "Ilość T2 (kg)",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RoznicaZl",
                DataPropertyName = "RoznicaZl",
                HeaderText = "± zł",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RoznicaProc",
                DataPropertyName = "RoznicaProc",
                HeaderText = "± %",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N1" }
            });

            // Jednolite końcówki: data / zł / kg / %
            dgv.CellFormatting -= DataGridViewPorownaniaSwiezeMrozone_CellFormatting_Custom;
            dgv.CellFormatting += DataGridViewPorownaniaSwiezeMrozone_CellFormatting_Custom;

            // Dwuklik -> okno dokumentów dnia
            dgv.CellDoubleClick -= DataGridViewPorownaniaSwiezeMrozone_CellDoubleClick;
            dgv.CellDoubleClick += DataGridViewPorownaniaSwiezeMrozone_CellDoubleClick;
        }

        private void Dgv_DataBindingComplete_AutoSize(object? s, DataGridViewBindingCompleteEventArgs e)
        {
            var dgv = s as DataGridView;
            dgv?.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
        }        // Jednolite formatowanie: zł/kg, kg, %
                 // Jednolite formatowanie: zł/kg, kg, %
        private void DataGridViewPorownaniaSwiezeMrozone_CellFormatting_Custom(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = (DataGridView)sender!;
            var col = dgv.Columns[e.ColumnIndex].Name;
            if (e.Value == null || e.Value == DBNull.Value) return;
            var pl = new CultureInfo("pl-PL");

            if (col == "Data" && e.Value is DateTime dt)
            {
                e.Value = dt.ToString("yyyy-MMMM-dd dddd", pl); // RRRR-MMMM-DD DDDD
                e.FormattingApplied = true;
            }
            else if (col is "CenaT1" or "CenaT2" or "RoznicaZl")
            {
                if (decimal.TryParse(e.Value.ToString(), NumberStyles.Any, pl, out var v))
                {
                    e.Value = $"{v:N2} zł/kg";
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    e.FormattingApplied = true;
                    if (col == "RoznicaZl")
                    {
                        if (v > 0) e.CellStyle.ForeColor = Color.DarkGreen;
                        else if (v < 0) e.CellStyle.ForeColor = Color.Firebrick;
                    }
                }
            }
            else if (col is "KgT1" or "KgT2")
            {
                if (decimal.TryParse(e.Value.ToString(), NumberStyles.Any, pl, out var kg))
                {
                    e.Value = $"{kg:N0} kg";
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    e.FormattingApplied = true;
                }
            }
            else if (col == "RoznicaProc")
            {
                if (decimal.TryParse(e.Value.ToString(), NumberStyles.Any, pl, out var p))
                {
                    e.Value = $"{p:N1}%";
                    e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    if (p > 0) e.CellStyle.ForeColor = Color.DarkGreen;
                    else if (p < 0) e.CellStyle.ForeColor = Color.Firebrick;
                    e.FormattingApplied = true;
                }
            }
        }
        private CheckBox? ZnajdzChkUkryjZerowe()
        {
            return tabControl.Controls.Find("chkUkryjZerowePorownanie", true).FirstOrDefault() as CheckBox;
        }

        private void ZastosujFiltrZerowychWPorownaniu()
        {
            var chk = ZnajdzChkUkryjZerowe();

            if (dataGridViewPorownaniaSwiezeMrozone.DataSource is DataTable src)
            {
                var dv = new DataView(src);
                if (chk != null && chk.Checked)
                    dv.RowFilter = "(ISNULL(KgT1,0) > 0) AND (ISNULL(KgT2,0) > 0)";

                dv.Sort = "[Data] ASC"; // najwcześniej → najpóźniej
                dataGridViewPorownaniaSwiezeMrozone.DataSource = dv.ToTable();
                dataGridViewPorownaniaSwiezeMrozone.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
            }
        }
       

        private void KonfigurujDataGridViewSwiezeMrozone()
        {
            var dgv = dataGridViewPorownaniaSwiezeMrozone;
            dgv.Columns.Clear();

            dgv.RowHeadersVisible = false;
            dgv.AutoGenerateColumns = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.ReadOnly = true;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.RowTemplate.Height = 26;
            dgv.GridColor = Color.LightGray;

            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 34;

            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#34495e");
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#f8f9fa");

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Data",
                DataPropertyName = "Data",
                HeaderText = "📅 Data",
                Width = 95,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd.MM.yyyy" }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaT1",
                DataPropertyName = "CenaT1",
                HeaderText = "Cena T1 (zł/kg)",
                Width = 115,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KgT1",
                DataPropertyName = "KgT1",
                HeaderText = "Ilość T1 (kg)",
                Width = 105,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CenaT2",
                DataPropertyName = "CenaT2",
                HeaderText = "Cena T2 (zł/kg)",
                Width = 115,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "KgT2",
                DataPropertyName = "KgT2",
                HeaderText = "Ilość T2 (kg)",
                Width = 105,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RoznicaZl",
                DataPropertyName = "RoznicaZl",
                HeaderText = "Różnica (zł/kg)",
                Width = 125,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
            });

            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "RoznicaProc",
                DataPropertyName = "RoznicaProc",
                HeaderText = "Różnica (%)",
                Width = 105,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N1" }
            });

            // 🎨 Kolorowanie różnic
            dgv.CellFormatting += (s, e) =>
            {
                if (e.Value == null) return;
                var col = dgv.Columns[e.ColumnIndex].Name;
                if (col == "RoznicaZl" || col == "RoznicaProc")
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        if (val > 0) e.CellStyle.ForeColor = Color.DarkGreen;
                        else if (val < 0) e.CellStyle.ForeColor = Color.Firebrick;
                    }
                }
            };

            // 🔄 Dwuklik -> okno dokumentów dnia
            dgv.CellDoubleClick -= DataGridViewPorownaniaSwiezeMrozone_CellDoubleClick;
            dgv.CellDoubleClick += DataGridViewPorownaniaSwiezeMrozone_CellDoubleClick;
        }

        // === OBSŁUGA DWUKLIKU W WIERSZ – OKNO DOKUMENTÓW DNIA DLA OBU TOWARÓW ===
        // === DWUKLIK W WIERSZ – DOKUMENTY DNIA ===
        private void DataGridViewPorownaniaSwiezeMrozone_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var combo1 = tabControl.Controls.Find("comboBoxTowar1", true).FirstOrDefault() as ComboBox;
            var combo2 = tabControl.Controls.Find("comboBoxTowar2", true).FirstOrDefault() as ComboBox;
            if (!(combo1?.SelectedValue is int t1) || !(combo2?.SelectedValue is int t2)) return;

            var row = dataGridViewPorownaniaSwiezeMrozone.Rows[e.RowIndex];
            if (!(row.Cells["Data"].Value is DateTime dzien)) return;

            using var f = new FormDokumentyTowarowDnia(connectionString, dzien, t1, combo1.Text, t2, combo2.Text);
            f.ShowDialog(this);
        }        // === LEKKIE, SAMOWYSTARCZALNE OKNO Z LISTĄ DOKUMENTÓW ===
private sealed class FormDokumentyTowarowDnia : Form
{
    private readonly string _conn;
    private readonly DateTime _dzien;
    private readonly int _t1, _t2;
    private readonly string _n1, _n2;
    private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };

    public FormDokumentyTowarowDnia(string conn, DateTime dzien, int t1, string n1, int t2, string n2)
    {
        _conn = conn; _dzien = dzien; _t1 = t1; _t2 = t2; _n1 = n1; _n2 = n2;

        Text = $"Dokumenty {_dzien:dd.MM.yyyy} – {_n1} + {_n2}";
        Width = 950; Height = 580;
        StartPosition = FormStartPosition.CenterParent;
        Controls.Add(_grid);

        KonfigurujGrid();
        Zaladuj();
    }

    private void KonfigurujGrid()
    {
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowTemplate.Height = 25;
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 32;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#f4f6f7");

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Numer", DataPropertyName="Numer", HeaderText="Kod dokumentu", Width=160 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Data", DataPropertyName="Data", HeaderText="Data", Width=95, DefaultCellStyle=new DataGridViewCellStyle{ Format="yyyy-MM-dd"} });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Kontrahent", DataPropertyName="Kontrahent", HeaderText="Kontrahent", Width=300 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Cena", DataPropertyName="Cena", HeaderText="Cena (zł/kg)", Width=110, DefaultCellStyle=new DataGridViewCellStyle{ Alignment=DataGridViewContentAlignment.MiddleRight, Format="N2"} });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Ilosc", DataPropertyName="Ilosc", HeaderText="Ilość (kg)", Width=100, DefaultCellStyle=new DataGridViewCellStyle{ Alignment=DataGridViewContentAlignment.MiddleRight, Format="N2"} });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="Netto", DataPropertyName="Netto", HeaderText="Wartość (zł)", Width=120, DefaultCellStyle=new DataGridViewCellStyle{ Alignment=DataGridViewContentAlignment.MiddleRight, Format="N2"} });
    }

    private void Zaladuj()
    {
        string sql = @"
SELECT 
    DK.kod AS Numer,                          -- dokładny kod z bazy
    CAST(DK.data AS date) AS Data,
    KH.Name AS Kontrahent,
    CAST(DP.cena AS decimal(18,4)) AS Cena,
    CAST(DP.ilosc AS decimal(18,3)) AS Ilosc,
    CAST(DP.cena * DP.ilosc AS decimal(18,2)) AS Netto
FROM HANDEL.HM.DK DK
JOIN HANDEL.HM.DP DP ON DP.super = DK.id
LEFT JOIN HANDEL.SSCommon.STContractors KH ON KH.Id = DP.idkh
WHERE CAST(DK.data AS date) = @d
  AND DP.idtw IN (@t1,@t2)
  AND DK.anulowany = 0
ORDER BY DK.kod;";

        using var cn = new SqlConnection(_conn);
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@d", _dzien);
        cmd.Parameters.AddWithValue("@t1", _t1);
        cmd.Parameters.AddWithValue("@t2", _t2);

        var dt = new DataTable();
        new SqlDataAdapter(cmd).Fill(dt);
        _grid.DataSource = dt;
    }
}

        private void DataGridViewAnalizaCen_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            var colName = dataGridViewAnalizaCen.Columns[e.ColumnIndex].Name;

            // Formatowanie z końcówkami zł i %
            if (e.Value != null && e.Value != DBNull.Value)
            {
                if (colName == "CenaWczoraj" || colName == "CenaDzisiaj" || colName == "SredniaCena" ||
                    colName == "MinCena" || colName == "MaxCena")
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        e.Value = $"{val:N2} zł";
                        e.FormattingApplied = true;
                    }
                }
                else if (colName == "ZmianaZl")
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        e.Value = $"{val:N2} zł";
                        e.FormattingApplied = true;

                        // ZMIANA: Zielony gdy wzrost, czerwony gdy spadek
                        if (val > 0)
                        {
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#27ae60");  // zielony
                            e.CellStyle.BackColor = ColorTranslator.FromHtml("#e8f5e9");
                        }
                        else if (val < 0)
                        {
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");  // czerwony
                            e.CellStyle.BackColor = ColorTranslator.FromHtml("#ffebee");
                        }
                    }
                }
                else if (colName == "ZmianaProcent" || colName == "TrendProcentowy" || colName == "OdchylenieOdSredniej")
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        e.Value = $"{val:N1}%";
                        e.FormattingApplied = true;

                        if (colName == "ZmianaProcent" || colName == "TrendProcentowy")
                        {
                            // ZMIANA: Zielony gdy wzrost, czerwony gdy spadek
                            if (val > 0)
                            {
                                e.CellStyle.ForeColor = ColorTranslator.FromHtml("#27ae60");
                                e.CellStyle.BackColor = ColorTranslator.FromHtml("#e8f5e9");
                            }
                            else if (val < 0)
                            {
                                e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                                e.CellStyle.BackColor = ColorTranslator.FromHtml("#ffebee");
                            }
                        }
                    }
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
                lblTrendInfo.Text = "📊 Wybierz towar aby zobaczyć porównanie cen dzień do dnia\n💡 Panel pokazuje zmianę ceny między wczoraj a dziś dla każdego handlowca";
                dataGridViewAnalizaCen.DataSource = null;
                return;
            }

            int towarId = (int)comboBoxTowarAnalizaCen.SelectedValue;
            DateTime dataOd = dateTimePickerAnalizaOd.Value.Date;
            DateTime dataDo = dateTimePickerAnalizaDo.Value.Date;

            // Wczorajszy dzień handlowy
            DateTime wczoraj = dataDo.AddDays(-1);
            while (wczoraj.DayOfWeek == DayOfWeek.Saturday || wczoraj.DayOfWeek == DayOfWeek.Sunday)
            {
                wczoraj = wczoraj.AddDays(-1);
            }

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
CenyWczoraj AS (
    SELECT 
        ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
        AVG(DP.cena) AS CenaWczoraj
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE DP.idtw = @TowarID
      AND CONVERT(date, DK.data) = @Wczoraj
      AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') NOT IN ('Ogólne')
    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
),
CenyDzisiaj AS (
    SELECT 
        ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
        AVG(DP.cena) AS CenaDzisiaj
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE DP.idtw = @TowarID
      AND CONVERT(date, DK.data) = @Dzisiaj
      AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') NOT IN ('Ogólne')
    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany')
),
CenyPoczatek AS (
    SELECT Handlowiec, AVG(Cena) AS CenaPoczatkowa
    FROM CenyHandlowcow
    WHERE Data < DATEADD(DAY, 7, @DataOd)
    GROUP BY Handlowiec
),
CenyKoniec AS (
    SELECT Handlowiec, AVG(Cena) AS CenaKoncowa
    FROM CenyHandlowcow
    WHERE Data >= DATEADD(DAY, -7, @DataDo)
    GROUP BY Handlowiec
)
SELECT 
    CH.Handlowiec,
    CAST(AVG(CH.Cena) AS DECIMAL(18,2)) AS SredniaCena,
    CAST(CW.CenaWczoraj AS DECIMAL(18,2)) AS CenaWczoraj,
    CAST(CD.CenaDzisiaj AS DECIMAL(18,2)) AS CenaDzisiaj,
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
LEFT JOIN CenyWczoraj CW ON CH.Handlowiec = CW.Handlowiec
LEFT JOIN CenyDzisiaj CD ON CH.Handlowiec = CD.Handlowiec
GROUP BY CH.Handlowiec, CP.CenaPoczatkowa, CK.CenaKoncowa, CW.CenaWczoraj, CD.CenaDzisiaj
ORDER BY SredniaCena DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@TowarID", towarId);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);
                    cmd.Parameters.AddWithValue("@Wczoraj", wczoraj);
                    cmd.Parameters.AddWithValue("@Dzisiaj", dataDo);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        dt.Columns.Add("ZmianaZl", typeof(decimal));
                        dt.Columns.Add("ZmianaProcent", typeof(decimal));
                        dt.Columns.Add("OdchylenieOdSredniej", typeof(decimal));

                        decimal sredniaRynkowa = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            sredniaRynkowa += Convert.ToDecimal(row["SredniaCena"]);
                        }
                        sredniaRynkowa /= dt.Rows.Count;

                        foreach (DataRow row in dt.Rows)
                        {
                            decimal sredniaCena = Convert.ToDecimal(row["SredniaCena"]);
                            decimal odchylenie = ((sredniaCena - sredniaRynkowa) / sredniaRynkowa) * 100;
                            row["OdchylenieOdSredniej"] = Math.Round(odchylenie, 1);

                            // Oblicz zmianę wczoraj -> dziś
                            if (row["CenaWczoraj"] != DBNull.Value && row["CenaDzisiaj"] != DBNull.Value)
                            {
                                decimal cenaWczoraj = Convert.ToDecimal(row["CenaWczoraj"]);
                                decimal cenaDzisiaj = Convert.ToDecimal(row["CenaDzisiaj"]);
                                decimal zmianaZl = cenaDzisiaj - cenaWczoraj;
                                decimal zmianaProcent = cenaWczoraj > 0 ? (zmianaZl / cenaWczoraj) * 100 : 0;

                                row["ZmianaZl"] = Math.Round(zmianaZl, 2);
                                row["ZmianaProcent"] = Math.Round(zmianaProcent, 1);
                            }
                            else
                            {
                                row["ZmianaZl"] = 0;
                                row["ZmianaProcent"] = 0;
                            }
                        }

                        // Wiersz średniej rynkowej
                        var sumaRow = dt.NewRow();
                        sumaRow["Handlowiec"] = "📊 ŚREDNIA RYNKOWA";
                        sumaRow["SredniaCena"] = Math.Round(sredniaRynkowa, 2);

                        decimal sredniaWczoraj = 0;
                        decimal sredniaDzisiaj = 0;
                        int countWczoraj = 0;
                        int countDzisiaj = 0;

                        foreach (DataRow row in dt.Rows)
                        {
                            if (row["CenaWczoraj"] != DBNull.Value)
                            {
                                sredniaWczoraj += Convert.ToDecimal(row["CenaWczoraj"]);
                                countWczoraj++;
                            }
                            if (row["CenaDzisiaj"] != DBNull.Value)
                            {
                                sredniaDzisiaj += Convert.ToDecimal(row["CenaDzisiaj"]);
                                countDzisiaj++;
                            }
                        }

                        if (countWczoraj > 0) sredniaWczoraj /= countWczoraj;
                        if (countDzisiaj > 0) sredniaDzisiaj /= countDzisiaj;

                        sumaRow["CenaWczoraj"] = countWczoraj > 0 ? Math.Round(sredniaWczoraj, 2) : (object)DBNull.Value;
                        sumaRow["CenaDzisiaj"] = countDzisiaj > 0 ? Math.Round(sredniaDzisiaj, 2) : (object)DBNull.Value;

                        if (countWczoraj > 0 && countDzisiaj > 0)
                        {
                            decimal zmianaZl = sredniaDzisiaj - sredniaWczoraj;
                            sumaRow["ZmianaZl"] = Math.Round(zmianaZl, 2);
                            sumaRow["ZmianaProcent"] = Math.Round((zmianaZl / sredniaWczoraj) * 100, 1);
                        }

                        sumaRow["MinCena"] = dt.AsEnumerable().Min(r => r.Field<decimal>("MinCena"));
                        sumaRow["MaxCena"] = dt.AsEnumerable().Max(r => r.Field<decimal>("MaxCena"));
                        sumaRow["LiczbaTransakcji"] = dt.AsEnumerable().Sum(r => r.Field<int>("LiczbaTransakcji"));
                        sumaRow["TrendProcentowy"] = Math.Round(dt.AsEnumerable().Average(r => r.Field<decimal>("TrendProcentowy")), 1);
                        sumaRow["OdchylenieOdSredniej"] = 0;

                        dt.Rows.InsertAt(sumaRow, 0);
                    }

                    dataGridViewAnalizaCen.DataSource = dt;

                    if (dataGridViewAnalizaCen.Rows.Count > 0)
                    {
                        dataGridViewAnalizaCen.Rows[0].DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#d5dbdb");
                        dataGridViewAnalizaCen.Rows[0].DefaultCellStyle.Font = new Font(dataGridViewAnalizaCen.Font, FontStyle.Bold);
                    }

                    // Aktualizacja żółtego panelu
                    if (dt.Rows.Count > 1)
                    {
                        var sumaRow = dt.Rows[0];

                        string wczorajStr = sumaRow["CenaWczoraj"] != DBNull.Value ?
                            $"{Convert.ToDecimal(sumaRow["CenaWczoraj"]):N2} zł" : "brak danych";
                        string dzisiajStr = sumaRow["CenaDzisiaj"] != DBNull.Value ?
                            $"{Convert.ToDecimal(sumaRow["CenaDzisiaj"]):N2} zł" : "brak danych";

                        string zmianaStr = "brak danych";
                        string kierunek = "";
                        Color backgroundColor = ColorTranslator.FromHtml("#fff9c4");

                        if (sumaRow["ZmianaZl"] != DBNull.Value && sumaRow["ZmianaProcent"] != DBNull.Value)
                        {
                            decimal zmianaZl = Convert.ToDecimal(sumaRow["ZmianaZl"]);
                            decimal zmianaProcent = Convert.ToDecimal(sumaRow["ZmianaProcent"]);

                            if (zmianaZl > 0)
                            {
                                kierunek = "↗ WZROST";
                                backgroundColor = ColorTranslator.FromHtml("#c8e6c9");  // jasny zielony
                                zmianaStr = $"+{zmianaZl:N2} zł (+{zmianaProcent:N1}%)";
                            }
                            else if (zmianaZl < 0)
                            {
                                kierunek = "↘ SPADEK";
                                backgroundColor = ColorTranslator.FromHtml("#ffcdd2");  // jasny czerwony
                                zmianaStr = $"{zmianaZl:N2} zł ({zmianaProcent:N1}%)";
                            }
                            else
                            {
                                kierunek = "→ BEZ ZMIAN";
                                zmianaStr = "0.00 zł (0.0%)";
                            }
                        }

                        int liczbaHandlowcow = dt.Rows.Count - 1;
                        decimal avgTrend = Convert.ToDecimal(sumaRow["TrendProcentowy"]);
                        string trendInfo = avgTrend > 0 ? $"↗ +{avgTrend:N1}%" :
                                          avgTrend < 0 ? $"↘ {avgTrend:N1}%" : "→ 0.0%";

                        lblTrendInfo.Text =
                            $"📊 PORÓWNANIE: {wczoraj:dd.MM.yyyy} ({wczorajStr}) ➜ {dataDo:dd.MM.yyyy} ({dzisiajStr})\n" +
                            $"📈 {kierunek}: {zmianaStr} | " +
                            $"📊 Trend okresu: {trendInfo} | " +
                            $"👥 Handlowców: {liczbaHandlowcow} | " +
                            $"🔢 Transakcji: {sumaRow["LiczbaTransakcji"]}";

                        lblTrendInfo.Parent.BackColor = backgroundColor;
                    }
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

        // === LICZENIE CEN JAK W SYMFONII: SUM(cena * ilość) / SUM(ilość) (ZAMIANA DOTYCHCZASOWEJ METODY) ===
        // === LICZENIE CEN JAK W SYMFONII: SUM(cena*ilosc)/SUM(ilosc) ===
        private void OdswiezPorownanieTowarow()
        {
            var combo1 = tabControl.Controls.Find("comboBoxTowar1", true).FirstOrDefault() as ComboBox;
            var combo2 = tabControl.Controls.Find("comboBoxTowar2", true).FirstOrDefault() as ComboBox;
            if (combo1 == null || combo2 == null) return;

            if (!(combo1.SelectedValue is int t1) || t1 == 0 ||
                !(combo2.SelectedValue is int t2) || t2 == 0)
            {
                dataGridViewPorownaniaSwiezeMrozone.DataSource = null;
                lblStatystykiPorown.Text = "⚖ Wybierz dwa towary aby zobaczyć porównanie cen";
                return;
            }

            var dOd = dateTimePickerOd.Value.Date;
            var dDo = dateTimePickerDo.Value.Date;

            string sql = @"
;WITH Sprz AS (
    SELECT 
        CAST(DK.data AS date) AS Dzien,
        DP.idtw AS TowarId,
        SUM(CAST(DP.ilosc AS decimal(18,3))) AS Kg,
        SUM(CAST(DP.cena AS decimal(18,4)) * CAST(DP.ilosc AS decimal(18,3))) AS Wartosc
    FROM HANDEL.HM.DK DK
    JOIN HANDEL.HM.DP DP ON DP.super = DK.id
    WHERE CAST(DK.data AS date) BETWEEN @dOd AND @dDo
      AND DP.idtw IN (@T1, @T2)
      AND DK.anulowany = 0
    GROUP BY CAST(DK.data AS date), DP.idtw
),
Pivoted AS (
    SELECT 
        Dzien,
        MAX(CASE WHEN TowarId = @T1 THEN Kg END)      AS KgT1,
        MAX(CASE WHEN TowarId = @T1 THEN Wartosc END) AS WartT1,
        MAX(CASE WHEN TowarId = @T2 THEN Kg END)      AS KgT2,
        MAX(CASE WHEN TowarId = @T2 THEN Wartosc END) AS WartT2
    FROM Sprz
    GROUP BY Dzien
)
SELECT
    Dzien AS Data,
    CASE WHEN KgT1 > 0 THEN WartT1 / KgT1 END AS CenaT1,
    KgT1,
    CASE WHEN KgT2 > 0 THEN WartT2 / KgT2 END AS CenaT2,
    KgT2,
    (CASE WHEN KgT1 > 0 THEN WartT1 / KgT1 END) - (CASE WHEN KgT2 > 0 THEN WartT2 / KgT2 END) AS RoznicaZl,
    CASE 
        WHEN (CASE WHEN KgT2 > 0 THEN WartT2 / KgT2 END) IS NULL OR (CASE WHEN KgT2 > 0 THEN WartT2 / KgT2 END) = 0 
            THEN NULL
        ELSE 100.0 * (
            (CASE WHEN KgT1 > 0 THEN WartT1 / KgT1 END)
            - (CASE WHEN KgT2 > 0 THEN WartT2 / KgT2 END)
        ) / (CASE WHEN KgT2 > 0 THEN WartT2 / KgT2 END)
    END AS RoznicaProc
FROM Pivoted
ORDER BY Dzien ASC;";

            try
            {
                using var conn = new SqlConnection(connectionString);
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@dOd", dOd);
                cmd.Parameters.AddWithValue("@dDo", dDo);
                cmd.Parameters.AddWithValue("@T1", t1);
                cmd.Parameters.AddWithValue("@T2", t2);

                using var da = new SqlDataAdapter(cmd);
                var dt = new DataTable();
                da.Fill(dt);

                // 1) Najpierw bind źródło
                dataGridViewPorownaniaSwiezeMrozone.DataSource = dt;

                // 2) Następnie filtr + sort (checkbox)
                ZastosujFiltrZerowychWPorownaniu();

                // 3) Statystyki panelu
                decimal sr1 = dt.AsEnumerable().Where(r => r["CenaT1"] != DBNull.Value).Select(r => r.Field<decimal>("CenaT1")).DefaultIfEmpty().Average();
                decimal sr2 = dt.AsEnumerable().Where(r => r["CenaT2"] != DBNull.Value).Select(r => r.Field<decimal>("CenaT2")).DefaultIfEmpty().Average();
                decimal diff = sr1 - sr2;
                lblStatystykiPorown.Text = $"Śr T1: {sr1:N2} zł/kg   |   Śr T2: {sr2:N2} zł/kg   |   Różnica: {diff:N2} zł/kg";

                // Wykres pokazujemy dopiero z guzika (nic tu nie wywołujemy)
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

        // 1. ZMIANA W METODZIE KonfigurujDataGridViewDokumenty() - dodanie nowych kolumn
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

            // NOWE KOLUMNY
            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DniTerminu",
                DataPropertyName = "DniTerminu",
                HeaderText = "📅 Dni terminu",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dataGridViewOdbiorcy.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DniDoTerminu",
                DataPropertyName = "DniDoTerminu",
                HeaderText = "⏰ Do terminu",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

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

            // NOWA OPCJA - Zgłoś reklamację
            var menuReklamacja = new ToolStripMenuItem
            {
                Text = "⚠ Zgłoś reklamację",
                ShortcutKeys = Keys.Control | Keys.R
            };
            menuReklamacja.Click += MenuReklamacja_Click;
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
                    if (menuItem.Text.Contains("Podgląd") ||
                        menuItem.Text.Contains("analiza") ||
                        menuItem.Text.Contains("reklamację"))  // ← TUTAJ SPRAWDZA REKLAMACJĘ
                    {
                        menuItem.Enabled = czyZaznaczono;
                    }
                }
            }
        }
        private void MenuReklamacja_Click(object? sender, EventArgs e)
        {
            if (dataGridViewOdbiorcy.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewOdbiorcy.SelectedRows[0];

                if (!Convert.ToBoolean(selectedRow.Cells["IsGroupRow"].Value) &&
                    selectedRow.Cells["ID"].Value != DBNull.Value)
                {
                    int idDokumentu = Convert.ToInt32(selectedRow.Cells["ID"].Value);
                    int idKontrahenta = Convert.ToInt32(selectedRow.Cells["khid"].Value);
                    string numerDokumentu = selectedRow.Cells["NumerDokumentu"].Value?.ToString() ?? "Nieznany";
                    string nazwaKontrahenta = selectedRow.Cells["NazwaFirmy"].Value?.ToString() ?? "Nieznany";

                    using (var formReklamacja = new FormReklamacja(
                        connectionString,
                        idDokumentu,
                        idKontrahenta,
                        numerDokumentu,
                        nazwaKontrahenta,
                        UserID))
                    {
                        if (formReklamacja.ShowDialog(this) == DialogResult.OK)
                        {
                            MessageBox.Show("✓ Reklamacja została pomyślnie zgłoszona!",
                                "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
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

            // NOWA KOLUMNA
            dataGridViewPlatnosci.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NajpozniejszaPlatnosc",
                DataPropertyName = "NajpozniejszaPlatnosc",
                HeaderText = "📅 Najpóźniejsza płatność",
                Width = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dataGridViewPlatnosci.CellDoubleClick += DataGridViewPlatnosci_CellDoubleClick;
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
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    WHERE DK.anulowany = 0
      AND C.Shortcut NOT LIKE '%Centrum Drobiu%'
      AND C.Shortcut NOT LIKE '%Sd/Kozio%'
      AND C.Shortcut NOT LIKE '%Piórkowski%'";

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
           ISNULL(PA.TerminPrawdziwy, D.plattermin)     AS TerminPlatnosci,
           CASE 
               WHEN (D.walbrutto - ISNULL(PA.KwotaRozliczona,0)) > 0.01 AND GETDATE() > ISNULL(PA.TerminPrawdziwy, D.plattermin)
               THEN DATEDIFF(day, ISNULL(PA.TerminPrawdziwy, D.plattermin), GETDATE())
               ELSE 0
           END AS DniPrzeterminowania
    FROM Dokumenty D
    LEFT JOIN PNAgg PA ON PA.dkid = D.id
),
MaxPrzeterminowania AS (
    SELECT 
        khid,
        MAX(CASE WHEN DniPrzeterminowania > 0 THEN DniPrzeterminowania ELSE NULL END) AS MaxDniPrzeterminowania
    FROM Saldo
    GROUP BY khid
)
SELECT 
    C.Shortcut AS Kontrahent,
    C.LimitAmount AS Limit,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS DoZaplacenia,
    CAST(C.LimitAmount - SUM(CASE WHEN S.DoZaplacenia > 0 THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS PrzekroczonyLimit,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() <= S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Terminowe,
    CAST(SUM(CASE WHEN S.DoZaplacenia > 0 AND GETDATE() >  S.TerminPlatnosci THEN S.DoZaplacenia ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane,
    MP.MaxDniPrzeterminowania AS NajpozniejszaPlatnosc
FROM Saldo S
JOIN [HANDEL].[SSCommon].[STContractors] C ON C.id = S.khid
LEFT JOIN MaxPrzeterminowania MP ON MP.khid = S.khid
WHERE C.Shortcut NOT LIKE '%Centrum Drobiu%'
  AND C.Shortcut NOT LIKE '%Sd/Kozio%'
  AND C.Shortcut NOT LIKE '%Piórkowski%'
GROUP BY C.Shortcut, C.LimitAmount, MP.MaxDniPrzeterminowania
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
                        sumaRow["NajpozniejszaPlatnosc"] = DBNull.Value;

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
            if (e.Value != null && e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                var row = dataGridViewPlatnosci.Rows[e.RowIndex];
                var colName = dataGridViewPlatnosci.Columns[e.ColumnIndex].Name;

                // Formatowanie kwot
                if (colName == "Limit" || colName == "DoZaplacenia" || colName == "PrzekroczonyLimit" ||
                    colName == "Terminowe" || colName == "Przeterminowane")
                {
                    if (decimal.TryParse(e.Value.ToString(), out decimal val))
                    {
                        e.Value = val.ToString("N2") + " zł";
                        e.FormattingApplied = true;
                    }
                }

                // Formatowanie kolumny NajpozniejszaPlatnosc
                if (colName == "NajpozniejszaPlatnosc")
                {
                    if (e.Value != null && e.Value != DBNull.Value)
                    {
                        int dni = Convert.ToInt32(e.Value);
                        if (dni > 0)
                        {
                            e.Value = $"⚠ {dni} dni po terminie";
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                            e.CellStyle.Font = new Font(dataGridViewPlatnosci.Font, FontStyle.Bold);
                        }
                        else
                        {
                            e.Value = "";
                        }
                    }
                    else
                    {
                        e.Value = "";
                    }
                    e.FormattingApplied = true;
                }

                // Kolorowanie gdy przekroczony limit między -150 000 a -2 000 000
                if (row.Cells["PrzekroczonyLimit"].Value != null &&
                    row.Cells["PrzekroczonyLimit"].Value != DBNull.Value &&
                    row.Cells["Kontrahent"].Value?.ToString() != "📊 SUMA")
                {
                    decimal przekroczony = Convert.ToDecimal(row.Cells["PrzekroczonyLimit"].Value);

                    if (przekroczony <= -2000 && przekroczony >= -2000000)
                    {
                        if (colName == "Kontrahent" || colName == "Limit" ||
                            colName == "DoZaplacenia" || colName == "PrzekroczonyLimit")
                        {
                            e.CellStyle.BackColor = ColorTranslator.FromHtml("#ffcdd2");
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#b71c1c");
                            e.CellStyle.Font = new Font(dataGridViewPlatnosci.Font, FontStyle.Bold);
                        }
                    }
                }

                // Kolorowanie kolumny Przeterminowane gdy wartość > 30 000
                if (colName == "Przeterminowane" &&
                    row.Cells["Przeterminowane"].Value != null &&
                    row.Cells["Przeterminowane"].Value != DBNull.Value &&
                    row.Cells["Kontrahent"].Value?.ToString() != "📊 SUMA")
                {
                    decimal przeterminowane = Convert.ToDecimal(row.Cells["Przeterminowane"].Value);

                    if (przeterminowane > 30000)
                    {
                        e.CellStyle.BackColor = ColorTranslator.FromHtml("#ff5252");
                        e.CellStyle.ForeColor = Color.White;
                        e.CellStyle.Font = new Font(dataGridViewPlatnosci.Font, FontStyle.Bold);
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
PlatnosciInfo AS (
    SELECT 
        DK.id,
        SUM(ISNULL(PN.kwotarozl, 0)) AS KwotaRozliczona
    FROM [HANDEL].[HM].[DK] DK
    LEFT JOIN [HANDEL].[HM].[PN] PN ON DK.id = PN.dkid
    GROUP BY DK.id
),
DokumentyFiltrowane AS (
    SELECT DISTINCT 
        DK.*, 
        WYM.CDim_Handlowiec_Val,
        DATEDIFF(day, DK.data, DK.plattermin) AS DniTerminu,
        CASE 
            WHEN (DK.walbrutto - ISNULL(PI.KwotaRozliczona, 0)) <= 0.01 THEN NULL
            WHEN DK.plattermin > GETDATE() THEN DATEDIFF(day, GETDATE(), DK.plattermin)
            WHEN DK.plattermin <= GETDATE() THEN -DATEDIFF(day, DK.plattermin, GETDATE())
            ELSE NULL
        END AS DniDoTerminu,
        (DK.walbrutto - ISNULL(PI.KwotaRozliczona, 0)) AS DoZaplaty
    FROM [HANDEL].[HM].[DK] DK
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    LEFT JOIN PlatnosciInfo PI ON DK.id = PI.id
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
    ISNULL(DF.CDim_Handlowiec_Val, '-') AS Handlowiec, DF.khid, DF.id,
    DF.DniTerminu,
    DF.DniDoTerminu,
    DF.DoZaplaty
FROM DokumentyFiltrowane DF
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DF.khid = C.id
INNER JOIN AgregatyDokumentu AD ON DF.id = AD.id_dk
UNION ALL
SELECT DISTINCT
    CONVERT(date, data) AS SortDate, 0 AS SortOrder, 1 AS IsGroupRow,
    NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL
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
        // Dodaj handler:
        private void BtnMapa_Click(object? sender, EventArgs e)
        {
            using (var mapaForm = new MapaOdbiorcowForm(connectionString, UserID, zaznaczeniHandlowcy))
            {
                mapaForm.ShowDialog(this);
            }
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
            else
            {
                var colName = dataGridViewOdbiorcy.Columns[e.ColumnIndex].Name;

                // Formatowanie kolumny DniTerminu
                if (colName == "DniTerminu" && e.Value != null && e.Value != DBNull.Value)
                {
                    int dni = Convert.ToInt32(e.Value);
                    e.Value = $"{dni} dni";
                    e.FormattingApplied = true;
                }

                // Formatowanie kolumny DniDoTerminu
                if (colName == "DniDoTerminu")
                {
                    if (e.Value == null || e.Value == DBNull.Value)
                    {
                        e.Value = "✓ Zapłacone";
                        e.CellStyle.ForeColor = ColorTranslator.FromHtml("#27ae60");
                        e.CellStyle.Font = new Font(dataGridViewOdbiorcy.Font, FontStyle.Bold);
                    }
                    else
                    {
                        int dni = Convert.ToInt32(e.Value);
                        if (dni > 0)
                        {
                            e.Value = $"{dni} dni";
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#3498db");
                        }
                        else if (dni == 0)
                        {
                            e.Value = "Dziś";
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#f39c12");
                            e.CellStyle.Font = new Font(dataGridViewOdbiorcy.Font, FontStyle.Bold);
                        }
                        else
                        {
                            e.Value = $"Po terminie ({Math.Abs(dni)} dni)";
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                            e.CellStyle.BackColor = ColorTranslator.FromHtml("#ffebee");
                            e.CellStyle.Font = new Font(dataGridViewOdbiorcy.Font, FontStyle.Bold);
                        }
                    }
                    e.FormattingApplied = true;
                }
            }
        }
        // NOWA METODA: Obsługa podwójnego kliknięcia w tabeli płatności
        private void DataGridViewPlatnosci_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // Sprawdzenie, czy kliknięto prawidłowy wiersz (nie nagłówek)
            if (e.RowIndex < 0)
            {
                return;
            }

            var row = dataGridViewPlatnosci.Rows[e.RowIndex];
            var kontrahentCellValue = row.Cells["Kontrahent"].Value;

            // Sprawdzenie, czy to nie jest wiersz sumy
            if (kontrahentCellValue != null && kontrahentCellValue.ToString() != "📊 SUMA")
            {
                string nazwaKontrahenta = kontrahentCellValue.ToString();

                // Utworzenie i wyświetlenie nowego okna ze szczegółami
                using (var formSzczegoly = new FormSzczegolyPlatnosci(connectionString, nazwaKontrahenta))
                {
                    formSzczegoly.ShowDialog(this);
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
    public class FormHistoriaCen : Form
    {
        private DataGridView dataGridView;
        private Label lblStatystyki;
        private string connectionString;
        private int currentTowarId;
        private string currentHandlowiec;
        private DateTime currentDataOd;
        private DateTime currentDataDo;

        public FormHistoriaCen(string connString, int towarId, string nazwaTowaru,
                               string handlowiec, DateTime dataOd, DateTime dataDo)
        {
            connectionString = connString;
            currentTowarId = towarId;
            currentHandlowiec = handlowiec;
            currentDataOd = dataOd;
            currentDataDo = dataDo;

            this.Text = $"📊 Historia cen - {handlowiec} - {nazwaTowaru}";
            this.WindowState = FormWindowState.Maximized;  // ZMIANA: Maksymalne okno
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F);

            Panel panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = ColorTranslator.FromHtml("#ecf0f1"),
                Padding = new Padding(10)
            };

            Label lblInfo = new Label
            {
                Text = $"👤 Handlowiec: {handlowiec}\n📦 Towar: {nazwaTowaru}\n📅 Okres: {dataOd:dd.MM.yyyy} - {dataDo:dd.MM.yyyy}",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2c3e50")
            };
            panelTop.Controls.Add(lblInfo);

            Panel panelStats = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = ColorTranslator.FromHtml("#d5f4e6"),
                Padding = new Padding(10)
            };

            lblStatystyki = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#27ae60"),
                TextAlign = ContentAlignment.MiddleLeft
            };
            panelStats.Controls.Add(lblStatystyki);

            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToOrderColumns = false,  // DODANE: Zakaz przestawiania kolumn
                AllowUserToResizeRows = false
            };
            KonfigurujKolumny();

            Panel panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10, 7, 10, 7),
                BackColor = ColorTranslator.FromHtml("#ecf0f1")
            };

            Button btnEksportuj = new Button
            {
                Text = "📊 Eksportuj CSV",
                Size = new Size(130, 36),
                Location = new Point(10, 7),
                BackColor = ColorTranslator.FromHtml("#3498db"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnEksportuj.FlatAppearance.BorderSize = 0;
            btnEksportuj.Click += BtnEksportuj_Click;



            Button btnInstrukcja = new Button
            {
                Text = "📖 Instrukcja",
                Size = new Size(120, 36),
                Location = new Point(150, 7),
                BackColor = ColorTranslator.FromHtml("#9b59b6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnInstrukcja.FlatAppearance.BorderSize = 0;
            btnInstrukcja.Click += BtnInstrukcja_Click;

            Button btnZamknij = new Button
            {
                Text = "✗ Zamknij",
                Size = new Size(110, 36),
                Anchor = AnchorStyles.Right,
                BackColor = ColorTranslator.FromHtml("#95a5a6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnZamknij.Location = new Point(this.ClientSize.Width - 130, 7);
            btnZamknij.FlatAppearance.BorderSize = 0;
            btnZamknij.Click += (s, e) => this.Close();

            panelBottom.Controls.Add(btnEksportuj);
            panelBottom.Controls.Add(btnInstrukcja);
            panelBottom.Controls.Add(btnZamknij);

            this.Controls.Add(dataGridView);
            this.Controls.Add(panelStats);
            this.Controls.Add(panelTop);
            this.Controls.Add(panelBottom);

            WczytajHistorie(towarId, handlowiec, dataOd, dataDo);
        }

        private void BtnInstrukcja_Click(object? sender, EventArgs e)
        {
            string instrukcja = @"📖 INSTRUKCJA HISTORII CEN

📅 NAGŁÓWKI DAT (szare wiersze)
   Grupują transakcje według dni
   Pokazują podsumowanie w odpowiednich kolumnach:
   • 🏢 Kontrahent → Data i dzień tygodnia
   • 💵 Cena → Średnia cena dnia (zł/kg)
   • ⚖ Ilość kg → Suma sprzedaży w dniu (kg)
   • 💰 Wartość → Suma wartości transakcji (zł)
   • ± Zmiana → Zmiana vs poprzedni dzień
   • 🔢 Trans. → Liczba transakcji w dniu

📊 SZCZEGÓŁOWE TRANSAKCJE (białe wiersze)

🏢 KONTRAHENT
   Nazwa firmy kupującej

💵 CENA
   Cena sprzedaży za kg (zł/kg)

⚖ ILOŚĆ KG
   Ilość sprzedanego towaru

💰 WARTOŚĆ
   Wartość transakcji (cena × ilość)

± ZMIANA vs POPRZ.
   Zmiana względem poprzedniej transakcji
   🟢 Zielony = wzrost (korzystne)
   🔴 Czerwony = spadek

± ZMIANA %
   Procentowa zmiana ceny

± ODCH. OD ŚR. DNIA
   Czy transakcja droższa/tańsza od średniej w dniu
   🟡 Żółte tło = duże odchylenie (>10%)

📊 POZYCJA CENY
   Miejsce na tle innych transakcji w dniu
   (np. 2/5 = druga najwyższa z pięciu)

📄 DOKUMENT
   Numer faktury

💡 WSKAZÓWKI:
   • Szare wiersze = podsumowania dni
   • Średnie dzienne pokazują trendy
   • Odchylenia pomagają znaleźć nietypowe ceny
   • Sortowanie jest wyłączone dla przejrzystości";

            MessageBox.Show(instrukcja, "📖 Instrukcja historii cen",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void KonfigurujKolumny()
        {
            dataGridView.Columns.Clear();

            // Ukryte kolumny pomocnicze
            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "IsGroupRow",
                DataPropertyName = "IsGroupRow",
                Visible = false
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "DataSort",
                DataPropertyName = "DataSort",
                Visible = false
            });

            // ZMIANA: Tylko jedna kolumna - Kontrahent (zawiera datę w nagłówkach i nazwę w szczegółach)
            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Kontrahent",
                DataPropertyName = "Kontrahent",
                HeaderText = "🏢 Kontrahent / 📅 Data",
                Width = 280,
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE: Zakaz sortowania
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Cena",
                DataPropertyName = "Cena",
                HeaderText = "💵 Cena",
                Width = 95,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ilosc",
                DataPropertyName = "Ilosc",
                HeaderText = "⚖ Ilość kg",
                Width = 95,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Wartosc",
                DataPropertyName = "Wartosc",
                HeaderText = "💰 Wartość",
                Width = 105,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ZmianaCeny",
                DataPropertyName = "ZmianaCeny",
                HeaderText = "± Zmiana\nvs poprz.",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ZmianaProcent",
                DataPropertyName = "ZmianaProcent",
                HeaderText = "± Zmiana",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "OdchylenieOdSredniej",
                DataPropertyName = "OdchylenieOdSredniej",
                HeaderText = "± Odch.\nod śr. dnia",
                Width = 95,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PozycjaCeny",
                DataPropertyName = "PozycjaCeny",
                HeaderText = "📊 Pozycja\nceny",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LiczbaTransakcji",
                DataPropertyName = "LiczbaTransakcji",
                HeaderText = "🔢 Trans.",
                Width = 75,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NumerDokumentu",
                DataPropertyName = "NumerDokumentu",
                HeaderText = "📄 Dokument",
                Width = 140,
                SortMode = DataGridViewColumnSortMode.NotSortable  // DODANE
            });

            dataGridView.EnableHeadersVisualStyles = false;
            dataGridView.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#3498db");
            dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            dataGridView.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridView.ColumnHeadersHeight = 48;
            dataGridView.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#ecf0f1");
            dataGridView.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#3498db");
            dataGridView.GridColor = ColorTranslator.FromHtml("#bdc3c7");
            dataGridView.RowTemplate.Height = 32;

            dataGridView.CellFormatting += DataGridView_CellFormatting;
            dataGridView.RowPrePaint += DataGridView_RowPrePaint;
        }

        private void WczytajHistorie(int towarId, string handlowiec, DateTime dataOd, DateTime dataDo)
        {
            string query = @"
WITH DaneTransakcji AS (
    SELECT 
        CONVERT(date, DK.data) AS Data,
        DK.data AS DataCzas,
        C.shortcut AS Kontrahent,
        DP.cena AS Cena,
        DP.ilosc AS Ilosc,
        DP.wartNetto AS Wartosc,
        DK.kod AS NumerDokumentu,
        ROW_NUMBER() OVER (ORDER BY DK.data DESC) AS RowNum
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE DP.idtw = @TowarID
      AND ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') = @Handlowiec
      AND DK.data >= @DataOd
      AND DK.data <= @DataDo
),
SrednieDzienne AS (
    SELECT 
        Data,
        AVG(Cena) AS SredniaCenaDnia,
        SUM(Ilosc) AS SumaIloscDnia,
        SUM(Wartosc) AS SumaWartoscDnia,
        COUNT(*) AS LiczbaTransakcji
    FROM DaneTransakcji
    GROUP BY Data
),
SrednieDniaPrzed AS (
    SELECT 
        Data,
        LAG(SredniaCenaDnia) OVER (ORDER BY Data) AS SredniaCenaPrzedDnia
    FROM SrednieDzienne
)
SELECT 
    DT.Data,
    DT.DataCzas,
    CASE DATEPART(WEEKDAY, DT.Data)
        WHEN 1 THEN 'Niedziela'
        WHEN 2 THEN 'Poniedziałek'
        WHEN 3 THEN 'Wtorek'
        WHEN 4 THEN 'Środa'
        WHEN 5 THEN 'Czwartek'
        WHEN 6 THEN 'Piątek'
        WHEN 7 THEN 'Sobota'
    END AS DzienTygodnia,
    DT.Kontrahent,
    DT.Cena,
    DT.Ilosc,
    DT.Wartosc,
    DT.NumerDokumentu,
    LAG(DT.Cena) OVER (ORDER BY DT.DataCzas) AS CenaPoprzednia,
    SD.SredniaCenaDnia,
    SD.SumaIloscDnia,
    SD.SumaWartoscDnia,
    SD.LiczbaTransakcji,
    SDP.SredniaCenaPrzedDnia,
    ROW_NUMBER() OVER (PARTITION BY DT.Data ORDER BY DT.Cena DESC) AS PozycjaCeny,
    COUNT(*) OVER (PARTITION BY DT.Data) AS LiczbaTransakcjiDnia
FROM DaneTransakcji DT
INNER JOIN SrednieDzienne SD ON DT.Data = SD.Data
LEFT JOIN SrednieDniaPrzed SDP ON DT.Data = SDP.Data
ORDER BY DT.DataCzas DESC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@TowarID", towarId);
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);

                    var adapter = new SqlDataAdapter(cmd);
                    var dtSource = new DataTable();
                    adapter.Fill(dtSource);

                    // Tworzenie DataTable z grupowaniem
                    var dt = new DataTable();
                    dt.Columns.Add("IsGroupRow", typeof(bool));
                    dt.Columns.Add("DataSort", typeof(DateTime));
                    dt.Columns.Add("Kontrahent", typeof(string));
                    dt.Columns.Add("Cena", typeof(string));
                    dt.Columns.Add("Ilosc", typeof(string));
                    dt.Columns.Add("Wartosc", typeof(string));
                    dt.Columns.Add("ZmianaCeny", typeof(string));
                    dt.Columns.Add("ZmianaProcent", typeof(string));
                    dt.Columns.Add("OdchylenieOdSredniej", typeof(string));
                    dt.Columns.Add("PozycjaCeny", typeof(string));
                    dt.Columns.Add("LiczbaTransakcji", typeof(string));
                    dt.Columns.Add("NumerDokumentu", typeof(string));

                    DateTime? ostatniaData = null;
                    decimal? ostatniaSredniaCenaDnia = null;

                    foreach (DataRow sourceRow in dtSource.Rows)
                    {
                        DateTime dataTransakcji = Convert.ToDateTime(sourceRow["Data"]);

                        // Dodaj nagłówek grupy jeśli nowa data
                        if (ostatniaData == null || ostatniaData.Value != dataTransakcji)
                        {
                            var groupRow = dt.NewRow();
                            groupRow["IsGroupRow"] = true;
                            groupRow["DataSort"] = dataTransakcji;

                            decimal sredniaCena = Convert.ToDecimal(sourceRow["SredniaCenaDnia"]);
                            decimal sumaIlosc = Convert.ToDecimal(sourceRow["SumaIloscDnia"]);
                            decimal sumaWartosc = Convert.ToDecimal(sourceRow["SumaWartoscDnia"]);
                            int liczbaTransakcji = Convert.ToInt32(sourceRow["LiczbaTransakcji"]);

                            string dzienTygodnia = sourceRow["DzienTygodnia"].ToString();

                            // ZMIANA: Data i dzień w kolumnie Kontrahent
                            groupRow["Kontrahent"] = $"📅 {dataTransakcji:dd.MM.yyyy} - {dzienTygodnia}";

                            // ZMIANA: Wartości w odpowiednich kolumnach
                            groupRow["Cena"] = $"{sredniaCena:N2} zł";  // Średnia
                            groupRow["Ilosc"] = $"{sumaIlosc:N2} kg";   // Suma
                            groupRow["Wartosc"] = $"{sumaWartosc:N2} zł"; // Suma
                            groupRow["LiczbaTransakcji"] = liczbaTransakcji.ToString();

                            // Zmiana vs poprzedni dzień
                            if (sourceRow["SredniaCenaPrzedDnia"] != DBNull.Value && ostatniaSredniaCenaDnia.HasValue)
                            {
                                decimal sredniaPrzedDnia = Convert.ToDecimal(sourceRow["SredniaCenaPrzedDnia"]);
                                decimal zmiana = sredniaCena - sredniaPrzedDnia;
                                decimal zmianaProcent = sredniaPrzedDnia > 0 ? (zmiana / sredniaPrzedDnia) * 100 : 0;

                                groupRow["ZmianaCeny"] = $"{zmiana:N2} zł";
                                groupRow["ZmianaProcent"] = $"{zmianaProcent:N1}%";
                            }
                            else
                            {
                                groupRow["ZmianaCeny"] = "—";
                                groupRow["ZmianaProcent"] = "—";
                            }

                            groupRow["OdchylenieOdSredniej"] = "—";
                            groupRow["PozycjaCeny"] = "—";
                            groupRow["NumerDokumentu"] = "";

                            dt.Rows.Add(groupRow);

                            ostatniaData = dataTransakcji;
                            ostatniaSredniaCenaDnia = sredniaCena;
                        }

                        // Dodaj wiersz szczegółowy
                        var detailRow = dt.NewRow();
                        detailRow["IsGroupRow"] = false;
                        detailRow["DataSort"] = dataTransakcji;
                        detailRow["Kontrahent"] = sourceRow["Kontrahent"].ToString();

                        decimal cena = Convert.ToDecimal(sourceRow["Cena"]);
                        decimal ilosc = Convert.ToDecimal(sourceRow["Ilosc"]);
                        decimal wartosc = Convert.ToDecimal(sourceRow["Wartosc"]);

                        detailRow["Cena"] = $"{cena:N2} zł";
                        detailRow["Ilosc"] = $"{ilosc:N2} kg";
                        detailRow["Wartosc"] = $"{wartosc:N2} zł";

                        // Zmiana vs poprzednia transakcja
                        if (sourceRow["CenaPoprzednia"] != DBNull.Value)
                        {
                            decimal cenaPoprzednia = Convert.ToDecimal(sourceRow["CenaPoprzednia"]);
                            decimal zmiana = cena - cenaPoprzednia;
                            decimal zmianaProcent = cenaPoprzednia > 0 ? (zmiana / cenaPoprzednia) * 100 : 0;

                            detailRow["ZmianaCeny"] = $"{zmiana:N2} zł";
                            detailRow["ZmianaProcent"] = $"{zmianaProcent:N1}%";
                        }
                        else
                        {
                            detailRow["ZmianaCeny"] = "—";
                            detailRow["ZmianaProcent"] = "—";
                        }

                        // Odchylenie od średniej dnia
                        decimal sredniaDnia = Convert.ToDecimal(sourceRow["SredniaCenaDnia"]);
                        decimal odchylenie = ((cena - sredniaDnia) / sredniaDnia) * 100;
                        detailRow["OdchylenieOdSredniej"] = $"{odchylenie:N1}%";

                        // Pozycja ceny
                        int pozycja = Convert.ToInt32(sourceRow["PozycjaCeny"]);
                        int liczba = Convert.ToInt32(sourceRow["LiczbaTransakcjiDnia"]);
                        detailRow["PozycjaCeny"] = $"{pozycja}/{liczba}";

                        detailRow["LiczbaTransakcji"] = "";
                        detailRow["NumerDokumentu"] = sourceRow["NumerDokumentu"].ToString();

                        dt.Rows.Add(detailRow);
                    }

                    dataGridView.DataSource = dt;

                    // Statystyki
                    if (dtSource.Rows.Count > 0)
                    {
                        decimal minCena = dtSource.AsEnumerable().Min(r => r.Field<decimal>("Cena"));
                        decimal maxCena = dtSource.AsEnumerable().Max(r => r.Field<decimal>("Cena"));
                        decimal avgCena = dtSource.AsEnumerable().Average(r => r.Field<decimal>("Cena"));
                        decimal sumaIlosc = dtSource.AsEnumerable().Sum(r => r.Field<decimal>("Ilosc"));
                        decimal sumaWartosc = dtSource.AsEnumerable().Sum(r => r.Field<decimal>("Wartosc"));
                        int liczbaTransakcji = dtSource.Rows.Count;
                        int liczbaDni = dtSource.AsEnumerable().Select(r => r.Field<DateTime>("Data")).Distinct().Count();

                        lblStatystyki.Text =
                            $"📊 PODSUMOWANIE CAŁEGO OKRESU:\n" +
                            $"💵 Cena: Min {minCena:N2} zł | Max {maxCena:N2} zł | Średnia {avgCena:N2} zł | " +
                            $"⚖ Suma: {sumaIlosc:N2} kg | 💰 Wartość: {sumaWartosc:N2} zł | " +
                            $"🔢 Transakcji: {liczbaTransakcji} | 📅 Dni handlowych: {liczbaDni}";
                    }
                    else
                    {
                        lblStatystyki.Text = "❌ Brak transakcji w wybranym okresie";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Błąd wczytywania historii: " + ex.Message, "Błąd",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DataGridView_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridView.Rows.Count) return;

            var row = dataGridView.Rows[e.RowIndex];
            if (row.Cells["IsGroupRow"].Value != null && Convert.ToBoolean(row.Cells["IsGroupRow"].Value))
            {
                row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#bdc3c7");
                row.DefaultCellStyle.ForeColor = Color.Black;
                row.DefaultCellStyle.Font = new Font(dataGridView.Font, FontStyle.Bold);
                row.Height = 38;
                row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
                row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor;
            }
        }

        private void DataGridView_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridView.Rows.Count) return;

            var row = dataGridView.Rows[e.RowIndex];
            bool isGroupRow = row.Cells["IsGroupRow"].Value != null &&
                             Convert.ToBoolean(row.Cells["IsGroupRow"].Value);

            if (!isGroupRow)
            {
                // Kolorowanie zmian - tylko dla wierszy szczegółowych
                var colName = dataGridView.Columns[e.ColumnIndex].Name;

                if ((colName == "ZmianaCeny" || colName == "ZmianaProcent") &&
                    e.Value != null && e.Value.ToString() != "—")
                {
                    string val = e.Value.ToString();
                    decimal numVal = 0;
                    string numStr = val.Replace("zł", "").Replace("%", "").Trim();

                    if (decimal.TryParse(numStr, out numVal))
                    {
                        if (numVal > 0)
                        {
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#27ae60");
                            e.CellStyle.BackColor = ColorTranslator.FromHtml("#e8f5e9");
                        }
                        else if (numVal < 0)
                        {
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                            e.CellStyle.BackColor = ColorTranslator.FromHtml("#ffebee");
                        }
                    }
                }

                if (colName == "OdchylenieOdSredniej" && e.Value != null && e.Value.ToString() != "—")
                {
                    string val = e.Value.ToString().Replace("%", "").Trim();
                    if (decimal.TryParse(val, out decimal numVal))
                    {
                        if (Math.Abs(numVal) > 10)
                        {
                            e.CellStyle.BackColor = ColorTranslator.FromHtml("#fff3cd");
                            e.CellStyle.Font = new Font(e.CellStyle.Font, FontStyle.Bold);
                        }
                    }
                }
            }
            else
            {
                // Kolorowanie zmian w nagłówkach grup
                var colName = dataGridView.Columns[e.ColumnIndex].Name;

                if ((colName == "ZmianaCeny" || colName == "ZmianaProcent") &&
                    e.Value != null && e.Value.ToString() != "—")
                {
                    string val = e.Value.ToString();
                    decimal numVal = 0;
                    string numStr = val.Replace("zł", "").Replace("%", "").Trim();

                    if (decimal.TryParse(numStr, out numVal))
                    {
                        if (numVal > 0)
                        {
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#27ae60");
                        }
                        else if (numVal < 0)
                        {
                            e.CellStyle.ForeColor = ColorTranslator.FromHtml("#e74c3c");
                        }
                    }
                }
            }
        }

        private void BtnEksportuj_Click(object? sender, EventArgs e)
        {
            if (dataGridView.Rows.Count == 0)
            {
                MessageBox.Show("ℹ Brak danych do eksportu", "Informacja",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV files (*.csv)|*.csv";
                saveDialog.FileName = $"Historia_Cen_{currentHandlowiec}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var csv = new System.Text.StringBuilder();

                        // Nagłówki
                        var headers = new List<string>();
                        foreach (DataGridViewColumn col in dataGridView.Columns)
                        {
                            if (col.Visible && col.Name != "IsGroupRow" && col.Name != "DataSort")
                                headers.Add(col.HeaderText.Replace("\n", " "));
                        }
                        csv.AppendLine(string.Join(";", headers));

                        // Dane
                        foreach (DataGridViewRow row in dataGridView.Rows)
                        {
                            var cells = new List<string>();
                            foreach (DataGridViewColumn col in dataGridView.Columns)
                            {
                                if (col.Visible && col.Name != "IsGroupRow" && col.Name != "DataSort")
                                {
                                    string cellValue = row.Cells[col.Name].Value?.ToString() ?? "";
                                    cells.Add(cellValue);
                                }
                            }
                            csv.AppendLine(string.Join(";", cells));
                        }

                        System.IO.File.WriteAllText(saveDialog.FileName, csv.ToString(), System.Text.Encoding.UTF8);
                        MessageBox.Show("✓ Eksport zakończony pomyślnie!", "Sukces",
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"❌ Błąd eksportu: {ex.Message}", "Błąd",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }

    // NOWA KLASA: Formularz do wyświetlania szczegółów płatności kontrahenta
    public class FormSzczegolyPlatnosci : Form
    {
        private DataGridView dataGridViewSzczegoly;
        private string connectionString;
        private string nazwaKontrahenta;

        public FormSzczegolyPlatnosci(string connString, string kontrahent)
        {
            this.connectionString = connString;
            this.nazwaKontrahenta = kontrahent;
            InitializeComponent();
            this.Load += FormSzczegolyPlatnosci_Load;
        }

        private void InitializeComponent()
        {
            this.dataGridViewSzczegoly = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSzczegoly)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridViewSzczegoly
            // 
            this.dataGridViewSzczegoly.AllowUserToAddRows = false;
            this.dataGridViewSzczegoly.AllowUserToDeleteRows = false;
            this.dataGridViewSzczegoly.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewSzczegoly.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridViewSzczegoly.Location = new System.Drawing.Point(0, 0);
            this.dataGridViewSzczegoly.Name = "dataGridViewSzczegoly";
            this.dataGridViewSzczegoly.ReadOnly = true;
            this.dataGridViewSzczegoly.RowTemplate.Height = 25;
            this.dataGridViewSzczegoly.Size = new System.Drawing.Size(1184, 561);
            this.dataGridViewSzczegoly.TabIndex = 0;
            this.dataGridViewSzczegoly.RowPrePaint += DataGridViewSzczegoly_RowPrePaint;
            // 
            // FormSzczegolyPlatnosci
            // 
            this.ClientSize = new System.Drawing.Size(1184, 561);
            this.Controls.Add(this.dataGridViewSzczegoly);
            this.Name = "FormSzczegolyPlatnosci";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Szczegóły płatności dla: " + this.nazwaKontrahenta;
            this.Font = new Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(900, 400);

            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewSzczegoly)).EndInit();
            this.ResumeLayout(false);
        }

        private void FormSzczegolyPlatnosci_Load(object sender, EventArgs e)
        {
            WczytajSzczegoly();
            KonfigurujWygladDataGridView();
        }

        private void WczytajSzczegoly()
        {
            // ZMODYFIKOWANE ZAPYTANIE: Dodano kolumnę DniTerminu
            string query = @"
            WITH PNAgg AS (
                SELECT
                    PN.dkid,
                    SUM(ISNULL(PN.kwotarozl, 0)) AS KwotaRozliczona
                FROM [HANDEL].[HM].[PN] AS PN
                GROUP BY PN.dkid
            )
            SELECT
                DK.kod AS NumerDokumentu,
                CONVERT(date, DK.data) AS DataDokumentu,
                CAST(DK.walbrutto AS DECIMAL(18, 2)) AS WartoscBrutto,
                CAST(ISNULL(PA.KwotaRozliczona, 0) AS DECIMAL(18, 2)) AS Zaplacono,
                CAST((DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) AS DECIMAL(18, 2)) AS PozostaloDoZaplaty,
                CONVERT(date, DK.plattermin) AS TerminPlatnosci,
                DATEDIFF(day, CONVERT(date, DK.data), CONVERT(date, DK.plattermin)) AS DniTerminu,
                CASE
                    WHEN GETDATE() > DK.plattermin THEN DATEDIFF(day, DK.plattermin, GETDATE())
                    ELSE 0
                END AS DniPoTerminie
            FROM [HANDEL].[HM].[DK] AS DK
            JOIN [HANDEL].[SSCommon].[STContractors] AS C ON DK.khid = C.id
            LEFT JOIN PNAgg AS PA ON DK.id = PA.dkid
            WHERE
                DK.anulowany = 0
                AND (DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) > 0.01
                AND C.Shortcut = @NazwaKontrahenta
            ORDER BY
                TerminPlatnosci ASC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@NazwaKontrahenta", this.nazwaKontrahenta);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    // Dodanie wiersza podsumowującego
                    if (dt.Rows.Count > 0)
                    {
                        decimal sumaWartoscBrutto = dt.AsEnumerable().Sum(row => row.Field<decimal>("WartoscBrutto"));
                        decimal sumaZaplacono = dt.AsEnumerable().Sum(row => row.Field<decimal>("Zaplacono"));
                        decimal sumaPozostalo = dt.AsEnumerable().Sum(row => row.Field<decimal>("PozostaloDoZaplaty"));

                        DataRow sumaRow = dt.NewRow();
                        sumaRow["NumerDokumentu"] = "📊 RAZEM:";
                        sumaRow["WartoscBrutto"] = sumaWartoscBrutto;
                        sumaRow["Zaplacono"] = sumaZaplacono;
                        sumaRow["PozostaloDoZaplaty"] = sumaPozostalo;
                        dt.Rows.Add(sumaRow);
                    }

                    dataGridViewSzczegoly.DataSource = dt;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas wczytywania szczegółów płatności: " + ex.Message, "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void KonfigurujWygladDataGridView()
        {
            dataGridViewSzczegoly.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewSzczegoly.RowHeadersVisible = false;
            dataGridViewSzczegoly.EnableHeadersVisualStyles = false;

            var headerStyle = dataGridViewSzczegoly.ColumnHeadersDefaultCellStyle;
            headerStyle.BackColor = ColorTranslator.FromHtml("#2c3e50");
            headerStyle.ForeColor = Color.White;
            headerStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dataGridViewSzczegoly.ColumnHeadersHeight = 35;

            dataGridViewSzczegoly.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#ecf0f1");
            dataGridViewSzczegoly.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#3498db");
            dataGridViewSzczegoly.GridColor = ColorTranslator.FromHtml("#bdc3c7");

            // Formatowanie i nazewnictwo kolumn
            if (dataGridViewSzczegoly.Columns.Contains("WartoscBrutto"))
            {
                dataGridViewSzczegoly.Columns["WartoscBrutto"].DefaultCellStyle.Format = "c2";
                dataGridViewSzczegoly.Columns["WartoscBrutto"].HeaderText = "Wartość Brutto";
            }
            if (dataGridViewSzczegoly.Columns.Contains("Zaplacono"))
            {
                dataGridViewSzczegoly.Columns["Zaplacono"].DefaultCellStyle.Format = "c2";
            }
            if (dataGridViewSzczegoly.Columns.Contains("PozostaloDoZaplaty"))
            {
                dataGridViewSzczegoly.Columns["PozostaloDoZaplaty"].DefaultCellStyle.Format = "c2";
                dataGridViewSzczegoly.Columns["PozostaloDoZaplaty"].HeaderText = "Pozostało do zapłaty";
            }
            if (dataGridViewSzczegoly.Columns.Contains("DataDokumentu"))
            {
                dataGridViewSzczegoly.Columns["DataDokumentu"].HeaderText = "Data Dokumentu";
            }
            if (dataGridViewSzczegoly.Columns.Contains("NumerDokumentu"))
            {
                dataGridViewSzczegoly.Columns["NumerDokumentu"].HeaderText = "Numer Dokumentu";
            }
            if (dataGridViewSzczegoly.Columns.Contains("TerminPlatnosci"))
            {
                dataGridViewSzczegoly.Columns["TerminPlatnosci"].HeaderText = "Termin Płatności";
            }
            // NOWA SEKCJA: Konfiguracja nowej kolumny
            if (dataGridViewSzczegoly.Columns.Contains("DniTerminu"))
            {
                dataGridViewSzczegoly.Columns["DniTerminu"].HeaderText = "Termin (dni)";
                dataGridViewSzczegoly.Columns["DniTerminu"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (dataGridViewSzczegoly.Columns.Contains("DniPoTerminie"))
            {
                dataGridViewSzczegoly.Columns["DniPoTerminie"].HeaderText = "Dni po terminie";
                dataGridViewSzczegoly.Columns["DniPoTerminie"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            }
        }

        // Metoda do stylizacji wiersza podsumowania
        private void DataGridViewSzczegoly_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dataGridViewSzczegoly.Rows.Count) return;

            var row = dataGridViewSzczegoly.Rows[e.RowIndex];
            if (row.Cells["NumerDokumentu"].Value?.ToString() == "📊 RAZEM:")
            {
                row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#d5dbdb");
                row.DefaultCellStyle.Font = new Font(dataGridViewSzczegoly.Font, FontStyle.Bold);
            }
            else
            {
                // Kolorowanie wierszy z przekroczonym terminem
                if (dataGridViewSzczegoly.Columns.Contains("DniPoTerminie") && row.Cells["DniPoTerminie"].Value is int dniPoTerminie && dniPoTerminie > 0)
                {
                    row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#ffebee"); // Jasnoczerwone tło
                    row.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#c62828"); // Ciemnoczerwony tekst
                }
            }
        }
    }
}