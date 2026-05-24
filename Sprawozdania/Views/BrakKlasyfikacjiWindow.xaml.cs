using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class BrakKlasyfikacjiWindow : Window
    {
        public ObservableCollection<P02DataService.TowarBezKlasyfikacji> Wszystkie { get; } = new();
        public ICollectionView Widok { get; private set; }

        private static readonly CultureInfo Pl = new("pl-PL");

        public BrakKlasyfikacjiWindow(IEnumerable<P02DataService.TowarBezKlasyfikacji> dane)
        {
            InitializeComponent();
            foreach (var d in dane) Wszystkie.Add(d);
            Widok = CollectionViewSource.GetDefaultView(Wszystkie);
            Widok.Filter = FiltrPredicate;
            dg.ItemsSource = Widok;

            decimal sumaKg = Wszystkie.Sum(x => x.Kg);
            lblSummary.Text = $"⚠ {Wszystkie.Count} towarów · {sumaKg.ToString("N0", Pl)} kg poza klasyfikacją";
            AktualizujFooter();
        }

        private void AktualizujFooter()
        {
            int wid = Widok.Cast<object>().Count();
            lblFooter.Text = wid == Wszystkie.Count
                ? $"Razem: {Wszystkie.Count} pozycji"
                : $"Widoczne: {wid} / {Wszystkie.Count}";
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            Widok.Refresh();
            AktualizujFooter();
        }

        private bool FiltrPredicate(object obj)
        {
            string q = (txtFilter?.Text ?? "").Trim();
            if (q.Length == 0) return true;
            if (obj is not P02DataService.TowarBezKlasyfikacji t) return false;
            return (t.Kod?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (t.Nazwa?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (t.Sww?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Kod\tNazwa\tKatalog\tSWW\tKg");
            foreach (var t in Widok.Cast<P02DataService.TowarBezKlasyfikacji>())
                sb.AppendLine($"{t.Kod}\t{t.Nazwa}\t{t.Katalog}\t{t.Sww}\t{t.Kg.ToString("F0", CultureInfo.InvariantCulture)}");
            try
            {
                Clipboard.SetText(sb.ToString());
                lblFooter.Text = "✓ Skopiowano do schowka";
            }
            catch (Exception ex) { MessageBox.Show("Błąd kopiowania: " + ex.Message); }
        }

        private void BtnCsv_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"P02_bez_klasyfikacji_{DateTime.Today:yyyy-MM-dd}.csv"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Kod;Nazwa;Katalog;SWW;Kg");
                foreach (var t in Widok.Cast<P02DataService.TowarBezKlasyfikacji>())
                    sb.AppendLine($"{Csv(t.Kod)};{Csv(t.Nazwa)};{t.Katalog};{Csv(t.Sww)};{t.Kg.ToString("F0", CultureInfo.InvariantCulture)}");
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                lblFooter.Text = "✓ Zapisano CSV";
            }
            catch (Exception ex) { MessageBox.Show("Błąd zapisu: " + ex.Message); }
        }

        private static string Csv(string? s) =>
            string.IsNullOrEmpty(s) ? "" : (s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s);

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
