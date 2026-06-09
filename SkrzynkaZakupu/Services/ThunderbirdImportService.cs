using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kalendarz1.SkrzynkaZakupu.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MimeKit;

namespace Kalendarz1.SkrzynkaZakupu.Services
{
    /// <summary>
    /// Import danych z Mozilla Thunderbird (pliki mbox z lokalnych folderów POP3).
    /// Zawsze zbiera adresy do książki adresowej; opcjonalnie wgrywa stare maile na serwer IMAP.
    /// </summary>
    public class ThunderbirdImportService
    {
        private readonly MailAccountSettings _cfg;
        public ThunderbirdImportService(MailAccountSettings cfg) => _cfg = cfg;

        public class TbFolder
        {
            public string Path { get; set; } = "";
            public string Display { get; set; } = "";
            public long SizeBytes { get; set; }
            public string SizeLabel => SizeBytes < 1024 * 1024 ? $"{SizeBytes / 1024.0:0} KB" : $"{SizeBytes / (1024.0 * 1024.0):0.#} MB";
            public bool Wybrany { get; set; } = true;
            public override string ToString() => Display;
        }

        public class ImportResult
        {
            public int Folderow { get; set; }
            public int Przeskanowano { get; set; }
            public int Wgrano { get; set; }
            public int AdresowZnaleziono { get; set; }
            public int AdresowZapisano { get; set; }
            public List<string> Bledy { get; } = new();
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>Wykrywa katalogi profili Thunderbirda na tym komputerze.</summary>
        public static List<string> WykryjProfile()
        {
            var wynik = new List<string>();
            try
            {
                var root = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thunderbird", "Profiles");
                if (Directory.Exists(root))
                    wynik.AddRange(Directory.GetDirectories(root));
            }
            catch { }
            return wynik;
        }

        /// <summary>
        /// Znajduje pliki mbox (foldery pocztowe) w podanym katalogu — rekurencyjnie.
        /// Plik jest mboxem, jeśli obok istnieje plik indeksu „.msf".
        /// </summary>
        public List<TbFolder> ZnajdzFoldery(string root)
        {
            var wynik = new List<TbFolder>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return wynik;
            try
            {
                foreach (var msf in Directory.EnumerateFiles(root, "*.msf", SearchOption.AllDirectories))
                {
                    var mbox = msf.Substring(0, msf.Length - 4); // usuń ".msf"
                    if (!File.Exists(mbox)) continue;
                    var fi = new FileInfo(mbox);
                    if (fi.Length == 0) continue;
                    wynik.Add(new TbFolder
                    {
                        Path = mbox,
                        Display = LadnaSciezka(root, mbox),
                        SizeBytes = fi.Length
                    });
                }
            }
            catch { }
            return wynik.OrderByDescending(f => f.SizeBytes).ToList();
        }

        private static string LadnaSciezka(string root, string mbox)
        {
            var rel = mbox.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? mbox.Substring(root.Length).TrimStart('\\', '/')
                : System.IO.Path.GetFileName(mbox);
            // "Mail\pop.x.pl\Local Folders.sbd\Archiwum" -> "pop.x.pl / Local Folders / Archiwum"
            rel = rel.Replace(".sbd", "");
            var czesci = rel.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(p => !p.Equals("Mail", StringComparison.OrdinalIgnoreCase)
                                     && !p.Equals("ImapMail", StringComparison.OrdinalIgnoreCase));
            return string.Join(" / ", czesci);
        }

        // ─────────────────────────────────────────────────────────────────
        public async Task<ImportResult> ImportujAsync(
            IEnumerable<TbFolder> foldery,
            bool wgrajNaSerwer,
            string folderDocelowy,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            var res = new ImportResult();
            var kontakty = new Dictionary<string, MailContact>(StringComparer.OrdinalIgnoreCase);

            ImapClient? client = null;
            IMailFolder? cel = null;
            var istniejaceId = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (wgrajNaSerwer)
                {
                    progress?.Report("Łączę z serwerem…");
                    client = await PolaczAsync(ct);
                    cel = await ZapewnijFolderAsync(client, folderDocelowy, ct);
                    await cel.OpenAsync(FolderAccess.ReadWrite, ct);
                    if (cel.Count > 0)
                    {
                        progress?.Report("Czytam istniejące wiadomości (dedup)…");
                        var sums = await cel.FetchAsync(0, cel.Count - 1, MessageSummaryItems.Envelope, ct);
                        foreach (var s in sums)
                            if (!string.IsNullOrEmpty(s.Envelope?.MessageId))
                                istniejaceId.Add(s.Envelope.MessageId);
                    }
                }

                foreach (var f in foldery)
                {
                    ct.ThrowIfCancellationRequested();
                    res.Folderow++;
                    progress?.Report($"Folder: {f.Display}…");
                    try
                    {
                        using var stream = File.OpenRead(f.Path);
                        var parser = new MimeParser(stream, MimeFormat.Mbox);
                        int wFolderze = 0;
                        while (!parser.IsEndOfStream)
                        {
                            ct.ThrowIfCancellationRequested();
                            MimeMessage msg;
                            try { msg = parser.ParseMessage(ct); }
                            catch { break; } // uszkodzony mbox — przerwij ten plik

                            res.Przeskanowano++;
                            wFolderze++;
                            ZbierzAdresy(kontakty, msg);

                            if (wgrajNaSerwer && cel != null)
                            {
                                var id = msg.MessageId ?? "";
                                if (string.IsNullOrEmpty(id) || !istniejaceId.Contains(id))
                                {
                                    try
                                    {
                                        await cel.AppendAsync(msg, MessageFlags.Seen, ct);
                                        if (!string.IsNullOrEmpty(id)) istniejaceId.Add(id);
                                        res.Wgrano++;
                                    }
                                    catch (Exception ex) { res.Bledy.Add($"{f.Display}: {ex.Message}"); }
                                }
                            }

                            if (wFolderze % 50 == 0)
                                progress?.Report($"{f.Display}: {wFolderze} wiadomości…");
                        }
                    }
                    catch (Exception ex) { res.Bledy.Add($"{f.Display}: {ex.Message}"); }
                }
            }
            finally
            {
                if (client != null) { try { await client.DisconnectAsync(true, ct); } catch { } client.Dispose(); }
            }

            // zapis adresów do wspólnej książki
            res.AdresowZnaleziono = kontakty.Count;
            progress?.Report($"Zapisuję {kontakty.Count} adresów…");
            res.AdresowZapisano = await new MailContactsService().UpsertManyAsync(kontakty.Values, "thunderbird");

            return res;
        }

        private static void ZbierzAdresy(Dictionary<string, MailContact> mapa, MimeMessage msg)
        {
            void Add(InternetAddressList? lista)
            {
                if (lista == null) return;
                foreach (var mb in lista.Mailboxes)
                {
                    var email = (mb.Address ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(email) || email.IndexOf('@') <= 0) continue;
                    if (!mapa.TryGetValue(email, out var ex))
                        mapa[email] = new MailContact { Email = email, DisplayName = mb.Name ?? "" };
                    else if (string.IsNullOrWhiteSpace(ex.DisplayName) && !string.IsNullOrWhiteSpace(mb.Name))
                        ex.DisplayName = mb.Name;
                }
            }
            Add(msg.From); Add(msg.To); Add(msg.Cc); Add(msg.ReplyTo);
        }

        private async Task<ImapClient> PolaczAsync(CancellationToken ct)
        {
            var client = new ImapClient();
            var secure = _cfg.ImapPort == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_cfg.ImapHost, _cfg.ImapPort, secure, ct);
            await client.AuthenticateAsync(_cfg.Login, _cfg.Password, ct);
            return client;
        }

        private static async Task<IMailFolder> ZapewnijFolderAsync(ImapClient client, string nazwa, CancellationToken ct)
        {
            try { return await client.GetFolderAsync(nazwa, ct); }
            catch
            {
                var top = client.GetFolder(client.PersonalNamespaces[0]);
                return await top.CreateAsync(nazwa, true, ct);
            }
        }
    }
}
