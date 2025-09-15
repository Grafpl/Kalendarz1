// Plik: Transport/transport-editor.cs
// Kompletny edytor kursu transportowego z obsługą ładunków
// WERSJA POPRAWIONA - bez duplikacji klas

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kalendarz1.Transport.Repozytorium;
using Kalendarz1.Transport.Pakowanie;

namespace Kalendarz1.Transport.Formularze
{
    public partial class EdytorKursu : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private long? _kursId;
        private readonly string _uzytkownik;
        private Kurs _kurs;
        private List<Ladunek> _ladunki;
        private List<Kierowca> _kierowcy;
        private List<Pojazd> _pojazdy;

        // Główne kontrolki
        private DateTimePicker dtpDataKursu;
        private ComboBox cboKierowca;
        private ComboBox cboPojazd;
        private TextBox txtTrasa;
        private DateTimePicker dtpGodzWyjazdu;
        private DateTimePicker dtpGodzPowrotu;
        private ComboBox cboStatus;
        private NumericUpDown nudPlanE2NaPalete;

        // Kontrolki ładunków
        private DataGridView dgvLadunki;
        private TextBox txtKodKlienta;
        private NumericUpDown nudPojemnikiE2;
        private NumericUpDown nudPaletyH1;
        private NumericUpDown nudPlanE2Override;
        private TextBox txtUwagiLadunek;
        private Button btnDodajLadunek;
        private Button btnUsunLadunek;
        private Button btnEdytujLadunek;

        // Kontrolki statystyk
        private Label lblSumaE2;
        private Label lblPaletyNominal;
        private Label lblPaletyMax;
        private Label lblProcNominal;
        private Label lblProcMax;
        private ProgressBar prgWypelnienie;

        // Przyciski akcji
        private Button btnZapisz;
        private Button btnAnuluj;
        private Button btnDodajZamowienia;
        private Button btnOptymalizuj;

        // Główny konstruktor - kompatybilny z transport-panel-main.cs
        public EdytorKursu(TransportRepozytorium repozytorium, long? kursId = null, DateTime? data = null, string uzytkownik = null)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            _kursId = kursId;
            _uzytkownik = uzytkownik ?? Environment.UserName;

            InitializeComponent();
            ConfigureForm();

            // Ustaw datę jeśli przekazana
            if (data.HasValue)
                dtpDataKursu.Value = data.Value;
            else
                dtpDataKursu.Value = DateTime.Today;

            _ = LoadDataAsync();
        }

        // Konstruktor dla nowego kursu z datą
        public EdytorKursu(TransportRepozytorium repozytorium, DateTime data, string uzytkownik = null)
            : this(repozytorium, null, data, uzytkownik)
        {
        }

        // Konstruktor dla edycji istniejącego kursu
        public EdytorKursu(TransportRepozytorium repozytorium, Kurs kurs, string uzytkownik = null)
            : this(repozytorium, kurs?.KursID, kurs?.DataKursu, uzytkownik)
        {
            _kurs = kurs;
        }

        private void InitializeComponent()
        {
            Text = _kursId.HasValue ? "Edycja kursu" : "Nowy kurs";
            Size = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            // Panel główny
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); // Nagłówek
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Lista ładunków
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // Przyciski

            // ========== PANEL NAGŁÓWKA ==========
            var panelNaglowek = new GroupBox
            {
                Text = "Dane kursu",
                Dock = DockStyle.Fill
            };

            var layoutNaglowek = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                Padding = new Padding(5)
            };

            // Wiersz 1
            layoutNaglowek.Controls.Add(new Label { Text = "Data kursu:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            dtpDataKursu = new DateTimePicker { Format = DateTimePickerFormat.Short };
            layoutNaglowek.Controls.Add(dtpDataKursu, 1, 0);

            layoutNaglowek.Controls.Add(new Label { Text = "Status:", TextAlign = ContentAlignment.MiddleRight }, 2, 0);
            cboStatus = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Items = { "Planowany", "Potwierdzony", "W realizacji", "Zakończony", "Anulowany" }
            };
            layoutNaglowek.Controls.Add(cboStatus, 3, 0);

            // Wiersz 2
            layoutNaglowek.Controls.Add(new Label { Text = "Kierowca:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            cboKierowca = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "PelneNazwisko" };
            layoutNaglowek.Controls.Add(cboKierowca, 1, 1);

            layoutNaglowek.Controls.Add(new Label { Text = "Pojazd:", TextAlign = ContentAlignment.MiddleRight }, 2, 1);
            cboPojazd = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Opis" };
            cboPojazd.SelectedIndexChanged += CboPojazd_SelectedIndexChanged;
            layoutNaglowek.Controls.Add(cboPojazd, 3, 1);

            // Wiersz 3
            layoutNaglowek.Controls.Add(new Label { Text = "Trasa:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            txtTrasa = new TextBox { Dock = DockStyle.Fill };
            layoutNaglowek.SetColumnSpan(txtTrasa, 3);
            layoutNaglowek.Controls.Add(txtTrasa, 1, 2);

            // Wiersz 4
            layoutNaglowek.Controls.Add(new Label { Text = "Godz. wyjazdu:", TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            dtpGodzWyjazdu = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true };
            layoutNaglowek.Controls.Add(dtpGodzWyjazdu, 1, 3);

            layoutNaglowek.Controls.Add(new Label { Text = "Godz. powrotu:", TextAlign = ContentAlignment.MiddleRight }, 2, 3);
            dtpGodzPowrotu = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true };
            layoutNaglowek.Controls.Add(dtpGodzPowrotu, 3, 3);

            panelNaglowek.Controls.Add(layoutNaglowek);
            mainLayout.Controls.Add(panelNaglowek, 0, 0);

            // ========== PANEL STATYSTYK ==========
            var panelStatystyki = new GroupBox
            {
                Text = "Wypełnienie pojazdu",
                Dock = DockStyle.Fill
            };

            var layoutStatystyki = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(5)
            };

            layoutStatystyki.Controls.Add(new Label { Text = "Plan E2/paletę:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            nudPlanE2NaPalete = new NumericUpDown { Minimum = 1, Maximum = 50, Value = 36 };
            nudPlanE2NaPalete.ValueChanged += async (s, e) => await AktualizujStatystyki();
            layoutStatystyki.Controls.Add(nudPlanE2NaPalete, 1, 0);

            layoutStatystyki.Controls.Add(new Label { Text = "Suma E2:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            lblSumaE2 = new Label { Text = "0", Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            layoutStatystyki.Controls.Add(lblSumaE2, 1, 1);

            layoutStatystyki.Controls.Add(new Label { Text = "Palety (nominal):", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            lblPaletyNominal = new Label { Text = "0", Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            layoutStatystyki.Controls.Add(lblPaletyNominal, 1, 2);

            layoutStatystyki.Controls.Add(new Label { Text = "Palety (max):", TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            lblPaletyMax = new Label { Text = "0" };
            layoutStatystyki.Controls.Add(lblPaletyMax, 1, 3);

            layoutStatystyki.Controls.Add(new Label { Text = "Wypełnienie (nom):", TextAlign = ContentAlignment.MiddleRight }, 0, 4);
            lblProcNominal = new Label { Text = "0%", Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            layoutStatystyki.Controls.Add(lblProcNominal, 1, 4);

            layoutStatystyki.Controls.Add(new Label { Text = "Wypełnienie (max):", TextAlign = ContentAlignment.MiddleRight }, 0, 5);
            lblProcMax = new Label { Text = "0%" };
            layoutStatystyki.Controls.Add(lblProcMax, 1, 5);

            prgWypelnienie = new ProgressBar { Minimum = 0, Maximum = 120, Height = 25 };
            layoutStatystyki.SetColumnSpan(prgWypelnienie, 2);
            layoutStatystyki.Controls.Add(prgWypelnienie, 0, 6);

            panelStatystyki.Controls.Add(layoutStatystyki);
            mainLayout.Controls.Add(panelStatystyki, 1, 0);

            // ========== PANEL ŁADUNKÓW ==========
            var panelLadunki = new GroupBox
            {
                Text = "Ładunki",
                Dock = DockStyle.Fill
            };

            var splitLadunki = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 250
            };

            // Grid z ładunkami
            dgvLadunki = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ReadOnly = true
            };
            dgvLadunki.SelectionChanged += DgvLadunki_SelectionChanged;
            splitLadunki.Panel1.Controls.Add(dgvLadunki);

            // Panel dodawania/edycji ładunku
            var panelEdycjaLadunku = new GroupBox
            {
                Text = "Dodaj/Edytuj ładunek",
                Dock = DockStyle.Fill
            };

            var layoutEdycja = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4,
                Padding = new Padding(5)
            };

            layoutEdycja.Controls.Add(new Label { Text = "Kod klienta:", TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            txtKodKlienta = new TextBox { Dock = DockStyle.Fill };
            layoutEdycja.Controls.Add(txtKodKlienta, 1, 0);

            layoutEdycja.Controls.Add(new Label { Text = "Pojemniki E2:", TextAlign = ContentAlignment.MiddleRight }, 2, 0);
            nudPojemnikiE2 = new NumericUpDown { Minimum = 0, Maximum = 1000, Dock = DockStyle.Fill };
            layoutEdycja.Controls.Add(nudPojemnikiE2, 3, 0);

            layoutEdycja.Controls.Add(new Label { Text = "Palety H1:", TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            nudPaletyH1 = new NumericUpDown { Minimum = 0, Maximum = 100, Dock = DockStyle.Fill };
            layoutEdycja.Controls.Add(nudPaletyH1, 1, 1);

            layoutEdycja.Controls.Add(new Label { Text = "E2/paletę (override):", TextAlign = ContentAlignment.MiddleRight }, 2, 1);
            nudPlanE2Override = new NumericUpDown { Minimum = 0, Maximum = 50, Dock = DockStyle.Fill };
            layoutEdycja.Controls.Add(nudPlanE2Override, 3, 1);

            layoutEdycja.Controls.Add(new Label { Text = "Uwagi:", TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            txtUwagiLadunek = new TextBox { Dock = DockStyle.Fill, Multiline = true };
            layoutEdycja.SetColumnSpan(txtUwagiLadunek, 3);
            layoutEdycja.Controls.Add(txtUwagiLadunek, 1, 2);

            // Przyciski akcji dla ładunku
            var panelPrzyciskiLadunek = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            btnDodajLadunek = new Button { Text = "Dodaj", Width = 80, Height = 30 };
            btnDodajLadunek.Click += BtnDodajNowyLadunek_Click;

            btnEdytujLadunek = new Button { Text = "Zapisz zmiany", Width = 100, Height = 30, Enabled = false };
            btnEdytujLadunek.Click += BtnEdytujLadunek_Click;

            btnUsunLadunek = new Button { Text = "Usuń", Width = 80, Height = 30, Enabled = false };
            btnUsunLadunek.Click += BtnUsunLadunek_Click;

            btnDodajZamowienia = new Button { Text = "Dodaj zamówienia...", Width = 130, Height = 30 };
            btnDodajZamowienia.Click += BtnDodajZamowienia_Click;

            panelPrzyciskiLadunek.Controls.Add(btnDodajLadunek);
            panelPrzyciskiLadunek.Controls.Add(btnEdytujLadunek);
            panelPrzyciskiLadunek.Controls.Add(btnUsunLadunek);
            panelPrzyciskiLadunek.Controls.Add(btnDodajZamowienia);

            layoutEdycja.SetColumnSpan(panelPrzyciskiLadunek, 4);
            layoutEdycja.Controls.Add(panelPrzyciskiLadunek, 0, 3);

            panelEdycjaLadunku.Controls.Add(layoutEdycja);
            splitLadunki.Panel2.Controls.Add(panelEdycjaLadunku);

            panelLadunki.Controls.Add(splitLadunki);
            mainLayout.SetColumnSpan(panelLadunki, 2);
            mainLayout.Controls.Add(panelLadunki, 0, 1);

            // ========== PANEL PRZYCISKÓW ==========
            var panelPrzyciski = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            btnZapisz = new Button
            {
                Text = "Zapisz",
                Width = 100,
                Height = 35,
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnZapisz.Click += BtnZapisz_Click;

            btnAnuluj = new Button
            {
                Text = "Anuluj",
                Width = 100,
                Height = 35
            };
            btnAnuluj.Click += (s, e) => Close();

            btnOptymalizuj = new Button
            {
                Text = "Optymalizuj",
                Width = 100,
                Height = 35
            };
            btnOptymalizuj.Click += BtnOptymalizuj_Click;

            panelPrzyciski.Controls.Add(btnZapisz);
            panelPrzyciski.Controls.Add(btnAnuluj);
            panelPrzyciski.Controls.Add(btnOptymalizuj);

            mainLayout.SetColumnSpan(panelPrzyciski, 2);
            mainLayout.Controls.Add(panelPrzyciski, 0, 2);

            Controls.Add(mainLayout);
        }

        private void ConfigureForm()
        {
            // Ustawienia domyślne
            cboStatus.SelectedIndex = 0;
            dtpGodzWyjazdu.Value = DateTime.Today.AddHours(6);
            dtpGodzPowrotu.Value = DateTime.Today.AddHours(18);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                // Załaduj słowniki
                _kierowcy = await _repozytorium.PobierzKierowcowAsync(true);
                _pojazdy = await _repozytorium.PobierzPojazdyAsync(true);

                // Wypełnij combobox'y
                cboKierowca.DataSource = _kierowcy;
                cboPojazd.DataSource = _pojazdy;

                // Jeśli edycja - załaduj dane kursu
                if (_kursId.HasValue && _kursId.Value > 0)
                {
                    await LoadKursData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task LoadKursData()
        {
            if (!_kursId.HasValue) return;

            try
            {
                // Pobierz dane kursu
                var kursy = await _repozytorium.PobierzKursyPoDacieAsync(DateTime.Today.AddDays(-30));
                _kurs = kursy.FirstOrDefault(k => k.KursID == _kursId.Value);

                if (_kurs == null)
                {
                    MessageBox.Show("Nie znaleziono kursu o podanym ID.",
                        "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Ustaw wartości w kontrolkach
                dtpDataKursu.Value = _kurs.DataKursu;
                cboKierowca.SelectedValue = _kurs.KierowcaID;
                cboPojazd.SelectedValue = _kurs.PojazdID;
                txtTrasa.Text = _kurs.Trasa;
                cboStatus.Text = _kurs.Status;
                nudPlanE2NaPalete.Value = _kurs.PlanE2NaPalete;

                if (_kurs.GodzWyjazdu.HasValue)
                    dtpGodzWyjazdu.Value = DateTime.Today.Add(_kurs.GodzWyjazdu.Value);
                if (_kurs.GodzPowrotu.HasValue)
                    dtpGodzPowrotu.Value = DateTime.Today.Add(_kurs.GodzPowrotu.Value);

                // Załaduj ładunki
                await LoadLadunki();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania kursu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadLadunki()
        {
            if (!_kursId.HasValue) return;

            try
            {
                _ladunki = await _repozytorium.PobierzLadunkiAsync(_kursId.Value);

                var dt = new DataTable();
                dt.Columns.Add("LadunekID", typeof(long));
                dt.Columns.Add("Kolejnosc", typeof(int));
                dt.Columns.Add("KodKlienta", typeof(string));
                dt.Columns.Add("PojemnikiE2", typeof(int));
                dt.Columns.Add("PaletyH1", typeof(int));
                dt.Columns.Add("Uwagi", typeof(string));

                foreach (var ladunek in _ladunki.OrderBy(l => l.Kolejnosc))
                {
                    dt.Rows.Add(
                        ladunek.LadunekID,
                        ladunek.Kolejnosc,
                        ladunek.KodKlienta,
                        ladunek.PojemnikiE2,
                        ladunek.PaletyH1 ?? 0,
                        ladunek.Uwagi
                    );
                }

                dgvLadunki.DataSource = dt;

                // Ukryj kolumnę ID
                if (dgvLadunki.Columns["LadunekID"] != null)
                    dgvLadunki.Columns["LadunekID"].Visible = false;

                await AktualizujStatystyki();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania ładunków: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnDodajNowyLadunek_Click(object sender, EventArgs e)
        {
            try
            {
                // Walidacja
                if (string.IsNullOrWhiteSpace(txtKodKlienta.Text))
                {
                    MessageBox.Show("Proszę podać kod klienta.",
                        "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtKodKlienta.Focus();
                    return;
                }

                if (nudPojemnikiE2.Value <= 0)
                {
                    MessageBox.Show("Proszę podać liczbę pojemników E2.",
                        "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    nudPojemnikiE2.Focus();
                    return;
                }

                // Jeśli nowy kurs - najpierw go zapisz
                if (!_kursId.HasValue || _kursId.Value <= 0)
                {
                    if (MessageBox.Show("Aby dodać ładunek, kurs musi być najpierw zapisany. Zapisać teraz?",
                        "Zapisać kurs?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        await SaveKurs();
                        if (!_kursId.HasValue || _kursId.Value <= 0)
                            return;
                    }
                    else
                    {
                        return;
                    }
                }

                // Utwórz nowy ładunek
                var nowyLadunek = new Ladunek
                {
                    KursID = _kursId.Value,
                    KodKlienta = txtKodKlienta.Text.Trim(),
                    PojemnikiE2 = (int)nudPojemnikiE2.Value,
                    PaletyH1 = nudPaletyH1.Value > 0 ? (int?)nudPaletyH1.Value : null,
                    PlanE2NaPaleteOverride = nudPlanE2Override.Value > 0 ? (byte?)nudPlanE2Override.Value : null,
                    Uwagi = string.IsNullOrWhiteSpace(txtUwagiLadunek.Text) ? null : txtUwagiLadunek.Text.Trim()
                };

                // Zapisz do bazy
                Cursor = Cursors.WaitCursor;
                await _repozytorium.DodajLadunekAsync(nowyLadunek);

                // Odśwież listę
                await LoadLadunki();

                // Wyczyść formularz
                ClearLadunekForm();

                MessageBox.Show("Ładunek został dodany.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania ładunku: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async void BtnEdytujLadunek_Click(object sender, EventArgs e)
        {
            if (dgvLadunki.CurrentRow == null) return;

            try
            {
                var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["LadunekID"].Value);
                var ladunek = _ladunki.FirstOrDefault(l => l.LadunekID == ladunekId);

                if (ladunek == null) return;

                // Aktualizuj dane
                ladunek.KodKlienta = txtKodKlienta.Text.Trim();
                ladunek.PojemnikiE2 = (int)nudPojemnikiE2.Value;
                ladunek.PaletyH1 = nudPaletyH1.Value > 0 ? (int?)nudPaletyH1.Value : null;
                ladunek.PlanE2NaPaleteOverride = nudPlanE2Override.Value > 0 ? (byte?)nudPlanE2Override.Value : null;
                ladunek.Uwagi = string.IsNullOrWhiteSpace(txtUwagiLadunek.Text) ? null : txtUwagiLadunek.Text.Trim();

                // Zapisz do bazy
                Cursor = Cursors.WaitCursor;
                await _repozytorium.AktualizujLadunekAsync(ladunek);

                // Odśwież listę
                await LoadLadunki();

                MessageBox.Show("Ładunek został zaktualizowany.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas aktualizacji ładunku: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async void BtnUsunLadunek_Click(object sender, EventArgs e)
        {
            if (dgvLadunki.CurrentRow == null) return;

            if (MessageBox.Show("Czy na pewno usunąć wybrany ładunek?",
                "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var ladunekId = Convert.ToInt64(dgvLadunki.CurrentRow.Cells["LadunekID"].Value);

                Cursor = Cursors.WaitCursor;
                await _repozytorium.UsunLadunekAsync(ladunekId);

                // Odśwież listę
                await LoadLadunki();

                MessageBox.Show("Ładunek został usunięty.",
                    "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas usuwania ładunku: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void DgvLadunki_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvLadunki.CurrentRow == null)
            {
                btnEdytujLadunek.Enabled = false;
                btnUsunLadunek.Enabled = false;
                ClearLadunekForm();
                return;
            }

            btnEdytujLadunek.Enabled = true;
            btnUsunLadunek.Enabled = true;

            // Wypełnij formularz danymi wybranego ładunku
            var row = dgvLadunki.CurrentRow;
            txtKodKlienta.Text = row.Cells["KodKlienta"].Value?.ToString() ?? "";
            nudPojemnikiE2.Value = Convert.ToInt32(row.Cells["PojemnikiE2"].Value ?? 0);
            nudPaletyH1.Value = Convert.ToInt32(row.Cells["PaletyH1"].Value ?? 0);
            txtUwagiLadunek.Text = row.Cells["Uwagi"].Value?.ToString() ?? "";

            // Znajdź ładunek dla override
            var ladunekId = Convert.ToInt64(row.Cells["LadunekID"].Value);
            var ladunek = _ladunki?.FirstOrDefault(l => l.LadunekID == ladunekId);
            nudPlanE2Override.Value = ladunek?.PlanE2NaPaleteOverride ?? 0;
        }

        private void ClearLadunekForm()
        {
            txtKodKlienta.Clear();
            nudPojemnikiE2.Value = 0;
            nudPaletyH1.Value = 0;
            nudPlanE2Override.Value = 0;
            txtUwagiLadunek.Clear();
        }

        private async void CboPojazd_SelectedIndexChanged(object sender, EventArgs e)
        {
            await AktualizujStatystyki();
        }

        private async Task AktualizujStatystyki()
        {
            try
            {
                if (!_kursId.HasValue || _kursId.Value <= 0)
                {
                    // Brak kursu - wyzeruj statystyki
                    lblSumaE2.Text = "0";
                    lblPaletyNominal.Text = "0";
                    lblPaletyMax.Text = "0";
                    lblProcNominal.Text = "0%";
                    lblProcMax.Text = "0%";
                    prgWypelnienie.Value = 0;
                    return;
                }

                // Oblicz pakowanie
                var wynik = await _repozytorium.ObliczPakowanieKursuAsync(_kursId.Value);

                // Aktualizuj etykiety
                lblSumaE2.Text = wynik.SumaE2.ToString();
                lblPaletyNominal.Text = wynik.PaletyNominal.ToString();
                lblPaletyMax.Text = wynik.PaletyMax.ToString();
                lblProcNominal.Text = $"{wynik.ProcNominal:F1}%";
                lblProcMax.Text = $"{wynik.ProcMax:F1}%";

                // Aktualizuj progress bar
                prgWypelnienie.Value = Math.Min(120, (int)wynik.ProcNominal);

                // Kolorowanie w zależności od wypełnienia
                if (wynik.ProcNominal > 100)
                {
                    lblProcNominal.ForeColor = Color.Red;
                    prgWypelnienie.ForeColor = Color.Red;
                }
                else if (wynik.ProcNominal > 90)
                {
                    lblProcNominal.ForeColor = Color.Orange;
                    prgWypelnienie.ForeColor = Color.Orange;
                }
                else
                {
                    lblProcNominal.ForeColor = Color.Green;
                    prgWypelnienie.ForeColor = Color.Green;
                }
            }
            catch (Exception ex)
            {
                // Logowanie błędu, ale nie przerywanie działania
                System.Diagnostics.Debug.WriteLine($"Błąd aktualizacji statystyk: {ex.Message}");
            }
        }

        private async void BtnZapisz_Click(object sender, EventArgs e)
        {
            try
            {
                // Walidacja
                if (cboKierowca.SelectedItem == null)
                {
                    MessageBox.Show("Proszę wybrać kierowcę.",
                        "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cboKierowca.Focus();
                    return;
                }

                if (cboPojazd.SelectedItem == null)
                {
                    MessageBox.Show("Proszę wybrać pojazd.",
                        "Brak danych", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cboPojazd.Focus();
                    return;
                }

                Cursor = Cursors.WaitCursor;
                await SaveKurs();

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private async Task SaveKurs()
        {
            try
            {
                var kierowca = cboKierowca.SelectedItem as Kierowca;
                var pojazd = cboPojazd.SelectedItem as Pojazd;

                if (kierowca == null || pojazd == null)
                {
                    throw new InvalidOperationException("Nie wybrano kierowcy lub pojazdu.");
                }

                var kurs = new Kurs
                {
                    KursID = _kursId ?? 0,
                    DataKursu = dtpDataKursu.Value.Date,
                    KierowcaID = kierowca.KierowcaID,
                    PojazdID = pojazd.PojazdID,
                    Trasa = string.IsNullOrWhiteSpace(txtTrasa.Text) ? null : txtTrasa.Text.Trim(),
                    GodzWyjazdu = dtpGodzWyjazdu.Value.TimeOfDay,
                    GodzPowrotu = dtpGodzPowrotu.Value.TimeOfDay,
                    Status = cboStatus.Text,
                    PlanE2NaPalete = (byte)nudPlanE2NaPalete.Value
                };

                if (_kursId.HasValue && _kursId.Value > 0)
                {
                    // Aktualizacja
                    await _repozytorium.AktualizujNaglowekKursuAsync(kurs, _uzytkownik);
                }
                else
                {
                    // Nowy kurs
                    _kursId = await _repozytorium.DodajKursAsync(kurs, _uzytkownik);
                    Text = "Edycja kursu";
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas zapisywania kursu do bazy danych: {ex.Message}", ex);
            }
        }

        private async void BtnDodajZamowienia_Click(object sender, EventArgs e)
        {
            try
            {
                // Najpierw zapisz kurs jeśli nowy
                if (!_kursId.HasValue || _kursId.Value <= 0)
                {
                    if (MessageBox.Show("Aby dodać zamówienia, kurs musi być najpierw zapisany. Zapisać teraz?",
                        "Zapisać kurs?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        await SaveKurs();
                        if (!_kursId.HasValue || _kursId.Value <= 0)
                            return;
                    }
                    else
                    {
                        return;
                    }
                }

                // Otwórz okno wyboru zamówień
                using var dlg = new WyborZamowienForm(_repozytorium, dtpDataKursu.Value, _kursId.Value);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // Odśwież listę ładunków
                    await LoadLadunki();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania zamówień: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnOptymalizuj_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_kursId.HasValue || _kursId.Value <= 0)
                {
                    MessageBox.Show("Najpierw zapisz kurs.",
                        "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (_ladunki == null || !_ladunki.Any())
                {
                    MessageBox.Show("Brak ładunków do optymalizacji.",
                        "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Cursor = Cursors.WaitCursor;

                // Tu można dodać logikę optymalizacji kolejności ładunków
                // np. sortowanie po trasie, wielkości, priorytetach itp.

                MessageBox.Show("Funkcja optymalizacji jest w przygotowaniu.",
                    "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas optymalizacji: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }

    // Formularz wyboru zamówień
    public class WyborZamowienForm : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private readonly DateTime _data;
        private readonly long _kursId;
        private DataGridView dgvZamowienia;
        private Button btnDodaj;
        private Button btnAnuluj;
        private List<ZamowienieTransport> _zamowienia;

        public WyborZamowienForm(TransportRepozytorium repozytorium, DateTime data, long kursId)
        {
            _repozytorium = repozytorium;
            _data = data;
            _kursId = kursId;
            InitializeComponent();
            _ = LoadZamowienia();
        }

        private void InitializeComponent()
        {
            Text = "Wybierz zamówienia do dodania";
            Size = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;

            dgvZamowienia = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var panelPrzyciski = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                FlowDirection = FlowDirection.RightToLeft
            };

            btnDodaj = new Button { Text = "Dodaj wybrane", Width = 120, Height = 35 };
            btnDodaj.Click += BtnDodaj_Click;

            btnAnuluj = new Button { Text = "Anuluj", Width = 100, Height = 35 };
            btnAnuluj.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            panelPrzyciski.Controls.Add(btnDodaj);
            panelPrzyciski.Controls.Add(btnAnuluj);

            Controls.Add(dgvZamowienia);
            Controls.Add(panelPrzyciski);
        }

        private async Task LoadZamowienia()
        {

        }

        private async void BtnDodaj_Click(object sender, EventArgs e)
        {
            if (dgvZamowienia.SelectedRows.Count == 0)
            {
                MessageBox.Show("Proszę wybrać zamówienia do dodania.",
                    "Brak wyboru", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
               
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania zamówień: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}