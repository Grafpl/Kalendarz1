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
    public partial class FormWyborHandlowcow : Window
    {
        private string connectionString;
        private ObservableCollection<CheckItem> wszyscyHandlowcy;
        private ObservableCollection<CheckItem> przefiltrowane;
        public List<string> WybraniHandlowcy { get; private set; }

        public FormWyborHandlowcow(string connString, List<string> aktualnieZaznaczeni)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            connectionString = connString;
            WybraniHandlowcy = new List<string>();

            wszyscyHandlowcy = new ObservableCollection<CheckItem>();
            przefiltrowane = new ObservableCollection<CheckItem>();

            WczytajHandlowcow(aktualnieZaznaczeni);
        }

        private void WczytajHandlowcow(List<string> zaznaczeni)
        {
            try
            {
                string query = @"
                    SELECT DISTINCT WYM.CDim_Handlowiec_Val
                    FROM [HANDEL].[SSCommon].[ContractorClassification] WYM
                    WHERE WYM.CDim_Handlowiec_Val IS NOT NULL AND WYM.CDim_Handlowiec_Val NOT IN ('Ogólne', 'Nieprzypisany')
                    ORDER BY WYM.CDim_Handlowiec_Val;";

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string nazwa = reader.GetString(0);
                                var item = new CheckItem
                                {
                                    Nazwa = nazwa,
                                    IsChecked = zaznaczeni == null || zaznaczeni.Count == 0 || zaznaczeni.Contains(nazwa)
                                };
                                item.PropertyChanged += Item_PropertyChanged;
                                wszyscyHandlowcy.Add(item);
                                przefiltrowane.Add(item);
                            }
                        }
                    }
                }
                listHandlowcy.ItemsSource = przefiltrowane;
                AktualizujLicznik();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania handlowców:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CheckItem.IsChecked))
            {
                AktualizujLicznik();
            }
        }

        private void AktualizujLicznik()
        {
            int zaznaczone = wszyscyHandlowcy.Count(k => k.IsChecked);
            txtLiczbaZaznaczonych.Text = $"Zaznaczono: {zaznaczone} / {wszyscyHandlowcy.Count}";
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            string szukaj = txtSzukaj.Text.ToLower();
            przefiltrowane.Clear();
            var wyniki = string.IsNullOrWhiteSpace(szukaj)
                ? wszyscyHandlowcy
                : wszyscyHandlowcy.Where(k => k.Nazwa.ToLower().Contains(szukaj));
            foreach (var item in wyniki)
            {
                przefiltrowane.Add(item);
            }
        }

        private void BtnZaznaczWszystkich_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in przefiltrowane) item.IsChecked = true;
        }

        private void BtnOdznaczWszystkich_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in przefiltrowane) item.IsChecked = false;
        }

        private void BtnZastosuj_Click(object sender, RoutedEventArgs e)
        {
            WybraniHandlowcy = wszyscyHandlowcy.Where(k => k.IsChecked).Select(k => k.Nazwa).ToList();
            this.DialogResult = true;
            this.Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    public class CheckItem : INotifyPropertyChanged
    {
        private bool isChecked;
        public string Nazwa { get; set; }
        public bool IsChecked
        {
            get => isChecked;
            set { if (isChecked != value) { isChecked = value; OnPropertyChanged(); } }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}