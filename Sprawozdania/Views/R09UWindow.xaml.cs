using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Models;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class R09UWindow : Window
    {
        // RoutedCommands dla skrótów F5/Ctrl+G/P/H/Left/Right/Esc
        public static readonly System.Windows.Input.RoutedCommand LoadCmd = new();
        public static readonly System.Windows.Input.RoutedCommand GenerateCmd = new();
        public static readonly System.Windows.Input.RoutedCommand PreviewCmd = new();
        public static readonly System.Windows.Input.RoutedCommand HistoryCmd = new();
        public static readonly System.Windows.Input.RoutedCommand PrevMonthCmd = new();
        public static readonly System.Windows.Input.RoutedCommand NextMonthCmd = new();
        public static readonly System.Windows.Input.RoutedCommand CloseCmd = new();

        private readonly R09UDataService _data = new();
        private readonly R09UXmlGenerator _gen = new();
        private readonly GusSubmissionsRepo _repo = new();
        private readonly R09UValidator _validator = new();

        public ObservableCollection<R09URowVm> Pozycje { get; } = new();
        private static readonly CultureInfo Pl = new("pl-PL");

        public R09UWindow()
        {
            InitializeComponent();
            dg.ItemsSource = Pozycje;

            foreach (var m in new[] {
                "styczeń","luty","marzec","kwiecień","maj","czerwiec",
                "lipiec","sierpień","wrzesień","październik","listopad","grudzień"
            })
                cbMiesiac.Items.Add(m);

            int currentRok = DateTime.Today.Year;
            for (int r = currentRok - 5; r <= currentRok + 1; r++) cbRok.Items.Add(r);

            var prev = DateTime.Today.AddMonths(-1);
            cbMiesiac.SelectedIndex = prev.Month - 1;
            cbRok.SelectedItem = prev.Year;

            Pozycje.CollectionChanged += (s, e) => { OdswiezKarty(); OdswiezEmptyState(); };

            CommandBindings.Add(new System.Windows.Input.CommandBinding(LoadCmd, (s, e) => BtnLoad_Click(s, e)));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(GenerateCmd, (s, e) => BtnGenerate_Click(s, e)));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(PreviewCmd, (s, e) => BtnPreviewXml_Click(s, e)));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(HistoryCmd, (s, e) => BtnHistory_Click(s, e)));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(PrevMonthCmd, (s, e) => PrzesunMiesiac(-1)));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(NextMonthCmd, (s, e) => PrzesunMiesiac(+1)));
            CommandBindings.Add(new System.Windows.Input.CommandBinding(CloseCmd, (s, e) => Close()));

            SourceInitialized += (s, e) =>
            {
                var wa = SystemParameters.WorkArea;
                Left = wa.Left; Top = wa.Top;
                Width = wa.Width; Height = wa.Height;
            };
        }

        private void Picker_Changed(object sender, SelectionChangedEventArgs e) { }
        private void BtnPrevMonth_Click(object sender, RoutedEventArgs e) => PrzesunMiesiac(-1);
        private void BtnNextMonth_Click(object sender, RoutedEventArgs e) => PrzesunMiesiac(+1);

        private void PrzesunMiesiac(int delta)
        {
            int m = cbMiesiac.SelectedIndex + 1;
            int r = (int)cbRok.SelectedItem;
            var d = new DateTime(r, m, 1).AddMonths(delta);
            if (!cbRok.Items.Contains(d.Year)) cbRok.Items.Add(d.Year);
            cbRok.SelectedItem = d.Year;
            cbMiesiac.SelectedIndex = d.Month - 1;
        }

        private (int rok, int miesiac) AktualnyOkres()
        {
            int m = cbMiesiac.SelectedIndex + 1;
            int r = cbRok.SelectedItem is int rr ? rr : DateTime.Today.Year;
            return (r, m);
        }

        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var (rok, mies) = AktualnyOkres();
            loadingText.Text = $"Pobieram dane ubojowe za {NazwaMc(mies)} {rok}…";
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                var brojler = await _data.PobierzBrojleryZaMiesiacAsync(rok, mies);
                Pozycje.Clear();
                Pozycje.Add(new R09URowVm(brojler));

                OdswiezStatus(brojler.JestPusta ? "Brak danych" : "✓ Pobrano",
                    brojler.JestPusta ? "#FEF3C7" : "#DCFCE7",
                    brojler.JestPusta ? "#92400E" : "#15803D");

                lblFooter.Text = $"Pobrano {NazwaMc(mies)} {rok}: w14 brojlery kurze · " +
                                 "r1=LumQnt, r2=NettoFarmWeight, r3=NettoWeight, r5=wartość z faktur";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd:\n" + ex.Message, "Błąd SQL",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                OdswiezStatus("✗ Błąd", "#FEE2E2", "#B91C1C");
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (Pozycje.Count == 0)
            {
                MessageBox.Show("Najpierw pobierz dane.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var cfg = GusSettingsManager.Load();
            if (!cfg.IsConfigured)
            {
                MessageBox.Show("Skonfiguruj REGON i osobę odpowiedzialną.",
                    "Brak konfiguracji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (rok, mies) = AktualnyOkres();
            var data = new R09UReportData
            {
                Rok = rok,
                Miesiac = mies,
                OkresOd = new DateTime(rok, mies, 1),
                OkresDo = new DateTime(rok, mies, 1).AddMonths(1).AddDays(-1),
                Dzial1 = Pozycje.Select(p => p.Source).ToList(),
                Dzial2 = new()
            };

            // Walidacja przed eksportem
            var issues = _validator.Validate(data, cfg);
            if (issues.Count > 0)
            {
                // Konwersja R09UValidator.ValidationIssue → P02Validator.ValidationIssue (wspólny dialog)
                var p02Issues = issues.Select(i => new P02Validator.ValidationIssue(
                    (P02Validator.Severity)(int)i.Severity, i.Field, i.Message)).ToList();
                var walidDlg = new WalidacjaDialog(p02Issues) { Owner = this };
                walidDlg.ShowDialog();
                if (!walidDlg.MoznaKontynuowac) return;
            }

            try
            {
                var xml = _gen.Build(data, cfg);

                string nazwa = R09UXmlGenerator.ProponowanaNazwaPliku(rok, mies, cfg.Regon);
                string folder = string.IsNullOrWhiteSpace(cfg.FolderEksportu)
                    ? GusSettingsManager.DomyslnyFolderEksportu() : cfg.FolderEksportu;
                Directory.CreateDirectory(folder);
                string sciezka = Path.Combine(folder, nazwa);

                using (var sw = new StreamWriter(sciezka, false, new UTF8Encoding(false)))
                    xml.Save(sw);

                await _repo.InsertAsync(new GusSubmissionRow
                {
                    Formularz = "R-09U",
                    FormularzWersja = "2.0",
                    OkresOd = data.OkresOd,
                    OkresDo = data.OkresDo,
                    Rok = rok,
                    Miesiac = mies,
                    Regon = cfg.Regon,
                    GeneratedXml = xml.ToString(),
                    PlikXml = sciezka,
                    Status = "Generated",
                    IloscPozycji = data.Dzial1.Count,
                    SumaWartosc = data.Dzial1.Sum(p => p.WartoscZl),
                    GeneratedBy = TryGetUserId(),
                    GeneratedAt = DateTime.Now
                });

                OdswiezStatus($"✓ XML wygenerowany", "#DCFCE7", "#15803D");

                var info = MessageBox.Show(
                    $"XML zapisany:\n{sciezka}\n\nOtworzyć folder?",
                    "✓ Sukces", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (info == MessageBoxResult.Yes)
                    Process.Start("explorer.exe", $"/select,\"{sciezka}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPreviewXml_Click(object sender, RoutedEventArgs e)
        {
            if (Pozycje.Count == 0) { MessageBox.Show("Brak danych do podglądu."); return; }
            try
            {
                var cfg = GusSettingsManager.Load();
                var (rok, mies) = AktualnyOkres();
                var data = new R09UReportData
                {
                    Rok = rok, Miesiac = mies,
                    OkresOd = new DateTime(rok, mies, 1),
                    OkresDo = new DateTime(rok, mies, 1).AddMonths(1).AddDays(-1),
                    Dzial1 = Pozycje.Select(p => p.Source).ToList(),
                    Dzial2 = new()
                };
                var xml = _gen.Build(data, cfg);
                var dlg = new Window
                {
                    Title = $"Podgląd XML — R-09U {NazwaMc(mies)} {rok}",
                    Width = 1100, Height = 700,
                    Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new TextBox
                    {
                        Text = xml.ToString(), FontFamily = new FontFamily("Consolas"),
                        FontSize = 12, IsReadOnly = true,
                        TextWrapping = TextWrapping.NoWrap,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10)
                    }
                };
                dlg.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
        }

        // ════════════════════════════════════════════════════════════════
        // DRILL-DOWN — dwuklik na komórkę liczbową
        // ════════════════════════════════════════════════════════════════
        private void Dg_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                var src = dep;
                while (src != null && src is not System.Windows.Controls.DataGridRow
                       && src is not System.Windows.Controls.Primitives.DataGridColumnHeader)
                    src = System.Windows.Media.VisualTreeHelper.GetParent(src);
                if (src is System.Windows.Controls.Primitives.DataGridColumnHeader) return;
            }

            if (dg.CurrentCell == null || dg.CurrentCell.Column == null) return;
            var col = dg.CurrentCell.Column;

            R09UDrillTyp? typ = null;
            string label = "";
            if (col == colR1) { typ = R09UDrillTyp.Partie; label = "r1 — Sztuki"; }
            else if (col == colR2) { typ = R09UDrillTyp.Partie; label = "r2 — Waga żywa (NettoFarmWeight)"; }
            else if (col == colR3) { typ = R09UDrillTyp.Partie; label = "r3 — Po uboju brutto (NettoWeight)"; }
            else if (col == colR4) { typ = R09UDrillTyp.Partie; label = "r4 — Handlowa netto"; }
            else if (col == colR5) { typ = R09UDrillTyp.FakturyZywca; label = "r5 — Wartość (faktury FVZ+FVR+FKZ)"; }

            if (typ == null) return;

            var (rok, mies) = AktualnyOkres();
            try
            {
                var dlg = new R09UDrillDownDialog(typ.Value, rok, mies, label) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd drill-down:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OdswiezEmptyState()
        {
            if (emptyStateOverlay != null)
                emptyStateOverlay.Visibility = Pozycje.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OdswiezKarty()
        {
            if (Pozycje.Count == 0)
            {
                cardSztuki.Text = cardZywa.Text = cardNetto.Text = cardWydajnosc.Text = cardWartosc.Text = "—";
                cardSztukiSub.Text = cardZywaSub.Text = cardNettoSub.Text = cardWydajnoscSub.Text = cardWartoscSub.Text = "";
                return;
            }
            var p = Pozycje[0].Source;
            cardSztuki.Text = $"{p.LiczbaSztuk.ToString("N0", Pl)}";
            cardSztukiSub.Text = "ubitych ptaków (r1)";
            cardZywa.Text = $"{(p.WagaZywaKg / 1000m).ToString("N1", Pl)} t";
            cardZywaSub.Text = $"{p.WagaZywaKg.ToString("N0", Pl)} kg (r2 NettoFarmWeight)";
            cardNetto.Text = $"{(p.WagaPoubojowaBruttoKg / 1000m).ToString("N1", Pl)} t";
            cardNettoSub.Text = $"{p.WagaPoubojowaBruttoKg.ToString("N0", Pl)} kg (r3 NettoWeight)";
            cardWydajnosc.Text = $"{p.WydajnoscPoubojowa.ToString("N1", Pl)} %";
            cardWydajnoscSub.Text = $"śr. masa szt: {p.SredniaMasaSztKg.ToString("N2", Pl)} kg";
            cardWartosc.Text = $"{(p.WartoscZl / 1000m).ToString("N0", Pl)} tys zł";
            cardWartoscSub.Text = $"r5 z FVZ+FVR+FKZ";
        }

        private void OdswiezStatus(string text, string bgHex, string fgHex)
        {
            txtStatus.Text = text;
            try
            {
                statusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex));
                txtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex));
            }
            catch { }
        }

        private static string NazwaMc(int m)
        {
            string[] n = { "", "styczeń","luty","marzec","kwiecień","maj","czerwiec",
                "lipiec","sierpień","wrzesień","październik","listopad","grudzień" };
            return (m >= 1 && m <= 12) ? n[m] : $"mc{m}";
        }

        private static int? TryGetUserId()
        {
            try { return int.TryParse(App.UserID, out int id) ? (int?)id : null; }
            catch { return null; }
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            try { new HistoriaSprawozdanGusWindow { Owner = this }.ShowDialog(); }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) =>
            new GusSettingsDialog { Owner = this }.ShowDialog();

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    public class R09URowVm : INotifyPropertyChanged
    {
        public R09UPozycja Source { get; }
        public R09URowVm(R09UPozycja s) { Source = s; }

        public string KategoriaLabel => Source.WierszLabel;

        public int LiczbaSztuk
        {
            get => Source.LiczbaSztuk;
            set { Source.LiczbaSztuk = value; OnCh(nameof(LiczbaSztuk)); }
        }
        public decimal WagaZywaKg
        {
            get => Source.WagaZywaKg;
            set { Source.WagaZywaKg = value; OnCh(nameof(WagaZywaKg)); }
        }
        public decimal WagaPoubojowaBruttoKg
        {
            get => Source.WagaPoubojowaBruttoKg;
            set { Source.WagaPoubojowaBruttoKg = value; OnCh(nameof(WagaPoubojowaBruttoKg)); }
        }
        public decimal WagaHandlowaNettoKg
        {
            get => Source.WagaHandlowaNettoKg;
            set { Source.WagaHandlowaNettoKg = value; OnCh(nameof(WagaHandlowaNettoKg)); }
        }
        public decimal WartoscZl
        {
            get => Source.WartoscZl;
            set { Source.WartoscZl = value; OnCh(nameof(WartoscZl)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnCh(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
