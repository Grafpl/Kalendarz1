using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.NotatkiZeSpotkan
{
    public partial class PodgladNotatki : Window
    {
        private readonly string _connectionString;
        private readonly string _userID;
        private readonly long _notatkaID;

        public PodgladNotatki(string connectionString, string userID, long notatkaID)
        {
            _connectionString = connectionString;
            _userID = userID;
            _notatkaID = notatkaID;

            InitializeComponent();
            LoadNotatka();
        }

        private void LoadNotatka()
        {
            try
            {
                if (!SprawdzDostep())
                {
                    MessageBox.Show("Nie masz dostępu do tej notatki.", "Brak dostępu",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string sql = @"
                SELECT 
                    n.TypSpotkania, 
                    n.DataSpotkania, 
                    n.DataUtworzenia, 
                    n.DataModyfikacji,
                    ISNULL(o.Name, n.TworcaID) AS TworcaNazwa,
                    n.KontrahentNazwa, 
                    n.Temat, 
                    n.TrescNotatki,
                    n.OsobaKontaktowa, 
                    n.DodatkoweInfo
                FROM NotatkiZeSpotkan n
                LEFT JOIN operators o ON o.ID = n.TworcaID
                WHERE n.NotatkaID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", _notatkaID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string typ = reader.GetString(0);
                                DateTime dataSpotkania = reader.GetDateTime(1);
                                DateTime dataUtworzenia = reader.GetDateTime(2);

                                txtTypSpotkania.Text = typ;
                                txtDataSpotkania.Text = dataSpotkania.ToString("dd.MM.yyyy");
                                txtDataUtworzenia.Text = dataUtworzenia.ToString("dd.MM.yyyy HH:mm");
                                txtTworca.Text = reader.GetString(4);
                                txtTemat.Text = reader.GetString(6);
                                txtTresc.Text = reader.GetString(7);

                                txtPodtytul.Text = $"{typ} • {dataSpotkania:dd.MM.yyyy}";

                                txtIcon.Text = typ switch
                                {
                                    "Zespół" => "👥",
                                    "Odbiorca" => "🏢",
                                    "Hodowca" => "🐔",
                                    _ => "📝"
                                };

                                if (!reader.IsDBNull(5))
                                {
                                    string kontrahentNazwa = reader.GetString(5);
                                    panelKontrahent.Visibility = Visibility.Visible;
                                    lblKontrahent.Text = typ == "Odbiorca" ? "🏢 Odbiorca" : "🐔 Hodowca";
                                    txtKontrahent.Text = kontrahentNazwa;
                                }

                                if (!reader.IsDBNull(8))
                                {
                                    panelOsobaKontaktowa.Visibility = Visibility.Visible;
                                    txtOsobaKontaktowa.Text = reader.GetString(8);
                                }

                                if (!reader.IsDBNull(9))
                                {
                                    panelDodatkoweInfo.Visibility = Visibility.Visible;
                                    txtDodatkoweInfo.Text = reader.GetString(9);
                                }
                            }
                        }
                    }

                    // Uczestnicy
                    if (txtTypSpotkania.Text == "Zespół")
                    {
                        var uczestnicy = new List<string>();

                        string sqlUczestnicy = @"
                    SELECT o.Name
                    FROM NotatkiUczestnicy u
                    INNER JOIN operators o ON o.ID = u.OperatorID
                    WHERE u.NotatkaID = @ID 
                    ORDER BY o.Name";

                        using (var cmd = new SqlCommand(sqlUczestnicy, conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", _notatkaID);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    uczestnicy.Add(reader.GetString(0));
                                }
                            }
                        }

                        if (uczestnicy.Any())
                        {
                            panelUczestnicy.Visibility = Visibility.Visible;
                            txtUczestnicy.Text = "• " + string.Join("\n• ", uczestnicy);
                        }
                    }

                    if (MaUprawnienieDoEdycji())
                    {
                        txtInfoEdycja.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }        // NOWA METODA
        private bool MaUprawnienieDoEdycji()
        {
            // Admin ma zawsze dostęp
            if (_userID == "11111") return true;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Sprawdź czy jest twórcą LUB ma widoczność
                    string sql = @"
                SELECT COUNT(*) 
                FROM NotatkiZeSpotkan n
                WHERE n.NotatkaID = @ID 
                  AND (n.TworcaID = @UserID 
                       OR EXISTS (SELECT 1 FROM NotatkiWidocznosc w 
                                  WHERE w.NotatkaID = n.NotatkaID 
                                    AND w.OperatorID = @UserID))";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", _notatkaID);
                        cmd.Parameters.AddWithValue("@UserID", _userID);
                        return (int)cmd.ExecuteScalar() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
        private bool SprawdzDostep()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT COUNT(*) 
                        FROM NotatkiZeSpotkan n
                        WHERE n.NotatkaID = @ID 
                          AND (@UserID = '11111' 
                               OR n.TworcaID = @UserID 
                               OR EXISTS (SELECT 1 FROM NotatkiWidocznosc w 
                                          WHERE w.NotatkaID = n.NotatkaID 
                                            AND w.OperatorID = @UserID))";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", _notatkaID);
                        cmd.Parameters.AddWithValue("@UserID", _userID);

                        return (int)cmd.ExecuteScalar() > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}