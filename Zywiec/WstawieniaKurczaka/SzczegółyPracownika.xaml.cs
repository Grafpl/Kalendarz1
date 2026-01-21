using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kalendarz1
{
    public class SzczegolyWstawienie
    {
        public int Lp { get; set; }
        public string Dostawca { get; set; }
        public DateTime? DataWstawienia { get; set; }
        public int? IloscWstawienia { get; set; }
        public DateTime? DataUtw { get; set; }
        public string Status { get; set; }
        public string KtoStworzyl { get; set; }
        public DateTime? DataConf { get; set; }

        // Avatar properties for the creator
        public string KtoStworzylId { get; set; }
        public Brush AvatarBackground { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C8A3A"));
        public string AvatarInitials { get; set; } = "?";
        public Visibility AvatarImageVisibility { get; set; } = Visibility.Collapsed;
        public ImageSource AvatarImageSource { get; set; }
    }

    public partial class SzczegółyPracownika : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string nazwaPracownika;
        private DateTime startDate;
        private DateTime endDate;

        public SzczegółyPracownika(string pracownik, DateTime dataOd, DateTime dataDo)
        {
            InitializeComponent();
<<<<<<< HEAD
            WindowIconHelper.SetIcon(this);
=======
>>>>>>> Zywiec-avatary

            nazwaPracownika = pracownik;
            startDate = dataOd;
            endDate = dataDo;

            txtNazwaPracownika.Text = $"Szczegoly: {pracownik}";
            txtOkres.Text = $"Okres: {dataOd:dd.MM.yyyy} - {dataDo.AddDays(-1):dd.MM.yyyy}";

            // Set initials
            txtAvatarInitials.Text = GetInitials(pracownik);

            // Load avatar
            LoadAvatarAsync(pracownik);

            LoadData();
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private void LoadAvatarAsync(string pracownikName)
        {
            Task.Run(() =>
            {
                // Get user ID from database
                string userId = GetUserIdByName(pracownikName);
                if (!string.IsNullOrEmpty(userId))
                {
                    var avatar = UserAvatarManager.GetAvatar(userId);
                    if (avatar != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var imageSource = ConvertToImageSource(avatar);
                            if (imageSource != null)
                            {
                                avatarEllipse.Fill = new ImageBrush(imageSource) { Stretch = Stretch.UniformToFill };
                                avatarEllipse.Visibility = Visibility.Visible;
                            }
                        });
                    }
                }
            });
        }

        private string GetUserIdByName(string name)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand("SELECT CAST(ID AS VARCHAR(20)) FROM dbo.operators WHERE Name = @Name", connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", name);
                        var result = cmd.ExecuteScalar();
                        return result?.ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using (var memory = new MemoryStream())
            {
                image.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memory;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        private void LoadData()
        {
            try
            {
                // Zaladuj co utworzyl
                var stworzone = LoadStworzonePrzezPracownika();
                dgStworzone.ItemsSource = stworzone;
                txtLiczbaStworzone.Text = stworzone.Count.ToString();

                // Zaladuj co potwierdzil
                var potwierdzone = LoadPotwierdzonePrzezPracownika();
                dgPotwierdzone.ItemsSource = potwierdzone;
                txtLiczbaPotwierdzone.Text = potwierdzone.Count.ToString();

                // Razem
                txtLiczbaRazem.Text = (stworzone.Count + potwierdzone.Count).ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania danych: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<SzczegolyWstawienie> LoadStworzonePrzezPracownika()
        {
            var lista = new List<SzczegolyWstawienie>();

            string query = @"
                SELECT 
                    w.Lp,
                    w.Dostawca,
                    w.DataWstawienia,
                    w.IloscWstawienia,
                    w.DataUtw,
                    CASE WHEN w.isConf = 1 THEN 'Potwierdzone' ELSE 'Oczekujace' END as Status
                FROM dbo.WstawieniaKurczakow w
                LEFT JOIN dbo.operators o ON w.KtoStwo = o.ID
                WHERE o.Name = @Pracownik 
                    AND w.DataUtw >= @StartDate 
                    AND w.DataUtw < @EndDate
                ORDER BY w.DataUtw DESC";

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@Pracownik", nazwaPracownika);
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new SzczegolyWstawienie
                        {
                            Lp = Convert.ToInt32(reader["Lp"]),
                            Dostawca = reader["Dostawca"]?.ToString() ?? "",
                            DataWstawienia = reader["DataWstawienia"] as DateTime?,
                            IloscWstawienia = reader["IloscWstawienia"] as int?,
                            DataUtw = reader["DataUtw"] as DateTime?,
                            Status = reader["Status"]?.ToString() ?? ""
                        });
                    }
                }
            }

            return lista;
        }

        private List<SzczegolyWstawienie> LoadPotwierdzonePrzezPracownika()
        {
            var lista = new List<SzczegolyWstawienie>();

            string query = @"
                SELECT
                    w.Lp,
                    w.Dostawca,
                    w.DataWstawienia,
                    w.IloscWstawienia,
                    os.Name as KtoStworzyl,
                    CAST(w.KtoStwo AS VARCHAR(20)) as KtoStworzylId,
                    w.DataConf
                FROM dbo.WstawieniaKurczakow w
                LEFT JOIN dbo.operators oc ON w.KtoConf = oc.ID
                LEFT JOIN dbo.operators os ON w.KtoStwo = os.ID
                WHERE oc.Name = @Pracownik
                    AND w.isConf = 1
                    AND w.DataConf >= @StartDate
                    AND w.DataConf < @EndDate
                ORDER BY w.DataConf DESC";

            using (var connection = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@Pracownik", nazwaPracownika);
                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);

                connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var ktoStworzyl = reader["KtoStworzyl"]?.ToString() ?? "Nieznany";
                        var item = new SzczegolyWstawienie
                        {
                            Lp = Convert.ToInt32(reader["Lp"]),
                            Dostawca = reader["Dostawca"]?.ToString() ?? "",
                            DataWstawienia = reader["DataWstawienia"] as DateTime?,
                            IloscWstawienia = reader["IloscWstawienia"] as int?,
                            KtoStworzyl = ktoStworzyl,
                            KtoStworzylId = reader["KtoStworzylId"]?.ToString(),
                            DataConf = reader["DataConf"] as DateTime?,
                            AvatarInitials = GetInitials(ktoStworzyl)
                        };
                        lista.Add(item);
                    }
                }
            }

            // Load avatars asynchronously
            LoadCreatorAvatarsAsync(lista);

            return lista;
        }

        private void LoadCreatorAvatarsAsync(List<SzczegolyWstawienie> items)
        {
            Task.Run(() =>
            {
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.KtoStworzylId))
                    {
                        var avatar = UserAvatarManager.GetAvatar(item.KtoStworzylId);
                        if (avatar != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var imageSource = ConvertToImageSource(avatar);
                                if (imageSource != null)
                                {
                                    item.AvatarImageSource = imageSource;
                                    item.AvatarImageVisibility = Visibility.Visible;
                                }
                            });
                        }
                    }
                }
                // Refresh the DataGrid
                Application.Current.Dispatcher.Invoke(() =>
                {
                    dgPotwierdzone.Items.Refresh();
                });
            });
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
