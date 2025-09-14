// Plik: Transport/TransportDialogs.cs
// Dialogi pomocnicze dla modułu transportu

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kalendarz1.Transport.Pakowanie;

namespace Kalendarz1.Transport
{
    /// <summary>
    /// Dialog do wyboru zamówienia z listy wolnych zamówień
    /// </summary>
    public class WybierzZamowienieDialog : Form
    {
        private DataGridView dgvZamowienia;
        private Button btnWybierz;
        private Button btnAnuluj;
        private TextBox txtFiltr;
        private Label lblFiltr;
        private Label lblInfo;
        private Panel panelBottom;
        private Panel panelTop;
        private CheckBox chkWielokrotnyWybor;

        public ZamowienieTransport? WybraneZamowienie { get; private set; }
        public List<ZamowienieTransport> WybraneZamowienia { get; private set; }
        private List<ZamowienieTransport> _zamowienia;
        private BindingSource _bindingSource;

        public WybierzZamowienieDialog(List<ZamowienieTransport> zamowienia)
        {
            _zamowienia = zamowienia ?? new List<ZamowienieTransport>();
            _bindingSource = new BindingSource();
            WybraneZamowienia = new List<ZamowienieTransport>();
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = "Wybierz zamówienie do dodania";
            Size = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Panel górny z filtrem
            panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(10)
            };

            lblFiltr = new Label
            {
                Text = "Filtruj:",
                Location = new Point(10, 17),
                AutoSize = true
            };

            txtFiltr = new TextBox
            {
                Location = new Point(60, 14),
                Width = 300,
                Font = new Font("Segoe UI", 10f)
            };
            txtFiltr.TextChanged += TxtFiltr_TextChanged;

            chkWielokrotnyWybor = new CheckBox
            {
                Text = "Wybór wielokrotny",
                Location = new Point(380, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f)
            };
            chkWielokrotnyWybor.CheckedChanged += ChkWielokrotnyWybor_CheckedChanged;

            lblInfo = new Label
            {
                Text = "",
                Location = new Point(520, 17),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 122, 204)
            };

            panelTop.Controls.AddRange(new Control[] { lblFiltr, txtFiltr, chkWielokrotnyWybor, lblInfo });

            // Grid z zamówieniami
            dgvZamowienia = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                GridColor = Color.FromArgb(224, 224, 224),
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 9f)
            };
            dgvZamowienia.DoubleClick += DgvZamowienia_DoubleClick;
            dgvZamowienia.SelectionChanged += DgvZamowienia_SelectionChanged;

            // Panel dolny z przyciskami
            panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            btnWybierz = new Button
            {
                Text = "Wybierz",
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnWybierz.Location = new Point(Width - btnWybierz.Width - 130, 10);
            btnWybierz.Click += BtnWybierz_Click;

            btnAnuluj = new Button
            {
                Text = "Anuluj",
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            btnAnuluj.Location = new Point(Width - btnAnuluj.Width - 20, 10);

            panelBottom.Controls.AddRange(new Control[] { btnWybierz, btnAnuluj });

            Controls.Add(dgvZamowienia);
            Controls.Add(panelBottom);
            Controls.Add(panelTop);
        }

        private void LoadData()
        {
            _bindingSource.DataSource = _zamowienia;
            dgvZamowienia.DataSource = _bindingSource;

            // Konfiguracja kolumn
            if (dgvZamowienia.Columns.Count > 0)
            {
                if (dgvZamowienia.Columns["ZamowienieID"] != null)
                {
                    dgvZamowienia.Columns["ZamowienieID"].HeaderText = "Nr zam.";
                    dgvZamowienia.Columns["ZamowienieID"].Width = 80;
                    dgvZamowienia.Columns["ZamowienieID"].FillWeight = 15;
                }

                if (dgvZamowienia.Columns["KlientID"] != null)
                    dgvZamowienia.Columns["KlientID"].Visible = false;

                if (dgvZamowienia.Columns["KlientNazwa"] != null)
                {
                    dgvZamowienia.Columns["KlientNazwa"].HeaderText = "Klient";
                    dgvZamowienia.Columns["KlientNazwa"].FillWeight = 35;
                }

                if (dgvZamowienia.Columns["DataZamowienia"] != null)
                {
                    dgvZamowienia.Columns["DataZamowienia"].HeaderText = "Data";
                    dgvZamowienia.Columns["DataZamowienia"].Width = 100;
                    dgvZamowienia.Columns["DataZamowienia"].DefaultCellStyle.Format = "yyyy-MM-dd";
                    dgvZamowienia.Columns["DataZamowienia"].FillWeight = 20;
                }

                if (dgvZamowienia.Columns["IloscKg"] != null)
                {
                    dgvZamowienia.Columns["IloscKg"].HeaderText = "Ilość (kg)";
                    dgvZamowienia.Columns["IloscKg"].Width = 100;
                    dgvZamowienia.Columns["IloscKg"].DefaultCellStyle.Format = "N0";
                    dgvZamowienia.Columns["IloscKg"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    dgvZamowienia.Columns["IloscKg"].FillWeight = 15;
                }

                if (dgvZamowienia.Columns["Status"] != null)
                {
                    dgvZamowienia.Columns["Status"].HeaderText = "Status";
                    dgvZamowienia.Columns["Status"].Width = 100;
                    dgvZamowienia.Columns["Status"].FillWeight = 15;
                }

                if (dgvZamowienia.Columns.Contains("Handlowiec"))
                {
                    dgvZamowienia.Columns["Handlowiec"].HeaderText = "Handlowiec";
                    dgvZamowienia.Columns["Handlowiec"].FillWeight = 20;
                }
            }

            // Pokoloruj wiersze według statusu
            dgvZamowienia.CellFormatting += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    var row = dgvZamowienia.Rows[e.RowIndex];
                    var status = row.Cells["Status"]?.Value?.ToString();

                    if (status == "Pilne")
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 240);
                        row.DefaultCellStyle.ForeColor = Color.DarkRed;
                    }
                }
            };
        }

        private void ChkWielokrotnyWybor_CheckedChanged(object? sender, EventArgs e)
        {
            dgvZamowienia.MultiSelect = chkWielokrotnyWybor.Checked;
            UpdateSelectionInfo();
        }

        private void DgvZamowienia_SelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            if (chkWielokrotnyWybor.Checked && dgvZamowienia.SelectedRows.Count > 0)
            {
                lblInfo.Text = $"Wybrano: {dgvZamowienia.SelectedRows.Count}";
            }
            else
            {
                lblInfo.Text = "";
            }
        }

        private void TxtFiltr_TextChanged(object? sender, EventArgs e)
        {
            var filtr = txtFiltr.Text.ToLower();
            if (string.IsNullOrWhiteSpace(filtr))
            {
                _bindingSource.DataSource = _zamowienia;
            }
            else
            {
                var filtered = _zamowienia.Where(z =>
                    z.KlientNazwa.ToLower().Contains(filtr) ||
                    z.ZamowienieID.ToString().Contains(filtr) ||
                    (z.Handlowiec?.ToLower().Contains(filtr) ?? false)
                ).ToList();
                _bindingSource.DataSource = filtered;
            }
            _bindingSource.ResetBindings(false);
        }

        private void DgvZamowienia_DoubleClick(object? sender, EventArgs e)
        {
            if (dgvZamowienia.CurrentRow != null)
            {
                if (chkWielokrotnyWybor.Checked)
                {
                    // W trybie wielokrotnym, dodaj tylko bieżący wiersz
                    WybraneZamowienia.Clear();
                    var zam = dgvZamowienia.CurrentRow.DataBoundItem as ZamowienieTransport;
                    if (zam != null)
                        WybraneZamowienia.Add(zam);
                }
                else
                {
                    WybraneZamowienie = dgvZamowienia.CurrentRow.DataBoundItem as ZamowienieTransport;
                    WybraneZamowienia.Clear();
                    if (WybraneZamowienie != null)
                        WybraneZamowienia.Add(WybraneZamowienie);
                }
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void BtnWybierz_Click(object? sender, EventArgs e)
        {
            if (chkWielokrotnyWybor.Checked)
            {
                // Tryb wielokrotny
                WybraneZamowienia.Clear();
                foreach (DataGridViewRow row in dgvZamowienia.SelectedRows)
                {
                    var zam = row.DataBoundItem as ZamowienieTransport;
                    if (zam != null)
                        WybraneZamowienia.Add(zam);
                }

                if (WybraneZamowienia.Count == 0)
                {
                    MessageBox.Show("Proszę wybrać co najmniej jedno zamówienie z listy.", "Brak wyboru",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                    return;
                }

                WybraneZamowienie = WybraneZamowienia.FirstOrDefault();
            }
            else
            {
                // Tryb pojedynczy
                if (dgvZamowienia.CurrentRow != null)
                {
                    WybraneZamowienie = dgvZamowienia.CurrentRow.DataBoundItem as ZamowienieTransport;
                    WybraneZamowienia.Clear();
                    if (WybraneZamowienie != null)
                        WybraneZamowienia.Add(WybraneZamowienie);
                }
                else
                {
                    MessageBox.Show("Proszę wybrać zamówienie z listy.", "Brak wyboru",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                }
            }
        }
    }

    /// <summary>
    /// Dialog do zmiany kolejności ładunków
    /// </summary>
    public class KolejnoscDialog : Form
    {
        private ListBox lstLadunki;
        private Button btnUp;
        private Button btnDown;
        private Button btnOK;
        private Button btnAnuluj;
        private Panel panelButtons;
        private Panel panelSide;
        private Label lblInstrukcja;

        public List<Ladunek> Ladunki { get; private set; }
        private List<Ladunek> _originalOrder;

        public KolejnoscDialog(List<Ladunek> ladunki)
        {
            Ladunki = ladunki.OrderBy(l => l.Kolejnosc).ToList();
            _originalOrder = Ladunki.Select(l => l.Clone()).ToList();
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            Text = "Zmień kolejność ładunków";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Instrukcja
            lblInstrukcja = new Label
            {
                Text = "Przeciągnij elementy lub użyj przycisków do zmiany kolejności:",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 5, 10, 5),
                Font = new Font("Segoe UI", 9f)
            };

            // Lista ładunków
            lstLadunki = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                ItemHeight = 30,
                DrawMode = DrawMode.OwnerDrawFixed,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            lstLadunki.DrawItem += LstLadunki_DrawItem;
            lstLadunki.SelectedIndexChanged += LstLadunki_SelectedIndexChanged;

            // Włącz drag & drop
            lstLadunki.AllowDrop = true;
            lstLadunki.MouseDown += LstLadunki_MouseDown;
            lstLadunki.DragOver += LstLadunki_DragOver;
            lstLadunki.DragDrop += LstLadunki_DragDrop;

            // Panel boczny z przyciskami góra/dół
            panelSide = new Panel
            {
                Dock = DockStyle.Right,
                Width = 120,
                Padding = new Padding(10)
            };

            btnUp = new Button
            {
                Text = "▲ W górę",
                Width = 100,
                Height = 40,
                Location = new Point(10, 50),
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnUp.Click += BtnUp_Click;

            btnDown = new Button
            {
                Text = "▼ W dół",
                Width = 100,
                Height = 40,
                Location = new Point(10, 100),
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnDown.Click += BtnDown_Click;

            panelSide.Controls.AddRange(new Control[] { btnUp, btnDown });

            // Panel dolny z przyciskami OK/Anuluj
            panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            btnOK = new Button
            {
                Text = "Zapisz kolejność",
                Width = 120,
                Height = 30,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOK.Location = new Point(Width - btnOK.Width - 150, 10);
            btnOK.Click += BtnOK_Click;

            btnAnuluj = new Button
            {
                Text = "Anuluj",
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            btnAnuluj.Location = new Point(Width - btnAnuluj.Width - 20, 10);

            panelButtons.Controls.AddRange(new Control[] { btnOK, btnAnuluj });

            Controls.Add(lstLadunki);
            Controls.Add(panelSide);
            Controls.Add(panelButtons);
            Controls.Add(lblInstrukcja);
        }

        private void LoadData()
        {
            lstLadunki.Items.Clear();
            foreach (var ladunek in Ladunki)
            {
                string display = $"{ladunek.Kolejnosc}. ";

                if (!string.IsNullOrEmpty(ladunek.KodKlienta))
                    display += $"[{ladunek.KodKlienta}] ";

                display += $"E2: {ladunek.PojemnikiE2}";

                if (ladunek.PaletyH1.HasValue && ladunek.PaletyH1.Value > 0)
                    display += $", H1: {ladunek.PaletyH1}";

                if (!string.IsNullOrEmpty(ladunek.Uwagi))
                    display += $" - {ladunek.Uwagi}";

                lstLadunki.Items.Add(display);
            }
        }

        private void LstLadunki_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();

            var text = lstLadunki.Items[e.Index]?.ToString() ?? "";
            var brush = (e.State & DrawItemState.Selected) != 0
                ? SystemBrushes.HighlightText
                : SystemBrushes.WindowText;

            // Rysuj numer kolejności w innym kolorze
            var parts = text.Split(new[] { ". " }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var numBrush = new SolidBrush(Color.FromArgb(0, 122, 204));
                var numFont = new Font(e.Font!, FontStyle.Bold);

                e.Graphics.DrawString(parts[0] + ". ", numFont,
                    (e.State & DrawItemState.Selected) != 0 ? brush : numBrush,
                    e.Bounds.X + 5, e.Bounds.Y + 5);

                var numSize = e.Graphics.MeasureString(parts[0] + ". ", numFont);
                e.Graphics.DrawString(parts[1], e.Font!, brush,
                    e.Bounds.X + 5 + numSize.Width, e.Bounds.Y + 5);
            }
            else
            {
                e.Graphics.DrawString(text, e.Font!, brush, e.Bounds.X + 5, e.Bounds.Y + 5);
            }

            e.DrawFocusRectangle();
        }

        private void LstLadunki_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var index = lstLadunki.SelectedIndex;
            btnUp.Enabled = index > 0;
            btnDown.Enabled = index >= 0 && index < lstLadunki.Items.Count - 1;
        }

        private void BtnUp_Click(object? sender, EventArgs e)
        {
            MoveItem(-1);
        }

        private void BtnDown_Click(object? sender, EventArgs e)
        {
            MoveItem(1);
        }

        private void MoveItem(int direction)
        {
            var index = lstLadunki.SelectedIndex;
            if (index < 0) return;

            var newIndex = index + direction;
            if (newIndex < 0 || newIndex >= lstLadunki.Items.Count) return;

            // Zamień elementy
            var temp = Ladunki[index];
            Ladunki[index] = Ladunki[newIndex];
            Ladunki[newIndex] = temp;

            // Zaktualizuj kolejność
            UpdateKolejnosc();

            // Odśwież listę
            LoadData();
            lstLadunki.SelectedIndex = newIndex;
        }

        private void UpdateKolejnosc()
        {
            for (int i = 0; i < Ladunki.Count; i++)
            {
                Ladunki[i].Kolejnosc = i + 1;
            }
        }

        // Drag & Drop
        private void LstLadunki_MouseDown(object? sender, MouseEventArgs e)
        {
            if (lstLadunki.SelectedItem != null)
            {
                lstLadunki.DoDragDrop(lstLadunki.SelectedIndex, DragDropEffects.Move);
            }
        }

        private void LstLadunki_DragOver(object? sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void LstLadunki_DragDrop(object? sender, DragEventArgs e)
        {
            var point = lstLadunki.PointToClient(new Point(e.X, e.Y));
            var index = lstLadunki.IndexFromPoint(point);

            if (index < 0) index = lstLadunki.Items.Count - 1;

            var data = e.Data?.GetData(typeof(int));
            if (data == null) return;

            var oldIndex = (int)data;

            if (oldIndex == index) return;

            var item = Ladunki[oldIndex];
            Ladunki.RemoveAt(oldIndex);
            Ladunki.Insert(index, item);

            UpdateKolejnosc();
            LoadData();
            lstLadunki.SelectedIndex = index;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            UpdateKolejnosc();
        }
    }

    /// <summary>
    /// Extension method dla klonowania Ladunek
    /// </summary>
    public static class LadunekExtensions
    {
        public static Ladunek Clone(this Ladunek source)
        {
            return new Ladunek
            {
                LadunekID = source.LadunekID,
                KursID = source.KursID,
                Kolejnosc = source.Kolejnosc,
                KodKlienta = source.KodKlienta,
                PojemnikiE2 = source.PojemnikiE2,
                PaletyH1 = source.PaletyH1,
                PlanE2NaPaleteOverride = source.PlanE2NaPaleteOverride,
                Uwagi = source.Uwagi,
                UtworzonoUTC = source.UtworzonoUTC
            };
        }
    }
}