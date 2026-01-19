using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.CRM
{
    public partial class WyborBranzyDialog : Window
    {
        private readonly string connectionString;
        private ObservableCollection<BranzaItem> wszystkieBranze = new ObservableCollection<BranzaItem>();
        private ObservableCollection<BranzaItem> przefiltrowaneBranze = new ObservableCollection<BranzaItem>();

        public bool ZapisanoZmiany { get; private set; } = false;

        public WyborBranzyDialog(string connString)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connectionString = connString;
            Loaded += WyborBranzyDialog_Loaded;
        }

        private void WyborBranzyDialog_Loaded(object sender, RoutedEventArgs e)
        {
            SprawdzIUtworzTabele();
            WczytajBranze();
        }

        private void SprawdzIUtworzTabele()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PriorytetoweBranzeCRM')
                        BEGIN
                            CREATE TABLE PriorytetoweBranzeCRM (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                PKD_Opis NVARCHAR(500) NOT NULL UNIQUE,
                                DataDodania DATETIME DEFAULT GETDATE()
                            )

                            -- Dodaj domyślne branże mięsne
                            INSERT INTO PriorytetoweBranzeCRM (PKD_Opis) VALUES
                                ('Sprzedaż detaliczna mięsa i wyrobów z mięsa prowadzona w wyspecjalizowanych sklepach'),
                                ('Przetwarzanie i konserwowanie mięsa z drobiu'),
                                ('Produkcja wyrobów z mięsa, włączając wyroby z mięsa drobiowego'),
                                ('Ubój zwierząt, z wyłączeniem drobiu i królików')
                        END", conn);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd tworzenia tabeli: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void WczytajBranze()
        {
            wszystkieBranze.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz wszystkie unikalne branże z liczbą firm
                    var cmd = new SqlCommand(@"
                        SELECT
                            o.PKD_Opis,
                            COUNT(*) as LiczbaFirm,
                            CASE WHEN p.PKD_Opis IS NOT NULL THEN 1 ELSE 0 END as CzyPriorytetowa
                        FROM OdbiorcyCRM o
                        LEFT JOIN PriorytetoweBranzeCRM p ON o.PKD_Opis = p.PKD_Opis
                        WHERE o.PKD_Opis IS NOT NULL AND o.PKD_Opis != ''
                        GROUP BY o.PKD_Opis, p.PKD_Opis
                        ORDER BY
                            CASE WHEN p.PKD_Opis IS NOT NULL THEN 0 ELSE 1 END,
                            COUNT(*) DESC", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wszystkieBranze.Add(new BranzaItem
                            {
                                Nazwa = reader["PKD_Opis"]?.ToString() ?? "",
                                LiczbaFirm = Convert.ToInt32(reader["LiczbaFirm"]),
                                CzyPriorytetowa = Convert.ToInt32(reader["CzyPriorytetowa"]) == 1
                            });
                        }
                    }
                }

                przefiltrowaneBranze = new ObservableCollection<BranzaItem>(wszystkieBranze);
                listaBranzy.ItemsSource = przefiltrowaneBranze;
                AktualizujLicznik();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania branż: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AktualizujLicznik()
        {
            int zaznaczone = wszystkieBranze.Count(b => b.CzyPriorytetowa);
            txtZaznaczone.Text = zaznaczone.ToString();
        }

        private void TxtSzukaj_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var szukaj = txtSzukaj.Text?.Trim().ToLower() ?? "";

            if (string.IsNullOrEmpty(szukaj))
            {
                przefiltrowaneBranze = new ObservableCollection<BranzaItem>(wszystkieBranze);
            }
            else
            {
                przefiltrowaneBranze = new ObservableCollection<BranzaItem>(
                    wszystkieBranze.Where(b => b.Nazwa.ToLower().Contains(szukaj))
                );
            }

            listaBranzy.ItemsSource = przefiltrowaneBranze;
        }

        private void ChkBranza_Changed(object sender, RoutedEventArgs e)
        {
            AktualizujLicznik();
        }

        private void BranzaItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is BranzaItem item)
            {
                item.CzyPriorytetowa = !item.CzyPriorytetowa;
                AktualizujLicznik();

                // Odśwież widok
                listaBranzy.ItemsSource = null;
                listaBranzy.ItemsSource = przefiltrowaneBranze;
            }
        }

        private void BtnZaznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            foreach (var branza in przefiltrowaneBranze)
            {
                branza.CzyPriorytetowa = true;
            }
            AktualizujLicznik();
            listaBranzy.ItemsSource = null;
            listaBranzy.ItemsSource = przefiltrowaneBranze;
        }

        private void BtnOdznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            foreach (var branza in przefiltrowaneBranze)
            {
                branza.CzyPriorytetowa = false;
            }
            AktualizujLicznik();
            listaBranzy.ItemsSource = null;
            listaBranzy.ItemsSource = przefiltrowaneBranze;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        // Usuń wszystkie obecne priorytetowe branże
                        var cmdDelete = new SqlCommand("DELETE FROM PriorytetoweBranzeCRM", conn, transaction);
                        cmdDelete.ExecuteNonQuery();

                        // Dodaj zaznaczone branże
                        foreach (var branza in wszystkieBranze.Where(b => b.CzyPriorytetowa))
                        {
                            var cmdInsert = new SqlCommand(@"
                                INSERT INTO PriorytetoweBranzeCRM (PKD_Opis) VALUES (@pkd)", conn, transaction);
                            cmdInsert.Parameters.AddWithValue("@pkd", branza.Nazwa);
                            cmdInsert.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                }

                int liczbaZapisanych = wszystkieBranze.Count(b => b.CzyPriorytetowa);
                MessageBox.Show($"Zapisano {liczbaZapisanych} priorytetowych branż.\n\nKontakty z tych branż będą wyświetlane na czerwono.",
                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                ZapisanoZmiany = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class BranzaItem : INotifyPropertyChanged
    {
        private bool czyPriorytetowa;

        public string Nazwa { get; set; }
        public int LiczbaFirm { get; set; }

        public bool CzyPriorytetowa
        {
            get => czyPriorytetowa;
            set
            {
                if (czyPriorytetowa != value)
                {
                    czyPriorytetowa = value;
                    OnPropertyChanged(nameof(CzyPriorytetowa));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
