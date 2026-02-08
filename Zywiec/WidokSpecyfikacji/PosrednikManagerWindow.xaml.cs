using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    public partial class PosrednikManagerWindow : Window
    {
        private readonly string _libraNetConnStr;
        private readonly string _handelConnStr = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connect Timeout=30;";
        private DispatcherTimer _searchTimer;

        public bool PosrednicyChanged { get; private set; }

        public PosrednikManagerWindow(string libraNetConnectionString)
        {
            InitializeComponent();
            _libraNetConnStr = libraNetConnectionString;

            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                SearchSymfoniaContractors();
            };

            Loaded += (s, e) => LoadMoiPosrednicy();
        }

        private void LoadMoiPosrednicy()
        {
            try
            {
                var list = new List<PosrednikItem>();
                using (var conn = new SqlConnection(_libraNetConnStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT Id, SymfoniaId, Name1, Shortcut, NIP, Street, City, PostalCode, Phone, Email FROM Posrednicy WHERE Aktywny = 1 ORDER BY Name1", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new PosrednikItem
                            {
                                ID = reader.GetInt32(0),
                                SymfoniaId = reader.GetInt32(1),
                                Nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Kod = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                NIP = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                Ulica = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Miasto = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                KodPocztowy = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                Telefon = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                Email = reader.IsDBNull(9) ? "" : reader.GetString(9)
                            });
                        }
                    }
                }
                dgPosrednicy.ItemsSource = list;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania pośredników:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchSymfoniaContractors()
        {
            string search = txtSearch.Text?.Trim();
            if (string.IsNullOrEmpty(search) || search.Length < 2) return;

            try
            {
                var results = new List<SymfoniaContractor>();
                using (var conn = new SqlConnection(_handelConnStr))
                {
                    conn.Open();
                    string sql = @"SELECT TOP 100
                                       C.Id,
                                       C.Shortcut,
                                       C.Name,
                                       C.NIP,
                                       ISNULL(A.Street, '') AS Street,
                                       ISNULL(A.HouseNo, '') AS HouseNo,
                                       ISNULL(A.Place, '') AS City,
                                       ISNULL(A.PostCode, '') AS PostCode,
                                       ISNULL(T.Telephone1, '') AS Phone,
                                       ISNULL(T.Email1, '') AS Email
                                   FROM [SSCommon].[STContractors] C
                                   LEFT JOIN [SSCommon].[STPostOfficeAddresses] A
                                       ON A.ContactGuid = C.ContactGuid AND A.AddressName = N'adres domyślny'
                                   LEFT JOIN [SSCommon].[STTeleItContacts] T
                                       ON T.ContactGuid = C.ContactGuid
                                   WHERE C.Shortcut LIKE @Search OR C.Name LIKE @Search OR C.NIP LIKE @Search
                                   ORDER BY C.Name";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Search", $"%{search}%");
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string street = reader.IsDBNull(4) ? "" : reader.GetString(4).Trim();
                                string houseNo = reader.IsDBNull(5) ? "" : reader.GetString(5).Trim();
                                string fullStreet = string.IsNullOrEmpty(houseNo) ? street : $"{street} {houseNo}";

                                results.Add(new SymfoniaContractor
                                {
                                    Id = reader.GetInt32(0),
                                    Shortcut = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim(),
                                    Name1 = reader.IsDBNull(2) ? "" : reader.GetString(2).Trim(),
                                    NIP = reader.IsDBNull(3) ? "" : reader.GetString(3).Trim(),
                                    Street = fullStreet,
                                    City = reader.IsDBNull(6) ? "" : reader.GetString(6).Trim(),
                                    PostalCode = reader.IsDBNull(7) ? "" : reader.GetString(7).Trim(),
                                    Phone = reader.IsDBNull(8) ? "" : reader.GetString(8).Trim(),
                                    Email = reader.IsDBNull(9) ? "" : reader.GetString(9).Trim()
                                });
                            }
                        }
                    }
                }
                dgSymfonia.ItemsSource = results;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wyszukiwania kontrahentów Symfonia:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            _searchTimer.Stop();
            SearchSymfoniaContractors();
        }

        private void BtnAddPosrednik_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgSymfonia.SelectedItem as SymfoniaContractor;
            if (selected == null)
            {
                MessageBox.Show("Zaznacz kontrahenta z prawego panelu.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_libraNetConnStr))
                {
                    conn.Open();

                    // Sprawdź czy już istnieje (aktywny)
                    using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Posrednicy WHERE SymfoniaId = @SId AND Aktywny = 1", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@SId", selected.Id);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show("Ten kontrahent jest już na liście pośredników.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }

                    // Sprawdź czy istnieje jako nieaktywny → reaktywuj
                    using (var reactivateCmd = new SqlCommand("UPDATE Posrednicy SET Aktywny = 1 WHERE SymfoniaId = @SId AND Aktywny = 0", conn))
                    {
                        reactivateCmd.Parameters.AddWithValue("@SId", selected.Id);
                        int updated = reactivateCmd.ExecuteNonQuery();
                        if (updated > 0)
                        {
                            PosrednicyChanged = true;
                            LoadMoiPosrednicy();
                            MessageBox.Show("Pośrednik reaktywowany.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }

                    // Wstaw nowego
                    string insertSql = @"INSERT INTO Posrednicy (SymfoniaId, Shortcut, Name1, NIP, Street, City, PostalCode, Phone, Email, DodanyPrzez)
                                         VALUES (@SId, @Shortcut, @Name1, @NIP, @Street, @City, @PostalCode, @Phone, @Email, @User)";
                    using (var insertCmd = new SqlCommand(insertSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@SId", selected.Id);
                        insertCmd.Parameters.AddWithValue("@Shortcut", (object)selected.Shortcut ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Name1", (object)selected.Name1 ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@NIP", (object)selected.NIP ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Street", (object)selected.Street ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@City", (object)selected.City ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@PostalCode", (object)selected.PostalCode ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Phone", (object)selected.Phone ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@Email", (object)selected.Email ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@User", App.UserID ?? "");
                        insertCmd.ExecuteNonQuery();
                    }
                }

                PosrednicyChanged = true;
                LoadMoiPosrednicy();
                MessageBox.Show($"Dodano pośrednika: {selected.Name1}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania pośrednika:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeactivate_Click(object sender, RoutedEventArgs e)
        {
            var selected = dgPosrednicy.SelectedItem as PosrednikItem;
            if (selected == null)
            {
                MessageBox.Show("Zaznacz pośrednika z lewego panelu.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Czy na pewno chcesz usunąć pośrednika \"{selected.Nazwa}\"?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_libraNetConnStr))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("UPDATE Posrednicy SET Aktywny = 0 WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", selected.ID);
                        cmd.ExecuteNonQuery();
                    }
                }
                PosrednicyChanged = true;
                LoadMoiPosrednicy();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd usuwania pośrednika:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRefreshPosrednicy_Click(object sender, RoutedEventArgs e)
        {
            LoadMoiPosrednicy();
        }
    }

    /// <summary>
    /// Model kontrahenta z Symfonii (.112) do wyszukiwania
    /// </summary>
    public class SymfoniaContractor
    {
        public int Id { get; set; }
        public string Shortcut { get; set; }
        public string Name1 { get; set; }
        public string NIP { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }
}
