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
            panelAkcji = new System.Windows.Forms.FlowLayoutPanel();
            btnZapisz = new System.Windows.Forms.Button();
            btnAnuluj = new System.Windows.Forms.Button();
            panelDetaleZamowienia = new System.Windows.Forms.Panel();
            label5 = new System.Windows.Forms.Label();
            textBoxUwagi = new System.Windows.Forms.TextBox();
            dateTimePickerGodzinaPrzyjazdu = new System.Windows.Forms.DateTimePicker();
            label3 = new System.Windows.Forms.Label();
            dateTimePickerSprzedaz = new System.Windows.Forms.DateTimePicker();
            label2 = new System.Windows.Forms.Label();
            panelOdbiorca = new System.Windows.Forms.Panel();
            listaWynikowOdbiorcy = new System.Windows.Forms.ListBox();
            panelDaneOdbiorcy = new System.Windows.Forms.Panel();
            lblHandlowiec = new System.Windows.Forms.Label();
            lblAdres = new System.Windows.Forms.Label();
            lblNip = new System.Windows.Forms.Label();
            lblWybranyOdbiorca = new System.Windows.Forms.Label();
            btnPickOdbiorca = new System.Windows.Forms.Button();
            cbHandlowiecFilter = new System.Windows.Forms.ComboBox();
            txtSzukajOdbiorcy = new System.Windows.Forms.TextBox();
            label1 = new System.Windows.Forms.Label();
            panelTytulowy = new System.Windows.Forms.Panel();
            lblTytul = new System.Windows.Forms.Label();
            panelDetails = new System.Windows.Forms.TableLayoutPanel();
            dataGridViewZamowienie = new System.Windows.Forms.DataGridView();
            panelSzukajTowaru = new System.Windows.Forms.Panel();
            txtSzukajTowaru = new System.Windows.Forms.TextBox();
            label4 = new System.Windows.Forms.Label();
            panelSumaGrid = new System.Windows.Forms.TableLayoutPanel();
            summaryLabelIlosc = new System.Windows.Forms.Label();
            summaryLabelPojemniki = new System.Windows.Forms.Label();
            summaryLabelPalety = new System.Windows.Forms.Label();
            label6 = new System.Windows.Forms.Label();
            rootLayout.SuspendLayout();
            panelMaster.SuspendLayout();
            panelAkcji.SuspendLayout();
            panelDetaleZamowienia.SuspendLayout();
            panelOdbiorca.SuspendLayout();
            panelDaneOdbiorcy.SuspendLayout();
            panelTytulowy.SuspendLayout();
            panelDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).BeginInit();
            panelSzukajTowaru.SuspendLayout();
            panelSumaGrid.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 2;
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 450F));
            rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.Controls.Add(panelMaster, 0, 0);
            rootLayout.Controls.Add(panelDetails, 1, 0);
            rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            rootLayout.Location = new System.Drawing.Point(0, 0);
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 1;
            rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            rootLayout.Size = new System.Drawing.Size(1370, 761);
            rootLayout.TabIndex = 0;
            // 
            // panelMaster
            // 
            panelMaster.BackColor = System.Drawing.Color.FromArgb(248, 249, 250);
            panelMaster.Controls.Add(panelAkcji);
            panelMaster.Controls.Add(panelDetaleZamowienia);
            panelMaster.Controls.Add(panelOdbiorca);
            panelMaster.Controls.Add(panelTytulowy);
            panelMaster.Dock = System.Windows.Forms.DockStyle.Fill;
            panelMaster.Location = new System.Drawing.Point(0, 0);
            panelMaster.Margin = new System.Windows.Forms.Padding(0);
            panelMaster.Name = "panelMaster";
            panelMaster.Size = new System.Drawing.Size(450, 761);
            panelMaster.TabIndex = 0;
            // 
            // panelAkcji
            // 
            panelAkcji.Controls.Add(btnZapisz);
            panelAkcji.Controls.Add(btnAnuluj);
            panelAkcji.Dock = System.Windows.Forms.DockStyle.Bottom;
            panelAkcji.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            panelAkcji.Location = new System.Drawing.Point(0, 671);
            panelAkcji.Name = "panelAkcji";
            panelAkcji.Padding = new System.Windows.Forms.Padding(20);
            panelAkcji.Size = new System.Drawing.Size(450, 90);
            panelAkcji.TabIndex = 3;
            // 
            // btnZapisz
            // 
            btnZapisz.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnZapisz.BackColor = System.Drawing.Color.FromArgb(25, 135, 84);
            btnZapisz.FlatAppearance.BorderSize = 0;
            btnZapisz.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnZapisz.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            btnZapisz.ForeColor = System.Drawing.Color.White;
            btnZapisz.Location = new System.Drawing.Point(308, 23);
            btnZapisz.Name = "btnZapisz";
            btnZapisz.Size = new System.Drawing.Size(99, 45);
            btnZapisz.TabIndex = 0;
            btnZapisz.Text = "Zapisz (Ctrl+S)";
            btnZapisz.UseVisualStyleBackColor = false;
            btnZapisz.Click += btnZapisz_Click;
            // 
            // btnAnuluj
            // 
            btnAnuluj.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnAnuluj.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnAnuluj.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnAnuluj.Location = new System.Drawing.Point(202, 23);
            btnAnuluj.Name = "btnAnuluj";
            btnAnuluj.Size = new System.Drawing.Size(100, 45);
            btnAnuluj.TabIndex = 1;
            btnAnuluj.Text = "Anuluj";
            btnAnuluj.UseVisualStyleBackColor = true;
            // 
            // panelDetaleZamowienia
            // 
            panelDetaleZamowienia.Controls.Add(label5);
            panelDetaleZamowienia.Controls.Add(textBoxUwagi);
            panelDetaleZamowienia.Controls.Add(dateTimePickerGodzinaPrzyjazdu);
            panelDetaleZamowienia.Controls.Add(label3);
            panelDetaleZamowienia.Controls.Add(dateTimePickerSprzedaz);
            panelDetaleZamowienia.Controls.Add(label2);
            panelDetaleZamowienia.Dock = System.Windows.Forms.DockStyle.Top;
            panelDetaleZamowienia.Location = new System.Drawing.Point(0, 316);
            panelDetaleZamowienia.Name = "panelDetaleZamowienia";
            panelDetaleZamowienia.Padding = new System.Windows.Forms.Padding(20, 10, 20, 10);
            panelDetaleZamowienia.Size = new System.Drawing.Size(450, 313);
            panelDetaleZamowienia.TabIndex = 2;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label5.Location = new System.Drawing.Point(23, 114);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(57, 17);
            label5.TabIndex = 5;
            label5.Text = "Notatka";
            // 
            // textBoxUwagi
            // 
            textBoxUwagi.AcceptsReturn = true;
            textBoxUwagi.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            textBoxUwagi.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            textBoxUwagi.Location = new System.Drawing.Point(23, 140);
            textBoxUwagi.Multiline = true;
            textBoxUwagi.Name = "textBoxUwagi";
            textBoxUwagi.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            textBoxUwagi.Size = new System.Drawing.Size(404, 163);
            textBoxUwagi.TabIndex = 4;
            // 
            // dateTimePickerGodzinaPrzyjazdu
            // 
            dateTimePickerGodzinaPrzyjazdu.CustomFormat = "HH:mm";
            dateTimePickerGodzinaPrzyjazdu.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dateTimePickerGodzinaPrzyjazdu.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            dateTimePickerGodzinaPrzyjazdu.Location = new System.Drawing.Point(212, 65);
            dateTimePickerGodzinaPrzyjazdu.Name = "dateTimePickerGodzinaPrzyjazdu";
            dateTimePickerGodzinaPrzyjazdu.ShowUpDown = true;
            dateTimePickerGodzinaPrzyjazdu.Size = new System.Drawing.Size(100, 25);
            dateTimePickerGodzinaPrzyjazdu.TabIndex = 3;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label3.Location = new System.Drawing.Point(209, 39);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(98, 17);
            label3.TabIndex = 2;
            label3.Text = "Godz. Odbioru";
            // 
            // dateTimePickerSprzedaz
            // 
            dateTimePickerSprzedaz.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dateTimePickerSprzedaz.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            dateTimePickerSprzedaz.Location = new System.Drawing.Point(23, 65);
            dateTimePickerSprzedaz.Name = "dateTimePickerSprzedaz";
            dateTimePickerSprzedaz.Size = new System.Drawing.Size(149, 25);
            dateTimePickerSprzedaz.TabIndex = 1;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label2.Location = new System.Drawing.Point(23, 39);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(102, 17);
            label2.TabIndex = 0;
            label2.Text = "Data Sprzedaży";
            // 
            // panelOdbiorca
            // 
            panelOdbiorca.Controls.Add(listaWynikowOdbiorcy);
            panelOdbiorca.Controls.Add(panelDaneOdbiorcy);
            panelOdbiorca.Controls.Add(btnPickOdbiorca);
            panelOdbiorca.Controls.Add(cbHandlowiecFilter);
            panelOdbiorca.Controls.Add(txtSzukajOdbiorcy);
            panelOdbiorca.Controls.Add(label1);
            panelOdbiorca.Dock = System.Windows.Forms.DockStyle.Top;
            panelOdbiorca.Location = new System.Drawing.Point(0, 70);
            panelOdbiorca.Name = "panelOdbiorca";
            panelOdbiorca.Padding = new System.Windows.Forms.Padding(20, 10, 20, 10);
            panelOdbiorca.Size = new System.Drawing.Size(450, 246);
            panelOdbiorca.TabIndex = 1;
            // 
            // listaWynikowOdbiorcy
            // 
            listaWynikowOdbiorcy.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            listaWynikowOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            listaWynikowOdbiorcy.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            listaWynikowOdbiorcy.FormattingEnabled = true;
            listaWynikowOdbiorcy.ItemHeight = 17;
            listaWynikowOdbiorcy.Location = new System.Drawing.Point(23, 99);
            listaWynikowOdbiorcy.Name = "listaWynikowOdbiorcy";
            listaWynikowOdbiorcy.Size = new System.Drawing.Size(404, 172);
            listaWynikowOdbiorcy.TabIndex = 5;
            listaWynikowOdbiorcy.Visible = false;
            // 
            // panelDaneOdbiorcy
            // 
            panelDaneOdbiorcy.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            panelDaneOdbiorcy.BackColor = System.Drawing.Color.White;
            panelDaneOdbiorcy.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelDaneOdbiorcy.Controls.Add(lblHandlowiec);
            panelDaneOdbiorcy.Controls.Add(lblAdres);
            panelDaneOdbiorcy.Controls.Add(lblNip);
            panelDaneOdbiorcy.Controls.Add(lblWybranyOdbiorca);
            panelDaneOdbiorcy.Location = new System.Drawing.Point(23, 105);
            panelDaneOdbiorcy.Name = "panelDaneOdbiorcy";
            panelDaneOdbiorcy.Size = new System.Drawing.Size(404, 128);
            panelDaneOdbiorcy.TabIndex = 4;
            panelDaneOdbiorcy.Visible = false;
            // 
            // lblHandlowiec
            // 
            lblHandlowiec.AutoSize = true;
            lblHandlowiec.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblHandlowiec.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
            lblHandlowiec.Location = new System.Drawing.Point(16, 96);
            lblHandlowiec.Name = "lblHandlowiec";
            lblHandlowiec.Size = new System.Drawing.Size(55, 15);
            lblHandlowiec.TabIndex = 3;
            lblHandlowiec.Text = "Opiekun:";
            // 
            // lblAdres
            // 
            lblAdres.AutoSize = true;
            lblAdres.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblAdres.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
            lblAdres.Location = new System.Drawing.Point(16, 70);
            lblAdres.Name = "lblAdres";
            lblAdres.Size = new System.Drawing.Size(40, 15);
            lblAdres.TabIndex = 2;
            lblAdres.Text = "Adres:";
            // 
            // lblNip
            // 
            lblNip.AutoSize = true;
            lblNip.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblNip.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
            lblNip.Location = new System.Drawing.Point(16, 45);
            lblNip.Name = "lblNip";
            lblNip.Size = new System.Drawing.Size(29, 15);
            lblNip.TabIndex = 1;
            lblNip.Text = "NIP:";
            // 
            // lblWybranyOdbiorca
            // 
            lblWybranyOdbiorca.AutoSize = true;
            lblWybranyOdbiorca.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblWybranyOdbiorca.Location = new System.Drawing.Point(14, 14);
            lblWybranyOdbiorca.Name = "lblWybranyOdbiorca";
            lblWybranyOdbiorca.Size = new System.Drawing.Size(139, 20);
            lblWybranyOdbiorca.TabIndex = 0;
            lblWybranyOdbiorca.Text = "Wybrany Odbiorca";
            // 
            // btnPickOdbiorca
            // 
            btnPickOdbiorca.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnPickOdbiorca.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnPickOdbiorca.Location = new System.Drawing.Point(347, 36);
            btnPickOdbiorca.Name = "btnPickOdbiorca";
            btnPickOdbiorca.Size = new System.Drawing.Size(80, 25);
            btnPickOdbiorca.TabIndex = 3;
            btnPickOdbiorca.Text = "Wybierz...";
            btnPickOdbiorca.UseVisualStyleBackColor = true;
            btnPickOdbiorca.Visible = false;
            // 
            // cbHandlowiecFilter
            // 
            cbHandlowiecFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cbHandlowiecFilter.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cbHandlowiecFilter.FormattingEnabled = true;
            cbHandlowiecFilter.Location = new System.Drawing.Point(23, 36);
            cbHandlowiecFilter.Name = "cbHandlowiecFilter";
            cbHandlowiecFilter.Size = new System.Drawing.Size(188, 25);
            cbHandlowiecFilter.TabIndex = 2;
            // 
            // txtSzukajOdbiorcy
            // 
            txtSzukajOdbiorcy.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            txtSzukajOdbiorcy.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtSzukajOdbiorcy.Location = new System.Drawing.Point(23, 68);
            txtSzukajOdbiorcy.Name = "txtSzukajOdbiorcy";
            txtSzukajOdbiorcy.PlaceholderText = "Wpisz nazwę, NIP lub miasto...";
            txtSzukajOdbiorcy.Size = new System.Drawing.Size(404, 25);
            txtSzukajOdbiorcy.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label1.Location = new System.Drawing.Point(23, 10);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(64, 17);
            label1.TabIndex = 0;
            label1.Text = "Odbiorca";
            // 
            // panelTytulowy
            // 
            panelTytulowy.Controls.Add(lblTytul);
            panelTytulowy.Dock = System.Windows.Forms.DockStyle.Top;
            panelTytulowy.Location = new System.Drawing.Point(0, 0);
            panelTytulowy.Name = "panelTytulowy";
            panelTytulowy.Size = new System.Drawing.Size(450, 70);
            panelTytulowy.TabIndex = 0;
            // 
            // lblTytul
            // 
            lblTytul.AutoSize = true;
            lblTytul.Font = new System.Drawing.Font("Segoe UI", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTytul.Location = new System.Drawing.Point(20, 20);
            lblTytul.Name = "lblTytul";
            lblTytul.Size = new System.Drawing.Size(197, 30);
            lblTytul.TabIndex = 0;
            lblTytul.Text = "Nowe Zamówienie";
            // 
            // panelDetails
            // 
            panelDetails.BackColor = System.Drawing.Color.White;
            panelDetails.ColumnCount = 1;
            panelDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelDetails.Controls.Add(dataGridViewZamowienie, 0, 1);
            panelDetails.Controls.Add(panelSzukajTowaru, 0, 0);
            panelDetails.Controls.Add(panelSumaGrid, 0, 2);
            panelDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            panelDetails.Location = new System.Drawing.Point(453, 3);
            panelDetails.Name = "panelDetails";
            panelDetails.Padding = new System.Windows.Forms.Padding(10, 20, 20, 10);
            panelDetails.RowCount = 3;
            panelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 60F));
            panelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            panelDetails.Size = new System.Drawing.Size(914, 755);
            panelDetails.TabIndex = 1;
            // 
            // dataGridViewZamowienie
            // 
            dataGridViewZamowienie.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZamowienie.Dock = System.Windows.Forms.DockStyle.Fill;
            dataGridViewZamowienie.Location = new System.Drawing.Point(13, 83);
            dataGridViewZamowienie.Name = "dataGridViewZamowienie";
            dataGridViewZamowienie.Size = new System.Drawing.Size(878, 619);
            dataGridViewZamowienie.TabIndex = 1;
            // 
            // panelSzukajTowaru
            // 
            panelSzukajTowaru.Controls.Add(txtSzukajTowaru);
            panelSzukajTowaru.Controls.Add(label4);
            panelSzukajTowaru.Dock = System.Windows.Forms.DockStyle.Fill;
            panelSzukajTowaru.Location = new System.Drawing.Point(10, 20);
            panelSzukajTowaru.Margin = new System.Windows.Forms.Padding(0);
            panelSzukajTowaru.Name = "panelSzukajTowaru";
            panelSzukajTowaru.Size = new System.Drawing.Size(884, 60);
            panelSzukajTowaru.TabIndex = 0;
            // 
            // txtSzukajTowaru
            // 
            txtSzukajTowaru.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtSzukajTowaru.Location = new System.Drawing.Point(3, 27);
            txtSzukajTowaru.Name = "txtSzukajTowaru";
            txtSzukajTowaru.PlaceholderText = "Wpisz kod towaru...";
            txtSzukajTowaru.Size = new System.Drawing.Size(351, 25);
            txtSzukajTowaru.TabIndex = 1;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label4.Location = new System.Drawing.Point(3, 4);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(126, 17);
            label4.TabIndex = 0;
            label4.Text = "Wyszukaj w ofercie";
            // 
            // panelSumaGrid
            // 
            panelSumaGrid.BackColor = System.Drawing.Color.FromArgb(248, 249, 250);
            panelSumaGrid.ColumnCount = 5;
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 48.63636F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10.22727F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 12.5F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 13.63636F));
            panelSumaGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            panelSumaGrid.Controls.Add(summaryLabelIlosc, 3, 0);
            panelSumaGrid.Controls.Add(summaryLabelPojemniki, 2, 0);
            panelSumaGrid.Controls.Add(summaryLabelPalety, 1, 0);
            panelSumaGrid.Controls.Add(label6, 0, 0);
            panelSumaGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            panelSumaGrid.Location = new System.Drawing.Point(13, 708);
            panelSumaGrid.Name = "panelSumaGrid";
            panelSumaGrid.RowCount = 1;
            panelSumaGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelSumaGrid.Size = new System.Drawing.Size(878, 34);
            panelSumaGrid.TabIndex = 2;
            // 
            // summaryLabelIlosc
            // 
            summaryLabelIlosc.AutoSize = true;
            summaryLabelIlosc.Dock = System.Windows.Forms.DockStyle.Fill;
            summaryLabelIlosc.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            summaryLabelIlosc.Location = new System.Drawing.Point(628, 0);
            summaryLabelIlosc.Name = "summaryLabelIlosc";
            summaryLabelIlosc.Size = new System.Drawing.Size(113, 34);
            summaryLabelIlosc.TabIndex = 3;
            summaryLabelIlosc.Text = "0 kg";
            summaryLabelIlosc.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // summaryLabelPojemniki
            // 
            summaryLabelPojemniki.AutoSize = true;
            summaryLabelPojemniki.Dock = System.Windows.Forms.DockStyle.Fill;
            summaryLabelPojemniki.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            summaryLabelPojemniki.Location = new System.Drawing.Point(519, 0);
            summaryLabelPojemniki.Name = "summaryLabelPojemniki";
            summaryLabelPojemniki.Size = new System.Drawing.Size(103, 34);
            summaryLabelPojemniki.TabIndex = 2;
            summaryLabelPojemniki.Text = "0";
            summaryLabelPojemniki.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // summaryLabelPalety
            // 
            summaryLabelPalety.AutoSize = true;
            summaryLabelPalety.Dock = System.Windows.Forms.DockStyle.Fill;
            summaryLabelPalety.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            summaryLabelPalety.Location = new System.Drawing.Point(430, 0);
            summaryLabelPalety.Name = "summaryLabelPalety";
            summaryLabelPalety.Size = new System.Drawing.Size(83, 34);
            summaryLabelPalety.TabIndex = 1;
            summaryLabelPalety.Text = "0";
            summaryLabelPalety.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Dock = System.Windows.Forms.DockStyle.Fill;
            label6.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            label6.Location = new System.Drawing.Point(3, 0);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(421, 34);
            label6.TabIndex = 0;
            label6.Text = "SUMA:";
            label6.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // WidokZamowienia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1370, 761);
            Controls.Add(rootLayout);
            MinimumSize = new System.Drawing.Size(1280, 800);
            Name = "WidokZamowienia";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Tworzenie Zamówienia";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            rootLayout.ResumeLayout(false);
            panelMaster.ResumeLayout(false);
            panelAkcji.ResumeLayout(false);
            panelDetaleZamowienia.ResumeLayout(false);
            panelDetaleZamowienia.PerformLayout();
            panelOdbiorca.ResumeLayout(false);
            panelOdbiorca.PerformLayout();
            panelDaneOdbiorcy.ResumeLayout(false);
            panelDaneOdbiorcy.PerformLayout();
            panelTytulowy.ResumeLayout(false);
            panelTytulowy.PerformLayout();
            panelDetails.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).EndInit();
            panelSzukajTowaru.ResumeLayout(false);
            panelSzukajTowaru.PerformLayout();
            panelSumaGrid.ResumeLayout(false);
            panelSumaGrid.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Panel panelMaster;
        private System.Windows.Forms.Panel panelTytulowy;
        private System.Windows.Forms.Label lblTytul;
        private System.Windows.Forms.Panel panelOdbiorca;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtSzukajOdbiorcy;
        private System.Windows.Forms.ComboBox cbHandlowiecFilter;
        private System.Windows.Forms.Button btnPickOdbiorca;
        private System.Windows.Forms.Panel panelDaneOdbiorcy;
        private System.Windows.Forms.Label lblWybranyOdbiorca;
        private System.Windows.Forms.Label lblNip;
        private System.Windows.Forms.Label lblAdres;
        private System.Windows.Forms.Label lblHandlowiec;
        private System.Windows.Forms.Panel panelDetaleZamowienia;
        private System.Windows.Forms.DateTimePicker dateTimePickerSprzedaz;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.DateTimePicker dateTimePickerGodzinaPrzyjazdu;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxUwagi;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Panel panelSzukajTowaru;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtSzukajTowaru;
        private System.Windows.Forms.DataGridView dataGridViewZamowienie;
        private System.Windows.Forms.ListBox listaWynikowOdbiorcy;
        private System.Windows.Forms.FlowLayoutPanel panelAkcji;
        private System.Windows.Forms.Button btnZapisz;
        private System.Windows.Forms.Button btnAnuluj;
        private System.Windows.Forms.TableLayoutPanel panelDetails;
        private System.Windows.Forms.TableLayoutPanel panelSumaGrid;
        private System.Windows.Forms.Label summaryLabelIlosc;
        private System.Windows.Forms.Label summaryLabelPojemniki;
        private System.Windows.Forms.Label summaryLabelPalety;
        private System.Windows.Forms.Label label6;
        // UWAGA: Usunąłem zduplikowane deklaracje lblSuma, lblSumaPojemnikow, lblSumaPalet
        //        ponieważ nie były one już używane na rzecz nowych kontrolek 'summaryLabel...'.
    }
}

