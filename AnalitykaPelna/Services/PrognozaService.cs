using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Średnia sprzedaż per dzień tygodnia z N tygodni wstecz.
    /// Zastępuje logikę PrognozyUbojuWindow + część AnalizaTygodniowaService.LoadPrognoza.
    /// Trzy widoki: Towary / Odbiorcy / Handlowcy.
    /// </summary>
    public class PrognozaService
    {
        private readonly string _connHandel;
        private readonly string _katalog;

        public PrognozaService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _connHandel = AnalitykaConfig.ConnHandel;
            _katalog = AnalitykaConfig.KatalogMieso;
        }

        public PrognozaService(string connHandel, string katalogMieso)
        {
            _connHandel = connHandel;
            _katalog = katalogMieso;
        }

        public async Task<List<TowarComboItem>> LoadTowaryAsync()
        {
            var lista = new List<TowarComboItem>
            {
                new() { IdHandel = 0, KodHandel = "— Wszystkie towary —" }
            };

            const string sql = @"
                SELECT DISTINCT TW.id, TW.kod, TW.nazwa
                FROM [HANDEL].[HM].[TW] TW
                INNER JOIN [HANDEL].[HM].[DP] DP ON TW.id = DP.idtw
                WHERE TW.katalog = @Katalog
                ORDER BY TW.kod";

            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Katalog", _katalog);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new TowarComboItem
                {
                    IdHandel = SqlSafe.ReadInt(reader, 0),
                    KodHandel = SqlSafe.ReadString(reader, 1),
                    Nazwa = SqlSafe.ReadString(reader, 2)
                });
            }
            return lista;
        }

        /// <summary>
        /// Surowe dane sprzedaży zgrupowane po dniu tygodnia (1=Pn..7=Nd, ISO).
        /// Klient agreguje je do trzech widoków: towary / odbiorcy / handlowcy.
        /// </summary>
        public async Task<List<SuroweDanePrognozy>> LoadSurowePrognozyAsync(FiltryAnaliz filtry)
        {
            var result = new List<SuroweDanePrognozy>();

            DateTime dataOd = DateTime.Today.AddDays(-7 * filtry.LiczbaTygodniPrognozy);
            DateTime dataDo = DateTime.Today.AddDays(-1);

            string odbiorcyCondition = filtry.OdbiorcyIds.Any()
                ? $" AND DK.khid IN ({string.Join(",", filtry.OdbiorcyIds.Select(i => i.ToString()))})"
                : string.Empty;

            string handlowcyCondition = "";
            if (filtry.Handlowcy.Any())
            {
                var nazwy = filtry.Handlowcy.Select((_, i) => $"@H{i}");
                handlowcyCondition = $" AND ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') IN ({string.Join(",", nazwy)})";
            }

            string sql = $@"
                SELECT
                    DK.khid AS KontrahentId,
                    C.shortcut AS NazwaKontrahenta,
                    DP.idtw AS TowarId,
                    TW.kod AS KodTowaru,
                    TW.nazwa AS NazwaTowaru,
                    ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK') AS Handlowiec,
                    CAST(DK.data AS DATE) AS Data,
                    CAST(SUM(DP.ilosc) AS DECIMAL(18,2)) AS Ilosc
                FROM [HANDEL].[HM].[DK] DK
                INNER JOIN [HANDEL].[HM].[DP] DP ON DK.id = DP.super
                INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
                INNER JOIN [HANDEL].[HM].[TW] TW ON DP.idtw = TW.id
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
                WHERE DK.anulowany = 0
                  AND TW.katalog = @Katalog
                  AND CAST(DK.data AS DATE) BETWEEN @DataOd AND @DataDo
                  AND (@TowarID IS NULL OR DP.idtw = @TowarID)
                  {odbiorcyCondition}
                  {handlowcyCondition}
                GROUP BY DK.khid, C.shortcut, DP.idtw, TW.kod, TW.nazwa,
                    ISNULL(WYM.CDim_Handlowiec_Val, 'BRAK'), CAST(DK.data AS DATE);";

            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Katalog", _katalog);
            cmd.Parameters.AddWithValue("@DataOd", dataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", dataDo.Date);
            cmd.Parameters.AddWithValue("@TowarID", (object?)filtry.TowarIdHandel ?? DBNull.Value);
            for (int i = 0; i < filtry.Handlowcy.Count; i++)
                cmd.Parameters.AddWithValue($"@H{i}", filtry.Handlowcy[i] ?? "");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new SuroweDanePrognozy
                {
                    KontrahentId = SqlSafe.ReadInt(reader, 0),
                    NazwaKontrahenta = SqlSafe.ReadString(reader, 1),
                    TowarId = SqlSafe.ReadInt(reader, 2),
                    KodTowaru = SqlSafe.ReadString(reader, 3),
                    NazwaTowaru = SqlSafe.ReadString(reader, 4),
                    Handlowiec = SqlSafe.ReadString(reader, 5),
                    Data = SqlSafe.ReadDate(reader, 6),
                    Ilosc = SqlSafe.ReadDecimal(reader, 7)
                });
            }
            return result;
        }

        /// <summary>
        /// Zwraca prognozę w wybranym widoku (towary/odbiorcy/handlowcy).
        /// Średnia kg per dzień tygodnia = suma kg / liczba_tygodni.
        /// </summary>
        public List<PrognozaWiersz> AgregujPrognoze(
            List<SuroweDanePrognozy> surowe, WidokPrognozy widok, int liczbaTygodni)
        {
            if (liczbaTygodni <= 0) liczbaTygodni = 1;

            IEnumerable<IGrouping<string, SuroweDanePrognozy>> grupy = widok switch
            {
                WidokPrognozy.Towary => surowe.GroupBy(r => r.KodTowaru + "|" + r.NazwaTowaru),
                WidokPrognozy.Odbiorcy => surowe.GroupBy(r => r.KontrahentId + "|" + r.NazwaKontrahenta),
                WidokPrognozy.Handlowcy => surowe.GroupBy(r => r.Handlowiec + "|" + r.Handlowiec),
                _ => surowe.GroupBy(r => r.KodTowaru)
            };

            var wyniki = new List<PrognozaWiersz>();
            foreach (var grupa in grupy)
            {
                var parts = grupa.Key.Split('|', 2);
                var w = new PrognozaWiersz
                {
                    Klucz = parts[0],
                    Etykieta = parts.Length > 1 ? parts[1] : parts[0]
                };

                foreach (var dzienGrupa in grupa.GroupBy(r => DzienTygodniaIso(r.Data)))
                {
                    decimal suma = dzienGrupa.Sum(r => r.Ilosc);
                    w.SredniePerDzienTygodnia[dzienGrupa.Key] = suma / liczbaTygodni;
                }
                wyniki.Add(w);
            }

            return wyniki
                .OrderByDescending(w => w.SumaTygodnia)
                .ToList();
        }

        public PrognozaPodsumowanie BudujPodsumowanie(List<PrognozaWiersz> wiersze, int liczbaTygodni)
        {
            var sumaPerDzien = new Dictionary<int, decimal>();
            foreach (var w in wiersze)
                foreach (var kv in w.SredniePerDzienTygodnia)
                    sumaPerDzien[kv.Key] = sumaPerDzien.GetValueOrDefault(kv.Key) + kv.Value;

            var p = new PrognozaPodsumowanie
            {
                LiczbaTygodni = liczbaTygodni,
                DataOdAnaliza = DateTime.Today.AddDays(-7 * liczbaTygodni),
                DataDoAnaliza = DateTime.Today.AddDays(-1),
                SredniaTygodniowa = sumaPerDzien.Values.Sum()
            };

            if (sumaPerDzien.Count > 0)
            {
                var max = sumaPerDzien.OrderByDescending(kv => kv.Value).First();
                var min = sumaPerDzien.OrderBy(kv => kv.Value).First();
                p.DzienMaxNazwa = PrognozaPodsumowanie.DzienNazwa(max.Key);
                p.DzienMaxKg = max.Value;
                p.DzienMinNazwa = PrognozaPodsumowanie.DzienNazwa(min.Key);
                p.DzienMinKg = min.Value;
            }
            return p;
        }

        // Normalizacja niezależna od @@DATEFIRST: 1=Pn .. 7=Nd
        private static int DzienTygodniaIso(DateTime d)
            => ((int)d.DayOfWeek + 6) % 7 + 1;
    }

    public class SuroweDanePrognozy
    {
        public int KontrahentId { get; set; }
        public string NazwaKontrahenta { get; set; } = "";
        public int TowarId { get; set; }
        public string KodTowaru { get; set; } = "";
        public string NazwaTowaru { get; set; } = "";
        public string Handlowiec { get; set; } = "";
        public DateTime Data { get; set; }
        public decimal Ilosc { get; set; }
    }
}
