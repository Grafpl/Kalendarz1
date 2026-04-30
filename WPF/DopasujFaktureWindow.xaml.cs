using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class DopasujFaktureWindow : Window
    {
        private readonly string _connHandel;
        private readonly int _klientId;
        private readonly int _orderId;
        private readonly decimal _orderTotalIlosc;
        private readonly string _obecnyNrFaktury;

        public string SelectedNumerFaktury { get; private set; } = "";

        public DopasujFaktureWindow(
            string connHandel,
            int klientId,
            string klientName,
            int orderId,
            decimal orderTotalIlosc,
            string obecnyNrFaktury)
        {
            InitializeComponent();
            _connHandel        = connHandel;
            _klientId          = klientId;
            _orderId           = orderId;
            _orderTotalIlosc   = orderTotalIlosc;
            _obecnyNrFaktury   = obecnyNrFaktury ?? "";

            txtKlient.Text   = klientName;
            txtKlientId.Text = $"khid: {klientId}";
            txtZamId.Text    = $"#{orderId}";
            txtZamSuma.Text  = $"{orderTotalIlosc:N2} kg";
            txtObecnyNr.Text = string.IsNullOrEmpty(_obecnyNrFaktury) ? "(brak)" : _obecnyNrFaktury;

            Loaded += async (_, _) => await LoadInvoicesAsync();
        }

        private int RangeMonths()
        {
            return cmbRange.SelectedIndex switch
            {
                1 => 3,
                2 => 6,
                3 => 12,
                _ => 1
            };
        }

        private async System.Threading.Tasks.Task LoadInvoicesAsync()
        {
            txtInfo.Text = "Ładowanie faktur...";
            var list = new ObservableCollection<InvoiceRow>();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                DateTime since = DateTime.Today.AddMonths(-RangeMonths());

                const string sql = @"SELECT dk.kod, dk.data, dk.typ_dk,
                                            COALESCE(SUM(dp.ilosc), 0) AS Suma,
                                            COALESCE(SUM(dp.wartNetto), 0) AS Wartosc
                                     FROM HM.DK dk
                                     LEFT JOIN HM.DP dp ON dp.super = dk.id
                                     WHERE dk.khid = @khid
                                       AND dk.typ_dk IN ('FVS', 'KFS', 'PAR')
                                       AND ISNULL(dk.anulowany, 0) = 0
                                       AND dk.data >= @since
                                     GROUP BY dk.kod, dk.data, dk.typ_dk
                                     ORDER BY dk.data DESC, dk.kod DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@khid", _klientId);
                cmd.Parameters.AddWithValue("@since", since);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string kod = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    DateTime data = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1);
                    string typ = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    decimal suma = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3));
                    decimal wartosc = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4));

                    var row = new InvoiceRow
                    {
                        Numer = kod,
                        Data  = data,
                        Suma  = suma,
                        Wartosc = wartosc
                    };
                    row.ComputeStatus(_orderTotalIlosc, _obecnyNrFaktury);
                    list.Add(row);
                }

                dgInvoices.ItemsSource = list;
                txtInfo.Text = list.Count == 0
                    ? "Brak faktur dla tego klienta w wybranym okresie"
                    : $"Znaleziono {list.Count} faktur. Kliknij dwukrotnie żeby wybrać.";
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
            if (!IsLoaded) return;
            await LoadInvoicesAsync();
        }

        private void DgInvoices_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            BtnWybierz_Click(sender, e);
        }

        private void BtnWybierz_Click(object sender, RoutedEventArgs e)
        {
            if (dgInvoices.SelectedItem is not InvoiceRow row)
            {
                MessageBox.Show(this, "Najpierw wybierz fakturę z listy.", "Brak wyboru",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedNumerFaktury = row.Numer;
            DialogResult = true;
            Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public class InvoiceRow
        {
            private static readonly Brush BrushOk    = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
            private static readonly Brush BrushWarn  = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)));
            private static readonly Brush BrushBlue  = Freeze(new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)));
            private static readonly Brush BrushGray  = Freeze(new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)));
            private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

            public string Numer { get; set; } = "";
            public DateTime Data { get; set; }
            public decimal Suma { get; set; }
            public decimal Wartosc { get; set; }

            public string DataStr     => Data.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            public string SumaStr     => Suma.ToString("N2", CultureInfo.GetCultureInfo("pl-PL"));
            public string WartoscStr  => Wartosc.ToString("N2", CultureInfo.GetCultureInfo("pl-PL"));

            public string StatusIcon { get; private set; } = "";
            public string StatusText { get; private set; } = "";
            public Brush  StatusBrush { get; private set; } = Brushes.Black;

            public void ComputeStatus(decimal orderTotalIlosc, string obecnyNr)
            {
                if (string.Equals(Numer, obecnyNr, StringComparison.OrdinalIgnoreCase))
                {
                    StatusIcon  = "●";
                    StatusText  = "Aktualnie przypisana";
                    StatusBrush = BrushBlue;
                    return;
                }
                decimal tol = Math.Max(1m, orderTotalIlosc * 0.005m);
                if (Math.Abs(Suma - orderTotalIlosc) <= tol)
                {
                    StatusIcon  = "✓";
                    StatusText  = "Ilość zgadza się z zamówieniem";
                    StatusBrush = BrushOk;
                }
                else
                {
                    decimal diff = Suma - orderTotalIlosc;
                    string sign = diff > 0 ? "+" : "";
                    StatusIcon  = "⚠";
                    StatusText  = $"Różnica: {sign}{diff:N2} kg";
                    StatusBrush = BrushWarn;
                }
            }
        }
    }
}
