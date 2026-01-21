using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using Kalendarz1.Komunikator.Models;
using Kalendarz1.Komunikator.Views;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Komunikator.Services
{
    /// <summary>
    /// Model danych dla nowej wiadomości
    /// </summary>
    public class NewMessageInfo
    {
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
    }

    /// <summary>
    /// Globalny serwis powiadomień czatu działający w tle
    /// Sprawdza co 30 sekund czy są nowe wiadomości i pokazuje dymek
    /// </summary>
    public class GlobalChatNotificationService : IDisposable
    {
        private const string CONNECTION_STRING = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const int POLLING_INTERVAL_SECONDS = 30;

        private readonly string _userId;
        private readonly DispatcherTimer _timer;
        private ChatBubbleNotification _bubbleWindow;
        private bool _isDisposed;
        private bool _temporarilyHidden; // Po kliknięciu dymka ukryj go tymczasowo
        private int _lastUnreadCount;
        private DateTime _lastCheckTime = DateTime.MinValue;

        /// <summary>
        /// Event wywoływany gdy są nowe wiadomości
        /// </summary>
        public event EventHandler<int> UnreadCountChanged;

        /// <summary>
        /// Event wywoływany gdy kliknięto dymek
        /// </summary>
        public event EventHandler BubbleClicked;

        public int UnreadCount => _lastUnreadCount;

        public GlobalChatNotificationService(string userId)
        {
            _userId = userId;
            _lastUnreadCount = 0;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(POLLING_INTERVAL_SECONDS)
            };
            _timer.Tick += Timer_Tick;
        }

        #region Zarządzanie timerem

        /// <summary>
        /// Uruchamia automatyczne sprawdzanie wiadomości
        /// </summary>
        public void Start()
        {
            if (_isDisposed) return;

            // Pierwsze sprawdzenie od razu
            CheckForNewMessages();

            _timer.Start();
        }

        /// <summary>
        /// Zatrzymuje automatyczne sprawdzanie
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
            HideBubble();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            CheckForNewMessages();
        }

        #endregion

        #region Sprawdzanie wiadomości

        /// <summary>
        /// Sprawdza czy są nowe nieprzeczytane wiadomości
        /// </summary>
        public void CheckForNewMessages()
        {
            try
            {
                int unreadCount = GetUnreadMessageCount();
                var newMessages = GetNewMessagesSince(_lastCheckTime);

                // Aktualizuj czas ostatniego sprawdzenia
                _lastCheckTime = DateTime.Now;

                // Jeśli zmienił się licznik
                if (unreadCount != _lastUnreadCount)
                {
                    _lastUnreadCount = unreadCount;
                    UnreadCountChanged?.Invoke(this, unreadCount);
                }

                // Pokaż lub ukryj dymek
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (unreadCount > 0)
                    {
                        ShowBubble(unreadCount, newMessages);
                    }
                    else
                    {
                        HideBubble();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GlobalChatNotification error: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera liczbę nieprzeczytanych wiadomości
        /// </summary>
        private int GetUnreadMessageCount()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                conn.Open();

                string sql = @"
                    SELECT COUNT(*) FROM ChatMessages
                    WHERE ReceiverId = @UserId AND ReadAt IS NULL AND IsDeleted = 0";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", _userId);
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Pobiera liczbę nadawców z nieprzeczytanymi wiadomościami
        /// </summary>
        private int GetUnreadSendersCount()
        {
            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                conn.Open();

                string sql = @"
                    SELECT COUNT(DISTINCT SenderId) FROM ChatMessages
                    WHERE ReceiverId = @UserId AND ReadAt IS NULL AND IsDeleted = 0";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", _userId);
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Pobiera nowe wiadomości od ostatniego sprawdzenia
        /// </summary>
        private List<NewMessageInfo> GetNewMessagesSince(DateTime since)
        {
            var messages = new List<NewMessageInfo>();

            if (since == DateTime.MinValue)
                return messages;

            try
            {
                using var conn = new SqlConnection(CONNECTION_STRING);
                conn.Open();

                string sql = @"
                    SELECT TOP 5 m.SenderId, m.Content, m.SentAt,
                           ISNULL(o.imienazwisko, m.SenderId) AS SenderName
                    FROM ChatMessages m
                    LEFT JOIN operators o ON m.SenderId = o.operatorid
                    WHERE m.ReceiverId = @UserId
                          AND m.ReadAt IS NULL
                          AND m.IsDeleted = 0
                          AND m.SentAt > @Since
                    ORDER BY m.SentAt DESC";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", _userId);
                cmd.Parameters.AddWithValue("@Since", since);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    messages.Add(new NewMessageInfo
                    {
                        SenderId = reader.GetString(0),
                        Content = reader.GetString(1),
                        SentAt = reader.GetDateTime(2),
                        SenderName = reader.GetString(3)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting new messages: {ex.Message}");
            }

            return messages;
        }

        #endregion

        #region Zarządzanie dymkiem

        /// <summary>
        /// Pokazuje dymek z liczbą wiadomości
        /// </summary>
        private void ShowBubble(int count, List<NewMessageInfo> newMessages)
        {
            // Nie pokazuj jeśli tymczasowo ukryty (po kliknięciu)
            if (_temporarilyHidden) return;

            if (_bubbleWindow == null || !_bubbleWindow.IsLoaded)
            {
                _bubbleWindow = new ChatBubbleNotification();
                _bubbleWindow.BubbleClicked += (s, e) =>
                {
                    // Natychmiast ukryj dymek po kliknięciu
                    HideBubble();
                    _temporarilyHidden = true; // Nie pokazuj ponownie przez chwilę
                    BubbleClicked?.Invoke(this, EventArgs.Empty);
                    OpenChatWindow();
                };
                _bubbleWindow.Closed += (s, e) =>
                {
                    _bubbleWindow = null;
                };
                _bubbleWindow.Show();
            }

            _bubbleWindow.UpdateCount(count, newMessages);
        }

        /// <summary>
        /// Ukrywa dymek
        /// </summary>
        private void HideBubble()
        {
            if (_bubbleWindow != null && _bubbleWindow.IsLoaded)
            {
                _bubbleWindow.Close();
                _bubbleWindow = null;
            }
        }

        /// <summary>
        /// Otwiera okno komunikatora
        /// </summary>
        private void OpenChatWindow()
        {
            try
            {
                // Szukaj istniejącego okna chatu
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is ChatMainWindow chatWindow)
                    {
                        chatWindow.WindowState = WindowState.Normal;
                        chatWindow.Activate();
                        return;
                    }
                }

                // Jeśli nie znaleziono, otwórz nowe
                var newChatWindow = new ChatMainWindow(App.UserID, App.UserFullName);
                newChatWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening chat window: {ex.Message}");
            }
        }

        /// <summary>
        /// Wymusza odświeżenie (np. po przeczytaniu wiadomości)
        /// </summary>
        public void Refresh()
        {
            // Zresetuj flagę tymczasowego ukrycia - wiadomości zostały przeczytane
            _temporarilyHidden = false;
            CheckForNewMessages();
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            HideBubble();
        }
    }

    /// <summary>
    /// Globalny manager powiadomień czatu dla całej aplikacji
    /// </summary>
    public static class GlobalChatManager
    {
        private static GlobalChatNotificationService _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Pobiera lub tworzy instancję serwisu
        /// </summary>
        public static GlobalChatNotificationService GetInstance(string userId)
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new GlobalChatNotificationService(userId);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Uruchamia serwis powiadomień
        /// </summary>
        public static void Start(string userId)
        {
            var instance = GetInstance(userId);
            instance.Start();
        }

        /// <summary>
        /// Zatrzymuje i zwalnia serwis
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        /// <summary>
        /// Wymusza odświeżenie
        /// </summary>
        public static void Refresh()
        {
            _instance?.Refresh();
        }

        /// <summary>
        /// Sprawdza czy serwis jest uruchomiony
        /// </summary>
        public static bool IsRunning => _instance != null;
    }
}
