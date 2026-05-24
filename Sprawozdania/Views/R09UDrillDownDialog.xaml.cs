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
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public enum R09UDrillTyp
    {
        Partie,         // r1-r4: dane z FarmerCalc
        FakturyZywca    // r5: wartość, faktury HANDEL
    }

    public partial class R09UDrillDownDialog : Window
    {
        private readonly R09UDataService _svc = new();
        private readonly R09UDrillTyp _typ;
        private readonly int _rok;
        private readonly int _miesiac;
        private readonly string _kolumnaLabel;

        public ObservableCollection<PartiaVm> Partie { get; } = new();
        public ObservableCollection<FakturaVm> Faktury { get; } = new();
        public ICollectionView WidokPartii { get; private set; } = null!;
        public ICollectionView WidokFaktur { get; private set; } = null!;

        private static readonly CultureInfo Pl = new("pl-PL");

        public R09UDrillDownDialog(R09UDrillTyp typ, int rok, int miesiac, string kolumnaLabel)
        {
            InitializeComponent();
            _typ = typ; _rok = rok; _miesiac = miesiac; _kolumnaLabel = kolumnaLabel;

            string ikona = typ == R09UDrillTyp.FakturyZywca ? "💰" : "🐔";
            Title = $"{ikona}  R-09U — {kolumnaLabel}";
            lblTitle.Text = $"{ikona}  R-09U · kolumna: {kolumnaLabel}";
            lblSubtitle.Text = $"Okres: {NazwaMc(miesiac)} {rok} · " +
                (typ == R09UDrillTyp.FakturyZywca
                    ? "faktury żywca FVZ+FVR+FKZ z HANDEL"
                    : "partie z LibraNet.FarmerCalc");

            if (typ == R09UDrillTyp.FakturyZywca)
            {
                dgPartie.Visibility = Visibility.Collapsed;
                frameFaktury.Visibility = Visibility.Visible;
                WidokFaktur = CollectionViewSource.GetDefaultView(Faktury);
                WidokFaktur.Filter = FaktFiltr;
                dgFaktury.ItemsSource = WidokFaktur;
            }
            else
            {
                dgPartie.Visibility = Visibility.Visible;
                frameFaktury.Visibility = Visibility.Collapsed;
                WidokPartii = CollectionViewSource.GetDefaultView(Partie);
                WidokPartii.Filter = PartFiltr;
                dgPartie.ItemsSource = WidokPartii;
            }

            Loaded += async (s, e) => await PobierzAsync();
        }

        private async Task PobierzAsync()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                if (_typ == R09UDrillTyp.FakturyZywca)
                {
                    var lista = await _svc.PobierzFakturyZywcaAsync(_rok, _miesiac);
                    Faktury.Clear();
                    foreach (var f in lista) Faktury.Add(new FakturaVm(f));
                    int dok = Faktury.Select(f => f.Numer).Distinct().Count();
                    decimal sumKg = Faktury.Sum(f => f.Kg);
                    decimal sumZl = Faktury.Sum(f => f.Wartosc);
                    lblSummary.Text = $"📊 {dok} faktur · {Faktury.Count} pozycji · {sumKg.ToString("N0", Pl)} kg · {sumZl.ToString("N0", Pl)} zł";
                    lblFooterRight.Text = $"Razem: {Faktury.Count} pozycji";
                }
                else
                {
                    var lista = await _svc.PobierzPartieAsync(_rok, _miesiac);
                    Partie.Clear();
                    foreach (var p in lista) Partie.Add(new PartiaVm(p));
                    int sumSzt = Partie.Sum(p => p.Sztuki);
                    decimal sumKgF = Partie.Sum(p => p.WagaFarmKg);
                    decimal sumKgU = Partie.Sum(p => p.WagaUbojniaKg);
                    lblSummary.Text = $"📊 {Partie.Count} partii · {sumSzt.ToString("N0", Pl)} szt · " +
                                      $"{sumKgF.ToString("N0", Pl)} kg farm · {sumKgU.ToString("N0", Pl)} kg ubojnia";
                    lblFooterRight.Text = $"Razem: {Partie.Count} partii";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_typ == R09UDrillTyp.FakturyZywca) WidokFaktur.Refresh();
            else WidokPartii.Refresh();
        }

        private bool PartFiltr(object obj)
        {
            string q = (txtFilter?.Text ?? "").Trim();
            if (q.Length == 0) return true;
            if (obj is not PartiaVm v) return false;
            return (v.Hodowca?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.LpDostawy?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool FaktFiltr(object obj)
        {
            string q = (txtFilter?.Text ?? "").Trim();
            if (q.Length == 0) return true;
            if (obj is not FakturaVm v) return false;
            return (v.Kontrahent?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.Numer?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.KodTowaru?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            if (_typ == R09UDrillTyp.FakturyZywca)
            {
                sb.AppendLine("Data\tTyp\tNumer\tKontrahent\tKod\tNazwa\tKg\tWartość");
                foreach (var v in WidokFaktur.Cast<FakturaVm>())
                    sb.AppendLine($"{v.Data:yyyy-MM-dd}\t{v.TypDk}\t{v.Numer}\t{v.Kontrahent}\t{v.KodTowaru}\t{v.NazwaTowaru}\t{v.Kg.ToString("F2", CultureInfo.InvariantCulture)}\t{v.Wartosc.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            else
            {
                sb.AppendLine("Data\tPartia\tHodowca\tSztuki\tFarmKg\tUbojniaKg\tCena");
                foreach (var v in WidokPartii.Cast<PartiaVm>())
                    sb.AppendLine($"{v.Data:yyyy-MM-dd}\t{v.LpDostawy}\t{v.Hodowca}\t{v.Sztuki}\t{v.WagaFarmKg.ToString("F2", CultureInfo.InvariantCulture)}\t{v.WagaUbojniaKg.ToString("F2", CultureInfo.InvariantCulture)}\t{v.CenaZlKg.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                lblFooter.Text = "✓ Skopiowano do schowka (TSV)";
            }
            catch (Exception ex) { MessageBox.Show("Błąd kopiowania: " + ex.Message); }
        }

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"R09U_{_typ}_{_rok:D4}_{_miesiac:D2}.csv"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var sb = new StringBuilder();
                if (_typ == R09UDrillTyp.FakturyZywca)
                {
                    sb.AppendLine("Data;Typ;Numer;Kontrahent;Kod;Nazwa;Kg;Wartosc");
                    foreach (var v in WidokFaktur.Cast<FakturaVm>())
                        sb.AppendLine($"{v.Data:yyyy-MM-dd};{v.TypDk};{Csv(v.Numer)};{Csv(v.Kontrahent)};{Csv(v.KodTowaru)};{Csv(v.NazwaTowaru)};{v.Kg.ToString("F2", CultureInfo.InvariantCulture)};{v.Wartosc.ToString("F2", CultureInfo.InvariantCulture)}");
                }
                else
                {
                    sb.AppendLine("Data;Partia;Hodowca;Sztuki;FarmKg;UbojniaKg;Cena");
                    foreach (var v in WidokPartii.Cast<PartiaVm>())
                        sb.AppendLine($"{v.Data:yyyy-MM-dd};{Csv(v.LpDostawy)};{Csv(v.Hodowca)};{v.Sztuki};{v.WagaFarmKg.ToString("F2", CultureInfo.InvariantCulture)};{v.WagaUbojniaKg.ToString("F2", CultureInfo.InvariantCulture)};{v.CenaZlKg.ToString("F2", CultureInfo.InvariantCulture)}");
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                lblFooter.Text = "✓ Zapisano CSV";
            }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
        }

        private static string Csv(string? s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private static string NazwaMc(int m)
        {
            string[] n = { "", "styczniu","lutym","marcu","kwietniu","maju","czerwcu",
                "lipcu","sierpniu","wrześniu","październiku","listopadzie","grudniu" };
            return (m >= 1 && m <= 12) ? n[m] : $"mc{m}";
        }
    }

    public class PartiaVm
    {
        public DateTime Data { get; }
        public string LpDostawy { get; }
        public string Hodowca { get; }
        public int Sztuki { get; }
        public decimal WagaFarmKg { get; }
        public decimal WagaUbojniaKg { get; }
        public decimal CenaZlKg { get; }
        public PartiaVm(R09UDataService.R09UPartiaRow r)
        {
            Data = r.Data; LpDostawy = r.LpDostawy; Hodowca = r.Hodowca;
            Sztuki = r.Sztuki; WagaFarmKg = r.WagaFarmKg; WagaUbojniaKg = r.WagaUbojniaKg; CenaZlKg = r.CenaZlKg;
        }
    }

    public class FakturaVm
    {
        public DateTime Data { get; }
        public string TypDk { get; }
        public string Numer { get; }
        public string Kontrahent { get; }
        public string KodTowaru { get; }
        public string NazwaTowaru { get; }
        public decimal Kg { get; }
        public decimal Wartosc { get; }
        public Brush TypBg { get; }
        public Brush TypFg { get; }
        public FakturaVm(R09UDataService.R09UFakturaRow r)
        {
            Data = r.Data; TypDk = r.TypDk; Numer = r.Numer; Kontrahent = r.Kontrahent;
            KodTowaru = r.KodTowaru; NazwaTowaru = r.NazwaTowaru; Kg = r.Kg; Wartosc = r.Wartosc;
            (string bg, string fg) = TypDk switch
            {
                "FVZ" => ("#DBEAFE", "#1E40AF"),
                "FVR" => ("#DCFCE7", "#15803D"),
                "FKZ" => ("#FEF3C7", "#92400E"),
                _ => ("#F0F2F5", "#374151")
            };
            TypBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            TypFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
        }
    }
}
