#nullable disable
using DocumentFormat.OpenXml.Wordprocessing;
using LiveChartsCore.Measure;
using System.Windows;
using System.Windows.Forms;

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
            rootLayout = new TableLayoutPanel();
            panelMaster = new Panel();
            panelDetaleZamowienia = new Panel();
            listaWynikowOdbiorcy = new ListBox();
            textBoxUwagi = new TextBox();
            chkWlasnyOdbior = new System.Windows.Forms.CheckBox();
            dateTimePickerGodzinaPrzyjazdu = new DateTimePicker();
            dateTimePickerSprzedaz = new DateTimePicker();
            panelOstatniOdbiorcy = new Panel();
            lblOstatniOdbiorcy = new Label();
            gridOstatniOdbiorcy = new DataGridView();
            panelOdbiorca = new Panel();
            lblTytul = new Label();
            cbHandlowiecFilter = new ComboBox();
            txtSzukajOdbiorcy = new TextBox();
            panelAkcji = new Panel();
            btnZapisz = new Button();
            btnAnuluj = new Button();
            panelDetails = new Panel();
            dataGridViewZamowienie = new DataGridView();
            panelDaneOdbiorcy = new Panel();
            lblHandlowiec = new Label();
            lblAdres = new Label();
            lblNip = new Label();
            lblWybranyOdbiorca = new Label();
            rootLayout.SuspendLayout();
            panelMaster.SuspendLayout();
            panelDetaleZamowienia.SuspendLayout();
            panelOstatniOdbiorcy.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridOstatniOdbiorcy).BeginInit();
            panelOdbiorca.SuspendLayout();
            panelAkcji.SuspendLayout();
            panelDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).BeginInit();
            panelDaneOdbiorcy.SuspendLayout();
            SuspendLayout();
            // 
            // rootLayout
            // 
            rootLayout.ColumnCount = 2;
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430F));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.Controls.Add(panelMaster, 0, 0);
            rootLayout.Controls.Add(panelDetails, 1, 0);
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.Location = new System.Drawing.Point(0, 0);
            rootLayout.Margin = new Padding(0);
            rootLayout.Name = "rootLayout";
            rootLayout.RowCount = 1;
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.Size = new System.Drawing.Size(1280, 720);
            rootLayout.TabIndex = 0;
            // 
            // panelMaster
            // 
            panelMaster.BackColor = System.Drawing.Color.White;
            panelMaster.Controls.Add(panelDetaleZamowienia);
            panelMaster.Controls.Add(panelOdbiorca);
            panelMaster.Controls.Add(panelAkcji);
            panelMaster.Dock = DockStyle.Fill;
            panelMaster.Location = new System.Drawing.Point(0, 0);
            panelMaster.Margin = new Padding(0);
            panelMaster.Name = "panelMaster";
            panelMaster.Size = new System.Drawing.Size(430, 720);
            panelMaster.TabIndex = 0;
            // 
            // panelDetaleZamowienia
            // 
            panelDetaleZamowienia.Controls.Add(listaWynikowOdbiorcy);
            panelDetaleZamowienia.Controls.Add(textBoxUwagi);
            panelDetaleZamowienia.Controls.Add(chkWlasnyOdbior);
            panelDetaleZamowienia.Controls.Add(dateTimePickerGodzinaPrzyjazdu);
            panelDetaleZamowienia.Controls.Add(dateTimePickerSprzedaz);
            panelDetaleZamowienia.Controls.Add(panelOstatniOdbiorcy);
            panelDetaleZamowienia.Dock = DockStyle.Fill;
            panelDetaleZamowienia.Location = new System.Drawing.Point(0, 95);
            panelDetaleZamowienia.Name = "panelDetaleZamowienia";
            panelDetaleZamowienia.Padding = new Padding(20, 5, 20, 5);
            panelDetaleZamowienia.Size = new System.Drawing.Size(430, 555);
            panelDetaleZamowienia.TabIndex = 2;
            // 
            // listaWynikowOdbiorcy
            // 
            listaWynikowOdbiorcy.BorderStyle = BorderStyle.FixedSingle;
            listaWynikowOdbiorcy.FormattingEnabled = true;
            listaWynikowOdbiorcy.ItemHeight = 15;
            listaWynikowOdbiorcy.Location = new System.Drawing.Point(23, 16);
            listaWynikowOdbiorcy.Name = "listaWynikowOdbiorcy";
            listaWynikowOdbiorcy.Size = new System.Drawing.Size(410, 92);
            listaWynikowOdbiorcy.TabIndex = 5;
            listaWynikowOdbiorcy.Visible = false;
            // 
            // textBoxUwagi
            // 
            textBoxUwagi.AcceptsReturn = true;
            textBoxUwagi.Location = new System.Drawing.Point(10, 500);
            textBoxUwagi.Multiline = true;
            textBoxUwagi.Name = "textBoxUwagi";
            textBoxUwagi.ScrollBars = ScrollBars.Vertical;
            textBoxUwagi.Size = new System.Drawing.Size(410, 80);
            textBoxUwagi.TabIndex = 4;
            // 
            // chkWlasnyOdbior
            // 
            chkWlasnyOdbior.AutoSize = true;
            chkWlasnyOdbior.Location = new System.Drawing.Point(10, 350);
            chkWlasnyOdbior.Name = "chkWlasnyOdbior";
            chkWlasnyOdbior.Size = new System.Drawing.Size(102, 19);
            chkWlasnyOdbior.TabIndex = 6;
            chkWlasnyOdbior.Text = "Własny odbiór";
            chkWlasnyOdbior.UseVisualStyleBackColor = true;
            // 
            // dateTimePickerGodzinaPrzyjazdu
            // 
            dateTimePickerGodzinaPrzyjazdu.Format = DateTimePickerFormat.Custom;
            dateTimePickerGodzinaPrzyjazdu.Location = new System.Drawing.Point(217, 320);
            dateTimePickerGodzinaPrzyjazdu.Name = "dateTimePickerGodzinaPrzyjazdu";
            dateTimePickerGodzinaPrzyjazdu.ShowUpDown = true;
            dateTimePickerGodzinaPrzyjazdu.Size = new System.Drawing.Size(203, 23);
            dateTimePickerGodzinaPrzyjazdu.TabIndex = 3;
            // 
            // dateTimePickerSprzedaz
            // 
            dateTimePickerSprzedaz.Format = DateTimePickerFormat.Short;
            dateTimePickerSprzedaz.Location = new System.Drawing.Point(190, 415);
            dateTimePickerSprzedaz.Name = "dateTimePickerSprzedaz";
            dateTimePickerSprzedaz.Size = new System.Drawing.Size(165, 23);
            dateTimePickerSprzedaz.TabIndex = 1;
            // 
            // panelOstatniOdbiorcy
            // 
            panelOstatniOdbiorcy.Controls.Add(lblOstatniOdbiorcy);
            panelOstatniOdbiorcy.Controls.Add(gridOstatniOdbiorcy);
            panelOstatniOdbiorcy.Location = new System.Drawing.Point(10, 20);
            panelOstatniOdbiorcy.Name = "panelOstatniOdbiorcy";
            panelOstatniOdbiorcy.Size = new System.Drawing.Size(410, 260);
            panelOstatniOdbiorcy.TabIndex = 6;
            // 
            // lblOstatniOdbiorcy
            // 
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
            gridOstatniOdbiorcy.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridOstatniOdbiorcy.Location = new System.Drawing.Point(10, 30);
            gridOstatniOdbiorcy.Name = "gridOstatniOdbiorcy";
            gridOstatniOdbiorcy.ReadOnly = true;
            gridOstatniOdbiorcy.Size = new System.Drawing.Size(390, 220);
            gridOstatniOdbiorcy.TabIndex = 1;
            // 
            // panelOdbiorca
            // 
            panelOdbiorca.Controls.Add(lblTytul);
            panelOdbiorca.Controls.Add(cbHandlowiecFilter);
            panelOdbiorca.Controls.Add(txtSzukajOdbiorcy);
            panelOdbiorca.Dock = DockStyle.Top;
            panelOdbiorca.Location = new System.Drawing.Point(0, 0);
            panelOdbiorca.Name = "panelOdbiorca";
            panelOdbiorca.Size = new System.Drawing.Size(430, 95);
            panelOdbiorca.TabIndex = 1;
            // 
            // lblTytul
            // 
            lblTytul.AutoSize = true;
            lblTytul.Location = new System.Drawing.Point(10, 15);
            lblTytul.Name = "lblTytul";
            lblTytul.Size = new System.Drawing.Size(104, 15);
            lblTytul.TabIndex = 5;
            lblTytul.Text = "Nowe zamówienie";
            // 
            // cbHandlowiecFilter
            // 
            cbHandlowiecFilter.FormattingEnabled = true;
            cbHandlowiecFilter.Location = new System.Drawing.Point(10, 85);
            cbHandlowiecFilter.Name = "cbHandlowiecFilter";
            cbHandlowiecFilter.Size = new System.Drawing.Size(410, 23);
            cbHandlowiecFilter.TabIndex = 2;
            // 
            // txtSzukajOdbiorcy
            // 
            txtSzukajOdbiorcy.Location = new System.Drawing.Point(10, 140);
            txtSzukajOdbiorcy.Name = "txtSzukajOdbiorcy";
            txtSzukajOdbiorcy.PlaceholderText = "Wpisz nazwę, NIP lub miasto...";
            txtSzukajOdbiorcy.Size = new System.Drawing.Size(410, 23);
            txtSzukajOdbiorcy.TabIndex = 1;
            // 
            // panelAkcji
            // 
            panelAkcji.BackColor = System.Drawing.Color.FromArgb(249, 250, 251);
            panelAkcji.Controls.Add(btnZapisz);
            panelAkcji.Controls.Add(btnAnuluj);
            panelAkcji.Dock = DockStyle.Bottom;
            panelAkcji.Location = new System.Drawing.Point(0, 650);
            panelAkcji.Name = "panelAkcji";
            panelAkcji.Size = new System.Drawing.Size(430, 70);
            panelAkcji.TabIndex = 3;
            // 
            // btnZapisz
            // 
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
            btnAnuluj.DialogResult = DialogResult.Cancel;
            btnAnuluj.Location = new System.Drawing.Point(125, 16);
            btnAnuluj.Name = "btnAnuluj";
            btnAnuluj.Size = new System.Drawing.Size(140, 38);
            btnAnuluj.TabIndex = 1;
            btnAnuluj.Text = "Anuluj";
            btnAnuluj.UseVisualStyleBackColor = false;
            // 
            // panelDetails
            // 
            panelDetails.BackColor = System.Drawing.Color.White;
            panelDetails.Controls.Add(dataGridViewZamowienie);
            panelDetails.Controls.Add(panelDaneOdbiorcy);
            panelDetails.Dock = DockStyle.Fill;
            panelDetails.Location = new System.Drawing.Point(430, 0);
            panelDetails.Margin = new Padding(0);
            panelDetails.Name = "panelDetails";
            panelDetails.Size = new System.Drawing.Size(850, 720);
            panelDetails.TabIndex = 1;
            // 
            // dataGridViewZamowienie
            // 
            dataGridViewZamowienie.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewZamowienie.Dock = DockStyle.Fill;
            dataGridViewZamowienie.Location = new System.Drawing.Point(0, 60);
            dataGridViewZamowienie.Name = "dataGridViewZamowienie";
            dataGridViewZamowienie.Size = new System.Drawing.Size(850, 660);
            dataGridViewZamowienie.TabIndex = 1;
            // 
            // panelDaneOdbiorcy
            // 
            panelDaneOdbiorcy.Controls.Add(lblHandlowiec);
            panelDaneOdbiorcy.Controls.Add(lblAdres);
            panelDaneOdbiorcy.Controls.Add(lblNip);
            panelDaneOdbiorcy.Controls.Add(lblWybranyOdbiorca);
            panelDaneOdbiorcy.Dock = DockStyle.Top;
            panelDaneOdbiorcy.Location = new System.Drawing.Point(0, 0);
            panelDaneOdbiorcy.Name = "panelDaneOdbiorcy";
            panelDaneOdbiorcy.Size = new System.Drawing.Size(850, 60);
            panelDaneOdbiorcy.TabIndex = 4;
            panelDaneOdbiorcy.Visible = false;
            // 
            // lblHandlowiec
            // 
            lblHandlowiec.AutoSize = true;
            lblHandlowiec.Location = new System.Drawing.Point(650, 30);
            lblHandlowiec.Name = "lblHandlowiec";
            lblHandlowiec.Size = new System.Drawing.Size(55, 15);
            lblHandlowiec.TabIndex = 3;
            lblHandlowiec.Text = "Opiekun:";
            // 
            // lblAdres
            // 
            lblAdres.AutoSize = true;
            lblAdres.Location = new System.Drawing.Point(450, 30);
            lblAdres.Name = "lblAdres";
            lblAdres.Size = new System.Drawing.Size(40, 15);
            lblAdres.TabIndex = 2;
            lblAdres.Text = "Adres:";
            // 
            // lblNip
            // 
            lblNip.AutoSize = true;
            lblNip.Location = new System.Drawing.Point(300, 30);
            lblNip.Name = "lblNip";
            lblNip.Size = new System.Drawing.Size(29, 15);
            lblNip.TabIndex = 1;
            lblNip.Text = "NIP:";
            // 
            // lblWybranyOdbiorca
            // 
            lblWybranyOdbiorca.Location = new System.Drawing.Point(15, 8);
            lblWybranyOdbiorca.Name = "lblWybranyOdbiorca";
            lblWybranyOdbiorca.Size = new System.Drawing.Size(820, 20);
            lblWybranyOdbiorca.TabIndex = 0;
            lblWybranyOdbiorca.Text = "Wybrany odbiorca";
            lblWybranyOdbiorca.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // WidokZamowienia
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1280, 720);
            Controls.Add(rootLayout);
            MinimumSize = new System.Drawing.Size(1200, 680);
            Name = "WidokZamowienia";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Zamówienie mięsa";
            WindowState = FormWindowState.Maximized;
            rootLayout.ResumeLayout(false);
            panelMaster.ResumeLayout(false);
            panelDetaleZamowienia.ResumeLayout(false);
            panelDetaleZamowienia.PerformLayout();
            panelOstatniOdbiorcy.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridOstatniOdbiorcy).EndInit();
            panelOdbiorca.ResumeLayout(false);
            panelOdbiorca.PerformLayout();
            panelAkcji.ResumeLayout(false);
            panelDetails.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewZamowienie).EndInit();
            panelDaneOdbiorcy.ResumeLayout(false);
            panelDaneOdbiorcy.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.Panel panelMaster;
        private System.Windows.Forms.TextBox txtSzukajOdbiorcy;
        private System.Windows.Forms.ComboBox cbHandlowiecFilter;
        private System.Windows.Forms.Panel panelDetaleZamowienia;
        private System.Windows.Forms.DateTimePicker dateTimePickerSprzedaz;
        private System.Windows.Forms.DateTimePicker dateTimePickerGodzinaPrzyjazdu;
        private System.Windows.Forms.CheckBox chkWlasnyOdbior;
        private System.Windows.Forms.TextBox textBoxUwagi;
        private System.Windows.Forms.DataGridView dataGridViewZamowienie;
        private System.Windows.Forms.ListBox listaWynikowOdbiorcy;
        private System.Windows.Forms.Panel panelAkcji;
        private System.Windows.Forms.Button btnZapisz;
        private System.Windows.Forms.Button btnAnuluj;
        private System.Windows.Forms.Panel panelDetails;
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