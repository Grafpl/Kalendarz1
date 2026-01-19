using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Kalendarz1.Zadania
{
    public partial class MeetingChangePopup : Window
    {
        private DispatcherTimer autoCloseTimer;
        private List<MeetingChange> changes;

        public event EventHandler<long> ViewMeetingRequested;

        // Colors
        private static readonly Color WarningOrange = (Color)ColorConverter.ConvertFromString("#F39C12");
        private static readonly Color AlertRed = (Color)ColorConverter.ConvertFromString("#E74C3C");
        private static readonly Color InfoBlue = (Color)ColorConverter.ConvertFromString("#3498DB");
        private static readonly Color PrimaryGreen = (Color)ColorConverter.ConvertFromString("#27AE60");
        private static readonly Color TextGray = (Color)ColorConverter.ConvertFromString("#7F8C8D");
        private static readonly Color CardBg = (Color)ColorConverter.ConvertFromString("#242B35");

        public MeetingChangePopup()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            changes = new List<MeetingChange>();
            Loaded += MeetingChangePopup_Loaded;
        }

        private void MeetingChangePopup_Loaded(object sender, RoutedEventArgs e)
        {
            PositionWindow();
            PlaySlideInAnimation();
            StartAutoCloseTimer();
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - ActualWidth - 10;
            Top = workArea.Bottom - ActualHeight - 10;
        }

        private void PlaySlideInAnimation()
        {
            var storyboard = (Storyboard)FindResource("SlideIn");
            storyboard.Begin(this);
        }

        private void StartAutoCloseTimer()
        {
            autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            autoCloseTimer.Tick += (s, e) =>
            {
                autoCloseTimer.Stop();
                CloseWithAnimation();
            };
            autoCloseTimer.Start();
        }

        private void CloseWithAnimation()
        {
            var storyboard = (Storyboard)FindResource("SlideOut");
            storyboard.Completed += (s, e) => Close();
            storyboard.Begin(this);
        }

        public void ShowChanges(List<MeetingChange> meetingChanges)
        {
            changes = meetingChanges;
            BuildContent();
        }

        private void BuildContent()
        {
            changesPanel.Children.Clear();

            if (changes.Count == 0) return;

            // Set icon and title based on change type
            var firstChange = changes[0];
            SetHeaderStyle(firstChange.ChangeType);

            foreach (var change in changes)
            {
                changesPanel.Children.Add(CreateChangeCard(change));
            }
        }

        private void SetHeaderStyle(MeetingChangeType type)
        {
            LinearGradientBrush gradient;
            string icon;
            string title;
            Color shadowColor;

            switch (type)
            {
                case MeetingChangeType.TimeChanged:
                    gradient = new LinearGradientBrush(WarningOrange, Color.FromArgb(255, 230, 126, 34), 45);
                    icon = "🕐";
                    title = "Zmiana terminu";
                    shadowColor = WarningOrange;
                    break;
                case MeetingChangeType.Cancelled:
                    gradient = new LinearGradientBrush(AlertRed, Color.FromArgb(255, 192, 57, 43), 45);
                    icon = "❌";
                    title = "Spotkanie anulowane";
                    shadowColor = AlertRed;
                    break;
                case MeetingChangeType.RemovedFromMeeting:
                    gradient = new LinearGradientBrush(AlertRed, Color.FromArgb(255, 192, 57, 43), 45);
                    icon = "👋";
                    title = "Usunięto ze spotkania";
                    shadowColor = AlertRed;
                    break;
                case MeetingChangeType.AddedToMeeting:
                    gradient = new LinearGradientBrush(PrimaryGreen, Color.FromArgb(255, 30, 132, 73), 45);
                    icon = "✉️";
                    title = "Nowe zaproszenie";
                    shadowColor = PrimaryGreen;
                    break;
                case MeetingChangeType.LocationChanged:
                    gradient = new LinearGradientBrush(InfoBlue, Color.FromArgb(255, 41, 128, 185), 45);
                    icon = "📍";
                    title = "Zmiana lokalizacji";
                    shadowColor = InfoBlue;
                    break;
                default:
                    gradient = new LinearGradientBrush(InfoBlue, Color.FromArgb(255, 41, 128, 185), 45);
                    icon = "ℹ️";
                    title = "Aktualizacja spotkania";
                    shadowColor = InfoBlue;
                    break;
            }

            iconBorder.Background = gradient;
            iconBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.4,
                Color = shadowColor
            };
            iconText.Text = icon;
            txtTitle.Text = title;
            txtTime.Text = "Przed chwilą";

            // Update action button
            btnAction.Background = gradient;
        }

        private Border CreateChangeCard(MeetingChange change)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            // Meeting title
            stack.Children.Add(new TextBlock
            {
                Text = change.MeetingTitle,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            // Change description
            var descText = GetChangeDescription(change);
            stack.Children.Add(new TextBlock
            {
                Text = descText,
                FontSize = 11,
                Foreground = new SolidColorBrush(TextGray),
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            // Show old vs new for time/location changes
            if (change.ChangeType == MeetingChangeType.TimeChanged && change.OldValue != null && change.NewValue != null)
            {
                var comparePanel = new Grid { Margin = new Thickness(0, 10, 0, 0) };
                comparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                comparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                comparePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Old time
                var oldStack = new StackPanel();
                oldStack.Children.Add(new TextBlock
                {
                    Text = "BYŁO",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(AlertRed)
                });
                oldStack.Children.Add(new TextBlock
                {
                    Text = change.OldValue,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                    TextDecorations = TextDecorations.Strikethrough
                });
                Grid.SetColumn(oldStack, 0);
                comparePanel.Children.Add(oldStack);

                // Arrow
                var arrow = new TextBlock
                {
                    Text = "→",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(TextGray),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 8, 10, 0)
                };
                Grid.SetColumn(arrow, 1);
                comparePanel.Children.Add(arrow);

                // New time
                var newStack = new StackPanel();
                newStack.Children.Add(new TextBlock
                {
                    Text = "TERAZ",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(PrimaryGreen)
                });
                newStack.Children.Add(new TextBlock
                {
                    Text = change.NewValue,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                });
                Grid.SetColumn(newStack, 2);
                comparePanel.Children.Add(newStack);

                stack.Children.Add(comparePanel);
            }

            card.Child = stack;
            return card;
        }

        private string GetChangeDescription(MeetingChange change)
        {
            switch (change.ChangeType)
            {
                case MeetingChangeType.TimeChanged:
                    return "Termin spotkania został zmieniony";
                case MeetingChangeType.Cancelled:
                    return "To spotkanie zostało anulowane przez organizatora";
                case MeetingChangeType.RemovedFromMeeting:
                    return "Zostałeś usunięty z listy uczestników";
                case MeetingChangeType.AddedToMeeting:
                    return "Zostałeś zaproszony na to spotkanie";
                case MeetingChangeType.LocationChanged:
                    return $"Nowa lokalizacja: {change.NewValue}";
                default:
                    return "Spotkanie zostało zaktualizowane";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            autoCloseTimer?.Stop();
            CloseWithAnimation();
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e)
        {
            autoCloseTimer?.Stop();
            CloseWithAnimation();
        }

        private void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            autoCloseTimer?.Stop();
            if (changes.Count > 0)
            {
                ViewMeetingRequested?.Invoke(this, changes[0].MeetingId);
            }
            CloseWithAnimation();
        }
    }

    public enum MeetingChangeType
    {
        TimeChanged,
        LocationChanged,
        Cancelled,
        RemovedFromMeeting,
        AddedToMeeting,
        Updated
    }

    public class MeetingChange
    {
        public long MeetingId { get; set; }
        public string MeetingTitle { get; set; }
        public MeetingChangeType ChangeType { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }
}
