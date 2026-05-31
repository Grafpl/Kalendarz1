using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Kalendarz1
{
    public partial class OstatnieWstawieniaHodowcyWindow : Window
    {
        private const string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "SP", "Z", "O", "O.O", "OO", "S.A", "SA", "P.H", "FHU", "FH", "PHU", "PPHU",
            "GOSP", "GOSPODARSTWO", "ROLNE", "HODOWLA", "DROBIU", "DROBIARSKIE",
            "FIRMA", "PRZEDSIEBIORSTWO", "S.C", "SC", "SPOLKA", "JAWNA",
            "JR", "JR.", "SR", "SR.", "I", "II", "III"
        };

        // Cache ImageBrush avatarów — współdzielony statycznie. Identyczny pattern jak w WidokWstawienia.
        private static readonly Dictionary<string, ImageBrush> _avatarBrushCache = new();
        private static readonly HashSet<string> _avatarMissingCache = new();

        private readonly List<HodowcaWierszVM> _wszystkie = new();
        private ICollectionView? _widok;
        private int? _odRoku = 2025;
        private bool _zaladowano;

        public OstatnieWstawieniaHodowcyWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            // Avatar dla każdego wiersza — pattern z WidokWstawienia.LoadAvatarForRow.
            dgHodowcy.LoadingRow += DgHodowcy_LoadingRow;

            Loaded += async (_, _) =>
            {
                if (_zaladowano) return;
                _zaladowano = true;
                await ZaladujAsync();
            };
        }

        private async Task ZaladujAsync()
        {
            txtStatus.Text = "⏳ Ładowanie...";
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                _wszystkie.Clear();
                var dane = await Task.Run(() => PobierzDaneZBazy(_odRoku));
                foreach (var w in dane)
                    _wszystkie.Add(w);

                WykryjKolizjeNazwisk(_wszystkie);

                _widok = CollectionViewSource.GetDefaultView(_wszystkie);
                _widok.GroupDescriptions.Clear();
                _widok.GroupDescriptions.Add(new PropertyGroupDescription(nameof(HodowcaWierszVM.GroupName)));

                _widok.SortDescriptions.Clear();
                _widok.SortDescriptions.Add(new SortDescription(nameof(HodowcaWierszVM.GroupSortKey), ListSortDirection.Ascending));
                // W obrębie grupy: od najwcześniejszego ostatniego wstawienia do najpóźniejszego
                // (na górze hodowcy "zaniedbani" — najdłużej bez nowego wstawienia).
                _widok.SortDescriptions.Add(new SortDescription(nameof(HodowcaWierszVM.OstatnieWstawienieData), ListSortDirection.Ascending));

                _widok.Filter = FiltrujWiersz;
                dgHodowcy.ItemsSource = _widok;

                AktualizujLicznikiGrup();

                int kolizji = _wszystkie.Count(w => w.MaKolizje);
                string zakresOpis = _odRoku.HasValue ? $"od {_odRoku} r." : "wszystkie lata";
                txtStatus.Text = $"✅ {_wszystkie.Count} hodowców ({zakresOpis}), kolizje nazwisk: {kolizji}. Dwuklik na wiersz = edycja wstawienia.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"❌ Błąd: {ex.Message}";
                MessageBox.Show("Nie udało się załadować danych:\n" + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ===== POBIERANIE DANYCH =====
        private List<HodowcaWierszVM> PobierzDaneZBazy(int? odRoku)
        {
            string warunekData = odRoku.HasValue
                ? "AND W.DataWstawienia >= @dataOd"
                : "";

            // Pobieramy 2 ostatnie wstawienia per hodowca + JOIN z operators dla wstawienia rn=1
            // (KtoStwo + nazwa — używane do avatara w UI).
            string sql = $@"
                WITH RankedWstawienia AS (
                    SELECT
                        W.Lp,
                        W.Dostawca,
                        W.DataWstawienia,
                        W.IloscWstawienia,
                        W.KtoStwo,
                        W.DataUtw,
                        ROW_NUMBER() OVER (
                            PARTITION BY W.Dostawca
                            ORDER BY W.DataWstawienia DESC, W.Lp DESC
                        ) AS rn,
                        (SELECT COUNT(*) FROM dbo.WstawieniaKurczakow W2
                          WHERE W2.Dostawca = W.Dostawca {warunekData.Replace("W.DataWstawienia", "W2.DataWstawienia")}) AS LiczbaWstawienHodowcy
                    FROM dbo.WstawieniaKurczakow W
                    WHERE 1=1 {warunekData}
                ),
                Top2 AS (
                    SELECT * FROM RankedWstawienia WHERE rn <= 2
                )
                SELECT
                    T.Dostawca,
                    T.Lp           AS WstawienieLp,
                    T.DataWstawienia,
                    T.IloscWstawienia,
                    T.rn,
                    T.LiczbaWstawienHodowcy,
                    CAST(T.KtoStwo AS VARCHAR(20)) AS KtoStwoId,
                    ISNULL(O.Name, '') AS KtoStwoName,
                    T.DataUtw,
                    HD.LP          AS DostawaLp,
                    HD.DataOdbioru,
                    HD.SztukiDek,
                    HD.WagaDek,
                    HD.Cena,
                    HD.Bufor,
                    HD.TypCeny,
                    HD.Auta
                FROM Top2 T
                LEFT JOIN dbo.operators O ON T.KtoStwo = O.ID
                LEFT JOIN dbo.HarmonogramDostaw HD ON HD.LpW = T.Lp
                ORDER BY T.Dostawca, T.rn, HD.DataOdbioru;";

            var map = new Dictionary<string, HodowcaBuilder>(StringComparer.OrdinalIgnoreCase);

            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 })
            {
                if (odRoku.HasValue)
                    cmd.Parameters.AddWithValue("@dataOd", new DateTime(odRoku.Value, 1, 1));

                conn.Open();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string dostawca = r["Dostawca"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(dostawca)) continue;

                    if (!map.TryGetValue(dostawca, out var b))
                    {
                        b = new HodowcaBuilder { Dostawca = dostawca };
                        map[dostawca] = b;
                    }

                    int wstawienieLp = Convert.ToInt32(r["WstawienieLp"]);
                    DateTime dataWst = r["DataWstawienia"] != DBNull.Value
                        ? Convert.ToDateTime(r["DataWstawienia"])
                        : DateTime.MinValue;
                    int ilosc = r["IloscWstawienia"] != DBNull.Value
                        ? Convert.ToInt32(r["IloscWstawienia"])
                        : 0;
                    int rn = Convert.ToInt32(r["rn"]);
                    int liczbaWst = r["LiczbaWstawienHodowcy"] != DBNull.Value
                        ? Convert.ToInt32(r["LiczbaWstawienHodowcy"])
                        : 0;
                    b.LiczbaWstawienHodowcy = liczbaWst;

                    if (!b.WstawieniaMap.ContainsKey(wstawienieLp))
                    {
                        b.WstawieniaMap[wstawienieLp] = new WstawienieAkumulator
                        {
                            Lp = wstawienieLp,
                            Data = dataWst,
                            Ilosc = ilosc,
                            Rn = rn,
                            KtoStwoId = r["KtoStwoId"]?.ToString() ?? "",
                            KtoStwoName = r["KtoStwoName"]?.ToString() ?? "",
                            DataUtw = r["DataUtw"] != DBNull.Value
                                ? Convert.ToDateTime(r["DataUtw"])
                                : (DateTime?)null
                        };
                    }

                    if (r["DostawaLp"] == DBNull.Value) continue;

                    var dostawa = new DostawaInfo
                    {
                        DataOdbioru = r["DataOdbioru"] != DBNull.Value
                            ? Convert.ToDateTime(r["DataOdbioru"])
                            : DateTime.MinValue,
                        SztukiDek = r["SztukiDek"] != DBNull.Value ? Convert.ToInt32(r["SztukiDek"]) : 0,
                        WagaDek = r["WagaDek"] != DBNull.Value ? Convert.ToDecimal(r["WagaDek"]) : 0m,
                        Cena = r["Cena"] != DBNull.Value ? Convert.ToDecimal(r["Cena"]) : 0m,
                        Bufor = r["Bufor"]?.ToString() ?? "",
                        TypCeny = r["TypCeny"]?.ToString() ?? "",
                        Auta = r["Auta"] != DBNull.Value ? Convert.ToInt32(r["Auta"]) : 0,
                        WstawienieLp = wstawienieLp,
                        WstawienieRn = rn
                    };
                    b.WstawieniaMap[wstawienieLp].Dostawy.Add(dostawa);
                }
            }

            var lista = new List<HodowcaWierszVM>(map.Count);
            foreach (var b in map.Values)
            {
                var wstawieniaPosortowane = b.WstawieniaMap.Values
                    .OrderBy(w => w.Data)
                    .ToList();
                if (wstawieniaPosortowane.Count == 0) continue;

                var wszystkieDostawy = wstawieniaPosortowane
                    .SelectMany(w => w.Dostawy)
                    .OrderBy(d => d.DataOdbioru)
                    .ToList();

                var klasyfikacja = Sklasyfikuj(wszystkieDostawy);

                var ostatnie = wstawieniaPosortowane[^1];

                var chipy = new ObservableCollection<DostawaChipVM>(
                    wszystkieDostawy.Select(d => new DostawaChipVM(d)));

                decimal sumaKg = wszystkieDostawy.Sum(d => d.SztukiDek * d.WagaDek);

                var (nazwisko, prefiks) = WyliczNazwisko(b.Dostawca);

                lista.Add(new HodowcaWierszVM
                {
                    Dostawca = b.Dostawca,
                    Nazwisko = nazwisko,
                    NazwiskoPrefiks = prefiks,
                    LiczbaWstawien = b.LiczbaWstawienHodowcy,
                    OstatnieWstawienieData = ostatnie.Data,
                    OstatnieWstawienieLp = ostatnie.Lp,
                    OstatnieWstawienieIlosc = ostatnie.Ilosc,
                    NajwczesniejszeWstawienieData = wstawieniaPosortowane[0].Data,
                    KtoStwoId = ostatnie.KtoStwoId,
                    KtoStwoName = ostatnie.KtoStwoName,
                    DataUtw = ostatnie.DataUtw,
                    DostawyChipy = chipy,
                    SumaWagi = sumaKg,
                    Klasyfikacja = klasyfikacja
                });
            }
            return lista;
        }

        // ===== KLASYFIKACJA =====
        private static GrupaKlasyfikacji Sklasyfikuj(List<DostawaInfo> dostawy)
        {
            if (dostawy.Count == 0) return GrupaKlasyfikacji.Inne;
            if (dostawy.Any(d => JestPotwierdzony(d.Bufor))) return GrupaKlasyfikacji.Aktywny;
            if (dostawy.Any(d => JestDoWykupienia(d.Bufor))) return GrupaKlasyfikacji.DoWykupienia;
            if (dostawy.Any(d => JestAnulowany(d.Bufor))) return GrupaKlasyfikacji.Anulowane;
            return GrupaKlasyfikacji.Inne;
        }

        private static bool JestPotwierdzony(string bufor)
        {
            if (string.IsNullOrEmpty(bufor)) return false;
            return bufor.Equals("Potwierdzony", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("Sprzedany", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("B.Wolny.", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("B.Wolny", StringComparison.OrdinalIgnoreCase)
                || bufor.Equals("B.Kontr.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool JestDoWykupienia(string bufor)
            => !string.IsNullOrEmpty(bufor)
               && bufor.IndexOf("wykupien", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool JestAnulowany(string bufor)
            => !string.IsNullOrEmpty(bufor)
               && bufor.IndexOf("Anulowan", StringComparison.OrdinalIgnoreCase) >= 0;

        // ===== KOLIZJE NAZWISK =====
        // W bazie nazwa hodowcy ma format "Nazwisko Imię" — bierzemy PIERWSZY token jako nazwisko,
        // grupujemy po pierwszych 5 znakach znormalizowanych. To łapie "podobne" nazwiska
        // (Wiankowski / Wiankiewicz → "WIANK"), a nie imiona (Krzysztof / Krzysztofa).
        private const int PrefixLen = 5;

        private static void WykryjKolizjeNazwisk(List<HodowcaWierszVM> lista)
        {
            var indeks = new Dictionary<string, List<HodowcaWierszVM>>(StringComparer.OrdinalIgnoreCase);

            foreach (var v in lista)
            {
                if (string.IsNullOrEmpty(v.NazwiskoPrefiks)) continue;
                if (!indeks.TryGetValue(v.NazwiskoPrefiks, out var grupa))
                {
                    grupa = new List<HodowcaWierszVM>();
                    indeks[v.NazwiskoPrefiks] = grupa;
                }
                grupa.Add(v);
            }

            foreach (var (prefiks, grupa) in indeks)
            {
                var roznePelneNazwy = grupa
                    .GroupBy(v => v.Dostawca.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (roznePelneNazwy.Count < 2) continue;

                foreach (var v in roznePelneNazwy)
                {
                    v.KolizjePartnerzy ??= new List<KolizjaPartner>();

                    foreach (var inny in roznePelneNazwy)
                    {
                        if (ReferenceEquals(v, inny)) continue;
                        if (v.KolizjePartnerzy.Any(p => string.Equals(p.Dostawca, inny.Dostawca, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        v.KolizjePartnerzy.Add(new KolizjaPartner
                        {
                            Dostawca = inny.Dostawca,
                            Token = inny.Nazwisko,
                            Prefiks = prefiks,
                            OstatnieWstawienieData = inny.OstatnieWstawienieData,
                            Klasyfikacja = inny.Klasyfikacja
                        });
                    }
                }
            }
        }

        // Wyciąga nazwisko jako PIERWSZY token (konwencja DB: "Nazwisko Imię"),
        // zwraca też prefiks 5-znakowy do detekcji kolizji.
        private static (string nazwisko, string prefiks) WyliczNazwisko(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("", "");

            var clean = new StringBuilder(fullName.Length);
            foreach (char c in fullName)
                clean.Append(char.IsLetterOrDigit(c) ? c : ' ');

            var rawTokens = clean.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length >= 2)
                .ToList();

            if (rawTokens.Count == 0)
                return ("", "");

            // Bierzemy pierwszy "prawdziwy" token (omijamy generyczne prefiksy biznesowe).
            string? primary = null;
            foreach (var t in rawTokens)
            {
                string norm = NormalizujToken(t);
                if (norm.Length < PrefixLen) continue;
                if (StopWords.Contains(norm)) continue;
                if (norm.All(char.IsDigit)) continue;
                primary = t;
                break;
            }

            if (primary == null) return ("", "");

            string normalizedPrimary = NormalizujToken(primary);
            string prefix = normalizedPrimary.Substring(0, PrefixLen);
            return (primary, prefix);
        }

        private static string NormalizujToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var normalized = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        // ===== FILTROWANIE =====
        private bool FiltrujWiersz(object o)
        {
            if (o is not HodowcaWierszVM v) return false;

            switch (v.Klasyfikacja)
            {
                case GrupaKlasyfikacji.Aktywny: if (btnFilterAktywne?.IsChecked != true) return false; break;
                case GrupaKlasyfikacji.DoWykupienia: if (btnFilterDoWykupienia?.IsChecked != true) return false; break;
                case GrupaKlasyfikacji.Anulowane: if (btnFilterAnulowane?.IsChecked != true) return false; break;
                case GrupaKlasyfikacji.Inne: if (btnFilterInne?.IsChecked != true) return false; break;
            }

            if (btnFilterKolizje?.IsChecked == true && !v.MaKolizje)
                return false;

            string? txt = txtFilter?.Text;
            if (!string.IsNullOrWhiteSpace(txt))
            {
                if (v.Dostawca.IndexOf(txt, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }
            return true;
        }

        private void AktualizujLicznikiGrup()
        {
            int aktywni = _wszystkie.Count(w => w.Klasyfikacja == GrupaKlasyfikacji.Aktywny);
            int doWyk = _wszystkie.Count(w => w.Klasyfikacja == GrupaKlasyfikacji.DoWykupienia);
            int anul = _wszystkie.Count(w => w.Klasyfikacja == GrupaKlasyfikacji.Anulowane);
            int inne = _wszystkie.Count(w => w.Klasyfikacja == GrupaKlasyfikacji.Inne);
            int kolizji = _wszystkie.Count(w => w.MaKolizje);

            lblAktywneCount.Text = $"Aktywni ({aktywni})";
            lblDoWykupieniaCount.Text = $"Do wykupienia ({doWyk})";
            lblAnulowaneCount.Text = $"Anulowane ({anul})";
            lblInneCount.Text = $"Inne ({inne})";
            lblKolizjeFilter.Text = $"Tylko kolizje ({kolizji})";
        }

        // ===== AVATAR (LoadingRow handler) =====
        private void DgHodowcy_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is not HodowcaWierszVM v) return;
            if (string.IsNullOrEmpty(v.KtoStwoId)) return;
            if (_avatarMissingCache.Contains(v.KtoStwoId)) return;

            // Defer: po wyrenderowaniu wiersza znajdujemy Ellipse w wizualnym drzewie.
            Dispatcher.BeginInvoke(new Action(() => UstawAvatarWWierszu(e.Row, v.KtoStwoId)),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UstawAvatarWWierszu(DataGridRow row, string userId)
        {
            try
            {
                if (!_avatarBrushCache.TryGetValue(userId, out var brush))
                {
                    if (!UserAvatarManager.HasAvatar(userId))
                    {
                        _avatarMissingCache.Add(userId);
                        return;
                    }

                    using var avatar = UserAvatarManager.GetAvatarRounded(userId, 44);
                    if (avatar == null)
                    {
                        _avatarMissingCache.Add(userId);
                        return;
                    }
                    brush = new ImageBrush(ConvertToImageSource(avatar))
                    {
                        Stretch = Stretch.UniformToFill
                    };
                    brush.Freeze();
                    _avatarBrushCache[userId] = brush;
                }

                var ellipse = FindVisualChild<Ellipse>(row, "avatarEllipse");
                var fallback = FindVisualChild<Border>(row, "avatarFallback");
                if (ellipse != null && fallback != null)
                {
                    ellipse.Fill = brush;
                    ellipse.Visibility = Visibility.Visible;
                    fallback.Visibility = Visibility.Collapsed;
                }
            }
            catch { /* brak avatara — zostaje fallback z inicjałem */ }
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string? name = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed)
                {
                    if (name == null || (child is FrameworkElement fe && fe.Name == name))
                        return typed;
                }
                var found = FindVisualChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using var memory = new MemoryStream();
            image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            memory.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = memory;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        // ===== HANDLERS =====
        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
            => _widok?.Refresh();

        private void FilterGroup_Click(object sender, RoutedEventArgs e)
            => _widok?.Refresh();

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
            => await ZaladujAsync();

        private async void CbOdRoku_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (cbOdRoku.SelectedItem is not ComboBoxItem item) return;

            string val = item.Content?.ToString() ?? "";
            if (string.Equals(val, "Wszystkie", StringComparison.OrdinalIgnoreCase))
                _odRoku = null;
            else if (int.TryParse(val, out int rok))
                _odRoku = rok;
            else
                return;

            await ZaladujAsync();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e) => Close();

        private void DgHodowcy_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgHodowcy.SelectedItem is not HodowcaWierszVM v) return;
            OtworzEdycjeWstawienia(v.OstatnieWstawienieLp);
        }

        private void OtworzEdycjeWstawienia(int lpWstawienia)
        {
            string dostawca = "";
            DateTime dataWst = DateTime.Today;
            int ilosc = 0;
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT Dostawca, DataWstawienia, IloscWstawienia FROM dbo.WstawieniaKurczakow WHERE Lp = @Lp",
                    conn);
                cmd.Parameters.AddWithValue("@Lp", lpWstawienia);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    dostawca = r["Dostawca"]?.ToString() ?? "";
                    dataWst = r["DataWstawienia"] != DBNull.Value
                        ? Convert.ToDateTime(r["DataWstawienia"]) : DateTime.Today;
                    ilosc = r["IloscWstawienia"] != DBNull.Value
                        ? Convert.ToInt32(r["IloscWstawienia"]) : 0;
                }
                else
                {
                    MessageBox.Show("Nie znaleziono wstawienia w bazie.", "Uwaga",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania danych wstawienia:\n" + ex.Message,
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var okno = new WstawienieWindow
            {
                UserID = App.UserID,
                Dostawca = dostawca,
                LpWstawienia = lpWstawienia,
                DataWstawienia = dataWst,
                SztWstawienia = ilosc,
                Modyfikacja = true,
                Owner = this
            };

            if (okno.ShowDialog() == true)
            {
                _ = ZaladujAsync();
            }
        }
    }

    // ===== VIEW MODELS =====
    public enum GrupaKlasyfikacji
    {
        Aktywny,
        DoWykupienia,
        Anulowane,
        Inne
    }

    internal class HodowcaBuilder
    {
        public string Dostawca { get; set; } = "";
        public int LiczbaWstawienHodowcy { get; set; }
        public Dictionary<int, WstawienieAkumulator> WstawieniaMap { get; } = new();
    }

    internal class WstawienieAkumulator
    {
        public int Lp { get; set; }
        public DateTime Data { get; set; }
        public int Ilosc { get; set; }
        public int Rn { get; set; }
        public string KtoStwoId { get; set; } = "";
        public string KtoStwoName { get; set; } = "";
        public DateTime? DataUtw { get; set; }
        public List<DostawaInfo> Dostawy { get; } = new();
    }

    public class DostawaInfo
    {
        public DateTime DataOdbioru { get; set; }
        public int SztukiDek { get; set; }
        public decimal WagaDek { get; set; }
        public decimal Cena { get; set; }
        public string Bufor { get; set; } = "";
        public string TypCeny { get; set; } = "";
        public int Auta { get; set; }
        public int WstawienieLp { get; set; }
        public int WstawienieRn { get; set; }
    }

    public class KolizjaPartner
    {
        public string Dostawca { get; set; } = "";
        public string Token { get; set; } = "";
        public string Prefiks { get; set; } = "";
        public DateTime OstatnieWstawienieData { get; set; }
        public GrupaKlasyfikacji Klasyfikacja { get; set; }

        public string StatusIkonka => Klasyfikacja switch
        {
            GrupaKlasyfikacji.Aktywny => "✅",
            GrupaKlasyfikacji.DoWykupienia => "🟡",
            GrupaKlasyfikacji.Anulowane => "🚫",
            _ => "⚪"
        };

        public string TooltipFull
        {
            get
            {
                string status = Klasyfikacja switch
                {
                    GrupaKlasyfikacji.Aktywny => "Aktywny",
                    GrupaKlasyfikacji.DoWykupienia => "Do wykupienia",
                    GrupaKlasyfikacji.Anulowane => "Anulowany",
                    _ => "Inne / brak dostaw"
                };
                string data = OstatnieWstawienieData == DateTime.MinValue
                    ? "(brak)"
                    : OstatnieWstawienieData.ToString("yyyy-MM-dd");
                return $"Podobny hodowca (kolizja nazwiska, prefiks {Prefiks}):\n{Dostawca}\nNazwisko: {Token}\nOstatnie wstawienie: {data}\nStatus: {status}";
            }
        }
    }

    public class HodowcaWierszVM
    {
        public string Dostawca { get; set; } = "";
        public string Nazwisko { get; set; } = "";
        public string NazwiskoPrefiks { get; set; } = "";

        public int LiczbaWstawien { get; set; }
        public DateTime OstatnieWstawienieData { get; set; }
        public DateTime NajwczesniejszeWstawienieData { get; set; }
        public int OstatnieWstawienieLp { get; set; }
        public int OstatnieWstawienieIlosc { get; set; }
        public decimal SumaWagi { get; set; }
        public GrupaKlasyfikacji Klasyfikacja { get; set; }
        public ObservableCollection<DostawaChipVM> DostawyChipy { get; set; } = new();

        // ===== AVATAR =====
        public string KtoStwoId { get; set; } = "";
        public string KtoStwoName { get; set; } = "";
        public DateTime? DataUtw { get; set; }

        public string KtoStwoInicjal
        {
            get
            {
                if (string.IsNullOrWhiteSpace(KtoStwoName)) return "?";
                var parts = KtoStwoName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "?";
                if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
                return (parts[0][0].ToString() + parts[1][0]).ToUpper();
            }
        }

        // Deterministyczny kolor fallbacku z hash KtoStwoId (paleta firmowa, czytelna).
        public Brush KtoStwoFallbackColor
        {
            get
            {
                Color[] paleta =
                {
                    Color.FromRgb(0x5C, 0x8A, 0x3A), // zielony firmowy
                    Color.FromRgb(0x34, 0x98, 0xDB), // niebieski
                    Color.FromRgb(0xE6, 0x7E, 0x22), // pomarańcz
                    Color.FromRgb(0x9B, 0x59, 0xB6), // fiolet
                    Color.FromRgb(0x16, 0xA0, 0x85), // turkus
                    Color.FromRgb(0xE7, 0x4C, 0x3C), // czerwony
                    Color.FromRgb(0xF3, 0x9C, 0x12), // żółto-pomarańcz
                    Color.FromRgb(0x2C, 0x3E, 0x50)  // grafit
                };
                if (string.IsNullOrEmpty(KtoStwoId)) return new SolidColorBrush(paleta[^1]);
                int sum = 0;
                foreach (char c in KtoStwoId) sum = sum * 31 + c;
                return new SolidColorBrush(paleta[Math.Abs(sum) % paleta.Length]);
            }
        }

        public string AvatarTooltip
        {
            get
            {
                if (string.IsNullOrWhiteSpace(KtoStwoName) && string.IsNullOrEmpty(KtoStwoId))
                    return "Wstawienie utworzone — autor nieznany";
                string osoba = string.IsNullOrWhiteSpace(KtoStwoName) ? $"ID {KtoStwoId}" : KtoStwoName;
                string kiedy = DataUtw.HasValue ? $"\nUtworzono: {DataUtw:yyyy-MM-dd HH:mm}" : "";
                return $"Ostatnie wstawienie utworzył:\n{osoba}{kiedy}";
            }
        }

        public List<KolizjaPartner>? KolizjePartnerzy { get; set; }
        public bool MaKolizje => KolizjePartnerzy != null && KolizjePartnerzy.Count > 0;
        public int MaKolizjeSortKey => MaKolizje ? 0 : 1;

        public string NazwiskoDisplay => string.IsNullOrEmpty(Nazwisko) ? "—" : Nazwisko;

        public int GroupSortKey => Klasyfikacja switch
        {
            GrupaKlasyfikacji.Aktywny => 0,
            GrupaKlasyfikacji.DoWykupienia => 1,
            GrupaKlasyfikacji.Anulowane => 2,
            _ => 3
        };

        public string GroupName => Klasyfikacja switch
        {
            GrupaKlasyfikacji.Aktywny => "Aktywni — z 2 ostatnich wstawień jest dostawa Potwierdzona",
            GrupaKlasyfikacji.DoWykupienia => "Do wykupienia",
            GrupaKlasyfikacji.Anulowane => "Anulowane",
            _ => "Inne / brak dostaw"
        };

        public string GroupShortName => Klasyfikacja switch
        {
            GrupaKlasyfikacji.Aktywny => "AKTYWNY",
            GrupaKlasyfikacji.DoWykupienia => "DO WYKUP.",
            GrupaKlasyfikacji.Anulowane => "ANULOWANY",
            _ => "INNE"
        };

        public string GroupIcon => Klasyfikacja switch
        {
            GrupaKlasyfikacji.Aktywny => "✅",
            GrupaKlasyfikacji.DoWykupienia => "🟡",
            GrupaKlasyfikacji.Anulowane => "🚫",
            _ => "⚪"
        };

        public Brush GroupColor => Klasyfikacja switch
        {
            GrupaKlasyfikacji.Aktywny => new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),
            GrupaKlasyfikacji.DoWykupienia => new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)),
            GrupaKlasyfikacji.Anulowane => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
            _ => new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6))
        };

        public Visibility KolizjaVisibility => MaKolizje ? Visibility.Visible : Visibility.Collapsed;
        public Brush KolizjaBackground => MaKolizje
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1))
            : Brushes.Transparent;

        public string KolizjaTooltip
        {
            get
            {
                if (!MaKolizje) return "";
                var sb = new StringBuilder();
                sb.AppendLine("⚠️ Kolizja nazwiska — w bazie są inni hodowcy z tym samym nazwiskiem:");
                sb.AppendLine();
                foreach (var p in KolizjePartnerzy!.OrderBy(x => x.Dostawca))
                {
                    string status = p.Klasyfikacja switch
                    {
                        GrupaKlasyfikacji.Aktywny => "✅ Aktywny",
                        GrupaKlasyfikacji.DoWykupienia => "🟡 Do wykup.",
                        GrupaKlasyfikacji.Anulowane => "🚫 Anul.",
                        _ => "⚪ Inne"
                    };
                    string data = p.OstatnieWstawienieData == DateTime.MinValue
                        ? "?" : p.OstatnieWstawienieData.ToString("yyyy-MM-dd");
                    sb.AppendLine($"  • {p.Dostawca}   [{status}, ost. {data}]");
                }
                sb.AppendLine();
                sb.AppendLine("Wspólne nazwisko: " +
                    string.Join(", ", KolizjePartnerzy!.Select(p => p.Token).Distinct(StringComparer.OrdinalIgnoreCase)));
                return sb.ToString().TrimEnd();
            }
        }
    }

    public class DostawaChipVM
    {
        private static readonly string[] DniSkrot = { "ndz", "pon", "wt", "śr", "czw", "pt", "sob" };

        private readonly DostawaInfo _d;
        public DostawaChipVM(DostawaInfo d) { _d = d; }

        public string DataKrotka
        {
            get
            {
                if (_d.DataOdbioru == DateTime.MinValue) return "—";
                return $"{DniSkrot[(int)_d.DataOdbioru.DayOfWeek]} {_d.DataOdbioru:dd.MM}";
            }
        }
        public string StatusKrotki => string.IsNullOrEmpty(_d.Bufor) ? "—" : SkrocStatus(_d.Bufor);

        // Druga linia chipa: dokładne dane dostawy (sztuki, ø waga, cena, typcena, auta)
        public string SzczegolyKrotkie
        {
            get
            {
                var parts = new List<string>(5);
                if (_d.SztukiDek > 0) parts.Add($"{_d.SztukiDek:#,0} szt");
                if (_d.WagaDek > 0) parts.Add($"ø{_d.WagaDek:0.00} kg");
                if (_d.Cena > 0) parts.Add($"{_d.Cena:0.00} zł");
                string typ = SkrocTypCeny(_d.TypCeny);
                if (!string.IsNullOrEmpty(typ)) parts.Add(typ);
                if (_d.Auta > 0) parts.Add($"{_d.Auta} aut");
                return parts.Count == 0 ? "(brak danych)" : string.Join(" · ", parts);
            }
        }

        public string Tooltip =>
            $"Dostawa: {(_d.DataOdbioru == DateTime.MinValue ? "?" : _d.DataOdbioru.ToString("yyyy-MM-dd (dddd)", new CultureInfo("pl-PL")))}\n" +
            $"Status (Bufor): {(_d.Bufor == "" ? "(brak)" : _d.Bufor)}\n" +
            $"Sztuki deklarowane: {_d.SztukiDek:#,0}\n" +
            $"Średnia waga: {_d.WagaDek:0.00} kg/szt\n" +
            $"Razem masa: {_d.SztukiDek * _d.WagaDek:#,0} kg\n" +
            $"Cena: {_d.Cena:0.00} zł\n" +
            $"Typ ceny: {(string.IsNullOrEmpty(_d.TypCeny) ? "(brak)" : _d.TypCeny)}\n" +
            $"Liczba aut: {_d.Auta}\n" +
            $"(z wstawienia rn={_d.WstawienieRn}, Lp={_d.WstawienieLp})";

        private static string SkrocTypCeny(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            string l = t.ToLowerInvariant();
            if (l.Contains("wolny")) return "wol.";
            if (l.Contains("kontr")) return "kontr.";
            if (l.Contains("minister") || l == "min") return "min.";
            if (l.Contains("rolnic")) return "rol.";
            if (l.Contains("łącz") || l.Contains("lacz")) return "łącz.";
            return t.Length > 6 ? t.Substring(0, 6) + "." : t;
        }

        public Brush ChipBackground => Kolory().bg;
        public Brush ChipForeground => Kolory().fg;
        public Brush ChipBorder => Kolory().border;

        private (Brush bg, Brush fg, Brush border) Kolory()
        {
            string b = _d.Bufor ?? "";
            if (b.Equals("Potwierdzony", StringComparison.OrdinalIgnoreCase))
                return (Brush(0xE8, 0xF5, 0xE9), Brush(0x1B, 0x5E, 0x20), Brush(0x81, 0xC7, 0x84));
            if (b.Equals("Sprzedany", StringComparison.OrdinalIgnoreCase))
                return (Brush(0xE0, 0xF2, 0xF1), Brush(0x00, 0x69, 0x5C), Brush(0x4D, 0xB6, 0xAC));
            if (b.IndexOf("wykupien", StringComparison.OrdinalIgnoreCase) >= 0)
                return (Brush(0xFF, 0xF3, 0xE0), Brush(0xE6, 0x5C, 0x00), Brush(0xFF, 0xB7, 0x4D));
            if (b.IndexOf("Anulowan", StringComparison.OrdinalIgnoreCase) >= 0)
                return (Brush(0xFF, 0xEB, 0xEE), Brush(0xB7, 0x1C, 0x1C), Brush(0xE5, 0x73, 0x73));
            if (b.Equals("B.Wolny.", StringComparison.OrdinalIgnoreCase) || b.Equals("B.Wolny", StringComparison.OrdinalIgnoreCase))
                return (Brush(0xE3, 0xF2, 0xFD), Brush(0x0D, 0x47, 0xA1), Brush(0x64, 0xB5, 0xF6));
            if (b.Equals("B.Kontr.", StringComparison.OrdinalIgnoreCase))
                return (Brush(0xF3, 0xE5, 0xF5), Brush(0x4A, 0x14, 0x8C), Brush(0xBA, 0x68, 0xC8));
            return (Brush(0xEC, 0xEF, 0xF1), Brush(0x37, 0x47, 0x4F), Brush(0xCF, 0xD8, 0xDC));
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

        private static string SkrocStatus(string s)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            return s switch
            {
                "Potwierdzony" => "Potw.",
                "Anulowany" => "Anul.",
                "Sprzedany" => "Sprz.",
                "Do wykupienia" => "Do wyk.",
                "B.Wolny." => "B.Wol.",
                "B.Kontr." => "B.Kon.",
                _ => s
            };
        }
    }
}
