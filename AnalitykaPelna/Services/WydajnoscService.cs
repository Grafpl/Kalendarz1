using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// Wydajność krojenia (HM.MZ magazyn ubojni) + per-hodowca (cross-DB: LibraNet PartiaDostawca → In0E).
    /// Faza 1: gotowa wydajność krojenia + per-klasa.
    /// Faza 5 dorzuci: pełna wydajność uboju z HarmonogramDostaw + per-zmiana.
    /// </summary>
    public class WydajnoscService
    {
        private readonly string _connHandel;
        private readonly string _connLibra;
        private readonly string _magazynUbojnia;

        public WydajnoscService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _connHandel = AnalitykaConfig.ConnHandel;
            _connLibra = AnalitykaConfig.ConnLibraNet;
            _magazynUbojnia = AnalitykaConfig.MagazynUbojnia;
        }

        public WydajnoscService(string connHandel, string connLibra, string magazynUbojnia)
        {
            _connHandel = connHandel;
            _connLibra = connLibra;
            _magazynUbojnia = magazynUbojnia;
        }

        /// <summary>
        /// Wydajność uboju per dzień: Żywiec (sPZ) → Tuszki A/B + podroby (sPWU magazyn 65554).
        /// </summary>
        public async Task<List<WydajnoscDzien>> LoadWydajnoscUbojuPerDzienAsync(FiltryAnaliz f)
        {
            var dni = new Dictionary<DateTime, WydajnoscDzien>();

            // 1. Żywiec sPZ + sRWU per dzień (kat. 65882)
            const string sqlZywiec = @"
                SELECT CAST(MG.[data] AS DATE) AS Data, MG.[seria], SUM(ABS(MZ.[ilosc])) AS Kg
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                WHERE MG.[seria] IN ('sPZ', 'sRWU')
                  AND TW.[katalog] = 65882
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                GROUP BY CAST(MG.[data] AS DATE), MG.[seria];";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlZywiec, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var data = SqlSafe.ReadDate(reader, 0);
                    var seria = SqlSafe.ReadString(reader, 1);
                    var kg = SqlSafe.ReadDecimal(reader, 2);

                    if (!dni.TryGetValue(data, out var d))
                    {
                        d = new WydajnoscDzien { Data = data };
                        dni[data] = d;
                    }
                    if (seria == "sPZ") d.ZywiecKg += kg;
                    else if (seria == "sRWU") d.ZywiecRwuKg += kg;
                }
            }

            // 2. PWU per dzień + kod (Tuszka A/B + 3 podroby)
            const string sqlPwu = @"
                SELECT CAST(MG.[data] AS DATE) AS Data, MZ.[kod], SUM(ABS(MZ.[ilosc])) AS Kg
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                WHERE MG.[seria] IN ('PWU', 'sPWU')
                  AND MZ.[magazyn] = @Magazyn
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  AND MZ.[kod] IN ('Kurczak A', 'Kurczak B', N'Wątroba', N'Żołądki', N'Serce')
                GROUP BY CAST(MG.[data] AS DATE), MZ.[kod];";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlPwu, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var data = SqlSafe.ReadDate(reader, 0);
                    var kod = SqlSafe.ReadString(reader, 1);
                    var kg = SqlSafe.ReadDecimal(reader, 2);

                    if (!dni.TryGetValue(data, out var d))
                    {
                        d = new WydajnoscDzien { Data = data };
                        dni[data] = d;
                    }
                    switch (kod)
                    {
                        case "Kurczak A": d.TuszkaAKg += kg; break;
                        case "Kurczak B": d.TuszkaBKg += kg; break;
                        case "Wątroba": d.WatrobaKg += kg; break;
                        case "Żołądki": d.ZoladkiKg += kg; break;
                        case "Serce": d.SerceKg += kg; break;
                    }
                }
            }

            // 3. Detekcja alertów (typowa norma uboju ~85% z podrobami, ~30% bez)
            double norma = AnalitykaConfig.NormaWydajnosciProc;
            double tol = AnalitykaConfig.TolerancjaWydajnosciProc;
            foreach (var d in dni.Values)
            {
                if (d.ZywiecKg <= 0) continue;
                double diff = Math.Abs((double)d.WydajnoscZPodrobamiProc - norma);
                if (diff > tol)
                {
                    d.CzyAlert = true;
                    d.Uwagi = $"Wydajność {d.WydajnoscZPodrobamiProc:F1}% (norma {norma:F0}% ±{tol:F0}%)";
                }
            }

            return dni.Values.OrderBy(d => d.Data).ToList();
        }

        /// <summary>
        /// Wydajność krojenia per dzień: TuszkaB (RWP, kod='Kurczak B') vs Elementy (PWP) + Podroby (sPWU).
        /// Źródło: tylko HANDEL (HM.MZ × HM.MG × HM.TW).
        /// (Pozostawione dla kompatybilności — nie używane w nowym Trendzie wydajności)
        /// </summary>
        public async Task<List<WydajnoscDzien>> LoadWydajnoscKrojeniaAsync(FiltryAnaliz f)
        {
            var dni = new Dictionary<DateTime, WydajnoscDzien>();

            // 1) Tuszki A i B na ubojni (sPWU = przychód, RWP = wydanie do krojenia)
            const string sqlTuszki = @"
                SELECT
                    CAST(MG.[data] AS DATE) AS Data,
                    MZ.[kod],
                    ABS(SUM(CASE WHEN MG.[seria] = 'sPWU' THEN MZ.[ilosc] ELSE 0 END)) AS Przychod,
                    SUM(CASE WHEN MG.[seria] = 'RWP' THEN ABS(MZ.[ilosc]) ELSE 0 END) AS Krojenie
                FROM [HM].[MZ] MZ
                INNER JOIN [HM].[MG] MG ON MZ.[super] = MG.[id]
                WHERE MZ.[kod] IN ('Kurczak A', 'Kurczak B')
                  AND MZ.[magazyn] = @Magazyn
                  AND MG.[data] >= @DataOd
                  AND MG.[data] <= @DataDo
                GROUP BY CAST(MG.[data] AS DATE), MZ.[kod];";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlTuszki, conn);
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    DateTime data = SqlSafe.ReadDate(reader, 0);
                    string kod = SqlSafe.ReadString(reader, 1);
                    decimal krojenie = SqlSafe.ReadDecimal(reader, 3);

                    if (!dni.TryGetValue(data, out var dzien))
                    {
                        dzien = new WydajnoscDzien { Data = data };
                        dni[data] = dzien;
                    }
                    if (kod == "Kurczak B") dzien.TuszkaBKg = krojenie;
                }
            }

            // 2) Elementy (PWP) i Podroby (sPWU) z ubojni
            const string sqlElementy = @"
                SELECT
                    CAST(MG.[data] AS DATE) AS Data,
                    MG.[seria],
                    SUM(ABS(MZ.[ilosc])) AS Suma
                FROM [HM].[MZ] MZ
                INNER JOIN [HM].[MG] MG ON MZ.[super] = MG.[id]
                WHERE MZ.[magazyn] = @Magazyn
                  AND MG.[data] >= @DataOd
                  AND MG.[data] <= @DataDo
                  AND MG.[seria] IN ('PWP', 'sPWU')
                GROUP BY CAST(MG.[data] AS DATE), MG.[seria];";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlElementy, conn);
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    DateTime data = SqlSafe.ReadDate(reader, 0);
                    string seria = SqlSafe.ReadString(reader, 1);
                    decimal suma = SqlSafe.ReadDecimal(reader, 2);

                    if (!dni.TryGetValue(data, out var dzien))
                    {
                        dzien = new WydajnoscDzien { Data = data };
                        dni[data] = dzien;
                    }
                    if (seria == "PWP") dzien.ElementyKg += suma;
                    else if (seria == "sPWU") dzien.WatrobaKg += suma; // legacy: rozkład w nowej metodzie
                }
            }

            // 3) Detekcja alertów
            double norma = AnalitykaConfig.NormaWydajnosciProc;
            double tol = AnalitykaConfig.TolerancjaWydajnosciProc;
            foreach (var d in dni.Values)
            {
                double diff = Math.Abs((double)d.WydajnoscProcent - norma);
                if (d.TuszkaBKg > 0 && diff > tol)
                {
                    d.CzyAlert = true;
                    d.Uwagi = $"Wydajność {d.WydajnoscProcent:F1}% (norma: {norma:F0}% ±{tol:F0}%)";
                }
            }

            return dni.Values.OrderBy(d => d.Data).ToList();
        }

        public async Task<List<WydajnoscSzczegolElement>> LoadSzczegolyElementowAsync(FiltryAnaliz f)
        {
            // Wczytaj real nazwy magazynów (idempotentnie — pierwszy call leci do DB, kolejne cache)
            await MagazynyHelper.LoadFromDatabaseAsync(_connHandel);

            var lista = new List<WydajnoscSzczegolElement>();

            // CTE-pattern: pre-agregacja per (data, kod, seria, magazyn, dok_id) żeby
            // STRING_AGG nie duplikował numerów przy wielolinjowych dokumentach.
            // Drugi level GROUP BY składa wynik per (data, kod, seria, magazyn) z licznikiem
            // i listą numerów. Magazyn relaksowany — pokazujemy wszystkie powiązane (PWP/RWP/sPWU).
            const string sql = @"
                WITH PerDok AS (
                    SELECT
                        CAST(MG.[data] AS DATE) AS Data,
                        MZ.[kod]                AS KodMZ,
                        MG.[seria]              AS Seria,
                        TW.[katalog]            AS Katalog,
                        TW.[nazwa]              AS NazwaTw,
                        MZ.[magazyn]            AS MagazynID,
                        MG.[id]                 AS DokId,
                        MG.[kod]                AS DokKod,
                        SUM(CASE WHEN MG.[seria] IN ('PWP', 'sPWU') THEN ABS(MZ.[ilosc]) ELSE 0 END) AS DokPrzychod,
                        SUM(CASE WHEN MG.[seria] = 'RWP' THEN ABS(MZ.[ilosc]) ELSE 0 END)            AS DokKrojenie
                    FROM [HM].[MZ] MZ
                    INNER JOIN [HM].[MG] MG ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                    WHERE MZ.[magazyn] = @Magazyn
                      AND MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd
                      AND MG.[data] <= @DataDo
                      AND MG.[seria] IN ('PWP', 'RWP', 'sPWU')
                    GROUP BY CAST(MG.[data] AS DATE), MZ.[kod], MG.[seria], TW.[katalog], TW.[nazwa],
                             MZ.[magazyn], MG.[id], MG.[kod]
                )
                SELECT
                    Data,
                    KodMZ,
                    Seria,
                    Katalog,
                    NazwaTw,
                    CASE
                        WHEN Katalog IN (67095, 67104) THEN 'Mięso'
                        WHEN Katalog = 67153 THEN 'Mrozony'
                        WHEN Katalog = 65882 THEN 'Zywy'
                        WHEN Katalog = 67094 THEN 'Odpady'
                        ELSE 'Inne'
                    END                                                     AS Kategoria,
                    SUM(DokPrzychod)                                        AS Przychod,
                    SUM(DokKrojenie)                                        AS Krojenie,
                    MagazynID,
                    COUNT(*)                                                AS LiczbaDok,
                    STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId)  AS NumeryDok
                FROM PerDok
                GROUP BY Data, KodMZ, Seria, Katalog, NazwaTw, MagazynID
                HAVING SUM(DokPrzychod) > 0 OR SUM(DokKrojenie) > 0
                ORDER BY Data, KodMZ, Seria;";

            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
            cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int? magId = SqlSafe.ReadInt(reader, 8);
                if (magId == 0) magId = null;
                lista.Add(new WydajnoscSzczegolElement
                {
                    Data = SqlSafe.ReadDate(reader, 0),
                    KodTowaru = SqlSafe.ReadString(reader, 1),
                    Seria = SqlSafe.ReadString(reader, 2),
                    Kategoria = SqlSafe.ReadString(reader, 5),
                    NazwaTowaru = SqlSafe.ReadString(reader, 4),
                    Przychod = SqlSafe.ReadDecimal(reader, 6),
                    Krojenie = SqlSafe.ReadDecimal(reader, 7),
                    MagazynID = magId,
                    MagazynNazwa = MagazynyHelper.Skrot(magId),
                    LiczbaDokumentow = SqlSafe.ReadInt(reader, 9),
                    NumeryDokumentow = SqlSafe.ReadString(reader, 10)
                });
            }

            return lista;
        }

        /// <summary>
        /// Per-hodowca: średnia waga sztuki w klasie (LibraNet In0E + PartiaDostawca).
        /// Tu liczymy sumę kg żywca per hodowca i liczbę sztuk (przybliżenie wydajności wejścia).
        /// </summary>
        public async Task<List<WydajnoscHodowca>> LoadWydajnoscPerHodowcaAsync(FiltryAnaliz f)
        {
            var lista = new List<WydajnoscHodowca>();

            const string sql = @"
                SELECT
                    pd.CustomerID,
                    pd.CustomerName,
                    COUNT(DISTINCT e.P1) AS LiczbaPartii,
                    SUM(CASE WHEN e.ActWeight > 0 THEN e.ActWeight ELSE 0 END) AS SumaKg
                FROM dbo.In0E e
                INNER JOIN dbo.PartiaDostawca pd ON e.P1 = pd.Partia
                WHERE e.Data >= @DataOd AND e.Data <= @DataDo
                  AND pd.CustomerName IS NOT NULL AND pd.CustomerName <> ''
                GROUP BY pd.CustomerID, pd.CustomerName
                ORDER BY SUM(CASE WHEN e.ActWeight > 0 THEN e.ActWeight ELSE 0 END) DESC;";

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", f.DataOd.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", f.DataDo.ToString("yyyy-MM-dd"));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new WydajnoscHodowca
                {
                    CustomerID = SqlSafe.ReadString(reader, 0),
                    Hodowca = SqlSafe.ReadString(reader, 1),
                    LiczbaPartii = SqlSafe.ReadInt(reader, 2),
                    SumaWyjscieKg = SqlSafe.ReadDecimal(reader, 3),
                    SumaTuszkaBKg = 0  // wymaga dopinania HM.MZ per partia (TODO Faza 5+)
                });
            }

            // Posortowane wg sumy malejąco — przypisz pozycje, % lidera, % udziału
            decimal lider = lista.Count > 0 ? lista[0].SumaWyjscieKg : 0;
            decimal sumaCalkowita = lista.Sum(h => h.SumaWyjscieKg);
            for (int i = 0; i < lista.Count; i++)
            {
                lista[i].Pozycja = i + 1;
                lista[i].ProcentLidera = lider <= 0 ? 0 : lista[i].SumaWyjscieKg / lider * 100m;
                lista[i].ProcentUdzialu = sumaCalkowita <= 0 ? 0 : lista[i].SumaWyjscieKg / sumaCalkowita * 100m;
            }

            return lista;
        }

        /// <summary>
        /// PEŁEN BILANS MATERIAŁOWY: kurczak żywy → ubój → krojenie.
        /// Łańcuch dokumentów Symfonii:
        ///   1. PZ (Przyjęcie Zewnętrzne) — kurczak żywy wjeżdża, katalog 65882
        ///   2. RWU/sRWU (Rozchód Wewnętrzny — Ubój) — żywiec rozchodowany do uboju
        ///   3. PWU/sPWU (Przychód Wewnętrzny — Ubój) — Tuszka A, Tuszka B, Podroby, Odpady
        ///   4. RWP (Rozchód Wewnętrzny — Produkcja) — Tuszka B wydana do krojenia
        ///   5. PWP (Przychód Wewnętrzny — Produkcja) — Filet, Skrzydło, Korpus itd.
        /// </summary>
        public async Task<BilansMaterialowy> LoadBilansMaterialowyAsync(FiltryAnaliz f)
        {
            var bm = new BilansMaterialowy();

            // Wczytaj real nazwy magazynów z bazy (raz, idempotentne).
            // Parsuje sufiksy kod-ów MM+/MM- żeby znaleźć "M. PROD", "M. DYST" itd.
            await MagazynyHelper.LoadFromDatabaseAsync(_connHandel);

            // ───────────────────────────────────────────────────────────
            // 1. ŻYWIEC sPZ — kurczak żywy przyjęty od hodowcy
            //    Kody: 'Kurczak żywy 7', 'Kurczak żywy 8', 'Kurczak żywy ...' (sufiksy)
            //    Filtr po katalogu 65882 + dodatkowo LIKE dla bezpieczeństwa.
            //    CTE: trzymamy unikalne (NazwaTw, KodPoz, Seria, Magazyn, DokId, DokKod)
            //    żeby STRING_AGG nie duplikował numerów.
            // ───────────────────────────────────────────────────────────
            const string sqlPZ = @"
                WITH B AS (
                    SELECT TW.[nazwa] AS NazwaTw, MZ.[kod] AS KodPoz, MG.[seria] AS Seria,
                           MZ.[magazyn] AS MagazynID, MG.[id] AS DokId, MG.[kod] AS DokKod,
                           SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                    WHERE MG.[seria] = 'sPZ' AND TW.[katalog] = 65882 AND MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY TW.[nazwa], MZ.[kod], MG.[seria], MZ.[magazyn], MG.[id], MG.[kod]
                )
                SELECT NazwaTw, KodPoz, Seria, MagazynID,
                       SUM(DokKg) AS Kg, COUNT(*) AS LiczbaDok,
                       STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId) AS NumeryDok
                FROM B
                GROUP BY NazwaTw, KodPoz, Seria, MagazynID;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlPZ, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int? magId = SqlSafe.ReadInt(reader, 3);
                    if (magId == 0) magId = null;
                    decimal kg = SqlSafe.ReadDecimal(reader, 4);
                    bm.ZywiecPzKg += kg;
                    bm.Pozycje.Add(new BilansMaterialowyWiersz
                    {
                        Etap = "ŻYWIEC PZ",
                        Kategoria = "Żywy",
                        Nazwa = SqlSafe.ReadString(reader, 0),
                        Kod = SqlSafe.ReadString(reader, 1),
                        Seria = SqlSafe.ReadString(reader, 2),
                        MagazynID = magId,
                        MagazynNazwa = MagazynyHelper.Skrot(magId),
                        Kg = kg,
                        LiczbaDokumentow = SqlSafe.ReadInt(reader, 5),
                        NumeryDokumentow = SqlSafe.ReadString(reader, 6)
                    });
                }
            }

            // ───────────────────────────────────────────────────────────
            // 2. ŻYWIEC sRWU — rozchód do uboju (faktycznie weszło na linię)
            //    To jest BAZA dla wydajności uboju.
            // ───────────────────────────────────────────────────────────
            const string sqlRWU = @"
                WITH B AS (
                    SELECT TW.[nazwa] AS NazwaTw, MZ.[kod] AS KodPoz, MG.[seria] AS Seria,
                           MZ.[magazyn] AS MagazynID, MG.[id] AS DokId, MG.[kod] AS DokKod,
                           SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                    WHERE MG.[seria] = 'sRWU' AND TW.[katalog] = 65882 AND MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY TW.[nazwa], MZ.[kod], MG.[seria], MZ.[magazyn], MG.[id], MG.[kod]
                )
                SELECT NazwaTw, KodPoz, Seria, MagazynID,
                       SUM(DokKg) AS Kg, COUNT(*) AS LiczbaDok,
                       STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId) AS NumeryDok
                FROM B
                GROUP BY NazwaTw, KodPoz, Seria, MagazynID;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlRWU, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int? magId = SqlSafe.ReadInt(reader, 3);
                    if (magId == 0) magId = null;
                    decimal kg = SqlSafe.ReadDecimal(reader, 4);
                    bm.ZywiecRwuKg += kg;
                    bm.Pozycje.Add(new BilansMaterialowyWiersz
                    {
                        Etap = "DO UBOJU",
                        Kategoria = "Żywy → ubój",
                        Nazwa = SqlSafe.ReadString(reader, 0),
                        Kod = SqlSafe.ReadString(reader, 1),
                        Seria = SqlSafe.ReadString(reader, 2),
                        MagazynID = magId,
                        MagazynNazwa = MagazynyHelper.Skrot(magId),
                        Kg = kg,
                        LiczbaDokumentow = SqlSafe.ReadInt(reader, 5),
                        NumeryDokumentow = SqlSafe.ReadString(reader, 6)
                    });
                }
            }

            // ───────────────────────────────────────────────────────────
            // 3. UBÓJ sPWU — tuszki + podroby + (ew.) odpady na magazyn 65554
            //    UWAGA: wszystkie tuszki/podroby siedzą w katalogu 67095 (mięso),
            //    rozpoznajemy je po MZ.kod, a NIE po katalogu.
            // ───────────────────────────────────────────────────────────
            const string sqlPWU = @"
                WITH B AS (
                    SELECT TW.[katalog] AS Katalog, TW.[nazwa] AS NazwaTw, MZ.[kod] AS KodPoz,
                           MG.[seria] AS Seria, MZ.[magazyn] AS MagazynID,
                           MG.[id] AS DokId, MG.[kod] AS DokKod,
                           SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                    WHERE MG.[seria] IN ('PWU', 'sPWU') AND MZ.[magazyn] = @Magazyn AND MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY TW.[katalog], TW.[nazwa], MZ.[kod], MG.[seria], MZ.[magazyn], MG.[id], MG.[kod]
                )
                SELECT Katalog, NazwaTw, KodPoz, Seria, MagazynID,
                       SUM(DokKg) AS Kg, COUNT(*) AS LiczbaDok,
                       STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId) AS NumeryDok
                FROM B
                GROUP BY Katalog, NazwaTw, KodPoz, Seria, MagazynID;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlPWU, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int katalog = SqlSafe.ReadInt(reader, 0);
                    string kod = SqlSafe.ReadString(reader, 2);
                    int magId = SqlSafe.ReadInt(reader, 4);
                    decimal kg = SqlSafe.ReadDecimal(reader, 5);

                    string kategoria;
                    if (kod == "Kurczak A") { bm.TuszkaAKg += kg; kategoria = "Tuszka A"; }
                    else if (kod == "Kurczak B") { bm.TuszkaBKg += kg; kategoria = "Tuszka B"; }
                    else if (katalog == 67094) { bm.OdpadyKg += kg; kategoria = "Odpady"; }
                    else { bm.PodrobyKg += kg; kategoria = "Podroby"; }

                    bm.Pozycje.Add(new BilansMaterialowyWiersz
                    {
                        Etap = "UBÓJ",
                        Kategoria = kategoria,
                        Nazwa = SqlSafe.ReadString(reader, 1),
                        Kod = kod,
                        Seria = SqlSafe.ReadString(reader, 3),
                        MagazynID = magId == 0 ? null : magId,
                        MagazynNazwa = MagazynyHelper.Skrot(magId == 0 ? null : magId),
                        Kg = kg,
                        LiczbaDokumentow = SqlSafe.ReadInt(reader, 6),
                        NumeryDokumentow = SqlSafe.ReadString(reader, 7)
                    });
                }
            }

            // ───────────────────────────────────────────────────────────
            // 4. MM- — przeniesienie międzymagazynowe.
            //    Sage Symfonia: sMM- (rozchód) i sMM+ (przychód) to OSOBNE dokumenty MG.
            //    Magazyn docelowy dla sMM- siedzi w polu MG.khdzial (Sage repurposuje
            //    pole "dział kontrahenta" do przechowywania ID magazynu docelowego).
            //    Zweryfikowane na próbie ~30 par MM-/MM+ — wzorzec spójny.
            // ───────────────────────────────────────────────────────────
            const string sqlMM = @"
                WITH B AS (
                    SELECT TW.[nazwa] AS NazwaTw, MZ.[kod] AS KodPoz, MG.[seria] AS Seria,
                           MZ.[magazyn] AS MagazynZ, MG.[khdzial] AS MagazynDo,
                           MG.[id] AS DokId, MG.[kod] AS DokKod,
                           SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                    WHERE MG.[seria] IN ('MM-', 'sMM-') AND MZ.[magazyn] = @Magazyn AND MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY TW.[nazwa], MZ.[kod], MG.[seria], MZ.[magazyn], MG.[khdzial], MG.[id], MG.[kod]
                )
                SELECT NazwaTw, KodPoz, Seria, MagazynZ, MagazynDo,
                       SUM(DokKg) AS Kg, COUNT(*) AS LiczbaDok,
                       STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId) AS NumeryDok
                FROM B
                GROUP BY NazwaTw, KodPoz, Seria, MagazynZ, MagazynDo;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlMM, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string nazwa = SqlSafe.ReadString(reader, 0);
                    string kod = SqlSafe.ReadString(reader, 1);
                    int magZ = SqlSafe.ReadInt(reader, 3);
                    int magDo = SqlSafe.ReadInt(reader, 4);
                    decimal kg = SqlSafe.ReadDecimal(reader, 5);

                    bool toTuszkaPodrob = kod is "Kurczak A" or "Kurczak B"
                        or "Wątroba" or "Żołądki" or "Serce";

                    if (toTuszkaPodrob) bm.MmTuszkiPodrobyKg += kg;
                    else bm.MmElementyKg += kg;

                    // UWAGA: Dla MM- użytkownik chce widzieć W GŁÓWNEJ kolumnie "Magazyn"
                    // dokąd towar POSZEDŁ (destination = magDo z khdzial), bo źródło to zawsze
                    // magazyn ubojni. Cel (MM-) trzymamy jako "skąd" dla pełnego obrazu kierunku.
                    bm.Pozycje.Add(new BilansMaterialowyWiersz
                    {
                        Etap = toTuszkaPodrob ? "MM- (przed krojeniem)" : "MM- (po krojeniu)",
                        Kategoria = "Przeniesienie",
                        Nazwa = nazwa,
                        Kod = kod,
                        Seria = SqlSafe.ReadString(reader, 2),
                        MagazynID = magDo == 0 ? null : magDo,
                        MagazynNazwa = MagazynyHelper.Skrot(magDo == 0 ? null : magDo),
                        MagazynDocelowyID = magZ == 0 ? null : magZ,
                        MagazynDocelowyNazwa = MagazynyHelper.Skrot(magZ == 0 ? null : magZ),
                        Kg = kg,
                        LiczbaDokumentow = SqlSafe.ReadInt(reader, 6),
                        NumeryDokumentow = SqlSafe.ReadString(reader, 7)
                    });
                }
            }

            // ───────────────────────────────────────────────────────────
            // 5. WEJŚCIE DO KROJENIA — sRWP/RWP, magazyn 65554, katalog mięsa
            //    To jest baza dla wydajności krojenia (nie sama Tuszka B,
            //    bo czasem do krojenia wchodzi też A lub inne towary).
            // ───────────────────────────────────────────────────────────
            const string sqlRWP = @"
                WITH B AS (
                    SELECT TW.[katalog] AS Katalog, TW.[nazwa] AS NazwaTw, MZ.[kod] AS KodPoz,
                           MG.[seria] AS Seria, MZ.[magazyn] AS MagazynID,
                           MG.[id] AS DokId, MG.[kod] AS DokKod,
                           SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                    WHERE MG.[seria] IN ('RWP', 'sRWP') AND MZ.[magazyn] = @Magazyn
                      AND TW.[katalog] IN (67095, 67104) AND MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY TW.[katalog], TW.[nazwa], MZ.[kod], MG.[seria], MZ.[magazyn], MG.[id], MG.[kod]
                )
                SELECT Katalog, NazwaTw, KodPoz, Seria, MagazynID,
                       SUM(DokKg) AS Kg, COUNT(*) AS LiczbaDok,
                       STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId) AS NumeryDok
                FROM B
                GROUP BY Katalog, NazwaTw, KodPoz, Seria, MagazynID;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlRWP, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int katalog = SqlSafe.ReadInt(reader, 0);
                    int magId = SqlSafe.ReadInt(reader, 4);
                    decimal kg = SqlSafe.ReadDecimal(reader, 5);
                    bm.WejscieKrojeniaKg += kg;
                    bm.Pozycje.Add(new BilansMaterialowyWiersz
                    {
                        Etap = "DO KROJENIA",
                        Kategoria = KategoriaZKatalogu(katalog),
                        Nazwa = SqlSafe.ReadString(reader, 1),
                        Kod = SqlSafe.ReadString(reader, 2),
                        Seria = SqlSafe.ReadString(reader, 3),
                        MagazynID = magId == 0 ? null : magId,
                        MagazynNazwa = MagazynyHelper.Skrot(magId == 0 ? null : magId),
                        Kg = kg,
                        LiczbaDokumentow = SqlSafe.ReadInt(reader, 6),
                        NumeryDokumentow = SqlSafe.ReadString(reader, 7)
                    });
                }
            }

            // ───────────────────────────────────────────────────────────
            // 6. WYJŚCIE KROJENIA — sPWP/PWP, elementy mięsne
            //    Wykluczamy kody tuszek/podrobów (defensywnie).
            // ───────────────────────────────────────────────────────────
            const string sqlPWP = @"
                WITH B AS (
                    SELECT TW.[katalog] AS Katalog, TW.[nazwa] AS NazwaTw, MZ.[kod] AS KodPoz,
                           MG.[seria] AS Seria, MZ.[magazyn] AS MagazynID,
                           MG.[id] AS DokId, MG.[kod] AS DokKod,
                           SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                    WHERE MG.[seria] IN ('PWP', 'sPWP') AND MZ.[magazyn] = @Magazyn
                      AND TW.[katalog] IN (67095, 67104)
                      AND MZ.[kod] NOT IN ('Kurczak A', 'Kurczak B', 'Wątroba', 'Żołądki', 'Serce')
                      AND MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY TW.[katalog], TW.[nazwa], MZ.[kod], MG.[seria], MZ.[magazyn], MG.[id], MG.[kod]
                )
                SELECT Katalog, NazwaTw, KodPoz, Seria, MagazynID,
                       SUM(DokKg) AS Kg, COUNT(*) AS LiczbaDok,
                       STRING_AGG(DokKod, ', ') WITHIN GROUP (ORDER BY DokId) AS NumeryDok
                FROM B
                GROUP BY Katalog, NazwaTw, KodPoz, Seria, MagazynID;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlPWP, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int katalog = SqlSafe.ReadInt(reader, 0);
                    int magId = SqlSafe.ReadInt(reader, 4);
                    decimal kg = SqlSafe.ReadDecimal(reader, 5);
                    bm.ElementyKg += kg;
                    bm.Pozycje.Add(new BilansMaterialowyWiersz
                    {
                        Etap = "KROJENIE",
                        Kategoria = KategoriaZKatalogu(katalog),
                        Nazwa = SqlSafe.ReadString(reader, 1),
                        Kod = SqlSafe.ReadString(reader, 2),
                        Seria = SqlSafe.ReadString(reader, 3),
                        MagazynID = magId == 0 ? null : magId,
                        MagazynNazwa = MagazynyHelper.Skrot(magId == 0 ? null : magId),
                        Kg = kg,
                        LiczbaDokumentow = SqlSafe.ReadInt(reader, 6),
                        NumeryDokumentow = SqlSafe.ReadString(reader, 7)
                    });
                }
            }

            return bm;
        }

        /// <summary>
        /// STAN MAGAZYNÓW — co weszło, co wyszło, czy się wyzerowało.
        /// Per magazyn agregujemy WSZYSTKIE serie (sPZ/sWZ/PWP/RWP/sMM-/sMM+/sMW/sPKM itp.)
        /// z HM.MZ. Kierunek (IN/OUT) wnioskujemy z prefiksu seri przez SeriaSymfoniaHelper.
        /// </summary>
        public async Task<List<StanMagazynu>> LoadStanMagazynowAsync(FiltryAnaliz f)
        {
            // Wczytaj real nazwy magazynów z bazy (idempotentnie)
            await MagazynyHelper.LoadFromDatabaseAsync(_connHandel);

            // Per (magazyn, seria) — sumy kg + count dokumentów
            const string sql = @"
                WITH PerDok AS (
                    SELECT
                        MZ.[magazyn] AS MagazynID,
                        MG.[seria]   AS Seria,
                        MG.[id]      AS DokId,
                        SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    WHERE MG.[anulowany] = 0
                      AND MZ.[magazyn] IS NOT NULL
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY MZ.[magazyn], MG.[seria], MG.[id]
                )
                SELECT MagazynID, Seria,
                       SUM(DokKg)      AS SumaKg,
                       COUNT(*)        AS LiczbaDok
                FROM PerDok
                GROUP BY MagazynID, Seria
                ORDER BY MagazynID, SUM(DokKg) DESC;";

            var perSeria = new List<(int MagazynID, string Seria, decimal Kg, int LiczbaDok)>();

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    perSeria.Add((
                        SqlSafe.ReadInt(reader, 0),
                        SqlSafe.ReadString(reader, 1),
                        SqlSafe.ReadDecimal(reader, 2),
                        SqlSafe.ReadInt(reader, 3)
                    ));
                }
            }

            // Agreguj per magazyn, klasyfikując IN/OUT po seria
            var stany = perSeria
                .GroupBy(x => x.MagazynID)
                .Select(g =>
                {
                    var magInfo = MagazynyHelper.Wszystkie.TryGetValue(g.Key, out var mi) ? mi : null;
                    var stan = new StanMagazynu
                    {
                        MagazynID = g.Key,
                        MagazynNazwa = magInfo?.Skrot ?? $"Mag. {g.Key}",
                        MagazynPelnaNazwa = magInfo?.PelnaNazwa ?? $"Magazyn {g.Key}",
                        MagazynKolorHex = magInfo?.KolorHex ?? "#94A3B8",
                        LiczbaDokumentow = g.Sum(x => x.LiczbaDok)
                    };

                    foreach (var (_, seria, kg, dok) in g)
                    {
                        bool przychod = SeriaSymfoniaHelper.JestPrzychodem(seria);
                        if (przychod) stan.PrzychodKg += kg;
                        else stan.RozchodKg += kg;

                        stan.RozkladSerii.Add(new StanMagazynuSeria
                        {
                            Seria = seria,
                            Kierunek = przychod ? "IN" : "OUT",
                            Kg = kg,
                            LiczbaDok = dok,
                            OpisSerii = SeriaSymfoniaHelper.Opis(seria)
                        });
                    }

                    // Sortuj rozkład: najpierw IN (od największych), potem OUT
                    stan.RozkladSerii = stan.RozkladSerii
                        .OrderBy(s => s.Kierunek == "IN" ? 0 : 1)
                        .ThenByDescending(s => s.Kg)
                        .ToList();

                    return stan;
                })
                .OrderByDescending(s => s.AktywnoscKg)
                .ToList();

            return stany;
        }

        /// <summary>
        /// Przepływy MM- między magazynami (z dokumentów sMM-).
        /// Magazyn źródłowy = MZ.magazyn, docelowy = MG.khdzial (Sage repurposes).
        /// </summary>
        public async Task<List<PrzeplywMagazynow>> LoadPrzeplywyMagazynowAsync(FiltryAnaliz f)
        {
            await MagazynyHelper.LoadFromDatabaseAsync(_connHandel);

            const string sql = @"
                WITH PerDok AS (
                    SELECT
                        MZ.[magazyn] AS MagazynZ,
                        MG.[khdzial] AS MagazynDo,
                        MG.[id]      AS DokId,
                        SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    WHERE MG.[seria] IN ('MM-', 'sMM-')
                      AND MG.[anulowany] = 0
                      AND MZ.[magazyn] IS NOT NULL
                      AND MG.[khdzial] IS NOT NULL AND MG.[khdzial] <> 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY MZ.[magazyn], MG.[khdzial], MG.[id]
                )
                SELECT MagazynZ, MagazynDo,
                       SUM(DokKg) AS Kg,
                       COUNT(*)   AS LiczbaDok
                FROM PerDok
                GROUP BY MagazynZ, MagazynDo
                ORDER BY SUM(DokKg) DESC;";

            var lista = new List<PrzeplywMagazynow>();
            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                int zId = SqlSafe.ReadInt(reader, 0);
                int doId = SqlSafe.ReadInt(reader, 1);
                lista.Add(new PrzeplywMagazynow
                {
                    MagazynZId = zId,
                    MagazynZNazwa = MagazynyHelper.Skrot(zId),
                    MagazynDoId = doId,
                    MagazynDoNazwa = MagazynyHelper.Skrot(doId),
                    Kg = SqlSafe.ReadDecimal(reader, 2),
                    LiczbaDok = SqlSafe.ReadInt(reader, 3)
                });
            }
            return lista;
        }

        /// <summary>
        /// TOWARY WYPRODUKOWANE — tylko pozycje które kiedykolwiek wystąpiły w PWU/PWP/PPM/PPK.
        /// Pełen lifecycle: produkcja → zużycie → sprzedaż + przepływy MM- gdzie poszły.
        /// </summary>
        public async Task<List<TowarProdukcyjny>> LoadTowaryProdukcjiAsync(FiltryAnaliz f)
        {
            await MagazynyHelper.LoadFromDatabaseAsync(_connHandel);

            // Jeden duży CTE: wszystkie operacje w okresie + klasyfikacja PRODUKCJA/SPRZEDAZ/ZUZYCIE
            // Filtr na pozycje z produkcji robimy przez INNER JOIN z ProdukcyjnePozycje (DISTINCT z PRODUKCJA).
            const string sql = @"
                WITH AllOps AS (
                    SELECT
                        MZ.[kod] AS KodPoz, TW.[nazwa] AS NazwaTw, TW.[katalog] AS Katalog,
                        MG.[seria] AS Seria, MG.[id] AS DokId, MG.[kod] AS DokKod,
                        ABS(MZ.[ilosc]) AS Kg,
                        CASE
                            WHEN MG.[seria] IN ('PWU','sPWU','PWP','sPWP','PPM','sPPM','PPK','sPPK') THEN 'PRODUKCJA'
                            WHEN MG.[seria] IN ('WZ','sWZ','WZ-W','sWZ-W','WZK','sWZK','WZKW','sWZKW') THEN 'SPRZEDAZ'
                            WHEN MG.[seria] IN ('RWP','sRWP','RWU','sRWU','RPM','sRPM','RPK','sRPK') THEN 'ZUZYCIE'
                            ELSE 'INNE'
                        END AS Typ
                    FROM [HM].[MZ] MZ
                    INNER JOIN [HM].[MG] MG ON MG.[id] = MZ.[super]
                    INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                    WHERE MG.[anulowany] = 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                ),
                ProdukcyjnePozycje AS (
                    SELECT DISTINCT KodPoz, NazwaTw, Katalog
                    FROM AllOps WHERE Typ = 'PRODUKCJA'
                ),
                PerPozycjaTyp AS (
                    SELECT
                        A.KodPoz, A.NazwaTw, A.Katalog, A.Typ,
                        SUM(A.Kg) AS Kg,
                        COUNT(DISTINCT A.DokId) AS LiczbaDok,
                        STRING_AGG(CAST(A.DokKod AS NVARCHAR(MAX)), ', ')
                            WITHIN GROUP (ORDER BY A.DokId) AS NumeryDok
                    FROM AllOps A
                    INNER JOIN ProdukcyjnePozycje P
                        ON P.KodPoz = A.KodPoz AND P.NazwaTw = A.NazwaTw AND P.Katalog = A.Katalog
                    WHERE A.Typ IN ('PRODUKCJA', 'SPRZEDAZ', 'ZUZYCIE')
                    GROUP BY A.KodPoz, A.NazwaTw, A.Katalog, A.Typ
                )
                SELECT
                    KodPoz, NazwaTw, Katalog,
                    SUM(CASE WHEN Typ = 'PRODUKCJA' THEN Kg ELSE 0 END)        AS WyprodukowanoKg,
                    SUM(CASE WHEN Typ = 'PRODUKCJA' THEN LiczbaDok ELSE 0 END) AS DokProd,
                    MAX(CASE WHEN Typ = 'PRODUKCJA' THEN NumeryDok END)        AS NumProd,
                    SUM(CASE WHEN Typ = 'SPRZEDAZ' THEN Kg ELSE 0 END)         AS SprzedanoKg,
                    SUM(CASE WHEN Typ = 'SPRZEDAZ' THEN LiczbaDok ELSE 0 END)  AS DokSprz,
                    MAX(CASE WHEN Typ = 'SPRZEDAZ' THEN NumeryDok END)         AS NumSprz,
                    SUM(CASE WHEN Typ = 'ZUZYCIE' THEN Kg ELSE 0 END)          AS ZuzytoKg,
                    SUM(CASE WHEN Typ = 'ZUZYCIE' THEN LiczbaDok ELSE 0 END)   AS DokZuz,
                    MAX(CASE WHEN Typ = 'ZUZYCIE' THEN NumeryDok END)          AS NumZuz
                FROM PerPozycjaTyp
                GROUP BY KodPoz, NazwaTw, Katalog
                ORDER BY SUM(CASE WHEN Typ = 'PRODUKCJA' THEN Kg ELSE 0 END) DESC;";

            var lista = new List<TowarProdukcyjny>();
            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int katalog = SqlSafe.ReadInt(reader, 2);
                    var (katIkona, katKolor, katNazwa) = MapujKategorie(katalog);
                    string kod = SqlSafe.ReadString(reader, 0);
                    lista.Add(new TowarProdukcyjny
                    {
                        Kod = kod,
                        Nazwa = SqlSafe.ReadString(reader, 1),
                        Katalog = katalog,
                        Kategoria = katNazwa,
                        KategoriaIkona = katIkona,
                        KategoriaKolor = katKolor,
                        WyprodukowanoKg = SqlSafe.ReadDecimal(reader, 3),
                        LiczbaDokProdukcji = SqlSafe.ReadInt(reader, 4),
                        NumeryDokProdukcji = SqlSafe.ReadString(reader, 5),
                        SprzedanoKg = SqlSafe.ReadDecimal(reader, 6),
                        LiczbaDokSprzedazy = SqlSafe.ReadInt(reader, 7),
                        NumeryDokSprzedazy = SqlSafe.ReadString(reader, 8),
                        ZuzytoKg = SqlSafe.ReadDecimal(reader, 9),
                        LiczbaDokZuzycia = SqlSafe.ReadInt(reader, 10),
                        NumeryDokZuzycia = SqlSafe.ReadString(reader, 11),
                        ZdjecieSciezka = ZnajdzZdjecieTowaru(kod)
                    });
                }
            }

            // Druga query: per (towar, magazyn źródłowy → magazyn docelowy) — przepływy MM-
            const string sqlPrzeplywy = @"
                WITH PerDok AS (
                    SELECT MZ.[kod] AS KodPoz, TW.[nazwa] AS NazwaTw,
                           MZ.[magazyn] AS MagZ, MG.[khdzial] AS MagDo,
                           MG.[id] AS DokId, MG.[kod] AS DokKod,
                           SUM(ABS(MZ.[ilosc])) AS DokKg
                    FROM [HM].[MG] MG
                    INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                    INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                    WHERE MG.[seria] IN ('MM-','sMM-')
                      AND MG.[anulowany] = 0
                      AND MZ.[magazyn] IS NOT NULL
                      AND MG.[khdzial] IS NOT NULL AND MG.[khdzial] <> 0
                      AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                    GROUP BY MZ.[kod], TW.[nazwa], MZ.[magazyn], MG.[khdzial], MG.[id], MG.[kod]
                )
                SELECT KodPoz, NazwaTw, MagZ, MagDo,
                       SUM(DokKg) AS Kg,
                       COUNT(*) AS LiczbaDok,
                       STRING_AGG(CAST(DokKod AS NVARCHAR(MAX)), ', ')
                           WITHIN GROUP (ORDER BY DokId) AS NumeryDok
                FROM PerDok
                GROUP BY KodPoz, NazwaTw, MagZ, MagDo;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlPrzeplywy, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string kodPoz = SqlSafe.ReadString(reader, 0);
                    string nazwa = SqlSafe.ReadString(reader, 1);
                    int magZ = SqlSafe.ReadInt(reader, 2);
                    int magDo = SqlSafe.ReadInt(reader, 3);
                    var towar = lista.FirstOrDefault(t => t.Kod == kodPoz && t.Nazwa == nazwa);
                    if (towar == null) continue;
                    towar.Przeplywy.Add(new TowarPrzeplyw
                    {
                        MagazynZId = magZ,
                        MagazynZNazwa = MagazynyHelper.Skrot(magZ),
                        MagazynDoId = magDo,
                        MagazynDoNazwa = MagazynyHelper.Skrot(magDo),
                        Kg = SqlSafe.ReadDecimal(reader, 4),
                        LiczbaDok = SqlSafe.ReadInt(reader, 5),
                        NumeryDok = SqlSafe.ReadString(reader, 6)
                    });
                }
            }

            // Sortuj przepływy w każdym towarze malejąco po kg
            foreach (var t in lista)
                t.Przeplywy = t.Przeplywy.OrderByDescending(p => p.Kg).ToList();

            return lista;
        }

        /// <summary>
        /// FLOW CHAIN summary — agregaty główne dla wizualizacji łańcucha produkcyjnego:
        ///   ŻYWIEC (sPZ kat. 65882) → UBÓJ (sPWU mag. 65555) → PRODUKCJA (PWP/sPWP mag. 65554)
        ///   → DYSTRYBUCJA (sMM- na 65556) → KLIENCI (sWZ).
        /// Plus odgałęzienia z PROD: MROŹNIA, KARMA, ODPADY.
        /// </summary>
        public async Task<FlowChainSummary> LoadFlowChainAsync(FiltryAnaliz f)
        {
            await MagazynyHelper.LoadFromDatabaseAsync(_connHandel);
            var fc = new FlowChainSummary();

            // Opcjonalny filtr katalogów towarów — jeśli ustawiony przez użytkownika
            // (np. żeby wyłączyć opakowania i artykuły sklepowe z bilansu)
            string katJoin = "";
            string katFilter = "";
            if (f.KatalogiTowarow != null && f.KatalogiTowarow.Count > 0)
            {
                // Sanitacja — tylko liczby całkowite (bez SQL injection)
                var safe = f.KatalogiTowarow.Where(k => k > 0).Select(k => k.ToString()).ToList();
                if (safe.Count > 0)
                {
                    katJoin = "INNER JOIN [HM].[TW] TW2 ON TW2.[id] = MZ.[idtw]";
                    katFilter = $"AND TW2.[katalog] IN ({string.Join(",", safe)})";
                }
            }

            // Pojedyncze zapytanie agregujące wszystko (ZYWIEC ma własny filtr katalog=65882,
            // pozostałe etapy używają opcjonalnego filtra z user-defined listy katalogów).
            string sql = $@"
                SELECT 'ZYWIEC' AS Etap, SUM(ABS(MZ.[ilosc])) AS Kg, COUNT(DISTINCT MG.[id]) AS Dok
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                WHERE MG.[seria] = 'sPZ' AND TW.[katalog] = 65882
                  AND MG.[anulowany] = 0 AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                UNION ALL
                SELECT 'UBOJ', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('PWU','sPWU') AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'PRODUKCJA', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('PWP','sPWP') AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'DYSTRYBUCJA', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65556 AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'KLIENCI', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('WZ','sWZ','WZ-W','sWZ-W','WZK','sWZK') AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'MROZNIA', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65552 AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'KARMA', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65547 AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'ODPADY', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65551 AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'MASARNIA', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65562 AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter}
                UNION ALL
                SELECT 'ROZ_KROJ', SUM(ABS(MZ.[ilosc])), COUNT(DISTINCT MG.[id])
                FROM [HM].[MG] MG INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id] {katJoin}
                WHERE MG.[seria] IN ('RWP','sRWP') AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo {katFilter};";

            using var conn = new SqlConnection(_connHandel);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
            cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string etap = SqlSafe.ReadString(reader, 0);
                decimal kg = SqlSafe.ReadDecimal(reader, 1);
                int dok = SqlSafe.ReadInt(reader, 2);
                FlowChainNode? node = etap switch
                {
                    "ZYWIEC" => fc.Zywiec,
                    "UBOJ" => fc.Uboj,
                    "PRODUKCJA" => fc.Produkcja,
                    "DYSTRYBUCJA" => fc.Dystrybucja,
                    "KLIENCI" => fc.Klienci,
                    "MROZNIA" => fc.Mroznia,
                    "KARMA" => fc.Karma,
                    "ODPADY" => fc.Odpady,
                    "MASARNIA" => fc.Masarnia,
                    "ROZ_KROJ" => fc.RozchodKrojenia,
                    _ => null
                };
                if (node != null) { node.Kg = kg; node.LiczbaDok = dok; }
            }

            return fc;
        }

        /// <summary>
        /// Próbuje znaleźć zdjęcie towaru w `Assets/Towary/{kod}.{ext}`.
        /// Sprawdza kolejno: jpg, jpeg, png, webp. Zwraca pełną ścieżkę albo null.
        /// </summary>
        private static string? ZnajdzZdjecieTowaru(string kod)
        {
            if (string.IsNullOrWhiteSpace(kod)) return null;
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string folder = System.IO.Path.Combine(baseDir, "Assets", "Towary");
                if (!System.IO.Directory.Exists(folder)) return null;

                // Sanitizuj kod (usuń znaki niedozwolone w nazwach plików)
                var invalid = System.IO.Path.GetInvalidFileNameChars();
                string safe = string.Concat(kod.Where(c => !invalid.Contains(c))).Trim();
                if (string.IsNullOrEmpty(safe)) return null;

                foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
                {
                    string p = System.IO.Path.Combine(folder, safe + ext);
                    if (System.IO.File.Exists(p)) return p;
                }
            }
            catch { /* defensywnie */ }
            return null;
        }

        /// <summary>
        /// Komplet danych dla widoku "Łańcuch Graficzny" — summary + WSZYSTKIE towary
        /// per każdy z 9 etapów (ZYWIEC, UBOJ, PRODUKCJA, DYSTRYBUCJA, KLIENCI, MROZNIA, MASARNIA, KARMA, ODPADY).
        /// Ładuje równolegle (Task.WhenAll) — typowo 1-2s na realnych danych.
        /// </summary>
        public async Task<FlowChainGraficznyData> LoadFlowChainGraficznyAsync(FiltryAnaliz f)
        {
            const int LIMIT = 500;  // hard cap żeby UI nie zawalił się przy patologicznych zakresach
            var summaryTask = LoadFlowChainAsync(f);
            var zywiecTask   = LoadTopTowaryEtapuAsync("ZYWIEC", f, LIMIT);
            var ubojTask     = LoadTopTowaryEtapuAsync("UBOJ", f, LIMIT);
            var prodTask     = LoadTopTowaryEtapuAsync("PRODUKCJA", f, LIMIT);
            var dystTask     = LoadTopTowaryEtapuAsync("DYSTRYBUCJA", f, LIMIT);
            var klienciTask  = LoadTopTowaryEtapuAsync("KLIENCI", f, LIMIT);
            var mrozTask     = LoadTopTowaryEtapuAsync("MROZNIA", f, LIMIT);
            var masarTask    = LoadTopTowaryEtapuAsync("MASARNIA", f, LIMIT);
            var karmaTask    = LoadTopTowaryEtapuAsync("KARMA", f, LIMIT);
            var odpadyTask   = LoadTopTowaryEtapuAsync("ODPADY", f, LIMIT);
            var rozchKrojTask = LoadTopTowaryEtapuAsync("ROZCHOD_KROJ", f, LIMIT);

            await Task.WhenAll(summaryTask, zywiecTask, ubojTask, prodTask, dystTask,
                               klienciTask, mrozTask, masarTask, karmaTask, odpadyTask, rozchKrojTask);

            return new FlowChainGraficznyData
            {
                Summary = summaryTask.Result,
                Zywiec = zywiecTask.Result,
                Uboj = ubojTask.Result,
                Produkcja = prodTask.Result,
                Dystrybucja = dystTask.Result,
                Klienci = klienciTask.Result,
                Mroznia = mrozTask.Result,
                Masarnia = masarTask.Result,
                Karma = karmaTask.Result,
                Odpady = odpadyTask.Result,
                RozchodKrojenia = rozchKrojTask.Result
            };
        }

        /// <summary>
        /// Top N towarów dla danego etapu — używane w kafelkach łańcucha produkcji (mini-lista pod KG).
        /// Lekka query, tylko TW.id + nazwa + kg + obrazek z TowarZdjecia.
        /// </summary>
        public async Task<List<FlowChainTowar>> LoadTopTowaryEtapuAsync(string etap, FiltryAnaliz f, int top = 5)
        {
            var lista = new List<FlowChainTowar>();
            string whereSeria = etap switch
            {
                "ZYWIEC"       => "MG.[seria] = 'sPZ' AND TW.[katalog] = 65882",
                "UBOJ"         => "MG.[seria] IN ('PWU','sPWU')",
                "PRODUKCJA"    => "MG.[seria] IN ('PWP','sPWP')",
                "DYSTRYBUCJA"  => "MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65556",
                "KLIENCI"      => "MG.[seria] IN ('WZ','sWZ','WZ-W','sWZ-W','WZK','sWZK')",
                "MROZNIA"      => "MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65552",
                "KARMA"        => "MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65547",
                "ODPADY"       => "MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65551",
                "MASARNIA"     => "MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65562",
                "ROZCHOD_KROJ" => "MG.[seria] IN ('RWP','sRWP')",
                _              => "1=0"
            };

            // Opcjonalny filtr katalogów (gdy user wybrał w UI)
            string katFilter = "";
            if (f.KatalogiTowarow != null && f.KatalogiTowarow.Count > 0 && etap != "ZYWIEC")
            {
                var safe = f.KatalogiTowarow.Where(k => k > 0).Select(k => k.ToString()).ToList();
                if (safe.Count > 0)
                    katFilter = $"AND TW.[katalog] IN ({string.Join(",", safe)})";
            }

            string sql = $@"
                SELECT TOP (@Top) TW.[id] AS TwId, TW.[kod] AS Kod, TW.[nazwa] AS Nazwa, TW.[katalog] AS Katalog,
                       SUM(ABS(MZ.[ilosc])) AS Kg, COUNT(DISTINCT MG.[id]) AS LiczbaDok
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                WHERE {whereSeria}
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  {katFilter}
                GROUP BY TW.[id], TW.[kod], TW.[nazwa], TW.[katalog]
                ORDER BY SUM(ABS(MZ.[ilosc])) DESC;";

            decimal sumaWszystkich = 0;
            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Top", top);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int katalog = SqlSafe.ReadInt(reader, 3);
                    var (_, _, kategoriaNazwa) = MapujKategorie(katalog);
                    decimal kg = SqlSafe.ReadDecimal(reader, 4);
                    sumaWszystkich += kg;
                    lista.Add(new FlowChainTowar
                    {
                        TwId = SqlSafe.ReadInt(reader, 0),
                        Kod = SqlSafe.ReadString(reader, 1),
                        Nazwa = SqlSafe.ReadString(reader, 2),
                        Katalog = katalog,
                        Kategoria = kategoriaNazwa,
                        Kg = kg,
                        LiczbaDok = SqlSafe.ReadInt(reader, 5)
                    });
                }
            }

            // Przelicz udział % (po sumie top N — nie zawsze cały etap)
            foreach (var t in lista)
                t.ProcentUdzialu = sumaWszystkich > 0 ? t.Kg / sumaWszystkich * 100m : 0;

            // Obrazki BLOB
            await TowaryZdjeciaService.LoadAsync(_connLibra);
            foreach (var t in lista)
                t.ImageSource = TowaryZdjeciaService.Get(t.TwId);

            return lista;
        }

        /// <summary>
        /// Pełen szczegół etapu łańcucha — wszystkie dokumenty + agregaty po towarze/dniu/magazynie/kontrahencie.
        /// Otwierany po kliknięciu kafelka w sub-tab "Stan magazynów".
        /// </summary>
        public async Task<FlowChainEtapDetail> LoadFlowChainEtapDetailAsync(string etap, FiltryAnaliz f)
        {
            await MagazynyHelper.LoadFromDatabaseAsync(_connHandel);

            // Mapowanie etap → SQL WHERE clause + metadata
            var (whereSeria, ikona, kolor, nazwa, opis) = etap switch
            {
                "ZYWIEC"       => ("MG.[seria] = 'sPZ' AND TW.[katalog] = 65882", "🐔", "#F59E0B", "ŻYWIEC", "Przyjęcia żywca od hodowców (sPZ, kat. 65882)"),
                "UBOJ"         => ("MG.[seria] IN ('PWU','sPWU')", "⚙", "#DC2626", "UBÓJ", "Wyjście z linii uboju — tuszki/podroby/odpady (sPWU)"),
                "PRODUKCJA"    => ("MG.[seria] IN ('PWP','sPWP')", "🔪", "#7C3AED", "PRODUKCJA", "Elementy po krojeniu (sPWP) — filet, korpus, skrzydło itp."),
                "DYSTRYBUCJA"  => ("MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65556", "📦", "#2563EB", "DYSTRYBUCJA", "Towar przeniesiony na M.DYST przez sMM- (cel = 65556)"),
                "KLIENCI"      => ("MG.[seria] IN ('WZ','sWZ','WZ-W','sWZ-W','WZK','sWZK')", "🚚", "#10B981", "KLIENCI", "Sprzedaż klientom (sWZ + warianty)"),
                "MROZNIA"      => ("MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65552", "❄", "#0EA5E9", "MROŹNIA", "Przeniesienie do mroźni przez sMM- (cel = 65552)"),
                "KARMA"        => ("MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65547", "🌾", "#CA8A04", "KARMA", "Przeniesienie do magazynu karmy (cel = 65547)"),
                "ODPADY"       => ("MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65551", "🗑", "#94A3B8", "ODPADY", "Przeniesienie do odpadów (cel = 65551)"),
                "MASARNIA"     => ("MG.[seria] IN ('MM-','sMM-') AND MG.[khdzial] = 65562", "🥓", "#9A3412", "MASARNIA", "Przeniesienie do masarni (cel = 65562)"),
                "ROZCHOD_KROJ" => ("MG.[seria] IN ('RWP','sRWP')", "🔪", "#7C3AED", "ROZCHÓD do krojenia", "Wsad do linii krojenia (sRWP)"),
                _              => ("1=0", "❓", "#94A3B8", etap, "Nieznany etap")
            };

            var det = new FlowChainEtapDetail
            {
                EtapNazwa = nazwa,
                EtapIkona = ikona,
                EtapKolor = kolor,
                EtapOpis = opis,
                DataOd = f.DataOd.Date,
                DataDo = f.DataDo.Date
            };

            // Opcjonalny filtr katalogów towarów — wspólny dla wszystkich 5 zapytań poniżej
            string katFilter = "";
            if (f.KatalogiTowarow != null && f.KatalogiTowarow.Count > 0 && etap != "ZYWIEC")
            {
                var safe = f.KatalogiTowarow.Where(k => k > 0).Select(k => k.ToString()).ToList();
                if (safe.Count > 0)
                    katFilter = $"AND TW.[katalog] IN ({string.Join(",", safe)})";
            }

            // Query 1: dokumenty (z agregacją kg per MG.id) + JOIN do kontrahenta
            string sqlDok = $@"
                SELECT MG.[id] AS DokId, MG.[kod] AS DokKod, CAST(MG.[data] AS DATE) AS Data,
                       MG.[seria] AS Seria, MG.[magazyn] AS MagId, MG.[khdzial] AS MagDoId,
                       MG.[khid] AS KhId, ISNULL(C.[shortcut], '') AS Kontrahent,
                       MG.[opis] AS Opis,
                       SUM(ABS(MZ.[ilosc])) AS Kg,
                       COUNT(*) AS LiczbaPozycji
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                LEFT JOIN [SSCommon].[STContractors] C ON C.[id] = MG.[khid]
                WHERE {whereSeria}
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  {katFilter}
                GROUP BY MG.[id], MG.[kod], MG.[data], MG.[seria], MG.[magazyn], MG.[khdzial],
                         MG.[khid], C.[shortcut], MG.[opis]
                ORDER BY MG.[data] DESC, MG.[id] DESC;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlDok, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int? magId = SqlSafe.ReadInt(reader, 4);
                    if (magId == 0) magId = null;
                    int? magDoId = SqlSafe.ReadInt(reader, 5);
                    if (magDoId == 0) magDoId = null;
                    int? khId = SqlSafe.ReadInt(reader, 6);
                    if (khId == 0) khId = null;

                    det.Dokumenty.Add(new FlowChainDokument
                    {
                        Id = SqlSafe.ReadInt(reader, 0),
                        Kod = SqlSafe.ReadString(reader, 1),
                        Data = SqlSafe.ReadDate(reader, 2),
                        Seria = SqlSafe.ReadString(reader, 3),
                        MagazynId = magId,
                        MagazynNazwa = MagazynyHelper.Skrot(magId),
                        MagazynDoId = magDoId,
                        MagazynDoNazwa = magDoId.HasValue ? MagazynyHelper.Skrot(magDoId) : "",
                        KhId = khId,
                        Kontrahent = SqlSafe.ReadString(reader, 7),
                        Opis = SqlSafe.ReadString(reader, 8),
                        Kg = SqlSafe.ReadDecimal(reader, 9),
                        LiczbaPozycji = SqlSafe.ReadInt(reader, 10)
                    });
                }
            }

            det.LiczbaDokumentow = det.Dokumenty.Count;
            det.SumaKg = det.Dokumenty.Sum(d => d.Kg);
            det.LiczbaPozycji = det.Dokumenty.Sum(d => d.LiczbaPozycji);

            // Query 2: per towar (z TW.id dla mapowania na obrazek z TowarZdjecia)
            string sqlTow = $@"
                SELECT TW.[id] AS TwId, TW.[kod] AS Kod, TW.[nazwa] AS Nazwa, TW.[katalog] AS Katalog,
                       SUM(ABS(MZ.[ilosc])) AS Kg, COUNT(DISTINCT MG.[id]) AS LiczbaDok
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                WHERE {whereSeria}
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  {katFilter}
                GROUP BY TW.[id], TW.[kod], TW.[nazwa], TW.[katalog]
                ORDER BY SUM(ABS(MZ.[ilosc])) DESC;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlTow, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int katalog = SqlSafe.ReadInt(reader, 3);
                    var (_, _, kategoriaNazwa) = MapujKategorie(katalog);
                    decimal kg = SqlSafe.ReadDecimal(reader, 4);
                    det.Towary.Add(new FlowChainTowar
                    {
                        TwId = SqlSafe.ReadInt(reader, 0),
                        Kod = SqlSafe.ReadString(reader, 1),
                        Nazwa = SqlSafe.ReadString(reader, 2),
                        Katalog = katalog,
                        Kategoria = kategoriaNazwa,
                        Kg = kg,
                        LiczbaDok = SqlSafe.ReadInt(reader, 5),
                        ProcentUdzialu = det.SumaKg > 0 ? kg / det.SumaKg * 100m : 0
                    });
                }
            }

            // Pobierz obrazki towarów (BLOB z LibraNet.TowarZdjecia) i przypisz
            await TowaryZdjeciaService.LoadAsync(_connLibra);
            foreach (var t in det.Towary)
            {
                t.ImageSource = TowaryZdjeciaService.Get(t.TwId);
            }

            // Query 3: per dzień
            string sqlDzien = $@"
                SELECT CAST(MG.[data] AS DATE) AS Data,
                       SUM(ABS(MZ.[ilosc])) AS Kg, COUNT(DISTINCT MG.[id]) AS LiczbaDok
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                WHERE {whereSeria}
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  {katFilter}
                GROUP BY CAST(MG.[data] AS DATE)
                ORDER BY CAST(MG.[data] AS DATE);";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlDzien, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    det.PerDzien.Add(new FlowChainDzien
                    {
                        Data = SqlSafe.ReadDate(reader, 0),
                        Kg = SqlSafe.ReadDecimal(reader, 1),
                        LiczbaDok = SqlSafe.ReadInt(reader, 2)
                    });
                }
            }

            // Query 4: per magazyn (źródłowy)
            string sqlMag = $@"
                SELECT MZ.[magazyn] AS MagId,
                       SUM(ABS(MZ.[ilosc])) AS Kg, COUNT(DISTINCT MG.[id]) AS LiczbaDok
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                WHERE {whereSeria}
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  {katFilter}
                GROUP BY MZ.[magazyn]
                ORDER BY SUM(ABS(MZ.[ilosc])) DESC;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlMag, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int? magId = SqlSafe.ReadInt(reader, 0);
                    if (magId == 0) magId = null;
                    decimal kg = SqlSafe.ReadDecimal(reader, 1);
                    det.PerMagazyn.Add(new FlowChainMagazyn
                    {
                        Id = magId,
                        Nazwa = MagazynyHelper.Skrot(magId),
                        Kg = kg,
                        LiczbaDok = SqlSafe.ReadInt(reader, 2),
                        ProcentUdzialu = det.SumaKg > 0 ? kg / det.SumaKg * 100m : 0
                    });
                }
            }

            // Query 5: per kontrahent (głównie dla ŻYWIEC i KLIENCI)
            string sqlKh = $@"
                SELECT MG.[khid] AS KhId, ISNULL(C.[shortcut], '— bez kontrahenta —') AS Nazwa,
                       SUM(ABS(MZ.[ilosc])) AS Kg, COUNT(DISTINCT MG.[id]) AS LiczbaDok
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                INNER JOIN [HM].[TW] TW ON TW.[id] = MZ.[idtw]
                LEFT JOIN [SSCommon].[STContractors] C ON C.[id] = MG.[khid]
                WHERE {whereSeria}
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  {katFilter}
                GROUP BY MG.[khid], C.[shortcut]
                ORDER BY SUM(ABS(MZ.[ilosc])) DESC;";

            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlKh, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int? khId = SqlSafe.ReadInt(reader, 0);
                    if (khId == 0) khId = null;
                    decimal kg = SqlSafe.ReadDecimal(reader, 2);
                    det.Kontrahenci.Add(new FlowChainKontrahent
                    {
                        KhId = khId,
                        Nazwa = SqlSafe.ReadString(reader, 1),
                        Kg = kg,
                        LiczbaDok = SqlSafe.ReadInt(reader, 3),
                        ProcentUdzialu = det.SumaKg > 0 ? kg / det.SumaKg * 100m : 0
                    });
                }
            }

            return det;
        }

        private static (string Ikona, string Kolor, string Nazwa) MapujKategorie(int katalog) => katalog switch
        {
            65882 => ("🐔", "#F59E0B", "Żywy"),
            67094 => ("🗑", "#94A3B8", "Odpady"),
            67095 => ("🥩", "#059669", "Mięso"),
            67104 => ("🍗", "#0891B2", "Mięso (inne)"),
            67153 => ("❄️", "#0EA5E9", "Mrożone"),
            _     => ("📦", "#7C3AED", "Inne")
        };

        /// <summary>
        /// Po załadowaniu danych klient sam wybiera bazę do liczenia % i przelicza pozycje.
        /// </summary>
        public static void PrzeliczProcenty(BilansMaterialowy bm, BazaUdzialu baza)
        {
            decimal mianownik = baza switch
            {
                BazaUdzialu.Zywiec => bm.ZywiecKg,
                BazaUdzialu.TuszkaAB => bm.SumaTuszkiAB,
                BazaUdzialu.TuszkaA => bm.TuszkaAKg,
                BazaUdzialu.TuszkaB => bm.TuszkaBKg,
                _ => 0
            };
            foreach (var p in bm.Pozycje)
                p.ProcentBazy = mianownik <= 0 ? 0 : p.Kg / mianownik * 100m;
        }

        private static string KategoriaZKatalogu(int katalog) => katalog switch
        {
            65882 => "Żywy",
            67094 => "Odpady",
            67095 => "Mięso świeże",
            67104 => "Mięso (inne)",
            67153 => "Mrożone",
            _ => "Inne"
        };

        /// <summary>
        /// Profesjonalny raport uzysku per hodowca w wybranej agregacji czasowej.
        /// Łączy sPZ (żywiec od konkretnego hodowcy w bazie Handel — khid → STContractors)
        /// z sPWU (Tuszki + Podroby na magazyn 65554) — proporcjonalnie do udziału żywca w dniu.
        ///
        /// UWAGA: agregacja PER DZIEŃ z proporcjonalnym podziałem PWU jest przybliżeniem
        /// (jeśli w jednym dniu żywiec dostarczyło N hodowców, ubojnia miesza ich na linii
        /// — nie da się dokładnie powiedzieć ile tuszek z którego, ale można proporcjonalnie).
        /// </summary>
        public async Task<List<UzyskiPerHodowca>> LoadUzyskiPerHodowcaAsync(
            FiltryAnaliz f, OkresAgregacji okres)
        {
            // ───────── 1. Żywiec sPZ per hodowca per dzień ─────────
            const string sqlZywiec = @"
                SELECT
                    CAST(MG.[data] AS DATE) AS Data,
                    MG.[khid] AS KhId,
                    ISNULL(C.[shortcut], '— bez kontrahenta —') AS Hodowca,
                    SUM(ABS(MZ.[ilosc])) AS Kg,
                    COUNT(DISTINCT MG.[id]) AS LiczbaDok
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                LEFT JOIN [SSCommon].[STContractors] C ON MG.[khid] = C.[id]
                INNER JOIN [HM].[TW] TW ON MZ.[idtw] = TW.id
                WHERE MG.[seria] = 'sPZ'
                  AND TW.[katalog] = 65882
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                GROUP BY CAST(MG.[data] AS DATE), MG.[khid], C.[shortcut]
                ORDER BY CAST(MG.[data] AS DATE);";

            var zywiec = new List<(DateTime Data, int KhId, string Hodowca, decimal Kg, int LiczbaDok)>();
            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlZywiec, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    zywiec.Add((
                        SqlSafe.ReadDate(reader, 0),
                        SqlSafe.ReadInt(reader, 1),
                        SqlSafe.ReadString(reader, 2),
                        SqlSafe.ReadDecimal(reader, 3),
                        SqlSafe.ReadInt(reader, 4)
                    ));
                }
            }

            // ───────── 2. PWU per dzień + kod (Tuszka A/B + Podroby) ─────────
            const string sqlPwu = @"
                SELECT
                    CAST(MG.[data] AS DATE) AS Data,
                    MZ.[kod] AS Kod,
                    SUM(ABS(MZ.[ilosc])) AS Kg
                FROM [HM].[MG] MG
                INNER JOIN [HM].[MZ] MZ ON MZ.[super] = MG.[id]
                WHERE MG.[seria] IN ('PWU', 'sPWU')
                  AND MZ.[magazyn] = @Magazyn
                  AND MG.[anulowany] = 0
                  AND MG.[data] >= @DataOd AND MG.[data] <= @DataDo
                  AND MZ.[kod] IN ('Kurczak A', 'Kurczak B', N'Wątroba', N'Żołądki', N'Serce')
                GROUP BY CAST(MG.[data] AS DATE), MZ.[kod];";

            var pwuPerDzien = new Dictionary<DateTime, Dictionary<string, decimal>>();
            using (var conn = new SqlConnection(_connHandel))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sqlPwu, conn) { CommandTimeout = 60 };
                cmd.Parameters.AddWithValue("@Magazyn", _magazynUbojnia);
                cmd.Parameters.AddWithValue("@DataOd", f.DataOd.Date);
                cmd.Parameters.AddWithValue("@DataDo", f.DataDo.Date.AddDays(1));
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var data = SqlSafe.ReadDate(reader, 0);
                    var kod = SqlSafe.ReadString(reader, 1);
                    var kg = SqlSafe.ReadDecimal(reader, 2);
                    if (!pwuPerDzien.TryGetValue(data, out var slo))
                    {
                        slo = new Dictionary<string, decimal>();
                        pwuPerDzien[data] = slo;
                    }
                    slo[kod] = (slo.TryGetValue(kod, out var v) ? v : 0) + kg;
                }
            }

            // ───────── 3. Per-dzień: rozdziel PWU proporcjonalnie do udziału żywca hodowcy ─────────
            var perDzienPerHodowca = new List<UzyskiPerHodowca>();
            foreach (var grupaDnia in zywiec.GroupBy(z => z.Data))
            {
                decimal sumaZywcaDnia = grupaDnia.Sum(z => z.Kg);
                if (sumaZywcaDnia <= 0) continue;
                pwuPerDzien.TryGetValue(grupaDnia.Key, out var pwu);
                decimal pwuA = pwu != null && pwu.TryGetValue("Kurczak A", out var a) ? a : 0;
                decimal pwuB = pwu != null && pwu.TryGetValue("Kurczak B", out var b) ? b : 0;
                decimal pwuW = pwu != null && pwu.TryGetValue("Wątroba", out var w) ? w : 0;
                decimal pwuZ = pwu != null && pwu.TryGetValue("Żołądki", out var z2) ? z2 : 0;
                decimal pwuS = pwu != null && pwu.TryGetValue("Serce", out var s) ? s : 0;

                foreach (var hod in grupaDnia)
                {
                    decimal udzial = hod.Kg / sumaZywcaDnia;
                    perDzienPerHodowca.Add(new UzyskiPerHodowca
                    {
                        DataOd = grupaDnia.Key,
                        DataDo = grupaDnia.Key,
                        KhId = hod.KhId,
                        Hodowca = hod.Hodowca,
                        LiczbaDokumentow = hod.LiczbaDok,
                        LiczbaDniDostaw = 1,
                        ZywiecKg = hod.Kg,
                        TuszkaAKg = pwuA * udzial,
                        TuszkaBKg = pwuB * udzial,
                        WatrobaKg = pwuW * udzial,
                        ZoladkiKg = pwuZ * udzial,
                        SerceKg = pwuS * udzial
                    });
                }
            }

            // ───────── 4. Agregacja per wybrany okres ─────────
            var pogrupowane = new Dictionary<string, UzyskiPerHodowca>();
            foreach (var w in perDzienPerHodowca)
            {
                var (klucz, etykieta, od, doData) = OkresHelper.DlaDaty(w.DataOd, okres);
                if (okres == OkresAgregacji.CalyOkres)
                {
                    od = f.DataOd.Date;
                    doData = f.DataDo.Date;
                    etykieta = $"Cały okres ({od:dd.MM.yyyy}–{doData:dd.MM.yyyy})";
                }
                string kluczGrupy = klucz + "|" + w.KhId;

                if (!pogrupowane.TryGetValue(kluczGrupy, out var agg))
                {
                    agg = new UzyskiPerHodowca
                    {
                        KluczOkresu = klucz,
                        EtykietaOkresu = etykieta,
                        DataOd = od,
                        DataDo = doData,
                        KhId = w.KhId,
                        Hodowca = w.Hodowca
                    };
                    pogrupowane[kluczGrupy] = agg;
                }
                agg.LiczbaDokumentow += w.LiczbaDokumentow;
                agg.LiczbaDniDostaw += w.LiczbaDniDostaw;
                agg.ZywiecKg += w.ZywiecKg;
                agg.TuszkaAKg += w.TuszkaAKg;
                agg.TuszkaBKg += w.TuszkaBKg;
                agg.WatrobaKg += w.WatrobaKg;
                agg.ZoladkiKg += w.ZoladkiKg;
                agg.SerceKg += w.SerceKg;
            }

            return pogrupowane.Values
                .OrderBy(u => u.DataOd)
                .ThenByDescending(u => u.ZywiecKg)
                .ToList();
        }

        /// <summary>
        /// Per-klasa wagowa kurczaka (QntInCont w In0E) z min/max/% udziału.
        /// </summary>
        public async Task<List<WydajnoscKlasa>> LoadWydajnoscPerKlasaAsync(FiltryAnaliz f)
        {
            var lista = new List<WydajnoscKlasa>();

            // Klasy towarowe drobiu: 4-7 = duży kurczak, 8-12 = mały. Klasy 1-3 i >12 odrzucamy.
            const string sql = @"
                SELECT
                    e.QntInCont AS Klasa,
                    COUNT(*) AS LiczbaWazen,
                    SUM(e.ActWeight) AS SumaActWeight,
                    MIN(e.ActWeight) AS MinWaga,
                    MAX(e.ActWeight) AS MaxWaga
                FROM dbo.In0E e
                WHERE e.Data >= @DataOd AND e.Data <= @DataDo
                  AND e.QntInCont IS NOT NULL
                  AND e.QntInCont BETWEEN 4 AND 12
                  AND e.ActWeight > 0
                GROUP BY e.QntInCont
                ORDER BY e.QntInCont;";

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", f.DataOd.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", f.DataDo.ToString("yyyy-MM-dd"));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new WydajnoscKlasa
                {
                    Klasa = SqlSafe.ReadInt(reader, 0),
                    LiczbaWazen = SqlSafe.ReadInt(reader, 1),
                    SumaActWeightKg = SqlSafe.ReadDecimal(reader, 2),
                    MinWagaSzt = SqlSafe.ReadDecimal(reader, 3),
                    MaxWagaSzt = SqlSafe.ReadDecimal(reader, 4)
                });
            }

            // % udziału per klasa
            decimal sumaWszystkich = lista.Sum(k => k.SumaActWeightKg);
            if (sumaWszystkich > 0)
                foreach (var k in lista)
                    k.ProcentUdzialu = k.SumaActWeightKg / sumaWszystkich * 100m;

            // Sortowanie: najpierw Duży (4-7), potem Mały (8-12), w grupie wg klasy rosnąco
            return lista
                .OrderBy(k => k.KolejnoscGrupy)
                .ThenBy(k => k.Klasa)
                .ToList();
        }

        /// <summary>
        /// Historia klas wagowych dzień-po-dniu — surowe punkty (Data, Klasa, LiczbaWazeń, SumaKg)
        /// dla zakresu z FiltryAnaliz. Agregacja czasowa (tydzień/miesiąc/kwartał/rok)
        /// wykonywana po stronie .NET przez OkresHelper.
        /// </summary>
        public async Task<List<HistoriaKlasPunkt>> LoadHistoriaKlasAsync(FiltryAnaliz f)
        {
            var lista = new List<HistoriaKlasPunkt>();

            const string sql = @"
                SELECT
                    e.Data,
                    e.QntInCont AS Klasa,
                    COUNT(*) AS LiczbaWazen,
                    SUM(e.ActWeight) AS SumaKg
                FROM dbo.In0E e
                WHERE e.Data >= @DataOd AND e.Data <= @DataDo
                  AND e.QntInCont IS NOT NULL
                  AND e.QntInCont BETWEEN 4 AND 12
                  AND e.ActWeight > 0
                GROUP BY e.Data, e.QntInCont
                ORDER BY e.Data, e.QntInCont;";

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.AddWithValue("@DataOd", f.DataOd.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", f.DataDo.ToString("yyyy-MM-dd"));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                lista.Add(new HistoriaKlasPunkt
                {
                    Data = SqlSafe.ReadDate(reader, 0),
                    Klasa = SqlSafe.ReadInt(reader, 1),
                    LiczbaWazen = SqlSafe.ReadInt(reader, 2),
                    SumaKg = SqlSafe.ReadDecimal(reader, 3)
                });
            }
            return lista;
        }

        /// <summary>
        /// Agregacja punktów dziennych do okresów (tydzień ISO / miesiąc / kwartał / rok).
        /// Zwraca posortowaną listę okresów z kg+ważeniami per-klasa.
        /// </summary>
        public static List<HistoriaKlasOkres> AgregujHistorie(
            List<HistoriaKlasPunkt> punkty, OkresAgregacji okres)
        {
            if (punkty == null || punkty.Count == 0) return new List<HistoriaKlasOkres>();

            var pl = new System.Globalization.CultureInfo("pl-PL");
            var grupy = punkty.GroupBy(p => OkresHelper.DlaDaty(p.Data, okres).Klucz);

            var wynik = new List<HistoriaKlasOkres>();
            foreach (var g in grupy)
            {
                var pierwszy = g.First();
                var (klucz, etykieta, od, doData) = OkresHelper.DlaDaty(pierwszy.Data, okres);

                var kg = g.GroupBy(p => p.Klasa).ToDictionary(gg => gg.Key, gg => gg.Sum(p => p.SumaKg));
                var wazen = g.GroupBy(p => p.Klasa).ToDictionary(gg => gg.Key, gg => gg.Sum(p => p.LiczbaWazen));

                string etykietaKrotka = okres switch
                {
                    OkresAgregacji.Dzienna => od.ToString("dd.MM"),
                    OkresAgregacji.Tygodniowa => $"T{System.Globalization.ISOWeek.GetWeekOfYear(od):00}",
                    OkresAgregacji.Miesieczna => od.ToString("MMM yy", pl),
                    OkresAgregacji.Kwartalna => $"Q{(od.Month - 1) / 3 + 1} {od:yy}",
                    OkresAgregacji.Roczna => od.Year.ToString(),
                    _ => etykieta
                };

                wynik.Add(new HistoriaKlasOkres
                {
                    Klucz = klucz,
                    Etykieta = etykieta,
                    EtykietaKrotka = etykietaKrotka,
                    DataOd = od,
                    DataDo = doData,
                    KgPerKlasa = kg,
                    WazeniaPerKlasa = wazen
                });
            }
            return wynik.OrderBy(o => o.DataOd).ToList();
        }
    }
}
