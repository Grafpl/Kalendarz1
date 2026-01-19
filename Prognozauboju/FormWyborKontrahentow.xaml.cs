using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.PrognozyUboju
{
    public partial class FormWyborKontrahentow : Window
    {
        private string connectionString;
        private ObservableCollection<KontrahentCheckItem> wszystkieKontrahenci;
        private ObservableCollection<KontrahentCheckItem> przefiltrowane;
        public List<int> WybraniKontrahenci { get; private set; }

        public FormWyborKontrahentow(string connString, List<int> aktualnieZaznaczeni)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connectionString = connString;
            WybraniKontrahenci = new List<int>();

            wszystkieKontrahenci = new ObservableCollection<KontrahentCheckItem>();
            przefiltrowane = new ObservableCollection<KontrahentCheckItem>();

            WczytajKontrahentow(aktualnieZaznaczeni);
        }

        private void WczytajKontrahentow(List<int> zaznaczeni)
        {
            try
            {
                string query = @"
                    SELECT DISTINCT 
                        C.id,
                        C.shortcut AS Nazwa,
                        COUNT(DISTINCT DK.id) AS LiczbaTransakcji
                    FROM [HANDEL].[SSCommon].[STContractors] C
                    INNER JOIN [HANDEL].[HM].[DK] DK ON C.id = DK.khid
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                    WHERE TW.katalog = '67095'
                      AND DK.anulowany = 0
                      AND DK.data >= DATEADD(MONTH, -6, GETDATE())
                    GROUP BY C.id, C.shortcut
                    HAVING COUNT(DISTINCT DK.id) > 0
                    ORDER BY C.shortcut;";

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int id = reader.GetInt32(0);
                                string nazwa = reader.GetString(1);
                                int liczbaTransakcji = reader.GetInt32(2);

                                var item = new KontrahentCheckItem
                                {
                                    Id = id,
                                    Nazwa = nazwa,
                                    Info = $"Transakcji w ostatnich 6 mies.: {liczbaTransakcji}",
                                    IsChecked = zaznaczeni == null || zaznaczeni.Count == 0 || zaznaczeni.Contains(id)
                                };

                                item.PropertyChanged += Item_PropertyChanged;
                                wszystkieKontrahenci.Add(item);
                                przefiltrowane.Add(item);
                            }
                        }
                    }
                }

                listKontrahenci.ItemsSource = przefiltrowane;
                AktualizujLicznik();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania kontrahentów:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(KontrahentCheckItem.IsChecked))
            {
                AktualizujLicznik();
            }
        }

        private void AktualizujLicznik()
        {
            int zaznaczone = wszystkieKontrahenci.Count(k => k.IsChecked);
            txtLiczbaZaznaczonych.Text = $"Zaznaczono: {zaznaczone} / {wszystkieKontrahenci.Count}";
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            string szukaj = txtSzukaj.Text.ToLower();

            przefiltrowane.Clear();

            var wyniki = string.IsNullOrWhiteSpace(szukaj)
                ? wszystkieKontrahenci
                : wszystkieKontrahenci.Where(k => k.Nazwa.ToLower().Contains(szukaj));

            foreach (var item in wyniki)
            {
                przefiltrowane.Add(item);
            }
        }

        private void BtnZaznaczWszystkich_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in przefiltrowane)
            {
                item.IsChecked = true;
            }
        }

        private void BtnOdznaczWszystkich_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in przefiltrowane)
            {
                item.IsChecked = false;
            }
        }

        private void BtnZastosuj_Click(object sender, RoutedEventArgs e)
        {
            WybraniKontrahenci = wszystkieKontrahenci
                .Where(k => k.IsChecked)
                .Select(k => k.Id)
                .ToList();

            this.DialogResult = true;
            this.Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class KontrahentCheckItem : INotifyPropertyChanged
    {
        private bool isChecked;

        public int Id { get; set; }
        public string Nazwa { get; set; }
        public string Info { get; set; }

        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (isChecked != value)
                {
                    isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}