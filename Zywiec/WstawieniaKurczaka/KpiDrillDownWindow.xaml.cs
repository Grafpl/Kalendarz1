using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kalendarz1.Zywiec.Kalendarz;

namespace Kalendarz1
{
    public partial class KpiDrillDownWindow : Window
    {
        private static readonly string ConnStr = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private static readonly Dictionary<string, string> ModuleColors = new Dictionary<string, string>
        {
            { "Wstawienia", "#27AE60" },
            { "Kalendarz",  "#2980B9" },
            { "Specyfikacja","#8E44AD" },
            { "Hodowcy",    "#E74C3C" },
            { "Dokumenty",  "#16A085" },
            { "Wnioski",    "#E67E22" },
            { "SUMA",       "#1ABC9C" },
            { "Audyt",      "#C0392B" },
            { "Efektywnosc","#2ECC71" },
            { "Czas",       "#3498DB" },
            { "Jakosc",     "#F39C12" },
            { "Regularnosc","#9B59B6" }
        };

        // Column width hints per column name
        private static readonly Dictionary<string, double> ColumnWidths = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "Akcja", 160 }, { "Modul", 110 },
            { "Lp", 60 },
            { "Dostawca", 200 }, { "Hodowca", 200 }, { "Szczegol", 300 },
            { "Data Wstawienia", 130 }, { "Data Odbioru", 130 }, { "Data Spec.", 115 },
            { "Data Akcji", 145 }, { "Data Wyslania", 145 },
            { "Data Planu", 115 },
            { "Ilosc", 75 }, { "Sztuki", 75 }, { "Waga", 85 },
            { "Status", 120 }, { "Decyzja", 120 },
            { "Pole", 120 }, { "Stara Wartosc", 140 }, { "Nowa Wartosc", 140 },
            { "Tresc", 260 }, { "Powod", 200 },
            { "Typ", 150 }, { "Zrodlo", 220 }, { "LP Dostawy", 85 },
            { "Telefon", 120 }, { "ID Hodowcy", 100 }, { "ID Dostawcy", 100 },
            { "Nr Wniosku", 90 },
            { "Wynik Telefonu", 130 }, { "Status Przed", 120 }, { "Status Po", 120 }
        };

        private string _accentHex;
        private string _userID;

        public KpiDrillDownWindow(string userName, string userID, string moduleName,
                                   DateTime startDate, DateTime endDate)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _userID = userID;
            _accentHex = ModuleColors.ContainsKey(moduleName) ? ModuleColors[moduleName] : "#2C3E50";
            var accent = (Color)ColorConverter.ConvertFromString(_accentHex);

            Title = $"{userName} - {moduleName}";
            txtUserName.Text = userName;
            txtModulName.Text = moduleName.ToUpper();
            txtPeriod.Text = $"{startDate:dd.MM.yyyy}  -  {endDate.AddDays(-1):dd.MM.yyyy}";

            txtInitials.Text = GetInitials(userName);
            avatarBorder.Background = new SolidColorBrush(accent);
            badgeModul.Background = new SolidColorBrush(accent);

            // Header gradient
            var darker = Color.FromRgb(
                (byte)Math.Max(accent.R * 0.2, 15),
                (byte)Math.Max(accent.G * 0.2, 15),
                (byte)Math.Max(accent.B * 0.2, 15));
            var mid = Color.FromRgb(
                (byte)(accent.R * 0.35),
                (byte)(accent.G * 0.35),
                (byte)(accent.B * 0.35));
            headerBar.Background = new LinearGradientBrush(darker, mid, 0);

            // Load avatar async
            LoadAvatar(userID);

            LoadData(userName, userID, moduleName, startDate, endDate);
        }

        private void LoadAvatar(string userID)
        {
            if (string.IsNullOrEmpty(userID)) return;
            Task.Run(() =>
            {
                var bitmap = UserAvatarManager.GetAvatar(userID);
                if (bitmap != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        using (var memory = new MemoryStream())
                        {
                            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                            memory.Position = 0;
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.StreamSource = memory;
                            bitmapImage.EndInit();
                            bitmapImage.Freeze();
                            avatarImageBrush.ImageSource = bitmapImage;
                        }
                    });
                }
            });
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private void LoadData(string userName, string userID, string moduleName,
                              DateTime startDate, DateTime endDate)
        {
            try
            {
                var dt = new DataTable();

                switch (moduleName)
                {
                    case "Wstawienia":    LoadWstawienia(dt, userID, startDate, endDate); break;
                    case "Kalendarz":     LoadKalendarz(dt, userID, startDate, endDate); break;
                    case "Specyfikacja":  LoadSpecyfikacja(dt, userID, startDate, endDate); break;
                    case "Hodowcy":       LoadHodowcy(dt, userID, startDate, endDate); break;
                    case "Dokumenty":     LoadDokumenty(dt, userID, startDate, endDate); break;
                    case "Wnioski":       LoadWnioski(dt, userID, startDate, endDate); break;
                    case "Audyt":         LoadAudyt(dt, userID, startDate, endDate); break;
                    case "Efektywnosc":   LoadAll(dt, userName, userID, startDate, endDate); break;
                    default:              LoadAll(dt, userName, userID, startDate, endDate); break;
                }

                dgDetails.ItemsSource = dt.DefaultView;
                txtRecordCount.Text = dt.Rows.Count.ToString();

                BuildSummaryCards(dt);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BuildSummaryCards(DataTable dt)
        {
            panelSummary.Children.Clear();

            string groupCol = dt.Columns.Contains("Modul") ? "Modul" : (dt.Columns.Contains("Akcja") ? "Akcja" : null);
            if (groupCol == null || dt.Rows.Count == 0)
            {
                txtTypCount.Text = "0";
                return;
            }

            var groups = new Dictionary<string, int>();
            foreach (DataRow row in dt.Rows)
            {
                string key = row[groupCol]?.ToString() ?? "Inne";
                if (!groups.ContainsKey(key)) groups[key] = 0;
                groups[key]++;
            }

            txtTypCount.Text = groups.Count.ToString();

            var accent = (Color)ColorConverter.ConvertFromString(_accentHex);
            var cardColors = new[] {
                accent,
                Color.FromRgb(52, 152, 219),
                Color.FromRgb(155, 89, 182),
                Color.FromRgb(243, 156, 18),
                Color.FromRgb(231, 76, 60),
                Color.FromRgb(26, 188, 156),
                Color.FromRgb(241, 196, 15),
                Color.FromRgb(230, 126, 34)
            };

            int idx = 0;
            foreach (var kvp in groups.OrderByDescending(x => x.Value))
            {
                var c = cardColors[idx % cardColors.Length];
                idx++;

                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(16, c.R, c.G, c.B)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 0, 8, 6),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(50, c.R, c.G, c.B)),
                    BorderThickness = new Thickness(1)
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal };

                sp.Children.Add(new TextBlock
                {
                    Text = kvp.Value.ToString(),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(c),
                    Margin = new Thickness(0, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                sp.Children.Add(new TextBlock
                {
                    Text = kvp.Key,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(73, 80, 87)),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Medium
                });

                card.Child = sp;
                panelSummary.Children.Add(card);
            }
        }

        // ==================== WSTAWIENIA ====================
        private void LoadWstawienia(DataTable dt, string userID, DateTime s, DateTime e)
        {
            FillTable(dt, @"
                SELECT 'Utworzyl' AS Akcja, w.Lp, w.Dostawca,
                       w.DataWstawienia AS [Data Wstawienia], w.IloscWstawienia AS Ilosc,
                       w.DataUtw AS [Data Akcji],
                       CASE WHEN w.isConf = 1 THEN 'Potwierdzone' ELSE 'Oczekujace' END AS Status
                FROM dbo.WstawieniaKurczakow w
                WHERE w.KtoStwo = TRY_CAST(@UID AS INT) AND w.DataUtw >= @S AND w.DataUtw < @E
                UNION ALL
                SELECT 'Potwierdzil', w.Lp, w.Dostawca,
                       w.DataWstawienia, w.IloscWstawienia,
                       w.DataConf, 'Potwierdzone'
                FROM dbo.WstawieniaKurczakow w
                WHERE w.isConf = 1 AND w.KtoConf = TRY_CAST(@UID AS INT) AND w.DataConf >= @S AND w.DataConf < @E
                ORDER BY [Data Akcji] DESC", userID, s, e);
        }

        // ==================== KALENDARZ ====================
        private void LoadKalendarz(DataTable dt, string userID, DateTime s, DateTime e)
        {
            FillTable(dt, @"
                SELECT 'Utworzyl dostawe' AS Akcja, h.LP, h.Dostawca,
                       h.DataOdbioru AS [Data Odbioru], h.SztukiDek AS Sztuki,
                       h.WagaDek AS Waga, h.Bufor AS Status, h.DataUtw AS [Data Akcji]
                FROM dbo.HarmonogramDostaw h
                WHERE h.ktoStwo = TRY_CAST(@UID AS INT) AND h.DataUtw >= @S AND h.DataUtw < @E
                UNION ALL
                SELECT 'Potwierdzil wage', h.LP, h.Dostawca,
                       h.DataOdbioru, h.SztukiDek, h.WagaDek, h.Bufor, h.KiedyWaga
                FROM dbo.HarmonogramDostaw h
                WHERE h.PotwWaga = 1 AND h.KtoWaga = TRY_CAST(@UID AS INT) AND h.KiedyWaga >= @S AND h.KiedyWaga < @E
                UNION ALL
                SELECT 'Potwierdzil sztuki', h.LP, h.Dostawca,
                       h.DataOdbioru, h.SztukiDek, h.WagaDek, h.Bufor, h.KiedySztuki
                FROM dbo.HarmonogramDostaw h
                WHERE h.PotwSztuki = 1 AND h.KtoSztuki = TRY_CAST(@UID AS INT) AND h.KiedySztuki >= @S AND h.KiedySztuki < @E
                ORDER BY [Data Akcji] DESC", userID, s, e);

            // Ceny dodane
            var dtCeny = new DataTable();
            FillTableSafe(dtCeny, @"
                SELECT 'Dodal cene ' + Typ AS Akcja, CAST(0 AS INT) AS LP, '' AS Dostawca,
                       Data AS [Data Odbioru], CAST(0 AS INT) AS Sztuki, CAST(Cena AS DECIMAL(10,2)) AS Waga,
                       Typ AS Status, KiedyDodal AS [Data Akcji]
                FROM (
                    SELECT 'Ministerialna' AS Typ, Data, Cena, KtoDodal, KiedyDodal FROM dbo.CenaMinisterialna WHERE KtoDodal = TRY_CAST(@UID AS INT) AND KiedyDodal >= @S AND KiedyDodal < @E
                    UNION ALL
                    SELECT 'Rolnicza', Data, Cena, KtoDodal, KiedyDodal FROM dbo.CenaRolnicza WHERE KtoDodal = TRY_CAST(@UID AS INT) AND KiedyDodal >= @S AND KiedyDodal < @E
                    UNION ALL
                    SELECT 'Tuszka', Data, Cena, KtoDodal, KiedyDodal FROM dbo.CenaTuszki WHERE KtoDodal = TRY_CAST(@UID AS INT) AND KiedyDodal >= @S AND KiedyDodal < @E
                ) c
                ORDER BY [Data Akcji] DESC", userID, s, e);

            foreach (DataRow row in dtCeny.Rows) dt.ImportRow(row);

            // Edycje z historii zmian (AuditLog_Dostawy)
            var dtEdycje = new DataTable();
            FillTableSafe(dtEdycje, @"
                SELECT
                    'Edycja: ' + ISNULL(a.ZrodloZmiany, '') AS Akcja,
                    TRY_CAST(a.RekordID AS INT) AS LP,
                    ISNULL(JSON_VALUE(a.DodatkoweInfo, '$.dostawca'), '') AS Dostawca,
                    NULL AS [Data Odbioru],
                    CAST(0 AS INT) AS Sztuki,
                    CAST(0 AS DECIMAL(10,2)) AS Waga,
                    ISNULL(a.NazwaPola, '') + ': ' + ISNULL(LEFT(a.StaraWartosc,30),'') + ' -> ' + ISNULL(LEFT(a.NowaWartosc,30),'') AS Status,
                    a.DataZmiany AS [Data Akcji]
                FROM dbo.AuditLog_Dostawy a
                WHERE a.UserID = @UID AND a.TypOperacji = 'UPDATE' AND a.DataZmiany >= @S AND a.DataZmiany < @E
                ORDER BY a.DataZmiany DESC", userID, s, e);

            foreach (DataRow row in dtEdycje.Rows) dt.ImportRow(row);

            // Notatki dodane
            var dtNotatki = new DataTable();
            FillTableSafe(dtNotatki, @"
                SELECT
                    'Dodal notatke' AS Akcja,
                    n.IndeksID AS LP,
                    '' AS Dostawca,
                    NULL AS [Data Odbioru],
                    CAST(0 AS INT) AS Sztuki,
                    CAST(0 AS DECIMAL(10,2)) AS Waga,
                    LEFT(n.Tresc, 80) AS Status,
                    n.DataUtworzenia AS [Data Akcji]
                FROM dbo.Notatki n
                WHERE n.KtoStworzyl = TRY_CAST(@UID AS INT) AND n.DataUtworzenia >= @S AND n.DataUtworzenia < @E
                ORDER BY n.DataUtworzenia DESC", userID, s, e);

            foreach (DataRow row in dtNotatki.Rows) dt.ImportRow(row);
        }

        // ==================== SPECYFIKACJA ====================
        private void LoadSpecyfikacja(DataTable dt, string userID, DateTime s, DateTime e)
        {
            FillTableSafe(dt, @"
                SELECT
                    CASE WHEN r.ZatwierdzoneByUserID = @UID AND r.Zatwierdzony = 1 AND r.DataZatwierdzenia >= @S AND r.DataZatwierdzenia < @E
                         THEN 'Wprowadzenie' ELSE 'Weryfikacja' END AS Akcja,
                    r.CalcDate AS [Data Specyfikacji],
                    r.FarmerCalcID AS [ID Rekordu],
                    CASE WHEN r.ZatwierdzoneByUserID = @UID AND r.Zatwierdzony = 1 AND r.DataZatwierdzenia >= @S AND r.DataZatwierdzenia < @E
                         THEN r.DataZatwierdzenia ELSE r.DataWeryfikacji END AS [Data Akcji],
                    ISNULL(r.ZatwierdzonePrzez, '') AS [Wprowadzil],
                    ISNULL(r.ZweryfikowanePrzez, '') AS [Zweryfikowal]
                FROM dbo.RozliczeniaZatwierdzenia r
                WHERE (r.ZatwierdzoneByUserID = @UID AND r.Zatwierdzony = 1 AND r.DataZatwierdzenia >= @S AND r.DataZatwierdzenia < @E)
                   OR (r.ZweryfikowaneByUserID = @UID AND r.Zweryfikowany = 1 AND r.DataWeryfikacji >= @S AND r.DataWeryfikacji < @E)
                ORDER BY [Data Akcji] DESC", userID, s, e);
        }

        // ==================== SMS ====================
        private void LoadSms(DataTable dt, string userID, DateTime s, DateTime e)
        {
            FillTableSafe(dt, @"
                SELECT CASE WHEN sh.SmsType = 'ALL' THEN 'SMS zbiorczy' ELSE 'SMS indywidualny' END AS Akcja,
                       sh.CalcDate AS [Data Planu], sh.CustomerGID AS [ID Hodowcy],
                       sh.PhoneNumber AS Telefon, LEFT(sh.SmsContent, 120) AS Tresc,
                       sh.SentDate AS [Data Wyslania]
                FROM dbo.SmsHistory sh
                WHERE sh.SentByUser = @UID AND sh.SentDate >= @S AND sh.SentDate < @E
                UNION ALL
                SELECT 'Zapis planu', mt.CalcDate, '', '',
                       CAST(mt.RecordCount AS VARCHAR(20)) + ' rekordow', mt.TransferDate
                FROM dbo.MatrycaTransferLog mt
                WHERE mt.TransferByUser = @UID AND mt.TransferDate >= @S AND mt.TransferDate < @E
                ORDER BY [Data Wyslania] DESC", userID, s, e);
        }

        // ==================== HODOWCY (Zmiany statusu + Notatki) ====================
        private void LoadHodowcy(DataTable dt, string userID, DateTime s, DateTime e)
        {
            FillTableSafe(dt, @"
                SELECT pa.TypAktywnosci AS Akcja, ph.Dostawca AS Hodowca,
                       pa.StatusPrzed AS [Status Przed], pa.StatusPo AS [Status Po],
                       LEFT(pa.Tresc, 150) AS Tresc, pa.DataUtworzenia AS [Data Akcji]
                FROM dbo.Pozyskiwanie_Aktywnosci pa
                LEFT JOIN dbo.Pozyskiwanie_Hodowcy ph ON pa.HodowcaId = ph.Id
                WHERE pa.UzytkownikId = @UID
                  AND pa.TypAktywnosci IN ('Zmiana statusu', 'Notatka')
                  AND pa.DataUtworzenia >= @S AND pa.DataUtworzenia < @E
                  AND pa.UzytkownikId <> 'IMPORT'
                ORDER BY pa.DataUtworzenia DESC", userID, s, e);
        }

        // ==================== DOKUMENTY ====================
        private void LoadDokumenty(DataTable dt, string userID, DateTime s, DateTime e)
        {
            FillTableSafe(dt, @"
                SELECT 'Utworzyl dokument' AS Akcja, h.LP, h.Dostawca,
                       h.DataOdbioru AS [Data Odbioru], h.KiedyUtw AS [Data Akcji]
                FROM dbo.HarmonogramDostaw h
                WHERE h.Utworzone = 1 AND h.KtoUtw = TRY_CAST(@UID AS INT) AND h.KiedyUtw >= @S AND h.KiedyUtw < @E
                UNION ALL
                SELECT 'Wyslal dokument', h.LP, h.Dostawca, h.DataOdbioru, h.KiedyWysl
                FROM dbo.HarmonogramDostaw h
                WHERE h.Wysłane = 1 AND h.KtoWysl = TRY_CAST(@UID AS INT) AND h.KiedyWysl >= @S AND h.KiedyWysl < @E
                UNION ALL
                SELECT 'Otrzymal dokument', h.LP, h.Dostawca, h.DataOdbioru, h.KiedyOtrzm
                FROM dbo.HarmonogramDostaw h
                WHERE h.Otrzymane = 1 AND h.KtoOtrzym = TRY_CAST(@UID AS INT) AND h.KiedyOtrzm >= @S AND h.KiedyOtrzm < @E
                ORDER BY [Data Akcji] DESC", userID, s, e);
        }

        // ==================== WNIOSKI ====================
        private void LoadWnioski(DataTable dt, string userID, DateTime s, DateTime e)
        {
            FillTableSafe(dt, @"
                SELECT CASE WHEN cr.RequestedBy = @UID THEN 'Zlozyl wniosek' ELSE 'Rozpatrzyl wniosek' END AS Akcja,
                       cr.CRID AS [Nr Wniosku], cr.DostawcaID AS [ID Dostawcy],
                       cr.Status, ISNULL(cr.DecyzjaTyp, '') AS Decyzja,
                       LEFT(ISNULL(cr.Reason, ''), 120) AS Powod,
                       CASE WHEN cr.RequestedBy = @UID THEN cr.RequestedAtUTC ELSE cr.DecyzjaKiedyUTC END AS [Data Akcji]
                FROM dbo.DostawcyCR cr
                WHERE (cr.RequestedBy = @UID AND cr.RequestedAtUTC >= @S AND cr.RequestedAtUTC < @E)
                   OR (cr.DecyzjaKto = @UID AND cr.DecyzjaKiedyUTC >= @S AND cr.DecyzjaKiedyUTC < @E)
                ORDER BY [Data Akcji] DESC", userID, s, e);
        }

        // ==================== AUDYT ====================
        private void LoadAudyt(DataTable dt, string userID, DateTime s, DateTime e)
        {
            // Puste edycje
            FillTableSafe(dt, @"
                SELECT 'Pusta edycja' AS Typ,
                       ISNULL(a.NazwaPola,'') AS Pole,
                       a.RekordID AS [LP Dostawy],
                       ISNULL(a.ZrodloZmiany,'') AS Zrodlo,
                       ISNULL(LEFT(a.StaraWartosc,50),'(puste)') AS [Wartosc],
                       a.DataZmiany AS [Data]
                FROM dbo.AuditLog_Dostawy a
                WHERE a.UserID = @UID AND a.TypOperacji = 'UPDATE'
                  AND a.DataZmiany >= @S AND a.DataZmiany < @E
                  AND ISNULL(a.StaraWartosc,'') = ISNULL(a.NowaWartosc,'')
                ORDER BY a.DataZmiany DESC", userID, s, e);

            // Odwrocone zmiany
            var dt2 = new DataTable();
            FillTableSafe(dt2, @"
                SELECT 'Odwrocona zmiana' AS Typ,
                       a1.NazwaPola AS Pole,
                       a1.RekordID AS [LP Dostawy],
                       a1.StaraWartosc + ' -> ' + a1.NowaWartosc + ' -> ' + a2.NowaWartosc AS Zrodlo,
                       CAST(DATEDIFF(SECOND, a1.DataZmiany, a2.DataZmiany) AS VARCHAR) + 's odstep' AS [Wartosc],
                       a1.DataZmiany AS [Data]
                FROM dbo.AuditLog_Dostawy a1
                INNER JOIN dbo.AuditLog_Dostawy a2
                    ON a1.UserID = a2.UserID AND a1.RekordID = a2.RekordID AND a1.NazwaPola = a2.NazwaPola
                    AND a2.DataZmiany > a1.DataZmiany
                    AND DATEDIFF(MINUTE, a1.DataZmiany, a2.DataZmiany) <= 10
                    AND ISNULL(a1.StaraWartosc,'') = ISNULL(a2.NowaWartosc,'')
                    AND ISNULL(a1.NowaWartosc,'') = ISNULL(a2.StaraWartosc,'')
                WHERE a1.UserID = @UID AND a1.TypOperacji = 'UPDATE'
                  AND a1.DataZmiany >= @S AND a1.DataZmiany < @E
                ORDER BY a1.DataZmiany DESC", userID, s, e);
            foreach (DataRow row in dt2.Rows) dt.ImportRow(row);

            // Samo-potwierdzenia
            var dt3 = new DataTable();
            FillTableSafe(dt3, @"
                SELECT 'Samo-potwierdzenie' AS Typ,
                       'Wstawienie' AS Pole,
                       CAST(w.Lp AS VARCHAR) AS [LP Dostawy],
                       w.Dostawca AS Zrodlo,
                       'Utworzyl: ' + CONVERT(VARCHAR, w.DataUtw, 104) + ', Potw: ' + CONVERT(VARCHAR, w.DataConf, 104) AS [Wartosc],
                       w.DataUtw AS [Data]
                FROM dbo.WstawieniaKurczakow w
                WHERE w.KtoStwo = TRY_CAST(@UID AS INT) AND w.KtoStwo = w.KtoConf
                  AND w.isConf = 1 AND w.DataUtw >= @S AND w.DataUtw < @E
                ORDER BY w.DataUtw DESC", userID, s, e);
            foreach (DataRow row in dt3.Rows) dt.ImportRow(row);

            // Szybkie serie
            var dt4 = new DataTable();
            FillTableSafe(dt4, @"
                SELECT 'Szybka seria' AS Typ,
                       a.NazwaPola AS Pole,
                       a.RekordID AS [LP Dostawy],
                       CAST(COUNT(*) AS VARCHAR) + ' edycji w 2 min' AS Zrodlo,
                       MIN(CONVERT(VARCHAR, a.DataZmiany, 108)) + ' - ' + MAX(CONVERT(VARCHAR, a.DataZmiany, 108)) AS [Wartosc],
                       MIN(a.DataZmiany) AS [Data]
                FROM dbo.AuditLog_Dostawy a
                WHERE a.UserID = @UID AND a.TypOperacji = 'UPDATE'
                  AND a.DataZmiany >= @S AND a.DataZmiany < @E
                GROUP BY a.RekordID, a.NazwaPola,
                         DATEADD(MINUTE, DATEDIFF(MINUTE, 0, a.DataZmiany) / 2 * 2, 0)
                HAVING COUNT(*) > 3
                ORDER BY [Data] DESC", userID, s, e);
            foreach (DataRow row in dt4.Rows) dt.ImportRow(row);
        }

        // ==================== ALL (SUMA) ====================
        private void LoadAll(DataTable dt, string userName, string userID, DateTime s, DateTime e)
        {
            dt.Columns.Add("Modul", typeof(string));
            dt.Columns.Add("Akcja", typeof(string));
            dt.Columns.Add("Szczegol", typeof(string));
            dt.Columns.Add("Data Akcji", typeof(DateTime));

            LoadAllModule(dt, "Wstawienia", userID, s, e, @"
                SELECT 'Utworzyl', w.Dostawca + ' (' + CAST(ISNULL(w.IloscWstawienia,0) AS VARCHAR) + ' szt.)', w.DataUtw
                FROM dbo.WstawieniaKurczakow w WHERE w.KtoStwo = TRY_CAST(@UID AS INT) AND w.DataUtw >= @S AND w.DataUtw < @E
                UNION ALL
                SELECT 'Potwierdzil', w.Dostawca + ' (' + CAST(ISNULL(w.IloscWstawienia,0) AS VARCHAR) + ' szt.)', w.DataConf
                FROM dbo.WstawieniaKurczakow w WHERE w.isConf = 1 AND w.KtoConf = TRY_CAST(@UID AS INT) AND w.DataConf >= @S AND w.DataConf < @E");

            LoadAllModule(dt, "Kalendarz", userID, s, e, @"
                SELECT 'Utworzyl dostawe', h.Dostawca + ' LP:' + CAST(h.LP AS VARCHAR), h.DataUtw
                FROM dbo.HarmonogramDostaw h WHERE h.ktoStwo = TRY_CAST(@UID AS INT) AND h.DataUtw >= @S AND h.DataUtw < @E
                UNION ALL
                SELECT 'Potw. wage', h.Dostawca + ' LP:' + CAST(h.LP AS VARCHAR), h.KiedyWaga
                FROM dbo.HarmonogramDostaw h WHERE h.PotwWaga = 1 AND h.KtoWaga = TRY_CAST(@UID AS INT) AND h.KiedyWaga >= @S AND h.KiedyWaga < @E
                UNION ALL
                SELECT 'Potw. sztuki', h.Dostawca + ' LP:' + CAST(h.LP AS VARCHAR), h.KiedySztuki
                FROM dbo.HarmonogramDostaw h WHERE h.PotwSztuki = 1 AND h.KtoSztuki = TRY_CAST(@UID AS INT) AND h.KiedySztuki >= @S AND h.KiedySztuki < @E");

            LoadAllModuleSafe(dt, "Kalendarz", userID, s, e, @"
                SELECT 'Edycja: ' + ISNULL(a.ZrodloZmiany,''), ISNULL(a.NazwaPola,'') + ' LP:' + a.RekordID, a.DataZmiany
                FROM dbo.AuditLog_Dostawy a
                WHERE a.UserID = @UID AND a.TypOperacji = 'UPDATE' AND a.DataZmiany >= @S AND a.DataZmiany < @E");

            LoadAllModuleSafe(dt, "Kalendarz", userID, s, e, @"
                SELECT 'Notatka', LEFT(n.Tresc,80), n.DataUtworzenia
                FROM dbo.Notatki n
                WHERE n.KtoStworzyl = TRY_CAST(@UID AS INT) AND n.DataUtworzenia >= @S AND n.DataUtworzenia < @E");

            LoadAllModuleSafe(dt, "Kalendarz", userID, s, e, @"
                SELECT 'Dodal cene ' + Typ, Typ + ' ' + CONVERT(VARCHAR(10), Data, 104) + ': ' + CAST(Cena AS VARCHAR(20)) + ' zl', KiedyDodal
                FROM (
                    SELECT 'Ministerialna' AS Typ, Data, Cena, KtoDodal, KiedyDodal FROM dbo.CenaMinisterialna WHERE KtoDodal = TRY_CAST(@UID AS INT) AND KiedyDodal >= @S AND KiedyDodal < @E
                    UNION ALL
                    SELECT 'Rolnicza', Data, Cena, KtoDodal, KiedyDodal FROM dbo.CenaRolnicza WHERE KtoDodal = TRY_CAST(@UID AS INT) AND KiedyDodal >= @S AND KiedyDodal < @E
                    UNION ALL
                    SELECT 'Tuszka', Data, Cena, KtoDodal, KiedyDodal FROM dbo.CenaTuszki WHERE KtoDodal = TRY_CAST(@UID AS INT) AND KiedyDodal >= @S AND KiedyDodal < @E
                ) c");

            LoadAllModuleSafe(dt, "Specyfikacja", userID, s, e, @"
                SELECT CASE WHEN r.ZatwierdzoneByUserID = @UID AND r.Zatwierdzony = 1 AND r.DataZatwierdzenia >= @S AND r.DataZatwierdzenia < @E
                            THEN 'Wprowadzenie' ELSE 'Weryfikacja' END,
                       'Spec. z ' + CONVERT(VARCHAR(10), r.CalcDate, 104),
                       CASE WHEN r.ZatwierdzoneByUserID = @UID AND r.Zatwierdzony = 1 AND r.DataZatwierdzenia >= @S AND r.DataZatwierdzenia < @E
                            THEN r.DataZatwierdzenia ELSE r.DataWeryfikacji END
                FROM dbo.RozliczeniaZatwierdzenia r
                WHERE (r.ZatwierdzoneByUserID = @UID AND r.Zatwierdzony = 1 AND r.DataZatwierdzenia >= @S AND r.DataZatwierdzenia < @E)
                   OR (r.ZweryfikowaneByUserID = @UID AND r.Zweryfikowany = 1 AND r.DataWeryfikacji >= @S AND r.DataWeryfikacji < @E)");

            LoadAllModuleSafe(dt, "Hodowcy", userID, s, e, @"
                SELECT pa.TypAktywnosci, ISNULL(ph.Dostawca,'') + ' ' + ISNULL(LEFT(pa.Tresc,80),''), pa.DataUtworzenia
                FROM dbo.Pozyskiwanie_Aktywnosci pa LEFT JOIN dbo.Pozyskiwanie_Hodowcy ph ON pa.HodowcaId = ph.Id
                WHERE pa.UzytkownikId = @UID AND pa.TypAktywnosci IN ('Zmiana statusu','Notatka') AND pa.DataUtworzenia >= @S AND pa.DataUtworzenia < @E AND pa.UzytkownikId <> 'IMPORT'");

            LoadAllModuleSafe(dt, "Wnioski", userID, s, e, @"
                SELECT CASE WHEN cr.RequestedBy = @UID THEN 'Zlozyl' ELSE 'Rozpatrzyl' END,
                       'Dostawca:' + cr.DostawcaID + ' ' + ISNULL(cr.DecyzjaTyp,''),
                       CASE WHEN cr.RequestedBy = @UID THEN cr.RequestedAtUTC ELSE cr.DecyzjaKiedyUTC END
                FROM dbo.DostawcyCR cr
                WHERE (cr.RequestedBy = @UID AND cr.RequestedAtUTC >= @S AND cr.RequestedAtUTC < @E)
                   OR (cr.DecyzjaKto = @UID AND cr.DecyzjaKiedyUTC >= @S AND cr.DecyzjaKiedyUTC < @E)");

            dt.DefaultView.Sort = "[Data Akcji] DESC";
        }

        private void LoadAllModule(DataTable dt, string moduleName, string userID, DateTime s, DateTime e, string sql)
        {
            try
            {
                using (var conn = new SqlConnection(ConnStr))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@UID", userID ?? "");
                    cmd.Parameters.AddWithValue("@S", s);
                    cmd.Parameters.AddWithValue("@E", e);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = dt.NewRow();
                            row["Modul"] = moduleName;
                            row["Akcja"] = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                            row["Szczegol"] = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                            row["Data Akcji"] = reader.IsDBNull(2) ? DBNull.Value : reader.GetValue(2);
                            dt.Rows.Add(row);
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadAllModuleSafe(DataTable dt, string mod, string uid, DateTime s, DateTime e, string sql)
        {
            try { LoadAllModule(dt, mod, uid, s, e, sql); } catch { }
        }

        // ==================== HELPERS ====================

        private void FillTable(DataTable dt, string sql, string userID, DateTime s, DateTime e)
        {
            using (var conn = new SqlConnection(ConnStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@UID", userID ?? "");
                cmd.Parameters.AddWithValue("@S", s);
                cmd.Parameters.AddWithValue("@E", e);
                conn.Open();
                using (var adapter = new SqlDataAdapter(cmd)) { adapter.Fill(dt); }
            }
        }

        private void FillTableSafe(DataTable dt, string sql, string userID, DateTime s, DateTime e)
        {
            try { FillTable(dt, sql, userID, s, e); } catch { }
        }

        private void DgDetails_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var accent = (Color)ColorConverter.ConvertFromString(_accentHex);

            // Set column widths from hints
            if (ColumnWidths.TryGetValue(e.PropertyName, out double w))
            {
                e.Column.Width = new DataGridLength(w);
                e.Column.MinWidth = Math.Min(w, 50);
            }
            else
            {
                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                e.Column.MinWidth = 80;
            }

            // DateTime formatting
            if ((e.PropertyType == typeof(DateTime) || e.PropertyType == typeof(DateTime?))
                && e.Column is DataGridTextColumn dtCol)
            {
                dtCol.Binding = new System.Windows.Data.Binding(e.PropertyName)
                {
                    StringFormat = "dd.MM.yyyy HH:mm"
                };
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(108, 117, 125))));
                style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.5));
                dtCol.ElementStyle = style;
            }

            // Akcja column - bold + accent color
            if (e.PropertyName == "Akcja" && e.Column is DataGridTextColumn akcjaCol)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(accent)));
                akcjaCol.ElementStyle = style;
            }

            // Modul column - bold grey
            if (e.PropertyName == "Modul" && e.Column is DataGridTextColumn modulCol)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(73, 80, 87))));
                modulCol.ElementStyle = style;
            }

            // Dostawca / Hodowca - semibold
            if ((e.PropertyName == "Dostawca" || e.PropertyName == "Hodowca" || e.PropertyName == "Szczegol")
                && e.Column is DataGridTextColumn dostawcaCol)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Medium));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(44, 62, 80))));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                dostawcaCol.ElementStyle = style;
            }

            // Status column
            if ((e.PropertyName == "Status" || e.PropertyName == "Decyzja") && e.Column is DataGridTextColumn statusCol)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(52, 73, 94))));
                statusCol.ElementStyle = style;
            }

            // Numeric columns - right align
            if ((e.PropertyName == "Lp" || e.PropertyName == "LP" || e.PropertyName == "Ilosc"
                || e.PropertyName == "Sztuki" || e.PropertyName == "Waga" || e.PropertyName == "Nr Wniosku")
                && e.Column is DataGridTextColumn numCol)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(52, 73, 94))));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Medium));
                numCol.ElementStyle = style;
            }

            // Tresc / Powod - wrap + trim
            if ((e.PropertyName == "Tresc" || e.PropertyName == "Powod") && e.Column is DataGridTextColumn trescCol)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(108, 117, 125))));
                style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.5));
                trescCol.ElementStyle = style;
            }
        }
    }
}
