using System;
using System.Windows.Threading;

namespace Kalendarz1.Zadania
{
    public class NotificationService
    {
        private static NotificationService _instance;
        private readonly DispatcherTimer _timer;
        private readonly string _userId;
        private DateTime _snoozedUntil = DateTime.MinValue;
        private NotificationWindow _currentWindow;

        public static NotificationService Instance => _instance;

        public event EventHandler OpenPanelRequested;

        private NotificationService(string userId)
        {
            _userId = userId;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(15) // Sprawdzaj co 15 minut
            };
            _timer.Tick += Timer_Tick;
        }

        public static void Initialize(string userId)
        {
            if (_instance == null)
            {
                _instance = new NotificationService(userId);
            }
        }

        public void Start()
        {
            _timer.Start();
            // Pokaż pierwsze powiadomienie po 5 sekundach od startu
            var startupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            startupTimer.Tick += (s, e) =>
            {
                startupTimer.Stop();
                ShowNotification();
            };
            startupTimer.Start();
        }

        public void ShowNotification()
        {
            // Sprawdź czy nie jest odłożone
            if (DateTime.Now < _snoozedUntil)
                return;

            // Sprawdź czy już nie ma otwartego okna
            if (_currentWindow != null)
                return;

            try
            {
                var window = new NotificationWindow(_userId);

                window.SnoozeRequested += (s, snoozeTime) =>
                {
                    _snoozedUntil = DateTime.Now.Add(snoozeTime);
                    _currentWindow = null;
                };

                window.OpenPanelRequested += (s, args) =>
                {
                    _currentWindow = null;
                    OpenPanelRequested?.Invoke(this, EventArgs.Empty);
                };

                window.Closed += (s, args) =>
                {
                    _currentWindow = null;
                };

                _currentWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Błąd powiadomień: {ex.Message}", "Błąd",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void SetInterval(TimeSpan interval)
        {
            _timer.Interval = interval;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            ShowNotificationIfNeeded();
        }

        public void ShowNotificationIfNeeded()
        {
            // Sprawdź czy nie jest odłożone
            if (DateTime.Now < _snoozedUntil)
                return;

            // Sprawdź czy już nie ma otwartego okna
            if (_currentWindow != null)
                return;

            try
            {
                var window = new NotificationWindow(_userId);

                if (!window.HasNotifications)
                {
                    return;
                }

                window.SnoozeRequested += (s, snoozeTime) =>
                {
                    _snoozedUntil = DateTime.Now.Add(snoozeTime);
                    _currentWindow = null;
                };

                window.OpenPanelRequested += (s, args) =>
                {
                    _currentWindow = null;
                    OpenPanelRequested?.Invoke(this, EventArgs.Empty);
                };

                window.Closed += (s, args) =>
                {
                    _currentWindow = null;
                };

                _currentWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing notification: {ex.Message}");
            }
        }

        public void ShowNow()
        {
            _snoozedUntil = DateTime.MinValue;
            ShowNotificationIfNeeded();
        }
    }
}
