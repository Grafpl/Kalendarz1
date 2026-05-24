using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    /// <summary>
    /// Dopasowuje fakturę zakupu (FVR/FVZ/FKZ) z HANDEL do konkretnej dostawy w FarmerCalc.
    /// Wzorzec na podstawie WPF.DopasujFaktureWindow (dla sprzedazy FVS), zaadaptowany do zakupu.
    /// </summary>
    public partial class DopasujFaktureZakupuWindow : Window
    {
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly int _farmerCalcId;
        private readonly int _idSymf;
        private readonly decimal _expectedKg;
        private readonly DateTime _calcDate;
        private readonly string _obecnyNrFV;

        /// <summary>Po DialogResult=true zawiera nr FV ktora wybrano (lub pusty string jak odpieto).</summary>
        public string SelectedNumerFaktury { get; private set; } = "";
        public int SelectedIdFV { get; private set; }
        public bool Odepnieto { get; private set; }

        public DopasujFaktureZakupuWindow(
            int farmerCalcId,
            int idSymf,
            string dostawcaNazwa,
            DateTime calcDate,
            decimal expectedKg,
            string obecnyNrFV)
        {
            InitializeComponent();
            _farmerCalcId = farmerCalcId;
            _idSymf = idSymf;
            _calcDate = calcDate.Date;
            _expectedKg = expectedKg;
            _obecnyNrFV = obecnyNrFV ?? "";

            txtDostawca.Text = dostawcaNazwa ?? "—";
            txtDostawcaMeta.Text = idSymf > 0 ? $"IdSymf: {idSymf}" : "BRAK mapowania na Symfonie";
            txtDostawa.Text = $"{_calcDate:dd.MM.yyyy} (FarmerCalc #{farmerCalcId})";
            txtDostawaKg.Text = $"Oczekiwane: {expectedKg:N2} kg";
            txtObecnyNr.Text = string.IsNullOrWhiteSpace(_obecnyNrFV) ? "(brak)" : _obecnyNrFV;

            if (_idSymf == 0)
            {
                txtInfo.Text = "Dostawca nie ma mapowania (IdSymf = 0) — nie można szukać faktur w Symfonii.";
                dgInvoices.IsEnabled = false;
            }
            else
            {
                Loaded += async (_, _) => await LoadInvoicesAsync();
            }
        }

        private int RangeMonths() => cmbRange.SelectedIndex switch
        {
            1 => 3,
            2 => 6,
            3 => 12,
            _ => 1
        };

        private async Task LoadInvoicesAsync()
        {
            txtInfo.Text = "Ładowanie faktur z Symfonii...";
            var list = new ObservableCollection<InvoiceZakRow>();

            try
            {
                using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();

                DateTime since = DateTime.Today.AddMonths(-RangeMonths());
                bool includeKorekty = chkKorekty.IsChecked == true;

                string typFilter = includeKorekty
                    ? "dk.typ_dk IN ('FVR','FVZ','FKZ')"
                    : "dk.typ_dk IN ('FVR','FVZ')";

                string sql = $@"
SELECT dk.id, dk.kod, dk.data, dk.typ_dk,
       COALESCE(SUM(ABS(dp.ilosc)), 0)    AS Suma,
       COALESCE(SUM(ABS(dp.wartNetto)),0) AS Wartosc
FROM HM.DK dk
LEFT JOIN HM.DP dp ON dp.super = dk.id
WHERE dk.khid = @khid
  AND {typFilter}
  AND ISNULL(dk.anulowany, 0) = 0
  AND dk.aktywny = 1
  AND dk.data >= @since
GROUP BY dk.id, dk.kod, dk.data, dk.typ_dk
ORDER BY dk.data DESC, dk.kod DESC";

                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@khid", _idSymf);
                cmd.Parameters.AddWithValue("@since", since);

                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var row = new InvoiceZakRow
                    {
                        Id = rdr.GetInt32(0),
                        Numer = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                        Data = rdr.IsDBNull(2) ? DateTime.MinValue : rdr.GetDateTime(2),
                        Typ = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                        Suma = rdr.IsDBNull(4) ? 0 : Convert.ToDecimal(rdr.GetValue(4)),
                        Wartosc = rdr.IsDBNull(5) ? 0 : Convert.ToDecimal(rdr.GetValue(5))
                    };
                    row.ComputeStatus(_expectedKg, _calcDate, _obecnyNrFV);
                    list.Add(row);
                }

                dgInvoices.ItemsSource = list;

                // Auto-select aktualnie przypisanej (jesli jest)
                foreach (var r in list)
                {
                    if (string.Equals(r.Numer, _obecnyNrFV, StringComparison.OrdinalIgnoreCase))
                    {
                        dgInvoices.SelectedItem = r;
                        dgInvoices.ScrollIntoView(r);
                        break;
                    }
                }

                txtInfo.Text = list.Count == 0
                    ? "Brak faktur zakupu dla tego dostawcy w wybranym okresie"
                    : $"Znaleziono {list.Count} faktur. Dwuklik = przypisanie, lub kliknij ✓ Przypisz wybraną.";
            }
            catch (Exception ex)
            {
                txtInfo.Text = $"Błąd: {ex.Message}";
                MessageBox.Show(this, ex.Message, "Błąd odczytu faktur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _idSymf == 0) return;
            await LoadInvoicesAsync();
        }

        private async void ChkKorekty_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _idSymf == 0) return;
            await LoadInvoicesAsync();
        }

        private void DgInvoices_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BtnWybierz_Click(sender, e);
        }

        private async void BtnWybierz_Click(object sender, RoutedEventArgs e)
        {
            if (dgInvoices.SelectedItem is not InvoiceZakRow row)
            {
                MessageBox.Show(this, "Najpierw wybierz fakturę z listy.", "Brak wyboru",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Ostrzezenie jak niezgodna kg
            decimal tol = Math.Max(1m, _expectedKg * 0.005m);
            if (Math.Abs(row.Suma - _expectedKg) > tol)
            {
                var diff = row.Suma - _expectedKg;
                var res = MessageBox.Show(this,
                    $"Wybrana FV: {row.Numer}\nSuma w FV: {row.Suma:N2} kg\nOczekiwane: {_expectedKg:N2} kg\nRóżnica: {diff:+#.00;-#.00;0} kg\n\nPrzypisac mimo to?",
                    "Niezgodna kwota", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res != MessageBoxResult.Yes) return;
            }

            // Zapisz do FarmerCalc
            bool ok = await ZapiszMapowanieFvAsync(row.Id, row.Numer);
            if (ok)
            {
                SelectedIdFV = row.Id;
                SelectedNumerFaktury = row.Numer;
                Odepnieto = false;
                DialogResult = true;
                Close();
            }
        }

        private async void BtnOdepnij_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_obecnyNrFV))
            {
                MessageBox.Show(this, "Ta dostawa nie ma przypisanej faktury.", "Brak",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var res = MessageBox.Show(this,
                $"Odepnij fakturę {_obecnyNrFV} od dostawy?\n\nFarmerCalc.SymfoniaIdFV / SymfoniaNrFV / Symfonia zostaną wyzerowane.",
                "Potwierdz odpiecie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            bool ok = await ZapiszMapowanieFvAsync(0, "");
            if (ok)
            {
                SelectedIdFV = 0;
                SelectedNumerFaktury = "";
                Odepnieto = true;
                DialogResult = true;
                Close();
            }
        }

        private async Task<bool> ZapiszMapowanieFvAsync(int idFv, string nrFv)
        {
            try
            {
                using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(@"
UPDATE dbo.FarmerCalc SET
    Symfonia           = CASE WHEN @IdFV > 0 THEN 1 ELSE 0 END,
    SymfoniaIdFV       = CASE WHEN @IdFV > 0 THEN @IdFV ELSE NULL END,
    SymfoniaNrFV       = CASE WHEN @NrFV = '' THEN NULL ELSE @NrFV END,
    SymfoniaExportDate = CASE WHEN @IdFV > 0 THEN GETDATE() ELSE NULL END
WHERE ID = @ID", cn);
                cmd.Parameters.AddWithValue("@IdFV", idFv);
                cmd.Parameters.AddWithValue("@NrFV", nrFv ?? "");
                cmd.Parameters.AddWithValue("@ID", _farmerCalcId);
                int n = await cmd.ExecuteNonQueryAsync();
                if (n == 0)
                {
                    MessageBox.Show(this, $"Nie znaleziono FarmerCalc.ID = {_farmerCalcId}.",
                        "Błąd zapisu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd zapisu: " + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ============ MODEL ============
        public class InvoiceZakRow
        {
            private static readonly Brush BrushOk    = Freeze(0x16, 0xA3, 0x4A);
            private static readonly Brush BrushWarn  = Freeze(0xEA, 0x58, 0x0C);
            private static readonly Brush BrushBlue  = Freeze(0x25, 0x63, 0xEB);
            private static readonly Brush BrushPink  = Freeze(0xDB, 0x27, 0x77);
            private static Brush Freeze(byte r, byte g, byte b) { var br = new SolidColorBrush(Color.FromRgb(r,g,b)); br.Freeze(); return br; }

            public int Id { get; set; }
            public string Numer { get; set; } = "";
            public string Typ { get; set; } = "";
            public DateTime Data { get; set; }
            public decimal Suma { get; set; }
            public decimal Wartosc { get; set; }

            public string DataStr    => Data.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            public string SumaStr    => Suma.ToString("N2", CultureInfo.GetCultureInfo("pl-PL"));
            public string WartoscStr => Wartosc.ToString("N2", CultureInfo.GetCultureInfo("pl-PL"));

            public string StatusIcon { get; private set; } = "";
            public string StatusText { get; private set; } = "";
            public Brush StatusBrush { get; private set; } = Brushes.Black;

            public void ComputeStatus(decimal expectedKg, DateTime expectedDate, string obecnyNr)
            {
                bool isCurrent = string.Equals(Numer, obecnyNr, StringComparison.OrdinalIgnoreCase);
                if (isCurrent)
                {
                    StatusIcon = "●";
                    StatusText = "Aktualnie przypisana";
                    StatusBrush = BrushBlue;
                    return;
                }

                decimal tolKg = Math.Max(1m, expectedKg * 0.005m);
                bool kgOk = Math.Abs(Suma - expectedKg) <= tolKg;
                bool dataOk = Data.Date == expectedDate.Date;

                if (kgOk && dataOk)
                {
                    StatusIcon = "✓";
                    StatusText = "Idealne dopasowanie (kg + data)";
                    StatusBrush = BrushOk;
                }
                else if (kgOk)
                {
                    StatusIcon = "✓";
                    StatusText = $"Kg pasują ({Suma:N2}) — data inna ({Data:dd.MM})";
                    StatusBrush = BrushOk;
                }
                else if (dataOk)
                {
                    decimal diff = Suma - expectedKg;
                    string sign = diff > 0 ? "+" : "";
                    StatusIcon = "⚠";
                    StatusText = $"Data ok — różnica kg: {sign}{diff:N2}";
                    StatusBrush = BrushWarn;
                }
                else
                {
                    decimal diff = Suma - expectedKg;
                    string sign = diff > 0 ? "+" : "";
                    StatusIcon = "⚠";
                    StatusText = $"Różnica kg: {sign}{diff:N2}  ·  inna data";
                    StatusBrush = BrushPink;
                }
            }
        }
    }
}
