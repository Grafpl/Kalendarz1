// Plik: /Transport/Widoki/TransportKierowcyForm.cs
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kalendarz1.Transport.Repozytorium;

namespace Kalendarz1.Transport.Widoki
{
    // ===== FORMULARZ KIEROWCÓW =====
    public partial class TransportKierowcyForm : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private DataGridView dgvKierowcy;
        private Button btnDodaj, btnEdytuj, btnUsun, btnZamknij;
        private CheckBox chkPokazNieaktywnych;
        
        public TransportKierowcyForm(TransportRepozytorium repozytorium)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            InitializeComponent();
            this.Load += async (s, e) => await WczytajKierowcow();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Zarządzanie kierowcami";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            
            // Checkbox
            chkPokazNieaktywnych = new CheckBox
            {
                Text = "Pokaż nieaktywnych",
                Dock = DockStyle.Fill
            };
            chkPokazNieaktywnych.CheckedChanged += async (s, e) => await WczytajKierowcow();
            tlp.Controls.Add(chkPokazNieaktywnych, 0, 0);
            
            // Grid
            dgvKierowcy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            tlp.Controls.Add(dgvKierowcy, 0, 1);
            
            // Przyciski
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };
            
            btnDodaj = new Button { Text = "Dodaj", Width = 100, Height = 35 };
            btnDodaj.Click += async (s, e) => await DodajKierowce();
            
            btnEdytuj = new Button { Text = "Edytuj", Width = 100, Height = 35 };
            btnEdytuj.Click += async (s, e) => await EdytujKierowce();
            
            btnUsun = new Button { Text = "Dezaktywuj", Width = 100, Height = 35 };
            btnUsun.Click += async (s, e) => await DezaktywujKierowce();
            
            btnZamknij = new Button { Text = "Zamknij", Width = 100, Height = 35 };
            btnZamknij.Click += (s, e) => Close();
            
            pnlButtons.Controls.AddRange(new Control[] { btnDodaj, btnEdytuj, btnUsun, btnZamknij });
            tlp.Controls.Add(pnlButtons, 0, 2);
            
            this.Controls.Add(tlp);
        }
        
        private async Task WczytajKierowcow()
        {
            var kierowcy = await _repozytorium.PobierzKierowcowAsync(!chkPokazNieaktywnych.Checked);
            dgvKierowcy.DataSource = kierowcy;
            
            if (dgvKierowcy.Columns["KierowcaID"] != null) 
                dgvKierowcy.Columns["KierowcaID"].Visible = false;
            if (dgvKierowcy.Columns["UtworzonoUTC"] != null) 
                dgvKierowcy.Columns["UtworzonoUTC"].Visible = false;
            if (dgvKierowcy.Columns["ZmienionoUTC"] != null) 
                dgvKierowcy.Columns["ZmienionoUTC"].Visible = false;
            if (dgvKierowcy.Columns["PelneNazwisko"] != null) 
                dgvKierowcy.Columns["PelneNazwisko"].Visible = false;
        }
        
        private async Task DodajKierowce()
        {
            using (var dlg = new KierowcaEdycjaForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var kierowca = new Kierowca
                    {
                        Imie = dlg.Imie,
                        Nazwisko = dlg.Nazwisko,
                        Telefon = dlg.Telefon,
                        Aktywny = true
                    };
                    //await _repozytorium.DodajKierowceAsync(kierowca);
                    await WczytajKierowcow();
                }
            }
        }
        
        private async Task EdytujKierowce()
        {
            if (dgvKierowcy.CurrentRow?.DataBoundItem is Kierowca kierowca)
            {
                using (var dlg = new KierowcaEdycjaForm(kierowca))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        kierowca.Imie = dlg.Imie;
                        kierowca.Nazwisko = dlg.Nazwisko;
                        kierowca.Telefon = dlg.Telefon;
                        //await _repozytorium.AktualizujKierowceAsync(kierowca);
                        await WczytajKierowcow();
                    }
                }
            }
        }
        
        private async Task DezaktywujKierowce()
        {
            if (dgvKierowcy.CurrentRow?.DataBoundItem is Kierowca kierowca)
            {
                string akcja = kierowca.Aktywny ? "dezaktywować" : "aktywować";
                var result = MessageBox.Show($"Czy na pewno chcesz {akcja} kierowcę {kierowca.Imie} {kierowca.Nazwisko}?",
                    "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                if (result == DialogResult.Yes)
                {
                    await _repozytorium.UstawAktywnyKierowcaAsync(kierowca.KierowcaID, !kierowca.Aktywny);
                    await WczytajKierowcow();
                }
            }
        }
    }
    
    // Dialog edycji kierowcy
    public class KierowcaEdycjaForm : Form
    {
        private TextBox txtImie, txtNazwisko, txtTelefon;
        
        public string Imie => txtImie.Text;
        public string Nazwisko => txtNazwisko.Text;
        public string Telefon => txtTelefon.Text;
        
        public KierowcaEdycjaForm(Kierowca kierowca = null)
        {
            InitializeComponent();
            if (kierowca != null)
            {
                this.Text = "Edycja kierowcy";
                txtImie.Text = kierowca.Imie;
                txtNazwisko.Text = kierowca.Nazwisko;
                txtTelefon.Text = kierowca.Telefon ?? "";
            }
            else
            {
                this.Text = "Nowy kierowca";
            }
        }
        
        private void InitializeComponent()
        {
            this.Size = new Size(350, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            
            var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(10) };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            
            tlp.Controls.Add(new Label { Text = "Imię:", Dock = DockStyle.Fill }, 0, 0);
            txtImie = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtImie, 1, 0);
            
            tlp.Controls.Add(new Label { Text = "Nazwisko:", Dock = DockStyle.Fill }, 0, 1);
            txtNazwisko = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtNazwisko, 1, 1);
            
            tlp.Controls.Add(new Label { Text = "Telefon:", Dock = DockStyle.Fill }, 0, 2);
            txtTelefon = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtTelefon, 1, 2);
            
            var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var btnAnuluj = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel };
            var btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            pnlButtons.Controls.AddRange(new Control[] { btnAnuluj, btnOK });
            
            tlp.SetColumnSpan(pnlButtons, 2);
            tlp.Controls.Add(pnlButtons, 0, 3);
            
            this.Controls.Add(tlp);
        }
    }
    
    // ===== FORMULARZ POJAZDÓW =====
    public partial class TransportPojazdyForm : Form
    {
        private readonly TransportRepozytorium _repozytorium;
        private DataGridView dgvPojazdy;
        private Button btnDodaj, btnEdytuj, btnUsun, btnZamknij;
        private CheckBox chkPokazNieaktywne;
        
        public TransportPojazdyForm(TransportRepozytorium repozytorium)
        {
            _repozytorium = repozytorium ?? throw new ArgumentNullException(nameof(repozytorium));
            InitializeComponent();
            this.Load += async (s, e) => await WczytajPojazdy();
        }
        
        private void InitializeComponent()
        {
            this.Text = "Zarządzanie pojazdami";
            this.Size = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            
            // Checkbox
            chkPokazNieaktywne = new CheckBox
            {
                Text = "Pokaż nieaktywne",
                Dock = DockStyle.Fill
            };
            chkPokazNieaktywne.CheckedChanged += async (s, e) => await WczytajPojazdy();
            tlp.Controls.Add(chkPokazNieaktywne, 0, 0);
            
            // Grid
            dgvPojazdy = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };
            tlp.Controls.Add(dgvPojazdy, 0, 1);
            
            // Przyciski
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };
            
            btnDodaj = new Button { Text = "Dodaj", Width = 100, Height = 35 };
            btnDodaj.Click += async (s, e) => await DodajPojazd();
            
            btnEdytuj = new Button { Text = "Edytuj", Width = 100, Height = 35 };
            btnEdytuj.Click += async (s, e) => await EdytujPojazd();
            
            btnUsun = new Button { Text = "Dezaktywuj", Width = 100, Height = 35 };
            btnUsun.Click += async (s, e) => await DezaktywujPojazd();
            
            btnZamknij = new Button { Text = "Zamknij", Width = 100, Height = 35 };
            btnZamknij.Click += (s, e) => Close();
            
            pnlButtons.Controls.AddRange(new Control[] { btnDodaj, btnEdytuj, btnUsun, btnZamknij });
            tlp.Controls.Add(pnlButtons, 0, 2);
            
            this.Controls.Add(tlp);
        }
        
        private async Task WczytajPojazdy()
        {
            var pojazdy = await _repozytorium.PobierzPojazdyAsync(!chkPokazNieaktywne.Checked);
            dgvPojazdy.DataSource = pojazdy;
            
            if (dgvPojazdy.Columns["PojazdID"] != null) 
                dgvPojazdy.Columns["PojazdID"].Visible = false;
            if (dgvPojazdy.Columns["UtworzonoUTC"] != null) 
                dgvPojazdy.Columns["UtworzonoUTC"].Visible = false;
            if (dgvPojazdy.Columns["ZmienionoUTC"] != null) 
                dgvPojazdy.Columns["ZmienionoUTC"].Visible = false;
            if (dgvPojazdy.Columns["Opis"] != null) 
                dgvPojazdy.Columns["Opis"].Visible = false;
        }
        
        private async Task DodajPojazd()
        {
            using (var dlg = new PojazdEdycjaForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var pojazd = new Pojazd
                    {
                        Rejestracja = dlg.Rejestracja,
                        Marka = dlg.Marka,
                        Model = dlg.Model,
                        PaletyH1 = dlg.PaletyH1,
                        Aktywny = true
                    };
                    //await _repozytorium.DodajPojazdAsync(pojazd);
                    await WczytajPojazdy();
                }
            }
        }
        
        private async Task EdytujPojazd()
        {
            if (dgvPojazdy.CurrentRow?.DataBoundItem is Pojazd pojazd)
            {
                using (var dlg = new PojazdEdycjaForm(pojazd))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        pojazd.Rejestracja = dlg.Rejestracja;
                        pojazd.Marka = dlg.Marka;
                        pojazd.Model = dlg.Model;
                        pojazd.PaletyH1 = dlg.PaletyH1;
                        //await _repozytorium.AktualizujPojazdAsync(pojazd);
                        await WczytajPojazdy();
                    }
                }
            }
        }
        
        private async Task DezaktywujPojazd()
        {
            if (dgvPojazdy.CurrentRow?.DataBoundItem is Pojazd pojazd)
            {
                string akcja = pojazd.Aktywny ? "dezaktywować" : "aktywować";
                var result = MessageBox.Show($"Czy na pewno chcesz {akcja} pojazd {pojazd.Rejestracja}?",
                    "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    
                if (result == DialogResult.Yes)
                {
                    await _repozytorium.UstawAktywnyPojazdAsync(pojazd.PojazdID, !pojazd.Aktywny);
                    await WczytajPojazdy();
                }
            }
        }
    }
    
    // Dialog edycji pojazdu
    public class PojazdEdycjaForm : Form
    {
        private TextBox txtRejestracja, txtMarka, txtModel;
        private NumericUpDown nudPalety;
        
        public string Rejestracja => txtRejestracja.Text.Trim().ToUpper();
        public string Marka => txtMarka.Text.Trim();
        public string Model => txtModel.Text.Trim();
        public int PaletyH1 => (int)nudPalety.Value;
        
        public PojazdEdycjaForm(Pojazd pojazd = null)
        {
            InitializeComponent();
            if (pojazd != null)
            {
                this.Text = "Edycja pojazdu";
                txtRejestracja.Text = pojazd.Rejestracja;
                txtMarka.Text = pojazd.Marka ?? "";
                txtModel.Text = pojazd.Model ?? "";
                nudPalety.Value = pojazd.PaletyH1;
            }
            else
            {
                this.Text = "Nowy pojazd";
                nudPalety.Value = 33;
            }
        }
        
        private void InitializeComponent()
        {
            this.Size = new Size(400, 250);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            
            var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(10) };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            
            tlp.Controls.Add(new Label { Text = "Rejestracja:", Dock = DockStyle.Fill }, 0, 0);
            txtRejestracja = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtRejestracja, 1, 0);
            
            tlp.Controls.Add(new Label { Text = "Marka:", Dock = DockStyle.Fill }, 0, 1);
            txtMarka = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtMarka, 1, 1);
            
            tlp.Controls.Add(new Label { Text = "Model:", Dock = DockStyle.Fill }, 0, 2);
            txtModel = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtModel, 1, 2);
            
            tlp.Controls.Add(new Label { Text = "Liczba palet:", Dock = DockStyle.Fill }, 0, 3);
            nudPalety = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 50, Value = 33 };
            tlp.Controls.Add(nudPalety, 1, 3);
            
            var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var btnAnuluj = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel };
            var btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            pnlButtons.Controls.AddRange(new Control[] { btnAnuluj, btnOK });
            
            tlp.SetColumnSpan(pnlButtons, 2);
            tlp.Controls.Add(pnlButtons, 0, 4);
            
            this.Controls.Add(tlp);
        }
    }
    
    // Klasy modeli używane przez formularze
    public class Kierowca
    {
        public int KierowcaID { get; set; }
        public string Imie { get; set; }
        public string Nazwisko { get; set; }
        public string Telefon { get; set; }
        public bool Aktywny { get; set; } = true;
        public DateTime UtworzonoUTC { get; set; }
        public DateTime? ZmienionoUTC { get; set; }
        
        public string PelneNazwisko => $"{Imie} {Nazwisko}";
    }
    
    public class Pojazd
    {
        public int PojazdID { get; set; }
        public string Rejestracja { get; set; }
        public string Marka { get; set; }
        public string Model { get; set; }
        public int PaletyH1 { get; set; } = 33;
        public bool Aktywny { get; set; } = true;
        public DateTime UtworzonoUTC { get; set; }
        public DateTime? ZmienionoUTC { get; set; }
        
        public string Opis => $"{Rejestracja} - {Marka} {Model}";
    }
}