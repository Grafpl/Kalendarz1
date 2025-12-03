using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.FakturyPanel.Models;

namespace Kalendarz1.FakturyPanel.Services
{
    /// <summary>
    /// Serwis danych dla panelu fakturzystek
    /// Obsługuje pobieranie zamówień i historii zmian
    /// </summary>
    public class FakturyDataService
    {
        private readonly string _connectionStringHandel;
        private readonly string _connectionStringLibraNet;

        public FakturyDataService()
        {
            _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            _connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        /// <summary>
        /// Pobiera listę zamówień dla panelu fakturzystek
        /// </summary>
        public async Task<List<ZamowienieFaktury>> PobierzZamowieniaAsync(FiltrZamowien filtr = null)
        {
            var zamowienia = new List<ZamowienieFaktury>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        z.ID,
                        z.DataOdbioru,
                        z.DataProdukcji,
                        z.Godzina,
                        z.Notatka,
                        z.Handlowiec,
                        z.WlasnyOdbior,
                        z.Anulowane,
                        z.DataDodania,
                        z.DodanyPrzez,
                        z.DataModyfikacji,
                        z.ZmodyfikowanyPrzez,
                        k.Nazwa AS OdbiorcaNazwa,
                        k.id AS OdbiorcaId,
                        COALESCE(SUM(zp.Palety), 0) AS SumaPalet,
                        COALESCE(SUM(zp.Pojemniki), 0) AS SumaPojemnikow,
                        COALESCE(SUM(zp.Ilosc), 0) AS SumaKg
                    FROM ZamowieniaMieso z
                    LEFT JOIN Kontrahenci k ON z.OdbiorcaId = k.id
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE 1=1 ";

                // Dodaj filtry
                if (filtr != null)
                {
                    if (filtr.DataOd.HasValue)
                        sql += " AND z.DataOdbioru >= @DataOd";

                    if (filtr.DataDo.HasValue)
                        sql += " AND z.DataOdbioru <= @DataDo";

                    if (!string.IsNullOrEmpty(filtr.Handlowiec) && filtr.Handlowiec != "— Wszyscy —")
                        sql += " AND z.Handlowiec = @Handlowiec";

                    if (!string.IsNullOrEmpty(filtr.SzukajTekst))
                        sql += " AND (k.Nazwa LIKE @Szukaj OR z.Notatka LIKE @Szukaj)";

                    if (!filtr.PokazAnulowane)
                        sql += " AND (z.Anulowane IS NULL OR z.Anulowane = 0)";
                }
                else
                {
                    sql += " AND (z.Anulowane IS NULL OR z.Anulowane = 0)";
                }

                sql += @"
                    GROUP BY z.ID, z.DataOdbioru, z.DataProdukcji, z.Godzina, z.Notatka,
                             z.Handlowiec, z.WlasnyOdbior, z.Anulowane, z.DataDodania,
                             z.DodanyPrzez, z.DataModyfikacji, z.ZmodyfikowanyPrzez,
                             k.Nazwa, k.id
                    ORDER BY z.DataOdbioru DESC, k.Nazwa";

                await using var cmd = new SqlCommand(sql, cn);

                if (filtr != null)
                {
                    if (filtr.DataOd.HasValue)
                        cmd.Parameters.AddWithValue("@DataOd", filtr.DataOd.Value);

                    if (filtr.DataDo.HasValue)
                        cmd.Parameters.AddWithValue("@DataDo", filtr.DataDo.Value);

                    if (!string.IsNullOrEmpty(filtr.Handlowiec) && filtr.Handlowiec != "— Wszyscy —")
                        cmd.Parameters.AddWithValue("@Handlowiec", filtr.Handlowiec);

                    if (!string.IsNullOrEmpty(filtr.SzukajTekst))
                        cmd.Parameters.AddWithValue("@Szukaj", $"%{filtr.SzukajTekst}%");
                }

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var zamowienie = new ZamowienieFaktury
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("ID")),
                        DataOdbioru = reader.IsDBNull(reader.GetOrdinal("DataOdbioru"))
                            ? DateTime.Today
                            : reader.GetDateTime(reader.GetOrdinal("DataOdbioru")),
                        DataProdukcji = reader.IsDBNull(reader.GetOrdinal("DataProdukcji"))
                            ? DateTime.Today
                            : reader.GetDateTime(reader.GetOrdinal("DataProdukcji")),
                        GodzinaOdbioru = reader.IsDBNull(reader.GetOrdinal("Godzina"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("Godzina")),
                        Notatka = reader.IsDBNull(reader.GetOrdinal("Notatka"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("Notatka")),
                        Handlowiec = reader.IsDBNull(reader.GetOrdinal("Handlowiec"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("Handlowiec")),
                        WlasnyOdbior = !reader.IsDBNull(reader.GetOrdinal("WlasnyOdbior"))
                            && reader.GetBoolean(reader.GetOrdinal("WlasnyOdbior")),
                        JestAnulowane = !reader.IsDBNull(reader.GetOrdinal("Anulowane"))
                            && reader.GetBoolean(reader.GetOrdinal("Anulowane")),
                        Odbiorca = reader.IsDBNull(reader.GetOrdinal("OdbiorcaNazwa"))
                            ? "Nieznany"
                            : reader.GetString(reader.GetOrdinal("OdbiorcaNazwa")),
                        OdbiorcaId = reader.IsDBNull(reader.GetOrdinal("OdbiorcaId"))
                            ? 0
                            : reader.GetInt32(reader.GetOrdinal("OdbiorcaId")),
                        SumaPalet = reader.IsDBNull(reader.GetOrdinal("SumaPalet"))
                            ? 0
                            : reader.GetDecimal(reader.GetOrdinal("SumaPalet")),
                        SumaPojemnikow = reader.IsDBNull(reader.GetOrdinal("SumaPojemnikow"))
                            ? 0
                            : reader.GetDecimal(reader.GetOrdinal("SumaPojemnikow")),
                        SumaKg = reader.IsDBNull(reader.GetOrdinal("SumaKg"))
                            ? 0
                            : reader.GetDecimal(reader.GetOrdinal("SumaKg")),
                        DataUtworzenia = reader.IsDBNull(reader.GetOrdinal("DataDodania"))
                            ? DateTime.Now
                            : reader.GetDateTime(reader.GetOrdinal("DataDodania")),
                        UtworzonyPrzez = reader.IsDBNull(reader.GetOrdinal("DodanyPrzez"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("DodanyPrzez")),
                        DataModyfikacji = reader.IsDBNull(reader.GetOrdinal("DataModyfikacji"))
                            ? (DateTime?)null
                            : reader.GetDateTime(reader.GetOrdinal("DataModyfikacji")),
                        ZmodyfikowanePrzez = reader.IsDBNull(reader.GetOrdinal("ZmodyfikowanyPrzez"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("ZmodyfikowanyPrzez"))
                    };

                    zamowienia.Add(zamowienie);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania zamówień: {ex.Message}");
                throw;
            }

            return zamowienia;
        }

        /// <summary>
        /// Pobiera szczegóły zamówienia (pozycje)
        /// </summary>
        public async Task<List<PozycjaZamowienia>> PobierzPozycjeZamowieniaAsync(int zamowienieId)
        {
            var pozycje = new List<PozycjaZamowienia>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        zp.ID,
                        zp.ZamowienieId,
                        zp.KodTowaru,
                        t.Nazwa AS NazwaTowaru,
                        zp.Ilosc,
                        zp.Pojemniki,
                        zp.Palety,
                        zp.Cena,
                        zp.E2,
                        zp.Folia,
                        zp.Hallal,
                        zp.Katalog
                    FROM ZamowieniaMiesoPozycje zp
                    LEFT JOIN Towary t ON zp.KodTowaru = t.Kod
                    WHERE zp.ZamowienieId = @ZamowienieId
                    ORDER BY zp.KodTowaru";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamowienieId", zamowienieId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var pozycja = new PozycjaZamowienia
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("ID")),
                        ZamowienieId = reader.GetInt32(reader.GetOrdinal("ZamowienieId")),
                        KodProduktu = reader.IsDBNull(reader.GetOrdinal("KodTowaru"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("KodTowaru")),
                        NazwaProduktu = reader.IsDBNull(reader.GetOrdinal("NazwaTowaru"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("NazwaTowaru")),
                        IloscKg = reader.IsDBNull(reader.GetOrdinal("Ilosc"))
                            ? 0
                            : reader.GetDecimal(reader.GetOrdinal("Ilosc")),
                        IloscPojemnikow = reader.IsDBNull(reader.GetOrdinal("Pojemniki"))
                            ? 0
                            : reader.GetDecimal(reader.GetOrdinal("Pojemniki")),
                        IloscPalet = reader.IsDBNull(reader.GetOrdinal("Palety"))
                            ? 0
                            : reader.GetDecimal(reader.GetOrdinal("Palety")),
                        Cena = reader.IsDBNull(reader.GetOrdinal("Cena"))
                            ? (decimal?)null
                            : reader.GetDecimal(reader.GetOrdinal("Cena")),
                        E2 = !reader.IsDBNull(reader.GetOrdinal("E2"))
                            && reader.GetBoolean(reader.GetOrdinal("E2")),
                        Folia = !reader.IsDBNull(reader.GetOrdinal("Folia"))
                            && reader.GetBoolean(reader.GetOrdinal("Folia")),
                        Hallal = !reader.IsDBNull(reader.GetOrdinal("Hallal"))
                            && reader.GetBoolean(reader.GetOrdinal("Hallal")),
                        Katalog = reader.IsDBNull(reader.GetOrdinal("Katalog"))
                            ? "67095"
                            : reader.GetString(reader.GetOrdinal("Katalog"))
                    };

                    pozycje.Add(pozycja);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania pozycji zamówienia: {ex.Message}");
                throw;
            }

            return pozycje;
        }

        /// <summary>
        /// Pobiera listę handlowców do filtrowania
        /// </summary>
        public async Task<List<string>> PobierzHandlowcowAsync()
        {
            var handlowcy = new List<string> { "— Wszyscy —" };

            try
            {
                await using var cn = new SqlConnection(_connectionStringLibraNet);
                await cn.OpenAsync();

                var sql = @"
                    SELECT DISTINCT HandlowiecNazwa
                    FROM MapowanieHandlowcow
                    WHERE CzyAktywny = 1
                    ORDER BY HandlowiecNazwa";

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                        handlowcy.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania handlowców: {ex.Message}");
            }

            return handlowcy;
        }

        /// <summary>
        /// Pobiera historię zmian zamówienia
        /// </summary>
        public async Task<List<HistoriaZmianZamowienia>> PobierzHistorieZamowieniaAsync(int zamowienieId)
        {
            var historia = new List<HistoriaZmianZamowienia>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringLibraNet);
                await cn.OpenAsync();

                // Najpierw sprawdź czy tabela istnieje
                var checkSql = @"
                    SELECT COUNT(*) FROM sys.objects
                    WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U')";

                await using var checkCmd = new SqlCommand(checkSql, cn);
                var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (!tableExists)
                {
                    // Tabela nie istnieje - zwróć pustą listę
                    return historia;
                }

                var sql = @"
                    SELECT
                        Id,
                        ZamowienieId,
                        TypZmiany,
                        PoleZmienione,
                        WartoscPoprzednia,
                        WartoscNowa,
                        Uzytkownik,
                        UzytkownikNazwa,
                        DataZmiany,
                        OpisZmiany
                    FROM HistoriaZmianZamowien
                    WHERE ZamowienieId = @ZamowienieId
                    ORDER BY DataZmiany DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamowienieId", zamowienieId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var zmiana = new HistoriaZmianZamowienia
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        ZamowienieId = reader.GetInt32(reader.GetOrdinal("ZamowienieId")),
                        TypZmiany = reader.IsDBNull(reader.GetOrdinal("TypZmiany"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("TypZmiany")),
                        PoleZmienione = reader.IsDBNull(reader.GetOrdinal("PoleZmienione"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("PoleZmienione")),
                        WartoscPoprzednia = reader.IsDBNull(reader.GetOrdinal("WartoscPoprzednia"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("WartoscPoprzednia")),
                        WartoscNowa = reader.IsDBNull(reader.GetOrdinal("WartoscNowa"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("WartoscNowa")),
                        Uzytkownik = reader.IsDBNull(reader.GetOrdinal("Uzytkownik"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("Uzytkownik")),
                        UzytkownikNazwa = reader.IsDBNull(reader.GetOrdinal("UzytkownikNazwa"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("UzytkownikNazwa")),
                        DataZmiany = reader.GetDateTime(reader.GetOrdinal("DataZmiany")),
                        OpisZmiany = reader.IsDBNull(reader.GetOrdinal("OpisZmiany"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("OpisZmiany"))
                    };

                    historia.Add(zmiana);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania historii zmian: {ex.Message}");
            }

            return historia;
        }

        /// <summary>
        /// Loguje zmianę w zamówieniu
        /// </summary>
        public async Task<int> LogujZmianeAsync(
            int zamowienieId,
            string typZmiany,
            string uzytkownik,
            string uzytkownikNazwa = null,
            string poleZmienione = null,
            string wartoscPoprzednia = null,
            string wartoscNowa = null,
            string opisZmiany = null)
        {
            try
            {
                await using var cn = new SqlConnection(_connectionStringLibraNet);
                await cn.OpenAsync();

                // Sprawdź czy tabela istnieje - jeśli nie, utwórz ją
                await EnsureHistoryTableExistsAsync(cn);

                var sql = @"
                    INSERT INTO HistoriaZmianZamowien
                        (ZamowienieId, TypZmiany, PoleZmienione, WartoscPoprzednia,
                         WartoscNowa, Uzytkownik, UzytkownikNazwa, OpisZmiany, NazwaKomputera)
                    VALUES
                        (@ZamowienieId, @TypZmiany, @PoleZmienione, @WartoscPoprzednia,
                         @WartoscNowa, @Uzytkownik, @UzytkownikNazwa, @OpisZmiany, @NazwaKomputera);
                    SELECT SCOPE_IDENTITY();";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@ZamowienieId", zamowienieId);
                cmd.Parameters.AddWithValue("@TypZmiany", typZmiany);
                cmd.Parameters.AddWithValue("@PoleZmienione", (object)poleZmienione ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WartoscPoprzednia", (object)wartoscPoprzednia ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@WartoscNowa", (object)wartoscNowa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Uzytkownik", uzytkownik);
                cmd.Parameters.AddWithValue("@UzytkownikNazwa", (object)uzytkownikNazwa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OpisZmiany", (object)opisZmiany ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NazwaKomputera", Environment.MachineName);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd logowania zmiany: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Tworzy tabelę historii zmian jeśli nie istnieje
        /// </summary>
        private async Task EnsureHistoryTableExistsAsync(SqlConnection cn)
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [dbo].[HistoriaZmianZamowien](
                        [Id] INT IDENTITY(1,1) PRIMARY KEY,
                        [ZamowienieId] INT NOT NULL,
                        [TypZmiany] NVARCHAR(50) NOT NULL,
                        [PoleZmienione] NVARCHAR(100) NULL,
                        [WartoscPoprzednia] NVARCHAR(MAX) NULL,
                        [WartoscNowa] NVARCHAR(MAX) NULL,
                        [Uzytkownik] NVARCHAR(50) NOT NULL,
                        [UzytkownikNazwa] NVARCHAR(200) NULL,
                        [DataZmiany] DATETIME NOT NULL DEFAULT GETDATE(),
                        [OpisZmiany] NVARCHAR(MAX) NULL,
                        [DodatkoweInfo] NVARCHAR(MAX) NULL,
                        [AdresIP] NVARCHAR(50) NULL,
                        [NazwaKomputera] NVARCHAR(100) NULL
                    );

                    CREATE NONCLUSTERED INDEX [IX_HistoriaZmianZamowien_ZamowienieId]
                    ON [dbo].[HistoriaZmianZamowien] ([ZamowienieId])
                    INCLUDE ([TypZmiany], [DataZmiany], [Uzytkownik]);

                    CREATE NONCLUSTERED INDEX [IX_HistoriaZmianZamowien_DataZmiany]
                    ON [dbo].[HistoriaZmianZamowien] ([DataZmiany] DESC)
                    INCLUDE ([ZamowienieId], [TypZmiany], [Uzytkownik]);
                END";

            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Pobiera ostatnie zmiany ze wszystkich zamówień
        /// </summary>
        public async Task<List<HistoriaZmianZamowienia>> PobierzOstatnieZmianyAsync(int iloscDni = 7, int limit = 100)
        {
            var historia = new List<HistoriaZmianZamowienia>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringLibraNet);
                await cn.OpenAsync();

                // Sprawdź czy tabela istnieje
                var checkSql = @"
                    SELECT COUNT(*) FROM sys.objects
                    WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U')";

                await using var checkCmd = new SqlCommand(checkSql, cn);
                var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                if (!tableExists)
                    return historia;

                var sql = @"
                    SELECT TOP (@Limit)
                        Id,
                        ZamowienieId,
                        TypZmiany,
                        PoleZmienione,
                        WartoscPoprzednia,
                        WartoscNowa,
                        Uzytkownik,
                        UzytkownikNazwa,
                        DataZmiany,
                        OpisZmiany
                    FROM HistoriaZmianZamowien
                    WHERE DataZmiany >= DATEADD(DAY, -@IloscDni, GETDATE())
                    ORDER BY DataZmiany DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Limit", limit);
                cmd.Parameters.AddWithValue("@IloscDni", iloscDni);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var zmiana = new HistoriaZmianZamowienia
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        ZamowienieId = reader.GetInt32(reader.GetOrdinal("ZamowienieId")),
                        TypZmiany = reader.IsDBNull(reader.GetOrdinal("TypZmiany"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("TypZmiany")),
                        PoleZmienione = reader.IsDBNull(reader.GetOrdinal("PoleZmienione"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("PoleZmienione")),
                        WartoscPoprzednia = reader.IsDBNull(reader.GetOrdinal("WartoscPoprzednia"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("WartoscPoprzednia")),
                        WartoscNowa = reader.IsDBNull(reader.GetOrdinal("WartoscNowa"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("WartoscNowa")),
                        Uzytkownik = reader.IsDBNull(reader.GetOrdinal("Uzytkownik"))
                            ? ""
                            : reader.GetString(reader.GetOrdinal("Uzytkownik")),
                        UzytkownikNazwa = reader.IsDBNull(reader.GetOrdinal("UzytkownikNazwa"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("UzytkownikNazwa")),
                        DataZmiany = reader.GetDateTime(reader.GetOrdinal("DataZmiany")),
                        OpisZmiany = reader.IsDBNull(reader.GetOrdinal("OpisZmiany"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("OpisZmiany"))
                    };

                    historia.Add(zmiana);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania ostatnich zmian: {ex.Message}");
            }

            return historia;
        }

        /// <summary>
        /// Aktualizuje notatkę zamówienia i loguje zmianę
        /// </summary>
        public async Task<bool> AktualizujNotatkeAsync(int zamowienieId, string nowaNotatka, string uzytkownik, string uzytkownikNazwa = null)
        {
            try
            {
                // Najpierw pobierz starą notatkę
                string staraNotatka = "";

                await using (var cn = new SqlConnection(_connectionStringHandel))
                {
                    await cn.OpenAsync();

                    var selectSql = "SELECT Notatka FROM ZamowieniaMieso WHERE ID = @Id";
                    await using var selectCmd = new SqlCommand(selectSql, cn);
                    selectCmd.Parameters.AddWithValue("@Id", zamowienieId);

                    var result = await selectCmd.ExecuteScalarAsync();
                    staraNotatka = result?.ToString() ?? "";

                    // Aktualizuj notatkę
                    var updateSql = @"
                        UPDATE ZamowieniaMieso
                        SET Notatka = @Notatka,
                            DataModyfikacji = GETDATE(),
                            ZmodyfikowanyPrzez = @Uzytkownik
                        WHERE ID = @Id";

                    await using var updateCmd = new SqlCommand(updateSql, cn);
                    updateCmd.Parameters.AddWithValue("@Id", zamowienieId);
                    updateCmd.Parameters.AddWithValue("@Notatka", nowaNotatka ?? "");
                    updateCmd.Parameters.AddWithValue("@Uzytkownik", uzytkownik);

                    await updateCmd.ExecuteNonQueryAsync();
                }

                // Zaloguj zmianę
                await LogujZmianeAsync(
                    zamowienieId,
                    "EDYCJA",
                    uzytkownik,
                    uzytkownikNazwa,
                    "Notatka",
                    staraNotatka,
                    nowaNotatka,
                    $"Zmieniono notatkę zamówienia #{zamowienieId}"
                );

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd aktualizacji notatki: {ex.Message}");
                return false;
            }
        }
    }
}
