using Kalendarz1.Pasza.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Pasza.Services
{
    /// <summary>
    /// Service modulu "Zakup Paszy": laduje paszarnie/hodowcow/towary z Symfonii (HANDEL),
    /// cennik marz + kolejka importu z LibraNet, dedup po stronie Symfonii.
    /// </summary>
    public class PaszaService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // ════════════════ LOOKUP-y z Symfonii (HANDEL) ════════════════

        public async Task<List<KontrahentSymfonia>> GetKontrahenciAsync(string filtr = "")
        {
            var lista = new List<KontrahentSymfonia>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            string sql = string.IsNullOrWhiteSpace(filtr)
                ? @"SELECT Id, ISNULL(Shortcut,'') AS Shortcut, ISNULL(Name,'') AS Name, ISNULL(NIP,'') AS NIP
                    FROM SSCommon.STContractors ORDER BY Name"
                : @"SELECT Id, ISNULL(Shortcut,'') AS Shortcut, ISNULL(Name,'') AS Name, ISNULL(NIP,'') AS NIP
                    FROM SSCommon.STContractors
                    WHERE Shortcut LIKE @f OR Name LIKE @f
                       OR REPLACE(REPLACE(ISNULL(NIP,''),'-',''),' ','') LIKE @f
                    ORDER BY Name";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            if (!string.IsNullOrWhiteSpace(filtr))
                cmd.Parameters.AddWithValue("@f", "%" + filtr.Trim() + "%");
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new KontrahentSymfonia
                {
                    Id = Convert.ToInt32(rdr["Id"]),
                    Shortcut = rdr["Shortcut"]?.ToString()?.Trim() ?? "",
                    Name = rdr["Name"]?.ToString()?.Trim() ?? "",
                    NIP = rdr["NIP"]?.ToString()?.Trim() ?? ""
                });
            }
            return lista;
        }

        public async Task<List<TowarPasza>> GetTowaryAsync(string filtr = "")
        {
            var lista = new List<TowarPasza>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            string sql = string.IsNullOrWhiteSpace(filtr)
                ? @"SELECT id, ISNULL(kod,'') AS kod, ISNULL(nazwa,'') AS nazwa, ISNULL(jm,'') AS jm
                    FROM HM.TW WHERE ISNULL(aktywny,0) = 1 ORDER BY nazwa"
                : @"SELECT id, ISNULL(kod,'') AS kod, ISNULL(nazwa,'') AS nazwa, ISNULL(jm,'') AS jm
                    FROM HM.TW WHERE ISNULL(aktywny,0) = 1 AND (kod LIKE @f OR nazwa LIKE @f)
                    ORDER BY nazwa";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            if (!string.IsNullOrWhiteSpace(filtr))
                cmd.Parameters.AddWithValue("@f", "%" + filtr.Trim() + "%");
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new TowarPasza
                {
                    Id = Convert.ToInt32(rdr["id"]),
                    Kod = rdr["kod"]?.ToString()?.Trim() ?? "",
                    Nazwa = rdr["nazwa"]?.ToString()?.Trim() ?? "",
                    Jm = rdr["jm"]?.ToString()?.Trim() ?? "t"
                });
            }
            return lista;
        }

        /// <summary>Sprawdza czy w HM.DK juz jest niezanulowany FVZ od tej paszarni na podanej dacie.
        /// Bezpieczne — przy błędzie SQL zwraca pusty wynik (nie wywala UI).</summary>
        public async Task<DedupResult> SprawdzDuplikatFvAsync(string paszarniaKhKod, DateTime dataWyst)
        {
            var wynik = new DedupResult();
            if (string.IsNullOrWhiteSpace(paszarniaKhKod)) return wynik;

            try
            {
                using var conn = new SqlConnection(ConnHandel);
                await conn.OpenAsync();
                string sql = @"
                    SELECT TOP 1 ISNULL(dk.kod,'') AS NrFv, dk.data
                    FROM HM.DK dk
                    JOIN SSCommon.STContractors k ON dk.khid = k.Id
                    WHERE dk.typ_dk = 'FVZ'
                      AND ISNULL(dk.anulowany,0) = 0
                      AND k.Shortcut = @kh
                      AND CAST(dk.data AS DATE) = @d
                    ORDER BY dk.id DESC";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                cmd.Parameters.AddWithValue("@kh", paszarniaKhKod);
                cmd.Parameters.AddWithValue("@d", dataWyst.Date);
                using var rdr = await cmd.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    wynik.NrIstniejacejFv = rdr["NrFv"]?.ToString() ?? "";
                    if (rdr["data"] != DBNull.Value)
                        wynik.DataIstniejacej = Convert.ToDateTime(rdr["data"]);
                }
            }
            catch
            {
                // Cisza — niedostępność Symfonii nie powinna blokować pracy w WPF
            }
            return wynik;
        }

        // ════════════════ CENNIK (LibraNet.PaszaCennik) ════════════════

        /// <summary>Zwraca marze obowiazujaca dla pary (hodowca, towar) na dany dzien — albo null gdy brak wpisu.</summary>
        public async Task<decimal?> GetMarzaZCennikaAsync(string hodowcaKhKod, string towarKod, DateTime dataWyst)
        {
            if (string.IsNullOrWhiteSpace(hodowcaKhKod) || string.IsNullOrWhiteSpace(towarKod))
                return null;

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                SELECT TOP 1 MarzaKwota
                FROM dbo.PaszaCennik
                WHERE Aktywny = 1
                  AND HodowcaKhKod = @h
                  AND TowarKod = @t
                  AND DataOd <= @d
                  AND (DataDo IS NULL OR DataDo >= @d)
                ORDER BY DataOd DESC";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            cmd.Parameters.AddWithValue("@h", hodowcaKhKod);
            cmd.Parameters.AddWithValue("@t", towarKod);
            cmd.Parameters.AddWithValue("@d", dataWyst.Date);
            var v = await cmd.ExecuteScalarAsync();
            return v == null || v == DBNull.Value ? null : (decimal?)Convert.ToDecimal(v);
        }

        public async Task<List<CennikItem>> GetCennikAsync(bool tylkoAktywne = true)
        {
            var lista = new List<CennikItem>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                SELECT Id, HodowcaKhKod, HodowcaNazwa, TowarKod, TowarNazwa, MarzaKwota,
                       DataOd, DataDo, Aktywny, ISNULL(Uwagi,'') AS Uwagi,
                       ISNULL(UtworzonoPrzez,'') AS UtworzonoPrzez, UtworzonoKiedy
                FROM dbo.PaszaCennik" +
                (tylkoAktywne ? " WHERE Aktywny = 1" : "") +
                " ORDER BY HodowcaNazwa, TowarNazwa, DataOd DESC";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new CennikItem
                {
                    Id = Convert.ToInt32(rdr["Id"]),
                    HodowcaKhKod = rdr["HodowcaKhKod"]?.ToString() ?? "",
                    HodowcaNazwa = rdr["HodowcaNazwa"]?.ToString() ?? "",
                    TowarKod = rdr["TowarKod"]?.ToString() ?? "",
                    TowarNazwa = rdr["TowarNazwa"]?.ToString() ?? "",
                    MarzaKwota = Convert.ToDecimal(rdr["MarzaKwota"]),
                    DataOd = Convert.ToDateTime(rdr["DataOd"]),
                    DataDo = rdr["DataDo"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rdr["DataDo"]),
                    Aktywny = Convert.ToBoolean(rdr["Aktywny"]),
                    Uwagi = rdr["Uwagi"]?.ToString() ?? "",
                    UtworzonoPrzez = rdr["UtworzonoPrzez"]?.ToString() ?? "",
                    UtworzonoKiedy = Convert.ToDateTime(rdr["UtworzonoKiedy"])
                });
            }
            return lista;
        }

        public async Task<int> UpsertCennikAsync(CennikItem item, string userId)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql;
            if (item.Id == 0)
            {
                sql = @"
                    INSERT INTO dbo.PaszaCennik
                        (HodowcaKhKod, HodowcaNazwa, TowarKod, TowarNazwa, MarzaKwota,
                         DataOd, DataDo, Aktywny, Uwagi, UtworzonoPrzez)
                    OUTPUT INSERTED.Id
                    VALUES (@h, @hn, @t, @tn, @m, @do, @dd, @a, @u, @up)";
            }
            else
            {
                sql = @"
                    UPDATE dbo.PaszaCennik SET
                        HodowcaKhKod = @h, HodowcaNazwa = @hn,
                        TowarKod = @t,    TowarNazwa = @tn,
                        MarzaKwota = @m,
                        DataOd = @do, DataDo = @dd, Aktywny = @a, Uwagi = @u,
                        ZmienionoPrzez = @up, ZmienionoKiedy = GETDATE()
                    OUTPUT INSERTED.Id
                    WHERE Id = @id";
            }
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@h", item.HodowcaKhKod);
            cmd.Parameters.AddWithValue("@hn", item.HodowcaNazwa);
            cmd.Parameters.AddWithValue("@t", item.TowarKod);
            cmd.Parameters.AddWithValue("@tn", item.TowarNazwa);
            cmd.Parameters.AddWithValue("@m", item.MarzaKwota);
            cmd.Parameters.AddWithValue("@do", item.DataOd.Date);
            cmd.Parameters.AddWithValue("@dd", (object?)item.DataDo?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", item.Aktywny);
            cmd.Parameters.AddWithValue("@u", (object?)item.Uwagi ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@up", userId);
            if (item.Id != 0) cmd.Parameters.AddWithValue("@id", item.Id);
            var r = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(r);
        }

        public async Task UsunCennikAsync(int id)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM dbo.PaszaCennik WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // ════════════════ KOLEJKA (LibraNet.PaszaImportQueue) ════════════════

        public async Task<int> DodajDoKolejkiAsync(KolejkaItem item, string userId)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                INSERT INTO dbo.PaszaImportQueue
                    (PaszarniaKhKod, PaszarniaNazwa, HodowcaKhKod, HodowcaNazwa,
                     TowarKod, TowarNazwa, TowarJm,
                     Ilosc, CenaZakNetto, MarzaKwota, VatProc,
                     NumerObcy, DataWystawienia, TerminDni,
                     Status, UtworzonoPrzez)
                OUTPUT INSERTED.Id
                VALUES (@psz, @pszn, @hod, @hodn,
                        @twk, @twn, @jm,
                        @il, @cz, @ma, @v,
                        @no, @dw, @td,
                        'NOWY', @u)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@psz", item.PaszarniaKhKod);
            cmd.Parameters.AddWithValue("@pszn", item.PaszarniaNazwa);
            cmd.Parameters.AddWithValue("@hod", item.HodowcaKhKod);
            cmd.Parameters.AddWithValue("@hodn", item.HodowcaNazwa);
            cmd.Parameters.AddWithValue("@twk", item.TowarKod);
            cmd.Parameters.AddWithValue("@twn", item.TowarNazwa);
            cmd.Parameters.AddWithValue("@jm", item.TowarJm);
            cmd.Parameters.AddWithValue("@il", item.Ilosc);
            cmd.Parameters.AddWithValue("@cz", item.CenaZakNetto);
            cmd.Parameters.AddWithValue("@ma", item.MarzaKwota);
            cmd.Parameters.AddWithValue("@v", item.VatProc);
            cmd.Parameters.AddWithValue("@no", (object?)item.NumerObcy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dw", item.DataWystawienia.Date);
            cmd.Parameters.AddWithValue("@td", item.TerminDni);
            cmd.Parameters.AddWithValue("@u", userId);
            var r = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(r);
        }

        public async Task<List<KolejkaItem>> GetKolejkaAsync(string? statusFilter = null)
        {
            var lista = new List<KolejkaItem>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                SELECT Id, PaszarniaKhKod, PaszarniaNazwa, HodowcaKhKod, HodowcaNazwa,
                       TowarKod, TowarNazwa, TowarJm,
                       Ilosc, CenaZakNetto, MarzaKwota, VatProc,
                       ISNULL(NumerObcy,'') AS NumerObcy, DataWystawienia, TerminDni,
                       CenaSprzNetto, CenaSprzBrutto,
                       WartoscZakNetto, WartoscSprzNetto, WartoscSprzBrutto, MarzaLaczna,
                       Status,
                       ISNULL(NrPZ,'') AS NrPZ, ISNULL(NrFVZ,'') AS NrFVZ,
                       ISNULL(NrWZ,'') AS NrWZ, ISNULL(NrFPP,'') AS NrFPP,
                       ISNULL(BladKomunikat,'') AS BladKomunikat,
                       ISNULL(UtworzonoPrzez,'') AS UtworzonoPrzez, UtworzonoKiedy, ImportowanoKiedy
                FROM dbo.PaszaImportQueue" +
                (string.IsNullOrEmpty(statusFilter) ? "" : " WHERE Status = @s") +
                " ORDER BY UtworzonoKiedy DESC";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            if (!string.IsNullOrEmpty(statusFilter))
                cmd.Parameters.AddWithValue("@s", statusFilter);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new KolejkaItem
                {
                    Id = Convert.ToInt32(rdr["Id"]),
                    PaszarniaKhKod = rdr["PaszarniaKhKod"]?.ToString() ?? "",
                    PaszarniaNazwa = rdr["PaszarniaNazwa"]?.ToString() ?? "",
                    HodowcaKhKod = rdr["HodowcaKhKod"]?.ToString() ?? "",
                    HodowcaNazwa = rdr["HodowcaNazwa"]?.ToString() ?? "",
                    TowarKod = rdr["TowarKod"]?.ToString() ?? "",
                    TowarNazwa = rdr["TowarNazwa"]?.ToString() ?? "",
                    TowarJm = rdr["TowarJm"]?.ToString() ?? "t",
                    Ilosc = Convert.ToDecimal(rdr["Ilosc"]),
                    CenaZakNetto = Convert.ToDecimal(rdr["CenaZakNetto"]),
                    MarzaKwota = Convert.ToDecimal(rdr["MarzaKwota"]),
                    VatProc = Convert.ToDecimal(rdr["VatProc"]),
                    NumerObcy = rdr["NumerObcy"]?.ToString() ?? "",
                    DataWystawienia = Convert.ToDateTime(rdr["DataWystawienia"]),
                    TerminDni = Convert.ToInt32(rdr["TerminDni"]),
                    CenaSprzNetto = Convert.ToDecimal(rdr["CenaSprzNetto"]),
                    CenaSprzBrutto = Convert.ToDecimal(rdr["CenaSprzBrutto"]),
                    WartoscZakNetto = Convert.ToDecimal(rdr["WartoscZakNetto"]),
                    WartoscSprzNetto = Convert.ToDecimal(rdr["WartoscSprzNetto"]),
                    WartoscSprzBrutto = Convert.ToDecimal(rdr["WartoscSprzBrutto"]),
                    MarzaLaczna = Convert.ToDecimal(rdr["MarzaLaczna"]),
                    Status = rdr["Status"]?.ToString() ?? "",
                    NrPZ = rdr["NrPZ"]?.ToString() ?? "",
                    NrFVZ = rdr["NrFVZ"]?.ToString() ?? "",
                    NrWZ = rdr["NrWZ"]?.ToString() ?? "",
                    NrFPP = rdr["NrFPP"]?.ToString() ?? "",
                    BladKomunikat = rdr["BladKomunikat"]?.ToString() ?? "",
                    UtworzonoPrzez = rdr["UtworzonoPrzez"]?.ToString() ?? "",
                    UtworzonoKiedy = Convert.ToDateTime(rdr["UtworzonoKiedy"]),
                    ImportowanoKiedy = rdr["ImportowanoKiedy"] == DBNull.Value
                        ? (DateTime?)null : Convert.ToDateTime(rdr["ImportowanoKiedy"])
                });
            }
            return lista;
        }

        public async Task AnulujKolejkaAsync(int id, string userId)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                UPDATE dbo.PaszaImportQueue
                SET Status = 'ANULOWANE',
                    AnulowanoPrzez = @u, AnulowanoKiedy = GETDATE()
                WHERE Id = @id AND Status = 'NOWY'";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>Wraca BLAD->NOWY zeby Symfonia mogla sprobowac ponownie.</summary>
        public async Task WyslijPonownieAsync(int id)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                UPDATE dbo.PaszaImportQueue
                SET Status = 'NOWY', BladKomunikat = NULL
                WHERE Id = @id AND Status = 'BLAD'";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // ════════════════ KATALOGI HM.TW (do filtra przy dodawaniu towaru) ════════════════

        public async Task<List<KatalogInfo>> GetKatalogiAsync()
        {
            // PROSTE i niezawodne: katalog z HM.TW + liczba + przykładowa nazwa towaru (do identyfikacji).
            // Bez HM.KT (niepewne czy istnieje w tej edycji Sage). Bez try/catch — to powinno działać wszędzie.
            var lista = new List<KatalogInfo>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            string sql = @"
                SELECT tw.katalog AS Id,
                       COUNT(*)   AS Liczba,
                       MIN(tw.nazwa) AS SampleNazwa
                FROM HM.TW tw
                WHERE ISNULL(tw.aktywny,0) = 1
                GROUP BY tw.katalog
                ORDER BY COUNT(*) DESC";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new KatalogInfo
                {
                    Id = rdr["Id"] == DBNull.Value ? 0 : Convert.ToInt32(rdr["Id"]),
                    LiczbaTowarow = Convert.ToInt32(rdr["Liczba"]),
                    SampleNazwa = rdr["SampleNazwa"]?.ToString() ?? ""
                });
            }
            return lista;
        }

        /// <summary>Towary w wybranym katalogu (lub wszystkie gdy katalogId == null). Filtr po nazwie/kod.</summary>
        public async Task<List<TowarPasza>> GetTowaryByKatalogAsync(int? katalogId, string filtr = "")
        {
            var lista = new List<TowarPasza>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            var w = new System.Text.StringBuilder("WHERE ISNULL(aktywny,0) = 1");
            if (katalogId.HasValue) w.Append(" AND katalog = @kat");
            if (!string.IsNullOrWhiteSpace(filtr)) w.Append(" AND (kod LIKE @f OR nazwa LIKE @f)");

            string sql = $@"
                SELECT id, ISNULL(kod,'') AS kod, ISNULL(nazwa,'') AS nazwa, ISNULL(jm,'') AS jm
                FROM HM.TW {w} ORDER BY nazwa";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            if (katalogId.HasValue) cmd.Parameters.AddWithValue("@kat", katalogId.Value);
            if (!string.IsNullOrWhiteSpace(filtr)) cmd.Parameters.AddWithValue("@f", "%" + filtr.Trim() + "%");
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new TowarPasza
                {
                    Id = Convert.ToInt32(rdr["id"]),
                    Kod = rdr["kod"]?.ToString()?.Trim() ?? "",
                    Nazwa = rdr["nazwa"]?.ToString()?.Trim() ?? "",
                    Jm = rdr["jm"]?.ToString()?.Trim() ?? "t"
                });
            }
            return lista;
        }

        // ════════════════ SŁOWNIK PASZARNI (LibraNet.PaszaPaszarnie) ════════════════

        public async Task<List<PaszarniaSlownik>> GetPaszarnieSlownikAsync(bool tylkoAktywne = true)
        {
            var lista = new List<PaszarniaSlownik>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                SELECT Id, KhKod, Nazwa, ISNULL(NIP,'') AS NIP, Kolejnosc, Aktywny, ISNULL(Notatki,'') AS Notatki, UtworzonoKiedy
                FROM dbo.PaszaPaszarnie" + (tylkoAktywne ? " WHERE Aktywny = 1" : "") +
                " ORDER BY Kolejnosc, Nazwa";
            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new PaszarniaSlownik
                {
                    Id = Convert.ToInt32(rdr["Id"]),
                    KhKod = rdr["KhKod"]?.ToString() ?? "",
                    Nazwa = rdr["Nazwa"]?.ToString() ?? "",
                    NIP = rdr["NIP"]?.ToString() ?? "",
                    Kolejnosc = Convert.ToInt32(rdr["Kolejnosc"]),
                    Aktywny = Convert.ToBoolean(rdr["Aktywny"]),
                    Notatki = rdr["Notatki"]?.ToString() ?? "",
                    UtworzonoKiedy = Convert.ToDateTime(rdr["UtworzonoKiedy"])
                });
            }
            return lista;
        }

        /// <summary>Dodaje paszarnię do słownika (z snapshotem nazwy/NIP z STContractors). Idempotentne — pomija duplikaty KhKod.</summary>
        public async Task<int> DodajPaszarnieDoSlownikaAsync(KontrahentSymfonia k, string userId)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.PaszaPaszarnie WHERE KhKod = @kh)
                BEGIN
                    INSERT INTO dbo.PaszaPaszarnie (KhKod, Nazwa, NIP, UtworzonoPrzez)
                    VALUES (@kh, @nazwa, @nip, @u);
                    SELECT SCOPE_IDENTITY();
                END
                ELSE
                BEGIN
                    -- jeśli był nieaktywny → reaktywuj
                    UPDATE dbo.PaszaPaszarnie SET Aktywny = 1, Nazwa = @nazwa, NIP = @nip WHERE KhKod = @kh;
                    SELECT Id FROM dbo.PaszaPaszarnie WHERE KhKod = @kh;
                END";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@kh", k.Shortcut);
            cmd.Parameters.AddWithValue("@nazwa", k.Name);
            cmd.Parameters.AddWithValue("@nip", (object?)k.NIP ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", userId);
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
        }

        public async Task UsunPaszarnieZeSlownikaAsync(int id)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM dbo.PaszaPaszarnie WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>Lista paszarni dla ComboBoxa w Kreatorze (aktywne, jako KontrahentSymfonia).</summary>
        public async Task<List<KontrahentSymfonia>> GetPaszarnieDoCBAsync()
        {
            var src = await GetPaszarnieSlownikAsync(tylkoAktywne: true);
            return src.Select(p => p.ToKontrahent()).ToList();
        }

        // ════════════════ SŁOWNIK TOWARÓW (LibraNet.PaszaTowary) ════════════════

        public async Task<List<TowarSlownik>> GetTowarySlownikAsync(bool tylkoAktywne = true)
        {
            var lista = new List<TowarSlownik>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                SELECT Id, TowarKod, TowarNazwa, Jm, KatalogId, ISNULL(KatalogNazwa,'') AS KatalogNazwa,
                       Kolejnosc, Aktywny, ISNULL(Notatki,'') AS Notatki, UtworzonoKiedy
                FROM dbo.PaszaTowary" + (tylkoAktywne ? " WHERE Aktywny = 1" : "") +
                " ORDER BY Kolejnosc, TowarNazwa";
            using var cmd = new SqlCommand(sql, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new TowarSlownik
                {
                    Id = Convert.ToInt32(rdr["Id"]),
                    TowarKod = rdr["TowarKod"]?.ToString() ?? "",
                    TowarNazwa = rdr["TowarNazwa"]?.ToString() ?? "",
                    Jm = rdr["Jm"]?.ToString() ?? "t",
                    KatalogId = rdr["KatalogId"] == DBNull.Value ? (int?)null : Convert.ToInt32(rdr["KatalogId"]),
                    KatalogNazwa = rdr["KatalogNazwa"]?.ToString() ?? "",
                    Kolejnosc = Convert.ToInt32(rdr["Kolejnosc"]),
                    Aktywny = Convert.ToBoolean(rdr["Aktywny"]),
                    Notatki = rdr["Notatki"]?.ToString() ?? "",
                    UtworzonoKiedy = Convert.ToDateTime(rdr["UtworzonoKiedy"])
                });
            }
            return lista;
        }

        /// <summary>Dodaje towar do słownika (idempotentnie, ze snapshotem nazwy katalogu).</summary>
        public async Task<int> DodajTowarDoSlownikaAsync(TowarPasza t, KatalogInfo? katalog, string userId)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            string sql = @"
                IF NOT EXISTS (SELECT 1 FROM dbo.PaszaTowary WHERE TowarKod = @kod)
                BEGIN
                    INSERT INTO dbo.PaszaTowary (TowarKod, TowarNazwa, Jm, KatalogId, KatalogNazwa, UtworzonoPrzez)
                    VALUES (@kod, @nazwa, @jm, @kid, @kn, @u);
                    SELECT SCOPE_IDENTITY();
                END
                ELSE
                BEGIN
                    UPDATE dbo.PaszaTowary
                    SET Aktywny = 1, TowarNazwa = @nazwa, Jm = @jm, KatalogId = @kid, KatalogNazwa = @kn
                    WHERE TowarKod = @kod;
                    SELECT Id FROM dbo.PaszaTowary WHERE TowarKod = @kod;
                END";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@kod", t.Kod);
            cmd.Parameters.AddWithValue("@nazwa", t.Nazwa);
            cmd.Parameters.AddWithValue("@jm", t.Jm);
            cmd.Parameters.AddWithValue("@kid", (object?)katalog?.Id ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@kn", (object?)katalog?.Nazwa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@u", userId);
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
        }

        public async Task UsunTowarZeSlownikaAsync(int id)
        {
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("DELETE FROM dbo.PaszaTowary WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<TowarPasza>> GetTowaryDoCBAsync()
        {
            var src = await GetTowarySlownikAsync(tylkoAktywne: true);
            return src.Select(t => t.ToTowar()).ToList();
        }

        // ════════════════ HODOWCY KUPUJĄCY NASZE TOWARY (ostatnie 12 mc) ════════════════

        /// <summary>
        /// Hodowcy którzy w ostatnich N miesiącach kupowali którykolwiek towar ze słownika PaszaTowary.
        /// Filtruje faktury sprzedaży (FPP/FVS/FV) z HM.DK + JOIN HM.DP/TW.
        /// </summary>
        public async Task<List<KontrahentSymfonia>> GetHodowcyKupujacychAsync(int monthsBack = 12)
        {
            // 1) Pobierz kody towarów ze słownika (LibraNet)
            var kody = (await GetTowarySlownikAsync(tylkoAktywne: true))
                       .Select(t => t.TowarKod)
                       .Where(k => !string.IsNullOrWhiteSpace(k))
                       .Distinct()
                       .ToList();

            if (kody.Count == 0) return new List<KontrahentSymfonia>();

            // 2) Buduj dynamiczny IN-clause z parametrami
            var paramNames = new List<string>();
            for (int i = 0; i < kody.Count; i++) paramNames.Add("@k" + i);
            string inClause = string.Join(",", paramNames);

            var lista = new List<KontrahentSymfonia>();
            using var conn = new SqlConnection(ConnHandel);
            await conn.OpenAsync();
            // UWAGA: przy SELECT DISTINCT, ORDER BY musi używać aliasów/wyrażeń z SELECT list,
            // nie surowej kolumny k.Name (inaczej: 'ORDER BY items must appear in the select list').
            string sql = $@"
                SELECT DISTINCT k.Id,
                       ISNULL(k.Shortcut,'') AS Shortcut,
                       ISNULL(k.Name,'')     AS Name,
                       ISNULL(k.NIP,'')      AS NIP
                FROM HM.DK dk
                JOIN SSCommon.STContractors k ON dk.khid = k.Id
                JOIN HM.DP p ON p.super = dk.id
                JOIN HM.TW t ON t.id = p.idtw
                WHERE dk.typ_dk IN ('FPP','FVS','FV','FVE','PAR')
                  AND ISNULL(dk.anulowany,0) = 0
                  AND dk.data >= DATEADD(MONTH, -@m, GETDATE())
                  AND t.kod IN ({inClause})
                ORDER BY Name";
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@m", monthsBack);
            for (int i = 0; i < kody.Count; i++)
                cmd.Parameters.AddWithValue(paramNames[i], kody[i]);

            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                lista.Add(new KontrahentSymfonia
                {
                    Id = Convert.ToInt32(rdr["Id"]),
                    Shortcut = rdr["Shortcut"]?.ToString()?.Trim() ?? "",
                    Name = rdr["Name"]?.ToString()?.Trim() ?? "",
                    NIP = rdr["NIP"]?.ToString()?.Trim() ?? ""
                });
            }
            return lista;
        }
    }
}
