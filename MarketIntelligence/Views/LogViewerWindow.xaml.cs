using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace Kalendarz1.MarketIntelligence.Views
{
    public partial class LogViewerWindow : Window
    {
        private readonly StringBuilder _logBuilder = new StringBuilder();

        public LogViewerWindow()
        {
            InitializeComponent();
        }

        public void AppendLog(string message, string level = "INFO")
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var levelPrefix = level switch
            {
                "ERROR" => "[X]",
                "SUCCESS" => "[OK]",
                "WARNING" => "[!]",
                "DEBUG" => "[D]",
                _ => "[i]"
            };

            var line = $"{timestamp} {levelPrefix} {message}";
            _logBuilder.AppendLine(line);

            Dispatcher.Invoke(() =>
            {
                txtLogs.Text = _logBuilder.ToString();
                txtLogs.ScrollToEnd();
            });
        }

        public void AppendSeparator(string title = null)
        {
            var separator = string.IsNullOrEmpty(title)
                ? new string('=', 80)
                : $"═══════════════════ {title} ═══════════════════";

            _logBuilder.AppendLine();
            _logBuilder.AppendLine(separator);
            _logBuilder.AppendLine();

            Dispatcher.Invoke(() =>
            {
                txtLogs.Text = _logBuilder.ToString();
                txtLogs.ScrollToEnd();
            });
        }

        public void AppendRawContent(string content, string label = "RAW CONTENT")
        {
            _logBuilder.AppendLine($"--- {label} START ---");
            _logBuilder.AppendLine(content);
            _logBuilder.AppendLine($"--- {label} END ---");
            _logBuilder.AppendLine();

            Dispatcher.Invoke(() =>
            {
                txtLogs.Text = _logBuilder.ToString();
                txtLogs.ScrollToEnd();
            });
        }

        public void Clear()
        {
            _logBuilder.Clear();
            Dispatcher.Invoke(() => txtLogs.Text = "");
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtLogs.Text);
                MessageBox.Show("Logi skopiowane do schowka!", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad kopiowania: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        private void SaveToFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log|All files (*.*)|*.*",
                    DefaultExt = ".txt",
                    FileName = $"briefing_logs_{DateTime.Now:yyyy-MM-dd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, txtLogs.Text, Encoding.UTF8);
                    MessageBox.Show($"Logi zapisane do:\n{dialog.FileName}", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad zapisu: {ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
