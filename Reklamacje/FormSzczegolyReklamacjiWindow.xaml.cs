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
        private int idKontrahenta;
        private string typReklamacji;
        private int? powiazanaReklamacjaId;
        private int? idFakturyOryginalnej;
        private string numerFakturyOryginalnej;
        private string nazwaKontrahenta;

        private const string HandelConnString = ReklamacjeConnectionStrings.Handel;

        private ObservableCollection<TowarSzczegoly> towary = new ObservableCollection<TowarSzczegoly>();
        private ObservableCollection<ZdjecieViewModel> zdjecia = new ObservableCollection<ZdjecieViewModel>();
        private ObservableCollection<HistoriaViewModel> historia = new ObservableCollection<HistoriaViewModel>();
        private ObservableCollection<PartiaViewModel> partie = new ObservableCollection<PartiaViewModel>();
        private ObservableCollection<KomentarzViewModel> komentarze = new ObservableCollection<KomentarzViewModel>();

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
            icKomentarze.ItemsSource = komentarze;

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
                    WczytajKomentarze(conn);
                }
                WybierzPierwszyTab();
                AktualizujKontekstoweAkcje();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania szczegolow:\n{ex.Message}",
                    "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string aktualnyStatus = "";
        private string aktualnyStatusV2 = "";
        private string aktualnaOsobaRozpatrujaca = "";
        private string aktualnyTypReklamacji = "";
        private bool aktualnyMaPowiazanie = false;
        private int aktualnaPowiazanaId = 0;

        private void WczytajPodstawoweInfo(SqlConnection conn)
        {
            try
            {
                string query = @"
                    SELECT r.*,
                        ISNULL(o1.Name, r.UserID) AS ZglaszajacyNazwa,
                        ISNULL(o2.Name, r.OsobaRozpatrujaca) AS RozpatrujacyNazwa,
                        ISNULL(o3.Name, r.UserZakonczenia) AS ZakonczylNazwa
                    FROM [dbo].[Reklamacje] r
                    LEFT JOIN [dbo].[operators] o1 ON r.UserID = o1.ID
                    LEFT JOIN [dbo].[operators] o2 ON r.OsobaRozpatrujaca = o2.ID
                    LEFT JOIN [dbo].[operators] o3 ON r.UserZakonczenia = o3.ID
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
                            nazwaKontrahenta = nazwaKontr;
                            string opis = SafeGet(reader, "Opis");
                            string komentarz = SafeGet(reader, "Komentarz");
                            string rozwiazanie = SafeGet(reader, "Rozwiazanie");
                            string zglaszajacyNazwa = SafeGet(reader, "ZglaszajacyNazwa");
                            string rozpatrujacyNazwa = SafeGet(reader, "RozpatrujacyNazwa");
                            string typRekl = SafeGet(reader, "TypReklamacji");
                            typReklamacji = typRekl;
                            string priorytet = SafeGet(reader, "Priorytet");

                            // Dane korekty — do przycisku "Zglos reklamacje do tej korekty"
                            try { idKontrahenta = Convert.ToInt32(reader["IdKontrahenta"]); } catch { idKontrahenta = 0; }
                            try { var v = reader["PowiazanaReklamacjaId"]; powiazanaReklamacjaId = (v == DBNull.Value) ? (int?)null : Convert.ToInt32(v); } catch { powiazanaReklamacjaId = null; }
                            try { var v = reader["IdFakturyOryginalnej"]; idFakturyOryginalnej = (v == DBNull.Value) ? (int?)null : Convert.ToInt32(v); } catch { idFakturyOryginalnej = null; }
                            numerFakturyOryginalnej = SafeGet(reader, "NumerFakturyOryginalnej");

                            // Pokaz przycisk tylko dla niepowiazanych otwartych korekt
                            string statusV2 = SafeGet(reader, "StatusV2");
                            bool toKorekta = typRekl == "Faktura korygujaca";
                            bool niepowiazana = !powiazanaReklamacjaId.HasValue || powiazanaReklamacjaId.Value <= 0;
                            bool otwarta = statusV2 == "ZGLOSZONA" || statusV2 == "W_ANALIZIE" || string.IsNullOrEmpty(statusV2);
                            if (btnZglosDoKorekty != null)
                                btnZglosDoKorekty.Visibility = (toKorekta && niepowiazana && otwarta) ? Visibility.Visible : Visibility.Collapsed;

                            // Zapisz biezacy stan dla AktualizujKontekstoweAkcje
                            aktualnyStatusV2 = string.IsNullOrEmpty(statusV2) ? "ZGLOSZONA" : statusV2;
                            aktualnaOsobaRozpatrujaca = SafeGet(reader, "OsobaRozpatrujaca");
                            aktualnyTypReklamacji = typRekl ?? "";
                            aktualnyMaPowiazanie = !niepowiazana;
                            aktualnaPowiazanaId = powiazanaReklamacjaId ?? 0;
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
                            string zakonczylId = SafeGet(reader, "UserZakonczenia");
                            string zakonczylNazwa = SafeGet(reader, "ZakonczylNazwa");

                            // === 3 avatary w headerze: ZGLOSIL -> PRZYJAL -> ZAKONCZYL + skutek ===
                            // 1. Zglosil
                            UstawAvatar(avatarZglaszajacyHeader, txtAvatarZglaszajacyHeader, zglaszajacyNazwa);
                            txtAvatarZglaszajacyNazwa.Text = string.IsNullOrEmpty(zglaszajacyNazwa) ? "—" : zglaszajacyNazwa;
                            UstawAvatarPhoto(zglaszajacyId, zglaszajacyNazwa, 80,
                                imgBrushZglaszajacyHeader, ellipseAvatarZglaszajacyHeader);

                            // 2. Przyjal — pokaz tylko gdy ktos przyjal (ma wpisana OsobaRozpatrujaca)
                            bool maPrzyjal = !string.IsNullOrWhiteSpace(rozpatrujacyNazwa);
                            avatarPrzyjalGroup.Visibility = maPrzyjal ? Visibility.Visible : Visibility.Collapsed;
                            sepZglosil_Przyjal.Visibility = maPrzyjal ? Visibility.Visible : Visibility.Collapsed;
                            if (maPrzyjal)
                            {
                                UstawAvatar(avatarRozpatrujacyHeader, txtAvatarRozpatrujacyHeader, rozpatrujacyNazwa);
                                txtAvatarRozpatrujacyNazwa.Text = rozpatrujacyNazwa;
                                UstawAvatarPhoto(rozpatrujacyId, rozpatrujacyNazwa, 80,
                                    imgBrushRozpatrujacyHeader, ellipseAvatarRozpatrujacyHeader);
                            }

                            // 3. Zakonczyl + skutek (StatusV2 finalny -> kolor + etykieta)
                            bool maZakonczyl = !string.IsNullOrWhiteSpace(zakonczylNazwa);
                            avatarZakonczylGroup.Visibility = maZakonczyl ? Visibility.Visible : Visibility.Collapsed;
                            sepPrzyjal_Zakonczyl.Visibility = maZakonczyl ? Visibility.Visible : Visibility.Collapsed;
                            if (maZakonczyl)
                            {
                                UstawAvatar(avatarZakonczylHeader, txtAvatarZakonczylHeader, zakonczylNazwa);
                                txtAvatarZakonczylNazwa.Text = zakonczylNazwa;
                                UstawAvatarPhoto(zakonczylId, zakonczylNazwa, 80,
                                    imgBrushZakonczylHeader, ellipseAvatarZakonczylHeader);

                                // Skutek (z jakim wynikiem zakonczyl)
                                string skutekText;
                                string skutekKolor;
                                switch (statusV2)
                                {
                                    case "ZASADNA": skutekText = "✓ UZNANA"; skutekKolor = "#27AE60"; break;
                                    case "ODRZUCONA": skutekText = "✕ ODRZUCONA"; skutekKolor = "#E74C3C"; break;
                                    case "ZAMKNIETA": skutekText = "🏁 ZAMKNIETA"; skutekKolor = "#7F8C8D"; break;
                                    case "POWIAZANA": skutekText = "🔗 POLACZONA"; skutekKolor = "#27AE60"; break;
                                    default: skutekText = StatusyV2.Etykieta(statusV2 ?? ""); skutekKolor = "#7F8C8D"; break;
                                }
                                txtSkutek.Text = skutekText;
                                badgeSkutek.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(skutekKolor));
                                badgeSkutek.Visibility = Visibility.Visible;
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

                            // Kategoryzacja (Etap 2.5)
                            string kategoria = SafeGet(reader, "KategoriaPrzyczyny");
                            string podkategoria = SafeGet(reader, "PodkategoriaPrzyczyny");
                            if (!string.IsNullOrWhiteSpace(kategoria) || !string.IsNullOrWhiteSpace(podkategoria))
                            {
                                sectionKategoria.Visibility = Visibility.Visible;
                                txtKategoria.Text = string.IsNullOrWhiteSpace(kategoria) ? "-" : kategoria;
                                txtPodkategoria.Text = string.IsNullOrWhiteSpace(podkategoria) ? "-" : podkategoria;
                            }

                            // Notatka jakosci (Workflow V2)
                            string notatkaJakosci = SafeGet(reader, "NotatkaJakosci");
                            if (!string.IsNullOrWhiteSpace(notatkaJakosci))
                            {
                                sectionNotatkaJakosci.Visibility = Visibility.Visible;
                                txtNotatkaJakosci.Text = notatkaJakosci;
                            }

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
                if (btnUsunZalacznik != null) btnUsunZalacznik.IsEnabled = true;
            }
            else
            {
                if (btnUsunZalacznik != null) btnUsunZalacznik.IsEnabled = false;
            }
        }

        // ========================================
        // ETAP 2: UPLOAD/USUWANIE ZALACZNIKOW
        // ========================================
        private void BtnDodajZalacznik_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Wybierz pliki do zalaczenia",
                Filter = "Obrazy i PDF|*.jpg;*.jpeg;*.png;*.bmp;*.pdf|Wszystkie pliki|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            int dodane = 0;
            const int MAX_SIZE = 10 * 1024 * 1024;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    foreach (var path in dlg.FileNames)
                    {
                        try
                        {
                            var fi = new System.IO.FileInfo(path);
                            if (fi.Length > MAX_SIZE)
                            {
                                MessageBox.Show($"Plik {fi.Name} ma {fi.Length / (1024 * 1024)}MB — limit to 10MB. Pomijam.",
                                    "Za duzy plik", MessageBoxButton.OK, MessageBoxImage.Warning);
                                continue;
                            }
                            byte[] dane = System.IO.File.ReadAllBytes(path);

                            // Zapisujemy do ReklamacjeZdjecia (jesli ma DaneZdjecia) — kompatybilnie z istniejacym kodem
                            using (var cmd = new SqlCommand(@"
                                INSERT INTO [dbo].[ReklamacjeZdjecia]
                                (IdReklamacji, NazwaPliku, SciezkaPliku, DaneZdjecia, DataDodania)
                                VALUES (@Id, @Nazwa, @Sciezka, @Dane, GETDATE())", conn))
                            {
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.Parameters.AddWithValue("@Nazwa", fi.Name);
                                cmd.Parameters.AddWithValue("@Sciezka", (object)path ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@Dane", dane);
                                cmd.ExecuteNonQuery();
                                dodane++;
                            }
                        }
                        catch (Exception exFile)
                        {
                            System.Diagnostics.Debug.WriteLine($"Blad zalacznika {path}: {exFile.Message}");
                        }
                    }

                    // Przeladuj liste zdjec
                    zdjecia.Clear();
                    WczytajZdjecia(conn);
                }
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie dodac zalacznika.", this);
                return;
            }

            if (dodane > 0)
            {
                txtZalacznikiHint.Text = $"  Dodano {dodane} plik(ow)";
            }
        }

        private void BtnUsunZalacznik_Click(object sender, RoutedEventArgs e)
        {
            if (!(lbThumbnails.SelectedItem is ZdjecieViewModel vm)) return;

            if (MessageBox.Show($"Usunac zalacznik '{vm.NazwaPliku}'?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("DELETE FROM [dbo].[ReklamacjeZdjecia] WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", vm.Id);
                        cmd.ExecuteNonQuery();
                    }
                    zdjecia.Clear();
                    WczytajZdjecia(conn);
                }
                imgPreview.Source = null;
                txtNoPhoto.Visibility = Visibility.Visible;
                txtKliknijPowieksz.Visibility = Visibility.Collapsed;
                btnUsunZalacznik.IsEnabled = false;
            }
            catch (Exception ex)
            {
                FriendlyError.Pokaz(ex, "Nie udalo sie usunac zalacznika.", this);
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
            var dialog = new Window
            {
                Title = "Zmiana statusu",
                Width = 380, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize
            };
            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = "Nowy status:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
            var combo = new ComboBox { Margin = new Thickness(0, 0, 0, 16) };
            foreach (var s in FormRozpatrzenieWindow.statusPipeline) combo.Items.Add(s);
            combo.SelectedItem = aktualnyStatus;
            Grid.SetRow(combo, 1);
            grid.Children.Add(combo);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "Zapisz", Width = 90, Height = 32, Background = Brushes.Green, Foreground = Brushes.White, Margin = new Thickness(0,0,8,0) };
            ok.Click += (s2, e2) =>
            {
                if (combo.SelectedItem == null) return;
                string nowyStatus = combo.SelectedItem.ToString();
                string nowyStatusV2 = nowyStatus switch
                {
                    "Przyjeta" => StatusyV2.W_ANALIZIE,
                    "Zaakceptowana" => StatusyV2.ZASADNA,
                    "Odrzucona" => StatusyV2.ODRZUCONA,
                    "Nowa" => StatusyV2.ZGLOSZONA,
                    _ => StatusyV2.ZGLOSZONA
                };
                try
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqlCommand(@"
                            UPDATE [dbo].[Reklamacje]
                            SET Status=@S, StatusV2=@SV2, OsobaRozpatrujaca=@U, DataModyfikacji=GETDATE(),
                                WymagaUzupelnienia = CASE WHEN @SV2 IN ('ZASADNA','ODRZUCONA','ZAMKNIETA') THEN 0 ELSE WymagaUzupelnienia END,
                                DataAnalizy = CASE WHEN DataAnalizy IS NULL THEN GETDATE() ELSE DataAnalizy END,
                                UserAnalizy = CASE WHEN UserAnalizy IS NULL THEN @U ELSE UserAnalizy END
                            WHERE Id=@Id", conn))
                        {
                            cmd.Parameters.AddWithValue("@S", nowyStatus);
                            cmd.Parameters.AddWithValue("@SV2", nowyStatusV2);
                            cmd.Parameters.AddWithValue("@U", userId);
                            cmd.Parameters.AddWithValue("@Id", idReklamacji);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    dialog.DialogResult = true;
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            };
            btns.Children.Add(ok);
            var cancel = new Button { Content = "Anuluj", Width = 80, Height = 32, IsCancel = true };
            btns.Children.Add(cancel);
            Grid.SetRow(btns, 2);
            grid.Children.Add(btns);
            dialog.Content = grid;
            if (dialog.ShowDialog() == true)
            {
                StatusZmieniony = true;
                ResetujSekcje();
                WczytajSzczegoly();
            }
        }

        private void ResetujSekcje()
        {
            sectionKomentarz.Visibility = Visibility.Collapsed;
            sectionRozwiazanie.Visibility = Visibility.Collapsed;
            sectionRozpatrzenie.Visibility = Visibility.Collapsed;
            sectionPartie.Visibility = Visibility.Collapsed;
            // sectionZdjecia zawsze widoczne (zeby mozna bylo dodac zalacznik nawet do pustej reklamacji)
            sectionHistoria.Visibility = Visibility.Collapsed;
            borderTowarySummary.Visibility = Visibility.Collapsed;
            if (sectionKategoria != null) sectionKategoria.Visibility = Visibility.Collapsed;
            if (sectionNotatkaJakosci != null) sectionNotatkaJakosci.Visibility = Visibility.Collapsed;
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
        // KOMENTARZE WEWNETRZNE
        // ========================================

        private void WczytajKomentarze(SqlConnection conn)
        {
            komentarze.Clear();
            try
            {
                using (var cmd = new SqlCommand(@"
                    SELECT k.Id, k.IdReklamacji, k.UserID, ISNULL(o.Name, k.UserID) AS UserName, k.Tresc, k.DataDodania
                    FROM [dbo].[ReklamacjeKomentarze] k
                    LEFT JOIN [dbo].[operators] o ON k.UserID = o.ID
                    WHERE k.IdReklamacji = @Id
                    ORDER BY k.DataDodania ASC", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", idReklamacji);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string odbiorcaId = r.IsDBNull(2) ? "" : r.GetString(2);
                            string userName = r.IsDBNull(3) ? "" : r.GetString(3);
                            komentarze.Add(new KomentarzViewModel
                            {
                                Id = r.GetInt32(0),
                                IdReklamacji = r.GetInt32(1),
                                UserId = odbiorcaId,
                                UserName = userName,
                                Tresc = r.IsDBNull(4) ? "" : r.GetString(4),
                                DataDodania = r.GetDateTime(5),
                                AvatarPhoto = FormRozpatrzenieWindow.LoadWpfAvatar(odbiorcaId, userName, 60)
                            });
                        }
                    }
                }
                txtKomentarzeCount.Text = $"({komentarze.Count})";
            }
            catch { }
        }

        private void BtnDodajKomentarz_Click(object sender, RoutedEventArgs e)
        {
            string tresc = txtNowyKomentarz.Text?.Trim();
            if (string.IsNullOrEmpty(tresc)) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [dbo].[ReklamacjeKomentarze] (IdReklamacji, UserID, Tresc)
                        VALUES (@Id, @User, @Tresc)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.Parameters.AddWithValue("@User", userId);
                        cmd.Parameters.AddWithValue("@Tresc", tresc);
                        cmd.ExecuteNonQuery();
                    }
                    txtNowyKomentarz.Text = "";
                    WczytajKomentarze(conn);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad dodawania komentarza:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================================
        // DRAG & DROP ZALACZNIKOW
        // ========================================

        private void Zalaczniki_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (sender is Border border)
                    border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD"));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Zalaczniki_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F7"));
        }

        private void Zalaczniki_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6F7"));

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var pliki = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (pliki == null || pliki.Length == 0) return;

            int dodano = 0;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    foreach (var sciezka in pliki)
                    {
                        var fi = new FileInfo(sciezka);
                        string ext = fi.Extension.ToLower();
                        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".pdf" && ext != ".bmp")
                        {
                            MessageBox.Show($"Pominieto nieobslugiwany format: {fi.Name}", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                            continue;
                        }
                        if (fi.Length > 10 * 1024 * 1024)
                        {
                            MessageBox.Show($"Plik {fi.Name} przekracza 10MB.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                            continue;
                        }

                        byte[] dane = File.ReadAllBytes(sciezka);
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO [dbo].[ReklamacjeZdjecia] (IdReklamacji, DaneZdjecia, NazwaPliku)
                            VALUES (@Id, @Dane, @Nazwa)", conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", idReklamacji);
                            cmd.Parameters.AddWithValue("@Dane", dane);
                            cmd.Parameters.AddWithValue("@Nazwa", fi.Name);
                            cmd.ExecuteNonQuery();
                            dodano++;
                        }
                    }

                    if (dodano > 0)
                    {
                        WczytajZdjecia(conn);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wgrywania plikow:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================================
        // DRUKOWANIE PROTOKOLU REKLAMACJI
        // ========================================

        private void BtnZglosDoKorekty_Click(object sender, RoutedEventArgs e)
        {
            if (typReklamacji != "Faktura korygujaca")
            {
                MessageBox.Show("Ta opcja jest dostepna tylko dla korekt z Symfonii.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (powiazanaReklamacjaId.HasValue && powiazanaReklamacjaId.Value > 0)
            {
                MessageBox.Show($"Ta korekta jest juz powiazana z reklamacja #{powiazanaReklamacjaId}.", "Juz powiazana", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!idFakturyOryginalnej.HasValue || idFakturyOryginalnej.Value <= 0)
            {
                MessageBox.Show("Brak informacji o fakturze bazowej dla tej korekty.\n\nUzyj przycisku 'Uzupelnij' w panelu glownym aby recznie wybrac fakture.",
                    "Brak faktury bazowej", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (idKontrahenta <= 0)
            {
                MessageBox.Show("Brak informacji o kontrahencie.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pobierz info o korekcie: IdDokumentu (HANDEL), suma kg, wartosc
            int idDokKorekty = 0;
            decimal? kgKorekty = null, wartKorekty = null;
            DateTime? dataKorekty = null;
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT IdDokumentu, SumaKg, SumaWartosc, DataZgloszenia, NumerDokumentu
                        FROM [dbo].[Reklamacje] WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                idDokKorekty = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                                kgKorekty = r.IsDBNull(1) ? (decimal?)null : r.GetDecimal(1);
                                wartKorekty = r.IsDBNull(2) ? (decimal?)null : r.GetDecimal(2);
                                dataKorekty = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3);
                            }
                        }
                    }
                }
            }
            catch { }

            string nrKorekty = "";
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT NumerDokumentu FROM [dbo].[Reklamacje] WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        var r = cmd.ExecuteScalar();
                        if (r != null && r != DBNull.Value) nrKorekty = r.ToString();
                    }
                }
            }
            catch { }

            // Otworz formularz "przypiety do korekty" — formularz sam powiazuje
            var okno = new FormReklamacjaWindow(
                HandelConnString, idFakturyOryginalnej.Value, idKontrahenta,
                numerFakturyOryginalnej, nazwaKontrahenta,
                userId, connectionString,
                idKorekty: idDokKorekty,
                nrKorekty: nrKorekty,
                dataKorekty: dataKorekty,
                wartoscKorekty: wartKorekty,
                kgKorekty: kgKorekty);
            okno.Owner = this;
            if (okno.ShowDialog() == true)
            {
                StatusZmieniony = true;
                ResetujSekcje();
                WczytajSzczegoly();
                MessageBox.Show("Reklamacja zostala zgloszona i powiazana z korekta.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var generator = new ReklamacjePDFGenerator(connectionString);
                string sciezka = generator.GenerujRaportReklamacji(idReklamacji);

                // Otworz plik w domyslnej przegladarce
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = sciezka,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad generowania raportu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================================
        // PANEL JAKOSCI - workflow V2 (analogiczny do glownego panelu)
        // ========================================

        // ========================================
        // KONTEKSTOWE POKAZYWANIE PRZYCISKOW WORKFLOW + STATUS BADGE
        // ========================================
        private void AktualizujKontekstoweAkcje()
        {
            string s = string.IsNullOrEmpty(aktualnyStatusV2) ? "ZGLOSZONA" : aktualnyStatusV2;
            bool ktosRozpatruje = !string.IsNullOrWhiteSpace(aktualnaOsobaRozpatrujaca);
            bool zakonczona = s == StatusyV2.ZASADNA || s == StatusyV2.ODRZUCONA
                              || s == StatusyV2.ZAMKNIETA || s == StatusyV2.POWIAZANA;

            // Domyslnie wszystkie schowane
            if (btnQ_Przyjmij != null) btnQ_Przyjmij.Visibility = Visibility.Collapsed;
            if (btnQ_Zatwierdz != null) btnQ_Zatwierdz.Visibility = Visibility.Collapsed;
            if (btnQ_Odrzuc != null) btnQ_Odrzuc.Visibility = Visibility.Collapsed;

            // Pokaz tylko te przyciski ktore maja sens
            if (!zakonczona)
            {
                if (s == StatusyV2.ZGLOSZONA && !ktosRozpatruje)
                {
                    // Nikt jeszcze nie przyjal — pokaz Przyjmij + Odrzuc
                    if (btnQ_Przyjmij != null) btnQ_Przyjmij.Visibility = Visibility.Visible;
                    if (btnQ_Odrzuc != null) btnQ_Odrzuc.Visibility = Visibility.Visible;
                }
                else
                {
                    // W toku (W_ANALIZIE lub ZGLOSZONA z Rozpatrujacym) — pokaz Zatwierdz + Odrzuc
                    if (btnQ_Zatwierdz != null) btnQ_Zatwierdz.Visibility = Visibility.Visible;
                    if (btnQ_Odrzuc != null) btnQ_Odrzuc.Visibility = Visibility.Visible;
                }
            }

            // Status badge w stopce
            if (badgeStatusFooter != null && txtStatusFooter != null)
            {
                txtStatusFooter.Text = StatusyV2.Etykieta(s);
                string kolor = s switch
                {
                    StatusyV2.ZGLOSZONA => "#3498DB",
                    StatusyV2.W_ANALIZIE => "#F39C12",
                    StatusyV2.ZASADNA => "#27AE60",
                    StatusyV2.ODRZUCONA => "#E74C3C",
                    StatusyV2.POWIAZANA => "#27AE60",
                    StatusyV2.ZAMKNIETA => "#7F8C8D",
                    _ => "#3498DB"
                };
                badgeStatusFooter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolor));
            }

            // Podpowiedz w stopce
            if (txtFooterHint != null)
            {
                txtFooterHint.Text = (s, ktosRozpatruje) switch
                {
                    (StatusyV2.ZGLOSZONA, false) => "Co dalej? Kliknij PRZYJMIJ aby przejac sprawe lub ODRZUC z powodem",
                    (StatusyV2.ZGLOSZONA, true) => "Sprawa w toku — kliknij ZATWIERDZ lub ODRZUC aby zakonczyc",
                    (StatusyV2.W_ANALIZIE, _) => "Sprawa w toku — kliknij ZATWIERDZ lub ODRZUC aby zakonczyc",
                    (StatusyV2.ZASADNA, _) => "Sprawa uznana — brak dalszych akcji",
                    (StatusyV2.ODRZUCONA, _) => "Sprawa odrzucona — brak dalszych akcji",
                    (StatusyV2.POWIAZANA, _) => "Sprawa polaczona z inna — brak dalszych akcji",
                    (StatusyV2.ZAMKNIETA, _) => "Sprawa zamknieta — brak dalszych akcji",
                    _ => ""
                };
            }
        }

        private void BtnWiecej_Click(object sender, RoutedEventArgs e)
        {
            if (popupWiecej != null) popupWiecej.IsOpen = !popupWiecej.IsOpen;
        }

        private void WykonajZmianeStatusuV2(string nowyStatusV2, string decyzja, string notatka,
            string przyczyna, string akcje, bool ustawDataAnalizy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        UPDATE [dbo].[Reklamacje]
                        SET StatusV2 = @StatusV2,
                            Status = CASE @StatusV2
                                WHEN 'ZGLOSZONA' THEN 'Nowa'
                                WHEN 'W_ANALIZIE' THEN 'W analizie'
                                WHEN 'ZASADNA' THEN 'Zaakceptowana'
                                WHEN 'POWIAZANA' THEN 'Zaakceptowana'
                                WHEN 'ZAMKNIETA' THEN 'Zamknieta'
                                WHEN 'ODRZUCONA' THEN 'Odrzucona'
                                ELSE Status
                            END,
                            DecyzjaJakosci = COALESCE(@Decyzja, DecyzjaJakosci),
                            NotatkaJakosci = CASE
                                WHEN @Notatka IS NOT NULL AND LEN(@Notatka) > 0
                                THEN ISNULL(NotatkaJakosci + CHAR(13) + CHAR(10), '') + @Stempel + @Notatka
                                ELSE NotatkaJakosci
                            END,
                            PrzyczynaGlowna = COALESCE(@PrzyczynaGlowna, PrzyczynaGlowna),
                            AkcjeNaprawcze = COALESCE(@AkcjeNaprawcze, AkcjeNaprawcze),
                            DataAnalizy = CASE WHEN @UstawDataAnalizy = 1 AND DataAnalizy IS NULL THEN GETDATE() ELSE DataAnalizy END,
                            UserAnalizy = CASE WHEN @UstawDataAnalizy = 1 AND UserAnalizy IS NULL THEN @User ELSE UserAnalizy END,
                            OsobaRozpatrujaca = CASE
                                WHEN @StatusV2 = 'W_ANALIZIE' AND (OsobaRozpatrujaca IS NULL OR LEN(OsobaRozpatrujaca) = 0)
                                THEN @User
                                ELSE OsobaRozpatrujaca
                            END,
                            DataZakonczenia = CASE
                                WHEN @StatusV2 IN ('ZASADNA','ODRZUCONA','ZAMKNIETA') AND DataZakonczenia IS NULL
                                THEN GETDATE()
                                ELSE DataZakonczenia
                            END,
                            UserZakonczenia = CASE
                                WHEN @StatusV2 IN ('ZASADNA','ODRZUCONA','ZAMKNIETA') AND UserZakonczenia IS NULL
                                THEN @User
                                ELSE UserZakonczenia
                            END,
                            WymagaUzupelnienia = CASE WHEN @StatusV2 IN ('W_ANALIZIE','ZASADNA','POWIAZANA','ZAMKNIETA','ODRZUCONA') THEN 0 ELSE WymagaUzupelnienia END
                        WHERE Id = @Id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.Parameters.AddWithValue("@StatusV2", nowyStatusV2);
                        cmd.Parameters.AddWithValue("@Decyzja", (object)decyzja ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Notatka", (object)notatka ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PrzyczynaGlowna", (object)przyczyna ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@AkcjeNaprawcze", (object)akcje ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Stempel", $"[{DateTime.Now:yyyy-MM-dd HH:mm} {userId}] ");
                        cmd.Parameters.AddWithValue("@UstawDataAnalizy", ustawDataAnalizy ? 1 : 0);
                        cmd.Parameters.AddWithValue("@User", userId ?? "");
                        cmd.ExecuteNonQuery();
                    }

                    // Wpis do historii
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [dbo].[ReklamacjeHistoria] (IdReklamacji, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji)
                        VALUES (@Id, @User, '', @Status, @Komentarz, 'PanelJakosci')", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.Parameters.AddWithValue("@User", userId ?? "");
                        cmd.Parameters.AddWithValue("@Status", nowyStatusV2);
                        cmd.Parameters.AddWithValue("@Komentarz", (object)(notatka ?? decyzja ?? "") ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
                StatusZmieniony = true;
                ResetujSekcje();
                WczytajSzczegoly();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zmiany statusu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Handler dla nowego przycisku "EDYTUJ DANE" (panel handlowca)
        private void BtnH_Edytuj_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new UzupelnijReklamacjeWindow(connectionString, idReklamacji, userId);
                window.Owner = this;
                if (window.ShowDialog() == true)
                {
                    StatusZmieniony = true;
                    WczytajSzczegoly();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwarcia edytora:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnQ_Przyjmij_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                "Przyjac reklamacje do rozpatrzenia?\n\nZostaniesz przypisany jako rozpatrujacy. Status zmieni sie na 'Rozpatrywana'.",
                "Przyjecie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            WykonajZmianeStatusuV2("W_ANALIZIE", null, null, null, null, true);
        }

        private void BtnQ_Zatwierdz_Click(object sender, RoutedEventArgs e)
        {
            var (ok, przyczyna, akcje) = PokazDialogZasadnaQ();
            if (!ok) return;
            WykonajZmianeStatusuV2("ZASADNA", "Zasadna", null, przyczyna, akcje, true);
        }

        private void BtnQ_Odrzuc_Click(object sender, RoutedEventArgs e)
        {
            var (ok, powod) = PokazDialogOdrzucQ();
            if (!ok) return;
            WykonajZmianeStatusuV2("ODRZUCONA", "Niezasadna", powod, null, null, true);
        }

        // Maly dialog dla Zatwierdz: przyczyna + akcje naprawcze
        private (bool ok, string przyczyna, string akcje) PokazDialogZasadnaQ()
        {
            var dlg = new Window
            {
                Title = "Zatwierdz reklamacje",
                Width = 480, Height = 380,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = Brushes.WhiteSmoke
            };
            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock { Text = "Zatwierdzic jako Uznana (zasadna)?", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
            sp.Children.Add(new TextBlock { Text = "Przyczyna glowna *", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            var txtPrzyczyna = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 10) };
            sp.Children.Add(txtPrzyczyna);
            sp.Children.Add(new TextBlock { Text = "Akcje naprawcze *", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            var txtAkcje = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, Padding = new Thickness(8) };
            sp.Children.Add(txtAkcje);

            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var btnAnu = new Button { Content = "Anuluj", Width = 90, Height = 32, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
            var btnOk = new Button { Content = "Zatwierdz", Width = 110, Height = 32, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60")), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, IsDefault = true };
            bool wynik = false;
            string p = "", a = "";
            btnAnu.Click += (s, e) => dlg.Close();
            btnOk.Click += (s, e) =>
            {
                p = txtPrzyczyna.Text?.Trim() ?? "";
                a = txtAkcje.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(p) || string.IsNullOrEmpty(a))
                {
                    MessageBox.Show("Wypelnij oba pola.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                wynik = true;
                dlg.Close();
            };
            bp.Children.Add(btnAnu);
            bp.Children.Add(btnOk);
            sp.Children.Add(bp);
            dlg.Content = sp;
            dlg.ShowDialog();
            return (wynik, p, a);
        }

        // Maly dialog dla Odrzuc: powod
        private (bool ok, string powod) PokazDialogOdrzucQ()
        {
            var dlg = new Window
            {
                Title = "Odrzuc reklamacje",
                Width = 460, Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, ResizeMode = ResizeMode.NoResize,
                Background = Brushes.WhiteSmoke
            };
            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock { Text = "Odrzucic reklamacje?", FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            sp.Children.Add(new TextBlock { Text = "Powod odrzucenia (zostanie dopisany do notatki jakosci)", FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")), Margin = new Thickness(0, 0, 0, 6) });
            var txt = new TextBox { Height = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, Padding = new Thickness(8) };
            sp.Children.Add(txt);
            var bp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var btnAnu = new Button { Content = "Anuluj", Width = 90, Height = 32, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
            var btnOk = new Button { Content = "Odrzuc", Width = 110, Height = 32, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, IsDefault = true };
            bool wynik = false;
            string powod = "";
            btnAnu.Click += (s, e) => dlg.Close();
            btnOk.Click += (s, e) =>
            {
                powod = txt.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(powod))
                {
                    MessageBox.Show("Podaj powod odrzucenia.", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                wynik = true;
                dlg.Close();
            };
            bp.Children.Add(btnAnu);
            bp.Children.Add(btnOk);
            sp.Children.Add(bp);
            dlg.Content = sp;
            dlg.ShowDialog();
            return (wynik, powod);
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

}
