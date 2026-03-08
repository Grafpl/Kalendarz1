using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Reklamacje
{
    public partial class UzupelnijReklamacjeWindow : Window
    {
        private readonly string connectionString;
        private readonly int idReklamacji;
        private readonly string userId;
        private readonly List<string> sciezkiZdjec = new List<string>();
        private int idDokumentu;

        private const string HandelConnString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        private static readonly HashSet<string> dozwoloneRozszerzenia = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif"
        };

        public UzupelnijReklamacjeWindow(string connString, int reklamacjaId, string user)
        {
            connectionString = connString;
            idReklamacji = reklamacjaId;
            userId = user;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            WczytajDane();
            WczytajTowary();
            WczytajIstniejaceZdjecia();
        }

        // ========================================
        // WCZYTYWANIE DANYCH
        // ========================================

        private void WczytajDane()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT r.NumerDokumentu, r.NazwaKontrahenta, r.Opis, r.Status,
                               r.TypReklamacji, ISNULL(r.SumaKg, 0) AS SumaKg, ISNULL(r.SumaWartosc, 0) AS SumaWartosc,
                               r.UserID, ISNULL(r.Priorytet, 'Normalny') AS Priorytet,
                               r.DataZgloszenia, r.IdDokumentu
                        FROM [dbo].[Reklamacje] r WHERE r.Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string nrDok = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                string kontrahent = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                string opis = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                string status = reader.IsDBNull(3) ? "Nowa" : reader.GetString(3);
                                string typ = reader.IsDBNull(4) ? "" : reader.GetString(4);
                                decimal sumaKg = reader.GetDecimal(5);
                                decimal sumaWartosc = reader.GetDecimal(6);
                                string handlowiecId = reader.IsDBNull(7) ? "" : reader.GetString(7);
                                string priorytet = reader.IsDBNull(8) ? "Normalny" : reader.GetString(8);
                                DateTime? dataZgl = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9);
                                idDokumentu = reader.IsDBNull(10) ? 0 : reader.GetInt32(10);

                                txtHeader.Text = $"UZUPELNIJ REKLAMACJE #{idReklamacji}";
                                txtSubheader.Text = $"{nrDok}  |  {kontrahent}";
                                txtNrDok.Text = nrDok;
                                txtKontrahent.Text = kontrahent;
                                txtTyp.Text = typ;
                                txtStatus.Text = status;
                                txtWaga.Text = $"{sumaKg:#,##0.00} kg";
                                txtWartosc.Text = $"{sumaWartosc:#,##0.00} zl";
                                txtOpis.Text = opis;

                                // Data zgloszenia
                                if (dataZgl.HasValue)
                                {
                                    txtDataZgloszenia.Text = dataZgl.Value.ToString("dd.MM.yyyy HH:mm");
                                    int dni = (DateTime.Now - dataZgl.Value).Days;
                                    txtDniOdZgloszenia.Text = $"{dni} dni";
                                    if (dni > 14)
                                        txtDniOdZgloszenia.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                                }

                                // Priorytet
                                UstawPriorytet(priorytet);

                                // Avatar kontrahenta
                                UstawAvatarKontrahenta(kontrahent);

                                // Avatar handlowca
                                UstawAvatarHandlowca(handlowiecId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad wczytywania: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajTowary()
        {
            try
            {
                var towary = new List<TowarReklamacjiInfo>();

                // 1. Sprawdz ReklamacjeTowary w LibraNet
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT rt.Symbol, rt.Nazwa,
                               ISNULL(rt.Waga, 0) AS Waga,
                               ISNULL(rt.Cena, 0) AS Cena,
                               ISNULL(rt.Wartosc, 0) AS Wartosc,
                               rt.PrzyczynaReklamacji
                        FROM [dbo].[ReklamacjeTowary] rt
                        WHERE rt.IdReklamacji = @Id
                        ORDER BY rt.Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                towary.Add(new TowarReklamacjiInfo
                                {
                                    Symbol = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    Nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    Waga = reader.GetDecimal(2),
                                    Cena = reader.GetDecimal(3),
                                    Wartosc = reader.GetDecimal(4),
                                    PrzyczynaReklamacji = reader.IsDBNull(5) ? "" : reader.GetString(5)
                                });
                            }
                        }
                    }
                }

                // 2. Zawsze pobierz z HANDEL jesli jest IdDokumentu (dane zrodlowe, poprawne roznice)
                if (idDokumentu > 0)
                {
                    var towaryHandel = WczytajTowaryZHandel(idDokumentu);
                    if (towaryHandel.Count > 0)
                    {
                        // Usun stare (mogly byc z blednym ABS) i zapisz poprawne
                        if (towary.Count > 0)
                            UsunStareTowary();
                        towary = towaryHandel;
                        ZapiszTowaryDoLibraNet(towary);
                    }
                }

                dgTowary.ItemsSource = towary;
                WyswietlPodsumowanieTowary(towary);
            }
            catch { }
        }

        private List<TowarReklamacjiInfo> WczytajTowaryZHandel(int idDok)
        {
            var towary = new List<TowarReklamacjiInfo>();
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
                                towary.Add(new TowarReklamacjiInfo
                                {
                                    Symbol = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    Nazwa = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    Waga = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                                    Cena = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)),
                                    Wartosc = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4)),
                                    PrzyczynaReklamacji = "Korekta"
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return towary;
        }

        private void UsunStareTowary()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("DELETE FROM [dbo].[ReklamacjeTowary] WHERE IdReklamacji = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        private void ZapiszTowaryDoLibraNet(List<TowarReklamacjiInfo> towary)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    foreach (var t in towary)
                    {
                        using (var cmd = new SqlCommand(@"
                            INSERT INTO [dbo].[ReklamacjeTowary]
                            (IdReklamacji, IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc, PrzyczynaReklamacji)
                            VALUES (@IdRek, 0, @Symbol, @Nazwa, @Waga, @Cena, @Wartosc, @Przyczyna)", conn))
                        {
                            cmd.Parameters.AddWithValue("@IdRek", idReklamacji);
                            cmd.Parameters.AddWithValue("@Symbol", t.Symbol ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Nazwa", t.Nazwa ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Waga", t.Waga);
                            cmd.Parameters.AddWithValue("@Cena", t.Cena);
                            cmd.Parameters.AddWithValue("@Wartosc", t.Wartosc);
                            cmd.Parameters.AddWithValue("@Przyczyna", t.PrzyczynaReklamacji ?? (object)DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { }
        }

        private void WyswietlPodsumowanieTowary(List<TowarReklamacjiInfo> towary)
        {
            if (towary.Count > 0)
            {
                txtTowaryCount.Text = $"  ({towary.Count} pozycji)";

                decimal sumaKg = towary.Sum(t => t.Waga);
                decimal sumaWartosc = towary.Sum(t => t.Wartosc);
                decimal sredniaCena = sumaKg > 0 ? sumaWartosc / sumaKg : 0;

                txtPodsumowaniePozycji.Text = $"{towary.Count} pozycji";
                txtSumaKgTowarow.Text = $"{sumaKg:#,##0.00} kg";
                txtSumaWartoscTowarow.Text = $"{sumaWartosc:#,##0.00} zl";
                txtSredniaCena.Text = $"{sredniaCena:#,##0.00} zl/kg";
                borderPodsumowanie.Visibility = Visibility.Visible;
            }
            else
            {
                txtTowaryCount.Text = "  (brak pozycji)";
            }
        }

        // ========================================
        // PRIORYTET
        // ========================================

        private void UstawPriorytet(string priorytet)
        {
            string[] priorytety = { "Niski", "Normalny", "Wysoki", "Krytyczny" };
            int index = Array.FindIndex(priorytety, p => p.Equals(priorytet, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index < cmbPriorytet.Items.Count)
                cmbPriorytet.SelectedIndex = index;
            else
                cmbPriorytet.SelectedIndex = 1;
        }

        private string PobierzWybranyPriorytet()
        {
            if (cmbPriorytet.SelectedItem is ComboBoxItem item)
            {
                var panel = item.Content as StackPanel;
                if (panel != null && panel.Children.Count > 1)
                {
                    var textBlock = panel.Children[1] as TextBlock;
                    return textBlock?.Text ?? "Normalny";
                }
            }
            return "Normalny";
        }

        // ========================================
        // AVATARY
        // ========================================

        private void UstawAvatarKontrahenta(string kontrahent)
        {
            string initials = FormRozpatrzenieWindow.GetInitials(kontrahent);
            txtAvatarKontrahent.Text = initials;
            avatarKontrahent.Background = FormRozpatrzenieWindow.GetAvatarBrush(kontrahent);
        }

        private void UstawAvatarHandlowca(string handlowiecId)
        {
            if (string.IsNullOrEmpty(handlowiecId)) return;

            string handlowiecName = PobierzNazweUzytkownika(handlowiecId);
            txtHandlowiecNazwa.Text = handlowiecName;
            txtAvatarHandlowiec.Text = FormRozpatrzenieWindow.GetInitials(handlowiecName);
            avatarHandlowiec.Background = FormRozpatrzenieWindow.GetAvatarBrush(handlowiecName);

            var avatarSource = FormRozpatrzenieWindow.LoadWpfAvatar(handlowiecId, handlowiecName, 84);
            if (avatarSource != null)
            {
                imgBrushAvatarHandlowiec.ImageSource = avatarSource;
                ellipseAvatarHandlowiec.Visibility = Visibility.Visible;
            }
        }

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

        // ========================================
        // ZDJECIA
        // ========================================

        private void WczytajIstniejaceZdjecia()
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ReklamacjeZdjecia", idReklamacji.ToString());

                if (Directory.Exists(folder))
                {
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        if (dozwoloneRozszerzenia.Contains(Path.GetExtension(file)))
                        {
                            sciezkiZdjec.Add(file);
                            listZdjecia.Items.Add(Path.GetFileName(file));
                        }
                    }
                }

                AktualizujLicznik();
                if (listZdjecia.Items.Count > 0)
                    listZdjecia.SelectedIndex = 0;
            }
            catch { }
        }

        private void ListZdjecia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listZdjecia.SelectedIndex >= 0 && listZdjecia.SelectedIndex < sciezkiZdjec.Count)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(sciezkiZdjec[listZdjecia.SelectedIndex]);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgPodglad.Source = bitmap;
                    txtBrakZdjecia.Visibility = Visibility.Collapsed;
                    btnUsunZdjecie.IsEnabled = true;
                }
                catch
                {
                    imgPodglad.Source = null;
                    txtBrakZdjecia.Visibility = Visibility.Visible;
                }
            }
            else
            {
                imgPodglad.Source = null;
                txtBrakZdjecia.Visibility = Visibility.Visible;
                btnUsunZdjecie.IsEnabled = false;
            }
        }

        private void BtnDodajZdjecia_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Pliki graficzne|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Wszystkie pliki|*.*",
                Multiselect = true,
                Title = "Wybierz zdjecia"
            };

            if (ofd.ShowDialog() == true)
            {
                foreach (string plik in ofd.FileNames)
                    DodajPlik(plik);
            }
        }

        private void BtnUsunZdjecie_Click(object sender, RoutedEventArgs e)
        {
            if (listZdjecia.SelectedIndex >= 0)
            {
                int index = listZdjecia.SelectedIndex;
                sciezkiZdjec.RemoveAt(index);
                listZdjecia.Items.RemoveAt(index);
                imgPodglad.Source = null;
                txtBrakZdjecia.Visibility = Visibility.Visible;
                btnUsunZdjecie.IsEnabled = false;
                AktualizujLicznik();
            }
        }

        private void DodajPlik(string plik)
        {
            if (!dozwoloneRozszerzenia.Contains(Path.GetExtension(plik))) return;
            if (sciezkiZdjec.Contains(plik)) return;

            sciezkiZdjec.Add(plik);
            listZdjecia.Items.Add(Path.GetFileName(plik));
            AktualizujLicznik();

            if (listZdjecia.SelectedIndex < 0)
                listZdjecia.SelectedIndex = 0;
        }

        private void AktualizujLicznik()
        {
            txtLicznikZdjec.Text = sciezkiZdjec.Count > 0 ? $"{sciezkiZdjec.Count} zdjec" : "";
        }

        // Drag & drop
        private void ZdjeciaPanel_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var pliki = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = pliki.Any(f => dozwoloneRozszerzenia.Contains(Path.GetExtension(f)))
                    ? DragDropEffects.Copy : DragDropEffects.None;
            }
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void ZdjeciaPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var pliki = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (pliki.Any(f => dozwoloneRozszerzenia.Contains(Path.GetExtension(f))))
                    dropOverlay.Visibility = Visibility.Visible;
            }
        }

        private void ZdjeciaPanel_DragLeave(object sender, DragEventArgs e)
        {
            dropOverlay.Visibility = Visibility.Collapsed;
        }

        private void ZdjeciaPanel_Drop(object sender, DragEventArgs e)
        {
            dropOverlay.Visibility = Visibility.Collapsed;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            foreach (string plik in (string[])e.Data.GetData(DataFormats.FileDrop))
                DodajPlik(plik);
        }

        // ========================================
        // ZAPIS
        // ========================================

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            string opis = txtOpis.Text.Trim();
            string priorytet = PobierzWybranyPriorytet();

            if (string.IsNullOrEmpty(opis))
            {
                txtError.Text = "Opis jest wymagany - opisz co bylo nie tak.";
                txtError.Visibility = Visibility.Visible;
                txtOpis.Focus();
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SqlCommand(@"
                                UPDATE [dbo].[Reklamacje]
                                SET Opis = @Opis,
                                    Priorytet = @Priorytet,
                                    UserID = CASE WHEN UserID = '' OR UserID IS NULL THEN @UserId ELSE UserID END,
                                    DataModyfikacji = GETDATE()
                                WHERE Id = @Id", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Opis", opis);
                                cmd.Parameters.AddWithValue("@Priorytet", priorytet);
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = new SqlCommand(@"
                                INSERT INTO [dbo].[ReklamacjeHistoria]
                                (IdReklamacji, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji)
                                VALUES (@Id, @UserId, '', '', @Komentarz, 'Uzupelnienie')", conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Id", idReklamacji);
                                cmd.Parameters.AddWithValue("@UserId", userId);
                                cmd.Parameters.AddWithValue("@Komentarz", $"Uzupelniono opis, priorytet: {priorytet}, dodano {sciezkiZdjec.Count} zdjec");
                                cmd.ExecuteNonQuery();
                            }

                            if (sciezkiZdjec.Count > 0)
                            {
                                string folder = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                    "ReklamacjeZdjecia", idReklamacji.ToString());
                                Directory.CreateDirectory(folder);

                                bool maBlob = false;
                                try
                                {
                                    using (var cmdCheck = new SqlCommand(
                                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ReklamacjeZdjecia' AND COLUMN_NAME = 'DaneZdjecia'", conn, transaction))
                                    {
                                        maBlob = Convert.ToInt32(cmdCheck.ExecuteScalar()) > 0;
                                    }
                                }
                                catch { }

                                foreach (string sciezka in sciezkiZdjec)
                                {
                                    string nazwa = Path.GetFileName(sciezka);
                                    string nowaSciezka = Path.Combine(folder, nazwa);

                                    if (!sciezka.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                                    {
                                        File.Copy(sciezka, nowaSciezka, true);
                                    }

                                    using (var cmdExists = new SqlCommand(
                                        "SELECT COUNT(*) FROM [dbo].[ReklamacjeZdjecia] WHERE IdReklamacji = @Id AND NazwaPliku = @Nazwa", conn, transaction))
                                    {
                                        cmdExists.Parameters.AddWithValue("@Id", idReklamacji);
                                        cmdExists.Parameters.AddWithValue("@Nazwa", nazwa);
                                        if (Convert.ToInt32(cmdExists.ExecuteScalar()) > 0) continue;
                                    }

                                    string insertSql = maBlob
                                        ? @"INSERT INTO [dbo].[ReklamacjeZdjecia] (IdReklamacji, NazwaPliku, SciezkaPliku, DodanePrzez, DaneZdjecia)
                                            VALUES (@Id, @Nazwa, @Sciezka, @User, @Blob)"
                                        : @"INSERT INTO [dbo].[ReklamacjeZdjecia] (IdReklamacji, NazwaPliku, SciezkaPliku, DodanePrzez)
                                            VALUES (@Id, @Nazwa, @Sciezka, @User)";

                                    using (var cmdIns = new SqlCommand(insertSql, conn, transaction))
                                    {
                                        cmdIns.Parameters.AddWithValue("@Id", idReklamacji);
                                        cmdIns.Parameters.AddWithValue("@Nazwa", nazwa);
                                        cmdIns.Parameters.AddWithValue("@Sciezka", nowaSciezka);
                                        cmdIns.Parameters.AddWithValue("@User", userId);

                                        if (maBlob)
                                        {
                                            string src = sciezka.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? sciezka : nowaSciezka;
                                            byte[] dane = File.ReadAllBytes(src);
                                            cmdIns.Parameters.Add("@Blob", SqlDbType.VarBinary, -1).Value = dane;
                                        }
                                        cmdIns.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                txtError.Text = $"Blad zapisu: {ex.Message}";
                txtError.Visibility = Visibility.Visible;
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    // Model do DataGrid towarow
    public class TowarReklamacjiInfo
    {
        public string Symbol { get; set; }
        public string Nazwa { get; set; }
        public decimal Waga { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }
        public string PrzyczynaReklamacji { get; set; }
    }
}
