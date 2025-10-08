// Plik: WidokZamowieniaPodsumowanie.Designer.cs
#nullable disable
namespace Kalendarz1
{
    partial class WidokZamowieniaPodsumowanie
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
            tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            panelNawigacja = new System.Windows.Forms.Panel();
            btnAnuluj = new System.Windows.Forms.Button();
            lblZakresDat = new System.Windows.Forms.Label();
            btnTydzienNext = new System.Windows.Forms.Button();
            btnTydzienPrev = new System.Windows.Forms.Button();
            btnNoweZamowienie = new System.Windows.Forms.Button();
            btnModyfikuj = new System.Windows.Forms.Button();
            btnOdswiez = new System.Windows.Forms.Button();
            panelDni = new System.Windows.Forms.FlowLayoutPanel();
            btnPon = new System.Windows.Forms.Button();
            btnWt = new System.Windows.Forms.Button();
            btnSr = new System.Windows.Forms.Button();
            btnCzw = new System.Windows.Forms.Button();
            btnPt = new System.Windows.Forms.Button();
            btnSo = new System.Windows.Forms.Button();
            btnNd = new System.Windows.Forms.Button();
            btnDuplikuj = new System.Windows.Forms.Button();
            btnUsun = new System.Windows.Forms.Button();
            panelGlowny = new System.Windows.Forms.TableLayoutPanel();
            panelMaster = new System.Windows.Forms.Panel();
            dgvZamowienia = new System.Windows.Forms.DataGridView();
            panelFiltry = new System.Windows.Forms.Panel();
            cbFiltrujTowar = new System.Windows.Forms.ComboBox();
            cbFiltrujHandlowca = new System.Windows.Forms.ComboBox();
            txtFiltrujOdbiorce = new System.Windows.Forms.TextBox();
            rbDataOdbioru = new System.Windows.Forms.RadioButton();
            rbDataUboju = new System.Windows.Forms.RadioButton();
            panelPodsumowanie = new System.Windows.Forms.Panel();
            lblPodsumowanie = new System.Windows.Forms.Label();
            panelDetail = new System.Windows.Forms.TableLayoutPanel();
            dgvAgregacja = new System.Windows.Forms.DataGridView();
            label2 = new System.Windows.Forms.Label();
            panelSzczegolyTop = new System.Windows.Forms.TableLayoutPanel();
            panelNotatki = new System.Windows.Forms.Panel();
            dgvSzczegoly = new System.Windows.Forms.DataGridView();
            txtNotatki = new System.Windows.Forms.TextBox();
            label3 = new System.Windows.Forms.Label();
            panelPrzychody = new System.Windows.Forms.Panel();
            dgvPrzychody = new System.Windows.Forms.DataGridView();
            label1 = new System.Windows.Forms.Label();
            tableLayoutPanel1.SuspendLayout();
            panelNawigacja.SuspendLayout();
            panelDni.SuspendLayout();
            panelGlowny.SuspendLayout();
            panelMaster.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvZamowienia).BeginInit();
            panelFiltry.SuspendLayout();
            panelPodsumowanie.SuspendLayout();
            panelDetail.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvAgregacja).BeginInit();
            panelSzczegolyTop.SuspendLayout();
            panelNotatki.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSzczegoly).BeginInit();
            panelPrzychody.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPrzychody).BeginInit();
            SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(panelNawigacja, 0, 0);
            tableLayoutPanel1.Controls.Add(panelGlowny, 0, 1);
            tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            tableLayoutPanel1.Size = new System.Drawing.Size(1264, 681);
            tableLayoutPanel1.TabIndex = 0;
            // 
            // panelNawigacja
            // 
            panelNawigacja.Controls.Add(btnAnuluj);
            panelNawigacja.Controls.Add(lblZakresDat);
            panelNawigacja.Controls.Add(btnTydzienNext);
            panelNawigacja.Controls.Add(btnTydzienPrev);
            panelNawigacja.Controls.Add(btnNoweZamowienie);
            panelNawigacja.Controls.Add(btnModyfikuj);
            panelNawigacja.Controls.Add(btnOdswiez);
            panelNawigacja.Controls.Add(panelDni);
            panelNawigacja.Controls.Add(btnDuplikuj);
            panelNawigacja.Controls.Add(btnUsun);
            panelNawigacja.Dock = System.Windows.Forms.DockStyle.Fill;
            panelNawigacja.Location = new System.Drawing.Point(3, 3);
            panelNawigacja.Name = "panelNawigacja";
            panelNawigacja.Size = new System.Drawing.Size(1258, 64);
            panelNawigacja.TabIndex = 0;
            // 
            // btnAnuluj
            // 
            btnAnuluj.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnAnuluj.BackColor = System.Drawing.Color.IndianRed;
            btnAnuluj.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            btnAnuluj.ForeColor = System.Drawing.Color.White;
            btnAnuluj.Location = new System.Drawing.Point(1020, 12);
            btnAnuluj.Name = "btnAnuluj";
            btnAnuluj.Size = new System.Drawing.Size(110, 40);
            btnAnuluj.TabIndex = 7;
            btnAnuluj.Text = "Anuluj";
            btnAnuluj.UseVisualStyleBackColor = false;
            btnAnuluj.Click += btnAnuluj_Click;
            // 
            // lblZakresDat
            // 
            lblZakresDat.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            lblZakresDat.Location = new System.Drawing.Point(82, 9);
            lblZakresDat.Name = "lblZakresDat";
            lblZakresDat.Size = new System.Drawing.Size(200, 49);
            lblZakresDat.TabIndex = 6;
            lblZakresDat.Text = "dd.MM.yyyy - dd.MM.yyyy";
            lblZakresDat.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnTydzienNext
            // 
            btnTydzienNext.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            btnTydzienNext.Location = new System.Drawing.Point(288, 12);
            btnTydzienNext.Name = "btnTydzienNext";
            btnTydzienNext.Size = new System.Drawing.Size(40, 40);
            btnTydzienNext.TabIndex = 5;
            btnTydzienNext.Text = ">";
            btnTydzienNext.UseVisualStyleBackColor = true;
            btnTydzienNext.Click += btnTydzienNext_Click;
            // 
            // btnTydzienPrev
            // 
            btnTydzienPrev.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            btnTydzienPrev.Location = new System.Drawing.Point(36, 12);
            btnTydzienPrev.Name = "btnTydzienPrev";
            btnTydzienPrev.Size = new System.Drawing.Size(40, 40);
            btnTydzienPrev.TabIndex = 4;
            btnTydzienPrev.Text = "<";
            btnTydzienPrev.UseVisualStyleBackColor = true;
            btnTydzienPrev.Click += btnTydzienPrev_Click;
            // 
            // btnNoweZamowienie
            // 
            btnNoweZamowienie.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnNoweZamowienie.BackColor = System.Drawing.Color.SeaGreen;
            btnNoweZamowienie.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            btnNoweZamowienie.ForeColor = System.Drawing.Color.White;
            btnNoweZamowienie.Location = new System.Drawing.Point(788, 12);
            btnNoweZamowienie.Name = "btnNoweZamowienie";
            btnNoweZamowienie.Size = new System.Drawing.Size(110, 40);
            btnNoweZamowienie.TabIndex = 3;
            btnNoweZamowienie.Text = "Nowe";
            btnNoweZamowienie.UseVisualStyleBackColor = false;
            btnNoweZamowienie.Click += btnNoweZamowienie_Click;
            // 
            // btnModyfikuj
            // 
            btnModyfikuj.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnModyfikuj.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            btnModyfikuj.Location = new System.Drawing.Point(904, 12);
            btnModyfikuj.Name = "btnModyfikuj";
            btnModyfikuj.Size = new System.Drawing.Size(110, 40);
            btnModyfikuj.TabIndex = 2;
            btnModyfikuj.Text = "Modyfikuj";
            btnModyfikuj.UseVisualStyleBackColor = true;
            btnModyfikuj.Click += btnModyfikuj_Click;
            // 
            // btnOdswiez
            // 
            btnOdswiez.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnOdswiez.Font = new System.Drawing.Font("Segoe UI", 9.75F);
            btnOdswiez.Location = new System.Drawing.Point(1136, 12);
            btnOdswiez.Name = "btnOdswiez";
            btnOdswiez.Size = new System.Drawing.Size(110, 40);
            btnOdswiez.TabIndex = 1;
            btnOdswiez.Text = "Odśwież";
            btnOdswiez.UseVisualStyleBackColor = true;
            btnOdswiez.Click += btnOdswiez_Click;
            // 
            // panelDni
            // 
            panelDni.Controls.Add(btnPon);
            panelDni.Controls.Add(btnWt);
            panelDni.Controls.Add(btnSr);
            panelDni.Controls.Add(btnCzw);
            panelDni.Controls.Add(btnPt);
            panelDni.Controls.Add(btnSo);
            panelDni.Controls.Add(btnNd);
            panelDni.Location = new System.Drawing.Point(344, 6);
            panelDni.Name = "panelDni";
            panelDni.Size = new System.Drawing.Size(670, 52);
            panelDni.TabIndex = 0;
            // 
            // btnPon
            // 
            btnPon.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnPon.Location = new System.Drawing.Point(3, 3);
            btnPon.Name = "btnPon";
            btnPon.Size = new System.Drawing.Size(70, 45);
            btnPon.TabIndex = 0;
            btnPon.Text = "Pon";
            btnPon.UseVisualStyleBackColor = true;
            // 
            // btnWt
            // 
            btnWt.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnWt.Location = new System.Drawing.Point(79, 3);
            btnWt.Name = "btnWt";
            btnWt.Size = new System.Drawing.Size(70, 45);
            btnWt.TabIndex = 1;
            btnWt.Text = "Wt";
            btnWt.UseVisualStyleBackColor = true;
            // 
            // btnSr
            // 
            btnSr.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnSr.Location = new System.Drawing.Point(155, 3);
            btnSr.Name = "btnSr";
            btnSr.Size = new System.Drawing.Size(70, 45);
            btnSr.TabIndex = 2;
            btnSr.Text = "Śr";
            btnSr.UseVisualStyleBackColor = true;
            // 
            // btnCzw
            // 
            btnCzw.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnCzw.Location = new System.Drawing.Point(231, 3);
            btnCzw.Name = "btnCzw";
            btnCzw.Size = new System.Drawing.Size(70, 45);
            btnCzw.TabIndex = 3;
            btnCzw.Text = "Czw";
            btnCzw.UseVisualStyleBackColor = true;
            // 
            // btnPt
            // 
            btnPt.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnPt.Location = new System.Drawing.Point(307, 3);
            btnPt.Name = "btnPt";
            btnPt.Size = new System.Drawing.Size(70, 45);
            btnPt.TabIndex = 4;
            btnPt.Text = "Pt";
            btnPt.UseVisualStyleBackColor = true;
            // 
            // btnSo
            // 
            btnSo.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnSo.Location = new System.Drawing.Point(383, 3);
            btnSo.Name = "btnSo";
            btnSo.Size = new System.Drawing.Size(70, 45);
            btnSo.TabIndex = 5;
            btnSo.Text = "So";
            btnSo.UseVisualStyleBackColor = true;
            // 
            // btnNd
            // 
            btnNd.Font = new System.Drawing.Font("Segoe UI", 9F);
            btnNd.Location = new System.Drawing.Point(459, 3);
            btnNd.Name = "btnNd";
            btnNd.Size = new System.Drawing.Size(70, 45);
            btnNd.TabIndex = 6;
            btnNd.Text = "Nd";
            btnNd.UseVisualStyleBackColor = true;
            // 
            // btnDuplikuj
            // 
            btnDuplikuj.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnDuplikuj.BackColor = System.Drawing.Color.RoyalBlue;
            btnDuplikuj.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            btnDuplikuj.ForeColor = System.Drawing.Color.White;
            btnDuplikuj.Location = new System.Drawing.Point(672, 12);
            btnDuplikuj.Name = "btnDuplikuj";
            btnDuplikuj.Size = new System.Drawing.Size(110, 40);
            btnDuplikuj.TabIndex = 8;
            btnDuplikuj.Text = "Duplikuj";
            btnDuplikuj.UseVisualStyleBackColor = false;
            btnDuplikuj.Click += btnDuplikuj_Click;
            // 
            // btnUsun
            // 
            btnUsun.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnUsun.BackColor = System.Drawing.Color.Black;
            btnUsun.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            btnUsun.ForeColor = System.Drawing.Color.White;
            btnUsun.Location = new System.Drawing.Point(560, 12);
            btnUsun.Name = "btnUsun";
            btnUsun.Size = new System.Drawing.Size(110, 40);
            btnUsun.TabIndex = 9;
            btnUsun.Text = "Usuń";
            btnUsun.UseVisualStyleBackColor = false;
            btnUsun.Visible = false;
            btnUsun.Click += btnUsun_Click;
            // 
            // panelGlowny
            // 
            panelGlowny.ColumnCount = 2;
            panelGlowny.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 70F));
            panelGlowny.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            panelGlowny.Controls.Add(panelMaster, 0, 0);
            panelGlowny.Controls.Add(panelDetail, 1, 0);
            panelGlowny.Dock = System.Windows.Forms.DockStyle.Fill;
            panelGlowny.Location = new System.Drawing.Point(3, 73);
            panelGlowny.Name = "panelGlowny";
            panelGlowny.RowCount = 1;
            panelGlowny.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelGlowny.Size = new System.Drawing.Size(1258, 605);
            panelGlowny.TabIndex = 1;
            // 
            // panelMaster
            // 
            panelMaster.Controls.Add(dgvZamowienia);
            panelMaster.Controls.Add(panelFiltry);
            panelMaster.Controls.Add(panelPodsumowanie);
            panelMaster.Dock = System.Windows.Forms.DockStyle.Fill;
            panelMaster.Location = new System.Drawing.Point(3, 3);
            panelMaster.Name = "panelMaster";
            panelMaster.Size = new System.Drawing.Size(874, 599);
            panelMaster.TabIndex = 0;
            // 
            // dgvZamowienia
            // 
            dgvZamowienia.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvZamowienia.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvZamowienia.Location = new System.Drawing.Point(0, 45);
            dgvZamowienia.Name = "dgvZamowienia";
            dgvZamowienia.Size = new System.Drawing.Size(874, 514);
            dgvZamowienia.TabIndex = 1;
            dgvZamowienia.CellClick += dgvZamowienia_CellClick;
            dgvZamowienia.RowPrePaint += dgvZamowienia_RowPrePaint;
            dgvZamowienia.SelectionChanged += dgvZamowienia_SelectionChanged;
            // 
            // panelFiltry
            // 
            panelFiltry.Controls.Add(cbFiltrujTowar);
            panelFiltry.Controls.Add(cbFiltrujHandlowca);
            panelFiltry.Controls.Add(txtFiltrujOdbiorce);
            panelFiltry.Controls.Add(rbDataOdbioru);
            panelFiltry.Controls.Add(rbDataUboju);
            panelFiltry.Dock = System.Windows.Forms.DockStyle.Top;
            panelFiltry.Location = new System.Drawing.Point(0, 0);
            panelFiltry.Name = "panelFiltry";
            panelFiltry.Padding = new System.Windows.Forms.Padding(5);
            panelFiltry.Size = new System.Drawing.Size(874, 45);
            panelFiltry.TabIndex = 2;
            // 
            // cbFiltrujTowar
            // 
            cbFiltrujTowar.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cbFiltrujTowar.FormattingEnabled = true;
            cbFiltrujTowar.Location = new System.Drawing.Point(453, 11);
            cbFiltrujTowar.Name = "cbFiltrujTowar";
            cbFiltrujTowar.Size = new System.Drawing.Size(220, 23);
            cbFiltrujTowar.TabIndex = 2;
            // 
            // cbFiltrujHandlowca
            // 
            cbFiltrujHandlowca.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cbFiltrujHandlowca.FormattingEnabled = true;
            cbFiltrujHandlowca.Location = new System.Drawing.Point(267, 11);
            cbFiltrujHandlowca.Name = "cbFiltrujHandlowca";
            cbFiltrujHandlowca.Size = new System.Drawing.Size(180, 23);
            cbFiltrujHandlowca.TabIndex = 1;
            // 
            // txtFiltrujOdbiorce
            // 
            txtFiltrujOdbiorce.Location = new System.Drawing.Point(12, 11);
            txtFiltrujOdbiorce.Name = "txtFiltrujOdbiorce";
            txtFiltrujOdbiorce.PlaceholderText = "Filtruj po nazwie odbiorcy...";
            txtFiltrujOdbiorce.Size = new System.Drawing.Size(249, 23);
            txtFiltrujOdbiorce.TabIndex = 0;
            // 
            // rbDataOdbioru
            // 
            rbDataOdbioru.AutoSize = true;
            rbDataOdbioru.Checked = true;
            rbDataOdbioru.Location = new System.Drawing.Point(690, 13);
            rbDataOdbioru.Name = "rbDataOdbioru";
            rbDataOdbioru.Size = new System.Drawing.Size(95, 19);
            rbDataOdbioru.TabIndex = 3;
            rbDataOdbioru.TabStop = true;
            rbDataOdbioru.Text = "Data odbioru";
            rbDataOdbioru.UseVisualStyleBackColor = true;
            // 
            // rbDataUboju
            // 
            rbDataUboju.AutoSize = true;
            rbDataUboju.Location = new System.Drawing.Point(790, 13);
            rbDataUboju.Name = "rbDataUboju";
            rbDataUboju.Size = new System.Drawing.Size(85, 19);
            rbDataUboju.TabIndex = 4;
            rbDataUboju.Text = "Data uboju";
            rbDataUboju.UseVisualStyleBackColor = true;
            // 
            // panelPodsumowanie
            // 
            panelPodsumowanie.BackColor = System.Drawing.SystemColors.Control;
            panelPodsumowanie.Controls.Add(lblPodsumowanie);
            panelPodsumowanie.Dock = System.Windows.Forms.DockStyle.Bottom;
            panelPodsumowanie.Location = new System.Drawing.Point(0, 559);
            panelPodsumowanie.Name = "panelPodsumowanie";
            panelPodsumowanie.Size = new System.Drawing.Size(874, 40);
            panelPodsumowanie.TabIndex = 0;
            // 
            // lblPodsumowanie
            // 
            lblPodsumowanie.AutoEllipsis = true;
            lblPodsumowanie.Dock = System.Windows.Forms.DockStyle.Fill;
            lblPodsumowanie.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            lblPodsumowanie.Location = new System.Drawing.Point(0, 0);
            lblPodsumowanie.Name = "lblPodsumowanie";
            lblPodsumowanie.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
            lblPodsumowanie.Size = new System.Drawing.Size(874, 40);
            lblPodsumowanie.TabIndex = 0;
            lblPodsumowanie.Text = "-";
            lblPodsumowanie.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelDetail
            // 
            panelDetail.ColumnCount = 1;
            panelDetail.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelDetail.Controls.Add(dgvAgregacja, 0, 2);
            panelDetail.Controls.Add(label2, 0, 1);
            panelDetail.Controls.Add(panelSzczegolyTop, 0, 0);
            panelDetail.Dock = System.Windows.Forms.DockStyle.Fill;
            panelDetail.Location = new System.Drawing.Point(883, 3);
            panelDetail.Name = "panelDetail";
            panelDetail.RowCount = 3;
            panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            panelDetail.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            panelDetail.Size = new System.Drawing.Size(372, 599);
            panelDetail.TabIndex = 1;
            // 
            // dgvAgregacja
            // 
            dgvAgregacja.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAgregacja.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvAgregacja.Location = new System.Drawing.Point(3, 260);
            dgvAgregacja.Name = "dgvAgregacja";
            dgvAgregacja.Size = new System.Drawing.Size(366, 336);
            dgvAgregacja.TabIndex = 1;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Dock = System.Windows.Forms.DockStyle.Fill;
            label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            label2.Location = new System.Drawing.Point(3, 227);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(366, 30);
            label2.TabIndex = 3;
            label2.Text = "Podsumowanie produktów dla wybranego dnia";
            label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelSzczegolyTop
            // 
            panelSzczegolyTop.ColumnCount = 2;
            panelSzczegolyTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            panelSzczegolyTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            panelSzczegolyTop.Controls.Add(panelNotatki, 0, 0);
            panelSzczegolyTop.Controls.Add(panelPrzychody, 1, 0);
            panelSzczegolyTop.Dock = System.Windows.Forms.DockStyle.Fill;
            panelSzczegolyTop.Location = new System.Drawing.Point(3, 3);
            panelSzczegolyTop.Name = "panelSzczegolyTop";
            panelSzczegolyTop.RowCount = 1;
            panelSzczegolyTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelSzczegolyTop.Size = new System.Drawing.Size(366, 221);
            panelSzczegolyTop.TabIndex = 4;
            // 
            // panelNotatki
            // 
            panelNotatki.Controls.Add(dgvSzczegoly);
            panelNotatki.Controls.Add(txtNotatki);
            panelNotatki.Controls.Add(label3);
            panelNotatki.Dock = System.Windows.Forms.DockStyle.Fill;
            panelNotatki.Location = new System.Drawing.Point(3, 3);
            panelNotatki.Name = "panelNotatki";
            panelNotatki.Size = new System.Drawing.Size(177, 215);
            panelNotatki.TabIndex = 0;
            // 
            // dgvSzczegoly
            // 
            dgvSzczegoly.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvSzczegoly.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvSzczegoly.Location = new System.Drawing.Point(0, 23);
            dgvSzczegoly.Name = "dgvSzczegoly";
            dgvSzczegoly.Size = new System.Drawing.Size(177, 92);
            dgvSzczegoly.TabIndex = 2;
            // 
            // txtNotatki
            // 
            txtNotatki.Dock = System.Windows.Forms.DockStyle.Bottom;
            txtNotatki.Location = new System.Drawing.Point(0, 115);
            txtNotatki.Multiline = true;
            txtNotatki.Name = "txtNotatki";
            txtNotatki.ReadOnly = true;
            txtNotatki.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtNotatki.Size = new System.Drawing.Size(177, 100);
            txtNotatki.TabIndex = 1;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Dock = System.Windows.Forms.DockStyle.Top;
            label3.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            label3.Location = new System.Drawing.Point(0, 0);
            label3.Name = "label3";
            label3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            label3.Size = new System.Drawing.Size(208, 23);
            label3.TabIndex = 0;
            label3.Text = "Szczegóły / Notatki zamówienia:";
            // 
            // panelPrzychody
            // 
            panelPrzychody.Controls.Add(dgvPrzychody);
            panelPrzychody.Controls.Add(label1);
            panelPrzychody.Dock = System.Windows.Forms.DockStyle.Fill;
            panelPrzychody.Location = new System.Drawing.Point(186, 3);
            panelPrzychody.Name = "panelPrzychody";
            panelPrzychody.Size = new System.Drawing.Size(177, 215);
            panelPrzychody.TabIndex = 1;
            // 
            // dgvPrzychody
            // 
            dgvPrzychody.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPrzychody.Dock = System.Windows.Forms.DockStyle.Fill;
            dgvPrzychody.Location = new System.Drawing.Point(0, 23);
            dgvPrzychody.Name = "dgvPrzychody";
            dgvPrzychody.Size = new System.Drawing.Size(177, 192);
            dgvPrzychody.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Dock = System.Windows.Forms.DockStyle.Top;
            label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            label1.Location = new System.Drawing.Point(0, 0);
            label1.Name = "label1";
            label1.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            label1.Size = new System.Drawing.Size(122, 23);
            label1.TabIndex = 0;
            label1.Text = "Przychody towaru:";
            // 
            // WidokZamowieniaPodsumowanie
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1264, 681);
            Controls.Add(tableLayoutPanel1);
            MinimumSize = new System.Drawing.Size(1280, 720);
            Name = "WidokZamowieniaPodsumowanie";
            Text = "Podsumowanie Tygodniowe Zamówień";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
            tableLayoutPanel1.ResumeLayout(false);
            panelNawigacja.ResumeLayout(false);
            panelDni.ResumeLayout(false);
            panelGlowny.ResumeLayout(false);
            panelMaster.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvZamowienia).EndInit();
            panelFiltry.ResumeLayout(false);
            panelFiltry.PerformLayout();
            panelPodsumowanie.ResumeLayout(false);
            panelDetail.ResumeLayout(false);
            panelDetail.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvAgregacja).EndInit();
            panelSzczegolyTop.ResumeLayout(false);
            panelNotatki.ResumeLayout(false);
            panelNotatki.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvSzczegoly).EndInit();
            panelPrzychody.ResumeLayout(false);
            panelPrzychody.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dgvPrzychody).EndInit();
            ResumeLayout(false);
            // btnCykliczne
            // 
            btnCykliczne = new System.Windows.Forms.Button();
            btnCykliczne.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnCykliczne.BackColor = System.Drawing.Color.DarkOrange;
            btnCykliczne.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold);
            btnCykliczne.ForeColor = System.Drawing.Color.White;
            btnCykliczne.Location = new System.Drawing.Point(556, 12);
            btnCykliczne.Name = "btnCykliczne";
            btnCykliczne.Size = new System.Drawing.Size(110, 40);
            btnCykliczne.TabIndex = 10;
            btnCykliczne.Text = "Cykliczne";
            btnCykliczne.UseVisualStyleBackColor = false;
            btnCykliczne.Click += btnCykliczne_Click;

            // Dodaj do panelu nawigacyjnego (po btnDuplikuj)
            panelNawigacja.Controls.Add(btnCykliczne);

            // Przesuń przycisk Usuń bardziej w lewo
            btnUsun.Location = new System.Drawing.Point(440, 12);

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
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel panelFiltry;
        private System.Windows.Forms.ComboBox cbFiltrujHandlowca;
        private System.Windows.Forms.TextBox txtFiltrujOdbiorce;
        private System.Windows.Forms.TableLayoutPanel panelSzczegolyTop;
        private System.Windows.Forms.Panel panelNotatki;
        private System.Windows.Forms.TextBox txtNotatki;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panelPrzychody;
        private System.Windows.Forms.DataGridView dgvPrzychody;
        private System.Windows.Forms.Button btnSo;
        private System.Windows.Forms.Button btnNd;
        private System.Windows.Forms.Button btnTydzienNext;
        private System.Windows.Forms.Button btnTydzienPrev;
        private System.Windows.Forms.Label lblZakresDat;
        private System.Windows.Forms.Button btnAnuluj;
        private System.Windows.Forms.ComboBox cbFiltrujTowar;
        private System.Windows.Forms.Button btnDuplikuj;
        private System.Windows.Forms.Button btnUsun;
        private System.Windows.Forms.DataGridView dgvPojTuszki;
        // W sekcji deklaracji (na końcu pliku przed klasą)
        private System.Windows.Forms.Button btnCykliczne;
        private System.Windows.Forms.RadioButton rbDataOdbioru;
        private System.Windows.Forms.RadioButton rbDataUboju;
    }
}