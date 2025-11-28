using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
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

                    string query = @"
                        SELECT
                            DP.id AS ID,
                            DP.kod AS Symbol,
                            TW.kod AS Nazwa,
                            CAST(DP.ilosc AS DECIMAL(10,2)) AS Ilosc,
                            CAST(DP.ilosc AS DECIMAL(10,2)) AS Waga
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
                                    Ilosc = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                                    Waga = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
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

                    string query = @"
                        SELECT
                            [guid],
                            [Partia],
                            [CustomerID],
                            [CustomerName],
                            CONVERT(VARCHAR, [CreateData], 104) + ' ' + LEFT([CreateGodzina], 8) AS DataUtw
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
                                partie.Add(new PartiaDostawcy
                                {
                                    GuidPartii = reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0),
                                    NrPartii = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    IdDostawcy = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    NazwaDostawcy = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    DataUtworzenia = reader.IsDBNull(4) ? "" : reader.GetString(4)
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                // Partie mogą nie istnieć - ignoruj błąd
            }
        }

        private void DgTowary_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AktualizujLiczniki();
        }

        private void DgPartie_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            AktualizujLiczniki();
        }

        private void AktualizujLiczniki()
        {
            int liczbaTowary = dgTowary.SelectedItems.Count;
            int liczbaPartii = dgPartie.SelectedItems.Count;
            int liczbaZdjec = sciezkiZdjec.Count;

            txtLicznikTowary.Text = $"{liczbaTowary} towar(ów)";
            txtLicznikPartie.Text = $"{liczbaPartii} parti(i)";
            txtLicznikZdjecia.Text = $"{liczbaZdjec} zdjęć";

            // Suma kg
            decimal sumaKg = 0;
            foreach (TowarReklamacji towar in dgTowary.SelectedItems)
            {
                sumaKg += towar.Waga;
            }
            txtSumaKg.Text = $"{sumaKg:N2} kg";
        }

        private void ListZdjecia_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
                    btnUsunZdjecie.IsEnabled = true;
                }
                catch
                {
                    imgPodglad.Source = null;
                }
            }
            else
            {
                imgPodglad.Source = null;
                btnUsunZdjecie.IsEnabled = false;
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
            if (dgTowary.SelectedItems.Count == 0)
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

            // Oblicz sumę kg
            decimal sumaKg = 0;
            foreach (TowarReklamacji towar in dgTowary.SelectedItems)
            {
                sumaKg += towar.Waga;
            }

            // Pobierz typ i priorytet
            string typReklamacji = (cmbTypReklamacji.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Inne";
            string priorytet = "Normalny";
            if (cmbPriorytet.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                var panel = item.Content as System.Windows.Controls.StackPanel;
                if (panel != null && panel.Children.Count > 1)
                {
                    var textBlock = panel.Children[1] as System.Windows.Controls.TextBlock;
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
                                (DataZgloszenia, UserID, IdDokumentu, NumerDokumentu, IdKontrahenta, NazwaKontrahenta, Opis, SumaKg, Status, TypReklamacji, Priorytet)
                                VALUES
                                (GETDATE(), @UserID, @IdDokumentu, @NumerDokumentu, @IdKontrahenta, @NazwaKontrahenta, @Opis, @SumaKg, 'Nowa', @TypReklamacji, @Priorytet);
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
                                cmd.Parameters.AddWithValue("@TypReklamacji", typReklamacji);
                                cmd.Parameters.AddWithValue("@Priorytet", priorytet);

                                idReklamacji = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // 2. Zapisz towary
                            string queryTowary = @"
                                INSERT INTO [dbo].[ReklamacjeTowary]
                                (IdReklamacji, IdTowaru, Symbol, Nazwa, Ilosc, Waga)
                                VALUES
                                (@IdReklamacji, @IdTowaru, @Symbol, @Nazwa, @Ilosc, @Waga)";

                            foreach (TowarReklamacji towar in dgTowary.SelectedItems)
                            {
                                using (SqlCommand cmd = new SqlCommand(queryTowary, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                                    cmd.Parameters.AddWithValue("@IdTowaru", towar.ID);
                                    cmd.Parameters.AddWithValue("@Symbol", towar.Symbol ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Nazwa", towar.Nazwa ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@Ilosc", towar.Ilosc);
                                    cmd.Parameters.AddWithValue("@Waga", towar.Waga);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // 3. Zapisz partie (jeśli są)
                            if (dgPartie.SelectedItems.Count > 0)
                            {
                                string queryPartie = @"
                                    INSERT INTO [dbo].[ReklamacjePartie]
                                    (IdReklamacji, GuidPartii, NumerPartii, CustomerID, CustomerName)
                                    VALUES
                                    (@IdReklamacji, @GuidPartii, @NumerPartii, @CustomerID, @CustomerName)";

                                foreach (PartiaDostawcy partia in dgPartie.SelectedItems)
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
                                (IdReklamacji, UserID, PoprzedniStatus, NowyStatus, Komentarz, TypAkcji)
                                VALUES
                                (@IdReklamacji, @UserID, NULL, 'Nowa', 'Utworzenie reklamacji', 'Utworzenie')";

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
                                $"Towarów: {dgTowary.SelectedItems.Count}\n" +
                                $"Suma kg: {sumaKg:N2}",
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
    public class TowarReklamacji
    {
        public int ID { get; set; }
        public string Symbol { get; set; }
        public string Nazwa { get; set; }
        public decimal Ilosc { get; set; }
        public decimal Waga { get; set; }
    }

    public class PartiaDostawcy
    {
        public Guid GuidPartii { get; set; }
        public string NrPartii { get; set; }
        public string IdDostawcy { get; set; }
        public string NazwaDostawcy { get; set; }
        public string DataUtworzenia { get; set; }
    }
}
