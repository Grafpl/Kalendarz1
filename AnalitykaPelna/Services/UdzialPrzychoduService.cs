using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Procentowy udział towarów w przychodzie (HANDEL Sage: FVS/FVR/FVZ).
    /// Granulacja D/T/M. Mianownik % = suma przychodu WSZYSTKICH towarów w danym okresie.
    /// </summary>
    public class UdzialPrzychoduService
    {
        private readonly string _connHandel;

        public UdzialPrzychoduService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _connHandel = AnalitykaConfig.ConnHandel;
        }

        public UdzialPrzychoduService(string connHandel)
        {
            _connHandel = connHandel;
        }

        /// <summary>
        /// Lista wszystkich towarów które miały przychód w zakresie + ich łączny przychód (do sortowania w pickerze).
        /// </summary>
        public async Task<List<TowarPickerItem>> LoadTowaryAsync(DateTime dataOd, DateTime dataDo)
        {
            var lista = new List<TowarPickerItem>();

            const string sql = @"
                SELECT
                    TW.id              AS IdTw,
                    TW.kod             AS Kod,
                    TW.nazwa           AS Nazwa,
                    CAST(TW.katalog AS NVARCHAR(20)) AS Katalog,
                    SUM(DP.ilosc * DP.cena) AS Wartosc,
                    SUM(DP.ilosc)           AS Ilosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                WHERE DK.anulowany = 0
                  AND DK.typ_dk IN ('FVS','FVR','FVZ')
                  AND CAST(DK.data AS DATE) BETWEEN @DataOd AND @DataDo
                  AND DP.idtw IS NOT NULL
                GROUP BY TW.id, TW.kod, TW.nazwa, TW.katalog
                HAVING SUM(DP.ilosc * DP.cena) > 0
                ORDER BY SUM(DP.ilosc * DP.cena) DESC;";

            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new TowarPickerItem
                {
                    IdHandel = SqlSafe.ReadInt(reader, 0),
                    Kod = SqlSafe.ReadString(reader, 1),
                    Nazwa = SqlSafe.ReadString(reader, 2),
                    Katalog = SqlSafe.ReadString(reader, 3),
                    SumaPrzychoduPLN = SqlSafe.ReadDecimal(reader, 4),
                    SumaIloscKg = SqlSafe.ReadDecimal(reader, 5)
                });
            }
            return lista;
        }

        /// <summary>
        /// Surowe wiersze przychodu per (towar × dzień). Klient agreguje do D/T/M.
        /// </summary>
        public async Task<List<PrzychodPerOkresDay>> LoadPrzychodPerDayAsync(DateTime dataOd, DateTime dataDo)
        {
            var lista = new List<PrzychodPerOkresDay>();

            const string sql = @"
                SELECT
                    DP.idtw                   AS IdTw,
                    CAST(DK.data AS DATE)     AS Dzien,
                    SUM(DP.ilosc * DP.cena)   AS Wartosc,
                    SUM(DP.ilosc)             AS Ilosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                WHERE DK.anulowany = 0
                  AND DK.typ_dk IN ('FVS','FVR','FVZ')
                  AND CAST(DK.data AS DATE) BETWEEN @DataOd AND @DataDo
                  AND DP.idtw IS NOT NULL
                GROUP BY DP.idtw, CAST(DK.data AS DATE);";

            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new PrzychodPerOkresDay
                {
                    IdHandel = SqlSafe.ReadInt(reader, 0),
                    Data = SqlSafe.ReadDate(reader, 1),
                    Wartosc = SqlSafe.ReadDecimal(reader, 2),
                    Ilosc = SqlSafe.ReadDecimal(reader, 3)
                });
            }
            return lista;
        }

        /// <summary>
        /// Agreguje surowe dane do wybranej granulacji i liczy % udziału wybranych towarów.
        /// </summary>
        public UdzialPrzychoduDataSet Agreguj(
            List<PrzychodPerOkresDay> surowe,
            IEnumerable<TowarPickerItem> wybrane,
            DateTime dataOd,
            DateTime dataDo,
            GranulacjaCzasu granulacja)
        {
            var ds = new UdzialPrzychoduDataSet
            {
                Granulacja = granulacja,
                DataOd = dataOd,
                DataDo = dataDo,
                OsCzasu = BudujOsCzasu(dataOd, dataDo, granulacja)
            };

            // Suma globalna per okres (mianownik %)
            foreach (var row in surowe)
            {
                var klucz = PoczatekOkresu(row.Data, granulacja);
                ds.SumaCalkowitaPerOkres[klucz] = ds.SumaCalkowitaPerOkres.GetValueOrDefault(klucz) + row.Wartosc;
                ds.SumaCalkowitaPLN += row.Wartosc;
            }

            // Serie dla zaznaczonych
            var paleta = Paleta();
            int idx = 0;
            foreach (var towar in wybrane)
            {
                var seria = new TowarUdzialSeria
                {
                    IdHandel = towar.IdHandel,
                    Kod = towar.Kod,
                    Nazwa = towar.Nazwa,
                    Kolor = paleta[idx % paleta.Length]
                };
                idx++;

                var wierszeTowaru = surowe.Where(r => r.IdHandel == towar.IdHandel);
                foreach (var row in wierszeTowaru)
                {
                    var klucz = PoczatekOkresu(row.Data, granulacja);
                    seria.WartoscPerOkres[klucz] = seria.WartoscPerOkres.GetValueOrDefault(klucz) + row.Wartosc;
                    seria.SumaWartosci += row.Wartosc;
                }

                // % udział per okres = wartosc_towaru / suma_globalna * 100
                foreach (var kv in seria.WartoscPerOkres)
                {
                    var suma = ds.SumaCalkowitaPerOkres.GetValueOrDefault(kv.Key);
                    seria.UdzialProcPerOkres[kv.Key] = suma > 0 ? kv.Value * 100m / suma : 0m;
                }

                // Średni % (po wszystkich okresach z osi, traktując brakujące jako 0)
                if (ds.OsCzasu.Count > 0)
                {
                    decimal sumaUdzialow = ds.OsCzasu.Sum(o => seria.UdzialProcPerOkres.GetValueOrDefault(o.PoczatekOkresu));
                    seria.SredniUdzialProc = sumaUdzialow / ds.OsCzasu.Count;
                }

                ds.Serie.Add(seria);
                ds.SumaZaznaczonychPLN += seria.SumaWartosci;
            }

            return ds;
        }

        // ─── Granulacja czasu ─────────────────────────────────────────────────

        public static DateTime PoczatekOkresu(DateTime data, GranulacjaCzasu g) => g switch
        {
            GranulacjaCzasu.Dzien => data.Date,
            GranulacjaCzasu.Tydzien => data.Date.AddDays(-(((int)data.DayOfWeek + 6) % 7)),  // ISO poniedziałek
            GranulacjaCzasu.Miesiac => new DateTime(data.Year, data.Month, 1),
            _ => data.Date
        };

        public static List<PunktCzasu> BudujOsCzasu(DateTime od, DateTime doD, GranulacjaCzasu g)
        {
            var lista = new List<PunktCzasu>();
            var pl = CultureInfo.GetCultureInfo("pl-PL");
            var start = PoczatekOkresu(od, g);
            var stop = PoczatekOkresu(doD, g);
            var cur = start;

            while (cur <= stop)
            {
                lista.Add(new PunktCzasu
                {
                    PoczatekOkresu = cur,
                    EtykietaKrotka = g switch
                    {
                        GranulacjaCzasu.Dzien => cur.ToString("dd.MM"),
                        GranulacjaCzasu.Tydzien => $"T{ISOWeek.GetWeekOfYear(cur)}",
                        GranulacjaCzasu.Miesiac => cur.ToString("MM.yyyy"),
                        _ => cur.ToString("dd.MM")
                    },
                    EtykietaPelna = g switch
                    {
                        GranulacjaCzasu.Dzien => cur.ToString("dddd, dd.MM.yyyy", pl),
                        GranulacjaCzasu.Tydzien => $"Tydzień {ISOWeek.GetWeekOfYear(cur)} {ISOWeek.GetYear(cur)} ({cur:dd.MM} – {cur.AddDays(6):dd.MM})",
                        GranulacjaCzasu.Miesiac => cur.ToString("LLLL yyyy", pl),
                        _ => cur.ToString("d", pl)
                    }
                });

                cur = g switch
                {
                    GranulacjaCzasu.Dzien => cur.AddDays(1),
                    GranulacjaCzasu.Tydzien => cur.AddDays(7),
                    GranulacjaCzasu.Miesiac => cur.AddMonths(1),
                    _ => cur.AddDays(1)
                };
            }
            return lista;
        }

        private static string[] Paleta() => new[]
        {
            "#7C3AED", "#2563EB", "#F97316", "#10B981", "#EF4444",
            "#EAB308", "#06B6D4", "#EC4899", "#84CC16", "#F59E0B",
            "#8B5CF6", "#3B82F6", "#14B8A6", "#A855F7", "#F43F5E"
        };
    }
}
