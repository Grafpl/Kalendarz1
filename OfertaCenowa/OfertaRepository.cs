using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Kalendarz1.OfertaCenowa
{
    /// <summary>
    /// Repozytorium do zarządzania ofertami w bazie danych
    /// </summary>
    public class OfertaRepository
    {
        private readonly string _connectionString;

        public OfertaRepository(string connectionString = null)
        {
            _connectionString = connectionString ?? 
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        #region Zapisywanie oferty

        /// <summary>
        /// Zapisuje ofertę wraz z pozycjami do bazy danych
        /// </summary>
        public async Task<(int OfertaID, string NumerOferty)> ZapiszOferteAsync(
            KlientOferta klient,
            List<TowarOferta> produkty,
            ParametryOferty parametry,
            string notatki,
            string transport,
            string handlowiecId,
            string handlowiecNazwa,
            string handlowiecEmail,
            string handlowiecTelefon,
            string sciezkaPliku,
            string nazwaPliku)
        {
            // Oblicz wartość netto
            decimal wartoscNetto = 0;
            foreach (var p in produkty)
            {
                wartoscNetto += p.Ilosc * p.CenaJednostkowa;
            }

            int ofertaId = 0;
            string numerOferty = "";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var transaction = conn.BeginTransaction();

            try
            {
                // 1. Zapisz nagłówek oferty używając procedury składowanej
                await using var cmd = new SqlCommand("sp_ZapiszOferte", conn, transaction);
                cmd.CommandType = CommandType.StoredProcedure;

                // Klient
                cmd.Parameters.AddWithValue("@KlientID", (object)klient.Id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientNazwa", klient.Nazwa);
                cmd.Parameters.AddWithValue("@KlientNIP", (object)klient.NIP ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientAdres", (object)klient.Adres ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientMiejscowosc", (object)klient.Miejscowosc ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientKodPocztowy", (object)klient.KodPocztowy ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientOsobaKontaktowa", (object)klient.OsobaKontaktowa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientTelefon", (object)klient.Telefon ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@KlientEmail", DBNull.Value);
                cmd.Parameters.AddWithValue("@CzyKlientReczny", klient.CzyReczny);

                // Handlowiec
                cmd.Parameters.AddWithValue("@HandlowiecID", handlowiecId);
                cmd.Parameters.AddWithValue("@HandlowiecNazwa", handlowiecNazwa);
                cmd.Parameters.AddWithValue("@HandlowiecEmail", (object)handlowiecEmail ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HandlowiecTelefon", (object)handlowiecTelefon ?? DBNull.Value);

                // Wartości
                cmd.Parameters.AddWithValue("@WartoscNetto", wartoscNetto);
                cmd.Parameters.AddWithValue("@LiczbaPozycji", produkty.Count);
                cmd.Parameters.AddWithValue("@Waluta", parametry.WalutaKonta);

                // Parametry
                cmd.Parameters.AddWithValue("@TerminPlatnosci", (object)parametry.TerminPlatnosci ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DniPlatnosci", parametry.DniPlatnosci);
                cmd.Parameters.AddWithValue("@DniWaznosci", parametry.DniWaznosci);
                cmd.Parameters.AddWithValue("@Transport", (object)transport ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@TypLogo", parametry.TypLogo.ToString());
                cmd.Parameters.AddWithValue("@Jezyk", parametry.Jezyk.ToString());
                cmd.Parameters.AddWithValue("@WalutaKonta", parametry.WalutaKonta);

                // Opcje wyświetlania
                cmd.Parameters.AddWithValue("@PokazOpakowanie", parametry.PokazOpakowanie);
                cmd.Parameters.AddWithValue("@PokazCene", parametry.PokazCene);
                cmd.Parameters.AddWithValue("@PokazIlosc", parametry.PokazIlosc);
                cmd.Parameters.AddWithValue("@PokazTerminPlatnosci", parametry.PokazTerminPlatnosci);

                // Notatki i plik
                cmd.Parameters.AddWithValue("@Notatki", (object)notatki ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SciezkaPliku", (object)sciezkaPliku ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NazwaPliku", (object)nazwaPliku ?? DBNull.Value);

                // Parametry wyjściowe
                var paramOfertaId = new SqlParameter("@OfertaID", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var paramNumerOferty = new SqlParameter("@NumerOferty", SqlDbType.NVarChar, 50) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(paramOfertaId);
                cmd.Parameters.Add(paramNumerOferty);

                await cmd.ExecuteNonQueryAsync();

                ofertaId = (int)paramOfertaId.Value;
                numerOferty = paramNumerOferty.Value.ToString();

                // 2. Zapisz pozycje oferty
                int lp = 1;
                foreach (var produkt in produkty)
                {
                    await using var cmdPoz = new SqlCommand(@"
                        INSERT INTO [Oferty_Pozycje] 
                        ([OfertaID], [TowarID], [TowarKod], [TowarNazwa], [Katalog], 
                         [Ilosc], [CenaJednostkowa], [Wartosc], [Opakowanie], [Lp])
                        VALUES 
                        (@OfertaID, @TowarID, @TowarKod, @TowarNazwa, @Katalog,
                         @Ilosc, @CenaJednostkowa, @Wartosc, @Opakowanie, @Lp)", conn, transaction);

                    cmdPoz.Parameters.AddWithValue("@OfertaID", ofertaId);
                    cmdPoz.Parameters.AddWithValue("@TowarID", produkt.Id > 0 ? produkt.Id : DBNull.Value);
                    cmdPoz.Parameters.AddWithValue("@TowarKod", produkt.Kod);
                    cmdPoz.Parameters.AddWithValue("@TowarNazwa", produkt.Nazwa);
                    cmdPoz.Parameters.AddWithValue("@Katalog", (object)produkt.Katalog ?? DBNull.Value);
                    cmdPoz.Parameters.AddWithValue("@Ilosc", produkt.Ilosc);
                    cmdPoz.Parameters.AddWithValue("@CenaJednostkowa", produkt.CenaJednostkowa);
                    cmdPoz.Parameters.AddWithValue("@Wartosc", produkt.Ilosc * produkt.CenaJednostkowa);
                    cmdPoz.Parameters.AddWithValue("@Opakowanie", (object)produkt.Opakowanie ?? DBNull.Value);
                    cmdPoz.Parameters.AddWithValue("@Lp", lp++);

                    await cmdPoz.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                return (ofertaId, numerOferty);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Pobieranie ofert

        /// <summary>
        /// Pobiera listę ofert z widoku vw_OfertyLista
        /// </summary>
        public async Task<List<OfertaListaItem>> PobierzListeOfertAsync(
            string filtrStatus = null,
            string filtrHandlowiec = null,
            string filtrKlient = null,
            DateTime? dataOd = null,
            DateTime? dataDo = null,
            int limit = 100)
        {
            var lista = new List<OfertaListaItem>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"SELECT TOP (@Limit) * FROM [vw_OfertyLista] WHERE 1=1";

            if (!string.IsNullOrEmpty(filtrStatus))
                sql += " AND [Status] = @Status";
            if (!string.IsNullOrEmpty(filtrHandlowiec))
                sql += " AND [HandlowiecID] = @HandlowiecID";
            if (!string.IsNullOrEmpty(filtrKlient))
                sql += " AND [KlientNazwa] LIKE @KlientNazwa";
            if (dataOd.HasValue)
                sql += " AND [DataWystawienia] >= @DataOd";
            if (dataDo.HasValue)
                sql += " AND [DataWystawienia] <= @DataDo";

            sql += " ORDER BY [DataWystawienia] DESC";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Limit", limit);

            if (!string.IsNullOrEmpty(filtrStatus))
                cmd.Parameters.AddWithValue("@Status", filtrStatus);
            if (!string.IsNullOrEmpty(filtrHandlowiec))
                cmd.Parameters.AddWithValue("@HandlowiecID", filtrHandlowiec);
            if (!string.IsNullOrEmpty(filtrKlient))
                cmd.Parameters.AddWithValue("@KlientNazwa", $"%{filtrKlient}%");
            if (dataOd.HasValue)
                cmd.Parameters.AddWithValue("@DataOd", dataOd.Value);
            if (dataDo.HasValue)
                cmd.Parameters.AddWithValue("@DataDo", dataDo.Value);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new OfertaListaItem
                {
                    ID = reader.GetInt32("ID"),
                    NumerOferty = reader.GetString("NumerOferty"),
                    DataWystawienia = reader.GetDateTime("DataWystawienia"),
                    DataWaznosci = reader.GetDateTime("DataWaznosci"),
                    KlientNazwa = reader.GetString("KlientNazwa"),
                    KlientNIP = reader.IsDBNull("KlientNIP") ? "" : reader.GetString("KlientNIP"),
                    KlientMiejscowosc = reader.IsDBNull("KlientMiejscowosc") ? "" : reader.GetString("KlientMiejscowosc"),
                    HandlowiecID = reader.GetString("HandlowiecID"),
                    HandlowiecNazwa = reader.GetString("HandlowiecNazwa"),
                    WartoscNetto = reader.GetDecimal("WartoscNetto"),
                    LiczbaPozycji = reader.GetInt32("LiczbaPozycji"),
                    Waluta = reader.GetString("Waluta"),
                    Status = reader.GetString("Status"),
                    CzyPrzeterminowana = reader.GetInt32("CzyPrzeterminowana") == 1,
                    CzyMaZamowienie = reader.GetInt32("CzyMaZamowienie") == 1,
                    CzyMaFakture = reader.GetInt32("CzyMaFakture") == 1,
                    NazwaPliku = reader.IsDBNull("NazwaPliku") ? "" : reader.GetString("NazwaPliku"),
                    SciezkaPDF = reader.IsDBNull("SciezkaPliku") ? "" : reader.GetString("SciezkaPliku")
                });
            }

            return lista;
        }

        /// <summary>
        /// Pobiera szczegóły pojedynczej oferty
        /// </summary>
        public async Task<OfertaSzczegoly> PobierzOferteAsync(int ofertaId)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("SELECT * FROM [Oferty] WHERE [ID] = @ID", conn);
            cmd.Parameters.AddWithValue("@ID", ofertaId);

            OfertaSzczegoly oferta = null;

            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    oferta = new OfertaSzczegoly
                    {
                        ID = reader.GetInt32("ID"),
                        NumerOferty = reader.GetString("NumerOferty"),
                        DataWystawienia = reader.GetDateTime("DataWystawienia"),
                        DataWaznosci = reader.GetDateTime("DataWaznosci"),
                        
                        KlientID = reader.IsDBNull("KlientID") ? "" : reader.GetString("KlientID"),
                        KlientNazwa = reader.GetString("KlientNazwa"),
                        KlientNIP = reader.IsDBNull("KlientNIP") ? "" : reader.GetString("KlientNIP"),
                        KlientAdres = reader.IsDBNull("KlientAdres") ? "" : reader.GetString("KlientAdres"),
                        KlientMiejscowosc = reader.IsDBNull("KlientMiejscowosc") ? "" : reader.GetString("KlientMiejscowosc"),
                        
                        HandlowiecID = reader.GetString("HandlowiecID"),
                        HandlowiecNazwa = reader.GetString("HandlowiecNazwa"),
                        
                        WartoscNetto = reader.GetDecimal("WartoscNetto"),
                        LiczbaPozycji = reader.GetInt32("LiczbaPozycji"),
                        Status = reader.GetString("Status"),
                        
                        Notatki = reader.IsDBNull("Notatki") ? "" : reader.GetString("Notatki"),
                        Transport = reader.IsDBNull("Transport") ? "" : reader.GetString("Transport"),
                        TerminPlatnosci = reader.IsDBNull("TerminPlatnosci") ? "" : reader.GetString("TerminPlatnosci"),
                        
                        SciezkaPliku = reader.IsDBNull("SciezkaPliku") ? "" : reader.GetString("SciezkaPliku"),
                        NazwaPliku = reader.IsDBNull("NazwaPliku") ? "" : reader.GetString("NazwaPliku")
                    };
                }
            }

            if (oferta == null) return null;

            await using var cmdPoz = new SqlCommand(
                "SELECT * FROM [Oferty_Pozycje] WHERE [OfertaID] = @OfertaID ORDER BY [Lp]", conn);
            cmdPoz.Parameters.AddWithValue("@OfertaID", ofertaId);

            await using var readerPoz = await cmdPoz.ExecuteReaderAsync();
            while (await readerPoz.ReadAsync())
            {
                oferta.Pozycje.Add(new OfertaPozycja
                {
                    ID = readerPoz.GetInt32("ID"),
                    Lp = readerPoz.GetInt32("Lp"),
                    TowarKod = readerPoz.GetString("TowarKod"),
                    TowarNazwa = readerPoz.GetString("TowarNazwa"),
                    Ilosc = readerPoz.GetDecimal("Ilosc"),
                    CenaJednostkowa = readerPoz.GetDecimal("CenaJednostkowa"),
                    Wartosc = readerPoz.GetDecimal("Wartosc"),
                    Opakowanie = readerPoz.IsDBNull("Opakowanie") ? "" : readerPoz.GetString("Opakowanie")
                });
            }

            return oferta;
        }

        #endregion

        #region Zmiana statusu

        public async Task ZmienStatusAsync(int ofertaId, string nowyStatus, string uzytkownikId, string komentarz = null)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var transaction = conn.BeginTransaction();

            try
            {
                string aktualnyStatus = "";
                await using (var cmdGet = new SqlCommand("SELECT [Status] FROM [Oferty] WHERE [ID] = @ID", conn, transaction))
                {
                    cmdGet.Parameters.AddWithValue("@ID", ofertaId);
                    var result = await cmdGet.ExecuteScalarAsync();
                    aktualnyStatus = result?.ToString() ?? "";
                }

                await using (var cmdUpdate = new SqlCommand(
                    "UPDATE [Oferty] SET [Status] = @Status WHERE [ID] = @ID", conn, transaction))
                {
                    cmdUpdate.Parameters.AddWithValue("@Status", nowyStatus);
                    cmdUpdate.Parameters.AddWithValue("@ID", ofertaId);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }

                await using (var cmdHist = new SqlCommand(@"
                    INSERT INTO [Oferty_Historia] 
                    ([OfertaID], [StatusPoprzedni], [StatusNowy], [DataZmiany], [UzytkownikID], [TypAkcji], [Komentarz])
                    VALUES 
                    (@OfertaID, @StatusPoprzedni, @StatusNowy, GETDATE(), @UzytkownikID, 'ZmianaStatusu', @Komentarz)", conn, transaction))
                {
                    cmdHist.Parameters.AddWithValue("@OfertaID", ofertaId);
                    cmdHist.Parameters.AddWithValue("@StatusPoprzedni", aktualnyStatus);
                    cmdHist.Parameters.AddWithValue("@StatusNowy", nowyStatus);
                    cmdHist.Parameters.AddWithValue("@UzytkownikID", uzytkownikId);
                    cmdHist.Parameters.AddWithValue("@Komentarz", (object)komentarz ?? DBNull.Value);
                    await cmdHist.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region Historia

        public async Task<List<OfertaHistoriaItem>> PobierzHistorieAsync(int ofertaId)
        {
            var lista = new List<OfertaHistoriaItem>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(@"
                SELECT h.*, u.Name as UzytkownikNazwa
                FROM [Oferty_Historia] h
                LEFT JOIN [Operators] u ON h.[UzytkownikID] = u.[ID]
                WHERE h.[OfertaID] = @OfertaID
                ORDER BY h.[DataZmiany] DESC", conn);
            cmd.Parameters.AddWithValue("@OfertaID", ofertaId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new OfertaHistoriaItem
                {
                    ID = reader.GetInt32("ID"),
                    StatusPoprzedni = reader.IsDBNull("StatusPoprzedni") ? "" : reader.GetString("StatusPoprzedni"),
                    StatusNowy = reader.GetString("StatusNowy"),
                    DataZmiany = reader.GetDateTime("DataZmiany"),
                    UzytkownikNazwa = reader.IsDBNull("UzytkownikNazwa") ? "" : reader.GetString("UzytkownikNazwa"),
                    TypAkcji = reader.IsDBNull("TypAkcji") ? "" : reader.GetString("TypAkcji"),
                    Komentarz = reader.IsDBNull("Komentarz") ? "" : reader.GetString("Komentarz")
                });
            }

            return lista;
        }

        #endregion

        #region Top Klienci

        /// <summary>
        /// Pobiera listę top klientów według wartości ofert
        /// </summary>
        public async Task<List<TopKlient>> PobierzTopKlientowAsync(int rok = 0, int limit = 10)
        {
            var lista = new List<TopKlient>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT TOP (@Limit)
                    KlientID,
                    KlientNazwa,
                    MAX(KlientNIP) as KlientNIP,
                    COUNT(*) as LiczbaOfert,
                    SUM(WartoscNetto) as SumaWartosci,
                    AVG(WartoscNetto) as SredniaWartosc,
                    MAX(DataWystawienia) as OstatniaOferta,
                    SUM(CASE WHEN Status = 'Zaakceptowana' THEN 1 ELSE 0 END) as Zaakceptowane,
                    SUM(CASE WHEN Status = 'Zaakceptowana' THEN WartoscNetto ELSE 0 END) as WartoscZaakceptowanych
                FROM Oferty
                WHERE KlientID IS NOT NULL";

            if (rok > 0)
                sql += " AND YEAR(DataWystawienia) = @Rok";

            sql += @"
                GROUP BY KlientID, KlientNazwa
                ORDER BY SumaWartosci DESC";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Limit", limit);
            if (rok > 0)
                cmd.Parameters.AddWithValue("@Rok", rok);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new TopKlient
                {
                    KlientID = reader.IsDBNull("KlientID") ? "" : reader.GetString("KlientID"),
                    KlientNazwa = reader.GetString("KlientNazwa"),
                    KlientNIP = reader.IsDBNull("KlientNIP") ? "" : reader.GetString("KlientNIP"),
                    LiczbaOfert = reader.GetInt32("LiczbaOfert"),
                    SumaWartosci = reader.GetDecimal("SumaWartosci"),
                    SredniaWartosc = reader.GetDecimal("SredniaWartosc"),
                    OstatniaOferta = reader.GetDateTime("OstatniaOferta"),
                    Zaakceptowane = reader.GetInt32("Zaakceptowane"),
                    WartoscZaakceptowanych = reader.GetDecimal("WartoscZaakceptowanych")
                });
            }

            return lista;
        }

        #endregion

        #region Słowniki

        public async Task<List<StatusOferty>> PobierzSlownikStatusowAsync()
        {
            var lista = new List<StatusOferty>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(
                "SELECT * FROM [Oferty_Slownik_Statusy] WHERE [CzyAktywny] = 1 ORDER BY [Kolejnosc]", conn);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new StatusOferty
                {
                    Kod = reader.GetString("Kod"),
                    Nazwa = reader.GetString("Nazwa"),
                    NazwaEN = reader.IsDBNull("NazwaEN") ? "" : reader.GetString("NazwaEN"),
                    Kolor = reader.IsDBNull("Kolor") ? "#000000" : reader.GetString("Kolor"),
                    Ikona = reader.IsDBNull("Ikona") ? "" : reader.GetString("Ikona")
                });
            }

            return lista;
        }

        #endregion
    }

    #region Modele danych (BEZ OfertaListaItem - jest w osobnym pliku OfertaListaItem.cs!)

    /// <summary>
    /// Szczegóły oferty z pozycjami
    /// </summary>
    public class OfertaSzczegoly
    {
        public int ID { get; set; }
        public string NumerOferty { get; set; } = "";
        public DateTime DataWystawienia { get; set; }
        public DateTime DataWaznosci { get; set; }
        
        public string KlientID { get; set; } = "";
        public string KlientNazwa { get; set; } = "";
        public string KlientNIP { get; set; } = "";
        public string KlientAdres { get; set; } = "";
        public string KlientMiejscowosc { get; set; } = "";
        
        public string HandlowiecID { get; set; } = "";
        public string HandlowiecNazwa { get; set; } = "";
        
        public decimal WartoscNetto { get; set; }
        public int LiczbaPozycji { get; set; }
        public string Status { get; set; } = "";
        
        public string Notatki { get; set; } = "";
        public string Transport { get; set; } = "";
        public string TerminPlatnosci { get; set; } = "";
        
        public string SciezkaPliku { get; set; } = "";
        public string NazwaPliku { get; set; } = "";
        
        public List<OfertaPozycja> Pozycje { get; set; } = new();
    }

    public class OfertaPozycja
    {
        public int ID { get; set; }
        public int Lp { get; set; }
        public string TowarKod { get; set; } = "";
        public string TowarNazwa { get; set; } = "";
        public decimal Ilosc { get; set; }
        public decimal CenaJednostkowa { get; set; }
        public decimal Wartosc { get; set; }
        public string Opakowanie { get; set; } = "";
    }

    public class StatystykaOfert
    {
        public int Rok { get; set; }
        public int Miesiac { get; set; }
        public string NazwaMiesiaca { get; set; } = "";
        public string HandlowiecID { get; set; } = "";
        public string HandlowiecNazwa { get; set; } = "";
        public int LiczbaOfert { get; set; }
        public decimal SumaWartosci { get; set; }
        public decimal SredniaWartosc { get; set; }
        public int Nowe { get; set; }
        public int Wyslane { get; set; }
        public int Zaakceptowane { get; set; }
        public int Odrzucone { get; set; }
        public decimal ProcentKonwersji { get; set; }
    }

    public class TopKlient
    {
        public string KlientID { get; set; } = "";
        public string KlientNazwa { get; set; } = "";
        public string KlientNIP { get; set; } = "";
        public int LiczbaOfert { get; set; }
        public decimal SumaWartosci { get; set; }
        public decimal SredniaWartosc { get; set; }
        public DateTime OstatniaOferta { get; set; }
        public int Zaakceptowane { get; set; }
        public decimal WartoscZaakceptowanych { get; set; }
    }

    public class OfertaHistoriaItem
    {
        public int ID { get; set; }
        public string StatusPoprzedni { get; set; } = "";
        public string StatusNowy { get; set; } = "";
        public DateTime DataZmiany { get; set; }
        public string UzytkownikNazwa { get; set; } = "";
        public string TypAkcji { get; set; } = "";
        public string Komentarz { get; set; } = "";

        public string Opis => string.IsNullOrEmpty(StatusPoprzedni)
            ? $"{StatusNowy}"
            : $"{StatusPoprzedni} → {StatusNowy}";
    }

    public class StatusOferty
    {
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
        public string NazwaEN { get; set; } = "";
        public string Kolor { get; set; } = "";
        public string Ikona { get; set; } = "";
    }

    #endregion
}
