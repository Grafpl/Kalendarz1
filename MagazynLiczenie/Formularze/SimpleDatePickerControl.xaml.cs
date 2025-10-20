using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.MagazynLiczenie.Formularze
{
    public partial class SimpleDatePickerControl : UserControl
    {
        private DateTime _currentMonth;
        private DateTime _selectedDate;
        private CultureInfo _culture = new CultureInfo("pl-PL");

        public event EventHandler<DateTime> DateSelected;

        public SimpleDatePickerControl()
        {
            InitializeComponent();
            _currentMonth = DateTime.Today;
            _selectedDate = DateTime.Today;
            UpdateCalendar();
        }

        public void SetDate(DateTime date)
        {
            _selectedDate = date;
            _currentMonth = new DateTime(date.Year, date.Month, 1);
            UpdateCalendar();
        }

        private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            UpdateCalendar();
        }

        private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            UpdateCalendar();
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            UpdateCalendar();
            DateSelected?.Invoke(this, _selectedDate);
        }

        private void UpdateCalendar()
        {
            // Aktualizuj nagłówek
            txtMonthYear.Text = _currentMonth.ToString("MMMM yyyy", _culture);

            // Wyczyść kalendarz
            gridCalendar.Children.Clear();

            // Pierwszy dzień miesiąca
            DateTime firstDay = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);

            // Dzień tygodnia pierwszego dnia (1 = poniedziałek, 7 = niedziela)
            int firstDayOfWeek = ((int)firstDay.DayOfWeek + 6) % 7;

            // Liczba dni w miesiącu
            int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

            // Dodaj puste komórki przed pierwszym dniem
            for (int i = 0; i < firstDayOfWeek; i++)
            {
                gridCalendar.Children.Add(new Border());
            }

            // Dodaj przyciski dni
            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime currentDate = new DateTime(_currentMonth.Year, _currentMonth.Month, day);

                var button = new Button
                {
                    Content = day.ToString(),
                    Tag = currentDate
                };

                // Styl przycisku
                if (currentDate.Date == DateTime.Today)
                {
                    // Dzisiaj - zielony
                    button.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113));
                    button.Foreground = Brushes.White;
                    button.BorderBrush = new SolidColorBrush(Color.FromRgb(39, 174, 96));
                }
                else if (currentDate.Date == _selectedDate.Date)
                {
                    // Wybrany - niebieski
                    button.Background = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    button.Foreground = Brushes.White;
                    button.BorderBrush = new SolidColorBrush(Color.FromRgb(41, 128, 185));
                }
                else if (currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    // Weekend - różowy
                    button.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                    button.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    button.BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199));
                }
                else
                {
                    // Zwykły dzień
                    button.Background = Brushes.White;
                    button.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                    button.BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199));
                }

                button.FontSize = 18;
                button.FontWeight = FontWeights.SemiBold;
                button.Margin = new Thickness(2);
                button.BorderThickness = new Thickness(2);
                button.Cursor = System.Windows.Input.Cursors.Hand;

                // Event handler
                button.Click += DayButton_Click;

                // Dodaj hover effect
                button.MouseEnter += (s, e) =>
                {
                    if ((s as Button)?.Tag is DateTime dt && dt.Date != _selectedDate.Date && dt.Date != DateTime.Today)
                    {
                        button.Background = new SolidColorBrush(Color.FromRgb(235, 245, 251));
                        button.BorderBrush = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                    }
                };

                button.MouseLeave += (s, e) =>
                {
                    if ((s as Button)?.Tag is DateTime dt && dt.Date != _selectedDate.Date && dt.Date != DateTime.Today)
                    {
                        if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
                        {
                            button.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                            button.BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199));
                        }
                        else
                        {
                            button.Background = Brushes.White;
                            button.BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199));
                        }
                    }
                };

                gridCalendar.Children.Add(button);
            }
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DateTime date)
            {
                _selectedDate = date;
                UpdateCalendar();
                DateSelected?.Invoke(this, _selectedDate);
            }
        }
    }
}