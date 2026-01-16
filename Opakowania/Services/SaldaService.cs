using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Opakowania.Models;

namespace Kalendarz1.Opakowania.Services
{
    /// <summary>
    /// Serwis do pobierania sald opakowań - ZOPTYMALIZOWANY
    /// Zasada: 1 zapytanie = wszystkie dane
    /// </summary>
    public class SaldaService
    {
        private readonly string _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly string _connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        #region Cache

        private static List<SaldoKontrahenta> _cache;
        private static DateTime _cacheTime;
        private static DateTime _cacheDataDo;
        private static string _cacheHandlowiec;
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

        private static bool IsCacheValid(DateTime dataDo, string handlowiec)
        {
            if (_cache == null) return false;
            if (DateTime.Now - _cacheTime > CacheLifetime) return false;
            if (_cacheDataDo != dataDo.Date) return false;
            if (_cacheHandlowiec != handlowiec) return false;
            return true;
        }

        public static void InvalidateCache() => _cache = null;

        #endregion

        #region Pobieranie sald (1 zapytanie!)

        /// <summary>
        /// Pobiera salda WSZYSTKICH kontrahentów, WSZYSTKICH typów opakowań - JEDNYM zapytaniem
        /// </summary>
        public async Task<List<SaldoKontrahenta>> PobierzWszystkieSaldaAsync(DateTime dataDo, string handlowiecFilter = null)
        {
            // Sprawdź cache
            if (IsCacheValid(dataDo, handlowiecFilter))
            {
                return _cache;
            }

            var wyniki = new List<SaldoKontrahenta>();

            // JEDNO SUPER-ZAPYTANIE - wszystkie typy opakowań w jednym wierszu
            string query = @"
SELECT
    C.id AS Id,
    C.Shortcut AS Kontrahent,
    C.Name AS Nazwa,
    ISNULL(WYM.CDim_Handlowiec_Val, '-') AS Handlowiec,

    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS DREW,

    MAX(MG.Data) AS OstatniDokument

FROM [HANDEL].[SSCommon].[STContractors] C
INNER JOIN [HANDEL].[HM].[MG] MG ON MG.khid = C.id
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.Id = WYM.ElementId

WHERE MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data <= @DataDo
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
  AND (@HandlowiecFilter IS NULL OR WYM.CDim_Handlowiec_Val = @HandlowiecFilter)

GROUP BY C.id, C.Shortcut, C.Name, WYM.CDim_Handlowiec_Val

HAVING ABS(SUM(CASE WHEN TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
              THEN MZ.Ilosc ELSE 0 END)) > 0

ORDER BY C.Shortcut";

            try
            {
                // Pobierz potwierdzenia (1 zapytanie)
                var potwierdzenia = await PobierzWszystkiePotwierdzenia();

                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 60;
                        command.Parameters.AddWithValue("@DataDo", dataDo);
                        command.Parameters.AddWithValue("@HandlowiecFilter", (object)handlowiecFilter ?? DBNull.Value);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var kontrahentId = reader.GetInt32(0);
                                var saldo = new SaldoKontrahenta
                                {
                                    Id = kontrahentId,
                                    Kontrahent = reader.GetString(1),
                                    Nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Handlowiec = reader.GetString(3),
                                    E2 = reader.GetInt32(4),
                                    H1 = reader.GetInt32(5),
                                    EURO = reader.GetInt32(6),
                                    PCV = reader.GetInt32(7),
                                    DREW = reader.GetInt32(8),
                                    OstatniDokument = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
                                };

                                // Dodaj potwierdzenia z cache
                                if (potwierdzenia.TryGetValue((kontrahentId, "E2"), out var pe2))
                                {
                                    saldo.E2Potwierdzone = true;
                                    saldo.E2DataPotwierdzenia = pe2;
                                }
                                if (potwierdzenia.TryGetValue((kontrahentId, "H1"), out var ph1))
                                {
                                    saldo.H1Potwierdzone = true;
                                    saldo.H1DataPotwierdzenia = ph1;
                                }
                                if (potwierdzenia.TryGetValue((kontrahentId, "EURO"), out var peuro))
                                {
                                    saldo.EUROPotwierdzone = true;
                                    saldo.EURODataPotwierdzenia = peuro;
                                }
                                if (potwierdzenia.TryGetValue((kontrahentId, "PCV"), out var ppcv))
                                {
                                    saldo.PCVPotwierdzone = true;
                                    saldo.PCVDataPotwierdzenia = ppcv;
                                }
                                if (potwierdzenia.TryGetValue((kontrahentId, "DREW"), out var pdrew))
                                {
                                    saldo.DREWPotwierdzone = true;
                                    saldo.DREWDataPotwierdzenia = pdrew;
                                }

                                wyniki.Add(saldo);
                            }
                        }
                    }
                }

                // Zapisz w cache
                _cache = wyniki;
                _cacheTime = DateTime.Now;
                _cacheDataDo = dataDo.Date;
                _cacheHandlowiec = handlowiecFilter;
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania sald: {ex.Message}", ex);
            }

            return wyniki;
        }

        #endregion

        #region Dokumenty kontrahenta

        /// <summary>
        /// Pobiera dokumenty kontrahenta w danym okresie
        /// </summary>
        public async Task<List<DokumentSalda>> PobierzDokumentyAsync(int kontrahentId, DateTime dataOd, DateTime dataDo)
        {
            var dokumenty = new List<DokumentSalda>();

            // Saldo początkowe
            string saldoPoczatkoweQuery = @"
SELECT
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS DREW
FROM [HANDEL].[HM].[MG] MG
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
WHERE MG.khid = @KontrahentId
  AND MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data <= @DataOd
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')";

            // Dokumenty w zakresie
            string dokumentyQuery = @"
SELECT
    MG.id AS Id,
    MG.kod AS NrDokumentu,
    MG.data AS Data,
    MG.opis AS Opis,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Pojemnik Drobiowy E2' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS E2,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta H1' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS H1,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta EURO' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS EURO,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta plastikowa' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS PCV,
    CAST(ISNULL(SUM(CASE WHEN TW.nazwa = 'Paleta Drewniana' THEN MZ.Ilosc ELSE 0 END), 0) AS INT) AS DREW
FROM [HANDEL].[HM].[MG] MG
INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
WHERE MG.khid = @KontrahentId
  AND MG.anulowany = 0
  AND MG.magazyn = 65559
  AND MG.typ_dk IN ('MW1', 'MP')
  AND MG.data > @DataOd
  AND MG.data <= @DataDo
  AND TW.nazwa IN ('Pojemnik Drobiowy E2', 'Paleta H1', 'Paleta EURO', 'Paleta plastikowa', 'Paleta Drewniana')
GROUP BY MG.id, MG.kod, MG.data, MG.opis
ORDER BY MG.data DESC";

            try
            {
                using (var connection = new SqlConnection(_connectionStringHandel))
                {
                    await connection.OpenAsync();

                    // Saldo początkowe
                    int sE2 = 0, sH1 = 0, sEURO = 0, sPCV = 0, sDREW = 0;
                    using (var cmd = new SqlCommand(saldoPoczatkoweQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                sE2 = reader.GetInt32(0);
                                sH1 = reader.GetInt32(1);
                                sEURO = reader.GetInt32(2);
                                sPCV = reader.GetInt32(3);
                                sDREW = reader.GetInt32(4);
                            }
                        }
                    }

                    // Dokumenty
                    var listaDoc = new List<DokumentSalda>();
                    using (var cmd = new SqlCommand(dokumentyQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                listaDoc.Add(new DokumentSalda
                                {
                                    Id = reader.GetInt32(0),
                                    NrDokumentu = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    Data = reader.GetDateTime(2),
                                    Opis = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    E2 = reader.GetInt32(4),
                                    H1 = reader.GetInt32(5),
                                    EURO = reader.GetInt32(6),
                                    PCV = reader.GetInt32(7),
                                    DREW = reader.GetInt32(8),
                                    JestSaldem = false
                                });
                            }
                        }
                    }

                    // Oblicz saldo końcowe
                    int kE2 = sE2, kH1 = sH1, kEURO = sEURO, kPCV = sPCV, kDREW = sDREW;
                    foreach (var doc in listaDoc)
                    {
                        kE2 += doc.E2;
                        kH1 += doc.H1;
                        kEURO += doc.EURO;
                        kPCV += doc.PCV;
                        kDREW += doc.DREW;
                    }

                    // Dodaj saldo końcowe na początek
                    dokumenty.Add(new DokumentSalda
                    {
                        NrDokumentu = $"Saldo na {dataDo:dd.MM.yyyy}",
                        Data = dataDo,
                        E2 = kE2,
                        H1 = kH1,
                        EURO = kEURO,
                        PCV = kPCV,
                        DREW = kDREW,
                        JestSaldem = true
                    });

                    // Dokumenty
                    dokumenty.AddRange(listaDoc);

                    // Saldo początkowe na koniec
                    dokumenty.Add(new DokumentSalda
                    {
                        NrDokumentu = $"Saldo na {dataOd:dd.MM.yyyy}",
                        Data = dataOd,
                        E2 = sE2,
                        H1 = sH1,
                        EURO = sEURO,
                        PCV = sPCV,
                        DREW = sDREW,
                        JestSaldem = true
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania dokumentów: {ex.Message}", ex);
            }

            return dokumenty;
        }

        #endregion

        #region Potwierdzenia

        /// <summary>
        /// Pobiera wszystkie potwierdzenia (1 zapytanie)
        /// </summary>
        private async Task<Dictionary<(int KontrahentId, string Typ), DateTime>> PobierzWszystkiePotwierdzenia()
        {
            var wyniki = new Dictionary<(int, string), DateTime>();

            string query = @"
SELECT KontrahentId, KodOpakowania, MAX(DataPotwierdzenia) AS DataPotwierdzenia
FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
WHERE StatusPotwierdzenia = 'Potwierdzone'
  AND DataPotwierdzenia >= DATEADD(DAY, -30, GETDATE())
GROUP BY KontrahentId, KodOpakowania";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var kontrahentId = reader.GetInt32(0);
                                var kod = reader.GetString(1);
                                var data = reader.GetDateTime(2);
                                wyniki[(kontrahentId, kod)] = data;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj - tabela może nie istnieć
            }

            return wyniki;
        }

        /// <summary>
        /// Dodaje nowe potwierdzenie
        /// </summary>
        public async Task<int> DodajPotwierdzenieAsync(int kontrahentId, string kontrahentNazwa, string typOpakowania,
            int iloscPotwierdzona, int saldoSystemowe, string status, string uwagi, string uzytkownikId, string uzytkownikNazwa)
        {
            string query = @"
INSERT INTO [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
(KontrahentId, KontrahentNazwa, KontrahentShortcut, TypOpakowania, KodOpakowania,
 DataPotwierdzenia, IloscPotwierdzona, SaldoSystemowe, StatusPotwierdzenia,
 Uwagi, UzytkownikId, UzytkownikNazwa)
VALUES
(@KontrahentId, @KontrahentNazwa, @KontrahentNazwa, @TypOpakowania, @KodOpakowania,
 GETDATE(), @IloscPotwierdzona, @SaldoSystemowe, @StatusPotwierdzenia,
 @Uwagi, @UzytkownikId, @UzytkownikNazwa);
SELECT SCOPE_IDENTITY();";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@KontrahentId", kontrahentId);
                        command.Parameters.AddWithValue("@KontrahentNazwa", kontrahentNazwa ?? "");
                        command.Parameters.AddWithValue("@TypOpakowania", GetNazwaSystemowa(typOpakowania));
                        command.Parameters.AddWithValue("@KodOpakowania", typOpakowania);
                        command.Parameters.AddWithValue("@IloscPotwierdzona", iloscPotwierdzona);
                        command.Parameters.AddWithValue("@SaldoSystemowe", saldoSystemowe);
                        command.Parameters.AddWithValue("@StatusPotwierdzenia", status);
                        command.Parameters.AddWithValue("@Uwagi", (object)uwagi ?? DBNull.Value);
                        command.Parameters.AddWithValue("@UzytkownikId", uzytkownikId);
                        command.Parameters.AddWithValue("@UzytkownikNazwa", uzytkownikNazwa ?? "");

                        var result = await command.ExecuteScalarAsync();
                        InvalidateCache();
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas dodawania potwierdzenia: {ex.Message}", ex);
            }
        }

        private string GetNazwaSystemowa(string kod) => kod switch
        {
            "E2" => "Pojemnik Drobiowy E2",
            "H1" => "Paleta H1",
            "EURO" => "Paleta EURO",
            "PCV" => "Paleta plastikowa",
            "DREW" => "Paleta Drewniana",
            _ => kod
        };

        /// <summary>
        /// Pobiera potwierdzenia dla konkretnego kontrahenta
        /// </summary>
        public async Task<List<Potwierdzenie>> PobierzPotwierdzeniaKontrahentaAsync(int kontrahentId)
        {
            var wyniki = new List<Potwierdzenie>();

            string query = @"
SELECT Id, KontrahentId, TypOpakowania, KodOpakowania, DataPotwierdzenia,
       IloscPotwierdzona, SaldoSystemowe, StatusPotwierdzenia, Uwagi,
       UzytkownikId, UzytkownikNazwa, DataWprowadzenia
FROM [LibraNet].[dbo].[PotwierdzeniaSaldaOpakowan]
WHERE KontrahentId = @KontrahentId
ORDER BY DataPotwierdzenia DESC, DataWprowadzenia DESC";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@KontrahentId", kontrahentId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                wyniki.Add(new Potwierdzenie
                                {
                                    Id = reader.GetInt32(0),
                                    KontrahentId = reader.GetInt32(1),
                                    TypOpakowania = reader.IsDBNull(3) ? reader.GetString(2) : reader.GetString(3),
                                    DataPotwierdzenia = reader.GetDateTime(4),
                                    IloscPotwierdzona = reader.GetInt32(5),
                                    SaldoSystemowe = reader.GetInt32(6),
                                    Status = reader.IsDBNull(7) ? "Oczekujące" : reader.GetString(7),
                                    Uwagi = reader.IsDBNull(8) ? null : reader.GetString(8),
                                    Uzytkownik = reader.IsDBNull(10) ? reader.GetString(9) : reader.GetString(10),
                                    DataWprowadzenia = reader.GetDateTime(11)
                                });
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignoruj - tabela może nie istnieć
            }

            return wyniki;
        }

        #endregion

        #region Uprawnienia

        /// <summary>
        /// Pobiera filtr handlowca dla użytkownika
        /// </summary>
        public async Task<string> PobierzHandlowcaAsync(string userId)
        {
            if (userId == "11111") return null; // Admin

            string query = @"
SELECT HandlowiecNazwa
FROM [LibraNet].[dbo].[MapowanieHandlowcow]
WHERE UserId = @UserId AND CzyAktywny = 1";

            try
            {
                using (var connection = new SqlConnection(_connectionStringLibraNet))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
