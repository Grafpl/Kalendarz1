// Plik: ScalowanieTowarowDialog.cs
// Dialog do konfiguracji scalania towarów w podsumowaniu dnia
#nullable enable
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    /// <summary>
    /// Model grupy scalania towarów
    /// </summary>
    public class GrupaScalowania
    {
        public int Id { get; set; }
        public string NazwaGrupy { get; set; } = "";
        public List<int> TowaryIdtw { get; set; } = new();

        public override string ToString() => NazwaGrupy;
    }

    /// <summary>
    /// Manager do obsługi tabeli ScalowanieTowarow w bazie danych
    /// </summary>
    public static class ScalowanieTowarowManager
    {
        public static async Task UtworzTabeleJesliNieIstniejeAsync(string connectionString)
        {
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();

            var checkSql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ScalowanieTowarow'";
            await using var checkCmd = new SqlCommand(checkSql, cn);
            if ((int)await checkCmd.ExecuteScalarAsync()! > 0) return;

            var createSql = @"
                CREATE TABLE [dbo].[ScalowanieTowarow] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [NazwaGrupy] NVARCHAR(100) NOT NULL,
                    [TowarIdtw] INT NOT NULL,
                    [DataUtworzenia] DATETIME DEFAULT GETDATE()
                );
                CREATE INDEX [IX_ScalTow_NazwaGrupy] ON [dbo].[ScalowanieTowarow] ([NazwaGrupy]);
                CREATE UNIQUE INDEX [IX_ScalTow_Towar] ON [dbo].[ScalowanieTowarow] ([TowarIdtw]);";
            await using var createCmd = new SqlCommand(createSql, cn);
            await createCmd.ExecuteNonQueryAsync();
        }

        public static async Task<List<GrupaScalowania>> PobierzWszystkieGrupyAsync(string connectionString)
        {
            var grupy = new Dictionary<string, GrupaScalowania>();

            try
            {
                await UtworzTabeleJesliNieIstniejeAsync(connectionString);

                await using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();

                const string sql = "SELECT Id, NazwaGrupy, TowarIdtw FROM [dbo].[ScalowanieTowarow] ORDER BY NazwaGrupy";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var nazwaGrupy = reader.GetString(1);
                    var towarIdtw = reader.GetInt32(2);

                    if (!grupy.ContainsKey(nazwaGrupy))
                    {
                        grupy[nazwaGrupy] = new GrupaScalowania
                        {
                            NazwaGrupy = nazwaGrupy,
                            TowaryIdtw = new List<int>()
                        };
                    }
                    grupy[nazwaGrupy].TowaryIdtw.Add(towarIdtw);
                }
            }
            catch { }

            return grupy.Values.ToList();
        }

        public static async Task<Dictionary<int, string>> PobierzMapowanieTowarowAsync(string connectionString)
        {
            // Zwraca słownik: TowarIdtw -> NazwaGrupy (do której należy)
            var mapowanie = new Dictionary<int, string>();

            try
            {
                await UtworzTabeleJesliNieIstniejeAsync(connectionString);

                await using var cn = new SqlConnection(connectionString);
                await cn.OpenAsync();

                const string sql = "SELECT TowarIdtw, NazwaGrupy FROM [dbo].[ScalowanieTowarow]";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    mapowanie[reader.GetInt32(0)] = reader.GetString(1);
                }
            }
            catch { }

            return mapowanie;
        }

        public static async Task ZapiszGrupeAsync(string connectionString, string nazwaGrupy, List<int> towaryIdtw)
        {
            await UtworzTabeleJesliNieIstniejeAsync(connectionString);

            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();
            await using var transaction = cn.BeginTransaction();

            try
            {
                // Usuń stare mapowania dla tej grupy
                await using var deleteCmd = new SqlCommand(
                    "DELETE FROM [dbo].[ScalowanieTowarow] WHERE NazwaGrupy = @Nazwa", cn, transaction);
                deleteCmd.Parameters.AddWithValue("@Nazwa", nazwaGrupy);
                await deleteCmd.ExecuteNonQueryAsync();

                // Dodaj nowe mapowania
                foreach (var idtw in towaryIdtw)
                {
                    // Najpierw usuń towar z innych grup (towar może być tylko w jednej grupie)
                    await using var deleteOtherCmd = new SqlCommand(
                        "DELETE FROM [dbo].[ScalowanieTowarow] WHERE TowarIdtw = @Idtw", cn, transaction);
                    deleteOtherCmd.Parameters.AddWithValue("@Idtw", idtw);
                    await deleteOtherCmd.ExecuteNonQueryAsync();

                    // Dodaj do tej grupy
                    await using var insertCmd = new SqlCommand(
                        "INSERT INTO [dbo].[ScalowanieTowarow] (NazwaGrupy, TowarIdtw) VALUES (@Nazwa, @Idtw)",
                        cn, transaction);
                    insertCmd.Parameters.AddWithValue("@Nazwa", nazwaGrupy);
                    insertCmd.Parameters.AddWithValue("@Idtw", idtw);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public static async Task UsunGrupeAsync(string connectionString, string nazwaGrupy)
        {
            await using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();

            await using var cmd = new SqlCommand(
                "DELETE FROM [dbo].[ScalowanieTowarow] WHERE NazwaGrupy = @Nazwa", cn);
            cmd.Parameters.AddWithValue("@Nazwa", nazwaGrupy);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Dialog do konfiguracji scalania towarów
    /// </summary>
    public class ScalowanieTowarowDialog : Form
    {
        private readonly string _connectionString;
        private readonly Dictionary<int, string> _towaryKatalog; // idtw -> kod towaru

        private ListBox lstGrupy = null!;
        private ListBox lstTowaryWGrupie = null!;
        private ListBox lstDostepneTowary = null!;
        private TextBox txtNazwaGrupy = null!;
        private Button btnNowaGrupa = null!;
        private Button btnUsunGrupe = null!;
        private Button btnDodajTowar = null!;
        private Button btnUsunTowar = null!;
        private Button btnZapisz = null!;
        private Button btnZamknij = null!;

        private List<GrupaScalowania> _grupy = new();
        private GrupaScalowania? _aktualnaGrupa;
        private bool _zmianyDoZapisu = false;

        public ScalowanieTowarowDialog(string connectionString, Dictionary<int, string> towaryKatalog)
        {
            _connectionString = connectionString;
            _towaryKatalog = towaryKatalog;

            InitializeComponent();
            _ = ZaladujDaneAsync();
        }

        private void InitializeComponent()
        {
            this.Text = "Konfiguracja scalania towarów";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Panel główny z TableLayoutPanel
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37.5F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37.5F));

            // Panel lewej kolumny - lista grup
            var panelGrupy = new Panel { Dock = DockStyle.Fill };
            var lblGrupy = new Label
            {
                Text = "Grupy scalania:",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Height = 25
            };

            lstGrupy = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F)
            };
            lstGrupy.SelectedIndexChanged += LstGrupy_SelectedIndexChanged;

            var panelGrupyButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight
            };

            btnNowaGrupa = new Button
            {
                Text = "+ Nowa",
                Size = new Size(75, 30),
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnNowaGrupa.Click += BtnNowaGrupa_Click;

            btnUsunGrupe = new Button
            {
                Text = "Usuń",
                Size = new Size(75, 30),
                BackColor = Color.IndianRed,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnUsunGrupe.Click += BtnUsunGrupe_Click;

            panelGrupyButtons.Controls.AddRange(new Control[] { btnNowaGrupa, btnUsunGrupe });

            panelGrupy.Controls.Add(lstGrupy);
            panelGrupy.Controls.Add(panelGrupyButtons);
            panelGrupy.Controls.Add(lblGrupy);

            // Panel środkowej kolumny - towary w grupie
            var panelTowaryWGrupie = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 0, 5, 0) };

            var panelNazwaGrupy = new Panel { Dock = DockStyle.Top, Height = 55 };
            var lblNazwa = new Label
            {
                Text = "Nazwa grupy:",
                Location = new Point(0, 0),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9F)
            };
            txtNazwaGrupy = new TextBox
            {
                Location = new Point(0, 22),
                Size = new Size(250, 25),
                Font = new Font("Segoe UI", 10F)
            };
            txtNazwaGrupy.TextChanged += (s, e) => _zmianyDoZapisu = true;
            panelNazwaGrupy.Controls.AddRange(new Control[] { lblNazwa, txtNazwaGrupy });

            var lblTowaryWGrupie = new Label
            {
                Text = "Towary w tej grupie:",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Height = 25
            };

            lstTowaryWGrupie = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                SelectionMode = SelectionMode.MultiExtended
            };

            var panelSrodkoweButtons = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnUsunTowar = new Button
            {
                Text = "< Usuń z grupy",
                Size = new Size(120, 30),
                Location = new Point(0, 5)
            };
            btnUsunTowar.Click += BtnUsunTowar_Click;
            panelSrodkoweButtons.Controls.Add(btnUsunTowar);

            panelTowaryWGrupie.Controls.Add(lstTowaryWGrupie);
            panelTowaryWGrupie.Controls.Add(panelSrodkoweButtons);
            panelTowaryWGrupie.Controls.Add(lblTowaryWGrupie);
            panelTowaryWGrupie.Controls.Add(panelNazwaGrupy);

            // Panel prawej kolumny - dostępne towary
            var panelDostepne = new Panel { Dock = DockStyle.Fill };
            var lblDostepne = new Label
            {
                Text = "Dostępne towary:",
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Height = 25
            };

            lstDostepneTowary = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                SelectionMode = SelectionMode.MultiExtended
            };

            var panelDostepneButtons = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            btnDodajTowar = new Button
            {
                Text = "Dodaj do grupy >",
                Size = new Size(120, 30),
                Location = new Point(0, 5),
                BackColor = Color.RoyalBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnDodajTowar.Click += BtnDodajTowar_Click;
            panelDostepneButtons.Controls.Add(btnDodajTowar);

            panelDostepne.Controls.Add(lstDostepneTowary);
            panelDostepne.Controls.Add(panelDostepneButtons);
            panelDostepne.Controls.Add(lblDostepne);

            mainLayout.Controls.Add(panelGrupy, 0, 0);
            mainLayout.Controls.Add(panelTowaryWGrupie, 1, 0);
            mainLayout.Controls.Add(panelDostepne, 2, 0);

            // Panel dolny z przyciskami
            var panelDolny = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10, 5, 10, 5)
            };

            btnZapisz = new Button
            {
                Text = "Zapisz zmiany",
                Size = new Size(130, 35),
                Location = new Point(10, 7),
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnZapisz.Click += BtnZapisz_Click;

            btnZamknij = new Button
            {
                Text = "Zamknij",
                Size = new Size(100, 35),
                Anchor = AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 120, 7),
                Font = new Font("Segoe UI", 10F)
            };
            btnZamknij.Click += (s, e) => this.Close();

            panelDolny.Controls.AddRange(new Control[] { btnZapisz, btnZamknij });

            // Info label
            var lblInfo = new Label
            {
                Text = "Towary przypisane do tej samej grupy będą sumowane razem w podsumowaniu dnia. " +
                       "Nazwa grupy będzie wyświetlana zamiast nazw poszczególnych towarów.",
                Dock = DockStyle.Bottom,
                Height = 35,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.DimGray,
                Padding = new Padding(10, 0, 10, 0)
            };

            this.Controls.Add(mainLayout);
            this.Controls.Add(lblInfo);
            this.Controls.Add(panelDolny);
        }

        private async Task ZaladujDaneAsync()
        {
            try
            {
                _grupy = await ScalowanieTowarowManager.PobierzWszystkieGrupyAsync(_connectionString);
                OdswiezListeGrup();
                OdswiezListeDostepnychTowarow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OdswiezListeGrup()
        {
            lstGrupy.Items.Clear();
            foreach (var grupa in _grupy.OrderBy(g => g.NazwaGrupy))
            {
                lstGrupy.Items.Add(grupa);
            }
        }

        private void OdswiezListeDostepnychTowarow()
        {
            lstDostepneTowary.Items.Clear();

            // Zbierz wszystkie towary już przypisane do grup
            var przypisane = new HashSet<int>();
            foreach (var grupa in _grupy)
            {
                foreach (var idtw in grupa.TowaryIdtw)
                    przypisane.Add(idtw);
            }

            // Dodaj towary które nie są przypisane
            foreach (var towar in _towaryKatalog.OrderBy(t => t.Value))
            {
                if (!przypisane.Contains(towar.Key))
                {
                    lstDostepneTowary.Items.Add(new TowarItem(towar.Key, towar.Value));
                }
            }
        }

        private void OdswiezListeTowarowWGrupie()
        {
            lstTowaryWGrupie.Items.Clear();

            if (_aktualnaGrupa == null) return;

            foreach (var idtw in _aktualnaGrupa.TowaryIdtw)
            {
                if (_towaryKatalog.TryGetValue(idtw, out var kod))
                {
                    lstTowaryWGrupie.Items.Add(new TowarItem(idtw, kod));
                }
            }
        }

        private void LstGrupy_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (lstGrupy.SelectedItem is GrupaScalowania grupa)
            {
                _aktualnaGrupa = grupa;
                txtNazwaGrupy.Text = grupa.NazwaGrupy;
                OdswiezListeTowarowWGrupie();
            }
            else
            {
                _aktualnaGrupa = null;
                txtNazwaGrupy.Text = "";
                lstTowaryWGrupie.Items.Clear();
            }
        }

        private void BtnNowaGrupa_Click(object? sender, EventArgs e)
        {
            var nazwa = Microsoft.VisualBasic.Interaction.InputBox(
                "Podaj nazwę nowej grupy scalania:",
                "Nowa grupa",
                "");

            if (string.IsNullOrWhiteSpace(nazwa)) return;

            if (_grupy.Any(g => g.NazwaGrupy.Equals(nazwa, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("Grupa o takiej nazwie już istnieje!", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var nowaGrupa = new GrupaScalowania { NazwaGrupy = nazwa };
            _grupy.Add(nowaGrupa);
            OdswiezListeGrup();

            // Zaznacz nową grupę
            for (int i = 0; i < lstGrupy.Items.Count; i++)
            {
                if (lstGrupy.Items[i] is GrupaScalowania g && g.NazwaGrupy == nazwa)
                {
                    lstGrupy.SelectedIndex = i;
                    break;
                }
            }

            _zmianyDoZapisu = true;
        }

        private async void BtnUsunGrupe_Click(object? sender, EventArgs e)
        {
            if (_aktualnaGrupa == null) return;

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć grupę '{_aktualnaGrupa.NazwaGrupy}'?",
                "Potwierdzenie",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            try
            {
                await ScalowanieTowarowManager.UsunGrupeAsync(_connectionString, _aktualnaGrupa.NazwaGrupy);
                _grupy.Remove(_aktualnaGrupa);
                _aktualnaGrupa = null;
                OdswiezListeGrup();
                OdswiezListeDostepnychTowarow();
                txtNazwaGrupy.Text = "";
                lstTowaryWGrupie.Items.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania grupy: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDodajTowar_Click(object? sender, EventArgs e)
        {
            if (_aktualnaGrupa == null)
            {
                MessageBox.Show("Najpierw wybierz lub utwórz grupę!", "Informacja",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var zaznaczone = lstDostepneTowary.SelectedItems.Cast<TowarItem>().ToList();
            if (!zaznaczone.Any()) return;

            foreach (var towar in zaznaczone)
            {
                _aktualnaGrupa.TowaryIdtw.Add(towar.Idtw);
            }

            OdswiezListeTowarowWGrupie();
            OdswiezListeDostepnychTowarow();
            _zmianyDoZapisu = true;
        }

        private void BtnUsunTowar_Click(object? sender, EventArgs e)
        {
            if (_aktualnaGrupa == null) return;

            var zaznaczone = lstTowaryWGrupie.SelectedItems.Cast<TowarItem>().ToList();
            if (!zaznaczone.Any()) return;

            foreach (var towar in zaznaczone)
            {
                _aktualnaGrupa.TowaryIdtw.Remove(towar.Idtw);
            }

            OdswiezListeTowarowWGrupie();
            OdswiezListeDostepnychTowarow();
            _zmianyDoZapisu = true;
        }

        private async void BtnZapisz_Click(object? sender, EventArgs e)
        {
            try
            {
                // Aktualizuj nazwę grupy jeśli zmieniona
                if (_aktualnaGrupa != null && !string.IsNullOrWhiteSpace(txtNazwaGrupy.Text))
                {
                    var nowaNazwa = txtNazwaGrupy.Text.Trim();
                    if (nowaNazwa != _aktualnaGrupa.NazwaGrupy)
                    {
                        // Usuń starą grupę z bazy
                        await ScalowanieTowarowManager.UsunGrupeAsync(_connectionString, _aktualnaGrupa.NazwaGrupy);
                        _aktualnaGrupa.NazwaGrupy = nowaNazwa;
                    }
                }

                // Zapisz wszystkie grupy
                foreach (var grupa in _grupy)
                {
                    if (grupa.TowaryIdtw.Any())
                    {
                        await ScalowanieTowarowManager.ZapiszGrupeAsync(
                            _connectionString,
                            grupa.NazwaGrupy,
                            grupa.TowaryIdtw);
                    }
                }

                _zmianyDoZapisu = false;
                MessageBox.Show("Konfiguracja scalania została zapisana.", "Sukces",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                OdswiezListeGrup();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu: {ex.Message}", "Błąd",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_zmianyDoZapisu)
            {
                var result = MessageBox.Show(
                    "Masz niezapisane zmiany. Czy chcesz je zapisać przed zamknięciem?",
                    "Niezapisane zmiany",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    BtnZapisz_Click(null, EventArgs.Empty);
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Pomocnicza klasa do wyświetlania towarów w ListBox
        /// </summary>
        private class TowarItem
        {
            public int Idtw { get; }
            public string Kod { get; }

            public TowarItem(int idtw, string kod)
            {
                Idtw = idtw;
                Kod = kod;
            }

            public override string ToString() => Kod;
        }
    }
}
