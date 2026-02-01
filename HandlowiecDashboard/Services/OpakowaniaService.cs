using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.HandlowiecDashboard.Configuration;
using Kalendarz1.HandlowiecDashboard.Constants;
using Kalendarz1.HandlowiecDashboard.Models;
using Kalendarz1.HandlowiecDashboard.Services.Interfaces;

namespace Kalendarz1.HandlowiecDashboard.Services
{
    public class OpakowaniaService : IOpakowaniaService
    {
        private readonly ILoggingService _logger;

        public OpakowaniaService(ILoggingService logger)
        {
            _logger = logger;
        }

        public async Task<OpakowaniaKPI> PobierzKPIAsync()
        {
            var kpi = new OpakowaniaKPI();
            
            try
            {
                await using var cn = new SqlConnection(DatabaseConfig.HandelConnectionString);
                await cn.OpenAsync();

                var sql = @"
                    -- Aktualne salda
                    SELECT 
                        ISNULL(SUM(CASE WHEN OP.id_opak = 1 THEN OP.saldo ELSE 0 END), 0) AS SumaE2,
                        ISNULL(SUM(CASE WHEN OP.id_opak = 2 THEN OP.saldo ELSE 0 END), 0) AS SumaH1,
                        COUNT(DISTINCT OP.id_kh) AS LiczbaKontrahentow
                    FROM [HANDEL].[HM].[OP_SALDO] OP
                    WHERE OP.saldo <> 0;

                    -- Liczba przekroczonych limitów (zakładam że limity są w STContractors)
                    SELECT COUNT(*) 
                    FROM [HANDEL].[HM].[OP_SALDO] OP
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON OP.id_kh = C.id
                    WHERE (OP.saldo > 50 AND OP.id_opak = 1)
                       OR (OP.saldo > 100 AND OP.id_opak = 2);

                    -- Zwroty w tym miesiącu
                    SELECT 
                        ISNULL(SUM(CASE WHEN MZ.idtw = 1 AND MZ.Ilosc < 0 THEN ABS(MZ.Ilosc) ELSE 0 END), 0),
                        ISNULL(SUM(CASE WHEN MZ.idtw = 2 AND MZ.Ilosc < 0 THEN ABS(MZ.Ilosc) ELSE 0 END), 0)
                    FROM [HANDEL].[HM].[MZ] MZ
                    WHERE MZ.data >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
                ";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = BusinessConstants.Defaults.CommandTimeoutSeconds;

                await using var reader = await cmd.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    kpi.SumaE2 = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    kpi.SumaH1 = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    kpi.LiczbaKontrahentow = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    kpi.WartoscZamrozona = kpi.SumaE2 * BusinessConstants.Opakowania.CenaE2 
                                         + kpi.SumaH1 * BusinessConstants.Opakowania.CenaH1;
                }

                await reader.NextResultAsync();
                if (await reader.ReadAsync())
                {
                    kpi.LiczbaPrzekroczonychLimitow = reader.GetInt32(0);
                }

                await reader.NextResultAsync();
                if (await reader.ReadAsync())
                {
                    kpi.ZwrotyE2Miesiac = reader.GetInt32(0);
                    kpi.ZwrotyH1Miesiac = reader.GetInt32(1);
                }

                _logger.LogDebug($"KPI opakowań: E2={kpi.SumaE2}, H1={kpi.SumaH1}, wartość={kpi.WartoscZamrozona:N0}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd pobierania KPI opakowań", ex);
            }

            return kpi;
        }

        public async Task<List<SaldoOpakowanKontrahenta>> PobierzSaldaZRyzykiemAsync(string handlowiec = null)
        {
            var wyniki = new List<SaldoOpakowanKontrahenta>();

            try
            {
                await using var cn = new SqlConnection(DatabaseConfig.HandelConnectionString);
                await cn.OpenAsync();

                var sql = @"
                    WITH SaldaOpakowan AS (
                        SELECT 
                            OP.id_kh,
                            SUM(CASE WHEN OP.id_opak = 1 THEN OP.saldo ELSE 0 END) AS SaldoE2,
                            SUM(CASE WHEN OP.id_opak = 2 THEN OP.saldo ELSE 0 END) AS SaldoH1
                        FROM [HANDEL].[HM].[OP_SALDO] OP
                        GROUP BY OP.id_kh
                        HAVING SUM(CASE WHEN OP.id_opak = 1 THEN OP.saldo ELSE 0 END) <> 0
                            OR SUM(CASE WHEN OP.id_opak = 2 THEN OP.saldo ELSE 0 END) <> 0
                    ),
                    OstatnieRuchy AS (
                        SELECT 
                            MG.khid,
                            MAX(CASE WHEN MZ.Ilosc < 0 THEN MZ.data END) AS OstatniZwrot,
                            MAX(CASE WHEN MZ.Ilosc > 0 THEN MZ.data END) AS OstatnieWydanie
                        FROM [HANDEL].[HM].[MZ] MZ
                        INNER JOIN [HANDEL].[HM].[MG] MG ON MZ.super = MG.id
                        WHERE MG.anulowany = 0
                        GROUP BY MG.khid
                    ),
                    Zaleglosci AS (
                        SELECT 
                            DK.khid,
                            SUM(CASE WHEN DK.walbrutto - ISNULL(PN.Rozliczone, 0) > 0.01 
                                      AND GETDATE() > DK.plattermin 
                                THEN DK.walbrutto - ISNULL(PN.Rozliczone, 0) ELSE 0 END) AS Zaleglosci
                        FROM [HANDEL].[HM].[DK] DK
                        LEFT JOIN (
                            SELECT dkid, SUM(ISNULL(kwotarozl, 0)) AS Rozliczone FROM [HANDEL].[HM].[PN] GROUP BY dkid
                        ) PN ON DK.id = PN.dkid
                        WHERE DK.anulowany = 0
                        GROUP BY DK.khid
                    )
                    SELECT 
                        C.id AS KontrahentId,
                        C.shortcut AS Kontrahent,
                        ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
                        ISNULL(S.SaldoE2, 0) AS SaldoE2,
                        ISNULL(S.SaldoH1, 0) AS SaldoH1,
                        50 AS LimitE2,
                        100 AS LimitH1,
                        R.OstatniZwrot,
                        R.OstatnieWydanie,
                        ISNULL(ZAL.Zaleglosci, 0) AS Zaleglosci
                    FROM SaldaOpakowan S
                    INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON S.id_kh = C.id
                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON C.id = WYM.ElementId
                    LEFT JOIN OstatnieRuchy R ON C.id = R.khid
                    LEFT JOIN Zaleglosci ZAL ON C.id = ZAL.khid
                ";

                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != BusinessConstants.Filtry.WszyscyHandlowcy)
                {
                    sql += " WHERE ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') = @Handlowiec";
                }

                sql += " ORDER BY (ISNULL(S.SaldoE2, 0) * 85 + ISNULL(S.SaldoH1, 0) * 25) DESC";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = BusinessConstants.Defaults.CommandTimeoutSeconds;
                
                if (!string.IsNullOrEmpty(handlowiec) && handlowiec != BusinessConstants.Filtry.WszyscyHandlowcy)
                    cmd.Parameters.AddWithValue("@Handlowiec", handlowiec);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var saldo = new SaldoOpakowanKontrahenta
                    {
                        KontrahentId = reader.GetInt32(0),
                        Kontrahent = reader.GetString(1),
                        Handlowiec = reader.GetString(2),
                        SaldoE2 = reader.GetInt32(3),
                        SaldoH1 = reader.GetInt32(4),
                        LimitE2 = reader.GetInt32(5),
                        LimitH1 = reader.GetInt32(6),
                        OstatniZwrot = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                        OstatnieWydanie = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        ZaleglosciPlatnosci = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9)
                    };

                    saldo.RiskScore = ObliczRiskScore(saldo);
                    wyniki.Add(saldo);
                }

                _logger.LogInfo($"Pobrano salda opakowań dla {wyniki.Count} kontrahentów");
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd pobierania sald opakowań", ex);
            }

            return wyniki;
        }

        private double ObliczRiskScore(SaldoOpakowanKontrahenta saldo)
        {
            double score = 0;

            // 1. Dni od ostatniego zwrotu (0-30 pkt)
            var dni = saldo.DniOdOstatniegoZwrotu;
            if (dni > BusinessConstants.Opakowania.ProgKrytyczny) score += 30;
            else if (dni > BusinessConstants.Opakowania.ProgOstrzezenie) score += 22;
            else if (dni > BusinessConstants.Opakowania.ProgUwaga) score += 15;
            else if (dni > BusinessConstants.Opakowania.ProgNorma) score += 8;

            // 2. Wartość opakowań (0-25 pkt)
            var wartosc = saldo.WartoscCalkowita;
            if (wartosc > 15000) score += 25;
            else if (wartosc > 10000) score += 18;
            else if (wartosc > 5000) score += 10;

            // 3. Przekroczenie limitów (0-20 pkt)
            if (saldo.PrzekroczonyLimitE2) score += 12;
            if (saldo.PrzekroczonyLimitH1) score += 8;

            // 4. Zaległości płatnicze (0-15 pkt)
            if (saldo.ZaleglosciPlatnosci > 10000) score += 15;
            else if (saldo.ZaleglosciPlatnosci > 5000) score += 10;
            else if (saldo.ZaleglosciPlatnosci > 0) score += 5;

            // 5. Brak jakichkolwiek zwrotów (0-10 pkt)
            if (!saldo.OstatniZwrot.HasValue && saldo.WartoscCalkowita > 1000) score += 10;

            return Math.Min(100, score);
        }

        public async Task<List<AgingOpakowan>> PobierzAgingAsync()
        {
            var aging = new List<AgingOpakowan>
            {
                new() { Przedzial = "0-30 dni", MinDni = 0, MaxDni = 30, Kolor = BusinessConstants.Kolory.Sukces },
                new() { Przedzial = "31-60 dni", MinDni = 31, MaxDni = 60, Kolor = BusinessConstants.Kolory.Ostrzezenie },
                new() { Przedzial = "61-90 dni", MinDni = 61, MaxDni = 90, Kolor = BusinessConstants.Kolory.Uwaga },
                new() { Przedzial = ">90 dni", MinDni = 91, MaxDni = 9999, Kolor = BusinessConstants.Kolory.Niebezpieczenstwo }
            };

            try
            {
                var salda = await PobierzSaldaZRyzykiemAsync();
                
                foreach (var a in aging)
                {
                    var w = salda.Where(s => s.DniOdOstatniegoZwrotu >= a.MinDni && s.DniOdOstatniegoZwrotu <= a.MaxDni).ToList();
                    a.IloscE2 = w.Sum(s => s.SaldoE2);
                    a.IloscH1 = w.Sum(s => s.SaldoH1);
                    a.Wartosc = w.Sum(s => s.WartoscCalkowita);
                    a.LiczbaKontrahentow = w.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd obliczania aging opakowań", ex);
            }

            return aging;
        }

        public async Task<List<TrendOpakowan>> PobierzTrendAsync(int miesiecy = 12)
        {
            var trend = new List<TrendOpakowan>();
            // TODO: Implementacja - zapytanie o historyczne salda
            _logger.LogWarning("PobierzTrendAsync - wymaga implementacji");
            return trend;
        }

        public async Task<List<AlertOpakowania>> PobierzAlertyAsync()
        {
            var alerty = new List<AlertOpakowania>();
            
            try
            {
                var salda = await PobierzSaldaZRyzykiemAsync();
                int id = 1;

                foreach (var s in salda.Where(x => x.RiskScore >= 40).OrderByDescending(x => x.RiskScore))
                {
                    var alert = new AlertOpakowania
                    {
                        Id = id++,
                        Kontrahent = s.Kontrahent,
                        Handlowiec = s.Handlowiec,
                        DataUtworzenia = DateTime.Now
                    };

                    if (s.RiskScore >= 80)
                    {
                        alert.Typ = "KRYTYCZNY";
                        alert.Komunikat = $"{s.WartoscTekst}, {s.DniOdZwrotuTekst} bez zwrotu" +
                                          (s.MaZaleglosciPlatnosci ? $", zaległości {s.ZaleglosciTekst}!" : "");
                    }
                    else if (s.RiskScore >= 60)
                    {
                        alert.Typ = "OSTRZEZENIE";
                        alert.Komunikat = s.PrzekroczonyLimitE2 || s.PrzekroczonyLimitH1
                            ? $"Przekroczony limit: E2 {s.LimitE2Tekst}, H1 {s.LimitH1Tekst}"
                            : $"{s.DniOdZwrotuTekst} od zwrotu, wartość {s.WartoscTekst}";
                    }
                    else
                    {
                        alert.Typ = "INFO";
                        alert.Komunikat = $"E2: {s.SaldoE2}, H1: {s.SaldoH1} - monitoruj";
                    }

                    alerty.Add(alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd generowania alertów opakowań", ex);
            }

            return alerty;
        }

        public async Task<List<RiskMapPoint>> PobierzMapeRyzykaAsync(string handlowiec = null)
        {
            var punkty = new List<RiskMapPoint>();
            
            try
            {
                var salda = await PobierzSaldaZRyzykiemAsync(handlowiec);
                
                foreach (var s in salda)
                {
                    punkty.Add(new RiskMapPoint
                    {
                        Kontrahent = s.Kontrahent,
                        Handlowiec = s.Handlowiec,
                        X = Math.Min(180, s.DniOdOstatniegoZwrotu),  // Cap at 180 dni
                        Y = (double)s.WartoscCalkowita,
                        Size = 10 + Math.Min(40, (double)s.WartoscCalkowita / 500),  // 10-50 px
                        Kolor = s.MaZaleglosciPlatnosci ? BusinessConstants.Kolory.Niebezpieczenstwo : s.KolorRyzyka,
                        RiskScore = s.RiskScore
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Błąd generowania mapy ryzyka", ex);
            }

            return punkty;
        }
    }
}
