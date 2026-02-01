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

            // Pobierz kontrahentów z Handel (Sage Symfonia schema)
            var kontrahenci = new Dictionary<int, KlientMapa>();
            const string sqlHandel = @"
                SELECT
                    C.Id,
                    C.Name AS NazwaFirmy,
                    ISNULL(C.Shortcut, '') AS Skrot,
                    ISNULL(POA.Place, '') AS Miasto,
                    ISNULL(POA.Street, '') AS Ulica,
                    ISNULL(POA.PostCode, '') AS KodPocztowy,
                    ISNULL(C.NIP, '') AS NIP,
                    ISNULL(C.LimitAmount, 0) AS LimitKupiecki,
                    ISNULL(WYM.CDim_Handlowiec_Val, N'Nieprzypisany') AS Handlowiec,
                    ISNULL((
                        SELECT SUM(DK.walbrutto - ISNULL(PN2.KwotaRozliczona, 0))
                        FROM [HM].[DK] DK
                        LEFT JOIN (
                            SELECT dkid, SUM(kwotarozl) AS KwotaRozliczona
                            FROM [HM].[PN]
                            GROUP BY dkid
                        ) PN2 ON PN2.dkid = DK.id
                        WHERE DK.khid = C.Id
                          AND DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                          AND DK.aktywny = 1
                          AND DK.anulowany = 0
                          AND DK.ok = 0
                          AND (DK.walbrutto - ISNULL(PN2.KwotaRozliczona, 0)) > 0.01
                    ), 0) AS Naleznosci
                FROM [SSCommon].[STContractors] C
                LEFT JOIN [SSCommon].[STPostOfficeAddresses] POA
                    ON POA.ContactGuid = C.ContactGuid
                    AND POA.AddressName = N'adres domyślny'
                LEFT JOIN [SSCommon].[ContractorClassification] WYM
                    ON C.Id = WYM.ElementId
                WHERE POA.Place IS NOT NULL AND POA.Place != ''";

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
                        Handlowiec = rd.IsDBNull(8) ? "" : rd.GetString(8)
                    };

                    decimal limit = rd.GetDecimal(7);
                    decimal naleznosci = rd.GetDecimal(9);
                    k.MaAlert = limit > 0 && naleznosci > limit * 0.8m;

                    kontrahenci[k.Id] = k;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MapaKlientow Handel error: {ex.Message}");
            }

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MapaKlientow LibraNet error: {ex.Message}");
            }

            // Pobierz obroty miesięczne
            const string sqlObroty = @"
                SELECT DK.khid,
                       SUM(DK.walbrutto) /
                       NULLIF(DATEDIFF(MONTH, MIN(DK.data), GETDATE()) + 1, 0) AS SrednioMiesiecznie
                FROM [HM].[DK] DK
                WHERE DK.typ_dk IN ('FVS', 'FVR', 'FVZ')
                  AND DK.aktywny = 1
                  AND DK.anulowany = 0
                  AND DK.data >= DATEADD(YEAR, -1, GETDATE())
                GROUP BY DK.khid";

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MapaKlientow Obroty error: {ex.Message}");
            }

            klienci.AddRange(kontrahenci.Values);
            return klienci;
        }
    }
}
