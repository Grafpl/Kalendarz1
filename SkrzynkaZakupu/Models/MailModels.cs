using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.SkrzynkaZakupu.Models
{
    /// <summary>
    /// Ustawienia połączenia ze skrzynką (IMAP + SMTP). Hasło trzymane lokalnie poza repo.
    /// </summary>
    public class MailAccountSettings
    {
        public string Email { get; set; } = "zakup@piorkowscy.com.pl";
        public string DisplayName { get; set; } = "Dział Zakupu Piórkowscy";

        public string ImapHost { get; set; } = "imap.dpoczta.pl";
        public int ImapPort { get; set; } = 993;           // SSL

        public string SmtpHost { get; set; } = "smtp.dpoczta.pl";
        public int SmtpPort { get; set; } = 587;           // STARTTLS

        public string Login { get; set; } = "zakup@piorkowscy.com.pl";
        public string Password { get; set; } = "";          // NIE serializujemy do repo — tylko %LOCALAPPDATA%
        public string Signature { get; set; } = "";          // podpis dodawany przy pisaniu
    }

    /// <summary>
    /// Folder serwerowy IMAP (wspólny dla całej skrzynki).
    /// </summary>
    public class MailFolderModel
    {
        public string FullName { get; set; } = "";          // np. "INBOX", "INBOX.Wysłane"
        public string DisplayName { get; set; } = "";       // ładna nazwa do UI
        public string Ikona { get; set; } = "📁";
        public int Unread { get; set; }                     // nieprzeczytane wg stanu LOKALNEGO (per user)
        public int Total { get; set; }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Pozycja na liście wiadomości (lekka — bez treści, bez załączników).
    /// </summary>
    public class MailMessageModel
    {
        public uint Uid { get; set; }                       // IMAP UID (unikalny w obrębie folderu)
        public string FolderFullName { get; set; } = "";
        public string From { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string To { get; set; } = "";
        public string Subject { get; set; } = "(bez tematu)";
        public DateTime Date { get; set; }
        public string Preview { get; set; } = "";           // pierwsze znaki treści
        public bool HasAttachments { get; set; }
        public bool IsReadLocal { get; set; }               // stan PER UŻYTKOWNIK (nie serwerowy \Seen)
        public bool IsFlagged { get; set; }                 // gwiazdka (serwerowa flaga \Flagged — wspólna)
        public string MessageId { get; set; } = "";
        public string Gwiazdka => IsFlagged ? "★" : "☆";

        /// <summary>Adresy (From+To+Cc) tej wiadomości — do pasywnego zasilania książki adresowej.</summary>
        public List<MailContact> Kontakty { get; set; } = new();

        public string DataLabel => Date == default
            ? ""
            : (Date.Date == DateTime.Today ? Date.ToString("HH:mm")
               : Date.Date == DateTime.Today.AddDays(-1) ? "wczoraj"
               : Date.Year == DateTime.Today.Year ? Date.ToString("dd MMM")
               : Date.ToString("dd.MM.yyyy"));

        public string Inicjaly => MailAvatar.Inicjaly(From, FromEmail);
        public string AvatarKolor => MailAvatar.Kolor(FromEmail);

        /// <summary>Grupa nagłówkowa na liście (Dziś / Wczoraj / W tym tygodniu / Starsze).</summary>
        public string Grupa
        {
            get
            {
                if (Date == default) return "Starsze";
                var d = Date.Date; var t = DateTime.Today;
                if (d == t) return "Dziś";
                if (d == t.AddDays(-1)) return "Wczoraj";
                if (d > t.AddDays(-7)) return "W tym tygodniu";
                return "Starsze";
            }
        }
    }

    /// <summary>Inicjały + deterministyczny kolor awatara na podstawie nadawcy.</summary>
    public static class MailAvatar
    {
        private static readonly string[] Palette =
        {
            "#EF5350","#EC407A","#AB47BC","#7E57C2","#5C6BC0","#42A5F5","#29B6F6",
            "#26C6DA","#26A69A","#66BB6A","#9CCC65","#FFA726","#FF7043","#8D6E63","#78909C"
        };

        public static string Inicjaly(string? name, string? email)
        {
            var src = !string.IsNullOrWhiteSpace(name) ? name!.Trim() : (email ?? "").Trim();
            if (string.IsNullOrEmpty(src)) return "?";
            var parts = src.Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && char.IsLetter(parts[0][0]) && char.IsLetter(parts[1][0]))
                return ("" + parts[0][0] + parts[1][0]).ToUpperInvariant();
            var letters = new string(src.Where(char.IsLetterOrDigit).ToArray());
            if (letters.Length >= 2) return letters.Substring(0, 2).ToUpperInvariant();
            return letters.Length == 1 ? letters.ToUpperInvariant() : "?";
        }

        public static string Kolor(string? key)
        {
            key ??= "";
            int h = 0;
            foreach (var c in key) h = (h * 31 + c) & 0x7FFFFFFF;
            return Palette[h % Palette.Length];
        }
    }

    /// <summary>
    /// Pełna treść wiadomości (dociągana po kliknięciu).
    /// </summary>
    public class MailBodyModel
    {
        public uint Uid { get; set; }
        public string From { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string To { get; set; } = "";
        public string Cc { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTime Date { get; set; }
        public string HtmlBody { get; set; } = "";
        public string TextBody { get; set; } = "";
        public bool IsHtml => !string.IsNullOrWhiteSpace(HtmlBody);
        public List<MailAttachmentModel> Attachments { get; set; } = new();
        public string MessageId { get; set; } = "";
        public string References { get; set; } = "";
    }

    /// <summary>Pozycja książki adresowej (do podpowiedzi w polu „Do").</summary>
    public class MailContact
    {
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int UseCount { get; set; }

        /// <summary>Etykieta na liście podpowiedzi: "Jan Kowalski <jan@x.pl>" lub sam adres.</summary>
        public string Etykieta => string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Equals(Email, StringComparison.OrdinalIgnoreCase)
            ? Email
            : $"{DisplayName}  <{Email}>";

        public string Inicjaly => MailAvatar.Inicjaly(DisplayName, Email);
        public string AvatarKolor => MailAvatar.Kolor(Email);
    }

    public class MailAttachmentModel
    {
        public string FileName { get; set; } = "zalacznik";
        public string ContentType { get; set; } = "";
        public long Size { get; set; }
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public string SizeLabel => Size < 1024 ? $"{Size} B"
            : Size < 1024 * 1024 ? $"{Size / 1024.0:0.#} KB"
            : $"{Size / (1024.0 * 1024.0):0.#} MB";
    }
}
