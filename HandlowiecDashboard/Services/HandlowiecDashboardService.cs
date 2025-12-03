using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.HandlowiecDashboard.Models;

namespace Kalendarz1.HandlowiecDashboard.Services
{
    /// <summary>
    /// Serwis danych dla Dashboard Handlowca
    /// </summary>
    public class HandlowiecDashboardService
    {
        private readonly string _connectionStringHandel;
        private readonly string _connectionStringLibraNet;
        private static readonly CultureInfo _pl = new CultureInfo("pl-PL");

        private static readonly string[] _miesiace = {
            "", "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
            "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień"
        };

        public HandlowiecDashboardService()
        {
            _connectionStringHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
            _connectionStringLibraNet = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        /// <summary>
        /// Pobiera KPI dla handlowca (bieżący miesiąc vs poprzedni)
        /// </summary>
        public async Task<HandlowiecKPI> PobierzKPIAsync(string handlowiec = null)
        {
            var kpi = new HandlowiecKPI();
            var dzis = DateTime.Today;
            var pierwszyDzienMiesiaca = new DateTime(dzis.Year, dzis.Month, 1);
            var pierwszyDzienPoprzedniego = pierwszyDzienMiesiaca.AddMonths(-1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                // Bieżący miesiąc
                var sqlBiezacy = @"
                    SELECT
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                        COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataOd AND z.DataOdbioru < @DataDo
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sqlBiezacy += " AND z.Handlowiec = @Handlowiec";

                await using (var cmd = new SqlCommand(sqlBiezacy, cn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", pierwszyDzienMiesiaca);
                    cmd.Parameters.AddWithValue("@DataDo", pierwszyDzienMiesiaca.AddMonths(1));
                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        kpi.LiczbaZamowienMiesiac = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        kpi.SumaKgMiesiac = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                        kpi.SumaWartoscMiesiac = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                        kpi.LiczbaOdbiorcowMiesiac = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        kpi.SredniWartoscZamowienia = kpi.LiczbaZamowienMiesiac > 0
                            ? kpi.SumaWartoscMiesiac / kpi.LiczbaZamowienMiesiac
                            : 0;
                    }
                }

                // Poprzedni miesiąc
                var sqlPoprzedni = sqlBiezacy;
                await using (var cmd = new SqlCommand(sqlPoprzedni, cn))
                {
                    cmd.Parameters.AddWithValue("@DataOd", pierwszyDzienPoprzedniego);
                    cmd.Parameters.AddWithValue("@DataDo", pierwszyDzienMiesiaca);
                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        kpi.LiczbaZamowienPoprzedni = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        kpi.SumaKgPoprzedni = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                        kpi.SumaWartoscPoprzedni = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                        kpi.LiczbaOdbiorcowPoprzedni = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania KPI: {ex.Message}");
            }

            return kpi;
        }

        /// <summary>
        /// Pobiera dane miesięczne dla wykresów (ostatnie 12 miesięcy)
        /// </summary>
        public async Task<List<DaneMiesieczne>> PobierzDaneMiesieczneAsync(string handlowiec = null, int liczbaMiesiecy = 12)
        {
            var dane = new List<DaneMiesieczne>();
            var dzis = DateTime.Today;
            var dataStart = new DateTime(dzis.Year, dzis.Month, 1).AddMonths(-liczbaMiesiecy + 1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        YEAR(z.DataOdbioru) as Rok,
                        MONTH(z.DataOdbioru) as Miesiac,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                        COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY YEAR(z.DataOdbioru), MONTH(z.DataOdbioru)
                    ORDER BY Rok, Miesiac";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var rok = reader.GetInt32(0);
                    var miesiac = reader.GetInt32(1);
                    dane.Add(new DaneMiesieczne
                    {
                        Rok = rok,
                        Miesiac = miesiac,
                        MiesiacNazwa = _miesiace[miesiac],
                        LiczbaZamowien = reader.GetInt32(2),
                        SumaKg = reader.GetDecimal(3),
                        SumaWartosc = reader.GetDecimal(4),
                        LiczbaOdbiorcow = reader.GetInt32(5),
                        SredniaCena = reader.GetDecimal(3) > 0 ? reader.GetDecimal(4) / reader.GetDecimal(3) : 0
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania danych miesięcznych: {ex.Message}");
            }

            return dane;
        }

        /// <summary>
        /// Pobiera top odbiorców handlowca
        /// </summary>
        public async Task<List<TopOdbiorca>> PobierzTopOdbiorcowAsync(string handlowiec = null, int limit = 10, int miesiecy = 3)
        {
            var odbiorcy = new List<TopOdbiorca>();
            var dataStart = DateTime.Today.AddMonths(-miesiecy);
            decimal sumaCalkowita = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP (@Limit)
                        z.OdbiorcaId,
                        k.Nazwa,
                        k.Miasto,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    LEFT JOIN Kontrahenci k ON z.OdbiorcaId = k.id
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY z.OdbiorcaId, k.Nazwa, k.Miasto
                    ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Limit", limit);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                int pozycja = 1;
                while (await reader.ReadAsync())
                {
                    var odbiorca = new TopOdbiorca
                    {
                        Pozycja = pozycja++,
                        OdbiorcaId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Nazwa = reader.IsDBNull(1) ? "Nieznany" : reader.GetString(1),
                        Miasto = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        LiczbaZamowien = reader.GetInt32(3),
                        SumaKg = reader.GetDecimal(4),
                        SumaWartosc = reader.GetDecimal(5)
                    };
                    sumaCalkowita += odbiorca.SumaWartosc;
                    odbiorcy.Add(odbiorca);
                }

                // Oblicz udział procentowy
                foreach (var o in odbiorcy)
                {
                    o.UdzialProcent = sumaCalkowita > 0 ? (o.SumaWartosc / sumaCalkowita) * 100 : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania top odbiorców: {ex.Message}");
            }

            return odbiorcy;
        }

        /// <summary>
        /// Pobiera statystyki kategorii produktów
        /// </summary>
        public async Task<List<KategoriaProduktow>> PobierzKategorieProduktowAsync(string handlowiec = null, int miesiecy = 1)
        {
            var kategorie = new List<KategoriaProduktow>();
            var dataStart = DateTime.Today.AddMonths(-miesiecy);
            decimal sumaCalkowita = 0;

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        CASE
                            WHEN zp.Katalog = '67153' THEN 'Mrożonki'
                            ELSE 'Świeże'
                        END as Kategoria,
                        zp.Katalog,
                        COUNT(DISTINCT z.ID) as LiczbaZamowien,
                        ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc
                    FROM ZamowieniaMieso z
                    INNER JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    WHERE z.DataOdbioru >= @DataStart
                        AND (z.Anulowane IS NULL OR z.Anulowane = 0)
                        AND zp.Ilosc > 0";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY
                        CASE WHEN zp.Katalog = '67153' THEN 'Mrożonki' ELSE 'Świeże' END,
                        zp.Katalog
                    ORDER BY SumaWartosc DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@DataStart", dataStart);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var kat = new KategoriaProduktow
                    {
                        Nazwa = reader.GetString(0),
                        Kod = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        LiczbaZamowien = reader.GetInt32(2),
                        SumaKg = reader.GetDecimal(3),
                        SumaWartosc = reader.GetDecimal(4)
                    };
                    sumaCalkowita += kat.SumaKg;
                    kategorie.Add(kat);
                }

                foreach (var k in kategorie)
                {
                    k.UdzialProcent = sumaCalkowita > 0 ? (k.SumaKg / sumaCalkowita) * 100 : 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania kategorii: {ex.Message}");
            }

            return kategorie;
        }

        /// <summary>
        /// Pobiera ostatnie zamówienia
        /// </summary>
        public async Task<List<OstatnieZamowienie>> PobierzOstatnieZamowieniaAsync(string handlowiec = null, int limit = 20)
        {
            var zamowienia = new List<OstatnieZamowienie>();

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT TOP (@Limit)
                        z.ID,
                        z.DataOdbioru,
                        k.Nazwa as Odbiorca,
                        ISNULL(SUM(zp.Ilosc), 0) as Kg,
                        ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as Wartosc,
                        CASE WHEN z.Anulowane = 1 THEN 'Anulowane' ELSE 'Aktywne' END as Status,
                        ISNULL(z.TransportStatus, 'Firma') as TransportStatus
                    FROM ZamowieniaMieso z
                    LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                    LEFT JOIN Kontrahenci k ON z.OdbiorcaId = k.id
                    WHERE 1=1";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    sql += " AND z.Handlowiec = @Handlowiec";

                sql += @"
                    GROUP BY z.ID, z.DataOdbioru, k.Nazwa, z.Anulowane, z.TransportStatus
                    ORDER BY z.DataOdbioru DESC, z.ID DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Limit", limit);
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    zamowienia.Add(new OstatnieZamowienie
                    {
                        Id = reader.GetInt32(0),
                        DataOdbioru = reader.GetDateTime(1),
                        Odbiorca = reader.IsDBNull(2) ? "Nieznany" : reader.GetString(2),
                        Kg = reader.GetDecimal(3),
                        Wartosc = reader.GetDecimal(4),
                        Status = reader.GetString(5),
                        TransportStatus = reader.GetString(6)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania zamówień: {ex.Message}");
            }

            return zamowienia;
        }

        /// <summary>
        /// Pobiera listę handlowców
        /// </summary>
        public async Task<List<string>> PobierzHandlowcowAsync()
        {
            var handlowcy = new List<string> { "— Wszyscy —" };

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                var sql = @"
                    SELECT DISTINCT Handlowiec
                    FROM ZamowieniaMieso
                    WHERE Handlowiec IS NOT NULL AND Handlowiec != ''
                    ORDER BY Handlowiec";

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
        /// Pobiera porównanie okresów (ostatnie 6 miesięcy)
        /// </summary>
        public async Task<List<PorownanieOkresu>> PobierzPorownanieOkresowAsync(string handlowiec = null)
        {
            var porownanie = new List<PorownanieOkresu>();
            var dzis = DateTime.Today;
            var biezacyMiesiac = new DateTime(dzis.Year, dzis.Month, 1);

            try
            {
                await using var cn = new SqlConnection(_connectionStringHandel);
                await cn.OpenAsync();

                for (int i = 0; i < 6; i++)
                {
                    var dataStart = biezacyMiesiac.AddMonths(-i);
                    var dataKoniec = dataStart.AddMonths(1);

                    var sql = @"
                        SELECT
                            COUNT(DISTINCT z.ID) as LiczbaZamowien,
                            ISNULL(SUM(zp.Ilosc), 0) as SumaKg,
                            ISNULL(SUM(zp.Ilosc * CAST(ISNULL(zp.Cena, 0) AS DECIMAL(18,2))), 0) as SumaWartosc,
                            COUNT(DISTINCT z.OdbiorcaId) as LiczbaOdbiorcow
                        FROM ZamowieniaMieso z
                        LEFT JOIN ZamowieniaMiesoPozycje zp ON z.ID = zp.ZamowienieId
                        WHERE z.DataOdbioru >= @DataStart AND z.DataOdbioru < @DataKoniec
                            AND (z.Anulowane IS NULL OR z.Anulowane = 0)";

                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        sql += " AND z.Handlowiec = @Handlowiec";

                    await using var cmd = new SqlCommand(sql, cn);
                    cmd.Parameters.AddWithValue("@DataStart", dataStart);
                    cmd.Parameters.AddWithValue("@DataKoniec", dataKoniec);
                    if (!string.IsNullOrEmpty(handlowiec) && handlowiec != "— Wszyscy —")
                        cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        porownanie.Add(new PorownanieOkresu
                        {
                            OkresNazwa = $"{_miesiace[dataStart.Month]} {dataStart.Year}",
                            LiczbaZamowien = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                            SumaKg = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1),
                            SumaWartosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                            LiczbaOdbiorcow = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            JestBiezacy = i == 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd pobierania porównania: {ex.Message}");
            }

            return porownanie;
        }
    }
}
