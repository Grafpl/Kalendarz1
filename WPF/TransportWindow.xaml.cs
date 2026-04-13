using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class TransportWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly string _connTransport;
        private readonly System.Text.StringBuilder _debugLog = new();
        private Dictionary<int, string> _etaCache = new();
        private static readonly Dictionary<string, string> _osrmCache = new(); // URL → JSON response (trwa przez sesję)
        private static readonly System.Net.Http.HttpClient _httpEta = CreateOsrmHttpClient();

        private static System.Net.Http.HttpClient CreateOsrmHttpClient()
        {
            var handler = new System.Net.Http.SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 8,
                AutomaticDecompression = System.Net.DecompressionMethods.All
            };
            var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.ConnectionClose = false;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Kalendarz1-Transport/1.0");
            return client;
        }
        private static Dictionary<string, BitmapSource> _avatarCache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _userIdToName = new();
        private Dictionary<string, string> _handlowiecToUserId = new(StringComparer.OrdinalIgnoreCase);

        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource GetAvatar(string nameOrId, int size = 28)
        {
            if (string.IsNullOrEmpty(nameOrId)) return null;
            if (_avatarCache.TryGetValue(nameOrId, out var cached)) return cached;
            try
            {
                BitmapSource bmp = null;
                // Spróbuj załadować z dysku
                if (UserAvatarManager.HasAvatar(nameOrId))
                    using (var av = UserAvatarManager.GetAvatarRounded(nameOrId, size))
                        if (av != null) bmp = BmpToBmpSrc(av);
                // Fallback — generuj z inicjałów
                if (bmp == null)
                {
                    var displayName = _userIdToName.TryGetValue(nameOrId, out var n) ? n : nameOrId;
                    using var defAv = UserAvatarManager.GenerateDefaultAvatar(displayName, nameOrId, size);
                    bmp = BmpToBmpSrc(defAv);
                }
                if (bmp != null) { bmp.Freeze(); _avatarCache[nameOrId] = bmp; }
                return bmp;
            }
            catch { return null; }
        }

        private BitmapSource BmpToBmpSrc(System.Drawing.Image img)
        {
            if (img == null) return null;
            using var bmp = new System.Drawing.Bitmap(img);
            var hBmp = bmp.GetHbitmap();
            try { return Imaging.CreateBitmapSourceFromHBitmap(hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); }
            finally { DeleteObject(hBmp); }
        }
        private readonly DataTable _dtTransport = new();
        private DateTime _selectedDate;
        private bool _isLoading;

        // Cache dla kolorów tras (grupowanie po KursID)
        private readonly Dictionary<long, Color> _routeColors = new();
        private readonly List<Color> _colorPalette = new List<Color>
        {
            Color.FromRgb(232, 245, 233), // jasny zielony
            Color.FromRgb(227, 242, 253), // jasny niebieski
            Color.FromRgb(255, 243, 224), // jasny pomarańczowy
            Color.FromRgb(243, 229, 245), // jasny fioletowy
            Color.FromRgb(255, 235, 238), // jasny różowy
            Color.FromRgb(224, 247, 250), // jasny cyjan
            Color.FromRgb(255, 249, 196), // jasny żółty
            Color.FromRgb(225, 245, 254), // jasny błękitny
        };
        private int _colorIndex = 0;

        // Timer do debounce
        private System.Windows.Threading.DispatcherTimer _filterDebounceTimer;

        // Do śledzenia grup (przerwy między trasami)
        private long? _previousKursId = null;

        public TransportWindow(string connLibra, string connHandel, string connTransport, DateTime? initialDate = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connLibra = connLibra;
            _connHandel = connHandel;
            _connTransport = connTransport;
            _selectedDate = initialDate ?? DateTime.Today;

            InitializeDataTable();
            InitializeDebounce();
            InitializeDate();
        }

        private void InitializeDataTable()
        {
            _dtTransport.Columns.Add("Id", typeof(int));
            _dtTransport.Columns.Add("KlientId", typeof(int));
            _dtTransport.Columns.Add("KursId", typeof(long)); // Do grupowania
            _dtTransport.Columns.Add("DataUboju", typeof(DateTime));
            _dtTransport.Columns.Add("Odbiorca", typeof(string));
            _dtTransport.Columns.Add("Handlowiec", typeof(string));
            _dtTransport.Columns.Add("IloscZamowiona", typeof(decimal));
            _dtTransport.Columns.Add("IloscWydana", typeof(decimal));
            _dtTransport.Columns.Add("Palety", typeof(decimal));
            _dtTransport.Columns.Add("Kierowca", typeof(string));
            _dtTransport.Columns.Add("Pojazd", typeof(string));
            _dtTransport.Columns.Add("GodzWyjazdu", typeof(string));
            _dtTransport.Columns.Add("Trasa", typeof(string));
            _dtTransport.Columns.Add("Status", typeof(string));
            _dtTransport.Columns.Add("GrupaIndex", typeof(int));
            _dtTransport.Columns.Add("TelefonKierowcy", typeof(string));
            _dtTransport.Columns.Add("Lokalizacja", typeof(string));
            _dtTransport.Columns.Add("ETA", typeof(string));
            _dtTransport.Columns.Add("KursUtworzyl", typeof(string));
            _dtTransport.Columns.Add("KursUtworzonoUTC", typeof(string));
            _dtTransport.Columns.Add("Kolejnosc", typeof(int));
            _dtTransport.Columns.Add("AdresDostawy", typeof(string));
            _dtTransport.Columns.Add("Awizacja", typeof(string));
            _dtTransport.Columns.Add("PorownanieETA", typeof(string));
            _dtTransport.Columns.Add("PorownanieETADiff", typeof(int)); // minuty różnicy (ujemne = szybciej, dodatnie = opóźnienie)

            dgTransport.ItemsSource = _dtTransport.DefaultView;
            SetupDataGrid();
        }

        private void InitializeDebounce()
        {
            _filterDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            _filterDebounceTimer.Interval = TimeSpan.FromMilliseconds(300);
            _filterDebounceTimer.Tick += (s, e) =>
            {
                _filterDebounceTimer.Stop();
                ApplyFilters();
            };
        }

        private void SetupDataGrid()
        {
            dgTransport.Columns.Clear();

            // Styl zawijania tekstu dla kolumn wielowierszowych
            var wrapStyle = new Style(typeof(TextBlock));
            wrapStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            wrapStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            wrapStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));

            dgTransport.Columns.Add(CreateOdbiorcaColumnZBadge());
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "#", Binding = new Binding("Kolejnosc"), Width = new DataGridLength(28), ElementStyle = (Style)FindResource("CenterAlignedCellStyle") });
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "Adres dostawy", Binding = new Binding("AdresDostawy"), Width = new DataGridLength(150), ElementStyle = wrapStyle });
            dgTransport.Columns.Add(CreateAvatarColumn("Handlowiec", "Handlowiec", 115));
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "Kierowca", Binding = new Binding("Kierowca"), Width = new DataGridLength(115), ElementStyle = wrapStyle });
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "Tel.", Binding = new Binding("TelefonKierowcy"), Width = new DataGridLength(85) });
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "Pojazd", Binding = new Binding("Pojazd"), Width = new DataGridLength(80), ElementStyle = (Style)FindResource("CenterAlignedCellStyle") });
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "Wyjazd z bazy", Binding = new Binding("GodzWyjazdu"), Width = new DataGridLength(110) });
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "ETA przyjazdu", Binding = new Binding("ETA"), Width = new DataGridLength(110), ElementStyle = (Style)FindResource("BoldCellStyle") });
            dgTransport.Columns.Add(CreatePorownanieColumn());
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "Awizacja", Binding = new Binding("Awizacja"), Width = new DataGridLength(105) });
            dgTransport.Columns.Add(new DataGridTextColumn { Header = "GPS", Binding = new Binding("Lokalizacja"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 100 });
            dgTransport.Columns.Add(CreateAvatarColumnWrap("Utworzył kurs", "KursUtworzyl", 120));

            dgTransport.RowHeight = double.NaN; // auto height dla wrapowania
            dgTransport.LoadingRow += DgTransport_LoadingRow;

            // Menu kontekstowe — prawy przycisk na wierszu
            var ctxMenu = new ContextMenu();
            var miGoogleMaps = new MenuItem { Header = "🗺️ Mapa Google — trasa z lokalizacji GPS do klienta" };
            miGoogleMaps.Click += MiGoogleMaps_Click;
            ctxMenu.Items.Add(miGoogleMaps);
            var miGoogleMapsBaza = new MenuItem { Header = "🏭 Mapa Google — trasa z bazy do klienta" };
            miGoogleMapsBaza.Click += MiGoogleMapsBaza_Click;
            ctxMenu.Items.Add(miGoogleMapsBaza);
            ctxMenu.Items.Add(new Separator());
            var miGoogleMapsKlient = new MenuItem { Header = "📍 Otwórz klienta w Google Maps" };
            miGoogleMapsKlient.Click += MiGoogleMapsKlient_Click;
            ctxMenu.Items.Add(miGoogleMapsKlient);
            dgTransport.ContextMenu = ctxMenu;
        }

        private Color GetColorForRoute(long kursId)
        {
            if (kursId == 0) return Colors.White;

            if (!_routeColors.TryGetValue(kursId, out var color))
            {
                color = _colorPalette[_colorIndex % _colorPalette.Count];
                _routeColors[kursId] = color;
                _colorIndex++;
            }
            return color;
        }

        // Oblicza tekst porównania ETA vs Awizacja + liczbę minut różnicy (ujemne = szybciej)
        private (string tekst, int diffMin) ObliczPorownanieETA(string etaText, DateTime? dataAwizacji)
        {
            if (string.IsNullOrEmpty(etaText) || etaText == "Czekanie..." || !dataAwizacji.HasValue)
                return ("", 0);

            // Awizacja bez godziny = brak porównania
            if (dataAwizacji.Value.Hour == 0 && dataAwizacji.Value.Minute == 0)
                return ("", 0);

            // Parsuj ETA z formatu "09.04 CZW. 14:15"
            try
            {
                var parts = etaText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return ("", 0);
                var datePart = parts[0]; // "09.04"
                var timePart = parts[2]; // "14:15"
                var dateBits = datePart.Split('.');
                var timeBits = timePart.Split(':');
                if (dateBits.Length != 2 || timeBits.Length != 2) return ("", 0);

                int day = int.Parse(dateBits[0]);
                int month = int.Parse(dateBits[1]);
                int hour = int.Parse(timeBits[0]);
                int min = int.Parse(timeBits[1]);
                var etaDt = new DateTime(dataAwizacji.Value.Year, month, day, hour, min, 0);

                var diff = (etaDt - dataAwizacji.Value).TotalMinutes;
                int diffMin = (int)Math.Round(diff);

                if (diffMin == 0) return ("Na czas", 0);
                if (diffMin < 0) return ($"Będzie {-diffMin} min szybciej", diffMin);
                return ($"Będzie {diffMin} min opóźnienia", diffMin);
            }
            catch
            {
                return ("", 0);
            }
        }

        private void InitializeDate()
        {
            dpData.SelectedDate = _selectedDate;
            txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd}";
            _ = LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            _debugLog.Clear();
            _sw.Restart();
            ShowLoader("Pobieranie danych...");
            Log($"START data={_selectedDate:yyyy-MM-dd}");
            txtStatus.Text = "Ładowanie...";

            try
            {
                _dtTransport.Rows.Clear();
                _routeColors.Clear();
                _colorIndex = 0;

                // Pobierz kontrahentów
                var contractors = new Dictionary<int, (string Name, string Salesman)>();
                await using (var cnHandel = new SqlConnection(_connHandel))
                {
                    await cnHandel.OpenAsync();
                    const string sqlContr = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                    FROM [HANDEL].[SSCommon].[STContractors] c
                                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym
                                    ON c.Id = wym.ElementId";
                    await using var cmdContr = new SqlCommand(sqlContr, cnHandel);
                    await using var rd = await cmdContr.ExecuteReaderAsync();

                    while (await rd.ReadAsync())
                    {
                        int id = rd.GetInt32(0);
                        string shortcut = rd.IsDBNull(1) ? "" : rd.GetString(1);
                        string salesman = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        contractors[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, salesman);
                    }
                }

                // Pobierz zamówienia TYLKO dla wybranej daty (wg DataUboju)
                var orders = new List<(int Id, int KlientId, decimal IloscZam, decimal IloscWyd, decimal Palety, long? KursId, DateTime DataUboju, DateTime? DataPrzyjazdu)>();
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();
                    const string sql = @"SELECT Id, KlientId, TransportKursID, DataUboju, DataPrzyjazdu
                                         FROM dbo.ZamowieniaMieso
                                         WHERE Status <> 'Anulowane'
                                           AND DataUboju = @SelectedDate
                                         ORDER BY DataUboju DESC";
                    await using var cmd = new SqlCommand(sql, cnLibra);
                    cmd.Parameters.AddWithValue("@SelectedDate", _selectedDate.Date);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int id = rdr.GetInt32(0);
                        int klientId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
                        long? kursId = rdr.IsDBNull(2) ? null : rdr.GetInt64(2);
                        DateTime dataUboju = rdr.IsDBNull(3) ? DateTime.MinValue : rdr.GetDateTime(3);
                        DateTime? dataPrzyjazdu = rdr.IsDBNull(4) ? null : rdr.GetDateTime(4);
                        orders.Add((id, klientId, 0m, 0m, 0m, kursId, dataUboju, dataPrzyjazdu));
                    }
                }

                // Pobierz ilości zamówione per zamówienie
                var orderQuantities = new Dictionary<int, (decimal Zam, decimal Wyd, decimal Palety)>();
                if (orders.Any())
                {
                    var orderIds = string.Join(",", orders.Select(o => o.Id));
                    await using var cnLibra = new SqlConnection(_connLibra);
                    await cnLibra.OpenAsync();
                    var sql = $@"SELECT zmt.ZamowienieId, SUM(zmt.Ilosc) as Zam
                                 FROM [dbo].[ZamowieniaMiesoTowar] zmt
                                 WHERE zmt.ZamowienieId IN ({orderIds})
                                 GROUP BY zmt.ZamowienieId";
                    await using var cmd = new SqlCommand(sql, cnLibra);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    while (await rdr.ReadAsync())
                    {
                        int orderId = rdr.GetInt32(0);
                        decimal zam = rdr.IsDBNull(1) ? 0m : rdr.GetDecimal(1);
                        orderQuantities[orderId] = (zam, 0m, 0m);
                    }
                }

                // Pobierz dane transportu
                var transportDetails = new Dictionary<long, (string Kierowca, string Pojazd, string Trasa, TimeSpan? GodzWyjazdu, string TelKierowcy, string Utworzyl, string Utworzono)>();
                // Kolejność ładunków per zamówienie
                var orderKolejnosc = new Dictionary<int, int>();
                var kursIds = orders.Where(o => o.KursId.HasValue).Select(o => o.KursId!.Value).Distinct().ToList();
                if (kursIds.Any())
                {
                    try
                    {
                        await using var cnTransport = new SqlConnection(_connTransport);
                        await cnTransport.OpenAsync();
                        var kursIdsList = string.Join(",", kursIds);
                        var sqlKurs = $@"SELECT k.KursID, k.Trasa, k.GodzWyjazdu,
                                        CONCAT(ki.Imie, ' ', ki.Nazwisko) as Kierowca,
                                        p.Rejestracja, ki.Telefon, k.Utworzyl,
                                        FORMAT(k.UtworzonoUTC, 'dd.MM HH:mm') as Utworzono
                                        FROM dbo.Kurs k
                                        LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                                        LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                                        WHERE k.KursID IN ({kursIdsList})";
                        await using var cmdKurs = new SqlCommand(sqlKurs, cnTransport);
                        await using var rdKurs = await cmdKurs.ExecuteReaderAsync();
                        while (await rdKurs.ReadAsync())
                        {
                            long kursId = rdKurs.GetInt64(0);
                            string trasa = rdKurs.IsDBNull(1) ? "" : rdKurs.GetString(1);
                            TimeSpan? godzWyjazdu = rdKurs.IsDBNull(2) ? null : rdKurs.GetTimeSpan(2);
                            string kierowca = rdKurs.IsDBNull(3) ? "" : rdKurs.GetString(3);
                            string pojazd = rdKurs.IsDBNull(4) ? "" : rdKurs.GetString(4);
                            string telKier = rdKurs.IsDBNull(5) ? "" : rdKurs.GetString(5);
                            string utworzyl = rdKurs.IsDBNull(6) ? "" : rdKurs.GetString(6);
                            string utworzono = rdKurs.IsDBNull(7) ? "" : rdKurs.GetString(7);
                            transportDetails[kursId] = (kierowca, pojazd, trasa, godzWyjazdu, telKier, utworzyl, utworzono);
                        }

                        // Kolejność ładunków — osobne połączenie (MARS workaround)
                        await using var cnLad = new SqlConnection(_connTransport);
                        await cnLad.OpenAsync();
                        var sqlLad = $@"SELECT KodKlienta, Kolejnosc, KursID FROM dbo.Ladunek
                            WHERE KursID IN ({kursIdsList}) ORDER BY KursID, Kolejnosc";
                        await using var cmdLad = new SqlCommand(sqlLad, cnLad);
                        await using var rdLad = await cmdLad.ExecuteReaderAsync();
                        while (await rdLad.ReadAsync())
                        {
                            var kodKl = rdLad.IsDBNull(0) ? "" : rdLad.GetString(0);
                            var kol = rdLad.GetInt32(1);
                            // Mapuj ZAM_xxx → OrderId
                            if (kodKl.StartsWith("ZAM_") && int.TryParse(kodKl.Substring(4), out var zamId))
                                orderKolejnosc[zamId] = kol;
                        }
                        Log($"Ładunki: {orderKolejnosc.Count} mapowań ZAM→Kolejnosc");
                        foreach (var kvp in orderKolejnosc.Take(5))
                            Log($"  ZAM_{kvp.Key} → kolejność {kvp.Value}");
                    }
                    catch (Exception ex) { Log($"BŁĄD transport: {ex.Message}"); }
                }
                Log($"Kursy: {transportDetails.Count}, Kolejności: {orderKolejnosc.Count}");

                // ═══ BUDUJ TABELĘ NATYCHMIAST (bez GPS/ETA) ═══
                var pojazdGps = new Dictionary<int, (string addr, int speed)>();
                var kursPojazdMap = new Dictionary<long, int>();
                var allAdresy = new Dictionary<string, MapaFloty.WebfleetOrderService.KlientAdresInfo>();

                // Pobierz TYLKO adresy z cache (szybkie ~100ms, bez Nominatim/OSRM)
                var addrSvc = new MapaFloty.WebfleetOrderService();
                try { await addrSvc.EnsureTablesAsync(); } catch { }
                var allKody = orders.Where(o => o.KursId.HasValue && orderKolejnosc.ContainsKey(o.Id))
                    .Select(o => $"ZAM_{o.Id}").Distinct().ToList();
                try { allAdresy = await addrSvc.PobierzAdresySzybkoAsync(allKody); } catch { }
                Log($"Adresy (szybkie): {allAdresy.Count}");
                // GPS i ETA będą doładowane w tle po wyświetleniu tabeli

                // Przypisz indeksy grup do sortowania
                var kursIdToGroupIndex = new Dictionary<long, int>();
                int groupIdx = 0;
                foreach (var kursId in kursIds.OrderBy(k =>
                    transportDetails.TryGetValue(k, out var td) ? td.GodzWyjazdu ?? TimeSpan.MaxValue : TimeSpan.MaxValue)
                    .ThenBy(k => transportDetails.TryGetValue(k, out var td) ? td.Trasa : ""))
                {
                    kursIdToGroupIndex[kursId] = groupIdx++;
                }

                // Pobierz nazwy użytkowników (operators) + mapowanie handlowiec→userId (UserHandlowcy)
                var userNames = new Dictionary<string, string>();
                try
                {
                    await using var cnU = new SqlConnection(_connLibra);
                    await cnU.OpenAsync();
                    await using var cmdU = new SqlCommand("SELECT CAST(ID AS varchar), Name FROM operators", cnU);
                    await using var rdU = await cmdU.ExecuteReaderAsync();
                    while (await rdU.ReadAsync())
                    {
                        userNames[rdU.GetString(0)] = rdU.IsDBNull(1) ? "" : rdU.GetString(1);
                        _userIdToName[rdU.GetString(0)] = rdU.IsDBNull(1) ? "" : rdU.GetString(1);
                    }
                }
                catch { }
                try
                {
                    await using var cnH = new SqlConnection(_connLibra);
                    await cnH.OpenAsync();
                    await using var cmdH = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cnH);
                    await using var rdH = await cmdH.ExecuteReaderAsync();
                    while (await rdH.ReadAsync())
                        _handlowiecToUserId[rdH.GetString(0)] = rdH.GetString(1);
                    Log($"Handlowcy→UserID: {_handlowiecToUserId.Count} mapowań");
                }
                catch (Exception ex) { Log($"UserHandlowcy err: {ex.Message}"); }

                _etaCache.Clear();
                Log($"Tabela: budowanie {orders.Count} wierszy...");

                // Buduj wiersze
                foreach (var order in orders)
                {
                    var (name, salesman) = contractors.TryGetValue(order.KlientId, out var c) ? c : ($"KH {order.KlientId}", "");
                    var (zam, wyd, palety) = orderQuantities.TryGetValue(order.Id, out var q) ? q : (0m, 0m, 0m);

                    string kierowca = "", pojazd = "", trasa = "", godzWyjazdu = "";
                    string telKier = "", lokalizacja = "", eta = "", utworzyl = "", utworzono = "";
                    string status = "Brak";
                    int grupaIndex = int.MaxValue;
                    int kolejnosc = 0;

                    if (order.KursId.HasValue && transportDetails.TryGetValue(order.KursId.Value, out var td))
                    {
                        kierowca = td.Kierowca;
                        pojazd = td.Pojazd;
                        trasa = td.Trasa;
                        if (td.GodzWyjazdu.HasValue)
                        {
                            var dzKursu = _selectedDate.ToString("ddd", new System.Globalization.CultureInfo("pl-PL")).ToUpper();
                            godzWyjazdu = $"{_selectedDate:dd.MM} {dzKursu} {td.GodzWyjazdu.Value:hh\\:mm}";
                        }
                        telKier = td.TelKierowcy;
                        utworzyl = td.Utworzyl;
                        utworzono = td.Utworzono;
                        status = "Przypisany";
                        grupaIndex = kursIdToGroupIndex.TryGetValue(order.KursId.Value, out var gi) ? gi : int.MaxValue - 1;
                    }

                    // Zamień ID twórcy na nazwę
                    if (!string.IsNullOrEmpty(utworzyl) && userNames.TryGetValue(utworzyl, out var uName))
                        utworzyl = uName;

                    // Lokalizacja GPS — placeholder jeśli kurs istnieje ale brak danych
                    if (order.KursId.HasValue)
                    {
                        var hasKursMap = kursPojazdMap.TryGetValue(order.KursId.Value, out var pid2);
                        if (hasKursMap && pojazdGps.TryGetValue(pid2, out var gps))
                            lokalizacja = gps.speed > 0 ? $"{gps.speed} km/h — {gps.addr}" : $"Postój — {gps.addr}";
                        else
                            lokalizacja = "Czekanie...";
                    }

                    // Kolejność na kursie
                    orderKolejnosc.TryGetValue(order.Id, out kolejnosc);

                    // ETA — OSRM (czas dojazdu z bazy do klienta wg kolejności + rozładunek)
                    if (kolejnosc > 0 && !string.IsNullOrEmpty(godzWyjazdu))
                    {
                        if (_etaCache.TryGetValue(order.Id, out var etaVal))
                            eta = etaVal;
                        else
                            eta = "Czekanie...";
                    }

                    // Adres dostawy (z cache OSRM adresów)
                    var adresDostawy = "";
                    if (allAdresy.TryGetValue($"ZAM_{order.Id}", out var addrInfo))
                        adresDostawy = addrInfo.PelnyAdres;

                    // Awizacja — data + dzień tygodnia + godzina
                    var awizacja = "";
                    if (order.DataPrzyjazdu.HasValue)
                    {
                        var dp = order.DataPrzyjazdu.Value;
                        var dzien = dp.ToString("ddd", new System.Globalization.CultureInfo("pl-PL")).ToUpper();
                        awizacja = dp.Hour > 0 ? $"{dp:dd.MM} {dzien} {dp:HH:mm}" : $"{dp:dd.MM} {dzien}";
                    }

                    // Porównanie ETA vs Awizacja
                    var (porownanieTxt, porownanieDiff) = ObliczPorownanieETA(eta, order.DataPrzyjazdu);

                    _dtTransport.Rows.Add(
                        order.Id, order.KlientId, order.KursId ?? 0L, order.DataUboju,
                        name, salesman, zam, wyd, palety,
                        kierowca, pojazd, godzWyjazdu, trasa, status, grupaIndex,
                        telKier, lokalizacja, eta, utworzyl, utworzono, kolejnosc,
                        adresDostawy, awizacja, porownanieTxt, porownanieDiff);
                }

                // Podsumowanie (debug)
                var withGps = 0; var withEta = 0; var withKol = 0;
                foreach (DataRow row in _dtTransport.Rows)
                {
                    if (!string.IsNullOrEmpty(row["Lokalizacja"]?.ToString())) withGps++;
                    if (!string.IsNullOrEmpty(row["ETA"]?.ToString())) withEta++;
                    if (row["Kolejnosc"] != DBNull.Value && Convert.ToInt32(row["Kolejnosc"]) > 0) withKol++;
                }
                Log($"WYNIK: {_dtTransport.Rows.Count} wierszy, GPS={withGps}, ETA={withEta}, Kolejność={withKol}, pojazdGps={pojazdGps.Count}");

                // Sortuj wg grupy (trasy), kolejności na kursie
                _dtTransport.DefaultView.Sort = "GrupaIndex ASC, Kolejnosc ASC, GodzWyjazdu ASC, Trasa ASC, Odbiorca ASC";

                // Aktualizuj statystyki
                UpdateStatistics();

                _sw.Stop();
                Log($"Tabela gotowa w {_sw.ElapsedMilliseconds}ms");
                HideLoader();
                txtStatus.Text = $"Gotowy ({_sw.ElapsedMilliseconds}ms) — GPS/ETA ładuje się...";

                // Doładuj GPS i ETA w tle (nie blokuje UI)
                _ = LoadGpsAndEtaInBackground(orders, transportDetails, orderKolejnosc, kursIds, allAdresy);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                HideLoader();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdateStatistics()
        {
            var view = _dtTransport.DefaultView;
            int przypisanych = 0;
            int bezTransportu = 0;
            decimal sumaKg = 0m;
            decimal sumaPalet = 0m;

            foreach (DataRowView row in view)
            {
                var status = row.Row.Field<string>("Status") ?? "";
                if (status == "Przypisany") przypisanych++;
                else bezTransportu++;

                if (!row.Row.IsNull("IloscZamowiona"))
                    sumaKg += Convert.ToDecimal(row["IloscZamowiona"]);
                if (!row.Row.IsNull("Palety"))
                    sumaPalet += Convert.ToDecimal(row["Palety"]);
            }

            txtPrzypisanych.Text = przypisanych.ToString();
            txtBezTransportu.Text = bezTransportu.ToString();
            txtSumaKg.Text = $"{sumaKg:N0}";
            txtLiczbaWierszy.Text = view.Count.ToString();
            txtSumaPalet.Text = $"{sumaPalet:N1}";
        }

        private void ApplyFilters()
        {
            var filters = new List<string>();

            // Filtr odbiorcy
            if (!string.IsNullOrWhiteSpace(txtFilterOdbiorca.Text))
            {
                filters.Add($"Odbiorca LIKE '%{txtFilterOdbiorca.Text.Replace("'", "''")}%'");
            }

            // Filtr statusu
            if (cmbFilterStatus.SelectedItem is ComboBoxItem item && item.Content.ToString() != "Wszystkie")
            {
                filters.Add($"Status = '{item.Content}'");
            }

            _dtTransport.DefaultView.RowFilter = filters.Any() ? string.Join(" AND ", filters) : "";
            UpdateStatistics();
        }

        private void DpData_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (dpData.SelectedDate.HasValue)
            {
                _selectedDate = dpData.SelectedDate.Value;
                txtSelectedDate.Text = $"Data: {_selectedDate:yyyy-MM-dd}";
                _ = LoadDataAsync();
            }
        }

        private void TxtFilterOdbiorca_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filterDebounceTimer?.Stop();
            _filterDebounceTimer?.Start();
        }

        private void CmbFilterStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private DataGridTemplateColumn CreateAvatarColumn(string header, string bindingName, double width)
        {
            var col = new DataGridTemplateColumn { Header = header, Width = new DataGridLength(width) };

            var template = new DataTemplate();
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackFactory.SetValue(StackPanel.MarginProperty, new Thickness(2));

            // Grid z avatarem (Border z inicjałami + Ellipse na zdjęcie)
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.WidthProperty, 34.0);
            gridFactory.SetValue(Grid.HeightProperty, 34.0);
            gridFactory.SetValue(Grid.MarginProperty, new Thickness(0, 0, 6, 0));

            // Border z inicjałami (fallback) — delikatniejsze kolory
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.WidthProperty, 34.0);
            borderFactory.SetValue(Border.HeightProperty, 34.0);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(17));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(144, 202, 249))); // jaśniejszy niebieski
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(224, 238, 252)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
            borderFactory.SetValue(Border.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 1, BlurRadius = 3, Opacity = 0.12, Direction = 270
            });

            var initialsFactory = new FrameworkElementFactory(typeof(TextBlock));
            initialsFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            initialsFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            initialsFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(21, 101, 192)));
            initialsFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            initialsFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            initialsFactory.SetBinding(TextBlock.TextProperty, new Binding(bindingName)
            {
                Converter = new InitialsConverter()
            });
            borderFactory.AppendChild(initialsFactory);
            gridFactory.AppendChild(borderFactory);

            // Ellipse na zdjęcie (domyślnie hidden, LoadingRow ustawia)
            var ellipseFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.WidthProperty, 34.0);
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.HeightProperty, 34.0);
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.StrokeProperty, new SolidColorBrush(Color.FromRgb(224, 238, 252)));
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.StrokeThicknessProperty, 1.5);
            ellipseFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.NameProperty, $"av_{bindingName}");
            gridFactory.AppendChild(ellipseFactory);

            stackFactory.AppendChild(gridFactory);

            // Tekst (nazwa)
            var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
            txtFactory.SetBinding(TextBlock.TextProperty, new Binding(bindingName));
            txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            txtFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            txtFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            stackFactory.AppendChild(txtFactory);

            template.VisualTree = stackFactory;
            col.CellTemplate = template;

            // Nie ucinaj avatarów
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(UIElement.ClipToBoundsProperty, false));
            col.CellStyle = cellStyle;

            return col;
        }

        // Wersja kolumny z avatarem i zawijaniem tekstu (dla "Utworzył kurs")
        private DataGridTemplateColumn CreateAvatarColumnWrap(string header, string bindingName, double width)
        {
            var col = CreateAvatarColumn(header, bindingName, width);
            // Zmień szablon — podmień TextTrimming na TextWrapping
            var template = new DataTemplate();
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            stackFactory.SetValue(StackPanel.MarginProperty, new Thickness(2));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.WidthProperty, 34.0);
            gridFactory.SetValue(Grid.HeightProperty, 34.0);
            gridFactory.SetValue(Grid.MarginProperty, new Thickness(0, 0, 6, 0));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.WidthProperty, 34.0);
            borderFactory.SetValue(Border.HeightProperty, 34.0);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(17));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(144, 202, 249)));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(224, 238, 252)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));

            var initialsFactory = new FrameworkElementFactory(typeof(TextBlock));
            initialsFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            initialsFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            initialsFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(21, 101, 192)));
            initialsFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            initialsFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            initialsFactory.SetBinding(TextBlock.TextProperty, new Binding(bindingName) { Converter = new InitialsConverter() });
            borderFactory.AppendChild(initialsFactory);
            gridFactory.AppendChild(borderFactory);

            var ellipseFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.WidthProperty, 34.0);
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.HeightProperty, 34.0);
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.StrokeProperty, new SolidColorBrush(Color.FromRgb(224, 238, 252)));
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.StrokeThicknessProperty, 1.5);
            ellipseFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            ellipseFactory.SetValue(System.Windows.Shapes.Ellipse.NameProperty, $"av_{bindingName}");
            gridFactory.AppendChild(ellipseFactory);

            stackFactory.AppendChild(gridFactory);

            // Tekst z zawijaniem zamiast ellipsis
            var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
            txtFactory.SetBinding(TextBlock.TextProperty, new Binding(bindingName));
            txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            txtFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            txtFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            txtFactory.SetValue(TextBlock.MaxWidthProperty, width - 50);
            stackFactory.AppendChild(txtFactory);

            template.VisualTree = stackFactory;
            col.CellTemplate = template;
            return col;
        }

        // Kolumna porównania ETA vs Awizacja — kolorowa (zielony/czerwony)
        private DataGridTemplateColumn CreatePorownanieColumn()
        {
            var col = new DataGridTemplateColumn { Header = "Różnica", Width = new DataGridLength(130) };

            var template = new DataTemplate();
            var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
            txtFactory.SetBinding(TextBlock.TextProperty, new Binding("PorownanieETA"));
            txtFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            txtFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            txtFactory.SetValue(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0));
            txtFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);

            // Kolor na podstawie PorownanieETADiff (ujemne = zielony, dodatnie = czerwony)
            var binding = new Binding("PorownanieETADiff") { Converter = new PorownanieColorConverter() };
            txtFactory.SetBinding(TextBlock.ForegroundProperty, binding);

            template.VisualTree = txtFactory;
            col.CellTemplate = template;
            return col;
        }

        // Kolumna Odbiorca z kolorowym kółkiem (kolor trasy) + pełna nazwa
        private DataGridTemplateColumn CreateOdbiorcaColumnZBadge()
        {
            var col = new DataGridTemplateColumn { Header = "Odbiorca", Width = new DataGridLength(150) };
            var template = new DataTemplate();

            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Kolorowe kółko — kolor trasy
            var dotFactory = new FrameworkElementFactory(typeof(Border));
            dotFactory.SetValue(Border.WidthProperty, 10.0);
            dotFactory.SetValue(Border.HeightProperty, 10.0);
            dotFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            dotFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 6, 0));
            dotFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            dotFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(180, 180, 180)));
            dotFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0.5));
            dotFactory.SetBinding(Border.BackgroundProperty, new Binding("KursId") { Converter = new KursIdToColorConverter(this) });
            stackFactory.AppendChild(dotFactory);

            var txtFactory = new FrameworkElementFactory(typeof(TextBlock));
            txtFactory.SetBinding(TextBlock.TextProperty, new Binding("Odbiorca"));
            txtFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            txtFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            txtFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            stackFactory.AppendChild(txtFactory);

            template.VisualTree = stackFactory;
            col.CellTemplate = template;
            return col;
        }

        // Converter: KursId → kolor trasy (używa _routeColors z TransportWindow)
        private class KursIdToColorConverter : System.Windows.Data.IValueConverter
        {
            private readonly TransportWindow _window;
            public KursIdToColorConverter(TransportWindow window) { _window = window; }
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value == null || value == DBNull.Value) return new SolidColorBrush(Colors.Transparent);
                try
                {
                    long kid = System.Convert.ToInt64(value);
                    if (kid == 0) return new SolidColorBrush(Color.FromRgb(200, 200, 200));
                    var c = _window.GetColorForRoute(kid);
                    // Pełny nasycony kolor (GetColorForRoute zwraca pastelowy — zrób mocniejszy)
                    byte r = (byte)Math.Max(0, c.R - 60);
                    byte g = (byte)Math.Max(0, c.G - 60);
                    byte b = (byte)Math.Max(0, c.B - 60);
                    return new SolidColorBrush(Color.FromRgb(r, g, b));
                }
                catch { return new SolidColorBrush(Colors.Gray); }
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value;
        }

        // Converter: int minuty → Brush (ujemne = zielony, dodatnie = czerwony, 0 = szary)
        private class PorownanieColorConverter : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value == null || value == DBNull.Value) return new SolidColorBrush(Color.FromRgb(150, 150, 150));
                int diff = System.Convert.ToInt32(value);
                if (diff < 0) return new SolidColorBrush(Color.FromRgb(22, 163, 74));   // zielony — szybciej
                if (diff > 0) return new SolidColorBrush(Color.FromRgb(220, 38, 38));   // czerwony — opóźnienie
                return new SolidColorBrush(Color.FromRgb(100, 116, 139));               // szary — na czas
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value;
        }

        // Converter: nazwa → inicjały (2 litery)
        private class InitialsConverter : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var name = value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) return "?";
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpper();
                return name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value;
        }

        private System.Diagnostics.Stopwatch _sw = new();
        private void Log(string msg) => _debugLog.AppendLine($"[{_sw.ElapsedMilliseconds,5}ms] {msg}");

        private void ShowLoader(string text) => Dispatcher.Invoke(() => { LoaderOverlay.Visibility = Visibility.Visible; LoaderDetail.Text = text; });
        private void HideLoader() => Dispatcher.Invoke(() => LoaderOverlay.Visibility = Visibility.Collapsed);

        private void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            var text = _debugLog.ToString();
            if (string.IsNullOrEmpty(text)) text = "Brak logów — załaduj dane najpierw";
            var result = MessageBox.Show(text + "\n\nSkopiować do schowka?", "Debug — Transport",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
                Clipboard.SetText(text);
        }

        // ══════════════════════════════════════════════════════════════════
        // Doładowanie GPS i ETA w tle — po wyświetleniu tabeli
        // ══════════════════════════════════════════════════════════════════

        private async Task LoadGpsAndEtaInBackground(
            List<(int Id, int KlientId, decimal IloscZam, decimal IloscWyd, decimal Palety, long? KursId, DateTime DataUboju, DateTime? DataPrzyjazdu)> orders,
            Dictionary<long, (string Kierowca, string Pojazd, string Trasa, TimeSpan? GodzWyjazdu, string TelKierowcy, string Utworzyl, string Utworzono)> transportDetails,
            Dictionary<int, int> orderKolejnosc,
            List<long> kursIds,
            Dictionary<string, MapaFloty.WebfleetOrderService.KlientAdresInfo> allAdresy)
        {
            try
            {
                var bgSw = System.Diagnostics.Stopwatch.StartNew();

                // 1. GPS — równolegle
                var pojazdGps = new Dictionary<int, (string addr, int speed)>();
                var kursPojazdMap = new Dictionary<long, int>();
                try
                {
                    var svc = new MapaFloty.KursMonitorService();
                    await using var cnT = new SqlConnection(_connTransport);
                    await cnT.OpenAsync();
                    await using var chk = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name='WebfleetVehicleMapping'", cnT);
                    if (Convert.ToInt32(await chk.ExecuteScalarAsync()) > 0)
                    {
                        var pojazdToObj = new Dictionary<int, string>();
                        await using var cmdM = new SqlCommand("SELECT PojazdID, WebfleetObjectNo FROM WebfleetVehicleMapping WHERE PojazdID IS NOT NULL AND WebfleetObjectNo IS NOT NULL", cnT);
                        await using (var rdM = await cmdM.ExecuteReaderAsync())
                            while (await rdM.ReadAsync()) pojazdToObj[Convert.ToInt32(rdM["PojazdID"])] = rdM["WebfleetObjectNo"]?.ToString() ?? "";

                        if (kursIds.Any())
                        {
                            await using var cnT2 = new SqlConnection(_connTransport);
                            await cnT2.OpenAsync();
                            await using var cmdK = new SqlCommand($"SELECT KursID, PojazdID FROM Kurs WHERE KursID IN ({string.Join(",", kursIds)}) AND PojazdID IS NOT NULL", cnT2);
                            await using var rdK = await cmdK.ExecuteReaderAsync();
                            while (await rdK.ReadAsync()) kursPojazdMap[rdK.GetInt64(0)] = rdK.GetInt32(1);
                        }

                        var gpsTasks = new List<(int pid, string obj, Task<(double lat, double lon, int speed, string address)?> t)>();
                        var seen = new HashSet<string>();
                        foreach (var kv in kursPojazdMap)
                        {
                            if (!pojazdToObj.TryGetValue(kv.Value, out var obj) || string.IsNullOrEmpty(obj) || !seen.Add(obj)) continue;
                            gpsTasks.Add((kv.Value, obj, svc.PobierzPozycjeAsync(obj)));
                        }
                        try { await Task.WhenAll(gpsTasks.Select(t => t.t)); } catch { }
                        foreach (var gt in gpsTasks)
                        {
                            try { var p = gt.t.IsCompletedSuccessfully ? gt.t.Result : null;
                                if (p.HasValue) pojazdGps[gt.pid] = (p.Value.address, p.Value.speed); } catch { }
                        }
                    }
                }
                catch { }
                Log($"BG GPS: {pojazdGps.Count} pojazdów, {bgSw.ElapsedMilliseconds}ms");

                // ETAP 1: Zaktualizuj GPS w wierszach od razu (nie czekaj na ETA)
                await Dispatcher.InvokeAsync(() =>
                {
                    int updated = 0;
                    foreach (DataRow row in _dtTransport.Rows)
                    {
                        var kursId = row.IsNull("KursId") ? 0L : (long)row["KursId"];
                        if (kursId > 0 && kursPojazdMap.TryGetValue(kursId, out var pid) && pojazdGps.TryGetValue(pid, out var gps))
                        {
                            row["Lokalizacja"] = gps.speed > 0 ? $"{gps.speed} km/h — {gps.addr}" : $"Postój — {gps.addr}";
                            updated++;
                        }
                        else if (kursId > 0 && row["Lokalizacja"]?.ToString() == "Czekanie...")
                        {
                            row["Lokalizacja"] = "Brak GPS";
                        }
                    }
                    dgTransport.Items.Refresh();
                    txtStatus.Text = $"GPS gotowy ({updated}) — ETA ładuje się...";
                });

                // 2. OSRM ETA — równolegle
                const double bazaLat = 51.86857, bazaLon = 19.79476;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var adresyWithGps = allAdresy.Count(a => a.Value.Lat != 0);
                Log($"BG Adresy: {allAdresy.Count} total, {adresyWithGps} z GPS");

                // Geokoduj adresy bez GPS (Nominatim — równolegle, max 2 na raz)
                if (adresyWithGps < allAdresy.Count)
                {
                    var doGeokodowania = allAdresy.Where(a => a.Value.Lat == 0 && !string.IsNullOrEmpty(a.Value.Miasto)).ToList();
                    var semaphore = new System.Threading.SemaphoreSlim(2);
                    var geoTasks = doGeokodowania.Select(async kv =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            var addrSvc2 = new MapaFloty.WebfleetOrderService();
                            var (lat, lon) = await addrSvc2.GeokodujAdresAsync(kv.Value.Ulica ?? "", kv.Value.Miasto, kv.Value.KodPocztowy);
                            if (lat != 0)
                            {
                                kv.Value.Lat = lat; kv.Value.Lon = lon;
                                await addrSvc2.ZapiszAdresAsync(kv.Value, "auto-bg");
                                Log($"BG GEO: {kv.Key} → {lat:F4},{lon:F4} ({kv.Value.Miasto})");
                            }
                            await Task.Delay(550); // Nominatim rate limit (2 równoległe = 1 req/s)
                        }
                        catch { }
                        finally { semaphore.Release(); }
                    });
                    await Task.WhenAll(geoTasks);
                    adresyWithGps = allAdresy.Count(a => a.Value.Lat != 0);
                    Log($"BG Adresy po geo: {adresyWithGps} z GPS");
                }

                var osrmJobs = new List<(long kursId, int startMin, List<int> orderIds, string url)>();
                foreach (var kursId in kursIds)
                {
                    if (!transportDetails.TryGetValue(kursId, out var td) || td.GodzWyjazdu == null) continue;
                    var przyst = orders.Where(x => x.KursId == kursId && orderKolejnosc.ContainsKey(x.Id))
                        .Select(o => (o.Id, kol: orderKolejnosc[o.Id], kod: $"ZAM_{o.Id}")).OrderBy(x => x.kol).ToList();
                    if (przyst.Count == 0) continue;
                    var points = new List<(double lat, double lon)> { (bazaLat, bazaLon) };
                    var oids = new List<int>();
                    foreach (var p in przyst)
                        if (allAdresy.TryGetValue(p.kod, out var a) && a.Lat != 0) { points.Add((a.Lat, a.Lon)); oids.Add(p.Id); }
                    if (points.Count < 2) continue;
                    var coords = string.Join(";", points.Select(p => $"{p.lon.ToString("F5", ci)},{p.lat.ToString("F5", ci)}"));
                    osrmJobs.Add((kursId, (int)td.GodzWyjazdu.Value.TotalMinutes, oids,
                        $"https://router.project-osrm.org/route/v1/driving/{coords}?overview=false&steps=false&annotations=duration"));
                }

                int totalJobs = osrmJobs.Count;
                int doneJobs = 0;
                var ordersMap = orders.ToDictionary(o => o.Id, o => o.DataPrzyjazdu);

                // Przetwarzaj kursy BATCHAMI po 3 równolegle — po każdym batchu aktualizuj UI
                const int BATCH_SIZE = 3;
                for (int batchStart = 0; batchStart < osrmJobs.Count; batchStart += BATCH_SIZE)
                {
                    var batch = osrmJobs.Skip(batchStart).Take(BATCH_SIZE).ToList();

                    // Pobierz wszystkie w batchu równolegle
                    var batchResults = await Task.WhenAll(batch.Select(async job =>
                    {
                        try
                        {
                            if (_osrmCache.TryGetValue(job.url, out var cached))
                                return (job, json: cached);
                            var json = await _httpEta.GetStringAsync(job.url);
                            _osrmCache[job.url] = json;
                            return (job, json);
                        }
                        catch { return (job, json: (string?)null); }
                    }));

                    var updatedOrderIds = new List<int>();
                    foreach (var (job, jsonResult) in batchResults)
                    {
                        doneJobs++;
                        if (string.IsNullOrEmpty(jsonResult)) continue;
                        try
                        {
                            var json = Newtonsoft.Json.Linq.JObject.Parse(jsonResult);
                            var legs = json["routes"]?[0]?["legs"];
                            if (legs == null) continue;
                            double cumMin = job.startMin;
                            for (int li = 0; li < legs.Count() && li < job.orderIds.Count; li++)
                            {
                                if (li > 0) cumMin += 50;
                                cumMin += (double)(legs[li]?["duration"] ?? 0) / 60.0;
                                var etaDt = _selectedDate.Date.AddMinutes((int)cumMin);
                                var dzE = etaDt.ToString("ddd", new System.Globalization.CultureInfo("pl-PL")).ToUpper();
                                _etaCache[job.orderIds[li]] = $"{etaDt:dd.MM} {dzE} {etaDt:HH:mm}";
                                updatedOrderIds.Add(job.orderIds[li]);
                            }
                        }
                        catch { }
                    }

                    if (updatedOrderIds.Count > 0)
                    {
                        int jobsSoFar = doneJobs;
                        int totalSoFar = totalJobs;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            foreach (DataRow row in _dtTransport.Rows)
                            {
                                var oid = (int)row["Id"];
                                if (updatedOrderIds.Contains(oid) && _etaCache.TryGetValue(oid, out var eta))
                                {
                                    row["ETA"] = eta;
                                    if (ordersMap.TryGetValue(oid, out var dp))
                                    {
                                        var (txt, diff) = ObliczPorownanieETA(eta, dp);
                                        row["PorownanieETA"] = txt;
                                        row["PorownanieETADiff"] = diff;
                                    }
                                }
                            }
                            dgTransport.Items.Refresh();
                            txtStatus.Text = $"ETA: {jobsSoFar}/{totalSoFar} kursów — {_etaCache.Count} zamówień";
                        });
                    }
                }
                Log($"BG ETA: {_etaCache.Count} zamówień, {bgSw.ElapsedMilliseconds}ms");

                // Debug: co jest w etaCache
                foreach (var kv in _etaCache.Take(5))
                    Log($"  etaCache[{kv.Key}] = {kv.Value}");

                // Debug: co jest w wierszach
                if (_dtTransport.Rows.Count > 0)
                {
                    var firstRow = _dtTransport.Rows[0];
                    Log($"  Row[0] Id={(int)firstRow["Id"]}, KursId={firstRow["KursId"]}");
                }

                // ETAP 2 FINAL: wyczyść pozostałe "Czekanie..." i ustaw status końcowy
                await Dispatcher.InvokeAsync(() =>
                {
                    foreach (DataRow row in _dtTransport.Rows)
                    {
                        if (row["ETA"]?.ToString() == "Czekanie...")
                            row["ETA"] = "";
                    }
                    dgTransport.Items.Refresh();
                    txtStatus.Text = $"Gotowy — ETA: {_etaCache.Count} ({bgSw.ElapsedMilliseconds}ms)";
                    Log($"BG UPDATE: ETA={_etaCache.Count}");
                    Log($"BG DONE w {bgSw.ElapsedMilliseconds}ms");
                });
            }
            catch (Exception ex) { Log($"BG ERR: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════════
        // Menu kontekstowe — Google Maps
        // ══════════════════════════════════════════════════════════════════

        private DataRowView? GetSelectedRow()
        {
            if (dgTransport.SelectedItem is DataRowView drv) return drv;
            return null;
        }

        private string? ExtractGpsAdres(string lokalizacja)
        {
            // Format: "50 km/h — ul. Przykładowa 10, Warszawa" lub "Postój — ul. X, Y"
            if (string.IsNullOrWhiteSpace(lokalizacja) || lokalizacja == "Czekanie..." || lokalizacja == "Brak GPS") return null;
            var idx = lokalizacja.IndexOf('—');
            if (idx < 0) return null;
            var adres = lokalizacja.Substring(idx + 1).Trim();
            return string.IsNullOrWhiteSpace(adres) ? null : adres;
        }

        private void OtworzGoogleMaps(string? from, string to)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                MessageBox.Show("Brak adresu docelowego klienta.", "Mapa Google", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string url;
            if (string.IsNullOrWhiteSpace(from))
                url = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(to)}";
            else
                url = $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(from)}&destination={Uri.EscapeDataString(to)}&travelmode=driving";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url, UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można otworzyć przeglądarki: {ex.Message}", "Mapa Google", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MiGoogleMaps_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedRow();
            if (row == null) { MessageBox.Show("Zaznacz wiersz.", "Mapa Google"); return; }
            var lokalizacja = row["Lokalizacja"]?.ToString() ?? "";
            var adresDostawy = row["AdresDostawy"]?.ToString() ?? "";
            var gpsAdres = ExtractGpsAdres(lokalizacja);
            if (gpsAdres == null)
            {
                MessageBox.Show("Brak aktualnej lokalizacji GPS pojazdu — użyję bazy jako punktu startowego.", "Mapa Google", MessageBoxButton.OK, MessageBoxImage.Information);
                OtworzGoogleMaps("Dębowa 32A, 99-322 Żychlin", adresDostawy);
                return;
            }
            OtworzGoogleMaps(gpsAdres, adresDostawy);
        }

        private void MiGoogleMapsBaza_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedRow();
            if (row == null) { MessageBox.Show("Zaznacz wiersz.", "Mapa Google"); return; }
            var adresDostawy = row["AdresDostawy"]?.ToString() ?? "";
            OtworzGoogleMaps("Dębowa 32A, 99-322 Żychlin", adresDostawy);
        }

        private void MiGoogleMapsKlient_Click(object sender, RoutedEventArgs e)
        {
            var row = GetSelectedRow();
            if (row == null) { MessageBox.Show("Zaznacz wiersz.", "Mapa Google"); return; }
            var adresDostawy = row["AdresDostawy"]?.ToString() ?? "";
            OtworzGoogleMaps(null, adresDostawy);
        }

        private void DgTransport_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.ClipToBounds = false;
            if (e.Row.Item is not DataRowView drv) return;

            // Avatary
            LoadAvatarForCell(e.Row, drv, "Handlowiec", "av_Handlowiec");
            LoadAvatarForCell(e.Row, drv, "KursUtworzyl", "av_KursUtworzyl");

            // Kolorowanie wierszy wg kursu
            var status = drv["Status"]?.ToString() ?? "";
            var kursId = drv.Row.IsNull("KursId") ? 0L : drv.Row.Field<long>("KursId");
            if (status == "Brak" || kursId == 0)
            {
                e.Row.Background = new SolidColorBrush(Color.FromRgb(255, 240, 238));
            }
            else
            {
                var color = GetColorForRoute(kursId);
                e.Row.Background = new SolidColorBrush(color);
                var rowIndex = _dtTransport.DefaultView.Cast<DataRowView>().ToList().IndexOf(drv);
                if (rowIndex > 0)
                {
                    var prevKursId = _dtTransport.DefaultView[rowIndex - 1].Row.IsNull("KursId") ? 0L : _dtTransport.DefaultView[rowIndex - 1].Row.Field<long>("KursId");
                    if (prevKursId != kursId && prevKursId != 0)
                    {
                        e.Row.BorderBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                        e.Row.BorderThickness = new Thickness(0, 3, 0, 0);
                    }
                    else e.Row.BorderThickness = new Thickness(0);
                }
            }
        }

        private void LoadAvatarForCell(DataGridRow row, DataRowView drv, string columnName, string ellipseName)
        {
            var val = drv[columnName]?.ToString() ?? "";
            if (string.IsNullOrEmpty(val)) return;

            // Znajdź userId: 1) handlowiec→userId, 2) nazwa→id z operators, 3) val jako fallback
            var userId = val;
            if (_handlowiecToUserId.TryGetValue(val, out var hUid))
                userId = hUid;
            else
            {
                foreach (var kv in _userIdToName)
                    if (kv.Value.Equals(val, StringComparison.OrdinalIgnoreCase)) { userId = kv.Key; break; }
            }

            Task.Run(() =>
            {
                try
                {
                    var avatar = UserAvatarManager.GetAvatar(userId);
                    if (avatar == null) return;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var ellipse = FindVisualChild<System.Windows.Shapes.Ellipse>(row, ellipseName);
                            if (ellipse != null)
                            {
                                var src = ConvertToImageSource(avatar);
                                if (src != null)
                                {
                                    ellipse.Fill = new ImageBrush(src) { Stretch = Stretch.UniformToFill };
                                    ellipse.Visibility = Visibility.Visible;
                                }
                            }
                        }
                        catch { }
                    });
                }
                catch { }
            });
        }

        private T? FindVisualChild<T>(DependencyObject parent, string? name = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tc && (name == null || (child is FrameworkElement fe && fe.Name == name)))
                    return tc;
                var found = FindVisualChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private ImageSource? ConvertToImageSource(System.Drawing.Image image)
        {
            using var ms = new System.IO.MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = ms; bmp.EndInit(); bmp.Freeze();
            return bmp;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
