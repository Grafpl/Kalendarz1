using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.Forms
{
    public class KartotekaPotwierdzienForm : Form
    {
        readonly int _kontrahentId;
        readonly string _nazwa;
        readonly OpakowaniaDataService _ds = new();
        DataGridView _grid;
        Label _lblStatus;
        List<PotwierdzenieSalda> _data = new();

        public KartotekaPotwierdzienForm(int kontrahentId, string nazwa)
        {
            _kontrahentId = kontrahentId;
            _nazwa = nazwa;
            Build();
            Load += async (_, __) => await Zaladuj();
        }

        void Build()
        {
            Text = $"Kartoteka potwierdzeń — {_nazwa}";
            Size = new Size(950, 550);
            MinimumSize = new Size(700, 350);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Color.White;
            KeyPreview = true;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // Status
            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(12, 0, 0, 0)
            };

            // Grid
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
                AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoGenerateColumns = false, RowHeadersVisible = false,
                BackgroundColor = Color.White, GridColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(241, 245, 249), ForeColor = Color.FromArgb(71, 85, 105),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    SelectionBackColor = Color.FromArgb(241, 245, 249), Padding = new Padding(6, 0, 6, 0)
                },
                ColumnHeadersHeight = 36, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 9.5f), Padding = new Padding(6, 2, 6, 2),
                    SelectionBackColor = Color.FromArgb(239, 246, 255), SelectionForeColor = Color.Black
                },
                RowTemplate = { Height = 34 }, EnableHeadersVisualStyles = false
            };
            typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(_grid, true);

            _grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "Data", HeaderText = "DATA", DataPropertyName = "DataPotwierdzeniaText", Width = 90, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "Kod", HeaderText = "TYP", DataPropertyName = "KodOpakowania", Width = 65, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "System", HeaderText = "W SYSTEMIE", DataPropertyName = "SaldoSystemowe", Width = 90, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "Potw", HeaderText = "POTWIERDZONE", DataPropertyName = "IloscPotwierdzona", Width = 110, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "Roznica", HeaderText = "RÓŻNICA", DataPropertyName = "Roznica", Width = 80, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", DataPropertyName = "StatusPotwierdzenia", Width = 100, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewButtonColumn  { Name = "Skan", HeaderText = "SKAN", Text = "Otwórz", UseColumnTextForButtonValue = true, Width = 70 },
                new DataGridViewTextBoxColumn { Name = "Kto", HeaderText = "KTO", DataPropertyName = "UzytkownikNazwa", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 80, SortMode = DataGridViewColumnSortMode.Automatic },
                new DataGridViewTextBoxColumn { Name = "Uwagi", HeaderText = "UWAGI", DataPropertyName = "Uwagi", Width = 120, SortMode = DataGridViewColumnSortMode.Automatic },
            });

            _grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                var col = _grid.Columns[e.ColumnIndex].Name;
                if (col == "Roznica" && e.Value is int r)
                {
                    e.CellStyle.ForeColor = r == 0 ? Color.FromArgb(156, 163, 175) : Color.FromArgb(220, 38, 38);
                    e.Value = r == 0 ? "0" : (r > 0 ? $"+{r}" : r.ToString());
                    e.FormattingApplied = true;
                }
                if (col == "Status" && e.Value is string s)
                {
                    e.CellStyle.ForeColor = s == "Potwierdzone" ? Color.FromArgb(22, 163, 74) :
                                             s == "Rozbieżność" ? Color.FromArgb(220, 38, 38) :
                                             Color.FromArgb(245, 158, 11);
                }
            };

            _grid.CellContentClick += (_, e) =>
            {
                if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Skan") return;
                if (_grid.Rows[e.RowIndex].DataBoundItem is PotwierdzenieSalda p && !string.IsNullOrEmpty(p.SciezkaZalacznika))
                {
                    try
                    {
                        if (File.Exists(p.SciezkaZalacznika))
                            Process.Start(new ProcessStartInfo(p.SciezkaZalacznika) { UseShellExecute = true });
                        else
                            MessageBox.Show($"Plik nie istnieje:\n{p.SciezkaZalacznika}", "Brak pliku", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
                else
                {
                    MessageBox.Show("Brak załączonego skanu.", "Informacja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            Controls.Add(_grid);
            Controls.Add(_lblStatus);
        }

        async Task Zaladuj()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _data = await _ds.PobierzPotwierdzeniaDlaKontrahentaAsync(_kontrahentId);
                _grid.DataSource = new System.ComponentModel.BindingList<PotwierdzenieSalda>(_data);

                int ok = _data.Count(p => p.StatusPotwierdzenia == "Potwierdzone");
                int roz = _data.Count(p => p.StatusPotwierdzenia == "Rozbieżność");
                int skany = _data.Count(p => !string.IsNullOrEmpty(p.SciezkaZalacznika));
                var ostatnie = _data.FirstOrDefault();
                string ost = ostatnie != null ? $"Ostatnie: {ostatnie.DataPotwierdzenia:dd.MM.yyyy}" : "Brak potwierdzeń";

                _lblStatus.Text = $"  {_data.Count} potwierdzeń  |  {ok} zgodnych  |  {roz} rozbieżności  |  {skany} ze skanem  |  {ost}";
            }
            catch (Exception ex) { _lblStatus.Text = "  Błąd: " + ex.Message; }
            finally { Cursor = Cursors.Default; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
