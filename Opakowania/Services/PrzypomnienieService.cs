using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do zarządzania przypomnieniami o potwierdzeniach sald
    /// </summary>
    public class PrzypomnienieService
    {
        private static readonly string _connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;" +
            "Min Pool Size=1;Max Pool Size=5;Connection Lifetime=300;Pooling=true;";

        // Konfiguracja SMTP
        private readonly string _smtpServer = "smtp.pronova.pl";
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "opakowania@pronova.pl";
        private readonly string _smtpPassword = "your_password";
        private readonly string _fromEmail = "opakowania@pronova.pl";
        private readonly string _fromName = "PRONOVA - Dział Opakowań";
        private readonly bool _enableSsl = true;

        #region Wysyłanie przypomnień

        /// <summary>
        /// Wysyła przypomnienie do kontrahenta i zapisuje w historii
        /// </summary>
        public async Task<bool> WyslijPrzypomnienieAsync(
            int kontrahentId,
            string kontrahentNazwa,
            string email,
            int saldoE2,
            int saldoH1,
            string uzytkownikId)
        {
            try
            {
                // Wysyłka emaila
                var subject = $"Przypomnienie - Potwierdzenie salda opakowań - PRONOVA";
                var body = GenerujTrescPrzypomnienia(kontrahentNazwa, saldoE2, saldoH1);

                await WyslijEmailAsync(email, subject, body);

                // Zapisz w historii
                await ZapiszHistoriePrzypomnienaAsync(kontrahentId, kontrahentNazwa, email, uzytkownikId);

                Debug.WriteLine($"[PRZYPOMNIENIE] Wysłano do {kontrahentNazwa} ({email})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PRZYPOMNIENIE] Błąd: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Wysyła masowe przypomnienia
        /// </summary>
        public async Task<(int wyslanych, int bledow)> WyslijMasowePrzypomnieniasync(
            IEnumerable<(int kontrahentId, string nazwa, string email, int e2, int h1)> kontrahenci,
            string uzytkownikId)
        {
            int wyslanych = 0;
            int bledow = 0;

            foreach (var k in kontrahenci)
            {
                try
                {
                    await WyslijPrzypomnienieAsync(k.kontrahentId, k.nazwa, k.email, k.e2, k.h1, uzytkownikId);
                    wyslanych++;
                    await Task.Delay(500); // Opóźnienie między wysyłkami
                }
                catch
                {
                    bledow++;
                }
            }

            return (wyslanych, bledow);
        }

        #endregion

        #region Historia przypomnień

        /// <summary>
        /// Pobiera historię wysłanych przypomnień (ostatnie przypomnienie per kontrahent)
        /// </summary>
        public async Task<Dictionary<int, DateTime>> PobierzHistoriePrzypomnienAsync()
        {
            var wynik = new Dictionary<int, DateTime>();

            string query = @"
SELECT KontrahentId, MAX(DataWyslania) AS OstatniaData
FROM [LibraNet].[dbo].[HistoriaPrzypomnienSald] WITH (NOLOCK)
WHERE DataWyslania >= DATEADD(MONTH, -6, GETDATE())
GROUP BY KontrahentId";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var kontrahentId = reader.GetInt32(0);
                    var data = reader.GetDateTime(1);
                    wynik[kontrahentId] = data;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PRZYPOMNIENIE] Błąd pobierania historii: {ex.Message}");
            }

            return wynik;
        }

        /// <summary>
        /// Pobiera szczegółową historię przypomnień dla kontrahenta
        /// </summary>
        public async Task<List<HistoriaPrzypomnienia>> PobierzHistorieKontrahentaAsync(int kontrahentId)
        {
            var wynik = new List<HistoriaPrzypomnienia>();

            string query = @"
SELECT Id, KontrahentId, KontrahentNazwa, Email, DataWyslania, UzytkownikId, UzytkownikNazwa, Typ
FROM [LibraNet].[dbo].[HistoriaPrzypomnienSald] WITH (NOLOCK)
WHERE KontrahentId = @KontrahentId
ORDER BY DataWyslania DESC";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@KontrahentId", kontrahentId);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    wynik.Add(new HistoriaPrzypomnienia
                    {
                        Id = reader.GetInt32(0),
                        KontrahentId = reader.GetInt32(1),
                        KontrahentNazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        DataWyslania = reader.GetDateTime(4),
                        UzytkownikId = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        UzytkownikNazwa = reader.IsDBNull(6) ? "" : reader.GetString(6),
                        Typ = reader.IsDBNull(7) ? "Przypomnienie" : reader.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PRZYPOMNIENIE] Błąd pobierania historii kontrahenta: {ex.Message}");
            }

            return wynik;
        }

        /// <summary>
        /// Zapisuje wysłane przypomnienie w historii
        /// </summary>
        private async Task ZapiszHistoriePrzypomnienaAsync(
            int kontrahentId,
            string kontrahentNazwa,
            string email,
            string uzytkownikId)
        {
            string query = @"
INSERT INTO [LibraNet].[dbo].[HistoriaPrzypomnienSald]
(KontrahentId, KontrahentNazwa, Email, DataWyslania, UzytkownikId, Typ)
VALUES
(@KontrahentId, @KontrahentNazwa, @Email, GETDATE(), @UzytkownikId, 'Przypomnienie')";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                command.Parameters.AddWithValue("@KontrahentNazwa", kontrahentNazwa ?? "");
                command.Parameters.AddWithValue("@Email", email ?? "");
                command.Parameters.AddWithValue("@UzytkownikId", uzytkownikId ?? "");

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PRZYPOMNIENIE] Błąd zapisu historii: {ex.Message}");
                // Nie rzucaj wyjątku - email został wysłany, zapis historii nie jest krytyczny
            }
        }

        /// <summary>
        /// Pobiera statystyki przypomnień
        /// </summary>
        public async Task<StatystykiPrzypomnien> PobierzStatystykiAsync()
        {
            var statystyki = new StatystykiPrzypomnien();

            string query = @"
SELECT
    COUNT(*) AS Lacznie,
    COUNT(DISTINCT KontrahentId) AS UnikalniKontrahenci,
    SUM(CASE WHEN DataWyslania >= DATEADD(DAY, -7, GETDATE()) THEN 1 ELSE 0 END) AS OstatnichTydzien,
    SUM(CASE WHEN DataWyslania >= DATEADD(DAY, -30, GETDATE()) THEN 1 ELSE 0 END) AS OstatnichMiesiac
FROM [LibraNet].[dbo].[HistoriaPrzypomnienSald] WITH (NOLOCK)
WHERE DataWyslania >= DATEADD(YEAR, -1, GETDATE())";

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    statystyki.Lacznie = reader.GetInt32(0);
                    statystyki.UnikalniKontrahenci = reader.GetInt32(1);
                    statystyki.OstatnichTydzien = reader.GetInt32(2);
                    statystyki.OstatnichMiesiac = reader.GetInt32(3);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PRZYPOMNIENIE] Błąd pobierania statystyk: {ex.Message}");
            }

            return statystyki;
        }

        #endregion

        #region Email

        private async Task WyslijEmailAsync(string emailOdbiorcy, string subject, string body)
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_fromEmail, _fromName);
            message.To.Add(new MailAddress(emailOdbiorcy));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;
            message.BodyEncoding = Encoding.UTF8;
            message.SubjectEncoding = Encoding.UTF8;

            using var client = new SmtpClient(_smtpServer, _smtpPort);
            client.Credentials = new NetworkCredential(_smtpUser, _smtpPassword);
            client.EnableSsl = _enableSsl;
            client.Timeout = 30000;

            await client.SendMailAsync(message);
        }

        private string GenerujTrescPrzypomnienia(string nazwaKontrahenta, int saldoE2, int saldoH1)
        {
            var saldaHtml = new StringBuilder();

            if (saldoE2 != 0)
            {
                var kolor = saldoE2 > 0 ? "#CC2F37" : "#4B833C";
                var tekst = saldoE2 > 0 ? $"+{saldoE2} (do zwrotu)" : $"{saldoE2} (do odebrania)";
                saldaHtml.AppendLine($@"
                <tr>
                    <td style='padding: 12px; border-bottom: 1px solid #eee;'><strong>Pojemnik Drobiowy E2</strong></td>
                    <td style='padding: 12px; border-bottom: 1px solid #eee; text-align: right; font-weight: bold; color: {kolor};'>{tekst}</td>
                </tr>");
            }

            if (saldoH1 != 0)
            {
                var kolor = saldoH1 > 0 ? "#CC2F37" : "#4B833C";
                var tekst = saldoH1 > 0 ? $"+{saldoH1} (do zwrotu)" : $"{saldoH1} (do odebrania)";
                saldaHtml.AppendLine($@"
                <tr>
                    <td style='padding: 12px;'><strong>Paleta H1</strong></td>
                    <td style='padding: 12px; text-align: right; font-weight: bold; color: {kolor};'>{tekst}</td>
                </tr>");
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 0; }}
        .header {{ background: linear-gradient(135deg, #4B833C, #2D5016); color: white; padding: 30px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .header p {{ margin: 10px 0 0 0; opacity: 0.9; font-size: 14px; }}
        .content {{ background: #fff; padding: 30px; }}
        .alert-box {{ background: #FFF3CD; border-left: 4px solid #FFC107; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .alert-box strong {{ color: #856404; }}
        .saldo-table {{ width: 100%; border-collapse: collapse; margin: 20px 0; background: #f8f9fa; border-radius: 8px; overflow: hidden; }}
        .saldo-table th {{ background: #4B833C; color: white; padding: 12px; text-align: left; }}
        .btn {{ display: inline-block; background: #4B833C; color: white; padding: 14px 28px; text-decoration: none; border-radius: 6px; margin-top: 20px; font-weight: bold; }}
        .btn:hover {{ background: #3a6d30; }}
        .footer {{ background: #f5f5f5; padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .footer a {{ color: #4B833C; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>PRONOVA SP. Z O.O.</h1>
            <p>Przypomnienie o potwierdzeniu salda opakowań</p>
        </div>
        <div class='content'>
            <p>Szanowni Państwo,</p>

            <div class='alert-box'>
                <strong>Przypomnienie!</strong><br/>
                Uprzejmie prosimy o potwierdzenie aktualnego salda opakowań dla firmy <strong>{nazwaKontrahenta}</strong>.
            </div>

            <p>Aktualne saldo na dzień <strong>{DateTime.Today:dd.MM.yyyy}</strong>:</p>

            <table class='saldo-table'>
                <tr>
                    <th>Typ opakowania</th>
                    <th style='text-align: right;'>Saldo</th>
                </tr>
                {saldaHtml}
            </table>

            <p><strong>Prosimy o:</strong></p>
            <ul>
                <li>Potwierdzenie zgodności powyższych sald</li>
                <li>W przypadku rozbieżności - kontakt z działem opakowań</li>
            </ul>

            <p>Odpowiedź prosimy przesłać poprzez e-mail zwrotny lub kontakt telefoniczny.</p>

            <p style='margin-top: 30px;'>Z poważaniem,<br/>
            <strong>Dział Opakowań</strong><br/>
            PRONOVA SP. Z O.O.</p>
        </div>
        <div class='footer'>
            <p>Ta wiadomość została wygenerowana automatycznie przez system zarządzania opakowaniami.</p>
            <p>Kontakt: <a href='mailto:opakowania@pronova.pl'>opakowania@pronova.pl</a></p>
        </div>
    </div>
</body>
</html>";
        }

        #endregion
    }

    /// <summary>
    /// Rekord historii przypomnienia
    /// </summary>
    public class HistoriaPrzypomnienia
    {
        public int Id { get; set; }
        public int KontrahentId { get; set; }
        public string KontrahentNazwa { get; set; }
        public string Email { get; set; }
        public DateTime DataWyslania { get; set; }
        public string UzytkownikId { get; set; }
        public string UzytkownikNazwa { get; set; }
        public string Typ { get; set; }

        public string DataWyslaniaText => DataWyslania.ToString("dd.MM.yyyy HH:mm");
    }

    /// <summary>
    /// Statystyki przypomnień
    /// </summary>
    public class StatystykiPrzypomnien
    {
        public int Lacznie { get; set; }
        public int UnikalniKontrahenci { get; set; }
        public int OstatnichTydzien { get; set; }
        public int OstatnichMiesiac { get; set; }
    }
}
