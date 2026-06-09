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
    /// Czytanie poczty przez IMAP (MailKit) ze skrzynki dpoczta.
    /// TRWAŁE połączenie (jak w najlepszych klientach) — jeden ImapClient utrzymywany między
    /// operacjami, serializowany semaforem, z auto-reconnectem. Treści maili cache'owane.
    /// NIE dotyka serwerowej flagi \Seen — przeczytane liczymy per-user lokalnie.
    /// </summary>
    public class ImapMailService
    {
        private readonly MailAccountSettings _cfg;
        private ImapClient? _client;
        private readonly SemaphoreSlim _gate = new(1, 1);

        // cache treści: klucz "folder|uid" -> body
        private readonly Dictionary<string, MailBodyModel> _bodyCache = new();
        private readonly Queue<string> _bodyOrder = new();
        private const int BodyCacheMax = 60;

        public ImapMailService(MailAccountSettings cfg) => _cfg = cfg;

        // ───────────────────────── połączenie ─────────────────────────
        private async Task EnsureAsync(CancellationToken ct)
        {
            if (_client != null && _client.IsConnected && _client.IsAuthenticated) return;
            ResetClient();
            var client = new ImapClient();
            var secure = _cfg.ImapPort == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_cfg.ImapHost, _cfg.ImapPort, secure, ct);
            await client.AuthenticateAsync(_cfg.Login, _cfg.Password, ct);
            _client = client;
        }

        private void ResetClient()
        {
            if (_client != null) { try { _client.Dispose(); } catch { } _client = null; }
        }

        private static bool JestBledemPolaczenia(Exception ex) =>
            ex is IOException
            or System.Net.Sockets.SocketException
            or ServiceNotConnectedException
            or ServiceNotAuthenticatedException
            or ImapProtocolException
            or ProtocolException;

        /// <summary>Wykonuje operację na trwałym połączeniu; przy zerwaniu — reconnect i jedna próba ponowna.</summary>
        private async Task<T> RunAsync<T>(Func<ImapClient, CancellationToken, Task<T>> work, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                for (int proba = 0; proba < 2; proba++)
                {
                    try
                    {
                        await EnsureAsync(ct);
                        return await work(_client!, ct);
                    }
                    catch (Exception ex) when (proba == 0 && JestBledemPolaczenia(ex))
                    {
                        ResetClient();
                    }
                }
                throw new InvalidOperationException("Nie udało się wykonać operacji IMAP.");
            }
            finally { _gate.Release(); }
        }

        /// <summary>Rozłącza i zwalnia połączenie (wołać przy zamknięciu okna).</summary>
        public async Task DisconnectQuietAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (_client != null)
                {
                    try { if (_client.IsConnected) await _client.DisconnectAsync(true); } catch { }
                    ResetClient();
                }
            }
            finally { _gate.Release(); }
        }

        // ───────────────────────── API ─────────────────────────
        public async Task<(bool ok, string error)> TestAsync(CancellationToken ct = default)
        {
            try
            {
                await RunAsync<bool>((_, _) => Task.FromResult(true), ct);
                return (true, "");
            }
            catch (Exception ex) { return (false, ex.Message); }
            finally { await DisconnectQuietAsync(); }
        }

        public Task<List<MailFolderModel>> GetFoldersAsync(CancellationToken ct = default)
            => RunAsync(async (client, c) =>
            {
                var result = new List<MailFolderModel>();
                var personal = client.GetFolder(client.PersonalNamespaces[0]);
                var inbox = client.Inbox;
                foreach (var f in OrderFolders(inbox, personal.GetSubfolders(false, c).ToList()))
                {
                    try
                    {
                        // STATUS = liczniki bez SELECT (szybciej niż otwieranie folderu)
                        f.Status(StatusItems.Count | StatusItems.Unread, c);
                        result.Add(new MailFolderModel
                        {
                            FullName = f.FullName,
                            DisplayName = LadnaNazwa(f),
                            Ikona = Ikona(f),
                            Total = f.Count,
                            Unread = f.Unread
                        });
                    }
                    catch { /* folder kontenerowy bez SELECT — pomijamy */ }
                }
                return result;
            }, ct);

        private static IEnumerable<IMailFolder> OrderFolders(IMailFolder inbox, List<IMailFolder> rest)
        {
            yield return inbox;
            foreach (var f in rest.Where(x => !x.FullName.Equals(inbox.FullName, StringComparison.OrdinalIgnoreCase)))
                yield return f;
        }

        public Task<List<MailMessageModel>> GetMessagesAsync(string folderFullName, int limit = 80, CancellationToken ct = default)
            => RunAsync(async (client, c) =>
            {
                var list = new List<MailMessageModel>();
                var folder = await client.GetFolderAsync(folderFullName, c);
                await folder.OpenAsync(FolderAccess.ReadOnly, c);

                int count = folder.Count;
                if (count == 0) return list;

                int start = Math.Max(0, count - limit);
                var summaries = await folder.FetchAsync(start, count - 1,
                    MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags |
                    MessageSummaryItems.BodyStructure | MessageSummaryItems.PreviewText, c);

                foreach (var s in summaries)
                {
                    var env = s.Envelope;
                    var from = env?.From?.Mailboxes?.FirstOrDefault();
                    var msg = new MailMessageModel
                    {
                        Uid = s.UniqueId.Id,
                        FolderFullName = folderFullName,
                        From = from?.Name ?? from?.Address ?? "(nieznany)",
                        FromEmail = from?.Address ?? "",
                        To = string.Join(", ", env?.To?.Mailboxes?.Select(m => m.Name ?? m.Address) ?? Enumerable.Empty<string>()),
                        Subject = string.IsNullOrWhiteSpace(env?.Subject) ? "(bez tematu)" : env.Subject,
                        Date = env?.Date?.LocalDateTime ?? DateTime.MinValue,
                        Preview = s.PreviewText ?? "",
                        HasAttachments = s.Attachments?.Any() == true,
                        IsFlagged = s.Flags?.HasFlag(MessageFlags.Flagged) == true,
                        MessageId = env?.MessageId ?? ""
                    };
                    DodajKontakty(msg.Kontakty, env?.From?.Mailboxes);
                    DodajKontakty(msg.Kontakty, env?.To?.Mailboxes);
                    DodajKontakty(msg.Kontakty, env?.Cc?.Mailboxes);
                    list.Add(msg);
                }
                return list.OrderByDescending(m => m.Date).ToList();
            }, ct);

        public async Task<MailBodyModel?> GetBodyAsync(string folderFullName, uint uid, CancellationToken ct = default)
        {
            string key = folderFullName + "|" + uid;
            lock (_bodyCache)
                if (_bodyCache.TryGetValue(key, out var cached)) return cached;

            var body = await RunAsync(async (client, c) =>
            {
                var folder = await client.GetFolderAsync(folderFullName, c);
                await folder.OpenAsync(FolderAccess.ReadOnly, c);
                var message = await folder.GetMessageAsync(new UniqueId(uid), c);
                if (message == null) return null;

                var b = new MailBodyModel
                {
                    Uid = uid,
                    From = message.From?.Mailboxes?.FirstOrDefault()?.Name
                           ?? message.From?.Mailboxes?.FirstOrDefault()?.Address ?? "",
                    FromEmail = message.From?.Mailboxes?.FirstOrDefault()?.Address ?? "",
                    To = string.Join(", ", message.To?.Mailboxes?.Select(m => m.Address) ?? Enumerable.Empty<string>()),
                    Cc = string.Join(", ", message.Cc?.Mailboxes?.Select(m => m.Address) ?? Enumerable.Empty<string>()),
                    Subject = message.Subject ?? "(bez tematu)",
                    Date = message.Date.LocalDateTime,
                    HtmlBody = message.HtmlBody ?? "",
                    TextBody = message.TextBody ?? "",
                    MessageId = message.MessageId ?? "",
                    References = message.MessageId ?? ""
                };

                foreach (var att in message.Attachments)
                {
                    using var ms = new MemoryStream();
                    string fileName = "zalacznik";
                    if (att is MimePart part)
                    {
                        fileName = part.FileName ?? "zalacznik";
                        await part.Content.DecodeToAsync(ms, c);
                    }
                    else if (att is MessagePart mp)
                    {
                        fileName = att.ContentDisposition?.FileName ?? "wiadomosc.eml";
                        await mp.Message.WriteToAsync(ms, c);
                    }
                    b.Attachments.Add(new MailAttachmentModel
                    {
                        FileName = fileName,
                        ContentType = att.ContentType?.MimeType ?? "application/octet-stream",
                        Size = ms.Length,
                        Content = ms.ToArray()
                    });
                }
                return (MailBodyModel?)b;
            }, ct);

            if (body != null) DodajDoCache(key, body);
            return body;
        }

        private void DodajDoCache(string key, MailBodyModel body)
        {
            lock (_bodyCache)
            {
                if (_bodyCache.ContainsKey(key)) return;
                _bodyCache[key] = body;
                _bodyOrder.Enqueue(key);
                while (_bodyOrder.Count > BodyCacheMax)
                {
                    var stary = _bodyOrder.Dequeue();
                    _bodyCache.Remove(stary);
                }
            }
        }

        private void UsunZCache(string folderFullName, uint uid)
        {
            lock (_bodyCache) _bodyCache.Remove(folderFullName + "|" + uid);
        }

        public async Task<List<MailContact>> GetAllAddressesAsync(IProgress<string>? progress = null, CancellationToken ct = default)
            => await RunAsync(async (client, c) =>
            {
                var mapa = new Dictionary<string, MailContact>(StringComparer.OrdinalIgnoreCase);
                var personal = client.GetFolder(client.PersonalNamespaces[0]);
                var foldery = new List<IMailFolder> { client.Inbox };
                foreach (var f in personal.GetSubfolders(false, c))
                    if (!f.FullName.Equals(client.Inbox.FullName, StringComparison.OrdinalIgnoreCase))
                        foldery.Add(f);

                foreach (var folder in foldery)
                {
                    c.ThrowIfCancellationRequested();
                    try
                    {
                        folder.Open(FolderAccess.ReadOnly, c);
                        if (folder.Count > 0)
                        {
                            progress?.Report($"Skanuję: {folder.Name} ({folder.Count})…");
                            var summaries = await folder.FetchAsync(0, folder.Count - 1, MessageSummaryItems.Envelope, c);
                            foreach (var s in summaries)
                            {
                                DodajZ(mapa, s.Envelope?.From?.Mailboxes);
                                DodajZ(mapa, s.Envelope?.To?.Mailboxes);
                                DodajZ(mapa, s.Envelope?.Cc?.Mailboxes);
                            }
                        }
                    }
                    catch { /* folder bez SELECT — pomijamy */ }
                }
                return mapa.Values.ToList();
            }, ct);

        private static void DodajKontakty(List<MailContact> cel, IEnumerable<MailboxAddress>? adresy)
        {
            if (adresy == null) return;
            foreach (var a in adresy)
            {
                var email = (a.Address ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(email) || email.IndexOf('@') <= 0) continue;
                cel.Add(new MailContact { Email = email, DisplayName = a.Name ?? "" });
            }
        }

        private static void DodajZ(Dictionary<string, MailContact> mapa, IEnumerable<MailboxAddress>? adresy)
        {
            if (adresy == null) return;
            foreach (var a in adresy)
            {
                var email = (a.Address ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(email) || email.IndexOf('@') <= 0) continue;
                if (!mapa.TryGetValue(email, out var ex))
                    mapa[email] = new MailContact { Email = email, DisplayName = a.Name ?? "" };
                else if (string.IsNullOrWhiteSpace(ex.DisplayName) && !string.IsNullOrWhiteSpace(a.Name))
                    ex.DisplayName = a.Name;
            }
        }

        /// <summary>Przenosi wiadomość; zwraca nowy UID w folderze docelowym (jeśli serwer wspiera UIDPLUS).</summary>
        public async Task<uint?> MoveAsync(string fromFolder, uint uid, string toFolder, CancellationToken ct = default)
        {
            var nowy = await RunAsync(async (client, c) =>
            {
                var src = await client.GetFolderAsync(fromFolder, c);
                var dst = await client.GetFolderAsync(toFolder, c);
                await src.OpenAsync(FolderAccess.ReadWrite, c);
                var u = await src.MoveToAsync(new UniqueId(uid), dst, c);
                return u?.Id;
            }, ct);
            UsunZCache(fromFolder, uid);
            return nowy;
        }

        /// <summary>Usuwa = przenosi do Kosza. Zwraca (folder kosza, nowy UID) do ewentualnego „Cofnij".</summary>
        public async Task<(string trashFolder, uint? newUid)> DeleteToTrashAsync(string fromFolder, uint uid, CancellationToken ct = default)
        {
            var wynik = await RunAsync(async (client, c) =>
            {
                var trash = client.GetFolder(SpecialFolder.Trash);
                var src = await client.GetFolderAsync(fromFolder, c);
                await src.OpenAsync(FolderAccess.ReadWrite, c);
                if (trash != null && !src.FullName.Equals(trash.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    var u = await src.MoveToAsync(new UniqueId(uid), trash, c);
                    return (trash.FullName, (uint?)u?.Id);
                }
                await src.AddFlagsAsync(new UniqueId(uid), MessageFlags.Deleted, true, c);
                await src.ExpungeAsync(c);
                return ("", (uint?)null);
            }, ct);
            UsunZCache(fromFolder, uid);
            return wynik;
        }

        /// <summary>Ustawia/zdejmuje gwiazdkę (serwerowa flaga \Flagged).</summary>
        public Task SetFlaggedAsync(string folder, uint uid, bool flagged, CancellationToken ct = default)
            => RunAsync(async (client, c) =>
            {
                var f = await client.GetFolderAsync(folder, c);
                await f.OpenAsync(FolderAccess.ReadWrite, c);
                if (flagged) await f.AddFlagsAsync(new UniqueId(uid), MessageFlags.Flagged, true, c);
                else await f.RemoveFlagsAsync(new UniqueId(uid), MessageFlags.Flagged, true, c);
                return true;
            }, ct);

        // ───────────────────────── helpers ─────────────────────────
        private static string LadnaNazwa(IMailFolder f)
        {
            var n = f.Name;
            return n.ToUpperInvariant() switch { "INBOX" => "Odebrane", _ => n };
        }

        private static string Ikona(IMailFolder f)
        {
            if ((f.Attributes & FolderAttributes.Inbox) != 0) return "📥";
            if ((f.Attributes & FolderAttributes.Sent) != 0) return "📤";
            if ((f.Attributes & FolderAttributes.Trash) != 0) return "🗑️";
            if ((f.Attributes & FolderAttributes.Drafts) != 0) return "📝";
            if ((f.Attributes & FolderAttributes.Junk) != 0) return "⚠️";
            return "📁";
        }
    }
}
