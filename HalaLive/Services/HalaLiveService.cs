using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;
using Kalendarz1.HalaLive.Models;

namespace Kalendarz1.HalaLive.Services
{
    /// <summary>
    /// Cross-DB queries dla dashboarda HalaLive (Sergiusz #10 — frustracja TOP 1).
    /// Ładuje wszystko równolegle przez Task.WhenAll, fail-safe: jeśli jedno źródło nie odpowiada,
    /// reszta wyświetla się normalnie.
    /// </summary>
    public class HalaLiveService
    {
        private const string ConnUnisystem =
            "Server=192.168.0.23\\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;TrustServerCertificate=True";
        private const string ConnLibraNet =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        /// <summary>Ładuje wszystkie metryki równolegle. Czas: ~2-3s przy normalnym ruchu.</summary>
        public async Task<HalaLiveData> LoadAllAsync()
        {
            var data = new HalaLiveData();

            // Wszystkie 6 metryk równolegle — fail-isolated (każda w try/catch)
            var tPracownicy = LoadPracownicySafe(data);
            var tZywiec = LoadZywiecSafe(data);
            var tKlasy = LoadKlasySafe(data);
            var tPalety = LoadPaletySafe(data);
            var tWydania = LoadWydaniaSafe(data);
            var tHodowcy = LoadHodowcyTopSafe(data);

            await Task.WhenAll(tPracownicy, tZywiec, tKlasy, tPalety, tWydania, tHodowcy);

            return data;
        }

        // 1. PRACOWNICY OBECNI (UNISYSTEM RCP)
        private async Task LoadPracownicySafe(HalaLiveData d)
        {
            try
            {
                using var conn = new SqlConnection(ConnUnisystem);
                await conn.OpenAsync();

                // Pracownicy obecni dziś: ktoś z rejestracją wejścia w ciągu ostatnich 16h
                // bez wyjścia (lub ostatnie zdarzenie to wejście)
                const string sql = @"
                    SELECT COUNT(DISTINCT r.KDINAR_EMPLOYEE_ID)
                    FROM V_KDINAR_ALL_REGISTRATIONS r
                    WHERE r.KDINAR_REGISTRTN_DATETIME >= DATEADD(HOUR, -16, GETDATE())
                      AND r.KDINAR_EMPLOYEE_ID IS NOT NULL";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 10 };
                var result = await cmd.ExecuteScalarAsync();
                d.PracownicyObecni = (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                d.ErrorUnisystem = ex.Message;
                d.PracownicyObecni = 0;
            }
        }

        // 2. PRZYCHÓD ŻYWCA DZIŚ (LibraNet In0E)
        private async Task LoadZywiecSafe(HalaLiveData d)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibraNet);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT
                        ISNULL(SUM(e.ActWeight), 0) AS SumKg,
                        COUNT(*) AS LiczbaWazen,
                        COUNT(DISTINCT e.P1) AS LiczbaPartii
                    FROM dbo.In0E e
                    WHERE CAST(e.Data AS DATE) = CAST(GETDATE() AS DATE)
                      AND e.ActWeight > 0";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 20 };
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    d.ZywiecKgDzis = r.IsDBNull(0) ? 0 : Convert.ToDecimal(r.GetValue(0));
                    d.ZywiecLiczbaSztuk = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                    d.ZywiecLiczbaDostaw = r.IsDBNull(2) ? 0 : r.GetInt32(2);
                }
            }
            catch (Exception ex)
            {
                d.ErrorLibraNet = ex.Message;
            }
        }

        // 3. KLASY A/B (In0E.QntInCont 4-12)
        private async Task LoadKlasySafe(HalaLiveData d)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibraNet);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT e.QntInCont, COUNT(*), ISNULL(SUM(e.ActWeight), 0)
                    FROM dbo.In0E e
                    WHERE e.QntInCont BETWEEN 4 AND 12
                      AND CAST(e.Data AS DATE) = CAST(GETDATE() AS DATE)
                      AND e.ActWeight > 0
                    GROUP BY e.QntInCont
                    ORDER BY e.QntInCont";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 20 };
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    d.Klasy.Add(new KlasaWagowa
                    {
                        Klasa = r.IsDBNull(0) ? 0 : Convert.ToInt32(r.GetValue(0)),
                        Liczba = r.IsDBNull(1) ? 0 : r.GetInt32(1),
                        SumaKg = r.IsDBNull(2) ? 0 : Convert.ToDecimal(r.GetValue(2))
                    });
                }
            }
            catch (Exception ex)
            {
                d.ErrorLibraNet = ex.Message;
            }
        }

        // 5. PALETY/WAZENIA DZIŚ
        private async Task LoadPaletySafe(HalaLiveData d)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibraNet);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT
                        COUNT(DISTINCT P1) AS Palety,
                        COUNT(*) AS Wazenia
                    FROM dbo.In0E
                    WHERE CAST(Data AS DATE) = CAST(GETDATE() AS DATE)
                      AND ActWeight > 0
                      AND P1 IS NOT NULL AND P1 <> ''";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 20 };
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    d.PaletyDzis = r.IsDBNull(0) ? 0 : r.GetInt32(0);
                    d.WazeniaDzis = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                }
            }
            catch (Exception ex)
            {
                d.ErrorLibraNet = ex.Message;
            }
        }

        // 4. WYDANIA DZIŚ (HANDEL HM.MG seria sWZ)
        private async Task LoadWydaniaSafe(HalaLiveData d)
        {
            try
            {
                using var conn = new SqlConnection(ConnHandel);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT
                        ISNULL(SUM(ABS(MZ.ilosc)), 0) AS WydaneKg,
                        COUNT(DISTINCT MG.id) AS LiczbaDokumentow
                    FROM [HANDEL].[HM].[MG] MG
                    INNER JOIN [HANDEL].[HM].[MZ] MZ ON MZ.super = MG.id
                    INNER JOIN [HANDEL].[HM].[TW] TW ON MZ.idtw = TW.id
                    WHERE MG.anulowany = 0
                      AND MG.seria IN ('sWZ', 'sWZ-W', 'WZ', 'WZ-W')
                      AND TW.katalog IN (67095, 67104, 67153)
                      AND CAST(MG.data AS DATE) = CAST(GETDATE() AS DATE)";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    d.WydaniaKgDzis = r.IsDBNull(0) ? 0 : Convert.ToDecimal(r.GetValue(0));
                    d.WydaniaLiczbaDokumentow = r.IsDBNull(1) ? 0 : r.GetInt32(1);
                }
            }
            catch (Exception ex)
            {
                d.ErrorHandel = ex.Message;
            }
        }

        // 6. HODOWCY DZIŚ (LibraNet listapartii + PartiaDostawca)
        private async Task LoadHodowcyTopSafe(HalaLiveData d)
        {
            try
            {
                using var conn = new SqlConnection(ConnLibraNet);
                await conn.OpenAsync();
                const string sql = @"
                    SELECT TOP 5
                        pd.CustomerID,
                        pd.CustomerName,
                        COUNT(DISTINCT e.P1) AS LiczbaPartii,
                        ISNULL(SUM(e.ActWeight), 0) AS SumaKg
                    FROM dbo.In0E e
                    INNER JOIN dbo.PartiaDostawca pd ON e.P1 = pd.Partia
                    WHERE CAST(e.Data AS DATE) = CAST(GETDATE() AS DATE)
                      AND e.ActWeight > 0
                      AND pd.CustomerName IS NOT NULL AND pd.CustomerName <> ''
                    GROUP BY pd.CustomerID, pd.CustomerName
                    ORDER BY SUM(e.ActWeight) DESC";
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 20 };
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    d.HodowcyTop.Add(new HodowcaDzis
                    {
                        Id = r.IsDBNull(0) ? "" : r.GetValue(0).ToString() ?? "",
                        Nazwa = r.IsDBNull(1) ? "" : r.GetString(1),
                        LiczbaPartii = r.IsDBNull(2) ? 0 : r.GetInt32(2),
                        SumaKg = r.IsDBNull(3) ? 0 : Convert.ToDecimal(r.GetValue(3))
                    });
                }
            }
            catch (Exception ex)
            {
                d.ErrorLibraNet = ex.Message;
            }
        }
    }
}
