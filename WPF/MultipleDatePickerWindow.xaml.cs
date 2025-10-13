using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace Kalendarz1.WPF
{
    public partial class MultipleDatePickerWindow : Window
    {
        public List<DateTime> SelectedDates { get; private set; } = new();
        public bool CopyNotes { get; private set; } = false;

        private ObservableCollection<DayItem> _days = new();

        public MultipleDatePickerWindow(string title)
        {
            InitializeComponent();
            Title = title;

            dpStartDate.SelectedDate = DateTime.Today.AddDays(1);
            dpEndDate.SelectedDate = DateTime.Today.AddDays(7);

            icDays.ItemsSource = _days;
            PopulateDays();
        }

        private void DateRange_Changed(object sender, RoutedEventArgs e)
        {
            PopulateDays();
        }

        private void PopulateDays()
        {
            _days.Clear();

            if (!dpStartDate.SelectedDate.HasValue || !dpEndDate.SelectedDate.HasValue)
                return;

            if (dpEndDate.SelectedDate < dpStartDate.SelectedDate)
            {
                lblInfo.Text = "Data końcowa nie może być wcześniejsza niż początkowa!";
                lblInfo.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            lblInfo.Text = "Wybierz dni do dublowania:";
            lblInfo.Foreground = System.Windows.Media.Brushes.Black;

            var current = dpStartDate.SelectedDate.Value.Date;
            while (current <= dpEndDate.SelectedDate.Value.Date)
            {
                _days.Add(new DayItem
                {
                    Date = current,
                    DisplayText = current.ToString("yyyy-MM-dd dddd", new System.Globalization.CultureInfo("pl-PL")),
                    IsSelected = false
                });
                current = current.AddDays(1);
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var day in _days)
                day.IsSelected = true;
        }

        private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var day in _days)
                day.IsSelected = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            SelectedDates.Clear();
            foreach (var day in _days.Where(d => d.IsSelected))
                SelectedDates.Add(day.Date);

            if (!SelectedDates.Any())
            {
                MessageBox.Show("Proszę wybrać przynajmniej jeden dzień.",
                    "Brak wyboru", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CopyNotes = chkCopyNotes.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class DayItem : INotifyPropertyChanged
    {
        public DateTime Date { get; set; }
        public string DisplayText { get; set; }

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}