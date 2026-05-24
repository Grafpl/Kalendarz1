using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.HDI
{
    /// <summary>
    /// Lista zamówień dla wybranej daty uboju — fakturzystka klika i przekazuje do HDI auto-fill.
    /// Zwraca SelectedOrderId (int) gdy user zatwierdzi wybór.
    /// </summary>
    public partial class WyborZamowieniaDialog : Window
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public int? SelectedOrderId { get; private set; }
        private readonly ObservableCollection<ZamowienieRow> _allRows = new();
        private readonly ObservableCollection<ZamowienieRow> _viewRows = new();

        public class ZamowienieRow
        {
            public int Id { get; set; }
            public int KlientId { get; set; }
            public string Klient { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public decimal IloscKg { get; set; }
            public string NumerFaktury { get; set; } = "";
            public DateTime? DataUboju { get; set; }
            public bool CzyZafakturowane { get; set; }
        }

        public WyborZamowieniaDialog(DateTime initialDate)
        {
            InitializeComponent();
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }
            DpDate.SelectedDate = initialDate.Date;
            GridZam.ItemsSource = _viewRows;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _allRows.Clear();
            _viewRows.Clear();
            var date = DpDate.SelectedDate ?? DateTime.Today;
            LblStatus.Text = $"⏳ Ładowanie zamówień dla {date:dd.MM.yyyy}…";

            try
            {
                // 1) ZamowieniaMieso dla daty uboju
                var tmp = new List<ZamowienieRow>();
                var klienciIds = new HashSet<int>();
                await using (var cn = new SqlConnection(ConnLibra))
                {
                    await cn.OpenAsync();
                    const string sql = @"
                        SELECT zm.Id, ISNULL(zm.KlientId,0) AS KlientId,
                               SUM(ISNULL(zmt.Ilosc, 0)) AS IloscKg,
                               ISNULL(zm.NumerFaktury,'') AS NumerFaktury,
                               zm.DataUboju,
                               ISNULL(zm.CzyZafakturowane, 0) AS CzyZafakturowane
                        FROM dbo.ZamowieniaMieso zm
                        LEFT JOIN dbo.ZamowieniaMiesoTowar zmt ON zm.Id = zmt.ZamowienieId
                        WHERE zm.DataUboju = @d
                          AND ISNULL(zm.Status, '') <> 'Anulowane'
                        GROUP BY zm.Id, zm.KlientId, zm.NumerFaktury, zm.DataUboju, zm.CzyZafakturowane
                        ORDER BY zm.Id DESC";
                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@d", date);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        var row = new ZamowienieRow
                        {
                            Id           = Convert.ToInt32(rd.GetValue(0)),
                            KlientId     = Convert.ToInt32(rd.GetValue(1)),
                            IloscKg      = rd.IsDBNull(2) ? 0 : Convert.ToDecimal(rd.GetValue(2)),
                            NumerFaktury = rd.IsDBNull(3) ? "" : rd.GetString(3),
                            DataUboju    = rd.IsDBNull(4) ? null : (DateTime?)rd.GetDateTime(4),
                            CzyZafakturowane = !rd.IsDBNull(5) && Convert.ToBoolean(rd.GetValue(5))
                        };
                        tmp.Add(row);
                        if (row.KlientId > 0) klienciIds.Add(row.KlientId);
                    }
                }

                // 2) Klienci + handlowcy z Sage (jedno query batch)
                var klienci = new Dictionary<int, (string Nazwa, string Handlowiec)>();
                if (klienciIds.Count > 0)
                {
                    string ids = string.Join(",", klienciIds);
                    try
                    {
                        await using var cn = new SqlConnection(ConnHandel);
                        await cn.OpenAsync();
                        string sql = $@"
                            SELECT C.Id, ISNULL(C.Name,'') AS Nazwa,
                                   ISNULL(W.CDim_Handlowiec_Val,'') AS Handlowiec
                            FROM [HANDEL].[SSCommon].[STContractors] C
                            LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] W ON W.ElementId = C.Id
                            WHERE C.Id IN ({ids})";
                        await using var cmd = new SqlCommand(sql, cn);
                        await using var rd = await cmd.ExecuteReaderAsync();
                        while (await rd.ReadAsync())
                        {
                            int kid = Convert.ToInt32(rd.GetValue(0));
                            klienci[kid] = (rd.GetString(1), rd.GetString(2));
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Wybor klient] {ex.Message}"); }
                }

                foreach (var r in tmp)
                {
                    if (klienci.TryGetValue(r.KlientId, out var k))
                    {
                        r.Klient = k.Nazwa;
                        r.Handlowiec = k.Handlowiec;
                    }
                    else r.Klient = $"Klient {r.KlientId}";
                    _allRows.Add(r);
                }
                ApplyFilter();
                LblStatus.Text = $"✓ {_allRows.Count} zamówień dla {date:dd.MM.yyyy}";
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"⚠ Błąd: {ex.Message}";
            }
        }

        private void ApplyFilter()
        {
            string f = (TxtFilter.Text ?? "").Trim().ToLowerInvariant();
            _viewRows.Clear();
            foreach (var r in _allRows)
            {
                if (string.IsNullOrEmpty(f)
                    || r.Id.ToString().Contains(f)
                    || r.Klient.ToLowerInvariant().Contains(f)
                    || r.Handlowiec.ToLowerInvariant().Contains(f)
                    || r.NumerFaktury.ToLowerInvariant().Contains(f))
                    _viewRows.Add(r);
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private async void BtnSearch_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private void GridZam_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Pick();
        private void BtnPick_Click(object sender, RoutedEventArgs e) => Pick();
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void Pick()
        {
            if (GridZam.SelectedItem is ZamowienieRow row)
            {
                SelectedOrderId = row.Id;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(this, "Wybierz wiersz z listy.", "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
