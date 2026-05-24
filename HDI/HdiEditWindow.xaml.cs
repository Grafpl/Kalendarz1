using Kalendarz1.HDI.Models;
using Kalendarz1.HDI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Kalendarz1.HDI
{
    public partial class HdiEditWindow : Window
    {
        private readonly HdiService _service = new();
        private HdiDokument _model;
        public ObservableCollection<HdiPartia> PartieRows { get; } = new();

        // Cache zgenerowanego PDF — preview otwiera się instant z pamięci.
        // Inwalidowany przy każdej zmianie (BindToModel).
        private byte[]? _cachedPdfBytes = null;
        private List<byte[]>? _cachedPdfImages = null;
        private System.Threading.CancellationTokenSource? _pdfCacheCts;

        // Dirty tracking — ostrzeż usera przed utratą niezapisanych zmian
        private bool _isDirty = false;
        private bool _savedSuccessfully = false;
        private bool _suppressDirty = false;   // gdy programowo wypełniamy (BindFromModel/auto-fill), nie ustawiaj dirty

        public HdiEditWindow() : this(null, null, null) { }
        public HdiEditWindow(int? existingId) : this(existingId, null, null) { }

        // Nowy konstruktor: orderId + numerFaktury — auto-fill z faktury Symfonii (lub zamówienia).
        // Używany z Panel Faktur: prawy klik na zamówieniu → "Zrób HDI" → wypełnia wszystkie pola.
        public HdiEditWindow(int? existingId, int? orderId, string? numerFaktury)
        {
            InitializeComponent();
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }

            // Wystawiający — preferuj App.UserFullName, ale gdy puste/równe ID, dociągniemy z DB w Loaded.
            string uid = Kalendarz1.App.UserID ?? "";
            string ufn = Kalendarz1.App.UserFullName ?? "";
            string wystawiajacyInit = !string.IsNullOrWhiteSpace(ufn) && !string.Equals(ufn, uid, StringComparison.OrdinalIgnoreCase)
                ? ufn : uid;
            _model = new HdiDokument
            {
                Rok = DateTime.Now.Year % 100,
                DataWystawienia = DateTime.Now,
                UtworzonoPrzez = uid,
                Wystawiajacy = wystawiajacyInit
            };

            GridPartie.ItemsSource = PartieRows;

            // Gdy user edytuje DataUboju per wiersz partii → automatycznie przelicz DataPrzydatnosci.
            // (Tuszka 7 dni, Elementy 6, Podroby 4, Mrożone +6 miesięcy)
            GridPartie.CellEditEnding += GridPartie_CellEditEnding;

            RbInny.Checked += (s, e) => TxtInnePanstwo.IsEnabled = true;
            RbInny.Unchecked += (s, e) => TxtInnePanstwo.IsEnabled = false;

            // Ostrzeż przed zamknięciem [X] z niezapisanymi zmianami
            Closing += (s, e) =>
            {
                if (_isDirty && !_savedSuccessfully)
                {
                    var r = MessageBox.Show(this,
                        "Masz niezapisane zmiany.\n\nZamknąć BEZ ZAPISU?",
                        "Niezapisane zmiany", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (r != MessageBoxResult.Yes) e.Cancel = true;
                }
            };

            Loaded += async (s, e) =>
            {
                // Hook dirty tracking RAZ — przed pierwszym BindFromModel (który resetuje dirty)
                HookDirtyTracking();

                if (existingId.HasValue)
                {
                    var loaded = await _service.GetByIdAsync(existingId.Value);
                    if (loaded != null) { _model = loaded; BindFromModel(); LblTitle.Text = $"📋 HDI {_model.NumerPelny}"; }
                    return;
                }

                int next = await _service.GetNextNumberAsync(_model.Rok);
                _model.Numer = next;
                LblNumer.Text = $"{next}/{_model.Rok:00}";

                // Dociągnij pełne imię + nazwisko wystawiającego z LibraNet.dbo.operators
                // gdy App.UserFullName puste lub równe UserID (dla starych logowań).
                if (string.IsNullOrWhiteSpace(_model.Wystawiajacy)
                    || string.Equals(_model.Wystawiajacy, _model.UtworzonoPrzez, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var fullName = await _service.GetOperatorFullNameAsync(_model.UtworzonoPrzez);
                        if (!string.IsNullOrWhiteSpace(fullName)) _model.Wystawiajacy = fullName;
                    }
                    catch { /* best-effort */ }
                }

                bool filledOk = false;
                bool willAutoFill = !string.IsNullOrWhiteSpace(numerFaktury) || orderId.HasValue;
                if (willAutoFill)
                {
                    ShowLoading("Pobieranie danych z bazy…");
                    HdiDiag.Log("HdiEditWindow", $"🚀 LOADED — auto-fill start: numerFaktury='{numerFaktury}' orderId={orderId}");
                }
                try
                {
                    if (!string.IsNullOrWhiteSpace(numerFaktury))
                        filledOk = await TryAutoFillFromInvoiceAsync(numerFaktury, orderId);

                    if (!filledOk && orderId.HasValue)
                    {
                        HdiDiag.Log("HdiEditWindow", $"Fallback do TryAutoFillFromOrderAsync({orderId.Value}) bo invoice path nie dał wyników");
                        TxtZamowienieId.Text = orderId.Value.ToString();
                        filledOk = await TryAutoFillFromOrderAsync(orderId.Value);
                    }

                    HdiDiag.Log("HdiEditWindow", $"🏁 auto-fill END: filledOk={filledOk}, Partie.Count={_model.Partie.Count}, KlientNazwa='{_model.KlientNazwa}'");
                }
                finally { if (willAutoFill) HideLoading(); }

                BindFromModel();

                // Krok 3: dodatkowy retry — uzupełnij brakujące pola z zamówienia,
                // gdy z faktury załadowano klienta ale partie/daty są niekompletne.
                if (filledOk && orderId.HasValue && _model.Partie.Count == 0)
                {
                    await TryAutoFillFromOrderAsync(orderId.Value);
                    BindFromModel();
                }
            };
        }

        // Auto-fill priority: faktura z Symfonii (HM.DP) — wierne kopiowanie tego co fakturzystka widzi.
        // Pobiera: klient + adres, opis towaru (1 lub wiele asortymentów), wagi netto/brutto,
        // liczbę opakowań (z zamówienia jeśli dostępne), numer rejestracyjny pojazdu (z transportu),
        // PER-ASORTYMENT partie (z listapartii dla daty uboju, podzielone proporcjonalnie wg wagi linii).
        // Zwraca true jeśli udało się załadować klienta i/lub pozycje.
        private async Task<bool> TryAutoFillFromInvoiceAsync(string numerFaktury, int? orderId)
        {
            HdiDiag.Log("HdiEditWindow", $"⚡ START TryAutoFillFromInvoiceAsync('{numerFaktury}', orderId={orderId})");
            var swAll = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // ⚡ PARALLEL: 3 niezależne zapytania jednocześnie zamiast sekwencyjnie.
                // Skraca czas z ~3-5s do ~1-1.5s w typowym przypadku.
                HdiDiag.Log("HdiEditWindow", "Start 3 PARALLEL tasks (Invoice+Transport+Order)");
                var invTask   = _service.LoadFromInvoiceAsync(numerFaktury);
                var transTask = orderId.HasValue ? _service.LoadTransportInfoAsync(orderId.Value)
                                                  : Task.FromResult<HdiService.TransportInfo?>(null);
                var orderTask = orderId.HasValue ? _service.LoadZamowienieAutoFillAsync(orderId.Value)
                                                  : Task.FromResult<HdiService.ZamowienieAutoFill?>(null);
                await Task.WhenAll(invTask, transTask, orderTask);
                var inv   = invTask.Result;
                var trans = transTask.Result;
                var order = orderTask.Result;

                if (inv == null)
                {
                    LblStatus.Text = $"⚠ Nie znaleziono faktury '{numerFaktury}' w Symfonii — próbuję zamówienia…";
                    return false;
                }

                // ── ID + KLIENT ────────────────────────────────────────────
                if (orderId.HasValue) { _model.ZamowienieId = orderId; TxtZamowienieId.Text = orderId.Value.ToString(); }
                _model.KlientId = inv.Khid;
                if (!string.IsNullOrWhiteSpace(inv.KlientNazwa)) _model.KlientNazwa = inv.KlientNazwa;
                if (!string.IsNullOrWhiteSpace(inv.KlientAdres))
                {
                    _model.KlientAdres = inv.KlientAdres;
                    _model.MiejscePrzeznaczenia = string.IsNullOrWhiteSpace(inv.KlientNazwa)
                        ? inv.KlientAdres : $"{inv.KlientNazwa}, {inv.KlientAdres}";
                }

                // ── DATY + OPIS + WAGI ────────────────────────────────────
                var dataUboju = inv.DataSprzedazy ?? inv.DataWystawienia ?? DateTime.Today;
                _model.DataWysylki = dataUboju;
                var nazwy = inv.Pozycje.Select(p => p.Nazwa).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
                _model.OpisTowaru = string.Join(", ", nazwy);
                if (inv.SumaIlosc > 0)
                {
                    _model.WagaNetto = inv.SumaIlosc;
                    if (!_model.WagaBrutto.HasValue || _model.WagaBrutto == 0)
                        _model.WagaBrutto = Math.Round(inv.SumaIlosc * 1.015m, 0);
                }

                // ── TRANSPORT (już pobrane równolegle) ────────────────────
                if (trans != null && !string.IsNullOrWhiteSpace(trans.NumerRejestracyjny)
                    && string.IsNullOrWhiteSpace(_model.NumerRejestracyjny))
                    _model.NumerRejestracyjny = trans.NumerRejestracyjny;

                // ── LICZBA OPAKOWAŃ z zamówienia (już pobrane równolegle) ─
                if (order?.Pozycje != null && order.Pozycje.Count > 0 && !_model.LiczbaOpakowan.HasValue)
                {
                    int sumaPoj = order.Pozycje.Where(p => p.Pojemniki.HasValue).Sum(p => p.Pojemniki!.Value);
                    if (sumaPoj > 0) _model.LiczbaOpakowan = sumaPoj;
                }

                // ── PARTIE: per-asortyment, RÓWNOLEGLE ────────────────────
                // Wcześniej: foreach + await (sekwencyjnie N×czas zapytania)
                // Teraz: Task.WhenAll dla wszystkich asortymentów naraz.
                //
                // FALLBACK: jeśli faktura nie ma pozycji ALE zamówienie ma — bierzemy
                // pozycje z zamówienia. Inaczej tabela partii byłaby pusta.
                var srcPozycje = new List<(string Nazwa, decimal Ilosc, int Twid)>();
                if (inv.Pozycje.Count > 0)
                    srcPozycje.AddRange(inv.Pozycje.Select(p => (p.Nazwa, p.Ilosc, p.Idtw)));
                else if (order?.Pozycje != null)
                    srcPozycje.AddRange(order.Pozycje.Select(p => (p.Nazwa, p.Ilosc, p.KodTowaru)));

                var groups = srcPozycje
                    .Where(p => !string.IsNullOrWhiteSpace(p.Nazwa) && p.Ilosc > 0)
                    .GroupBy(p => p.Nazwa)
                    .Select(g => new { Asortyment = g.Key, SumKg = g.Sum(x => x.Ilosc), Twid = g.First().Twid })
                    .ToList();

                // Batch load obrazków towarów — jedno zapytanie dla wszystkich asortymentów
                var twImagesTask = _service.GetTowarImagesAsync(groups.Select(g => g.Twid));

                var partieTasks = groups.Select(g =>
                    _service.LoadPartieDlaDniaAsync(dataUboju, g.Asortyment)
                ).ToList();
                var partieResults = partieTasks.Count > 0
                    ? await Task.WhenAll(partieTasks)
                    : System.Array.Empty<List<HdiPartia>>();
                var twImages = await twImagesTask;

                var allPartie = new List<HdiPartia>();
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var g = groups[gi];
                    var partieDlaDnia = partieResults[gi];
                    var dataPrzyd = HdiProduktKlasyfikator.PrzydatnoscOd(dataUboju.Date, g.Asortyment);
                    bool mrozony = HdiProduktKlasyfikator.IsMrozony(g.Asortyment);
                    twImages.TryGetValue(g.Twid, out var img);
                    if (partieDlaDnia.Count > 0)
                    {
                        decimal perPartia = Math.Round(g.SumKg / partieDlaDnia.Count, 0);
                        for (int i = 0; i < partieDlaDnia.Count; i++)
                        {
                            partieDlaDnia[i].Asortyment = g.Asortyment;
                            partieDlaDnia[i].WagaKg = perPartia;
                            partieDlaDnia[i].DataPrzydatnosci = dataPrzyd;
                            if (!mrozony) partieDlaDnia[i].DataMrozenia = null;
                            partieDlaDnia[i].Idtw = g.Twid;
                            partieDlaDnia[i].Image = img;
                        }
                        partieDlaDnia[^1].WagaKg = g.SumKg - perPartia * (partieDlaDnia.Count - 1);
                        allPartie.AddRange(partieDlaDnia);
                    }
                    else
                    {
                        allPartie.Add(new HdiPartia
                        {
                            Asortyment = g.Asortyment,
                            NumerPartii = "",
                            DataUboju = dataUboju.Date,
                            DataMrozenia = mrozony ? dataUboju.Date : (DateTime?)null,
                            DataPrzydatnosci = dataPrzyd,
                            WagaKg = g.SumKg,
                            Idtw = g.Twid,
                            Image = img
                        });
                    }
                }
                if (allPartie.Count > 0) _model.Partie = allPartie;

                LblStatus.Text = $"✓ Z faktury {numerFaktury}: {inv.Pozycje.Count} poz · {inv.SumaIlosc:0.##} kg · {allPartie.Count} part. · {(string.IsNullOrEmpty(_model.NumerRejestracyjny) ? "—" : _model.NumerRejestracyjny)}";
                swAll.Stop();
                HdiDiag.Time("HdiEditWindow", $"✅ DONE TryAutoFillFromInvoiceAsync: {inv.Pozycje.Count} poz, {allPartie.Count} part, klient='{_model.KlientNazwa}'", swAll.ElapsedMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"⚠ Błąd auto-fill faktury: {ex.Message}";
                HdiDiag.Error("HdiEditWindow", "TryAutoFillFromInvoiceAsync EXCEPTION", ex);
                return false;
            }
        }

        // Fallback gdy zamówienie nie ma jeszcze numeru faktury — wypełniamy z LibraNet.
        // Również używane jako uzupełnienie po fakturze (gdy brakuje partii / opakowań / transportu).
        private async Task<bool> TryAutoFillFromOrderAsync(int orderId)
        {
            try
            {
                var fill = await _service.LoadZamowienieAutoFillAsync(orderId);
                if (fill == null) { LblStatus.Text = $"⚠ Nie znaleziono zamówienia #{orderId}."; return false; }

                _model.ZamowienieId = fill.ZamowienieId;
                if (!_model.KlientId.HasValue) _model.KlientId = fill.KlientId;
                if (string.IsNullOrWhiteSpace(_model.KlientNazwa) && !string.IsNullOrWhiteSpace(fill.KlientNazwa)) _model.KlientNazwa = fill.KlientNazwa;
                if (string.IsNullOrWhiteSpace(_model.KlientAdres) && !string.IsNullOrWhiteSpace(fill.KlientAdres)) _model.KlientAdres = fill.KlientAdres;
                if (string.IsNullOrWhiteSpace(_model.MiejscePrzeznaczenia))
                {
                    if (!string.IsNullOrWhiteSpace(fill.KlientAdres))
                    {
                        _model.MiejscePrzeznaczenia = string.IsNullOrWhiteSpace(fill.KlientNazwa)
                            ? fill.KlientAdres
                            : $"{fill.KlientNazwa}, {fill.KlientAdres}";
                    }
                }
                if (!_model.DataWysylki.HasValue) _model.DataWysylki = fill.DataWysylki ?? fill.DataUboju;

                if (fill.Pozycje.Count > 0)
                {
                    var nazwy = fill.Pozycje.Select(p => p.Nazwa).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
                    if (string.IsNullOrWhiteSpace(_model.OpisTowaru))
                        _model.OpisTowaru = string.Join(", ", nazwy);

                    decimal sumaIl = fill.Pozycje.Sum(p => p.Ilosc);
                    if (!_model.WagaNetto.HasValue && sumaIl > 0) _model.WagaNetto = sumaIl;
                    if ((!_model.WagaBrutto.HasValue || _model.WagaBrutto == 0) && sumaIl > 0)
                        _model.WagaBrutto = Math.Round(sumaIl * 1.015m, 0);

                    int sumaPoj = fill.Pozycje.Where(p => p.Pojemniki.HasValue).Sum(p => p.Pojemniki!.Value);
                    if (!_model.LiczbaOpakowan.HasValue && sumaPoj > 0) _model.LiczbaOpakowan = sumaPoj;

                    // ⚡ PARALLEL: partie per asortyment + transport jednocześnie.
                    // FIX: nie wymagamy fill.DataUboju.HasValue — gdy null, używamy DataWysylki lub Today
                    // żeby ZAWSZE stworzyć wiersze partii dla każdego asortymentu.
                    if (_model.Partie.Count == 0 && fill.Pozycje.Count > 0)
                    {
                        var dataUbojuFallback = fill.DataUboju ?? fill.DataWysylki ?? DateTime.Today;
                        var groups = fill.Pozycje
                            .Where(p => !string.IsNullOrWhiteSpace(p.Nazwa) && p.Ilosc > 0)
                            .GroupBy(p => p.Nazwa)
                            .Select(g => new { Asortyment = g.Key, SumKg = g.Sum(x => x.Ilosc) })
                            .ToList();

                        var partieTasks = groups.Select(g =>
                            _service.LoadPartieDlaDniaAsync(dataUbojuFallback, g.Asortyment)
                        ).ToList();
                        var transTask = string.IsNullOrWhiteSpace(_model.NumerRejestracyjny)
                            ? _service.LoadTransportInfoAsync(orderId)
                            : Task.FromResult<HdiService.TransportInfo?>(null);

                        await Task.WhenAll(partieTasks.Cast<Task>().Append(transTask));
                        var partieResults = partieTasks.Select(t => t.Result).ToList();
                        var trans = transTask.Result;

                        var allPartie = new List<HdiPartia>();
                        for (int gi = 0; gi < groups.Count; gi++)
                        {
                            var g = groups[gi];
                            var partieDlaDnia = partieResults[gi];
                            var dataPrzyd = HdiProduktKlasyfikator.PrzydatnoscOd(dataUbojuFallback.Date, g.Asortyment);
                            bool mrozony = HdiProduktKlasyfikator.IsMrozony(g.Asortyment);
                            if (partieDlaDnia.Count > 0)
                            {
                                decimal perPartia = Math.Round(g.SumKg / partieDlaDnia.Count, 0);
                                for (int i = 0; i < partieDlaDnia.Count; i++)
                                {
                                    partieDlaDnia[i].Asortyment = g.Asortyment;
                                    partieDlaDnia[i].WagaKg = perPartia;
                                    partieDlaDnia[i].DataPrzydatnosci = dataPrzyd;
                                    if (!mrozony) partieDlaDnia[i].DataMrozenia = null;
                                }
                                partieDlaDnia[^1].WagaKg = g.SumKg - perPartia * (partieDlaDnia.Count - 1);
                                allPartie.AddRange(partieDlaDnia);
                            }
                            else
                            {
                                // Fallback row — listapartii nie ma rekordów dla tego dnia,
                                // tworzymy 1 wiersz z asortymentem + wagą żeby user widział towar w tabeli.
                                allPartie.Add(new HdiPartia
                                {
                                    Asortyment = g.Asortyment, NumerPartii = "",
                                    DataUboju = dataUbojuFallback.Date,
                                    DataMrozenia = mrozony ? dataUbojuFallback.Date : (DateTime?)null,
                                    DataPrzydatnosci = dataPrzyd,
                                    WagaKg = g.SumKg
                                });
                            }
                        }
                        if (allPartie.Count > 0) _model.Partie = allPartie;

                        if (trans != null && !string.IsNullOrWhiteSpace(trans.NumerRejestracyjny)
                            && string.IsNullOrWhiteSpace(_model.NumerRejestracyjny))
                            _model.NumerRejestracyjny = trans.NumerRejestracyjny;
                    }
                }

                if (LblStatus.Text.StartsWith("⚠") || string.IsNullOrEmpty(LblStatus.Text))
                    LblStatus.Text = $"✓ Z zamówienia #{orderId}: {fill.Pozycje.Count} pozycji · klient {fill.KlientNazwa} · {(string.IsNullOrEmpty(_model.NumerRejestracyjny) ? "—" : _model.NumerRejestracyjny)}";
                return true;
            }
            catch (Exception ex) { LblStatus.Text = $"⚠ Błąd auto-fill zamówienia: {ex.Message}"; return false; }
        }

        // ── Bindowanie z/do modelu ──────────────────────────────────────────
        // Flagi blokujące pętle handlerów: gdy programowo aktualizujemy checkbox/textbox,
        // nie wywołujemy handlerów które by od razu nadpisały wartość.
        private bool _suppressPackagingUpdate = false;

        private void BindFromModel()
        {
            var swBind = System.Diagnostics.Stopwatch.StartNew();
            HdiDiag.Log("BindFromModel", "START");
            LblNumer.Text = _model.NumerPelny;
            TxtKlientNazwa.Text = _model.KlientNazwa;
            TxtKlientAdres.Text = _model.KlientAdres;
            TxtOpisTowaru.Text = _model.OpisTowaru;
            TxtRodzajOpakowan.Text = _model.RodzajOpakowan;
            // Zsynchronizuj 4 checkboxy z istniejącym stringiem (case-insensitive contains)
            SyncPackagingCheckboxesFromText(_model.RodzajOpakowan);
            TxtLiczbaOpakowan.Text = _model.LiczbaOpakowan?.ToString() ?? "";
            TxtWagaNetto.Text = _model.WagaNetto?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            TxtWagaBrutto.Text = _model.WagaBrutto?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
            DpDataWysylki.SelectedDate = _model.DataWysylki;
            TxtNumerRejestracyjny.Text = _model.NumerRejestracyjny;
            TxtNumerRejNaczepy.Text = _model.NumerRejNaczepy;
            TxtUwagiTransport.Text = _model.UwagiTransport;
            TxtMiejscowosc.Text = _model.MiejscowoscWystawienia;
            DpDataWystawienia.SelectedDate = _model.DataWystawienia;
            // Wystawiający — pełne imię + nazwisko (gotowe pod parafkę).
            // Fallback chain: model.Wystawiajacy → App.UserFullName → App.UserID.
            string ufn = Kalendarz1.App.UserFullName ?? "";
            string uid = Kalendarz1.App.UserID ?? "";
            TxtWystawiajacy.Text = !string.IsNullOrWhiteSpace(_model.Wystawiajacy) ? _model.Wystawiajacy
                                 : !string.IsNullOrWhiteSpace(ufn) ? ufn
                                 : uid;

            RbKrajowy.IsChecked = _model.RynekKrajowy;
            RbUE.IsChecked = _model.RynekUE;
            RbInny.IsChecked = _model.RynekInny;
            TxtInnePanstwo.Text = _model.InnePanstwo;
            TxtInnePanstwo.IsEnabled = _model.RynekInny;

            HdiDiag.Time("BindFromModel", "All TextBox/DatePicker filled", swBind.ElapsedMilliseconds);
            var swPartie = System.Diagnostics.Stopwatch.StartNew();
            PartieRows.Clear();
            foreach (var p in _model.Partie) PartieRows.Add(p);
            HdiDiag.Time("BindFromModel", $"PartieRows populated ({_model.Partie.Count} rows — TRIGGERS DataGrid render)", swPartie.ElapsedMilliseconds);

            SchedulePdfPrecache();
            _isDirty = false;
            HdiDiag.Time("BindFromModel", "✅ DONE (after this UI should be responsive)", swBind.ElapsedMilliseconds);

            // Log gdy WPF skończy render (Loaded priority = bardzo nisko, fires gdy nic innego się nie dzieje)
            Dispatcher.BeginInvoke(new Action(() =>
                HdiDiag.Log("BindFromModel", "🎨 WPF render dispatcher returned — UI is now responsive")),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // Wpinane raz w Loaded — łapie wszystkie edycje pól użytkownika do _isDirty.
        // Programowe zmiany przez BindFromModel są chronione przez _suppressDirty (nie używane teraz,
        // bo BindFromModel ustawia _isDirty=false na końcu).
        private void HookDirtyTracking()
        {
            void OnText(object s, TextChangedEventArgs e) { if (!_suppressDirty) _isDirty = true; }
            void OnSel(object s, SelectionChangedEventArgs e) { if (!_suppressDirty) _isDirty = true; }
            void OnChk(object s, RoutedEventArgs e) { if (!_suppressDirty) _isDirty = true; }
            void OnDate(object s, SelectionChangedEventArgs e) { if (!_suppressDirty) _isDirty = true; }

            TxtKlientNazwa.TextChanged += OnText;
            TxtKlientAdres.TextChanged += OnText;
            TxtOpisTowaru.TextChanged += OnText;
            TxtRodzajOpakowan.TextChanged += OnText;
            TxtLiczbaOpakowan.TextChanged += OnText;
            TxtWagaNetto.TextChanged += OnText;
            TxtWagaBrutto.TextChanged += OnText;
            TxtNumerRejestracyjny.TextChanged += OnText;
            TxtNumerRejNaczepy.TextChanged += OnText;
            TxtUwagiTransport.TextChanged += OnText;
            TxtMiejscowosc.TextChanged += OnText;
            TxtWystawiajacy.TextChanged += OnText;
            TxtInnePanstwo.TextChanged += OnText;
            DpDataWysylki.SelectedDateChanged += OnDate;
            DpDataWystawienia.SelectedDateChanged += OnDate;
            ChkPaletaDrewno.Checked += OnChk; ChkPaletaDrewno.Unchecked += OnChk;
            ChkPoliblok.Checked += OnChk; ChkPoliblok.Unchecked += OnChk;
            ChkPaletaH1.Checked += OnChk; ChkPaletaH1.Unchecked += OnChk;
            ChkPaletaEuro.Checked += OnChk; ChkPaletaEuro.Unchecked += OnChk;
            ChkPojemnikE2.Checked += OnChk; ChkPojemnikE2.Unchecked += OnChk;
            RbKrajowy.Checked += OnChk; RbUE.Checked += OnChk; RbInny.Checked += OnChk;
            PartieRows.CollectionChanged += (s, e) => { if (!_suppressDirty) _isDirty = true; };
        }

        // Auto-przeliczanie dat dla wiersza partii po zmianie DataUboju.
        // ZACHOWUJE DELTĘ: gdy stara DataPrzydatnosci była uboj+6 dni, po zmianie uboju
        // nowa DataPrzydatnosci też będzie nowy_uboj+6 dni (user już ją skorygował ręcznie,
        // nie wracamy do auto-klasyfikatora).
        //
        // Przykład user'a: uboj 1 stycznia → przyd 7 stycznia (delta=6). Zmień uboj→2 stycznia,
        // przyd automatycznie skoczy na 8 stycznia (zachowana delta=6).
        //
        // Pierwsza edycja (gdy partia świeżo dodana, brak starych dat) → użyj klasyfikatora.
        private void DpDataUbojuPartia_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DatePicker dp) return;
            if (dp.DataContext is not HdiPartia partia) return;
            if (!partia.DataUboju.HasValue) return;

            // Wyciągnij STARĄ datę uboju z eventArgs (przed user'ską zmianą)
            DateTime? oldUboj = null;
            if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is DateTime rem) oldUboj = rem;
            DateTime newUboj = partia.DataUboju.Value.Date;

            // Mamy starą datę uboju i starą przydatność → PRZESUŃ proporcjonalnie (zachowaj deltę)
            if (oldUboj.HasValue && partia.DataPrzydatnosci.HasValue)
            {
                var delta = partia.DataPrzydatnosci.Value.Date - oldUboj.Value.Date;
                partia.DataPrzydatnosci = newUboj + delta;

                // To samo dla DataMrozenia (jeśli była ustawiona — czyli mrożone)
                if (partia.DataMrozenia.HasValue)
                {
                    var deltaMroz = partia.DataMrozenia.Value.Date - oldUboj.Value.Date;
                    partia.DataMrozenia = newUboj + deltaMroz;
                }

                // INPC w HdiPartia powoduje że bindingi DatePicker auto-update — Refresh() nie potrzebny
                // (i powodował "Refresh niedozwolony podczas EditItem transaction").
            }
            else
            {
                // Brak starej daty (pierwsze ustawienie) → użyj auto-klasyfikatora
                RecalcPartiaPrzydatnosc(partia);
            }
        }

        // Klik na miniaturę towaru w wierszu partii → otwiera picker mrożone/świeże.
        // Po wyborze: aktualizuje Asortyment + Idtw + Image + przelicza datę przydatności wg nowej kategorii.
        private void TowarThumb_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not FrameworkElement fe) return;
                if (fe.DataContext is not HdiPartia partia) return;

                HdiDiag.Log("HdiEditWindow", $"TowarThumb_Click → opening picker for partia '{partia.Asortyment}' (Idtw={partia.Idtw})");
                var dlg = new TowarPickerDialog(_service) { Owner = this };
                bool? result = dlg.ShowDialog();
                if (result != true || dlg.SelectedTowar == null)
                {
                    HdiDiag.Log("HdiEditWindow", "TowarPicker → user anulował lub brak wyboru");
                    return;
                }

                var t = dlg.SelectedTowar;
                partia.Asortyment = string.IsNullOrWhiteSpace(t.Nazwa) ? t.Kod : t.Nazwa;
                partia.Idtw = t.Id;
                partia.Image = t.Image;

                if (partia.DataUboju.HasValue)
                {
                    partia.DataPrzydatnosci = HdiProduktKlasyfikator.PrzydatnoscOd(partia.DataUboju.Value.Date, partia.Asortyment);
                    partia.DataMrozenia = t.IsMrozone ? partia.DataUboju.Value.Date : (DateTime?)null;
                }

                LblStatus.Text = $"✓ Zmieniono na: {partia.Asortyment} ({(t.IsMrozone ? "mrożone" : "świeże")})";
                HdiDiag.Log("HdiEditWindow", $"TowarPicker → wybrano: {partia.Asortyment} (Idtw={t.Id}, mrożone={t.IsMrozone})");
            }
            catch (Exception ex)
            {
                HdiDiag.Error("HdiEditWindow", "TowarThumb_Click EXCEPTION", ex);
                MessageBox.Show(this, $"Błąd otwarcia katalogu towarów:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Reset wiersza partii — przywraca domyślną deltę wg klasyfikatora (tuszka +7 / etc.)
        // User klika 🔄 gdy chce wrócić do domyślnej formuły (np. po ręcznej edycji która już nie jest aktualna).
        private void BtnResetRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not HdiPartia partia) return;
            RecalcPartiaPrzydatnosc(partia);
            LblStatus.Text = $"🔄 Zresetowano przydatność dla '{partia.Asortyment}' do domyślnej formuły";
        }

        // Cascade: gdy user zmienia DataWysylki, wszystkie partie się przesuwają o tę samą liczbę dni.
        // Wpięte w XAML jako DpDataWysylki SelectedDateChanged.
        private void DpDataWysylki_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Tylko gdy mamy STARĄ datę z eventArgs (czyli był wcześniejszy SelectedDate, nie pierwsze ustawienie)
            if (e.RemovedItems.Count == 0 || e.RemovedItems[0] is not DateTime oldDate) return;
            if (!DpDataWysylki.SelectedDate.HasValue) return;
            var newDate = DpDataWysylki.SelectedDate.Value.Date;
            var delta = newDate - oldDate.Date;
            if (delta == TimeSpan.Zero) return;

            // Przesuń wszystkie partie o tę samą liczbę dni
            int n = 0;
            foreach (var p in PartieRows)
            {
                if (p.DataUboju.HasValue) p.DataUboju = p.DataUboju.Value + delta;
                if (p.DataMrozenia.HasValue) p.DataMrozenia = p.DataMrozenia.Value + delta;
                if (p.DataPrzydatnosci.HasValue) p.DataPrzydatnosci = p.DataPrzydatnosci.Value + delta;
                n++;
            }
            if (n > 0)
                LblStatus.Text = $"📅 Przesunięto {n} partii o {delta.Days:+0;-0} dni (zmiana daty wysyłki)";
        }

        // Wspólna logika: licz DataPrzydatnosci wg klasyfikacji + ustaw DataMrozenia tylko gdy mrożone.
        // Używana TYLKO przy pierwszym ustawieniu daty (gdy nie mamy starej delty do zachowania).
        private void RecalcPartiaPrzydatnosc(HdiPartia partia)
        {
            try
            {
                if (!partia.DataUboju.HasValue) return;
                var nazwa = partia.Asortyment ?? "";
                partia.DataPrzydatnosci = HdiProduktKlasyfikator.PrzydatnoscOd(partia.DataUboju.Value.Date, nazwa);
                bool mrozony = HdiProduktKlasyfikator.IsMrozony(nazwa);
                partia.DataMrozenia = mrozony ? partia.DataUboju.Value.Date : (DateTime?)null;
                // INPC w HdiPartia powoduje że bindingi DatePicker auto-update — Refresh() nie potrzebny
                // (i powodował "Refresh niedozwolony podczas EditItem transaction").
            }
            catch { }
        }

        // Zachowane dla kompatybilności — gdy user edytuje Asortyment (TextColumn) zmienia
        // się też kategoria → przelicz przydatność (tuszka 7 vs elementy 6 vs podroby 4).
        private void GridPartie_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row?.Item is not HdiPartia partia) return;
            string colName = (e.Column?.Header as string)?.ToLowerInvariant() ?? "";
            if (!(colName.Contains("asortyment") || colName.Contains("nazwa") || colName.Contains("opis"))) return;
            Dispatcher.BeginInvoke(new Action(() => RecalcPartiaPrzydatnosc(partia)),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // Patrzy na string TxtRodzajOpakowan i ustawia odpowiednie checkboxy (case-insensitive).
        private void SyncPackagingCheckboxesFromText(string? text)
        {
            _suppressPackagingUpdate = true;
            try
            {
                string t = (text ?? "").ToUpperInvariant();
                bool euro = t.Contains("EURO");
                bool h1   = t.Contains("H1");
                bool poli = t.Contains("POLIBLOK");
                bool e2   = t.Contains("E2") || t.Contains("POJEMNIK E2");
                bool drewno = (t.Contains("DREWNO") || t.Contains("DREWNIAN")) && !euro;
                ChkPaletaDrewno.IsChecked = drewno;
                ChkPoliblok.IsChecked = poli;
                ChkPaletaH1.IsChecked = h1;
                ChkPaletaEuro.IsChecked = euro;
                ChkPojemnikE2.IsChecked = e2;
            }
            finally { _suppressPackagingUpdate = false; }
        }

        private void PackagingChk_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressPackagingUpdate) return;
            var parts = new List<string>();
            if (ChkPaletaDrewno.IsChecked == true) parts.Add("PALETA DREWNIANA");
            if (ChkPoliblok.IsChecked == true)     parts.Add("POLIBLOK");
            if (ChkPaletaH1.IsChecked == true)     parts.Add("PALETA H1");
            if (ChkPaletaEuro.IsChecked == true)   parts.Add("PALETA EURO DREWNIANA");
            if (ChkPojemnikE2.IsChecked == true)   parts.Add("POJEMNIK E2");
            TxtRodzajOpakowan.Text = string.Join(", ", parts);
        }

        // Liczba opakowań — tylko cyfry (manualne wpisywanie)
        private void LiczbaOpakowan_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // dopuszczamy tylko cyfry (bez minusów, bez spacji)
            foreach (var ch in e.Text) { if (!char.IsDigit(ch)) { e.Handled = true; return; } }
        }

        private bool BindToModel()
        {
            _model.KlientNazwa = (TxtKlientNazwa.Text ?? "").Trim();
            _model.KlientAdres = (TxtKlientAdres.Text ?? "").Trim();
            _model.OpisTowaru = (TxtOpisTowaru.Text ?? "").Trim();
            _model.RodzajOpakowan = (TxtRodzajOpakowan.Text ?? "").Trim();

            _model.LiczbaOpakowan = int.TryParse((TxtLiczbaOpakowan.Text ?? "").Trim(), out var lo) ? lo : null;
            _model.WagaNetto = TryParseDecimal(TxtWagaNetto.Text);
            _model.WagaBrutto = TryParseDecimal(TxtWagaBrutto.Text);
            _model.DataWysylki = DpDataWysylki.SelectedDate;
            _model.NumerRejestracyjny = (TxtNumerRejestracyjny.Text ?? "").Trim();
            _model.NumerRejNaczepy = (TxtNumerRejNaczepy.Text ?? "").Trim();
            _model.UwagiTransport = (TxtUwagiTransport.Text ?? "").Trim();
            _model.MiejscowoscWystawienia = (TxtMiejscowosc.Text ?? "").Trim();
            _model.DataWystawienia = DpDataWystawienia.SelectedDate ?? DateTime.Now;
            _model.Wystawiajacy = (TxtWystawiajacy.Text ?? "").Trim();

            _model.RynekKrajowy = RbKrajowy.IsChecked == true;
            _model.RynekUE = RbUE.IsChecked == true;
            _model.RynekInny = RbInny.IsChecked == true;
            _model.InnePanstwo = (TxtInnePanstwo.Text ?? "").Trim();

            _model.Partie = PartieRows.ToList();
            return true;
        }

        private static decimal? TryParseDecimal(string? s)
        {
            s = (s ?? "").Replace(",", ".").Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? (decimal?)v : null;
        }

        // ── Auto-fill z zamówienia ──────────────────────────────────────────
        // "Pobierz" — jeśli ID podane → ładuj. Jeśli puste → OTWÓRZ DIALOG wyboru zamówienia
        // dla wybranej daty wysyłki (DpDataWysylki) z listy zamówień z bazy.
        private async void BtnLoadFromOrder_Click(object sender, RoutedEventArgs e)
        {
            int zid = 0;
            if (!int.TryParse((TxtZamowienieId.Text ?? "").Trim(), out zid) || zid <= 0)
            {
                // Puste / niepoprawne ID — otwórz dialog wyboru z daty wysyłki
                var d = DpDataWysylki.SelectedDate ?? DateTime.Today;
                var dlg = new WyborZamowieniaDialog(d) { Owner = this };
                if (dlg.ShowDialog() == true && dlg.SelectedOrderId.HasValue)
                {
                    zid = dlg.SelectedOrderId.Value;
                    TxtZamowienieId.Text = zid.ToString();
                }
                else
                {
                    return;   // user anulował
                }
            }

            LblStatus.Text = $"⏳ Pobieranie danych zamówienia #{zid}…";
            ShowLoading($"Pobieranie zamówienia #{zid}…");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                string? numerFaktury = await GetNumerFakturyAsync(zid);
                bool ok = false;
                if (!string.IsNullOrWhiteSpace(numerFaktury))
                    ok = await TryAutoFillFromInvoiceAsync(numerFaktury!, zid);

                if (!ok) ok = await TryAutoFillFromOrderAsync(zid);

                if (!ok)
                {
                    MessageBox.Show(this, $"Nie znaleziono zamówienia #{zid} (ani powiązanej faktury w Symfonii).",
                        "Brak danych", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                BindFromModel();
                sw.Stop();
                LblStatus.Text += $"  ⚡ {sw.ElapsedMilliseconds}ms";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd pobierania zamówienia: " + ex.Message, "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LblStatus.Text = $"⚠ Błąd: {ex.Message}";
            }
            finally { HideLoading(); }
        }

        // Loading overlay helpers
        private void ShowLoading(string message)
        {
            LblLoading.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }
        private void HideLoading() => LoadingOverlay.Visibility = Visibility.Collapsed;

        // Pre-generowanie PDF — WYŁĄCZONE (powodowało zwieszanie apki przy każdym Bind).
        // Preview generuje synchronicznie w momencie kliknięcia "Podgląd PDF".
        private void SchedulePdfPrecache()
        {
            // No-op. Cache inwalidujemy bo dane mogły się zmienić.
            _cachedPdfBytes = null;
            _cachedPdfImages = null;
        }

        // Płytka kopia modelu wystarczająca dla PDF (lista partii skopiowana po referencji wartości).
        private static HdiDokument CloneModelForPdf(HdiDokument m)
        {
            return new HdiDokument
            {
                Id = m.Id, Numer = m.Numer, Rok = m.Rok,
                ZamowienieId = m.ZamowienieId, KlientId = m.KlientId,
                KlientNazwa = m.KlientNazwa, KlientAdres = m.KlientAdres,
                OpisTowaru = m.OpisTowaru, RodzajOpakowan = m.RodzajOpakowan,
                LiczbaOpakowan = m.LiczbaOpakowan, WagaNetto = m.WagaNetto, WagaBrutto = m.WagaBrutto,
                Pochodzenie = m.Pochodzenie, MiejscePozyskania = m.MiejscePozyskania,
                DataWysylki = m.DataWysylki, MiejscePrzeznaczenia = m.MiejscePrzeznaczenia,
                NumerRejestracyjny = m.NumerRejestracyjny, UwagiTransport = m.UwagiTransport, UwagiTechnologia = m.UwagiTechnologia,
                RynekKrajowy = m.RynekKrajowy, RynekUE = m.RynekUE, RynekInny = m.RynekInny, InnePanstwo = m.InnePanstwo,
                MiejscowoscWystawienia = m.MiejscowoscWystawienia, DataWystawienia = m.DataWystawienia,
                UtworzonoPrzez = m.UtworzonoPrzez, Wystawiajacy = m.Wystawiajacy, Status = m.Status,
                Partie = m.Partie.Select(p => new HdiPartia
                {
                    Asortyment = p.Asortyment, NumerPartii = p.NumerPartii,
                    DataUboju = p.DataUboju, DataMrozenia = p.DataMrozenia,
                    DataPrzydatnosci = p.DataPrzydatnosci, WagaKg = p.WagaKg
                }).ToList()
            };
        }

        // Pomocnicze: pobierz numer faktury dla zamówienia (lub null jeśli puste/brak).
        private async Task<string?> GetNumerFakturyAsync(int orderId)
        {
            try
            {
                const string connLibra =
                    "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(connLibra);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT NumerFaktury FROM dbo.ZamowieniaMieso WHERE Id = @id", cn);
                cmd.Parameters.AddWithValue("@id", orderId);
                var r = await cmd.ExecuteScalarAsync();
                if (r == null || r == DBNull.Value) return null;
                string s = r.ToString() ?? "";
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            catch { return null; }
        }

        private async void BtnLoadPartie_Click(object sender, RoutedEventArgs e)
        {
            // Bierz datę wysyłki jako "dzień uboju" (lub dzisiaj jeśli puste)
            DateTime data = DpDataWysylki.SelectedDate ?? DateTime.Today;
            try
            {
                var partie = await _service.LoadPartieDlaDniaAsync(data, TxtOpisTowaru.Text ?? "");
                if (partie.Count == 0)
                {
                    MessageBox.Show(this, $"Brak partii w listapartii dla daty {data:dd.MM.yyyy}.", "Brak", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                PartieRows.Clear();
                foreach (var p in partie) PartieRows.Add(p);
                LblStatus.Text = $"✓ Załadowano {partie.Count} partii z bazy";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd ładowania partii: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Tabela partii: add/remove ───────────────────────────────────────
        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            PartieRows.Add(new HdiPartia
            {
                Asortyment = TxtOpisTowaru.Text ?? "",
                DataUboju = DateTime.Today,
                DataMrozenia = DateTime.Today,
                DataPrzydatnosci = DateTime.Today.AddMonths(6)
            });
        }

        private void BtnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is HdiPartia p)
                PartieRows.Remove(p);
        }

        // ── Save / Cancel / Preview ─────────────────────────────────────────
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!BindToModel()) return;

            if (string.IsNullOrWhiteSpace(_model.KlientNazwa))
            { MessageBox.Show(this, "Podaj nazwę klienta.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(_model.OpisTowaru))
            { MessageBox.Show(this, "Podaj opis towaru.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                if (_model.Id == 0)
                {
                    var id = await _service.CreateAsync(_model);
                    LblStatus.Text = $"✓ Zapisano HDI {_model.NumerPelny} (ID: {id}) · ⚡ {sw.ElapsedMilliseconds}ms";
                }
                else
                {
                    await _service.UpdateAsync(_model);
                    LblStatus.Text = $"✓ Zaktualizowano HDI {_model.NumerPelny} · ⚡ {sw.ElapsedMilliseconds}ms";
                }
                _savedSuccessfully = true;
                _isDirty = false;
                FlashButton(BtnSave, "✓ Zapisz HDI", "✅ Zapisano!");
                // Krótkie opóźnienie żeby user zobaczył toast przed zamknięciem
                await Task.Delay(700);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Dirty flag — ostrzeż przed utratą zmian
            if (_isDirty && !_savedSuccessfully)
            {
                var r = MessageBox.Show(this,
                    "Masz niezapisane zmiany.\n\nZamknąć BEZ ZAPISU?\n\n(TAK = zamknij, NIE = wróć do edycji)",
                    "Niezapisane zmiany", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }
            DialogResult = false;
            Close();
        }

        // Drukuj bez podglądu — przez WPF PrintDialog (oryginał + kopia).
        private void BtnDirectPrint_Click(object sender, RoutedEventArgs e)
        {
            if (!BindToModel()) return;
            try
            {
                var dialog = new System.Windows.Controls.PrintDialog();
                if (dialog.ShowDialog() != true) { LblStatus.Text = "Drukowanie anulowane"; return; }

                var gen = new HdiPdfGenerator();
                var pages = gen.GenerateImages(_model);
                var doc = HdiPreviewWindow.BuildPrintDocument(pages, dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
                dialog.PrintDocument(doc.DocumentPaginator, $"HDI {_model.NumerPelny}");
                LblStatus.Text = $"🖨️ HDI {_model.NumerPelny}: oryginał + kopia → '{dialog.PrintQueue?.Name ?? "domyślna"}'";
                FlashButton(BtnDirectPrint, "🖨️ Drukuj", "🖨️ Wysłano!");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Błąd drukowania:\n\n" + ex.Message + "\n\nSpróbuj zapisać PDF i wydrukować ręcznie.",
                    "Drukowanie", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Otwiera live-log okno (F12 lub przycisk 🐛 w footerze)
        private static HdiDiagWindow? _diagWindow;
        private void OpenDiagWindow()
        {
            if (_diagWindow == null || !_diagWindow.IsVisible)
            {
                _diagWindow = new HdiDiagWindow();
                _diagWindow.Closed += (s, e) => _diagWindow = null;
                _diagWindow.Show();
            }
            else { _diagWindow.Activate(); }
        }

        private void BtnOpenDiag_Click(object sender, RoutedEventArgs e) => OpenDiagWindow();

        // Otwiera plik logu w Notatniku — bulletproof debugging
        // Plik: %TEMP%\Kalendarz1_HDI_diag.log
        private void BtnOpenLogFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = HdiDiag.LogFilePath;
                if (!System.IO.File.Exists(path))
                {
                    System.IO.File.WriteAllText(path, "(brak logów — wykonaj akcję w HDI najpierw)\n");
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
                LblStatus.Text = $"📂 Otwarto: {path}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Nie mogę otworzyć: {HdiDiag.LogFilePath}\n\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Visual feedback po akcji — przycisk zmienia tekst na 1.5s
        private void FlashButton(Button btn, string original, string flash)
        {
            try
            {
                btn.Content = flash;
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                t.Tick += (s, e) => { btn.Content = original; t.Stop(); };
                t.Start();
            }
            catch { btn.Content = original; }
        }

        // Keyboard shortcuts: Ctrl+S zapisz, Ctrl+P podgląd PDF, Ctrl+D drukuj direct, Ctrl+R pobierz, Esc zamknij, F5 auto-fill
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Handled) return;
            bool isText = e.OriginalSource is TextBox || e.OriginalSource is DatePickerTextBox;

            if (e.Key == System.Windows.Input.Key.Escape && !isText) { BtnCancel_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.F12) { OpenDiagWindow(); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.D && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            { BtnDirectPrint_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.S && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            { BtnSave_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.P && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            { BtnPreviewPdf_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.R && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            { BtnLoadFromOrder_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.F5 && !isText)
            { BtnLoadFromOrder_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }

        private void BtnPreviewPdf_Click(object sender, RoutedEventArgs e)
        {
            if (!BindToModel()) return;
            try
            {
                // Jeśli mamy świeży cache (PDF + obrazy) — przekaż żeby preview otworzył się instant
                HdiPreviewWindow preview;
                if (_cachedPdfBytes != null && _cachedPdfImages != null && _cachedPdfImages.Count > 0)
                    preview = new HdiPreviewWindow(_model, _cachedPdfBytes, _cachedPdfImages) { Owner = this };
                else
                    preview = new HdiPreviewWindow(_model) { Owner = this };
                preview.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd generowania PDF: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
