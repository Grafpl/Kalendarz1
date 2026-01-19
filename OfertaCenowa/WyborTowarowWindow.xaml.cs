using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace Kalendarz1.OfertaCenowa
{
    public partial class WyborTowarowWindow : Window
    {
        private ObservableCollection<TowarWybor> _towary;
        public List<TowarOferta> WybraneTowary { get; private set; }

        public WyborTowarowWindow(List<TowarOferta> dostepneTowary)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            WybraneTowary = new List<TowarOferta>();

            _towary = new ObservableCollection<TowarWybor>(
                dostepneTowary.Select(t => new TowarWybor(t))
            );

            dgTowary.ItemsSource = _towary;
        }

        private void TxtFiltr_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string filtr = txtFiltr.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(filtr))
            {
                dgTowary.ItemsSource = _towary;
            }
            else
            {
                var przefiltrowane = _towary.Where(t => 
                    t.Kod.ToLower().Contains(filtr) || 
                    t.Nazwa.ToLower().Contains(filtr)
                ).ToList();
                
                dgTowary.ItemsSource = new ObservableCollection<TowarWybor>(przefiltrowane);
            }
        }

        private void BtnZaznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            foreach (var towar in _towary)
            {
                towar.IsSelected = true;
            }
            dgTowary.Items.Refresh();
        }

        private void BtnDodajWybrane_Click(object sender, RoutedEventArgs e)
        {
            WybraneTowary = _towary
                .Where(t => t.IsSelected)
                .Select(t => t.Towar)
                .ToList();

            if (!WybraneTowary.Any())
            {
                MessageBox.Show("Nie wybrano żadnych towarów.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }

    /// <summary>
    /// Klasa pomocnicza dla wyboru towarów z checkboxem
    /// </summary>
    public class TowarWybor : INotifyPropertyChanged
    {
        public TowarOferta Towar { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public string Kod => Towar.Kod;
        public string Nazwa => Towar.Nazwa;
        public string Katalog => Towar.Katalog;

        public TowarWybor(TowarOferta towar)
        {
            Towar = towar;
            IsSelected = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
