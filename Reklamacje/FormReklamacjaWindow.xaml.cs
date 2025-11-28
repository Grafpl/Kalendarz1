using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Kalendarz1.Reklamacje
{
    public partial class FormReklamacjaWindow : Window
    {
        private string connectionStringHandel;
        private string connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private int idDokumentu;
        private int idKontrahenta;
        private string numerDokumentu;
        private string nazwaKontrahenta;
        private string userId;

        private ObservableCollection<TowarReklamacji> towary = new ObservableCollection<TowarReklamacji>();
        private ObservableCollection<PartiaDostawcy> partie = new ObservableCollection<PartiaDostawcy>();
        private List<string> sciezkiZdjec = new List<string>();

        public bool ReklamacjaZapisana { get; private set; } = false;

        public FormReklamacjaWindow(string connStringHandel, int dokId, int kontrId, string nrDok, string nazwaKontr, string user)
        {
            InitializeComponent();

            connectionStringHandel = connStringHandel;
            idDokumentu = dokId;
            idKontrahenta = kontrId;
            numerDokumentu = nrDok;
            nazwaKontrahenta = nazwaKontr;
            userId = user;

            txtKontrahent.Text = nazwaKontrahenta;
            txtFaktura.Text = numerDokumentu;

            dgTowary.ItemsSource = towary;
            dgPartie.ItemsSource = partie;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajTowary();
            WczytajPartie();
            AktualizujLiczniki();
        }

        private void WczytajTowary()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringHandel))
                {
                    conn.Open();

                    // Pobierz towary z faktury - oblicz wartość jako ilość * cena
                    string query = @"
                        SELECT
                            DP.id AS ID,
                            DP.kod AS Symbol,
                            ISNULL(TW.nazwa, TW.kod) AS Nazwa,
                            CAST(ISNULL(DP.ilosc, 0) AS DECIMAL(10,2)) AS Waga,
                            CAST(ISNULL(DP.cena, 0) AS DECIMAL(10,2)) AS Cena,
                            CAST(ISNULL(DP.ilosc, 0) * ISNULL(DP.cena, 0) AS DECIMAL(10,2)) AS Wartosc
                        FROM [HM].[DP] DP
                        LEFT JOIN [HM].[TW] TW ON DP.idtw = TW.ID
                        WHERE DP.super = @IdDokumentu
                        ORDER BY DP.lp";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdDokumentu", idDokumentu);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            towary.Clear();
                            while (reader.Read())
                            {
                                towary.Add(new TowarReklamacji
                                {
                                    ID = reader.GetInt32(0),
                                    Symbol = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    Nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Waga = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                    Cena = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                                    Wartosc = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania towarów:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajPartie()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();

                    // Sprawdź czy tabela istnieje
                    string checkTable = @"
                        SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_NAME = 'PartiaDostawca'";

                    using (SqlCommand cmdCheck = new SqlCommand(checkTable, conn))
                    {
                        int tableExists = Convert.ToInt32(cmdCheck.ExecuteScalar());
                        if (tableExists == 0)
                        {
                            txtPartieInfo.Text = "Tabela partii nie istnieje";
                            return;
                        }
                    }

                    string query = @"
                        SELECT
                            CAST([guid] AS NVARCHAR(100)) AS GuidStr,
                            [Partia],
                            [CustomerID],
                            [CustomerName],
                            CONVERT(VARCHAR, [CreateData], 104) + ' ' + LEFT(CAST([CreateGodzina] AS VARCHAR), 8) AS DataUtw
                        FROM [dbo].[PartiaDostawca]
                        WHERE [CreateData] >= DATEADD(DAY, -14, GETDATE())
                        ORDER BY [CreateData] DESC, [CreateGodzina] DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            partie.Clear();
                            while (reader.Read())
                            {
                                string guidStr = reader.IsDBNull(0) ? "" : reader.GetString(0);
                                Guid parsedGuid = Guid.Empty;
                                Guid.TryParse(guidStr, out parsedGuid);

                                partie.Add(new PartiaDostawcy
                                {
                                    GuidPartii = parsedGuid,
                                    GuidPartiiStr = guidStr,
                                    NrPartii = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    IdDostawcy = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NazwaDostawcy = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    DataUtworzenia = reader.IsDBNull(4) ? "" : reader.GetString(4)
                                });
                            }
                        }
                    }

                    if (partie.Count == 0)
                    {
                        txtPartieInfo.Text = "Brak partii z ostatnich 14 dni";
                    }
                }
            }
            catch (Exception ex)
            {
                txtPartieInfo.Text = $"Błąd: {ex.Message}";
            }
        }

        // Obsługa checkboxów dla towarów
        private void ChkTowar_Click(object sender, RoutedEventArgs e)
        {
            AktualizujLiczniki();
        }

        private void ChkWszystkieTowary_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            bool isChecked = checkBox?.IsChecked ?? false;
            foreach (var towar in towary)
            {
                towar.IsSelected = isChecked;
            }
            AktualizujLiczniki();
        }

        // Obsługa checkboxów dla partii
        private void ChkPartia_Click(object sender, RoutedEventArgs e)
        {
            AktualizujLiczniki();
        }

        private void ChkWszystkiePartie_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            bool isChecked = checkBox?.IsChecked ?? false;
            foreach (var partia in partie)
            {
                partia.IsSelected = isChecked;
            }
            AktualizujLiczniki();
        }

        private void AktualizujLiczniki()
        {
            int liczbaTowary = towary.Count(t => t.IsSelected);
            int liczbaPartii = partie.Count(p => p.IsSelected);
            int liczbaZdjec = sciezkiZdjec.Count;

            txtLicznikTowary.Text = $"{liczbaTowary} towar(ów)";
            txtLicznikPartie.Text = $"{liczbaPartii} parti(i)";
            txtLicznikZdjecia.Text = $"{liczbaZdjec} zdjęć";

            // Suma kg i wartości
            decimal sumaKg = towary.Where(t => t.IsSelected).Sum(t => t.Waga);
            decimal sumaWartosc = towary.Where(t => t.IsSelected).Sum(t => t.Wartosc);
            txtSumaKg.Text = $"{sumaKg:N2} kg";
            txtSumaWartosc.Text = $"{sumaWartosc:N2} zł";
        }

        private void ListZdjecia_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listZdjecia.SelectedIndex >= 0 && listZdjecia.SelectedIndex < sciezkiZdjec.Count)
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
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

        private void ImgPodglad_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (listZdjecia.SelectedIndex >= 0 && listZdjecia.SelectedIndex < sciezkiZdjec.Count)
            {
                // Otwórz okno z powiększonym podglądem
                var previewWindow = new Window
                {
                    Title = "Podgląd zdjęcia - kliknij aby zamknąć",
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.None,
                    Background = System.Windows.Media.Brushes.Black,
                    Cursor = Cursors.Hand
                };

                var image = new Image
                {
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(20)
                };

                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(sciezkiZdjec[listZdjecia.SelectedIndex]);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    image.Source = bitmap;
                }
                catch
                {
                    return;
                }

                previewWindow.Content = image;
                previewWindow.MouseLeftButtonDown += (s, args) => previewWindow.Close();
                previewWindow.KeyDown += (s, args) =>
                {
                    if (args.Key == Key.Escape) previewWindow.Close();
                };

                previewWindow.ShowDialog();
            }
        }

        private void BtnDodajZdjecia_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Pliki graficzne|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Wszystkie pliki|*.*",
                Multiselect = true,
                Title = "Wybierz zdjęcia do reklamacji"
            };

            if (ofd.ShowDialog() == true)
            {
                foreach (string plik in ofd.FileNames)
                {
                    if (!sciezkiZdjec.Contains(plik))
                    {
                        sciezkiZdjec.Add(plik);
                        listZdjecia.Items.Add(Path.GetFileName(plik));
                    }
                }
                AktualizujLiczniki();

                // Automatycznie zaznacz pierwsze zdjęcie
                if (listZdjecia.Items.Count > 0 && listZdjecia.SelectedIndex < 0)
                {
                    listZdjecia.SelectedIndex = 0;
                }
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
                AktualizujLiczniki();
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnZgloszReklamacje_Click(object sender, RoutedEventArgs e)
        {
            // Walidacja
            var zaznaczoneTowary = towary.Where(t => t.IsSelected).ToList();
            if (zaznaczoneTowary.Count == 0)
            {
                MessageBox.Show("Zaznacz przynajmniej jeden towar do reklamacji!",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtOpis.Text))
            {
                MessageBox.Show("Wprowadź opis problemu!",
                    "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtOpis.Focus();
                return;
            }

            // Oblicz sumy
            decimal sumaKg = zaznaczoneTowary.Sum(t => t.Waga);
            decimal sumaWartosc = zaznaczoneTowary.Sum(t => t.Wartosc);

            // Pobierz typ i priorytet
            string typReklamacji = (cmbTypReklamacji.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Inne";
            string priorytet = "Normalny";
            if (cmbPriorytet.SelectedItem is ComboBoxItem item)
            {
                var panel = item.Content as StackPanel;
                if (panel != null && panel.Children.Count > 1)
                {
                    var textBlock = panel.Children[1] as TextBlock;
                    priorytet = textBlock?.Text ?? "Normalny";
                }
            }

            int idReklamacji = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionStringLibraNet))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Zapisz główny rekord reklamacji
                            string queryReklamacja = @"
                                INSERT INTO [dbo].[Reklamacje]
                                (DataZgloszenia, UserID, IdDokumentu, NumerDokumentu, IdKontrahenta, NazwaKontrahenta, Opis, SumaKg, SumaWartosc, Status, TypReklamacji, Priorytet)
                                VALUES
                                (GETDATE(), @UserID, @IdDokumentu, @NumerDokumentu, @IdKontrahenta, @NazwaKontrahenta, @Opis, @SumaKg, @SumaWartosc, 'Nowa', @TypReklamacji, @Priorytet);
                                SELECT SCOPE_IDENTITY();";

                            using (SqlCommand cmd = new SqlCommand(queryReklamacja, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.Parameters.AddWithValue("@IdDokumentu", idDokumentu);
                                cmd.Parameters.AddWithValue("@NumerDokumentu", numerDokumentu);
                                cmd.Parameters.AddWithValue("@IdKontrahenta", idKontrahenta);
                                cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);
                                cmd.Parameters.AddWithValue("@Opis", txtOpis.Text.Trim());
                                cmd.Parameters.AddWithValue("@SumaKg", sumaKg);
                                cmd.Parameters.AddWithValue("@SumaWartosc", sumaWartosc);
                                cmd.Parameters.AddWithValue("@TypReklamacji", typReklamacji);
                                cmd.Parameters.AddWithValue("@Priorytet", priorytet);

                                idReklamacji = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 2. Zapisz towary
                            string queryTowary = @"
                                INSERT INTO [dbo].[ReklamacjeTowary]
                                (IdReklamacji, IdTowaru, Symbol, Nazwa, Waga, Cena, Wartosc)
                                VALUES
                                (@IdReklamacji, @IdTowaru, @Symbol, @Nazwa, @Waga, @Cena, @Wartosc)";

                            foreach (TowarReklamacji towar in zaznaczoneTowary)
                            {
                                using (SqlCommand cmd = new SqlCommand(queryTowary, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                    cmd.Parameters.AddWithValue("@IdTowaru", towar.ID);
                                    cmd.Parameters.AddWithValue("@Symbol", towar.Symbol ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Nazwa", towar.Nazwa ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Waga", towar.Waga);
                                    cmd.Parameters.AddWithValue("@Cena", towar.Cena);
                                    cmd.Parameters.AddWithValue("@Wartosc", towar.Wartosc);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // 3. Zapisz partie (jeśli są)
                            var zaznaczonePartie = partie.Where(p => p.IsSelected).ToList();
                            if (zaznaczonePartie.Count > 0)
                            {
                                string queryPartie = @"
                                    INSERT INTO [dbo].[ReklamacjePartie]
                                    (IdReklamacji, GuidPartii, NumerPartii, CustomerID, CustomerName)
                                    VALUES
                                    (@IdReklamacji, @GuidPartii, @NumerPartii, @CustomerID, @CustomerName)";

                                foreach (PartiaDostawcy partia in zaznaczonePartie)
                                {
                                    using (SqlCommand cmd = new SqlCommand(queryPartie, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                        cmd.Parameters.AddWithValue("@GuidPartii", partia.GuidPartii != Guid.Empty ? (object)partia.GuidPartii : DBNull.Value);
                                        cmd.Parameters.AddWithValue("@NumerPartii", partia.NrPartii ?? (object)DBNull.Value);
                                        cmd.Parameters.AddWithValue("@CustomerID", partia.IdDostawcy ?? (object)DBNull.Value);
                                        cmd.Parameters.AddWithValue("@CustomerName", partia.NazwaDostawcy ?? (object)DBNull.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 4. Zapisz zdjęcia (jeśli są)
                            if (sciezkiZdjec.Count > 0)
                            {
                                string folderReklamacji = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                    "ReklamacjeZdjecia",
                                    idReklamacji.ToString());

                                Directory.CreateDirectory(folderReklamacji);

                                string queryZdjecia = @"
                                    INSERT INTO [dbo].[ReklamacjeZdjecia]
                                    (IdReklamacji, NazwaPliku, SciezkaPliku, DodanePrzez)
                                    VALUES
                                    (@IdReklamacji, @NazwaPliku, @SciezkaPliku, @DodanePrzez)";

                                foreach (string sciezkaZrodlowa in sciezkiZdjec)
                                {
                                    string nazwaPliku = Path.GetFileName(sciezkaZrodlowa);
                                    string nowaSciezka = Path.Combine(folderReklamacji, nazwaPliku);

                                    File.Copy(sciezkaZrodlowa, nowaSciezka, true);

                                    using (SqlCommand cmd = new SqlCommand(queryZdjecia, conn, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                        cmd.Parameters.AddWithValue("@NazwaPliku", nazwaPliku);
                                        cmd.Parameters.AddWithValue("@SciezkaPliku", nowaSciezka);
                                        cmd.Parameters.AddWithValue("@DodanePrzez", userId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // 5. Dodaj wpis do historii
                            string queryHistoria = @"
                                INSERT INTO [dbo].[ReklamacjeHistoria]
                                (IdReklamacji, UserID, StatusNowy, Komentarz, TypAkcji)
                                VALUES
                                (@IdReklamacji, @UserID, 'Nowa', 'Utworzenie reklamacji', 'Utworzenie')";

                            using (SqlCommand cmd = new SqlCommand(queryHistoria, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                cmd.Parameters.AddWithValue("@UserID", userId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            MessageBox.Show(
                                $"Reklamacja nr {idReklamacji} została pomyślnie zgłoszona!\n\n" +
                                $"Typ: {typReklamacji}\n" +
                                $"Priorytet: {priorytet}\n" +
                                $"Towarów: {zaznaczoneTowary.Count}\n" +
                                $"Suma kg: {sumaKg:N2}\n" +
                                $"Wartość: {sumaWartosc:N2} zł",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            ReklamacjaZapisana = true;
                            this.DialogResult = true;
                            this.Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"Błąd podczas zapisywania: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania reklamacji:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Klasy pomocnicze
    public class TowarReklamacji : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public int ID { get; set; }
        public string Symbol { get; set; }
        public string Nazwa { get; set; }
        public decimal Waga { get; set; }
        public decimal Cena { get; set; }
        public decimal Wartosc { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public class PartiaDostawcy : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public Guid GuidPartii { get; set; }
        public string GuidPartiiStr { get; set; }
        public string NrPartii { get; set; }
        public string IdDostawcy { get; set; }
        public string NazwaDostawcy { get; set; }
        public string DataUtworzenia { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }
}
