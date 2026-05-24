// ════════════════════════════════════════════════════════════════════════════
// KontraktyService.cs — CRUD + numeracja + statusy
// Część 4 audytu (2026-05-23) — kod startowy do skopiowania do projektu
// Target lokalizacja: Kontrakty/Services/KontraktyService.cs
//
// Zależności:
//   - System.Data.SqlClient (już używane w projekcie)
//   - dbo.Kontrakty, dbo.KontraktyAudit (SQL schema z 01_Kontrakty_v1_schema.sql)
//   - dbo.sp_KontraktyNastepnyNumer (atomowa numeracja)
//
// Wzorzec: jak inne serwisy w projekcie (np. R09UDataService.cs) — async/await,
// hardcoded conn string (do wyciągnięcia w Q3 do appsettings).
// ════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Kalendarz1.Kontrakty.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Kontrakty.Services
{
    public class KontraktyService
    {
        // TODO Q3 2026: wyciągnąć do appsettings.json (jak AnalitykaConfig)
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // ────────────────────────────────────────────────────────────────────
        // 1. LISTA / FILTROWANIE
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lista wszystkich kontraktów z filtrem statusu (NULL = wszystkie).
        /// </summary>
        public async Task<List<KontraktDto>> GetAllAsync(string? statusFilter = null)
        {
            const string sql = @"
SELECT Id, NumerKontraktu, Rok, LpRoku, DostawcaId, TypKontraktu, Status,
       DataPodpisania, DataObowiazujeOd, DataObowiazujeDo, OkresWypowiedzenia,
       ProcentUbytku, TypCeny, Cena, TerminPlatnosciDni, RozliczanaWaga, MinimalnaIlosc,
       NipSnapshot, NrGospodarstwaSnapshot, NazwaHodowcySnapshot, AdresSnapshot,
       LiczySieDoArimr, PartiaPiorkowscy,
       UtworzylUserId, UtworzylKiedy, EdytowalUserId, EdytowalKiedy, PowodWypowiedzenia,
       SciezkaWord, SciezkaPdfSkan
FROM dbo.Kontrakty
WHERE (@Status IS NULL OR Status = @Status)
ORDER BY Rok DESC, LpRoku DESC;";

            var result = new List<KontraktDto>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Status", (object?)statusFilter ?? DBNull.Value);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                result.Add(Map(rdr));
            return result;
        }

        public async Task<KontraktDto?> GetByIdAsync(int id)
        {
            const string sql = @"SELECT * FROM dbo.Kontrakty WHERE Id = @Id;";
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync()) return Map(rdr);
            return null;
        }

        /// <summary>
        /// Aktywne kontrakty dla hodowcy na konkretną datę (np. dla kalendarza dostaw).
        /// </summary>
        public async Task<List<KontraktDto>> GetAktywneDlaHodowcyNaDateAsync(int dostawcaId, DateTime data)
        {
            const string sql = @"
SELECT * FROM dbo.Kontrakty
WHERE DostawcaId = @DostawcaId
  AND Status IN ('ACTIVE','EXPIRING','SIGNED')
  AND DataObowiazujeOd <= @Data
  AND (DataObowiazujeDo IS NULL OR DataObowiazujeDo >= @Data)
ORDER BY LiczySieDoArimr DESC, DataPodpisania DESC;";

            var result = new List<KontraktDto>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DostawcaId", dostawcaId);
            cmd.Parameters.AddWithValue("@Data", data);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                result.Add(Map(rdr));
            return result;
        }

        // ────────────────────────────────────────────────────────────────────
        // 2. NUMERACJA (atomowa, przez stored procedure)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generuje kolejny numer kontraktu dla danego roku (np. "47/27").
        /// Atomowo — nawet przy równoległych użytkownikach nie powstaną duplikaty.
        /// </summary>
        public async Task<(string Numer, int Lp)> GenerateNextNumberAsync(short rok)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("dbo.sp_KontraktyNastepnyNumer", conn)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Rok", rok);
            var pNumer = cmd.Parameters.Add("@NumerOut", SqlDbType.VarChar, 20);
            pNumer.Direction = ParameterDirection.Output;
            var pLp = cmd.Parameters.Add("@LpOut", SqlDbType.Int);
            pLp.Direction = ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();
            return ((string)pNumer.Value, (int)pLp.Value);
        }

        // ────────────────────────────────────────────────────────────────────
        // 3. CRUD
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tworzy nowy kontrakt. Wywołuje GenerateNextNumberAsync wewnątrz.
        /// Audyt log automatyczny.
        /// </summary>
        public async Task<int> CreateAsync(KontraktDto k, string userId)
        {
            // 1. Wygeneruj numer
            var (numer, lp) = await GenerateNextNumberAsync(k.Rok);
            k.NumerKontraktu = numer;
            k.LpRoku = lp;
            k.UtworzylUserId = userId;
            k.UtworzylKiedy = DateTime.Now;

            const string sql = @"
INSERT INTO dbo.Kontrakty (
    NumerKontraktu, Rok, LpRoku, DostawcaId, TypKontraktu, Status,
    DataPodpisania, DataObowiazujeOd, DataObowiazujeDo, OkresWypowiedzenia,
    ProcentUbytku, TypCeny, Cena, TerminPlatnosciDni, RozliczanaWaga, MinimalnaIlosc,
    NipSnapshot, NrGospodarstwaSnapshot, NazwaHodowcySnapshot, AdresSnapshot,
    LiczySieDoArimr, PartiaPiorkowscy,
    UtworzylUserId, UtworzylKiedy,
    SciezkaWord, SciezkaPdfSkan
)
OUTPUT INSERTED.Id
VALUES (
    @NumerKontraktu, @Rok, @LpRoku, @DostawcaId, @TypKontraktu, @Status,
    @DataPodpisania, @DataObowiazujeOd, @DataObowiazujeDo, @OkresWypowiedzenia,
    @ProcentUbytku, @TypCeny, @Cena, @TerminPlatnosciDni, @RozliczanaWaga, @MinimalnaIlosc,
    @NipSnapshot, @NrGospodarstwaSnapshot, @NazwaHodowcySnapshot, @AdresSnapshot,
    @LiczySieDoArimr, @PartiaPiorkowscy,
    @UtworzylUserId, @UtworzylKiedy,
    @SciezkaWord, @SciezkaPdfSkan
);";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            BindKontraktParams(cmd, k);
            var newId = (int)(await cmd.ExecuteScalarAsync())!;

            // 2. Audit log
            await AddAuditAsync(newId, userId, "CREATED", null, null, k.NumerKontraktu);
            return newId;
        }

        /// <summary>
        /// Aktualizuje kontrakt (po edycji w UI). Audyt log automatyczny.
        /// </summary>
        public async Task UpdateAsync(KontraktDto k, string userId)
        {
            // Pobierz stary stan do diff'u
            var stary = await GetByIdAsync(k.Id) ?? throw new InvalidOperationException("Kontrakt nie istnieje");

            k.EdytowalUserId = userId;
            k.EdytowalKiedy = DateTime.Now;

            const string sql = @"
UPDATE dbo.Kontrakty SET
    DataPodpisania = @DataPodpisania,
    DataObowiazujeOd = @DataObowiazujeOd,
    DataObowiazujeDo = @DataObowiazujeDo,
    Status = @Status,
    OkresWypowiedzenia = @OkresWypowiedzenia,
    ProcentUbytku = @ProcentUbytku,
    TypCeny = @TypCeny,
    Cena = @Cena,
    TerminPlatnosciDni = @TerminPlatnosciDni,
    RozliczanaWaga = @RozliczanaWaga,
    MinimalnaIlosc = @MinimalnaIlosc,
    NipSnapshot = @NipSnapshot,
    NrGospodarstwaSnapshot = @NrGospodarstwaSnapshot,
    NazwaHodowcySnapshot = @NazwaHodowcySnapshot,
    AdresSnapshot = @AdresSnapshot,
    LiczySieDoArimr = @LiczySieDoArimr,
    PartiaPiorkowscy = @PartiaPiorkowscy,
    EdytowalUserId = @EdytowalUserId,
    EdytowalKiedy = @EdytowalKiedy,
    PowodWypowiedzenia = @PowodWypowiedzenia,
    SciezkaWord = @SciezkaWord,
    SciezkaPdfSkan = @SciezkaPdfSkan
WHERE Id = @Id;";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", k.Id);
            BindKontraktParams(cmd, k);
            await cmd.ExecuteNonQueryAsync();

            // Audyt — co się zmieniło
            if (stary.Status != k.Status)
                await AddAuditAsync(k.Id, userId, "STATUS_CHANGED", "Status", stary.Status, k.Status);
            if (stary.Cena != k.Cena)
                await AddAuditAsync(k.Id, userId, "EDITED", "Cena", stary.Cena?.ToString(), k.Cena?.ToString());
            if (stary.DataObowiazujeDo != k.DataObowiazujeDo)
                await AddAuditAsync(k.Id, userId, "EDITED", "DataObowiazujeDo",
                    stary.DataObowiazujeDo?.ToString("yyyy-MM-dd"),
                    k.DataObowiazujeDo?.ToString("yyyy-MM-dd"));
        }

        /// <summary>
        /// Zmienia status (np. po wygenerowaniu Worda DRAFT → PRINTED).
        /// </summary>
        public async Task ChangeStatusAsync(int kontraktId, string nowyStatus, string userId, string? komentarz = null)
        {
            var k = await GetByIdAsync(kontraktId) ?? throw new InvalidOperationException("Kontrakt nie istnieje");
            var staryStatus = k.Status;

            const string sql = @"UPDATE dbo.Kontrakty
SET Status = @Status, EdytowalUserId = @UserId, EdytowalKiedy = GETDATE(),
    PowodWypowiedzenia = CASE WHEN @Status = 'TERMINATED' THEN @Komentarz ELSE PowodWypowiedzenia END
WHERE Id = @Id;";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", kontraktId);
            cmd.Parameters.AddWithValue("@Status", nowyStatus);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Komentarz", (object?)komentarz ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();

            await AddAuditAsync(kontraktId, userId, "STATUS_CHANGED", "Status", staryStatus, nowyStatus);
        }

        // ────────────────────────────────────────────────────────────────────
        // 4. ARiMR COMPLIANCE (z view)
        // ────────────────────────────────────────────────────────────────────

        public async Task<ArimrComplianceSnapshot?> GetArimrComplianceAsync()
        {
            const string sql = "SELECT * FROM dbo.v_ArimrCompliance;";
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            using var rdr = await cmd.ExecuteReaderAsync();
            if (!await rdr.ReadAsync()) return null;

            return new ArimrComplianceSnapshot
            {
                SurowiecCaloscKg = rdr["SurowiecCaloscKg"] as decimal? ?? 0,
                SurowiecArimrKg = rdr["SurowiecArimrKg"] as decimal? ?? 0,
                HodowcowOgolem = Convert.ToInt32(rdr["HodowcowOgolem"]),
                HodowcowArimr = Convert.ToInt32(rdr["HodowcowArimr"]),
                ProcentArimr = rdr["ProcentArimr"] as decimal? ?? 0,
                Status = rdr["Status"] as string ?? "BRAK_DANYCH",
                WyliczonoKiedy = (DateTime)rdr["WyliczonoKiedy"]
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // 5. AUDIT LOG
        // ────────────────────────────────────────────────────────────────────

        private async Task AddAuditAsync(int kontraktId, string userId, string akcja,
            string? pole, string? stara, string? nowa)
        {
            const string sql = @"
INSERT INTO dbo.KontraktyAudit (KontraktId, UserId, Akcja, PoleZmienione, StaraWartosc, NowaWartosc)
VALUES (@KontraktId, @UserId, @Akcja, @Pole, @Stara, @Nowa);";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@KontraktId", kontraktId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Akcja", akcja);
            cmd.Parameters.AddWithValue("@Pole", (object?)pole ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Stara", (object?)stara ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nowa", (object?)nowa ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        // ────────────────────────────────────────────────────────────────────
        // PRIVATE — mapping + bind
        // ────────────────────────────────────────────────────────────────────

        private static KontraktDto Map(IDataReader r) => new()
        {
            Id = (int)r["Id"],
            NumerKontraktu = (string)r["NumerKontraktu"],
            Rok = (short)r["Rok"],
            LpRoku = (int)r["LpRoku"],
            DostawcaId = (int)r["DostawcaId"],
            TypKontraktu = (string)r["TypKontraktu"],
            Status = (string)r["Status"],
            DataPodpisania = r["DataPodpisania"] as DateTime?,
            DataObowiazujeOd = (DateTime)r["DataObowiazujeOd"],
            DataObowiazujeDo = r["DataObowiazujeDo"] as DateTime?,
            OkresWypowiedzeniaDni = (int)r["OkresWypowiedzenia"],
            ProcentUbytku = (decimal)r["ProcentUbytku"],
            TypCeny = (string)r["TypCeny"],
            Cena = r["Cena"] as decimal?,
            TerminPlatnosciDni = (int)r["TerminPlatnosciDni"],
            RozliczanaWaga = (string)r["RozliczanaWaga"],
            MinimalnaIlosc = r["MinimalnaIlosc"] as int?,
            NipSnapshot = r["NipSnapshot"] as string,
            NrGospodarstwaSnapshot = r["NrGospodarstwaSnapshot"] as string,
            NazwaHodowcySnapshot = r["NazwaHodowcySnapshot"] as string,
            AdresSnapshot = r["AdresSnapshot"] as string,
            LiczySieDoArimr = (bool)r["LiczySieDoArimr"],
            PartiaPiorkowscy = r["PartiaPiorkowscy"] as string,
            UtworzylUserId = (string)r["UtworzylUserId"],
            UtworzylKiedy = (DateTime)r["UtworzylKiedy"],
            EdytowalUserId = r["EdytowalUserId"] as string,
            EdytowalKiedy = r["EdytowalKiedy"] as DateTime?,
            PowodWypowiedzenia = r["PowodWypowiedzenia"] as string,
            SciezkaWord = r["SciezkaWord"] as string,
            SciezkaPdfSkan = r["SciezkaPdfSkan"] as string,
        };

        private static void BindKontraktParams(SqlCommand cmd, KontraktDto k)
        {
            cmd.Parameters.AddWithValue("@NumerKontraktu", k.NumerKontraktu);
            cmd.Parameters.AddWithValue("@Rok", k.Rok);
            cmd.Parameters.AddWithValue("@LpRoku", k.LpRoku);
            cmd.Parameters.AddWithValue("@DostawcaId", k.DostawcaId);
            cmd.Parameters.AddWithValue("@TypKontraktu", k.TypKontraktu);
            cmd.Parameters.AddWithValue("@Status", k.Status);
            cmd.Parameters.AddWithValue("@DataPodpisania", (object?)k.DataPodpisania ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DataObowiazujeOd", k.DataObowiazujeOd);
            cmd.Parameters.AddWithValue("@DataObowiazujeDo", (object?)k.DataObowiazujeDo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@OkresWypowiedzenia", k.OkresWypowiedzeniaDni);
            cmd.Parameters.AddWithValue("@ProcentUbytku", k.ProcentUbytku);
            cmd.Parameters.AddWithValue("@TypCeny", k.TypCeny);
            cmd.Parameters.AddWithValue("@Cena", (object?)k.Cena ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TerminPlatnosciDni", k.TerminPlatnosciDni);
            cmd.Parameters.AddWithValue("@RozliczanaWaga", k.RozliczanaWaga);
            cmd.Parameters.AddWithValue("@MinimalnaIlosc", (object?)k.MinimalnaIlosc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NipSnapshot", (object?)k.NipSnapshot ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NrGospodarstwaSnapshot", (object?)k.NrGospodarstwaSnapshot ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NazwaHodowcySnapshot", (object?)k.NazwaHodowcySnapshot ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AdresSnapshot", (object?)k.AdresSnapshot ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LiczySieDoArimr", k.LiczySieDoArimr);
            cmd.Parameters.AddWithValue("@PartiaPiorkowscy", (object?)k.PartiaPiorkowscy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UtworzylUserId", k.UtworzylUserId);
            cmd.Parameters.AddWithValue("@UtworzylKiedy", k.UtworzylKiedy);
            cmd.Parameters.AddWithValue("@EdytowalUserId", (object?)k.EdytowalUserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EdytowalKiedy", (object?)k.EdytowalKiedy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PowodWypowiedzenia", (object?)k.PowodWypowiedzenia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SciezkaWord", (object?)k.SciezkaWord ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SciezkaPdfSkan", (object?)k.SciezkaPdfSkan ?? DBNull.Value);
        }
    }
}
