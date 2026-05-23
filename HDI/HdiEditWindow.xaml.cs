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

            Loaded += async (s, e) =>
            {
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
                if (willAutoFill) ShowLoading("Pobieranie danych z bazy…");
                try
                {
                    if (!string.IsNullOrWhiteSpace(numerFaktury))
                        filledOk = await TryAutoFillFromInvoiceAsync(numerFaktury, orderId);

                    if (!filledOk && orderId.HasValue)
                    {
                        TxtZamowienieId.Text = orderId.Value.ToString();
                        filledOk = await TryAutoFillFromOrderAsync(orderId.Value);
                    }
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
            try
            {
                // ⚡ PARALLEL: 3 niezależne zapytania jednocześnie zamiast sekwencyjnie.
                // Skraca czas z ~3-5s do ~1-1.5s w typowym przypadku.
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
                var groups = inv.Pozycje
                    .Where(p => !string.IsNullOrWhiteSpace(p.Nazwa) && p.Ilosc > 0)
                    .GroupBy(p => p.Nazwa)
                    .Select(g => new { Asortyment = g.Key, SumKg = g.Sum(x => x.Ilosc) })
                    .ToList();

                var partieTasks = groups.Select(g =>
                    _service.LoadPartieDlaDniaAsync(dataUboju, g.Asortyment)
                ).ToList();
                var partieResults = await Task.WhenAll(partieTasks);

                var allPartie = new List<HdiPartia>();
                for (int gi = 0; gi < groups.Count; gi++)
                {
                    var g = groups[gi];
                    var partieDlaDnia = partieResults[gi];
                    var dataPrzyd = HdiProduktKlasyfikator.PrzydatnoscOd(dataUboju.Date, g.Asortyment);
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
                        allPartie.Add(new HdiPartia
                        {
                            Asortyment = g.Asortyment,
                            NumerPartii = "",
                            DataUboju = dataUboju.Date,
                            DataMrozenia = mrozony ? dataUboju.Date : (DateTime?)null,
                            DataPrzydatnosci = dataPrzyd,
                            WagaKg = g.SumKg
                        });
                    }
                }
                if (allPartie.Count > 0) _model.Partie = allPartie;

                LblStatus.Text = $"✓ Z faktury {numerFaktury}: {inv.Pozycje.Count} poz · {inv.SumaIlosc:0.##} kg · {allPartie.Count} part. · {(string.IsNullOrEmpty(_model.NumerRejestracyjny) ? "—" : _model.NumerRejestracyjny)}";
                return true;
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"⚠ Błąd auto-fill faktury: {ex.Message}";
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

                    // ⚡ PARALLEL: partie per asortyment + transport jednocześnie
                    if (_model.Partie.Count == 0 && fill.DataUboju.HasValue)
                    {
                        var groups = fill.Pozycje
                            .Where(p => !string.IsNullOrWhiteSpace(p.Nazwa) && p.Ilosc > 0)
                            .GroupBy(p => p.Nazwa)
                            .Select(g => new { Asortyment = g.Key, SumKg = g.Sum(x => x.Ilosc) })
                            .ToList();

                        var partieTasks = groups.Select(g =>
                            _service.LoadPartieDlaDniaAsync(fill.DataUboju.Value, g.Asortyment)
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
                            var dataPrzyd = HdiProduktKlasyfikator.PrzydatnoscOd(fill.DataUboju.Value.Date, g.Asortyment);
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
                                allPartie.Add(new HdiPartia
                                {
                                    Asortyment = g.Asortyment, NumerPartii = "",
                                    DataUboju = fill.DataUboju.Value.Date,
                                    DataMrozenia = mrozony ? fill.DataUboju.Value.Date : (DateTime?)null,
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

            PartieRows.Clear();
            foreach (var p in _model.Partie) PartieRows.Add(p);
            // Po każdym wczytaniu / Bind — regeneruj PDF w tle, żeby preview był instant
            SchedulePdfPrecache();
        }

        // Auto-przeliczanie DataPrzydatnosci dla wiersza partii po zmianie DataUboju.
        // Wyzwalane bezpośrednio przez DatePicker.SelectedDateChanged (działa natychmiast,
        // w przeciwieństwie do CellEditEnding które dla TemplateColumn z DatePicker nie odpala).
        private void DpDataUbojuPartia_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DatePicker dp) return;
            if (dp.DataContext is not HdiPartia partia) return;
            RecalcPartiaPrzydatnosc(partia);
        }

        // Wspólna logika: licz DataPrzydatnosci wg klasyfikacji + ustaw DataMrozenia tylko gdy mrożone.
        private void RecalcPartiaPrzydatnosc(HdiPartia partia)
        {
            try
            {
                if (!partia.DataUboju.HasValue) return;
                var nazwa = partia.Asortyment ?? "";
                partia.DataPrzydatnosci = HdiProduktKlasyfikator.PrzydatnoscOd(partia.DataUboju.Value.Date, nazwa);
                bool mrozony = HdiProduktKlasyfikator.IsMrozony(nazwa);
                partia.DataMrozenia = mrozony ? partia.DataUboju.Value.Date : (DateTime?)null;
                // Refresh wiersza grida (DataPicker DataPrzydatnosci + DataMrozenia)
                GridPartie.Items.Refresh();
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

        // Pre-generowanie PDF w tle (po Bind) — preview otwiera się instant z cache.
        // Inwalidowany za każdym wywołaniem (gdy user wpisuje, cache staje się przestarzały).
        private void SchedulePdfPrecache()
        {
            _pdfCacheCts?.Cancel();
            _pdfCacheCts = new System.Threading.CancellationTokenSource();
            var ct = _pdfCacheCts.Token;
            // Snapshot modelu (deep enough na potrzeby PDF)
            BindToModel();
            var snapshot = CloneModelForPdf(_model);
            _ = Task.Run(() =>
            {
                try
                {
                    if (ct.IsCancellationRequested) return;
                    var gen = new HdiPdfGenerator();
                    var pdf = gen.Generate(snapshot);
                    if (ct.IsCancellationRequested) return;
                    var imgs = gen.GenerateImages(snapshot);
                    if (ct.IsCancellationRequested) return;
                    _cachedPdfBytes = pdf;
                    _cachedPdfImages = imgs;
                }
                catch { /* best-effort; preview path zrobi to synchronicznie jako fallback */ }
            }, ct);
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
                if (_model.Id == 0)
                {
                    var id = await _service.CreateAsync(_model);
                    LblStatus.Text = $"✓ Zapisano HDI {_model.NumerPelny} (ID: {id})";
                }
                else
                {
                    await _service.UpdateAsync(_model);
                    LblStatus.Text = $"✓ Zaktualizowano HDI {_model.NumerPelny}";
                }
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        // Keyboard shortcuts: Ctrl+S zapisz, Ctrl+P podgląd PDF, Ctrl+R pobierz, Esc zamknij, F5 odśwież auto-fill
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Handled) return;
            // Pomijaj gdy user pisze w TextBox/DatePicker — wymaga modyfikatora Ctrl
            bool isText = e.OriginalSource is TextBox || e.OriginalSource is DatePickerTextBox;

            if (e.Key == System.Windows.Input.Key.Escape && !isText) { BtnCancel_Click(this, new RoutedEventArgs()); e.Handled = true; }
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
