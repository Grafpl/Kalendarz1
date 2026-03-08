using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Reklamacje
{
    public partial class FormRozpatrzenieWindow : Window
    {
        private readonly string connectionString;
        private readonly int idReklamacji;
        private readonly string userId;
        private readonly string aktualnyStatus;

        public bool Zapisano { get; private set; }

        // ========================================
        // DEFINICJE STATUSOW
        // ========================================

        internal static readonly Dictionary<string, List<string>> dozwolonePrzejscia = new Dictionary<string, List<string>>
        {
            { "Nowa",            new List<string> { "Przyjeta" } },
            { "Przyjeta",       new List<string> { "Zaakceptowana", "Odrzucona" } },
            { "Zaakceptowana",  new List<string>() },
            { "Odrzucona",      new List<string>() },
            // Legacy
            { "Zamknieta",      new List<string>() },
            { "Zamknięta",      new List<string>() },
            { "W trakcie",      new List<string> { "Przyjeta", "Zaakceptowana", "Odrzucona" } },
            { "W analizie",     new List<string> { "Przyjeta", "Zaakceptowana", "Odrzucona" } },
            { "W trakcie realizacji", new List<string> { "Zaakceptowana", "Odrzucona" } },
            { "Oczekuje na dostawce", new List<string> { "Przyjeta", "Zaakceptowana" } }
        };

        internal static readonly string[] statusPipeline = {
            "Nowa", "Przyjeta", "Zaakceptowana", "Odrzucona"
        };

        internal static readonly Dictionary<string, string> statusKolory = new Dictionary<string, string>
        {
            { "Nowa",            "#3498DB" },
            { "Przyjeta",       "#F39C12" },
            { "Zaakceptowana",  "#27AE60" },
            { "Odrzucona",      "#E74C3C" },
            { "Zamknieta",      "#95A5A6" },
            { "Zamknięta",      "#95A5A6" },
            // Legacy
            { "W trakcie",             "#F39C12" },
            { "W analizie",            "#F39C12" },
            { "W trakcie realizacji",  "#E67E22" },
            { "Oczekuje na dostawce",  "#9B59B6" }
        };

        public FormRozpatrzenieWindow(string connString, int reklamacjaId, string aktualnyStatus, string user)
        {
            connectionString = connString;
            idReklamacji = reklamacjaId;
            this.aktualnyStatus = aktualnyStatus;
            userId = user;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            txtHeader.Text = $"ROZPATRZENIE #{idReklamacji}";
            txtSubheader.Text = $"Aktualny status: {aktualnyStatus}";

            // Rozpatrujacy avatar
            string userName = PobierzNazweUzytkownika(userId);
            txtRozpatrujacyNazwa.Text = userName;
            txtAvatarRozpatrujacy.Text = GetInitials(userName);
            avatarRozpatrujacy.Background = GetAvatarBrush(userName);

            var avatarSource = LoadWpfAvatar(userId, userName, 72);
            if (avatarSource != null)
            {
                imgBrushAvatarRozpatrujacy.ImageSource = avatarSource;
                ellipseAvatarRozpatrujacy.Visibility = Visibility.Visible;
            }

            BudujPipeline();

            // Status dropdown
            bool isAdmin = userId == "11111";
            if (isAdmin)
            {
                foreach (var s in statusPipeline)
                    if (s != aktualnyStatus)
                        cmbStatus.Items.Add(s);
            }
            else if (dozwolonePrzejscia.ContainsKey(aktualnyStatus))
            {
                foreach (var s in dozwolonePrzejscia[aktualnyStatus])
                    cmbStatus.Items.Add(s);
            }

            if (cmbStatus.Items.Count > 0)
                cmbStatus.SelectedIndex = 0;
            else
            {
                cmbStatus.Items.Add(aktualnyStatus);
                cmbStatus.SelectedIndex = 0;
                cmbStatus.IsEnabled = false;
                btnZapisz.IsEnabled = false;
            }

            WczytajIstniejaceDane();
            WczytajHistorie();
        }

        // ========================================
        // AVATAR HELPERS
        // ========================================

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        internal static ImageSource LoadWpfAvatar(string odbiorcaId, string name, int size)
        {
            if (string.IsNullOrEmpty(odbiorcaId)) return null;
            try
            {
                using (var img = UserAvatarManager.GetAvatarRounded(odbiorcaId, size))
                {
                    if (img == null) return null;
                    using (var bmp = new System.Drawing.Bitmap(img))
                    {
                        var hBitmap = bmp.GetHbitmap();
                        try
                        {
                            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            source.Freeze();
                            return source;
                        }
                        finally { DeleteObject(hBitmap); }
                    }
                }
            }
            catch { return null; }
        }

        // ========================================
        // PIPELINE VISUAL
        // ========================================

        private void BudujPipeline()
        {
            pipelinePanel.Children.Clear();
            int currentIdx = Array.IndexOf(statusPipeline, aktualnyStatus);
            // Legacy status mapping
            if (currentIdx < 0)
            {
                if (aktualnyStatus == "Zamknięta" || aktualnyStatus == "Zamknieta") currentIdx = 2; // map to Zaakceptowana position
                else if (aktualnyStatus == "W trakcie" || aktualnyStatus == "W analizie" ||
                         aktualnyStatus == "W trakcie realizacji" || aktualnyStatus == "Oczekuje na dostawce")
                    currentIdx = 1; // map to Przyjeta position
            }

            for (int i = 0; i < statusPipeline.Length; i++)
            {
                string s = statusPipeline[i];
                string hexColor = statusKolory.ContainsKey(s) ? statusKolory[s] : "#BDC3C7";
                var color = (Color)ColorConverter.ConvertFromString(hexColor);

                bool isCurrent = (i == currentIdx);
                bool isPast = (i < currentIdx);

                // For branching statuses (Zaakceptowana/Odrzucona), only highlight the actual one
                if (i >= 2 && currentIdx >= 2 && i != currentIdx)
                {
                    isCurrent = false;
                    isPast = false;
                }

                var border = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 2, 0),
                    Background = isCurrent ? new SolidColorBrush(color) :
                                 isPast ? new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)) :
                                 new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    BorderThickness = isCurrent ? new Thickness(2) : new Thickness(1),
                    BorderBrush = isCurrent ? new SolidColorBrush(color) :
                                  isPast ? new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)) :
                                  new SolidColorBrush(Color.FromRgb(220, 220, 220))
                };

                var txt = new TextBlock
                {
                    Text = s,
                    FontSize = isCurrent ? 11 : 10,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isCurrent ? Brushes.White :
                                 isPast ? new SolidColorBrush(color) :
                                 new SolidColorBrush(Color.FromRgb(180, 180, 180))
                };

                border.Child = txt;
                pipelinePanel.Children.Add(border);

                if (i < statusPipeline.Length - 1)
                {
                    // Show "/" between Zaakceptowana and Odrzucona
                    string separator = (i == 2) ? "/" : "\u25B8";
                    pipelinePanel.Children.Add(new TextBlock
                    {
                        Text = separator,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(3, 0, 3, 0)
                    });
                }
            }
        }

        // ========================================
        // DATA LOADING
        // ========================================

        private void WczytajIstniejaceDane()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT PrzyczynaGlowna, AkcjeNaprawcze FROM [dbo].[Reklamacje] WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["PrzyczynaGlowna"] != DBNull.Value)
                                    txtPrzyczyna.Text = reader["PrzyczynaGlowna"].ToString();
                                if (reader["AkcjeNaprawcze"] != DBNull.Value)
                                    txtAkcje.Text = reader["AkcjeNaprawcze"].ToString();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void WczytajHistorie()
        {
            try
            {
                var items = new ObservableCollection<HistoriaMiniItem>();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 6 h.DataZmiany, h.PoprzedniStatus, h.StatusNowy,
                               ISNULL(o.Name, h.UserID) AS Uzytkownik
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
                                string statusNowy = reader["StatusNowy"] != DBNull.Value ? reader["StatusNowy"].ToString() : "";
                                string hexColor = statusKolory.ContainsKey(statusNowy) ? statusKolory[statusNowy] : "#BDC3C7";

                                items.Add(new HistoriaMiniItem
                                {
                                    PoprzedniStatus = reader["PoprzedniStatus"] != DBNull.Value ? reader["PoprzedniStatus"].ToString() : "",
                                    StatusNowy = statusNowy,
                                    Uzytkownik = reader["Uzytkownik"] != DBNull.Value ? reader["Uzytkownik"].ToString() : "",
                                    Data = reader["DataZmiany"] != DBNull.Value ? Convert.ToDateTime(reader["DataZmiany"]).ToString("dd.MM HH:mm") : "",
                                    Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor))
                                });
                            }
                        }
                    }
                }
                icHistoria.ItemsSource = items;
            }
            catch { }
        }

        // ========================================
        // STATUS CHANGE HANDLER
        // ========================================

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // No special handling needed - all fields are optional
        }

        // ========================================
        // SAVE
        // ========================================

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            string nowyStatus = cmbStatus.SelectedItem?.ToString() ?? aktualnyStatus;
            string przyczyna = txtPrzyczyna.Text.Trim();
            string akcje = txtAkcje.Text.Trim();
            string komentarz = txtKomentarz.Text.Trim();

            try
            {
                btnZapisz.IsEnabled = false;

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var setClauses = new List<string>
                    {
                        "Status = @NowyStatus",
                        "OsobaRozpatrujaca = @UserID",
                        "DataModyfikacji = GETDATE()"
                    };

                    if (!string.IsNullOrEmpty(przyczyna))
                        setClauses.Add("PrzyczynaGlowna = @PrzyczynaGlowna");
                    if (!string.IsNullOrEmpty(akcje))
                        setClauses.Add("AkcjeNaprawcze = @AkcjeNaprawcze");
                    if (!string.IsNullOrEmpty(komentarz))
                        setClauses.Add("Komentarz = ISNULL(Komentarz, '') + CHAR(13) + CHAR(10) + @NowyKomentarz");

                    string updateSql = $"UPDATE [dbo].[Reklamacje] SET {string.Join(", ", setClauses)} WHERE Id = @Id";

                    using (var cmd = new SqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.Parameters.AddWithValue("@NowyStatus", nowyStatus);
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        if (!string.IsNullOrEmpty(przyczyna))
                            cmd.Parameters.AddWithValue("@PrzyczynaGlowna", przyczyna);
                        if (!string.IsNullOrEmpty(akcje))
                            cmd.Parameters.AddWithValue("@AkcjeNaprawcze", akcje);
                        if (!string.IsNullOrEmpty(komentarz))
                            cmd.Parameters.AddWithValue("@NowyKomentarz", $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {userId}: {komentarz}");
                        cmd.ExecuteNonQuery();
                    }

                    // History
                    string histKomentarz = "";
                    if (!string.IsNullOrEmpty(komentarz))
                        histKomentarz = komentarz;
                    if (!string.IsNullOrEmpty(przyczyna))
                        histKomentarz += (histKomentarz.Length > 0 ? "\n" : "") + $"[Przyczyna] {przyczyna}";
                    if (!string.IsNullOrEmpty(akcje))
                        histKomentarz += (histKomentarz.Length > 0 ? "\n" : "") + $"[Akcje] {akcje}";

                    if (string.IsNullOrEmpty(histKomentarz))
                        histKomentarz = $"Zmiana statusu: {aktualnyStatus} -> {nowyStatus}";

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [dbo].[ReklamacjeHistoria]
                            (IdReklamacji, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji)
                        VALUES
                            (@IdReklamacji, @UserID, @PoprzedniStatus, @StatusNowy, @Komentarz, 'Rozpatrzenie')", conn))
                    {
                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        cmd.Parameters.AddWithValue("@PoprzedniStatus", aktualnyStatus);
                        cmd.Parameters.AddWithValue("@StatusNowy", nowyStatus);
                        cmd.Parameters.AddWithValue("@Komentarz", histKomentarz);
                        cmd.ExecuteNonQuery();
                    }
                }

                Zapisano = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                PokazBlad($"Blad zapisu: {ex.Message}");
                btnZapisz.IsEnabled = true;
            }
        }

        private void PokazBlad(string msg)
        {
            txtError.Text = msg;
            txtError.Visibility = Visibility.Visible;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ========================================
        // HELPERS
        // ========================================

        private string PobierzNazweUzytkownika(string id)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT Name FROM operators WHERE ID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            return result.ToString();
                    }
                }
            }
            catch { }
            return id;
        }

        internal static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[parts.Length - 1][0])}";
            return char.ToUpper(parts[0][0]).ToString();
        }

        internal static SolidColorBrush GetAvatarBrush(string name)
        {
            string[] palette = {
                "#1ABC9C", "#2ECC71", "#3498DB", "#9B59B6", "#E67E22",
                "#E74C3C", "#16A085", "#27AE60", "#2980B9", "#8E44AD"
            };
            int hash = 0;
            if (!string.IsNullOrEmpty(name))
                foreach (char c in name) hash = hash * 31 + c;
            int idx = Math.Abs(hash) % palette.Length;
            var color = (Color)ColorConverter.ConvertFromString(palette[idx]);
            return new SolidColorBrush(color);
        }

        internal static string GetStatusColor(string status)
        {
            return statusKolory.ContainsKey(status) ? statusKolory[status] : "#BDC3C7";
        }
    }

    public class HistoriaMiniItem
    {
        public string PoprzedniStatus { get; set; }
        public string StatusNowy { get; set; }
        public string Uzytkownik { get; set; }
        public string Data { get; set; }
        public SolidColorBrush Kolor { get; set; }
    }
}
