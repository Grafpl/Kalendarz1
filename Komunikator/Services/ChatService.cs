using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Kalendarz1.Komunikator.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Komunikator.Services
{
    /// <summary>
    /// Event args dla wskaźnika pisania
    /// </summary>
    public class TypingEventArgs : EventArgs
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public bool IsTyping { get; set; }
    }

    /// <summary>
    /// Event args dla reakcji
    /// </summary>
    public class ReactionEventArgs : EventArgs
    {
        public int MessageId { get; set; }
        public string UserId { get; set; }
        public string Emoji { get; set; }
        public bool IsAdded { get; set; }
    }

    /// <summary>
    /// Serwis do obsługi komunikatora firmowego
    /// </summary>
    public class ChatService : IDisposable
    {
        private readonly string _connectionString;
        private readonly string _currentUserId;
        private Timer _pollingTimer;
        private Timer _typingTimer;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private bool _isDisposed = false;
        private HashSet<string> _currentlyTyping = new HashSet<string>();

        public event EventHandler<List<ChatMessage>> NewMessagesReceived;
        public event EventHandler<ChatUser> UserStatusChanged;
        public event EventHandler<TypingEventArgs> UserTypingChanged;
        public event EventHandler<ReactionEventArgs> ReactionReceived;

        public ChatService(string currentUserId)
        {
            _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            _currentUserId = currentUserId;
        }

        /// <summary>
        /// Inicjalizuje tabelę wiadomości jeśli nie istnieje
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            string createTableSql = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChatMessages' AND xtype='U')
                BEGIN
                    CREATE TABLE ChatMessages (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        SenderId NVARCHAR(50) NOT NULL,
                        ReceiverId NVARCHAR(50) NOT NULL,
                        Content NVARCHAR(MAX) NOT NULL,
                        SentAt DATETIME NOT NULL DEFAULT GETDATE(),
                        ReadAt DATETIME NULL,
                        MessageType INT NOT NULL DEFAULT 0,
                        IsDeleted BIT NOT NULL DEFAULT 0
                    );
                    CREATE INDEX IX_ChatMessages_SenderId ON ChatMessages(SenderId);
                    CREATE INDEX IX_ChatMessages_ReceiverId ON ChatMessages(ReceiverId);
                    CREATE INDEX IX_ChatMessages_SentAt ON ChatMessages(SentAt);
                END;

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChatUserStatus' AND xtype='U')
                BEGIN
                    CREATE TABLE ChatUserStatus (
                        UserId NVARCHAR(50) PRIMARY KEY,
                        IsOnline BIT NOT NULL DEFAULT 0,
                        LastSeen DATETIME NULL,
                        LastActivity DATETIME NULL
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChatReactions' AND xtype='U')
                BEGIN
                    CREATE TABLE ChatReactions (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        MessageId INT NOT NULL,
                        UserId NVARCHAR(50) NOT NULL,
                        Emoji NVARCHAR(10) NOT NULL,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                        CONSTRAINT FK_ChatReactions_Message FOREIGN KEY (MessageId) REFERENCES ChatMessages(Id),
                        CONSTRAINT UQ_ChatReactions_User_Message_Emoji UNIQUE (MessageId, UserId, Emoji)
                    );
                    CREATE INDEX IX_ChatReactions_MessageId ON ChatReactions(MessageId);
                END;

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChatTypingStatus' AND xtype='U')
                BEGIN
                    CREATE TABLE ChatTypingStatus (
                        UserId NVARCHAR(50) NOT NULL,
                        TargetUserId NVARCHAR(50) NOT NULL,
                        IsTyping BIT NOT NULL DEFAULT 0,
                        LastUpdate DATETIME NOT NULL DEFAULT GETDATE(),
                        PRIMARY KEY (UserId, TargetUserId)
                    );
                END;
            ";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(createTableSql, conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ChatService InitializeDatabase error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rozpoczyna nasłuchiwanie na nowe wiadomości
        /// </summary>
        public void StartPolling(int intervalSeconds = 3)
        {
            _lastCheckTime = DateTime.Now;
            UpdateUserStatus(true);

            _pollingTimer = new Timer(intervalSeconds * 1000);
            _pollingTimer.Elapsed += async (s, e) => await CheckNewMessagesAsync();
            _pollingTimer.Start();

            // Timer dla wskaźnika pisania (częstsze sprawdzanie)
            _typingTimer = new Timer(1500);
            _typingTimer.Elapsed += async (s, e) => await CheckTypingStatusAsync();
            _typingTimer.Start();
        }

        /// <summary>
        /// Zatrzymuje nasłuchiwanie
        /// </summary>
        public void StopPolling()
        {
            _pollingTimer?.Stop();
            _pollingTimer?.Dispose();
            _pollingTimer = null;

            _typingTimer?.Stop();
            _typingTimer?.Dispose();
            _typingTimer = null;

            UpdateUserStatus(false);
        }

        /// <summary>
        /// Aktualizuje status online użytkownika
        /// </summary>
        public void UpdateUserStatus(bool isOnline)
        {
            Task.Run(async () =>
            {
                try
                {
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        string sql = @"
                            MERGE ChatUserStatus AS target
                            USING (SELECT @UserId AS UserId) AS source
                            ON target.UserId = source.UserId
                            WHEN MATCHED THEN
                                UPDATE SET IsOnline = @IsOnline, LastSeen = GETDATE(), LastActivity = GETDATE()
                            WHEN NOT MATCHED THEN
                                INSERT (UserId, IsOnline, LastSeen, LastActivity)
                                VALUES (@UserId, @IsOnline, GETDATE(), GETDATE());
                        ";
                        using (var cmd = new SqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                            cmd.Parameters.AddWithValue("@IsOnline", isOnline);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Sprawdza nowe wiadomości
        /// </summary>
        private async Task CheckNewMessagesAsync()
        {
            try
            {
                var newMessages = await GetNewMessagesAsync(_lastCheckTime);
                if (newMessages.Count > 0)
                {
                    _lastCheckTime = newMessages.Max(m => m.SentAt);
                    NewMessagesReceived?.Invoke(this, newMessages);
                }

                // Aktualizuj aktywność
                UpdateUserStatus(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckNewMessages error: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera nowe wiadomości od określonego czasu
        /// </summary>
        private async Task<List<ChatMessage>> GetNewMessagesAsync(DateTime since)
        {
            var messages = new List<ChatMessage>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        SELECT m.Id, m.SenderId, s.Name AS SenderName, m.ReceiverId, r.Name AS ReceiverName,
                               m.Content, m.SentAt, m.ReadAt, m.MessageType
                        FROM ChatMessages m
                        LEFT JOIN operators s ON m.SenderId = s.ID
                        LEFT JOIN operators r ON m.ReceiverId = r.ID
                        WHERE m.ReceiverId = @UserId
                          AND m.SentAt > @Since
                          AND m.IsDeleted = 0
                        ORDER BY m.SentAt ASC
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        cmd.Parameters.AddWithValue("@Since", since);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                messages.Add(new ChatMessage
                                {
                                    Id = reader.GetInt32(0),
                                    SenderId = reader.GetString(1),
                                    SenderName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                                    ReceiverId = reader.GetString(3),
                                    ReceiverName = reader.IsDBNull(4) ? reader.GetString(3) : reader.GetString(4),
                                    Content = reader.GetString(5),
                                    SentAt = reader.GetDateTime(6),
                                    ReadAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                                    Type = (MessageType)reader.GetInt32(8)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetNewMessages error: {ex.Message}");
            }

            return messages;
        }

        /// <summary>
        /// Wysyła wiadomość do użytkownika
        /// </summary>
        public async Task<bool> SendMessageAsync(string receiverId, string content, MessageType type = MessageType.Text)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        INSERT INTO ChatMessages (SenderId, ReceiverId, Content, SentAt, MessageType)
                        VALUES (@SenderId, @ReceiverId, @Content, GETDATE(), @MessageType)
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SenderId", _currentUserId);
                        cmd.Parameters.AddWithValue("@ReceiverId", receiverId);
                        cmd.Parameters.AddWithValue("@Content", content);
                        cmd.Parameters.AddWithValue("@MessageType", (int)type);

                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendMessage error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pobiera historię rozmowy z użytkownikiem
        /// </summary>
        public async Task<List<ChatMessage>> GetConversationAsync(string otherUserId, int limit = 100, int offset = 0)
        {
            var messages = new List<ChatMessage>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        SELECT m.Id, m.SenderId, s.Name AS SenderName, m.ReceiverId, r.Name AS ReceiverName,
                               m.Content, m.SentAt, m.ReadAt, m.MessageType
                        FROM ChatMessages m
                        LEFT JOIN operators s ON m.SenderId = s.ID
                        LEFT JOIN operators r ON m.ReceiverId = r.ID
                        WHERE m.IsDeleted = 0
                          AND ((m.SenderId = @CurrentUserId AND m.ReceiverId = @OtherUserId)
                           OR (m.SenderId = @OtherUserId AND m.ReceiverId = @CurrentUserId))
                        ORDER BY m.SentAt DESC
                        OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CurrentUserId", _currentUserId);
                        cmd.Parameters.AddWithValue("@OtherUserId", otherUserId);
                        cmd.Parameters.AddWithValue("@Limit", limit);
                        cmd.Parameters.AddWithValue("@Offset", offset);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                messages.Add(new ChatMessage
                                {
                                    Id = reader.GetInt32(0),
                                    SenderId = reader.GetString(1),
                                    SenderName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                                    ReceiverId = reader.GetString(3),
                                    ReceiverName = reader.IsDBNull(4) ? reader.GetString(3) : reader.GetString(4),
                                    Content = reader.GetString(5),
                                    SentAt = reader.GetDateTime(6),
                                    ReadAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                                    Type = (MessageType)reader.GetInt32(8)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetConversation error: {ex.Message}");
            }

            messages.Reverse(); // Odwróć kolejność aby były chronologicznie
            return messages;
        }

        /// <summary>
        /// Oznacza wiadomości jako przeczytane
        /// </summary>
        public async Task MarkMessagesAsReadAsync(string senderId)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        UPDATE ChatMessages
                        SET ReadAt = GETDATE()
                        WHERE SenderId = @SenderId
                          AND ReceiverId = @ReceiverId
                          AND ReadAt IS NULL
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@SenderId", senderId);
                        cmd.Parameters.AddWithValue("@ReceiverId", _currentUserId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MarkMessagesAsRead error: {ex.Message}");
            }
        }

        #region Reactions

        /// <summary>
        /// Dodaje reakcję do wiadomości
        /// </summary>
        public async Task<bool> AddReactionAsync(int messageId, string emoji)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        IF NOT EXISTS (SELECT 1 FROM ChatReactions WHERE MessageId = @MessageId AND UserId = @UserId AND Emoji = @Emoji)
                        BEGIN
                            INSERT INTO ChatReactions (MessageId, UserId, Emoji) VALUES (@MessageId, @UserId, @Emoji)
                        END
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@MessageId", messageId);
                        cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        cmd.Parameters.AddWithValue("@Emoji", emoji);
                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddReaction error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Usuwa reakcję z wiadomości
        /// </summary>
        public async Task<bool> RemoveReactionAsync(int messageId, string emoji)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = "DELETE FROM ChatReactions WHERE MessageId = @MessageId AND UserId = @UserId AND Emoji = @Emoji";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@MessageId", messageId);
                        cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        cmd.Parameters.AddWithValue("@Emoji", emoji);
                        await cmd.ExecuteNonQueryAsync();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveReaction error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pobiera reakcje dla wiadomości
        /// </summary>
        public async Task<List<ChatReaction>> GetReactionsAsync(int messageId)
        {
            var reactions = new List<ChatReaction>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        SELECT r.Id, r.MessageId, r.UserId, o.Name AS UserName, r.Emoji, r.CreatedAt
                        FROM ChatReactions r
                        LEFT JOIN operators o ON r.UserId = o.ID
                        WHERE r.MessageId = @MessageId
                        ORDER BY r.CreatedAt
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@MessageId", messageId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                reactions.Add(new ChatReaction
                                {
                                    Id = reader.GetInt32(0),
                                    MessageId = reader.GetInt32(1),
                                    UserId = reader.GetString(2),
                                    UserName = reader.IsDBNull(3) ? reader.GetString(2) : reader.GetString(3),
                                    Emoji = reader.GetString(4),
                                    CreatedAt = reader.GetDateTime(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetReactions error: {ex.Message}");
            }

            return reactions;
        }

        /// <summary>
        /// Pobiera reakcje dla wielu wiadomości (optymalizacja)
        /// </summary>
        public async Task<Dictionary<int, List<ChatReaction>>> GetReactionsForMessagesAsync(List<int> messageIds)
        {
            var result = new Dictionary<int, List<ChatReaction>>();
            if (messageIds == null || messageIds.Count == 0) return result;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string ids = string.Join(",", messageIds);
                    string sql = $@"
                        SELECT r.Id, r.MessageId, r.UserId, o.Name AS UserName, r.Emoji, r.CreatedAt
                        FROM ChatReactions r
                        LEFT JOIN operators o ON r.UserId = o.ID
                        WHERE r.MessageId IN ({ids})
                        ORDER BY r.MessageId, r.CreatedAt
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var reaction = new ChatReaction
                                {
                                    Id = reader.GetInt32(0),
                                    MessageId = reader.GetInt32(1),
                                    UserId = reader.GetString(2),
                                    UserName = reader.IsDBNull(3) ? reader.GetString(2) : reader.GetString(3),
                                    Emoji = reader.GetString(4),
                                    CreatedAt = reader.GetDateTime(5)
                                };

                                if (!result.ContainsKey(reaction.MessageId))
                                    result[reaction.MessageId] = new List<ChatReaction>();

                                result[reaction.MessageId].Add(reaction);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetReactionsForMessages error: {ex.Message}");
            }

            return result;
        }

        #endregion

        #region Typing Indicator

        /// <summary>
        /// Ustawia status pisania
        /// </summary>
        public async Task SetTypingStatusAsync(string targetUserId, bool isTyping)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        MERGE ChatTypingStatus AS target
                        USING (SELECT @UserId AS UserId, @TargetUserId AS TargetUserId) AS source
                        ON target.UserId = source.UserId AND target.TargetUserId = source.TargetUserId
                        WHEN MATCHED THEN
                            UPDATE SET IsTyping = @IsTyping, LastUpdate = GETDATE()
                        WHEN NOT MATCHED THEN
                            INSERT (UserId, TargetUserId, IsTyping, LastUpdate)
                            VALUES (@UserId, @TargetUserId, @IsTyping, GETDATE());
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        cmd.Parameters.AddWithValue("@TargetUserId", targetUserId);
                        cmd.Parameters.AddWithValue("@IsTyping", isTyping);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetTypingStatus error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sprawdza status pisania użytkowników
        /// </summary>
        private async Task CheckTypingStatusAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    // Pobierz użytkowników którzy piszą do nas (ostatnie 5 sekund)
                    string sql = @"
                        SELECT t.UserId, o.Name
                        FROM ChatTypingStatus t
                        LEFT JOIN operators o ON t.UserId = o.ID
                        WHERE t.TargetUserId = @CurrentUserId
                          AND t.IsTyping = 1
                          AND DATEDIFF(SECOND, t.LastUpdate, GETDATE()) < 5
                    ";

                    var typingUsers = new HashSet<string>();

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CurrentUserId", _currentUserId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var userId = reader.GetString(0);
                                var userName = reader.IsDBNull(1) ? userId : reader.GetString(1);
                                typingUsers.Add(userId);

                                // Jeśli użytkownik zaczął pisać
                                if (!_currentlyTyping.Contains(userId))
                                {
                                    UserTypingChanged?.Invoke(this, new TypingEventArgs
                                    {
                                        UserId = userId,
                                        UserName = userName,
                                        IsTyping = true
                                    });
                                }
                            }
                        }
                    }

                    // Sprawdź którzy przestali pisać
                    foreach (var userId in _currentlyTyping.ToList())
                    {
                        if (!typingUsers.Contains(userId))
                        {
                            UserTypingChanged?.Invoke(this, new TypingEventArgs
                            {
                                UserId = userId,
                                IsTyping = false
                            });
                        }
                    }

                    _currentlyTyping = typingUsers;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckTypingStatus error: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Pobiera listę wszystkich użytkowników z liczbą nieprzeczytanych wiadomości
        /// </summary>
        public async Task<List<ChatUser>> GetAllUsersAsync()
        {
            var users = new List<ChatUser>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        SELECT
                            o.ID,
                            o.Name,
                            ISNULL(s.IsOnline, 0) AS IsOnline,
                            s.LastSeen,
                            (SELECT COUNT(*) FROM ChatMessages m
                             WHERE m.SenderId = o.ID AND m.ReceiverId = @CurrentUserId AND m.ReadAt IS NULL AND m.IsDeleted = 0) AS UnreadCount,
                            (SELECT TOP 1 Content FROM ChatMessages m
                             WHERE ((m.SenderId = o.ID AND m.ReceiverId = @CurrentUserId) OR (m.SenderId = @CurrentUserId AND m.ReceiverId = o.ID))
                               AND m.IsDeleted = 0
                             ORDER BY m.SentAt DESC) AS LastMessage,
                            (SELECT TOP 1 SentAt FROM ChatMessages m
                             WHERE ((m.SenderId = o.ID AND m.ReceiverId = @CurrentUserId) OR (m.SenderId = @CurrentUserId AND m.ReceiverId = o.ID))
                               AND m.IsDeleted = 0
                             ORDER BY m.SentAt DESC) AS LastMessageTime
                        FROM operators o
                        LEFT JOIN ChatUserStatus s ON o.ID = s.UserId
                        WHERE o.ID != @CurrentUserId
                        ORDER BY
                            CASE WHEN (SELECT TOP 1 SentAt FROM ChatMessages m
                                       WHERE ((m.SenderId = o.ID AND m.ReceiverId = @CurrentUserId) OR (m.SenderId = @CurrentUserId AND m.ReceiverId = o.ID))
                                         AND m.IsDeleted = 0
                                       ORDER BY m.SentAt DESC) IS NOT NULL THEN 0 ELSE 1 END,
                            (SELECT TOP 1 SentAt FROM ChatMessages m
                             WHERE ((m.SenderId = o.ID AND m.ReceiverId = @CurrentUserId) OR (m.SenderId = @CurrentUserId AND m.ReceiverId = o.ID))
                               AND m.IsDeleted = 0
                             ORDER BY m.SentAt DESC) DESC,
                            o.Name
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CurrentUserId", _currentUserId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(new ChatUser
                                {
                                    UserId = reader.GetString(0),
                                    Name = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1),
                                    IsOnline = reader.GetBoolean(2),
                                    LastSeen = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                                    UnreadCount = reader.GetInt32(4),
                                    LastMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    LastMessageTime = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllUsers error: {ex.Message}");
            }

            return users;
        }

        /// <summary>
        /// Pobiera liczbę wszystkich nieprzeczytanych wiadomości
        /// </summary>
        public async Task<int> GetTotalUnreadCountAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        SELECT COUNT(*) FROM ChatMessages
                        WHERE ReceiverId = @UserId AND ReadAt IS NULL AND IsDeleted = 0
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _currentUserId);
                        var result = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Pobiera liczbę osób które wysłały nieprzeczytane wiadomości (statyczna metoda dla Menu)
        /// </summary>
        public static int GetUnreadSendersCount(string userId)
        {
            try
            {
                string connStr = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    string sql = @"
                        SELECT COUNT(DISTINCT SenderId) FROM ChatMessages
                        WHERE ReceiverId = @UserId AND ReadAt IS NULL AND IsDeleted = 0
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        var result = cmd.ExecuteScalar();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Wyszukuje użytkowników
        /// </summary>
        public async Task<List<ChatUser>> SearchUsersAsync(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return await GetAllUsersAsync();

            var users = new List<ChatUser>();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"
                        SELECT
                            o.ID,
                            o.Name,
                            ISNULL(s.IsOnline, 0) AS IsOnline,
                            s.LastSeen
                        FROM operators o
                        LEFT JOIN ChatUserStatus s ON o.ID = s.UserId
                        WHERE o.ID != @CurrentUserId
                          AND (o.ID LIKE @Search OR o.Name LIKE @Search)
                        ORDER BY o.Name
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CurrentUserId", _currentUserId);
                        cmd.Parameters.AddWithValue("@Search", $"%{searchText}%");

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(new ChatUser
                                {
                                    UserId = reader.GetString(0),
                                    Name = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1),
                                    IsOnline = reader.GetBoolean(2),
                                    LastSeen = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchUsers error: {ex.Message}");
            }

            return users;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopPolling();
        }
    }
}
