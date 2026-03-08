using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Partie.Models;

namespace Kalendarz1.Partie.Services
{
    public class PartiaService
    {
        private readonly string _connectionString;
        private static bool _schemaChecked;

        public PartiaService(string connectionString = null)
        {
            _connectionString = connectionString ??
                "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        }

        // ═══════════════════════════════════════════════════════════════
        // SCHEMA AUTO-MIGRATION (adds V2 columns/tables if missing)
        // ═══════════════════════════════════════════════════════════════

        private async Task EnsureSchemaAsync()
        {
            if (_schemaChecked) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // Add StatusV2 column to listapartii if missing
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('listapartii') AND name = 'StatusV2')
    ALTER TABLE listapartii ADD StatusV2 varchar(30) NULL DEFAULT 'IN_PRODUCTION'", conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Add HarmonogramLp column to listapartii if missing
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('listapartii') AND name = 'HarmonogramLp')
    ALTER TABLE listapartii ADD HarmonogramLp int NULL", conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Create PartiaStatus table if missing
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PartiaStatus')
BEGIN
    CREATE TABLE PartiaStatus (
        ID int NOT NULL IDENTITY PRIMARY KEY,
        Partia varchar(15) NOT NULL,
        Status varchar(30) NOT NULL,
        StatusPoprzedni varchar(30) NULL,
        OperatorID varchar(15) NULL,
        OperatorNazwa nvarchar(50) NULL,
        Komentarz nvarchar(500) NULL,
        CreatedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_PartiaStatus_Partia ON PartiaStatus(Partia);
END", conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Create QC_Normy table if missing
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QC_Normy')
BEGIN
    CREATE TABLE QC_Normy (
        ID int NOT NULL IDENTITY PRIMARY KEY,
        Nazwa nvarchar(50) NOT NULL,
        Opis nvarchar(200) NULL,
        MinWartosc decimal(10,2) NULL,
        MaxWartosc decimal(10,2) NULL,
        JednostkaMiary nvarchar(20) NULL,
        Kategoria varchar(30) NOT NULL DEFAULT 'TEMPERATURA',
        IsAktywna bit NOT NULL DEFAULT 1,
        Kolejnosc int NOT NULL DEFAULT 0
    );
    INSERT INTO QC_Normy (Nazwa, Opis, MinWartosc, MaxWartosc, JednostkaMiary, Kategoria, Kolejnosc) VALUES
    ('TempRampa','Temperatura na rampie',NULL,4.00,'C','TEMPERATURA',1),
    ('TempChillera','Temperatura chillera',-2.00,2.00,'C','TEMPERATURA',2),
    ('TempTunel','Temperatura tunelu',NULL,-18.00,'C','TEMPERATURA',3),
    ('KlasaB','Procent klasy B',NULL,20.00,'%','PODSUMOWANIE',10),
    ('Przekarmienie','Przekarmienie w kg',NULL,50.00,'kg','PODSUMOWANIE',11),
    ('Skrzydla','Ocena wad skrzydel (1-5)',1,5,'pkt','WADY',20),
    ('Nogi','Ocena wad nog (1-5)',1,5,'pkt','WADY',21),
    ('Oparzenia','Ocena oparzen (1-5)',1,5,'pkt','WADY',22);
END", conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Create PartiaAuditLog table if missing
                    using (var cmd = new SqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PartiaAuditLog')
BEGIN
    CREATE TABLE PartiaAuditLog (
        ID int NOT NULL IDENTITY PRIMARY KEY,
        Partia varchar(15) NOT NULL,
        Akcja nvarchar(30) NOT NULL,
        Opis nvarchar(500) NULL,
        OperatorID varchar(15) NULL,
        OperatorNazwa nvarchar(50) NULL,
        CreatedAtUTC datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_PAL_Partia ON PartiaAuditLog(Partia);
END", conn))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                _schemaChecked = true;
            }
            catch
            {
                // Schema check failure should not block the app — columns may already exist
                _schemaChecked = true;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LISTA PARTII (master grid)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<PartiaModel>> GetPartieAsync(string dataOd, string dataDo,
            string dzialFilter = null, int? statusFilter = null, string szukaj = null,
            string statusV2Filter = null)
        {
            await EnsureSchemaAsync();
            var partie = new List<PartiaModel>();

            string query = @"
SELECT
    lp.GUID, lp.DIR_ID, lp.Partia, lp.CreateData, lp.CreateGodzina,
    lp.IsClose, lp.CloseData, lp.CloseGodzina,
    lp.CreateOperator, lp.CloseOperator, lp.ArticleID,
    lp.StatusV2, lp.HarmonogramLp,
    pd.CustomerID, pd.CustomerName,
    op_create.Name AS OtworzylNazwa,
    op_close.Name AS ZamknalNazwa,
    fc.DeclI1 AS SztDekl, fc.NettoWeight AS NettoSkup,
    fc.DeclI2 AS Padle, fc.Price AS CenaSkup,
    fc.VetNo, fc.VetComment,
    w_out.WydanoKg, w_out.WydanoSzt,
    w_in.PrzyjetoKg, w_in.PrzyjetoSzt,
    qcp.KlasaB_Proc, qcp.Przekarmienie_Kg,
    qct_rampa.Srednia AS TempRampa,
    qcw.Skrzydla_Ocena, qcw.Nogi_Ocena, qcw.Oparzenia_Ocena,
    CASE WHEN qct_rampa.Srednia IS NOT NULL THEN 1 ELSE 0 END AS MaTemperatury,
    CASE WHEN qcw.Skrzydla_Ocena IS NOT NULL THEN 1 ELSE 0 END AS MaWady,
    (SELECT COUNT(*) FROM Zdjecia z WHERE z.PartiaId = lp.Partia) AS IloscZdjec
FROM listapartii lp
LEFT JOIN PartiaDostawca pd ON lp.Partia = pd.Partia
LEFT JOIN operators op_create ON lp.CreateOperator = op_create.ID
LEFT JOIN operators op_close ON lp.CloseOperator = op_close.ID
LEFT JOIN (
    SELECT Partia, DeclI1, DeclI2, NettoWeight, Price, VetNo, VetComment,
           ROW_NUMBER() OVER (PARTITION BY Partia ORDER BY ID DESC) AS rn
    FROM FarmerCalc WHERE Deleted = 0 OR Deleted IS NULL
) fc ON fc.Partia = lp.Partia AND fc.rn = 1
LEFT JOIN (
    SELECT P1 AS Partia, SUM(ActWeight) AS WydanoKg, SUM(Quantity) AS WydanoSzt
    FROM Out1A WHERE ActWeight IS NOT NULL
    GROUP BY P1
) w_out ON w_out.Partia = lp.Partia
LEFT JOIN (
    SELECT P1 AS Partia, SUM(ActWeight) AS PrzyjetoKg, SUM(Quantity) AS PrzyjetoSzt
    FROM In0E WHERE ActWeight IS NOT NULL
    GROUP BY P1
) w_in ON w_in.Partia = lp.Partia
LEFT JOIN vw_QC_Podsum qcp ON qcp.PartiaId = lp.Partia
LEFT JOIN (
    SELECT PartiaId, Srednia,
           ROW_NUMBER() OVER (PARTITION BY PartiaId ORDER BY ID DESC) AS rn
    FROM TemperaturyMiejsca WHERE LOWER(Miejsce) = 'rampa'
) qct_rampa ON qct_rampa.PartiaId = lp.Partia AND qct_rampa.rn = 1
LEFT JOIN vw_QC_WadySkale qcw ON qcw.PartiaId = lp.Partia
WHERE lp.CreateData >= @DataOd AND lp.CreateData <= @DataDo";

            if (!string.IsNullOrEmpty(dzialFilter))
                query += " AND lp.DIR_ID = @Dzial";
            if (statusFilter.HasValue)
                query += " AND ISNULL(lp.IsClose, 0) = @Status";
            if (!string.IsNullOrEmpty(szukaj))
                query += " AND (lp.Partia LIKE @Szukaj OR pd.CustomerName LIKE @Szukaj OR pd.CustomerID LIKE @Szukaj)";
            if (!string.IsNullOrEmpty(statusV2Filter))
                query += " AND lp.StatusV2 = @StatusV2";

            query += " ORDER BY lp.CreateData DESC, lp.CreateGodzina DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);
                    if (!string.IsNullOrEmpty(dzialFilter))
                        cmd.Parameters.AddWithValue("@Dzial", dzialFilter);
                    if (statusFilter.HasValue)
                        cmd.Parameters.AddWithValue("@Status", statusFilter.Value);
                    if (!string.IsNullOrEmpty(szukaj))
                        cmd.Parameters.AddWithValue("@Szukaj", $"%{szukaj}%");
                    if (!string.IsNullOrEmpty(statusV2Filter))
                        cmd.Parameters.AddWithValue("@StatusV2", statusV2Filter);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            partie.Add(new PartiaModel
                            {
                                GUID = GetStringSafe(reader, "GUID"),
                                DirID = GetStringSafe(reader, "DIR_ID"),
                                Partia = GetStringSafe(reader, "Partia"),
                                CreateData = GetStringSafe(reader, "CreateData"),
                                CreateGodzina = GetStringSafe(reader, "CreateGodzina"),
                                IsClose = GetIntSafe(reader, "IsClose"),
                                CloseData = GetStringSafe(reader, "CloseData"),
                                CloseGodzina = GetStringSafe(reader, "CloseGodzina"),
                                CreateOperator = GetStringSafe(reader, "CreateOperator"),
                                CloseOperator = GetStringSafe(reader, "CloseOperator"),
                                ArticleID = GetStringSafe(reader, "ArticleID"),
                                StatusV2String = GetStringSafe(reader, "StatusV2"),
                                HarmonogramLp = GetIntNullable(reader, "HarmonogramLp"),
                                CustomerID = GetStringSafe(reader, "CustomerID"),
                                CustomerName = GetStringSafe(reader, "CustomerName"),
                                OtworzylNazwa = GetStringSafe(reader, "OtworzylNazwa"),
                                ZamknalNazwa = GetStringSafe(reader, "ZamknalNazwa"),
                                SztDekl = GetIntSafe(reader, "SztDekl"),
                                NettoSkup = GetDecimalSafe(reader, "NettoSkup"),
                                Padle = GetIntSafe(reader, "Padle"),
                                CenaSkup = GetDecimalSafe(reader, "CenaSkup"),
                                VetNo = GetStringSafe(reader, "VetNo"),
                                VetComment = GetStringSafe(reader, "VetComment"),
                                WydanoKg = GetDecimalSafe(reader, "WydanoKg"),
                                WydanoSzt = GetIntSafe(reader, "WydanoSzt"),
                                PrzyjetoKg = GetDecimalSafe(reader, "PrzyjetoKg"),
                                PrzyjetoSzt = GetIntSafe(reader, "PrzyjetoSzt"),
                                KlasaBProc = GetDecimalNullable(reader, "KlasaB_Proc"),
                                PrzekarmienieKg = GetDecimalNullable(reader, "Przekarmienie_Kg"),
                                TempRampa = GetDecimalNullable(reader, "TempRampa"),
                                SkrzydlaOcena = GetIntNullable(reader, "Skrzydla_Ocena"),
                                NogiOcena = GetIntNullable(reader, "Nogi_Ocena"),
                                OparzeniaOcena = GetIntNullable(reader, "Oparzenia_Ocena"),
                                MaTemperatury = GetIntSafe(reader, "MaTemperatury") == 1,
                                MaWady = GetIntSafe(reader, "MaWady") == 1,
                                IloscZdjec = GetIntSafe(reader, "IloscZdjec")
                            });
                        }
                    }
                }
            }
            return partie;
        }

        // ═══════════════════════════════════════════════════════════════
        // WAZENIA (detail tab)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<WazenieModel>> GetWazeniaAsync(string partia)
        {
            var lista = new List<WazenieModel>();
            string query = @"
SELECT GUID, ArticleID, ArticleName, JM, ActWeight, Quantity, Weight,
       ISNULL(Tara,0) AS Tara, Data, Godzina, Wagowy, Direction, P1, P2, 'Out1A' AS Zrodlo
FROM Out1A WHERE P1 = @Partia
UNION ALL
SELECT GUID, ArticleID, ArticleName, JM, ActWeight, Quantity, Weight,
       ISNULL(Tara,0) AS Tara, Data, Godzina, Wagowy, Direction, P1, P2, 'In0E' AS Zrodlo
FROM In0E WHERE P1 = @Partia
ORDER BY Data DESC, Godzina DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@Partia", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new WazenieModel
                            {
                                GUID = GetStringSafe(reader, "GUID"),
                                ArticleID = GetStringSafe(reader, "ArticleID"),
                                ArticleName = GetStringSafe(reader, "ArticleName"),
                                JM = GetStringSafe(reader, "JM"),
                                ActWeight = GetDecimalSafe(reader, "ActWeight"),
                                Quantity = GetIntSafe(reader, "Quantity"),
                                Weight = GetDecimalSafe(reader, "Weight"),
                                Tara = GetDecimalSafe(reader, "Tara"),
                                Data = GetStringSafe(reader, "Data"),
                                Godzina = GetStringSafe(reader, "Godzina"),
                                Wagowy = GetStringSafe(reader, "Wagowy"),
                                Direction = GetStringSafe(reader, "Direction"),
                                P1 = GetStringSafe(reader, "P1"),
                                P2 = GetStringSafe(reader, "P2"),
                                Zrodlo = GetStringSafe(reader, "Zrodlo")
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // PRODUKTY (sumy per artykul)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<ProduktPartiiModel>> GetProduktyAsync(string partia)
        {
            var lista = new List<ProduktPartiiModel>();
            string query = @"
SELECT ArticleID, ArticleName, JM,
       SUM(CASE WHEN ActWeight > 0 THEN ActWeight ELSE 0 END) AS WydanoDodatnie,
       SUM(CASE WHEN ActWeight < 0 THEN ActWeight ELSE 0 END) AS StornoUjemne,
       SUM(ActWeight) AS NettoKg,
       SUM(CASE WHEN ActWeight > 0 THEN Quantity ELSE 0 END) AS SztDodatnie,
       COUNT(*) AS IleWazen
FROM Out1A WHERE P1 = @Partia AND ActWeight IS NOT NULL
GROUP BY ArticleID, ArticleName, JM
ORDER BY SUM(ActWeight) DESC";

            decimal totalKg = 0;
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Partia", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var p = new ProduktPartiiModel
                            {
                                ArticleID = GetStringSafe(reader, "ArticleID"),
                                ArticleName = GetStringSafe(reader, "ArticleName"),
                                JM = GetStringSafe(reader, "JM"),
                                WydanoDodatnie = GetDecimalSafe(reader, "WydanoDodatnie"),
                                StornoUjemne = GetDecimalSafe(reader, "StornoUjemne"),
                                NettoKg = GetDecimalSafe(reader, "NettoKg"),
                                SztDodatnie = GetIntSafe(reader, "SztDodatnie"),
                                IleWazen = GetIntSafe(reader, "IleWazen")
                            };
                            totalKg += p.NettoKg;
                            lista.Add(p);
                        }
                    }
                }
            }

            if (totalKg > 0)
            {
                foreach (var p in lista)
                    p.ProcentUdzialu = Math.Round(p.NettoKg / totalKg * 100, 1);
            }

            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // QC DATA
        // ═══════════════════════════════════════════════════════════════

        public async Task<QCDataModel> GetQCDataAsync(string partia)
        {
            var qc = new QCDataModel();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Temperatury
                using (var cmd = new SqlCommand(
                    "SELECT Miejsce, Proba1, Proba2, Proba3, Proba4, Srednia, Wykonal FROM TemperaturyMiejsca WHERE PartiaId = @P ORDER BY ID", conn))
                {
                    cmd.Parameters.AddWithValue("@P", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            qc.Temperatury.Add(new TemperaturaModel
                            {
                                Miejsce = GetStringSafe(reader, "Miejsce"),
                                Proba1 = GetDecimalNullable(reader, "Proba1"),
                                Proba2 = GetDecimalNullable(reader, "Proba2"),
                                Proba3 = GetDecimalNullable(reader, "Proba3"),
                                Proba4 = GetDecimalNullable(reader, "Proba4"),
                                Srednia = GetDecimalNullable(reader, "Srednia"),
                                Wykonal = GetStringSafe(reader, "Wykonal")
                            });
                        }
                    }
                }

                // Wady (ostatni wpis)
                using (var cmd = new SqlCommand(
                    "SELECT TOP 1 Skrzydla_Ocena, Nogi_Ocena, Oparzenia_Ocena FROM WadyPartiiSkale WHERE PartiaId = @P ORDER BY ID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@P", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            qc.SkrzydlaOcena = GetIntNullable(reader, "Skrzydla_Ocena");
                            qc.NogiOcena = GetIntNullable(reader, "Nogi_Ocena");
                            qc.OparzeniaOcena = GetIntNullable(reader, "Oparzenia_Ocena");
                        }
                    }
                }

                // Podsumowanie
                using (var cmd = new SqlCommand(
                    "SELECT TOP 1 KlasaB_Proc, Przekarmienie_Kg, Notatka FROM PodsumaPartii WHERE PartiaId = @P ORDER BY ID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@P", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            qc.KlasaBProc = GetDecimalNullable(reader, "KlasaB_Proc");
                            qc.PrzekarmienieKg = GetDecimalNullable(reader, "Przekarmienie_Kg");
                            qc.Notatka = GetStringSafe(reader, "Notatka");
                        }
                    }
                }

                // Zdjecia
                using (var cmd = new SqlCommand(
                    "SELECT SciezkaPliku, Opis, WadaTyp, Wykonal FROM Zdjecia WHERE PartiaId = @P ORDER BY ID", conn))
                {
                    cmd.Parameters.AddWithValue("@P", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            qc.Zdjecia.Add(new ZdjecieModel
                            {
                                SciezkaPliku = GetStringSafe(reader, "SciezkaPliku"),
                                Opis = GetStringSafe(reader, "Opis"),
                                WadaTyp = GetStringSafe(reader, "WadaTyp"),
                                Wykonal = GetStringSafe(reader, "Wykonal")
                            });
                        }
                    }
                }
            }
            return qc;
        }

        // ═══════════════════════════════════════════════════════════════
        // SKUP (FarmerCalc)
        // ═══════════════════════════════════════════════════════════════

        public async Task<SkupDataModel> GetSkupDataAsync(string partia)
        {
            string query = @"
SELECT TOP 1
    fc.ID, fc.CalcDate,
    ISNULL(dos.ShortName, fc.CustomerRealGID) AS CustomerName,
    fc.CustomerRealGID AS CustomerID,
    ISNULL(d.Name, '') AS KierowcaNazwa,
    fc.CarID, fc.TrailerID,
    ISNULL(fc.FullWeight, 0) AS BruttoWeight,
    ISNULL(fc.EmptyWeight, 0) AS EmptyWeight,
    ISNULL(fc.NettoWeight, 0) AS NettoWeight,
    ISNULL(fc.DeclI1, 0) AS DeclI1,
    ISNULL(fc.DeclI2, 0) AS DeclI2,
    ISNULL(fc.Price, 0) AS Price,
    fc.Wyjazd, fc.Zaladunek, fc.Przyjazd,
    ISNULL(fc.StartKM, 0) AS StartKM,
    ISNULL(fc.StopKM, 0) AS StopKM
FROM FarmerCalc fc
LEFT JOIN Dostawcy dos ON fc.CustomerRealGID = dos.ID
LEFT JOIN Driver d ON fc.DriverGID = d.GID
WHERE fc.Partia = @Partia AND (fc.Deleted = 0 OR fc.Deleted IS NULL)
ORDER BY fc.ID DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Partia", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new SkupDataModel
                            {
                                FarmerCalcID = GetIntSafe(reader, "ID"),
                                CalcDate = GetDateTimeNullable(reader, "CalcDate"),
                                CustomerName = GetStringSafe(reader, "CustomerName"),
                                CustomerID = GetStringSafe(reader, "CustomerID"),
                                KierowcaNazwa = GetStringSafe(reader, "KierowcaNazwa"),
                                CarID = GetStringSafe(reader, "CarID"),
                                TrailerID = GetStringSafe(reader, "TrailerID"),
                                BruttoWeight = GetDecimalSafe(reader, "BruttoWeight"),
                                EmptyWeight = GetDecimalSafe(reader, "EmptyWeight"),
                                NettoWeight = GetDecimalSafe(reader, "NettoWeight"),
                                DeclI1 = GetIntSafe(reader, "DeclI1"),
                                DeclI2 = GetIntSafe(reader, "DeclI2"),
                                Price = GetDecimalSafe(reader, "Price"),
                                Wyjazd = GetDateTimeNullable(reader, "Wyjazd"),
                                Zaladunek = GetDateTimeNullable(reader, "Zaladunek"),
                                Przyjazd = GetDateTimeNullable(reader, "Przyjazd"),
                                StartKM = GetIntSafe(reader, "StartKM"),
                                StopKM = GetIntSafe(reader, "StopKM")
                            };
                        }
                    }
                }
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════
        // HACCP
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<HaccpModel>> GetHaccpAsync(string partia)
        {
            var lista = new List<HaccpModel>();
            string query = @"
SELECT Dir_ID1 AS ZDzialu, ID1 AS Artykul, P1 AS PartiaZrodlowa,
       Dir_ID2 AS NaDzial, ID2 AS ArtykulDocelowy, P2 AS PartiaDocelowa,
       ISNULL(SumaKg, 0) AS SumaKg, minDate, maxDate
FROM Haccp
WHERE P1 = @Partia OR P2 = @Partia
ORDER BY minDate";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Partia", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new HaccpModel
                            {
                                ZDzialu = GetStringSafe(reader, "ZDzialu"),
                                Artykul = GetStringSafe(reader, "Artykul"),
                                PartiaZrodlowa = GetStringSafe(reader, "PartiaZrodlowa"),
                                NaDzial = GetStringSafe(reader, "NaDzial"),
                                ArtykulDocelowy = GetStringSafe(reader, "ArtykulDocelowy"),
                                PartiaDocelowa = GetStringSafe(reader, "PartiaDocelowa"),
                                SumaKg = GetDecimalSafe(reader, "SumaKg"),
                                MinDate = GetStringSafe(reader, "minDate"),
                                MaxDate = GetStringSafe(reader, "maxDate")
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // TIMELINE
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<TimelineEvent>> GetTimelineAsync(string partia)
        {
            var lista = new List<TimelineEvent>();
            string query = @"
SELECT EventTime, EventType, Description FROM (
    SELECT lp.CreateData + ' ' + lp.CreateGodzina AS EventTime,
           'OPEN' AS EventType,
           'Otwarcie partii ' + lp.Partia + ' przez ' + ISNULL(o.Name, lp.CreateOperator) AS Description
    FROM listapartii lp LEFT JOIN operators o ON lp.CreateOperator = o.ID
    WHERE lp.Partia = @Partia
    UNION ALL
    SELECT lp.CloseData + ' ' + lp.CloseGodzina,
           'CLOSE',
           'Zamkniecie partii przez ' + ISNULL(o.Name, lp.CloseOperator)
    FROM listapartii lp LEFT JOIN operators o ON lp.CloseOperator = o.ID
    WHERE lp.Partia = @Partia AND lp.IsClose = 1
    UNION ALL
    SELECT Data + ' ' + Godzina, 'WEIGHT',
           'Wazenie: ' + ArticleName + ' ' + CAST(ActWeight AS varchar) + ' kg (' + ISNULL(Wagowy,'') + ')'
    FROM Out1A WHERE P1 = @Partia
    UNION ALL
    SELECT Data + ' ' + Godzina, 'WEIGHT',
           'Przyjecie 0E: ' + ArticleName + ' ' + CAST(ActWeight AS varchar) + ' kg'
    FROM In0E WHERE P1 = @Partia
) events
WHERE EventTime IS NOT NULL
ORDER BY EventTime ASC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@Partia", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new TimelineEvent
                            {
                                EventTime = GetStringSafe(reader, "EventTime"),
                                EventType = GetStringSafe(reader, "EventType"),
                                Description = GetStringSafe(reader, "Description")
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // STATYSTYKI (dolny panel)
        // ═══════════════════════════════════════════════════════════════

        public async Task<PartieStatsModel> GetStatsAsync(List<PartiaModel> partie)
        {
            var stats = new PartieStatsModel
            {
                LiczbaPartii = partie.Count,
                Otwartych = 0,
                Zamknietych = 0
            };

            string dzis = DateTime.Today.ToString("yyyy-MM-dd");
            decimal sumWyd = 0, cntWyd = 0;
            decimal sumKlB = 0, cntKlB = 0;
            decimal sumTemp = 0, cntTemp = 0;

            foreach (var p in partie)
            {
                if (p.IsClose == 1) stats.Zamknietych++;
                else stats.Otwartych++;

                if (p.CreateData == dzis)
                {
                    stats.DzisPartii++;
                    stats.DzisKg += p.NettoSkup;
                }

                if (p.WydajnoscProc.HasValue) { sumWyd += p.WydajnoscProc.Value; cntWyd++; }
                if (p.KlasaBProc.HasValue) { sumKlB += p.KlasaBProc.Value; cntKlB++; }
                if (p.TempRampa.HasValue) { sumTemp += p.TempRampa.Value; cntTemp++; }
            }

            stats.SrWydajnosc = cntWyd > 0 ? Math.Round(sumWyd / cntWyd, 1) : 0;
            stats.SrKlasaB = cntKlB > 0 ? Math.Round(sumKlB / cntKlB, 1) : 0;
            stats.SrTempRampa = cntTemp > 0 ? Math.Round(sumTemp / cntTemp, 1) : 0;

            return stats;
        }

        // ═══════════════════════════════════════════════════════════════
        // TWORZENIE PARTII
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> CreatePartiaAsync(string dirId, string customerID, string customerName,
            string articleId, string operatorId)
        {
            string nrPartii = null;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        // Generowanie numeru partii
                        var now = DateTime.Now;
                        int rok2 = now.Year % 100;
                        int dzienRoku = now.DayOfYear;
                        string dayKey = dzienRoku.ToString("D3");

                        using (var cmd = new SqlCommand(
                            "SELECT ISNULL(MAX(PartNo), 0) + 1 FROM partnumbers WHERE Day = @Day", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Day", $"{rok2}{dayKey}");
                            var nextNo = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            nrPartii = $"{rok2}{dayKey}{nextNo:D3}";
                        }

                        // Wpis do partnumbers
                        using (var cmd = new SqlCommand(@"
INSERT INTO partnumbers (ServerID, TermID, ScaleType, Day, PartNo, CalcPartNo)
VALUES ('1', '1', '1', @Day, @PartNo, @CalcPartNo)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Day", $"{rok2}{dayKey}");
                            cmd.Parameters.AddWithValue("@PartNo",
                                Convert.ToInt32(nrPartii.Substring(5)));
                            cmd.Parameters.AddWithValue("@CalcPartNo", nrPartii);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string guid = Guid.NewGuid().ToString();
                        string dataStr = now.ToString("yyyy-MM-dd");
                        string godzinaStr = now.ToString("HH:mm:ss");

                        // INSERT listapartii
                        using (var cmd = new SqlCommand(@"
INSERT INTO listapartii (GUID, DIR_ID, Partia, ArticleID, CreateData, CreateGodzina, CreateOperator, IsClose)
VALUES (@GUID, @DirID, @Partia, @ArticleID, @Data, @Godz, @Operator, 0)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@GUID", guid);
                            cmd.Parameters.AddWithValue("@DirID", dirId);
                            cmd.Parameters.AddWithValue("@Partia", nrPartii);
                            cmd.Parameters.AddWithValue("@ArticleID", (object)articleId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Data", dataStr);
                            cmd.Parameters.AddWithValue("@Godz", godzinaStr);
                            cmd.Parameters.AddWithValue("@Operator", (object)operatorId ?? DBNull.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // INSERT PartiaDostawca
                        using (var cmd = new SqlCommand(@"
INSERT INTO PartiaDostawca (guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina, ModificationData, ModificationGodzina)
VALUES (@GUID, @Partia, @CustID, @CustName, @Data, @Godz, @Data, @Godz)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@GUID", Guid.NewGuid().ToString());
                            cmd.Parameters.AddWithValue("@Partia", nrPartii);
                            cmd.Parameters.AddWithValue("@CustID", customerID);
                            cmd.Parameters.AddWithValue("@CustName", customerName ?? "");
                            cmd.Parameters.AddWithValue("@Data", dataStr);
                            cmd.Parameters.AddWithValue("@Godz", godzinaStr);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Audit log
                        await InsertAuditLogAsync(conn, tran, nrPartii, "Otwarta",
                            $"Nowa partia {nrPartii}, dzial {dirId}, dostawca {customerName}",
                            operatorId, null);

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
            return nrPartii;
        }

        // ═══════════════════════════════════════════════════════════════
        // ZAMYKANIE PARTII
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> ClosePartiaAsync(string partia, string operatorId, string komentarz)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string dataStr = DateTime.Now.ToString("yyyy-MM-dd");
                string godzinaStr = DateTime.Now.ToString("HH:mm:ss");

                using (var cmd = new SqlCommand(@"
UPDATE listapartii SET
    IsClose = 1,
    CloseData = @Data, CloseGodzina = @Godz, CloseOperator = @Operator,
    ModificationData = @Data, ModificationGodzina = @Godz
WHERE Partia = @Partia AND (IsClose = 0 OR IsClose IS NULL)", conn))
                {
                    cmd.Parameters.AddWithValue("@Data", dataStr);
                    cmd.Parameters.AddWithValue("@Godz", godzinaStr);
                    cmd.Parameters.AddWithValue("@Operator", (object)operatorId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Partia", partia);
                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows > 0)
                    {
                        await InsertAuditLogAsync(conn, null, partia, "Zamknieta",
                            string.IsNullOrEmpty(komentarz) ? "Zamkniecie partii" : komentarz,
                            operatorId, null);
                    }
                    return rows > 0;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PONOWNE OTWARCIE
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> ReopenPartiaAsync(string partia, string operatorId, string powod)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string dataStr = DateTime.Now.ToString("yyyy-MM-dd");
                string godzinaStr = DateTime.Now.ToString("HH:mm:ss");

                using (var cmd = new SqlCommand(@"
UPDATE listapartii SET
    IsClose = 0,
    CloseData = NULL, CloseGodzina = NULL, CloseOperator = NULL,
    ModificationData = @Data, ModificationGodzina = @Godz
WHERE Partia = @Partia AND IsClose = 1", conn))
                {
                    cmd.Parameters.AddWithValue("@Data", dataStr);
                    cmd.Parameters.AddWithValue("@Godz", godzinaStr);
                    cmd.Parameters.AddWithValue("@Partia", partia);
                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows > 0)
                    {
                        await InsertAuditLogAsync(conn, null, partia, "PonownieOtwarta",
                            "Powod: " + (powod ?? ""), operatorId, null);
                    }
                    return rows > 0;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DOSTAWCY (combo)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<DostawcaComboItem>> GetDostawcyAsync()
        {
            var lista = new List<DostawcaComboItem>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT ID, ShortName FROM Dostawcy WHERE ISNULL(Halt, 0) = 0 ORDER BY ShortName", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new DostawcaComboItem
                            {
                                ID = GetStringSafe(reader, "ID"),
                                Name = GetStringSafe(reader, "ShortName")
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // HARMONOGRAM DOSTAW (dzisiejszy)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<HarmonogramItem>> GetDzisHarmonogramAsync(string data = null)
        {
            await EnsureSchemaAsync();
            var lista = new List<HarmonogramItem>();
            string dzis = data ?? DateTime.Today.ToString("yyyy-MM-dd");

            string query = @"
SELECT h.Lp, h.DataOdbioru, h.Dostawca, h.Auta, h.SztukiDek, h.WagaDek,
       h.TypCeny, h.Cena, h.Bufor, h.LpW,
       CASE WHEN EXISTS(SELECT 1 FROM listapartii lp WHERE lp.HarmonogramLp = h.Lp) THEN 1 ELSE 0 END AS MaPartie
FROM HarmonogramDostaw h
WHERE h.DataOdbioru = @Data
ORDER BY h.Lp";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Data", dzis);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new HarmonogramItem
                            {
                                Lp = GetIntSafe(reader, "Lp"),
                                DataOdbioru = GetStringSafe(reader, "DataOdbioru"),
                                Dostawca = GetStringSafe(reader, "Dostawca"),
                                Auta = GetIntNullable(reader, "Auta"),
                                SztukiDek = GetIntNullable(reader, "SztukiDek"),
                                WagaDek = GetDecimalNullable(reader, "WagaDek"),
                                TypCeny = GetStringSafe(reader, "TypCeny"),
                                Cena = GetDecimalNullable(reader, "Cena"),
                                Bufor = GetStringSafe(reader, "Bufor"),
                                LpW = GetIntNullable(reader, "LpW"),
                                MaPartie = GetIntSafe(reader, "MaPartie") == 1
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // QC NORMY (configurable norms from DB)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<QCNormaModel>> GetNormyAsync()
        {
            var lista = new List<QCNormaModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT ID, Nazwa, Opis, MinWartosc, MaxWartosc, JednostkaMiary, Kategoria, Kolejnosc FROM QC_Normy WHERE IsAktywna = 1 ORDER BY Kolejnosc", conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new QCNormaModel
                            {
                                ID = GetIntSafe(reader, "ID"),
                                Nazwa = GetStringSafe(reader, "Nazwa"),
                                Opis = GetStringSafe(reader, "Opis"),
                                MinWartosc = GetDecimalNullable(reader, "MinWartosc"),
                                MaxWartosc = GetDecimalNullable(reader, "MaxWartosc"),
                                JednostkaMiary = GetStringSafe(reader, "JednostkaMiary"),
                                Kategoria = GetStringSafe(reader, "Kategoria"),
                                Kolejnosc = GetIntSafe(reader, "Kolejnosc")
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // QC COMPLETENESS CHECK
        // ═══════════════════════════════════════════════════════════════

        public List<ChecklistItem> BuildQCChecklist(PartiaModel partia, List<QCNormaModel> normy)
        {
            var items = new List<ChecklistItem>();

            // Temperatury
            var normTemp = normy.Find(n => n.Nazwa == "TempRampa");
            items.Add(new ChecklistItem
            {
                Nazwa = "Temperatury",
                Opis = "Pomiary temperatur (rampa, chiller, tunel)",
                IsChecked = partia.MaTemperatury,
                IsOK = partia.MaTemperatury && (normTemp == null || normTemp.IsInNorm(partia.TempRampa)),
                IsWarning = partia.MaTemperatury && normTemp != null && !normTemp.IsInNorm(partia.TempRampa),
                Status = partia.MaTemperatury
                    ? (partia.TempRampa.HasValue ? $"Rampa: {partia.TempRampa:N1} C" : "Wypelnione")
                    : "BRAK"
            });

            // Wady
            items.Add(new ChecklistItem
            {
                Nazwa = "Ocena wad",
                Opis = "Skrzydla, nogi, oparzenia (1-5)",
                IsChecked = partia.MaWady,
                IsOK = partia.MaWady,
                Status = partia.MaWady
                    ? $"S:{partia.SkrzydlaOcena} N:{partia.NogiOcena} O:{partia.OparzeniaOcena}"
                    : "BRAK"
            });

            // Klasa B
            var normKlB = normy.Find(n => n.Nazwa == "KlasaB");
            items.Add(new ChecklistItem
            {
                Nazwa = "Klasa B",
                Opis = normKlB != null ? $"Max: {normKlB.MaxWartosc}%" : "Procent klasy B",
                IsChecked = partia.KlasaBProc.HasValue,
                IsOK = partia.KlasaBProc.HasValue && (normKlB == null || normKlB.IsInNorm(partia.KlasaBProc)),
                IsWarning = partia.KlasaBProc.HasValue && normKlB != null && !normKlB.IsInNorm(partia.KlasaBProc),
                Status = partia.KlasaBProc.HasValue ? $"{partia.KlasaBProc:N1}%" : "BRAK"
            });

            // Zdjecia
            items.Add(new ChecklistItem
            {
                Nazwa = "Zdjecia",
                Opis = "Dokumentacja fotograficzna",
                IsChecked = partia.IloscZdjec > 0,
                IsOK = partia.IloscZdjec > 0,
                Status = partia.IloscZdjec > 0 ? $"{partia.IloscZdjec} szt." : "BRAK"
            });

            // Wet
            items.Add(new ChecklistItem
            {
                Nazwa = "Swiadectwo wet.",
                Opis = "Nr swiadectwa weterynaryjnego",
                IsChecked = !string.IsNullOrEmpty(partia.VetNo),
                IsOK = !string.IsNullOrEmpty(partia.VetNo),
                Status = !string.IsNullOrEmpty(partia.VetNo) ? partia.VetNo : "BRAK"
            });

            return items;
        }

        // ═══════════════════════════════════════════════════════════════
        // STATUS V2 OPERATIONS
        // ═══════════════════════════════════════════════════════════════

        public async Task UpdateStatusV2Async(string partia, string newStatus, string oldStatus,
            string operatorId, string operatorNazwa, string komentarz = null)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SqlCommand(
                            "UPDATE listapartii SET StatusV2 = @Status, ModificationData = @Data, ModificationGodzina = @Godz WHERE Partia = @Partia", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Status", newStatus);
                            cmd.Parameters.AddWithValue("@Data", DateTime.Now.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@Godz", DateTime.Now.ToString("HH:mm:ss"));
                            cmd.Parameters.AddWithValue("@Partia", partia);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (var cmd = new SqlCommand(@"
INSERT INTO PartiaStatus (Partia, Status, StatusPoprzedni, OperatorID, OperatorNazwa, Komentarz)
VALUES (@Partia, @Status, @Prev, @OpID, @OpName, @Komentarz)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Partia", partia);
                            cmd.Parameters.AddWithValue("@Status", newStatus);
                            cmd.Parameters.AddWithValue("@Prev", (object)oldStatus ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@OpID", (object)operatorId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@OpName", (object)operatorNazwa ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Komentarz", (object)komentarz ?? DBNull.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task<List<PartiaStatusHistoryItem>> GetStatusHistoryAsync(string partia)
        {
            var lista = new List<PartiaStatusHistoryItem>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT ID, Status, StatusPoprzedni, OperatorNazwa, Komentarz, CreatedAtUTC FROM PartiaStatus WHERE Partia = @P ORDER BY ID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@P", partia);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new PartiaStatusHistoryItem
                            {
                                ID = GetIntSafe(reader, "ID"),
                                Status = GetStringSafe(reader, "Status"),
                                StatusPoprzedni = GetStringSafe(reader, "StatusPoprzedni"),
                                OperatorNazwa = GetStringSafe(reader, "OperatorNazwa"),
                                Komentarz = GetStringSafe(reader, "Komentarz"),
                                CreatedAtUTC = reader.GetDateTime(reader.GetOrdinal("CreatedAtUTC"))
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // AUTO STATUS DETECTION
        // ═══════════════════════════════════════════════════════════════

        public PartiaStatusEnum DetectAutoStatus(PartiaModel partia)
        {
            if (partia.IsClose == 1)
            {
                return (partia.MaTemperatury && partia.MaWady)
                    ? PartiaStatusEnum.CLOSED
                    : PartiaStatusEnum.CLOSED_INCOMPLETE;
            }

            if (partia.WydanoKg > 0)
                return PartiaStatusEnum.IN_PRODUCTION;

            if (!string.IsNullOrEmpty(partia.VetNo))
                return PartiaStatusEnum.APPROVED;

            if (partia.NettoSkup > 0)
                return PartiaStatusEnum.AT_RAMP;

            return PartiaStatusEnum.PLANNED;
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATE PARTIA V2 (with harmonogram linkage)
        // ═══════════════════════════════════════════════════════════════

        public async Task<string> CreatePartiaFromHarmonogramAsync(string dirId, string customerID,
            string customerName, string articleId, string operatorId, int? harmonogramLp)
        {
            await EnsureSchemaAsync();
            string nrPartii = null;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        var now = DateTime.Now;
                        int rok2 = now.Year % 100;
                        int dzienRoku = now.DayOfYear;
                        string dayKey = dzienRoku.ToString("D3");

                        using (var cmd = new SqlCommand(
                            "SELECT ISNULL(MAX(PartNo), 0) + 1 FROM partnumbers WHERE Day = @Day", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Day", $"{rok2}{dayKey}");
                            var nextNo = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            nrPartii = $"{rok2}{dayKey}{nextNo:D3}";
                        }

                        using (var cmd = new SqlCommand(@"
INSERT INTO partnumbers (ServerID, TermID, ScaleType, Day, PartNo, CalcPartNo)
VALUES ('1', '1', '1', @Day, @PartNo, @CalcPartNo)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Day", $"{rok2}{dayKey}");
                            cmd.Parameters.AddWithValue("@PartNo", Convert.ToInt32(nrPartii.Substring(5)));
                            cmd.Parameters.AddWithValue("@CalcPartNo", nrPartii);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string guid = Guid.NewGuid().ToString();
                        string dataStr = now.ToString("yyyy-MM-dd");
                        string godzinaStr = now.ToString("HH:mm:ss");

                        using (var cmd = new SqlCommand(@"
INSERT INTO listapartii (GUID, DIR_ID, Partia, ArticleID, CreateData, CreateGodzina, CreateOperator, IsClose, StatusV2, HarmonogramLp)
VALUES (@GUID, @DirID, @Partia, @ArticleID, @Data, @Godz, @Operator, 0, @StatusV2, @HarmLp)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@GUID", guid);
                            cmd.Parameters.AddWithValue("@DirID", dirId);
                            cmd.Parameters.AddWithValue("@Partia", nrPartii);
                            cmd.Parameters.AddWithValue("@ArticleID", (object)articleId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Data", dataStr);
                            cmd.Parameters.AddWithValue("@Godz", godzinaStr);
                            cmd.Parameters.AddWithValue("@Operator", (object)operatorId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@StatusV2", PartiaStatusEnum.PLANNED.ToString());
                            cmd.Parameters.AddWithValue("@HarmLp", (object)harmonogramLp ?? DBNull.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (var cmd = new SqlCommand(@"
INSERT INTO PartiaDostawca (guid, Partia, CustomerID, CustomerName, CreateData, CreateGodzina, ModificationData, ModificationGodzina)
VALUES (@GUID, @Partia, @CustID, @CustName, @Data, @Godz, @Data, @Godz)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@GUID", Guid.NewGuid().ToString());
                            cmd.Parameters.AddWithValue("@Partia", nrPartii);
                            cmd.Parameters.AddWithValue("@CustID", customerID);
                            cmd.Parameters.AddWithValue("@CustName", customerName ?? "");
                            cmd.Parameters.AddWithValue("@Data", dataStr);
                            cmd.Parameters.AddWithValue("@Godz", godzinaStr);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Status history
                        using (var cmd = new SqlCommand(@"
INSERT INTO PartiaStatus (Partia, Status, StatusPoprzedni, OperatorID, Komentarz)
VALUES (@Partia, @Status, NULL, @OpID, @Komentarz)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Partia", nrPartii);
                            cmd.Parameters.AddWithValue("@Status", PartiaStatusEnum.PLANNED.ToString());
                            cmd.Parameters.AddWithValue("@OpID", (object)operatorId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Komentarz",
                                harmonogramLp.HasValue ? $"Z harmonogramu Lp={harmonogramLp}" : "Reczne utworzenie");
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await InsertAuditLogAsync(conn, tran, nrPartii, "Otwarta",
                            $"Nowa partia {nrPartii}, dzial {dirId}, dostawca {customerName}" +
                            (harmonogramLp.HasValue ? $", harm.Lp={harmonogramLp}" : ""),
                            operatorId, null);

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
            return nrPartii;
        }

        // ═══════════════════════════════════════════════════════════════
        // CLOSE PARTIA V2 (with status)
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> ClosePartiaV2Async(string partia, string operatorId, string komentarz,
            bool qcComplete)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string dataStr = DateTime.Now.ToString("yyyy-MM-dd");
                string godzinaStr = DateTime.Now.ToString("HH:mm:ss");
                string newStatus = qcComplete
                    ? PartiaStatusEnum.CLOSED.ToString()
                    : PartiaStatusEnum.CLOSED_INCOMPLETE.ToString();

                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SqlCommand(@"
UPDATE listapartii SET
    IsClose = 1, StatusV2 = @StatusV2,
    CloseData = @Data, CloseGodzina = @Godz, CloseOperator = @Operator,
    ModificationData = @Data, ModificationGodzina = @Godz
WHERE Partia = @Partia AND (IsClose = 0 OR IsClose IS NULL)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@StatusV2", newStatus);
                            cmd.Parameters.AddWithValue("@Data", dataStr);
                            cmd.Parameters.AddWithValue("@Godz", godzinaStr);
                            cmd.Parameters.AddWithValue("@Operator", (object)operatorId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Partia", partia);
                            int rows = await cmd.ExecuteNonQueryAsync();
                            if (rows == 0) { tran.Rollback(); return false; }
                        }

                        using (var cmd = new SqlCommand(@"
INSERT INTO PartiaStatus (Partia, Status, StatusPoprzedni, OperatorID, Komentarz)
VALUES (@Partia, @Status, 'IN_PRODUCTION', @OpID, @Komentarz)", conn, tran))
                        {
                            cmd.Parameters.AddWithValue("@Partia", partia);
                            cmd.Parameters.AddWithValue("@Status", newStatus);
                            cmd.Parameters.AddWithValue("@OpID", (object)operatorId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Komentarz", (object)komentarz ?? DBNull.Value);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await InsertAuditLogAsync(conn, tran, partia, "Zamknieta",
                            string.IsNullOrEmpty(komentarz) ? "Zamkniecie partii" : komentarz,
                            operatorId, null);

                        tran.Commit();
                        return true;
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DZIS PARTIE (for ProdukcjaDzis dashboard)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<PartiaModel>> GetPartieDzisAsync()
        {
            string dzis = DateTime.Today.ToString("yyyy-MM-dd");
            return await GetPartieAsync(dzis, dzis);
        }

        // ═══════════════════════════════════════════════════════════════
        // AUTO STATUS DETECTION (Feature 10)
        // ═══════════════════════════════════════════════════════════════

        public async Task RunAutoStatusDetectionAsync(List<PartiaModel> parties)
        {
            var updates = new List<(string partia, string newStatus)>();

            foreach (var p in parties)
            {
                if (!p.IsActive) continue;

                var detected = DetectAutoStatus(p);
                if (detected != p.StatusV2 && (int)detected > (int)p.StatusV2
                    && (int)detected <= (int)PartiaStatusEnum.IN_PRODUCTION)
                {
                    updates.Add((p.Partia, detected.ToString()));
                    p.StatusV2String = detected.ToString();
                }
            }

            if (updates.Count == 0) return;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                foreach (var (partia, newStatus) in updates)
                {
                    using (var cmd = new SqlCommand(
                        "UPDATE listapartii SET StatusV2 = @Status, ModificationData = @Data, ModificationGodzina = @Godz WHERE Partia = @Partia", conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", newStatus);
                        cmd.Parameters.AddWithValue("@Data", DateTime.Now.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@Godz", DateTime.Now.ToString("HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@Partia", partia);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ALERTS (Feature 7)
        // ═══════════════════════════════════════════════════════════════

        public List<AlertModel> GetAlerts(List<PartiaModel> parties, List<QCNormaModel> normy)
        {
            var alerts = new List<AlertModel>();
            var now = DateTime.Now;

            foreach (var p in parties.Where(p => p.IsActive))
            {
                // Open > 3h without weighings
                if (p.WydanoKg == 0 && !string.IsNullOrEmpty(p.CreateGodzina))
                {
                    if (TimeSpan.TryParse(p.CreateGodzina, out var openTime))
                    {
                        var openDate = DateTime.Today.Add(openTime);
                        if ((now - openDate).TotalHours > 3)
                        {
                            alerts.Add(new AlertModel
                            {
                                Severity = "WARNING",
                                Message = $"Partia {p.Partia} otwarta >3h bez wazen",
                                Partia = p.Partia
                            });
                        }
                    }
                }

                // Temp > norm
                var normTemp = normy?.Find(n => n.Nazwa == "TempRampa");
                if (normTemp != null && p.TempRampa.HasValue && !normTemp.IsInNorm(p.TempRampa))
                {
                    alerts.Add(new AlertModel
                    {
                        Severity = "ERROR",
                        Message = $"Partia {p.Partia}: temp rampa {p.TempRampa:N1}C (max {normTemp.MaxWartosc}C)",
                        Partia = p.Partia
                    });
                }

                // No vet certificate
                if (string.IsNullOrEmpty(p.VetNo) && p.NettoSkup > 0)
                {
                    alerts.Add(new AlertModel
                    {
                        Severity = "WARNING",
                        Message = $"Partia {p.Partia}: brak swiadectwa wet.",
                        Partia = p.Partia
                    });
                }

                // Klasa B above norm
                var normKlB = normy?.Find(n => n.Nazwa == "KlasaB");
                if (normKlB != null && p.KlasaBProc.HasValue && !normKlB.IsInNorm(p.KlasaBProc))
                {
                    alerts.Add(new AlertModel
                    {
                        Severity = "WARNING",
                        Message = $"Partia {p.Partia}: klasa B {p.KlasaBProc:N1}% (max {normKlB.MaxWartosc}%)",
                        Partia = p.Partia
                    });
                }
            }

            return alerts
                .OrderByDescending(a => a.Severity == "ERROR" ? 2 : a.Severity == "WARNING" ? 1 : 0)
                .ToList();
        }

        // ═══════════════════════════════════════════════════════════════
        // DOSTAWCA COMPARISON (Feature 6)
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<DostawcaComparisonModel>> GetDostawcaComparisonAsync(string dataOd, string dataDo)
        {
            var lista = new List<DostawcaComparisonModel>();
            string query = @"
SELECT
    pd.CustomerID, pd.CustomerName,
    COUNT(*) AS IloscPartii,
    AVG(CASE WHEN w.WydanoKg > 0 AND fc.NettoWeight > 0
         THEN w.WydanoKg / fc.NettoWeight * 100 ELSE NULL END) AS SrWydajnosc,
    AVG(qcp.KlasaB_Proc) AS SrKlasaB,
    AVG(qct.Srednia) AS SrTempRampa,
    SUM(ISNULL(w.WydanoKg, 0)) AS SumKg,
    SUM(ISNULL(fc.DeclI1, 0)) AS SumSzt,
    AVG(CAST(ISNULL(fc.DeclI2, 0) AS decimal)) AS SrPadle
FROM listapartii lp
LEFT JOIN PartiaDostawca pd ON lp.Partia = pd.Partia
LEFT JOIN (
    SELECT P1 AS Partia, SUM(ActWeight) AS WydanoKg FROM Out1A WHERE ActWeight IS NOT NULL GROUP BY P1
) w ON w.Partia = lp.Partia
LEFT JOIN (
    SELECT Partia, NettoWeight, DeclI1, DeclI2,
           ROW_NUMBER() OVER (PARTITION BY Partia ORDER BY ID DESC) AS rn
    FROM FarmerCalc WHERE Deleted = 0 OR Deleted IS NULL
) fc ON fc.Partia = lp.Partia AND fc.rn = 1
LEFT JOIN vw_QC_Podsum qcp ON qcp.PartiaId = lp.Partia
LEFT JOIN (
    SELECT PartiaId, Srednia,
           ROW_NUMBER() OVER (PARTITION BY PartiaId ORDER BY ID DESC) AS rn
    FROM TemperaturyMiejsca WHERE LOWER(Miejsce) = 'rampa'
) qct ON qct.PartiaId = lp.Partia AND qct.rn = 1
WHERE lp.CreateData >= @DataOd AND lp.CreateData <= @DataDo
    AND pd.CustomerID IS NOT NULL
GROUP BY pd.CustomerID, pd.CustomerName
HAVING COUNT(*) >= 1
ORDER BY AVG(CASE WHEN w.WydanoKg > 0 AND fc.NettoWeight > 0
    THEN w.WydanoKg / fc.NettoWeight * 100 ELSE NULL END) DESC";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@DataOd", dataOd);
                    cmd.Parameters.AddWithValue("@DataDo", dataDo);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            lista.Add(new DostawcaComparisonModel
                            {
                                CustomerID = GetStringSafe(reader, "CustomerID"),
                                CustomerName = GetStringSafe(reader, "CustomerName"),
                                IloscPartii = GetIntSafe(reader, "IloscPartii"),
                                SrWydajnosc = GetDecimalSafe(reader, "SrWydajnosc"),
                                SrKlasaB = GetDecimalSafe(reader, "SrKlasaB"),
                                SrTempRampa = GetDecimalSafe(reader, "SrTempRampa"),
                                SumKg = GetDecimalSafe(reader, "SumKg"),
                                SumSzt = GetIntSafe(reader, "SumSzt"),
                                SrPadle = GetDecimalSafe(reader, "SrPadle")
                            });
                        }
                    }
                }
            }
            return lista;
        }

        // ═══════════════════════════════════════════════════════════════
        // HOURLY PRODUCTION BULK (Feature 4 - sparkline)
        // ═══════════════════════════════════════════════════════════════

        public async Task<Dictionary<string, List<HourlyProductionPoint>>> GetHourlyProductionBulkAsync(string dzisData = null)
        {
            var result = new Dictionary<string, List<HourlyProductionPoint>>();
            string dzis = dzisData ?? DateTime.Today.ToString("yyyy-MM-dd");

            string query = @"
SELECT o.P1 AS Partia,
       DATEPART(HOUR, CAST(o.Godzina AS time)) AS Godzina,
       SUM(o.ActWeight) AS KgPerHour
FROM Out1A o
INNER JOIN listapartii lp ON o.P1 = lp.Partia
WHERE lp.CreateData = @Dzis AND o.ActWeight > 0
GROUP BY o.P1, DATEPART(HOUR, CAST(o.Godzina AS time))
ORDER BY o.P1, Godzina";

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@Dzis", dzis);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string partia = GetStringSafe(reader, "Partia");
                            int hour = GetIntSafe(reader, "Godzina");
                            decimal kg = GetDecimalSafe(reader, "KgPerHour");

                            if (!result.ContainsKey(partia))
                                result[partia] = new List<HourlyProductionPoint>();

                            result[partia].Add(new HourlyProductionPoint { Hour = hour, CumulativeKg = kg });
                        }
                    }
                }
            }

            // Convert to cumulative
            foreach (var kvp in result)
            {
                var ordered = kvp.Value.OrderBy(p => p.Hour).ToList();
                decimal cumulative = 0;
                foreach (var pt in ordered)
                {
                    cumulative += pt.CumulativeKg;
                    pt.CumulativeKg = cumulative;
                }
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private async Task InsertAuditLogAsync(SqlConnection conn, SqlTransaction tran,
            string partia, string akcja, string opis, string operatorId, string operatorNazwa)
        {
            using (var cmd = new SqlCommand(@"
INSERT INTO PartiaAuditLog (Partia, Akcja, Opis, OperatorID, OperatorNazwa)
VALUES (@Partia, @Akcja, @Opis, @OpID, @OpNazwa)", conn, tran))
            {
                cmd.Parameters.AddWithValue("@Partia", partia);
                cmd.Parameters.AddWithValue("@Akcja", akcja);
                cmd.Parameters.AddWithValue("@Opis", (object)opis ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OpID", (object)operatorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OpNazwa", (object)operatorNazwa ?? DBNull.Value);
                try { await cmd.ExecuteNonQueryAsync(); } catch { /* audit log failure should not break main operation */ }
            }
        }

        private static string GetStringSafe(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return "";
            var value = reader.GetValue(ordinal);
            return value is string s ? s : value.ToString();
        }

        private static int GetIntSafe(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return 0;
            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static int? GetIntNullable(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToInt32(reader.GetValue(ordinal));
        }

        private static decimal GetDecimalSafe(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return 0;
            return Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static decimal? GetDecimalNullable(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToDecimal(reader.GetValue(ordinal));
        }

        private static DateTime? GetDateTimeNullable(SqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
