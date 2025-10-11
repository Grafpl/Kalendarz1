// Plik: WidokZamowieniaPodsumowanie.Designer.cs
// WERSJA 9.7 – Oczyszczona i z oficjalnie dodanym przełącznikiem filtrów
#nullable disable

namespace Kalendarz1
{
    partial class WidokZamowieniaPodsumowanie
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panelNawigacja = new System.Windows.Forms.Panel();
            this.btnCykliczne = new System.Windows.Forms.Button();
            this.btnAnuluj = new System.Windows.Forms.Button();
            this.lblZakresDat = new System.Windows.Forms.Label();
            this.btnTydzienNext = new System.Windows.Forms.Button();
            this.btnTydzienPrev = new System.Windows.Forms.Button();
            this.btnNoweZamowienie = new System.Windows.Forms.Button();
            this.btnModyfikuj = new System.Windows.Forms.Button();
            this.btnOdswiez = new System.Windows.Forms.Button();
            this.panelDni = new System.Windows.Forms.FlowLayoutPanel();
            this.btnPon = new System.Windows.Forms.Button();
            this.btnWt = new System.Windows.Forms.Button();
            this.btnSr = new System.Windows.Forms.Button();
            this.btnCzw = new System.Windows.Forms.Button();
            this.btnPt = new System.Windows.Forms.Button();
            this.btnSo = new System.Windows.Forms.Button();
            this.btnNd = new System.Windows.Forms.Button();
            this.btnDuplikuj = new System.Windows.Forms.Button();
            this.btnUsun = new System.Windows.Forms.Button();
            this.panelGlowny = new System.Windows.Forms.TableLayoutPanel();
            this.panelMaster = new System.Windows.Forms.Panel();
            this.dgvZamowienia = new System.Windows.Forms.DataGridView();
            this.panelFiltry = new System.Windows.Forms.Panel();
            this.chkPokazWydaniaBezZamowien = new System.Windows.Forms.CheckBox();
            this.rbDataUboju = new System.Windows.Forms.RadioButton();
            this.rbDataOdbioru = new System.Windows.Forms.RadioButton();
            this.cbFiltrujTowar = new System.Windows.Forms.ComboBox();
            this.cbFiltrujHandlowca = new System.Windows.Forms.ComboBox();
            this.txtFiltrujOdbiorce = new System.Windows.Forms.TextBox();
            this.panelPodsumowanie = new System.Windows.Forms.Panel();
            this.lblPodsumowanie = new System.Windows.Forms.Label();
            this.panelDetail = new System.Windows.Forms.TableLayoutPanel();
            this.dgvAgregacja = new System.Windows.Forms.DataGridView();
            this.label2 = new System.Windows.Forms.Label();
            this.panelSzczegolyTop = new System.Windows.Forms.Panel();
            this.dgvSzczegoly = new System.Windows.Forms.DataGridView();
            this.txtNotatki = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tableLayoutPanel1.SuspendLayout();
            this.panelNawigacja.SuspendLayout();
            this.panelDni.SuspendLayout();
            this.panelGlowny.SuspendLayout();
            this.panelMaster.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvZamowienia)).BeginInit();
            this.panelFiltry.SuspendLayout();
            this.panelPodsumowanie.SuspendLayout();
            this.panelDetail.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAgregacja)).BeginInit();
            this.panelSzczegolyTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSzczegoly)).BeginInit();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.panelNawigacja, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panelGlowny, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1264, 681);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // panelNawigacja
            // 
            this.panelNawigacja.Controls.Add(this.btnCykliczne);
            this.panelNawigacja.Controls.Add(this.btnAnuluj);
            this.panelNawigacja.Controls.Add(this.lblZakresDat);
            this.panelNawigacja.Controls.Add(this.btnTydzienNext);
            this.panelNawigacja.Controls.Add(this.btnTydzienPrev);
            this.panelNawigacja.Controls.Add(this.btnNoweZamowienie);
            this.panelNawigacja.Controls.Add(this.btnModyfikuj);
            this.panelNawigacja.Controls.Add(this.btnOdswiez);
            this.panelNawigacja.Controls.Add(this.panelDni);
            this.panelNawigacja.Controls.Add(this.btnDuplikuj);
            this.panelNawigacja.Controls.Add(this.btnUsun);
            this.panelNawigacja.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelNawigacja.Location = new System.Drawing.Point(3, 3);
            this.panelNawigacja.Name = "panelNawigacja";
            this.panelNawigacja.Size = new System.Drawing.Size(1258, 64);
            this.panelNawigacja.TabIndex = 0;
            // 
            // btnCykliczne
            // 
            this.btnCykliczne.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCykliczne.BackColor = System.Drawing.Color.DarkOrange;
            this.btnCykliczne.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.btnCykliczne.ForeColor = System.Drawing.Color.White;
            this.btnCykliczne.Location = new System.Drawing.Point(556, 12);
            this.btnCykliczne.Name = "btnCykliczne";
            this.btnCykliczne.Size = new System.Drawing.Size(110, 40);
            this.btnCykliczne.TabIndex = 10;
            this.btnCykliczne.Text = "Cykliczne";
            this.btnCykliczne.UseVisualStyleBackColor = false;
            this.btnCykliczne.Click += new System.EventHandler(this.btnCykliczne_Click);
            // 
            // btnAnuluj
            // 
            this.btnAnuluj.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAnuluj.BackColor = System.Drawing.Color.IndianRed;
            this.btnAnuluj.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.btnAnuluj.ForeColor = System.Drawing.Color.White;
            this.btnAnuluj.Location = new System.Drawing.Point(1020, 12);
            this.btnAnuluj.Name = "btnAnuluj";
            this.btnAnuluj.Size = new System.Drawing.Size(110, 40);
            this.btnAnuluj.TabIndex = 7;
            this.btnAnuluj.Text = "Anuluj";
            this.btnAnuluj.UseVisualStyleBackColor = false;
            this.btnAnuluj.Click += new System.EventHandler(this.btnAnuluj_Click);
            // 
            // lblZakresDat
            // 
            this.lblZakresDat.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblZakresDat.Location = new System.Drawing.Point(82, 9);
            this.lblZakresDat.Name = "lblZakresDat";
            this.lblZakresDat.Size = new System.Drawing.Size(200, 49);
            this.lblZakresDat.TabIndex = 6;
            this.lblZakresDat.Text = "dd.MM.yyyy - dd.MM.yyyy";
            this.lblZakresDat.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnTydzienNext
            // 
            this.btnTydzienNext.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.btnTydzienNext.Location = new System.Drawing.Point(288, 12);
            this.btnTydzienNext.Name = "btnTydzienNext";
            this.btnTydzienNext.Size = new System.Drawing.Size(40, 40);
            this.btnTydzienNext.TabIndex = 5;
            this.btnTydzienNext.Text = ">";
            this.btnTydzienNext.UseVisualStyleBackColor = true;
            this.btnTydzienNext.Click += new System.EventHandler(this.btnTydzienNext_Click);
            // 
            // btnTydzienPrev
            // 
            this.btnTydzienPrev.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.btnTydzienPrev.Location = new System.Drawing.Point(36, 12);
            this.btnTydzienPrev.Name = "btnTydzienPrev";
            this.btnTydzienPrev.Size = new System.Drawing.Size(40, 40);
            this.btnTydzienPrev.TabIndex = 4;
            this.btnTydzienPrev.Text = "<";
            this.btnTydzienPrev.UseVisualStyleBackColor = true;
            this.btnTydzienPrev.Click += new System.EventHandler(this.btnTydzienPrev_Click);
            // 
            // btnNoweZamowienie
            // 
            this.btnNoweZamowienie.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnNoweZamowienie.BackColor = System.Drawing.Color.SeaGreen;
            this.btnNoweZamowienie.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.btnNoweZamowienie.ForeColor = System.Drawing.Color.White;
            this.btnNoweZamowienie.Location = new System.Drawing.Point(788, 12);
            this.btnNoweZamowienie.Name = "btnNoweZamowienie";
            this.btnNoweZamowienie.Size = new System.Drawing.Size(110, 40);
            this.btnNoweZamowienie.TabIndex = 3;
            this.btnNoweZamowienie.Text = "Nowe";
            this.btnNoweZamowienie.UseVisualStyleBackColor = false;
            this.btnNoweZamowienie.Click += new System.EventHandler(this.btnNoweZamowienie_Click);
            // 
            // btnModyfikuj
            // 
            this.btnModyfikuj.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnModyfikuj.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.btnModyfikuj.Location = new System.Drawing.Point(904, 12);
            this.btnModyfikuj.Name = "btnModyfikuj";
            this.btnModyfikuj.Size = new System.Drawing.Size(110, 40);
            this.btnModyfikuj.TabIndex = 2;
            this.btnModyfikuj.Text = "Modyfikuj";
            this.btnModyfikuj.UseVisualStyleBackColor = true;
            this.btnModyfikuj.Click += new System.EventHandler(this.btnModyfikuj_Click);
            // 
            // btnOdswiez
            // 
            this.btnOdswiez.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOdswiez.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            this.btnOdswiez.Location = new System.Drawing.Point(1136, 12);
            this.btnOdswiez.Name = "btnOdswiez";
            this.btnOdswiez.Size = new System.Drawing.Size(110, 40);
            this.btnOdswiez.TabIndex = 1;
            this.btnOdswiez.Text = "Odśwież";
            this.btnOdswiez.UseVisualStyleBackColor = true;
            this.btnOdswiez.Click += new System.EventHandler(this.btnOdswiez_Click);
            // 
            // panelDni
            // 
            this.panelDni.Controls.Add(this.btnPon);
            this.panelDni.Controls.Add(this.btnWt);
            this.panelDni.Controls.Add(this.btnSr);
            this.panelDni.Controls.Add(this.btnCzw);
            this.panelDni.Controls.Add(this.btnPt);
            this.panelDni.Controls.Add(this.btnSo);
            this.panelDni.Controls.Add(this.btnNd);
            this.panelDni.Location = new System.Drawing.Point(344, 6);
            this.panelDni.Name = "panelDni";
            this.panelDni.Size = new System.Drawing.Size(532, 52);
            this.panelDni.TabIndex = 0;
            // 
            // btnPon
            // 
            this.btnPon.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnPon.Location = new System.Drawing.Point(3, 3);
            this.btnPon.Name = "btnPon";
            this.btnPon.Size = new System.Drawing.Size(70, 45);
            this.btnPon.TabIndex = 0;
            this.btnPon.Text = "Pon";
            this.btnPon.UseVisualStyleBackColor = true;
            // 
            // btnWt
            // 
            this.btnWt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnWt.Location = new System.Drawing.Point(79, 3);
            this.btnWt.Name = "btnWt";
            this.btnWt.Size = new System.Drawing.Size(70, 45);
            this.btnWt.TabIndex = 1;
            this.btnWt.Text = "Wt";
            this.btnWt.UseVisualStyleBackColor = true;
            // 
            // btnSr
            // 
            this.btnSr.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnSr.Location = new System.Drawing.Point(155, 3);
            this.btnSr.Name = "btnSr";
            this.btnSr.Size = new System.Drawing.Size(70, 45);
            this.btnSr.TabIndex = 2;
            this.btnSr.Text = "Śr";
            this.btnSr.UseVisualStyleBackColor = true;
            // 
            // btnCzw
            // 
            this.btnCzw.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnCzw.Location = new System.Drawing.Point(231, 3);
            this.btnCzw.Name = "btnCzw";
            this.btnCzw.Size = new System.Drawing.Size(70, 45);
            this.btnCzw.TabIndex = 3;
            this.btnCzw.Text = "Czw";
            this.btnCzw.UseVisualStyleBackColor = true;
            // 
            // btnPt
            // 
            this.btnPt.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnPt.Location = new System.Drawing.Point(307, 3);
            this.btnPt.Name = "btnPt";
            this.btnPt.Size = new System.Drawing.Size(70, 45);
            this.btnPt.TabIndex = 4;
            this.btnPt.Text = "Pt";
            this.btnPt.UseVisualStyleBackColor = true;
            // 
            // btnSo
            // 
            this.btnSo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnSo.Location = new System.Drawing.Point(383, 3);
            this.btnSo.Name = "btnSo";
            this.btnSo.Size = new System.Drawing.Size(70, 45);
            this.btnSo.TabIndex = 5;
            this.btnSo.Text = "So";
            this.btnSo.UseVisualStyleBackColor = true;
            // 
            // btnNd
            // 
            this.btnNd.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnNd.Location = new System.Drawing.Point(459, 3);
            this.btnNd.Name = "btnNd";
            this.btnNd.Size = new System.Drawing.Size(70, 45);
            this.btnNd.TabIndex = 6;
            this.btnNd.Text = "Nd";
            this.btnNd.UseVisualStyleBackColor = true;
            // 
            // btnDuplikuj
            // 
            this.btnDuplikuj.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDuplikuj.BackColor = System.Drawing.Color.RoyalBlue;
            this.btnDuplikuj.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.btnDuplikuj.ForeColor = System.Drawing.Color.White;
            this.btnDuplikuj.Location = new System.Drawing.Point(672, 12);
            this.btnDuplikuj.Name = "btnDuplikuj";
            this.btnDuplikuj.Size = new System.Drawing.Size(110, 40);
            this.btnDuplikuj.TabIndex = 8;
            this.btnDuplikuj.Text = "Duplikuj";
            this.btnDuplikuj.UseVisualStyleBackColor = false;
            this.btnDuplikuj.Click += new System.EventHandler(this.btnDuplikuj_Click);
            // 
            // btnUsun
            // 
            this.btnUsun.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnUsun.BackColor = System.Drawing.Color.Black;
            this.btnUsun.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.btnUsun.ForeColor = System.Drawing.Color.White;
            this.btnUsun.Location = new System.Drawing.Point(440, 12);
            this.btnUsun.Name = "btnUsun";
            this.btnUsun.Size = new System.Drawing.Size(110, 40);
            this.btnUsun.TabIndex = 9;
            this.btnUsun.Text = "Usuń";
            this.btnUsun.UseVisualStyleBackColor = false;
            this.btnUsun.Visible = false;
            this.btnUsun.Click += new System.EventHandler(this.btnUsun_Click);
            // 
            // panelGlowny
            // 
            this.panelGlowny.ColumnCount = 2;
            this.panelGlowny.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70F));
            this.panelGlowny.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.panelGlowny.Controls.Add(this.panelMaster, 0, 0);
            this.panelGlowny.Controls.Add(this.panelDetail, 1, 0);
            this.panelGlowny.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelGlowny.Location = new System.Drawing.Point(3, 73);
            this.panelGlowny.Name = "panelGlowny";
            this.panelGlowny.RowCount = 1;
            this.panelGlowny.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelGlowny.Size = new System.Drawing.Size(1258, 605);
            this.panelGlowny.TabIndex = 1;
            // 
            // panelMaster
            // 
            this.panelMaster.Controls.Add(this.dgvZamowienia);
            this.panelMaster.Controls.Add(this.panelFiltry);
            this.panelMaster.Controls.Add(this.panelPodsumowanie);
            this.panelMaster.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMaster.Location = new System.Drawing.Point(3, 3);
            this.panelMaster.Name = "panelMaster";
            this.panelMaster.Size = new System.Drawing.Size(874, 599);
            this.panelMaster.TabIndex = 0;
            // 
            // dgvZamowienia
            // 
            this.dgvZamowienia.AllowUserToAddRows = false;
            this.dgvZamowienia.AllowUserToDeleteRows = false;
            this.dgvZamowienia.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvZamowienia.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvZamowienia.Location = new System.Drawing.Point(0, 45);
            this.dgvZamowienia.Name = "dgvZamowienia";
            this.dgvZamowienia.ReadOnly = true;
            this.dgvZamowienia.RowTemplate.Height = 25;
            this.dgvZamowienia.Size = new System.Drawing.Size(874, 514);
            this.dgvZamowienia.TabIndex = 1;
            // 
            // panelFiltry
            // 
            this.panelFiltry.Controls.Add(this.chkPokazWydaniaBezZamowien);
            this.panelFiltry.Controls.Add(this.rbDataUboju);
            this.panelFiltry.Controls.Add(this.rbDataOdbioru);
            this.panelFiltry.Controls.Add(this.cbFiltrujTowar);
            this.panelFiltry.Controls.Add(this.cbFiltrujHandlowca);
            this.panelFiltry.Controls.Add(this.txtFiltrujOdbiorce);
            this.panelFiltry.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelFiltry.Location = new System.Drawing.Point(0, 0);
            this.panelFiltry.Name = "panelFiltry";
            this.panelFiltry.Padding = new System.Windows.Forms.Padding(5);
            this.panelFiltry.Size = new System.Drawing.Size(874, 45);
            this.panelFiltry.TabIndex = 2;
            // 
            // chkPokazWydaniaBezZamowien
            // 
            this.chkPokazWydaniaBezZamowien.AutoSize = true;
            this.chkPokazWydaniaBezZamowien.Location = new System.Drawing.Point(515, 13);
            this.chkPokazWydaniaBezZamowien.Name = "chkPokazWydaniaBezZamowien";
            this.chkPokazWydaniaBezZamowien.Size = new System.Drawing.Size(158, 19);
            this.chkPokazWydaniaBezZamowien.TabIndex = 5;
            this.chkPokazWydaniaBezZamowien.Text = "Pokaż wydania bez zam.";
            this.chkPokazWydaniaBezZamowien.UseVisualStyleBackColor = true;
            this.chkPokazWydaniaBezZamowien.CheckedChanged += new System.EventHandler(this.ChkPokazWydaniaBezZamowien_CheckedChanged);
            // 
            // rbDataUboju
            // 
            this.rbDataUboju.AutoSize = true;
            this.rbDataUboju.Location = new System.Drawing.Point(784, 13);
            this.rbDataUboju.Name = "rbDataUboju";
            this.rbDataUboju.Size = new System.Drawing.Size(85, 19);
            this.rbDataUboju.TabIndex = 4;
            this.rbDataUboju.Text = "Data uboju";
            this.rbDataUboju.UseVisualStyleBackColor = true;
            // 
            // rbDataOdbioru
            // 
            this.rbDataOdbioru.AutoSize = true;
            this.rbDataOdbioru.Checked = true;
            this.rbDataOdbioru.Location = new System.Drawing.Point(679, 13);
            this.rbDataOdbioru.Name = "rbDataOdbioru";
            this.rbDataOdbioru.Size = new System.Drawing.Size(95, 19);
            this.rbDataOdbioru.TabIndex = 3;
            this.rbDataOdbioru.TabStop = true;
            this.rbDataOdbioru.Text = "Data odbioru";
            this.rbDataOdbioru.UseVisualStyleBackColor = true;
            // 
            // cbFiltrujTowar
            // 
            this.cbFiltrujTowar.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFiltrujTowar.FormattingEnabled = true;
            this.cbFiltrujTowar.Location = new System.Drawing.Point(344, 11);
            this.cbFiltrujTowar.Name = "cbFiltrujTowar";
            this.cbFiltrujTowar.Size = new System.Drawing.Size(165, 23);
            this.cbFiltrujTowar.TabIndex = 2;
            // 
            // cbFiltrujHandlowca
            // 
            this.cbFiltrujHandlowca.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbFiltrujHandlowca.FormattingEnabled = true;
            this.cbFiltrujHandlowca.Location = new System.Drawing.Point(188, 11);
            this.cbFiltrujHandlowca.Name = "cbFiltrujHandlowca";
            this.cbFiltrujHandlowca.Size = new System.Drawing.Size(150, 23);
            this.cbFiltrujHandlowca.TabIndex = 1;
            // 
            // txtFiltrujOdbiorce
            // 
            this.txtFiltrujOdbiorce.Location = new System.Drawing.Point(12, 11);
            this.txtFiltrujOdbiorce.Name = "txtFiltrujOdbiorce";
            this.txtFiltrujOdbiorce.PlaceholderText = "Filtruj odbiorcę...";
            this.txtFiltrujOdbiorce.Size = new System.Drawing.Size(170, 23);
            this.txtFiltrujOdbiorce.TabIndex = 0;
            // 
            // panelPodsumowanie
            // 
            this.panelPodsumowanie.BackColor = System.Drawing.SystemColors.Control;
            this.panelPodsumowanie.Controls.Add(this.lblPodsumowanie);
            this.panelPodsumowanie.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelPodsumowanie.Location = new System.Drawing.Point(0, 559);
            this.panelPodsumowanie.Name = "panelPodsumowanie";
            this.panelPodsumowanie.Size = new System.Drawing.Size(874, 40);
            this.panelPodsumowanie.TabIndex = 0;
            // 
            // lblPodsumowanie
            // 
            this.lblPodsumowanie.AutoEllipsis = true;
            this.lblPodsumowanie.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPodsumowanie.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblPodsumowanie.Location = new System.Drawing.Point(0, 0);
            this.lblPodsumowanie.Name = "lblPodsumowanie";
            this.lblPodsumowanie.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
            this.lblPodsumowanie.Size = new System.Drawing.Size(874, 40);
            this.lblPodsumowanie.TabIndex = 0;
            this.lblPodsumowanie.Text = "-";
            this.lblPodsumowanie.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelDetail
            // 
            this.panelDetail.ColumnCount = 1;
            this.panelDetail.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.panelDetail.Controls.Add(this.dgvAgregacja, 0, 2);
            this.panelDetail.Controls.Add(this.label2, 0, 1);
            this.panelDetail.Controls.Add(this.panelSzczegolyTop, 0, 0);
            this.panelDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelDetail.Location = new System.Drawing.Point(883, 3);
            this.panelDetail.Name = "panelDetail";
            this.panelDetail.RowCount = 3;
            this.panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.panelDetail.Size = new System.Drawing.Size(372, 599);
            this.panelDetail.TabIndex = 1;
            // 
            // dgvAgregacja
            // 
            this.dgvAgregacja.AllowUserToAddRows = false;
            this.dgvAgregacja.AllowUserToDeleteRows = false;
            this.dgvAgregacja.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAgregacja.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvAgregacja.Location = new System.Drawing.Point(3, 260);
            this.dgvAgregacja.Name = "dgvAgregacja";
            this.dgvAgregacja.ReadOnly = true;
            this.dgvAgregacja.RowTemplate.Height = 25;
            this.dgvAgregacja.Size = new System.Drawing.Size(366, 336);
            this.dgvAgregacja.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.label2.Location = new System.Drawing.Point(3, 227);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(366, 30);
            this.label2.TabIndex = 3;
            this.label2.Text = "Podsumowanie produktów dla wybranego dnia";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelSzczegolyTop
            // 
            this.panelSzczegolyTop.Controls.Add(this.dgvSzczegoly);
            this.panelSzczegolyTop.Controls.Add(this.txtNotatki);
            this.panelSzczegolyTop.Controls.Add(this.label3);
            this.panelSzczegolyTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelSzczegolyTop.Location = new System.Drawing.Point(3, 3);
            this.panelSzczegolyTop.Name = "panelSzczegolyTop";
            this.panelSzczegolyTop.Size = new System.Drawing.Size(366, 221);
            this.panelSzczegolyTop.TabIndex = 4;
            // 
            // dgvSzczegoly
            // 
            this.dgvSzczegoly.AllowUserToAddRows = false;
            this.dgvSzczegoly.AllowUserToDeleteRows = false;
            this.dgvSzczegoly.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSzczegoly.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvSzczegoly.Location = new System.Drawing.Point(0, 23);
            this.dgvSzczegoly.Name = "dgvSzczegoly";
            this.dgvSzczegoly.ReadOnly = true;
            this.dgvSzczegoly.RowTemplate.Height = 25;
            this.dgvSzczegoly.Size = new System.Drawing.Size(366, 98);
            this.dgvSzczegoly.TabIndex = 2;
            // 
            // txtNotatki
            // 
            this.txtNotatki.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtNotatki.Location = new System.Drawing.Point(0, 121);
            this.txtNotatki.Multiline = true;
            this.txtNotatki.Name = "txtNotatki";
            this.txtNotatki.ReadOnly = true;
            this.txtNotatki.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtNotatki.Size = new System.Drawing.Size(366, 100);
            this.txtNotatki.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Top;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            this.label3.Location = new System.Drawing.Point(0, 0);
            this.label3.Name = "label3";
            this.label3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.label3.Size = new System.Drawing.Size(208, 23);
            this.label3.TabIndex = 0;
            this.label3.Text = "Szczegóły / Notatki zamówienia:";
            // 
            // WidokZamowieniaPodsumowanie
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 681);
            this.Controls.Add(this.tableLayoutPanel1);
            this.MinimumSize = new System.Drawing.Size(1280, 720);
            this.Name = "WidokZamowieniaPodsumowanie";
            this.Text = "Podsumowanie Tygodniowe Zamówień";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panelNawigacja.ResumeLayout(false);
            this.panelDni.ResumeLayout(false);
            this.panelGlowny.ResumeLayout(false);
            this.panelMaster.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvZamowienia)).EndInit();
            this.panelFiltry.ResumeLayout(false);
            this.panelFiltry.PerformLayout();
            this.panelPodsumowanie.ResumeLayout(false);
            this.panelDetail.ResumeLayout(false);
            this.panelDetail.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAgregacja)).EndInit();
            this.panelSzczegolyTop.ResumeLayout(false);
            this.panelSzczegolyTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSzczegoly)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panelNawigacja;
        private System.Windows.Forms.FlowLayoutPanel panelDni;
        private System.Windows.Forms.Button btnPon;
        private System.Windows.Forms.Button btnWt;
        private System.Windows.Forms.Button btnSr;
        private System.Windows.Forms.Button btnCzw;
        private System.Windows.Forms.Button btnPt;
        private System.Windows.Forms.Button btnOdswiez;
        private System.Windows.Forms.Button btnNoweZamowienie;
        private System.Windows.Forms.Button btnModyfikuj;
        private System.Windows.Forms.TableLayoutPanel panelGlowny;
        private System.Windows.Forms.Panel panelMaster;
        private System.Windows.Forms.DataGridView dgvZamowienia;
        private System.Windows.Forms.Panel panelPodsumowanie;
        private System.Windows.Forms.Label lblPodsumowanie;
        private System.Windows.Forms.TableLayoutPanel panelDetail;
        private System.Windows.Forms.DataGridView dgvSzczegoly;
        private System.Windows.Forms.DataGridView dgvAgregacja;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel panelFiltry;
        private System.Windows.Forms.ComboBox cbFiltrujHandlowca;
        private System.Windows.Forms.TextBox txtFiltrujOdbiorce;
        private System.Windows.Forms.Panel panelSzczegolyTop;
        private System.Windows.Forms.TextBox txtNotatki;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnSo;
        private System.Windows.Forms.Button btnNd;
        private System.Windows.Forms.Button btnTydzienNext;
        private System.Windows.Forms.Button btnTydzienPrev;
        private System.Windows.Forms.Label lblZakresDat;
        private System.Windows.Forms.Button btnAnuluj;
        private System.Windows.Forms.ComboBox cbFiltrujTowar;
        private System.Windows.Forms.Button btnDuplikuj;
        private System.Windows.Forms.Button btnUsun;
        private System.Windows.Forms.Button btnCykliczne;
        private System.Windows.Forms.RadioButton rbDataOdbioru;
        private System.Windows.Forms.RadioButton rbDataUboju;
        private System.Windows.Forms.CheckBox chkPokazWydaniaBezZamowien;
    }
}