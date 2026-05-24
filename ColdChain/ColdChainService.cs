using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Services;

namespace Kalendarz1.ColdChain
{
    /// <summary>
    /// Cold Chain HACCP (#2). Tryb MANUALNY (operator wpisuje) działa od razu.
    /// Tryb AUTO (sondy Modbus) — patrz TODO ReadModbusAsync (po zakupie sprzętu).
    /// Baza: LibraNet. Wymaga ColdChain/SQL/CreateColdChain.sql.
    /// </summary>
    public class ColdChainService
    {
        private readonly string _conn;

        public ColdChainService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _conn = AnalitykaConfig.ConnLibraNet;
        }

        /// <summary>Wszystkie aktywne punkty CCP z ostatnim pomiarem.</summary>
        public async Task<List<CCPPunkt>> GetPunktyZOstatnimPomiaremAsync()
        {
            var lista = new List<CCPPunkt>();
            const string sql = @"
SELECT p.Id, p.Kod, p.Nazwa, p.TypPomiaru, p.LimitDolny, p.LimitGorny,
       p.Jednostka, p.CzestotliwoscMin, p.Aktywny,
       m.Wartosc AS OstWartosc, m.PomiarDateTime AS OstDt
FROM dbo.CCP_Punkt p
OUTER APPLY (
    SELECT TOP 1 Wartosc, PomiarDateTime
    FROM dbo.CCP_Pomiar
    WHERE PunktId = p.Id
    ORDER BY PomiarDateTime DESC
) m
WHERE p.Aktywny = 1
ORDER BY p.Kod;";

            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new CCPPunkt
                {
                    Id = r.GetInt32(0),
                    Kod = r.GetString(1),
                    Nazwa = r.GetString(2),
                    TypPomiaru = r.GetString(3),
                    LimitDolny = r.IsDBNull(4) ? null : r.GetDecimal(4),
                    LimitGorny = r.IsDBNull(5) ? null : r.GetDecimal(5),
                    Jednostka = r.GetString(6),
                    CzestotliwoscMin = r.IsDBNull(7) ? null : r.GetInt32(7),
                    Aktywny = r.GetBoolean(8),
                    OstatniaWartosc = r.IsDBNull(9) ? null : r.GetDecimal(9),
                    OstatniPomiarDateTime = r.IsDBNull(10) ? null : r.GetDateTime(10)
                });
            }
            return lista;
        }

        /// <summary>
        /// Zapisuje pomiar (tryb manualny). Po zapisie wykrywa incydent jeśli poza limitem.
        /// </summary>
        public async Task ZapiszPomiarAsync(int punktId, decimal wartosc, string? operatorId, string? uwagi)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            // 1. Zapis pomiaru
            const string ins = @"
INSERT INTO dbo.CCP_Pomiar (PunktId, PomiarDateTime, Wartosc, Zrodlo, OperatorId, Uwagi)
VALUES (@P, GETDATE(), @W, 'MANUALNY', @Op, @U);";
            using (var cmd = new SqlCommand(ins, cn) { CommandTimeout = 30 })
            {
                cmd.Parameters.AddWithValue("@P", punktId);
                cmd.Parameters.AddWithValue("@W", wartosc);
                cmd.Parameters.AddWithValue("@Op", (object?)operatorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@U", (object?)uwagi ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Sprawdź limity punktu
            decimal? limDol = null, limGor = null;
            const string limSql = "SELECT LimitDolny, LimitGorny FROM dbo.CCP_Punkt WHERE Id = @P;";
            using (var cmd = new SqlCommand(limSql, cn))
            {
                cmd.Parameters.AddWithValue("@P", punktId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    limDol = r.IsDBNull(0) ? null : r.GetDecimal(0);
                    limGor = r.IsDBNull(1) ? null : r.GetDecimal(1);
                }
            }

            bool poza = (limDol.HasValue && wartosc < limDol.Value)
                     || (limGor.HasValue && wartosc > limGor.Value);

            // 3. Zarządzaj incydentem
            if (poza)
                await OtworzLubPrzedluzIncydentAsync(cn, punktId, wartosc, limDol, limGor);
            else
                await ZamknijOtwartyIncydentAutoAsync(cn, punktId);
        }

        private async Task OtworzLubPrzedluzIncydentAsync(SqlConnection cn, int punktId,
            decimal wartosc, decimal? limDol, decimal? limGor)
        {
            // Czy jest otwarty incydent dla tego punktu?
            const string check = @"SELECT TOP 1 Id, WartoscMin, WartoscMax FROM dbo.CCP_Incydent
WHERE PunktId = @P AND StatusFinal = 'OTWARTY' ORDER BY StartDateTime DESC;";
            long? incId = null; decimal min = wartosc, max = wartosc;
            using (var cmd = new SqlCommand(check, cn))
            {
                cmd.Parameters.AddWithValue("@P", punktId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    incId = r.GetInt64(0);
                    if (!r.IsDBNull(1)) min = Math.Min(min, r.GetDecimal(1));
                    if (!r.IsDBNull(2)) max = Math.Max(max, r.GetDecimal(2));
                }
            }

            if (incId.HasValue)
            {
                const string upd = "UPDATE dbo.CCP_Incydent SET WartoscMin=@Min, WartoscMax=@Max WHERE Id=@Id;";
                using var cmd = new SqlCommand(upd, cn);
                cmd.Parameters.AddWithValue("@Min", min);
                cmd.Parameters.AddWithValue("@Max", max);
                cmd.Parameters.AddWithValue("@Id", incId.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                const string ins = @"
INSERT INTO dbo.CCP_Incydent (PunktId, StartDateTime, WartoscMin, WartoscMax, LimitDolny, LimitGorny, Priorytet, StatusFinal)
VALUES (@P, GETDATE(), @Min, @Max, @LD, @LG, 'WYSOKI', 'OTWARTY');";
                using var cmd = new SqlCommand(ins, cn);
                cmd.Parameters.AddWithValue("@P", punktId);
                cmd.Parameters.AddWithValue("@Min", wartosc);
                cmd.Parameters.AddWithValue("@Max", wartosc);
                cmd.Parameters.AddWithValue("@LD", (object?)limDol ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LG", (object?)limGor ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task ZamknijOtwartyIncydentAutoAsync(SqlConnection cn, int punktId)
        {
            // Gdy wartość wraca do normy — zamknij otwarty incydent (czas powrotu)
            const string upd = @"
UPDATE dbo.CCP_Incydent
SET EndDateTime = GETDATE()
WHERE PunktId = @P AND StatusFinal = 'OTWARTY' AND EndDateTime IS NULL;";
            using var cmd = new SqlCommand(upd, cn);
            cmd.Parameters.AddWithValue("@P", punktId);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>Ostatnie pomiary danego punktu (do wykresu).</summary>
        public async Task<List<CCPPomiar>> GetPomiaryAsync(int punktId, int ostatnieN = 60)
        {
            var lista = new List<CCPPomiar>();
            const string sql = @"
SELECT TOP (@N) Id, PunktId, PomiarDateTime, Wartosc, Zrodlo, OperatorId, Uwagi
FROM dbo.CCP_Pomiar WHERE PunktId = @P
ORDER BY PomiarDateTime DESC;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@N", ostatnieN);
            cmd.Parameters.AddWithValue("@P", punktId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new CCPPomiar
                {
                    Id = r.GetInt64(0),
                    PunktId = r.GetInt32(1),
                    PomiarDateTime = r.GetDateTime(2),
                    Wartosc = r.GetDecimal(3),
                    Zrodlo = r.GetString(4),
                    OperatorId = r.IsDBNull(5) ? null : r.GetString(5),
                    Uwagi = r.IsDBNull(6) ? null : r.GetString(6)
                });
            }
            return lista;
        }

        /// <summary>Incydenty (domyślnie otwarte + ostatnie zamknięte).</summary>
        public async Task<List<CCPIncydent>> GetIncydentyAsync(bool tylkoOtwarte = false)
        {
            var lista = new List<CCPIncydent>();
            string sql = @"
SELECT i.Id, i.PunktId, p.Nazwa, i.StartDateTime, i.EndDateTime,
       i.WartoscMin, i.WartoscMax, i.LimitDolny, i.LimitGorny,
       i.Priorytet, i.StatusFinal, i.KorektaOpis, i.KorektaPrzezId
FROM dbo.CCP_Incydent i
JOIN dbo.CCP_Punkt p ON p.Id = i.PunktId
" + (tylkoOtwarte ? "WHERE i.StatusFinal = 'OTWARTY'" : "") + @"
ORDER BY i.StartDateTime DESC;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new CCPIncydent
                {
                    Id = r.GetInt64(0),
                    PunktId = r.GetInt32(1),
                    PunktNazwa = r.GetString(2),
                    StartDateTime = r.GetDateTime(3),
                    EndDateTime = r.IsDBNull(4) ? null : r.GetDateTime(4),
                    WartoscMin = r.IsDBNull(5) ? null : r.GetDecimal(5),
                    WartoscMax = r.IsDBNull(6) ? null : r.GetDecimal(6),
                    LimitDolny = r.IsDBNull(7) ? null : r.GetDecimal(7),
                    LimitGorny = r.IsDBNull(8) ? null : r.GetDecimal(8),
                    Priorytet = r.GetString(9),
                    StatusFinal = r.GetString(10),
                    KorektaOpis = r.IsDBNull(11) ? null : r.GetString(11),
                    KorektaPrzezId = r.IsDBNull(12) ? null : r.GetString(12)
                });
            }
            return lista;
        }

        /// <summary>Zamyka incydent z opisem korekty (działanie naprawcze HACCP).</summary>
        public async Task ZamknijIncydentAsync(long incydentId, string korektaOpis, string? user)
        {
            const string sql = @"
UPDATE dbo.CCP_Incydent
SET StatusFinal = 'ZAMKNIETY',
    EndDateTime = ISNULL(EndDateTime, GETDATE()),
    KorektaOpis = @Op, KorektaPrzezId = @U, KorektaDateTime = GETDATE()
WHERE Id = @Id;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@Op", korektaOpis);
            cmd.Parameters.AddWithValue("@U", (object?)user ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", incydentId);
            await cmd.ExecuteNonQueryAsync();
        }

        // TODO (tryb AUTO, po zakupie sond):
        //   public async Task ReadModbusAsync() — odczyt PT1000 przez NModbus,
        //   zapis z Zrodlo='AUTO'. Wzorzec w BAZA_WIEDZY/30_POMYSLY/09_Scalding_Monitor.md.
    }
}
