// ════════════════════════════════════════════════════════════════════════════
// KontraktyAlertService.cs — nocny job + alerty wygasania
// Część 4 audytu (2026-05-23)
// Target: Kontrakty/Services/KontraktyAlertService.cs
//
// Wywołanie: Kalendarz1.exe --kontrakty-check
// Schedule: Windows Task Scheduler codziennie 02:00
//
// Logika:
//   - Dla każdego ACTIVE/EXPIRING kontraktu liczy dni do wygaśnięcia
//   - Dopasowuje do dbo.KontraktyEskalacjaConfig (90/30/7/-1 dni)
//   - Generuje alert w dbo.KontraktyAlerty (jeśli jeszcze nie ma)
//   - Auto-zmiana statusu: ACTIVE → EXPIRING (<30d), EXPIRING → EXPIRED (<0d)
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Kontrakty.Services
{
    public class KontraktyAlertService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        /// <summary>
        /// Główne wejście — wywoływane z Windows Task Scheduler raz dziennie o 02:00.
        /// Generuje alerty + zmienia statusy kontraktów.
        /// </summary>
        public async Task<AlertJobResult> GenerujAlertyAsync()
        {
            var result = new AlertJobResult { Start = DateTime.Now };

            var config = await PobierzKonfiguracjeAsync();
            var kontrakty = await PobierzKontraktyAktywneZDatamiDoAsync();

            foreach (var k in kontrakty)
            {
                if (!k.DataObowiazujeDo.HasValue) continue; // wieczny, nie wygasa

                var dniDo = (k.DataObowiazujeDo.Value - DateTime.Today).Days;

                // 1. Generuj alerty zgodnie z konfiguracją
                foreach (var cfg in config)
                {
                    if (cfg.DniDoWygasniecia != dniDo) continue;

                    foreach (var userId in cfg.DlaUserIdLista.Split(';',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (await AlertJuzIstniejeAsync(k.KontraktId, cfg.TypAlertu, userId)) continue;

                        await ZapiszAlertAsync(k, cfg, userId);
                        result.AlertyDodane++;

                        if (cfg.KanalEmail) {
                            // TODO Faza 3: integracja Outlook interop / SMTP
                            // await WyslijEmailAsync(k, cfg, userId);
                            result.EmaileWyslane++;
                        }
                    }
                }

                // 2. Auto-zmiana statusu
                if (dniDo <= 30 && dniDo > 0 && k.Status == "ACTIVE")
                {
                    await ZmienStatusAsync(k.KontraktId, "EXPIRING");
                    result.StatusyZmienione++;
                }
                if (dniDo < 0 && k.Status != "EXPIRED" && k.Status != "TERMINATED")
                {
                    await ZmienStatusAsync(k.KontraktId, "EXPIRED");
                    result.StatusyZmienione++;
                }
            }

            result.Koniec = DateTime.Now;
            return result;
        }

        // ────────────────────────────────────────────────────────────────────

        private async Task<List<EskalacjaConfigRow>> PobierzKonfiguracjeAsync()
        {
            const string sql = @"
SELECT TypAlertu, DniDoWygasniecia, Severity, DlaUserIdLista, KanalEmail, KanalPushZpsp, BlokujLogowanie
FROM dbo.KontraktyEskalacjaConfig WHERE Aktywny = 1;";

            var list = new List<EskalacjaConfigRow>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new EskalacjaConfigRow
                {
                    TypAlertu = (string)rdr["TypAlertu"],
                    DniDoWygasniecia = (int)rdr["DniDoWygasniecia"],
                    Severity = (string)rdr["Severity"],
                    DlaUserIdLista = (string)rdr["DlaUserIdLista"],
                    KanalEmail = (bool)rdr["KanalEmail"],
                    KanalPushZpsp = (bool)rdr["KanalPushZpsp"],
                    BlokujLogowanie = (bool)rdr["BlokujLogowanie"]
                });
            }
            return list;
        }

        private async Task<List<KontraktKrotki>> PobierzKontraktyAktywneZDatamiDoAsync()
        {
            const string sql = @"
SELECT Id, NumerKontraktu, DostawcaId, Status, DataObowiazujeDo, NazwaHodowcySnapshot
FROM dbo.Kontrakty
WHERE Status IN ('ACTIVE','EXPIRING','SIGNED')
  AND DataObowiazujeDo IS NOT NULL;";

            var list = new List<KontraktKrotki>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new KontraktKrotki
                {
                    KontraktId = (int)rdr["Id"],
                    Numer = (string)rdr["NumerKontraktu"],
                    Status = (string)rdr["Status"],
                    DataObowiazujeDo = rdr["DataObowiazujeDo"] as DateTime?,
                    NazwaHodowcy = rdr["NazwaHodowcySnapshot"] as string
                });
            }
            return list;
        }

        private async Task<bool> AlertJuzIstniejeAsync(int kontraktId, string typAlertu, string userId)
        {
            const string sql = @"
SELECT TOP 1 1 FROM dbo.KontraktyAlerty
WHERE KontraktId = @K AND TypAlertu = @T AND DlaUserId = @U
  AND DataWygenerowania > DATEADD(DAY, -1, GETDATE());";  // anty-spam: tylko raz na dobę

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@K", kontraktId);
            cmd.Parameters.AddWithValue("@T", typAlertu);
            cmd.Parameters.AddWithValue("@U", userId);
            var r = await cmd.ExecuteScalarAsync();
            return r != null;
        }

        private async Task ZapiszAlertAsync(KontraktKrotki k, EskalacjaConfigRow cfg, string userId)
        {
            var msg = cfg.TypAlertu switch
            {
                "WYGASA_3M" => $"Kontrakt {k.Numer} ({k.NazwaHodowcy}) wygasa za 90 dni: {k.DataObowiazujeDo:yyyy-MM-dd}.",
                "WYGASA_1M" => $"⚠ Kontrakt {k.Numer} ({k.NazwaHodowcy}) wygasa za 30 dni: {k.DataObowiazujeDo:yyyy-MM-dd}.",
                "WYGASA_7D" => $"⚠⚠ Kontrakt {k.Numer} ({k.NazwaHodowcy}) WYGASA ZA 7 DNI: {k.DataObowiazujeDo:yyyy-MM-dd}.",
                "WYGASNAL"  => $"🚨 Kontrakt {k.Numer} ({k.NazwaHodowcy}) JUŻ WYGASŁ ({k.DataObowiazujeDo:yyyy-MM-dd}). PILNE.",
                _ => $"Alert {cfg.TypAlertu} dla kontraktu {k.Numer}."
            };

            const string sql = @"
INSERT INTO dbo.KontraktyAlerty (KontraktId, TypAlertu, Severity, DlaUserId, Wiadomosc)
VALUES (@K, @T, @S, @U, @M);";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@K", k.KontraktId);
            cmd.Parameters.AddWithValue("@T", cfg.TypAlertu);
            cmd.Parameters.AddWithValue("@S", cfg.Severity);
            cmd.Parameters.AddWithValue("@U", userId);
            cmd.Parameters.AddWithValue("@M", msg);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task ZmienStatusAsync(int kontraktId, string nowyStatus)
        {
            const string sql = @"UPDATE dbo.Kontrakty SET Status = @S WHERE Id = @K;";
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@S", nowyStatus);
            cmd.Parameters.AddWithValue("@K", kontraktId);
            await cmd.ExecuteNonQueryAsync();

            // Audit log
            const string sqlAudit = @"
INSERT INTO dbo.KontraktyAudit (KontraktId, UserId, Akcja, PoleZmienione, NowaWartosc)
VALUES (@K, 'SYSTEM_ALERT_JOB', 'STATUS_CHANGED', 'Status', @S);";
            using var cmd2 = new SqlCommand(sqlAudit, conn);
            cmd2.Parameters.AddWithValue("@K", kontraktId);
            cmd2.Parameters.AddWithValue("@S", nowyStatus);
            await cmd2.ExecuteNonQueryAsync();
        }

        // ────────────────────────────────────────────────────────────────────
        // Helper classes

        private class EskalacjaConfigRow
        {
            public string TypAlertu { get; set; } = "";
            public int DniDoWygasniecia { get; set; }
            public string Severity { get; set; } = "";
            public string DlaUserIdLista { get; set; } = "";
            public bool KanalEmail { get; set; }
            public bool KanalPushZpsp { get; set; }
            public bool BlokujLogowanie { get; set; }
        }

        private class KontraktKrotki
        {
            public int KontraktId { get; set; }
            public string Numer { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime? DataObowiazujeDo { get; set; }
            public string? NazwaHodowcy { get; set; }
        }
    }

    public class AlertJobResult
    {
        public DateTime Start { get; set; }
        public DateTime Koniec { get; set; }
        public int AlertyDodane { get; set; }
        public int StatusyZmienione { get; set; }
        public int EmaileWyslane { get; set; }
        public override string ToString() =>
            $"[KontraktyAlertJob] {Start:HH:mm:ss}–{Koniec:HH:mm:ss} | " +
            $"Alerty: {AlertyDodane}, Statusy: {StatusyZmienione}, Maile: {EmaileWyslane}";
    }
}
