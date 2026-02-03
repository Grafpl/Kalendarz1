using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Windows;
using System.Windows.Input;

namespace Kalendarz1.CRM.Dialogs
{
    public partial class EmailTemplateDialog : Window
    {
        private readonly string _connectionString;
        private readonly DataRowView _contact;
        private readonly string _operatorId;
        private string _operatorName;
        private string _recipientEmail;

        public EmailTemplateDialog(string connectionString, DataRowView contact, string operatorId)
        {
            InitializeComponent();
            _connectionString = connectionString;
            _contact = contact;
            _operatorId = operatorId;

            LoadOperatorName();
            InitializeForm();
        }

        private void LoadOperatorName()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                var cmd = new SqlCommand("SELECT Name FROM operators WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", _operatorId);
                _operatorName = cmd.ExecuteScalar()?.ToString() ?? "Handlowiec";
            }
            catch
            {
                _operatorName = "Handlowiec";
            }
        }

        private void InitializeForm()
        {
            _recipientEmail = _contact["EMAIL"]?.ToString()?.Trim() ?? "";
            string nazwa = _contact["NAZWA"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(_recipientEmail) || !_recipientEmail.Contains("@"))
            {
                txtRecipient.Text = "Brak adresu email!";
                btnSend.IsEnabled = false;
            }
            else
            {
                txtRecipient.Text = $"Do: {_recipientEmail}";
            }

            // Default template
            ApplyTemplate("offer");
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag != null)
            {
                ApplyTemplate(btn.Tag.ToString());
            }
        }

        private void ApplyTemplate(string templateType)
        {
            string nazwa = _contact["NAZWA"]?.ToString() ?? "Szanowni Panstwo";
            string miasto = _contact["MIASTO"]?.ToString() ?? "";
            string osoba = _contact["OsobaKontaktowa"]?.ToString() ?? "";

            string subject = "";
            string body = "";

            switch (templateType)
            {
                case "offer":
                    subject = "Oferta wspolpracy - {{Nazwa}}";
                    body = @"Szanowni Panstwo,

Dziekujemy za zainteresowanie nasza oferta. W zalaczeniu przesylamy szczegolowa propozycje wspolpracy.

Nasza firma specjalizuje sie w dostarczaniu wysokiej jakosci uslug, ktore moga znaczaco wspomoc Panstwa dzialalnosc.

Chetnie odpowiemy na wszelkie pytania i omowimy szczegoly podczas rozmowy telefonicznej.

Z powazaniem,
{{Handlowiec}}
PRONOVA";
                    break;

                case "followup":
                    subject = "Kontynuacja rozmowy - {{Nazwa}}";
                    body = @"Szanowni Panstwo,

Nawiazujac do naszej ostatniej rozmowy, chcialem sie upewnic, czy mieli Panstwo okazje zapoznac sie z przeslana oferta.

Czy maja Panstwo jakies pytania lub watpliwosci, ktore moglbym wyjasnic?

Chetnie umowie sie na rozmowe w dogodnym dla Panstwa terminie.

Z powazaniem,
{{Handlowiec}}
PRONOVA";
                    break;

                case "reminder":
                    subject = "Przypomnienie - {{Nazwa}}";
                    body = @"Szanowni Panstwo,

Pozwalam sobie przypomnieo o naszej ofercie wspolpracy.

Jestem przekonany, ze nasze rozwiazania moga przyniesc Panstwu wymierne korzysci. Chetnie przedstawie szczegoly podczas krotkie] rozmowy telefonicznej.

Prosze o kontakt w dogodnym terminie.

Z powazaniem,
{{Handlowiec}}
PRONOVA";
                    break;

                case "thanks":
                    subject = "Podziekowanie za rozmowe - {{Nazwa}}";
                    body = @"Szanowni Panstwo,

Serdecznie dziekuje za dzisiejsza rozmowe i poswiecony czas.

Zgodnie z ustaleniami, przygotuje dla Panstwa szczegolowa oferte i przesle ja w najblizszych dniach.

W razie jakichkolwiek pytan, pozostaje do dyspozycji.

Z powazaniem,
{{Handlowiec}}
PRONOVA";
                    break;

                case "info":
                    subject = "Informacja - {{Nazwa}}";
                    body = @"Szanowni Panstwo,

Przesylam informacje, o ktore Panstwo prosili.

W razie dodatkowych pytan, chetnie sluze pomoca.

Z powazaniem,
{{Handlowiec}}
PRONOVA";
                    break;
            }

            // Replace variables
            subject = ReplaceVariables(subject);
            body = ReplaceVariables(body);

            txtSubject.Text = subject;
            txtBody.Text = body;
        }

        private string ReplaceVariables(string text)
        {
            string nazwa = _contact["NAZWA"]?.ToString() ?? "";
            string miasto = _contact["MIASTO"]?.ToString() ?? "";
            string osoba = _contact["OsobaKontaktowa"]?.ToString() ?? "";

            return text
                .Replace("{{Nazwa}}", nazwa)
                .Replace("{{Miasto}}", miasto)
                .Replace("{{OsobaKontaktowa}}", string.IsNullOrEmpty(osoba) ? "Szanowni Panstwo" : osoba)
                .Replace("{{Handlowiec}}", _operatorName);
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_recipientEmail) || !_recipientEmail.Contains("@"))
            {
                MessageBox.Show("Brak prawidlowego adresu email.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtSubject.Text))
            {
                MessageBox.Show("Wprowadz temat wiadomosci.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtBody.Text))
            {
                MessageBox.Show("Wprowadz tresc wiadomosci.", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Open default email client with mailto link
                string subject = Uri.EscapeDataString(txtSubject.Text);
                string body = Uri.EscapeDataString(txtBody.Text);
                string mailto = $"mailto:{_recipientEmail}?subject={subject}&body={body}";

                Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });

                // Save note if checked
                if (chkSaveNote.IsChecked == true)
                {
                    SaveEmailNote();
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas otwierania klienta email: {ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveEmailNote()
        {
            try
            {
                int contactId = Convert.ToInt32(_contact["ID"]);

                using var conn = new SqlConnection(_connectionString);
                conn.Open();

                var cmd = new SqlCommand(@"
                    INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, DataUtworzenia, Operator)
                    VALUES (@id, @tresc, GETDATE(), @op)", conn);
                cmd.Parameters.AddWithValue("@id", contactId);
                cmd.Parameters.AddWithValue("@tresc", $"[Email] Temat: {txtSubject.Text}");
                cmd.Parameters.AddWithValue("@op", _operatorId);
                cmd.ExecuteNonQuery();

                // Log to history
                var cmdLog = new SqlCommand(@"
                    INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany)
                    VALUES (@id, 'Wyslano email', @val, @op, GETDATE())", conn);
                cmdLog.Parameters.AddWithValue("@id", contactId);
                cmdLog.Parameters.AddWithValue("@val", $"Temat: {txtSubject.Text}");
                cmdLog.Parameters.AddWithValue("@op", _operatorId);
                cmdLog.ExecuteNonQuery();
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
