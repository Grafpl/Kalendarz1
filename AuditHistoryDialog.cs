using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    // Prosty dialog modalny pokazujący historię zmian flag dla danego LP.
    // Otwierany z context menu w SprawdzalkaUmow → "Pokaż historię zmian".
    public class AuditHistoryDialog : Form
    {
        private readonly DataGridView _grid;

        public AuditHistoryDialog(int lp, string dostawca, DataTable history)
        {
            Text = $"Historia zmian — LP {lp} ({dostawca})";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 480);
            BackColor = Color.White;
            MinimizeBox = false;
            MaximizeBox = true;

            var lblHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 50,
                Text = $"📋  Historia zmian dostawy LP {lp}",
                Font = SprawdzalkaUmowStyles.ButtonFont,
                ForeColor = SprawdzalkaUmowStyles.TextDark,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 10, 0, 0),
                BackColor = Color.FromArgb(245, 247, 249)
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate = { Height = 32 }
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = SprawdzalkaUmowStyles.Primary;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font = SprawdzalkaUmowStyles.ToolbarBoldFont;
            _grid.ColumnHeadersHeight = 36;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = SprawdzalkaUmowStyles.RowAlternate;

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ChangedAt",
                HeaderText = "Kiedy",
                Width = 150,
                DefaultCellStyle = { Format = "yyyy-MM-dd HH:mm:ss" }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ColumnName",
                HeaderText = "Co zmieniono",
                Width = 140
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "OldValue",
                HeaderText = "Poprzednia",
                Width = 100,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "NewValue",
                HeaderText = "Nowa",
                Width = 100,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "UserName",
                HeaderText = "Kto",
                Width = 200
            });

            _grid.CellFormatting += FormatBoolCells;

            var btnClose = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Text = "Zamknij",
                Font = SprawdzalkaUmowStyles.ToolbarBoldFont,
                BackColor = SprawdzalkaUmowStyles.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();

            Controls.Add(_grid);
            Controls.Add(btnClose);
            Controls.Add(lblHeader);

            if (history == null || history.Rows.Count == 0)
            {
                _grid.Visible = false;
                var lblEmpty = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "📭\n\nBrak wpisów w historii.\n\nMożliwe że tabela audit jeszcze nie została utworzona\nlub żadne zmiany nie zostały zapisane dla tej dostawy.",
                    Font = new Font("Segoe UI", 11F),
                    ForeColor = SprawdzalkaUmowStyles.TextSubtle,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                Controls.Add(lblEmpty);
                lblEmpty.BringToFront();
            }
            else
            {
                _grid.DataSource = history;
            }
        }

        private void FormatBoolCells(object sender, DataGridViewCellFormattingEventArgs e)
        {
            string col = _grid.Columns[e.ColumnIndex].DataPropertyName;
            if (col != "OldValue" && col != "NewValue") return;

            if (e.Value == null || e.Value == DBNull.Value)
            {
                e.Value = "—";
                e.FormattingApplied = true;
                return;
            }
            bool b = Convert.ToBoolean(e.Value);
            e.Value = b ? "✓ TAK" : "✗ NIE";
            e.CellStyle.ForeColor = b ? SprawdzalkaUmowStyles.RowComplete : Color.FromArgb(231, 76, 60);
            e.CellStyle.Font = SprawdzalkaUmowStyles.CellBoldFont;
            e.FormattingApplied = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _grid?.Dispose();
            base.Dispose(disposing);
        }
    }
}
