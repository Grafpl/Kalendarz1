using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    // Dialog pokazujący wszystkie historyczne dostawy z FarmerCalc, w których
    // konkretna wartość (Ubytek/Cena/TypCeny/Dodatek/PiK) pasuje do sugerowanej.
    // Otwierany kliknięciem na kartę "Sugerowane" w UmowyForm.
    public class HistoriaWartosciDialog : Form
    {
        public HistoriaWartosciDialog(string poleOpis, string dostawca, string podswietl, DataTable dane)
        {
            Text = "Historia: " + poleOpis;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 540);
            BackColor = Color.White;
            MinimizeBox = false;
            MaximizeBox = true;
            FormBorderStyle = FormBorderStyle.Sizable;
            try { WindowIconHelper.SetIcon(this); } catch { }

            // === HEADER ===
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.FromArgb(38, 50, 56)
            };
            var lblTitle = new Label
            {
                Text = "📜  HISTORIA · " + poleOpis.ToUpper(),
                Location = new Point(20, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
            var lblSub = new Label
            {
                Text = dostawca + "  ·  " + dane.Rows.Count + " przypadków",
                Location = new Point(20, 40),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(207, 216, 220),
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);

            // === GRID ===
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 36,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 30 },
                GridColor = Color.FromArgb(238, 238, 238)
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(55, 71, 79);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(197, 225, 165);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(33, 33, 33);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Data", DataPropertyName = "Data", HeaderText = "Data",
                Width = 110, DefaultCellStyle = { Format = "yyyy-MM-dd  (ddd)" }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "TypCeny", DataPropertyName = "TypCeny", HeaderText = "Typ Ceny", Width = 110
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Cena", DataPropertyName = "Cena", HeaderText = "Cena", Width = 75,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00",
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold) }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Dodatek", DataPropertyName = "Dodatek", HeaderText = "Dodatek", Width = 75,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.00" }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Ubytek", DataPropertyName = "Ubytek", HeaderText = "Ubytek %", Width = 75,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "0.0" }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PiK", DataPropertyName = "PiK", HeaderText = "PiK", Width = 60,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "WagaHod", DataPropertyName = "WagaHod", HeaderText = "Hod (kg)", Width = 90,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "WagaUbojnia", DataPropertyName = "WagaUbojnia", HeaderText = "Uboj (kg)", Width = 90,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });

            // PiK bool → TAK/NIE
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (grid.Columns[e.ColumnIndex].Name != "PiK") return;
                if (e.Value == null || e.Value == DBNull.Value) { e.Value = "—"; e.FormattingApplied = true; return; }
                bool b = Convert.ToBoolean(e.Value);
                e.Value = b ? "TAK" : "NIE";
                e.CellStyle.ForeColor = b ? Color.FromArgb(46, 125, 50) : Color.FromArgb(127, 140, 141);
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                e.FormattingApplied = true;
            };

            // Podświetl wiersze gdzie wartość pasuje do "podswietl" (np. Ubytek 0,5%)
            // - dla każdej kolumny której wartość ToString() zawiera podswietl, daj kolor tła
            if (!string.IsNullOrEmpty(podswietl))
            {
                grid.RowPrePaint += (s, e) =>
                {
                    if (e.RowIndex < 0 || e.RowIndex >= grid.RowCount) return;
                    grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 220);
                };
            }

            grid.DataSource = dane;

            // === FOOTER ===
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(245, 247, 250)
            };
            var btnClose = new Button
            {
                Text = "Zamknij",
                Location = new Point(0, 0),
                Dock = DockStyle.Right,
                Size = new Size(140, 50),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 142, 60);
            btnClose.Click += (s, e) => Close();
            pnlFooter.Controls.Add(btnClose);

            Controls.Add(grid);
            Controls.Add(pnlFooter);
            Controls.Add(pnlHeader);

            if (dane.Rows.Count == 0)
            {
                grid.Visible = false;
                var lblEmpty = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "📭\n\nBrak historycznych przypadków pasujących do tej wartości.",
                    Font = new Font("Segoe UI", 11F),
                    ForeColor = Color.FromArgb(127, 140, 141),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Controls.Add(lblEmpty);
                lblEmpty.BringToFront();
            }
        }
    }
}
