#nullable disable
namespace Kalendarz1
{
    partial class WidokZamowienia
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            rootLayout = new System.Windows.Forms.TableLayoutPanel();
            panelMaster = new System.Windows.Forms.Panel();
            panelAkcji = new System.Windows.Forms.Panel();
            btnZapisz = new System.Windows.Forms.Button();
            btnAnuluj = new System.Windows.Forms.Button();
            panelDetaleZamowienia = new System.Windows.Forms.Panel();
            listaWynikowOdbiorcy = new System.Windows.Forms.ListBox();
            label5 = new System.Windows.Forms.Label();
            textBoxUwagi = new System.Windows.Forms.TextBox();
            dateTimePickerGodzinaPrzyjazdu = new System.Windows.Forms.DateTimePicker();
            label3 = new System.Windows.Forms.Label();
            dateTimePickerSprzedaz = new System.Windows.Forms.DateTimePicker();
            label2 = new System.Windows.Forms.Label();
            panelOstatniOdbiorcy = new System.Windows.Forms.Panel();
            lblOstatniOdbiorcy = new System.Windows.Forms.Label();
            gridOstatniOdbiorcy = new System.Windows.Forms.DataGridView();
            dateTimePickerProdukcji = new System.Windows.Forms.DateTimePicker();
            panelOdbiorca = new System.Windows.Forms.Panel();
            lblTytul = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            cbHandlowiecFilter = new System.Windows.Forms.ComboBox();
            txtSzukajOdbiorcy = new System.Windows.Forms.TextBox();
            label1 = new System.Windows.Forms.Label();
            panelDetails = new System.Windows.Forms.Panel();
            dataGridViewZamowienie = new System.Windows.Forms.DataGridView();
            panelDaneOdbiorcy = new System.Windows.Forms.Panel();
            lblHandlowiec = new System.Windows.Forms.Label();
            lblAdres = new System.Windows.Forms.Label();
            lblNip = new System.Windows.Forms.Label();
            lblWybranyOdbiorca = new System.Windows.Forms.Label();
            panelSumaGrid = new System.Windows.Forms.TableLayoutPanel();
            summaryLabelIlosc = new System.Windows.Forms.Label();
            summaryLabelPojemniki = new System.Windows.Forms.Label();
            summaryLabelPalety = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
            rootLayout.SuspendLayout();
            panelMaster.SuspendLayout();
            panelAkcji.SuspendLayout();
            panelDetaleZamowienia.SuspendLayout();
            panelOstatniOdbiorcy.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridOstatniOdbiorcy).BeginInit();
            panelOdbiorca.SuspendLayout();
            panelDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).BeginInit();
            panelDaneOdbiorcy.SuspendLayout();
            panelSumaGrid.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 2;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 430F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.Controls.Add(panelMaster, 0, 0);
            rootLayout.Controls.Add(panelDetails, 1, 0);
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.Location = new System.Drawing.Point(0, 0);
            rootLayout.Margin = new System.Windows.Forms.Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 1;
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.Size = new System.Drawing.Size(1280, 720);
            rootLayout.TabIndex = 0;
            // 
            // panelMaster
            // 
            panelMaster.BackColor = System.Drawing.Color.White;
            panelMaster.Controls.Add(panelAkcji);
            panelMaster.Controls.Add(panelDetaleZamowienia);
            panelMaster.Controls.Add(panelOdbiorca);
            panelMaster.Dock = System.Windows.Forms.DockStyle.Fill;
            panelMaster.Location = new System.Drawing.Point(0, 0);
            panelMaster.Margin = new System.Windows.Forms.Padding(0);
            panelMaster.Name = "panelMaster";
            panelMaster.Size = new System.Drawing.Size(430, 720);
            panelMaster.TabIndex = 0;
            // 
            // panelAkcji
            // 

            panelAkcji.BackColor = System.Drawing.Color.FromArgb(249, 250, 251);
            panelAkcji.Controls.Add(btnZapisz);
            panelAkcji.Controls.Add(btnAnuluj);
            panelAkcji.Dock = System.Windows.Forms.DockStyle.Bottom;
            panelAkcji.Location = new System.Drawing.Point(0, 650);
            panelAkcji.Name = "panelAkcji";
            panelAkcji.Size = new System.Drawing.Size(430, 70);
            panelAkcji.TabIndex = 3;


            // 
// dateTimePickerProdukcji
// 
dateTimePickerProdukcji.CalendarMonthBackground = System.Drawing.Color.FromArgb(249, 250, 251);
dateTimePickerProdukcji.Font = new System.Drawing.Font("Segoe UI", 10.5F);
dateTimePickerProdukcji.Format = System.Windows.Forms.DateTimePickerFormat.Short;
dateTimePickerProdukcji.Location = new System.Drawing.Point(10, 35);
dateTimePickerProdukcji.Name = "dateTimePickerProdukcji";
dateTimePickerProdukcji.Size = new System.Drawing.Size(203, 26);
dateTimePickerProdukcji.TabIndex = 0;
            // 
            // btnZapisz
            // 
            btnZapisz.BackColor = System.Drawing.Color.FromArgb(99, 102, 241);
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnZapisz.Font = new System.Drawing.Font("Segoe UI Semibold", 10F);
            btnZapisz.ForeColor = System.Drawing.Color.White;
            btnZapisz.Location = new System.Drawing.Point(275, 16);
            btnZapisz.Name = "btnZapisz";
            btnZapisz.Size = new System.Drawing.Size(140, 38);
            btnZapisz.TabIndex = 0;
            btnZapisz.Text = "Zapisz";
            btnZapisz.UseVisualStyleBackColor = false;
            btnZapisz.Click += btnZapisz_Click;
            // 
            // btnAnuluj
            // 
            btnAnuluj.BackColor = System.Drawing.Color.FromArgb(243, 244, 246);
            btnAnuluj.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnAnuluj.FlatAppearance.BorderSize = 0;
            btnAnuluj.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnAnuluj.Font = new System.Drawing.Font("Segoe UI", 10F);
            btnAnuluj.ForeColor = System.Drawing.Color.FromArgb(75, 85, 99);
            btnAnuluj.Location = new System.Drawing.Point(125, 16);
            btnAnuluj.Name = "btnAnuluj";
            btnAnuluj.Size = new System.Drawing.Size(140, 38);
            btnAnuluj.TabIndex = 1;
            btnAnuluj.Text = "Anuluj";
            btnAnuluj.UseVisualStyleBackColor = false;
            // 
            // panelDetaleZamowienia
            // 
            panelDetaleZamowienia.Controls.Add(listaWynikowOdbiorcy);
            panelDetaleZamowienia.Controls.Add(label5);
            panelDetaleZamowienia.Controls.Add(textBoxUwagi);
            panelDetaleZamowienia.Controls.Add(dateTimePickerGodzinaPrzyjazdu);
            panelDetaleZamowienia.Controls.Add(label3);
            panelDetaleZamowienia.Controls.Add(dateTimePickerSprzedaz);
            panelDetaleZamowienia.Controls.Add(label2);
            panelDetaleZamowienia.Controls.Add(panelOstatniOdbiorcy);
            panelDetaleZamowienia.Dock = System.Windows.Forms.DockStyle.Fill;
            panelDetaleZamowienia.Location = new System.Drawing.Point(0, 180);
            panelDetaleZamowienia.Name = "panelDetaleZamowienia";
            panelDetaleZamowienia.Padding = new System.Windows.Forms.Padding(20, 5, 20, 5);
            panelDetaleZamowienia.Size = new System.Drawing.Size(430, 470);
            panelDetaleZamowienia.TabIndex = 2;
            // 
            // listaWynikowOdbiorcy
            // 
            listaWynikowOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            listaWynikowOdbiorcy.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            listaWynikowOdbiorcy.FormattingEnabled = true;
            listaWynikowOdbiorcy.ItemHeight = 17;
            listaWynikowOdbiorcy.Location = new System.Drawing.Point(10, 30);
            listaWynikowOdbiorcy.Name = "listaWynikowOdbiorcy";
            listaWynikowOdbiorcy.Size = new System.Drawing.Size(410, 155);
            listaWynikowOdbiorcy.TabIndex = 5;
            listaWynikowOdbiorcy.Visible = false;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new System.Drawing.Font("Segoe UI", 9F);
            label5.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            label5.Location = new System.Drawing.Point(10, 365);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(48, 15);
            label5.TabIndex = 5;
            label5.Text = "Notatka";
            // 
            // textBoxUwagi
            // 
            textBoxUwagi.AcceptsReturn = true;
            textBoxUwagi.BackColor = System.Drawing.Color.FromArgb(249, 250, 251);
            textBoxUwagi.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            textBoxUwagi.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            textBoxUwagi.Location = new System.Drawing.Point(10, 385);
            textBoxUwagi.Multiline = true;
            textBoxUwagi.Name = "textBoxUwagi";
            textBoxUwagi.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            textBoxUwagi.Size = new System.Drawing.Size(410, 80);
            textBoxUwagi.TabIndex = 4;
            // 
            // dateTimePickerGodzinaPrzyjazdu
            // 
            dateTimePickerGodzinaPrzyjazdu.CalendarMonthBackground = System.Drawing.Color.FromArgb(249, 250, 251);
            dateTimePickerGodzinaPrzyjazdu.CustomFormat = "HH:mm";
            dateTimePickerGodzinaPrzyjazdu.Font = new System.Drawing.Font("Segoe UI", 10.5F);
            dateTimePickerGodzinaPrzyjazdu.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            dateTimePickerGodzinaPrzyjazdu.Location = new System.Drawing.Point(217, 320);
            dateTimePickerGodzinaPrzyjazdu.Name = "dateTimePickerGodzinaPrzyjazdu";
            dateTimePickerGodzinaPrzyjazdu.ShowUpDown = true;
            dateTimePickerGodzinaPrzyjazdu.Size = new System.Drawing.Size(203, 26);
            dateTimePickerGodzinaPrzyjazdu.TabIndex = 3;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold);
            label3.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            label3.Location = new System.Drawing.Point(217, 295);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(126, 17);
            label3.TabIndex = 2;
            label3.Text = "🕐 Godzina odbioru";
            // 
            // dateTimePickerSprzedaz
            // 
            dateTimePickerSprzedaz.CalendarMonthBackground = System.Drawing.Color.FromArgb(249, 250, 251);
            dateTimePickerSprzedaz.Font = new System.Drawing.Font("Segoe UI", 10.5F);
            dateTimePickerSprzedaz.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            dateTimePickerSprzedaz.Location = new System.Drawing.Point(10, 320);
            dateTimePickerSprzedaz.Name = "dateTimePickerSprzedaz";
            dateTimePickerSprzedaz.Size = new System.Drawing.Size(203, 26);
            dateTimePickerSprzedaz.TabIndex = 1;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5F, System.Drawing.FontStyle.Bold);
            label2.ForeColor = System.Drawing.Color.FromArgb(31, 41, 55);
            label2.Location = new System.Drawing.Point(10, 295);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(108, 17);
            label2.TabIndex = 0;
            label2.Text = "📅 Data odbioru";
            // 
            // panelOstatniOdbiorcy
            // 
            panelOstatniOdbiorcy.BackColor = System.Drawing.Color.FromArgb(249, 250, 251);
            panelOstatniOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.None;
            panelOstatniOdbiorcy.Controls.Add(lblOstatniOdbiorcy);
            panelOstatniOdbiorcy.Controls.Add(gridOstatniOdbiorcy);
            panelOstatniOdbiorcy.Location = new System.Drawing.Point(10, 20);
            panelOstatniOdbiorcy.Name = "panelOstatniOdbiorcy";
            panelOstatniOdbiorcy.Size = new System.Drawing.Size(410, 260);
            panelOstatniOdbiorcy.TabIndex = 6;
            panelOstatniOdbiorcy.Visible = true;
            // 
            // lblOstatniOdbiorcy
            // 
            lblOstatniOdbiorcy.BackColor = System.Drawing.Color.Transparent;
            lblOstatniOdbiorcy.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblOstatniOdbiorcy.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            lblOstatniOdbiorcy.Location = new System.Drawing.Point(10, 8);
            lblOstatniOdbiorcy.Name = "lblOstatniOdbiorcy";
            lblOstatniOdbiorcy.Size = new System.Drawing.Size(390, 20);
            lblOstatniOdbiorcy.TabIndex = 0;
            lblOstatniOdbiorcy.Text = "Wybierz odbiorcę:";
            // 
            // gridOstatniOdbiorcy
            // 
            gridOstatniOdbiorcy.AllowUserToAddRows = false;
            gridOstatniOdbiorcy.AllowUserToDeleteRows = false;
            gridOstatniOdbiorcy.AllowUserToResizeColumns = false;
            gridOstatniOdbiorcy.AllowUserToResizeRows = false;
            gridOstatniOdbiorcy.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridOstatniOdbiorcy.BackgroundColor = System.Drawing.Color.White;
            gridOstatniOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridOstatniOdbiorcy.ColumnHeadersVisible = false;
            gridOstatniOdbiorcy.Font = new System.Drawing.Font("Segoe UI", 9F);
            gridOstatniOdbiorcy.GridColor = System.Drawing.Color.FromArgb(243, 244, 246);
            gridOstatniOdbiorcy.Location = new System.Drawing.Point(10, 30);
            gridOstatniOdbiorcy.MultiSelect = false;
            gridOstatniOdbiorcy.Name = "gridOstatniOdbiorcy";
            gridOstatniOdbiorcy.ReadOnly = true;
            gridOstatniOdbiorcy.RowHeadersVisible = false;
            gridOstatniOdbiorcy.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            gridOstatniOdbiorcy.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            gridOstatniOdbiorcy.Size = new System.Drawing.Size(390, 220);
            gridOstatniOdbiorcy.TabIndex = 1;
            // 
            // panelOdbiorca
            // 
            panelOdbiorca.Controls.Add(lblTytul);
            panelOdbiorca.Controls.Add(label4);
            panelOdbiorca.Controls.Add(cbHandlowiecFilter);
            panelOdbiorca.Controls.Add(txtSzukajOdbiorcy);
            panelOdbiorca.Controls.Add(label1);
            panelOdbiorca.Dock = System.Windows.Forms.DockStyle.Top;
            panelOdbiorca.Location = new System.Drawing.Point(0, 0);
            panelOdbiorca.Name = "panelOdbiorca";
            panelOdbiorca.Padding = new System.Windows.Forms.Padding(20, 15, 20, 10);
            panelOdbiorca.Size = new System.Drawing.Size(430, 180);
            panelOdbiorca.TabIndex = 1;
            // 
            // lblTytul
            // 
            lblTytul.AutoSize = true;
            lblTytul.Font = new System.Drawing.Font("Segoe UI Semibold", 16F, System.Drawing.FontStyle.Bold);
            lblTytul.ForeColor = System.Drawing.Color.FromArgb(17, 24, 39);
            lblTytul.Location = new System.Drawing.Point(10, 15);
            lblTytul.Name = "lblTytul";
            lblTytul.Size = new System.Drawing.Size(189, 30);
            lblTytul.TabIndex = 5;
            lblTytul.Text = "Nowe zamówienie";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new System.Drawing.Font("Segoe UI", 9F);
            label4.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            label4.Location = new System.Drawing.Point(10, 65);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(70, 15);
            label4.TabIndex = 6;
            label4.Text = "Handlowiec";
            // 
            // cbHandlowiecFilter
            // 
            cbHandlowiecFilter.BackColor = System.Drawing.Color.FromArgb(249, 250, 251);
            cbHandlowiecFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cbHandlowiecFilter.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            cbHandlowiecFilter.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            cbHandlowiecFilter.FormattingEnabled = true;
            cbHandlowiecFilter.Location = new System.Drawing.Point(10, 85);
            cbHandlowiecFilter.Name = "cbHandlowiecFilter";
            cbHandlowiecFilter.Size = new System.Drawing.Size(410, 25);
            cbHandlowiecFilter.TabIndex = 2;
            // 
            // txtSzukajOdbiorcy
            // 
            txtSzukajOdbiorcy.BackColor = System.Drawing.Color.FromArgb(249, 250, 251);
            txtSzukajOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtSzukajOdbiorcy.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            txtSzukajOdbiorcy.Location = new System.Drawing.Point(10, 140);
            txtSzukajOdbiorcy.Name = "txtSzukajOdbiorcy";
            txtSzukajOdbiorcy.PlaceholderText = "Wpisz nazwę, NIP lub miasto...";
            txtSzukajOdbiorcy.Size = new System.Drawing.Size(410, 24);
            txtSzukajOdbiorcy.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Segoe UI", 9F);
            label1.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            label1.Location = new System.Drawing.Point(10, 120);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(54, 15);
            label1.TabIndex = 0;
            label1.Text = "Odbiorca";
            // 
            // panelDetails
            // 
            panelDetails.BackColor = System.Drawing.Color.White;
            panelDetails.Controls.Add(dataGridViewZamowienie);
            panelDetails.Controls.Add(panelDaneOdbiorcy);
            panelDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            panelDetails.Location = new System.Drawing.Point(430, 0);
            panelDetails.Margin = new System.Windows.Forms.Padding(0);
            panelDetails.Name = "panelDetails";
            panelDetails.Padding = new System.Windows.Forms.Padding(0, 0, 0, 60);
            panelDetails.Size = new System.Drawing.Size(850, 720);
            panelDetails.TabIndex = 1;
            // 
            // dataGridViewZamowienie
            // 
            dataGridViewZamowienie.BackgroundColor = System.Drawing.Color.White;
            dataGridViewZamowienie.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dataGridViewZamowienie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZamowienie.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridViewZamowienie.GridColor = System.Drawing.Color.FromArgb(243, 244, 246);
            dataGridViewZamowienie.Location = new System.Drawing.Point(0, 60);
            dataGridViewZamowienie.Name = "dataGridViewZamowienie";
            dataGridViewZamowienie.Size = new System.Drawing.Size(850, 600);
            dataGridViewZamowienie.TabIndex = 1;
            // 
            // panelDaneOdbiorcy
            // 
            panelDaneOdbiorcy.BackColor = System.Drawing.Color.White;
            panelDaneOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.None;
            panelDaneOdbiorcy.Controls.Add(lblHandlowiec);
            panelDaneOdbiorcy.Controls.Add(lblAdres);
            panelDaneOdbiorcy.Controls.Add(lblNip);
            panelDaneOdbiorcy.Controls.Add(lblWybranyOdbiorca);
            panelDaneOdbiorcy.Dock = System.Windows.Forms.DockStyle.Top;
            panelDaneOdbiorcy.Location = new System.Drawing.Point(0, 0);
            panelDaneOdbiorcy.Name = "panelDaneOdbiorcy";
            panelDaneOdbiorcy.Size = new System.Drawing.Size(850, 60);
            panelDaneOdbiorcy.TabIndex = 4;
            panelDaneOdbiorcy.Visible = false;
            // 
            // lblHandlowiec
            // 
            lblHandlowiec.AutoSize = true;
            lblHandlowiec.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblHandlowiec.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            lblHandlowiec.Location = new System.Drawing.Point(650, 30);
            lblHandlowiec.Name = "lblHandlowiec";
            lblHandlowiec.Size = new System.Drawing.Size(55, 15);
            lblHandlowiec.TabIndex = 3;
            lblHandlowiec.Text = "Opiekun:";
            // 
            // lblAdres
            // 
            lblAdres.AutoSize = true;
            lblAdres.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblAdres.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            lblAdres.Location = new System.Drawing.Point(450, 30);
            lblAdres.Name = "lblAdres";
            lblAdres.Size = new System.Drawing.Size(40, 15);
            lblAdres.TabIndex = 2;
            lblAdres.Text = "Adres:";
            // 
            // lblNip
            // 
            lblNip.AutoSize = true;
            lblNip.Font = new System.Drawing.Font("Segoe UI", 9F);
            lblNip.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128);
            lblNip.Location = new System.Drawing.Point(300, 30);
            lblNip.Name = "lblNip";
            lblNip.Size = new System.Drawing.Size(29, 15);
            lblNip.TabIndex = 1;
            lblNip.Text = "NIP:";
            // 
            // lblWybranyOdbiorca
            // 
            lblWybranyOdbiorca.AutoSize = false;
            lblWybranyOdbiorca.Font = new System.Drawing.Font("Segoe UI Semibold", 11F, System.Drawing.FontStyle.Bold);
            lblWybranyOdbiorca.ForeColor = System.Drawing.Color.FromArgb(17, 24, 39);
            lblWybranyOdbiorca.Location = new System.Drawing.Point(15, 8);
            lblWybranyOdbiorca.Name = "lblWybranyOdbiorca";
            lblWybranyOdbiorca.Size = new System.Drawing.Size(820, 20);
            lblWybranyOdbiorca.TabIndex = 0;
            lblWybranyOdbiorca.Text = "Wybrany odbiorca";
            lblWybranyOdbiorca.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelSumaGrid
            // 
            panelSumaGrid.BackColor = System.Drawing.Color.FromArgb(17, 24, 39);
            panelSumaGrid.ColumnCount = 5;
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            panelSumaGrid.Controls.Add(summaryLabelIlosc, 3, 0);
            panelSumaGrid.Controls.Add(summaryLabelPojemniki, 2, 0);
            panelSumaGrid.Controls.Add(summaryLabelPalety, 1, 0);
            panelSumaGrid.Controls.Add(label6, 0, 0);
            panelSumaGrid.Dock = System.Windows.Forms.DockStyle.Bottom;
            panelSumaGrid.Location = new System.Drawing.Point(0, 660);
            panelSumaGrid.Name = "panelSumaGrid";
            panelSumaGrid.RowCount = 1;
            panelSumaGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelSumaGrid.Size = new System.Drawing.Size(850, 60);
            panelSumaGrid.TabIndex = 2;
            panelSumaGrid.Visible = false;
            // 
            // summaryLabelIlosc
            // 
            summaryLabelIlosc.AutoSize = true;
            summaryLabelIlosc.Dock = System.Windows.Forms.DockStyle.Fill;
            summaryLabelIlosc.Font = new System.Drawing.Font("Segoe UI Semibold", 12F, System.Drawing.FontStyle.Bold);
            summaryLabelIlosc.ForeColor = System.Drawing.Color.White;
            summaryLabelIlosc.Location = new System.Drawing.Point(597, 0);
            summaryLabelIlosc.Name = "summaryLabelIlosc";
            summaryLabelIlosc.Size = new System.Drawing.Size(121, 60);
            summaryLabelIlosc.TabIndex = 3;
            summaryLabelIlosc.Text = "0 kg";
            summaryLabelIlosc.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // summaryLabelPojemniki
            // 
            summaryLabelPojemniki.AutoSize = true;
            summaryLabelPojemniki.Dock = System.Windows.Forms.DockStyle.Fill;
            summaryLabelPojemniki.Font = new System.Drawing.Font("Segoe UI Semibold", 12F, System.Drawing.FontStyle.Bold);
            summaryLabelPojemniki.ForeColor = System.Drawing.Color.White;
            summaryLabelPojemniki.Location = new System.Drawing.Point(470, 0);
            summaryLabelPojemniki.Name = "summaryLabelPojemniki";
            summaryLabelPojemniki.Size = new System.Drawing.Size(121, 60);
            summaryLabelPojemniki.TabIndex = 2;
            summaryLabelPojemniki.Text = "0";
            summaryLabelPojemniki.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // summaryLabelPalety
            // 
            summaryLabelPalety.AutoSize = true;
            summaryLabelPalety.Dock = System.Windows.Forms.DockStyle.Fill;
            summaryLabelPalety.Font = new System.Drawing.Font("Segoe UI Semibold", 12F, System.Drawing.FontStyle.Bold);
            summaryLabelPalety.ForeColor = System.Drawing.Color.White;
            summaryLabelPalety.Location = new System.Drawing.Point(343, 0);
            summaryLabelPalety.Name = "summaryLabelPalety";
            summaryLabelPalety.Size = new System.Drawing.Size(121, 60);
            summaryLabelPalety.TabIndex = 1;
            summaryLabelPalety.Text = "0";
            summaryLabelPalety.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Dock = System.Windows.Forms.DockStyle.Fill;
            label6.Font = new System.Drawing.Font("Segoe UI", 10F);
            label6.ForeColor = System.Drawing.Color.FromArgb(156, 163, 175);
            label6.Location = new System.Drawing.Point(3, 0);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(334, 60);
            label6.TabIndex = 0;
            label6.Text = "PODSUMOWANIE";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // WidokZamowienia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(249, 250, 251);
            ClientSize = new System.Drawing.Size(1280, 720);
            Controls.Add(rootLayout);
            MinimumSize = new System.Drawing.Size(1200, 680);
            Name = "WidokZamowienia";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Zamówienie mięsa";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            rootLayout.ResumeLayout(false);
            panelMaster.ResumeLayout(false);
            panelAkcji.ResumeLayout(false);
            panelDetaleZamowienia.ResumeLayout(false);
            panelDetaleZamowienia.PerformLayout();
            panelOstatniOdbiorcy.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridOstatniOdbiorcy).EndInit();
            panelOdbiorca.ResumeLayout(false);
            panelOdbiorca.PerformLayout();
            panelDetails.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).EndInit();
            panelDaneOdbiorcy.ResumeLayout(false);
            panelDaneOdbiorcy.PerformLayout();
            panelSumaGrid.ResumeLayout(false);
            panelSumaGrid.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Panel panelMaster;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtSzukajOdbiorcy;
        private System.Windows.Forms.ComboBox cbHandlowiecFilter;
        private System.Windows.Forms.Panel panelDetaleZamowienia;
        private System.Windows.Forms.DateTimePicker dateTimePickerSprzedaz;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker dateTimePickerGodzinaPrzyjazdu;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxUwagi;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.DataGridView dataGridViewZamowienie;
        private System.Windows.Forms.ListBox listaWynikowOdbiorcy;
        private System.Windows.Forms.Panel panelAkcji;
        private System.Windows.Forms.Button btnZapisz;
        private System.Windows.Forms.Button btnAnuluj;
        private System.Windows.Forms.Panel panelDetails;
        private System.Windows.Forms.TableLayoutPanel panelSumaGrid;
        private System.Windows.Forms.Label summaryLabelIlosc;
        private System.Windows.Forms.Label summaryLabelPojemniki;
        private System.Windows.Forms.Label summaryLabelPalety;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Panel panelOdbiorca;
        private System.Windows.Forms.Label lblTytul;
        private System.Windows.Forms.Panel panelDaneOdbiorcy;
        private System.Windows.Forms.Label lblHandlowiec;
        private System.Windows.Forms.Label lblAdres;
        private System.Windows.Forms.Label lblNip;
        private System.Windows.Forms.Label lblWybranyOdbiorca;
        private System.Windows.Forms.Panel panelOstatniOdbiorcy;
        private System.Windows.Forms.Label lblOstatniOdbiorcy;
        private System.Windows.Forms.DataGridView gridOstatniOdbiorcy;
    }
}