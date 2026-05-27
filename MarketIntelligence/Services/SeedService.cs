using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.MarketIntelligence.Services.AI;

namespace Kalendarz1.MarketIntelligence.Services
{
    /// <summary>
    /// Jednorazowy seed tabeli intel_Entities — JEŚLI jest pusta.
    /// Źródło prawdy: EntityKnowledgeBase (bogaty hardcoded słownik konkurentów/inwestorów/
    /// regulatorów) + dodatkowe encje (klienci sieciowi, dostawcy, regiony, towary) z briefu.
    /// Dedup po Name (case-insensitive) — KB i lista dodatkowa się nie dublują.
    /// </summary>
    public class SeedService
    {
        private readonly string _connectionString;

        public SeedService(string connectionString = null)
        {
            _connectionString = connectionString ?? MarketIntelligenceConfig.LibraNetConnectionString;
        }

        public async Task SeedEntitiesIfEmptyAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Tabela istnieje? (DatabaseSetup tworzy ją wcześniej; defensywnie sprawdź)
                using (var check = new SqlCommand(
                    "SELECT COUNT(*) FROM sys.tables WHERE name='intel_Entities'", conn))
                {
                    if ((int)(await check.ExecuteScalarAsync() ?? 0) == 0) return;
                }

                using (var cnt = new SqlCommand("SELECT COUNT(*) FROM intel_Entities", conn))
                {
                    if ((int)(await cnt.ExecuteScalarAsync() ?? 0) > 0)
                    {
                        Debug.WriteLine("[Seed] intel_Entities już wypełnione — pomijam seed.");
                        return;
                    }
                }

                var entities = BuildSeedList();
                int inserted = 0;
                foreach (var e in entities)
                {
                    using var cmd = new SqlCommand(@"
INSERT INTO intel_Entities (Name, EntityType, Aliases, IsTracked, Notes)
VALUES (@n, @t, @a, 1, @notes)", conn);
                    cmd.Parameters.AddWithValue("@n", e.Name);
                    cmd.Parameters.AddWithValue("@t", e.Type);
                    cmd.Parameters.AddWithValue("@a", (object)e.Aliases ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@notes", (object)e.Notes ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                    inserted++;
                }
                Debug.WriteLine($"[Seed] ✓ intel_Entities zaseedowane: {inserted} encji.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Seed] error: {ex.Message}");
            }
        }

        private static List<(string Name, string Type, string Aliases, string Notes)> BuildSeedList()
        {
            var result = new List<(string, string, string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string name, string type, string aliases, string notes = null)
            {
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) return;
                result.Add((name, type, aliases, notes));
            }

            // 1) Z EntityKnowledgeBase (źródło prawdy)
            foreach (var kv in EntityKnowledgeBase.Entities)
            {
                var info = kv.Value;
                var name = string.IsNullOrWhiteSpace(info.Name) ? kv.Key : info.Name;
                var aliases = (info.Aliases != null && info.Aliases.Length > 0)
                    ? string.Join(";", new[] { kv.Key }.Concat(info.Aliases).Distinct())
                    : kv.Key;
                Add(name, MapType(info.Type), aliases, info.Category);
            }

            // 2) Dodatkowe encje z briefu (klienci/dostawcy/regiony/towary), jeśli nie ma w KB
            // Klienci sieciowi
            Add("Biedronka", "customer", "Biedronka;Jeronimo Martins");
            Add("Lidl", "customer", "Lidl;Lidl Polska");
            Add("Kaufland", "customer", "Kaufland");
            Add("Auchan", "customer", "Auchan");
            Add("Carrefour", "customer", "Carrefour");
            Add("Dino", "customer", "Dino;Dino Polska");
            Add("Makro", "customer", "Makro;Makro Cash and Carry");
            Add("Selgros", "customer", "Selgros");
            // Dostawcy / pasze
            Add("JDA", "supplier", "JDA");
            Add("Stróżewski", "supplier", "Stróżewski;Stróżewski Boguszyce");
            Add("Esencja", "supplier", "Esencja");
            Add("Cargill", "supplier", "Cargill");
            Add("ADM", "supplier", "ADM;Archer Daniels Midland");
            // Regulatorzy (jeśli nie z KB)
            Add("ARiMR", "regulator", "ARiMR;Agencja Restrukturyzacji i Modernizacji Rolnictwa");
            Add("KOWR", "regulator", "KOWR;Krajowy Ośrodek Wsparcia Rolnictwa");
            Add("MRiRW", "regulator", "MRiRW;Ministerstwo Rolnictwa;Ministerstwo Rolnictwa i Rozwoju Wsi");
            Add("GIW", "regulator", "GIW;Główny Inspektorat Weterynarii;Inspekcja Weterynaryjna;GLW;Główny Lekarz Weterynarii");
            Add("PIORiN", "regulator", "PIORiN");
            // Regiony (HPAI)
            Add("Łódzkie", "region", "łódzkie;Łódzkie;woj. łódzkie");
            Add("Wielkopolska", "region", "Wielkopolska;wielkopolskie");
            Add("Mazowsze", "region", "Mazowsze;mazowieckie");
            // Towary
            Add("Pszenica", "commodity", "pszenica;wheat");
            Add("Soja", "commodity", "soja;soybean;śruta sojowa");
            Add("Kukurydza", "commodity", "kukurydza;corn;maize");

            return result;
        }

        private static string MapType(EntityType t) => t switch
        {
            EntityType.Competitor => "competitor",
            EntityType.Investor => "competitor",     // inwestorzy (ADQ/LDC) = krajobraz konkurencyjny
            EntityType.Importer => "competitor",     // import = konkurencja zagraniczna
            EntityType.Customer => "customer",
            EntityType.Government => "regulator",
            EntityType.Organization => "regulator",
            EntityType.Person => "person",
            EntityType.Regulation => "other",
            _ => "other"
        };
    }
}
