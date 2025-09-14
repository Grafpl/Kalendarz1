using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

internal sealed class HistoriaHodowcyForm : Form
{
    private readonly string _conn;
    private readonly string _dostawca;
    private readonly DataGridView _grid = new();

    public HistoriaHodowcyForm(string connectionString, string dostawca)
    {
        _conn = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _dostawca = dostawca ?? throw new ArgumentNullException(nameof(dostawca));
        Text = $"Historia i notatki — {dostawca}";
        Width = 1180; Height = 760; // większe okno

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AutoGenerateColumns = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

        _grid.CellFormatting += Grid_CellFormatting; // sufiks „ pkt.”

        Controls.Add(_grid);

        Shown += async (_, __) => await LoadDataAsync();
    }
    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        var grid = (DataGridView)sender!;
        string col = grid.Columns[e.ColumnIndex].Name;

        // kolumny punktowe
        if (col is "OcenaCena" or "OcenaTransport" or "OcenaKomunikacja" or "OcenaElastycznosc")
        {
            if (e.Value != null && e.Value != DBNull.Value)
            {
                e.Value = $"{Convert.ToInt32(e.Value)} pkt.";
                e.FormattingApplied = true;
            }
        }
        else if (col == "SredniaWiersza")
        {
            if (e.Value != null && e.Value != DBNull.Value)
            {
                var dec = Convert.ToDecimal(e.Value);
                e.Value = $"{dec.ToString("N1")} pkt.";
                e.FormattingApplied = true;
            }
        }
    }

    private async Task LoadDataAsync()
    {
        const string sql = @"
SELECT
    f.DostawaLp,
    h.Dostawca,
    f.Kto,
    f.DataDostawy,
    f.DataAnkiety,
    f.OcenaCena,
    f.OcenaTransport,
    f.OcenaKomunikacja,
    f.OcenaElastycznosc,
    CAST(ROUND(
        (
            COALESCE(CAST(f.OcenaCena AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaTransport AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaKomunikacja AS DECIMAL(10,4)),0) +
            COALESCE(CAST(f.OcenaElastycznosc AS DECIMAL(10,4)),0)
        ) / NULLIF(
            (CASE WHEN f.OcenaCena IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN f.OcenaTransport IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN f.OcenaKomunikacja IS NOT NULL THEN 1 ELSE 0 END) +
            (CASE WHEN f.OcenaElastycznosc IS NOT NULL THEN 1 ELSE 0 END), 0
        ), 1) AS DECIMAL(10,1)) AS SredniaWiersza,
    f.Notatka
FROM dbo.DostawaFeedback f
INNER JOIN dbo.HarmonogramDostaw h
    ON h.Lp = f.DostawaLp
WHERE h.Dostawca = @dostawca
ORDER BY f.DataDostawy DESC, f.DataAnkiety DESC, f.DostawaLp DESC;";

        using var cn = new SqlConnection(_conn);
        await cn.OpenAsync();
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@dostawca", _dostawca);
        using var da = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        da.Fill(dt);

        _grid.DataSource = dt;

        if (_grid.Columns.Contains("DataDostawy"))
            _grid.Columns["DataDostawy"].DefaultCellStyle.Format = "yyyy-MM-dd";
        if (_grid.Columns.Contains("DataAnkiety"))
            _grid.Columns["DataAnkiety"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
    }
}
