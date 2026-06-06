using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kalendarz1.Kontrakty.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Kontrakty.Services
{
    /// <summary>
    /// KontraktyService — operacje zapisu i odczytu szczegółów (CRUD + wersjonowanie).
    /// Wersjonowanie: nagłówek dbo.Kontrakty + N wersji dbo.KontraktyWersje (IsAktualna=1 = bieżąca).
    /// </summary>
    public partial class KontraktyService
    {
        // ─── Picker hodowców (dbo.Dostawcy) ───────────────────────────────────
        public async Task<List<HodowcaPicker>> GetHodowcyAsync(string? szukaj)
        {
            var lista = new List<HodowcaPicker>();
            const string sql = @"
SELECT TOP (60)
    ID,
    ISNULL([Name], ISNULL(ShortName,'(brak nazwy)')) AS Nazwa,
    ISNULL(Nip,'') AS Nip,
    ISNULL(AnimNo,'') AS NrGosp,
    LTRIM(RTRIM(ISNULL([Address],'') + ' ' + ISNULL(PostalCode,'') + ' ' + ISNULL(City,''))) AS Adres,
    ISNULL(Pesel,'') AS Pesel,
    ISNULL(Regon,'') AS Regon,
    ISNULL(IDCard,'') AS NrDowodu,
    ISNULL(Phone1,'') AS Telefon,
    ISNULL(Email,'') AS Email
FROM dbo.Dostawcy
WHERE IsDeliverer = 1
  AND (@q = '' OR [Name] LIKE @q OR Nip LIKE @q OR ID LIKE @q)
ORDER BY [Name];";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@q", string.IsNullOrWhiteSpace(szukaj) ? "" : "%" + szukaj.Trim() + "%");
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    lista.Add(new HodowcaPicker
                    {
                        DostawcaId = r.GetValue(0).ToString() ?? "",
                        Nazwa = r.GetString(1),
                        Nip = r.GetString(2),
                        NrGospodarstwa = r.GetString(3),
                        Adres = r.GetString(4),
                        Pesel = r.GetString(5),
                        Regon = r.GetString(6),
                        NrDowodu = r.GetString(7),
                        Telefon = r.GetString(8),
                        Email = r.GetString(9)
                    });
                }
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Szczegóły nagłówka ───────────────────────────────────────────────
        public async Task<KontraktDetail?> GetDetailAsync(int kontraktId)
        {
            const string sql = @"
SELECT Id, NumerKontraktu, Rok, LpRoku, DostawcaId, TypKontraktu, LiczySieDoArimr, Podmiot,
       NazwaHodowcySnapshot, NipSnapshot, NrGospodarstwaSnapshot, AdresSnapshot,
       PoprzedniKontraktId, UtworzylUserId, UtworzylKiedy, ZamknietyKiedy, PowodZamkniecia, EmailRODO,
       PeselSnapshot, RegonSnapshot, NrDowoduSnapshot, TelefonSnapshot
FROM dbo.Kontrakty WHERE Id = @id;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@id", kontraktId);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            return new KontraktDetail
            {
                Id = r.GetInt32(0),
                NumerKontraktu = r.IsDBNull(1) ? "" : r.GetString(1),
                Rok = r.IsDBNull(2) ? (short)0 : r.GetInt16(2),
                LpRoku = r.IsDBNull(3) ? 0 : r.GetInt32(3),
                DostawcaId = r.IsDBNull(4) ? "" : (r.GetValue(4).ToString() ?? ""),
                TypKontraktu = r.IsDBNull(5) ? "" : r.GetString(5),
                LiczySieDoArimr = !r.IsDBNull(6) && r.GetBoolean(6),
                Podmiot = r.IsDBNull(7) ? "PIORKOWSCY_SC" : r.GetString(7),
                NazwaHodowcySnapshot = r.IsDBNull(8) ? null : r.GetString(8),
                NipSnapshot = r.IsDBNull(9) ? null : r.GetString(9),
                NrGospodarstwaSnapshot = r.IsDBNull(10) ? null : r.GetString(10),
                AdresSnapshot = r.IsDBNull(11) ? null : r.GetString(11),
                PoprzedniKontraktId = r.IsDBNull(12) ? null : r.GetInt32(12),
                UtworzylUserId = r.IsDBNull(13) ? "" : r.GetString(13),
                UtworzylKiedy = r.IsDBNull(14) ? DateTime.Now : r.GetDateTime(14),
                ZamknietyKiedy = r.IsDBNull(15) ? null : r.GetDateTime(15),
                PowodZamkniecia = r.IsDBNull(16) ? null : r.GetString(16),
                EmailRODO = r.IsDBNull(17) ? null : r.GetString(17),
                PeselSnapshot = r.IsDBNull(18) ? null : r.GetString(18),
                RegonSnapshot = r.IsDBNull(19) ? null : r.GetString(19),
                NrDowoduSnapshot = r.IsDBNull(20) ? null : r.GetString(20),
                TelefonSnapshot = r.IsDBNull(21) ? null : r.GetString(21),
            };
        }

        // ─── Harmonogram cykli (per wersja) ───────────────────────────────────
        public async Task<List<HarmonogramCykl>> GetHarmonogramAsync(int wersjaId)
        {
            var lista = new List<HarmonogramCykl>();
            const string sql = @"
SELECT Id, KontraktId, WersjaId, NrCyklu, DataWstawienia, IloscWstawiona, IloscUbiorki,
       DzienUbiorki, DataUbojuKoncowego, IloscUboju, Status
FROM dbo.KontraktyHarmonogram WHERE WersjaId=@w ORDER BY NrCyklu;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@w", wersjaId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    lista.Add(new HarmonogramCykl
                    {
                        Id = r.GetInt32(0),
                        KontraktId = r.GetInt32(1),
                        WersjaId = r.GetInt32(2),
                        NrCyklu = r.GetInt32(3),
                        DataWstawienia = r.IsDBNull(4) ? null : r.GetDateTime(4),
                        IloscWstawiona = r.IsDBNull(5) ? null : r.GetInt32(5),
                        IloscUbiorki = r.IsDBNull(6) ? null : r.GetInt32(6),
                        DzienUbiorki = r.IsDBNull(7) ? null : r.GetInt32(7),
                        DataUbojuKoncowego = r.IsDBNull(8) ? null : r.GetDateTime(8),
                        IloscUboju = r.IsDBNull(9) ? null : r.GetInt32(9),
                        Status = r.IsDBNull(10) ? "PLANOWANY" : r.GetString(10),
                    });
                }
            }
            catch (SqlException) { }
            return lista;
        }

        /// <summary>Zastępuje cały harmonogram danej wersji (DELETE + INSERT w transakcji).</summary>
        public async Task SaveHarmonogramAsync(int kontraktId, int wersjaId, List<HarmonogramCykl> cykle)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            try
            {
                using (var del = new SqlCommand("DELETE FROM dbo.KontraktyHarmonogram WHERE WersjaId=@w;", cn, tx) { CommandTimeout = Timeout })
                {
                    del.Parameters.AddWithValue("@w", wersjaId);
                    await del.ExecuteNonQueryAsync();
                }
                await InsertHarmonogramAsync(cn, tx, kontraktId, wersjaId, cykle);
                tx.Commit();
            }
            catch { try { tx.Rollback(); } catch { } throw; }
        }

        // ─── Załączniki (skany PDF) ───────────────────────────────────────────
        public async Task<List<KontraktZalacznik>> GetZalacznikiAsync(int kontraktId)
        {
            var lista = new List<KontraktZalacznik>();
            const string sql = @"
SELECT Id, KontraktId, WersjaId, TypZalacznika, NazwaPliku, SciezkaUnc, DodalUserId, DodanyKiedy, Opis
FROM dbo.KontraktyZalaczniki WHERE KontraktId=@k ORDER BY DodanyKiedy DESC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@k", kontraktId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    lista.Add(new KontraktZalacznik
                    {
                        Id = r.GetInt32(0),
                        KontraktId = r.GetInt32(1),
                        WersjaId = r.IsDBNull(2) ? null : r.GetInt32(2),
                        TypZalacznika = r.IsDBNull(3) ? "INNE" : r.GetString(3),
                        NazwaPliku = r.IsDBNull(4) ? "" : r.GetString(4),
                        SciezkaUnc = r.IsDBNull(5) ? "" : r.GetString(5),
                        DodalUserId = r.IsDBNull(6) ? "" : r.GetString(6),
                        DodanyKiedy = r.IsDBNull(7) ? DateTime.Now : r.GetDateTime(7),
                        Opis = r.IsDBNull(8) ? null : r.GetString(8),
                    });
                }
            }
            catch (SqlException) { }
            return lista;
        }

        public async Task<int> AddZalacznikAsync(int kontraktId, int? wersjaId, string typ,
            string nazwaPliku, string sciezkaUnc, string userId, string? opis)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
INSERT INTO dbo.KontraktyZalaczniki (KontraktId, WersjaId, TypZalacznika, NazwaPliku, SciezkaUnc, DodalUserId, Opis)
OUTPUT INSERTED.Id
VALUES (@k, @w, @t, @n, @s, @u, @o);", cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@k", kontraktId);
            cmd.Parameters.AddWithValue("@w", Nz(wersjaId));
            cmd.Parameters.AddWithValue("@t", typ);
            cmd.Parameters.AddWithValue("@n", nazwaPliku);
            cmd.Parameters.AddWithValue("@s", sciezkaUnc);
            cmd.Parameters.AddWithValue("@u", userId);
            cmd.Parameters.AddWithValue("@o", Nz(opis));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // ─── Ostatnia umowa zakupu hodowcy (HarmonogramDostaw, match po nazwie) ─
        public class OstatniaUmowaZakupu
        {
            public int Lp { get; set; }
            public DateTime DataOdbioru { get; set; }
            public bool Utworzone { get; set; }
            public bool Wyslane { get; set; }
            public bool Otrzymane { get; set; }
            public int LiczbaWOstatnich90Dniach { get; set; }
        }

        public async Task<OstatniaUmowaZakupu?> GetOstatniaUmoweZakupuAsync(string nazwaHodowcy)
        {
            if (string.IsNullOrWhiteSpace(nazwaHodowcy)) return null;
            const string sql = @"
SELECT TOP 1
    LP, DataOdbioru,
    ISNULL([Utworzone],0)  AS Utworzone,
    ISNULL([Wysłane],0)    AS Wyslane,
    ISNULL([Otrzymane],0)  AS Otrzymane,
    (SELECT COUNT(*) FROM dbo.HarmonogramDostaw h2
       WHERE h2.Dostawca = @n AND h2.Bufor = 'Potwierdzony'
         AND h2.DataOdbioru >= DATEADD(DAY,-90,GETDATE())) AS Cnt90
FROM dbo.HarmonogramDostaw
WHERE Dostawca = @n AND Bufor = 'Potwierdzony'
ORDER BY DataOdbioru DESC, LP DESC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@n", nazwaHodowcy.Trim());
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return null;
                return new OstatniaUmowaZakupu
                {
                    Lp = r.GetInt32(0),
                    DataOdbioru = r.GetDateTime(1),
                    Utworzone = !r.IsDBNull(2) && Convert.ToBoolean(r.GetValue(2)),
                    Wyslane = !r.IsDBNull(3) && Convert.ToBoolean(r.GetValue(3)),
                    Otrzymane = !r.IsDBNull(4) && Convert.ToBoolean(r.GetValue(4)),
                    LiczbaWOstatnich90Dniach = r.IsDBNull(5) ? 0 : r.GetInt32(5)
                };
            }
            catch (SqlException) { return null; }
        }

        // ─── Quick-copy: ostatni kontrakt danego hodowcy (do „kopiuj z poprzedniego") ─
        public async Task<int?> GetOstatniKontraktIdHodowcyAsync(string dostawcaId)
        {
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 Id FROM dbo.Kontrakty WHERE DostawcaId=@d ORDER BY Rok DESC, LpRoku DESC;", cn)
                { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@d", dostawcaId);
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? (int?)null : Convert.ToInt32(r);
            }
            catch (SqlException) { return null; }
        }

        // ─── Wersje kontraktu ─────────────────────────────────────────────────
        public async Task<List<KontraktWersja>> GetWersjeAsync(int kontraktId)
        {
            var lista = new List<KontraktWersja>();
            const string sql = @"
SELECT w.Id, w.KontraktId, w.NrWersji, w.IsAktualna, w.Status, w.DataPodpisania, w.ObowiazujeOd, w.ObowiazujeDo,
       w.OkresWypowiedzeniaDni, w.ProcentUbytku, w.TypCeny, w.Cena, w.DodatekZl, w.TerminPlatnosciDni,
       w.RozliczanaWaga, w.MinimalnaIloscSzt, w.Ekskluzywnosc, w.KlauzuleSzczegolne,
       w.SciezkaWord, w.SciezkaPdfSkan, w.SzablonId, w.PowodZmiany, w.UtworzylUserId, w.UtworzylKiedy,
       ISNULL(op.Name, w.UtworzylUserId) AS UtworzylNazwa,
       w.CenaMin, w.CenaMax, w.Indeksacja, w.CzestotliwoscDostaw, w.MaxIloscSzt, w.TransportCzyj,
       w.PaszaOdNas, w.PisklakiOdNas, w.KaraUmownaZl, w.AutoOdnowienie, w.PrawoPierwokupu,
       w.OsobaKontaktowa, w.TelefonKontaktowy,
       w.DostawcaPaszyNazwa, w.DostawcaPisklatNazwa, w.BonusOpis,
       w.KonfiskatyHodowca
FROM dbo.KontraktyWersje w
LEFT JOIN dbo.operators op ON op.ID = w.UtworzylUserId
WHERE w.KontraktId = @id ORDER BY w.NrWersji DESC;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@id", kontraktId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                lista.Add(new KontraktWersja
                {
                    Id = r.GetInt32(0),
                    KontraktId = r.GetInt32(1),
                    NrWersji = r.GetInt32(2),
                    IsAktualna = !r.IsDBNull(3) && r.GetBoolean(3),
                    Status = r.IsDBNull(4) ? "DRAFT" : r.GetString(4),
                    DataPodpisania = r.IsDBNull(5) ? null : r.GetDateTime(5),
                    ObowiazujeOd = r.IsDBNull(6) ? DateTime.Today : r.GetDateTime(6),
                    ObowiazujeDo = r.IsDBNull(7) ? null : r.GetDateTime(7),
                    OkresWypowiedzeniaDni = r.IsDBNull(8) ? 90 : r.GetInt32(8),
                    ProcentUbytku = r.IsDBNull(9) ? null : r.GetDecimal(9),
                    TypCeny = r.IsDBNull(10) ? "wolnorynkowa" : r.GetString(10),
                    Cena = r.IsDBNull(11) ? null : r.GetDecimal(11),
                    DodatekZl = r.IsDBNull(12) ? null : r.GetDecimal(12),
                    TerminPlatnosciDni = r.IsDBNull(13) ? 21 : r.GetInt32(13),
                    RozliczanaWaga = r.IsDBNull(14) ? "NETTO_HODOWCY" : r.GetString(14),
                    MinimalnaIloscSzt = r.IsDBNull(15) ? null : r.GetInt32(15),
                    Ekskluzywnosc = !r.IsDBNull(16) && r.GetBoolean(16),
                    KlauzuleSzczegolne = r.IsDBNull(17) ? null : r.GetString(17),
                    SciezkaWord = r.IsDBNull(18) ? null : r.GetString(18),
                    SciezkaPdfSkan = r.IsDBNull(19) ? null : r.GetString(19),
                    SzablonId = r.IsDBNull(20) ? null : r.GetInt32(20),
                    PowodZmiany = r.IsDBNull(21) ? null : r.GetString(21),
                    UtworzylUserId = r.IsDBNull(22) ? "" : r.GetString(22),
                    UtworzylKiedy = r.IsDBNull(23) ? DateTime.Now : r.GetDateTime(23),
                    UtworzylNazwa = r.IsDBNull(24) ? "" : r.GetString(24),
                    CenaMin = r.IsDBNull(25) ? null : r.GetDecimal(25),
                    CenaMax = r.IsDBNull(26) ? null : r.GetDecimal(26),
                    Indeksacja = r.IsDBNull(27) ? null : r.GetString(27),
                    CzestotliwoscDostaw = r.IsDBNull(28) ? null : r.GetString(28),
                    MaxIloscSzt = r.IsDBNull(29) ? null : r.GetInt32(29),
                    TransportCzyj = r.IsDBNull(30) ? null : r.GetString(30),
                    PaszaOdNas = !r.IsDBNull(31) && r.GetBoolean(31),
                    PisklakiOdNas = !r.IsDBNull(32) && r.GetBoolean(32),
                    KaraUmownaZl = r.IsDBNull(33) ? null : r.GetDecimal(33),
                    AutoOdnowienie = !r.IsDBNull(34) && r.GetBoolean(34),
                    PrawoPierwokupu = !r.IsDBNull(35) && r.GetBoolean(35),
                    OsobaKontaktowa = r.IsDBNull(36) ? null : r.GetString(36),
                    TelefonKontaktowy = r.IsDBNull(37) ? null : r.GetString(37),
                    DostawcaPaszyNazwa = r.IsDBNull(38) ? null : r.GetString(38),
                    DostawcaPisklatNazwa = r.IsDBNull(39) ? null : r.GetString(39),
                    BonusOpis = r.IsDBNull(40) ? null : r.GetString(40),
                    KonfiskatyHodowca = r.IsDBNull(41) || r.GetBoolean(41),
                });
            }
            return lista;
        }

        // ─── Tworzenie kontraktu (nagłówek + wersja 1) ────────────────────────
        public async Task<(int kontraktId, string numer)> CreateKontraktAsync(KontraktDetail h, KontraktWersja w, string userId)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            try
            {
                short rok = (short)w.ObowiazujeOd.Year;

                // 1) numer (atomowo, wg konfigurowalnego formatu)
                var (lp, numer) = await NadajNumerAsync(cn, tx, rok);

                // 2) nagłówek + 3) wersja 1 (aktualna)
                int newId = await InsertNaglowekAsync(cn, tx, h, rok, lp, numer, userId);
                await InsertWersjaAsync(cn, tx, newId, 1, true, w, userId);
                await LogAuditAsync(cn, tx, newId, null, "Kontrakt", "UTWORZENIE", "Numer", null, numer, userId);

                tx.Commit();
                return (newId, numer);
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        // ─── Tworzenie kontraktu Z HARMONOGRAMEM (kreator) — jedna transakcja ──
        public async Task<(int kontraktId, int wersjaId, string numer)> CreateKontraktZHarmonogramAsync(
            KontraktDetail h, KontraktWersja w, List<HarmonogramCykl> cykle, string userId)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            try
            {
                short rok = (short)w.ObowiazujeOd.Year;
                var (lp, numer) = await NadajNumerAsync(cn, tx, rok);

                int kontraktId = await InsertNaglowekAsync(cn, tx, h, rok, lp, numer, userId);
                int wersjaId = await InsertWersjaAsync(cn, tx, kontraktId, 1, true, w, userId);
                await InsertHarmonogramAsync(cn, tx, kontraktId, wersjaId, cykle);
                await LogAuditAsync(cn, tx, kontraktId, wersjaId, "Kontrakt", "UTWORZENIE", "Numer", null, numer, userId);

                tx.Commit();
                return (kontraktId, wersjaId, numer);
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        // ─── Numeracja: konfigurowalny format (KontraktyNumeracjaConfig) ───────
        // Zwraca (lp, numer). Atomowo w transakcji; gdy brak tabeli configu → legacy „{lp}/{rr}".
        private async Task<(int lp, string numer)> NadajNumerAsync(SqlConnection cn, SqlTransaction tx, short rok)
        {
            // maksymalne Lp w roku (zabezpieczenie przed duplikatem niezależnie od configu)
            int maxLp;
            using (var cmdMax = new SqlCommand(
                "SELECT ISNULL(MAX(LpRoku),0) FROM dbo.Kontrakty WITH (TABLOCKX, HOLDLOCK) WHERE Rok=@rok;",
                cn, tx) { CommandTimeout = Timeout })
            { cmdMax.Parameters.AddWithValue("@rok", rok); maxLp = Convert.ToInt32(await cmdMax.ExecuteScalarAsync()); }

            // czy istnieje tabela configu? (bez wyjątku w transakcji)
            bool hasCfg;
            using (var cmdHas = new SqlCommand(
                "SELECT CASE WHEN OBJECT_ID('dbo.KontraktyNumeracjaConfig','U') IS NOT NULL THEN 1 ELSE 0 END;",
                cn, tx) { CommandTimeout = Timeout })
            { hasCfg = Convert.ToInt32(await cmdHas.ExecuteScalarAsync()) == 1; }

            if (!hasCfg) { int lpL = maxLp + 1; return (lpL, $"{lpL}/{rok % 100:00}"); }

            int cfgId = 0; string? format = null; bool reset = true; short cfgRok = rok; int seq = 1;
            using (var cmdCfg = new SqlCommand(
                "SELECT TOP 1 Id, FormatSzablon, ResetRoczny, Rok, NastepnyNumer FROM dbo.KontraktyNumeracjaConfig WITH (UPDLOCK, HOLDLOCK) ORDER BY Id;",
                cn, tx) { CommandTimeout = Timeout })
            using (var r = await cmdCfg.ExecuteReaderAsync())
            {
                if (await r.ReadAsync())
                {
                    cfgId = r.GetInt32(0);
                    format = r.IsDBNull(1) ? null : r.GetString(1);
                    reset = !r.IsDBNull(2) && r.GetBoolean(2);
                    cfgRok = r.IsDBNull(3) ? rok : r.GetInt16(3);
                    seq = r.IsDBNull(4) ? 1 : r.GetInt32(4);
                }
            }
            if (cfgId == 0 || string.IsNullOrWhiteSpace(format))
            { int lpL = maxLp + 1; return (lpL, $"{lpL}/{rok % 100:00}"); }

            if (reset && cfgRok != rok) seq = 1;     // reset roczny
            int lp = Math.Max(seq, maxLp + 1);        // nigdy nie cofaj się pod istniejące
            string numer = FormatujNumer(format!, rok, lp);

            using (var cmdUpd = new SqlCommand(
                "UPDATE dbo.KontraktyNumeracjaConfig SET NastepnyNumer=@next, Rok=@rok WHERE Id=@id;",
                cn, tx) { CommandTimeout = Timeout })
            {
                cmdUpd.Parameters.AddWithValue("@next", lp + 1);
                cmdUpd.Parameters.AddWithValue("@rok", rok);
                cmdUpd.Parameters.AddWithValue("@id", cfgId);
                await cmdUpd.ExecuteNonQueryAsync();
            }
            return (lp, numer);
        }

        /// <summary>Podstawia tokeny formatu: {ROK} {RR} {NNNN} {NNN} {NN} {N}.</summary>
        public static string FormatujNumer(string format, short rok, int lp)
            => format.Replace("{ROK}", rok.ToString())
                     .Replace("{RR}",   (rok % 100).ToString("00"))
                     .Replace("{NNNN}", lp.ToString("0000"))
                     .Replace("{NNN}",  lp.ToString("000"))
                     .Replace("{NN}",   lp.ToString("00"))
                     .Replace("{N}",    lp.ToString());

        // ─── Config numeracji: odczyt / zapis (okno ustawień) ─────────────────
        public async Task<NumeracjaConfig?> GetNumeracjaConfigAsync()
        {
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 Id, FormatSzablon, ResetRoczny, Rok, NastepnyNumer FROM dbo.KontraktyNumeracjaConfig ORDER BY Id;",
                    cn) { CommandTimeout = Timeout };
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    return new NumeracjaConfig
                    {
                        Id = r.GetInt32(0),
                        FormatSzablon = r.IsDBNull(1) ? "" : r.GetString(1),
                        ResetRoczny = !r.IsDBNull(2) && r.GetBoolean(2),
                        Rok = r.IsDBNull(3) ? (short)DateTime.Now.Year : r.GetInt16(3),
                        NastepnyNumer = r.IsDBNull(4) ? 1 : r.GetInt32(4)
                    };
            }
            catch (SqlException) { }
            return null;
        }

        public async Task SaveNumeracjaConfigAsync(NumeracjaConfig c)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
IF EXISTS (SELECT 1 FROM dbo.KontraktyNumeracjaConfig WHERE Id=@id)
    UPDATE dbo.KontraktyNumeracjaConfig SET FormatSzablon=@fmt, ResetRoczny=@reset, Rok=@rok, NastepnyNumer=@next WHERE Id=@id;
ELSE
    INSERT INTO dbo.KontraktyNumeracjaConfig(FormatSzablon, ResetRoczny, Rok, NastepnyNumer) VALUES (@fmt, @reset, @rok, @next);",
                cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@id", c.Id);
            cmd.Parameters.AddWithValue("@fmt", string.IsNullOrWhiteSpace(c.FormatSzablon) ? "K/{ROK}/{NNNN}" : c.FormatSzablon);
            cmd.Parameters.AddWithValue("@reset", c.ResetRoczny);
            cmd.Parameters.AddWithValue("@rok", c.Rok);
            cmd.Parameters.AddWithValue("@next", c.NastepnyNumer);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<int> InsertNaglowekAsync(SqlConnection cn, SqlTransaction tx,
            KontraktDetail h, short rok, int lp, string numer, string userId)
        {
            using var cmd = new SqlCommand(@"
INSERT INTO dbo.Kontrakty
 (NumerKontraktu, Rok, LpRoku, DostawcaId, TypKontraktu, LiczySieDoArimr, Podmiot,
  NazwaHodowcySnapshot, NipSnapshot, NrGospodarstwaSnapshot, AdresSnapshot, EmailRODO,
  PeselSnapshot, RegonSnapshot, NrDowoduSnapshot, TelefonSnapshot, UtworzylUserId)
OUTPUT INSERTED.Id
VALUES (@numer, @rok, @lp, @dost, @typ, @arimr, @podmiot,
  @nazwa, @nip, @gosp, @adres, @email,
  @pesel, @regon, @dowod, @tel, @user);", cn, tx) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@numer", numer);
            cmd.Parameters.AddWithValue("@rok", rok);
            cmd.Parameters.AddWithValue("@lp", lp);
            cmd.Parameters.AddWithValue("@dost", h.DostawcaId);
            cmd.Parameters.AddWithValue("@typ", h.TypKontraktu);
            cmd.Parameters.AddWithValue("@arimr", h.LiczySieDoArimr);
            cmd.Parameters.AddWithValue("@podmiot", h.Podmiot);
            cmd.Parameters.AddWithValue("@nazwa", Nz(h.NazwaHodowcySnapshot));
            cmd.Parameters.AddWithValue("@nip", Nz(h.NipSnapshot));
            cmd.Parameters.AddWithValue("@gosp", Nz(h.NrGospodarstwaSnapshot));
            cmd.Parameters.AddWithValue("@adres", Nz(h.AdresSnapshot));
            cmd.Parameters.AddWithValue("@email", Nz(h.EmailRODO));
            cmd.Parameters.AddWithValue("@pesel", Nz(h.PeselSnapshot));
            cmd.Parameters.AddWithValue("@regon", Nz(h.RegonSnapshot));
            cmd.Parameters.AddWithValue("@dowod", Nz(h.NrDowoduSnapshot));
            cmd.Parameters.AddWithValue("@tel", Nz(h.TelefonSnapshot));
            cmd.Parameters.AddWithValue("@user", userId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        private static async Task InsertHarmonogramAsync(SqlConnection cn, SqlTransaction tx,
            int kontraktId, int wersjaId, List<HarmonogramCykl>? cykle)
        {
            if (cykle == null) return;
            foreach (var c in cykle)
            {
                using var cmd = new SqlCommand(@"
INSERT INTO dbo.KontraktyHarmonogram
 (KontraktId, WersjaId, NrCyklu, DataWstawienia, IloscWstawiona, IloscUbiorki, DzienUbiorki, DataUbojuKoncowego, IloscUboju, Status)
VALUES (@kid, @wid, @nr, @dw, @iw, @iu, @du, @duk, @iuboj, @st);", cn, tx) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@kid", kontraktId);
                cmd.Parameters.AddWithValue("@wid", wersjaId);
                cmd.Parameters.AddWithValue("@nr", c.NrCyklu);
                cmd.Parameters.AddWithValue("@dw", Nz(c.DataWstawienia?.Date));
                cmd.Parameters.AddWithValue("@iw", Nz(c.IloscWstawiona));
                cmd.Parameters.AddWithValue("@iu", Nz(c.IloscUbiorki));
                cmd.Parameters.AddWithValue("@du", Nz(c.DzienUbiorki));
                cmd.Parameters.AddWithValue("@duk", Nz(c.DataUbojuKoncowego?.Date));
                cmd.Parameters.AddWithValue("@iuboj", Nz(c.IloscUboju));
                cmd.Parameters.AddWithValue("@st", string.IsNullOrWhiteSpace(c.Status) ? "PLANOWANY" : c.Status);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // ─── Przedłużenie = nowa wersja (NEGOCJACJE, nieaktualna do aktywacji) ─
        public async Task<int> CreateRenewalVersionAsync(int kontraktId, KontraktWersja w, string userId)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            try
            {
                using var cmdNr = new SqlCommand(
                    "SELECT ISNULL(MAX(NrWersji),0)+1 FROM dbo.KontraktyWersje WITH (UPDLOCK, HOLDLOCK) WHERE KontraktId=@kid;",
                    cn, tx) { CommandTimeout = Timeout };
                cmdNr.Parameters.AddWithValue("@kid", kontraktId);
                int nr = Convert.ToInt32(await cmdNr.ExecuteScalarAsync());

                if (string.IsNullOrWhiteSpace(w.Status) || w.Status == "DRAFT") w.Status = "NEGOCJACJE";
                int wid = await InsertWersjaAsync(cn, tx, kontraktId, nr, false, w, userId);
                await LogAuditAsync(cn, tx, kontraktId, wid, "KontraktyWersje", "PRZEDLUZENIE", "NrWersji", null, "v" + nr, userId);
                tx.Commit();
                return wid;
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        private static async Task<int> InsertWersjaAsync(SqlConnection cn, SqlTransaction tx,
            int kontraktId, int nrWersji, bool aktualna, KontraktWersja w, string userId)
        {
            using var cmd = new SqlCommand(@"
INSERT INTO dbo.KontraktyWersje
 (KontraktId, NrWersji, IsAktualna, Status, DataPodpisania, ObowiazujeOd, ObowiazujeDo, OkresWypowiedzeniaDni,
  ProcentUbytku, TypCeny, Cena, DodatekZl, TerminPlatnosciDni, RozliczanaWaga, MinimalnaIloscSzt,
  Ekskluzywnosc, KlauzuleSzczegolne, SciezkaWord, SciezkaPdfSkan, SzablonId, PowodZmiany, UtworzylUserId,
  CenaMin, CenaMax, Indeksacja, CzestotliwoscDostaw, MaxIloscSzt, TransportCzyj,
  PaszaOdNas, PisklakiOdNas, KaraUmownaZl, AutoOdnowienie, PrawoPierwokupu, OsobaKontaktowa, TelefonKontaktowy,
  DostawcaPaszyNazwa, DostawcaPisklatNazwa, BonusOpis, KonfiskatyHodowca)
OUTPUT INSERTED.Id
VALUES (@kid, @nr, @akt, @status, @podp, @od, @do, @wyp,
  @ubytek, @typceny, @cena, @dodatek, @termin, @waga, @minszt,
  @eksk, @klauz, @word, @pdf, @szablon, @powod, @user,
  @cenaMin, @cenaMax, @indeks, @czest, @maxszt, @transport,
  @pasza, @pisklaki, @kara, @autoodn, @pierwokup, @osoba, @telefon,
  @paszaNaz, @pisklNaz, @bonus, @konfHod);", cn, tx) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@kid", kontraktId);
            cmd.Parameters.AddWithValue("@nr", nrWersji);
            cmd.Parameters.AddWithValue("@akt", aktualna);
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(w.Status) ? "DRAFT" : w.Status);
            cmd.Parameters.AddWithValue("@podp", Nz(w.DataPodpisania));
            cmd.Parameters.AddWithValue("@od", w.ObowiazujeOd.Date);
            cmd.Parameters.AddWithValue("@do", Nz(w.ObowiazujeDo?.Date));
            cmd.Parameters.AddWithValue("@wyp", w.OkresWypowiedzeniaDni);
            cmd.Parameters.AddWithValue("@ubytek", Nz(w.ProcentUbytku));
            cmd.Parameters.AddWithValue("@typceny", w.TypCeny);
            cmd.Parameters.AddWithValue("@cena", Nz(w.Cena));
            cmd.Parameters.AddWithValue("@dodatek", Nz(w.DodatekZl));
            cmd.Parameters.AddWithValue("@termin", w.TerminPlatnosciDni);
            cmd.Parameters.AddWithValue("@waga", w.RozliczanaWaga);
            cmd.Parameters.AddWithValue("@minszt", Nz(w.MinimalnaIloscSzt));
            cmd.Parameters.AddWithValue("@eksk", w.Ekskluzywnosc);
            cmd.Parameters.AddWithValue("@klauz", Nz(w.KlauzuleSzczegolne));
            cmd.Parameters.AddWithValue("@word", Nz(w.SciezkaWord));
            cmd.Parameters.AddWithValue("@pdf", Nz(w.SciezkaPdfSkan));
            cmd.Parameters.AddWithValue("@szablon", Nz(w.SzablonId));
            cmd.Parameters.AddWithValue("@powod", Nz(w.PowodZmiany));
            cmd.Parameters.AddWithValue("@user", userId);
            cmd.Parameters.AddWithValue("@cenaMin", Nz(w.CenaMin));
            cmd.Parameters.AddWithValue("@cenaMax", Nz(w.CenaMax));
            cmd.Parameters.AddWithValue("@indeks", Nz(w.Indeksacja));
            cmd.Parameters.AddWithValue("@czest", Nz(w.CzestotliwoscDostaw));
            cmd.Parameters.AddWithValue("@maxszt", Nz(w.MaxIloscSzt));
            cmd.Parameters.AddWithValue("@transport", Nz(w.TransportCzyj));
            cmd.Parameters.AddWithValue("@pasza", w.PaszaOdNas);
            cmd.Parameters.AddWithValue("@pisklaki", w.PisklakiOdNas);
            cmd.Parameters.AddWithValue("@kara", Nz(w.KaraUmownaZl));
            cmd.Parameters.AddWithValue("@autoodn", w.AutoOdnowienie);
            cmd.Parameters.AddWithValue("@pierwokup", w.PrawoPierwokupu);
            cmd.Parameters.AddWithValue("@osoba", Nz(w.OsobaKontaktowa));
            cmd.Parameters.AddWithValue("@telefon", Nz(w.TelefonKontaktowy));
            cmd.Parameters.AddWithValue("@paszaNaz", Nz(w.DostawcaPaszyNazwa));
            cmd.Parameters.AddWithValue("@pisklNaz", Nz(w.DostawcaPisklatNazwa));
            cmd.Parameters.AddWithValue("@bonus", Nz(w.BonusOpis));
            cmd.Parameters.AddWithValue("@konfHod", w.KonfiskatyHodowca);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // ─── Edycja warunków wersji (tylko szkic / w negocjacji) ──────────────
        public async Task UpdateWersjaAsync(KontraktWersja w)
        {
            const string sql = @"
UPDATE dbo.KontraktyWersje SET
  DataPodpisania=@podp, ObowiazujeOd=@od, ObowiazujeDo=@do, OkresWypowiedzeniaDni=@wyp,
  ProcentUbytku=@ubytek, TypCeny=@typceny, Cena=@cena, DodatekZl=@dodatek, TerminPlatnosciDni=@termin,
  RozliczanaWaga=@waga, MinimalnaIloscSzt=@minszt, Ekskluzywnosc=@eksk, KlauzuleSzczegolne=@klauz,
  PowodZmiany=@powod,
  CenaMin=@cenaMin, CenaMax=@cenaMax, Indeksacja=@indeks, CzestotliwoscDostaw=@czest, MaxIloscSzt=@maxszt,
  TransportCzyj=@transport, PaszaOdNas=@pasza, PisklakiOdNas=@pisklaki, KaraUmownaZl=@kara,
  AutoOdnowienie=@autoodn, PrawoPierwokupu=@pierwokup, OsobaKontaktowa=@osoba, TelefonKontaktowy=@telefon,
  DostawcaPaszyNazwa=@paszaNaz, DostawcaPisklatNazwa=@pisklNaz, BonusOpis=@bonus,
  KonfiskatyHodowca=@konfHod
WHERE Id=@id;";
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@id", w.Id);
            cmd.Parameters.AddWithValue("@podp", Nz(w.DataPodpisania));
            cmd.Parameters.AddWithValue("@od", w.ObowiazujeOd.Date);
            cmd.Parameters.AddWithValue("@do", Nz(w.ObowiazujeDo?.Date));
            cmd.Parameters.AddWithValue("@wyp", w.OkresWypowiedzeniaDni);
            cmd.Parameters.AddWithValue("@ubytek", Nz(w.ProcentUbytku));
            cmd.Parameters.AddWithValue("@typceny", w.TypCeny);
            cmd.Parameters.AddWithValue("@cena", Nz(w.Cena));
            cmd.Parameters.AddWithValue("@dodatek", Nz(w.DodatekZl));
            cmd.Parameters.AddWithValue("@termin", w.TerminPlatnosciDni);
            cmd.Parameters.AddWithValue("@waga", w.RozliczanaWaga);
            cmd.Parameters.AddWithValue("@minszt", Nz(w.MinimalnaIloscSzt));
            cmd.Parameters.AddWithValue("@eksk", w.Ekskluzywnosc);
            cmd.Parameters.AddWithValue("@klauz", Nz(w.KlauzuleSzczegolne));
            cmd.Parameters.AddWithValue("@powod", Nz(w.PowodZmiany));
            cmd.Parameters.AddWithValue("@cenaMin", Nz(w.CenaMin));
            cmd.Parameters.AddWithValue("@cenaMax", Nz(w.CenaMax));
            cmd.Parameters.AddWithValue("@indeks", Nz(w.Indeksacja));
            cmd.Parameters.AddWithValue("@czest", Nz(w.CzestotliwoscDostaw));
            cmd.Parameters.AddWithValue("@maxszt", Nz(w.MaxIloscSzt));
            cmd.Parameters.AddWithValue("@transport", Nz(w.TransportCzyj));
            cmd.Parameters.AddWithValue("@pasza", w.PaszaOdNas);
            cmd.Parameters.AddWithValue("@pisklaki", w.PisklakiOdNas);
            cmd.Parameters.AddWithValue("@kara", Nz(w.KaraUmownaZl));
            cmd.Parameters.AddWithValue("@autoodn", w.AutoOdnowienie);
            cmd.Parameters.AddWithValue("@pierwokup", w.PrawoPierwokupu);
            cmd.Parameters.AddWithValue("@osoba", Nz(w.OsobaKontaktowa));
            cmd.Parameters.AddWithValue("@telefon", Nz(w.TelefonKontaktowy));
            cmd.Parameters.AddWithValue("@paszaNaz", Nz(w.DostawcaPaszyNazwa));
            cmd.Parameters.AddWithValue("@pisklNaz", Nz(w.DostawcaPisklatNazwa));
            cmd.Parameters.AddWithValue("@bonus", Nz(w.BonusOpis));
            cmd.Parameters.AddWithValue("@konfHod", w.KonfiskatyHodowca);
            await cmd.ExecuteNonQueryAsync();
        }

        // ─── Zmiana statusu wersji ────────────────────────────────────────────
        public async Task ChangeStatusWersjaAsync(int wersjaId, string status, string? userId = null)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();

            // odczytaj poprzedni status + kontraktId (do audytu)
            string? stary = null; int? kid = null;
            using (var q = new SqlCommand("SELECT Status, KontraktId FROM dbo.KontraktyWersje WHERE Id=@id;", cn) { CommandTimeout = Timeout })
            {
                q.Parameters.AddWithValue("@id", wersjaId);
                using var r = await q.ExecuteReaderAsync();
                if (await r.ReadAsync()) { stary = r.IsDBNull(0) ? null : r.GetString(0); kid = r.IsDBNull(1) ? null : r.GetInt32(1); }
            }

            using var cmd = new SqlCommand("UPDATE dbo.KontraktyWersje SET Status=@s WHERE Id=@id;", cn)
            { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@s", status);
            cmd.Parameters.AddWithValue("@id", wersjaId);
            await cmd.ExecuteNonQueryAsync();

            await LogAuditAsync(cn, null, kid, wersjaId, "KontraktyWersje", "ZMIANA_STATUSU", "Status", stary, status, userId);
        }

        // ─── Audyt (8.3) — zapis na poziomie aplikacji (zna App.UserID) ───────
        private async Task LogAuditAsync(SqlConnection cn, SqlTransaction? tx, int? kontraktId, int? wersjaId,
            string tabela, string operacja, string? pole, string? przed, string? po, string? userId)
        {
            try
            {
                using var cmd = new SqlCommand(@"
INSERT INTO dbo.KontraktyAuditLog (KontraktId, WersjaId, Tabela, Operacja, Pole, WartoscPrzed, WartoscPo, UserId)
VALUES (@k,@w,@t,@o,@p,@b,@a,@u);", cn, tx) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@k", (object?)kontraktId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@w", (object?)wersjaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@t", tabela);
                cmd.Parameters.AddWithValue("@o", operacja);
                cmd.Parameters.AddWithValue("@p", (object?)pole ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@b", (object?)przed ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@a", (object?)po ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@u", (object?)userId ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException) { /* brak tabeli audytu — pomiń, nie blokuj operacji */ }
        }

        public async Task<List<AuditWpis>> GetAuditLogAsync(int kontraktId)
        {
            var lista = new List<AuditWpis>();
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(@"
SELECT Id, KontraktId, WersjaId, Tabela, Operacja, Pole, WartoscPrzed, WartoscPo, UserId, Kiedy
FROM dbo.KontraktyAuditLog WHERE KontraktId=@k ORDER BY Kiedy DESC, Id DESC;", cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@k", kontraktId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    lista.Add(new AuditWpis
                    {
                        Id = r.GetInt64(0),
                        KontraktId = r.IsDBNull(1) ? null : r.GetInt32(1),
                        WersjaId = r.IsDBNull(2) ? null : r.GetInt32(2),
                        Tabela = r.IsDBNull(3) ? "" : r.GetString(3),
                        Operacja = r.IsDBNull(4) ? "" : r.GetString(4),
                        Pole = r.IsDBNull(5) ? null : r.GetString(5),
                        WartoscPrzed = r.IsDBNull(6) ? null : r.GetString(6),
                        WartoscPo = r.IsDBNull(7) ? null : r.GetString(7),
                        UserId = r.IsDBNull(8) ? null : r.GetString(8),
                        Kiedy = r.IsDBNull(9) ? DateTime.Now : r.GetDateTime(9)
                    });
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Transformacja JDG→sp. z o.o. (9.2): aport vs dzierżawa ───────────
        public async Task<TransformacjaDecyzja?> GetTransformacjaAsync(int kontraktId)
        {
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT KontraktId, Decyzja, DataDecyzji, UserId, Uzasadnienie FROM dbo.KontraktyTransformacja WHERE KontraktId=@k;",
                    cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@k", kontraktId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    return new TransformacjaDecyzja
                    {
                        KontraktId = r.GetInt32(0),
                        Decyzja = r.IsDBNull(1) ? "NIEOKRESLONE" : r.GetString(1),
                        DataDecyzji = r.IsDBNull(2) ? null : r.GetDateTime(2),
                        UserId = r.IsDBNull(3) ? null : r.GetString(3),
                        Uzasadnienie = r.IsDBNull(4) ? null : r.GetString(4)
                    };
            }
            catch (SqlException) { }
            return null;
        }

        public async Task SaveTransformacjaAsync(TransformacjaDecyzja t, string userId)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
MERGE dbo.KontraktyTransformacja AS d
USING (SELECT @k AS KontraktId) AS s ON d.KontraktId = s.KontraktId
WHEN MATCHED THEN UPDATE SET Decyzja=@dec, DataDecyzji=CAST(GETDATE() AS DATE), UserId=@u, Uzasadnienie=@uz
WHEN NOT MATCHED THEN INSERT (KontraktId, Decyzja, DataDecyzji, UserId, Uzasadnienie)
     VALUES (@k, @dec, CAST(GETDATE() AS DATE), @u, @uz);", cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@k", t.KontraktId);
            cmd.Parameters.AddWithValue("@dec", t.Decyzja);
            cmd.Parameters.AddWithValue("@u", userId ?? "");
            cmd.Parameters.AddWithValue("@uz", (object?)Nz(t.Uzasadnienie) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            await LogAuditAsync(cn, null, t.KontraktId, null, "Kontrakt", "TRANSFORMACJA", "Decyzja", null, t.Decyzja, userId);
        }

        // ─── Aktywacja wersji (staje się bieżąca; poprzednie → SUPERSEDED) ─────
        public async Task ActivateWersjaAsync(int kontraktId, int wersjaId, string? userId = null)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var tx = cn.BeginTransaction();
            try
            {
                using (var c1 = new SqlCommand(@"
UPDATE dbo.KontraktyWersje
SET IsAktualna = 0,
    Status = CASE WHEN Status IN ('ACTIVE','EXPIRING','SIGNED') THEN 'SUPERSEDED' ELSE Status END
WHERE KontraktId = @kid AND Id <> @wid;", cn, tx) { CommandTimeout = Timeout })
                {
                    c1.Parameters.AddWithValue("@kid", kontraktId);
                    c1.Parameters.AddWithValue("@wid", wersjaId);
                    await c1.ExecuteNonQueryAsync();
                }
                using (var c2 = new SqlCommand(
                    "UPDATE dbo.KontraktyWersje SET IsAktualna = 1, Status = 'ACTIVE' WHERE Id = @wid;", cn, tx)
                { CommandTimeout = Timeout })
                {
                    c2.Parameters.AddWithValue("@wid", wersjaId);
                    await c2.ExecuteNonQueryAsync();
                }
                await LogAuditAsync(cn, tx, kontraktId, wersjaId, "KontraktyWersje", "AKTYWACJA", null, null, "ACTIVE", userId);
                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        // ─── Zapis ścieżek pliku (po generacji Word / dodaniu skanu) ──────────
        public async Task SetSciezkiAsync(int wersjaId, string? word, string? pdf)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(@"
UPDATE dbo.KontraktyWersje
SET SciezkaWord = COALESCE(@word, SciezkaWord), SciezkaPdfSkan = COALESCE(@pdf, SciezkaPdfSkan)
WHERE Id=@id;", cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@id", wersjaId);
            cmd.Parameters.AddWithValue("@word", Nz(word));
            cmd.Parameters.AddWithValue("@pdf", Nz(pdf));
            await cmd.ExecuteNonQueryAsync();
        }

        // ─── Ścieżka szablonu Word dla typu ───────────────────────────────────
        public async Task<string?> GetTemplatePathAsync(string typKontraktu)
        {
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 SciezkaSzablon FROM dbo.KontraktyTemplates WHERE TypKontraktu=@t AND Aktywny=1 ORDER BY Id DESC;",
                    cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@t", typKontraktu);
                return (await cmd.ExecuteScalarAsync()) as string;
            }
            catch (SqlException) { return null; }
        }

        // ─── Generowanie alertów wygasania (idempotentne) ─────────────────────
        public async Task<int> GenerujAlertyAsync()
        {
            // 1) alerty wygasania (severity: <0 CRIT, ≤30 HIGH, ≤60 WARN, ≤90 INFO)
            const string sqlWygasanie = @"
;WITH Prog AS (
    SELECT v.KontraktId, v.Id AS WersjaId, k.NumerKontraktu,
           ISNULL(k.NazwaHodowcySnapshot, d.Name) AS Hodowca,
           DATEDIFF(DAY, CAST(GETDATE() AS DATE), v.ObowiazujeDo) AS Dni
    FROM dbo.KontraktyWersje v
    JOIN dbo.Kontrakty k ON k.Id = v.KontraktId
    LEFT JOIN dbo.Dostawcy d ON d.ID = k.DostawcaId
    WHERE v.IsAktualna = 1 AND v.ObowiazujeDo IS NOT NULL
      AND v.Status IN ('ACTIVE','EXPIRING','SIGNED')
),
Klas AS (
    SELECT *,
      CASE WHEN Dni < 0 THEN 'WYGASNAL'
           WHEN Dni <= 30 THEN 'WYGASA_30'
           WHEN Dni <= 60 THEN 'WYGASA_60'
           WHEN Dni <= 90 THEN 'WYGASA_90' END AS Typ,
      CASE WHEN Dni < 0 THEN 'CRIT' WHEN Dni <= 30 THEN 'HIGH' WHEN Dni <= 60 THEN 'WARN' ELSE 'INFO' END AS Sev
    FROM Prog WHERE Dni <= 90
)
INSERT INTO dbo.KontraktyAlerty (KontraktId, WersjaId, TypAlertu, Severity, DlaUserId, Wiadomosc)
SELECT k.KontraktId, k.WersjaId, k.Typ, k.Sev, 'asia',
       'Kontrakt ' + k.NumerKontraktu + ' (' + ISNULL(k.Hodowca,'?') + ') ' +
       CASE WHEN k.Dni < 0 THEN 'WYGASŁ ' + CAST(-k.Dni AS VARCHAR) + ' dni temu'
            ELSE 'wygasa za ' + CAST(k.Dni AS VARCHAR) + ' dni' END
FROM Klas k
WHERE k.Typ IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM dbo.KontraktyAlerty a
    WHERE a.KontraktId = k.KontraktId AND a.TypAlertu = k.Typ AND a.Przeczytany = 0);";

            // 2) martwe umowy (9.1): aktywny kontrakt (≥3 mies.), a hodowca nie dostarczał >180 dni
            const string sqlMartwe = @"
;WITH Akt AS (
    SELECT v.KontraktId, v.Id AS WersjaId, k.NumerKontraktu, k.DostawcaId,
           ISNULL(k.NazwaHodowcySnapshot, d.Name) AS Hodowca
    FROM dbo.KontraktyWersje v
    JOIN dbo.Kontrakty k ON k.Id = v.KontraktId
    LEFT JOIN dbo.Dostawcy d ON d.ID = k.DostawcaId
    WHERE v.IsAktualna = 1 AND v.Status IN ('ACTIVE','EXPIRING','SIGNED')
      AND v.ObowiazujeOd <= DATEADD(MONTH, -3, CAST(GETDATE() AS DATE))
),
Dost AS (
    SELECT LTRIM(RTRIM(CustomerGID)) AS gid, MAX(CalcDate) AS Ost
    FROM dbo.FarmerCalc GROUP BY LTRIM(RTRIM(CustomerGID))
)
INSERT INTO dbo.KontraktyAlerty (KontraktId, WersjaId, TypAlertu, Severity, DlaUserId, Wiadomosc)
SELECT a.KontraktId, a.WersjaId, 'MARTWA_UMOWA', 'WARN', 'asia',
       'Kontrakt ' + a.NumerKontraktu + ' (' + ISNULL(a.Hodowca,'?') + ') aktywny, ale ' +
       CASE WHEN dd.Ost IS NULL THEN 'brak dostaw w bazie'
            ELSE 'brak dostaw od ' + CAST(DATEDIFF(DAY, dd.Ost, CAST(GETDATE() AS DATE)) AS VARCHAR) + ' dni' END
FROM Akt a
LEFT JOIN Dost dd ON dd.gid = LTRIM(RTRIM(a.DostawcaId))
WHERE (dd.Ost IS NULL OR dd.Ost < DATEADD(DAY, -180, CAST(GETDATE() AS DATE)))
  AND NOT EXISTS (
    SELECT 1 FROM dbo.KontraktyAlerty a2
    WHERE a2.KontraktId = a.KontraktId AND a2.TypAlertu = 'MARTWA_UMOWA' AND a2.Przeczytany = 0);";

            // 3) eskalacja: nieprzeczytane CRIT/HIGH starsze niż 7 dni → znacznik WyslanoEskalacje
            const string sqlEskalacja = @"
UPDATE dbo.KontraktyAlerty
SET WyslanoEskalacje = SYSDATETIME()
WHERE Przeczytany = 0 AND WyslanoEskalacje IS NULL
  AND Severity IN ('CRIT','HIGH')
  AND DataWygenerowania < DATEADD(DAY, -7, SYSDATETIME());";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                int n = 0;
                foreach (var sql in new[] { sqlWygasanie, sqlMartwe, sqlEskalacja })
                {
                    using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                    try { n += await cmd.ExecuteNonQueryAsync(); }
                    catch (SqlException) { /* np. brak kolumny WyslanoEskalacje/FarmerCalc — pomiń krok */ }
                }
                return n;
            }
            catch (SqlException) { return 0; }
        }

        // ─── Szanse: hodowcy o dużym wolumenie BEZ aktywnego kontraktu (5.2) ──
        // (nie zapisujemy do KontraktyAlerty — KontraktId NOT NULL; liczymy na żywo do panelu Alertów)
        public async Task<List<HodowcaBezKontraktu>> GetSzanseBezKontraktuAsync(int minTony = 100, int top = 10)
        {
            var lista = new List<HodowcaBezKontraktu>();
            string sql = $@"
SELECT TOP ({top}) DostawcaId, Hodowca, LiczbaDostaw, WagaKg12m, OstatniaDostawa
FROM dbo.v_HodowcyBezKontraktu
WHERE WagaKg12m >= @min
ORDER BY WagaKg12m DESC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@min", minTony * 1000m);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    lista.Add(new HodowcaBezKontraktu
                    {
                        DostawcaId = r.IsDBNull(0) ? "" : (r.GetValue(0).ToString() ?? ""),
                        Hodowca = r.IsDBNull(1) ? "(brak nazwy)" : r.GetString(1),
                        LiczbaDostaw = GetIntSafe(r, 2),
                        WagaKg12m = GetDecSafe(r, 3),
                        OstatniaDostawa = r.IsDBNull(4) ? null : r.GetDateTime(4)
                    });
            }
            catch (SqlException) { }
            return lista;
        }

        // ─── Alerty: lista (+ numer/hodowca) ──────────────────────────────────
        public async Task<List<KontraktAlertItem>> GetAlertyAsync(bool tylkoNieprzeczytane)
        {
            var lista = new List<KontraktAlertItem>();
            string where = tylkoNieprzeczytane ? "WHERE a.Przeczytany = 0" : "";
            string sql = $@"
SELECT a.Id, a.KontraktId, a.TypAlertu, a.Severity, a.Wiadomosc, a.DataWygenerowania, a.Przeczytany, a.AkcjaPodjeta,
       k.NumerKontraktu, ISNULL(k.NazwaHodowcySnapshot, d.Name) AS Hodowca,
       CASE WHEN a.WyslanoEskalacje IS NOT NULL THEN 1 ELSE 0 END AS Eskalowany
FROM dbo.KontraktyAlerty a
JOIN dbo.Kontrakty k ON k.Id = a.KontraktId
LEFT JOIN dbo.Dostawcy d ON d.ID = k.DostawcaId
{where}
ORDER BY a.Przeczytany ASC,
         CASE a.Severity WHEN 'CRIT' THEN 0 WHEN 'HIGH' THEN 1 WHEN 'WARN' THEN 2 ELSE 3 END,
         a.DataWygenerowania DESC;";
            try
            {
                using var cn = new SqlConnection(_conn);
                await cn.OpenAsync();
                using var cmd = new SqlCommand(sql, cn) { CommandTimeout = Timeout };
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    lista.Add(new KontraktAlertItem
                    {
                        Id = r.GetInt32(0),
                        KontraktId = r.GetInt32(1),
                        TypAlertu = r.IsDBNull(2) ? "" : r.GetString(2),
                        Severity = r.IsDBNull(3) ? "INFO" : r.GetString(3),
                        Wiadomosc = r.IsDBNull(4) ? "" : r.GetString(4),
                        DataWygenerowania = r.IsDBNull(5) ? DateTime.Now : r.GetDateTime(5),
                        Przeczytany = !r.IsDBNull(6) && r.GetBoolean(6),
                        AkcjaPodjeta = !r.IsDBNull(7) && r.GetBoolean(7),
                        NumerKontraktu = r.IsDBNull(8) ? "" : r.GetString(8),
                        Hodowca = r.IsDBNull(9) ? "" : r.GetString(9),
                        Eskalowany = !r.IsDBNull(10) && r.GetInt32(10) == 1,
                    });
                }
            }
            catch (SqlException) { }
            return lista;
        }

        public async Task MarkAlertReadAsync(int id, string userId)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(
                "UPDATE dbo.KontraktyAlerty SET Przeczytany=1, PrzeczytanyKiedy=SYSDATETIME(), PrzeczytanyKto=@u WHERE Id=@id;",
                cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@u", userId ?? "");
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task MarkAlertActionAsync(int id)
        {
            using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            using var cmd = new SqlCommand(
                "UPDATE dbo.KontraktyAlerty SET AkcjaPodjeta=1, Przeczytany=1 WHERE Id=@id;", cn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        // ─── Licznik wygasających (do badge w menu — wywoływane synchronicznie) ─
        public static int CountWygasajaceSync(int dni)
        {
            try
            {
                Kalendarz1.AnalitykaPelna.Services.AnalitykaConfig.ZaladujJesliTrzeba();
                using var cn = new SqlConnection(Kalendarz1.AnalitykaPelna.Services.AnalitykaConfig.ConnLibraNet);
                cn.Open();
                using var cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.v_KontraktyAktualne WHERE Status IN ('ACTIVE','EXPIRING') AND DniDoWygasniecia IS NOT NULL AND DniDoWygasniecia <= @d;",
                    cn) { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@d", dni);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch { return 0; }
        }

        private static object Nz(object? v) => v ?? DBNull.Value;
    }
}
