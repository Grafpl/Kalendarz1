using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.HandlowiecDashboard.Configuration;
using Kalendarz1.HandlowiecDashboard.Constants;
using Kalendarz1.HandlowiecDashboard.Models;
using Kalendarz1.HandlowiecDashboard.Services.Interfaces;

namespace Kalendarz1.HandlowiecDashboard.Services
{
    /// <summary>
    /// Serwis do pobierania danych o realizacji celów sprzedażowych
    /// </summary>
    public class CeleService : ICeleService
    {
        private readonly ILoggingService _logger;

        public CeleService(ILoggingService logger)
        {
            _logger = logger;
        }

        public async Task<RealizacjaCelu> PobierzRealizacjeCeluAsync(string handlowiec, int rok, int miesiac)
        {
            var realizacja = new RealizacjaCelu
            {
                Handlowiec = handlowiec,
                Rok = rok,
                Miesiac = miesiac,
                CelWartoscZl = BusinessConstants.Defaults.DomyslnyCelMiesiecznyZl,
                CelKg = BusinessConstants.Defaults.DomyslnyCelMiesiecznyKg,
                CelLiczbaKlientow = BusinessConstants.Defaults.DomyslnyCelLiczbaKlientow
            };

            try
            {
                await using var cn = new SqlConnection(DatabaseConfig.HandelConnectionString);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        ISNULL(SUM(DP.wartNetto), 0) AS WartoscZl,
                        ISNULL(SUM(DP.ilosc), 0) AS Kg,
                        COUNT(DISTINCT DK.khid) AS LiczbaKlientow
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok
                      AND MONTH(DK.data) = @Miesiac
                      AND ISNULL(WYM.CDim_Handlowiec_Val, @Nieprzypisany) = @Handlowiec";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = BusinessConstants.Defaults.CommandTimeoutSeconds;
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);
                cmd.Parameters.AddWithValue("@Nieprzypisany", BusinessConstants.Filtry.Nieprzypisany);

                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    realizacja.AktualnaWartoscZl = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                    realizacja.AktualneKg = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                    realizacja.AktualnaLiczbaKlientow = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                }

                _logger.LogDebug($"Pobrano realizację celu dla {handlowiec}: {realizacja.RealizacjaWartoscProcent:F1}%");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Błąd pobierania realizacji celu dla {handlowiec}", ex);
            }

            return realizacja;
        }

        public async Task<List<RealizacjaCelu>> PobierzRealizacjeWszystkichAsync(int rok, int miesiac)
        {
            var wyniki = new List<RealizacjaCelu>();

            try
            {
                await using var cn = new SqlConnection(DatabaseConfig.HandelConnectionString);
                await cn.OpenAsync();

                var sql = @"
                    SELECT
                        ISNULL(WYM.CDim_Handlowiec_Val, @Nieprzypisany) AS Handlowiec,
                        ISNULL(SUM(DP.wartNetto), 0) AS WartoscZl,
                        ISNULL(SUM(DP.ilosc), 0) AS Kg,
                        COUNT(DISTINCT DK.khid) AS LiczbaKlientow
                    FROM [HANDEL].[HM].[DK] DK
                    INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                    WHERE YEAR(DK.data) = @Rok
                      AND MONTH(DK.data) = @Miesiac
                      AND ISNULL(WYM.CDim_Handlowiec_Val, @Nieprzypisany) NOT IN ('Ogolne', 'Ogólne')
                    GROUP BY ISNULL(WYM.CDim_Handlowiec_Val, @Nieprzypisany)
                    ORDER BY WartoscZl DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = BusinessConstants.Defaults.CommandTimeoutSeconds;
                cmd.Parameters.AddWithValue("@Rok", rok);
                cmd.Parameters.AddWithValue("@Miesiac", miesiac);
                cmd.Parameters.AddWithValue("@Nieprzypisany", BusinessConstants.Filtry.Nieprzypisany);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    wyniki.Add(new RealizacjaCelu
                    {
                        Handlowiec = reader.GetString(0),
                        Rok = rok,
                        Miesiac = miesiac,
                        AktualnaWartoscZl = Convert.ToDecimal(reader.GetValue(1)),
                        AktualneKg = Convert.ToDecimal(reader.GetValue(2)),
                        AktualnaLiczbaKlientow = reader.GetInt32(3),
                        CelWartoscZl = BusinessConstants.Defaults.DomyslnyCelMiesiecznyZl,
                        CelKg = BusinessConstants.Defaults.DomyslnyCelMiesiecznyKg,
                        CelLiczbaKlientow = BusinessConstants.Defaults.DomyslnyCelLiczbaKlientow
                    });
                }

                _logger.LogInfo($"Pobrano realizacje celów dla {wyniki.Count} handlowców ({miesiac}/{rok})");
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd pobierania realizacji celów wszystkich handlowców", ex);
            }

            return wyniki;
        }

        public async Task ZapiszCelAsync(CelHandlowca cel)
        {
            // TODO: Implementacja zapisu celu do bazy gdy będzie tabela celów
            _logger.LogWarning("ZapiszCelAsync - nie zaimplementowano");
            await Task.CompletedTask;
        }
    }
}
