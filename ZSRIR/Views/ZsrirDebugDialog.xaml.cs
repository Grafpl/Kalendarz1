using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Kalendarz1.ZSRIR.Models;
using Kalendarz1.ZSRIR.Services;

namespace Kalendarz1.ZSRIR.Views
{
    // Debugger dla wysyłki ZSRIR — pokazuje WSZYSTKO (kontekst, IDs, raw responses z API, payload, response).
    // Jeden duży guzik "📋 SKOPIUJ WSZYSTKO DLA CLAUDE" generuje markdown z pełną diagnostyką.
    public partial class ZsrirDebugDialog : Window
    {
        // Kontekst wejściowy
        private readonly DateTime _od;
        private readonly DateTime _do;
        private readonly decimal _kg;
        private readonly decimal _wartosc;
        private readonly string _kategoria;
        private readonly ZsrirSecrets _secrets;

        // Dane z API (po Load)
        private int? _formReportingPeriodId;
        private int? _commodityGroupId;
        private int? _priceFieldId;
        private int? _amountFieldId;

        // Raw odpowiedzi z API (do zakładek + raportu diagnostycznego)
        private List<ReportingPeriod> _allPeriods = new();
        private FormConfiguration? _cfg;
        private string? _rawPeriodsJson;
        private string? _rawConfigJson;
        private string? _rawSuppliersJson;
        private string? _rawFormsJson;
        private List<DataSupplier> _allSuppliers = new();
        private List<FormInfo> _allForms = new();

        // Po wysyłce próbnej
        private string? _lastSendPayloadJson;
        private string? _lastSendResponseRaw;
        private int? _lastSendStatusCode;
        private string? _lastSendError;
        private string? _lastSendTraceMatch;   // np. "✓ kurcz+brojler" lub "⚠ brojler (gęś?)"

        // Repo (do UPSERT po sukcesie)
        private readonly ZsrirSubmissionsRepo _repo = new();

        // Kandydaci do ręcznego wyboru
        private List<KategoriaKandydat> _wszystkieKategorieKandydaci = new();
        private List<OkresKandydat> _wszystkieOkresyKandydaci = new();

        private class KategoriaKandydat
        {
            public CommodityGroup Group { get; init; } = new();
            public string Label => $"{Group.Id} — {Group.Name}";
            public override string ToString() => Label;
        }

        private class OkresKandydat
        {
            public ReportingPeriod Period { get; init; } = new();
            public string Match { get; init; } = "";   // np. "✓ dokładnie" / "○ tylko 'od'"
            public string Label => $"{Match} #{Period.Id}: {Period.DateFrom:dd.MM.yyyy} – {Period.DateTo:dd.MM.yyyy} (do: {Period.DateTimeEnd:dd.MM HH:mm}{(Period.IsOpen ? " ✓otwarty" : " ✗zamknięty")})";
            public override string ToString() => Label;
        }

        // PL culture
        private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

        public ZsrirDebugDialog(DateTime od, DateTime doD, decimal kg, decimal wartosc, string kategoria, ZsrirSecrets secrets)
        {
            InitializeComponent();
            _od = od; _do = doD; _kg = kg; _wartosc = wartosc; _kategoria = kategoria; _secrets = secrets;

            // Nagłówki
            lblOkres.Text = $"{_od:dd.MM.yyyy} – {_do:dd.MM.yyyy}";
            lblOkres2.Text = $"{_od:dd.MM.yyyy} – {_do:dd.MM.yyyy}";
            lblKategoria.Text = _kategoria;

            // Kalkulacja
            decimal tony = Math.Round(_kg / 1000m, 3);
            decimal cenaKg = _kg > 0 ? _wartosc / _kg : 0;
            decimal cenaTona = Math.Round(cenaKg * 1000m, 2);

            lblKg.Text = _kg.ToString("N2", Pl) + " kg";
            lblWartosc.Text = _wartosc.ToString("N2", Pl) + " zł";
            lblCenaKg.Text = cenaKg.ToString("N4", Pl) + " zł/kg";
            lblTony.Text = tony.ToString("N3", Pl);

            lblFormId.Text = _secrets.FormId?.ToString() ?? "—";
            lblDataSupplierId.Text = _secrets.DataSupplierId?.ToString() ?? "—";
            lblFormReportingPeriodId.Text = "(ładowanie...)";
            lblCommodityGroupId.Text = "(ładowanie...)";
            lblPriceFieldId.Text = "(ładowanie...)";
            lblAmountFieldId.Text = "(ładowanie...)";

            txtCenaTona.Text = cenaTona.ToString("0.##", Pl);
            txtTony.Text = tony.ToString("0.###", Pl);

            Loaded += async (_, __) => await LoadConfigAsync();
            RefreshPayloadPreview();
        }

        // ============ ŁADOWANIE KONFIGURACJI ============
        private async Task LoadConfigAsync()
        {
            if (!_secrets.IsConfigured || _secrets.FormId == null || _secrets.DataSupplierId == null)
            {
                lblFormReportingPeriodId.Text = "—";
                lblCommodityGroupId.Text = "—";
                lblPriceFieldId.Text = "—";
                lblAmountFieldId.Text = "—";
                lblHint.Text = "⚠ Brak konfiguracji ZSRIR — otwórz '⚙ Konfiguracja' w głównym oknie.";
                btnSendTest.IsEnabled = false;
                return;
            }

            ShowOverlay("Pobieranie konfiguracji z API ZSRIR...");
            try
            {
                using var api = new ZsrirApiClient(_secrets);

                _allPeriods = await api.GetReportingPeriodsAsync(_secrets.FormId!.Value);
                _rawPeriodsJson = api.LastPeriodsRawJson;

                _cfg = await api.GetFormConfigurationAsync(_secrets.FormId!.Value);
                _rawConfigJson = api.LastFormConfigRawJson;

                // Dodatkowa diagnostyka — co ZSRIR wie o naszym koncie
                try { _allSuppliers = await api.GetDataSuppliersAsync(); _rawSuppliersJson = api.LastDataSuppliersRawJson; }
                catch (Exception ex) { _rawSuppliersJson = "(błąd: " + ex.Message + ")"; }

                try
                {
                    if (_secrets.DataSupplierId.HasValue)
                    {
                        _allForms = await api.GetFormsAsync(_secrets.DataSupplierId.Value);
                        _rawFormsJson = api.LastFormsRawJson;
                    }
                }
                catch (Exception ex) { _rawFormsJson = "(błąd: " + ex.Message + ")"; }

                txtSuppliersRaw.Text = _rawSuppliersJson != null ? PrettyJson(_rawSuppliersJson) : "(brak — nie wywołano)";
                txtFormsRaw.Text = _rawFormsJson != null ? PrettyJson(_rawFormsJson) : "(brak — nie wywołano)";

                // BANNER — czerwone ostrzeżenie gdy brak okresów lub coś nie tak
                PokazBanner();

                // ===== OKRESY — auto match + lista kandydatów do combo =====
                _wszystkieOkresyKandydaci = _allPeriods
                    .OrderByDescending(p => p.DateFrom)
                    .Select(p =>
                    {
                        string m;
                        if (p.DateFrom.Date == _od && p.DateTo.Date == _do) m = "✓✓ dokładnie";
                        else if (p.DateFrom.Date == _od) m = "✓ pasuje 'od'";
                        else if (p.DateTo.Date == _do) m = "○ pasuje 'do'";
                        else m = "  inny";
                        return new OkresKandydat { Period = p, Match = m };
                    })
                    .ToList();

                var periodMatch = _allPeriods.FirstOrDefault(p => p.DateFrom.Date == _od && p.DateTo.Date == _do)
                               ?? _allPeriods.FirstOrDefault(p => p.IsOpen && p.DateFrom.Date == _od);
                _formReportingPeriodId = periodMatch?.Id;
                lblFormReportingPeriodId.Text = periodMatch != null
                    ? $"{periodMatch.Id} ({periodMatch.DateFrom:dd.MM} – {periodMatch.DateTo:dd.MM})"
                    : "(nie znaleziono — wybierz ręcznie poniżej!)";
                cmbOkresManualny.ItemsSource = _wszystkieOkresyKandydaci;
                cmbOkresManualny.SelectedItem = _wszystkieOkresyKandydaci.FirstOrDefault(k => k.Period.Id == periodMatch?.Id);

                // ===== KATEGORIE — kurcz+brojler priority =====
                var wszystkie = new List<CommodityGroup>();
                if (_cfg?.CommodityGroup != null) Spłaszcz(_cfg.CommodityGroup, wszystkie);

                _wszystkieKategorieKandydaci = wszystkie
                    .Where(g => { var n = (g.Name ?? "").ToLowerInvariant(); return n.Contains("kurcz") || n.Contains("brojler"); })
                    .Select(g => new KategoriaKandydat { Group = g })
                    .ToList();

                CommodityGroup? Pasuje(Func<string, bool> pred) =>
                    wszystkie.FirstOrDefault(g => pred((g.Name ?? "").ToLowerInvariant()));

                CommodityGroup? brojler;
                string traceKat;
                brojler = Pasuje(n => n.Contains("kurcz") && n.Contains("brojler"));
                if (brojler != null) traceKat = "✓ kurcz+brojler";
                else
                {
                    brojler = Pasuje(n => n.Contains("kurcz"));
                    if (brojler != null) traceKat = "kurcz (fallback)";
                    else
                    {
                        brojler = Pasuje(n => n.Contains("brojler"));
                        traceKat = brojler != null ? "⚠ brojler (UWAGA — może być gęś/indyk!)" : "(brak)";
                    }
                }
                _lastSendTraceMatch = traceKat;

                _commodityGroupId = brojler?.Id;
                lblCommodityGroupId.Text = brojler is not null
                    ? $"{brojler.Id} ({brojler.Name}) — {traceKat}"
                    : "(nie znaleziono grupy)";
                cmbKategoriaManualne.ItemsSource = _wszystkieKategorieKandydaci;
                cmbKategoriaManualne.SelectedItem = _wszystkieKategorieKandydaci.FirstOrDefault(k => k.Group.Id == brojler?.Id);

                // ===== POLA =====
                AktualizujPola(brojler);

                // ===== Zakładki diagnostyczne =====
                BudujTabelePeriods();
                BudujTabeleKategorii(wszystkie);
                BudujTabelePol(brojler);
                txtPeriodsRaw.Text = _rawPeriodsJson != null ? PrettyJson(_rawPeriodsJson) : "(brak)";
                txtConfigRaw.Text = _rawConfigJson != null ? PrettyJson(_rawConfigJson) : "(brak)";

                bool ready = _formReportingPeriodId != null && _commodityGroupId != null
                             && _priceFieldId != null && _amountFieldId != null;
                btnSendTest.IsEnabled = ready;
                lblHint.Text = ready
                    ? "Tip: 'Wyślij próbnie' wykonuje REALNĄ wysyłkę do ZSRIR. UWAŻAJ z kategorią — może być gęś."
                    : "⚠ Brakuje danych — wybierz ręcznie okres i/lub kategorię w combo.";
            }
            catch (Exception ex)
            {
                lblHint.Text = "❌ Błąd ładowania konfiguracji: " + ex.Message;
                MessageBox.Show("Błąd ładowania konfiguracji API:\n\n" + ex.Message, "ZSRIR Debug",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideOverlay();
                RefreshPayloadPreview();
            }
        }

        private void AktualizujPola(CommodityGroup? brojler)
        {
            var pola = new List<FormField>();
            if (brojler != null) pola.AddRange(brojler.FormFields);
            if (_cfg != null) pola.AddRange(_cfg.FormFields);

            var price = pola.FirstOrDefault(f => string.Equals(f.Type, "Price", StringComparison.OrdinalIgnoreCase));
            var amount = pola.FirstOrDefault(f => string.Equals(f.Type, "Amount", StringComparison.OrdinalIgnoreCase));
            var desc = pola.FirstOrDefault(f => string.Equals(f.Type, "Description", StringComparison.OrdinalIgnoreCase));
            _priceFieldId = price?.Id;
            _amountFieldId = amount?.Id;
            _commentFieldId = desc?.Id;
            lblPriceFieldId.Text = price != null ? $"{price.Id} ({price.Name}{(price.Unit != null ? ", " + price.Unit : "")})" : "(brak pola Price)";
            lblAmountFieldId.Text = amount != null ? $"{amount.Id} ({amount.Name}{(amount.Unit != null ? ", " + amount.Unit : "")})" : "(brak pola Amount)";
        }

        private static void Spłaszcz(CommodityGroup g, List<CommodityGroup> output)
        {
            output.Add(g);
            foreach (var sub in g.CommodityGroups) Spłaszcz(sub, output);
        }

        // ============ TABLE BUILDERS (zakładki diagnostyczne) ============
        private void BudujTabelePeriods()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ID    | DateFrom    DateTo      | DateTimeEnd        | IsOpen | Match (vs Twój okres)");
            sb.AppendLine("------+-------------------------+--------------------+--------+----------------------");
            foreach (var k in _wszystkieOkresyKandydaci)
            {
                sb.AppendLine($"{k.Period.Id,-5} | {k.Period.DateFrom:dd.MM.yyyy} {k.Period.DateTo:dd.MM.yyyy} | {k.Period.DateTimeEnd:dd.MM.yyyy HH:mm} | {(k.Period.IsOpen ? "OTW " : "ZAM ")}  | {k.Match}");
            }
            sb.AppendLine();
            sb.AppendLine($"Łącznie: {_allPeriods.Count} okresów.");
            sb.AppendLine($"Twój żądany okres: {_od:dd.MM.yyyy} – {_do:dd.MM.yyyy}");
            txtPeriodsTable.Text = sb.ToString();
        }

        private void BudujTabeleKategorii(List<CommodityGroup> wszystkie)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Wszystkie kategorie w drzewie ({wszystkie.Count}):");
            sb.AppendLine("ID    | Nazwa                                            | Mark");
            sb.AppendLine("------+--------------------------------------------------+------");
            foreach (var g in wszystkie.OrderBy(x => x.Id))
            {
                string n = (g.Name ?? "").ToLowerInvariant();
                string mark = "";
                if (n.Contains("kurcz") && n.Contains("brojler")) mark = "✓✓ kurcz+brojler";
                else if (n.Contains("kurcz")) mark = "✓  kurcz";
                else if (n.Contains("brojler")) mark = "⚠  brojler (inny gatunek?)";
                string nazwa = g.Name ?? "";
                if (nazwa.Length > 48) nazwa = nazwa.Substring(0, 48);
                sb.AppendLine($"{g.Id,-5} | {nazwa.PadRight(48)} | {mark}");
            }
            txtCategoriesTable.Text = sb.ToString();
        }

        private void BudujTabelePol(CommodityGroup? brojler)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Pola wybranej kategorii ({brojler?.Name ?? "—"}):");
            sb.AppendLine("ID    | Type        | Name                              | Unit       | Required | Min / Max");
            sb.AppendLine("------+-------------+-----------------------------------+------------+----------+------------");
            if (brojler != null)
                foreach (var f in brojler.FormFields)
                    sb.AppendLine($"{f.Id,-5} | {f.Type,-11} | {(f.Name ?? "").PadRight(33).Substring(0, 33)} | {(f.Unit ?? "").PadRight(10).Substring(0, 10)} | {(f.IsRequired ? "TAK     " : "nie     ")} | {f.MinValue?.ToString(Pl) ?? "—"} / {f.MaxValue?.ToString(Pl) ?? "—"}");

            sb.AppendLine();
            sb.AppendLine("Pola na poziomie ROOT (cfg.formFields):");
            sb.AppendLine("ID    | Type        | Name                              | Unit       | Required");
            sb.AppendLine("------+-------------+-----------------------------------+------------+---------");
            if (_cfg != null)
                foreach (var f in _cfg.FormFields)
                    sb.AppendLine($"{f.Id,-5} | {f.Type,-11} | {(f.Name ?? "").PadRight(33).Substring(0, 33)} | {(f.Unit ?? "").PadRight(10).Substring(0, 10)} | {(f.IsRequired ? "TAK" : "nie")}");
            txtFormFieldsTable.Text = sb.ToString();
        }

        // ============ COMBOBOXY ============
        private void CmbKategoriaManualne_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbKategoriaManualne.SelectedItem is not KategoriaKandydat kk) return;
            var brojler = kk.Group;
            _commodityGroupId = brojler.Id;
            lblCommodityGroupId.Text = $"{brojler.Id} ({brojler.Name}) — wybrane ręcznie";
            AktualizujPola(brojler);
            BudujTabelePol(brojler);
            btnSendTest.IsEnabled = _formReportingPeriodId != null && _commodityGroupId != null
                                    && _priceFieldId != null && _amountFieldId != null;
            RefreshPayloadPreview();
        }

        private void CmbOkresManualny_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbOkresManualny.SelectedItem is not OkresKandydat ok) return;
            _formReportingPeriodId = ok.Period.Id;
            lblFormReportingPeriodId.Text = $"{ok.Period.Id} ({ok.Period.DateFrom:dd.MM} – {ok.Period.DateTo:dd.MM}) — wybrane ręcznie";
            txtRecznyPeriodId.Text = ok.Period.Id.ToString();
            btnSendTest.IsEnabled = _formReportingPeriodId != null && _commodityGroupId != null
                                    && _priceFieldId != null && _amountFieldId != null;
            RefreshPayloadPreview();
        }

        // Ręczne wpisanie ID okresu (gdy MRiRW podało numer telefonicznie / mailem)
        private void TxtRecznyPeriodId_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string s = (txtRecznyPeriodId.Text ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return;
            if (int.TryParse(s, out int id) && id > 0)
            {
                _formReportingPeriodId = id;
                var match = _allPeriods.FirstOrDefault(p => p.Id == id);
                lblFormReportingPeriodId.Text = match != null
                    ? $"{id} ({match.DateFrom:dd.MM} – {match.DateTo:dd.MM}) — wpisane ręcznie ✓ pasuje do listy"
                    : $"{id} — wpisane ręcznie ⚠ NIE MA na liście API (na własną odpowiedzialność)";
                btnSendTest.IsEnabled = _commodityGroupId != null && _priceFieldId != null && _amountFieldId != null;
                RefreshPayloadPreview();
            }
        }

        // Banner ostrzeżenia — pokaż gdy API zwróciło 0 okresów lub coś nie gra.
        private void PokazBanner()
        {
            var problemy = new List<string>();

            if (_allPeriods.Count == 0)
            {
                // Oblicz najbliższy poniedziałek (typowy moment otwarcia nowego okresu w ZSRIR)
                var dzis = DateTime.Today;
                int dni = ((int)DayOfWeek.Monday - (int)dzis.DayOfWeek + 7) % 7;
                if (dni == 0) dni = 7;
                var nastepnyPn = dzis.AddDays(dni);
                problemy.Add($"• GetReportingPeriods zwróciło PUSTĄ LISTĘ dla FormId={_secrets.FormId}. "
                    + "Najpewniej (a) deadline minął i okres został zamknięty po stronie ZSRIR, "
                    + "albo (b) nowy okres jeszcze nie został otwarty. "
                    + $"Następny poniedziałek: {nastepnyPn:dd.MM.yyyy} (wtedy ZSRIR zwykle otwiera nowy okres tygodniowy). "
                    + "Tymczasem: zaloguj się na zsrir.minrol.gov.pl, albo zadzwoń do Pani Czeczko (22 623-16-06) "
                    + "z prośbą o reotwarcie zamkniętego okresu.");
            }

            if (_secrets.DataSupplierId.HasValue && _allSuppliers.Count > 0
                && !_allSuppliers.Any(s => s.Id == _secrets.DataSupplierId.Value))
                problemy.Add($"• DataSupplierId={_secrets.DataSupplierId} NIE WYSTĘPUJE w liście GetDataSuppliers "
                    + $"(API widzi {_allSuppliers.Count} dostawców: {string.Join(", ", _allSuppliers.Select(s => s.Id))}). "
                    + "Konto ma uprawnienia do innego dostawcy — przelogować / poprawić konfigurację.");

            if (_secrets.FormId.HasValue && _allForms.Count > 0
                && !_allForms.Any(f => f.Id == _secrets.FormId.Value))
                problemy.Add($"• FormId={_secrets.FormId} NIE WYSTĘPUJE w liście GetForms dla tego dostawcy "
                    + $"(API widzi: {string.Join(", ", _allForms.Select(f => $"{f.Id}={f.Name}"))}). "
                    + "Wybrany formularz nie jest dostępny dla tego DataSupplierId.");

            if (problemy.Count == 0)
            {
                bannerOstrzezenie.Visibility = Visibility.Collapsed;
                return;
            }
            lblBannerTresc.Text = string.Join("\n\n", problemy);
            bannerOstrzezenie.Visibility = Visibility.Visible;
        }

        // ID pola Komentarz (Description) — wyszukane runtime w cfg.FormFields, ale 270 to typowa wartość.
        private int? _commentFieldId;

        // ============ PAYLOAD PREVIEW ============
        private void RefreshPayloadPreview()
        {
            string price = txtCenaTona?.Text ?? "";
            string amount = txtTony?.Text ?? "";
            string komentarz = (txtKomentarz?.Text ?? "").Trim();

            var fieldValues = new Dictionary<string, string>
            {
                [(_priceFieldId?.ToString() ?? "?")] = price,
                [(_amountFieldId?.ToString() ?? "?")] = amount
            };
            if (!string.IsNullOrEmpty(komentarz) && _commentFieldId.HasValue)
                fieldValues[_commentFieldId.Value.ToString()] = komentarz;

            var body = new AddFormRequest
            {
                FormReportingPeriodId = _formReportingPeriodId ?? 0,
                DataSupplierId = _secrets.DataSupplierId ?? 0,
                Forms = new()
                {
                    new FormPayload
                    {
                        CommodityGroupId = _commodityGroupId ?? 0,
                        FormFieldsValues = fieldValues
                    }
                }
            };

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            txtPayload.Text = JsonSerializer.Serialize(body, opts);
        }

        // ============ HANDLERY UI ============
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void EditValue_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshPayloadPreview();
        private async void BtnReloadConfig_Click(object sender, RoutedEventArgs e) => await LoadConfigAsync();

        private void BtnReloadValues_Click(object sender, RoutedEventArgs e)
        {
            decimal tony = Math.Round(_kg / 1000m, 3);
            decimal cenaKg = _kg > 0 ? _wartosc / _kg : 0;
            decimal cenaTona = Math.Round(cenaKg * 1000m, 2);
            txtCenaTona.Text = cenaTona.ToString("0.##", Pl);
            txtTony.Text = tony.ToString("0.###", Pl);
        }

        private void BtnCopyPayload_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(txtPayload.Text ?? ""); lblHint.Text = "✓ Payload skopiowany do schowka."; }
            catch (Exception ex) { lblHint.Text = "❌ Nie udało się skopiować: " + ex.Message; }
        }

        private void BtnCopyResponse_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(txtResponse.Text ?? ""); lblHint.Text = "✓ Odpowiedź skopiowana do schowka."; }
            catch (Exception ex) { lblHint.Text = "❌ Nie udało się skopiować: " + ex.Message; }
        }

        private void BtnCopyPhone_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText("22 623-16-06"); lblHint.Text = "✓ Numer telefonu Czeczko skopiowany."; }
            catch { }
        }
        private void BtnCopyEmail_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText("malgorzata.czeczko@minrol.gov.pl"); lblHint.Text = "✓ Email Czeczko skopiowany."; }
            catch { }
        }

        // ============ WYSYŁKA ZERO ============
        // AddFormZero — formularz zerowy. Wysłany dla okresu, w którym nie było skupu.
        // Czasem pomaga "odblokować" zamknięty okres po stronie ZSRIR.
        private async void BtnSendZero_Click(object sender, RoutedEventArgs e)
        {
            if (_formReportingPeriodId == null)
            {
                MessageBox.Show("Brak FormReportingPeriodId — najpierw wybierz okres (z combo lub wpisz ID ręcznie).",
                    "ZSRIR Debug", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string msg = $"Wysłać formularz ZEROWY (AddFormZero)?\n\n" +
                $"FormReportingPeriodId: {_formReportingPeriodId}\n" +
                $"DataSupplierId: {_secrets.DataSupplierId}\n\n" +
                $"Znaczenie: zgłaszamy że w tym okresie NIE BYŁO skupu drobiu rzeźnego. " +
                $"To realna wysyłka do MRiRW.";
            if (MessageBox.Show(msg, "Wyślij formularz zerowy?",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            ShowOverlay("Wysyłanie AddFormZero do API ZSRIR...");
            txtResponse.Text = "";
            badgeStatus.Visibility = Visibility.Collapsed;

            string status = "Failed";
            string? response = null, errorMsg = null;
            int? statusCode = null;

            try
            {
                using var api = new ZsrirApiClient(_secrets);
                response = await api.AddFormZeroAsync(new AddFormZeroRequest
                {
                    FormReportingPeriodId = _formReportingPeriodId!.Value,
                    DataSupplierId = _secrets.DataSupplierId!.Value
                });
                statusCode = api.LastStatusCode;
                status = "Zero";
                ShowResponseBadge(statusCode ?? 200, "OK (ZERO)", "#1B5E20", "#E8F5E9");
                txtResponse.Text = PrettyJson(response ?? "");
                lblHint.Text = "✓ AddFormZero wysłany.";
            }
            catch (ZsrirApiException ex)
            {
                errorMsg = ex.Message;
                response = ex.RawBody;
                statusCode = ex.StatusCode;
                ShowResponseBadge(ex.StatusCode, $"HTTP {ex.StatusCode}", "#B71C1C", "#FFEBEE");
                txtResponse.Text = PrettyJson(ex.RawBody ?? "");
                lblHint.Text = "❌ AddFormZero nieudany.";
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                ShowResponseBadge(0, "EXCEPTION", "#B71C1C", "#FFEBEE");
                txtResponse.Text = ex.ToString();
                lblHint.Text = "❌ Wyjątek: " + ex.Message;
            }
            finally { HideOverlay(); }

            _lastSendResponseRaw = response;
            _lastSendStatusCode = statusCode;
            _lastSendError = errorMsg;
        }

        // ============ MEGA: SKOPIUJ WSZYSTKO ============
        private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
        {
            string raport = BudujRaportDiagnostyczny();
            try
            {
                Clipboard.SetText(raport);
                lblMegaHint.Text = $"✓ Skopiowano {raport.Length:N0} znaków do schowka. Wklej do Claude.";
            }
            catch (Exception ex) { lblMegaHint.Text = "❌ Schowek niedostępny: " + ex.Message; }
        }

        private void BtnSaveAll_Click(object sender, RoutedEventArgs e)
        {
            string raport = BudujRaportDiagnostyczny();
            try
            {
                string fn = $"ZSRIR_debug_{DateTime.Now:yyyy-MM-dd_HHmm}.md";
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fn);
                File.WriteAllText(path, raport, Encoding.UTF8);
                lblMegaHint.Text = $"✓ Zapisano: {path}";
            }
            catch (Exception ex) { lblMegaHint.Text = "❌ Zapis nieudany: " + ex.Message; }
        }

        // Budowa raportu markdown — WSZYSTKO co potrzebne do diagnostyki.
        private string BudujRaportDiagnostyczny()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# ZSRIR Debug Report — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("## Kontekst wejściowy (z aplikacji)");
            sb.AppendLine($"- Kategoria (hardcoded w ZPSP): **{_kategoria}**");
            sb.AppendLine($"- Okres żądany: **{_od:dd.MM.yyyy} – {_do:dd.MM.yyyy}**");
            sb.AppendLine($"- Kg razem (z HANDEL / Sage Symfonia, faktury skupu): **{_kg.ToString("N2", Pl)} kg**");
            sb.AppendLine($"- Wartość netto: **{_wartosc.ToString("N2", Pl)} zł**");
            decimal tony = Math.Round(_kg / 1000m, 3);
            decimal cenaKg = _kg > 0 ? _wartosc / _kg : 0;
            decimal cenaTona = Math.Round(cenaKg * 1000m, 2);
            sb.AppendLine($"- Cena: {cenaKg.ToString("N4", Pl)} zł/kg = {cenaTona.ToString("N2", Pl)} zł/t");
            sb.AppendLine($"- Tony (kg/1000): {tony.ToString("N3", Pl)}");
            sb.AppendLine();

            sb.AppendLine("## Konfiguracja API (z secrets.json)");
            sb.AppendLine($"- ApiBaseUrl: `{_secrets.ApiBaseUrl}`");
            sb.AppendLine($"- FormId: `{_secrets.FormId}`");
            sb.AppendLine($"- DataSupplierId: `{_secrets.DataSupplierId}`");
            sb.AppendLine();

            sb.AppendLine("## Wybrane ID (stan UI w momencie kopiowania)");
            sb.AppendLine($"- FormReportingPeriodId: **{_formReportingPeriodId?.ToString() ?? "(BRAK!)"}**");
            sb.AppendLine($"- CommodityGroupId: **{_commodityGroupId?.ToString() ?? "(BRAK!)"}** ({lblCommodityGroupId.Text})");
            sb.AppendLine($"- PriceFieldId: **{_priceFieldId?.ToString() ?? "(BRAK!)"}** ({lblPriceFieldId.Text})");
            sb.AppendLine($"- AmountFieldId: **{_amountFieldId?.ToString() ?? "(BRAK!)"}** ({lblAmountFieldId.Text})");
            sb.AppendLine($"- Auto-match kategorii: {_lastSendTraceMatch ?? "—"}");
            sb.AppendLine();

            sb.AppendLine("## Wartości w polach edycji");
            sb.AppendLine($"- Cena (tekst do wysłania): `{txtCenaTona.Text}`");
            sb.AppendLine($"- Ilość (tekst do wysłania): `{txtTony.Text}`");
            if (!string.IsNullOrWhiteSpace(txtKomentarz?.Text))
                sb.AppendLine($"- Komentarz: `{txtKomentarz.Text}`");
            sb.AppendLine();

            sb.AppendLine("## Payload (POST /api/DataSupplierFormApi/AddForm)");
            sb.AppendLine("```json");
            sb.AppendLine(txtPayload.Text ?? "");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## Ostatnia odpowiedź API (po 'Wyślij próbnie')");
            if (_lastSendStatusCode.HasValue || _lastSendResponseRaw != null || _lastSendError != null)
            {
                sb.AppendLine($"- Status HTTP: **{_lastSendStatusCode?.ToString() ?? "—"}**");
                if (_lastSendError != null) sb.AppendLine($"- Komunikat wyjątku: `{_lastSendError}`");
                sb.AppendLine("```json");
                sb.AppendLine(PrettyJson(_lastSendResponseRaw ?? "(brak)"));
                sb.AppendLine("```");
            }
            else
            {
                sb.AppendLine("(jeszcze nie wysłano — kliknij '🚀 Wyślij próbnie' żeby otrzymać odpowiedź API)");
            }
            sb.AppendLine();

            sb.AppendLine($"## Wszystkie okresy zwrócone przez GetReportingPeriods (łącznie {_allPeriods.Count})");
            sb.AppendLine("```");
            sb.AppendLine(txtPeriodsTable.Text);
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## Wszystkie kategorie (spłaszczone drzewo)");
            sb.AppendLine("```");
            sb.AppendLine(txtCategoriesTable.Text);
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## Pola formularza dla wybranej kategorii + root");
            sb.AppendLine("```");
            sb.AppendLine(txtFormFieldsTable.Text);
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## RAW: GetReportingPeriods response");
            sb.AppendLine("```json");
            sb.AppendLine(_rawPeriodsJson ?? "(brak)");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("## RAW: GetFormConfiguration response");
            sb.AppendLine("```json");
            sb.AppendLine(_rawConfigJson ?? "(brak)");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine($"## RAW: GetDataSuppliers response (dostawcy widoczni dla konta)");
            sb.AppendLine($"_Łącznie {_allSuppliers.Count}; oczekiwany DataSupplierId={_secrets.DataSupplierId}_");
            sb.AppendLine("```json");
            sb.AppendLine(_rawSuppliersJson ?? "(brak)");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine($"## RAW: GetForms response (formularze dla DataSupplierId={_secrets.DataSupplierId})");
            sb.AppendLine($"_Łącznie {_allForms.Count}; oczekiwany FormId={_secrets.FormId}_");
            sb.AppendLine("```json");
            sb.AppendLine(_rawFormsJson ?? "(brak)");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine("_Wygenerowane przez ZsrirDebugDialog (Kalendarz1 / ZPSP)._");
            return sb.ToString();
        }

        // ============ WYSYŁKA PRÓBNA ============
        private async void BtnSendTest_Click(object sender, RoutedEventArgs e)
        {
            if (_formReportingPeriodId == null || _commodityGroupId == null
                || _priceFieldId == null || _amountFieldId == null)
            {
                MessageBox.Show("Brak kompletu danych konfiguracji — wybierz okres i kategorię w combo.",
                    "ZSRIR Debug", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string priceStr = (txtCenaTona.Text ?? "").Trim();
            string amountStr = (txtTony.Text ?? "").Trim();
            if (string.IsNullOrEmpty(priceStr) || string.IsNullOrEmpty(amountStr))
            {
                MessageBox.Show("Wypełnij Cena i Ilość.", "ZSRIR Debug", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool priceParsed = decimal.TryParse(priceStr, NumberStyles.Number, Pl, out decimal priceDec);
            bool amountParsed = decimal.TryParse(amountStr, NumberStyles.Number, Pl, out decimal amountDec);
            if (!priceParsed || !amountParsed)
            {
                var ans = MessageBox.Show(
                    "Wartości nie parsują się jako liczba PL. Wysłać mimo to?",
                    "ZSRIR Debug", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ans != MessageBoxResult.Yes) return;
            }

            // Ostatnia szansa na sprawdzenie — pokaż dokładnie co lecie do API.
            string komentarz = (txtKomentarz?.Text ?? "").Trim();
            string confirmMsg = "🚀 REALNA WYSYŁKA do MRiRW przez API ZSRIR.\n\n"
                + $"Okres (Id): {_formReportingPeriodId}\n"
                + $"Kategoria (Id): {_commodityGroupId} — {(_wszystkieKategorieKandydaci.FirstOrDefault(k => k.Group.Id == _commodityGroupId)?.Group.Name ?? "?")}\n"
                + $"Cena: {priceStr} zł/tona (pole {_priceFieldId})\n"
                + $"Ilość: {amountStr} tony (pole {_amountFieldId})\n"
                + (string.IsNullOrEmpty(komentarz) ? "" : $"Komentarz: \"{komentarz}\" (pole {_commentFieldId})\n")
                + "\nKontynuować?";
            if (MessageBox.Show(confirmMsg, "Potwierdzenie realnej wysyłki",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            ShowOverlay("Wysyłanie do API ZSRIR...");
            txtResponse.Text = "";
            badgeStatus.Visibility = Visibility.Collapsed;

            string status = "Failed";
            string? response = null, errorMsg = null;
            int? statusCode = null;

            try
            {
                using var api = new ZsrirApiClient(_secrets);

                var fields = new Dictionary<string, string>
                {
                    [_priceFieldId!.Value.ToString()] = priceStr,
                    [_amountFieldId!.Value.ToString()] = amountStr
                };
                if (!string.IsNullOrEmpty(komentarz) && _commentFieldId.HasValue)
                    fields[_commentFieldId.Value.ToString()] = komentarz;

                var body = new AddFormRequest
                {
                    FormReportingPeriodId = _formReportingPeriodId!.Value,
                    DataSupplierId = _secrets.DataSupplierId!.Value,
                    Forms = new()
                    {
                        new FormPayload
                        {
                            CommodityGroupId = _commodityGroupId!.Value,
                            FormFieldsValues = fields
                        }
                    }
                };

                response = await api.AddFormAsync(body);
                statusCode = api.LastStatusCode;
                _lastSendPayloadJson = api.LastRequestJson;
                status = "Sent";
                ShowResponseBadge(statusCode ?? 200, "OK", "#1B5E20", "#E8F5E9");
                txtResponse.Text = PrettyJson(response ?? "");
            }
            catch (ZsrirApiException ex)
            {
                errorMsg = ex.Message;
                response = ex.RawBody;
                statusCode = ex.StatusCode;
                ShowResponseBadge(ex.StatusCode, $"HTTP {ex.StatusCode}", "#B71C1C", "#FFEBEE");
                txtResponse.Text = PrettyJson(ex.RawBody ?? "");
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                ShowResponseBadge(0, "EXCEPTION", "#B71C1C", "#FFEBEE");
                txtResponse.Text = ex.ToString();
            }
            finally
            {
                HideOverlay();
            }

            _lastSendResponseRaw = response;
            _lastSendStatusCode = statusCode;
            _lastSendError = errorMsg;

            // Zapis do historii (UPSERT)
            try
            {
                int? userId = int.TryParse(App.UserID, out int u) ? u : (int?)null;
                decimal tonyForHist = priceParsed && amountParsed ? amountDec : Math.Round(_kg / 1000m, 3);
                decimal cenaForHist = priceParsed && amountParsed ? priceDec : (_kg > 0 ? Math.Round((_wartosc / _kg) * 1000m, 2) : 0);

                await _repo.InsertAsync(new SubmissionRow
                {
                    OkresOd = _od,
                    OkresDo = _do,
                    KategoriaTowaru = _kategoria,
                    CommodityGroupId = _commodityGroupId,
                    KgRazem = Math.Round(_kg, 2),
                    TonyRazem = tonyForHist,
                    WartoscNetto = Math.Round(_wartosc, 2),
                    CenaZlTona = cenaForHist,
                    FormReportingPeriodId = _formReportingPeriodId,
                    DataSupplierId = _secrets.DataSupplierId,
                    Status = status,
                    ApiResponse = response,
                    ErrorMessage = errorMsg,
                    WyslanyPrzez = userId,
                    WyslanyDataCzas = DateTime.Now
                });
                lblHint.Text = status == "Sent" ? "✓ Wysłano. Wpis zapisany w historii." : "❌ Błąd. Wpis zapisany w historii ze statusem Failed.";
            }
            catch (Exception saveEx)
            {
                lblHint.Text = "⚠ Zapis do historii nieudany: " + saveEx.Message;
            }
        }

        // ============ Helpers ============
        private void ShowOverlay(string msg)
        {
            lblOverlayMsg.Text = msg;
            overlay.Visibility = Visibility.Visible;
        }
        private void HideOverlay() => overlay.Visibility = Visibility.Collapsed;

        private void ShowResponseBadge(int code, string label, string fgHex, string bgHex)
        {
            lblStatusCode.Text = label;
            lblStatusCode.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex));
            badgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex));
            badgeStatus.Visibility = Visibility.Visible;
        }

        private static string PrettyJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "(pusta odpowiedź)";
            try
            {
                using var doc = JsonDocument.Parse(raw);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            catch { return raw; }
        }
    }
}
