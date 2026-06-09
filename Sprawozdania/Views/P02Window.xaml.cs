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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Models;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class P02Window : Window
    {
        // RoutedCommands dla skrótów klawiszowych (F5 / Ctrl+G / Ctrl+P / Ctrl+H / Esc)
        public static readonly RoutedCommand LoadCmd = new();
        public static readonly RoutedCommand GenerateCmd = new();
        public static readonly RoutedCommand PreviewCmd = new();
        public static readonly RoutedCommand HistoryCmd = new();
        public static readonly RoutedCommand PrevMonthCmd = new();
        public static readonly RoutedCommand NextMonthCmd = new();
        public static readonly RoutedCommand CloseCmd = new();

        private readonly P02DataService _data = new();
        private readonly P02XmlGenerator _gen = new();
        private readonly GusSubmissionsRepo _repo = new();
        private readonly P02Validator _validator = new();

        public ObservableCollection<P02RowViewModel> Pozycje { get; } = new();
        private System.Collections.Generic.List<P02DataService.TowarBezKlasyfikacji> _bezKlasyf = new();

        private static readonly CultureInfo Pl = new("pl-PL");

        public P02Window()
        {
            InitializeComponent();
            dgPozycje.ItemsSource = Pozycje;

            // Wczytaj zapamiętaną wersję XML
            var cfgInit = GusSettingsManager.Load();
            foreach (ComboBoxItem item in cbWersja.Items)
            {
                if ((item.Tag?.ToString() ?? "") == cfgInit.P02FormularzWersja)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // Miesiące 1-12
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

            Pozycje.CollectionChanged += (s, e) => { OdswiezSumy(); OdswiezEmptyState(); };

            // Bindings komend → metody
            CommandBindings.Add(new CommandBinding(LoadCmd, (s, e) => BtnLoad_Click(s, e)));
            CommandBindings.Add(new CommandBinding(GenerateCmd, (s, e) => BtnGenerate_Click(s, e)));
            CommandBindings.Add(new CommandBinding(PreviewCmd, (s, e) => BtnPreviewXml_Click(s, e)));
            CommandBindings.Add(new CommandBinding(HistoryCmd, (s, e) => BtnHistory_Click(s, e)));
            CommandBindings.Add(new CommandBinding(PrevMonthCmd, (s, e) => PrzesunMiesiac(-1)));
            CommandBindings.Add(new CommandBinding(NextMonthCmd, (s, e) => PrzesunMiesiac(+1)));
            CommandBindings.Add(new CommandBinding(CloseCmd, (s, e) => Close()));

            // Pełne okno — WorkArea (taskbar widoczny)
            SourceInitialized += (s, e) =>
            {
                var wa = SystemParameters.WorkArea;
                Left = wa.Left; Top = wa.Top;
                Width = wa.Width; Height = wa.Height;
            };

            Loaded += async (s, e) => await ZaladujHistorieAsync();
        }

        // ════════════════════════════════════════════════════════════════
        // PICKER
        // ════════════════════════════════════════════════════════════════
        private void Picker_Changed(object sender, SelectionChangedEventArgs e) => _ = ZaladujHistorieAsync();
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

        // ════════════════════════════════════════════════════════════════
        // POBIERANIE z HANDEL
        // ════════════════════════════════════════════════════════════════
        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var (rok, mies) = AktualnyOkres();
            loadingText.Text = $"Pobieram sprzedaż + produkcję za {NazwaMiesiaca(mies)} {rok} i YTD…";
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                var poz = await _data.PobierzZaMiesiacAsync(rok, mies);
                Pozycje.Clear();
                int lp = 1;
                foreach (var p in poz)
                    Pozycje.Add(new P02RowViewModel(p) { Lp = lp++ });

                DateTime od = new(rok, mies, 1);
                DateTime doD = od.AddMonths(1).AddDays(-1);
                _bezKlasyf = await _data.PobierzTowaryBezKlasyfikacjiAsync(od, doD);
                if (_bezKlasyf.Count > 0)
                {
                    decimal sumaKg = _bezKlasyf.Sum(x => x.Kg);
                    lblBrakKlasyf.Text = $"{_bezKlasyf.Count} towarów mięsnych w fakturach NIE pasuje do żadnej z 4 kategorii P-02 " +
                        $"({sumaKg:N0} kg) — kliknij 'Pokaż listę' żeby zobaczyć szczegóły.";
                    brakKlasyfPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    brakKlasyfPanel.Visibility = Visibility.Collapsed;
                }

                int nieZerowe = Pozycje.Count(p => !p.Source.JestPusta);
                OdswiezStatus($"✓ Pobrano · {nieZerowe}/4 pozycji z danymi", "#DCFCE7", "#15803D");
                lblFooter.Text = $"Pobrano {NazwaMiesiaca(mies)} {rok} (HANDEL .112). " +
                                 $"DWUKLIK na komórkę liczbową, aby zobaczyć dokumenty źródłowe.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania danych:\n" + ex.Message, "Błąd SQL",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                OdswiezStatus("✗ Błąd pobierania", "#FEE2E2", "#B91C1C");
            }
            finally
            {
                loadingOverlay.Visibility = Visibility.Collapsed;
                OdswiezSumy();
            }
        }

        // ════════════════════════════════════════════════════════════════
        // DRILL-DOWN — dwuklik komórki w DataGrid
        // ════════════════════════════════════════════════════════════════
        private void DgPozycje_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignoruj dwuklik na header
            if (e.OriginalSource is DependencyObject dep)
            {
                var src = dep;
                while (src != null && src is not DataGridRow && src is not DataGridColumnHeader)
                    src = VisualTreeHelper.GetParent(src);
                if (src is DataGridColumnHeader) return;
            }

            if (dgPozycje.CurrentCell == null || dgPozycje.CurrentCell.Column == null) return;
            if (dgPozycje.CurrentItem is not P02RowViewModel vm) return;

            var col = dgPozycje.CurrentCell.Column;
            DrillDownTyp? typ = null;
            if (col == colProdMc) typ = DrillDownTyp.ProdukcjaMc;
            else if (col == colProdYtd) typ = DrillDownTyp.ProdukcjaYtd;
            else if (col == colSprzMc) typ = DrillDownTyp.SprzedazMc;
            else if (col == colSprzYtd) typ = DrillDownTyp.SprzedazYtd;

            if (typ == null) return;            // dwuklik nie na komórce drill-down
            if (string.IsNullOrWhiteSpace(vm.Pkwiu)) return;

            var (rok, mies) = AktualnyOkres();
            try
            {
                var dlg = new DokumentyDrillDownDialog(vm.Pkwiu, typ.Value, rok, mies) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwarcia drill-down:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnShowBrakKlasyf_Click(object sender, RoutedEventArgs e)
        {
            if (_bezKlasyf.Count == 0) return;
            try
            {
                var dlg = new BrakKlasyfikacjiWindow(_bezKlasyf) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd otwarcia: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // GENEROWANIE XML
        // ════════════════════════════════════════════════════════════════
        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (Pozycje.Count == 0)
            {
                MessageBox.Show("Najpierw pobierz dane z HANDEL.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var cfg = GusSettingsManager.Load();
            if (!cfg.IsConfigured)
            {
                MessageBox.Show("Skonfiguruj REGON i osobę odpowiedzialną w 'Konfiguracja'.",
                    "Brak konfiguracji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (rok, mies) = AktualnyOkres();

            bool juzWyslane = await _repo.ExistsSentForPeriodAsync("P-02", rok, mies);
            if (juzWyslane)
            {
                var r = MessageBox.Show(
                    $"Sprawozdanie P-02 za {NazwaMiesiaca(mies)} {rok} jest oznaczone jako wysłane.\nWygenerować ponownie?",
                    "Już wysłane", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }

            var data = new P02ReportData
            {
                Rok = rok,
                Miesiac = mies,
                OkresOd = new DateTime(rok, mies, 1),
                OkresDo = new DateTime(rok, mies, 1).AddMonths(1).AddDays(-1),
                Pozycje = Pozycje.Select(p => p.ToPozycja()).ToList()
            };

            // ═══════════ WALIDACJA przed eksportem ═══════════
            var issues = _validator.Validate(data, cfg);
            if (issues.Count > 0)
            {
                var walidDlg = new WalidacjaDialog(issues) { Owner = this };
                walidDlg.ShowDialog();
                if (!walidDlg.MoznaKontynuowac) return;
            }

            try
            {
                var xml = _gen.Build(data, cfg);

                string nazwa = P02XmlGenerator.ProponowanaNazwaPliku(rok, mies, cfg.Regon);
                string folder = string.IsNullOrWhiteSpace(cfg.FolderEksportu)
                    ? GusSettingsManager.DomyslnyFolderEksportu() : cfg.FolderEksportu;
                Directory.CreateDirectory(folder);
                string sciezka = Path.Combine(folder, nazwa);

                using (var sw = new StreamWriter(sciezka, false, new UTF8Encoding(false)))
                    xml.Save(sw);

                int id = await _repo.InsertAsync(new GusSubmissionRow
                {
                    Formularz = "P-02",
                    FormularzWersja = "16.0",
                    OkresOd = data.OkresOd,
                    OkresDo = data.OkresDo,
                    Rok = rok,
                    Miesiac = mies,
                    Regon = cfg.Regon,
                    GeneratedXml = xml.ToString(),
                    PlikXml = sciezka,
                    Status = "Generated",
                    IloscPozycji = data.Pozycje.Count,
                    SumaWartosc = data.Pozycje.Sum(p => p.SprzedazWMiesiacuTony),
                    GeneratedBy = TryGetUserId(),
                    GeneratedAt = DateTime.Now
                });

                OdswiezStatus($"✓ XML wygenerowany ({data.Pozycje.Count} poz.)", "#DCFCE7", "#15803D");

                var info = MessageBox.Show(
                    $"XML zapisany:\n{sciezka}\n\nOtworzyć folder w Eksploratorze?",
                    "✓ Sukces", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (info == MessageBoxResult.Yes)
                    Process.Start("explorer.exe", $"/select,\"{sciezka}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd generacji XML:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                OdswiezStatus("✗ Błąd generacji", "#FEE2E2", "#B91C1C");
            }
        }

        private void BtnPreviewXml_Click(object sender, RoutedEventArgs e)
        {
            if (Pozycje.Count == 0)
            {
                MessageBox.Show("Brak pozycji do podglądu.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                var cfg = GusSettingsManager.Load();
                var (rok, mies) = AktualnyOkres();
                var data = new P02ReportData
                {
                    Rok = rok, Miesiac = mies,
                    OkresOd = new DateTime(rok, mies, 1),
                    OkresDo = new DateTime(rok, mies, 1).AddMonths(1).AddDays(-1),
                    Pozycje = Pozycje.Select(p => p.ToPozycja()).ToList()
                };
                var xml = _gen.Build(data, cfg);

                var dlg = new Window
                {
                    Title = $"Podgląd XML — P-02 {NazwaMiesiaca(mies)} {rok}",
                    Width = 1200, Height = 750,
                    Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = new TextBox
                    {
                        Text = xml.ToString(),
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
            catch (Exception ex)
            {
                MessageBox.Show("Błąd budowy XML:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // HISTORIA
        // ════════════════════════════════════════════════════════════════
        private async Task ZaladujHistorieAsync()
        {
            try
            {
                var (rok, mies) = AktualnyOkres();
                var ost = await _repo.GetForPeriodAsync("P-02", rok, mies);
                if (ost == null)
                {
                    OdswiezStatus("Nie wygenerowane", "#FEF3C7", "#92400E");
                    return;
                }
                string when = ost.GeneratedAt.ToString("dd.MM.yyyy HH:mm");
                string kto = ost.GeneratedByImie ?? "—";
                switch (ost.Status)
                {
                    case "Sent": OdswiezStatus($"✓ Wysłane · {when} ({kto})", "#DCFCE7", "#15803D"); break;
                    case "Exported":
                    case "Generated": OdswiezStatus($"📄 Wygenerowane · {when}", "#DBEAFE", "#1E40AF"); break;
                    case "Failed": OdswiezStatus($"✗ Błąd · {when}", "#FEE2E2", "#B91C1C"); break;
                    default: OdswiezStatus(ost.Status, "#F0F2F5", "#374151"); break;
                }
            }
            catch { /* nie blokuj */ }
        }

        private async void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new HistoriaSprawozdanGusWindow { Owner = this };
                dlg.ShowDialog();
                // Po zamknięciu historii — odśwież status bieżącego okresu (mogło być oznaczone jako wysłane)
                await ZaladujHistorieAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć historii:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // UI helpers
        // ════════════════════════════════════════════════════════════════
        private void OdswiezSumy()
        {
            decimal prodMc = Pozycje.Sum(p => p.ProdukcjaWMiesiacuTony);
            decimal sprzMc = Pozycje.Sum(p => p.SprzedazWMiesiacuTony);
            decimal prodYtd = Pozycje.Sum(p => p.ProdukcjaOdPoczatkuRokuTony);
            decimal sprzYtd = Pozycje.Sum(p => p.SprzedazOdPoczatkuRokuTony);
            lblSumProd.Text = $"Prod: {prodMc.ToString("N0", Pl)} t  ·  YTD {prodYtd.ToString("N0", Pl)} t";
            lblSumSprz.Text = $"Sprz: {sprzMc.ToString("N0", Pl)} t  ·  YTD {sprzYtd.ToString("N0", Pl)} t";
        }

        private void OdswiezEmptyState()
        {
            if (emptyStateOverlay != null)
                emptyStateOverlay.Visibility = Pozycje.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        private static string NazwaMiesiaca(int m)
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

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new GusSettingsDialog { Owner = this };
            dlg.ShowDialog();
        }

        // ════════════════════════════════════════════════════════════════
        // SUGESTIA ZAPASÓW Z BAZY (opcjonalna — Symfonia zostawia zera)
        // ════════════════════════════════════════════════════════════════
        private void BtnSugerujZapasy_Click(object sender, RoutedEventArgs e)
        {
            var (rok, mies) = AktualnyOkres();
            try
            {
                var dlg = new ZapasyDialog(rok, mies) { Owner = this };
                if (dlg.ShowDialog() == true && dlg.Zaakceptowane != null)
                {
                    // Wpisz zaakceptowane wartości do kolumny ZapasyWyrobowTony
                    foreach (var vm in Pozycje)
                    {
                        if (dlg.Zaakceptowane.TryGetValue(vm.Pkwiu, out var t))
                            vm.ZapasyWyrobowTony = t;
                    }
                    OdswiezSumy();
                    lblFooter.Text = $"✓ Wpisano sugerowane zapasy wyrobów na koniec {NazwaMiesiaca(mies)} {rok}. " +
                                     "Możesz je nadal edytować ręcznie w DataGrid.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd dialogu zapasów:\n" + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // Wybór wersji formularza XML (zapisz do settings)
        private void CbWersja_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (cbWersja.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                try
                {
                    var cfg = GusSettingsManager.Load();
                    cfg.P02FormularzWersja = tag;
                    GusSettingsManager.Save(cfg);
                    lblFooter.Text = $"✓ Wersja formularza P-02 ustawiona na {tag} (zapamiętane)";
                }
                catch { /* ignore */ }
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // ViewModel dla wiersza DataGrid
    // Dodaje: Lp, KolorPasek (kolor lewej krawędzi per PKWiU)
    // ════════════════════════════════════════════════════════════════════
    public class P02RowViewModel : INotifyPropertyChanged
    {
        public P02Pozycja Source { get; }

        public P02RowViewModel(P02Pozycja s) { Source = s; }

        public int Lp { get; set; }

        public string Pkwiu
        {
            get => Source.Pkwiu;
            set
            {
                Source.Pkwiu = value;
                OnChanged(nameof(Pkwiu));
                OnChanged(nameof(KolorPasek));
            }
        }
        public string NazwaWyrobu
        {
            get => Source.NazwaWyrobu;
            set { Source.NazwaWyrobu = value; OnChanged(nameof(NazwaWyrobu)); }
        }
        public string JednostkaKod
        {
            get => Source.JednostkaKod;
            set { Source.JednostkaKod = value; OnChanged(nameof(JednostkaKod)); }
        }
        public decimal ProdukcjaWMiesiacuTony
        {
            get => Source.ProdukcjaWMiesiacuTony;
            set { Source.ProdukcjaWMiesiacuTony = value; OnChanged(nameof(ProdukcjaWMiesiacuTony)); }
        }
        public decimal ProdukcjaOdPoczatkuRokuTony
        {
            get => Source.ProdukcjaOdPoczatkuRokuTony;
            set { Source.ProdukcjaOdPoczatkuRokuTony = value; OnChanged(nameof(ProdukcjaOdPoczatkuRokuTony)); }
        }
        public decimal SprzedazWMiesiacuTony
        {
            get => Source.SprzedazWMiesiacuTony;
            set { Source.SprzedazWMiesiacuTony = value; OnChanged(nameof(SprzedazWMiesiacuTony)); }
        }
        public decimal SprzedazOdPoczatkuRokuTony
        {
            get => Source.SprzedazOdPoczatkuRokuTony;
            set { Source.SprzedazOdPoczatkuRokuTony = value; OnChanged(nameof(SprzedazOdPoczatkuRokuTony)); }
        }
        public decimal ZapasyWyrobowTony
        {
            get => Source.ZapasyWyrobowTony;
            set { Source.ZapasyWyrobowTony = value; OnChanged(nameof(ZapasyWyrobowTony)); }
        }
        public decimal ZapasyTowarowTony
        {
            get => Source.ZapasyTowarowTony;
            set { Source.ZapasyTowarowTony = value; OnChanged(nameof(ZapasyTowarowTony)); }
        }

        public int LiczbaTowarow => Source.LiczbaTowarow;

        // Kolor paska po lewej stronie komórki PKWiU
        public Brush KolorPasek
        {
            get
            {
                string hex = (Source.Pkwiu ?? "") switch
                {
                    "10.12.10-10" => "#2563EB",   // niebieski — tuszki świeże
                    "10.12.10-50" => "#16A34A",   // zielony — elementy świeże
                    "10.12.20-13" => "#06B6D4",   // cyjan — tuszki mrożone
                    "10.12.20-53" => "#7C3AED",   // fiolet — elementy mrożone
                    _ => "#9CA3AF"                 // szary fallback
                };
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
        }

        // Tooltip per komórka PKWiU — pokazuje kody towarów Sage które wpadły do tej pozycji
        public string KodyTooltip
        {
            get
            {
                if (Source.KodyTowarow.Count == 0) return "Brak danych z bazy — wprowadź ręcznie.";
                if (Source.KodyTowarow.Count <= 12)
                    return "Kody Sage w tej pozycji:\n• " + string.Join("\n• ", Source.KodyTowarow);
                var first = Source.KodyTowarow.Take(10);
                return $"Kody Sage w tej pozycji (top 10 z {Source.KodyTowarow.Count}):\n• "
                    + string.Join("\n• ", first) + $"\n…i {Source.KodyTowarow.Count - 10} więcej";
            }
        }

        public P02Pozycja ToPozycja() => Source;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
