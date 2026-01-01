using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Input;  // ← DODAJ TEN USING

using System.IO;
using System.Text;

namespace Kalendarz1.WPF
{
    public partial class PotencjalniOdbiorcy : Window
    {
        private readonly string _connectionString;
        private int _produktId;
        private string _produktNazwa;
        private readonly decimal _plan;
        private readonly decimal _fakt;
        private readonly decimal _zamowienia;
        private readonly decimal _bilans;
        private readonly DateTime _dataReferencja;
        private DataTable _dtOdbiorcy;
        private DataView _dvFiltrowany;
        private bool _isLoading = true;

        public PotencjalniOdbiorcy(string connectionString, int produktId, string produktNazwa,
            decimal plan, decimal fakt, decimal zamowienia, decimal bilans, DateTime dataReferencja)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _produktId = produktId;
            _produktNazwa = produktNazwa;
            _plan = plan;
            _fakt = fakt;
            _zamowienia = zamowienia;
            _bilans = bilans;
            _dataReferencja = dataReferencja;

            Loaded += PotencjalniOdbiorcy_Loaded;
            dgOdbiorcy.SelectionChanged += DgOdbiorcy_SelectionChanged;
        }

        private async void PotencjalniOdbiorcy_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;

            // Załaduj listę produktów do ComboBox
            await ZaladujListeProduktowAsync();

            // Ustaw aktualnie wybrany produkt
            cbWyborProduktu.SelectedValue = _produktId;

            decimal referencja = Math.Max(_plan, _fakt);
            decimal brakuje = referencja - _zamowienia;

            AktualizujNaglowek();

            txtPlan.Text = $"{referencja:N0} kg";
            txtZamowienia.Text = $"{_zamowienia:N0} kg";
            txtBrakuje.Text = brakuje > 0 ? $"{brakuje:N0} kg" : "0 kg";
            txtBrakuje.Foreground = brakuje > 0 ?
                new SolidColorBrush(Color.FromRgb(231, 76, 60)) :
                new SolidColorBrush(Color.FromRgb(39, 174, 96));

            txtBilans.Text = $"{_bilans:N0} kg";
            txtBilans.Foreground = _bilans >= 0 ?
                new SolidColorBrush(Color.FromRgb(39, 174, 96)) :
                new SolidColorBrush(Color.FromRgb(231, 76, 60));

            await WczytajOdbiorcowAsync();

            _isLoading = false;
        }

        private async System.Threading.Tasks.Task ZaladujListeProduktowAsync()
        {
            try
            {
                var produkty = new Dictionary<int, string>();

                await using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();

                // Pobierz produkty z katalogu 67095 i 67153
                var cmd = new SqlCommand(@"
                    SELECT ID, kod 
                    FROM [HANDEL].[HM].[TW] 
                    WHERE katalog IN (67095, 67153)
                    ORDER BY katalog, kod", cn);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string nazwa = reader.GetString(1);
                    produkty[id] = nazwa;
                }

                cbWyborProduktu.ItemsSource = produkty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania listy produktów:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AktualizujNaglowek()
        {
            txtNaglowek.Text = $"🔍 Potencjalni odbiorcy dla: {_produktNazwa}\n" +
                              $"📅 Data referencyjna: {_dataReferencja:yyyy-MM-dd dddd}";
        }

        private async void CbWyborProduktu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || cbWyborProduktu.SelectedValue == null)
                return;

            // Pobierz nowy produkt
            if (cbWyborProduktu.SelectedValue is int nowyProduktId &&
                nowyProduktId != _produktId)
            {
                _produktId = nowyProduktId;
                _produktNazwa = cbWyborProduktu.Text;

                AktualizujNaglowek();
                await WczytajOdbiorcowAsync();
            }
        }

        private async void BtnPrzeladujDane_Click(object sender, RoutedEventArgs e)
        {
            if (cbWyborProduktu.SelectedValue == null)
            {
                MessageBox.Show("Wybierz produkt z listy.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _produktId = (int)cbWyborProduktu.SelectedValue;
            _produktNazwa = cbWyborProduktu.Text;

            AktualizujNaglowek();
            await WczytajOdbiorcowAsync();
        }

        private async System.Threading.Tasks.Task WczytajOdbiorcowAsync()
        {
            try
            {
                // Pokaż kursor oczekiwania
                this.Cursor = Cursors.Wait;

                // Sprawdź czy filtr "Wszystkie okresy" jest wybrany
                bool wszystkieOkresy = cbFiltrSwiezosc?.SelectedItem is ComboBoxItem item &&
                                       item.Tag?.ToString() == "all";

                string query = @"
WITH HistoriaZakupow AS (
    SELECT
        C.id AS KlientId,
        C.shortcut AS Odbiorca,
        ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
        DP.cena AS Cena,
        DP.ilosc AS Ilosc,
        DP.wartNetto AS Wartosc,
        DK.data AS Data,
        YEAR(DK.data) AS Rok,
        ROW_NUMBER() OVER (PARTITION BY C.id ORDER BY DK.data DESC) AS Ranking
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
    WHERE DP.idtw = @ProduktId
      AND DK.anulowany = 0
      AND DK.data < @DataReferencja
      " + (wszystkieOkresy ? "" : "AND DK.data >= DATEADD(MONTH, -12, @DataReferencja)") + @"
),
OstatnieTransakcje AS (
    SELECT
        KlientId,
        Odbiorca,
        Handlowiec,
        Cena AS OstCena,
        Ilosc AS OstIlosc,
        Data AS OstData,
        DATEDIFF(DAY, Data, @DataReferencja) AS DniTemu
    FROM HistoriaZakupow
    WHERE Ranking = 1
),
StatystykiKlienta AS (
    SELECT
        KlientId,
        COUNT(*) AS LiczbaTransakcji,
        AVG(Cena) AS SredniaCena,
        AVG(Ilosc) AS SredniaIlosc,
        SUM(Ilosc) AS SumaIlosc,
        SUM(Wartosc) AS SumaWartosc,
        MIN(Data) AS PierwszyZakup,
        MAX(Data) AS OstatniZakup,
        CASE
            WHEN COUNT(*) > 1 THEN
                CAST(DATEDIFF(DAY, MIN(Data), MAX(Data)) AS FLOAT) / (COUNT(*) - 1)
            ELSE NULL
        END AS SrednioDniMiedzyZakupami
    FROM HistoriaZakupow
    GROUP BY KlientId
),
-- Liczba różnych produktów kupowanych przez klienta
RozneProduktyCTE AS (
    SELECT
        C.id AS KlientId,
        COUNT(DISTINCT DP.idtw) AS LiczbaRoznychProduktow
    FROM [HANDEL].[HM].[DK] DK
    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
    WHERE DK.anulowany = 0 AND DK.data >= DATEADD(MONTH, -12, @DataReferencja)
    GROUP BY C.id
),
SELECT
    OT.KlientId,
    OT.Odbiorca,
    OT.Handlowiec,
    CAST(OT.OstCena AS DECIMAL(18,2)) AS OstCena,
    CAST(OT.OstIlosc AS DECIMAL(18,2)) AS OstIlosc,
    OT.OstData,
    OT.DniTemu,
    SK.LiczbaTransakcji,
    CAST(SK.SredniaCena AS DECIMAL(18,2)) AS SredniaCena,
    CAST(SK.SredniaIlosc AS DECIMAL(18,2)) AS SredniaIlosc,
    CAST(SK.SumaIlosc AS DECIMAL(18,2)) AS SumaIlosc,
    CAST(SK.SumaWartosc AS DECIMAL(18,2)) AS SumaWartosc,
    CASE
        WHEN SK.SrednioDniMiedzyZakupami IS NULL THEN 'Jednorazowy'
        WHEN SK.SrednioDniMiedzyZakupami <= 14 THEN 'Co 2 tyg'
        WHEN SK.SrednioDniMiedzyZakupami <= 30 THEN 'Miesięcznie'
        WHEN SK.SrednioDniMiedzyZakupami <= 60 THEN 'Co 2 mies.'
        WHEN SK.SrednioDniMiedzyZakupami <= 90 THEN 'Kwartalnie'
        ELSE 'Nieregularny'
    END AS Regularnosc,
    -- Scoring potencjału (0-100)
    CAST(
        CASE
            -- Świeżość (max 30 pkt)
            WHEN OT.DniTemu <= 7 THEN 30
            WHEN OT.DniTemu <= 14 THEN 25
            WHEN OT.DniTemu <= 30 THEN 20
            WHEN OT.DniTemu <= 60 THEN 15
            WHEN OT.DniTemu <= 90 THEN 10
            ELSE 5
        END +
        -- Regularność (max 25 pkt)
        CASE
            WHEN SK.SrednioDniMiedzyZakupami <= 14 THEN 25
            WHEN SK.SrednioDniMiedzyZakupami <= 30 THEN 20
            WHEN SK.SrednioDniMiedzyZakupami <= 60 THEN 15
            WHEN SK.SrednioDniMiedzyZakupami <= 90 THEN 10
            ELSE 5
        END +
        -- Liczba transakcji (max 20 pkt)
        CASE
            WHEN SK.LiczbaTransakcji >= 20 THEN 20
            WHEN SK.LiczbaTransakcji >= 10 THEN 15
            WHEN SK.LiczbaTransakcji >= 5 THEN 10
            WHEN SK.LiczbaTransakcji >= 2 THEN 5
            ELSE 2
        END +
        -- Wartość (max 25 pkt)
        CASE
            WHEN SK.SumaWartosc >= 100000 THEN 25
            WHEN SK.SumaWartosc >= 50000 THEN 20
            WHEN SK.SumaWartosc >= 20000 THEN 15
            WHEN SK.SumaWartosc >= 10000 THEN 10
            ELSE 5
        END
    AS INT) AS Scoring,
    -- Prognoza następnego zakupu
    CASE
        WHEN SK.SrednioDniMiedzyZakupami IS NOT NULL THEN
            DATEADD(DAY, CAST(SK.SrednioDniMiedzyZakupami AS INT), SK.OstatniZakup)
        ELSE NULL
    END AS PrognozaNastepnegoZakupu,
    -- Liczba różnych produktów
    ISNULL(RP.LiczbaRoznychProduktow, 0) AS LiczbaProduktow
FROM OstatnieTransakcje OT
INNER JOIN StatystykiKlienta SK ON OT.KlientId = SK.KlientId
LEFT JOIN RozneProduktyCTE RP ON OT.KlientId = RP.KlientId
ORDER BY OT.DniTemu ASC, SK.SumaWartosc DESC;";

                await using var cn = new SqlConnection(_connectionString);
                await cn.OpenAsync();

                var cmd = new SqlCommand(query, cn);
                cmd.Parameters.AddWithValue("@ProduktId", _produktId);
                cmd.Parameters.AddWithValue("@DataReferencja", _dataReferencja.Date);

                var adapter = new SqlDataAdapter(cmd);
                _dtOdbiorcy = new DataTable();
                adapter.Fill(_dtOdbiorcy);

                // Wczytaj handlowców do ComboBox
                var handlowcy = _dtOdbiorcy.AsEnumerable()
                    .Select(r => r.Field<string>("Handlowiec"))
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();

                cbFiltrHandlowiec.Items.Clear();
                cbFiltrHandlowiec.Items.Add(new ComboBoxItem { Content = "👥 Wszyscy handlowcy", Tag = "" });
                foreach (var h in handlowcy)
                {
                    cbFiltrHandlowiec.Items.Add(new ComboBoxItem { Content = $"👤 {h}", Tag = h });
                }
                cbFiltrHandlowiec.SelectedIndex = 0;

                _dvFiltrowany = _dtOdbiorcy.DefaultView;
                dgOdbiorcy.ItemsSource = _dvFiltrowany;

                dgOdbiorcy.LoadingRow -= DgOdbiorcy_LoadingRow;
                dgOdbiorcy.LoadingRow += DgOdbiorcy_LoadingRow;

                AktualizujStatystyki();

                if (_dtOdbiorcy.Rows.Count == 0)
                {
                    MessageBox.Show($"Nie znaleziono odbiorców dla produktu '{_produktNazwa}' w ostatnich 12 miesiącach.",
                        "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"✓ Załadowano {_dtOdbiorcy.Rows.Count} odbiorców dla produktu '{_produktNazwa}'",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania odbiorców:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void DgOdbiorcy_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                // Kolorowanie na podstawie dni od ostatniego zakupu
                var dniTemu = rowView.Row.Field<int>("DniTemu");
                if (dniTemu <= 30)
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(245, 250, 245)); // Zielonkawy
                else if (dniTemu <= 90)
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 250, 240)); // Żółtawy
                else
                    e.Row.Background = new SolidColorBrush(Color.FromRgb(253, 237, 236)); // Czerwonawy
            }
        }

        private void DgOdbiorcy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgOdbiorcy == null || txtZaznaczono == null)
                return;

            try
            {
                int count = dgOdbiorcy.SelectedItems.Count;
                if (count > 0)
                {
                    decimal sumaIlosc = 0;
                    decimal sumaWartosc = 0;

                    foreach (var item in dgOdbiorcy.SelectedItems)
                    {
                        if (item is DataRowView row)
                        {
                            sumaIlosc += row.Row.Field<decimal>("OstIlosc");
                            sumaWartosc += row.Row.Field<decimal>("SumaWartosc");
                        }
                    }

                    txtZaznaczono.Text = $"✓ Zaznaczono: {count} odbiorców | Suma ost. ilości: {sumaIlosc:N0} kg | Suma wartości: {sumaWartosc:N0} zł";
                }
                else
                {
                    txtZaznaczono.Text = "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zaznaczenia: {ex.Message}");
            }
        }
        private void AktualizujStatystyki()
        {
            if (_dvFiltrowany == null || txtLiczbaOdbiorców == null || txtStatystykiFiltrow == null)
                return;

            try
            {
                int liczba = _dvFiltrowany.Count;
                txtLiczbaOdbiorców.Text = $"{liczba}";

                if (liczba > 0)
                {
                    decimal sumaOstIlosc = 0;
                    decimal sumaWartosc = 0;
                    int aktywnych30 = 0;
                    int aktywnych90 = 0;

                    foreach (DataRowView row in _dvFiltrowany)
                    {
                        sumaOstIlosc += row.Row.Field<decimal>("OstIlosc");
                        sumaWartosc += row.Row.Field<decimal>("SumaWartosc");
                        int dni = row.Row.Field<int>("DniTemu");
                        if (dni <= 30) aktywnych30++;
                        if (dni <= 90) aktywnych90++;
                    }

                    txtStatystykiFiltrow.Text =
                        $"📊 Suma ostatnich zamówień: {sumaOstIlosc:N0} kg | " +
                        $"💰 Suma wartości historycznych: {sumaWartosc:N0} zł | " +
                        $"🟢 Aktywni (30 dni): {aktywnych30} | " +
                        $"🟠 Aktywni (90 dni): {aktywnych90}";
                }
                else
                {
                    txtStatystykiFiltrow.Text = "Brak odbiorców spełniających kryteria";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd statystyk: {ex.Message}");
            }
        }
        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_dtOdbiorcy != null && _dvFiltrowany != null)
                ZastosujFiltry();
        }

        private void ZastosujFiltry()
        {
            if (_dtOdbiorcy == null || _dvFiltrowany == null)
                return;

            try
            {
                var filtry = new List<string>();

                // Filtr wyszukiwarki tekstowej
                if (!string.IsNullOrWhiteSpace(txtSzukaj?.Text))
                {
                    var szukany = txtSzukaj.Text.Trim().Replace("'", "''");
                    filtry.Add($"(Odbiorca LIKE '%{szukany}%' OR Handlowiec LIKE '%{szukany}%')");
                }

                // Filtr handlowca
                if (cbFiltrHandlowiec?.SelectedItem is ComboBoxItem handlowiecItem &&
                    !string.IsNullOrEmpty(handlowiecItem.Tag?.ToString()))
                {
                    filtry.Add($"Handlowiec = '{handlowiecItem.Tag.ToString().Replace("'", "''")}'");
                }

                // Filtr świeżości (tylko gdy nie "all")
                if (cbFiltrSwiezosc?.SelectedItem is ComboBoxItem swiezoscItem &&
                    swiezoscItem.Tag != null && swiezoscItem.Tag.ToString() != "all")
                {
                    if (int.TryParse(swiezoscItem.Tag.ToString(), out int dni) && dni > 0)
                    {
                        filtry.Add($"DniTemu <= {dni}");
                    }
                }

                // Filtr minimalnej ilości
                if (!string.IsNullOrEmpty(txtMinIlosc?.Text) &&
                    decimal.TryParse(txtMinIlosc.Text, out decimal minIlosc) &&
                    minIlosc > 0)
                {
                    filtry.Add($"OstIlosc >= {minIlosc}");
                }

                // Filtr minimalnej wartości
                if (!string.IsNullOrEmpty(txtMinWartosc?.Text) &&
                    decimal.TryParse(txtMinWartosc.Text, out decimal minWartosc) &&
                    minWartosc > 0)
                {
                    filtry.Add($"SumaWartosc >= {minWartosc}");
                }

                _dvFiltrowany.RowFilter = filtry.Count > 0 ? string.Join(" AND ", filtry) : "";
                AktualizujStatystyki();
            }
            catch (Exception ex)
            {
                // Ignoruj błędy podczas inicjalizacji
                System.Diagnostics.Debug.WriteLine($"Błąd filtrowania: {ex.Message}");
            }
        }
        private void CbFiltrHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_dtOdbiorcy != null && _dvFiltrowany != null)
                ZastosujFiltry();
        }

        private async void CbFiltrSwiezosc_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;

            // Gdy zmienia się okres, trzeba przeładować dane (bo SQL się zmienia)
            if (cbFiltrSwiezosc?.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                // Przeładuj dane tylko gdy zmienia się na "all" lub z "all"
                if (tag == "all" || (_dtOdbiorcy != null && _dvFiltrowany != null))
                {
                    await WczytajOdbiorcowAsync();
                }
            }
        }

        private void TxtMinIlosc_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_dtOdbiorcy != null && _dvFiltrowany != null)
                ZastosujFiltry();
        }

        private void TxtMinWartosc_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_dtOdbiorcy != null && _dvFiltrowany != null)
                ZastosujFiltry();
        }
        private void BtnResetujFiltry_Click(object sender, RoutedEventArgs e)
        {
            if (txtSzukaj != null) txtSzukaj.Text = "";
            cbFiltrHandlowiec.SelectedIndex = 0;
            cbFiltrSwiezosc.SelectedIndex = 4; // "Ostatni rok" jako domyślny
            txtMinIlosc.Text = "0";
            txtMinWartosc.Text = "0";
        }

        private void BtnKopiujListe_Click(object sender, RoutedEventArgs e)
        {
            if (_dvFiltrowany == null || _dvFiltrowany.Count == 0)
            {
                MessageBox.Show("Brak danych do skopiowania.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var lista = new StringBuilder();
            lista.AppendLine($"Potencjalni odbiorcy - {_produktNazwa}");
            lista.AppendLine($"Data: {DateTime.Now:yyyy-MM-dd HH:mm}");
            lista.AppendLine($"Znaleziono: {_dvFiltrowany.Count} odbiorców");
            lista.AppendLine();

            foreach (DataRowView row in _dvFiltrowany)
            {
                var odbiorca = row.Row.Field<string>("Odbiorca");
                var handlowiec = row.Row.Field<string>("Handlowiec");
                var ostIlosc = row.Row.Field<decimal>("OstIlosc");
                var ostData = row.Row.Field<DateTime>("OstData");

                lista.AppendLine($"• {odbiorca} ({handlowiec}) - Ostatni zakup: {ostIlosc:N0} kg ({ostData:yyyy-MM-dd})");
            }

            Clipboard.SetText(lista.ToString());
            MessageBox.Show($"Skopiowano listę {_dvFiltrowany.Count} odbiorców do schowka!",
                "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void BtnEksportuj_Click(object sender, RoutedEventArgs e)
        {
            if (_dvFiltrowany == null || _dvFiltrowany.Count == 0)
            {
                MessageBox.Show("Brak danych do eksportu.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Potencjalni_Odbiorcy_{_produktNazwa}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new StringBuilder();

                    csv.AppendLine($"Potencjalni odbiorcy - {_produktNazwa}");
                    csv.AppendLine($"Data eksportu: {DateTime.Now:yyyy-MM-dd HH:mm}");
                    csv.AppendLine($"Okres analizy: ostatnie 12 miesięcy do {_dataReferencja:yyyy-MM-dd}");
                    csv.AppendLine();

                    var headers = new List<string> {
    "Odbiorca", "Handlowiec", "Ostatnia cena", "Ostatnia ilość (kg)",
    "Ostatni zakup", "Dni temu", "Liczba transakcji", "Średnia cena",
    "Średnia ilość (kg)", "Suma wartość (zł)", "Regularność", "Limit (zł)"
};
                    csv.AppendLine(string.Join(";", headers));

                    foreach (DataRowView row in _dvFiltrowany)
                    {
                        var values = new List<string>
    {
        row.Row.Field<string>("Odbiorca"),
        row.Row.Field<string>("Handlowiec"),
        row.Row.Field<decimal>("OstCena").ToString("N2"),
        row.Row.Field<decimal>("OstIlosc").ToString("N2"),
        row.Row.Field<DateTime>("OstData").ToString("yyyy-MM-dd"),
        row.Row.Field<int>("DniTemu").ToString(),
        row.Row.Field<int>("LiczbaTransakcji").ToString(),
        row.Row.Field<decimal>("SredniaCena").ToString("N2"),
        row.Row.Field<decimal>("SredniaIlosc").ToString("N2"),
        row.Row.Field<decimal>("SumaWartosc").ToString("N2"),
        row.Row.Field<string>("Regularnosc"),
        row.Row.Field<decimal>("Limit").ToString("N2")
    };
                        csv.AppendLine(string.Join(";", values));
                    }

                    File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show("Eksport zakończony pomyślnie!", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas eksportu:\n{ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}