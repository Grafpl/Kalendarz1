using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Models;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public enum DrillDownTyp
    {
        SprzedazMc,
        SprzedazYtd,
        ProdukcjaMc,
        ProdukcjaYtd
    }

    public partial class DokumentyDrillDownDialog : Window
    {
        private readonly P02DataService _svc = new();
        private readonly string _pkwiu;
        private readonly DrillDownTyp _typ;
        private readonly int _rok;
        private readonly int _miesiac;

        public ObservableCollection<DokumentVm> Wszystkie { get; } = new();
        public ICollectionView Widok { get; private set; }

        private static readonly CultureInfo Pl = new("pl-PL");

        public DokumentyDrillDownDialog(string pkwiu, DrillDownTyp typ, int rok, int miesiac)
        {
            InitializeComponent();
            _pkwiu = pkwiu;
            _typ = typ;
            _rok = rok;
            _miesiac = miesiac;

            Widok = CollectionViewSource.GetDefaultView(Wszystkie);
            Widok.Filter = FiltrPredicate;
            dgDokumenty.ItemsSource = Widok;

            // Schowaj kolumnę wartości dla produkcji (jest zawsze 0)
            colWartosc.Visibility = (typ == DrillDownTyp.ProdukcjaMc || typ == DrillDownTyp.ProdukcjaYtd)
                ? Visibility.Collapsed : Visibility.Visible;

            UstawTytul();
            Loaded += async (s, e) => await PobierzAsync();
        }

        private void UstawTytul()
        {
            string nazwaWyrobu = PkwiuKlasyfikator.NazwyWyrobow.TryGetValue(_pkwiu, out var n) ? n : _pkwiu;
            string typLabel = _typ switch
            {
                DrillDownTyp.SprzedazMc => $"Sprzedaż w {NazwaMiesiaca(_miesiac)} {_rok}",
                DrillDownTyp.SprzedazYtd => $"Sprzedaż 01-styczeń → {NazwaMiesiaca(_miesiac)} {_rok}",
                DrillDownTyp.ProdukcjaMc => $"Produkcja w {NazwaMiesiaca(_miesiac)} {_rok}",
                DrillDownTyp.ProdukcjaYtd => $"Produkcja 01-styczeń → {NazwaMiesiaca(_miesiac)} {_rok}",
                _ => ""
            };
            string ikona = _typ switch
            {
                DrillDownTyp.SprzedazMc or DrillDownTyp.SprzedazYtd => "💰",
                _ => "🏭"
            };
            Title = $"{ikona}  {_pkwiu} — {typLabel}";
            lblTitle.Text = $"{ikona}  {_pkwiu}  ·  {typLabel}";
            lblSubtitle.Text = nazwaWyrobu;
        }

        private (DateTime od, DateTime doD) AktualnyZakres() => _typ switch
        {
            DrillDownTyp.SprzedazYtd or DrillDownTyp.ProdukcjaYtd =>
                (new DateTime(_rok, 1, 1), new DateTime(_rok, _miesiac, 1).AddMonths(1).AddDays(-1)),
            _ => (new DateTime(_rok, _miesiac, 1), new DateTime(_rok, _miesiac, 1).AddMonths(1).AddDays(-1))
        };

        private async Task PobierzAsync()
        {
            try
            {
                var (od, doD) = AktualnyZakres();
                Mouse.OverrideCursor = Cursors.Wait;

                List<P02DokumentRow> dane;
                if (_typ == DrillDownTyp.SprzedazMc || _typ == DrillDownTyp.SprzedazYtd)
                    dane = await _svc.PobierzDokumentySprzedazyAsync(_pkwiu, od, doD);
                else
                    dane = await _svc.PobierzDokumentyProdukcjiAsync(_pkwiu, od, doD);

                Wszystkie.Clear();
                foreach (var d in dane) Wszystkie.Add(new DokumentVm(d));

                int liczbaDok = Wszystkie.Select(x => x.NumerDokumentu).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                decimal sumaKg = Wszystkie.Sum(x => x.IloscKg);
                decimal sumaZl = Wszystkie.Sum(x => x.WartoscNetto);

                lblSummary.Text = $"📊 {liczbaDok} dok.  ·  {Wszystkie.Count} pozycji  ·  {sumaKg.ToString("N0", Pl)} kg" +
                                  (sumaZl > 0 ? $"  ·  {sumaZl.ToString("N0", Pl)} zł" : "");
                lblFooterRight.Text = $"Razem: {Wszystkie.Count} pozycji";

                ZbudujDailyStrip();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania dokumentów:\n" + ex.Message,
                    "Błąd SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // DAILY STRIP — mini-wykres słupkowy ilości per dzień w okresie
        // ════════════════════════════════════════════════════════════════
        private void ZbudujDailyStrip()
        {
            var (od, doD) = AktualnyZakres();
            int dni = (doD - od).Days + 1;
            if (dni <= 1 || Wszystkie.Count == 0)
            {
                dailyStripPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Agregacja kg per dzień
            var perDay = new decimal[dni];
            foreach (var v in Wszystkie)
            {
                int idx = (v.Data.Date - od).Days;
                if (idx >= 0 && idx < dni) perDay[idx] += v.IloscKg;
            }
            decimal max = perDay.Length > 0 ? perDay.Max() : 0;
            if (max <= 0)
            {
                dailyStripPanel.Visibility = Visibility.Collapsed;
                return;
            }

            // Kolor zależny od typu (sprzedaż=niebieski, produkcja=zielony)
            string barHex = (_typ == DrillDownTyp.SprzedazMc || _typ == DrillDownTyp.SprzedazYtd)
                ? "#3B82F6" : "#10B981";
            string barHexZero = "#F1F5F9";

            var items = new List<DailyBarVm>();
            const double maxH = 38.0;
            for (int i = 0; i < dni; i++)
            {
                var d = od.AddDays(i);
                decimal kg = perDay[i];
                double h = (double)(kg / max) * maxH;
                if (h < 1 && kg > 0) h = 2;
                string color = kg > 0 ? barHex : barHexZero;
                string label = (dni <= 31) ? d.Day.ToString() :
                               (i == 0 || i == dni - 1 || i % Math.Max(1, dni / 15) == 0) ? d.ToString("dd.MM") : "";
                string ttip = $"{d:dddd, dd MMMM yyyy}\n{kg:N0} kg" + (kg <= 0 ? "  (brak)" : "");
                items.Add(new DailyBarVm
                {
                    DayLabel = label,
                    BarHeight = Math.Max(1, h),
                    Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                    Tooltip = ttip
                });
            }
            dailyStrip.ItemsSource = items;
            lblDailyMax.Text = $"max: {max.ToString("N0", Pl)} kg/dzień";
            dailyStripPanel.Visibility = Visibility.Visible;
        }

        // ════════════════════════════════════════════════════════════════
        // FILTER
        // ════════════════════════════════════════════════════════════════
        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            Widok.Refresh();
            int filtrowane = Widok.Cast<object>().Count();
            lblFooterRight.Text = filtrowane == Wszystkie.Count
                ? $"Razem: {Wszystkie.Count} pozycji"
                : $"Widoczne: {filtrowane} / {Wszystkie.Count} pozycji";
        }

        private bool FiltrPredicate(object obj)
        {
            string q = (txtFilter?.Text ?? "").Trim();
            if (q.Length == 0) return true;
            if (obj is not DokumentVm v) return false;
            return (v.KodTowaru?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.NazwaTowaru?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.Kontrahent?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.NumerDokumentu?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ════════════════════════════════════════════════════════════════
        // COPY / CSV
        // ════════════════════════════════════════════════════════════════
        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Data\tTyp\tNumer\tKontrahent\tKod\tNazwa\tIlość kg\tWartość zł");
            foreach (var v in Widok.Cast<DokumentVm>())
            {
                sb.Append(v.Data.ToString("yyyy-MM-dd")).Append('\t')
                  .Append(v.TypDokumentu).Append('\t')
                  .Append(v.NumerDokumentu).Append('\t')
                  .Append(v.Kontrahent ?? "").Append('\t')
                  .Append(v.KodTowaru).Append('\t')
                  .Append(v.NazwaTowaru).Append('\t')
                  .Append(v.IloscKg.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(v.WartoscNetto.ToString("F2", CultureInfo.InvariantCulture))
                  .AppendLine();
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                lblFooter.Text = $"✓ Skopiowano {Widok.Cast<object>().Count()} pozycji do schowka (TSV)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd kopiowania: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"P02_{_pkwiu}_{_typ}_{_rok:D4}-{_miesiac:D2}.csv".Replace("-", "_")
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Data;Typ;Numer;Kontrahent;Kod;Nazwa;Ilosc_kg;Wartosc_zl");
                foreach (var v in Widok.Cast<DokumentVm>())
                {
                    sb.Append(v.Data.ToString("yyyy-MM-dd")).Append(';')
                      .Append(v.TypDokumentu).Append(';')
                      .Append(Csv(v.NumerDokumentu)).Append(';')
                      .Append(Csv(v.Kontrahent ?? "")).Append(';')
                      .Append(Csv(v.KodTowaru)).Append(';')
                      .Append(Csv(v.NazwaTowaru)).Append(';')
                      .Append(v.IloscKg.ToString("F2", CultureInfo.InvariantCulture)).Append(';')
                      .Append(v.WartoscNetto.ToString("F2", CultureInfo.InvariantCulture))
                      .AppendLine();
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                lblFooter.Text = $"✓ Zapisano {Widok.Cast<object>().Count()} pozycji do CSV";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu CSV: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string Csv(string? s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private static string NazwaMiesiaca(int m)
        {
            string[] n = { "", "styczniu","lutym","marcu","kwietniu","maju","czerwcu",
                "lipcu","sierpniu","wrześniu","październiku","listopadzie","grudniu" };
            return (m >= 1 && m <= 12) ? n[m] : $"m-cu {m}";
        }
    }

    // ViewModel dla słupka w daily strip
    public class DailyBarVm
    {
        public string DayLabel { get; set; } = "";
        public double BarHeight { get; set; }
        public Brush Color { get; set; } = Brushes.Transparent;
        public string Tooltip { get; set; } = "";
    }

    // ════════════════════════════════════════════════════════════════════
    // ViewModel z kolorowaniem typu dokumentu
    // ════════════════════════════════════════════════════════════════════
    public class DokumentVm
    {
        public DateTime Data { get; }
        public string TypDokumentu { get; }
        public string NumerDokumentu { get; }
        public string? Kontrahent { get; }
        public string KodTowaru { get; }
        public string NazwaTowaru { get; }
        public decimal IloscKg { get; }
        public decimal WartoscNetto { get; }

        public Brush TypBackground { get; }
        public Brush TypForeground { get; }

        public DokumentVm(P02DokumentRow r)
        {
            Data = r.Data;
            TypDokumentu = r.TypDokumentu;
            NumerDokumentu = r.NumerDokumentu;
            Kontrahent = r.Kontrahent;
            KodTowaru = r.KodTowaru;
            NazwaTowaru = r.NazwaTowaru;
            IloscKg = r.IloscKg;
            WartoscNetto = r.WartoscNetto;

            // Kolory pill per typ dokumentu
            (string bg, string fg) = TypDokumentu switch
            {
                "FVS" => ("#DCFCE7", "#15803D"),  // zielony — faktura sprzedaży
                "FKS" => ("#FEF3C7", "#92400E"),  // żółty — korekta sprzedaży
                "PWU" => ("#DBEAFE", "#1E40AF"),  // niebieski — przyjęcie z uboju
                "PWP" => ("#EDE9FE", "#5B21B6"),  // fiolet — przyjęcie z produkcji
                "PWK" => ("#FCE7F3", "#9D174D"),  // róż — przyjęcie korygujące
                _ => ("#F0F2F5", "#374151")
            };
            TypBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            TypForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
        }
    }
}
