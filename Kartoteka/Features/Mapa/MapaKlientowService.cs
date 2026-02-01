using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Kartoteka.Models;

namespace Kalendarz1.Kartoteka.Features.Mapa
{
    public class MapaKlientowService
    {
        private readonly string _connLibra;
        private readonly string _connHandel;

        public MapaKlientowService(string connLibra, string connHandel)
        {
            _connLibra = connLibra;
            _connHandel = connHandel;
        }

        public async Task<List<KlientMapa>> PobierzKlientowDoMapyAsync()
        {
            var klienci = new List<KlientMapa>();

            // Pobierz kontrahentów z Handel
            var kontrahenci = new Dictionary<int, KlientMapa>();
            const string sqlHandel = @"
                SELECT sc.Id, sc.Name, sc.ShortName, sc.City, sc.Street, sc.ZIPCode, sc.NIP,
                       ISNULL(sc.LimitKredytowy, 0) AS LimitKr,
                       ISNULL(sc.Naleznosci, 0) AS Naleznosci,
                       ISNULL(h.Handlowiec, '') AS Handlowiec
                FROM SSCommon.STContractors sc
                OUTER APPLY (
                    SELECT TOP 1 sc2.Name AS Handlowiec
                    FROM HM.DK dk
                    INNER JOIN SSCommon.STContractors sc2 ON dk.IdHandlowca = sc2.Id
                    WHERE dk.IdKontrahenta = sc.Id AND dk.TypDokumentu IN (1,2)
                    ORDER BY dk.DataFaktury DESC
                ) h
                WHERE sc.IsActive = 1 AND sc.City IS NOT NULL AND sc.City != ''";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sqlHandel, cn);
                cmd.CommandTimeout = 30;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var k = new KlientMapa
                    {
                        Id = rd.GetInt32(0),
                        NazwaFirmy = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        Skrot = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Miasto = rd.IsDBNull(3) ? "" : rd.GetString(3),
                        Ulica = rd.IsDBNull(4) ? "" : rd.GetString(4),
                        KodPocztowy = rd.IsDBNull(5) ? "" : rd.GetString(5),
                        NIP = rd.IsDBNull(6) ? "" : rd.GetString(6),
                        IsActive = true,
                        Handlowiec = rd.IsDBNull(9) ? "" : rd.GetString(9)
                    };

                    decimal limit = rd.GetDecimal(7);
                    decimal naleznosci = rd.GetDecimal(8);
                    k.MaAlert = limit > 0 && naleznosci > limit * 0.8m;

                    kontrahenci[k.Id] = k;
                }
            }
            catch { }

            // Pobierz dane własne z LibraNet (kategoria, współrzędne)
            const string sqlLibra = @"
                SELECT IdSymfonia, KategoriaHandlowca, Latitude, Longitude
                FROM dbo.KartotekaOdbiorcyDane
                WHERE IdSymfonia IS NOT NULL";

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sqlLibra, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    if (kontrahenci.TryGetValue(id, out var k))
                    {
                        k.Kategoria = rd.IsDBNull(1) ? "C" : rd.GetString(1).Trim();
                        k.Latitude = rd.IsDBNull(2) ? null : (double)rd.GetDecimal(2);
                        k.Longitude = rd.IsDBNull(3) ? null : (double)rd.GetDecimal(3);
                    }
                }
            }
            catch { }

            // Pobierz obroty miesięczne
            const string sqlObroty = @"
                SELECT dk.IdKontrahenta, SUM(dk.WartoscNetto) /
                       NULLIF(DATEDIFF(MONTH, MIN(dk.DataFaktury), GETDATE()) + 1, 0) AS SrednioMiesiecznie
                FROM HM.DK dk
                WHERE dk.TypDokumentu IN (1,2) AND dk.DataFaktury >= DATEADD(YEAR, -1, GETDATE())
                GROUP BY dk.IdKontrahenta";

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sqlObroty, cn);
                cmd.CommandTimeout = 30;
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    if (kontrahenci.TryGetValue(id, out var k) && !rd.IsDBNull(1))
                        k.ObrotyMiesieczne = rd.GetDecimal(1);
                }
            }
            catch { }

            klienci.AddRange(kontrahenci.Values);
            return klienci;
        }
    }
}
