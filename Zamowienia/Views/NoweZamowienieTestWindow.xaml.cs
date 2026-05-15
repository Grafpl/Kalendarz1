using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1.Zamowienia.Views
{
    public partial class NoweZamowienieTestWindow : Window
    {
        // ════════════════════ CONSTS ════════════════════
        private const decimal POJEMNIKOW_NA_PALECIE = 36m;
        private const decimal POJEMNIKOW_NA_PALECIE_E2 = 40m;
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_POJEMNIKU_PODROBY = 10m;

        // Podroby (serce, wątroba, żołądki) = 10 kg/poj. Reszta = 15 kg/poj.
        private static decimal KgPerPoj(ProductVm p)
            => p.KategoriaDisplay == "Podroby" ? KG_NA_POJEMNIKU_PODROBY : KG_NA_POJEMNIKU;
        private const int LIMIT_PALET_TIR = 33;
        private const int LIMIT_PALET_SOLOWKA = 18;

        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly CultureInfo _pl = new("pl-PL");

        public string UserID { get; }

        // ════════════════════ STAN ════════════════════
        private int _currentStep = 1;
        private string _aktywnyKatalog = "67095";

        private readonly List<KontrahentVm> _kontrahenci = new();
        private readonly List<ProductVm> _produkty = new();
        private readonly Dictionary<string, DateTime> _ostatnieZamowieniaKlienta = new();
        private readonly Dictionary<DateTime, decimal> _obciazenieDni = new();
        private readonly List<int> _favoriteIds = new();
        private readonly List<int> _customerHours = new();
        private readonly HashSet<string> _userHandlowcy = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _handlowiecMapowanie = new(StringComparer.OrdinalIgnoreCase);
        // Process-wide cache avatarów handlowców — dzielony między wszystkie instancje okna.
        // Pierwsze pokazanie awataru: GDI decode + Convert. Kolejne: instant z RAM.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Windows.Media.Imaging.BitmapSource> s_handlowiecAvatarCache = new(StringComparer.OrdinalIgnoreCase);

        private KontrahentVm? _wybranyKlient;
        private DateTime _wybranaData = DateTime.Today.AddDays(1);
        private TimeSpan _wybranaGodzina = new(8, 0, 0);
        private DateTime _dataProdukcji = DateTime.Today;
        private bool _uiReady;
        private readonly int? _editOrderId;
        private bool _isEditMode => _editOrderId.HasValue;
        private OrderSnapshot? _originalSnapshot;   // tylko w edit-mode — do diffa w confirm overlay

        public NoweZamowienieTestWindow(string userId) : this(userId, null) { }

        public NoweZamowienieTestWindow(string userId, int? orderId)
        {
            UserID = userId ?? "";
            _editOrderId = orderId;
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            if (_isEditMode) Title = $"Edytuj zamówienie #{_editOrderId}";

            // Hot keys
            PreviewKeyDown += (s, e) =>
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

                if (e.Key == Key.Escape)
                {
                    if (PopupHotkeys != null && PopupHotkeys.IsOpen) { PopupHotkeys.IsOpen = false; e.Handled = true; return; }
                    BtnCancel_Click(s, e);
                    e.Handled = true;
                }
                else if (e.Key == Key.F1)
                {
                    if (BtnHotkeysHelp != null) BtnHotkeysHelp.IsChecked = !(BtnHotkeysHelp.IsChecked ?? false);
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.S)
                {
                    if (BtnSave.IsEnabled) BtnSave_Click(s, e);
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.F)
                {
                    TxtCustSearch?.Focus();
                    TxtCustSearch?.SelectAll();
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.R)
                {
                    if (_wybranyKlient != null) _ = RepeatLastOrderAsync();
                    else ShowToast("Najpierw wybierz klienta", false);
                    e.Handled = true;
                }
                else if (ctrl && (e.Key == Key.D1 || e.Key == Key.NumPad1))
                {
                    BtnTypProduktu_Click(BtnSwieze, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (ctrl && (e.Key == Key.D2 || e.Key == Key.NumPad2))
                {
                    BtnTypProduktu_Click(BtnMrozone, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.N)
                {
                    ClearCart();
                    e.Handled = true;
                }
            };
        }

        private void ClearCart()
        {
            bool any = _produkty.Any(p => p.QtyKg > 0);
            if (!any) return;
            var r = MessageBox.Show(this, "Wyczyścić koszyk (wszystkie pozycje)?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
            foreach (var p in _produkty)
            {
                p.QtyKg = 0;
                p.E2 = false; p.Folia = false; p.Hallal = false; p.Strefa = false;
                RecalcProductDisplay(p);
            }
            RebuildCart();
            ShowToast("Koszyk wyczyszczony", true);
        }

        // ════════════════════ HANDLERY CHIP / POWTÓRZ (legacy — termin teraz inline w sidebarze) ════════════════════

        private void BtnTerminChip_Click(object sender, RoutedEventArgs e)
        {
            // Legacy proxy — termin jest teraz inline w sidebarze, nie ma popup
        }

        private void BtnTerminPopupClose_Click(object sender, RoutedEventArgs e)
        {
            // Legacy proxy
        }

        private async void BtnRepeatLast_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranyKlient == null) return;
            await RepeatLastOrderAsync();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            bool hasItems = _produkty.Any(p => p.QtyKg > 0);
            if (hasItems)
            {
                var r = MessageBox.Show(this, "Masz pozycje w koszyku. Na pewno zamknąć bez zapisywania?",
                    "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }
            Close();
        }

        private async Task RepeatLastOrderAsync()
        {
            if (_wybranyKlient == null || !int.TryParse(_wybranyKlient.Id, out int kid)) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"
                    SELECT TOP 1 z.Id, z.DataPrzyjazdu
                    FROM dbo.ZamowieniaMieso z
                    WHERE z.KlientId = @kid
                    ORDER BY z.Id DESC", cn);
                cmd.Parameters.AddWithValue("@kid", kid);
                int orderId = 0; DateTime? data = null;
                using (var rd = await cmd.ExecuteReaderAsync())
                {
                    if (await rd.ReadAsync())
                    {
                        orderId = Convert.ToInt32(rd["Id"]);
                        if (rd["DataPrzyjazdu"] != DBNull.Value) data = Convert.ToDateTime(rd["DataPrzyjazdu"]);
                    }
                }
                if (orderId == 0) { ShowToast("Brak historii zamówień tego klienta", false); return; }

                var items = new List<(int Id, decimal Ilosc, string? Cena, bool E2, bool Folia, bool Hallal, bool Strefa)>();
                var cmd2 = new SqlCommand("SELECT KodTowaru, Ilosc, Cena, ISNULL(E2,0) AS E2, ISNULL(Folia,0) AS Folia, ISNULL(Hallal,0) AS Hallal, ISNULL(Strefa,0) AS Strefa FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @oid", cn);
                cmd2.Parameters.AddWithValue("@oid", orderId);
                using (var rd2 = await cmd2.ExecuteReaderAsync())
                {
                    while (await rd2.ReadAsync())
                    {
                        if (!int.TryParse(rd2["KodTowaru"]?.ToString(), out int twrId)) continue;
                        decimal il = Convert.ToDecimal(rd2["Ilosc"] ?? 0);
                        string? cena = rd2["Cena"]?.ToString();
                        bool e2 = Convert.ToBoolean(rd2["E2"]);
                        bool fol = Convert.ToBoolean(rd2["Folia"]);
                        bool hal = Convert.ToBoolean(rd2["Hallal"]);
                        bool str = Convert.ToBoolean(rd2["Strefa"]);
                        items.Add((twrId, il, cena, e2, fol, hal, str));
                    }
                }
                if (items.Count == 0) { ShowToast("Brak pozycji w ostatnim zamówieniu", false); return; }

                string podsumowanie = string.Join("\n", items.Take(8).Select(it =>
                {
                    var p = _produkty.FirstOrDefault(x => x.Id == it.Id);
                    return $"  • {p?.Kod ?? "(towar " + it.Id + ")"} — {it.Ilosc:N0} kg";
                }));
                if (items.Count > 8) podsumowanie += $"\n  … +{items.Count - 8} więcej";

                var dataStr = data.HasValue ? data.Value.ToString("dd.MM.yyyy") : "?";
                var r = MessageBox.Show(this,
                    $"Wczytać ostatnie zamówienie z {dataStr}?\n\n{podsumowanie}",
                    "Powtórz zamówienie", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;

                // Zastosuj
                foreach (var p in _produkty) p.QtyKg = 0;
                foreach (var it in items)
                {
                    var p = _produkty.FirstOrDefault(x => x.Id == it.Id);
                    if (p == null) continue;
                    p.QtyKg = it.Ilosc;
                    if (!string.IsNullOrWhiteSpace(it.Cena)) p.Cena = it.Cena;
                    p.E2 = it.E2;
                    p.Folia = it.Folia;
                    p.Hallal = it.Hallal;
                    p.Strefa = it.Strefa;
                }
                foreach (var p in _produkty.Where(x => x.QtyKg > 0)) RecalcProductDisplay(p);
                RenderProducts();
                RebuildCart();
                ShowToast($"✓ Wczytano {items.Count} pozycji z {dataStr}", true);
            }
            catch (Exception ex)
            {
                ShowToast("Błąd: " + ex.Message, false);
            }
        }

        // ════════════════════ ŁADOWANIE ════════════════════

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _uiReady = true;

                if (DateTime.Today.DayOfWeek == DayOfWeek.Friday)
                    _wybranaData = DateTime.Today.AddDays(3);

                // 4 niezależne loady równolegle (różne DB, brak współdzielonego stanu)
                var tHandlowcy = LoadUserHandlowcyAsync();   // LibraNet
                var tKontr     = LoadKontrahenciAsync();      // Handel
                var tObc       = LoadObciazeniaDniAsync();    // LibraNet
                var tProd      = LoadProductsAsync();         // Handel
                await Task.WhenAll(tHandlowcy, tKontr, tObc, tProd);

                // Zależy od _kontrahenci (mapuje OstatnieZamowienie + LimitKredytowy)
                await LoadOstatnieZamowieniaAsync();

                // UI render — bez obrazków towarów (ImageSource ma INPC, dograne się same)
                RenderCustomers();
                RenderDaysProd();
                RenderDays();
                RenderHours();
                RenderProducts();
                UpdateValidation();
                UpdateTermDisplay();
                RebuildCart();

                // Edit-mode: wczytaj istniejące zamówienie (po renderze, nadpisuje stan)
                if (_isEditMode)
                {
                    await LoadExistingOrderAsync(_editOrderId!.Value);
                }

                // Tło: ciężki BLOB (TowarZdjecia). Bindingi odświeżą się przez INPC.
                _ = LoadProductImagesAsync();
                // Tło: pre-load avatarów handlowców do static cache (jeden raz na sesję).
                _ = Task.Run(PreloadHandlowiecAvatars);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd ładowania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Pre-cache wszystkich avatarów z _handlowiecMapowanie do static cache.
        // Wywoływane raz w Window_Loaded (background). Bez wpływu na UI — wynik widać przy następnym wyborze klienta.
        private void PreloadHandlowiecAvatars()
        {
            try
            {
                var names = _handlowiecMapowanie.Keys.ToList();
                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (s_handlowiecAvatarCache.ContainsKey(name)) continue;
                    try { EnsureHandlowiecAvatarCached(name); } catch { }
                }
            }
            catch { }
        }

        // Wczytuje istniejące zamówienie do edycji — wywoływane po wszystkich Load*Async + pierwszym renderze
        private async Task LoadExistingOrderAsync(int orderId)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // 1) Header
                bool dataProdExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "DataProdukcji");
                string colDataProd = dataProdExists ? ", DataProdukcji" : "";
                string headerSql = $@"SELECT KlientId, DataPrzyjazdu, Uwagi, TransportStatus, TrybE2{colDataProd}
                                       FROM dbo.ZamowieniaMieso WHERE Id = @id";

                int klientId = 0;
                DateTime dataPrzyjazdu = DateTime.Today;
                DateTime? dataProdukcji = null;
                string uwagi = "";
                string transportStatus = "Oczekuje";
                await using (var cmd = new SqlCommand(headerSql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    if (!await rd.ReadAsync())
                    {
                        MessageBox.Show(this, $"Nie znaleziono zamówienia #{orderId}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Close();
                        return;
                    }
                    klientId = Convert.ToInt32(rd["KlientId"]);
                    dataPrzyjazdu = Convert.ToDateTime(rd["DataPrzyjazdu"]);
                    uwagi = rd["Uwagi"]?.ToString() ?? "";
                    transportStatus = rd["TransportStatus"]?.ToString() ?? "Oczekuje";
                    if (dataProdExists && rd["DataProdukcji"] != DBNull.Value)
                        dataProdukcji = Convert.ToDateTime(rd["DataProdukcji"]);
                }

                // 2) Items
                bool strefaTowarExists = await ColumnExistsAsync(cn, "ZamowieniaMiesoTowar", "Strefa");
                string strefaCol = strefaTowarExists ? ", ISNULL(Strefa,0) AS Strefa" : ", CAST(0 AS BIT) AS Strefa";
                string itemsSql = $@"SELECT KodTowaru, Ilosc, Cena, ISNULL(E2,0) AS E2, ISNULL(Folia,0) AS Folia, ISNULL(Hallal,0) AS Hallal{strefaCol}
                                      FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @id";
                var items = new List<(int Id, decimal Ilosc, string? Cena, bool E2, bool Folia, bool Hallal, bool Strefa)>();
                await using (var cmd = new SqlCommand(itemsSql, cn))
                {
                    cmd.Parameters.AddWithValue("@id", orderId);
                    await using var rd = await cmd.ExecuteReaderAsync();
                    while (await rd.ReadAsync())
                    {
                        if (!int.TryParse(rd["KodTowaru"]?.ToString(), out int twrId)) continue;
                        items.Add((
                            twrId,
                            Convert.ToDecimal(rd["Ilosc"] ?? 0),
                            rd["Cena"]?.ToString(),
                            Convert.ToBoolean(rd["E2"]),
                            Convert.ToBoolean(rd["Folia"]),
                            Convert.ToBoolean(rd["Hallal"]),
                            Convert.ToBoolean(rd["Strefa"])
                        ));
                    }
                }

                // 3) Termin
                _wybranaData = dataPrzyjazdu.Date;
                _wybranaGodzina = dataPrzyjazdu.TimeOfDay;
                if (dataProdukcji.HasValue) _dataProdukcji = dataProdukcji.Value.Date;

                // 4) Klient — znajdź w _kontrahenci i zastosuj
                _wybranyKlient = _kontrahenci.FirstOrDefault(k => k.Id == klientId.ToString(CultureInfo.InvariantCulture));
                if (_wybranyKlient != null)
                {
                    if (ClientListContainer != null) ClientListContainer.Visibility = Visibility.Collapsed;
                    await ApplySelectedCustomerAsync();
                }

                // 5) Notatka + transport (po ApplySelectedCustomerAsync, które mogło załadować preferencje klienta)
                TxtUwagi.Text = uwagi;
                ChkWlasnyOdbior.IsChecked = string.Equals(transportStatus, "Wlasny", StringComparison.OrdinalIgnoreCase);
                ChkSidebarWlasny.IsChecked = ChkWlasnyOdbior.IsChecked;
                LblGodzinaHeader.Text = ChkWlasnyOdbior.IsChecked == true ? "Godzina odbioru" : "Godzina przyjazdu";

                // 6) Pozycje koszyka
                foreach (var p in _produkty)
                {
                    p.QtyKg = 0;
                    p.E2 = false; p.Folia = false; p.Hallal = false; p.Strefa = false;
                    p.Cena = "";
                }
                foreach (var it in items)
                {
                    var p = _produkty.FirstOrDefault(x => x.Id == it.Id);
                    if (p == null) continue;
                    p.QtyKg = it.Ilosc;
                    if (!string.IsNullOrWhiteSpace(it.Cena)) p.Cena = it.Cena;
                    p.E2 = it.E2;
                    p.Folia = it.Folia;
                    p.Hallal = it.Hallal;
                    p.Strefa = it.Strefa;
                }
                foreach (var p in _produkty.Where(x => x.QtyKg > 0)) RecalcProductDisplay(p);

                // 7) Snapshot oryginalnego stanu — używane do diff w confirm overlay
                _originalSnapshot = new OrderSnapshot
                {
                    DataOdbioru = dataPrzyjazdu.Date,
                    Godzina = dataPrzyjazdu.TimeOfDay,
                    DataProdukcji = dataProdukcji ?? DateTime.Today,
                    WlasnyTransport = string.Equals(transportStatus, "Wlasny", StringComparison.OrdinalIgnoreCase),
                    Uwagi = uwagi ?? "",
                    Items = items.ToDictionary(
                        it => it.Id,
                        it => new ItemSnapshot
                        {
                            KodTowaru = it.Id,
                            Kod = _produkty.FirstOrDefault(x => x.Id == it.Id)?.Kod ?? $"Towar {it.Id}",
                            Ilosc = it.Ilosc,
                            Cena = it.Cena ?? "",
                            E2 = it.E2,
                            Folia = it.Folia,
                            Hallal = it.Hallal,
                            Strefa = it.Strefa
                        })
                };

                // 8) Re-render
                RenderDays();
                RenderHours();
                RenderProducts();
                RebuildCart();
                UpdateTermDisplay();
                UpdateValidation();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd wczytywania zamówienia: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadKontrahenciAsync()
        {
            const string sql = @"
                SELECT c.Id, c.Shortcut AS Nazwa, c.NIP, ISNULL(c.LimitAmount,0) AS LimitAmount,
                    poa.Postcode AS KodPocztowy, poa.Street AS Miejscowosc,
                    wym.CDim_Handlowiec_Val AS Handlowiec
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa
                    ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
                ORDER BY c.Shortcut;";

            _kontrahenci.Clear();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                _kontrahenci.Add(new KontrahentVm
                {
                    Id = rd["Id"]?.ToString() ?? "",
                    Nazwa = rd["Nazwa"]?.ToString() ?? "",
                    NIP = rd["NIP"]?.ToString() ?? "",
                    LimitKredytowy = rd["LimitAmount"] == DBNull.Value ? 0m : Convert.ToDecimal(rd["LimitAmount"]),
                    KodPocztowy = rd["KodPocztowy"]?.ToString() ?? "",
                    Miejscowosc = rd["Miejscowosc"]?.ToString() ?? "",
                    Handlowiec = rd["Handlowiec"]?.ToString() ?? ""
                });
            }
        }

        private async Task LoadOstatnieZamowieniaAsync()
        {
            _ostatnieZamowieniaKlienta.Clear();
            // Tylko zamówienia bieżącego użytkownika (jego "ostatni klienci")
            const string sql = @"
                SELECT KlientId, MAX(DataPrzyjazdu) AS Last
                FROM dbo.ZamowieniaMieso
                WHERE IdUser = @uid
                GROUP BY KlientId";
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@uid", UserID ?? "");
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string kid = rd["KlientId"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(kid) && rd["Last"] != DBNull.Value)
                    {
                        _ostatnieZamowieniaKlienta[kid] = Convert.ToDateTime(rd["Last"]);
                    }
                }
            }
            catch { }

            // LimitKredytowy zaciągnięty już w LoadKontrahenciAsync (jeden roundtrip mniej)
            foreach (var k in _kontrahenci)
            {
                if (_ostatnieZamowieniaKlienta.TryGetValue(k.Id, out var dt))
                    k.OstatnieZamowienie = dt;
            }
        }

        private async Task LoadObciazeniaDniAsync()
        {
            _obciazenieDni.Clear();
            try
            {
                const string sql = @"
                    SELECT CAST(DataPrzyjazdu AS DATE) AS D, ISNULL(SUM(LiczbaPalet),0) AS Pal
                    FROM dbo.ZamowieniaMieso
                    WHERE DataPrzyjazdu >= CAST(GETDATE() AS DATE)
                      AND DataPrzyjazdu < DATEADD(DAY, 14, CAST(GETDATE() AS DATE))
                    GROUP BY CAST(DataPrzyjazdu AS DATE)";
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    DateTime d = Convert.ToDateTime(rd["D"]);
                    decimal pal = Convert.ToDecimal(rd["Pal"]);
                    _obciazenieDni[d.Date] = pal;
                }
            }
            catch { }
        }

        private async Task LoadProductsAsync()
        {
            _produkty.Clear();
            var excluded = new HashSet<string> { "KURCZAK B", "FILET C" };
            var priorityOrder = new Dictionary<string, int>
            {
                { "KURCZAK A", 1 }, { "FILET A", 2 }, { "ĆWIARTKA", 3 }, { "SKRZYDŁO I", 4 },
                { "NOGA", 5 }, { "PAŁKA", 6 }, { "KORPUS", 7 }, { "POLĘDWICZKI", 8 },
                { "SERCE", 9 }, { "WĄTROBA", 10 }, { "ŻOŁĄDKI", 11 }, { "ĆWIARTKA II", 12 },
                { "FILET II", 13 }, { "FILET II PP", 14 }, { "SKRZYDŁO II", 15 }
            };

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();

            // Jeden roundtrip dla obu katalogów — grupowane potem w .NET
            var perKatalog = new Dictionary<string, List<(int Id, string Kod, int Pri)>>
            {
                { "67095", new List<(int, string, int)>() },
                { "67153", new List<(int, string, int)>() }
            };
            await using (var cmd = new SqlCommand(
                "SELECT Id, Kod, CAST(katalog AS NVARCHAR(32)) AS Katalog FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095','67153') ORDER BY Kod ASC", cn))
            await using (var rd = await cmd.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    var kod = rd.GetString(1);
                    if (excluded.Any(x => kod.ToUpper().Contains(x))) continue;
                    var katalog = rd["Katalog"]?.ToString() ?? "";
                    if (!perKatalog.ContainsKey(katalog)) continue;
                    int pri = int.MaxValue;
                    foreach (var kvp in priorityOrder)
                        if (kod.ToUpper().Contains(kvp.Key)) { pri = kvp.Value; break; }
                    perKatalog[katalog].Add((rd.GetInt32(0), kod, pri));
                }
            }

            foreach (var katalog in new[] { "67095", "67153" })
            {
                foreach (var t in perKatalog[katalog].OrderBy(x => x.Pri).ThenBy(x => x.Kod))
                {
                    var kat = DetectKategoria(t.Kod);
                    _produkty.Add(new ProductVm
                    {
                        Id = t.Id,
                        Kod = t.Kod,
                        Katalog = katalog,
                        KategoriaDisplay = kat,
                        PlaceholderEmoji = EmojiForKategoria(kat)
                    });
                }
            }
        }

        // Process-wide cache zdjęć — dzielony między wszystkie instancje okna.
        // Pierwsze otwarcie: pobiera z DB. Kolejne otwarcia: instant (z RAM).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, ImageSource> s_productImageCache = new();
        private static int s_productImageTableMissing; // 0 = nieznane, 1 = brak tabeli (nie próbuj ponownie)

        private async Task LoadProductImagesAsync()
        {
            try
            {
                // Krok 1: natychmiast zastosuj wszystko co już mamy w cache (instant UI).
                var needIds = new List<int>(_produkty.Count);
                foreach (var p in _produkty)
                {
                    if (s_productImageCache.TryGetValue(p.Id, out var cached))
                    {
                        p.ImageSource = cached;
                        p.HasImageVisibility = Visibility.Visible;
                        p.PlaceholderVisibility = Visibility.Collapsed;
                    }
                    else
                    {
                        needIds.Add(p.Id);
                    }
                }
                if (needIds.Count == 0) return;
                if (System.Threading.Volatile.Read(ref s_productImageTableMissing) == 1) return;

                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                if (System.Threading.Volatile.Read(ref s_productImageTableMissing) == 0)
                {
                    await using var c = new SqlCommand(
                        "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END", cn);
                    if ((int)(await c.ExecuteScalarAsync())! == 0)
                    {
                        System.Threading.Volatile.Write(ref s_productImageTableMissing, 1);
                        return;
                    }
                }

                // Krok 2: pobierz tylko brakujące ID (zamiast całej tabeli setek/tysięcy wierszy).
                var paramNames = new string[needIds.Count];
                for (int i = 0; i < needIds.Count; i++) paramNames[i] = "@id" + i;
                string sql = $"SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1 AND TowarId IN ({string.Join(",", paramNames)})";

                await using var cmd = new SqlCommand(sql, cn);
                for (int i = 0; i < needIds.Count; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], needIds[i]);

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    if (rd.IsDBNull(1)) continue;
                    try
                    {
                        byte[] data = (byte[])rd[1];
                        BitmapImage bi;
                        using (var ms = new MemoryStream(data))
                        {
                            bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.StreamSource = ms;
                            bi.DecodePixelWidth = 160;   // karta produktu ~120px, 160 zapewnia ostrość przy zoomie/HiDPI
                            bi.EndInit();
                        }
                        bi.Freeze();
                        s_productImageCache[id] = bi;

                        // Streaming: aktualizuj UI per-obrazek (psychologicznie szybciej niż batch).
                        await Dispatcher.InvokeAsync(() =>
                        {
                            var p = _produkty.FirstOrDefault(x => x.Id == id);
                            if (p != null)
                            {
                                p.ImageSource = bi;
                                p.HasImageVisibility = Visibility.Visible;
                                p.PlaceholderVisibility = Visibility.Collapsed;
                            }
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ════════════════════ STEPPER ════════════════════

        // 2-stop logical flow: krok 1 = klient + termin (scalone), krok 3 = pozycje (BtnStep2 ukryty)
        private void BtnStep1_Click(object sender, RoutedEventArgs e) => GoToStep(1);
        private void BtnStep2_Click(object sender, RoutedEventArgs e) => GoToStep(1); // legacy — przekierowanie
        private void BtnStep3_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranyKlient == null) { ShowToast("Najpierw wybierz klienta", false); return; }
            if (!IsTermValid()) { ShowToast("Najpierw wybierz datę odbioru", false); return; }
            GoToStep(3);
        }

        private bool IsTermValid()
            => _wybranaData.Date >= DateTime.Today && _wybranaGodzina.Hours > 0;

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1)
            {
                if (_wybranyKlient == null) { ShowToast("Wybierz klienta", false); return; }
                if (!IsTermValid()) { ShowToast("Wybierz datę odbioru i godzinę", false); return; }
                GoToStep(3);
            }
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 3) GoToStep(1);
        }

        private void GoToStep(int step)
        {
            // krok 2 to logiczny placeholder — kierujemy ruch na 1
            if (step == 2) step = 1;

            _currentStep = step;
            // W nowym layoucie wszystko widoczne jednocześnie — krok jest tylko logiczny
            if (step == 3) RenderProducts();
        }

        private void UpdateStepCircle(Border circle, TextBlock num, bool completed, bool active, string defaultLabel)
        {
            if (active)
            {
                var lg = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#6BA044")!,
                    (Color)ColorConverter.ConvertFromString("#46682C")!,
                    new Point(0, 0), new Point(0, 1));
                circle.Background = lg;
                circle.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Color = (Color)ColorConverter.ConvertFromString("#46682C")!,
                    Opacity = 0.40
                };
                num.Foreground = Brushes.White;
                num.Text = defaultLabel;
                num.FontSize = 15;
            }
            else if (completed)
            {
                circle.Background = (Brush)FindResource("BrandGreen");
                circle.Effect = null;
                num.Foreground = Brushes.White;
                num.Text = "✓";
                num.FontSize = 16;
            }
            else
            {
                circle.Background = (Brush)new BrushConverter().ConvertFrom("#E5E7EB")!;
                circle.Effect = null;
                num.Foreground = (Brush)FindResource("TextSecondary");
                num.Text = defaultLabel;
                num.FontSize = 15;
            }
        }

        // ════════════════════ KROK 1: KLIENT ════════════════════

        private async Task LoadUserHandlowcyAsync()
        {
            _userHandlowcy.Clear();
            _handlowiecMapowanie.Clear();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                // Mapowanie wszystkich (HandlowiecName → UserID)
                var cmdMap = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn);
                using (var rdM = await cmdMap.ExecuteReaderAsync())
                {
                    while (await rdM.ReadAsync())
                    {
                        var h = rdM["HandlowiecName"]?.ToString();
                        var uid = rdM["UserID"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(h) && !string.IsNullOrWhiteSpace(uid))
                            _handlowiecMapowanie[h!] = uid!;
                        if (!string.IsNullOrWhiteSpace(h) && string.Equals(uid, UserID, StringComparison.OrdinalIgnoreCase))
                            _userHandlowcy.Add(h!);
                    }
                }
            }
            catch { }
        }

        // Ładuje rzeczywisty avatar handlowca z UserAvatarManager (lub generuje domyślny)
        private void EnsureHandlowiecAvatarCached(string handlowiec, int size = 64)
        {
            if (string.IsNullOrEmpty(handlowiec)) return;
            if (s_handlowiecAvatarCache.ContainsKey(handlowiec)) return;

            System.Windows.Media.Imaging.BitmapSource? bmp = null;
            if (_handlowiecMapowanie.TryGetValue(handlowiec, out var uid))
            {
                try
                {
                    if (UserAvatarManager.HasAvatar(uid))
                        using (var av = UserAvatarManager.GetAvatarRounded(uid, size))
                            if (av != null) bmp = ConvertToBitmapSource(av);
                    if (bmp == null)
                        using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid, size))
                            bmp = ConvertToBitmapSource(defAv);
                }
                catch { }
            }
            if (bmp == null)
            {
                try
                {
                    using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, handlowiec, size))
                        bmp = ConvertToBitmapSource(defAv);
                }
                catch { }
            }
            if (bmp != null)
            {
                bmp.Freeze();
                s_handlowiecAvatarCache[handlowiec] = bmp;
            }
        }

        private static System.Windows.Media.Imaging.BitmapSource? ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using var bitmap = new System.Drawing.Bitmap(image);
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                NoweZamowienieTestWindow_DeleteObject(hBitmap);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool NoweZamowienieTestWindow_DeleteObject(IntPtr hObject);

        // Ustawia avatar handlowca na Border (jeśli image dostępny → ImageBrush, inaczej gradient + initials)
        private void ApplyHandlowiecAvatar(Border avatarBorder, TextBlock? initialsBlock, string handlowiec)
        {
            if (string.IsNullOrWhiteSpace(handlowiec))
            {
                avatarBorder.Visibility = Visibility.Collapsed;
                return;
            }
            avatarBorder.Visibility = Visibility.Visible;
            EnsureHandlowiecAvatarCached(handlowiec);
            if (s_handlowiecAvatarCache.TryGetValue(handlowiec, out var bmp))
            {
                avatarBorder.Background = new System.Windows.Media.ImageBrush(bmp) { Stretch = System.Windows.Media.Stretch.UniformToFill };
                if (initialsBlock != null) initialsBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                avatarBorder.Background = MakeAvatarGradient(handlowiec);
                if (initialsBlock != null)
                {
                    initialsBlock.Text = GetInitials(handlowiec);
                    initialsBlock.Visibility = Visibility.Visible;
                }
            }
        }

        private void RenderCustomers()
        {
            string filter = (TxtCustSearch.Text ?? "").Trim().ToLowerInvariant();

            IEnumerable<KontrahentVm> q = _kontrahenci;

            if (!string.IsNullOrEmpty(filter))
            {
                q = q.Where(k =>
                    k.Nazwa.ToLowerInvariant().Contains(filter) ||
                    k.NIP.ToLowerInvariant().Contains(filter) ||
                    k.Miejscowosc.ToLowerInvariant().Contains(filter));
            }
            else
            {
                // Bez wyszukiwania: moi klienci najpierw, potem reszta — w obu posortowane od najświeższej historii
                q = q.OrderByDescending(k => _userHandlowcy.Count > 0 && _userHandlowcy.Contains(k.Handlowiec))
                     .ThenByDescending(k => k.OstatnieZamowienie ?? DateTime.MinValue)
                     .ThenBy(k => k.Nazwa);
            }

            var list = q.Take(200).ToList();
            list.ForEach(RecalcCustomerBadge);

            ListCustomers.ItemsSource = list;

            LblCustListTitle.Text = string.IsNullOrEmpty(filter) ? "Klienci" : "Wyniki wyszukiwania";
            LblCustListCount.Text = $"{list.Count} {(list.Count == 1 ? "wynik" : list.Count > 1 && list.Count < 5 ? "wyniki" : "wyników")}";
            TxtCustPlaceholder.Visibility = string.IsNullOrEmpty(TxtCustSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Paleta gradientów dla avatarów — wybierana po hashu nazwy
        private static readonly (string from, string to)[] _avatarPalette = new[]
        {
            ("#6BA044", "#46682C"), // green
            ("#3B82F6", "#1E40AF"), // blue
            ("#8B5CF6", "#5B21B6"), // violet
            ("#EC4899", "#9D174D"), // pink
            ("#F59E0B", "#B45309"), // amber
            ("#10B981", "#065F46"), // emerald
            ("#06B6D4", "#0E7490"), // cyan
            ("#EF4444", "#991B1B"), // red
            ("#0EA5E9", "#075985"), // sky
            ("#A855F7", "#6B21A8"), // purple
        };

        private static Brush MakeAvatarGradient(string text)
        {
            int hash = 0;
            foreach (char c in text) hash = (hash * 31 + c) & 0x7FFFFFFF;
            var (from, to) = _avatarPalette[hash % _avatarPalette.Length];
            var lg = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            lg.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(from)!, 0));
            lg.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(to)!, 1));
            lg.Freeze();
            return lg;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ', '.', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0]).ToUpperInvariant();
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        }

        private void RecalcCustomerBadge(KontrahentVm k)
        {
            if (k.LimitKredytowy <= 0)
            {
                k.LimitBadge = "—";
                k.LimitBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")!);
                k.LimitBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")!);
            }
            else
            {
                k.LimitBadge = "OK";
                k.LimitBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")!);
                k.LimitBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534")!);
            }

            if (k.OstatnieZamowienie.HasValue)
            {
                int days = (DateTime.Today - k.OstatnieZamowienie.Value.Date).Days;
                k.LastOrderDisplay = days switch
                {
                    0 => "Ostatnio: dziś",
                    1 => "Ostatnio: wczoraj",
                    < 7 => $"Ostatnio: {days} dni temu",
                    < 30 => $"Ostatnio: {days / 7} tyg. temu",
                    _ => $"Ostatnio: {k.OstatnieZamowienie.Value:dd.MM.yyyy}"
                };
                k.LastOrderShort = days switch
                {
                    0 => "dziś",
                    1 => "wczoraj",
                    < 7 => $"{days}d temu",
                    < 30 => $"{days / 7}tyg temu",
                    _ => k.OstatnieZamowienie.Value.ToString("dd.MM")
                };
            }
            else
            {
                k.LastOrderDisplay = "Brak historii";
                k.LastOrderShort = "—";
            }

            k.NipDisplay = string.IsNullOrEmpty(k.NIP) ? "" : "NIP " + k.NIP;
            k.Initials = GetInitials(k.Nazwa);
            k.AvatarBrush = MakeAvatarGradient(k.Nazwa);
            k.HandlowiecShort = string.IsNullOrEmpty(k.Handlowiec) ? "—"
                : (k.Handlowiec.Length > 10 ? k.Handlowiec.Substring(0, 10) + "…" : k.Handlowiec);
        }

        private void TxtCustSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Pokaż listę gdy user wpisuje (była ukryta po wyborze)
            if (FindName("ClientListContainer") is Border clc && !string.IsNullOrEmpty(TxtCustSearch.Text))
                clc.Visibility = Visibility.Visible;
            RenderCustomers();
        }
        private async void BtnCustomerCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string id)
            {
                var k = _kontrahenci.FirstOrDefault(x => x.Id == id);
                if (k != null)
                {
                    _wybranyKlient = k;
                    await ApplySelectedCustomerAsync();
                    // Schowaj listę klientów po wyborze
                    if (FindName("ClientListContainer") is Border clc) clc.Visibility = Visibility.Collapsed;
                    // Wyczyść search
                    TxtCustSearch.Text = "";
                }
            }
        }

        private async Task ApplySelectedCustomerAsync()
        {
            if (_wybranyKlient == null) return;

            CustomerStrip.Visibility = Visibility.Visible;
            // krótka animacja slide-in
            var slideIn = new DoubleAnimation(-30, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            CustomerStrip.RenderTransform = new TranslateTransform();
            CustomerStrip.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            CustomerStrip.Opacity = 0;
            CustomerStrip.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)));

            CustName.Text = _wybranyKlient.Nazwa;
            string nip = _wybranyKlient.NIP ?? "";
            CustNip.Text = string.IsNullOrEmpty(nip) ? "" : "NIP " + nip;
            CustNipSep.Visibility = string.IsNullOrEmpty(nip) ? Visibility.Collapsed : Visibility.Visible;
            CustAddress.Text = $"{_wybranyKlient.KodPocztowy} {_wybranyKlient.Miejscowosc}".Trim();
            CustHandlowiec.Text = string.IsNullOrEmpty(_wybranyKlient.Handlowiec) ? "—" : _wybranyKlient.Handlowiec;
            CustInitials.Text = GetInitials(_wybranyKlient.Nazwa);
            CustAvatarBorder.Background = MakeAvatarGradient(_wybranyKlient.Nazwa);

            // Header chip
            HeaderChip.Visibility = Visibility.Visible;
            ChipName.Text = _wybranyKlient.Nazwa;
            ChipInitials.Text = GetInitials(_wybranyKlient.Nazwa);
            ChipAvatar.Background = MakeAvatarGradient(_wybranyKlient.Nazwa);

            // Avatar handlowca (z UserAvatarManager — prawdziwe avatary)
            try
            {
                if (FindName("ChipHandlowiecRow") is StackPanel hrow &&
                    FindName("ChipHandlowiecAvatar") is Border hav &&
                    FindName("ChipHandlowiecInitials") is TextBlock hin &&
                    FindName("ChipHandlowiecName") is TextBlock hname)
                {
                    string h = _wybranyKlient.Handlowiec ?? "";
                    if (!string.IsNullOrWhiteSpace(h))
                    {
                        hrow.Visibility = Visibility.Visible;
                        ApplyHandlowiecAvatar(hav, hin, h);
                        hname.Text = h;
                    }
                    else
                    {
                        hrow.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch { }

            // Pokaż chip terminu, przycisk Powtórz i panel termin info w sidebar
            BtnTerminChip.Visibility = Visibility.Visible;
            BtnRepeatLast.Visibility = Visibility.Visible;
            SidebarTerminInfo.Visibility = Visibility.Visible;

            Step1Sub.Text = _wybranyKlient.Nazwa.Length > 20 ? _wybranyKlient.Nazwa.Substring(0, 20) + "…" : _wybranyKlient.Nazwa;

            // Aktywuj sekcję terminu (legacy proxy)
            TerminContainer.IsEnabled = true;
            TerminContainer.Opacity = 1.0;

            await LoadPlatnosciAsync();
            await LoadCustomerPreferencesAsync();
            await LoadCustomerTransportPreferenceAsync();
            await LoadFavoritesAsync();
            await LoadKoszykSuggestionsAsync();
            await LoadNoteSuggestionsAsync();
            // Po wczytaniu favorites — przebuduj listę produktów żeby najczęstsze klienta były na górze
            RenderProducts();
            UpdateValidation();
        }

        // ════════════════════ SUGESTIE NOTATEK (smart ranking) ════════════════════

        private Kalendarz1.Zamowienia.Services.NotatkiService? _notatkiSvc;
        private bool _notatkiSchemaReady;

        public class NoteSuggestionDisplayVm
        {
            public int? SzablonId { get; set; }
            public string Text { get; set; } = "";
            public string Display { get; set; } = "";
            public string Tooltip { get; set; } = "";
            public Brush ChipBrush { get; set; } = Brushes.WhiteSmoke;
            public Brush ChipBorderBrush { get; set; } = Brushes.LightGray;
        }

        private async Task EnsureNotatkiSchemaAsync()
        {
            if (_notatkiSchemaReady) return;
            _notatkiSvc ??= new Kalendarz1.Zamowienia.Services.NotatkiService(_connLibra);
            try
            {
                await _notatkiSvc.EnsureSchemaAsync();
                _notatkiSchemaReady = true;
            }
            catch { /* best-effort, sugestie po prostu nie będą działać */ }
        }

        private async Task LoadNoteSuggestionsAsync()
        {
            if (_wybranyKlient == null || !int.TryParse(_wybranyKlient.Id, out int kid))
            {
                NoteSuggestionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            await EnsureNotatkiSchemaAsync();
            if (_notatkiSvc == null || !_notatkiSchemaReady)
            {
                NoteSuggestionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var koszyk = _produkty.Where(p => p.QtyKg > 0).Select(p => p.Id).ToList();

            List<Kalendarz1.Zamowienia.Services.NotatkiService.SuggestionVm> list;
            try
            {
                list = await _notatkiSvc.GetSuggestionsAsync(kid, UserID, koszyk, maxResults: 18);
            }
            catch
            {
                NoteSuggestionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var display = list.Select(s => new NoteSuggestionDisplayVm
            {
                SzablonId = s.SzablonId,
                Text = s.Text,
                Display = s.Display,
                Tooltip = s.Tooltip,
                ChipBrush = ParseBrush(s.ChipColor, "#F0F4F8"),
                ChipBorderBrush = ParseBrush(s.ChipBorder, "#CBD5E0")
            }).ToList();

            ListNoteSuggestions.ItemsSource = display;
            NoteSuggestionsPanel.Visibility = display.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            LblNoteSuggestionsHeader.Text = display.Count > 0
                ? $"💡 Propozycje · {display.Count} (klik = wstaw)"
                : "💡 Propozycje";
        }

        private static Brush ParseBrush(string hex, string fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) hex = fallback;
                var c = (Color)ColorConverter.ConvertFromString(hex)!;
                var b = new SolidColorBrush(c);
                b.Freeze();
                return b;
            }
            catch
            {
                return Brushes.WhiteSmoke;
            }
        }

        private async void BtnNoteSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.DataContext is not NoteSuggestionDisplayVm vm) return;
            string note = vm.Text ?? "";
            if (string.IsNullOrEmpty(note)) return;

            string current = (TxtUwagi.Text ?? "").Trim();
            if (string.IsNullOrEmpty(current))
                TxtUwagi.Text = note;
            else
                TxtUwagi.Text = current + " / " + note;
            TxtUwagi.CaretIndex = TxtUwagi.Text.Length;
            TxtUwagi.Focus();

            // Tracking — system uczy się że ten szablon zadziałał
            if (_notatkiSvc != null && _wybranyKlient != null && int.TryParse(_wybranyKlient.Id, out int kid))
            {
                var towary = _produkty.Where(p => p.QtyKg > 0).Select(p => p.Id);
                await _notatkiSvc.LogUsageAsync(note, kid, UserID,
                    Kalendarz1.Zamowienia.Services.NotatkiService.AkcjaWstawiona,
                    towary, vm.SzablonId);
            }
        }

        private async void BtnSaveNoteTemplate_Click(object sender, RoutedEventArgs e)
        {
            string current = (TxtUwagi.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(current))
            {
                MessageBox.Show(this, "Najpierw wpisz notatkę w polu, którą chcesz zapisać jako szablon.",
                    "Pusta notatka", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int? klientId = (_wybranyKlient != null && int.TryParse(_wybranyKlient.Id, out int kid)) ? kid : (int?)null;
            string klientNazwa = _wybranyKlient?.Nazwa ?? "";

            var dlg = new ZapiszSzablonNotatkiDialog(current, klientId, klientNazwa, UserID) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            await EnsureNotatkiSchemaAsync();
            if (_notatkiSvc == null) return;

            try
            {
                await _notatkiSvc.SaveTemplateAsync(
                    dlg.Tekst, dlg.Kategoria, dlg.Zakres,
                    dlg.Zakres == Kalendarz1.Zamowienia.Services.NotatkiService.ZakresPerKlient ? klientId : (int?)null,
                    dlg.Zakres == Kalendarz1.Zamowienia.Services.NotatkiService.ZakresPerHandlowiec ? UserID : null,
                    dlg.Pinowane, UserID);
                ShowToast("✓ Szablon zapisany", true);
                await LoadNoteSuggestionsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nie udało się zapisać szablonu:\n" + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnManageNoteTemplates_Click(object sender, RoutedEventArgs e)
        {
            await EnsureNotatkiSchemaAsync();
            if (_notatkiSvc == null) return;
            var win = new ZarzadzanieSzablonamiNotatekWindow(_connLibra, UserID) { Owner = this };
            win.ShowDialog();
            await LoadNoteSuggestionsAsync();   // odśwież po edycjach
        }

        // ════════════════════ SMART-FILL KOSZYKA (chipy z historii klienta) ════════════════════

        private Kalendarz1.Zamowienia.Services.SugestieKoszykaService? _sugestieKoszykaSvc;
        private bool _sugestieKoszykaSchemaReady;
        private List<Kalendarz1.Zamowienia.Services.SugestieKoszykaService.SugestiaProduktu> _ostatnieSugestieKoszyka = new();

        public class KoszykSuggestionDisplayVm
        {
            public int KodTowaru { get; set; }
            public string Display { get; set; } = "";
            public string Tooltip { get; set; } = "";
        }

        private async Task EnsureSugestieKoszykaSchemaAsync()
        {
            if (_sugestieKoszykaSchemaReady) return;
            _sugestieKoszykaSvc ??= new Kalendarz1.Zamowienia.Services.SugestieKoszykaService(_connLibra);
            try
            {
                await _sugestieKoszykaSvc.EnsureSchemaAsync();
                _sugestieKoszykaSchemaReady = true;
            }
            catch { /* best-effort, sugestie po prostu mogą działać read-only bez tabeli KoszykiUzycia */ }
        }

        private async Task LoadKoszykSuggestionsAsync()
        {
            if (_wybranyKlient == null || !int.TryParse(_wybranyKlient.Id, out int kid))
            {
                KoszykSuggestionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            await EnsureSugestieKoszykaSchemaAsync();
            _sugestieKoszykaSvc ??= new Kalendarz1.Zamowienia.Services.SugestieKoszykaService(_connLibra);

            List<Kalendarz1.Zamowienia.Services.SugestieKoszykaService.SugestiaProduktu> list;
            try
            {
                list = await _sugestieKoszykaSvc.GetSuggestionsAsync(kid, days: 90, max: 12);
            }
            catch
            {
                KoszykSuggestionsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            _ostatnieSugestieKoszyka = list;

            // Reset flag dla produktów bez wpisanej ilości (zapobiega zostawieniu flag z poprzedniego klienta).
            // Ręcznie wpisane (QtyKg > 0) zachowujemy nietknięte.
            foreach (var pp in _produkty.Where(x => x.QtyKg == 0))
            {
                pp.E2 = false; pp.Folia = false; pp.Hallal = false; pp.Strefa = false;
            }

            // Pre-fill flag z DOMINANT (≥70%) na ProductVm — działa dla Favorites (TOP 10) i głównej listy
            // bo obie pokazują te same instancje ProductVm.
            foreach (var sug in list)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == sug.KodTowaru);
                if (p == null || p.QtyKg > 0) continue;   // nie nadpisuj jeśli user wpisał ilość
                p.E2 = sug.E2Dominant;
                p.Folia = sug.FoliaDominant;
                p.Hallal = sug.HallalDominant;
                p.Strefa = sug.StrefaDominant;
            }

            var display = list.Select(s =>
            {
                var p = _produkty.FirstOrDefault(x => x.Id == s.KodTowaru);
                string nazwa = p?.Kod ?? $"Towar {s.KodTowaru}";
                string skrot = nazwa.Length > 24 ? nazwa.Substring(0, 23) + "…" : nazwa;

                var flagiDom = new List<string>();
                if (s.E2Dominant) flagiDom.Add("E2");
                if (s.FoliaDominant) flagiDom.Add("Folia");
                if (s.HallalDominant) flagiDom.Add("Halal");
                if (s.StrefaDominant) flagiDom.Add("Strefa");

                string statusStr = flagiDom.Count > 0 ? " · " + string.Join(" ", flagiDom) : "";
                string disp = $"📦 {skrot} · {s.SugerowanaIlosc:N0}kg{statusStr}";

                string flagiPct = $"E2 {s.E2Pct:P0} · Folia {s.FoliaPct:P0} · Halal {s.HallalPct:P0} · Strefa {s.StrefaPct:P0}";
                string tip = $"{nazwa}\n• {s.Czestotliwosc} zamówień (90 dni)"
                           + (s.CzestotliwoscOstatnich30 > 0 ? $" · {s.CzestotliwoscOstatnich30} w 30 dniach" : "")
                           + $"\n• Ostatnio: {s.OstatnioUzyte:dd.MM.yyyy}"
                           + $"\n• Ostatnia ilość: {s.SugerowanaIlosc:N0} kg"
                           + (string.IsNullOrEmpty(s.SugerowanaCena) ? "" : $"\n• Ostatnia cena: {s.SugerowanaCena}")
                           + $"\n• {flagiPct}"
                           + (flagiDom.Count > 0 ? $"\n• Domyślnie (≥70%): {string.Join(" + ", flagiDom)}" : "\n• Brak dominanty flag (<70%)")
                           + "\n\nKlik = pre-fill ilości/ceny/flag";

                return new KoszykSuggestionDisplayVm
                {
                    KodTowaru = s.KodTowaru,
                    Display = disp,
                    Tooltip = tip
                };
            }).ToList();

            ListKoszykSuggestions.ItemsSource = display;
            KoszykSuggestionsPanel.Visibility = display.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            LblKoszykSuggestionsHeader.Text = display.Count > 0
                ? $"Smart-fill · {display.Count} z historii klienta (klik = wstaw)"
                : "Smart-fill (z historii klienta)";
        }

        private void BtnKoszykSuggestion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (btn.Tag is not int kodTowaru) return;

            var sug = _ostatnieSugestieKoszyka.FirstOrDefault(s => s.KodTowaru == kodTowaru);
            if (sug == null) return;

            var p = _produkty.FirstOrDefault(x => x.Id == kodTowaru);
            if (p == null) { ShowToast("Towar niedostępny w aktualnym katalogu", false); return; }

            if (p.QtyKg > 0)
            {
                ShowToast($"Już w koszyku: {p.Kod} · {p.QtyKg:N0} kg", false);
                return;
            }

            p.QtyKg = sug.SugerowanaIlosc;
            if (string.IsNullOrWhiteSpace(p.Cena) && !string.IsNullOrWhiteSpace(sug.SugerowanaCena))
                p.Cena = sug.SugerowanaCena;
            p.E2 = sug.E2Dominant;
            p.Folia = sug.FoliaDominant;
            p.Hallal = sug.HallalDominant;
            p.Strefa = sug.StrefaDominant;
            RecalcProductDisplay(p);
            RenderProducts();
            RebuildCart();
            ShowToast($"✓ Dodano: {p.Kod} · {sug.SugerowanaIlosc:N0} kg", true);

            // Tracking — system uczy się że ten chip zadziałał
            if (_sugestieKoszykaSvc != null && _wybranyKlient != null && int.TryParse(_wybranyKlient.Id, out int kid))
            {
                _ = _sugestieKoszykaSvc.LogUsageAsync(kid, UserID, kodTowaru, sug.SugerowanaIlosc,
                    Kalendarz1.Zamowienia.Services.SugestieKoszykaService.AkcjaWstawiona);
            }
        }

        private void BtnFillTypowe_Click(object sender, RoutedEventArgs e)
        {
            if (_ostatnieSugestieKoszyka.Count == 0)
            {
                ShowToast("Brak historii dla tego klienta", false);
                return;
            }

            var topN = _ostatnieSugestieKoszyka.Take(5).ToList();
            int dodano = 0;
            foreach (var sug in topN)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == sug.KodTowaru);
                if (p == null || p.QtyKg > 0) continue;

                p.QtyKg = sug.SugerowanaIlosc;
                if (string.IsNullOrWhiteSpace(p.Cena) && !string.IsNullOrWhiteSpace(sug.SugerowanaCena))
                    p.Cena = sug.SugerowanaCena;
                p.E2 = sug.E2Dominant;
                p.Folia = sug.FoliaDominant;
                p.Hallal = sug.HallalDominant;
                p.Strefa = sug.StrefaDominant;
                RecalcProductDisplay(p);
                dodano++;

                if (_sugestieKoszykaSvc != null && _wybranyKlient != null && int.TryParse(_wybranyKlient.Id, out int kid))
                {
                    _ = _sugestieKoszykaSvc.LogUsageAsync(kid, UserID, sug.KodTowaru, sug.SugerowanaIlosc,
                        Kalendarz1.Zamowienia.Services.SugestieKoszykaService.AkcjaWstawiona);
                }
            }

            if (dodano > 0)
            {
                RenderProducts();
                RebuildCart();
                ShowToast($"✓ Dodano {dodano} typowych pozycji", true);
            }
            else
            {
                ShowToast("Wszystkie typowe pozycje już w koszyku", false);
            }
        }

        private void HeaderChip_Click(object sender, MouseButtonEventArgs e) => BtnChangeCustomer_Click(sender, e);

        private async Task LoadCustomerPreferencesAsync()
        {
            if (_wybranyKlient == null) return;
            int kid;
            if (!int.TryParse(_wybranyKlient.Id, out kid)) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Top 6 najczęściej wybieranych godzin przyjazdu (pomijamy 0 = nieustawione)
                _customerHours.Clear();
                var cmdH = new SqlCommand(@"
                    SELECT TOP 6 DATEPART(HOUR, DataPrzyjazdu) AS H, COUNT(*) AS C
                    FROM dbo.ZamowieniaMieso
                    WHERE KlientId = @kid AND DataPrzyjazdu IS NOT NULL
                      AND DATEPART(HOUR, DataPrzyjazdu) > 0
                    GROUP BY DATEPART(HOUR, DataPrzyjazdu)
                    ORDER BY COUNT(*) DESC, DATEPART(HOUR, DataPrzyjazdu) ASC", cn);
                cmdH.Parameters.AddWithValue("@kid", kid);
                using (var rdH = await cmdH.ExecuteReaderAsync())
                {
                    while (await rdH.ReadAsync())
                    {
                        int h = Convert.ToInt32(rdH["H"]);
                        if (h > 0 && h < 24) _customerHours.Add(h);
                    }
                }
                if (_customerHours.Count > 0)
                    _wybranyKlient.PreferredHour = _customerHours[0];

                // Najczęstszy odstęp dni między datą produkcji a datą odbioru
                var cmdD = new SqlCommand(@"
                    SELECT TOP 1 DATEDIFF(DAY, DataProdukcji, CAST(DataPrzyjazdu AS DATE)) AS Diff
                    FROM dbo.ZamowieniaMieso
                    WHERE KlientId = @kid
                      AND DataProdukcji IS NOT NULL
                      AND DataPrzyjazdu IS NOT NULL
                      AND DATEDIFF(DAY, DataProdukcji, CAST(DataPrzyjazdu AS DATE)) BETWEEN 0 AND 7
                    GROUP BY DATEDIFF(DAY, DataProdukcji, CAST(DataPrzyjazdu AS DATE))
                    ORDER BY COUNT(*) DESC", cn);
                cmdD.Parameters.AddWithValue("@kid", kid);
                var dObj = await cmdD.ExecuteScalarAsync();
                if (dObj != null && dObj != DBNull.Value)
                    _wybranyKlient.PreferredDeliveryDiff = Convert.ToInt32(dObj);
            }
            catch { }

            // Zastosuj preferencje
            if (_wybranyKlient.PreferredHour is int hr && hr > 0)
            {
                _wybranaGodzina = new TimeSpan(hr, 0, 0);
                TxtCustomHour.Text = $"{hr:00}:00";
            }

            int diff = _wybranyKlient.PreferredDeliveryDiff ?? 1;
            if (diff < 0) diff = 1;
            var newDelivery = _dataProdukcji.AddDays(diff);
            while (newDelivery.DayOfWeek == DayOfWeek.Sunday) newDelivery = newDelivery.AddDays(1);
            _wybranaData = newDelivery;

            RenderDaysProd();
            RenderDays();
            RenderHours();
            UpdateTermDisplay();
        }

        private async Task LoadPlatnosciAsync()
        {
            if (_wybranyKlient == null) return;
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var cmd1 = new SqlCommand("SELECT ISNULL(LimitAmount,0) FROM [HANDEL].[SSCommon].[STContractors] WHERE id=@id", cn);
                cmd1.Parameters.AddWithValue("@id", int.Parse(_wybranyKlient.Id));
                var limit = Convert.ToDecimal(await cmd1.ExecuteScalarAsync() ?? 0);

                var cmd2 = new SqlCommand(@"
                    WITH PNAgg AS (SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl,0)) AS KR FROM [HANDEL].[HM].[PN] PN GROUP BY PN.dkid)
                    SELECT ISNULL(SUM(DK.walbrutto - ISNULL(PA.KR, 0)), 0)
                    FROM [HANDEL].[HM].[DK] DK
                    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
                    WHERE DK.khid = @id AND DK.anulowany = 0 AND (DK.walbrutto - ISNULL(PA.KR, 0)) > 0", cn);
                cmd2.Parameters.AddWithValue("@id", int.Parse(_wybranyKlient.Id));
                var dluzny = Convert.ToDecimal(await cmd2.ExecuteScalarAsync() ?? 0);

                _wybranyKlient.LimitKredytowy = limit;
                _wybranyKlient.DoZaplacenia = dluzny;

                UpdateLimitDisplay();
            }
            catch
            {
                HdrLblLimitHint.Text = "Błąd odczytu limitu";
            }
        }

        private void BtnChangeCustomer_Click(object sender, RoutedEventArgs e)
        {
            _wybranyKlient = null;
            CustomerStrip.Visibility = Visibility.Collapsed;
            HeaderChip.Visibility = Visibility.Collapsed;
            Step1Sub.Text = "Wybierz odbiorcę";
            HdrLimitPctLabel.Text = "—";
            HdrProgressLimit.Width = 0;
            HdrLblLimitHint.Text = "Wybierz klienta";
            ChipPalety.Text = "0/33";
            ChipLimit.Text = "—";
            ChipTermin.Text = "—";
            BtnTerminChip.Visibility = Visibility.Collapsed;
            BtnRepeatLast.Visibility = Visibility.Collapsed;
            SidebarTerminInfo.Visibility = Visibility.Collapsed;
            _favoriteIds.Clear();
            _customerHours.Clear();
            FavoritesPanel.Visibility = Visibility.Collapsed;
            RenderHours();
            // Wyłącz sekcję terminu z powrotem (legacy proxy)
            TerminContainer.IsEnabled = false;
            TerminContainer.Opacity = 0.45;
            GoToStep(1);
            UpdateValidation();
        }

        // ════════════════════ KROK 2: TERMIN ════════════════════

        private void RenderDays()
        {
            var days = new List<DayVm>();
            DateTime start = DateTime.Today;
            int added = 0;
            int offset = 0;
            while (added < 7)
            {
                DateTime d = start.AddDays(offset++);
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;
                _obciazenieDni.TryGetValue(d, out decimal pal);
                bool isSelected = d.Date == _wybranaData.Date;
                double loadPct = Math.Min(1.0, (double)pal / LIMIT_PALET_TIR);
                days.Add(new DayVm
                {
                    Date = d,
                    DayName = _pl.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek).ToUpperInvariant(),
                    DayNum = d.Day.ToString(),
                    MonthShort = _pl.DateTimeFormat.GetAbbreviatedMonthName(d.Month).ToUpperInvariant(),
                    LoadDisplay = pal == 0 ? "wolne" : $"{pal:N0} pal",
                    IsSelected = isSelected,
                    BgBrush = isSelected ? (Brush)FindResource("BrandGreenLight") : Brushes.White,
                    BorderBrush = isSelected ? (Brush)FindResource("BrandGreen") : (Brush)FindResource("Border"),
                    ForeBrush = isSelected ? (Brush)FindResource("BrandGreenDark") : (Brush)FindResource("TextPrimary"),
                    LoadBrush = pal >= LIMIT_PALET_TIR ? (Brush)FindResource("Danger") : pal >= 20 ? (Brush)FindResource("Warning") : (Brush)FindResource("BrandGreen"),
                    LoadBarWidth = Math.Max(2, 50 * loadPct)
                });
                added++;
            }
            ListDays.ItemsSource = days;
            LblOdbiorSelected.Text = $"{_wybranaData:dd.MM.yyyy} ({_pl.DateTimeFormat.GetDayName(_wybranaData.DayOfWeek)})";
        }

        private void RenderDaysProd()
        {
            var days = new List<DayVm>();
            DateTime start = DateTime.Today;
            int added = 0;
            int offset = 0;
            while (added < 7)
            {
                DateTime d = start.AddDays(offset++);
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;
                bool isSelected = d.Date == _dataProdukcji.Date;
                days.Add(new DayVm
                {
                    Date = d,
                    DayName = _pl.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek).ToUpperInvariant(),
                    DayNum = d.Day.ToString(),
                    MonthShort = _pl.DateTimeFormat.GetAbbreviatedMonthName(d.Month).ToUpperInvariant(),
                    LoadDisplay = "",
                    IsSelected = isSelected,
                    BgBrush = isSelected ? (Brush)FindResource("BrandGreenLight") : Brushes.White,
                    BorderBrush = isSelected ? (Brush)FindResource("BrandGreen") : (Brush)FindResource("Border"),
                    ForeBrush = isSelected ? (Brush)FindResource("BrandGreenDark") : (Brush)FindResource("TextPrimary"),
                    LoadBrush = (Brush)FindResource("TextMuted"),
                    LoadBarWidth = 0
                });
                added++;
            }
            ListDaysProd.ItemsSource = days;
            LblProdSelected.Text = $"{_dataProdukcji:dd.MM.yyyy} ({_pl.DateTimeFormat.GetDayName(_dataProdukcji.DayOfWeek)})";
        }

        private void BtnDayProdCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is DateTime d)
            {
                _dataProdukcji = d;
                // Auto-dobranie daty odbioru bazując na preferencji klienta
                int diff = _wybranyKlient?.PreferredDeliveryDiff ?? 1;
                if (diff < 0) diff = 0;
                var newDelivery = _dataProdukcji.AddDays(diff);
                // pomiń niedzielę
                while (newDelivery.DayOfWeek == DayOfWeek.Sunday) newDelivery = newDelivery.AddDays(1);
                _wybranaData = newDelivery;
                RenderDaysProd();
                RenderDays();
                UpdateTermDisplay();
                UpdateValidation();
            }
        }

        private void BtnDayCard_Click(object sender, RoutedEventArgs e)
        {
            // klikanie daty odbioru NIE może modyfikować daty produkcji - to są niezależne pola
            if (sender is Button b && b.Tag is DateTime d)
            {
                _wybranaData = d;
                RenderDays();
                UpdateTermDisplay();
                UpdateValidation();
            }
        }

        private static readonly int[] _domyslneGodziny = { 6, 8, 10, 12, 14, 16 };

        private void RenderHours()
        {
            // Użyj godzin klienta (top N) jeśli są — inaczej fallback do 6/8/10/12/14/16
            var sourceHours = _customerHours.Count > 0
                ? _customerHours.OrderBy(h => h).ToList()
                : _domyslneGodziny.ToList();

            // Upewnij się że aktualnie wybrana godzina jest w liście (żeby chip pokazał selekcję)
            int selH = _wybranaGodzina.Hours;
            if (selH > 0 && !sourceHours.Contains(selH))
            {
                sourceHours.Add(selH);
                sourceHours = sourceHours.OrderBy(h => h).ToList();
            }

            var hours = new List<HourVm>();
            foreach (int h in sourceHours)
            {
                var time = new TimeSpan(h, 0, 0);
                bool selected = _wybranaGodzina == time;
                hours.Add(new HourVm
                {
                    Hour = time,
                    HourDisplay = $"{h:00}:00",
                    BgBrush = selected ? (Brush)FindResource("BrandGreen") : Brushes.White,
                    BorderBrush = selected ? (Brush)FindResource("BrandGreen") : (Brush)FindResource("Border"),
                    ForeBrush = selected ? Brushes.White : (Brush)FindResource("TextPrimary")
                });
            }
            ListHours.ItemsSource = hours;
        }

        private void BtnHourChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is TimeSpan t)
            {
                _wybranaGodzina = t;
                TxtCustomHour.Text = $"{t.Hours:00}:{t.Minutes:00}";
                RenderHours();
                UpdateTermDisplay();
                // Zamknij popup po wyborze
                if (FindName("HourPopup") is System.Windows.Controls.Primitives.Popup pop) pop.IsOpen = false;
            }
        }

        private void BtnHourHelp_Click(object sender, RoutedEventArgs e)
        {
            if (FindName("HourPopup") is System.Windows.Controls.Primitives.Popup pop)
                pop.IsOpen = !pop.IsOpen;
        }

        private void TxtCustomHour_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyCustomHour(showToast: true);
        }

        private void TxtCustomHour_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyCustomHour(showToast: false);
        }
        private void TxtCustomHour_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyCustomHour(showToast: false);
        }

        private void ApplyCustomHour(bool showToast)
        {
            if (TimeSpan.TryParse(TxtCustomHour.Text, out var t))
            {
                _wybranaGodzina = t;
                RenderHours();
                UpdateTermDisplay();
                if (showToast) ShowToast($"✓ Godzina ustawiona: {t.Hours:00}:{t.Minutes:00}", true);
            }
        }

        // ── DatePicker handlery (alternatywa dla kart dni) ──
        private void DpProd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpProd.SelectedDate is DateTime d && d.Date != _dataProdukcji.Date)
            {
                _dataProdukcji = d.Date;
                int diff = _wybranyKlient?.PreferredDeliveryDiff ?? 1;
                if (diff < 0) diff = 1;
                var newDelivery = _dataProdukcji.AddDays(diff);
                while (newDelivery.DayOfWeek == DayOfWeek.Sunday) newDelivery = newDelivery.AddDays(1);
                _wybranaData = newDelivery;
                RenderDaysProd();
                RenderDays();
                UpdateTermDisplay();
            }
        }

        private void DpOdbior_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpOdbior.SelectedDate is DateTime d && d.Date != _wybranaData.Date)
            {
                _wybranaData = d.Date;
                RenderDays();
                UpdateTermDisplay();
            }
        }

        private bool _suppressTransportSync;
        private void ChkWlasnyOdbior_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTransportLabels();
            if (_suppressTransportSync) return;
            _suppressTransportSync = true;
            ChkSidebarWlasny.IsChecked = ChkWlasnyOdbior.IsChecked;
            _suppressTransportSync = false;
        }

        private void ChkSidebarWlasny_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressTransportSync) return;
            _suppressTransportSync = true;
            ChkWlasnyOdbior.IsChecked = ChkSidebarWlasny.IsChecked;
            _suppressTransportSync = false;
            UpdateTransportLabels();
        }

        // Wspólny helper — etykiety zależne od tego czy klient własnym transportem.
        // Wlasny=true → produkty wydawane klientowi przy ubojni (Piórkowscy = ubojnia).
        // Wlasny=false → my dowozimy do klienta.
        private void UpdateTransportLabels()
        {
            bool wlasny = ChkWlasnyOdbior?.IsChecked == true;
            if (LblGodzinaHeader != null)
                LblGodzinaHeader.Text = wlasny ? "⏱ Godzina na ubojni" : "⏱ Godzina u klienta";
            if (LblOdbiorHeader != null)
                LblOdbiorHeader.Text = wlasny ? "🚚 Odbiór na ubojni" : "🚚 Odbiór u klienta";
        }

        private void UpdateTermDisplay()
        {
            string day = _pl.DateTimeFormat.GetDayName(_wybranaData.DayOfWeek);
            HdrTerminMain.Text = $"{_wybranaData:dd.MM.yyyy}";
            HdrTerminSub.Text = $"{day} · {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
            Step2Sub.Text = $"{_wybranaData:dd.MM} · {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
            ChipTermin.Text = $"{_wybranaData:dd.MM} · {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
            // Ikona transportu w chipie terminu
            try
            {
                if (FindName("ChipTransport") is TextBlock chipTr)
                    chipTr.Text = ChkWlasnyOdbior?.IsChecked == true ? "🚚" : "";
            }
            catch { }

            string prodDay = _pl.DateTimeFormat.GetDayName(_dataProdukcji.DayOfWeek);
            SbProdMain.Text = $"{_dataProdukcji:dd.MM.yyyy}";
            SbProdSub.Text = prodDay;
            // Dni tygodnia pod DatePickerami
            try
            {
                if (FindName("LblProdDay") is TextBlock lpd)
                    lpd.Text = $"📆 {prodDay}";
                if (FindName("LblOdbiorDay") is TextBlock lod)
                    lod.Text = $"📆 {day}";
            }
            catch { }
            // Sidebar transport
            try
            {
                if (FindName("SbTransport") is TextBlock sbT)
                    sbT.Text = ChkWlasnyOdbior?.IsChecked == true ? "🚚 Klient własnym transportem" : "Standard (firmowy)";
            }
            catch { }

            // Sync DatePickers (bez wywołania ich SelectionChanged dzięki check d != current w handlerach)
            if (DpProd.SelectedDate?.Date != _dataProdukcji.Date) DpProd.SelectedDate = _dataProdukcji;
            if (DpOdbior.SelectedDate?.Date != _wybranaData.Date) DpOdbior.SelectedDate = _wybranaData;
        }

        // ════════════════════ KROK 3: POZYCJE ════════════════════

        private void RenderProducts()
        {
            var visible = _produkty.Where(p => p.Katalog == _aktywnyKatalog).ToList();
            foreach (var p in visible)
            {
                RecalcProductDisplay(p);
            }

            // Najczęściej kupowane (zachowując kolejność popularności)
            List<ProductVm> favs = new();
            if (_favoriteIds.Count > 0)
            {
                favs = _favoriteIds
                    .Select(id => visible.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<ProductVm>()
                    .Take(10)
                    .ToList();
            }

            if (favs.Count > 0)
            {
                ListFavorites.ItemsSource = null;
                ListFavorites.ItemsSource = favs;
                LblFavoritesCount.Text = $"({favs.Count})";
                FavoritesPanel.Visibility = Visibility.Visible;
            }
            else
            {
                FavoritesPanel.Visibility = Visibility.Collapsed;
            }

            // Wszystkie produkty — z wykluczeniem tych już pokazanych w "Najczęściej kupowane"
            var favIds = new HashSet<int>(favs.Select(p => p.Id));
            var others = visible.Where(p => !favIds.Contains(p.Id)).ToList();
            ListProducts.ItemsSource = null;
            ListProducts.ItemsSource = others;
        }

        // Wczytaj domyślny transport klienta — jeśli >50% zamówień z ostatnich 6 miesięcy było "Wlasny",
        // automatycznie zaznacz checkbox. Wymaga min 2 zamówień (mniej = nie ufamy próbie).
        private async Task LoadCustomerTransportPreferenceAsync()
        {
            if (_wybranyKlient == null || !int.TryParse(_wybranyKlient.Id, out int kid)) return;
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"
                    SELECT
                        SUM(CASE WHEN TransportStatus = 'Wlasny' THEN 1 ELSE 0 END) AS WlasnyCnt,
                        COUNT(*) AS TotalCnt
                    FROM dbo.ZamowieniaMieso
                    WHERE KlientId = @kid
                      AND TransportStatus IS NOT NULL
                      AND DataPrzyjazdu > DATEADD(MONTH, -6, GETDATE())", cn);
                cmd.Parameters.AddWithValue("@kid", kid);
                int wlasnyCnt = 0, totalCnt = 0;
                await using (var rd = await cmd.ExecuteReaderAsync())
                {
                    if (await rd.ReadAsync())
                    {
                        wlasnyCnt = rd["WlasnyCnt"] == DBNull.Value ? 0 : Convert.ToInt32(rd["WlasnyCnt"]);
                        totalCnt = rd["TotalCnt"] == DBNull.Value ? 0 : Convert.ToInt32(rd["TotalCnt"]);
                    }
                }

                if (totalCnt < 2) return;   // za mała próba — nie zmieniaj domyślnego
                bool wlasnyDominant = (wlasnyCnt * 100 / totalCnt) > 50;

                _suppressTransportSync = true;
                ChkWlasnyOdbior.IsChecked = wlasnyDominant;
                ChkSidebarWlasny.IsChecked = wlasnyDominant;
                _suppressTransportSync = false;
                UpdateTransportLabels();
            }
            catch { }
        }

        private async Task LoadFavoritesAsync()
        {
            _favoriteIds.Clear();
            if (_wybranyKlient == null || !int.TryParse(_wybranyKlient.Id, out int kid)) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"
                    SELECT TOP 10 t.KodTowaru, COUNT(*) AS Cnt, SUM(t.Ilosc) AS Suma
                    FROM dbo.ZamowieniaMiesoTowar t
                    INNER JOIN dbo.ZamowieniaMieso z ON z.Id = t.ZamowienieId
                    WHERE z.KlientId = @kid AND t.KodTowaru IS NOT NULL
                    GROUP BY t.KodTowaru
                    ORDER BY COUNT(*) DESC, SUM(t.Ilosc) DESC", cn);
                cmd.Parameters.AddWithValue("@kid", kid);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    if (int.TryParse(rd["KodTowaru"]?.ToString(), out int id))
                        _favoriteIds.Add(id);
                }
            }
            catch { }
        }

        private static string EmojiForKategoria(string kat) => kat switch
        {
            "Tuszka" => "🐔",
            "Filet" => "🥩",
            "Ćwiartka" => "🍗",
            "Noga" => "🍗",
            "Skrzydło" => "🪽",
            "Korpus" => "🦴",
            "Polędwiczki" => "🥩",
            "Podroby" => "🫀",
            _ => "🍖"
        };

        private static string DetectKategoria(string kod)
        {
            string u = kod.ToUpperInvariant();
            if (u.Contains("KURCZAK")) return "Tuszka";
            if (u.Contains("FILET")) return "Filet";
            if (u.Contains("ĆWIARTKA") || u.Contains("CWIARTKA")) return "Ćwiartka";
            if (u.Contains("NOGA") || u.Contains("PAŁKA") || u.Contains("PALKA")) return "Noga";
            if (u.Contains("SKRZYDŁO") || u.Contains("SKRZYDLO")) return "Skrzydło";
            if (u.Contains("KORPUS")) return "Korpus";
            if (u.Contains("POLĘDWICZ") || u.Contains("POLEDWICZ")) return "Polędwiczki";
            if (u.Contains("SERCE") || u.Contains("WĄTROBA") || u.Contains("WATROBA") || u.Contains("ŻOŁĄDK") || u.Contains("ZOLADK")) return "Podroby";
            return "Inne";
        }

        private static readonly Brush _highlightBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C8A3A")!);
        private static readonly Brush _normalBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEDF1")!);

        // skip == "kg" / "poj" / "pal" → nie nadpisuj pola które użytkownik aktualnie edytuje
        private void RecalcProductDisplay(ProductVm p, string? skip = null)
        {
            decimal kgPoj = KgPerPoj(p);
            decimal pojNaPalecie = p.E2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
            p.Pojemniki = p.QtyKg > 0 ? Math.Ceiling(p.QtyKg / kgPoj) : 0;
            p.Palety = p.Pojemniki > 0 ? Math.Round(p.Pojemniki / pojNaPalecie, 2) : 0;

            if (skip != "kg")  p.QtyKgDisplay = p.QtyKg > 0 ? p.QtyKg.ToString("0", _pl) : "";
            if (skip != "poj") p.PojDisplay   = p.Pojemniki > 0 ? p.Pojemniki.ToString("0", _pl) : "";
            if (skip != "pal") p.PalDisplay   = p.Palety > 0 ? p.Palety.ToString("0.##", _pl) : "";
            p.CenaDisplay = p.Cena ?? "";

            // Wartość = cena × kg
            if (TryParseInput(p.Cena, out var cenaVal) && cenaVal > 0 && p.QtyKg > 0)
                p.WartoscDisplay = (cenaVal * p.QtyKg).ToString("N2", _pl) + " zł";
            else
                p.WartoscDisplay = "";

            bool inCart = p.QtyKg > 0;
            p.InCartVisibility = inCart ? Visibility.Visible : Visibility.Collapsed;
            p.InCartBadge = inCart ? "✓" : "";
            p.ProductBorder = inCart ? _highlightBorder : _normalBorder;
            p.ProductBorderThickness = inCart ? new Thickness(2) : new Thickness(1);

            p.NotifyAll(skip);
        }

        private void BtnTypProduktu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string kat)
            {
                _aktywnyKatalog = kat;
                if (kat == "67095")
                {
                    BtnSwieze.Background = (Brush)FindResource("BrandGreen");
                    BtnSwieze.Foreground = Brushes.White;
                    BtnMrozone.Background = Brushes.Transparent;
                    BtnMrozone.Foreground = (Brush)FindResource("TextSecondary");
                }
                else
                {
                    BtnMrozone.Background = (Brush)FindResource("BrandGreen");
                    BtnMrozone.Foreground = Brushes.White;
                    BtnSwieze.Background = Brushes.Transparent;
                    BtnSwieze.Foreground = (Brush)FindResource("TextSecondary");
                }
                RenderProducts();
            }
        }

        private void TxtProductSearch_TextChanged(object sender, TextChangedEventArgs e) => RenderProducts();

        private void TxtNumeric_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

        private static bool TryParseInput(string? text, out decimal value)
            => decimal.TryParse((text ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out value);

        private static void MoveFocusNext(object sender)
        {
            if (sender is UIElement ui)
                ui.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        // Ochrona pola edytowanego: zachowujemy dokładny tekst+kursor, by żaden refresh bindingu
        // nie nadpisał tego co użytkownik wpisuje (działa dla każdego ItemsControl, w tym favoritesów).
        private static void PreserveTypedText(TextBox tb, string userText, int caret)
        {
            if (tb.Text != userText)
            {
                tb.Text = userText;
            }
            int safeCaret = Math.Min(caret, tb.Text.Length);
            if (safeCaret >= 0 && tb.CaretIndex != safeCaret)
                tb.CaretIndex = safeCaret;
        }

        // Flaga blokująca rekurencyjne TextChanged przy programowej aktualizacji rodzeństwa pola
        private bool _internalTextUpdate;

        // Szukaj TextBoxa w całym drzewie wizualnym po Name (FieldKg/FieldPoj/FieldPal) + Tag (Id produktu)
        private static TextBox? FindTextBoxByName(DependencyObject root, int productId, string fieldName)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBox tb && tb.Name == fieldName && tb.Tag is int id && id == productId)
                    return tb;
                var deeper = FindTextBoxByName(child, productId, fieldName);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // Wymusza ustawienie Text na rodzeństwie (z flagą żeby nie wpaść w rekurencję)
        private void ForceSiblingText(TextBox source, string fieldName, string value)
        {
            if (source.Tag is not int productId) return;
            var sib = FindTextBoxByName(this, productId, fieldName);
            if (sib == null || sib == source) return;
            if (sib.Text == value) return;
            _internalTextUpdate = true;
            try { sib.Text = value; }
            finally { _internalTextUpdate = false; }
        }


        // ── KG ──
        private void TxtQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_internalTextUpdate) return;
            if (sender is not TextBox tb || tb.Tag is not int id) return;
            string userText = tb.Text ?? "";
            int caret = tb.CaretIndex;

            var p = _produkty.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            if (string.IsNullOrWhiteSpace(userText))
            {
                p.QtyKg = 0;
            }
            else if (TryParseInput(userText, out var val))
            {
                p.QtyKg = Math.Max(0, val);
            }
            else
            {
                // niepoprawny wpis — nie ruszamy modelu, ale chronimy tekst
                PreserveTypedText(tb, userText, caret);
                return;
            }

            RecalcProductDisplay(p, skip: "kg");
            RebuildCart();
            // Wymuszamy aktualizację POJ i PAL bezpośrednio (nie ufamy bindingowi)
            ForceSiblingText(tb, "FieldPoj", p.PojDisplay);
            ForceSiblingText(tb, "FieldPal", p.PalDisplay);
            PreserveTypedText(tb, userText, caret);
        }
        private void TxtQty_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) MoveFocusNext(sender);
        }
        private void TxtQty_LostFocus(object sender, RoutedEventArgs e) { }

        // ── POJEMNIKI ──
        private void TxtPoj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_internalTextUpdate) return;
            if (sender is not TextBox tb || tb.Tag is not int id) return;
            string userText = tb.Text ?? "";
            int caret = tb.CaretIndex;

            var p = _produkty.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            if (string.IsNullOrWhiteSpace(userText))
            {
                p.QtyKg = 0;
            }
            else if (TryParseInput(userText, out var poj))
            {
                poj = Math.Max(0, Math.Round(poj));
                p.QtyKg = poj * KgPerPoj(p);
            }
            else
            {
                PreserveTypedText(tb, userText, caret);
                return;
            }

            RecalcProductDisplay(p, skip: "poj");
            RebuildCart();
            // Wymuszamy aktualizację KG i PAL bezpośrednio
            ForceSiblingText(tb, "FieldKg", p.QtyKgDisplay);
            ForceSiblingText(tb, "FieldPal", p.PalDisplay);
            PreserveTypedText(tb, userText, caret);
        }
        private void TxtPoj_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) MoveFocusNext(sender);
        }
        private void TxtPoj_LostFocus(object sender, RoutedEventArgs e) { }

        // ── PALETY ──
        private void TxtPal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_internalTextUpdate) return;
            if (sender is not TextBox tb || tb.Tag is not int id) return;
            string userText = tb.Text ?? "";
            int caret = tb.CaretIndex;

            var p = _produkty.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            if (string.IsNullOrWhiteSpace(userText))
            {
                p.QtyKg = 0;
            }
            else if (TryParseInput(userText, out var pal))
            {
                pal = Math.Max(0, pal);
                decimal pojNaPalecie = p.E2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
                decimal poj = Math.Round(pal * pojNaPalecie);
                p.QtyKg = poj * KgPerPoj(p);
            }
            else
            {
                PreserveTypedText(tb, userText, caret);
                return;
            }

            RecalcProductDisplay(p, skip: "pal");
            RebuildCart();
            // Wymuszamy aktualizację KG i POJ bezpośrednio
            ForceSiblingText(tb, "FieldKg", p.QtyKgDisplay);
            ForceSiblingText(tb, "FieldPoj", p.PojDisplay);
            PreserveTypedText(tb, userText, caret);
        }
        private void TxtPal_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) MoveFocusNext(sender);
        }
        private void TxtPal_LostFocus(object sender, RoutedEventArgs e) { }

        private void TxtCena_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is int id)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == id);
                if (p != null) p.Cena = tb.Text;
            }
        }

        private void ChkOpcja_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is int id)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    RecalcProductDisplay(p);
                    RebuildCart();
                }
            }
        }

        private void BtnCartRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int id)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    p.QtyKg = 0;
                    RecalcProductDisplay(p);
                    RebuildCart();
                    if (_currentStep == 3) RenderProducts();
                }
            }
        }

        // ════════════════════ KOSZYK / SUMARYZACJA ════════════════════

        private DispatcherTimer? _suggestionsReloadTimer;
        private void ScheduleSuggestionsReload()
        {
            if (_suggestionsReloadTimer == null)
            {
                _suggestionsReloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
                _suggestionsReloadTimer.Tick += async (s, e) =>
                {
                    _suggestionsReloadTimer!.Stop();
                    if (NoteSuggestionsPanel?.Visibility == Visibility.Visible)
                        await LoadNoteSuggestionsAsync();
                };
            }
            _suggestionsReloadTimer.Stop();
            _suggestionsReloadTimer.Start();
        }

        private void RebuildCart()
        {
            var inCart = _produkty.Where(p => p.QtyKg > 0).ToList();

            // Re-rankuj sugestie notatek bo koszyk się zmienił (towar-aware boost)
            ScheduleSuggestionsReload();

            int pozycji = inCart.Count;
            decimal pojemniki = inCart.Sum(p => p.Pojemniki);
            decimal palety = inCart.Sum(p => p.Palety);
            decimal kg = inCart.Sum(p => p.QtyKg);

            TotPozycje.Text = pozycji.ToString();
            TotPojemniki.Text = pojemniki.ToString("0", _pl);
            TotKg.Text = kg.ToString("N0", _pl) + " kg";
            TotPalety.Text = palety.ToString("0.##", _pl);

            double pct = Math.Min(1.0, (double)palety / LIMIT_PALET_TIR);

            // Pasek palet w panelu klienta (sidebar)
            double headerBarMax = HdrProgressPalety.Parent is Border parentBar && parentBar.ActualWidth > 0
                ? parentBar.ActualWidth : 280;
            HdrTotPalety.Text = palety.ToString("0.##", _pl);
            HdrProgressPalety.Width = Math.Max(0, headerBarMax * pct);

            string paletyHint;
            if (palety <= LIMIT_PALET_SOLOWKA) paletyHint = $"Solówka {palety:0.##}/{LIMIT_PALET_SOLOWKA} ✓";
            else if (palety <= LIMIT_PALET_TIR) paletyHint = $"TIR {palety:0.##}/{LIMIT_PALET_TIR}";
            else paletyHint = $"⚠ Przekroczenie TIR ({palety:0.##}/{LIMIT_PALET_TIR})";
            HdrLblPaletyHint.Text = paletyHint;

            var paletyColor = palety > LIMIT_PALET_TIR ? (Brush)FindResource("Danger")
                : palety > LIMIT_PALET_SOLOWKA ? (Brush)FindResource("Warning")
                : (Brush)FindResource("BrandGreen");
            HdrProgressPalety.Background = paletyColor;

            // Header chip palety
            ChipPalety.Text = $"{palety:0.##}/{LIMIT_PALET_TIR}";

            // cart list
            var cartItems = inCart.Select(p =>
            {
                bool hasCena = decimal.TryParse((p.Cena ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var cenaVal) && cenaVal > 0;
                decimal wartosc = hasCena ? cenaVal * p.QtyKg : 0;
                return new CartItem
                {
                    Id = p.Id,
                    Kod = p.Kod,
                    Detail = $"{p.QtyKg:N0} kg · {p.Pojemniki:0} poj · {p.Palety:0.##} pal",
                    CenaDisplay = hasCena ? $"{cenaVal:N2} zł/kg" : "—",
                    WartoscDisplay = hasCena ? $"{wartosc:N2} zł" : "",
                    CenaVisibility = Visibility.Visible,
                    ImageSource = p.ImageSource,
                    HasImageVisibility = p.HasImageVisibility,
                    PlaceholderVisibility = p.PlaceholderVisibility,
                    PlaceholderEmoji = p.PlaceholderEmoji,
                    IconE2 = "📦",
                    IconE2Visibility = p.E2 ? Visibility.Visible : Visibility.Collapsed,
                    IconFolia = "🧴",
                    IconFoliaVisibility = p.Folia ? Visibility.Visible : Visibility.Collapsed,
                    IconHallal = "🔪",
                    IconHallalVisibility = p.Hallal ? Visibility.Visible : Visibility.Collapsed,
                    IconStrefa = "⚠️",
                    IconStrefaVisibility = p.Strefa ? Visibility.Visible : Visibility.Collapsed
                };
            }).ToList();
            ListCart.ItemsSource = cartItems;
            EmptyCartHint.Visibility = cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LblCartCount.Text = pozycji + (pozycji == 1 ? " pozycja" : pozycji > 1 && pozycji < 5 ? " pozycje" : " pozycji");
            Step3Sub.Text = pozycji == 0 ? "Wybierz produkty" : $"{pozycji} poz · {kg:N0} kg";

            UpdateLimitDisplay();
            UpdateValidation();
        }

        private static string BuildTags(ProductVm p)
        {
            var t = new List<string>();
            if (p.E2) t.Add("E2");
            if (p.Folia) t.Add("Folia");
            if (p.Hallal) t.Add("Hallal");
            return t.Count == 0 ? "" : "🏷 " + string.Join(" · ", t);
        }

        private void UpdateLimitDisplay()
        {
            double headerBarMax = HdrProgressLimit.Parent is Border parentBar && parentBar.ActualWidth > 0
                ? parentBar.ActualWidth : 280;
            if (_wybranyKlient == null || _wybranyKlient.LimitKredytowy <= 0)
            {
                HdrLimitPctLabel.Text = "—";
                HdrProgressLimit.Width = 0;
                HdrLblLimitHint.Text = _wybranyKlient == null ? "Wybierz klienta" : "Brak limitu";
                ChipLimit.Text = "—";
                return;
            }

            decimal wykorzystany = _wybranyKlient.DoZaplacenia;
            decimal limit = _wybranyKlient.LimitKredytowy;
            decimal pct = Math.Min(100, (wykorzystany / limit) * 100);
            HdrLimitPctLabel.Text = pct.ToString("N0", _pl) + "%";
            HdrProgressLimit.Width = Math.Max(0, headerBarMax * (double)(pct / 100m));
            HdrLblLimitHint.Text = $"{wykorzystany:N0}/{limit:N0} zł · pozostało {(limit - wykorzystany):N0} zł";
            // ChipLimit = ikona stanu (zielony / żółty / czerwony)
            ChipLimit.Text = pct >= 100 ? "🔴" : pct > 80 ? "🟡" : "🟢";
            ChipLimit.ToolTip = $"Limit: {wykorzystany:N0}/{limit:N0} zł ({pct:N0}%)";

            if (pct >= 100)
            {
                HdrProgressLimit.Background = (Brush)FindResource("Danger");
                HdrLblLimitHint.Foreground = (Brush)FindResource("Danger");
            }
            else if (pct > 80)
            {
                HdrProgressLimit.Background = (Brush)FindResource("Warning");
                HdrLblLimitHint.Foreground = (Brush)FindResource("TextSecondary");
            }
            else
            {
                HdrProgressLimit.Background = (Brush)FindResource("Success");
                HdrLblLimitHint.Foreground = (Brush)FindResource("TextSecondary");
            }
        }

        private void UpdateValidation()
        {
            bool hasClient = _wybranyKlient != null;
            // W trybie edycji dopuszczamy przeszłe daty (zamówienie już istnieje, mogło być z wczoraj)
            bool hasTerm = _isEditMode || _wybranaData.Date >= DateTime.Today;
            bool hasItems = _produkty.Any(p => p.QtyKg > 0);

            SetValIndicator(ValKlient, ValKlientChip, hasClient);
            SetValIndicator(ValTermin, ValTerminChip, hasTerm);
            SetValIndicator(ValPozycje, ValPozycjeChip, hasItems);

            BtnSave.IsEnabled = hasClient && hasTerm && hasItems;
        }

        private static readonly Brush _validChipBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")!);
        private void SetValIndicator(TextBlock tb, Border chip, bool ok)
        {
            tb.Text = ok ? "✓" : "○";
            tb.Foreground = ok ? (Brush)FindResource("Success") : (Brush)FindResource("TextMuted");
            chip.Background = ok ? _validChipBg : Brushes.Transparent;
        }

        // ════════════════════ ZAPIS ════════════════════

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranyKlient == null) { ShowToast("Wybierz klienta", false); return; }
            var inCart = _produkty.Where(p => p.QtyKg > 0).ToList();
            if (inCart.Count == 0) { ShowToast("Brak pozycji w koszyku", false); return; }

            decimal totalPalety = inCart.Sum(p => p.Palety);
            if (totalPalety > LIMIT_PALET_TIR)
            {
                var r = MessageBox.Show(this,
                    $"Łączna liczba palet ({totalPalety:N1}) przekracza limit TIR ({LIMIT_PALET_TIR}).\n\nKontynuować zapis?",
                    "Przekroczenie limitu", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            // Pokaż dialog potwierdzenia
            FillConfirmDialog(inCart);
            ConfirmOverlay.Visibility = Visibility.Visible;
        }

        private void FillConfirmDialog(List<ProductVm> inCart)
        {
            // Klient
            ConfirmCustomerName.Text = _wybranyKlient!.Nazwa;
            ConfirmInitials.Text = GetInitials(_wybranyKlient.Nazwa);
            ConfirmAvatar.Background = MakeAvatarGradient(_wybranyKlient.Nazwa);
            string nip = !string.IsNullOrEmpty(_wybranyKlient.NIP) ? "NIP " + _wybranyKlient.NIP : "";
            string adr = $"{_wybranyKlient.KodPocztowy} {_wybranyKlient.Miejscowosc}".Trim();
            ConfirmCustomerSub.Text = string.Join(" · ", new[] { nip, adr }.Where(s => !string.IsNullOrWhiteSpace(s)));

            // Handlowiec avatar (real)
            string h = _wybranyKlient.Handlowiec ?? "";
            if (!string.IsNullOrWhiteSpace(h))
            {
                ConfirmHandlowiecRow.Visibility = Visibility.Visible;
                ApplyHandlowiecAvatar(ConfirmHandlowiecAvatar, ConfirmHandlowiecInitials, h);
                ConfirmHandlowiecName.Text = h;
            }
            else
            {
                ConfirmHandlowiecRow.Visibility = Visibility.Collapsed;
            }

            // Limit chip ikona
            try
            {
                if (_wybranyKlient.LimitKredytowy > 0)
                {
                    decimal pct = Math.Min(100, (_wybranyKlient.DoZaplacenia / _wybranyKlient.LimitKredytowy) * 100);
                    ConfirmLimitChip.Text = pct >= 100 ? "🔴" : pct > 80 ? "🟡" : "🟢";
                    ConfirmLimitChip.ToolTip = $"Limit: {_wybranyKlient.DoZaplacenia:N0}/{_wybranyKlient.LimitKredytowy:N0} zł ({pct:N0}%)";
                }
                else
                {
                    ConfirmLimitChip.Text = "⚪";
                    ConfirmLimitChip.ToolTip = "Brak limitu";
                }
            }
            catch { }

            // Termin — week strip + big godzina
            BuildConfirmWeek();
            ConfirmGodzina.Text = $"{_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
            string odbDay = _pl.DateTimeFormat.GetDayName(_wybranaData.DayOfWeek);
            ConfirmGodzinaDay.Text = $"{odbDay} · {_wybranaData:dd.MM.yyyy}";

            bool wlasny = ChkWlasnyOdbior?.IsChecked == true;
            ConfirmTransport.Text = wlasny ? "🚚 Klient własnym transportem" : "Standard (firmowy)";
            ConfirmTransportSub.Text = wlasny ? "(data odbioru NA UBOJNI)" : "";

            // Notatka
            string uwagi = TxtUwagi?.Text ?? "";
            if (!string.IsNullOrWhiteSpace(uwagi))
            {
                ConfirmNotatkaContainer.Visibility = Visibility.Visible;
                ConfirmNotatka.Text = uwagi;
            }
            else
            {
                ConfirmNotatkaContainer.Visibility = Visibility.Collapsed;
            }

            // Diff panel (tylko w edit-mode gdy są zmiany)
            FillConfirmDiff(inCart);

            // Lista towarów (reuse CartItem)
            int brakCenyCount = 0;
            var items = inCart.Select(p =>
            {
                bool hasCena = TryParseInput(p.Cena, out var cenaVal) && cenaVal > 0;
                if (!hasCena) brakCenyCount++;
                decimal wartosc = hasCena ? cenaVal * p.QtyKg : 0;
                return new CartItem
                {
                    Id = p.Id,
                    Kod = p.Kod,
                    Detail = $"{p.QtyKg:N0} kg · {p.Pojemniki:0} poj · {p.Palety:0.##} pal",
                    CenaDisplay = hasCena ? $"{cenaVal:N2} zł/kg" : "",
                    WartoscDisplay = hasCena ? $"{wartosc:N2} zł" : "⚠ brak ceny",
                    CenaVisibility = hasCena ? Visibility.Visible : Visibility.Collapsed,
                    ImageSource = p.ImageSource,
                    HasImageVisibility = p.HasImageVisibility,
                    PlaceholderVisibility = p.PlaceholderVisibility,
                    PlaceholderEmoji = p.PlaceholderEmoji,
                    IconE2 = "📦",
                    IconE2Visibility = p.E2 ? Visibility.Visible : Visibility.Collapsed,
                    IconFolia = "🧴",
                    IconFoliaVisibility = p.Folia ? Visibility.Visible : Visibility.Collapsed,
                    IconHallal = "🔪",
                    IconHallalVisibility = p.Hallal ? Visibility.Visible : Visibility.Collapsed,
                    IconStrefa = "⚠",
                    IconStrefaVisibility = p.Strefa ? Visibility.Visible : Visibility.Collapsed
                };
            }).ToList();
            ConfirmListItems.ItemsSource = items;

            // Brak ceny chip
            if (brakCenyCount > 0)
            {
                ConfirmBrakCenyChip.Visibility = Visibility.Visible;
                ConfirmBrakCenyText.Text = $"⚠ {brakCenyCount} bez ceny";
            }
            else
            {
                ConfirmBrakCenyChip.Visibility = Visibility.Collapsed;
            }

            // Sumy
            int pozycji = inCart.Count;
            decimal sumaKg = inCart.Sum(p => p.QtyKg);
            decimal sumaPoj = inCart.Sum(p => p.Pojemniki);
            decimal sumaPal = inCart.Sum(p => p.Palety);
            decimal totalWart = inCart.Sum(p =>
            {
                if (TryParseInput(p.Cena, out var c) && c > 0) return c * p.QtyKg;
                return 0m;
            });
            ConfirmSumy.Text = $"Σ {pozycji} pozycji · {sumaKg:N0} kg · {sumaPoj:N0} poj · {sumaPal:N2} pal";
            ConfirmTotalWartosc.Text = totalWart > 0 ? $"{totalWart:N2} zł" : "";
        }

        // Diff panel — pokazuje się tylko w edit-mode i tylko gdy są zmiany vs oryginał z bazy.
        // Gdy diff niepusty: ukrywa wszystkie inne sekcje (kalendarz, godzinę, transport, listę towarów, sumy).
        private void FillConfirmDiff(List<ProductVm> inCart)
        {
            if (!_isEditMode || _originalSnapshot == null)
            {
                ConfirmDiffPanel.Visibility = Visibility.Collapsed;
                ToggleConfirmFullView(show: true);
                return;
            }

            var diff = BuildDiff(inCart, _originalSnapshot);
            if (diff.Count == 0)
            {
                // Edit-mode bez zmian — pokaż normalnie pełny widok (status quo), bez panelu diff.
                ConfirmDiffPanel.Visibility = Visibility.Collapsed;
                ToggleConfirmFullView(show: true);
                return;
            }

            // Edit-mode + są zmiany → tylko diff. Reszta ukryta.
            ConfirmDiffPanel.Visibility = Visibility.Visible;
            ConfirmDiffHeader.Text = $"Zmiany w zamówieniu #{_editOrderId}";
            ConfirmDiffBadge.Text = diff.Count.ToString();
            ConfirmDiffList.ItemsSource = diff;
            ToggleConfirmFullView(show: false);
        }

        // Ukryj/pokaż sekcje confirm overlay poza panelem diff. Klient header zostaje zawsze (kontekst).
        private void ToggleConfirmFullView(bool show)
        {
            var v = show ? Visibility.Visible : Visibility.Collapsed;
            if (ConfirmWeekTitle != null) ConfirmWeekTitle.Visibility = v;
            if (ConfirmWeekContainer != null) ConfirmWeekContainer.Visibility = v;
            if (ConfirmGodzinaTransportGrid != null) ConfirmGodzinaTransportGrid.Visibility = v;
            if (ConfirmTowaryHeader != null) ConfirmTowaryHeader.Visibility = v;
            if (ConfirmTowaryListContainer != null) ConfirmTowaryListContainer.Visibility = v;
            if (ConfirmSumyContainer != null) ConfirmSumyContainer.Visibility = v;
            // Notatka: nawet w pełnym widoku ukryta jeśli pusta — zostaw oryginalną logikę z FillConfirmDialog
            if (!show && ConfirmNotatkaContainer != null) ConfirmNotatkaContainer.Visibility = Visibility.Collapsed;
        }

        // Palety stylów per typ zmiany — tworzymy raz, freeze, używamy w wielu wierszach.
        private static class DiffStyle
        {
            public static readonly Brush IconBgChange = FrozenBrush("#0EA5E9");   // niebieski
            public static readonly Brush IconBgAdd    = FrozenBrush("#16A34A");   // zielony
            public static readonly Brush IconBgRemove = FrozenBrush("#DC2626");   // czerwony
            public static readonly Brush LabelChange  = FrozenBrush("#0369A1");
            public static readonly Brush LabelAdd     = FrozenBrush("#15803D");
            public static readonly Brush LabelRemove  = FrozenBrush("#B91C1C");
            public static readonly Brush RowBgChange  = FrozenBrush("#F0F9FF");
            public static readonly Brush RowBgAdd     = FrozenBrush("#F0FDF4");
            public static readonly Brush RowBgRemove  = FrozenBrush("#FEF2F2");
            public static readonly Brush RowBdChange  = FrozenBrush("#7DD3FC");
            public static readonly Brush RowBdAdd     = FrozenBrush("#86EFAC");
            public static readonly Brush RowBdRemove  = FrozenBrush("#FECACA");

            private static Brush FrozenBrush(string hex)
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
                b.Freeze();
                return b;
            }
        }

        private List<DiffRowVm> BuildDiff(List<ProductVm> inCart, OrderSnapshot orig)
        {
            var rows = new List<DiffRowVm>();

            // ── Pola zamówienia (Termin, Produkcja, Transport, Notatka) — wszystkie traktowane jako "zmiana" ──

            if (orig.DataOdbioru != _wybranaData.Date || orig.Godzina != _wybranaGodzina)
            {
                rows.Add(FieldDiffRow(
                    icon: "~", emoji: "⏱",
                    label: "Termin odbioru",
                    before: $"{orig.DataOdbioru:dd.MM.yyyy} · {orig.Godzina.Hours:00}:{orig.Godzina.Minutes:00}",
                    after: $"{_wybranaData:dd.MM.yyyy} · {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}"));
            }

            if (orig.DataProdukcji.Date != _dataProdukcji.Date)
            {
                rows.Add(FieldDiffRow(
                    icon: "~", emoji: "🏭",
                    label: "Data produkcji",
                    before: $"{orig.DataProdukcji:dd.MM.yyyy}",
                    after: $"{_dataProdukcji:dd.MM.yyyy}"));
            }

            bool nowyWlasny = ChkWlasnyOdbior?.IsChecked == true;
            if (orig.WlasnyTransport != nowyWlasny)
            {
                rows.Add(FieldDiffRow(
                    icon: "~", emoji: "🚚",
                    label: "Transport",
                    before: orig.WlasnyTransport ? "Klient własnym" : "Firmowy",
                    after: nowyWlasny ? "Klient własnym" : "Firmowy"));
            }

            string nowaUwagi = (TxtUwagi?.Text ?? "").Trim();
            string origUwagi = (orig.Uwagi ?? "").Trim();
            if (nowaUwagi != origUwagi)
            {
                rows.Add(FieldDiffRow(
                    icon: "~", emoji: "📝",
                    label: "Notatka",
                    before: string.IsNullOrEmpty(origUwagi) ? "(pusta)" : Truncate(origUwagi, 80),
                    after: string.IsNullOrEmpty(nowaUwagi) ? "(pusta)" : Truncate(nowaUwagi, 80)));
            }

            // ── Pozycje koszyka — z miniaturą towaru (image z ProductVm) ──

            var nowyKoszyk = inCart.ToDictionary(p => p.Id);

            // + Dodane
            foreach (var p in inCart)
            {
                if (orig.Items.ContainsKey(p.Id)) continue;
                rows.Add(ItemDiffRow("+", p,
                    before: "—",
                    after: FormatItemSummary(p.QtyKg, p.Cena, p.E2, p.Folia, p.Hallal, p.Strefa)));
            }

            // − Usunięte
            foreach (var kvp in orig.Items)
            {
                if (nowyKoszyk.ContainsKey(kvp.Key)) continue;
                var it = kvp.Value;
                var p = _produkty.FirstOrDefault(x => x.Id == it.KodTowaru);
                rows.Add(ItemDiffRow("−", p, fallbackLabel: it.Kod,
                    before: FormatItemSummary(it.Ilosc, it.Cena, it.E2, it.Folia, it.Hallal, it.Strefa),
                    after: "usunięte"));
            }

            // ~ Zmienione (ilość/cena/flagi)
            foreach (var p in inCart)
            {
                if (!orig.Items.TryGetValue(p.Id, out var orig_it)) continue;

                bool ilDiff = orig_it.Ilosc != p.QtyKg;
                bool cenaDiff = !string.Equals((orig_it.Cena ?? "").Trim(), (p.Cena ?? "").Trim(), StringComparison.Ordinal);
                bool flagsDiff = orig_it.E2 != p.E2 || orig_it.Folia != p.Folia || orig_it.Hallal != p.Hallal || orig_it.Strefa != p.Strefa;

                if (!ilDiff && !cenaDiff && !flagsDiff) continue;

                rows.Add(ItemDiffRow("~", p,
                    before: FormatItemSummary(orig_it.Ilosc, orig_it.Cena, orig_it.E2, orig_it.Folia, orig_it.Hallal, orig_it.Strefa),
                    after: FormatItemSummary(p.QtyKg, p.Cena, p.E2, p.Folia, p.Hallal, p.Strefa)));
            }

            return rows;
        }

        // Wiersz diff dla pola (Termin/Produkcja/Transport/Notatka) — placeholder emoji, brak image, kolor niebieski.
        private static DiffRowVm FieldDiffRow(string icon, string emoji, string label, string before, string after)
        {
            return new DiffRowVm
            {
                Icon = icon,
                IconBg = DiffStyle.IconBgChange,
                LabelColor = DiffStyle.LabelChange,
                RowBg = DiffStyle.RowBgChange,
                RowBorder = DiffStyle.RowBdChange,
                PlaceholderEmoji = emoji,
                PlaceholderVisibility = Visibility.Visible,
                HasImageVisibility = Visibility.Collapsed,
                Label = label,
                Before = before,
                After = after
            };
        }

        // Wiersz diff dla pozycji towaru — miniatura z ProductVm jeśli dostępna, kolor zależny od typu (+/~/−).
        private DiffRowVm ItemDiffRow(string icon, ProductVm? p, string before, string after, string? fallbackLabel = null)
        {
            Brush iconBg, labelClr, rowBg, rowBd;
            switch (icon)
            {
                case "+": iconBg = DiffStyle.IconBgAdd; labelClr = DiffStyle.LabelAdd; rowBg = DiffStyle.RowBgAdd; rowBd = DiffStyle.RowBdAdd; break;
                case "−": iconBg = DiffStyle.IconBgRemove; labelClr = DiffStyle.LabelRemove; rowBg = DiffStyle.RowBgRemove; rowBd = DiffStyle.RowBdRemove; break;
                default: iconBg = DiffStyle.IconBgChange; labelClr = DiffStyle.LabelChange; rowBg = DiffStyle.RowBgChange; rowBd = DiffStyle.RowBdChange; break;
            }

            var row = new DiffRowVm
            {
                Icon = icon,
                IconBg = iconBg,
                LabelColor = labelClr,
                RowBg = rowBg,
                RowBorder = rowBd,
                Label = p?.Kod ?? fallbackLabel ?? "—",
                Before = before,
                After = after,
                PlaceholderEmoji = p != null ? EmojiForKategoria(p.KategoriaDisplay) : "📦"
            };

            // Image z ProductVm jeśli wczytany; inaczej placeholder emoji.
            if (p?.ImageSource != null)
            {
                row.ImageSource = p.ImageSource;
                row.HasImageVisibility = Visibility.Visible;
                row.PlaceholderVisibility = Visibility.Collapsed;
            }
            else
            {
                row.HasImageVisibility = Visibility.Collapsed;
                row.PlaceholderVisibility = Visibility.Visible;
            }
            return row;
        }

        private static string FormatItemSummary(decimal ilosc, string? cena, bool e2, bool folia, bool hallal, bool strefa)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(ilosc.ToString("N0")).Append(" kg");
            if (!string.IsNullOrWhiteSpace(cena)) sb.Append(" · ").Append(cena).Append(" zł");
            var flagi = new List<string>();
            if (e2) flagi.Add("E2");
            if (folia) flagi.Add("Folia");
            if (hallal) flagi.Add("Halal");
            if (strefa) flagi.Add("Strefa");
            if (flagi.Count > 0) sb.Append(" · ").Append(string.Join(" ", flagi));
            return sb.ToString();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max - 1) + "…";
        }

        private void BuildConfirmWeek()
        {
            // Znajdź poniedziałek tygodnia w którym jest data produkcji
            int dow = (int)_dataProdukcji.DayOfWeek; // Sun=0, Mon=1...Sat=6
            int daysFromMonday = dow == 0 ? 6 : dow - 1;
            var monday = _dataProdukcji.AddDays(-daysFromMonday).Date;

            // Jeśli odbiór jest > tydzień po prod, pokaż 14 dni (2 tygodnie)
            int span = (_wybranaData.Date - monday).Days >= 7 ? 14 : 7;
            string title = span == 14 ? "📆 Tygodnie zamówienia (2 tygodnie)" : "📆 Tydzień zamówienia";
            ConfirmWeekTitle.Text = title;

            string[] dayNames = { "Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd" };
            var prodBg = (Brush)new BrushConverter().ConvertFrom("#FFE8B3")!;
            var prodBorder = (Brush)new BrushConverter().ConvertFrom("#F59E0B")!;
            var odbBg = (Brush)new BrushConverter().ConvertFrom("#D1FAE5")!;
            var odbBorder = (Brush)FindResource("BrandGreen");
            var weekendBg = (Brush)new BrushConverter().ConvertFrom("#F3F4F6")!;
            var defaultBg = Brushes.White;
            var defaultBorder = (Brush)FindResource("Border");

            var list = new List<WeekDayVm>();
            for (int i = 0; i < span; i++)
            {
                var d = monday.AddDays(i);
                bool isProd = d == _dataProdukcji.Date;
                bool isOdbior = d == _wybranaData.Date;
                bool isSat = d.DayOfWeek == DayOfWeek.Saturday;
                bool isSun = d.DayOfWeek == DayOfWeek.Sunday;

                string marker = "";
                string markerLabel = "";
                Brush bg = (isSat || isSun) ? weekendBg : defaultBg;
                Brush border = defaultBorder;
                Brush fore = (isSat || isSun) ? (Brush)FindResource("TextMuted") : (Brush)FindResource("TextPrimary");

                string hourLabel = "";
                if (isProd && isOdbior)
                {
                    marker = "🏭🚚";
                    markerLabel = "PROD+ODB";
                    bg = prodBg;
                    border = prodBorder;
                    fore = (Brush)new BrushConverter().ConvertFrom("#92400E")!;
                    hourLabel = $"{_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
                }
                else if (isProd)
                {
                    marker = "🏭";
                    markerLabel = "PRODUKCJA";
                    bg = prodBg;
                    border = prodBorder;
                    fore = (Brush)new BrushConverter().ConvertFrom("#92400E")!;
                }
                else if (isOdbior)
                {
                    marker = "🚚";
                    markerLabel = "ODBIÓR";
                    bg = odbBg;
                    border = odbBorder;
                    fore = (Brush)FindResource("BrandGreenDark");
                    hourLabel = $"{_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
                }

                list.Add(new WeekDayVm
                {
                    Date = d,
                    DayName = dayNames[(i % 7)],
                    DayNum = d.Day.ToString("00"),
                    Marker = marker,
                    MarkerLabel = markerLabel,
                    HourLabel = hourLabel,
                    BgBrush = bg,
                    BorderBrush = border,
                    ForeBrush = fore
                });
            }
            // Jeśli 14 dni, ustaw 7 columns by Wrap
            if (span == 14)
            {
                if (ConfirmWeekItems.ItemsPanel?.LoadContent() is UniformGrid ug)
                    ug.Columns = 7;
            }
            ConfirmWeekItems.ItemsSource = list;
        }

        private void BtnConfirmCancel_Click(object sender, RoutedEventArgs e)
        {
            ConfirmOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnConfirmSave_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranyKlient == null) return;
            var inCart = _produkty.Where(p => p.QtyKg > 0).ToList();
            if (inCart.Count == 0) return;

            BtnSave.IsEnabled = false;
            Cursor = Cursors.Wait;
            try
            {
                int orderId = await SaveOrderAsync(inCart);

                // ── Logowanie historii zmian — szczegółowo per pozycja, żeby filtr towaru w HistoriaZmianWindow
                //    pokazał "0 kg → 100 kg" dla wpisów UTWORZENIA i "100 → 150 kg" dla edycji.
                _ = LogujHistorieAsync(orderId, inCart);

                // Tracking: notatka wpisana ręcznie (system uczy się że istniejące propozycje nie wystarczyły)
                string finalNote = (TxtUwagi.Text ?? "").Trim();
                if (!string.IsNullOrEmpty(finalNote) && _notatkiSvc != null && _wybranyKlient != null
                    && int.TryParse(_wybranyKlient.Id, out int kidLog))
                {
                    var towary = inCart.Select(p => p.Id);
                    _ = _notatkiSvc.LogUsageAsync(finalNote, kidLog, UserID,
                        Kalendarz1.Zamowienia.Services.NotatkiService.AkcjaWpisana,
                        towary, null);
                }

                ConfirmOverlay.Visibility = Visibility.Collapsed;
                ShowToast(_isEditMode ? $"✓ Zamówienie #{orderId} zaktualizowane" : $"✓ Zamówienie #{orderId} zapisane", true);
                await Task.Delay(900);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
                Cursor = Cursors.Arrow;
            }
        }

        private async Task<int> SaveOrderAsync(List<ProductVm> items)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            bool dataProdExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "DataProdukcji");
            bool dataUbojuExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "DataUboju");
            bool strefaTowarExists = await ColumnExistsAsync(cn, "ZamowieniaMiesoTowar", "Strefa");
            bool czyModMagazynuExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "CzyZmodyfikowaneDlaMagazynu");
            bool czyModProdukcjiExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "CzyZmodyfikowaneDlaProdukcji");
            bool uwagiSnapshotExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "UwagiSnapshot");

            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

            decimal sumaPoj = items.Sum(p => p.Pojemniki);
            decimal sumaPal = items.Sum(p => p.Palety);
            bool czyE2 = items.Any(p => p.E2);
            string transportStatus = ChkWlasnyOdbior.IsChecked == true ? "Wlasny" : "Oczekuje";

            DateTime dataProdukcji = _dataProdukcji;
            DateTime dataPrzyjazdu = _wybranaData.Date.Add(_wybranaGodzina);

            int orderId;
            if (_isEditMode)
            {
                orderId = _editOrderId!.Value;

                string updateSql = @"UPDATE [dbo].[ZamowieniaMieso] SET
                    DataZamowienia = @dz, DataPrzyjazdu = @dp, KlientId = @kid, Uwagi = @uw,
                    KtoMod = @km, KiedyMod = SYSDATETIME(), LiczbaPojemnikow = @poj,
                    LiczbaPalet = @pal, TrybE2 = @e2, TransportStatus = @ts,
                    CzyZmodyfikowaneDlaFaktur = 1, DataOstatniejModyfikacji = SYSDATETIME(), ModyfikowalPrzez = @fullName";
                if (dataProdExists) updateSql += ", DataProdukcji = @dprod";
                if (dataUbojuExists) updateSql += ", DataUboju = @duboj";
                if (czyModMagazynuExists) updateSql += ", CzyZmodyfikowaneDlaMagazynu = 1";
                if (czyModProdukcjiExists) updateSql += ", CzyZmodyfikowaneDlaProdukcji = 1";
                if (uwagiSnapshotExists) updateSql += ", UwagiSnapshot = CASE WHEN UwagiSnapshot IS NULL THEN Uwagi ELSE UwagiSnapshot END";
                updateSql += " WHERE Id = @id";

                var cmdUpd = new SqlCommand(updateSql, cn, tr);
                cmdUpd.Parameters.AddWithValue("@dz", _wybranaData.Date);
                cmdUpd.Parameters.AddWithValue("@dp", dataPrzyjazdu);
                if (dataProdExists) cmdUpd.Parameters.AddWithValue("@dprod", dataProdukcji);
                if (dataUbojuExists) cmdUpd.Parameters.AddWithValue("@duboj", dataProdukcji);
                cmdUpd.Parameters.AddWithValue("@kid", int.Parse(_wybranyKlient!.Id));
                cmdUpd.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(TxtUwagi.Text) ? (object)DBNull.Value : TxtUwagi.Text);
                cmdUpd.Parameters.AddWithValue("@km", UserID);
                cmdUpd.Parameters.AddWithValue("@fullName", App.UserFullName ?? UserID);
                cmdUpd.Parameters.AddWithValue("@id", orderId);
                cmdUpd.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPoj));
                cmdUpd.Parameters.AddWithValue("@pal", sumaPal);
                cmdUpd.Parameters.AddWithValue("@e2", czyE2);
                cmdUpd.Parameters.AddWithValue("@ts", transportStatus);
                await cmdUpd.ExecuteNonQueryAsync();

                // Snapshot pre-edit (best-effort, jak w starym oknie)
                try
                {
                    var cmdSnap = new SqlCommand(@"
                        IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name='ZamowieniaMiesoSnapshot' AND type='U')
                        BEGIN
                            CREATE TABLE dbo.ZamowieniaMiesoSnapshot (
                                Id INT IDENTITY(1,1) PRIMARY KEY,
                                ZamowienieId INT NOT NULL,
                                KodTowaru INT NOT NULL,
                                Ilosc DECIMAL(18,3) NOT NULL,
                                Folia BIT NULL,
                                Hallal BIT NULL,
                                E2 BIT NULL DEFAULT 0,
                                Strefa BIT NULL DEFAULT 0,
                                DataSnapshotu DATETIME NOT NULL DEFAULT GETDATE(),
                                TypSnapshotu NVARCHAR(20) NOT NULL
                            );
                            CREATE INDEX IX_Snapshot_ZamowienieId ON dbo.ZamowieniaMiesoSnapshot(ZamowienieId);
                        END;
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ZamowieniaMiesoSnapshot') AND name='E2')
                            ALTER TABLE dbo.ZamowieniaMiesoSnapshot ADD E2 BIT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ZamowieniaMiesoSnapshot') AND name='Strefa')
                            ALTER TABLE dbo.ZamowieniaMiesoSnapshot ADD Strefa BIT NULL DEFAULT 0;
                        IF NOT EXISTS (SELECT 1 FROM dbo.ZamowieniaMiesoSnapshot WHERE ZamowienieId=@id AND TypSnapshotu='Realizacja')
                        BEGIN
                            INSERT INTO dbo.ZamowieniaMiesoSnapshot (ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, E2, Strefa, TypSnapshotu)
                            SELECT ZamowienieId, KodTowaru, Ilosc, Folia, Hallal, ISNULL(E2,0), ISNULL(Strefa,0), 'Realizacja'
                            FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId=@id AND Ilosc > 0;
                        END", cn, tr);
                    cmdSnap.Parameters.AddWithValue("@id", orderId);
                    await cmdSnap.ExecuteNonQueryAsync();
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Snapshot save error: {ex.Message}"); }

                var cmdDel = new SqlCommand("DELETE FROM dbo.ZamowieniaMiesoTowar WHERE ZamowienieId = @id", cn, tr);
                cmdDel.Parameters.AddWithValue("@id", orderId);
                await cmdDel.ExecuteNonQueryAsync();
            }
            else
            {
                var cmdGetId = new SqlCommand("SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.ZamowieniaMieso", cn, tr);
                orderId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

                string insertCols = "Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus";
                string insertVals = "@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, @ts";
                if (dataProdExists) { insertCols += ", DataProdukcji"; insertVals += ", @dprod"; }
                if (dataUbojuExists) { insertCols += ", DataUboju"; insertVals += ", @duboj"; }

                var cmdIns = new SqlCommand($"INSERT INTO dbo.ZamowieniaMieso ({insertCols}) VALUES ({insertVals})", cn, tr);
                cmdIns.Parameters.AddWithValue("@id", orderId);
                cmdIns.Parameters.AddWithValue("@dz", _wybranaData.Date);
                cmdIns.Parameters.AddWithValue("@dp", dataPrzyjazdu);
                cmdIns.Parameters.AddWithValue("@kid", int.Parse(_wybranyKlient!.Id));
                cmdIns.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(TxtUwagi.Text) ? (object)DBNull.Value : TxtUwagi.Text);
                cmdIns.Parameters.AddWithValue("@u", UserID);
                cmdIns.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPoj));
                cmdIns.Parameters.AddWithValue("@pal", sumaPal);
                cmdIns.Parameters.AddWithValue("@e2", czyE2);
                cmdIns.Parameters.AddWithValue("@ts", transportStatus);
                if (dataProdExists) cmdIns.Parameters.AddWithValue("@dprod", dataProdukcji);
                if (dataUbojuExists) cmdIns.Parameters.AddWithValue("@duboj", dataProdukcji);
                await cmdIns.ExecuteNonQueryAsync();
            }

            string strefaCol = strefaTowarExists ? ", Strefa" : "";
            string strefaVal = strefaTowarExists ? ", @strefa" : "";
            var cmdItem = new SqlCommand(
                $@"INSERT INTO dbo.ZamowieniaMiesoTowar
                   (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal{strefaCol})
                   VALUES (@zid, @kt, @il, @ce, @poj, @pal, @e2, @folia, @hallal{strefaVal})", cn, tr);

            cmdItem.Parameters.Add("@zid", SqlDbType.Int);
            cmdItem.Parameters.Add("@kt", SqlDbType.Int);
            cmdItem.Parameters.Add("@il", SqlDbType.Decimal);
            cmdItem.Parameters.Add("@ce", SqlDbType.VarChar, 20);
            cmdItem.Parameters.Add("@poj", SqlDbType.Int);
            cmdItem.Parameters.Add("@pal", SqlDbType.Decimal);
            cmdItem.Parameters.Add("@e2", SqlDbType.Bit);
            cmdItem.Parameters.Add("@folia", SqlDbType.Bit);
            cmdItem.Parameters.Add("@hallal", SqlDbType.Bit);
            if (strefaTowarExists) cmdItem.Parameters.Add("@strefa", SqlDbType.Bit);

            foreach (var p in items)
            {
                cmdItem.Parameters["@zid"].Value = orderId;
                cmdItem.Parameters["@kt"].Value = p.Id;
                cmdItem.Parameters["@il"].Value = p.QtyKg;

                string cenaOut = "0";
                if (!string.IsNullOrWhiteSpace(p.Cena) && decimal.TryParse(p.Cena.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var ceVal) && ceVal > 0)
                    cenaOut = ceVal.ToString("F2", CultureInfo.InvariantCulture);
                cmdItem.Parameters["@ce"].Value = cenaOut;

                cmdItem.Parameters["@poj"].Value = (int)Math.Round(p.Pojemniki);
                cmdItem.Parameters["@pal"].Value = p.Palety;
                cmdItem.Parameters["@e2"].Value = p.E2;
                cmdItem.Parameters["@folia"].Value = p.Folia;
                cmdItem.Parameters["@hallal"].Value = p.Hallal;
                if (strefaTowarExists) cmdItem.Parameters["@strefa"].Value = p.Strefa;

                await cmdItem.ExecuteNonQueryAsync();
            }

            await tr.CommitAsync();
            return orderId;
        }

        // ════════════════════ LOGOWANIE HISTORII ZMIAN ════════════════════
        // Po zapisie zamówienia loguje wszystkie zmiany szczegółowo:
        //   - Nowe zamówienie: 1 wpis UTWORZENIE + N wpisów "Pozycja: X - Zam." (0 → ilość) + flag/cena per pozycja
        //   - Edycja: porównuje z _originalSnapshot — log dla każdej różnicy (termin/transport/notatka/pozycje)
        // Dzięki temu w HistoriaZmianWindow filtr "Towar = Filet" + "Kategoria = KG" pokazuje pełną historię ilości.
        private async Task LogujHistorieAsync(int orderId, List<ProductVm> inCart)
        {
            try
            {
                string userId = UserID;
                string userName = App.UserFullName ?? UserID;

                if (!_isEditMode)
                {
                    // ─── NOWE ZAMÓWIENIE ───
                    string klient = _wybranyKlient?.Nazwa ?? "";
                    string termin = $"{_wybranaData:dd.MM.yyyy} {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
                    string opisOgolny = $"Utworzono zamówienie #{orderId} · klient: {klient} · termin: {termin} · pozycji: {inCart.Count}";
                    await Kalendarz1.Services.HistoriaZmianService.LogujUtworzenie(orderId, userId, userName, opisOgolny);

                    // Każda pozycja jako oddzielny wpis structured (0 → ilosc)
                    foreach (var p in inCart)
                    {
                        await Kalendarz1.Services.HistoriaZmianService.LogujDodaniePozycji(orderId, p.Kod, p.QtyKg, userId, userName);

                        // Cena — tylko jeśli ustawiona
                        if (!string.IsNullOrWhiteSpace(p.Cena) && p.Cena != "0")
                        {
                            await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Cena",
                                "0", p.Cena, userId, userName);
                        }
                        // Flagi włączone — log każdą osobno (żeby były widoczne pod filtrem Kategoria=FLAGI)
                        if (p.E2) await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "E2", "false", "true", userId, userName);
                        if (p.Folia) await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Folia", "false", "true", userId, userName);
                        if (p.Hallal) await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Hallal", "false", "true", userId, userName);
                        if (p.Strefa) await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Strefa", "false", "true", userId, userName);
                    }
                    return;
                }

                // ─── EDYCJA — porównaj z _originalSnapshot i log tylko różnice ───
                if (_originalSnapshot == null) return;

                // Termin / Data produkcji / Transport / Notatka
                if (_originalSnapshot.DataOdbioru != _wybranaData.Date || _originalSnapshot.Godzina != _wybranaGodzina)
                {
                    string stary = $"{_originalSnapshot.DataOdbioru:dd.MM.yyyy} {_originalSnapshot.Godzina.Hours:00}:{_originalSnapshot.Godzina.Minutes:00}";
                    string nowy = $"{_wybranaData:dd.MM.yyyy} {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
                    await Kalendarz1.Services.HistoriaZmianService.LogujEdycje(orderId, userId, userName, "DataPrzyjazdu", stary, nowy);
                }
                if (_originalSnapshot.DataProdukcji.Date != _dataProdukcji.Date)
                {
                    await Kalendarz1.Services.HistoriaZmianService.LogujZmianeData(orderId, userId, "DataProdukcji",
                        _originalSnapshot.DataProdukcji, _dataProdukcji, userName);
                }
                bool nowyWlasny = ChkWlasnyOdbior?.IsChecked == true;
                if (_originalSnapshot.WlasnyTransport != nowyWlasny)
                {
                    await Kalendarz1.Services.HistoriaZmianService.LogujEdycje(orderId, userId, userName, "Transport",
                        _originalSnapshot.WlasnyTransport ? "Wlasny" : "Firmowy",
                        nowyWlasny ? "Wlasny" : "Firmowy");
                }
                string nowaUwagi = (TxtUwagi?.Text ?? "").Trim();
                string origUwagi = (_originalSnapshot.Uwagi ?? "").Trim();
                if (nowaUwagi != origUwagi)
                {
                    await Kalendarz1.Services.HistoriaZmianService.LogujZmianeNotatki(orderId, userId, origUwagi, nowaUwagi, userName);
                }

                // Pozycje — dodane / usunięte / zmienione
                var nowyKoszyk = inCart.ToDictionary(p => p.Id);

                foreach (var p in inCart)
                {
                    if (_originalSnapshot.Items.TryGetValue(p.Id, out var orig_it))
                    {
                        // Zmiana ilości
                        if (orig_it.Ilosc != p.QtyKg)
                        {
                            await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Zam.",
                                orig_it.Ilosc.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                                p.QtyKg.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                                userId, userName);
                        }
                        // Zmiana ceny
                        string origCena = (orig_it.Cena ?? "").Trim();
                        string nowaCena = (p.Cena ?? "").Trim();
                        if (origCena != nowaCena)
                        {
                            await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Cena",
                                string.IsNullOrEmpty(origCena) ? "0" : origCena,
                                string.IsNullOrEmpty(nowaCena) ? "0" : nowaCena,
                                userId, userName);
                        }
                        // Flagi
                        if (orig_it.E2 != p.E2)
                            await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "E2", orig_it.E2.ToString().ToLower(), p.E2.ToString().ToLower(), userId, userName);
                        if (orig_it.Folia != p.Folia)
                            await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Folia", orig_it.Folia.ToString().ToLower(), p.Folia.ToString().ToLower(), userId, userName);
                        if (orig_it.Hallal != p.Hallal)
                            await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Hallal", orig_it.Hallal.ToString().ToLower(), p.Hallal.ToString().ToLower(), userId, userName);
                        if (orig_it.Strefa != p.Strefa)
                            await Kalendarz1.Services.HistoriaZmianService.LogujZmianePozycji(orderId, p.Kod, "Strefa", orig_it.Strefa.ToString().ToLower(), p.Strefa.ToString().ToLower(), userId, userName);
                    }
                    else
                    {
                        // Nowa pozycja w edycji
                        await Kalendarz1.Services.HistoriaZmianService.LogujDodaniePozycji(orderId, p.Kod, p.QtyKg, userId, userName);
                    }
                }

                // Usunięte pozycje (były w oryginale, nie ma w koszyku)
                foreach (var kvp in _originalSnapshot.Items)
                {
                    if (nowyKoszyk.ContainsKey(kvp.Key)) continue;
                    await Kalendarz1.Services.HistoriaZmianService.LogujUsunieciePozycji(orderId, kvp.Value.Kod, kvp.Value.Ilosc, userId, userName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LogujHistorie] {ex.Message}");
                // Logowanie historii nie powinno przerywać zapisu — best-effort
            }
        }

        private async Task<bool> ColumnExistsAsync(SqlConnection cn, string table, string column)
        {
            var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = @c", cn);
            cmd.Parameters.AddWithValue("@t", "dbo." + table);
            cmd.Parameters.AddWithValue("@c", column);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        // ════════════════════ ACTION BAR ════════════════════

        private void BtnClose_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void TxtUwagi_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtUwagiPlaceholder.Visibility = string.IsNullOrEmpty(TxtUwagi.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ════════════════════ TOAST ════════════════════

        private DispatcherTimer? _toastTimer;
        private void ShowToast(string text, bool success)
        {
            ToastText.Text = text;
            ToastIcon.Text = success ? "✓" : "⚠";
            ToastIcon.Foreground = success ? (Brush)FindResource("Success") : (Brush)FindResource("Warning");
            Toast.Visibility = Visibility.Visible;
            Toast.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            Toast.BeginAnimation(OpacityProperty, fadeIn);

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer!.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fadeOut.Completed += (_, _) => Toast.Visibility = Visibility.Collapsed;
                Toast.BeginAnimation(OpacityProperty, fadeOut);
            };
            _toastTimer.Start();
        }

        // ═══════════════════════════════════════════════════════════
        // VIEW MODELS
        // ═══════════════════════════════════════════════════════════

        public class KontrahentVm : INotifyPropertyChanged
        {
            public string Id { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public string NIP { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Miejscowosc { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public DateTime? OstatnieZamowienie { get; set; }
            public decimal LimitKredytowy { get; set; }
            public decimal DoZaplacenia { get; set; }

            public string LastOrderDisplay { get; set; } = "";
            public string LastOrderShort { get; set; } = "";
            public string HandlowiecShort { get; set; } = "";
            public string NipDisplay { get; set; } = "";
            public string Initials { get; set; } = "";
            public Brush AvatarBrush { get; set; } = Brushes.Gray;
            public string LimitBadge { get; set; } = "";
            public Brush LimitBadgeBg { get; set; } = Brushes.LightGray;
            public Brush LimitBadgeFg { get; set; } = Brushes.Gray;

            // Preferencje klienta wyliczone z historii zamówień
            public int? PreferredHour { get; set; }
            public int? PreferredDeliveryDiff { get; set; }

#pragma warning disable CS0067
            public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
        }

        public class DayVm
        {
            public DateTime Date { get; set; }
            public string DayName { get; set; } = "";
            public string DayNum { get; set; } = "";
            public string MonthShort { get; set; } = "";
            public string LoadDisplay { get; set; } = "";
            public bool IsSelected { get; set; }
            public Brush BgBrush { get; set; } = Brushes.White;
            public Brush BorderBrush { get; set; } = Brushes.LightGray;
            public Brush ForeBrush { get; set; } = Brushes.Black;
            public Brush LoadBrush { get; set; } = Brushes.Gray;
            public double LoadBarWidth { get; set; }
        }

        public class HourVm
        {
            public TimeSpan Hour { get; set; }
            public string HourDisplay { get; set; } = "";
            public Brush BgBrush { get; set; } = Brushes.White;
            public Brush BorderBrush { get; set; } = Brushes.LightGray;
            public Brush ForeBrush { get; set; } = Brushes.Black;
        }

        public class ProductVm : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Katalog { get; set; } = "";
            public string KategoriaDisplay { get; set; } = "";
            public decimal QtyKg { get; set; }
            public bool E2 { get; set; }
            public bool Folia { get; set; }
            public bool Hallal { get; set; }
            public bool Strefa { get; set; }
            public string? Cena { get; set; }

            public decimal Pojemniki { get; set; }
            public decimal Palety { get; set; }

            public string QtyKgDisplay { get; set; } = "0";
            public string WartoscDisplay { get; set; } = "";
            public string PojDisplay { get; set; } = "0";
            public string PalDisplay { get; set; } = "0";
            public string CenaDisplay { get; set; } = "";
            public Visibility InCartVisibility { get; set; } = Visibility.Collapsed;
            public string InCartBadge { get; set; } = "";
            public Brush ProductBorder { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEDF1")!);
            public Thickness ProductBorderThickness { get; set; } = new Thickness(1);

            private ImageSource? _imageSource;
            public ImageSource? ImageSource
            {
                get => _imageSource;
                set { _imageSource = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageSource))); }
            }
            private Visibility _hasImageVisibility = Visibility.Collapsed;
            public Visibility HasImageVisibility
            {
                get => _hasImageVisibility;
                set { _hasImageVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasImageVisibility))); }
            }
            private Visibility _placeholderVisibility = Visibility.Visible;
            public Visibility PlaceholderVisibility
            {
                get => _placeholderVisibility;
                set { _placeholderVisibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderVisibility))); }
            }
            public string PlaceholderEmoji { get; set; } = "🍗";

            public event PropertyChangedEventHandler? PropertyChanged;
            public void NotifyAll(string? skip = null)
            {
                if (skip != "kg")  PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QtyKgDisplay)));
                if (skip != "poj") PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PojDisplay)));
                if (skip != "pal") PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PalDisplay)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CenaDisplay)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WartoscDisplay)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InCartVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InCartBadge)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(E2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Folia)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hallal)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Strefa)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProductBorder)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProductBorderThickness)));
            }
        }

        public class CartItem
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Detail { get; set; } = "";
            public string CenaDisplay { get; set; } = "";
            public string WartoscDisplay { get; set; } = "";
            public Visibility CenaVisibility { get; set; } = Visibility.Collapsed;
            public ImageSource? ImageSource { get; set; }
            public Visibility HasImageVisibility { get; set; } = Visibility.Collapsed;
            public Visibility PlaceholderVisibility { get; set; } = Visibility.Visible;
            public string PlaceholderEmoji { get; set; } = "🍗";
            public string IconE2 { get; set; } = "";
            public Visibility IconE2Visibility { get; set; } = Visibility.Collapsed;
            public string IconFolia { get; set; } = "";
            public Visibility IconFoliaVisibility { get; set; } = Visibility.Collapsed;
            public string IconHallal { get; set; } = "";
            public Visibility IconHallalVisibility { get; set; } = Visibility.Collapsed;
            public string IconStrefa { get; set; } = "";
            public Visibility IconStrefaVisibility { get; set; } = Visibility.Collapsed;
        }

        public class HandlowiecVm
        {
            public string Name { get; set; } = "";
            public string Initials { get; set; } = "";
            public Brush AvatarBrush { get; set; } = Brushes.Gray;
            public Visibility AvatarVisibility { get; set; } = Visibility.Visible;
            public bool IsSpecial { get; set; }
            public string SpecialTag { get; set; } = "";
            public override string ToString() => Name;
        }

        public class WeekDayVm
        {
            public DateTime Date { get; set; }
            public string DayName { get; set; } = "";
            public string DayNum { get; set; } = "";
            public string Marker { get; set; } = "";
            public string MarkerLabel { get; set; } = "";
            public string HourLabel { get; set; } = "";
            public Brush BgBrush { get; set; } = Brushes.White;
            public Brush BorderBrush { get; set; } = Brushes.LightGray;
            public Brush ForeBrush { get; set; } = Brushes.Black;
        }

        // ════════════════════ DIFF — snapshot oryginalnego stanu zamówienia (edit-mode) ════════════════════

        public class ItemSnapshot
        {
            public int KodTowaru { get; set; }
            public string Kod { get; set; } = "";
            public decimal Ilosc { get; set; }
            public string Cena { get; set; } = "";
            public bool E2 { get; set; }
            public bool Folia { get; set; }
            public bool Hallal { get; set; }
            public bool Strefa { get; set; }
        }

        public class OrderSnapshot
        {
            public DateTime DataOdbioru { get; set; }
            public TimeSpan Godzina { get; set; }
            public DateTime DataProdukcji { get; set; }
            public bool WlasnyTransport { get; set; }
            public string Uwagi { get; set; } = "";
            public Dictionary<int, ItemSnapshot> Items { get; set; } = new();
        }

        public class DiffRowVm
        {
            public string Icon { get; set; } = "";              // "+" "~" "−" w kółku
            public Brush IconBg { get; set; } = Brushes.Gray;   // wypełnienie kółka (zielony/niebieski/czerwony)
            public string Label { get; set; } = "";
            public Brush LabelColor { get; set; } = Brushes.Black;
            public string Before { get; set; } = "";
            public string After { get; set; } = "";
            public Brush RowBg { get; set; } = Brushes.White;
            public Brush RowBorder { get; set; } = Brushes.LightGray;
            // Miniatura: dla pól emoji (⏱ 🏭 🚚 📝), dla towarów ImageSource z _produkty (fallback emoji)
            public ImageSource? ImageSource { get; set; }
            public Visibility HasImageVisibility { get; set; } = Visibility.Collapsed;
            public Visibility PlaceholderVisibility { get; set; } = Visibility.Visible;
            public string PlaceholderEmoji { get; set; } = "📋";
        }
    }
}
