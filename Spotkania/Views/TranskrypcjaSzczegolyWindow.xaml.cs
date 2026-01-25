using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Kalendarz1.Spotkania.Models;
using Kalendarz1.Spotkania.Services;

namespace Kalendarz1.Spotkania.Views
{
    public partial class TranskrypcjaSzczegolyWindow : Window
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        private readonly FirefliesService _firefliesService;
        private readonly string _firefliesId;
        private long _transkrypcjaId;

        // Dane
        private FirefliesTranskrypcja? _transkrypcja;
        private FirefliesTranscriptDto? _transkrypcjaApi;
        private List<FirefliesSentenceDto> _zdaniaApi = new();

        // Kolekcje
        private ObservableCollection<MowcaMapowanieDisplay> _mowcy = new();
        private ObservableCollection<ZdanieDisplay> _zdania = new();
        private List<PracownikItem> _pracownicy = new();

        // Ustawienia
        private bool _autoZapisMapowan = true;
        private bool _uzyjNazwSystemowych = true;
        private bool _pokazCzasy = true;
        private string _filtrMowcy = "";
        private string _filtrTekst = "";
        private double _calkowityCzas = 0;
        private double _fontSize = 13;
        private bool _isInitialized = false;

        // Nowe ustawienia
        private bool _trybCiemny = false;
        private string _filtrEmocji = ""; // "pytania", "wazne", "akcje", ""
        private bool _widokKolumnowy = false;

        // Kolory
        private static readonly string[] KoloryMowcow = {
            "#2196F3", "#4CAF50", "#FF9800", "#9C27B0", "#F44336",
            "#00BCD4", "#795548", "#607D8B", "#E91E63", "#3F51B5"
        };
        private static readonly string[] KoloryTla = {
            "#E3F2FD", "#E8F5E9", "#FFF3E0", "#F3E5F5", "#FFEBEE",
            "#E0F7FA", "#EFEBE9", "#ECEFF1", "#FCE4EC", "#E8EAF6"
        };

        public TranskrypcjaSzczegolyWindow(string firefliesId, long transkrypcjaId = 0)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _firefliesService = new FirefliesService();
            _firefliesId = firefliesId;
            _transkrypcjaId = transkrypcjaId;

            TxtFirefliesId.Text = $"Fireflies ID: {firefliesId}";
            ListaMowcow.ItemsSource = _mowcy;
            ListaZdan.ItemsSource = _zdania;

            // Skroty klawiaturowe
            KeyDown += Window_KeyDown;
            PreviewKeyDown += Window_PreviewKeyDown;

            Loaded += async (s, e) =>
            {
                await LoadDataAsync();
                _isInitialized = true;
            };
        }

        #region Ladowanie danych

        private async Task LoadDataAsync()
        {
            try
            {
                UstawStatus("Ladowanie danych...");

                // 1. Pobierz pracownikow
                await PobierzPracownikow();

                // 2. Pobierz transkrypcje z bazy
                if (_transkrypcjaId > 0)
                {
                    _transkrypcja = await _firefliesService.PobierzTranskrypcjeZBazyPoId(_transkrypcjaId);
                }

                // 3. Pobierz z API
                UstawStatus("Pobieranie z Fireflies...");
                try
                {
                    _zdaniaApi = await _firefliesService.PobierzZdaniaTranskrypcji(_firefliesId);
                    _transkrypcjaApi = await _firefliesService.PobierzSzczegolyTranskrypcji(_firefliesId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Blad API: {ex.Message}");
                }

                // 4. Wypelnij formularz
                WypelnijFormularz();

                // 5. Wykryj mowcow
                WykryjMowcowZZdan();

                // 6. Zaladuj zapisane mapowania PRZED wypelnieniem zdan
                await ZaladujZapisaneMapowania();

                // 7. Auto-dopasuj mówców z globalnej bazy wiedzy (jeśli brak zapisanych mapowań)
                await AutoDopasujMowcowZGlobalnejBazy();

                // 8. Wypelnij zdania
                WypelnijZdania();

                // 9. Pobierz powiazania
                await PobierzSpotkaniaINotatki();

                UstawStatus("");
                UstawSyncStatus(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                UstawStatus($"Blad: {ex.Message}");
            }
        }

        private async Task PobierzPracownikow()
        {
            _pracownicy.Clear();
            _pracownicy.Add(new PracownikItem { UserID = "", DisplayName = "(Nie przypisano)" });

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();

            string sql = @"SELECT ID, Name FROM operators ORDER BY Name";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var name = reader.IsDBNull(1) ? id : reader.GetString(1);
                _pracownicy.Add(new PracownikItem
                {
                    UserID = id,
                    DisplayName = string.IsNullOrEmpty(name) ? id : name
                });
            }
        }

        private void WypelnijFormularz()
        {
            if (_transkrypcjaApi != null)
            {
                TxtTytul.Text = _transkrypcjaApi.Title ?? "";
                TxtData.Text = _transkrypcjaApi.DateAsDateTime?.ToString("dd.MM.yyyy HH:mm") ?? "---";
                TxtCzasTrwania.Text = FormatujCzas(_transkrypcjaApi.Duration);
                TxtOrganizator.Text = _transkrypcjaApi.EmailOrganizatora ?? "Nieznany";
                TxtPodsumowanie.Text = _transkrypcjaApi.Summary?.Overview ?? "";
                TxtSlowaKluczowe.Text = _transkrypcjaApi.Summary?.Keywords != null
                    ? string.Join(", ", _transkrypcjaApi.Summary.Keywords) : "";
                _calkowityCzas = _transkrypcjaApi.Duration ?? 0;
            }
            else if (_transkrypcja != null)
            {
                TxtTytul.Text = _transkrypcja.Tytul ?? "";
                TxtData.Text = _transkrypcja.DataSpotkaniaDisplay;
                TxtCzasTrwania.Text = _transkrypcja.CzasTrwaniaDisplay;
                TxtOrganizator.Text = _transkrypcja.HostEmail ?? "Nieznany";
                TxtPodsumowanie.Text = _transkrypcja.Podsumowanie ?? "";
                TxtSlowaKluczowe.Text = _transkrypcja.SlowKluczowe != null
                    ? string.Join(", ", _transkrypcja.SlowKluczowe) : "";
                _calkowityCzas = _transkrypcja.CzasTrwaniaSekundy;
            }
        }

        private void WykryjMowcowZZdan()
        {
            _mowcy.Clear();

            // Najpierw probuj z API
            if (_zdaniaApi != null && _zdaniaApi.Count > 0)
            {
                WykryjMowcowZApi();
            }
            // Jesli brak API, wykryj z tekstu transkrypcji
            else if (_transkrypcja?.Transkrypcja != null)
            {
                WykryjMowcowZTekstu();
            }

            TxtLiczbaMowcow.Text = $"({_mowcy.Count})";
        }

        private void WykryjMowcowZApi()
        {
            // Grupuj i oblicz statystyki
            var grupyMowcow = _zdaniaApi
                .GroupBy(z => new { z.SpeakerId, z.SpeakerName })
                .Select((g, idx) => {
                    var zdania = g.OrderBy(z => z.StartTime).ToList();
                    double czasMowienia = 0;
                    foreach (var z in zdania)
                    {
                        czasMowienia += (z.EndTime - z.StartTime);
                    }

                    return new
                    {
                        SpeakerId = g.Key.SpeakerId,
                        SpeakerName = g.Key.SpeakerName,
                        LiczbaWypowiedzi = zdania.Count,
                        CzasMowienia = czasMowienia,
                        PrzykladowaWypowiedz = zdania.FirstOrDefault()?.Text ?? "",
                        PierwszyCzas = zdania.FirstOrDefault()?.StartTime ?? 0,
                        KolorIndex = idx
                    };
                })
                .OrderByDescending(m => m.CzasMowienia)
                .ToList();

            foreach (var g in grupyMowcow)
            {
                var kolorIdx = g.KolorIndex % KoloryMowcow.Length;
                var procentCzasu = _calkowityCzas > 0 ? (g.CzasMowienia / _calkowityCzas * 100) : 0;

                _mowcy.Add(new MowcaMapowanieDisplay
                {
                    SpeakerId = g.SpeakerId,
                    SpeakerNameFireflies = g.SpeakerName ?? $"Mowca {g.SpeakerId}",
                    LiczbaWypowiedzi = g.LiczbaWypowiedzi,
                    CzasMowienia = g.CzasMowienia,
                    ProcentCzasu = procentCzasu,
                    PrzykladowaWypowiedz = g.PrzykladowaWypowiedz.Length > 120
                        ? g.PrzykladowaWypowiedz.Substring(0, 120) + "..."
                        : g.PrzykladowaWypowiedz,
                    PierwszyCzas = g.PierwszyCzas,
                    KolorMowcy = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryMowcow[kolorIdx])),
                    TloKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryTla[kolorIdx])),
                    DostepniPracownicy = _pracownicy
                });
            }
        }

        private void WykryjMowcowZTekstu()
        {
            var lines = _transkrypcja!.Transkrypcja!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var mowcyDict = new Dictionary<string, (int liczba, string przyklad)>();

            foreach (var line in lines)
            {
                string mowcaNazwa = "";

                // Format: [Nazwa]: tekst
                if (line.StartsWith("[") && line.Contains("]"))
                {
                    var endBracket = line.IndexOf(']');
                    mowcaNazwa = line.Substring(1, endBracket - 1).Trim();
                }
                // Format: Nazwa: tekst
                else if (line.Contains(":"))
                {
                    var colonIdx = line.IndexOf(':');
                    var potentialName = line.Substring(0, colonIdx).Trim();
                    // Sprawdz czy to wyglada jak nazwa (nie jest za dluga, nie zaczyna sie od cyfry itp.)
                    if (potentialName.Length > 0 && potentialName.Length < 50 && !char.IsDigit(potentialName[0]))
                    {
                        mowcaNazwa = potentialName;
                    }
                }

                if (!string.IsNullOrEmpty(mowcaNazwa))
                {
                    if (mowcyDict.ContainsKey(mowcaNazwa))
                    {
                        var (liczba, przyklad) = mowcyDict[mowcaNazwa];
                        mowcyDict[mowcaNazwa] = (liczba + 1, przyklad);
                    }
                    else
                    {
                        var tekst = line.Contains(":") ? line.Substring(line.IndexOf(':') + 1).Trim() : line;
                        mowcyDict[mowcaNazwa] = (1, tekst.Length > 120 ? tekst.Substring(0, 120) + "..." : tekst);
                    }
                }
            }

            int idx = 0;
            foreach (var kvp in mowcyDict.OrderByDescending(x => x.Value.liczba))
            {
                var kolorIdx = idx % KoloryMowcow.Length;
                var procentCzasu = lines.Length > 0 ? (kvp.Value.liczba * 100.0 / lines.Length) : 0;

                _mowcy.Add(new MowcaMapowanieDisplay
                {
                    SpeakerId = idx,
                    SpeakerNameFireflies = kvp.Key,
                    LiczbaWypowiedzi = kvp.Value.liczba,
                    CzasMowienia = 0,
                    ProcentCzasu = procentCzasu,
                    PrzykladowaWypowiedz = kvp.Value.przyklad,
                    KolorMowcy = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryMowcow[kolorIdx])),
                    TloKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryTla[kolorIdx])),
                    DostepniPracownicy = _pracownicy
                });
                idx++;
            }
        }

        private async Task ZaladujZapisaneMapowania()
        {
            if (_transkrypcjaId <= 0) return;

            try
            {
                var mapowania = await _firefliesService.PobierzMapowanieMowcow(_transkrypcjaId);

                foreach (var m in mapowania)
                {
                    // Szukaj mowcy po SpeakerId LUB po nazwie
                    var mowca = _mowcy.FirstOrDefault(x =>
                        (m.SpeakerId.HasValue && x.SpeakerId == m.SpeakerId) ||
                        (!string.IsNullOrEmpty(m.SpeakerNameFireflies) && x.SpeakerNameFireflies == m.SpeakerNameFireflies));

                    if (mowca != null && !string.IsNullOrEmpty(m.PrzypisanyUserID))
                    {
                        // Ustaw przypisanie bez wywolywania PropertyChanged (jeszcze nie ma UI)
                        mowca.PrzypisanyUserID = m.PrzypisanyUserID;
                        mowca.PrzypisanyUserName = m.PrzypisanyUserName;
                        mowca.ZrodloMapowania = "zapisane";

                        System.Diagnostics.Debug.WriteLine($"Zaladowano mapowanie: {mowca.SpeakerNameFireflies} -> {m.PrzypisanyUserName}");
                    }
                }

                // Odswiez liste mowcow
                ListaMowcow.Items.Refresh();

                // Aktualizuj status
                AktualizujStatusMapowania();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad mapowan: {ex.Message}");
            }
        }

        private async Task AutoDopasujMowcowZGlobalnejBazy()
        {
            int autoDopasowano = 0;
            int sugestii = 0;

            try
            {
                foreach (var mowca in _mowcy.Where(m => !m.JestPrzypisany))
                {
                    // Pobierz sugestie z globalnej bazy
                    var sugestie = await _firefliesService.PobierzSugestieMapowan(mowca.SpeakerNameFireflies);

                    if (sugestie.Count > 0)
                    {
                        var najlepsza = sugestie.First();
                        mowca.Sugestie = sugestie;

                        // Auto-przypisz tylko jeśli pewność >= 70% i dopasowanie >= 80%
                        if (najlepsza.Pewnosc >= 70 && najlepsza.Dopasowanie >= 80)
                        {
                            mowca.PrzypisanyUserID = najlepsza.UserID;
                            mowca.PrzypisanyUserName = najlepsza.UserName;
                            mowca.ZrodloMapowania = "auto";
                            mowca.PewnoscMapowania = najlepsza.Pewnosc;
                            mowca.LiczbaUzycMapowania = najlepsza.LiczbaUzyc;
                            autoDopasowano++;

                            System.Diagnostics.Debug.WriteLine($"Auto-dopasowano: {mowca.SpeakerNameFireflies} -> {najlepsza.UserName} ({najlepsza.Pewnosc}%)");
                        }
                        else
                        {
                            // Ma sugestie ale nie wystarczająco pewne - pokaż jako sugestię
                            mowca.MaSugestie = true;
                            sugestii++;
                        }
                    }
                }

                // Odswiez UI
                ListaMowcow.Items.Refresh();
                AktualizujStatusMapowania();

                // Pokaż informację o auto-dopasowaniu
                if (autoDopasowano > 0 || sugestii > 0)
                {
                    var msg = new StringBuilder();
                    if (autoDopasowano > 0)
                        msg.Append($"🤖 Auto-dopasowano {autoDopasowano} mówców. ");
                    if (sugestii > 0)
                        msg.Append($"💡 {sugestii} sugestii do sprawdzenia.");

                    TxtStatusMapowania.Text = msg.ToString();
                    StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        autoDopasowano > 0 ? "#E3F2FD" : "#FFF8E1"));
                }

                // Pokaż przycisk sugestii jeśli są
                AktualizujPrzyciskSugestii();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad auto-dopasowania: {ex.Message}");
            }
        }

        private void WypelnijZdania()
        {
            _zdania.Clear();

            if (_zdaniaApi != null && _zdaniaApi.Count > 0)
            {
                foreach (var z in _zdaniaApi.OrderBy(x => x.Index))
                {
                    var mowca = _mowcy.FirstOrDefault(m =>
                        m.SpeakerId == z.SpeakerId &&
                        (m.SpeakerNameFireflies == z.SpeakerName || m.SpeakerNameFireflies == $"Mowca {z.SpeakerId}"));

                    var zdanie = new ZdanieDisplay
                    {
                        Index = z.Index,
                        SpeakerId = z.SpeakerId,
                        MowcaFireflies = z.SpeakerName ?? $"Mowca {z.SpeakerId}",
                        Tekst = z.Text ?? "",
                        StartTime = z.StartTime,
                        TloKolor = mowca?.TloKolor ?? new SolidColorBrush(Colors.White),
                        MowcaKolor = mowca?.KolorMowcy ?? new SolidColorBrush(Colors.Gray),
                        UzyjNazwySystemowej = _uzyjNazwSystemowych,
                        PokazCzas = _pokazCzasy
                    };

                    // Przypisz nazwe systemowa jesli jest mapowanie
                    if (mowca != null && !string.IsNullOrEmpty(mowca.PrzypisanyUserID))
                    {
                        var pracownik = _pracownicy.FirstOrDefault(p => p.UserID == mowca.PrzypisanyUserID);
                        zdanie.MowcaSystemowy = pracownik?.DisplayName;
                    }

                    _zdania.Add(zdanie);
                }
            }
            else if (_transkrypcja?.Transkrypcja != null)
            {
                var lines = _transkrypcja.Transkrypcja.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                int idx = 0;

                foreach (var line in lines)
                {
                    var zdanie = new ZdanieDisplay { Index = idx++, UzyjNazwySystemowej = _uzyjNazwSystemowych, PokazCzas = _pokazCzasy };
                    string mowcaNazwa = "";
                    string tekst = line;

                    // Format: [Nazwa]: tekst
                    if (line.StartsWith("[") && line.Contains("]"))
                    {
                        var endBracket = line.IndexOf(']');
                        mowcaNazwa = line.Substring(1, endBracket - 1).Trim();
                        tekst = line.Substring(endBracket + 1).TrimStart(':', ' ');
                    }
                    // Format: Nazwa: tekst
                    else if (line.Contains(":"))
                    {
                        var colonIdx = line.IndexOf(':');
                        var potentialName = line.Substring(0, colonIdx).Trim();
                        if (potentialName.Length > 0 && potentialName.Length < 50 && !char.IsDigit(potentialName[0]))
                        {
                            mowcaNazwa = potentialName;
                            tekst = line.Substring(colonIdx + 1).Trim();
                        }
                    }

                    zdanie.MowcaFireflies = mowcaNazwa;
                    zdanie.Tekst = tekst;

                    // Znajdz mowce i przypisz kolory
                    var mowca = _mowcy.FirstOrDefault(m => m.SpeakerNameFireflies == mowcaNazwa);
                    if (mowca != null)
                    {
                        zdanie.SpeakerId = mowca.SpeakerId;
                        zdanie.TloKolor = mowca.TloKolor;
                        zdanie.MowcaKolor = mowca.KolorMowcy;

                        // Przypisz nazwe systemowa jesli jest mapowanie
                        if (!string.IsNullOrEmpty(mowca.PrzypisanyUserID))
                        {
                            var pracownik = _pracownicy.FirstOrDefault(p => p.UserID == mowca.PrzypisanyUserID);
                            zdanie.MowcaSystemowy = pracownik?.DisplayName;
                        }
                    }

                    _zdania.Add(zdanie);
                }
            }

            TxtLiczbaZdan.Text = $"({_zdania.Count} wypowiedzi)";
        }

        private async Task PobierzSpotkaniaINotatki()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                var spotkania = new List<SpotkanieComboItem> { new() { SpotkaniID = 0, TytulDisplay = "(Brak)" } };
                string sqlS = @"SELECT TOP 100 SpotkaniID, Tytul, DataRozpoczecia FROM Spotkania ORDER BY DataRozpoczecia DESC";
                using (var cmdS = new SqlCommand(sqlS, conn))
                using (var readerS = await cmdS.ExecuteReaderAsync())
                {
                    while (await readerS.ReadAsync())
                    {
                        spotkania.Add(new SpotkanieComboItem
                        {
                            SpotkaniID = readerS.GetInt64(0),
                            TytulDisplay = $"{readerS.GetDateTime(2):dd.MM} - {(readerS.IsDBNull(1) ? "Bez tytulu" : readerS.GetString(1))}"
                        });
                    }
                }
                CmbSpotkanie.ItemsSource = spotkania;
                CmbSpotkanie.SelectedIndex = 0;

                var notatki = new List<NotatkaComboItem> { new() { NotatkaID = 0, TematDisplay = "(Brak)" } };
                string sqlN = @"SELECT TOP 100 NotatkaID, Temat, DataSpotkania FROM NotatkiZeSpotkan ORDER BY DataSpotkania DESC";
                using (var cmdN = new SqlCommand(sqlN, conn))
                using (var readerN = await cmdN.ExecuteReaderAsync())
                {
                    while (await readerN.ReadAsync())
                    {
                        var data = readerN.IsDBNull(2) ? "" : readerN.GetDateTime(2).ToString("dd.MM");
                        notatki.Add(new NotatkaComboItem
                        {
                            NotatkaID = readerN.GetInt64(0),
                            TematDisplay = $"{data} - {(readerN.IsDBNull(1) ? "Bez tematu" : readerN.GetString(1))}"
                        });
                    }
                }
                CmbNotatka.ItemsSource = notatki;
                CmbNotatka.SelectedIndex = 0;

                if (_transkrypcja?.SpotkaniID.HasValue == true)
                {
                    var s = spotkania.FirstOrDefault(x => x.SpotkaniID == _transkrypcja.SpotkaniID);
                    if (s != null) CmbSpotkanie.SelectedItem = s;
                }
                if (_transkrypcja?.NotatkaID.HasValue == true)
                {
                    var n = notatki.FirstOrDefault(x => x.NotatkaID == _transkrypcja.NotatkaID);
                    if (n != null) CmbNotatka.SelectedItem = n;
                }
            }
            catch { }
        }

        #endregion

        #region Event Handlers - Mapowanie

        private async void CmbMowca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            var cmb = sender as ComboBox;
            var mowca = cmb?.Tag as MowcaMapowanieDisplay;
            if (mowca == null) return;

            // Pobierz wybrana wartosc
            var selectedItem = cmb?.SelectedItem as PracownikItem;
            if (selectedItem != null)
            {
                mowca.PrzypisanyUserName = selectedItem.UserID == "" ? null : selectedItem.DisplayName;
                // Oznacz jako ręczne mapowanie (użytkownik sam wybrał)
                if (!string.IsNullOrEmpty(selectedItem.UserID))
                {
                    mowca.ZrodloMapowania = "reczne";
                    mowca.MaSugestie = false; // Skoro przypisano, nie pokazuj już sugestii
                }
            }
            else
            {
                mowca.PrzypisanyUserName = null;
                mowca.ZrodloMapowania = "";
            }

            // Aktualizuj zdania w transkrypcji
            AktualizujZdaniaMowcy(mowca);

            // Aktualizuj status
            AktualizujStatusMapowania();
            AktualizujPrzyciskSugestii();

            // Auto-zapisz do bazy
            if (_transkrypcjaId > 0)
            {
                try
                {
                    await ZapiszMapowaniaDoDb();
                    TxtStatusMapowania.Text = $"✓ Zapisano: {mowca.DisplayFireflies} → {mowca.PrzypisanyUserName ?? "(brak)"}";
                    StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
                }
                catch (Exception ex)
                {
                    TxtStatusMapowania.Text = $"Blad zapisu: {ex.Message}";
                    StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEBEE"));
                }
            }

            // Odswiez liste mowcow aby pokazac zmiane ramki
            ListaMowcow.Items.Refresh();
        }

        private void AktualizujPrzyciskSugestii()
        {
            var mowcyZSugestiami = _mowcy.Count(m => m.MaSugestie && !m.JestPrzypisany);
            BtnZastosujSugestie.Visibility = mowcyZSugestiami > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (mowcyZSugestiami > 0)
            {
                BtnZastosujSugestie.Content = $"💡 Sugestie ({mowcyZSugestiami})";
            }
        }

        private void AktualizujZdaniaMowcy(MowcaMapowanieDisplay mowca)
        {
            if (_zdania == null) return;

            foreach (var z in _zdania.Where(x =>
                (mowca.SpeakerId.HasValue && x.SpeakerId == mowca.SpeakerId) ||
                x.MowcaFireflies == mowca.SpeakerNameFireflies))
            {
                if (!string.IsNullOrEmpty(mowca.PrzypisanyUserID))
                {
                    var pracownik = _pracownicy.FirstOrDefault(p => p.UserID == mowca.PrzypisanyUserID);
                    z.MowcaSystemowy = pracownik?.DisplayName;
                }
                else
                {
                    z.MowcaSystemowy = null;
                }
            }

            ListaZdan.Items.Refresh();
        }

        private void AktualizujStatusMapowania()
        {
            var przypisanych = _mowcy.Count(m => m.JestPrzypisany);
            var wszystkich = _mowcy.Count;

            if (przypisanych == 0)
            {
                TxtStatusMapowania.Text = "Wybierz pracownikow z list rozwijanych";
                StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
            }
            else if (przypisanych == wszystkich)
            {
                TxtStatusMapowania.Text = $"Wszyscy mowcy przypisani ({przypisanych}/{wszystkich})";
                StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
            }
            else
            {
                TxtStatusMapowania.Text = $"Przypisano {przypisanych} z {wszystkich} mowcow";
                StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
            }
        }

        private async void BtnAutoDopasuj_Click(object sender, RoutedEventArgs e)
        {
            int dopasowano = 0;

            foreach (var mowca in _mowcy.Where(m => !m.JestPrzypisany))
            {
                // Szukaj pracownika po nazwisku
                var nazwaFireflies = mowca.SpeakerNameFireflies?.ToLower() ?? "";

                // Pomijamy ogolne nazwy jak "Speaker 1"
                if (nazwaFireflies.StartsWith("speaker") || nazwaFireflies.StartsWith("mowca"))
                    continue;

                // Szukaj najlepszego dopasowania
                var najlepszyPracownik = _pracownicy
                    .Where(p => !string.IsNullOrEmpty(p.UserID))
                    .FirstOrDefault(p =>
                    {
                        var nazwaPracownika = p.DisplayName?.ToLower() ?? "";
                        // Pelne dopasowanie
                        if (nazwaPracownika == nazwaFireflies) return true;
                        // Zawiera nazwe
                        if (nazwaPracownika.Contains(nazwaFireflies) || nazwaFireflies.Contains(nazwaPracownika)) return true;
                        // Sprawdz poszczegolne slowa
                        var slowaFireflies = nazwaFireflies.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var slowaPracownika = nazwaPracownika.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        return slowaFireflies.Any(sf => slowaPracownika.Any(sp => sp.Contains(sf) || sf.Contains(sp)));
                    });

                if (najlepszyPracownik != null)
                {
                    mowca.PrzypisanyUserID = najlepszyPracownik.UserID;
                    mowca.PrzypisanyUserName = najlepszyPracownik.DisplayName;
                    AktualizujZdaniaMowcy(mowca);
                    dopasowano++;
                }
            }

            if (dopasowano > 0)
            {
                // Zapisz do bazy
                if (_transkrypcjaId > 0)
                {
                    await ZapiszMapowaniaDoDb();
                }
                TxtStatusMapowania.Text = $"Automatycznie dopasowano {dopasowano} mowcow";
                StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F5E9"));
            }
            else
            {
                TxtStatusMapowania.Text = "Nie znaleziono automatycznych dopasowan. Przypisz recznie.";
                StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF3E0"));
            }

            ListaMowcow.Items.Refresh();
            AktualizujStatusMapowania();
        }

        private async void BtnWyczyscMapowania_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Usunac wszystkie przypisania mowcow?", "Potwierdz",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            foreach (var mowca in _mowcy)
            {
                mowca.PrzypisanyUserID = null;
                mowca.PrzypisanyUserName = null;
                AktualizujZdaniaMowcy(mowca);
            }

            if (_transkrypcjaId > 0)
            {
                await ZapiszMapowaniaDoDb();
            }

            TxtStatusMapowania.Text = "Wyczyszczono wszystkie przypisania";
            StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
            ListaMowcow.Items.Refresh();
            AktualizujPrzyciskSugestii();
        }

        private async void BtnZastosujSugestie_Click(object sender, RoutedEventArgs e)
        {
            int zastosowano = 0;

            foreach (var mowca in _mowcy.Where(m => m.MaSugestie && !m.JestPrzypisany && m.NajlepszaSugestia != null))
            {
                var sugestia = mowca.NajlepszaSugestia!;
                mowca.PrzypisanyUserID = sugestia.UserID;
                mowca.PrzypisanyUserName = sugestia.UserName;
                mowca.ZrodloMapowania = "sugestia";
                mowca.PewnoscMapowania = sugestia.Pewnosc;
                mowca.LiczbaUzycMapowania = sugestia.LiczbaUzyc;
                mowca.MaSugestie = false;
                AktualizujZdaniaMowcy(mowca);
                zastosowano++;
            }

            if (zastosowano > 0 && _transkrypcjaId > 0)
            {
                await ZapiszMapowaniaDoDb();
            }

            TxtStatusMapowania.Text = $"💡 Zastosowano {zastosowano} sugestii z bazy wiedzy";
            StatusMapowania.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
            ListaMowcow.Items.Refresh();
            AktualizujStatusMapowania();
            AktualizujPrzyciskSugestii();
        }

        private async void BtnPokazBazeGlosow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mapowania = await _firefliesService.PobierzWszystkieGlobalneMapowania();

                var dialog = new Window
                {
                    Title = "📚 Baza wiedzy o głosach",
                    Width = 700,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
                };

                var mainPanel = new Grid();
                mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Header
                var headerPanel = new StackPanel { Margin = new Thickness(15, 15, 15, 10) };
                headerPanel.Children.Add(new TextBlock
                {
                    Text = "System uczy się Twoich przypisań mówców",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333"))
                });
                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"Zapisano {mapowania.Count} wzorców głosów",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")),
                    Margin = new Thickness(0, 5, 0, 0)
                });
                Grid.SetRow(headerPanel, 0);
                mainPanel.Children.Add(headerPanel);

                // Lista
                var listView = new ListView
                {
                    Margin = new Thickness(15, 0, 15, 15),
                    Background = Brushes.White
                };

                var gridView = new GridView();
                gridView.Columns.Add(new GridViewColumn { Header = "Głos z Fireflies", DisplayMemberBinding = new System.Windows.Data.Binding("OriginalPattern"), Width = 180 });
                gridView.Columns.Add(new GridViewColumn { Header = "Pracownik", DisplayMemberBinding = new System.Windows.Data.Binding("UserName"), Width = 180 });
                gridView.Columns.Add(new GridViewColumn { Header = "Użyć", DisplayMemberBinding = new System.Windows.Data.Binding("LiczbaUzyc"), Width = 60 });
                gridView.Columns.Add(new GridViewColumn { Header = "Pewność", DisplayMemberBinding = new System.Windows.Data.Binding("PewnoscDisplay"), Width = 80 });
                listView.View = gridView;
                listView.ItemsSource = mapowania;

                Grid.SetRow(listView, 1);
                mainPanel.Children.Add(listView);

                // Przyciski
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(15, 0, 15, 15)
                };

                var btnUsun = new Button
                {
                    Content = "🗑️ Wyczyść bazę",
                    Padding = new Thickness(15, 8, 15, 8),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 0, 10, 0)
                };
                btnUsun.Click += async (s, ev) =>
                {
                    if (MessageBox.Show("Usunąć całą bazę wiedzy o głosach?\nSystem będzie musiał uczyć się od nowa.",
                        "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        await _firefliesService.ResetujGlobalneMapowania();
                        dialog.Close();
                        MessageBox.Show("Baza wiedzy została wyczyszczona.", "Gotowe", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };
                buttonPanel.Children.Add(btnUsun);

                var btnZamknij = new Button
                {
                    Content = "Zamknij",
                    Padding = new Thickness(15, 8, 15, 8),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#607D8B")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0)
                };
                btnZamknij.Click += (s, ev) => dialog.Close();
                buttonPanel.Children.Add(btnZamknij);

                Grid.SetRow(buttonPanel, 2);
                mainPanel.Children.Add(buttonPanel);

                dialog.Content = mainPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOdsluchajMowce_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var mowca = btn?.Tag as MowcaMapowanieDisplay;
            if (mowca == null) return;

            // Pobierz fragmenty tego mówcy z transkrypcji
            var fragmenty = _zdaniaApi
                .Where(z => z.SpeakerName == mowca.SpeakerNameFireflies)
                .OrderBy(z => z.StartTime)
                .ToList();

            // Sprawdź dostępne URLe
            var audioUrl = _transkrypcjaApi?.AudioUrl;
            var transcriptUrl = _transkrypcja?.TranskrypcjaUrl ?? _transkrypcjaApi?.TranscriptUrl;

            // Zawsze pokaż odtwarzacz z listą fragmentów
            PokazOdtwarzaczZFragmentami(audioUrl, transcriptUrl, mowca, fragmenty);
        }

        #region B8: Tryb szybkiego mapowania

        /// <summary>
        /// B8: Wizard szybkiego mapowania mówców - krok po kroku, Enter = następny
        /// </summary>
        private void BtnSzybkieMapowanie_Click(object sender, RoutedEventArgs e)
        {
            var nieprzypisani = _mowcy.Where(m => !m.JestPrzypisany).ToList();
            if (nieprzypisani.Count == 0)
            {
                MessageBox.Show("Wszyscy mówcy są już przypisani!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PokazWizardMapowania(nieprzypisani, 0);
        }

        private void PokazWizardMapowania(List<MowcaMapowanieDisplay> mowcy, int index)
        {
            if (index >= mowcy.Count)
            {
                MessageBox.Show($"Gotowe! Przypisano {mowcy.Count} mówców.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                ListaMowcow.Items.Refresh();
                AktualizujStatusMapowania();
                if (_transkrypcjaId > 0) _ = ZapiszMapowaniaDoDb();
                return;
            }

            var mowca = mowcy[index];
            var pozostalo = mowcy.Count - index;

            var dialog = new Window
            {
                Title = $"⚡ Szybkie mapowanie ({index + 1}/{mowcy.Count})",
                Width = 600,
                Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a2e")),
                ResizeMode = ResizeMode.NoResize
            };

            var mainStack = new StackPanel { Margin = new Thickness(25) };

            // Nagłówek postępu
            var progressPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            var progressBar = new ProgressBar { Width = 200, Height = 6, Value = (index + 1) * 100.0 / mowcy.Count, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")) };
            progressPanel.Children.Add(progressBar);
            progressPanel.Children.Add(new TextBlock { Text = $"  {pozostalo} pozostało", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center });
            mainStack.Children.Add(progressPanel);

            // Karta mówcy
            var mowcaCard = new Border
            {
                Background = mowca.TloKolor,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20)
            };
            var mowcaStack = new StackPanel();

            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var avatar = new Border { Width = 50, Height = 50, CornerRadius = new CornerRadius(25), Background = mowca.KolorMowcy };
            avatar.Child = new TextBlock { Text = "🎤", FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            headerRow.Children.Add(avatar);

            var infoStack = new StackPanel { Margin = new Thickness(15, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock { Text = mowca.DisplayFireflies, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333")) });
            infoStack.Children.Add(new TextBlock { Text = mowca.StatystykiDisplay, FontSize = 12, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")) });
            headerRow.Children.Add(infoStack);
            mowcaStack.Children.Add(headerRow);

            // Przykładowa wypowiedź
            var przykladBorder = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 0) };
            przykladBorder.Child = new TextBlock
            {
                Text = $"\"{mowca.PrzykladowaWypowiedz}\"",
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555")),
                MaxHeight = 60
            };
            mowcaStack.Children.Add(przykladBorder);

            mowcaCard.Child = mowcaStack;
            mainStack.Children.Add(mowcaCard);

            // ComboBox z pracownikami
            var selectLabel = new TextBlock { Text = "Wybierz pracownika:", Foreground = Brushes.White, FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
            mainStack.Children.Add(selectLabel);

            var comboBox = new ComboBox
            {
                ItemsSource = _pracownicy,
                DisplayMemberPath = "DisplayName",
                SelectedValuePath = "UserID",
                FontSize = 16,
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 20)
            };

            // Sugestia jeśli jest
            if (mowca.NajlepszaSugestia != null)
            {
                var sugestia = mowca.NajlepszaSugestia;
                var pracownikSugestia = _pracownicy.FirstOrDefault(p => p.UserID == sugestia.UserID);
                if (pracownikSugestia != null)
                {
                    comboBox.SelectedItem = pracownikSugestia;
                    var sugestiaBadge = new TextBlock
                    {
                        Text = $"💡 Sugestia: {sugestia.UserName} ({sugestia.Pewnosc:F0}%)",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")),
                        FontSize = 12,
                        Margin = new Thickness(0, -15, 0, 10)
                    };
                    mainStack.Children.Add(sugestiaBadge);
                }
            }

            mainStack.Children.Add(comboBox);

            // Przyciski
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            var btnPominBtn = new Button
            {
                Content = "Pomiń (Esc)",
                Padding = new Thickness(20, 12, 20, 12),
                Margin = new Thickness(0, 0, 10, 0),
                Background = Brushes.DimGray,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14
            };

            var btnDalejBtn = new Button
            {
                Content = index < mowcy.Count - 1 ? "Dalej (Enter) →" : "Zakończ ✓",
                Padding = new Thickness(25, 12, 25, 12),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            btnPominBtn.Click += (s, ev) =>
            {
                dialog.Close();
                PokazWizardMapowania(mowcy, index + 1);
            };

            btnDalejBtn.Click += (s, ev) =>
            {
                var selected = comboBox.SelectedItem as PracownikItem;
                if (selected != null && !string.IsNullOrEmpty(selected.UserID))
                {
                    mowca.PrzypisanyUserID = selected.UserID;
                    mowca.PrzypisanyUserName = selected.DisplayName;
                    mowca.ZrodloMapowania = "reczne";
                    mowca.MaSugestie = false;
                    AktualizujZdaniaMowcy(mowca);
                }
                dialog.Close();
                PokazWizardMapowania(mowcy, index + 1);
            };

            btnPanel.Children.Add(btnPominBtn);
            btnPanel.Children.Add(btnDalejBtn);
            mainStack.Children.Add(btnPanel);

            dialog.Content = mainStack;

            // Skróty klawiszowe
            dialog.PreviewKeyDown += (s, ev) =>
            {
                if (ev.Key == Key.Enter)
                {
                    btnDalejBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ev.Handled = true;
                }
                else if (ev.Key == Key.Escape)
                {
                    btnPominBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ev.Handled = true;
                }
            };

            dialog.Loaded += (s, ev) => comboBox.Focus();
            dialog.ShowDialog();
        }

        #endregion

        #region C11: Podsumowanie wg osób

        /// <summary>
        /// C11: Podsumowanie co powiedział każdy uczestnik
        /// </summary>
        private void BtnPodsumowanieNaMowce_Click(object sender, RoutedEventArgs e)
        {
            if (_zdaniaApi == null || _zdaniaApi.Count == 0)
            {
                MessageBox.Show("Brak danych transkrypcji do analizy.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "👤 Podsumowanie wg uczestników",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Nagłówek
            var header = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2")), Padding = new Thickness(20, 15, 20, 15) };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock { Text = "Co powiedział każdy uczestnik?", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            headerStack.Children.Add(new TextBlock { Text = $"{_mowcy.Count} uczestników • {_zdaniaApi.Count} wypowiedzi", FontSize = 12, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B3E5FC")) });
            header.Child = headerStack;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Scroll z kartami mówców
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20) };
            var cardsStack = new StackPanel();

            foreach (var mowca in _mowcy.OrderByDescending(m => m.CzasMowienia))
            {
                var fragmenty = _zdaniaApi.Where(z => z.SpeakerName == mowca.SpeakerNameFireflies).OrderBy(z => z.StartTime).ToList();
                if (fragmenty.Count == 0) continue;

                var card = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(20),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = 0.15 }
                };

                var cardStack = new StackPanel();

                // Nagłówek mówcy
                var mowcaHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
                var colorBadge = new Border { Width = 8, Height = 40, CornerRadius = new CornerRadius(4), Background = mowca.KolorMowcy, Margin = new Thickness(0, 0, 12, 0) };
                mowcaHeader.Children.Add(colorBadge);

                var mowcaInfo = new StackPanel();
                var displayName = !string.IsNullOrEmpty(mowca.PrzypisanyUserName) ? mowca.PrzypisanyUserName : mowca.DisplayFireflies;
                mowcaInfo.Children.Add(new TextBlock { Text = displayName, FontSize = 16, FontWeight = FontWeights.Bold });
                mowcaInfo.Children.Add(new TextBlock { Text = mowca.StatystykiDisplay, FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")) });
                mowcaHeader.Children.Add(mowcaInfo);
                cardStack.Children.Add(mowcaHeader);

                // Analiza tematów - grupuj po słowach kluczowych
                var tematy = AnalizujTematyMowcy(fragmenty);
                if (tematy.Count > 0)
                {
                    var tematyPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
                    foreach (var temat in tematy.Take(5))
                    {
                        var chip = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD")),
                            CornerRadius = new CornerRadius(12),
                            Padding = new Thickness(10, 4, 10, 4),
                            Margin = new Thickness(0, 0, 6, 6)
                        };
                        chip.Child = new TextBlock { Text = temat, FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2")) };
                        tematyPanel.Children.Add(chip);
                    }
                    cardStack.Children.Add(tematyPanel);
                }

                // Kluczowe wypowiedzi
                var kluczowe = WybierzKluczoweWypowiedzi(fragmenty, 3);
                foreach (var wypowiedz in kluczowe)
                {
                    var wypBorder = new Border
                    {
                        Background = mowca.TloKolor,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12),
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    var wypStack = new StackPanel();
                    wypStack.Children.Add(new TextBlock { Text = $"⏱ {TimeSpan.FromSeconds(wypowiedz.StartTime):mm\\:ss}", FontSize = 10, Foreground = Brushes.Gray });
                    wypStack.Children.Add(new TextBlock { Text = wypowiedz.Text, TextWrapping = TextWrapping.Wrap, FontSize = 13 });
                    wypBorder.Child = wypStack;
                    cardStack.Children.Add(wypBorder);
                }

                // Statystyki
                var statsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                var pytaniaCount = fragmenty.Count(f => f.Text?.Contains("?") == true);
                var akcjeCount = fragmenty.Count(f => CzyWypowiedzZawieraAkcje(f.Text));

                if (pytaniaCount > 0)
                {
                    statsPanel.Children.Add(new TextBlock { Text = $"❓ {pytaniaCount} pytań", FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")), Margin = new Thickness(0, 0, 15, 0) });
                }
                if (akcjeCount > 0)
                {
                    statsPanel.Children.Add(new TextBlock { Text = $"✅ {akcjeCount} akcji", FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")) });
                }
                if (statsPanel.Children.Count > 0)
                    cardStack.Children.Add(statsPanel);

                card.Child = cardStack;
                cardsStack.Children.Add(card);
            }

            scroll.Content = cardsStack;
            Grid.SetRow(scroll, 1);
            mainGrid.Children.Add(scroll);

            // Przyciski na dole
            var footer = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")), Padding = new Thickness(20, 15, 20, 15) };
            var footerStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnKopiuj = new Button { Content = "📋 Kopiuj", Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var btnZamknij = new Button { Content = "Zamknij", Padding = new Thickness(15, 8, 15, 8), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#607D8B")), Foreground = Brushes.White, BorderThickness = new Thickness(0) };

            btnKopiuj.Click += (s, ev) =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("PODSUMOWANIE WG UCZESTNIKÓW");
                sb.AppendLine(new string('=', 50));
                sb.AppendLine();

                foreach (var mowca in _mowcy.OrderByDescending(m => m.CzasMowienia))
                {
                    var fragmenty = _zdaniaApi.Where(z => z.SpeakerName == mowca.SpeakerNameFireflies).ToList();
                    if (fragmenty.Count == 0) continue;

                    var displayName = !string.IsNullOrEmpty(mowca.PrzypisanyUserName) ? mowca.PrzypisanyUserName : mowca.DisplayFireflies;
                    sb.AppendLine($"### {displayName} ({mowca.StatystykiDisplay})");
                    sb.AppendLine();

                    var kluczowe = WybierzKluczoweWypowiedzi(fragmenty, 3);
                    foreach (var wyp in kluczowe)
                    {
                        sb.AppendLine($"  • {wyp.Text}");
                    }
                    sb.AppendLine();
                }

                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Skopiowano do schowka!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            btnZamknij.Click += (s, ev) => dialog.Close();

            footerStack.Children.Add(btnKopiuj);
            footerStack.Children.Add(btnZamknij);
            footer.Child = footerStack;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            dialog.Content = mainGrid;
            dialog.Show();
        }

        private List<string> AnalizujTematyMowcy(List<FirefliesSentenceDto> fragmenty)
        {
            var tematy = new Dictionary<string, int>();
            var slowaKluczowe = new[] { "projekt", "spotkanie", "termin", "budżet", "klient", "zespół", "raport", "prezentacja", "deadline", "zadanie", "problem", "rozwiązanie", "plan", "harmonogram", "koszt", "umowa", "oferta", "analiza", "test", "produkt", "usługa", "wdrożenie", "szkolenie", "dokumentacja" };

            foreach (var frag in fragmenty)
            {
                if (string.IsNullOrEmpty(frag.Text)) continue;
                var tekstLower = frag.Text.ToLower();

                foreach (var slowo in slowaKluczowe)
                {
                    if (tekstLower.Contains(slowo))
                    {
                        if (!tematy.ContainsKey(slowo))
                            tematy[slowo] = 0;
                        tematy[slowo]++;
                    }
                }
            }

            return tematy.OrderByDescending(t => t.Value).Select(t => t.Key).ToList();
        }

        private List<FirefliesSentenceDto> WybierzKluczoweWypowiedzi(List<FirefliesSentenceDto> fragmenty, int limit)
        {
            // Priorytetyzuj: pytania, akcje, długie wypowiedzi
            return fragmenty
                .Where(f => !string.IsNullOrEmpty(f.Text) && f.Text.Length > 30)
                .OrderByDescending(f =>
                {
                    int score = f.Text!.Length / 20;
                    if (f.Text.Contains("?")) score += 5;
                    if (CzyWypowiedzZawieraAkcje(f.Text)) score += 3;
                    return score;
                })
                .Take(limit)
                .OrderBy(f => f.StartTime)
                .ToList();
        }

        private bool CzyWypowiedzZawieraAkcje(string? tekst)
        {
            if (string.IsNullOrEmpty(tekst)) return false;
            var lower = tekst.ToLower();
            return lower.Contains("trzeba") || lower.Contains("musimy") || lower.Contains("należy") ||
                   lower.Contains("zrobimy") || lower.Contains("przygotować") || lower.Contains("wysłać") ||
                   lower.Contains("sprawdzić") || lower.Contains("ustalić");
        }

        #endregion

        #region D13: Merge mówców

        /// <summary>
        /// D13: Łączenie mówców jako ta sama osoba
        /// </summary>
        private async void BtnMergeMowcow_Click(object sender, RoutedEventArgs e)
        {
            if (_mowcy.Count < 2)
            {
                MessageBox.Show("Potrzebujesz co najmniej 2 mówców do połączenia.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "🔗 Połącz mówców",
                Width = 550,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            };

            var mainStack = new StackPanel { Margin = new Thickness(20) };

            mainStack.Children.Add(new TextBlock
            {
                Text = "Zaznacz mówców, którzy są tą samą osobą:",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            mainStack.Children.Add(new TextBlock
            {
                Text = "Fireflies czasem rozdziela jedną osobę na kilku mówców. Możesz ich połączyć.",
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Lista mówców z checkboxami
            var listPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            var checkboxes = new List<(CheckBox cb, MowcaMapowanieDisplay mowca)>();

            foreach (var mowca in _mowcy)
            {
                var row = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var rowStack = new StackPanel { Orientation = Orientation.Horizontal };
                var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };

                var colorBadge = new Border
                {
                    Width = 8,
                    Height = 30,
                    CornerRadius = new CornerRadius(4),
                    Background = mowca.KolorMowcy,
                    Margin = new Thickness(0, 0, 12, 0)
                };

                var infoStack = new StackPanel();
                infoStack.Children.Add(new TextBlock { Text = mowca.DisplayFireflies, FontWeight = FontWeights.SemiBold });
                infoStack.Children.Add(new TextBlock { Text = mowca.StatystykiDisplay, FontSize = 11, Foreground = Brushes.Gray });

                rowStack.Children.Add(cb);
                rowStack.Children.Add(colorBadge);
                rowStack.Children.Add(infoStack);
                row.Child = rowStack;
                listPanel.Children.Add(row);

                checkboxes.Add((cb, mowca));
            }

            var scroll = new ScrollViewer { MaxHeight = 250, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = listPanel;
            mainStack.Children.Add(scroll);

            // Wybór docelowego pracownika
            mainStack.Children.Add(new TextBlock { Text = "Przypisz wszystkich do pracownika:", FontSize = 12, Margin = new Thickness(0, 0, 0, 8) });

            var comboBox = new ComboBox
            {
                ItemsSource = _pracownicy,
                DisplayMemberPath = "DisplayName",
                SelectedValuePath = "UserID",
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainStack.Children.Add(comboBox);

            // Przyciski
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnAnuluj = new Button { Content = "Anuluj", Padding = new Thickness(15, 8, 15, 8), Background = Brushes.DimGray, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 10, 0) };
            var btnPolacz = new Button { Content = "🔗 Połącz", Padding = new Thickness(20, 8, 20, 8), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold };

            btnAnuluj.Click += (s, ev) => dialog.Close();
            btnPolacz.Click += async (s, ev) =>
            {
                var zaznaczeni = checkboxes.Where(c => c.cb.IsChecked == true).Select(c => c.mowca).ToList();
                if (zaznaczeni.Count < 2)
                {
                    MessageBox.Show("Zaznacz co najmniej 2 mówców do połączenia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selected = comboBox.SelectedItem as PracownikItem;
                if (selected == null || string.IsNullOrEmpty(selected.UserID))
                {
                    MessageBox.Show("Wybierz pracownika do przypisania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Przypisz wszystkich zaznaczonych do tego samego pracownika
                foreach (var mowca in zaznaczeni)
                {
                    mowca.PrzypisanyUserID = selected.UserID;
                    mowca.PrzypisanyUserName = selected.DisplayName;
                    mowca.ZrodloMapowania = "merge";
                    mowca.MaSugestie = false;
                    AktualizujZdaniaMowcy(mowca);
                }

                if (_transkrypcjaId > 0)
                {
                    await ZapiszMapowaniaDoDb();
                }

                ListaMowcow.Items.Refresh();
                AktualizujStatusMapowania();

                MessageBox.Show($"Połączono {zaznaczeni.Count} mówców jako: {selected.DisplayName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                dialog.Close();
            };

            btnPanel.Children.Add(btnAnuluj);
            btnPanel.Children.Add(btnPolacz);
            mainStack.Children.Add(btnPanel);

            dialog.Content = mainStack;
            dialog.ShowDialog();
        }

        #endregion

        #region D14: Profil głosowy (w odtwarzaczu)

        /// <summary>
        /// D14: Zapisz profil głosowy pracownika - wywoływane z odtwarzacza
        /// </summary>
        private async Task ZapiszProfilGlosowy(MowcaMapowanieDisplay mowca, double startTime, double endTime)
        {
            if (string.IsNullOrEmpty(mowca.PrzypisanyUserID))
            {
                MessageBox.Show("Najpierw przypisz mówcę do pracownika.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                // Upewnij się że tabela istnieje
                string createTable = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FirefliesProfileGlosowe')
                    BEGIN
                        CREATE TABLE FirefliesProfileGlosowe (
                            ID INT IDENTITY(1,1) PRIMARY KEY,
                            UserID NVARCHAR(50) NOT NULL,
                            UserName NVARCHAR(200),
                            FirefliesTranscriptID NVARCHAR(100),
                            SpeakerName NVARCHAR(200),
                            StartTime FLOAT,
                            EndTime FLOAT,
                            Tekst NVARCHAR(MAX),
                            AudioUrl NVARCHAR(500),
                            TranscriptUrl NVARCHAR(500),
                            DataUtworzenia DATETIME DEFAULT GETDATE()
                        );
                    END";
                using (var cmdCreate = new SqlCommand(createTable, conn))
                    await cmdCreate.ExecuteNonQueryAsync();

                // Znajdź fragment do zapisania
                var fragment = _zdaniaApi.FirstOrDefault(z =>
                    z.SpeakerName == mowca.SpeakerNameFireflies &&
                    Math.Abs(z.StartTime - startTime) < 1);

                // Zapisz profil
                string sql = @"INSERT INTO FirefliesProfileGlosowe
                    (UserID, UserName, FirefliesTranscriptID, SpeakerName, StartTime, EndTime, Tekst, AudioUrl, TranscriptUrl)
                    VALUES (@UserID, @UserName, @FID, @Speaker, @Start, @End, @Tekst, @Audio, @Transcript)";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserID", mowca.PrzypisanyUserID);
                cmd.Parameters.AddWithValue("@UserName", (object?)mowca.PrzypisanyUserName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FID", _firefliesId);
                cmd.Parameters.AddWithValue("@Speaker", (object?)mowca.SpeakerNameFireflies ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Start", startTime);
                cmd.Parameters.AddWithValue("@End", endTime);
                cmd.Parameters.AddWithValue("@Tekst", (object?)fragment?.Text ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Audio", (object?)_transkrypcjaApi?.AudioUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Transcript", (object?)_transkrypcjaApi?.TranscriptUrl ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                MessageBox.Show($"Zapisano profil głosowy dla: {mowca.PrzypisanyUserName}\nFragment: {TimeSpan.FromSeconds(startTime):mm\\:ss} - {TimeSpan.FromSeconds(endTime):mm\\:ss}",
                    "Profil zapisany", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu profilu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private Window? _odtwarzaczWindow = null;
        private MediaElement? _mediaElement = null;
        private System.Windows.Threading.DispatcherTimer? _playerTimer = null;
        private Slider? _seekSlider = null;
        private TextBlock? _timeDisplay = null;
        private TextBlock? _currentFragmentText = null;
        private Button? _playPauseBtn = null;
        private bool _isDraggingSlider = false;
        private List<FirefliesSentenceDto>? _currentFragmenty = null;
        private int _currentFragmentIndex = 0;
        private string? _currentTranscriptUrl = null;

        /// <summary>
        /// Szybki odtwarzacz próbek głosu - natywny audio lub lista fragmentów
        /// </summary>
        private void PokazOdtwarzaczZFragmentami(string? audioUrl, string? transcriptUrl, MowcaMapowanieDisplay mowca, List<FirefliesSentenceDto> fragmenty)
        {
            // Zamknij poprzednie
            if (_odtwarzaczWindow != null && _odtwarzaczWindow.IsLoaded)
            {
                _playerTimer?.Stop();
                _mediaElement?.Stop();
                _odtwarzaczWindow.Close();
            }

            _currentFragmenty = fragmenty;
            _currentFragmentIndex = 0;
            _currentTranscriptUrl = transcriptUrl;

            // Brak danych
            if (fragmenty.Count == 0)
            {
                MessageBox.Show($"Brak fragmentów dla: {mowca.DisplayFireflies}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _odtwarzaczWindow = new Window
            {
                Title = $"🎧 {mowca.DisplayFireflies}",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a2e")),
                ResizeMode = ResizeMode.CanResizeWithGrip
            };

            var mainStack = new StackPanel { Margin = new Thickness(15) };

            // === NAGŁÓWEK ===
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            var avatar = new Border
            {
                Width = 50, Height = 50,
                CornerRadius = new CornerRadius(25),
                Background = mowca.KolorMowcy,
                Margin = new Thickness(0, 0, 12, 0)
            };
            avatar.Child = new TextBlock { Text = "🎧", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            headerStack.Children.Add(avatar);

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock { Text = mowca.DisplayFireflies, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
            infoStack.Children.Add(new TextBlock { Text = $"{fragmenty.Count} fragmentów • {mowca.StatystykiDisplay}", FontSize = 11, Foreground = Brushes.Gray });
            headerStack.Children.Add(infoStack);
            mainStack.Children.Add(headerStack);

            // === AKTUALNY FRAGMENT ===
            var fragmentBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16213e")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var fragmentStack = new StackPanel();

            var timeLabel = new TextBlock
            {
                Text = $"⏱ {TimeSpan.FromSeconds(fragmenty[0].StartTime):mm\\:ss}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64B5F6")),
                Margin = new Thickness(0, 0, 0, 8)
            };
            fragmentStack.Children.Add(timeLabel);

            _currentFragmentText = new TextBlock
            {
                Text = fragmenty[0].Text ?? "",
                FontSize = 14,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 100
            };
            fragmentStack.Children.Add(_currentFragmentText);

            // Nawigacja fragmentów
            var navStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var btnPrevFrag = new Button { Content = "◀ Poprzedni", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 10, 0), Background = Brushes.DimGray, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var fragCounter = new TextBlock { Text = $"1 / {fragmenty.Count}", Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
            var btnNextFrag = new Button { Content = "Następny ▶", Padding = new Thickness(10, 5, 10, 5), Background = Brushes.DimGray, Foreground = Brushes.White, BorderThickness = new Thickness(0) };

            btnPrevFrag.Click += (s, ev) =>
            {
                if (_currentFragmentIndex > 0)
                {
                    _currentFragmentIndex--;
                    UpdateFragmentDisplay(timeLabel, fragCounter, fragmenty);
                }
            };
            btnNextFrag.Click += (s, ev) =>
            {
                if (_currentFragmentIndex < fragmenty.Count - 1)
                {
                    _currentFragmentIndex++;
                    UpdateFragmentDisplay(timeLabel, fragCounter, fragmenty);
                }
            };

            navStack.Children.Add(btnPrevFrag);
            navStack.Children.Add(fragCounter);
            navStack.Children.Add(btnNextFrag);
            fragmentStack.Children.Add(navStack);

            fragmentBorder.Child = fragmentStack;
            mainStack.Children.Add(fragmentBorder);

            // === ODTWARZACZ AUDIO (jeśli dostępny) ===
            if (!string.IsNullOrEmpty(audioUrl))
            {
                var audioPanel = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0d1b2a")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var audioStack = new StackPanel();

                // Slider
                var sliderRow = new Grid();
                sliderRow.ColumnDefinitions.Add(new ColumnDefinition());
                sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _seekSlider = new Slider { Minimum = 0, Maximum = 100, Value = 0, Margin = new Thickness(0, 0, 10, 0) };
                _seekSlider.PreviewMouseDown += (s, ev) => _isDraggingSlider = true;
                _seekSlider.PreviewMouseUp += SeekSlider_MouseUp;
                Grid.SetColumn(_seekSlider, 0);
                sliderRow.Children.Add(_seekSlider);

                _timeDisplay = new TextBlock { Text = "00:00 / 00:00", Foreground = Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(_timeDisplay, 1);
                sliderRow.Children.Add(_timeDisplay);
                audioStack.Children.Add(sliderRow);

                // Przyciski
                var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };

                var btnBack = new Button { Content = "⏪ -10s", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(5), Background = Brushes.DimGray, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
                btnBack.Click += (s, ev) => SeekRelative(-10);

                _playPauseBtn = new Button { Content = "▶ Play", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(5), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold };
                _playPauseBtn.Click += BtnPlayPause_Click;

                var btnFwd = new Button { Content = "+10s ⏩", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(5), Background = Brushes.DimGray, Foreground = Brushes.White, BorderThickness = new Thickness(0) };
                btnFwd.Click += (s, ev) => SeekRelative(10);

                var btnGoToFrag = new Button { Content = "▶ Od fragmentu", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(5), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
                btnGoToFrag.Click += (s, ev) =>
                {
                    if (_mediaElement != null && _currentFragmenty != null && _currentFragmentIndex < _currentFragmenty.Count)
                    {
                        _mediaElement.Position = TimeSpan.FromSeconds(_currentFragmenty[_currentFragmentIndex].StartTime);
                        if (_playPauseBtn?.Content?.ToString()?.Contains("Play") == true)
                        {
                            _mediaElement.Play();
                            _playPauseBtn.Content = "⏸ Pauza";
                            _playerTimer?.Start();
                        }
                    }
                };

                btnRow.Children.Add(btnBack);
                btnRow.Children.Add(_playPauseBtn);
                btnRow.Children.Add(btnFwd);
                btnRow.Children.Add(btnGoToFrag);
                audioStack.Children.Add(btnRow);

                audioPanel.Child = audioStack;
                mainStack.Children.Add(audioPanel);

                // MediaElement
                _mediaElement = new MediaElement
                {
                    LoadedBehavior = MediaState.Manual,
                    UnloadedBehavior = MediaState.Stop,
                    Volume = 0.8,
                    Width = 0, Height = 0,
                    Visibility = Visibility.Collapsed
                };
                _mediaElement.MediaOpened += (s, ev) =>
                {
                    // Auto-start od pierwszego fragmentu
                    if (fragmenty.Count > 0)
                        _mediaElement.Position = TimeSpan.FromSeconds(fragmenty[0].StartTime);
                    _mediaElement.Play();
                    if (_playPauseBtn != null) _playPauseBtn.Content = "⏸ Pauza";
                    _playerTimer?.Start();
                };
                _mediaElement.MediaEnded += (s, ev) =>
                {
                    _playerTimer?.Stop();
                    if (_playPauseBtn != null) _playPauseBtn.Content = "▶ Play";
                };
                _mediaElement.MediaFailed += (s, ev) =>
                {
                    MessageBox.Show("Nie można załadować audio. Użyj przycisku 'Otwórz w Fireflies'.", "Błąd audio", MessageBoxButton.OK, MessageBoxImage.Warning);
                };

                try { _mediaElement.Source = new Uri(audioUrl); } catch { }

                // Timer
                _playerTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                _playerTimer.Tick += PlayerTimer_Tick;
            }
            else
            {
                // Brak audio - info
                var noAudioInfo = new TextBlock
                {
                    Text = "ℹ️ Audio niedostępne w API. Użyj przycisku poniżej aby otworzyć w Fireflies.",
                    Foreground = Brushes.Orange,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                mainStack.Children.Add(noAudioInfo);
            }

            // === PRZYCISKI ===
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };

            if (!string.IsNullOrEmpty(transcriptUrl))
            {
                var btnOpen = new Button
                {
                    Content = "🌐 Otwórz w Fireflies",
                    Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5722")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.Bold
                };
                btnOpen.Click += (s, ev) =>
                {
                    var url = transcriptUrl;
                    if (_currentFragmenty != null && _currentFragmentIndex < _currentFragmenty.Count)
                    {
                        var time = _currentFragmenty[_currentFragmentIndex].StartTime;
                        url = $"{transcriptUrl}{(transcriptUrl.Contains("?") ? "&" : "?")}t={Math.Floor(time)}";
                    }
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
                };
                btnPanel.Children.Add(btnOpen);
            }

            if (!mowca.JestPrzypisany)
            {
                var btnAssign = new Button
                {
                    Content = "✓ Przypisz mówcę",
                    Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.Bold
                };
                btnAssign.Click += (s, ev) =>
                {
                    _playerTimer?.Stop();
                    _mediaElement?.Stop();
                    _odtwarzaczWindow?.Close();
                    TxtStatusMapowania.Text = $"Wybierz pracownika dla: {mowca.DisplayFireflies}";
                };
                btnPanel.Children.Add(btnAssign);
            }
            else
            {
                // D14: Przycisk zapisu profilu głosowego (tylko jeśli mówca jest przypisany)
                var btnSaveProfile = new Button
                {
                    Content = "💾 Zapisz profil głosu",
                    Padding = new Thickness(15, 10, 15, 10),
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    ToolTip = "Zapisz ten fragment jako próbkę głosu pracownika"
                };
                btnSaveProfile.Click += async (s, ev) =>
                {
                    if (_currentFragmenty != null && _currentFragmentIndex < _currentFragmenty.Count)
                    {
                        var frag = _currentFragmenty[_currentFragmentIndex];
                        await ZapiszProfilGlosowy(mowca, frag.StartTime, frag.EndTime);
                    }
                };
                btnPanel.Children.Add(btnSaveProfile);
            }

            var btnClose = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(5),
                Background = Brushes.DimGray,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            btnClose.Click += (s, ev) =>
            {
                _playerTimer?.Stop();
                _mediaElement?.Stop();
                _odtwarzaczWindow?.Close();
            };
            btnPanel.Children.Add(btnClose);

            mainStack.Children.Add(btnPanel);

            // Dodaj MediaElement do okna (jeśli istnieje)
            if (_mediaElement != null)
            {
                var container = new Grid();
                container.Children.Add(mainStack);
                container.Children.Add(_mediaElement);
                _odtwarzaczWindow.Content = container;
            }
            else
            {
                _odtwarzaczWindow.Content = mainStack;
            }

            _odtwarzaczWindow.Closing += (s, ev) =>
            {
                _playerTimer?.Stop();
                _mediaElement?.Stop();
            };

            _odtwarzaczWindow.Show();
        }

        private void UpdateFragmentDisplay(TextBlock timeLabel, TextBlock counter, List<FirefliesSentenceDto> fragmenty)
        {
            if (_currentFragmentIndex >= 0 && _currentFragmentIndex < fragmenty.Count)
            {
                var frag = fragmenty[_currentFragmentIndex];
                timeLabel.Text = $"⏱ {TimeSpan.FromSeconds(frag.StartTime):mm\\:ss}";
                if (_currentFragmentText != null) _currentFragmentText.Text = frag.Text ?? "";
                counter.Text = $"{_currentFragmentIndex + 1} / {fragmenty.Count}";
            }
        }

        private Button CreateTransportButton(string content, string tooltip, bool isPrimary = false)
        {
            return new Button
            {
                Content = content,
                Width = isPrimary ? 50 : 40,
                Height = isPrimary ? 50 : 40,
                FontSize = isPrimary ? 20 : 16,
                Margin = new Thickness(5, 0, 5, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isPrimary ? "#4CAF50" : "#37474F")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = tooltip
            };
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaElement == null) return;

            if (_playPauseBtn?.Content?.ToString()?.Contains("Play") == true)
            {
                _mediaElement.Play();
                _playPauseBtn.Content = "⏸ Pauza";
                _playerTimer?.Start();
            }
            else
            {
                _mediaElement.Pause();
                if (_playPauseBtn != null) _playPauseBtn.Content = "▶ Play";
                _playerTimer?.Stop();
            }
        }

        private void SeekRelative(double seconds)
        {
            if (_mediaElement?.NaturalDuration.HasTimeSpan != true) return;
            var newPos = _mediaElement.Position.TotalSeconds + seconds;
            newPos = Math.Max(0, Math.Min(newPos, _mediaElement.NaturalDuration.TimeSpan.TotalSeconds));
            _mediaElement.Position = TimeSpan.FromSeconds(newPos);
        }

        private void SeekSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDraggingSlider = false;
            if (_mediaElement?.NaturalDuration.HasTimeSpan == true && _seekSlider != null)
            {
                var newPos = (_seekSlider.Value / 100.0) * _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;
                _mediaElement.Position = TimeSpan.FromSeconds(newPos);
            }
        }

        private void PlayerTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaElement?.NaturalDuration.HasTimeSpan != true || _isDraggingSlider) return;

            var pos = _mediaElement.Position.TotalSeconds;
            var dur = _mediaElement.NaturalDuration.TimeSpan.TotalSeconds;

            if (_seekSlider != null)
                _seekSlider.Value = (pos / dur) * 100;

            if (_timeDisplay != null)
                _timeDisplay.Text = $"{TimeSpan.FromSeconds(pos):mm\\:ss} / {TimeSpan.FromSeconds(dur):mm\\:ss}";
        }

        #endregion

        #region Event Handlers - Transkrypcja

        private void ChkUzyjNazwSystemowych_Changed(object sender, RoutedEventArgs e)
        {
            if (_zdania == null || _zdania.Count == 0) return;
            _uzyjNazwSystemowych = ChkUzyjNazwSystemowych.IsChecked == true;
            foreach (var z in _zdania) z.UzyjNazwySystemowej = _uzyjNazwSystemowych;
            ListaZdan.Items.Refresh();
        }

        private void ChkPokazCzasy_Changed(object sender, RoutedEventArgs e)
        {
            if (_zdania == null || _zdania.Count == 0) return;
            _pokazCzasy = ChkPokazCzasy.IsChecked == true;
            foreach (var z in _zdania) z.PokazCzas = _pokazCzasy;
            ListaZdan.Items.Refresh();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_zdania == null || _zdania.Count == 0) return;
            _filtrTekst = TxtSzukaj.Text?.ToLower() ?? "";
            FiltrujZdania();
        }

        private void BtnWyczyscSzukaj_Click(object sender, RoutedEventArgs e)
        {
            TxtSzukaj.Text = "";
            _filtrTekst = "";
            _filtrMowcy = "";
            _filtrEmocji = "";
            ResetujPrzyciskiFiltrow();
            FiltrujZdania();
        }

        private void BtnFiltruj_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Wszyscy mowcy", Tag = "" });
            menu.Items.Add(new Separator());

            foreach (var m in _mowcy)
            {
                var nazwa = !string.IsNullOrEmpty(m.PrzypisanyUserName) ? m.PrzypisanyUserName : m.DisplayFireflies;
                menu.Items.Add(new MenuItem { Header = nazwa, Tag = m.SpeakerNameFireflies });
            }

            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                item.Click += (s, ev) =>
                {
                    _filtrMowcy = (s as MenuItem)?.Tag?.ToString() ?? "";
                    FiltrujZdania();
                };
            }

            menu.IsOpen = true;
        }

        private void FiltrujZdania()
        {
            if (_zdania == null || _zdania.Count == 0) return;
            foreach (var z in _zdania)
            {
                bool pasujeTekst = string.IsNullOrEmpty(_filtrTekst) ||
                    z.Tekst?.ToLower().Contains(_filtrTekst) == true ||
                    z.MowcaWyswietlany?.ToLower().Contains(_filtrTekst) == true;

                bool pasujeMowca = string.IsNullOrEmpty(_filtrMowcy) ||
                    z.MowcaFireflies == _filtrMowcy;

                bool pasujeEmocja = SprawdzFiltrEmocji(z.Tekst);

                z.Widocznosc = pasujeTekst && pasujeMowca && pasujeEmocja ? Visibility.Visible : Visibility.Collapsed;
            }

            ListaZdan.Items.Refresh();
        }

        private bool SprawdzFiltrEmocji(string? tekst)
        {
            if (string.IsNullOrEmpty(_filtrEmocji)) return true;
            if (string.IsNullOrEmpty(tekst)) return false;

            var tekstLower = tekst.ToLower();

            switch (_filtrEmocji)
            {
                case "pytania":
                    return tekst.Contains("?") ||
                           tekstLower.Contains("czy ") ||
                           tekstLower.Contains("jak ") ||
                           tekstLower.Contains("co ") ||
                           tekstLower.Contains("gdzie ") ||
                           tekstLower.Contains("kiedy ") ||
                           tekstLower.Contains("dlaczego ") ||
                           tekstLower.Contains("ile ");

                case "wazne":
                    // Liczby, daty, kwoty, nazwy wlasne
                    return System.Text.RegularExpressions.Regex.IsMatch(tekst, @"\d+") ||
                           tekstLower.Contains("ważn") ||
                           tekstLower.Contains("kluczow") ||
                           tekstLower.Contains("termin") ||
                           tekstLower.Contains("deadline") ||
                           tekstLower.Contains("pilne") ||
                           tekstLower.Contains("priorytet") ||
                           tekstLower.Contains("zł") ||
                           tekstLower.Contains("euro") ||
                           tekstLower.Contains("procent") ||
                           tekstLower.Contains("%");

                case "akcje":
                    return tekstLower.Contains("trzeba") ||
                           tekstLower.Contains("musimy") ||
                           tekstLower.Contains("należy") ||
                           tekstLower.Contains("zrobimy") ||
                           tekstLower.Contains("zrobić") ||
                           tekstLower.Contains("wykonać") ||
                           tekstLower.Contains("przygotować") ||
                           tekstLower.Contains("wysłać") ||
                           tekstLower.Contains("sprawdzić") ||
                           tekstLower.Contains("ustalić") ||
                           tekstLower.Contains("umówić") ||
                           tekstLower.Contains("action") ||
                           tekstLower.Contains("todo") ||
                           tekstLower.Contains("task");

                default:
                    return true;
            }
        }

        private void ResetujPrzyciskiFiltrow()
        {
            var szaryKolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9E9E"));
            BtnFiltrPytania.Background = szaryKolor;
            BtnFiltrWazne.Background = szaryKolor;
            BtnFiltrAkcje.Background = szaryKolor;
        }

        private void UstawAktywnyFiltr(Button aktywny)
        {
            ResetujPrzyciskiFiltrow();
            aktywny.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
        }

        private void BtnFiltrPytania_Click(object sender, RoutedEventArgs e)
        {
            if (_filtrEmocji == "pytania")
            {
                _filtrEmocji = "";
                ResetujPrzyciskiFiltrow();
            }
            else
            {
                _filtrEmocji = "pytania";
                UstawAktywnyFiltr(BtnFiltrPytania);
            }
            FiltrujZdania();
        }

        private void BtnFiltrWazne_Click(object sender, RoutedEventArgs e)
        {
            if (_filtrEmocji == "wazne")
            {
                _filtrEmocji = "";
                ResetujPrzyciskiFiltrow();
            }
            else
            {
                _filtrEmocji = "wazne";
                UstawAktywnyFiltr(BtnFiltrWazne);
            }
            FiltrujZdania();
        }

        private void BtnFiltrAkcje_Click(object sender, RoutedEventArgs e)
        {
            if (_filtrEmocji == "akcje")
            {
                _filtrEmocji = "";
                ResetujPrzyciskiFiltrow();
            }
            else
            {
                _filtrEmocji = "akcje";
                UstawAktywnyFiltr(BtnFiltrAkcje);
            }
            FiltrujZdania();
        }

        #endregion

        #region Tryb ciemny i widok kolumnowy

        private void BtnTrybCiemny_Click(object sender, RoutedEventArgs e)
        {
            _trybCiemny = !_trybCiemny;
            ZastosujTrybKolorow();
        }

        private void ZastosujTrybKolorow()
        {
            if (_trybCiemny)
            {
                // Tryb ciemny
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
                BtnTrybCiemny.Content = "☀️";

                // Aktualizuj karty
                foreach (var child in FindVisualChildren<Border>(this))
                {
                    if (child.Background is SolidColorBrush brush && brush.Color == Colors.White)
                    {
                        child.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D2D"));
                    }
                }

                // Aktualizuj teksty
                foreach (var tb in FindVisualChildren<TextBlock>(this))
                {
                    if (tb.Foreground is SolidColorBrush brush)
                    {
                        var color = brush.Color.ToString();
                        if (color == "#FF333333" || color == "#FF666666")
                        {
                            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                        }
                    }
                }
            }
            else
            {
                // Tryb jasny - przywroc oryginalne kolory
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"));
                BtnTrybCiemny.Content = "🌙";

                // Wymaga ponownego zaladowania okna dla pelnego efektu
                // Na razie odswiezamy podstawowe elementy
            }
        }

        private void BtnWidokKolumnowy_Click(object sender, RoutedEventArgs e)
        {
            _widokKolumnowy = !_widokKolumnowy;

            if (_widokKolumnowy)
            {
                BtnWidokKolumnowy.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                PokazWidokKolumnowy();
            }
            else
            {
                BtnWidokKolumnowy.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#607D8B"));
                // Powrot do normalnego widoku - odswiez liste
                ListaZdan.Items.Refresh();
            }
        }

        private void PokazWidokKolumnowy()
        {
            // Otworz nowe okno z widokiem kolumnowym
            var dialog = new Window
            {
                Title = "Widok kolumnowy - rozmowa",
                Width = 1200,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5"))
            };

            var mainGrid = new Grid();
            var columnCount = Math.Min(_mowcy.Count, 4);
            for (int i = 0; i < columnCount; i++)
            {
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int i = 0; i < columnCount && i < _mowcy.Count; i++)
            {
                var mowca = _mowcy.ElementAt(i);
                var column = new StackPanel { Margin = new Thickness(10) };

                // Naglowek kolumny
                var header = new Border
                {
                    Background = mowca.KolorMowcy,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var headerText = new TextBlock
                {
                    Text = !string.IsNullOrEmpty(mowca.PrzypisanyUserName) ? mowca.PrzypisanyUserName : mowca.DisplayFireflies,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center
                };
                header.Child = headerText;
                column.Children.Add(header);

                // Wypowiedzi mowcy
                var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                var wypowiedziPanel = new StackPanel();

                foreach (var z in _zdania.Where(x => x.MowcaFireflies == mowca.SpeakerNameFireflies || x.SpeakerId == mowca.SpeakerId))
                {
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Colors.White),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8),
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    var textBlock = new TextBlock
                    {
                        Text = z.Tekst,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12
                    };
                    border.Child = textBlock;
                    wypowiedziPanel.Children.Add(border);
                }

                scrollViewer.Content = wypowiedziPanel;
                column.Children.Add(scrollViewer);

                Grid.SetColumn(column, i);
                mainGrid.Children.Add(column);
            }

            dialog.Content = mainGrid;
            dialog.Show();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }

        private void BtnKopiujTranskrypcje_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"TRANSKRYPCJA: {TxtTytul.Text}");
            sb.AppendLine($"Data: {TxtData.Text}");
            sb.AppendLine($"Czas trwania: {TxtCzasTrwania.Text}");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();

            foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
            {
                var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                    ? z.MowcaSystemowy : z.MowcaFireflies;
                sb.AppendLine($"[{z.CzasDisplay}] {mowca}:");
                sb.AppendLine($"  {z.Tekst}");
                sb.AppendLine();
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show("Transkrypcja skopiowana do schowka!", "Skopiowano", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnEksportuj_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Kopiuj jako tekst", Tag = "txt" });
            menu.Items.Add(new MenuItem { Header = "Kopiuj jako CSV", Tag = "csv" });
            menu.Items.Add(new MenuItem { Header = "Kopiuj jako HTML", Tag = "html" });

            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                item.Click += (s, ev) => EksportujJako((s as MenuItem)?.Tag?.ToString() ?? "txt");
            }

            menu.IsOpen = true;
        }

        private void EksportujJako(string format)
        {
            var sb = new StringBuilder();

            switch (format)
            {
                case "csv":
                    sb.AppendLine("Czas,Mowca,Tekst");
                    foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
                    {
                        var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                            ? z.MowcaSystemowy : z.MowcaFireflies;
                        sb.AppendLine($"\"{z.CzasDisplay}\",\"{mowca}\",\"{z.Tekst?.Replace("\"", "\"\"")}\"");
                    }
                    break;

                case "html":
                    sb.AppendLine("<html><body>");
                    sb.AppendLine($"<h1>{TxtTytul.Text}</h1>");
                    sb.AppendLine($"<p>Data: {TxtData.Text} | Czas: {TxtCzasTrwania.Text}</p>");
                    sb.AppendLine("<table border='1'><tr><th>Czas</th><th>Mowca</th><th>Tekst</th></tr>");
                    foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
                    {
                        var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                            ? z.MowcaSystemowy : z.MowcaFireflies;
                        sb.AppendLine($"<tr><td>{z.CzasDisplay}</td><td><b>{mowca}</b></td><td>{z.Tekst}</td></tr>");
                    }
                    sb.AppendLine("</table></body></html>");
                    break;

                default:
                    BtnKopiujTranskrypcje_Click(null, null);
                    return;
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"Eksport {format.ToUpper()} skopiowany do schowka!", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Skroty klawiaturowe

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+S - Zapisz
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                BtnZapiszWszystko_Click(null, null);
            }
            // Ctrl+F - Szukaj
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                TxtSzukaj.Focus();
                TxtSzukaj.SelectAll();
            }
            // Ctrl+R - Odswiez
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                BtnOdswiez_Click(null, null);
            }
            // Ctrl+G - Idz do czasu
            else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                PokazDialogIdzDoCzasu();
            }
            // Escape - Wyczysc szukanie
            else if (e.Key == Key.Escape)
            {
                if (!string.IsNullOrEmpty(TxtSzukaj.Text))
                {
                    TxtSzukaj.Text = "";
                    _filtrTekst = "";
                    _filtrMowcy = "";
                    FiltrujZdania();
                    e.Handled = true;
                }
            }
            // Ctrl+Plus - Powieksz czcionke
            else if ((e.Key == Key.Add || e.Key == Key.OemPlus) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                ZmienRozmiarCzcionki(2);
            }
            // Ctrl+Minus - Pomniejsz czcionke
            else if ((e.Key == Key.Subtract || e.Key == Key.OemMinus) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                ZmienRozmiarCzcionki(-2);
            }
            // Ctrl+0 - Resetuj rozmiar czcionki
            else if (e.Key == Key.D0 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                _fontSize = 13;
                AktualizujRozmiarCzcionki();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Home - Idz na poczatek
            if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (ListaZdan.Items.Count > 0)
                {
                    ListaZdan.ScrollIntoView(ListaZdan.Items[0]);
                }
            }
            // End - Idz na koniec
            else if (e.Key == Key.End && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (ListaZdan.Items.Count > 0)
                {
                    ListaZdan.ScrollIntoView(ListaZdan.Items[ListaZdan.Items.Count - 1]);
                }
            }
        }

        #endregion

        #region Nawigacja i rozmiar czcionki

        private void PokazDialogIdzDoCzasu()
        {
            var dialog = new Window
            {
                Title = "Idz do czasu",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock { Text = "Podaj czas (mm:ss lub sekundy):", Margin = new Thickness(0, 0, 0, 10) });

            var txtCzas = new TextBox { FontSize = 14, Padding = new Thickness(5) };
            panel.Children.Add(txtCzas);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var btnOk = new Button { Content = "Idz", Width = 80, Margin = new Thickness(5, 0, 0, 0) };
            var btnAnuluj = new Button { Content = "Anuluj", Width = 80 };

            btnOk.Click += (s, e) =>
            {
                if (SprobujPrzejscDoCzasu(txtCzas.Text))
                    dialog.Close();
            };
            btnAnuluj.Click += (s, e) => dialog.Close();

            btnPanel.Children.Add(btnAnuluj);
            btnPanel.Children.Add(btnOk);
            panel.Children.Add(btnPanel);

            dialog.Content = panel;
            txtCzas.Focus();
            dialog.ShowDialog();
        }

        private bool SprobujPrzejscDoCzasu(string czasTekst)
        {
            double sekundy = 0;

            // Probuj parsowac mm:ss
            if (czasTekst.Contains(":"))
            {
                var parts = czasTekst.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int min) && int.TryParse(parts[1], out int sek))
                {
                    sekundy = min * 60 + sek;
                }
                else
                {
                    MessageBox.Show("Niepoprawny format czasu. Uzyj mm:ss lub liczby sekund.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            else if (!double.TryParse(czasTekst, out sekundy))
            {
                MessageBox.Show("Niepoprawny format czasu. Uzyj mm:ss lub liczby sekund.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Znajdz zdanie najblizsze temu czasowi
            var zdanie = _zdania.OrderBy(z => Math.Abs(z.StartTime - sekundy)).FirstOrDefault();
            if (zdanie != null)
            {
                ListaZdan.ScrollIntoView(zdanie);
                ListaZdan.SelectedItem = zdanie;
                return true;
            }

            return false;
        }

        private void BtnIdzDoMowcy_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            foreach (var m in _mowcy)
            {
                var nazwa = !string.IsNullOrEmpty(m.PrzypisanyUserName) ? m.PrzypisanyUserName : m.DisplayFireflies;
                var menuItem = new MenuItem { Header = nazwa, Tag = m.SpeakerId };
                menuItem.Click += (s, ev) =>
                {
                    var speakerId = (s as MenuItem)?.Tag as int?;
                    if (speakerId.HasValue)
                    {
                        var zdanie = _zdania.FirstOrDefault(z => z.SpeakerId == speakerId.Value);
                        if (zdanie != null)
                        {
                            ListaZdan.ScrollIntoView(zdanie);
                            ListaZdan.SelectedItem = zdanie;
                        }
                    }
                };
                menu.Items.Add(menuItem);
            }
            menu.IsOpen = true;
        }

        private void ZmienRozmiarCzcionki(double delta)
        {
            _fontSize = Math.Max(10, Math.Min(24, _fontSize + delta));
            AktualizujRozmiarCzcionki();
        }

        private void AktualizujRozmiarCzcionki()
        {
            ListaZdan.FontSize = _fontSize;
            UstawStatus($"Rozmiar czcionki: {_fontSize}pt");
        }

        private void BtnZwiekszCzcionke_Click(object sender, RoutedEventArgs e)
        {
            ZmienRozmiarCzcionki(2);
        }

        private void BtnZmniejszCzcionke_Click(object sender, RoutedEventArgs e)
        {
            ZmienRozmiarCzcionki(-2);
        }

        #endregion

        #region Eksport do pliku

        private void BtnZapiszDoPliku_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "Zapisz jako TXT...", Tag = "txt" });
            menu.Items.Add(new MenuItem { Header = "Zapisz jako CSV...", Tag = "csv" });
            menu.Items.Add(new MenuItem { Header = "Zapisz jako HTML...", Tag = "html" });

            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                item.Click += (s, ev) => ZapiszDoPliku((s as MenuItem)?.Tag?.ToString() ?? "txt");
            }

            menu.IsOpen = true;
        }

        private void ZapiszDoPliku(string format)
        {
            var dialog = new SaveFileDialog();
            var tytulBezowy = TxtTytul.Text?.Replace(":", "").Replace("/", "-").Replace("\\", "-") ?? "transkrypcja";

            switch (format)
            {
                case "csv":
                    dialog.Filter = "CSV files (*.csv)|*.csv";
                    dialog.DefaultExt = ".csv";
                    break;
                case "html":
                    dialog.Filter = "HTML files (*.html)|*.html";
                    dialog.DefaultExt = ".html";
                    break;
                default:
                    dialog.Filter = "Text files (*.txt)|*.txt";
                    dialog.DefaultExt = ".txt";
                    break;
            }

            dialog.FileName = $"{tytulBezowy}_{DateTime.Now:yyyy-MM-dd}";

            if (dialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();

                switch (format)
                {
                    case "csv":
                        sb.AppendLine("Czas,Mowca,Tekst");
                        foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
                        {
                            var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                                ? z.MowcaSystemowy : z.MowcaFireflies;
                            sb.AppendLine($"\"{z.CzasDisplay}\",\"{mowca}\",\"{z.Tekst?.Replace("\"", "\"\"")}\"");
                        }
                        break;

                    case "html":
                        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>");
                        sb.AppendLine("body { font-family: 'Segoe UI', sans-serif; max-width: 900px; margin: 0 auto; padding: 20px; }");
                        sb.AppendLine("h1 { color: #1976D2; } .meta { color: #666; margin-bottom: 20px; }");
                        sb.AppendLine("table { width: 100%; border-collapse: collapse; }");
                        sb.AppendLine("th { background: #1976D2; color: white; padding: 10px; text-align: left; }");
                        sb.AppendLine("td { padding: 8px; border-bottom: 1px solid #ddd; }");
                        sb.AppendLine("tr:hover { background: #f5f5f5; }");
                        sb.AppendLine(".speaker { font-weight: bold; white-space: nowrap; }");
                        sb.AppendLine(".time { color: #666; font-size: 0.9em; }");
                        sb.AppendLine("</style></head><body>");
                        sb.AppendLine($"<h1>{TxtTytul.Text}</h1>");
                        sb.AppendLine($"<p class=\"meta\">Data: {TxtData.Text} | Czas trwania: {TxtCzasTrwania.Text} | Organizator: {TxtOrganizator.Text}</p>");

                        if (!string.IsNullOrEmpty(TxtPodsumowanie.Text))
                        {
                            sb.AppendLine($"<h3>Podsumowanie</h3><p>{TxtPodsumowanie.Text}</p>");
                        }

                        sb.AppendLine("<h3>Transkrypcja</h3>");
                        sb.AppendLine("<table><tr><th>Czas</th><th>Mowca</th><th>Tekst</th></tr>");
                        foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
                        {
                            var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                                ? z.MowcaSystemowy : z.MowcaFireflies;
                            sb.AppendLine($"<tr><td class=\"time\">{z.CzasDisplay}</td><td class=\"speaker\">{mowca}</td><td>{System.Net.WebUtility.HtmlEncode(z.Tekst)}</td></tr>");
                        }
                        sb.AppendLine("</table></body></html>");
                        break;

                    default:
                        sb.AppendLine($"TRANSKRYPCJA: {TxtTytul.Text}");
                        sb.AppendLine($"Data: {TxtData.Text}");
                        sb.AppendLine($"Czas trwania: {TxtCzasTrwania.Text}");
                        sb.AppendLine($"Organizator: {TxtOrganizator.Text}");
                        sb.AppendLine(new string('=', 60));

                        if (!string.IsNullOrEmpty(TxtPodsumowanie.Text))
                        {
                            sb.AppendLine();
                            sb.AppendLine("PODSUMOWANIE:");
                            sb.AppendLine(TxtPodsumowanie.Text);
                            sb.AppendLine(new string('-', 60));
                        }

                        sb.AppendLine();
                        sb.AppendLine("TRANSKRYPCJA:");
                        sb.AppendLine();

                        foreach (var z in _zdania.Where(x => x.Widocznosc == Visibility.Visible))
                        {
                            var mowca = _uzyjNazwSystemowych && !string.IsNullOrEmpty(z.MowcaSystemowy)
                                ? z.MowcaSystemowy : z.MowcaFireflies;
                            sb.AppendLine($"[{z.CzasDisplay}] {mowca}:");
                            sb.AppendLine($"  {z.Tekst}");
                            sb.AppendLine();
                        }
                        break;
                }

                try
                {
                    System.IO.File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Zapisano do pliku:\n{dialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad zapisu: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Event Handlers - Glowne

        private void BtnOtworzFireflies_Click(object sender, RoutedEventArgs e)
        {
            var url = _transkrypcja?.TranskrypcjaUrl ?? $"https://app.fireflies.ai/view/{_firefliesId}";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            BtnOdswiez.IsEnabled = false;
            try
            {
                _zdaniaApi = await _firefliesService.PobierzZdaniaTranskrypcji(_firefliesId);
                _transkrypcjaApi = await _firefliesService.PobierzSzczegolyTranskrypcji(_firefliesId);
                WypelnijFormularz();
                WykryjMowcowZZdan();
                await ZaladujZapisaneMapowania();
                WypelnijZdania();
                UstawSyncStatus(true);
                MessageBox.Show("Odswiezono!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnOdswiez.IsEnabled = true;
            }
        }

        private async void BtnSyncTytul_Click(object sender, RoutedEventArgs e)
        {
            var nowyTytul = TxtTytul.Text.Trim();
            if (string.IsNullOrEmpty(nowyTytul)) return;

            BtnSyncTytul.IsEnabled = false;
            UstawStatus("Synchronizacja tytulu...");

            try
            {
                var (success, message) = await _firefliesService.AktualizujTytulWFireflies(_firefliesId, nowyTytul);
                if (success)
                {
                    UstawSyncStatus(true);
                    MessageBox.Show("Tytul zaktualizowany w Fireflies!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Blad: {message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                BtnSyncTytul.IsEnabled = true;
                UstawStatus("");
            }
        }

        private async void BtnPowiazSpotkanie_Click(object sender, RoutedEventArgs e)
        {
            var spotkanie = CmbSpotkanie.SelectedItem as SpotkanieComboItem;
            if (spotkanie == null || spotkanie.SpotkaniID == 0 || _transkrypcjaId <= 0) return;

            await _firefliesService.PowiazTranskrypcjeZeSpotkaniem(_transkrypcjaId, spotkanie.SpotkaniID);
            MessageBox.Show("Powiazano!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnPowiazNotatke_Click(object sender, RoutedEventArgs e)
        {
            var notatka = CmbNotatka.SelectedItem as NotatkaComboItem;
            if (notatka == null || notatka.NotatkaID == 0 || _transkrypcjaId <= 0) return;

            await _firefliesService.PowiazTranskrypcjeZNotatka(_transkrypcjaId, notatka.NotatkaID);
            MessageBox.Show("Powiazano!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Usunac transkrypcje?", "Potwierdz", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            using var conn = new SqlConnection(CONNECTION_STRING);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM FirefliesTranskrypcje WHERE TranskrypcjaID = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", _transkrypcjaId);
            await cmd.ExecuteNonQueryAsync();

            DialogResult = true;
            Close();
        }

        private async void BtnZapiszWszystko_Click(object sender, RoutedEventArgs e)
        {
            BtnZapiszWszystko.IsEnabled = false;
            UstawStatus("Zapisywanie...");

            try
            {
                await ZapiszDoDb();
                await ZapiszMapowaniaDoDb();

                var nowyTytul = TxtTytul.Text.Trim();
                if (!string.IsNullOrEmpty(nowyTytul))
                {
                    await _firefliesService.AktualizujTytulWFireflies(_firefliesId, nowyTytul);
                }

                UstawSyncStatus(true);
                MessageBox.Show("Zapisano i zsynchronizowano!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnZapiszWszystko.IsEnabled = true;
            }
        }

        private async void BtnZapiszLokalnie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await ZapiszDoDb();
                await ZapiszMapowaniaDoDb();
                MessageBox.Show("Zapisano lokalnie!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region Zapis do DB

        private async Task ZapiszDoDb()
        {
            var slowa = TxtSlowaKluczowe.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList();

            var uczestnicyJson = JsonSerializer.Serialize(_mowcy.Select(m => new
            {
                nazwa = m.SpeakerNameFireflies,
                speakerId = m.SpeakerId?.ToString(),
                userId = m.PrzypisanyUserID,
                userName = m.PrzypisanyUserName
            }).ToList());

            if (_transkrypcjaId > 0)
            {
                await _firefliesService.AktualizujTranskrypcjeWBazie(_transkrypcjaId, TxtTytul.Text, TxtPodsumowanie.Text, slowa, uczestnicyJson);
            }
            else
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                await conn.OpenAsync();

                var transkrypcjaTekst = string.Join("\n", _zdania.Select(z => $"[{z.MowcaFireflies}]: {z.Tekst}"));

                string sql = @"INSERT INTO FirefliesTranskrypcje
                    (FirefliesID, Tytul, DataSpotkania, CzasTrwaniaSekundy, Uczestnicy, HostEmail,
                     Transkrypcja, TranskrypcjaUrl, Podsumowanie, SlowKluczowe, StatusImportu, DataImportu)
                VALUES (@FirefliesID, @Tytul, @Data, @Czas, @Ucz, @Host, @Trans, @Url, @Pod, @Slowa, 'Reczny', GETDATE());
                SELECT SCOPE_IDENTITY();";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@FirefliesID", _firefliesId);
                cmd.Parameters.AddWithValue("@Tytul", TxtTytul.Text);
                cmd.Parameters.AddWithValue("@Data", (object?)_transkrypcjaApi?.DateAsDateTime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Czas", _transkrypcjaApi?.Duration ?? 0);
                cmd.Parameters.AddWithValue("@Ucz", uczestnicyJson);
                cmd.Parameters.AddWithValue("@Host", (object?)_transkrypcjaApi?.EmailOrganizatora ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Trans", transkrypcjaTekst);
                cmd.Parameters.AddWithValue("@Url", (object?)_transkrypcjaApi?.TranscriptUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Pod", (object?)TxtPodsumowanie.Text ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Slowa", JsonSerializer.Serialize(slowa));

                var newId = await cmd.ExecuteScalarAsync();
                _transkrypcjaId = Convert.ToInt64(newId);
            }
        }

        private async Task ZapiszMapowaniaDoDb()
        {
            if (_transkrypcjaId <= 0)
            {
                await ZapiszDoDb();
            }

            var mapowania = _mowcy.Select(m => new MowcaMapowanie
            {
                SpeakerId = m.SpeakerId,
                SpeakerNameFireflies = m.SpeakerNameFireflies,
                PrzypisanyUserID = m.PrzypisanyUserID,
                PrzypisanyUserName = m.PrzypisanyUserName
            }).ToList();

            await _firefliesService.ZapiszMapowanieMowcow(_transkrypcjaId, mapowania);
        }

        #endregion

        #region Pomocnicze

        private void UstawStatus(string tekst) => TxtStatusZapisu.Text = tekst;

        private void UstawSyncStatus(bool ok, string? blad = null)
        {
            BadgeSync.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ok ? "#4CAF50" : "#FF9800"));
            TxtSyncStatus.Text = ok ? "Zsynchronizowane" : (blad ?? "Niezapisane");
        }

        private string FormatujCzas(double? sek)
        {
            if (!sek.HasValue) return "---";
            var ts = TimeSpan.FromSeconds(sek.Value);
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}min" : $"{ts.Minutes}min {ts.Seconds}s";
        }

        #endregion
    }

    #region Helper Classes

    public class MowcaMapowanieDisplay : INotifyPropertyChanged
    {
        private string? _przypisanyUserID;
        private string? _przypisanyUserName;
        private string _zrodloMapowania = ""; // "auto", "zapisane", "reczne"
        private bool _maSugestie;
        private double _pewnoscMapowania;
        private int _liczbaUzycMapowania;

        public int? SpeakerId { get; set; }
        public string? SpeakerNameFireflies { get; set; }
        public int LiczbaWypowiedzi { get; set; }
        public double CzasMowienia { get; set; }
        public double ProcentCzasu { get; set; }
        public string? PrzykladowaWypowiedz { get; set; }

        // Timestamp pierwszej wypowiedzi (w sekundach) - do odtwarzania
        public double PierwszyCzas { get; set; }

        public string? PrzypisanyUserID
        {
            get => _przypisanyUserID;
            set
            {
                _przypisanyUserID = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusPrzypisania));
                OnPropertyChanged(nameof(StatusTlo));
                OnPropertyChanged(nameof(StatusKolor));
                OnPropertyChanged(nameof(JestPrzypisany));
                OnPropertyChanged(nameof(RamkaBrush));
                OnPropertyChanged(nameof(AutoMapowanieBadge));
                OnPropertyChanged(nameof(AutoMapowanieBadgeWidocznosc));
            }
        }

        public string? PrzypisanyUserName
        {
            get => _przypisanyUserName;
            set { _przypisanyUserName = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusPrzypisania)); }
        }

        // Właściwości dla auto-mapowania
        public string ZrodloMapowania
        {
            get => _zrodloMapowania;
            set
            {
                _zrodloMapowania = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoMapowanieBadge));
                OnPropertyChanged(nameof(AutoMapowanieBadgeWidocznosc));
                OnPropertyChanged(nameof(AutoMapowanieBadgeTlo));
            }
        }

        public bool MaSugestie
        {
            get => _maSugestie;
            set
            {
                _maSugestie = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SugestiaBadgeWidocznosc));
            }
        }

        public double PewnoscMapowania
        {
            get => _pewnoscMapowania;
            set
            {
                _pewnoscMapowania = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoMapowanieBadge));
            }
        }

        public int LiczbaUzycMapowania
        {
            get => _liczbaUzycMapowania;
            set { _liczbaUzycMapowania = value; OnPropertyChanged(); }
        }

        public List<GlobalMapowanieSugestia> Sugestie { get; set; } = new();
        public GlobalMapowanieSugestia? NajlepszaSugestia => Sugestie.FirstOrDefault();

        public List<PracownikItem> DostepniPracownicy { get; set; } = new();

        public SolidColorBrush KolorMowcy { get; set; } = new SolidColorBrush(Colors.Gray);
        public SolidColorBrush TloKolor { get; set; } = new SolidColorBrush(Colors.White);

        // Podstawowe wyswietlanie
        public string DisplayFireflies => SpeakerNameFireflies ?? $"Mowca {SpeakerId}";
        public string CzasMowieniaDisplay => TimeSpan.FromSeconds(CzasMowienia).ToString(@"mm\:ss");
        public string ProcentCzasuDisplay => $"({ProcentCzasu:F0}%)";

        // Nowe wlasciwosci dla uproszczonego UI
        public string StatystykiDisplay => $"{LiczbaWypowiedzi} wyp. | {CzasMowieniaDisplay} ({ProcentCzasu:F0}%)";
        public bool JestPrzypisany => !string.IsNullOrEmpty(PrzypisanyUserID);

        public SolidColorBrush RamkaBrush => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(JestPrzypisany ? "#4CAF50" : "#E0E0E0"));

        public string StatusPrzypisania
        {
            get
            {
                if (string.IsNullOrEmpty(PrzypisanyUserID))
                    return MaSugestie ? "💡 Mamy sugestię!" : "Nie przypisano";

                var status = $"Przypisano: {PrzypisanyUserName}";
                if (ZrodloMapowania == "auto")
                    status = $"🤖 Auto: {PrzypisanyUserName} ({PewnoscMapowania:F0}%)";
                return status;
            }
        }

        // Badge dla auto-mapowania
        public string AutoMapowanieBadge
        {
            get
            {
                if (ZrodloMapowania == "auto")
                    return $"🤖 {PewnoscMapowania:F0}%";
                if (ZrodloMapowania == "sugestia")
                    return $"💡 {PewnoscMapowania:F0}%";
                if (ZrodloMapowania == "zapisane")
                    return "💾";
                if (ZrodloMapowania == "reczne")
                    return "✓";
                return "";
            }
        }

        public Visibility AutoMapowanieBadgeWidocznosc =>
            !string.IsNullOrEmpty(ZrodloMapowania) && JestPrzypisany ? Visibility.Visible : Visibility.Collapsed;

        public SolidColorBrush AutoMapowanieBadgeTlo
        {
            get
            {
                if (ZrodloMapowania == "auto")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                if (ZrodloMapowania == "sugestia")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
                if (ZrodloMapowania == "zapisane")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                if (ZrodloMapowania == "reczne")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0"));
                return new SolidColorBrush(Colors.Gray);
            }
        }

        public Visibility SugestiaBadgeWidocznosc =>
            MaSugestie && !JestPrzypisany ? Visibility.Visible : Visibility.Collapsed;

        public SolidColorBrush StatusTlo => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(
                string.IsNullOrEmpty(PrzypisanyUserID)
                    ? (MaSugestie ? "#FFF8E1" : "#FFF3E0")
                    : (ZrodloMapowania == "auto" ? "#E3F2FD" : "#E8F5E9")));

        public SolidColorBrush StatusKolor => new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(
                string.IsNullOrEmpty(PrzypisanyUserID)
                    ? (MaSugestie ? "#F57F17" : "#E65100")
                    : (ZrodloMapowania == "auto" ? "#1565C0" : "#2E7D32")));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PracownikItem
    {
        private static readonly string[] KoloryAvatarow = {
            "#2196F3", "#4CAF50", "#FF9800", "#9C27B0", "#F44336",
            "#00BCD4", "#795548", "#607D8B", "#E91E63", "#3F51B5"
        };

        public string UserID { get; set; } = "";
        public string DisplayName { get; set; } = "";

        public string Inicjaly
        {
            get
            {
                if (string.IsNullOrEmpty(DisplayName) || string.IsNullOrEmpty(UserID))
                    return "";
                var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                return DisplayName.Length >= 2 ? DisplayName.Substring(0, 2).ToUpper() : DisplayName.ToUpper();
            }
        }

        public SolidColorBrush KolorTla
        {
            get
            {
                if (string.IsNullOrEmpty(UserID)) return new SolidColorBrush(Colors.Transparent);
                var hash = UserID.GetHashCode();
                var idx = Math.Abs(hash) % KoloryAvatarow.Length;
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(KoloryAvatarow[idx]));
            }
        }

        public Visibility InicjalyWidocznosc => string.IsNullOrEmpty(UserID) ? Visibility.Collapsed : Visibility.Visible;
    }

    public class ZdanieDisplay : INotifyPropertyChanged
    {
        private bool _uzyjNazwySystemowej = true;
        private bool _pokazCzas = true;
        private Visibility _widocznosc = Visibility.Visible;

        public int Index { get; set; }
        public int? SpeakerId { get; set; }
        public string? MowcaFireflies { get; set; }
        public string? MowcaSystemowy { get; set; }
        public string? Tekst { get; set; }
        public double StartTime { get; set; }

        public bool UzyjNazwySystemowej
        {
            get => _uzyjNazwySystemowej;
            set { _uzyjNazwySystemowej = value; OnPropertyChanged(nameof(MowcaWyswietlany)); }
        }

        public bool PokazCzas
        {
            get => _pokazCzas;
            set { _pokazCzas = value; OnPropertyChanged(nameof(CzasWidocznosc)); }
        }

        public Visibility Widocznosc
        {
            get => _widocznosc;
            set { _widocznosc = value; OnPropertyChanged(); }
        }

        public string CzasDisplay => TimeSpan.FromSeconds(StartTime).ToString(@"mm\:ss");
        public Visibility CzasWidocznosc => PokazCzas ? Visibility.Visible : Visibility.Collapsed;

        public string MowcaWyswietlany =>
            UzyjNazwySystemowej && !string.IsNullOrEmpty(MowcaSystemowy)
                ? MowcaSystemowy
                : MowcaFireflies ?? "?";

        public SolidColorBrush TloKolor { get; set; } = new SolidColorBrush(Colors.White);
        public SolidColorBrush MowcaKolor { get; set; } = new SolidColorBrush(Colors.Gray);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SpotkanieComboItem
    {
        public long SpotkaniID { get; set; }
        public string TytulDisplay { get; set; } = "";
    }

    public class NotatkaComboItem
    {
        public long NotatkaID { get; set; }
        public string TematDisplay { get; set; } = "";
    }

    #endregion
}
