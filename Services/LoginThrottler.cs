using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace Kalendarz1.Services
{
    /// <summary>
    /// Throttling logowania + audit log.
    /// Po 5 błędnych próbach → konto zablokowane na 15 minut.
    /// Każda próba (sukces/błąd) zapisana w LoginAttempts (audit log BRC/IFS).
    /// </summary>
    public static class LoginThrottler
    {
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 15;

        private const string ConnectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        /// <summary>Czy konto jest zablokowane? Zwraca minuty pozostałe do odblokowania.</summary>
        public static async Task<(bool locked, int minutesLeft)> IsLockedAsync(string userId)
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT LockedUntil FROM dbo.operators WHERE ID = @id", conn);
                cmd.Parameters.AddWithValue("@id", userId);
                var result = await cmd.ExecuteScalarAsync();
                if (result is DateTime lockedUntil && lockedUntil > DateTime.Now)
                {
                    var minLeft = (int)Math.Ceiling((lockedUntil - DateTime.Now).TotalMinutes);
                    return (true, Math.Max(1, minLeft));
                }
                return (false, 0);
            }
            catch
            {
                // Jeśli baza nie odpowiada — nie blokuj logowania (fail-open)
                return (false, 0);
            }
        }

        /// <summary>Zapisz nieudaną próbę logowania. Po MaxFailedAttempts → lockout.</summary>
        public static async Task RecordFailureAsync(string userId, string reason)
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                // 1. Inkrementuj licznik; jeśli >= max → zablokuj
                //    UWAGA: tylko jeśli user istnieje (inaczej UPDATE 0 rows = OK)
                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.operators
                    SET FailedAttempts = FailedAttempts + 1,
                        LockedUntil = CASE
                            WHEN FailedAttempts + 1 >= @max
                            THEN DATEADD(MINUTE, @lock, GETDATE())
                            ELSE LockedUntil
                        END
                    WHERE ID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.Parameters.AddWithValue("@max", MaxFailedAttempts);
                    cmd.Parameters.AddWithValue("@lock", LockoutMinutes);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 2. Audit log
                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.LoginAttempts (UserId, Success, MachineName, FailureReason)
                    VALUES (@id, 0, @machine, @reason);", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId ?? "(empty)");
                    cmd.Parameters.AddWithValue("@machine", Environment.MachineName);
                    cmd.Parameters.AddWithValue("@reason", (object?)reason ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoginThrottler.RecordFailureAsync error: {ex.Message}");
            }
        }

        /// <summary>Zapisz udaną próbę logowania. Wyzeruj licznik błędów.</summary>
        public static async Task RecordSuccessAsync(string userId)
        {
            try
            {
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    UPDATE dbo.operators
                    SET FailedAttempts = 0,
                        LockedUntil = NULL,
                        LastSuccessfulLogin = GETDATE()
                    WHERE ID = @id;", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.LoginAttempts (UserId, Success, MachineName)
                    VALUES (@id, 1, @machine);", conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.Parameters.AddWithValue("@machine", Environment.MachineName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoginThrottler.RecordSuccessAsync error: {ex.Message}");
            }
        }
    }
}
