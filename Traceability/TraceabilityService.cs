using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.Traceability
{
    /// <summary>
    /// Traceability (#3) — rejestracja palet wyrobu, reverse trace (lot→hodowca), recall.
    /// Baza: LibraNet. Wymaga Traceability/SQL/CreateTraceability.sql.
    /// Drukarka etykiet (lot+QR) — osobny krok (po zakupie sprzętu).
    /// </summary>
    public class TraceabilityService
    {
        private readonly string _conn;

        public TraceabilityService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _conn = AnalitykaConfig.ConnLibraNet;
        }

        // ════════════════════════════════════════════════════════════════
        // REVERSE TRACE NA ISTNIEJĄCYCH DANYCH (bez ręcznej rejestracji palet)
        // Źródła: PartiaDostawca (hodowca), In0E (przyjęcie surowca),
        //         Haccp (przepływy między działami), Out1A (krojenie/wyjście).
        // ════════════════════════════════════════════════════════════════
        public async Task<TraceExistingResult> ReverseTraceExistingAsync(string partia)
        {
            var res = new TraceExistingResult { Partia = partia };
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            // 1) Hodowca z PartiaDostawca
            try
            {
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 CustomerID, CustomerName FROM dbo.PartiaDostawca WHERE Partia = @P", cn)
                { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@P", partia);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    res.CustomerID = r["CustomerID"]?.ToString()?.Trim();
                    res.Hodowca = r["CustomerName"]?.ToString()?.Trim();
                }
            }
            catch { /* PartiaDostawca zawsze jest, ale defensywnie */ }

            // 2) Przyjęcie surowca z In0E (per towar)
            try
            {
                const string sql = @"
SELECT ArticleName, MAX(JM) AS JM,
       SUM(ISNULL(ActWeight,0)) AS Kg, SUM(ISNULL(Quantity,0)) AS Szt,
       MIN(Data) AS DataMin
FROM dbo.In0E WHERE P1 = @P
GROUP BY ArticleName
ORDER BY SUM(ISNULL(ActWeight,0)) DESC;";
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@P", partia);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    res.Przyjecia.Add(new TracePrzyjecie
                    {
                        Towar = r["ArticleName"]?.ToString()?.Trim() ?? "",
                        Jm = r["JM"]?.ToString()?.Trim(),
                        Kg = ToDec(r["Kg"]),
                        Szt = ToDec(r["Szt"]),
                        Data = FmtData(r["DataMin"])
                    });
            }
            catch { }

            // 3) Przepływy między działami z Haccp
            try
            {
                const string sql = @"
SELECT Dir_ID1, Dir_ID2, SumaKg, Kind, minDate
FROM dbo.Haccp WHERE P1 = @P OR P2 = @P
ORDER BY minDate;";
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@P", partia);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    res.Przeplywy.Add(new TracePrzeplyw
                    {
                        DzialZ = r["Dir_ID1"]?.ToString()?.Trim(),
                        DzialDo = r["Dir_ID2"]?.ToString()?.Trim(),
                        SumaKg = ToDec(r["SumaKg"]),
                        Kind = r["Kind"]?.ToString()?.Trim(),
                        Data = FmtData(r["minDate"])
                    });
            }
            catch { /* tabela Haccp może nie istnieć w niektórych środowiskach */ }

            // 4) Wyjście / krojenie z Out1A (per towar)
            try
            {
                const string sql = @"
SELECT ArticleName, MAX(JM) AS JM,
       SUM(ISNULL(ActWeight,0)) AS Kg, MAX(DocNo) AS DocNo, MIN(Data) AS DataMin
FROM dbo.Out1A WHERE P1 = @P
GROUP BY ArticleName
ORDER BY SUM(ISNULL(ActWeight,0)) DESC;";
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@P", partia);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    res.Wyjscia.Add(new TraceWyjscie
                    {
                        Towar = r["ArticleName"]?.ToString()?.Trim() ?? "",
                        Jm = r["JM"]?.ToString()?.Trim(),
                        Kg = ToDec(r["Kg"]),
                        DocNo = r["DocNo"]?.ToString()?.Trim(),
                        Data = FmtData(r["DataMin"])
                    });
            }
            catch { }

            if (!res.Znaleziono && res.Blad == null)
                res.Blad = "Nie znaleziono danych dla tej partii (sprawdź numer partii).";
            return res;
        }

        /// <summary>Lista partii do wyboru w reverse trace (z In0E, ostatnie dni).</summary>
        public async Task<List<string>> GetPartieReverseAsync(int dniWstecz = 90)
        {
            var lista = new List<string>();
            const string sql = @"
SELECT DISTINCT TOP 500 P1
FROM dbo.In0E
WHERE P1 IS NOT NULL AND Data >= @Od
ORDER BY P1 DESC;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Od", DateTime.Today.AddDays(-dniWstecz).ToString("yyyy-MM-dd"));
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var p = r["P1"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(p)) lista.Add(p!);
            }
            return lista;
        }

        private static decimal ToDec(object? o)
            => o == null || o == DBNull.Value ? 0m : Convert.ToDecimal(o);

        private static string FmtData(object? o)
        {
            if (o == null || o == DBNull.Value) return "";
            if (o is DateTime dt) return dt.ToString("dd.MM.yyyy");
            var s = o.ToString() ?? "";
            return DateTime.TryParse(s, out var d) ? d.ToString("dd.MM.yyyy") : s;
        }

        /// <summary>Generuje kolejny lot number na dziś: PIO-RRRR-MM-DD-NNN.</summary>
        public async Task<string> GenerujLotNumberAsync(DateTime data)
        {
            string prefix = $"PIO-{data:yyyy-MM-dd}-";
            const string sql = @"
SELECT ISNULL(MAX(CAST(RIGHT(LotNumber,3) AS INT)),0)
FROM dbo.PaletaWyrob WHERE LotNumber LIKE @Prefix + '%';";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@Prefix", prefix);
            int max = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            return $"{prefix}{(max + 1):D3}";
        }

        /// <summary>Rejestruje nową paletę wyrobu wraz ze składem (partie surowe).</summary>
        public async Task<long> RejestrujPaleteAsync(PaletaWyrob p, List<PaletaSklad> sklad, string? user)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            try
            {
                const string insP = @"
INSERT INTO dbo.PaletaWyrob (LotNumber, DataProdukcji, Smiana, Linia, OperatorId, KodTowaru, NazwaTowaru, LiczbaSztuk, WagaKg, DataWaznosci, Status)
OUTPUT INSERTED.Id
VALUES (@Lot, @Data, @Sm, @Lin, @Op, @Kod, @Nazwa, @Szt, @Waga, @Wazn, 'NA_MAGAZYNIE');";
                long id;
                using (var cmd = new SqlCommand(insP, cn, tx) { CommandTimeout = 30 })
                {
                    cmd.Parameters.AddWithValue("@Lot", p.LotNumber);
                    cmd.Parameters.AddWithValue("@Data", p.DataProdukcji.Date);
                    cmd.Parameters.AddWithValue("@Sm", (object?)p.Smiana ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Lin", (object?)p.Linia ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Op", (object?)(p.OperatorId ?? user) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Kod", (object?)p.KodTowaru ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Nazwa", (object?)p.NazwaTowaru ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Szt", (object?)p.LiczbaSztuk ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Waga", p.WagaKg);
                    cmd.Parameters.AddWithValue("@Wazn", (object?)p.DataWaznosci ?? DBNull.Value);
                    id = (long)(await cmd.ExecuteScalarAsync())!;
                }

                const string insS = @"
INSERT INTO dbo.PaletaWyrobSklad (PaletaWyrobId, Partia, CustomerID, CustomerName, WagaKgUdzial, Notatki)
VALUES (@P, @Partia, @CID, @CName, @W, @N);";
                foreach (var s in sklad)
                {
                    using var cmd = new SqlCommand(insS, cn, tx) { CommandTimeout = 30 };
                    cmd.Parameters.AddWithValue("@P", id);
                    cmd.Parameters.AddWithValue("@Partia", s.Partia);
                    cmd.Parameters.AddWithValue("@CID", (object?)s.CustomerID ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CName", (object?)s.CustomerName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@W", (object?)s.WagaKgUdzial ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@N", (object?)s.Notatki ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                tx.Commit();
                return id;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>REVERSE TRACE: od lot number → paleta + skład (hodowcy) + wydania.</summary>
        public async Task<ReverseTraceResult> ReverseTraceAsync(string lotNumber)
        {
            var res = new ReverseTraceResult { LotNumber = lotNumber };
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            const string sqlP = @"
SELECT Id, LotNumber, DataProdukcji, Smiana, Linia, OperatorId, KodTowaru, NazwaTowaru,
       LiczbaSztuk, WagaKg, DataWaznosci, Status
FROM dbo.PaletaWyrob WHERE LotNumber = @Lot;";
            using (var cmd = new SqlCommand(sqlP, cn) { CommandTimeout = 30 })
            {
                cmd.Parameters.AddWithValue("@Lot", lotNumber);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                {
                    res.Blad = "Nie znaleziono palety o tym lot numerze.";
                    return res;
                }
                res.Paleta = new PaletaWyrob
                {
                    Id = r.GetInt64(0),
                    LotNumber = r.GetString(1),
                    DataProdukcji = r.GetDateTime(2),
                    Smiana = r.IsDBNull(3) ? null : r.GetString(3),
                    Linia = r.IsDBNull(4) ? null : r.GetString(4),
                    OperatorId = r.IsDBNull(5) ? null : r.GetString(5),
                    KodTowaru = r.IsDBNull(6) ? null : r.GetString(6),
                    NazwaTowaru = r.IsDBNull(7) ? null : r.GetString(7),
                    LiczbaSztuk = r.IsDBNull(8) ? null : r.GetInt32(8),
                    WagaKg = r.GetDecimal(9),
                    DataWaznosci = r.IsDBNull(10) ? null : r.GetDateTime(10),
                    Status = r.GetString(11)
                };
            }

            res.Sklad = await GetSkladAsync(cn, res.Paleta.Id);
            res.Wydania = await GetWydaniaAsync(cn, res.Paleta.Id);
            return res;
        }

        private async Task<List<PaletaSklad>> GetSkladAsync(SqlConnection cn, long paletaId)
        {
            var lista = new List<PaletaSklad>();
            const string sql = @"
SELECT Id, PaletaWyrobId, Partia, CustomerID, CustomerName, WagaKgUdzial, Notatki
FROM dbo.PaletaWyrobSklad WHERE PaletaWyrobId = @P;";
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@P", paletaId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new PaletaSklad
                {
                    Id = r.GetInt64(0),
                    PaletaWyrobId = r.GetInt64(1),
                    Partia = r.GetString(2),
                    CustomerID = r.IsDBNull(3) ? null : r.GetString(3),
                    CustomerName = r.IsDBNull(4) ? null : r.GetString(4),
                    WagaKgUdzial = r.IsDBNull(5) ? null : r.GetDecimal(5),
                    Notatki = r.IsDBNull(6) ? null : r.GetString(6)
                });
            }
            return lista;
        }

        private async Task<List<PaletaWydanie>> GetWydaniaAsync(SqlConnection cn, long paletaId)
        {
            var lista = new List<PaletaWydanie>();
            const string sql = @"
SELECT Id, PaletaWyrobId, NumerDokumentu, KlientId, KlientNazwa, WagaKgWydana, DataWydania
FROM dbo.DokumentPaletWydania WHERE PaletaWyrobId = @P ORDER BY DataWydania;";
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@P", paletaId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new PaletaWydanie
                {
                    Id = r.GetInt64(0),
                    PaletaWyrobId = r.GetInt64(1),
                    NumerDokumentu = r.IsDBNull(2) ? null : r.GetString(2),
                    KlientId = r.IsDBNull(3) ? null : r.GetInt32(3),
                    KlientNazwa = r.IsDBNull(4) ? null : r.GetString(4),
                    WagaKgWydana = r.IsDBNull(5) ? null : r.GetDecimal(5),
                    DataWydania = r.GetDateTime(6)
                });
            }
            return lista;
        }

        /// <summary>Znajdź palety wyrobu objęte recallem (po partii / dacie / towarze).</summary>
        public async Task<List<PaletaWyrob>> ZnajdzPaletyDoRecallAsync(string typZakresu, string zakresIdent)
        {
            var lista = new List<PaletaWyrob>();
            string where = typZakresu switch
            {
                "PARTIA" => "EXISTS (SELECT 1 FROM dbo.PaletaWyrobSklad s WHERE s.PaletaWyrobId = p.Id AND s.Partia = @Z)",
                "HODOWCA" => "EXISTS (SELECT 1 FROM dbo.PaletaWyrobSklad s WHERE s.PaletaWyrobId = p.Id AND s.CustomerID = @Z)",
                "DATA" => "p.DataProdukcji = @ZData",
                "TOWAR" => "p.KodTowaru = @Z",
                _ => "1=0"
            };
            string sql = $@"
SELECT Id, LotNumber, DataProdukcji, KodTowaru, NazwaTowaru, WagaKg, Status
FROM dbo.PaletaWyrob p WHERE {where};";

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            if (typZakresu == "DATA" && DateTime.TryParse(zakresIdent, out var d))
                cmd.Parameters.AddWithValue("@ZData", d.Date);
            else
                cmd.Parameters.AddWithValue("@Z", zakresIdent);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new PaletaWyrob
                {
                    Id = r.GetInt64(0),
                    LotNumber = r.GetString(1),
                    DataProdukcji = r.GetDateTime(2),
                    KodTowaru = r.IsDBNull(3) ? null : r.GetString(3),
                    NazwaTowaru = r.IsDBNull(4) ? null : r.GetString(4),
                    WagaKg = r.GetDecimal(5),
                    Status = r.GetString(6)
                });
            }
            return lista;
        }

        /// <summary>Inicjuje recall: tworzy wpis + powiązania palet + zmienia ich status na WYCOFANO.</summary>
        public async Task<RecallResult> InicjujRecallAsync(string typZakresu, string zakresIdent,
            string powod, string kategoria, string? user)
        {
            var palety = await ZnajdzPaletyDoRecallAsync(typZakresu, zakresIdent);

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            try
            {
                string recallNumber = await GenerujRecallNumberAsync(cn, tx);

                long recallId;
                const string insR = @"
INSERT INTO dbo.Recall (RecallNumber, InicjowanyPrzez, Powod, Kategoria, TypZakresu, ZakresIdent, LiczbaPalet, WagaKg, Status)
OUTPUT INSERTED.Id
VALUES (@Num, @User, @Powod, @Kat, @Typ, @Z, @LP, @Waga, 'OTWARTY');";
                using (var cmd = new SqlCommand(insR, cn, tx) { CommandTimeout = 30 })
                {
                    cmd.Parameters.AddWithValue("@Num", recallNumber);
                    cmd.Parameters.AddWithValue("@User", (object?)user ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Powod", (object?)powod ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Kat", kategoria);
                    cmd.Parameters.AddWithValue("@Typ", typZakresu);
                    cmd.Parameters.AddWithValue("@Z", zakresIdent);
                    cmd.Parameters.AddWithValue("@LP", palety.Count);
                    cmd.Parameters.AddWithValue("@Waga", palety.Sum(x => x.WagaKg));
                    recallId = (long)(await cmd.ExecuteScalarAsync())!;
                }

                foreach (var p in palety)
                {
                    using (var cmd = new SqlCommand(
                        "INSERT INTO dbo.RecallPalety (RecallId, PaletaWyrobId, Status) VALUES (@R,@P,'OBJETA');",
                        cn, tx))
                    {
                        cmd.Parameters.AddWithValue("@R", recallId);
                        cmd.Parameters.AddWithValue("@P", p.Id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                    using (var cmd = new SqlCommand(
                        "UPDATE dbo.PaletaWyrob SET Status='WYCOFANO' WHERE Id=@P;", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("@P", p.Id);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                tx.Commit();
                return new RecallResult
                {
                    RecallId = recallId,
                    RecallNumber = recallNumber,
                    LiczbaPalet = palety.Count,
                    WagaKg = palety.Sum(x => x.WagaKg),
                    Palety = palety
                };
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private async Task<string> GenerujRecallNumberAsync(SqlConnection cn, SqlTransaction tx)
        {
            string prefix = $"REC-{DateTime.Now:yyyy}-";
            const string sql = @"
SELECT ISNULL(MAX(CAST(RIGHT(RecallNumber,3) AS INT)),0)
FROM dbo.Recall WHERE RecallNumber LIKE @Prefix + '%';";
            using var cmd = new SqlCommand(sql, cn, tx) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@Prefix", prefix);
            int max = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            return $"{prefix}{(max + 1):D3}";
        }

        /// <summary>Lista recalli (domyślnie wszystkie).</summary>
        public async Task<List<Recall>> GetRecalleAsync()
        {
            var lista = new List<Recall>();
            const string sql = @"
SELECT Id, RecallNumber, DataInicjacji, InicjowanyPrzez, Powod, Kategoria,
       TypZakresu, ZakresIdent, LiczbaPalet, LiczbaKlientow, WagaKg, Status, DataZamkniecia
FROM dbo.Recall ORDER BY DataInicjacji DESC;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new Recall
                {
                    Id = r.GetInt64(0),
                    RecallNumber = r.GetString(1),
                    DataInicjacji = r.GetDateTime(2),
                    InicjowanyPrzez = r.IsDBNull(3) ? null : r.GetString(3),
                    Powod = r.IsDBNull(4) ? null : r.GetString(4),
                    Kategoria = r.GetString(5),
                    TypZakresu = r.GetString(6),
                    ZakresIdent = r.IsDBNull(7) ? null : r.GetString(7),
                    LiczbaPalet = r.IsDBNull(8) ? null : r.GetInt32(8),
                    LiczbaKlientow = r.IsDBNull(9) ? null : r.GetInt32(9),
                    WagaKg = r.IsDBNull(10) ? null : r.GetDecimal(10),
                    Status = r.GetString(11),
                    DataZamkniecia = r.IsDBNull(12) ? null : r.GetDateTime(12)
                });
            }
            return lista;
        }

        public async Task ZamknijRecallAsync(long recallId, string? notatki, string? user)
        {
            const string sql = @"
UPDATE dbo.Recall SET Status='ZAMKNIETY', DataZamkniecia=GETDATE(),
    Notatki = ISNULL(@N,Notatki) WHERE Id=@Id;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@N", (object?)notatki ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", recallId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>Partie z hodowcami (do formularza rejestracji składu palety).</summary>
        public async Task<List<PaletaSklad>> GetPartieZHodowcamiAsync(int dniWstecz = 60)
        {
            var lista = new List<PaletaSklad>();
            const string sql = @"
SELECT DISTINCT TOP 300 pd.Partia, pd.CustomerID, pd.CustomerName
FROM dbo.PartiaDostawca pd
WHERE EXISTS (SELECT 1 FROM dbo.In0E e WHERE e.P1 = pd.Partia AND e.Data >= @Od)
ORDER BY pd.Partia DESC;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Od", DateTime.Today.AddDays(-dniWstecz).ToString("yyyy-MM-dd"));
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new PaletaSklad
                {
                    Partia = r["Partia"]?.ToString() ?? "",
                    CustomerID = r["CustomerID"]?.ToString(),
                    CustomerName = r["CustomerName"]?.ToString()
                });
            }
            return lista;
        }
    }
}
