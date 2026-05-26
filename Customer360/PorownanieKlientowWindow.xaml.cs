using Kalendarz1.Customer360.Models;
using Kalendarz1.Customer360.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.Customer360
{
    public partial class PorownanieKlientowWindow : Window
    {
        private readonly Customer360Service _service = new();
        private static readonly CultureInfo Pl = new("pl-PL");
        private readonly int _idA;
        private KlientKpi? _kpiA, _kpiB;
        private int? _idB;
        // Scoring bierzemy z kpi.Score (Customer360Score) — bez osobnego zapytania

        public PorownanieKlientowWindow(int idA, string nazwaA)
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            _idA = idA;
            LblA.Text = nazwaA;
            Loaded += async (s, e) => { await WczytajA(); Render(); };
        }

        private async Task WczytajA()
        {
            _kpiA = await _service.GetKpiAsync(_idA);
        }

        private async void BtnWybierzB_Click(object sender, RoutedEventArgs e)
        {
            var pick = new KlientPickerDialog { Owner = this };
            if (pick.ShowDialog() == true && pick.Selected != null)
            {
                _idB = pick.Selected.Id;
                LblB.Text = pick.Selected.Nazwa;
                Cursor = System.Windows.Input.Cursors.Wait;
                try
                {
                    _kpiB = await _service.GetKpiAsync(_idB.Value);
                }
                finally { Cursor = System.Windows.Input.Cursors.Arrow; }
                Render();
            }
        }

        private void Render()
        {
            GridPorownanie.Children.Clear();
            GridPorownanie.RowDefinitions.Clear();
            GridPorownanie.ColumnDefinitions.Clear();
            GridPorownanie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            GridPorownanie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            GridPorownanie.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            var bc = new BrushConverter();
            Brush B(string hex) { try { return (Brush)bc.ConvertFromString(hex)!; } catch { return Brushes.Gray; } }

            // wiersz: etykieta, wartośćA, wartośćB; lepszy = pogrubiony zielony
            void Wiersz(string etykieta, string a, string b, double? rawA = null, double? rawB = null, bool wyzejLepiej = true)
            {
                GridPorownanie.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var bg = row % 2 == 0 ? "#FFFFFF" : "#F8FAFC";

                var lbl = new Border { Background = B(bg), Padding = new Thickness(10, 8, 10, 8) };
                lbl.Child = new TextBlock { Text = etykieta, FontSize = 12, Foreground = B("#475569"), FontWeight = FontWeights.SemiBold };
                Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0); GridPorownanie.Children.Add(lbl);

                bool aLepszy = false, bLepszy = false;
                if (rawA.HasValue && rawB.HasValue && Math.Abs(rawA.Value - rawB.Value) > 0.0001)
                {
                    bool aWieksze = rawA.Value > rawB.Value;
                    aLepszy = wyzejLepiej ? aWieksze : !aWieksze;
                    bLepszy = !aLepszy;
                }

                Border Cell(string txt, bool lepszy)
                {
                    var bdr = new Border { Background = B(bg), Padding = new Thickness(10, 8, 10, 8) };
                    bdr.Child = new TextBlock
                    {
                        Text = txt, FontSize = 13,
                        FontWeight = lepszy ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = lepszy ? B("#15803D") : B("#0F172A")
                    };
                    return bdr;
                }
                var ca = Cell(a, aLepszy); Grid.SetRow(ca, row); Grid.SetColumn(ca, 1); GridPorownanie.Children.Add(ca);
                var cb = Cell(string.IsNullOrEmpty(b) ? "—" : b, bLepszy); Grid.SetRow(cb, row); Grid.SetColumn(cb, 2); GridPorownanie.Children.Add(cb);
                row++;
            }

            string Zl(decimal v) => $"{v:N0} zł";
            var a = _kpiA; var b = _kpiB;
            bool hasB = b != null;

            Wiersz("Obrót 12M", Zl(a?.Obrot12M ?? 0), hasB ? Zl(b!.Obrot12M) : "", (double?)(a?.Obrot12M ?? 0), hasB ? (double?)b!.Obrot12M : null, true);
            Wiersz("Śr. wartość faktury", Zl(a != null && a.LiczbaFaktur12M > 0 ? a.Obrot12M / a.LiczbaFaktur12M : 0),
                   hasB ? Zl(b!.LiczbaFaktur12M > 0 ? b.Obrot12M / b.LiczbaFaktur12M : 0) : "",
                   (double?)(a != null && a.LiczbaFaktur12M > 0 ? a.Obrot12M / a.LiczbaFaktur12M : 0),
                   hasB ? (double?)(b!.LiczbaFaktur12M > 0 ? b.Obrot12M / b.LiczbaFaktur12M : 0) : null, true);
            Wiersz("Zamówień 12M", $"{a?.LiczbaZamowien12M ?? 0}", hasB ? $"{b!.LiczbaZamowien12M}" : "", a?.LiczbaZamowien12M, hasB ? b!.LiczbaZamowien12M : (int?)null, true);
            Wiersz("Suma kg 12M", $"{a?.SumaKg12M ?? 0:N0} kg", hasB ? $"{b!.SumaKg12M:N0} kg" : "", (double?)(a?.SumaKg12M ?? 0), hasB ? (double?)b!.SumaKg12M : null, true);
            Wiersz("Dni od ost. zamówienia", a?.OstatnieZamowienie.HasValue == true ? $"{a.DniOdOstatniegoZamowienia} dni" : "brak",
                   hasB ? (b!.OstatnieZamowienie.HasValue ? $"{b.DniOdOstatniegoZamowienia} dni" : "brak") : "",
                   a?.OstatnieZamowienie.HasValue == true ? a.DniOdOstatniegoZamowienia : (double?)null,
                   hasB && b!.OstatnieZamowienie.HasValue ? b.DniOdOstatniegoZamowienia : (double?)null, false);
            Wiersz("Limit kredytowy", Zl(a?.LimitKredytowy ?? 0), hasB ? Zl(b!.LimitKredytowy) : "", null, null);
            Wiersz("Do zapłaty", Zl(a?.DoZaplaty ?? 0), hasB ? Zl(b!.DoZaplaty) : "", (double?)(a?.DoZaplaty ?? 0), hasB ? (double?)b!.DoZaplaty : null, false);
            Wiersz("Przeterminowane", Zl(a?.Przeterminowane ?? 0), hasB ? Zl(b!.Przeterminowane) : "", (double?)(a?.Przeterminowane ?? 0), hasB ? (double?)b!.Przeterminowane : null, false);
            Wiersz("Reklamacje 12M", $"{a?.LiczbaReklamacji12M ?? 0}", hasB ? $"{b!.LiczbaReklamacji12M}" : "", a?.LiczbaReklamacji12M, hasB ? b!.LiczbaReklamacji12M : (int?)null, false);
            var scA = _kpiA?.Score; var scB = _kpiB?.Score;
            Wiersz("Scoring", scA != null ? $"{scA.Total}/100 ({scA.Litera} {scA.Kategoria})" : "—",
                   scB != null ? $"{scB.Total}/100 ({scB.Litera} {scB.Kategoria})" : "",
                   scA?.Total, scB?.Total, true);
            Wiersz("Churn", a?.ChurnRiskLevel ?? "—", hasB ? (b!.ChurnRiskLevel ?? "—") : "");
        }
    }
}
