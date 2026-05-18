using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kalendarz1.AnalitykaPelna.Services;
using Kalendarz1.Hodowcy.Models;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Hodowcy.Services
{
    /// <summary>
    /// Pobiera kompletny profil hodowcy z LibraNet (Dostawcy + listapartii + FarmerCalc + In0E +
    /// HarmonogramDostaw + DostawcyAdresy). Konsoliduje 6 tabel w jeden wygodny model.
    /// </summary>
    public class HodowcaProfilService
    {
        private readonly string _connLibra;
        private const int Timeout = 60;

        public HodowcaProfilService()
        {
            AnalitykaConfig.ZaladujJesliTrzeba();
            _connLibra = AnalitykaConfig.ConnLibraNet;
        }

        public HodowcaProfilService(string connLibra) => _connLibra = connLibra;

        // ─── PROFIL ───────────────────────────────────────────────────────────

        public async Task<HodowcaProfil?> LoadProfilAsync(string customerID)
        {
            const string sql = @"
                SELECT GUID, GID, ID, IdSymf, IsDeliverer, IsCustomer, IsRolnik, IsSkupowy,
                    ShortName, [Name], Nip, Halt, Trasa,
                    PriceTypeID, Addition, Loss, [Address], PostalCode, City, ProvinceID,
                    Distance, Phone1, Phone2, Phone3, Info1, Info2, Info3, Email, AnimNo, IRZPlus,
                    IncDeadConf,
                    Regon, Pesel, IDCard, IDCardDate, IDCardAuth, TypOsobowosci, TypOsobowosci2
                FROM dbo.Dostawcy WHERE ID = @ID;";

            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@ID", customerID);
            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;

            return new HodowcaProfil
            {
                ID = R(rd, "ID"),
                GID = I(rd, "GID"),
                IdSymf = IN(rd, "IdSymf"),
                IsDeliverer = B(rd, "IsDeliverer"),
                IsCustomer = B(rd, "IsCustomer"),
                IsRolnik = B(rd, "IsRolnik"),
                IsSkupowy = B(rd, "IsSkupowy"),
                ShortName = R(rd, "ShortName"),
                Name = R(rd, "Name"),
                Nip = R(rd, "Nip"),
                Halt = B(rd, "Halt"),
                Trasa = R(rd, "Trasa"),
                PriceTypeID = IN(rd, "PriceTypeID"),
                Addition = DN(rd, "Addition"),
                Loss = DN(rd, "Loss"),
                Address = R(rd, "Address"),
                PostalCode = R(rd, "PostalCode"),
                City = R(rd, "City"),
                ProvinceID = R(rd, "ProvinceID"),
                Distance = IN(rd, "Distance"),
                Phone1 = R(rd, "Phone1"),
                Phone2 = R(rd, "Phone2"),
                Phone3 = R(rd, "Phone3"),
                Info1 = R(rd, "Info1"),
                Info2 = R(rd, "Info2"),
                Info3 = R(rd, "Info3"),
                Email = R(rd, "Email"),
                AnimNo = R(rd, "AnimNo"),
                IRZPlus = R(rd, "IRZPlus"),
                IncDeadConf = B(rd, "IncDeadConf"),
                Regon = R(rd, "Regon"),
                Pesel = R(rd, "Pesel"),
                IDCard = R(rd, "IDCard"),
                IDCardDate = TN(rd, "IDCardDate"),
                IDCardAuth = R(rd, "IDCardAuth"),
                TypOsobowosci = R(rd, "TypOsobowosci"),
                TypOsobowosci2 = R(rd, "TypOsobowosci2")
            };
        }

        // ─── HISTORIA PARTII ──────────────────────────────────────────────────

        public async Task<List<HodowcaPartia>> LoadHistoriaPartiiAsync(string customerID, int top = 200)
        {
            // PRAWDZIWY schema: listapartii ma tylko PK + status, RESZTĘ (kg, sztuki, cena, padłe,
            // wydajność, klasa B, temp. rampy) wyciągamy z 4 zewnętrznych źródeł:
            //   - dbo.FarmerCalc      → NettoWeight (kg żywca), DeclI1 (sztuki), DeclI2 (padłe), Price (cena)
            //   - dbo.In0E            → PrzyjetoKg (suma ActWeight), PrzyjetoSzt (count)
            //   - dbo.Out1A           → WydanoKg (suma ActWeight) — do liczenia wydajności
            //   - dbo.vw_QC_Podsum    → KlasaB_Proc (jakość)         [opcjonalne]
            //   - dbo.TemperaturyMiejsca → Srednia (rampa)            [opcjonalne]
            // Wydajność liczymy w .NET: WydanoKg / NettoWeight * 100.
            //
            // Sprawdzamy istnienie opcjonalnych źródeł aby nie wysypać query gdy ich brak.

            bool maQC = await ObjectExistsAsync("dbo.vw_QC_Podsum");
            bool maTemp = await ObjectExistsAsync("dbo.TemperaturyMiejsca");
            bool maOut1A = await ObjectExistsAsync("dbo.Out1A");

            // FarmerCalc używa PartiaGuid (NIE Partia/string) — JOIN przez PartiaDostawca.guid
            // Sprawdzamy istnienie kolumny "guid" w PartiaDostawca (powinna być, ale defensywnie)
            bool joinFcByGuid = await ColumnExistsAsync("dbo.PartiaDostawca", "guid");

            var sb = new System.Text.StringBuilder();
            sb.Append($@"
                SELECT TOP ({top})
                    fc.CarLp             AS LpDostawy,
                    lp.Partia,
                    lp.CreateData,
                    lp.CreateGodzina,
                    fc.NettoWeight       AS NettoSkup,
                    fc.NettoFarmWeight   AS NettoH,
                    fc.FullFarmWeight    AS BruttoH,
                    fc.EmptyFarmWeight   AS TaraH,
                    fc.FullWeight        AS BruttoU,
                    fc.EmptyWeight       AS TaraU,
                    fc.DeclI1            AS SztDekl,
                    fc.DeclI2            AS Padle,
                    fc.DeclI3            AS CH,
                    fc.DeclI4            AS NW,
                    fc.DeclI5            AS ZM,
                    fc.LumQnt            AS LUMEL,
                    fc.ProdQnt           AS SztWyb,
                    fc.ProdWgt           AS KgWyb,
                    fc.Loss              AS Loss,
                    fc.IncDeadConf       AS PiK,
                    fc.Opasienie         AS Opasienie,
                    fc.Price             AS CenaSkup,
                    fc.VetNo,
                    fc.VetComment,
                    fc.CarID             AS Auto,
                    fc.TrailerID         AS Naczepa,
                    drv.Name             AS Kierowca,
                    fc.Przyjazd,
                    fc.DojazdHodowca,
                    fc.Zaladunek,
                    fc.ZaladunekKoniec,
                    fc.WyjazdHodowca,
                    fc.KoniecUslugi,
                    lp.IsClose,
                    lp.StatusV2,
                    lp.HarmonogramLp,
                    inE.PrzyjetoKg,
                    inE.PrzyjetoSzt,
                    {(maOut1A ? "outE.WydanoKg" : "CAST(NULL AS decimal(18,2))")} AS WydanoKg,
                    {(maQC ? "qcp.KlasaB_Proc" : "CAST(NULL AS decimal(18,2))")} AS KlasaBProc,
                    {(maTemp ? "qct.Srednia" : "CAST(NULL AS decimal(18,2))")}     AS TempRampa,
                    hd.DataOdbioru   AS DataWstawienia,
                    hd.SztukiDek     AS SztDekHarm,
                    hd.WagaDek       AS WagaDekHarm,
                    hd.Auta          AS AutaHarm
                FROM dbo.listapartii lp
                INNER JOIN dbo.PartiaDostawca pd ON pd.Partia = lp.Partia
                LEFT JOIN (
                    SELECT {(joinFcByGuid ? "PartiaGuid," : "")} Partia, CarLp, DeclI1, DeclI2, DeclI3, DeclI4, DeclI5,
                           LumQnt, ProdQnt, ProdWgt,
                           NettoWeight, NettoFarmWeight, FullFarmWeight, EmptyFarmWeight, FullWeight, EmptyWeight,
                           Loss, IncDeadConf, Opasienie,
                           Price, VetNo, VetComment, CarID, TrailerID, DriverGID,
                           Przyjazd, DojazdHodowca, Zaladunek, ZaladunekKoniec, WyjazdHodowca, KoniecUslugi,
                           ROW_NUMBER() OVER (PARTITION BY {(joinFcByGuid ? "PartiaGuid" : "Partia")} ORDER BY ID DESC) AS rn
                    FROM dbo.FarmerCalc
                    WHERE ISNULL(Deleted, 0) = 0
                ) fc ON {(joinFcByGuid ? "fc.PartiaGuid = pd.guid" : "fc.Partia = lp.Partia")} AND fc.rn = 1
                LEFT JOIN dbo.Driver drv ON drv.GID = fc.DriverGID
                LEFT JOIN (
                    SELECT P1 AS Partia,
                           SUM(CASE WHEN ActWeight > 0 THEN ActWeight ELSE 0 END) AS PrzyjetoKg,
                           COUNT(*) AS PrzyjetoSzt
                    FROM dbo.In0E
                    WHERE QntInCont BETWEEN 4 AND 12
                    GROUP BY P1
                ) inE ON inE.Partia = lp.Partia
                LEFT JOIN dbo.HarmonogramDostaw hd ON hd.Lp = lp.HarmonogramLp
            ");
            if (maOut1A)
                sb.Append(@"
                LEFT JOIN (
                    SELECT P1 AS Partia, SUM(ActWeight) AS WydanoKg
                    FROM dbo.Out1A
                    WHERE ActWeight IS NOT NULL
                    GROUP BY P1
                ) outE ON outE.Partia = lp.Partia
                ");
            if (maQC)
                sb.Append("\nLEFT JOIN dbo.vw_QC_Podsum qcp ON qcp.PartiaId = lp.Partia\n");
            if (maTemp)
                sb.Append(@"
                LEFT JOIN (
                    SELECT PartiaId, Srednia,
                           ROW_NUMBER() OVER (PARTITION BY PartiaId ORDER BY ID DESC) AS rn
                    FROM dbo.TemperaturyMiejsca
                    WHERE LOWER(Miejsce) = 'rampa'
                ) qct ON qct.PartiaId = lp.Partia AND qct.rn = 1
                ");
            sb.Append(@"
                WHERE pd.CustomerID = @CustomerID
                ORDER BY lp.CreateData DESC, lp.CreateGodzina DESC;");

            var list = new List<HodowcaPartia>();
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sb.ToString(), conn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@CustomerID", customerID);
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                decimal? netto = DN(rd, "NettoSkup");
                decimal? wydano = DN(rd, "WydanoKg");
                decimal? wydaj = (netto.HasValue && netto.Value > 0 && wydano.HasValue)
                    ? wydano.Value / netto.Value * 100m
                    : (decimal?)null;

                decimal? loss = DN(rd, "Loss");
                list.Add(new HodowcaPartia
                {
                    LpDostawy = IN(rd, "LpDostawy"),
                    Partia = R(rd, "Partia"),
                    CreateData = T(rd, "CreateData"),
                    CreateGodzina = R(rd, "CreateGodzina"),
                    NettoSkup = netto,
                    NettoH = DN(rd, "NettoH"),
                    BruttoH = DN(rd, "BruttoH"),
                    TaraH = DN(rd, "TaraH"),
                    BruttoU = DN(rd, "BruttoU"),
                    TaraU = DN(rd, "TaraU"),
                    SztDekl = IN(rd, "SztDekl"),
                    Padle = DN(rd, "Padle"),
                    CH = IN(rd, "CH"),
                    NW = IN(rd, "NW"),
                    ZM = IN(rd, "ZM"),
                    LUMEL = IN(rd, "LUMEL"),
                    SztWyb = IN(rd, "SztWyb"),
                    KgWyb = DN(rd, "KgWyb"),
                    UbytekProc = loss.HasValue ? loss.Value * 100m : (decimal?)null,
                    PiK = B(rd, "PiK"),
                    Opasienie = DN(rd, "Opasienie"),
                    WydajnoscProc = wydaj,
                    KlasaBProc = DN(rd, "KlasaBProc"),
                    TempRampa = DN(rd, "TempRampa"),
                    IsClose = B(rd, "IsClose"),
                    StatusV2 = R(rd, "StatusV2"),
                    CenaSkup = DN(rd, "CenaSkup"),
                    VetNo = R(rd, "VetNo"),
                    VetComment = R(rd, "VetComment"),
                    PrzyjetoKg = DN(rd, "PrzyjetoKg"),
                    PrzyjetoSzt = IN(rd, "PrzyjetoSzt"),
                    DataWstawienia = TN(rd, "DataWstawienia"),
                    SztDekHarm = IN(rd, "SztDekHarm"),
                    WagaDekHarm = DN(rd, "WagaDekHarm"),
                    AutaHarm = R(rd, "AutaHarm"),
                    Kierowca = R(rd, "Kierowca"),
                    Auto = R(rd, "Auto"),
                    Naczepa = R(rd, "Naczepa"),
                    Przyjazd = TN(rd, "Przyjazd"),
                    DojazdHodowca = TN(rd, "DojazdHodowca"),
                    Zaladunek = TN(rd, "Zaladunek"),
                    ZaladunekKoniec = TN(rd, "ZaladunekKoniec"),
                    WyjazdHodowca = TN(rd, "WyjazdHodowca"),
                    KoniecUslugi = TN(rd, "KoniecUslugi")
                });
            }
            return list;
        }

        /// <summary>Czy obiekt (tabela/view) istnieje w bazie. Cache-owane per service-instance.</summary>
        private readonly Dictionary<string, bool> _existsCache = new();
        private async Task<bool> ObjectExistsAsync(string objectName)
        {
            if (_existsCache.TryGetValue(objectName, out bool cached)) return cached;
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(
                "SELECT CASE WHEN OBJECT_ID(@n) IS NOT NULL THEN 1 ELSE 0 END", conn);
            cmd.Parameters.AddWithValue("@n", objectName);
            int v = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _existsCache[objectName] = v == 1;
            return v == 1;
        }

        private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            string key = $"{tableName}.{columnName}";
            if (_existsCache.TryGetValue(key, out bool cached)) return cached;
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(@"
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(@t) AND name = @c
                ) THEN 1 ELSE 0 END", conn);
            cmd.Parameters.AddWithValue("@t", tableName);
            cmd.Parameters.AddWithValue("@c", columnName);
            int v = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            _existsCache[key] = v == 1;
            return v == 1;
        }

        // ─── STATYSTYKI ───────────────────────────────────────────────────────

        public HodowcaStatystyki BudujStatystyki(List<HodowcaPartia> partie, int? oknoOstatnichDni = 90)
        {
            DateTime? cutoff = oknoOstatnichDni.HasValue
                ? DateTime.Today.AddDays(-oknoOstatnichDni.Value)
                : (DateTime?)null;
            var p = cutoff.HasValue
                ? partie.Where(x => x.CreateData >= cutoff.Value).ToList()
                : partie;

            if (p.Count == 0)
                return new HodowcaStatystyki { LiczbaPartii = 0, LiczbaPartiiZycie = partie.Count };

            var stat = new HodowcaStatystyki
            {
                LiczbaPartii = p.Count,
                SumaSkupKg = p.Sum(x => x.NettoSkup ?? 0m),
                SumaPrzyjetoKg = p.Sum(x => x.PrzyjetoKg ?? 0m),
                SrWydajnosc = p.Where(x => x.WydajnoscProc.HasValue).Select(x => x.WydajnoscProc!.Value).DefaultIfEmpty(0m).Average(),
                SrKlasaB = p.Where(x => x.KlasaBProc.HasValue).Select(x => x.KlasaBProc!.Value).DefaultIfEmpty(0m).Average(),
                SrTempRampa = p.Where(x => x.TempRampa.HasValue).Select(x => x.TempRampa!.Value).DefaultIfEmpty(0m).Average(),
                OstatniaDostawa = p.Max(x => x.CreateData),
                PierwszaDostawa = p.Min(x => x.CreateData),
                SumaSztukDek = p.Sum(x => (decimal?)x.SztDekl ?? 0m),
                SumaPadle = p.Sum(x => x.Padle ?? 0m),
                SrCenaSkup = p.Where(x => x.CenaSkup.HasValue).Select(x => x.CenaSkup!.Value).DefaultIfEmpty(0m).Average(),
                SzacowanyObrot = p.Sum(x => (x.NettoSkup ?? 0m) * (x.CenaSkup ?? 0m)),
                LiczbaPartiiZycie = partie.Count
            };

            // Wiek partii (cykl od wstawienia)
            var pZWiek = p.Where(x => x.WiekDni.HasValue && x.WiekDni.Value >= 0 && x.WiekDni.Value < 100).ToList();
            stat.LiczbaPartiiZWstawieniem = pZWiek.Count;
            if (pZWiek.Count > 0)
            {
                stat.SrWiekDni = (decimal)pZWiek.Average(x => x.WiekDni!.Value);
                stat.MinWiekDni = pZWiek.Min(x => x.WiekDni!.Value);
                stat.MaxWiekDni = pZWiek.Max(x => x.WiekDni!.Value);
            }

            // Średnia waga sztuki
            var pZWaga = p.Where(x => x.SrWagaSzt.HasValue && x.SrWagaSzt.Value > 0).ToList();
            if (pZWaga.Count > 0)
                stat.SrWagaSzt = pZWaga.Average(x => x.SrWagaSzt!.Value);

            // Łączna strata sztuk + %
            var pZStrata = p.Where(x => x.StratySzt.HasValue && x.SztDekl.HasValue && x.SztDekl.Value > 0).ToList();
            if (pZStrata.Count > 0)
            {
                stat.SumaStratSzt = pZStrata.Sum(x => x.StratySzt!.Value);
                int sumaDek = pZStrata.Sum(x => x.SztDekl!.Value);
                stat.StratySztProc = sumaDek > 0 ? stat.SumaStratSzt!.Value * 100m / sumaDek : 0m;
            }

            // ─── Konfiskaty (CH/NW/ZM) i LUMEL ─────────────────────────────────
            stat.SumaCH = p.Sum(x => x.CH ?? 0);
            stat.SumaNW = p.Sum(x => x.NW ?? 0);
            stat.SumaZM = p.Sum(x => x.ZM ?? 0);
            stat.SumaLUMEL = p.Sum(x => x.LUMEL ?? 0);
            stat.SumaKonfiskat = stat.SumaCH + stat.SumaNW + stat.SumaZM;

            int sumaDekKonf = p.Where(x => x.SztDekl.HasValue && x.SztDekl.Value > 0).Sum(x => x.SztDekl!.Value);
            if (sumaDekKonf > 0)
                stat.KonfiskatyProc = stat.SumaKonfiskat * 100m / sumaDekKonf;

            // Ubytek transportowy (NettoH vs NettoU)
            var pZUbytek = p.Where(x => x.UbytekTransKg.HasValue && x.NettoH.HasValue && x.NettoH.Value > 0).ToList();
            if (pZUbytek.Count > 0)
            {
                stat.SumaUbytekTransKg = pZUbytek.Sum(x => x.UbytekTransKg!.Value);
                stat.SrUbytekTransProc = pZUbytek
                    .Select(x => x.UbytekTransProc!.Value)
                    .DefaultIfEmpty(0m).Average();
            }
            stat.DniOdOstatniej = stat.OstatniaDostawa.HasValue
                ? (DateTime.Today - stat.OstatniaDostawa.Value.Date).Days
                : (int?)null;

            // Średni cykl między dostawami (mediana lub średnia różnica dni)
            var sortedDaty = p.Select(x => x.CreateData.Date).Distinct().OrderBy(d => d).ToList();
            if (sortedDaty.Count >= 2)
            {
                var roznice = new List<int>();
                for (int i = 1; i < sortedDaty.Count; i++)
                    roznice.Add((sortedDaty[i] - sortedDaty[i - 1]).Days);
                stat.SrCyklDni = (int)Math.Round(roznice.Average());
            }

            return stat;
        }

        // ─── HARMONOGRAM ─────────────────────────────────────────────────────

        public async Task<List<HodowcaHarmonogramItem>> LoadHarmonogramAsync(string customerName, int dniWPrzod = 90, int dniWstecz = 60)
        {
            // HarmonogramDostaw używa nazwy dostawcy jako klucza (bo tabela jest „luźna").
            // Sprawdzamy które kolumny opcjonalne istnieją — różne instalacje LibraNet mogą się różnić.
            bool maAuta = await ColumnExistsAsync("dbo.HarmonogramDostaw", "Auta");
            bool maTypCeny = await ColumnExistsAsync("dbo.HarmonogramDostaw", "TypCeny");
            bool maCena = await ColumnExistsAsync("dbo.HarmonogramDostaw", "Cena");
            bool maBufor = await ColumnExistsAsync("dbo.HarmonogramDostaw", "Bufor");
            bool maLpW = await ColumnExistsAsync("dbo.HarmonogramDostaw", "LpW");
            bool maSztSzuf = await ColumnExistsAsync("dbo.HarmonogramDostaw", "SztSzuflada");
            bool maTypUm = await ColumnExistsAsync("dbo.HarmonogramDostaw", "TypUmowy");
            bool maKtoUtw = await ColumnExistsAsync("dbo.HarmonogramDostaw", "KtoUtw");
            bool maKiedyUtw = await ColumnExistsAsync("dbo.HarmonogramDostaw", "KiedyUtw");

            string sql = $@"
                SELECT h.Lp, h.DataOdbioru, h.SztukiDek, h.WagaDek,
                       {(maAuta ? "h.Auta" : "CAST(NULL AS nvarchar(100))")}      AS Auta,
                       {(maTypCeny ? "h.TypCeny" : "CAST(NULL AS nvarchar(50))")}  AS TypCeny,
                       {(maCena ? "h.Cena" : "CAST(NULL AS decimal(10,2))")}       AS Cena,
                       {(maBufor ? "h.Bufor" : "CAST(NULL AS int)")}                AS Bufor,
                       {(maLpW ? "h.LpW" : "CAST(NULL AS int)")}                    AS LpW,
                       {(maSztSzuf ? "h.SztSzuflada" : "CAST(NULL AS int)")}        AS SztSzuflada,
                       {(maTypUm ? "h.TypUmowy" : "CAST(NULL AS nvarchar(50))")}    AS TypUmowy,
                       {(maKtoUtw ? "h.KtoUtw" : "CAST(NULL AS nvarchar(50))")}     AS KtoUtw,
                       {(maKiedyUtw ? "h.KiedyUtw" : "CAST(NULL AS datetime)")}     AS KiedyUtw,
                       (SELECT TOP 1 lp.Partia FROM dbo.listapartii lp WHERE lp.HarmonogramLp = h.Lp) AS PartiaNumer,
                       CASE WHEN EXISTS (
                           SELECT 1 FROM dbo.listapartii lp WHERE lp.HarmonogramLp = h.Lp
                       ) THEN 1 ELSE 0 END AS MaPartie
                FROM dbo.HarmonogramDostaw h
                WHERE LTRIM(RTRIM(UPPER(ISNULL(h.Dostawca, '')))) = LTRIM(RTRIM(UPPER(@Name)))
                  AND h.DataOdbioru >= @DataOd
                  AND h.DataOdbioru <= @DataDo
                ORDER BY h.DataOdbioru DESC, h.Lp DESC;";

            var list = new List<HodowcaHarmonogramItem>();
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@Name", customerName ?? "");
            cmd.Parameters.AddWithValue("@DataOd", DateTime.Today.AddDays(-dniWstecz).ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@DataDo", DateTime.Today.AddDays(dniWPrzod).ToString("yyyy-MM-dd"));
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new HodowcaHarmonogramItem
                {
                    Lp = I(rd, "Lp"),
                    LpW = IN(rd, "LpW"),
                    DataOdbioru = T(rd, "DataOdbioru"),
                    SztukiDek = IN(rd, "SztukiDek"),
                    WagaDek = DN(rd, "WagaDek"),
                    SztSzuflada = IN(rd, "SztSzuflada"),
                    Auta = R(rd, "Auta"),
                    TypCeny = R(rd, "TypCeny"),
                    Cena = DN(rd, "Cena"),
                    Bufor = IN(rd, "Bufor"),
                    TypUmowy = R(rd, "TypUmowy"),
                    KtoUtw = R(rd, "KtoUtw"),
                    KiedyUtw = TN(rd, "KiedyUtw"),
                    PartiaNumer = R(rd, "PartiaNumer"),
                    MaPartie = I(rd, "MaPartie") == 1
                });
            }
            return list;
        }

        // ─── FERMY (DostawcyAdresy Kind=2) ───────────────────────────────────

        public async Task<List<HodowcaFerma>> LoadFermyAsync(int gid)
        {
            const string sql = @"
                SELECT [Name], [Address], PostalCode, City, AnimNo, IRZPlus, Info1
                FROM dbo.DostawcyAdresy
                WHERE CustomerGID = @G AND Kind = 2 AND ISNULL(Deleted, 0) = 0
                ORDER BY [Name];";

            var list = new List<HodowcaFerma>();
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@G", gid);
            using var rd = await cmd.ExecuteReaderAsync();
            int lp = 1;
            while (await rd.ReadAsync())
            {
                list.Add(new HodowcaFerma
                {
                    Lp = lp++,
                    Adres = string.Join(" ", new[] { R(rd, "Name"), R(rd, "Address") }
                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                    PostalCode = R(rd, "PostalCode"),
                    City = R(rd, "City"),
                    AnimNo = R(rd, "AnimNo"),
                    Uwagi = R(rd, "Info1")
                });
            }
            return list;
        }

        // ─── KLASY WAGOWE (In0E QntInCont per hodowca) ────────────────────────

        public async Task<List<HodowcaKlasaWagowa>> LoadKlasyWagoweAsync(string customerID, int dniWstecz = 365)
        {
            const string sql = @"
                SELECT e.QntInCont AS Klasa,
                       COUNT(*) AS LiczbaWazen,
                       SUM(CASE WHEN e.ActWeight > 0 THEN e.ActWeight ELSE 0 END) AS SumaKg
                FROM dbo.In0E e
                INNER JOIN dbo.PartiaDostawca pd ON e.P1 = pd.Partia
                WHERE pd.CustomerID = @CustomerID
                  AND e.QntInCont BETWEEN 4 AND 12
                  AND e.ActWeight > 0
                  AND e.Data >= @DataOd
                GROUP BY e.QntInCont
                ORDER BY e.QntInCont;";

            var lista = new List<HodowcaKlasaWagowa>();
            using var conn = new SqlConnection(_connLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = Timeout };
            cmd.Parameters.AddWithValue("@CustomerID", customerID);
            cmd.Parameters.AddWithValue("@DataOd", DateTime.Today.AddDays(-dniWstecz).ToString("yyyy-MM-dd"));
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int klasa = I(rd, "Klasa");
                int liczba = I(rd, "LiczbaWazen");
                decimal kg = DN(rd, "SumaKg") ?? 0m;
                lista.Add(new HodowcaKlasaWagowa
                {
                    Klasa = klasa,
                    LiczbaWazen = liczba,
                    SumaKg = kg,
                    SrWagaPalety = liczba > 0 ? kg / liczba : 0m
                });
            }
            decimal suma = lista.Sum(k => k.SumaKg);
            if (suma > 0)
                foreach (var k in lista) k.ProcentUdzialu = k.SumaKg / suma * 100m;
            return lista;
        }

        // ─── RANKING (pozycja vs średnia zakładu) ─────────────────────────────

        public async Task<HodowcaRanking> LoadRankingAsync(string customerID, int dniWstecz = 90)
        {
            // NettoWeight z FarmerCalc + WydanoKg z Out1A → wydajność liczona po stronie SQL.
            // Out1A może nie istnieć — wtedy wydajność = NULL i ranking liczony tylko po wolumenie.
            bool maOut1A = await ObjectExistsAsync("dbo.Out1A");

            string sql = $@"
                WITH PerPartia AS (
                    SELECT pd.CustomerID,
                           ISNULL(fc.NettoWeight, 0) AS NettoKg,
                           {(maOut1A ? "ISNULL(outE.WydanoKg, 0)" : "CAST(0 AS decimal(18,2))")} AS WydanoKg
                    FROM dbo.listapartii lp
                    INNER JOIN dbo.PartiaDostawca pd ON pd.Partia = lp.Partia
                    LEFT JOIN (
                        SELECT Partia, NettoWeight,
                               ROW_NUMBER() OVER (PARTITION BY Partia ORDER BY ID DESC) AS rn
                        FROM dbo.FarmerCalc WHERE ISNULL(Deleted, 0) = 0
                    ) fc ON fc.Partia = lp.Partia AND fc.rn = 1
                    {(maOut1A ? @"
                    LEFT JOIN (
                        SELECT P1 AS Partia, SUM(ActWeight) AS WydanoKg
                        FROM dbo.Out1A WHERE ActWeight IS NOT NULL
                        GROUP BY P1
                    ) outE ON outE.Partia = lp.Partia" : "")}
                    WHERE lp.CreateData >= @DataOd
                      AND pd.CustomerID IS NOT NULL AND pd.CustomerID <> ''
                )
                SELECT CustomerID,
                       SUM(NettoKg) AS SumaKg,
                       SUM(WydanoKg) AS SumaWydanoKg,
                       CASE WHEN SUM(NettoKg) > 0
                            THEN SUM(WydanoKg) * 100.0 / SUM(NettoKg)
                            ELSE NULL END AS SrWydajnosc
                FROM PerPartia
                GROUP BY CustomerID
                HAVING SUM(NettoKg) > 0;";

            var dict = new Dictionary<string, (decimal? wydaj, decimal kg)>();
            using (var conn = new SqlConnection(_connLibra))
            {
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn) { CommandTimeout = Timeout };
                cmd.Parameters.AddWithValue("@DataOd", DateTime.Today.AddDays(-dniWstecz).ToString("yyyy-MM-dd"));
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string id = R(rd, "CustomerID");
                    decimal? w = DN(rd, "SrWydajnosc");
                    decimal kg = DN(rd, "SumaKg") ?? 0m;
                    if (!string.IsNullOrEmpty(id)) dict[id] = (w, kg);
                }
            }

            var ranking = new HodowcaRanking { LiczbaHodowcow = dict.Count };
            if (!dict.ContainsKey(customerID) || dict.Count == 0) return ranking;

            var (mojaW, mojaKg) = dict[customerID];
            ranking.MojaWydajnosc = mojaW ?? 0m;
            ranking.MojaSumaKg = mojaKg;

            var wszystkieW = dict.Values.Where(v => v.wydaj.HasValue).Select(v => v.wydaj!.Value).OrderByDescending(v => v).ToList();
            var wszystkieKg = dict.Values.Select(v => v.kg).OrderByDescending(v => v).ToList();

            if (wszystkieW.Count > 0)
            {
                ranking.SredniaZakladu = wszystkieW.Average();
                ranking.MedianaZakladu = wszystkieW[wszystkieW.Count / 2];
                int top10Cnt = Math.Max(1, wszystkieW.Count / 10);
                ranking.Top10Wydajnosc = wszystkieW.Take(top10Cnt).Average();
                ranking.Pozycja = wszystkieW.IndexOf(ranking.MojaWydajnosc) + 1;
                ranking.RoznicaDoSredniej = ranking.MojaWydajnosc - ranking.SredniaZakladu;
            }
            ranking.RankingKg = wszystkieKg.IndexOf(mojaKg) + 1;
            decimal sumaCalkowita = wszystkieKg.Sum();
            ranking.RynekUdzial = sumaCalkowita > 0 ? mojaKg / sumaCalkowita * 100m : 0m;

            // Ocena tekstowa
            if (ranking.LiczbaHodowcow > 0 && ranking.Pozycja > 0)
            {
                double percentyl = (double)ranking.Pozycja / ranking.LiczbaHodowcow * 100.0;
                if (percentyl <= 5) ranking.OcenaTextowa = "🏆 Top 5% — elita zakładu";
                else if (percentyl <= 10) ranking.OcenaTextowa = "🥇 Top 10%";
                else if (percentyl <= 25) ranking.OcenaTextowa = "🥈 Top 25%";
                else if (percentyl <= 50) ranking.OcenaTextowa = "✓ Powyżej mediany";
                else if (percentyl <= 75) ranking.OcenaTextowa = "Poniżej mediany";
                else ranking.OcenaTextowa = "⚠ Dolne 25% — wymaga uwagi";
            }

            return ranking;
        }

        // ─── AGREGACJA OKRESÓW (klient-side, OkresHelper) ─────────────────────

        public static List<HodowcaOkresAgregowany> AgregujOkresami(
            List<HodowcaPartia> partie, AnalitykaPelna.Models.OkresAgregacji okres)
        {
            if (partie == null || partie.Count == 0) return new List<HodowcaOkresAgregowany>();
            var pl = new System.Globalization.CultureInfo("pl-PL");

            var grupy = partie.GroupBy(p => AnalitykaPelna.Models.OkresHelper.DlaDaty(p.CreateData, okres).Klucz);
            var wynik = new List<HodowcaOkresAgregowany>();
            foreach (var g in grupy)
            {
                var pierwszy = g.First();
                var (klucz, etykieta, od, doData) = AnalitykaPelna.Models.OkresHelper.DlaDaty(pierwszy.CreateData, okres);
                string krotka = okres switch
                {
                    AnalitykaPelna.Models.OkresAgregacji.Dzienna => od.ToString("dd.MM"),
                    AnalitykaPelna.Models.OkresAgregacji.Tygodniowa => $"T{System.Globalization.ISOWeek.GetWeekOfYear(od):00}",
                    AnalitykaPelna.Models.OkresAgregacji.Miesieczna => od.ToString("MMM yy", pl),
                    AnalitykaPelna.Models.OkresAgregacji.Kwartalna => $"Q{(od.Month - 1) / 3 + 1} {od:yy}",
                    AnalitykaPelna.Models.OkresAgregacji.Roczna => od.Year.ToString(),
                    _ => etykieta
                };

                var lista = g.ToList();
                wynik.Add(new HodowcaOkresAgregowany
                {
                    Klucz = klucz,
                    Etykieta = etykieta,
                    EtykietaKrotka = krotka,
                    DataOd = od,
                    DataDo = doData,
                    LiczbaPartii = lista.Count,
                    SumaKg = lista.Sum(p => p.NettoSkup ?? 0m),
                    SumaSztuk = lista.Sum(p => p.SztDekl ?? 0),
                    SrWydajnosc = lista.Where(p => p.WydajnoscProc.HasValue).Select(p => p.WydajnoscProc!.Value).DefaultIfEmpty(0m).Average(),
                    SrKlasaB = lista.Where(p => p.KlasaBProc.HasValue).Select(p => p.KlasaBProc!.Value).DefaultIfEmpty(0m).Average(),
                    SrCena = lista.Where(p => p.CenaSkup.HasValue).Select(p => p.CenaSkup!.Value).DefaultIfEmpty(0m).Average(),
                    SumaWartosc = lista.Sum(p => (p.NettoSkup ?? 0m) * (p.CenaSkup ?? 0m)),
                    SumaPadle = lista.Sum(p => p.Padle ?? 0m),
                    SrTempRampa = lista.Where(p => p.TempRampa.HasValue).Select(p => p.TempRampa!.Value).DefaultIfEmpty(0m).Average()
                });
            }
            return wynik.OrderBy(o => o.DataOd).ToList();
        }

        // ─── ANOMALIE (najlepsze/najgorsze partie) ────────────────────────────

        public static List<HodowcaAnomalia> WykryjAnomalie(List<HodowcaPartia> partie)
        {
            var lista = new List<HodowcaAnomalia>();
            if (partie == null || partie.Count == 0) return lista;
            var ostatnieRok = partie.Where(p => p.CreateData >= DateTime.Today.AddDays(-365)).ToList();
            if (ostatnieRok.Count == 0) ostatnieRok = partie;

            var najlepsza = ostatnieRok.Where(p => p.WydajnoscProc.HasValue).OrderByDescending(p => p.WydajnoscProc).FirstOrDefault();
            var najgorsza = ostatnieRok.Where(p => p.WydajnoscProc.HasValue).OrderBy(p => p.WydajnoscProc).FirstOrDefault();
            var najwiekszaKg = ostatnieRok.OrderByDescending(p => p.NettoSkup ?? 0m).FirstOrDefault();
            var najwiekszePadle = ostatnieRok.Where(p => p.Padle.HasValue && p.Padle.Value > 0).OrderByDescending(p => p.Padle).FirstOrDefault();
            var najwyzszaCena = ostatnieRok.Where(p => p.CenaSkup.HasValue && p.CenaSkup > 0).OrderByDescending(p => p.CenaSkup).FirstOrDefault();
            var najnizszaTemp = ostatnieRok.Where(p => p.TempRampa.HasValue).OrderBy(p => p.TempRampa).FirstOrDefault();

            if (najlepsza != null) lista.Add(new HodowcaAnomalia { Typ = "🏆 Najwyższa wydajność", Partia = najlepsza.Partia, Data = najlepsza.CreateData, Wartosc = najlepsza.WydajnoscProc!.Value, Jednostka = "%" });
            if (najgorsza != null && najgorsza.Partia != najlepsza?.Partia) lista.Add(new HodowcaAnomalia { Typ = "⚠ Najniższa wydajność", Partia = najgorsza.Partia, Data = najgorsza.CreateData, Wartosc = najgorsza.WydajnoscProc!.Value, Jednostka = "%" });
            if (najwiekszaKg != null && (najwiekszaKg.NettoSkup ?? 0m) > 0) lista.Add(new HodowcaAnomalia { Typ = "📦 Największa partia", Partia = najwiekszaKg.Partia, Data = najwiekszaKg.CreateData, Wartosc = najwiekszaKg.NettoSkup!.Value, Jednostka = "kg" });
            if (najwiekszePadle != null) lista.Add(new HodowcaAnomalia { Typ = "💀 Najwięcej padłych", Partia = najwiekszePadle.Partia, Data = najwiekszePadle.CreateData, Wartosc = najwiekszePadle.Padle!.Value, Jednostka = "szt." });
            if (najwyzszaCena != null) lista.Add(new HodowcaAnomalia { Typ = "💎 Najwyższa cena", Partia = najwyzszaCena.Partia, Data = najwyzszaCena.CreateData, Wartosc = najwyzszaCena.CenaSkup!.Value, Jednostka = "zł/kg" });
            if (najnizszaTemp != null) lista.Add(new HodowcaAnomalia { Typ = "❄ Najniższa temp. rampy", Partia = najnizszaTemp.Partia, Data = najnizszaTemp.CreateData, Wartosc = najnizszaTemp.TempRampa!.Value, Jednostka = "°C" });
            return lista;
        }

        // ─── ZBIORCZE ─────────────────────────────────────────────────────────

        /// <summary>
        /// Załaduj wszystko, defensywnie: każde źródło w osobnym try/catch.
        /// Błąd jednego źródła nie blokuje całej karty — błędy zbierane są do listy Bledy.
        /// </summary>
        public async Task<HodowcaKartaDane?> LoadAllAsync(string customerID)
        {
            // Profil jest WYMAGANY — bez niego nie ma karty
            HodowcaProfil? profil;
            try { profil = await LoadProfilAsync(customerID); }
            catch (Exception ex)
            {
                throw new Exception($"Nie udało się pobrać profilu z dbo.Dostawcy: {ex.Message}", ex);
            }
            if (profil == null) return null;

            var dane = new HodowcaKartaDane { Profil = profil };

            // Partie — opcjonalne (gdy brak FarmerCalc/Out1A → pusto, ale karta się otworzy)
            try { dane.Partie = await LoadHistoriaPartiiAsync(customerID); }
            catch (Exception ex) { dane.Bledy.Add($"📦 Partie: {Skroc(ex.Message)}"); }

            // Statystyki — z partii (in-memory, nie może wysypać)
            try
            {
                dane.Stat90Dni = BudujStatystyki(dane.Partie, oknoOstatnichDni: 90);
                dane.StatCaleZycie = BudujStatystyki(dane.Partie, oknoOstatnichDni: null);
            }
            catch (Exception ex) { dane.Bledy.Add($"📊 Statystyki: {Skroc(ex.Message)}"); }

            // Harmonogram
            try { dane.Harmonogram = await LoadHarmonogramAsync(profil.Name); }
            catch (Exception ex) { dane.Bledy.Add($"📅 Harmonogram: {Skroc(ex.Message)}"); }

            // Fermy
            try { dane.Fermy = await LoadFermyAsync(profil.GID); }
            catch (Exception ex) { dane.Bledy.Add($"🏠 Fermy: {Skroc(ex.Message)}"); }

            // Klasy wagowe
            try { dane.Klasy = await LoadKlasyWagoweAsync(customerID); }
            catch (Exception ex) { dane.Bledy.Add($"⚖️ Klasy wagowe: {Skroc(ex.Message)}"); }

            // Ranking
            try { dane.Ranking = await LoadRankingAsync(customerID); }
            catch (Exception ex) { dane.Bledy.Add($"🏆 Ranking: {Skroc(ex.Message)}"); }

            // Anomalie + Trend (in-memory)
            try { dane.Anomalie = WykryjAnomalie(dane.Partie); }
            catch (Exception ex) { dane.Bledy.Add($"⚠️ Anomalie: {Skroc(ex.Message)}"); }

            try
            {
                dane.Trend = dane.Partie
                    .OrderBy(p => p.CreateData)
                    .Select(p => new HodowcaTrendPunkt
                    {
                        Data = p.CreateData,
                        Partia = p.Partia,
                        WydajnoscProc = p.WydajnoscProc,
                        KlasaBProc = p.KlasaBProc,
                        NettoSkup = p.NettoSkup ?? 0m
                    })
                    .ToList();
            }
            catch (Exception ex) { dane.Bledy.Add($"📈 Trend: {Skroc(ex.Message)}"); }

            return dane;
        }

        private static string Skroc(string s) => s.Length > 200 ? s.Substring(0, 200) + "..." : s;

        // ─── helpers (safe readers) ───────────────────────────────────────────

        private static string R(SqlDataReader rd, string col)
        {
            int idx = rd.GetOrdinal(col);
            return rd.IsDBNull(idx) ? "" : Convert.ToString(rd.GetValue(idx)) ?? "";
        }

        private static int I(SqlDataReader rd, string col)
        {
            int idx = rd.GetOrdinal(col);
            if (rd.IsDBNull(idx)) return 0;
            var v = rd.GetValue(idx);
            return v is int i ? i : Convert.ToInt32(v);
        }

        private static int? IN(SqlDataReader rd, string col)
        {
            int idx = rd.GetOrdinal(col);
            if (rd.IsDBNull(idx)) return null;
            var v = rd.GetValue(idx);
            if (v is int i) return i;
            return Convert.ToInt32(v);
        }

        private static decimal? DN(SqlDataReader rd, string col)
        {
            int idx = rd.GetOrdinal(col);
            if (rd.IsDBNull(idx)) return null;
            var v = rd.GetValue(idx);
            return v is decimal d ? d : Convert.ToDecimal(v);
        }

        private static DateTime T(SqlDataReader rd, string col)
        {
            int idx = rd.GetOrdinal(col);
            return rd.IsDBNull(idx) ? DateTime.MinValue : Convert.ToDateTime(rd.GetValue(idx));
        }

        private static DateTime? TN(SqlDataReader rd, string col)
        {
            int idx = rd.GetOrdinal(col);
            return rd.IsDBNull(idx) ? null : Convert.ToDateTime(rd.GetValue(idx));
        }

        private static bool B(SqlDataReader rd, string col)
        {
            int idx = rd.GetOrdinal(col);
            if (rd.IsDBNull(idx)) return false;
            var v = rd.GetValue(idx);
            return v switch
            {
                bool b => b,
                int i => i != 0,
                short s => s != 0,
                byte by => by != 0,
                string str => str == "1" || string.Equals(str, "true", StringComparison.OrdinalIgnoreCase),
                _ => Convert.ToInt32(v) != 0
            };
        }
    }
}
