using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Data.SqlClient;
using Kalendarz1.CRM.Models;
using Kalendarz1.CRM.Services;
using Kalendarz1.CRM.Dialogs;

namespace Kalendarz1.CRM
{
    public partial class CallReminderWindow : Window
    {
        private readonly string _connectionString;
        private readonly string _userID;
        private readonly CallReminderConfig _config;
        private List<ContactToCall> _contacts;
        private int _reminderLogID;
        private int _callsCount = 0;
        private int _notesCount = 0;
        private int _statusChangesCount = 0;

        public CallReminderWindow(string connectionString, string userID, CallReminderConfig config)
        {
            InitializeComponent();

            _connectionString = connectionString;
            _userID = userID;
            _config = config;

            txtTime.Text = DateTime.Now.ToString("HH:mm");
            LoadContacts();
        }

        private void LoadContacts()
        {
            _contacts = CallReminderService.Instance.GetRandomContacts(_config?.ContactsPerReminder ?? 5);

            if (_contacts.Count == 0)
            {
                MessageBox.Show("Brak kontaktów do wyświetlenia.\nWszystkie kontakty zostały już dziś obsłużone lub nie ma kontaktów spełniających kryteria.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                return;
            }

            contactsList.ItemsSource = _contacts;
            txtSubtitle.Text = $"Zadzwoń do {_contacts.Count} losowych kontaktów";
            txtProgress.Text = $"0 / {_contacts.Count} obsłużonych";
            progressBar.Maximum = _contacts.Count;

            // Create reminder log
            _reminderLogID = CallReminderService.Instance.CreateReminderLog(_contacts.Count);
        }

        private void UpdateProgress()
        {
            int completed = _contacts.Count(c => c.IsCompleted);
            progressBar.Value = completed;
            txtProgress.Text = $"{completed} / {_contacts.Count} obsłużonych";

            // Enable close button if at least 50% completed
            btnClose.IsEnabled = completed >= (_contacts.Count / 2.0) || !string.IsNullOrWhiteSpace(txtSkipReason.Text);

            // Refresh the list to show completed indicators
            contactsList.Items.Refresh();
        }

        private void BtnCall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContactToCall contact)
            {
                // Open phone dialer
                try
                {
                    var phone = contact.Telefon?.Replace(" ", "").Replace("-", "");
                    if (!string.IsNullOrEmpty(phone))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"tel:{phone}",
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening phone: {ex.Message}");
                }

                // Show call result dialog
                var dialog = new CallResultDialog(contact.Nazwa);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    contact.WasCalled = true;
                    _callsCount++;

                    // Handle status change from dialog
                    if (!string.IsNullOrEmpty(dialog.SelectedStatus) && dialog.SelectedStatus != contact.Status)
                    {
                        ChangeContactStatus(contact, dialog.SelectedStatus);
                    }

                    // Handle note from dialog
                    if (!string.IsNullOrWhiteSpace(dialog.Note))
                    {
                        AddNoteToContact(contact, dialog.Note);
                    }

                    // Log action
                    CallReminderService.Instance.LogContactAction(_reminderLogID, contact.ID,
                        true, !string.IsNullOrWhiteSpace(dialog.Note), contact.StatusChanged, contact.NewStatus);

                    UpdateProgress();
                }
            }
        }

        private void BtnNote_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContactToCall contact)
            {
                var dialog = new AddNoteDialog(contact.Nazwa);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NoteText))
                {
                    AddNoteToContact(contact, dialog.NoteText);
                    contact.NoteAdded = true;

                    CallReminderService.Instance.LogContactAction(_reminderLogID, contact.ID,
                        false, true, false, null);

                    UpdateProgress();
                }
            }
        }

        private void BtnStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContactToCall contact)
            {
                var dialog = new ChangeStatusDialog(contact.Nazwa, contact.Status);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedStatus))
                {
                    ChangeContactStatus(contact, dialog.SelectedStatus);

                    CallReminderService.Instance.LogContactAction(_reminderLogID, contact.ID,
                        false, false, true, dialog.SelectedStatus);

                    UpdateProgress();
                }
            }
        }

        private void BtnGoogle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ContactToCall contact)
            {
                try
                {
                    var query = System.Net.WebUtility.UrlEncode($"{contact.Nazwa} {contact.Miasto}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"https://www.google.com/search?q={query}",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening Google: {ex.Message}");
                }
            }
        }

        private void AddNoteToContact(ContactToCall contact, string note)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(
                    "INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @note, @user)", conn);
                cmd.Parameters.AddWithValue("@id", contact.ID);
                cmd.Parameters.AddWithValue("@note", note);
                cmd.Parameters.AddWithValue("@user", _userID);
                cmd.ExecuteNonQuery();

                _notesCount++;
                contact.NoteAdded = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeContactStatus(ContactToCall contact, string newStatus)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                // Update status
                var cmdUpdate = new SqlCommand(
                    "UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn);
                cmdUpdate.Parameters.AddWithValue("@status", newStatus);
                cmdUpdate.Parameters.AddWithValue("@id", contact.ID);
                cmdUpdate.ExecuteNonQuery();

                // Log history
                var cmdLog = new SqlCommand(
                    "INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) " +
                    "VALUES (@id, 'Zmiana statusu', @val, @user, GETDATE())", conn);
                cmdLog.Parameters.AddWithValue("@id", contact.ID);
                cmdLog.Parameters.AddWithValue("@val", newStatus);
                cmdLog.Parameters.AddWithValue("@user", _userID);
                cmdLog.ExecuteNonQuery();

                _statusChangesCount++;
                contact.StatusChanged = true;
                contact.NewStatus = newStatus;
                contact.Status = newStatus;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            // Toggle skip reason panel
            skipReasonPanel.Visibility = skipReasonPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (skipReasonPanel.Visibility == Visibility.Visible)
            {
                txtSkipReason.Focus();
            }

            // Enable close if reason provided
            btnClose.IsEnabled = !string.IsNullOrWhiteSpace(txtSkipReason.Text) ||
                                _contacts.Count(c => c.IsCompleted) >= (_contacts.Count / 2.0);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            int completed = _contacts.Count(c => c.IsCompleted);
            bool hasSkipReason = !string.IsNullOrWhiteSpace(txtSkipReason.Text);

            // Validate
            if (completed < (_contacts.Count / 2.0) && !hasSkipReason)
            {
                MessageBox.Show("Obsłuż przynajmniej połowę kontaktów lub podaj powód pominięcia!",
                    "Wymagana akcja", MessageBoxButton.OK, MessageBoxImage.Warning);
                skipReasonPanel.Visibility = Visibility.Visible;
                txtSkipReason.Focus();
                return;
            }

            // Complete reminder log
            CallReminderService.Instance.CompleteReminder(
                _reminderLogID,
                _callsCount,
                _notesCount,
                _statusChangesCount,
                hasSkipReason && completed < (_contacts.Count / 2.0),
                txtSkipReason.Text
            );

            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            int completed = _contacts?.Count(c => c.IsCompleted) ?? 0;
            bool hasSkipReason = !string.IsNullOrWhiteSpace(txtSkipReason?.Text);
            int total = _contacts?.Count ?? 1;

            // Prevent closing without action
            if (completed < (total / 2.0) && !hasSkipReason)
            {
                e.Cancel = true;
                MessageBox.Show("Obsłuż przynajmniej połowę kontaktów lub podaj powód pominięcia!",
                    "Nie można zamknąć", MessageBoxButton.OK, MessageBoxImage.Warning);
                skipReasonPanel.Visibility = Visibility.Visible;
                txtSkipReason?.Focus();
            }

            base.OnClosing(e);
        }
    }
}
