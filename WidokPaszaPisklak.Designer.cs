namespace Kalendarz1
{
    partial class WidokPaszaPisklak
    {
        /// <summary>Required designer variable.</summary>
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            // === ToolStrip ===
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(4, 2, 4, 2);
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1100, 31);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";

            // Szukaj
            this.toolStripLabelSearch = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabelSearch.Name = "toolStripLabelSearch";
            this.toolStripLabelSearch.Size = new System.Drawing.Size(47, 24);
            this.toolStripLabelSearch.Text = "Szukaj:";

            this.txtSearch = new System.Windows.Forms.ToolStripTextBox();
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtSearch.AutoSize = false;
            this.txtSearch.Size = new System.Drawing.Size(260, 27);
            this.txtSearch.ToolTipText = "Wpisz, aby filtrować (Kontrahent/Kod/Data).";

            // Status DK.ok
            this.toolStripLabelStatus = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabelStatus.Name = "toolStripLabelStatus";
            this.toolStripLabelStatus.Text = "Status:";

            this.cbStatus = new System.Windows.Forms.ToolStripComboBox();
            this.cbStatus.Name = "cbStatus";
            this.cbStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbStatus.AutoSize = false;
            this.cbStatus.Width = 160;
            this.cbStatus.Items.AddRange(new object[] { "Nierozliczone (ok=0)", "Rozliczone (ok=1)", "Wszystkie" });
            this.cbStatus.SelectedIndex = 0;

            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();

            // Zakres dat
            this.toolStripLabelFrom = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabelFrom.Name = "toolStripLabelFrom";
            this.toolStripLabelFrom.Text = "Data od:";

            this._dtFromHost = new System.Windows.Forms.ToolStripControlHost(new System.Windows.Forms.DateTimePicker()
            {
                Width = 130,
                Format = System.Windows.Forms.DateTimePickerFormat.Short
            });

            this.toolStripLabelTo = new System.Windows.Forms.ToolStripLabel();
            this.toolStripLabelTo.Name = "toolStripLabelTo";
            this.toolStripLabelTo.Text = "do:";

            this._dtToHost = new System.Windows.Forms.ToolStripControlHost(new System.Windows.Forms.DateTimePicker()
            {
                Width = 130,
                Format = System.Windows.Forms.DateTimePickerFormat.Short
            });

            this.btnPresets = new System.Windows.Forms.ToolStripDropDownButton();
            this.btnPresets.Name = "btnPresets";
            this.btnPresets.Text = "Zakres";

            // Standardowe przyciski
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.btnGroup = new System.Windows.Forms.ToolStripButton();
            this.btnGroup.Name = "btnGroup";
            this.btnGroup.Text = "Grupuj wg kontrahenta";
            this.btnGroup.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnGroup.CheckOnClick = true;

            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();

            this.btnRefresh = new System.Windows.Forms.ToolStripButton();
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Text = "Odśwież";
            this.btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;

            this.btnExportCsv = new System.Windows.Forms.ToolStripButton();
            this.btnExportCsv.Name = "btnExportCsv";
            this.btnExportCsv.Text = "Eksport CSV";
            this.btnExportCsv.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;

            this.btnCopy = new System.Windows.Forms.ToolStripButton();
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Text = "Kopiuj zaznaczone";
            this.btnCopy.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;

            this.btnColumns = new System.Windows.Forms.ToolStripButton();
            this.btnColumns.Name = "btnColumns";
            this.btnColumns.Text = "Kolumny…";
            this.btnColumns.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;

            // Złożenie ToolStrip
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.toolStripLabelSearch,
                this.txtSearch,

                this.toolStripLabelStatus,
                this.cbStatus,
                this.toolStripSeparator3,

                this.toolStripLabelFrom,
                this._dtFromHost,
                this.toolStripLabelTo,
                this._dtToHost,
                this.btnPresets,

                this.toolStripSeparator1,
                this.btnGroup,
                this.toolStripSeparator2,
                this.btnRefresh,
                this.btnExportCsv,
                this.btnCopy,
                this.btnColumns
            });

            // === StatusStrip ===
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusStrip1.Location = new System.Drawing.Point(0, 628);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 16, 0);
            this.statusStrip1.Size = new System.Drawing.Size(1100, 22);
            this.statusStrip1.TabIndex = 2;

            this.lblCount = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblCount.Name = "lblCount";
            this.lblCount.Size = new System.Drawing.Size(68, 17);
            this.lblCount.Text = "Wiersze: 0";

            this.lblSum = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSum.Margin = new System.Windows.Forms.Padding(16, 3, 0, 2);
            this.lblSum.Size = new System.Drawing.Size(124, 17);
            this.lblSum.Text = "Suma walBrutto: 0,00";

            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.lblCount, this.lblSum
            });

            // === DataGridView ===
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 31);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowTemplate.Height = 28;
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.Margin = new System.Windows.Forms.Padding(0);

            // === ContextMenu ===
            this.cmsGrid = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miCopyCell = new System.Windows.Forms.ToolStripMenuItem();
            this.miCopyRow = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.miFilterByValue = new System.Windows.Forms.ToolStripMenuItem();
            this.miClearFilter = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.miHistory = new System.Windows.Forms.ToolStripMenuItem();

            this.miCopyCell.Name = "miCopyCell";
            this.miCopyCell.Size = new System.Drawing.Size(196, 22);
            this.miCopyCell.Text = "Kopiuj komórkę";

            this.miCopyRow.Name = "miCopyRow";
            this.miCopyRow.Size = new System.Drawing.Size(196, 22);
            this.miCopyRow.Text = "Kopiuj wiersz";

            this.miFilterByValue.Name = "miFilterByValue";
            this.miFilterByValue.Size = new System.Drawing.Size(196, 22);
            this.miFilterByValue.Text = "Filtruj wg tej wartości";

            this.miClearFilter.Name = "miClearFilter";
            this.miClearFilter.Size = new System.Drawing.Size(196, 22);
            this.miClearFilter.Text = "Wyczyść filtr";

            this.miHistory.Name = "miHistory";
            this.miHistory.Size = new System.Drawing.Size(196, 22);
            this.miHistory.Text = "Pokaż historię kontrahenta";

            this.cmsGrid.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.miCopyCell,
                this.miCopyRow,
                this.toolStripMenuItem1,
                this.miFilterByValue,
                this.miClearFilter,
                this.toolStripMenuItem2,
                this.miHistory
            });

            this.dataGridView1.ContextMenuStrip = this.cmsGrid;

            // === Form ===
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1100, 650);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "WidokPaszaPisklak";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Pasza / Pisklak – dokumenty sprzedaży";

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel toolStripLabelSearch;
        private System.Windows.Forms.ToolStripTextBox txtSearch;

        private System.Windows.Forms.ToolStripLabel toolStripLabelStatus;
        private System.Windows.Forms.ToolStripComboBox cbStatus;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripLabel toolStripLabelFrom;
        private System.Windows.Forms.ToolStripControlHost _dtFromHost;
        private System.Windows.Forms.ToolStripLabel toolStripLabelTo;
        private System.Windows.Forms.ToolStripControlHost _dtToHost;
        private System.Windows.Forms.ToolStripDropDownButton btnPresets;

        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton btnGroup;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripButton btnExportCsv;
        private System.Windows.Forms.ToolStripButton btnCopy;
        private System.Windows.Forms.ToolStripButton btnColumns;

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblCount;
        private System.Windows.Forms.ToolStripStatusLabel lblSum;

        private System.Windows.Forms.DataGridView dataGridView1;

        private System.Windows.Forms.ContextMenuStrip cmsGrid;
        private System.Windows.Forms.ToolStripMenuItem miCopyCell;
        private System.Windows.Forms.ToolStripMenuItem miCopyRow;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem miFilterByValue;
        private System.Windows.Forms.ToolStripMenuItem miClearFilter;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem miHistory;
    }
}
