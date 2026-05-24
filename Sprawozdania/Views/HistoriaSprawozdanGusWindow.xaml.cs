using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Models;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class HistoriaSprawozdanGusWindow : Window
    {
        private readonly GusSubmissionsRepo _repo = new();
        public ObservableCollection<HistoriaVm> Wszystkie { get; } = new();
        public ICollectionView Widok { get; private set; }

        public HistoriaSprawozdanGusWindow()
        {
            InitializeComponent();
            Widok = CollectionViewSource.GetDefaultView(Wszystkie);
            Widok.Filter = FiltrPredicate;
            dg.ItemsSource = Widok;
            Loaded += async (s, e) => await ZaladujAsync();
        }

        private async Task ZaladujAsync()
        {
            try
            {
                var lista = await _repo.GetRecentAsync(null, 200);
                Wszystkie.Clear();
                foreach (var r in lista) Wszystkie.Add(new HistoriaVm(r));
                Widok.Refresh();
                AktualizujFooter();
                emptyState.Visibility = Wszystkie.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania historii:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AktualizujFooter()
        {
            int widoczne = Widok.Cast<object>().Count();
            int razem = Wszystkie.Count;
            lblFooter.Text = razem == widoczne
                ? $"Razem: {razem} sprawozdań"
                : $"Widoczne: {widoczne} / {razem} sprawozdań";
        }

        // ════════════════════════════════════════════════════════════════
        // FILTRY
        // ════════════════════════════════════════════════════════════════
        private void Filtr_Changed(object sender, SelectionChangedEventArgs e)
        {
            Widok?.Refresh();
            AktualizujFooter();
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            Widok?.Refresh();
            AktualizujFooter();
        }

        private bool FiltrPredicate(object obj)
        {
            if (obj is not HistoriaVm v) return false;

            // Filtr formularza
            string formFilter = (cbFiltrFormularza?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            if (!string.IsNullOrEmpty(formFilter) && !string.Equals(v.Formularz, formFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            // Filtr tekstowy
            string q = (txtFilter?.Text ?? "").Trim();
            if (q.Length == 0) return true;
            return (v.Formularz?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.OkresText?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.Status?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.GeneratedByImie?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                || (v.NumerWPortalu?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await ZaladujAsync();

        // ════════════════════════════════════════════════════════════════
        // AKCJE PER WIERSZ
        // ════════════════════════════════════════════════════════════════
        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not HistoriaVm v) return;
            if (string.IsNullOrWhiteSpace(v.PlikXml) || !File.Exists(v.PlikXml))
            {
                MessageBox.Show("Plik XML nie istnieje na dysku.\nMógł zostać przeniesiony lub usunięty.",
                    "Plik nie znaleziony", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try { Process.Start("explorer.exe", $"/select,\"{v.PlikXml}\""); }
            catch (Exception ex) { MessageBox.Show("Błąd otwarcia: " + ex.Message); }
        }

        private void BtnViewXml_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not HistoriaVm v) return;
            if (string.IsNullOrEmpty(v.GeneratedXml))
            {
                MessageBox.Show("Brak treści XML w bazie.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new Window
            {
                Title = $"XML — {v.Formularz} {v.OkresText}",
                Width = 1200, Height = 750,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox
                {
                    Text = v.GeneratedXml,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Padding = new Thickness(10)
                }
            };
            dlg.ShowDialog();
        }

        private async void BtnMarkSent_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not HistoriaVm v) return;
            var dlg = new MarkAsSentDialog(v.OkresText) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    await _repo.UpdateStatusAsync(v.Id, "Sent",
                        sentAt: DateTime.Now,
                        numerWPortalu: dlg.NumerWPortalu);
                    await ZaladujAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd zapisu: " + ex.Message, "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Dg_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dg.SelectedItem is HistoriaVm v)
                BtnViewXml_Click(new Button { DataContext = v }, e);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    // ════════════════════════════════════════════════════════════════════
    // ViewModel z statusem jako pill
    // ════════════════════════════════════════════════════════════════════
    public class HistoriaVm
    {
        public int Id { get; }
        public string Formularz { get; }
        public DateTime OkresOd { get; }
        public DateTime OkresDo { get; }
        public string OkresText { get; }
        public string Status { get; }
        public string StatusLabel { get; }
        public Brush StatusBg { get; }
        public Brush StatusFg { get; }
        public int IloscPozycji { get; }
        public decimal SumaWartosc { get; }
        public string? GeneratedByImie { get; }
        public DateTime GeneratedAt { get; }
        public string? GeneratedXml { get; }
        public string? PlikXml { get; }
        public string? NumerWPortalu { get; }
        public Visibility ShowMarkSent { get; }

        public HistoriaVm(GusSubmissionRow r)
        {
            Id = r.Id;
            Formularz = r.Formularz;
            OkresOd = r.OkresOd;
            OkresDo = r.OkresDo;
            Status = r.Status;
            IloscPozycji = r.IloscPozycji;
            SumaWartosc = r.SumaWartosc;
            GeneratedByImie = r.GeneratedByImie;
            GeneratedAt = r.GeneratedAt;
            GeneratedXml = r.GeneratedXml;
            PlikXml = r.PlikXml;
            NumerWPortalu = r.NumerWPortalu;

            // Format okresu
            OkresText = r.Miesiac.HasValue
                ? $"{NazwaMiesiacaSkr(r.Miesiac.Value)} {r.Rok}"
                : r.Rok.ToString();

            // Status pill — kolor + label
            (string bg, string fg, string label) = r.Status switch
            {
                "Sent" => ("#DCFCE7", "#15803D", "✓ Wysłane"),
                "Generated" => ("#DBEAFE", "#1E40AF", "📄 Wygenerowane"),
                "Exported" => ("#E0E7FF", "#3730A3", "📤 Wyeksportowane"),
                "Failed" => ("#FEE2E2", "#B91C1C", "✗ Błąd"),
                "Draft" => ("#F0F2F5", "#374151", "📝 Szkic"),
                _ => ("#F0F2F5", "#374151", r.Status)
            };
            StatusBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            StatusFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
            StatusLabel = label;

            ShowMarkSent = r.Status == "Generated" ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string NazwaMiesiacaSkr(int m)
        {
            string[] n = { "", "I","II","III","IV","V","VI","VII","VIII","IX","X","XI","XII" };
            return (m >= 1 && m <= 12) ? n[m] : $"m{m}";
        }
    }
}
