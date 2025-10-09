// =============== OdbiorcyRepository.cs - ZOPTYMALIZOWANE ZAPYTANIE ===============
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1
{
    public class OdbiorcyRepository
    {
        private readonly string connectionString;
        private readonly CoordinatesCache coordinatesCache;

        public OdbiorcyRepository(string connString)
        {
            connectionString = connString;
            coordinatesCache = new CoordinatesCache();
        }

        public async Task<List<OdbiorcaDto>> GetAllAsync(OdbiorcaFilter filter)
        {
            var result = new List<OdbiorcaDto>();

            // Zoptymalizowane zapytanie z indeksami
            string query = @"
                WITH FilteredContractors AS (
                    SELECT 
                        C.Id,
                        C.Shortcut AS NazwaKrotka,
                        C.Name AS NazwaPelna,
                        PA.Street AS Ulica,
                        PA.PostCode AS KodPocztowy,
                        PA.Place AS Miejscowosc,
                        WYM.CDim_Handlowiec_Val AS HandlowiecNazwa,
                        ROW_NUMBER() OVER (PARTITION BY C.Id ORDER BY C.Id) as rn
                    FROM [HANDEL].[SSCommon].[STContractors] C WITH (NOLOCK)
                    LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] PA WITH (NOLOCK)
                        ON PA.ContactGuid = C.ContactGuid 
                        AND PA.AddressName = 'adres domyślny'
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM WITH (NOLOCK)
                        ON C.Id = WYM.ElementId
                    WHERE C.Id IS NOT NULL";

            if (filter?.Handlowcy?.Count > 0)
            {
                var handlowcyList = string.Join("','", filter.Handlowcy.Select(h => h.Replace("'", "''")));
                query += $" AND WYM.CDim_Handlowiec_Val IN ('{handlowcyList}')";
            }

            if (!string.IsNullOrWhiteSpace(filter?.SearchText))
            {
                query += @" AND (C.Shortcut LIKE @search 
                          OR C.Name LIKE @search
                          OR PA.Place LIKE @search)";
            }

            query += @")
                SELECT Id, NazwaKrotka, NazwaPelna, Ulica, KodPocztowy, 
                       Miejscowosc, HandlowiecNazwa
                FROM FilteredContractors 
                WHERE rn = 1
                ORDER BY HandlowiecNazwa, NazwaKrotka";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn)
                    {
                        CommandTimeout = 30
                    };

                    if (!string.IsNullOrWhiteSpace(filter?.SearchText))
                    {
                        cmd.Parameters.AddWithValue("@search", $"%{filter.SearchText}%");
                    }

                    await conn.OpenAsync();

                    using (var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            string nazwaKrotka = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            string nazwaPelna = reader.IsDBNull(2) ? nazwaKrotka : reader.GetString(2);

                            string ulica = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            string kodPocztowy = reader.IsDBNull(4) ? "" : reader.GetString(4);
                            string miejscowosc = reader.IsDBNull(5) ? "" : reader.GetString(5);

                            string adresPelny = BuildAddress(ulica, kodPocztowy, miejscowosc, nazwaPelna);

                            var odbiorca = new OdbiorcaDto
                            {
                                Id = id,
                                Nazwa = nazwaKrotka,
                                AdresPelny = adresPelny,
                                HandlowiecId = id,
                                HandlowiecNazwa = reader.IsDBNull(6) ? "Nieprzypisany" : reader.GetString(6)
                            };

                            // Pobierz współrzędne asynchronicznie
                            var coords = await coordinatesCache.GetCoordinatesAsync(id);
                            if (coords.HasValue)
                            {
                                odbiorca.Latitude = coords.Value.lat;
                                odbiorca.Longitude = coords.Value.lng;
                            }

                            result.Add(odbiorca);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd pobierania danych: {ex.Message}", ex);
            }

            return result;
        }

        private string BuildAddress(string ulica, string kodPocztowy, string miejscowosc, string nazwaPelna)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(ulica))
                parts.Add(ulica);

            if (!string.IsNullOrWhiteSpace(kodPocztowy) || !string.IsNullOrWhiteSpace(miejscowosc))
            {
                parts.Add($"{kodPocztowy} {miejscowosc}".Trim());
            }

            return parts.Count > 0 ? string.Join(", ", parts) : nazwaPelna;
        }

        public async Task SaveCoordinatesLocallyAsync(int id, double lat, double lng)
        {
            await coordinatesCache.SetCoordinatesAsync(id, lat, lng);
        }

        public async Task<List<string>> GetDistinctHandlowcyAsync()
        {
            var result = new List<string>();

            string query = @"
                SELECT DISTINCT TOP 100 CDim_Handlowiec_Val
                FROM [HANDEL].[SSCommon].[ContractorClassification] WITH (NOLOCK)
                WHERE CDim_Handlowiec_Val IS NOT NULL
                ORDER BY CDim_Handlowiec_Val";

            using (var conn = new SqlConnection(connectionString))
            {
                var cmd = new SqlCommand(query, conn);
                await conn.OpenAsync();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(reader.GetString(0));
                    }
                }
            }

            return result;
        }
    }
}