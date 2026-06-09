using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.Sprawozdania.Models;
using Kalendarz1.Sprawozdania.Services;

namespace Kalendarz1.Sprawozdania.Views
{
    public partial class R09UWindow : Window
    {
        public static readonly RoutedCommand LoadCmd = new();
        public static readonly RoutedCommand GenerateCmd = new();
        public static readonly RoutedCommand PreviewCmd = new();
        public static readonly RoutedCommand HistoryCmd = new();
        public static readonly RoutedCommand PrevMonthCmd = new();
        public static readonly RoutedCommand NextMonthCmd = new();
        public static readonly RoutedCommand CloseCmd = new();

        private readonly R09USpecyfikacjeService _spec = new();
        private readonly R09UXmlGenerator _gen = new();
        private readonly GusSubmissionsRepo _repo = new();
        private readonly R09UValidator _validator = new();

        public ObservableCollection<R09USpecDzien> Dni { get; } = new();
        public ObservableCollection<WynikRow> Wynik { get; } = new();

        private static readonly CultureInfo Pl = new("pl-PL");

        public R09UWindow()
        {
            InitializeComponent();
            dgDni.ItemsSource = Dni;
            dgWynik.ItemsSource = Wynik;

            foreach (var m in new[] {
                "styczeń","luty","marzec","kwiecień","maj","czerwiec",
                "lipiec","sierpień","wrzesień","październik","listopad","grudzień"
            }) cbMiesiac.Items.Add(m);

            int currentRok = DateTime.Today.Year;
            for (int r = currentRok - 5; r <= currentRok + 1; r++) cbRok.Items.Add(r);

            // Default: ZAWSZE poprzedni miesiąc (per ustaleniu Sergiusza)
            var prev = DateTime.Today.AddMonths(-1);
            cbMiesiac.SelectedIndex = prev.Month - 1;
            cbRok.SelectedItem = prev.Year;

            Dni.CollectionChanged += (s, e) => { OdswiezKarty(); OdswiezEmptyState(); };

            CommandBindings.Add(new CommandBinding(LoadCmd, (s, e) => BtnLoad_Click(s, e)));
            CommandBindings.Add(new CommandBinding(GenerateCmd, (s, e) => BtnGenerate_Click(s, e)));
            CommandBindings.Add(new CommandBinding(PreviewCmd, (s, e) => BtnPreviewXml_Click(s, e)));
            CommandBindings.Add(new CommandBinding(HistoryCmd, (s, e) => BtnHistory_Click(s, e)));
            CommandBindings.Add(new CommandBinding(PrevMonthCmd, (s, e) => PrzesunMiesiac(-1)));
            CommandBindings.Add(new CommandBinding(NextMonthCmd, (s, e) => PrzesunMiesiac(+1)));
            CommandBindings.Add(new CommandBinding(CloseCmd, (s, e) => Close()));

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

        // ════════════════════════════════════════════════════════════════
        // POBIERANIE
        // ════════════════════════════════════════════════════════════════
        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var (rok, mies) = AktualnyOkres();
            loadingText.Text = $"Agreguję Specyfikacje za {NazwaMc(mies)} {rok}…";
            loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                var dni = await _spec.PobierzZaMiesiacAsync(rok, mies);
                Dni.Clear();
                foreach (var d in dni) Dni.Add(d);

                OdswiezStatus(dni.Count == 0 ? "Brak danych" : $"✓ Pobrano {dni.Count} dni",
                    dni.Count == 0 ? "#FEF3C7" : "#DCFCE7",
                    dni.Count == 0 ? "#92400E" : "#15803D");
                lblFooter.Text = $"Pobrano {dni.Count} dni · {NazwaMc(mies)} {rok}. " +
                                 "Tabela powyżej → mini-tabela R-09U poniżej (D1+D2).";
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

        // ════════════════════════════════════════════════════════════════
        // GENEROWANIE XML
        // ════════════════════════════════════════════════════════════════
        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (Dni.Count == 0)
            {
                MessageBox.Show("Najpierw pobierz dane.", "Brak danych",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var cfg = GusSettingsManager.Load();
            if (!cfg.IsConfigured)
            {
                MessageBox.Show("Skonfiguruj REGON i osobę odpowiedzialną.", "Brak konfiguracji",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (rok, mies) = AktualnyOkres();
            var data = ZbudujReportData(rok, mies);

            // Walidacja
            var issues = _validator.Validate(data, cfg);
            if (issues.Count > 0)
            {
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
                    Rok = rok, Miesiac = mies,
                    Regon = cfg.Regon,
                    GeneratedXml = xml.ToString(),
                    PlikXml = sciezka,
                    Status = "Generated",
                    IloscPozycji = 2, // D1 + D2 dla brojlera
                    SumaWartosc = data.Dzial1.Sum(p => p.WagaZywaKg) + data.Dzial2.Sum(p => p.WagaZywaKg),
                    GeneratedBy = TryGetUserId(),
                    GeneratedAt = DateTime.Now
                });

                OdswiezStatus("✓ XML wygenerowany", "#DCFCE7", "#15803D");
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
            if (Dni.Count == 0) { MessageBox.Show("Brak danych do podglądu."); return; }
            try
            {
                var cfg = GusSettingsManager.Load();
                var (rok, mies) = AktualnyOkres();
                var data = ZbudujReportData(rok, mies);
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
        // MAPOWANIE → R-09U
        // ════════════════════════════════════════════════════════════════
        private R09UReportData ZbudujReportData(int rok, int mies)
        {
            int sumD1Szt = Dni.Sum(d => d.D1_Sztuki);
            decimal sumD1Kg = Dni.Sum(d => d.D1_Kg);
            int sumD2Szt = Dni.Sum(d => d.D2_Sztuki);
            decimal sumD2Kg = Dni.Sum(d => d.D2_Kg);

            return new R09UReportData
            {
                Rok = rok, Miesiac = mies,
                OkresOd = new DateTime(rok, mies, 1),
                OkresDo = new DateTime(rok, mies, 1).AddMonths(1).AddDays(-1),
                Dzial1 = new List<R09UPozycja>
                {
                    new()
                    {
                        Wiersz = R09UWiersz.Brojlery_Kurze,
                        LiczbaSztuk = sumD1Szt,
                        WagaZywaKg = sumD1Kg,
                        WagaPoubojowaBruttoKg = 0,
                        WagaHandlowaNettoKg = 0,
                        WartoscZl = 0
                    }
                },
                Dzial2 = new List<R09UPozycja>
                {
                    new()
                    {
                        Wiersz = R09UWiersz.Brojlery_Kurze,
                        LiczbaSztuk = sumD2Szt,
                        WagaZywaKg = sumD2Kg,
                        WagaPoubojowaBruttoKg = 0,
                        WagaHandlowaNettoKg = 0,
                        WartoscZl = 0
                    }
                }
            };
        }

        // ════════════════════════════════════════════════════════════════
        // UI
        // ════════════════════════════════════════════════════════════════
        private void OdswiezKarty()
        {
            int d1Szt = Dni.Sum(d => d.D1_Sztuki);
            decimal d1Kg = Dni.Sum(d => d.D1_Kg);
            int d2Szt = Dni.Sum(d => d.D2_Sztuki);
            decimal d2Kg = Dni.Sum(d => d.D2_Kg);
            int zdat = Dni.Sum(d => d.ZdatneSzt);
            int padle = Dni.Sum(d => d.PadleSzt);
            int konfi = Dni.Sum(d => d.KonfiSzt);
            decimal zywiec = Dni.Sum(d => d.ZywiecKg);
            decimal konfKg = Dni.Sum(d => d.KonfiKg);
            decimal padleKg = Dni.Sum(d => d.PadleKg);

            cardD1Szt.Text = d1Szt.ToString("N0", Pl);
            cardD1SztSub.Text = $"Zdatne {zdat:N0} + Konfi {konfi:N0}";
            cardD1Kg.Text = $"{(d1Kg / 1000m).ToString("N1", Pl)} t";
            cardD1KgSub.Text = $"Żywiec {zywiec:N0} + Konfi kg {konfKg:N0}";

            cardD2Szt.Text = d2Szt.ToString("N0", Pl);
            cardD2SztSub.Text = $"Padłe {padle:N0} + Konfi {konfi:N0}";
            cardD2Kg.Text = $"{d2Kg.ToString("N0", Pl)} kg";
            cardD2KgSub.Text = $"Padłe kg {padleKg:N0} + Konfi kg {konfKg:N0}";

            // Update mini tabeli wynikowej
            Wynik.Clear();
            Wynik.Add(new WynikRow
            {
                Dzial = "🟢 Dział 1 (ubój całkowity)",
                Sztuki = d1Szt,
                Kg = d1Kg,
                Wzor = "Zdatne[szt] + Konfi[szt]  ·  Żywiec[kg] + Konfi[kg]"
            });
            Wynik.Add(new WynikRow
            {
                Dzial = "🔴 Dział 2 (nie do konsumpcji)",
                Sztuki = d2Szt,
                Kg = d2Kg,
                Wzor = "Padłe[szt] + Konfi[szt]  ·  Suma[kg] = Padłe + Konfi"
            });
        }

        private void OdswiezEmptyState()
        {
            if (emptyStateOverlay != null)
                emptyStateOverlay.Visibility = Dni.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

    // ViewModel wiersza w mini-tabeli wynikowej R-09U
    public class WynikRow
    {
        public string Dzial { get; set; } = "";
        public int Sztuki { get; set; }
        public decimal Kg { get; set; }
        public string Wzor { get; set; } = "";
    }
}
