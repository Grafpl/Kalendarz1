using Kalendarz1.Customer360.Models;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kalendarz1.Customer360.Services
{
    /// <summary>
    /// Pulpit portfela — zbiorcze dane finansowe WSZYSTKICH klientów (jedno zapytanie do HANDEL).
    /// Zasila landing: alerty kredytowe, churn, ranking top klientów.
    /// </summary>
    public class PortfelService
    {
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        public string? LastError { get; private set; }

        /// <summary>Pobiera portfel: per klient obrót 12M, saldo, przeterminowane, ostatnia faktura.</summary>
        public async Task<List<PortfelKlient>> GetPortfelAsync()
        {
            LastError = null;
            var lista = new List<PortfelKlient>();
            try
            {
                await using var cn = new SqlConnection(ConnHandel);
                await cn.OpenAsync();

                // Jedno zbiorcze zapytanie: agregacja per khid.
                // LEFT JOIN HM.DK + okno 60M: zlapac rowniez klientow uspionych (z dlugiem ze starszych faktur).
                // HAVING COUNT(DK.id) > 0: bez zadnej faktury w 60M klient nie pojawia sie w pulpicie.
                // PN.Termin = MIN: najwczesniejszy termin rat — MAX chowal pierwotne przeterminowanie po reschedule.
                // OUTER APPLY MAX: deterministyczny handlowiec (TOP 1 bez ORDER BY zwracal losowy wiersz przy >1 wpisach).
                const string sql = @"
                    SELECT C.Id,
                           ISNULL(NULLIF(LTRIM(RTRIM(C.Shortcut)),''), ISNULL(C.Name,'')) AS Nazwa,
                           ISNULL(WYM.Handlowiec,'') AS Handlowiec,
                           ISNULL(C.LimitAmount, 0) AS Limit,
                           ISNULL(SUM(CASE WHEN DK.data >= DATEADD(MONTH,-12,GETDATE()) THEN DK.walbrutto ELSE 0 END), 0) AS Obrot12M,
                           ISNULL(SUM(CASE WHEN (DK.walbrutto - ISNULL(PN.Rozl,0)) > 0.01 THEN DK.walbrutto - ISNULL(PN.Rozl,0) ELSE 0 END), 0) AS DoZaplaty,
                           ISNULL(SUM(CASE WHEN (DK.walbrutto - ISNULL(PN.Rozl,0)) > 0.01 AND GETDATE() > ISNULL(PN.Termin, DK.plattermin)
                                           THEN DK.walbrutto - ISNULL(PN.Rozl,0) ELSE 0 END), 0) AS Przeterm,
                           ISNULL(MAX(CASE WHEN (DK.walbrutto - ISNULL(PN.Rozl,0)) > 0.01 AND GETDATE() > ISNULL(PN.Termin, DK.plattermin)
                                           THEN DATEDIFF(DAY, ISNULL(PN.Termin, DK.plattermin), GETDATE()) ELSE 0 END), 0) AS MaxDni,
                           MAX(DK.data) AS OstatniaFaktura
                    FROM [HANDEL].[SSCommon].[STContractors] C
                    LEFT JOIN [HANDEL].[HM].[DK] DK ON DK.khid = C.Id
                         AND DK.anulowany = 0
                         AND DK.typ_dk IN ('FVS','FVR','FVZ')
                         AND DK.data >= DATEADD(MONTH, -60, GETDATE())
                    LEFT JOIN (
                        SELECT dkid, SUM(ISNULL(kwotarozl,0)) AS Rozl, MIN(Termin) AS Termin
                        FROM [HANDEL].[HM].[PN] GROUP BY dkid
                    ) PN ON PN.dkid = DK.id
                    OUTER APPLY (
                        SELECT MAX(ISNULL(CDim_Handlowiec_Val,'')) AS Handlowiec
                        FROM [HANDEL].[SSCommon].[ContractorClassification]
                        WHERE ElementId = C.Id
                    ) WYM
                    GROUP BY C.Id, C.Shortcut, C.Name, C.LimitAmount, WYM.Handlowiec
                    HAVING COUNT(DK.id) > 0";
                await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 120 };
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    lista.Add(new PortfelKlient
                    {
                        Id = rd.GetInt32(0),
                        Nazwa = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        Handlowiec = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Limit = rd.IsDBNull(3) ? 0m : Convert.ToDecimal(rd.GetValue(3)),
                        Obrot12M = rd.IsDBNull(4) ? 0m : Convert.ToDecimal(rd.GetValue(4)),
                        DoZaplaty = rd.IsDBNull(5) ? 0m : Convert.ToDecimal(rd.GetValue(5)),
                        Przeterminowane = rd.IsDBNull(6) ? 0m : Convert.ToDecimal(rd.GetValue(6)),
                        MaxDniOpoznienia = rd.IsDBNull(7) ? 0 : Convert.ToInt32(rd.GetValue(7)),
                        OstatniaFaktura = rd.IsDBNull(8) ? null : (DateTime?)rd.GetDateTime(8)
                    });
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                System.Diagnostics.Debug.WriteLine("[Portfel] " + ex);
            }
            return lista;
        }

        /// <summary>Liczy podsumowanie portfela z listy klientów.</summary>
        public PortfelPodsumowanie Podsumuj(List<PortfelKlient> klienci, int churnProgDni = 60)
        {
            var p = new PortfelPodsumowanie
            {
                LiczbaKlientow = klienci.Count,
                ObrotPortfela12M = klienci.Sum(k => k.Obrot12M),
                SumaPrzeterminowanych = klienci.Sum(k => k.Przeterminowane),
                LiczbaZPrzeterminowanymi = klienci.Count(k => k.MaPrzeterminowane),
                LiczbaPrzekroczonyLimit = klienci.Count(k => k.PrzekroczonyLimit),
                // Churn: byli aktywni (obrót>0) ale brak faktury od > progu dni
                LiczbaChurnZagrozonych = klienci.Count(k => k.Obrot12M > 0 && k.DniOdOstatniej > churnProgDni)
            };
            decimal obrotTop10 = klienci.OrderByDescending(k => k.Obrot12M).Take(10).Sum(k => k.Obrot12M);
            p.ObrotTop10Proc = p.ObrotPortfela12M > 0 ? obrotTop10 / p.ObrotPortfela12M * 100m : 0m;
            return p;
        }
    }
}
