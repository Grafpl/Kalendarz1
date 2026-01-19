using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Kalendarz1.Komunikator.Models;
using Kalendarz1.Komunikator.Services;

namespace Kalendarz1.Komunikator.Views
{
    /// <summary>
    /// Główne okno komunikatora firmowego
    /// </summary>
    public partial class ChatMainWindow : Window
    {
        private readonly string _currentUserId;
        private readonly string _currentUserName;
        private ChatService _chatService;
        private ChatUser _selectedUser;
        private ObservableCollection<ContactViewModel> _contacts;
        private ObservableCollection<MessageViewModel> _messages;
        private DispatcherTimer _typingSendTimer;
        private bool _isTyping = false;
        private Storyboard _typingAnimation;

        public ChatMainWindow(string userId, string userName = null)
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _currentUserId = userId;
            _currentUserName = userName ?? userId;

            _contacts = new ObservableCollection<ContactViewModel>();
            _messages = new ObservableCollection<MessageViewModel>();

            ContactsList.ItemsSource = _contacts;
            MessagesList.ItemsSource = _messages;

            // Setup typing timer (stops typing status after 3 seconds of inactivity)
            _typingSendTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _typingSendTimer.Tick += TypingSendTimer_Tick;

            Loaded += ChatMainWindow_Loaded;
            Closing += ChatMainWindow_Closing;
        }

        private async void ChatMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Pokaż skeleton loading
            ContactsSkeletonPanel.Visibility = Visibility.Visible;
            ContactsList.Visibility = Visibility.Collapsed;

            // Ustaw avatar i nazwę aktualnego użytkownika
            CurrentUserName.Text = _currentUserName;
            LoadCurrentUserAvatar();

            // Inicjalizuj serwis
            _chatService = new ChatService(_currentUserId);
            await _chatService.InitializeDatabaseAsync();

            // Rozpocznij nasłuchiwanie
            _chatService.NewMessagesReceived += OnNewMessagesReceived;
            _chatService.UserTypingChanged += OnUserTypingChanged;
            _chatService.StartPolling(3);

            // Załaduj kontakty
            await LoadContactsAsync();

            // Ukryj skeleton loading
            ContactsSkeletonPanel.Visibility = Visibility.Collapsed;
            ContactsList.Visibility = Visibility.Visible;

            // Setup typing animation
            _typingAnimation = FindResource("TypingAnimation") as Storyboard;

            // Subscribe to message input text changed
            MessageInput.TextChanged += MessageInput_TextChanged;
        }

        private void ChatMainWindow_Closing(object sender, CancelEventArgs e)
        {
            _chatService?.Dispose();
        }

        private void LoadCurrentUserAvatar()
        {
            try
            {
                BitmapSource avatar = null;
                if (UserAvatarManager.HasAvatar(_currentUserId))
                {
                    using (var img = UserAvatarManager.GetAvatarRounded(_currentUserId, 44))
                    {
                        if (img != null)
                            avatar = ConvertToBitmapSource(img);
                    }
                }

                if (avatar == null)
                {
                    using (var img = UserAvatarManager.GenerateDefaultAvatar(_currentUserName, _currentUserId, 44))
                    {
                        avatar = ConvertToBitmapSource(img);
                    }
                }

                if (avatar != null)
                    CurrentUserAvatar.ImageSource = avatar;
            }
            catch { }
        }

        private async Task LoadContactsAsync()
        {
            try
            {
                var users = await _chatService.GetAllUsersAsync();

                Dispatcher.Invoke(() =>
                {
                    _contacts.Clear();
                    foreach (var user in users)
                    {
                        _contacts.Add(new ContactViewModel(user, _currentUserId));
                    }

                    UpdateTotalUnreadBadge();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadContacts error: {ex.Message}");
            }
        }

        private void UpdateTotalUnreadBadge()
        {
            var total = _contacts.Sum(c => c.UnreadCount);
            if (total > 0)
            {
                TotalUnreadCount.Text = total > 99 ? "99+" : total.ToString();
                TotalUnreadBadge.Visibility = Visibility.Visible;

                // Update taskbar badge
                TaskbarInfo.Description = $"{total} nieprzeczytanych wiadomości";
                TaskbarInfo.Overlay = CreateBadgeOverlay(total);
            }
            else
            {
                TotalUnreadBadge.Visibility = Visibility.Collapsed;
                TaskbarInfo.Description = "Komunikator Firmowy";
                TaskbarInfo.Overlay = null;
            }
        }

        private DrawingImage CreateBadgeOverlay(int count)
        {
            var drawingGroup = new DrawingGroup();

            // Background circle
            var backgroundGeometry = new EllipseGeometry(new Point(8, 8), 8, 8);
            var backgroundDrawing = new GeometryDrawing(
                new SolidColorBrush(Color.FromRgb(37, 211, 102)), // #25D366
                null,
                backgroundGeometry);
            drawingGroup.Children.Add(backgroundDrawing);

            // Text
            var text = count > 99 ? "99+" : count.ToString();
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                count > 9 ? 8 : 10,
                Brushes.White,
                1.0);

            var textGeometry = formattedText.BuildGeometry(new Point(8 - formattedText.Width / 2, 8 - formattedText.Height / 2));
            var textDrawing = new GeometryDrawing(Brushes.White, null, textGeometry);
            drawingGroup.Children.Add(textDrawing);

            return new DrawingImage(drawingGroup);
        }

        private void OnNewMessagesReceived(object sender, List<ChatMessage> messages)
        {
            Dispatcher.Invoke(async () =>
            {
                // Odśwież listę kontaktów
                await LoadContactsAsync();

                // Jeśli jest aktywna rozmowa, dodaj nowe wiadomości
                if (_selectedUser != null)
                {
                    var relevantMessages = messages.Where(m =>
                        m.SenderId == _selectedUser.UserId || m.ReceiverId == _selectedUser.UserId).ToList();

                    if (relevantMessages.Any())
                    {
                        foreach (var msg in relevantMessages)
                        {
                            var vm = new MessageViewModel(msg, _currentUserId) { IsNew = true };
                            _messages.Add(vm);
                        }

                        // Oznacz jako przeczytane
                        await _chatService.MarkMessagesAsReadAsync(_selectedUser.UserId);

                        // Przewiń na dół i animuj
                        ScrollToBottom();
                        AnimateReceivedMessages(relevantMessages.Count);

                        // Flash window if not focused
                        if (!IsActive)
                        {
                            FlashWindow();
                        }
                    }
                }

                // Pokaż powiadomienie dla wiadomości spoza aktywnej rozmowy
                var otherMessages = _selectedUser != null
                    ? messages.Where(m => m.SenderId != _selectedUser.UserId).ToList()
                    : messages;

                foreach (var msg in otherMessages)
                {
                    ShowNotification(msg);
                }
            });
        }

        private void AnimateReceivedMessages(int count)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var animation = FindResource("MessageFadeIn") as Storyboard;
                if (animation == null) return;

                for (int i = 0; i < count; i++)
                {
                    var index = _messages.Count - 1 - i;
                    if (index < 0) break;

                    var container = MessagesList.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
                    if (container != null)
                    {
                        container.RenderTransform = new TranslateTransform();
                        animation.Begin(container);
                    }
                }
            }), DispatcherPriority.Background);
        }

        private void FlashWindow()
        {
            // Flash taskbar
            TaskbarInfo.ProgressState = TaskbarItemProgressState.Paused;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            int flashCount = 0;
            timer.Tick += (s, e) =>
            {
                flashCount++;
                TaskbarInfo.ProgressState = flashCount % 2 == 0 ? TaskbarItemProgressState.Paused : TaskbarItemProgressState.None;
                if (flashCount >= 6)
                {
                    timer.Stop();
                    TaskbarInfo.ProgressState = TaskbarItemProgressState.None;
                }
            };
            timer.Start();
        }

        private void ShowNotification(ChatMessage message)
        {
            try
            {
                var popup = new ChatNotificationPopup(message);
                popup.MessageClicked += (s, e) =>
                {
                    // Znajdź kontakt i otwórz rozmowę
                    var contact = _contacts.FirstOrDefault(c => c.UserId == message.SenderId);
                    if (contact != null)
                    {
                        SelectContact(contact);
                    }

                    // Przywróć okno na wierzch
                    this.WindowState = WindowState.Normal;
                    this.Activate();
                    this.Focus();
                };
                popup.Show();
            }
            catch { }
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                await LoadContactsAsync();
                return;
            }

            try
            {
                var users = await _chatService.SearchUsersAsync(searchText);

                _contacts.Clear();
                foreach (var user in users)
                {
                    _contacts.Add(new ContactViewModel(user, _currentUserId));
                }
            }
            catch { }
        }

        private void ContactsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContactsList.SelectedItem is ContactViewModel contact)
            {
                SelectContact(contact);
            }
        }

        private async void SelectContact(ContactViewModel contact)
        {
            _selectedUser = new ChatUser
            {
                UserId = contact.UserId,
                Name = contact.Name,
                IsOnline = contact.IsOnline
            };

            // Pokaż panel rozmowy
            NoConversationPanel.Visibility = Visibility.Collapsed;
            ConversationPanel.Visibility = Visibility.Visible;

            // Ustaw nagłówek
            SelectedUserName.Text = contact.Name;
            SelectedUserStatus.Text = contact.IsOnline ? "Online" : contact.OnlineStatus;

            // Ustaw efekty online
            if (contact.IsOnline)
            {
                SelectedUserGlow.Opacity = 0.3;
                SelectedUserBorder.Stroke = (Brush)FindResource("OnlineBrush");
                SelectedUserDot.Fill = (Brush)FindResource("OnlineBrush");
                SelectedUserStatus.Foreground = (Brush)FindResource("OnlineBrush");
            }
            else
            {
                SelectedUserGlow.Opacity = 0;
                SelectedUserBorder.Stroke = Brushes.Transparent;
                SelectedUserDot.Fill = (Brush)FindResource("OfflineBrush");
                SelectedUserStatus.Foreground = (Brush)FindResource("OfflineBrush");
            }

            // Załaduj avatar
            if (contact.AvatarSource != null)
                SelectedUserAvatar.ImageSource = contact.AvatarSource;

            // Załaduj historię rozmowy
            await LoadConversationAsync(contact.UserId);

            // Oznacz jako przeczytane
            await _chatService.MarkMessagesAsReadAsync(contact.UserId);

            // Odśwież kontakty (aby zaktualizować badge)
            contact.UnreadCount = 0;
            UpdateTotalUnreadBadge();

            // Skup się na polu wprowadzania
            MessageInput.Focus();
        }

        private async Task LoadConversationAsync(string otherUserId)
        {
            try
            {
                var messages = await _chatService.GetConversationAsync(otherUserId);

                _messages.Clear();

                MessageViewModel previousVm = null;
                DateTime? lastDate = null;

                foreach (var msg in messages)
                {
                    // Dodaj separator daty jeśli nowy dzień
                    if (lastDate == null || msg.SentAt.Date != lastDate.Value.Date)
                    {
                        _messages.Add(new MessageViewModel(msg.SentAt.Date, true));
                        lastDate = msg.SentAt.Date;
                    }

                    var vm = new MessageViewModel(msg, _currentUserId);

                    // Grupowanie - pokaż avatar tylko dla pierwszej wiadomości w grupie
                    if (previousVm != null &&
                        previousVm.SenderId == msg.SenderId &&
                        !previousVm.IsDateSeparator &&
                        (msg.SentAt - previousVm.SentAt).TotalMinutes < 5)
                    {
                        vm.ShowAvatar = false;
                        vm.IsFirstInGroup = false;
                        previousVm.IsLastInGroup = false;
                    }
                    else
                    {
                        vm.ShowAvatar = !vm.IsFromMe;
                        vm.IsFirstInGroup = true;
                    }

                    vm.IsLastInGroup = true;
                    _messages.Add(vm);
                    previousVm = vm;
                }

                // Przewiń na dół
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadConversation error: {ex.Message}");
            }
        }


        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            if (_selectedUser == null) return;

            var content = MessageInput.Text?.Trim();
            if (string.IsNullOrEmpty(content)) return;

            MessageInput.Text = "";

            // Stop typing indicator
            _typingSendTimer.Stop();
            if (_isTyping)
            {
                _isTyping = false;
                _ = _chatService.SetTypingStatusAsync(_selectedUser.UserId, false);
            }

            var success = await _chatService.SendMessageAsync(_selectedUser.UserId, content);
            if (success)
            {
                // Dodaj wiadomość do listy
                var msg = new ChatMessage
                {
                    SenderId = _currentUserId,
                    SenderName = _currentUserName,
                    ReceiverId = _selectedUser.UserId,
                    ReceiverName = _selectedUser.Name,
                    Content = content,
                    SentAt = DateTime.Now,
                    Type = MessageType.Text
                };

                var vm = new MessageViewModel(msg, _currentUserId) { IsNew = true };
                _messages.Add(vm);
                ScrollToBottom();

                // Animate the new message
                AnimateNewMessage();

                // Odśwież kontakty (aby zaktualizować ostatnią wiadomość)
                await LoadContactsAsync();
            }
        }

        private void AnimateNewMessage()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Find the last message container and animate it
                var container = MessagesList.ItemContainerGenerator.ContainerFromIndex(_messages.Count - 1) as FrameworkElement;
                if (container != null)
                {
                    var animation = FindResource("MessageSendAnimation") as Storyboard;
                    if (animation != null)
                    {
                        // Ensure the element has a ScaleTransform
                        container.RenderTransformOrigin = new Point(1, 1);
                        container.RenderTransform = new ScaleTransform(1, 1);
                        animation.Begin(container);
                    }
                }
            }), DispatcherPriority.Background);
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessagesScrollViewer.ScrollToEnd();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ContactAvatar_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement element && element.DataContext is ContactViewModel contact)
            {
                ShowAvatarPreview(contact.AvatarSource, contact.Name, contact.IsOnline, contact.OnlineStatus);
            }
        }

        private void SelectedUserAvatar_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_selectedUser != null)
            {
                var avatar = SelectedUserAvatar.ImageSource as BitmapSource;
                ShowAvatarPreview(avatar, _selectedUser.Name, _selectedUser.IsOnline);
            }
        }

        private void MessageAvatar_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement element && element.DataContext is MessageViewModel message)
            {
                ShowAvatarPreview(message.SenderAvatar, message.SenderName, false);
            }
        }

        private void ShowAvatarPreview(BitmapSource avatar, string userName, bool isOnline, string status = null)
        {
            if (avatar != null)
            {
                AvatarPreviewWindow.ShowPreview(avatar, userName, isOnline, status);
            }
        }

        #region Typing Indicator

        private void OnUserTypingChanged(object sender, TypingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Only show typing indicator for the selected user
                if (_selectedUser != null && e.UserId == _selectedUser.UserId)
                {
                    if (e.IsTyping)
                    {
                        TypingUserName.Text = e.UserName ?? _selectedUser.Name;
                        TypingIndicator.Visibility = Visibility.Visible;
                        _typingAnimation?.Begin(this, true);
                    }
                    else
                    {
                        TypingIndicator.Visibility = Visibility.Collapsed;
                        _typingAnimation?.Stop(this);
                    }
                }
            });
        }

        private void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_selectedUser == null) return;

            // Start/reset typing timer
            _typingSendTimer.Stop();
            _typingSendTimer.Start();

            // Send typing status if not already typing
            if (!_isTyping)
            {
                _isTyping = true;
                _ = _chatService.SetTypingStatusAsync(_selectedUser.UserId, true);
            }
        }

        private void TypingSendTimer_Tick(object sender, EventArgs e)
        {
            _typingSendTimer.Stop();

            // Stop typing
            if (_isTyping && _selectedUser != null)
            {
                _isTyping = false;
                _ = _chatService.SetTypingStatusAsync(_selectedUser.UserId, false);
            }
        }

        #endregion

        #region Reactions

        private void Message_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is MessageViewModel message && !message.IsDateSeparator)
            {
                ShowReactionPicker(element, message);
            }
        }

        private void ShowReactionPicker(FrameworkElement targetElement, MessageViewModel message)
        {
            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = targetElement,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 44, 51)), // #202C33
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(8, 4, 8, 4)
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var emoji in ReactionEmojis.All)
            {
                var btn = new Button
                {
                    Content = emoji,
                    Style = FindResource("ReactionButton") as Style,
                    Tag = new Tuple<MessageViewModel, string>(message, emoji)
                };
                btn.Click += ReactionButton_Click;
                panel.Children.Add(btn);
            }

            border.Child = panel;
            popup.Child = border;
            popup.IsOpen = true;
        }

        private async void ReactionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Tuple<MessageViewModel, string> data)
            {
                var message = data.Item1;
                var emoji = data.Item2;

                // Close popup
                var popup = btn.Parent as FrameworkElement;
                while (popup != null && !(popup is System.Windows.Controls.Primitives.Popup))
                    popup = popup.Parent as FrameworkElement;
                if (popup is System.Windows.Controls.Primitives.Popup p)
                    p.IsOpen = false;

                // Check if already reacted with this emoji
                var existingReaction = message.Reactions?.FirstOrDefault(r => r.Emoji == emoji && r.UserId == _currentUserId);
                if (existingReaction != null)
                {
                    await _chatService.RemoveReactionAsync(message.MessageId, emoji);
                    message.Reactions.Remove(existingReaction);
                }
                else
                {
                    await _chatService.AddReactionAsync(message.MessageId, emoji);

                    if (message.Reactions == null)
                        message.Reactions = new ObservableCollection<ReactionViewModel>();

                    // Check if this emoji already exists from others
                    var existingGroup = message.Reactions.FirstOrDefault(r => r.Emoji == emoji);
                    if (existingGroup != null)
                    {
                        existingGroup.Count++;
                        existingGroup.UserId = _currentUserId; // Mark as reacted by current user too
                    }
                    else
                    {
                        message.Reactions.Add(new ReactionViewModel
                        {
                            Emoji = emoji,
                            Count = 1,
                            UserId = _currentUserId
                        });
                    }
                }

                message.OnPropertyChanged(nameof(message.Reactions));
            }
        }

        #endregion

        private BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;

            using (var bitmap = new System.Drawing.Bitmap(image))
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }

    #region ViewModels

    public class ContactViewModel : INotifyPropertyChanged
    {
        private int _unreadCount;

        public string UserId { get; set; }
        public string Name { get; set; }
        public bool IsOnline { get; set; }
        public string OnlineStatus { get; set; }
        public string LastMessage { get; set; }
        public string FormattedLastMessageTime { get; set; }
        public BitmapSource AvatarSource { get; set; }

        public int UnreadCount
        {
            get => _unreadCount;
            set
            {
                _unreadCount = value;
                OnPropertyChanged(nameof(UnreadCount));
                OnPropertyChanged(nameof(HasUnread));
            }
        }

        public bool HasUnread => UnreadCount > 0;

        public ContactViewModel(ChatUser user, string currentUserId)
        {
            UserId = user.UserId;
            Name = user.Name;
            IsOnline = user.IsOnline;
            OnlineStatus = user.OnlineStatus;
            UnreadCount = user.UnreadCount;
            LastMessage = user.LastMessage;
            FormattedLastMessageTime = user.FormattedLastMessageTime;

            // Załaduj avatar
            LoadAvatar(user);
        }

        private void LoadAvatar(ChatUser user)
        {
            try
            {
                AvatarSource = user.GetAvatarBitmap(50);
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MessageViewModel : INotifyPropertyChanged
    {
        public int MessageId { get; set; }
        public string Content { get; set; }
        public string FormattedTime { get; set; }
        public bool IsFromMe { get; set; }
        public string ReadStatus { get; set; }
        public string SenderName { get; set; }
        public string SenderId { get; set; }
        public DateTime SentAt { get; set; }
        public BitmapSource SenderAvatar { get; set; }

        // Grupowanie wiadomości
        public bool ShowAvatar { get; set; } = true;
        public bool IsFirstInGroup { get; set; } = true;
        public bool IsLastInGroup { get; set; } = true;

        // Separator daty
        public bool IsDateSeparator { get; set; }
        public string DateText { get; set; }

        // Reakcje
        public ObservableCollection<ReactionViewModel> Reactions { get; set; } = new ObservableCollection<ReactionViewModel>();

        // Animation flag
        public bool IsNew { get; set; } = false;

        // Konstruktor dla separatora daty
        public MessageViewModel(DateTime date, bool isSeparator)
        {
            IsDateSeparator = true;
            SentAt = date;

            if (date.Date == DateTime.Today)
                DateText = "Dzisiaj";
            else if (date.Date == DateTime.Today.AddDays(-1))
                DateText = "Wczoraj";
            else if (date.Date > DateTime.Today.AddDays(-7))
                DateText = date.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
            else
                DateText = date.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("pl-PL"));
        }

        // Konstruktor dla wiadomości
        public MessageViewModel(ChatMessage message, string currentUserId)
        {
            MessageId = message.Id;
            Content = message.Content;
            FormattedTime = message.SentAt.ToString("HH:mm");
            IsFromMe = message.SenderId == currentUserId;
            ReadStatus = message.IsRead ? "✓✓" : "✓";
            SenderName = message.SenderName;
            SenderId = message.SenderId;
            SentAt = message.SentAt;
            IsDateSeparator = false;

            // Załaduj avatar nadawcy dla wiadomości od innych
            if (!IsFromMe)
            {
                LoadSenderAvatar(message.SenderId, message.SenderName);
            }
        }

        private void LoadSenderAvatar(string senderId, string senderName)
        {
            try
            {
                if (UserAvatarManager.HasAvatar(senderId))
                {
                    using (var img = UserAvatarManager.GetAvatarRounded(senderId, 32))
                    {
                        if (img != null)
                        {
                            SenderAvatar = ConvertToBitmapSource(img);
                            return;
                        }
                    }
                }

                using (var img = UserAvatarManager.GenerateDefaultAvatar(senderName, senderId, 32))
                {
                    SenderAvatar = ConvertToBitmapSource(img);
                }
            }
            catch { }
        }

        private BitmapSource ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;

            using (var bitmap = new System.Drawing.Bitmap(image))
            {
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ReactionViewModel : INotifyPropertyChanged
    {
        private int _count = 1;

        public string Emoji { get; set; }
        public string UserId { get; set; }

        public int Count
        {
            get => _count;
            set
            {
                _count = value;
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(nameof(ShowCount));
            }
        }

        public bool ShowCount => Count > 1;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion

    #region Converters

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
