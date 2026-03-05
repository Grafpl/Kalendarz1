using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.KartotekaTowarow
{
    public class ArticleModel
    {
        public string GUID { get; set; } = "";
        public string ID { get; set; } = "";
        public string? ShortName { get; set; }
        public string? Name { get; set; }
        public int? Grupa { get; set; }
        public int? Grupa1 { get; set; }
        public double? Cena1 { get; set; }
        public double? Cena2 { get; set; }
        public double? Cena3 { get; set; }
        public int? Rodzaj { get; set; }
        public string? JM { get; set; }
        public double? WRC { get; set; }
        public decimal? Wydajnosc { get; set; }
        public string? Ingredients1 { get; set; }
        public string? Ingredients2 { get; set; }
        public string? Ingredients3 { get; set; }
        public string? Ingredients4 { get; set; }
        public string? Ingredients5 { get; set; }
        public string? Ingredients6 { get; set; }
        public string? Ingredients7 { get; set; }
        public string? Ingredients8 { get; set; }
        public int? Duration { get; set; }
        public string? TempOfStorage { get; set; }
        public short? Halt { get; set; }
        public double? Przelicznik { get; set; }
        public string? CreateData { get; set; }
        public string? CreateGodzina { get; set; }
        public string? ModificationData { get; set; }
        public string? ModificationGodzina { get; set; }
        public string? RELATED_ID1 { get; set; }
        public string? RELATED_ID2 { get; set; }
        public string? RELATED_ID3 { get; set; }
        public short? isStandard { get; set; }
        public decimal? StandardWeight { get; set; }
        public decimal? StandardTol { get; set; }
        public decimal? StandardTolMinus { get; set; }
        public string? NameLine1 { get; set; }
        public string? NameLine2 { get; set; }
    }

    public class PartitionInfo
    {
        public int Zestaw { get; set; }
        public string? GrupaName { get; set; }
        public int Position { get; set; }
        public string ID { get; set; } = "";
        public string? Name { get; set; }
    }

    public class PhotoInfo
    {
        public int Id { get; set; }
        public byte[] Zdjecie { get; set; } = Array.Empty<byte>();
        public string? NazwaPliku { get; set; }
        public string? TypMIME { get; set; }
        public int? RozmiarKB { get; set; }
        public DateTime? DataDodania { get; set; }
        public bool Aktywne { get; set; } = true;
    }

    public class KonfiguracjaProdukcji
    {
        public int ID { get; set; }
        public int TowarID { get; set; }
        public string? NazwaTowaru { get; set; }
        public decimal ProcentUdzialu { get; set; }
        public bool Aktywny { get; set; }
        public string? GrupaScalowania { get; set; }
    }

    public static class ArticleService
    {
        private static readonly string _connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public static async Task<List<ArticleModel>> GetAllAsync()
        {
            var list = new List<ArticleModel>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT GUID, ID, ShortName, Name, Grupa, Grupa1, Cena1, Cena2, Cena3,
                           Rodzaj, JM, WRC, Wydajnosc, Ingredients1, Ingredients2, Ingredients3,
                           Ingredients4, Ingredients5, Ingredients6, Ingredients7, Ingredients8,
                           Duration, TempOfStorage, Halt, Przelicznik,
                           CreateData, CreateGodzina, ModificationData, ModificationGodzina,
                           RELATED_ID1, RELATED_ID2, RELATED_ID3,
                           isStandard, StandardWeight, StandardTol, StandardTolMinus,
                           NameLine1, NameLine2
                    FROM Article
                    ORDER BY Name", conn);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(MapArticle(reader));
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad ladowania artykulow: {ex.Message}", ex);
            }
            return list;
        }

        public static async Task<ArticleModel?> GetByGuidAsync(string guid)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT GUID, ID, ShortName, Name, Grupa, Grupa1, Cena1, Cena2, Cena3,
                           Rodzaj, JM, WRC, Wydajnosc, Ingredients1, Ingredients2, Ingredients3,
                           Ingredients4, Ingredients5, Ingredients6, Ingredients7, Ingredients8,
                           Duration, TempOfStorage, Halt, Przelicznik,
                           CreateData, CreateGodzina, ModificationData, ModificationGodzina,
                           RELATED_ID1, RELATED_ID2, RELATED_ID3,
                           isStandard, StandardWeight, StandardTol, StandardTolMinus,
                           NameLine1, NameLine2
                    FROM Article WHERE GUID = @GUID", conn);
                cmd.Parameters.AddWithValue("@GUID", guid);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return MapArticle(reader);
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad ladowania artykulu: {ex.Message}", ex);
            }
            return null;
        }

        public static async Task InsertAsync(ArticleModel a)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    INSERT INTO Article (GUID, ID, ShortName, Name, Grupa, Grupa1, Cena1, Cena2, Cena3,
                        Rodzaj, JM, WRC, Wydajnosc, Ingredients1, Ingredients2, Ingredients3, Ingredients4,
                        Ingredients5, Ingredients6, Ingredients7, Ingredients8, Duration, TempOfStorage,
                        Halt, Przelicznik, CreateData, CreateGodzina, ModificationData, ModificationGodzina,
                        RELATED_ID1, RELATED_ID2, RELATED_ID3, isStandard, StandardWeight, StandardTol,
                        StandardTolMinus, NameLine1, NameLine2)
                    VALUES (@GUID, @ID, @ShortName, @Name, @Grupa, @Grupa1, @Cena1, @Cena2, @Cena3,
                        @Rodzaj, @JM, @WRC, @Wydajnosc, @Ing1, @Ing2, @Ing3, @Ing4,
                        @Ing5, @Ing6, @Ing7, @Ing8, @Duration, @TempOfStorage,
                        @Halt, @Przelicznik,
                        CONVERT(varchar(10), GETDATE(), 120), CONVERT(varchar(8), GETDATE(), 108),
                        CONVERT(varchar(10), GETDATE(), 120), CONVERT(varchar(8), GETDATE(), 108),
                        @Related1, @Related2, @Related3, @isStandard, @StdWeight, @StdTol,
                        @StdTolMinus, @NameLine1, @NameLine2)", conn);

                AddArticleParams(cmd, a);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad zapisu artykulu: {ex.Message}", ex);
            }
        }

        public static async Task UpdateAsync(ArticleModel a)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE Article SET
                        ShortName=@ShortName, Name=@Name, Grupa=@Grupa, Grupa1=@Grupa1,
                        Cena1=@Cena1, Cena2=@Cena2, Cena3=@Cena3, Rodzaj=@Rodzaj, JM=@JM,
                        WRC=@WRC, Wydajnosc=@Wydajnosc,
                        Ingredients1=@Ing1, Ingredients2=@Ing2, Ingredients3=@Ing3, Ingredients4=@Ing4,
                        Ingredients5=@Ing5, Ingredients6=@Ing6, Ingredients7=@Ing7, Ingredients8=@Ing8,
                        Duration=@Duration, TempOfStorage=@TempOfStorage, Halt=@Halt, Przelicznik=@Przelicznik,
                        ModificationData=CONVERT(varchar(10), GETDATE(), 120),
                        ModificationGodzina=CONVERT(varchar(8), GETDATE(), 108),
                        RELATED_ID1=@Related1, RELATED_ID2=@Related2, RELATED_ID3=@Related3,
                        isStandard=@isStandard, StandardWeight=@StdWeight,
                        StandardTol=@StdTol, StandardTolMinus=@StdTolMinus,
                        NameLine1=@NameLine1, NameLine2=@NameLine2
                    WHERE GUID=@GUID", conn);

                AddArticleParams(cmd, a);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad aktualizacji artykulu: {ex.Message}", ex);
            }
        }

        public static async Task UpdateFieldAsync(string guid, string fieldName, object? value)
        {
            // Whitelist of allowed inline-editable fields
            var allowed = new HashSet<string> { "Cena1", "Cena2", "Cena3", "Halt" };
            if (!allowed.Contains(fieldName))
                throw new ArgumentException($"Pole {fieldName} nie jest dozwolone do inline edycji.");

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand($@"
                    UPDATE Article SET
                        [{fieldName}]=@Value,
                        ModificationData=CONVERT(varchar(10), GETDATE(), 120),
                        ModificationGodzina=CONVERT(varchar(8), GETDATE(), 108)
                    WHERE GUID=@GUID", conn);
                cmd.Parameters.AddWithValue("@Value", value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@GUID", guid);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad aktualizacji pola {fieldName}: {ex.Message}", ex);
            }
        }

        public static async Task<bool> CheckIdUniqueAsync(string id, string? excludeGuid = null)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                string sql = excludeGuid != null
                    ? "SELECT COUNT(*) FROM Article WHERE ID=@ID AND GUID<>@GUID"
                    : "SELECT COUNT(*) FROM Article WHERE ID=@ID";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@ID", id);
                if (excludeGuid != null)
                    cmd.Parameters.AddWithValue("@GUID", excludeGuid);

                var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                return count == 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad sprawdzania unikalnosci ID: {ex.Message}", ex);
            }
        }

        public static async Task<List<int>> GetDistinctGrupyAsync()
        {
            var list = new List<int>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT DISTINCT Grupa FROM Article WHERE Grupa IS NOT NULL ORDER BY Grupa", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader.GetInt32(0));
            }
            catch { }
            return list;
        }

        public static async Task<List<int>> GetDistinctRodzajeAsync()
        {
            var list = new List<int>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT DISTINCT Rodzaj FROM Article WHERE Rodzaj IS NOT NULL ORDER BY Rodzaj", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader.GetInt32(0));
            }
            catch { }
            return list;
        }

        // === PHOTOS ===

        public static async Task<List<PhotoInfo>> GetPhotosAsync(string articleId)
        {
            var list = new List<PhotoInfo>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Try TowarZdjecia first (by matching Article.ID to a numeric TowarId)
                using var cmd = new SqlCommand(@"
                    SELECT Id, Zdjecie, NazwaPliku, TypMIME, RozmiarKB, DataDodania, Aktywne
                    FROM TowarZdjecia
                    WHERE TowarId = (SELECT TOP 1 CAST(
                        CASE WHEN ISNUMERIC(ID) = 1 THEN ID ELSE NULL END AS int)
                        FROM Article WHERE ID = @ArticleID)
                      AND Aktywne = 1
                    ORDER BY DataDodania DESC", conn);
                cmd.Parameters.AddWithValue("@ArticleID", articleId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PhotoInfo
                    {
                        Id = reader.GetInt32(0),
                        Zdjecie = (byte[])reader["Zdjecie"],
                        NazwaPliku = reader["NazwaPliku"] as string,
                        TypMIME = reader["TypMIME"] as string,
                        RozmiarKB = reader["RozmiarKB"] as int?,
                        DataDodania = reader["DataDodania"] as DateTime?,
                        Aktywne = true
                    });
                }
            }
            catch { }
            return list;
        }

        public static async Task<byte[]?> GetPhotoAsync(string articleId)
        {
            try
            {
                // Try TowarZdjecia first
                var photos = await GetPhotosAsync(articleId);
                if (photos.Count > 0)
                    return photos[0].Zdjecie;

                // Fallback: ArtPartitionD.Img
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 1 Img FROM ArtPartitionD WHERE ID=@ArticleID AND Img IS NOT NULL", conn);
                cmd.Parameters.AddWithValue("@ArticleID", articleId);
                var result = await cmd.ExecuteScalarAsync();
                return result as byte[];
            }
            catch { return null; }
        }

        public static async Task SavePhotoAsync(string articleId, byte[] imageData, string fileName, string mimeType)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Determine TowarId from Article.ID
                int towarId;
                using (var cmdId = new SqlCommand(
                    "SELECT TOP 1 CAST(CASE WHEN ISNUMERIC(ID) = 1 THEN ID ELSE NULL END AS int) FROM Article WHERE ID = @AID", conn))
                {
                    cmdId.Parameters.AddWithValue("@AID", articleId);
                    var result = await cmdId.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value)
                        towarId = Math.Abs(articleId.GetHashCode()) % 100000;
                    else
                        towarId = (int)result;
                }

                using var cmd = new SqlCommand(@"
                    INSERT INTO TowarZdjecia (TowarId, Zdjecie, NazwaPliku, TypMIME, RozmiarKB, DataDodania, DodanyPrzez, Aktywne)
                    VALUES (@TowarId, @Zdjecie, @NazwaPliku, @TypMIME, @RozmiarKB, GETDATE(), @DodanyPrzez, 1)", conn);
                cmd.Parameters.AddWithValue("@TowarId", towarId);
                cmd.Parameters.Add("@Zdjecie", SqlDbType.VarBinary, -1).Value = imageData;
                cmd.Parameters.AddWithValue("@NazwaPliku", fileName);
                cmd.Parameters.AddWithValue("@TypMIME", mimeType);
                cmd.Parameters.AddWithValue("@RozmiarKB", imageData.Length / 1024);
                cmd.Parameters.AddWithValue("@DodanyPrzez", App.UserFullName ?? App.UserID ?? "system");
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad zapisu zdjecia: {ex.Message}", ex);
            }
        }

        public static async Task DeletePhotoAsync(int photoId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("UPDATE TowarZdjecia SET Aktywne=0 WHERE Id=@Id", conn);
                cmd.Parameters.AddWithValue("@Id", photoId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Blad usuwania zdjecia: {ex.Message}", ex);
            }
        }

        // === PARTITIONS ===

        public static async Task<List<PartitionInfo>> GetPartitionsAsync(string articleId)
        {
            var list = new List<PartitionInfo>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT h.Zestaw, h.Name AS GrupaName, d.Position, d.ID, d.Name
                    FROM ArtPartitionD d
                    JOIN ArtPartitionH h ON d.Zestaw=h.Zestaw AND d.GroupID=h.GroupID
                    WHERE d.ID=@ArticleID
                    ORDER BY h.Zestaw, d.Position", conn);
                cmd.Parameters.AddWithValue("@ArticleID", articleId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new PartitionInfo
                    {
                        Zestaw = reader.GetInt32(0),
                        GrupaName = reader["GrupaName"] as string,
                        Position = reader.GetInt32(2),
                        ID = reader.GetString(3),
                        Name = reader["Name"] as string
                    });
                }
            }
            catch { }
            return list;
        }

        // === KONFIGURACJA PRODUKCJI ===

        public static async Task<KonfiguracjaProdukcji?> GetKonfiguracjaAsync(string articleId)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                int towarId;
                using (var cmdId = new SqlCommand(
                    "SELECT TOP 1 CAST(CASE WHEN ISNUMERIC(ID) = 1 THEN ID ELSE NULL END AS int) FROM Article WHERE ID = @AID", conn))
                {
                    cmdId.Parameters.AddWithValue("@AID", articleId);
                    var result = await cmdId.ExecuteScalarAsync();
                    if (result == null || result == DBNull.Value) return null;
                    towarId = (int)result;
                }

                using var cmd = new SqlCommand(@"
                    SELECT ID, TowarID, NazwaTowaru, ProcentUdzialu, Aktywny, GrupaScalowania
                    FROM KonfiguracjaProduktow WHERE TowarID=@TowarID AND Aktywny=1", conn);
                cmd.Parameters.AddWithValue("@TowarID", towarId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new KonfiguracjaProdukcji
                    {
                        ID = reader.GetInt32(0),
                        TowarID = reader.GetInt32(1),
                        NazwaTowaru = reader["NazwaTowaru"] as string,
                        ProcentUdzialu = reader.GetDecimal(3),
                        Aktywny = true,
                        GrupaScalowania = reader["GrupaScalowania"] as string
                    };
                }
            }
            catch { }
            return null;
        }

        public static async Task<List<string>> GetDistinctGrupyScalowaniaAsync()
        {
            var list = new List<string>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(
                    "SELECT DISTINCT GrupaScalowania FROM KonfiguracjaProduktow WHERE GrupaScalowania IS NOT NULL ORDER BY GrupaScalowania", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader.GetString(0));
            }
            catch { }
            return list;
        }

        // === ALL ARTICLES for ComboBox lookups ===

        public static async Task<List<(string ID, string Name)>> GetArticleListAsync()
        {
            var list = new List<(string, string)>();
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand("SELECT ID, Name FROM Article ORDER BY Name", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add((reader.GetString(0), reader["Name"] as string ?? ""));
            }
            catch { }
            return list;
        }

        // === STATS ===

        public static async Task<(int total, int active, int halted)> GetStatsAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(@"
                    SELECT
                        COUNT(*) AS Total,
                        SUM(CASE WHEN ISNULL(Halt,0)=0 THEN 1 ELSE 0 END) AS Active,
                        SUM(CASE WHEN Halt=1 THEN 1 ELSE 0 END) AS Halted
                    FROM Article", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                    return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
            }
            catch { }
            return (0, 0, 0);
        }

        // === HELPERS ===

        private static void AddArticleParams(SqlCommand cmd, ArticleModel a)
        {
            cmd.Parameters.AddWithValue("@GUID", a.GUID);
            cmd.Parameters.AddWithValue("@ID", a.ID);
            cmd.Parameters.AddWithValue("@ShortName", (object?)a.ShortName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", (object?)a.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Grupa", (object?)a.Grupa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Grupa1", (object?)a.Grupa1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cena1", (object?)a.Cena1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cena2", (object?)a.Cena2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cena3", (object?)a.Cena3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Rodzaj", (object?)a.Rodzaj ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@JM", (object?)a.JM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@WRC", (object?)a.WRC ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Wydajnosc", (object?)a.Wydajnosc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing1", (object?)a.Ingredients1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing2", (object?)a.Ingredients2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing3", (object?)a.Ingredients3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing4", (object?)a.Ingredients4 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing5", (object?)a.Ingredients5 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing6", (object?)a.Ingredients6 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing7", (object?)a.Ingredients7 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ing8", (object?)a.Ingredients8 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Duration", (object?)a.Duration ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TempOfStorage", (object?)a.TempOfStorage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Halt", (object?)a.Halt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Przelicznik", (object?)a.Przelicznik ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Related1", (object?)a.RELATED_ID1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Related2", (object?)a.RELATED_ID2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Related3", (object?)a.RELATED_ID3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isStandard", (object?)a.isStandard ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StdWeight", (object?)a.StandardWeight ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StdTol", (object?)a.StandardTol ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@StdTolMinus", (object?)a.StandardTolMinus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NameLine1", (object?)a.NameLine1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@NameLine2", (object?)a.NameLine2 ?? DBNull.Value);
        }

        private static ArticleModel MapArticle(SqlDataReader r)
        {
            return new ArticleModel
            {
                GUID = r["GUID"] as string ?? "",
                ID = r["ID"] as string ?? "",
                ShortName = r["ShortName"] as string,
                Name = r["Name"] as string,
                Grupa = r["Grupa"] as int?,
                Grupa1 = r["Grupa1"] as int?,
                Cena1 = r["Cena1"] as double?,
                Cena2 = r["Cena2"] as double?,
                Cena3 = r["Cena3"] as double?,
                Rodzaj = r["Rodzaj"] as int?,
                JM = r["JM"] as string,
                WRC = r["WRC"] as double?,
                Wydajnosc = r["Wydajnosc"] as decimal?,
                Ingredients1 = r["Ingredients1"] as string,
                Ingredients2 = r["Ingredients2"] as string,
                Ingredients3 = r["Ingredients3"] as string,
                Ingredients4 = r["Ingredients4"] as string,
                Ingredients5 = r["Ingredients5"] as string,
                Ingredients6 = r["Ingredients6"] as string,
                Ingredients7 = r["Ingredients7"] as string,
                Ingredients8 = r["Ingredients8"] as string,
                Duration = r["Duration"] as int?,
                TempOfStorage = r["TempOfStorage"] as string,
                Halt = r["Halt"] as short?,
                Przelicznik = r["Przelicznik"] as double?,
                CreateData = r["CreateData"] as string,
                CreateGodzina = r["CreateGodzina"] as string,
                ModificationData = r["ModificationData"] as string,
                ModificationGodzina = r["ModificationGodzina"] as string,
                RELATED_ID1 = r["RELATED_ID1"] as string,
                RELATED_ID2 = r["RELATED_ID2"] as string,
                RELATED_ID3 = r["RELATED_ID3"] as string,
                isStandard = r["isStandard"] as short?,
                StandardWeight = r["StandardWeight"] as decimal?,
                StandardTol = r["StandardTol"] as decimal?,
                StandardTolMinus = r["StandardTolMinus"] as decimal?,
                NameLine1 = r["NameLine1"] as string,
                NameLine2 = r["NameLine2"] as string
            };
        }
    }
}
