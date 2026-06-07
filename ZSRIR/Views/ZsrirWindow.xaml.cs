using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.ZSRIR.Models;
using Kalendarz1.ZSRIR.Services;

namespace Kalendarz1.ZSRIR.Views
{
    public partial class ZsrirWindow : Window
    {
        public static readonly RoutedCommand CloseCmd = new RoutedCommand();
        private static readonly CultureInfo Pl = new CultureInfo("pl-PL");

        // Kategoria towarowa raportowana — dla Piórkowskich = "Kurczęta brojler"
        // CommodityGroupId pobierane runtime z GetFormConfiguration.
        private const string KategoriaTowaru = "Kurczęta brojler";

        private readonly ZsrirSubmissionsRepo _repo = new();
        private readonly ZsrirDataBuilder _builder = new();
        private ZsrirDataBuilder.RaportTygodniowy? _aktualnyRaport;

        public ObservableCollection<HistRow> HistoryRows { get; } = new();

        public ZsrirWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(CloseCmd, (s, e) => Close()));
            dgHistory.ItemsSource = HistoryRows;

            // Default: poprzedni tydzień
            var (od, doD) = ZsrirDataBuilder.PoprzedniTydzien();
            dpFrom.SelectedDate = od;
            dpTo.SelectedDate = doD;

            Loaded += async (s, e) =>
            {
                AktualizujBadgeKonfiguracji();
                await OdswiezWszystkoAsync();
            };
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ============ BADGE STATUS KONFIGURACJI ============
        private void AktualizujBadgeKonfiguracji()
        {
            var s = ZsrirSecretsManager.Load();
            if (!s.IsConfigured)
            {
                connDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB400"));
                lblConn.Text = "Nieskonfigurowane";
            }
            else if (s.DataSupplierId == null || s.FormId == null)
            {
                connDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB400"));
                lblConn.Text = $"Login OK · brak dostawcy/form. (Test po.)";
            }
            else
            {
                connDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                lblConn.Text = $"Skonfigurowane · {s.Username}";
            }
        }

        // ============ PRESETY ============
        private async void BtnPreviousWeek_Click(object sender, RoutedEventArgs e)
        {
            var (od, doD) = ZsrirDataBuilder.PoprzedniTydzien();
            dpFrom.SelectedDate = od;
            dpTo.SelectedDate = doD;
            await OdswiezPodgladAsync();
        }

        private async void BtnRefreshPreview_Click(object sender, RoutedEventArgs e) => await OdswiezPodgladAsync();

        // ============ ODŚWIEŻANIE ============
        private async Task OdswiezWszystkoAsync()
        {
            await OdswiezPodgladAsync();
            await OdswiezHistorieAsync();
        }

        private async Task OdswiezPodgladAsync()
        {
            if (!dpFrom.SelectedDate.HasValue || !dpTo.SelectedDate.HasValue) return;
            DateTime od = dpFrom.SelectedDate.Value.Date;
            DateTime doD = dpTo.SelectedDate.Value.Date;

            ShowLoading("Liczę dane raportu...");
            try
            {
                _aktualnyRaport = await _builder.ZbudujRaportAsync(od, doD);
                lblOkres.Text = $"{od:dd.MM.yyyy} – {doD:dd.MM.yyyy}";
                lblKg.Text = _aktualnyRaport.Kg.ToString("N0", Pl) + " kg";
                lblTony.Text = _aktualnyRaport.Tony.ToString("N3", Pl) + " t";
                lblWartosc.Text = _aktualnyRaport.WartoscNetto.ToString("N2", Pl) + " zł";
                lblCenaTona.Text = _aktualnyRaport.CenaZlTona.ToString("N2", Pl) + " zł/t";
                lblCenaKg.Text = "= " + _aktualnyRaport.CenaZlKg.ToString("N2", Pl) + " zł/kg";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd liczenia raportu: " + ex.Message, "ZSRIR",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { HideLoading(); }
        }

        private async Task OdswiezHistorieAsync()
        {
            try
            {
                var rows = await _repo.GetRecentAsync(100);
                HistoryRows.Clear();
                foreach (var r in rows) HistoryRows.Add(HistRow.From(r));
                lblHistCount.Text = $" · {rows.Count} wysyłek";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania historii: " + ex.Message, "ZSRIR",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============ TEST POŁĄCZENIA ============
        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            var secrets = ZsrirSecretsManager.Load();
            if (!secrets.IsConfigured)
            {
                MessageBox.Show("Najpierw skonfiguruj login/hasło w Ustawieniach.",
                    "ZSRIR — brak konfiguracji", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            ShowLoading("Łączę z API ZSRIR...");
            try
            {
                using var api = new ZsrirApiClient(secrets);
                var (ok, msg) = await api.TestConnectionAsync();
                if (ok) MessageBox.Show("✓ " + msg, "ZSRIR — test OK", MessageBoxButton.OK, MessageBoxImage.Information);
                else MessageBox.Show("✗ " + msg, "ZSRIR — błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { HideLoading(); AktualizujBadgeKonfiguracji(); }
        }

        // ============ USTAWIENIA ============
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ZsrirSettingsDialog { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                AktualizujBadgeKonfiguracji();
            }
        }

        // ============ WYSŁKA ============
        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnyRaport == null) { await OdswiezPodgladAsync(); if (_aktualnyRaport == null) return; }

            var secrets = ZsrirSecretsManager.Load();
            if (!secrets.IsConfigured)
            {
                MessageBox.Show("Skonfiguruj login/hasło API w Ustawieniach.", "ZSRIR",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (secrets.DataSupplierId == null || secrets.FormId == null)
            {
                MessageBox.Show("Brak wybranego dostawcy / formularza w Ustawieniach.", "ZSRIR",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Sprawdź czy już nie wysłaliśmy
            if (await _repo.ExistsForPeriodAsync(_aktualnyRaport.OkresOd, _aktualnyRaport.OkresDo, KategoriaTowaru))
            {
                var conf = MessageBox.Show(
                    $"Już wysłano raport dla okresu {_aktualnyRaport.OkresOd:dd.MM} – {_aktualnyRaport.OkresDo:dd.MM.yyyy} kategorii \"{KategoriaTowaru}\".\n\nWysłać mimo to?",
                    "Duplikacja", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (conf != MessageBoxResult.Yes) return;
            }

            // Potwierdzenie z podsumowaniem
            string podsumowanie =
                $"Wysłać do ZSRIR następujące dane?\n\n" +
                $"Kategoria:  {KategoriaTowaru}\n" +
                $"Okres:      {_aktualnyRaport.OkresOd:dd.MM.yyyy} – {_aktualnyRaport.OkresDo:dd.MM.yyyy}\n" +
                $"Ilość:      {_aktualnyRaport.Tony:N3} ton  ({_aktualnyRaport.Kg:N0} kg)\n" +
                $"Wartość:    {_aktualnyRaport.WartoscNetto:N2} zł netto\n" +
                $"Cena:       {_aktualnyRaport.CenaZlTona:N2} zł/t";
            if (MessageBox.Show(podsumowanie, "Potwierdzenie wysyłki",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            ShowLoading("Wysyłam do ZSRIR...");
            string? response = null;
            string? errorMsg = null;
            string status = "Pending";
            int? commodityGroupId = null;

            try
            {
                using var api = new ZsrirApiClient(secrets);

                // Pobierz aktualny okres sprawozdawczy + konfigurację pól (key dla Price/Amount)
                var periods = await api.GetReportingPeriodsAsync(secrets.FormId!.Value);
                var openPeriod = FindMatchingPeriod(periods, _aktualnyRaport.OkresOd, _aktualnyRaport.OkresDo);
                if (openPeriod == null)
                    throw new Exception($"Brak otwartego okresu sprawozdawczego dla {_aktualnyRaport.OkresOd:dd.MM}–{_aktualnyRaport.OkresDo:dd.MM.yyyy}");

                var formCfg = await api.GetFormConfigurationAsync(secrets.FormId!.Value);
                if (formCfg == null) throw new Exception("Pobranie konfiguracji formularza nie powiodło się.");
                var brojler = FindBrojlerGroup(formCfg);
                if (brojler == null) throw new Exception("Nie znaleziono kategorii \"Kurczęta brojler\" w formularzu.");
                commodityGroupId = brojler.Id;

                string priceKey = FindFieldKey(brojler, "Price") ?? "Price";
                string amountKey = FindFieldKey(brojler, "Amount") ?? "Amount";

                if (_aktualnyRaport.Tony <= 0)
                {
                    // Formularz zerowy
                    response = await api.AddFormZeroAsync(new AddFormZeroRequest
                    {
                        FormReportingPeriodId = openPeriod.Id,
                        DataSupplierId = secrets.DataSupplierId!.Value
                    });
                    status = "Zero";
                }
                else
                {
                    var body = new AddFormRequest
                    {
                        FormReportingPeriodId = openPeriod.Id,
                        DataSupplierId = secrets.DataSupplierId!.Value,
                        Forms = new()
                        {
                            new FormPayload
                            {
                                CommodityGroupId = brojler.Id,
                                FormFieldsValues = new()
                                {
                                    // ZSRIR akceptuje TYLKO PL format (przecinek dziesiętny).
                                    [priceKey] = _aktualnyRaport.CenaZlTona.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pl-PL")),
                                    [amountKey] = _aktualnyRaport.Tony.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pl-PL"))
                                }
                            }
                        }
                    };
                    response = await api.AddFormAsync(body);
                    status = "Sent";
                }
            }
            catch (ZsrirApiException ex) { errorMsg = ex.Message; response = ex.RawBody; status = "Failed"; }
            catch (Exception ex) { errorMsg = ex.Message; status = "Failed"; }
            finally { HideLoading(); }

            // Zapisz historię niezależnie od wyniku
            int? userId = int.TryParse(App.UserID, out int u) ? u : (int?)null;
            var row = new SubmissionRow
            {
                OkresOd = _aktualnyRaport.OkresOd,
                OkresDo = _aktualnyRaport.OkresDo,
                KategoriaTowaru = KategoriaTowaru,
                CommodityGroupId = commodityGroupId,
                KgRazem = _aktualnyRaport.Kg,
                TonyRazem = _aktualnyRaport.Tony,
                WartoscNetto = _aktualnyRaport.WartoscNetto,
                CenaZlTona = _aktualnyRaport.CenaZlTona,
                FormReportingPeriodId = null, // wpiszemy jeśli mamy z odpowiedzi
                DataSupplierId = secrets.DataSupplierId,
                Status = status,
                ApiResponse = response,
                ErrorMessage = errorMsg,
                WyslanyPrzez = userId,
                WyslanyDataCzas = DateTime.Now
            };
            await _repo.InsertAsync(row);
            await OdswiezHistorieAsync();

            if (status == "Sent" || status == "Zero")
                MessageBox.Show($"✓ Raport wysłany do ZSRIR (status: {status}).",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"✗ Błąd wysyłki:\n{errorMsg}",
                    "Niepowodzenie", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // ============ HELPERS (zaktualizowane wg dokumentacji ZSRIR API 1.0) ============
        private static ReportingPeriod? FindMatchingPeriod(List<ReportingPeriod> periods, DateTime od, DateTime doD)
        {
            // Preferuj okres dokładnie pokrywający się z naszym zakresem (§4.3: dateFrom/dateTo)
            foreach (var p in periods)
            {
                if (p.DateFrom.Date == od && p.DateTo.Date == doD) return p;
            }
            // Fallback: najbliższy otwarty
            foreach (var p in periods)
            {
                if (p.IsOpen && p.DateFrom.Date == od) return p;
            }
            return null;
        }

        // Szuka grupy kurczak/kurczęta brojler — z priorytetem na "kurcz" PRZED "brojler",
        // bo samo "brojler" pasuje też do "gęsi typu brojler" w drzewie ZSRIR.
        private static CommodityGroup? FindBrojlerGroup(FormConfiguration cfg)
        {
            var all = new List<CommodityGroup>();
            if (cfg.CommodityGroup != null) Flatten(cfg.CommodityGroup, all);

            CommodityGroup? Match(Func<string, bool> pred) =>
                all.FirstOrDefault(g => pred((g.Name ?? "").ToLowerInvariant()));

            return Match(n => n.Contains("kurcz") && n.Contains("brojler"))
                ?? Match(n => n.Contains("kurcz"))
                ?? Match(n => n.Contains("brojler"));   // ostatnia deska ratunku — może być inny gatunek!
        }
        private static void Flatten(CommodityGroup g, List<CommodityGroup> output)
        {
            output.Add(g);
            foreach (var sub in g.CommodityGroups) Flatten(sub, output);
        }

        // Klucz pola w formFieldsValues = ID pola jako string (§4.6 dokumentacji)
        private static string? FindFieldKey(CommodityGroup g, string typeName)
        {
            foreach (var f in g.FormFields)
                if (string.Equals(f.Type, typeName, StringComparison.OrdinalIgnoreCase))
                    return f.Id.ToString();
            return null;
        }

        private void ShowLoading(string text)
        {
            lblLoading.Text = text;
            loadingOverlay.Visibility = Visibility.Visible;
        }
        private void HideLoading() => loadingOverlay.Visibility = Visibility.Collapsed;
    }

    // ============ View model dla DataGrid historii ============
    public class HistRow
    {
        public DateTime OkresOd { get; set; }
        public DateTime OkresDo { get; set; }
        public string KategoriaTowaru { get; set; } = "";
        public decimal TonyRazem { get; set; }
        public decimal WartoscNetto { get; set; }
        public decimal CenaZlTona { get; set; }
        public string Status { get; set; } = "";
        public Brush StatusBg { get; set; } = Brushes.LightGray;
        public Brush StatusFg { get; set; } = Brushes.Black;
        public string? WyslanyPrzezImie { get; set; }
        public DateTime? WyslanyDataCzas { get; set; }
        public string? ErrorMessage { get; set; }

        public static HistRow From(SubmissionRow r)
        {
            (Brush bg, Brush fg) = r.Status switch
            {
                "Sent" => (NewBrush("#E8F5E9"), NewBrush("#1B5E20")),
                "Zero" => (NewBrush("#E3F2FD"), NewBrush("#0D47A1")),
                "Failed" => (NewBrush("#FDEAEA"), NewBrush("#B72A2A")),
                _ => (NewBrush("#FFF4E0"), NewBrush("#8A5500"))
            };
            return new HistRow
            {
                OkresOd = r.OkresOd, OkresDo = r.OkresDo,
                KategoriaTowaru = r.KategoriaTowaru,
                TonyRazem = r.TonyRazem,
                WartoscNetto = r.WartoscNetto,
                CenaZlTona = r.CenaZlTona,
                Status = r.Status,
                StatusBg = bg, StatusFg = fg,
                WyslanyPrzezImie = r.WyslanyPrzezImie,
                WyslanyDataCzas = r.WyslanyDataCzas,
                ErrorMessage = r.ErrorMessage
            };
        }
        private static Brush NewBrush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze(); return b;
        }
    }
}
