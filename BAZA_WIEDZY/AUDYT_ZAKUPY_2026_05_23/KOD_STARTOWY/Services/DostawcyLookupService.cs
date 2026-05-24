// ════════════════════════════════════════════════════════════════════════════
// DostawcyLookupService.cs — lookup hodowców z DOSTAWCY (do ComboBox w edytorze)
// Część 4 audytu (2026-05-23)
// Target: Kontrakty/Services/DostawcyLookupService.cs
//
// UWAGA: tabela DOSTAWCY w LibraNet ma kolumny zależne od historycznej struktury.
// Poniższe nazwy kolumn (Name, NIP, Adres, NrGospodarstwa) MOGĄ wymagać korekty
// po sprawdzeniu rzeczywistego schematu: SELECT TOP 1 * FROM dbo.DOSTAWCY;
// ════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Kontrakty.Services
{
    public class DostawcaLookup
    {
        public int Id { get; set; }
        public string Nazwa { get; set; } = "";
        public string? Nip { get; set; }
        public string? Adres { get; set; }
        public string? NrGospodarstwa { get; set; }

        // Wyświetlanie w ComboBox
        public override string ToString() => $"{Nazwa} (#{Id})";
    }

    public class DostawcyLookupService
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        /// <summary>
        /// Lista aktywnych hodowców do wyboru w edytorze kontraktu.
        /// SPRAWDŹ nazwy kolumn po: SELECT TOP 1 * FROM dbo.DOSTAWCY;
        /// </summary>
        public async Task<List<DostawcaLookup>> GetAllAsync()
        {
            // Defensywnie: ISNULL na opcjonalne kolumny. Jeśli kolumna nie istnieje,
            // złap SqlException 207 i użyj prostszej kwerendy (jak w Menu1.GetUserLoginInfo).
            const string sql = @"
SELECT ID,
       ISNULL(Name, CAST(ID AS NVARCHAR(20))) AS Nazwa,
       NULL AS Nip,           -- TODO: podmień na realną kolumnę NIP gdy istnieje
       NULL AS Adres,         -- TODO: podmień na realną kolumnę adresu
       NULL AS NrGospodarstwa -- TODO: podmień na realną kolumnę nr gospodarstwa
FROM dbo.DOSTAWCY
ORDER BY Name;";

            var list = new List<DostawcaLookup>();
            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new DostawcaLookup
                {
                    Id = (int)rdr["ID"],
                    Nazwa = rdr["Nazwa"] as string ?? "",
                    Nip = rdr["Nip"] as string,
                    Adres = rdr["Adres"] as string,
                    NrGospodarstwa = rdr["NrGospodarstwa"] as string
                });
            }
            return list;
        }

        /// <summary>
        /// Pobiera snapshot danych jednego hodowcy (do zapisania w kontrakcie).
        /// </summary>
        public async Task<DostawcaLookup?> GetByIdAsync(int id)
        {
            const string sql = @"
SELECT ID, ISNULL(Name, CAST(ID AS NVARCHAR(20))) AS Nazwa,
       NULL AS Nip, NULL AS Adres, NULL AS NrGospodarstwa
FROM dbo.DOSTAWCY WHERE ID = @Id;";

            using var conn = new SqlConnection(ConnLibra);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new DostawcaLookup
                {
                    Id = (int)rdr["ID"],
                    Nazwa = rdr["Nazwa"] as string ?? "",
                    Nip = rdr["Nip"] as string,
                    Adres = rdr["Adres"] as string,
                    NrGospodarstwa = rdr["NrGospodarstwa"] as string
                };
            }
            return null;
        }
    }
}
