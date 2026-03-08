using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Reklamacje
{
    public partial class FormSzczegolyReklamacjiWindow : Window
    {
        private readonly string connectionString;
        private readonly int idReklamacji;
        private readonly string userId;
        private int idDokumentu;

        private const string HandelConnString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private ObservableCollection<TowarSzczegoly> towary = new ObservableCollection<TowarSzczegoly>();
        private ObservableCollection<ZdjecieViewModel> zdjecia = new ObservableCollection<ZdjecieViewModel>();
        private ObservableCollection<HistoriaViewModel> historia = new ObservableCollection<HistoriaViewModel>();
        private ObservableCollection<PartiaViewModel> partie = new ObservableCollection<PartiaViewModel>();

        public bool StatusZmieniony { get; private set; }

        public FormSzczegolyReklamacjiWindow(string connString, int reklamacjaId, string user)
        {
            connectionString = connString;
            idReklamacji = reklamacjaId;
            userId = user;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            Title = $"Szczegoly reklamacji #{idReklamacji}";
            txtHeaderTitle.Text = $"REKLAMACJA #{idReklamacji}";

            dgTowary.ItemsSource = towary;
            icPartie.ItemsSource = partie;
            icHistoria.ItemsSource = historia;
            lbThumbnails.ItemsSource = zdjecia;

            Loaded += (s, e) => WczytajSzczegoly();
        }

        // ========================================
        // WCZYTYWANIE DANYCH
        // ========================================

        private void WczytajSzczegoly()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    WczytajPodstawoweInfo(conn);
                    WczytajTowary(conn);
                    WczytajPartie(conn);
                    WczytajZdjecia(conn);
                    WczytajHistorie(conn);
                }
                WybierzPierwszyTab();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania szczegolow:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string aktualnyStatus = "";

        private void WczytajPodstawoweInfo(SqlConnection conn)
        {
            try
            {
                string query = @"
                    SELECT r.*,
                        ISNULL(o1.Name, r.UserID) AS ZglaszajacyNazwa,
                        ISNULL(o2.Name, r.OsobaRozpatrujaca) AS RozpatrujacyNazwa
                    FROM [dbo].[Reklamacje] r
                    LEFT JOIN [dbo].[operators] o1 ON r.UserID = o1.ID
                    LEFT JOIN [dbo].[operators] o2 ON r.OsobaRozpatrujaca = o2.ID
                    WHERE r.Id = @Id";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string status = SafeGet(reader, "Status");
                            aktualnyStatus = status;
                            string numerDok = SafeGet(reader, "NumerDokumentu");
                            string nazwaKontr = SafeGet(reader, "NazwaKontrahenta");
                            string opis = SafeGet(reader, "Opis");
                            string komentarz = SafeGet(reader, "Komentarz");
                            string rozwiazanie = SafeGet(reader, "Rozwiazanie");
                            string zglaszajacyNazwa = SafeGet(reader, "ZglaszajacyNazwa");
                            string rozpatrujacyNazwa = SafeGet(reader, "RozpatrujacyNazwa");
                            string typRekl = SafeGet(reader, "TypReklamacji");
                            string priorytet = SafeGet(reader, "Priorytet");
                            decimal sumaKg = SafeGetDecimal(reader, "SumaKg");
                            decimal sumaWartosc = SafeGetDecimal(reader, "SumaWartosc");
                            DateTime dataZgl = SafeGetDateTime(reader, "DataZgloszenia");
                            DateTime? dataZamkn = SafeGetNullableDateTime(reader, "DataZamkniecia");
                            string przyczynaGlowna = SafeGet(reader, "PrzyczynaGlowna");
                            string akcjeNaprawcze = SafeGet(reader, "AkcjeNaprawcze");

                            // Header
                            txtHeaderSubtitle.Text = $"{numerDok}  |  {nazwaKontr}";
                            txtHeaderData.Text = dataZgl.ToString("dd.MM.yyyy HH:mm");

                            if (dataZamkn.HasValue)
                            {
                                panelHeaderZamkniecia.Visibility = Visibility.Visible;
                                txtHeaderZamkniecia.Text = dataZamkn.Value.ToString("dd.MM.yyyy HH:mm");
                            }

                            // Status badge
                            UstawStatusBadge(status);

                            // Priorytet badge
                            UstawPriorytetBadge(priorytet);

                            string zglaszajacyId = SafeGet(reader, "UserID");
                            string rozpatrujacyId = SafeGet(reader, "OsobaRozpatrujaca");

                            // Avatary w headerze
                            UstawAvatar(avatarZglaszajacyHeader, txtAvatarZglaszajacyHeader, zglaszajacyNazwa);
                            txtAvatarZglaszajacyNazwa.Text = zglaszajacyNazwa;
                            UstawAvatarPhoto(zglaszajacyId, zglaszajacyNazwa, 80,
                                imgBrushZglaszajacyHeader, ellipseAvatarZglaszajacyHeader);
                            if (!string.IsNullOrEmpty(rozpatrujacyNazwa))
                            {
                                UstawAvatar(avatarRozpatrujacyHeader, txtAvatarRozpatrujacyHeader, rozpatrujacyNazwa);
                                txtAvatarRozpatrujacyNazwa.Text = rozpatrujacyNazwa;
                                UstawAvatarPhoto(rozpatrujacyId, rozpatrujacyNazwa, 80,
                                    imgBrushRozpatrujacyHeader, ellipseAvatarRozpatrujacyHeader);
                            }

                            // Karty informacyjne
                            txtNumerDokumentu.Text = numerDok;
                            txtIdDokumentu.Text = $"ID: {SafeGet(reader, "IdDokumentu")}";
                            string idDokStr = SafeGet(reader, "IdDokumentu");
                            if (!string.IsNullOrEmpty(idDokStr) && int.TryParse(idDokStr, out int parsedIdDok))
                                idDokumentu = parsedIdDok;
                            txtNazwaKontrahenta.Text = nazwaKontr;
                            txtIdKontrahenta.Text = $"ID: {SafeGet(reader, "IdKontrahenta")}";
                            txtSumaKg.Text = sumaKg.ToString("#,##0.00");
                            txtSumaWartosc.Text = sumaWartosc.ToString("#,##0.00");

                            // Avatary w karcie Odpowiedzialnosc
                            txtZglaszajacy.Text = zglaszajacyNazwa;
                            UstawAvatar(avatarZglaszajacyCard, txtAvatarZglaszajacyCard, zglaszajacyNazwa);
                            UstawAvatarPhoto(zglaszajacyId, zglaszajacyNazwa, 52,
                                imgBrushZglaszajacyCard, ellipseAvatarZglaszajacyCard);
                            if (!string.IsNullOrEmpty(rozpatrujacyNazwa))
                            {
                                txtRozpatrujacy.Text = rozpatrujacyNazwa;
                                UstawAvatar(avatarRozpatrujacyCard, txtAvatarRozpatrujacyCard, rozpatrujacyNazwa);
                                UstawAvatarPhoto(rozpatrujacyId, rozpatrujacyNazwa, 52,
                                    imgBrushRozpatrujacyCard, ellipseAvatarRozpatrujacyCard);
                            }
                            else
                            {
                                txtRozpatrujacy.Text = "-";
                            }
                            txtTypReklamacji.Text = string.IsNullOrEmpty(typRekl) ? "Inne" : typRekl;

                            // Opis
                            txtOpis.Text = string.IsNullOrWhiteSpace(opis) ? "(brak opisu)" : opis;

                            // Komentarz
                            if (!string.IsNullOrWhiteSpace(komentarz))
                            {
                                sectionKomentarz.Visibility = Visibility.Visible;
                                txtKomentarz.Text = komentarz;
                            }

                            // Rozwiazanie
                            if (!string.IsNullOrWhiteSpace(rozwiazanie))
                            {
                                sectionRozwiazanie.Visibility = Visibility.Visible;
                                txtRozwiazanie.Text = rozwiazanie;
                            }

                            // Rozpatrzenie
                            if (!string.IsNullOrWhiteSpace(przyczynaGlowna) || !string.IsNullOrWhiteSpace(akcjeNaprawcze))
                            {
                                sectionRozpatrzenie.Visibility = Visibility.Visible;
                                txtPrzyczynaGlowna.Text = string.IsNullOrWhiteSpace(przyczynaGlowna) ? "(nie podano)" : przyczynaGlowna;
                                txtAkcjeNaprawcze.Text = string.IsNullOrWhiteSpace(akcjeNaprawcze) ? "(nie podano)" : akcjeNaprawcze;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtOpis.Text = $"Blad wczytywania: {ex.Message}";
            }
        }

        private void WczytajTowary(SqlConnection conn)
        {
            try
            {
                towary.Clear();

                // 1. Read cached items from LibraNet
                using (var cmd = new SqlCommand(
                    "SELECT IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc FROM [dbo].[ReklamacjeTowary] WHERE IdReklamacji = @Id ORDER BY Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        int lp = 1;
                        while (reader.Read())
                        {
                            towary.Add(new TowarSzczegoly
                            {
                                Lp = lp++,
                                Symbol = reader["Symbol"] != DBNull.Value ? reader["Symbol"].ToString() : "",
                                Nazwa = reader["Nazwa"] != DBNull.Value ? reader["Nazwa"].ToString() : "",
                                Waga = reader["Waga"] != DBNull.Value ? Convert.ToDecimal(reader["Waga"]) : 0,
                                Cena = reader["Cena"] != DBNull.Value ? Convert.ToDecimal(reader["Cena"]) : 0,
                                Wartosc = reader["Wartosc"] != DBNull.Value ? Convert.ToDecimal(reader["Wartosc"]) : 0
                            });
                        }
                    }
                }

                // 2. If we have IdDokumentu, re-fetch from HANDEL for correct GROUP BY values
                if (idDokumentu > 0)
                {
                    var towaryHandel = WczytajTowaryZHandel(idDokumentu);
                    if (towaryHandel.Count > 0)
                    {
                        if (towary.Count > 0)
                            UsunStareTowary(conn);
                        towary.Clear();
                        int lp = 1;
                        foreach (var t in towaryHandel)
                        {
                            towary.Add(new TowarSzczegoly
                            {
                                Lp = lp++,
                                Symbol = t.Symbol,
                                Nazwa = t.Nazwa,
                                Waga = t.Waga,
                                Cena = t.Cena,
                                Wartosc = t.Wartosc
                            });
                        }
                        ZapiszTowaryDoLibraNet(conn, towaryHandel);
                    }
                }

                WyswietlPodsumowanieTowary();
            }
            catch { }
        }

        private List<TowarSzczegoly> WczytajTowaryZHandel(int idDok)
        {
            var result = new List<TowarSzczegoly>();
            try
            {
                using (var conn = new SqlConnection(HandelConnString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT DP.kod AS Symbol,
                               ISNULL(TW.nazwa, TW.kod) AS Nazwa,
                               ABS(SUM(ISNULL(DP.ilosc, 0))) AS Waga,
                               MAX(ABS(ISNULL(DP.cena, 0))) AS Cena,
                               ABS(SUM(ISNULL(DP.ilosc, 0) * ISNULL(DP.cena, 0))) AS Wartosc
                        FROM [HANDEL].[HM].[DP] DP
                        LEFT JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.ID
                        WHERE DP.super = @IdDok
                        GROUP BY DP.kod, TW.nazwa, TW.kod
                        HAVING ABS(SUM(ISNULL(DP.ilosc, 0))) > 0.01
                        ORDER BY MIN(DP.lp)", conn))
                    {
                        cmd.Parameters.AddWithValue("@IdDok", idDok);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new TowarSzczegoly
                                {
                                    Symbol = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    Nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    Waga = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                                    Cena = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)),
                                    Wartosc = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4))
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private void UsunStareTowary(SqlConnection conn)
        {
            try
            {
                using (var cmd = new SqlCommand("DELETE FROM [dbo].[ReklamacjeTowary] WHERE IdReklamacji = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void ZapiszTowaryDoLibraNet(SqlConnection conn, List<TowarSzczegoly> items)
        {
            try
            {
                foreach (var t in items)
                {
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [dbo].[ReklamacjeTowary]
                        (IdReklamacji, IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc, PrzyczynaReklamacji)
                        VALUES (@IdRek, 0, @Symbol, @Nazwa, @Waga, @Cena, @Wartosc, 'Korekta')", conn))
                    {
                        cmd.Parameters.AddWithValue("@IdRek", idReklamacji);
                        cmd.Parameters.AddWithValue("@Symbol", t.Symbol ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Nazwa", t.Nazwa ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Waga", t.Waga);
                        cmd.Parameters.AddWithValue("@Cena", t.Cena);
                        cmd.Parameters.AddWithValue("@Wartosc", t.Wartosc);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        private void WyswietlPodsumowanieTowary()
        {
            txtTowaryCount.Text = $"({towary.Count})";
            decimal sumaKg = towary.Sum(t => t.Waga);
            decimal sumaWart = towary.Sum(t => t.Wartosc);
            txtTowarySumaKg.Text = $"{sumaKg:#,##0.00} kg";
            txtTowarySumaWartosc.Text = $"{sumaWart:#,##0.00} zl";

            if (towary.Count > 0)
            {
                decimal sredniaCena = sumaKg > 0 ? sumaWart / sumaKg : 0;
                txtTowarySummaryInfo.Text = $"{towary.Count} pozycji  |  {sumaKg:#,##0.00} kg  |  {sumaWart:#,##0.00} zl  |  sr. {sredniaCena:#,##0.00} zl/kg";
                borderTowarySummary.Visibility = Visibility.Visible;
            }
        }

        private void WczytajPartie(SqlConnection conn)
        {
            try
            {
                partie.Clear();
                using (var cmd = new SqlCommand(
                    "SELECT NumerPartii, CustomerName, DataDodania FROM [dbo].[ReklamacjePartie] WHERE IdReklamacji = @Id ORDER BY DataDodania DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string numer = reader["NumerPartii"] != DBNull.Value ? reader["NumerPartii"].ToString() : "";
                            string dostawca = reader["CustomerName"] != DBNull.Value ? reader["CustomerName"].ToString() : "";
                            DateTime? data = reader["DataDodania"] != DBNull.Value ? Convert.ToDateTime(reader["DataDodania"]) : (DateTime?)null;

                            if (string.IsNullOrEmpty(numer) && !string.IsNullOrEmpty(dostawca))
                                numer = dostawca;

                            if (!string.IsNullOrEmpty(numer))
                            {
                                partie.Add(new PartiaViewModel
                                {
                                    NumerPartii = numer,
                                    Dostawca = dostawca,
                                    DataDodania = data
                                });
                            }
                        }
                    }
                }

                if (partie.Count > 0)
                {
                    sectionPartie.Visibility = Visibility.Visible;
                    txtPartieCount.Text = $"({partie.Count})";
                }
            }
            catch { }
        }

        private void WczytajZdjecia(SqlConnection conn)
        {
            try
            {
                zdjecia.Clear();

                bool maBlob = false;
                try
                {
                    using (var cmdCheck = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ReklamacjeZdjecia' AND COLUMN_NAME='DaneZdjecia'", conn))
                    {
                        maBlob = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
                    }
                }
                catch { }

                string query = maBlob
                    ? "SELECT Id, NazwaPliku, SciezkaPliku, DaneZdjecia FROM [dbo].[ReklamacjeZdjecia] WHERE IdReklamacji = @Id ORDER BY DataDodania"
                    : "SELECT Id, NazwaPliku, SciezkaPliku FROM [dbo].[ReklamacjeZdjecia] WHERE IdReklamacji = @Id ORDER BY DataDodania";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var vm = new ZdjecieViewModel();
                            vm.Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0;
                            vm.NazwaPliku = reader["NazwaPliku"] != DBNull.Value ? reader["NazwaPliku"].ToString() : "";
                            vm.SciezkaPliku = reader["SciezkaPliku"] != DBNull.Value ? reader["SciezkaPliku"].ToString() : "";

                            if (maBlob && reader["DaneZdjecia"] != DBNull.Value)
                                vm.DaneZdjecia = (byte[])reader["DaneZdjecia"];

                            // Wygeneruj miniature
                            vm.Miniatura = LoadBitmapImage(vm, 150);

                            if (vm.Miniatura != null)
                                zdjecia.Add(vm);
                        }
                    }
                }

                if (zdjecia.Count > 0)
                {
                    sectionZdjecia.Visibility = Visibility.Visible;
                    txtZdjeciaCount.Text = $"({zdjecia.Count})";
                    lbThumbnails.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void WczytajHistorie(SqlConnection conn)
        {
            try
            {
                historia.Clear();
                using (var cmd = new SqlCommand(@"
                    SELECT h.DataZmiany, h.PoprzedniStatus, h.StatusNowy,
                           h.UserID, ISNULL(o.Name, h.UserID) AS UzytkownikNazwa, h.Komentarz
                    FROM [dbo].[ReklamacjeHistoria] h
                    LEFT JOIN [dbo].[operators] o ON h.UserID = o.ID
                    WHERE h.IdReklamacji = @Id
                    ORDER BY h.DataZmiany DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string nazwa = reader["UzytkownikNazwa"] != DBNull.Value ? reader["UzytkownikNazwa"].ToString() : "";
                            string odbiorcaId = reader["UserID"] != DBNull.Value ? reader["UserID"].ToString() : "";
                            var avatarPhoto = FormRozpatrzenieWindow.LoadWpfAvatar(odbiorcaId, nazwa, 60);
                            historia.Add(new HistoriaViewModel
                            {
                                DataZmiany = reader["DataZmiany"] != DBNull.Value ? Convert.ToDateTime(reader["DataZmiany"]) : DateTime.MinValue,
                                PoprzedniStatus = reader["PoprzedniStatus"] != DBNull.Value ? reader["PoprzedniStatus"].ToString() : "",
                                StatusNowy = reader["StatusNowy"] != DBNull.Value ? reader["StatusNowy"].ToString() : "",
                                Uzytkownik = nazwa,
                                Komentarz = reader["Komentarz"] != DBNull.Value ? reader["Komentarz"].ToString() : "",
                                Inicjaly = FormRozpatrzenieWindow.GetInitials(nazwa),
                                AvatarColor = FormRozpatrzenieWindow.GetAvatarBrush(nazwa),
                                AvatarPhoto = avatarPhoto
                            });
                        }
                    }
                }

                if (historia.Count > 0)
                {
                    sectionHistoria.Visibility = Visibility.Visible;
                    txtHistoriaCount.Text = $"({historia.Count})";
                }
            }
            catch { }
        }

        // ========================================
        // UI HELPERS
        // ========================================

        private void UstawStatusBadge(string status)
        {
            txtStatusBadge.Text = status ?? "Nieznany";
            string hex = FormRozpatrzenieWindow.GetStatusColor(status ?? "");
            var bg = (Color)ColorConverter.ConvertFromString(hex);
            badgeStatus.Background = new SolidColorBrush(bg);
            txtStatusBadge.Foreground = Brushes.White;
        }

        private void UstawPriorytetBadge(string priorytet)
        {
            txtPriorytet.Text = priorytet ?? "Normalny";
            Color bg, fg;

            switch (priorytet)
            {
                case "Niski":
                    bg = (Color)ColorConverter.ConvertFromString("#D5F5E3");
                    fg = (Color)ColorConverter.ConvertFromString("#1E8449");
                    break;
                case "Wysoki":
                    bg = (Color)ColorConverter.ConvertFromString("#FDEBD0");
                    fg = (Color)ColorConverter.ConvertFromString("#E67E22");
                    break;
                case "Krytyczny":
                    bg = (Color)ColorConverter.ConvertFromString("#FADBD8");
                    fg = (Color)ColorConverter.ConvertFromString("#C0392B");
                    break;
                default: // Normalny
                    bg = (Color)ColorConverter.ConvertFromString("#EBF5FB");
                    fg = (Color)ColorConverter.ConvertFromString("#2980B9");
                    break;
            }

            badgePriorytet.Background = new SolidColorBrush(bg);
            txtPriorytet.Foreground = new SolidColorBrush(fg);
        }

        // ========================================
        // ZDJECIA
        // ========================================

        private BitmapImage LoadBitmapImage(ZdjecieViewModel vm, int? decodeWidth = null)
        {
            try
            {
                if (vm.DaneZdjecia != null && vm.DaneZdjecia.Length > 0)
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = new MemoryStream(vm.DaneZdjecia);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    if (decodeWidth.HasValue) bi.DecodePixelWidth = decodeWidth.Value;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }

                if (!string.IsNullOrEmpty(vm.SciezkaPliku) && File.Exists(vm.SciezkaPliku))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(vm.SciezkaPliku);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    if (decodeWidth.HasValue) bi.DecodePixelWidth = decodeWidth.Value;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch { }
            return null;
        }

        private void Thumbnail_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbThumbnails.SelectedItem is ZdjecieViewModel vm)
            {
                var fullImage = LoadBitmapImage(vm);
                imgPreview.Source = fullImage;
                txtNoPhoto.Visibility = fullImage != null ? Visibility.Collapsed : Visibility.Visible;
                txtKliknijPowieksz.Visibility = fullImage != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (imgPreview.Source != null)
            {
                var podglad = new Window
                {
                    Title = "Podglad zdjecia - kliknij lub ESC aby zamknac",
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.None,
                    Background = Brushes.Black,
                    Cursor = Cursors.Hand
                };

                var img = new Image
                {
                    Source = imgPreview.Source,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(20)
                };

                img.MouseLeftButtonDown += (s, args) => podglad.Close();
                podglad.KeyDown += (s, args) =>
                {
                    if (args.Key == Key.Escape || args.Key == Key.Enter || args.Key == Key.Space)
                        podglad.Close();
                };

                podglad.Content = img;
                podglad.ShowDialog();
            }
        }

        // ========================================
        // PRZYCISKI
        // ========================================

        private void BtnRozpatrz_Click(object sender, RoutedEventArgs e)
        {
            var window = new FormRozpatrzenieWindow(connectionString, idReklamacji, aktualnyStatus, userId);
            window.Owner = this;
            if (window.ShowDialog() == true && window.Zapisano)
            {
                StatusZmieniony = true;
                ResetujSekcje();
                WczytajSzczegoly();
            }
        }

        private void BtnZmienStatus_Click(object sender, RoutedEventArgs e)
        {
            using (var formZmiana = new FormZmianaStatusu(connectionString, idReklamacji, "", userId))
            {
                if (formZmiana.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    StatusZmieniony = true;
                    ResetujSekcje();
                    WczytajSzczegoly();
                }
            }
        }

        private void ResetujSekcje()
        {
            sectionKomentarz.Visibility = Visibility.Collapsed;
            sectionRozwiazanie.Visibility = Visibility.Collapsed;
            sectionRozpatrzenie.Visibility = Visibility.Collapsed;
            sectionPartie.Visibility = Visibility.Collapsed;
            sectionZdjecia.Visibility = Visibility.Collapsed;
            sectionHistoria.Visibility = Visibility.Collapsed;
            borderTowarySummary.Visibility = Visibility.Collapsed;
        }

        private void WybierzPierwszyTab()
        {
            foreach (var item in tabBottom.Items)
            {
                if (item is System.Windows.Controls.TabItem tab && tab.Visibility == Visibility.Visible)
                {
                    tabBottom.SelectedItem = tab;
                    break;
                }
            }
        }

        private void BtnOtworzFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ReklamacjeZdjecia",
                idReklamacji.ToString());

            if (Directory.Exists(folder))
                System.Diagnostics.Process.Start("explorer.exe", folder);
            else
                MessageBox.Show("Folder ze zdjeciami nie istnieje.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExportPDF_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var generator = new ReklamacjePDFGenerator(connectionString);
                var sciezka = generator.GenerujRaportReklamacji(idReklamacji);

                var result = MessageBox.Show(
                    $"Raport zostal wygenerowany:\n{sciezka}\n\nCzy otworzyc raport w przegladarce?",
                    "Sukces", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    generator.OtworzRaport(sciezka);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad generowania raportu:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnEmail_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Wyslij raport reklamacji",
                Width = 480, Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            var sp = new StackPanel { Margin = new Thickness(24) };

            sp.Children.Add(new TextBlock
            {
                Text = $"Wyslij raport reklamacji #{idReklamacji} na email:",
                FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12)
            });

            sp.Children.Add(new TextBlock { Text = "Adres email:", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 4) });

            var txtEmail = new TextBox
            {
                FontSize = 13, Padding = new Thickness(10, 8, 10, 8),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")),
                Margin = new Thickness(0, 0, 0, 8)
            };
            sp.Children.Add(txtEmail);

            sp.Children.Add(new TextBlock
            {
                Text = "Uwaga: Funkcja email wymaga konfiguracji serwera SMTP.",
                FontSize = 10, Foreground = Brushes.Gray, FontStyle = FontStyles.Italic, Margin = new Thickness(0, 0, 0, 16)
            });

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var btnWyslij = new Button
            {
                Content = "Wyslij", Width = 100, Height = 36, Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")),
                Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            btnWyslij.Click += async (s, args) =>
            {
                string email = txtEmail.Text.Trim();
                if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                {
                    MessageBox.Show("Wprowadz poprawny adres email.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                try
                {
                    btnWyslij.IsEnabled = false;
                    btnWyslij.Content = "Wysylanie...";
                    var generator = new ReklamacjePDFGenerator(connectionString);
                    var sciezka = generator.GenerujRaportReklamacji(idReklamacji);
                    var emailService = new ReklamacjeEmailService();
                    var result = await emailService.WyslijRaportReklamacji(email, idReklamacji, "", sciezka);
                    if (result.Success)
                    {
                        MessageBox.Show($"Raport zostal wyslany na adres:\n{email}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                    }
                    else
                    {
                        MessageBox.Show($"Nie udalo sie wyslac email:\n{result.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad wysylania:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnWyslij.IsEnabled = true;
                    btnWyslij.Content = "Wyslij";
                }
            };
            btnPanel.Children.Add(btnWyslij);

            var btnAnuluj = new Button
            {
                Content = "Anuluj", Width = 100, Height = 36,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            btnAnuluj.Click += (s, args) => dialog.Close();
            btnPanel.Children.Add(btnAnuluj);

            sp.Children.Add(btnPanel);
            dialog.Content = sp;
            dialog.ShowDialog();
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = StatusZmieniony;
            Close();
        }

        // ========================================
        // AVATARY
        // ========================================

        private void UstawAvatar(System.Windows.Controls.Border border, System.Windows.Controls.TextBlock textBlock, string name)
        {
            textBlock.Text = FormRozpatrzenieWindow.GetInitials(name);
            border.Background = FormRozpatrzenieWindow.GetAvatarBrush(name);
        }

        private void UstawAvatarPhoto(string odbiorcaId, string name, int size,
            System.Windows.Media.ImageBrush imgBrush, System.Windows.Shapes.Ellipse ellipse)
        {
            var source = FormRozpatrzenieWindow.LoadWpfAvatar(odbiorcaId, name, size);
            if (source != null)
            {
                imgBrush.ImageSource = source;
                ellipse.Visibility = Visibility.Visible;
            }
        }

        // ========================================
        // HELPER METHODS
        // ========================================

        private string SafeGet(SqlDataReader reader, string col)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(col, StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(i))
                        return reader[i].ToString();
                }
            }
            catch { }
            return "";
        }

        private decimal SafeGetDecimal(SqlDataReader reader, string col)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(col, StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(i))
                        return Convert.ToDecimal(reader[i]);
                }
            }
            catch { }
            return 0;
        }

        private DateTime SafeGetDateTime(SqlDataReader reader, string col)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(col, StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(i))
                        return Convert.ToDateTime(reader[i]);
                }
            }
            catch { }
            return DateTime.MinValue;
        }

        private DateTime? SafeGetNullableDateTime(SqlDataReader reader, string col)
        {
            try
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.GetName(i).Equals(col, StringComparison.OrdinalIgnoreCase) && !reader.IsDBNull(i))
                        return Convert.ToDateTime(reader[i]);
                }
            }
            catch { }
            return null;
        }
    }

    // ========================================
    // MODELE DANYCH
    // ========================================

    public class TowarSzczegoly
    {
        public int Lp { get; set; }
        public string Symbol { get; set; }
        public string Nazwa { get; set; }
        public decimal Waga { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }
    }

    public class ZdjecieViewModel
    {
        public int Id { get; set; }
        public string NazwaPliku { get; set; }
        public string SciezkaPliku { get; set; }
        public byte[] DaneZdjecia { get; set; }
        public BitmapImage Miniatura { get; set; }
    }

    public class PartiaViewModel
    {
        public string NumerPartii { get; set; }
        public string Dostawca { get; set; }
        public DateTime? DataDodania { get; set; }
        public string DataDodaniaStr => DataDodania?.ToString("dd.MM.yyyy HH:mm") ?? "";
    }

    public class HistoriaViewModel
    {
        public DateTime DataZmiany { get; set; }
        public string DataZmianyStr => DataZmiany.ToString("dd.MM.yyyy HH:mm");
        public string PoprzedniStatus { get; set; }
        public string StatusNowy { get; set; }
        public string Uzytkownik { get; set; }
        public string Komentarz { get; set; }
        public string Inicjaly { get; set; }
        public SolidColorBrush AvatarColor { get; set; }
        public ImageSource AvatarPhoto { get; set; }
        public Visibility AvatarPhotoVisibility => AvatarPhoto != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility KomentarzVisibility => string.IsNullOrWhiteSpace(Komentarz) ? Visibility.Collapsed : Visibility.Visible;

        public SolidColorBrush KolorStatusu
        {
            get
            {
                string hex = FormRozpatrzenieWindow.GetStatusColor(StatusNowy ?? "");
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }
        }
    }
}
