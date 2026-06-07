using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zamowienia.Services
{
    /// <summary>
    /// Obsługa "puli bilansu": zamówienia/wydania towarów-dzieci (np. Noga, Pałka, Udziec)
    /// są doliczane do puli towaru-rodzica (np. Ćwiartka) na kartach "Podsumowanie dnia"
    /// w oknie Zamówienia Klientów. Tabela: LibraNet.dbo.BilansSkladniki.
    /// </summary>
    public class BilansSkladnikiService
    {
        private readonly string _connLibra;

        public BilansSkladnikiService(string connLibra)
        {
            _connLibra = connLibra;
        }

        /// <summary>Tworzy tabelę jeśli nie istnieje (zgodnie z CreateBilansSkladniki.sql).</summary>
        public async Task EnsureTableAsync()
        {
            const string sql = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BilansSkladniki')
BEGIN
    CREATE TABLE dbo.BilansSkladniki (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        ParentTowarId    INT            NOT NULL,
        ParentNazwa      NVARCHAR(200)  NULL,
        ChildTowarId     INT            NOT NULL,
        ChildNazwa       NVARCHAR(200)  NULL,
        Aktywny          BIT            NOT NULL CONSTRAINT DF_BilansSkladniki_Aktywny DEFAULT 1,
        DataModyfikacji  DATETIME       NOT NULL CONSTRAINT DF_BilansSkladniki_Data DEFAULT GETDATE(),
        ModyfikowalPrzez NVARCHAR(100)  NULL
    );
    CREATE UNIQUE INDEX UX_BilansSkladniki_Pair ON dbo.BilansSkladniki(ParentTowarId, ChildTowarId);
END";
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Mapowanie rodzic → lista aktywnych id dzieci. Używane przez Podsumowanie dnia.
        /// Nigdy nie rzuca — przy błędzie zwraca pusty słownik (karta działa po staremu).
        /// </summary>
        public async Task<Dictionary<int, List<int>>> GetMapowanieAsync()
        {
            var map = new Dictionary<int, List<int>>();
            try
            {
                await EnsureTableAsync();
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                const string sql = "SELECT ParentTowarId, ChildTowarId FROM dbo.BilansSkladniki WHERE Aktywny = 1";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int parent = rdr.GetInt32(0);
                    int child = rdr.GetInt32(1);
                    if (!map.TryGetValue(parent, out var list))
                    {
                        list = new List<int>();
                        map[parent] = list;
                    }
                    if (!list.Contains(child))
                        list.Add(child);
                }
            }
            catch { }
            return map;
        }

        /// <summary>Wszystkie pule (pogrupowane po rodzicu) dla widoku kreatora.</summary>
        public async Task<List<PulaModel>> GetWszystkieAsync()
        {
            var rezultat = new Dictionary<int, PulaModel>();
            await EnsureTableAsync();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            const string sql = @"SELECT ParentTowarId, ParentNazwa, ChildTowarId, ChildNazwa
                                 FROM dbo.BilansSkladniki WHERE Aktywny = 1
                                 ORDER BY ParentNazwa, ChildNazwa";
            await using var cmd = new SqlCommand(sql, cn);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                int parent = rdr.GetInt32(0);
                if (!rezultat.TryGetValue(parent, out var pula))
                {
                    pula = new PulaModel
                    {
                        ParentTowarId = parent,
                        ParentNazwa = rdr.IsDBNull(1) ? "" : rdr.GetString(1)
                    };
                    rezultat[parent] = pula;
                }
                pula.Skladniki.Add(new SkladnikModel
                {
                    TowarID = rdr.GetInt32(2),
                    Nazwa = rdr.IsDBNull(3) ? "" : rdr.GetString(3)
                });
            }
            return rezultat.Values.OrderBy(p => p.ParentNazwa).ToList();
        }

        /// <summary>
        /// Zapisuje pulę — zastępuje WSZYSTKIE składniki danego rodzica
        /// (usuwa stare, wstawia podane). Pusta lista = usunięcie puli.
        /// </summary>
        public async Task SaveParentAsync(int parentId, string parentNazwa,
            IEnumerable<SkladnikModel> skladniki, string user)
        {
            await EnsureTableAsync();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var trans = (SqlTransaction)await cn.BeginTransactionAsync();
            try
            {
                await using (var del = new SqlCommand(
                    "DELETE FROM dbo.BilansSkladniki WHERE ParentTowarId = @p", cn, trans))
                {
                    del.Parameters.AddWithValue("@p", parentId);
                    await del.ExecuteNonQueryAsync();
                }

                foreach (var s in skladniki)
                {
                    await using var ins = new SqlCommand(@"
                        INSERT INTO dbo.BilansSkladniki
                            (ParentTowarId, ParentNazwa, ChildTowarId, ChildNazwa, Aktywny, DataModyfikacji, ModyfikowalPrzez)
                        VALUES (@p, @pn, @c, @cn2, 1, GETDATE(), @u)", cn, trans);
                    ins.Parameters.AddWithValue("@p", parentId);
                    ins.Parameters.AddWithValue("@pn", (object)parentNazwa ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@c", s.TowarID);
                    ins.Parameters.AddWithValue("@cn2", (object)s.Nazwa ?? DBNull.Value);
                    ins.Parameters.AddWithValue("@u", (object)user ?? DBNull.Value);
                    await ins.ExecuteNonQueryAsync();
                }

                await trans.CommitAsync();
            }
            catch
            {
                await trans.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteParentAsync(int parentId)
        {
            await EnsureTableAsync();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "DELETE FROM dbo.BilansSkladniki WHERE ParentTowarId = @p", cn);
            cmd.Parameters.AddWithValue("@p", parentId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public class PulaModel
    {
        public int ParentTowarId { get; set; }
        public string ParentNazwa { get; set; } = "";
        public List<SkladnikModel> Skladniki { get; set; } = new();
        public int LiczbaSkladnikow => Skladniki.Count;
        public string SkladnikiText => string.Join(", ", Skladniki.Select(s => s.Nazwa));
    }

    public class SkladnikModel
    {
        public int TowarID { get; set; }
        public string Kod { get; set; } = "";
        public string Nazwa { get; set; } = "";
    }
}
