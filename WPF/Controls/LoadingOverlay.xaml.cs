using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.WPF.Controls
{
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(LoadingOverlay),
                new PropertyMetadata(false));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty LoadingMessageProperty =
            DependencyProperty.Register(nameof(LoadingMessage), typeof(string), typeof(LoadingOverlay),
                new PropertyMetadata("Ładowanie danych..."));

        public string LoadingMessage
        {
            get => (string)GetValue(LoadingMessageProperty);
            set => SetValue(LoadingMessageProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(LoadingOverlay),
                new PropertyMetadata(0.0));

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty ShowProgressProperty =
            DependencyProperty.Register(nameof(ShowProgress), typeof(bool), typeof(LoadingOverlay),
                new PropertyMetadata(false));

        public bool ShowProgress
        {
            get => (bool)GetValue(ShowProgressProperty);
            set => SetValue(ShowProgressProperty, value);
        }

        #endregion

        #region Helper Methods

        public void Show(string message = "Ładowanie danych...")
        {
            LoadingMessage = message;
            IsLoading = true;
        }

        public void Hide()
        {
            IsLoading = false;
            Progress = 0;
        }

        public void UpdateProgress(double progress, string message = null)
        {
            Progress = progress;
            if (message != null)
                LoadingMessage = message;
        }

        #endregion
    }

    /// <summary>
    /// Helper do zarządzania loading overlay z using pattern
    /// </summary>
    public class LoadingScope : System.IDisposable
    {
        private readonly LoadingOverlay _overlay;

        public LoadingScope(LoadingOverlay overlay, string message = "Ładowanie danych...")
        {
            _overlay = overlay;
            _overlay?.Show(message);
        }

        public void UpdateMessage(string message)
        {
            if (_overlay != null)
                _overlay.LoadingMessage = message;
        }

        public void UpdateProgress(double progress)
        {
            if (_overlay != null)
            {
                _overlay.ShowProgress = true;
                _overlay.Progress = progress;
            }
        }

        public void Dispose()
        {
            _overlay?.Hide();
        }
    }
}
