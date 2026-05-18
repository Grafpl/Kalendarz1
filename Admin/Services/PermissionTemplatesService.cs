using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Kalendarz1.Admin.Models;

namespace Kalendarz1.Admin.Services
{
    // CRUD dla custom szablonów uprawnień (kompozytowe stanowiska).
    // Tabela LibraNet.dbo.PermissionTemplates — tworzona automatycznie przy starcie.
    // Wszystkie metody zwracają tuple z error message, żeby UI mogło pokazać user'owi
    // co poszło nie tak (zamiast cichego catch).
    public class PermissionTemplatesService
    {
        private readonly string _connStr = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public async Task<(bool Ok, string? Error)> EnsureTableExistsAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.PermissionTemplates') AND type = 'U')
                BEGIN
                    CREATE TABLE dbo.PermissionTemplates (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        Description NVARCHAR(500),
                        ModuleKeys NVARCHAR(MAX),  -- CSV: 'DaneHodowcy,DokumentyZakupu,...'
                        Icon NVARCHAR(10),
                        Color NVARCHAR(20),
                        CreatedAt DATETIME DEFAULT GETDATE(),
                        CreatedBy NVARCHAR(50)
                    );
                    CREATE NONCLUSTERED INDEX IX_PermissionTemplates_Name ON dbo.PermissionTemplates (Name);
                END";
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.EnsureTable] {ex}");
                return (false, ex.Message);
            }
        }

        public async Task<(List<PermissionTemplate> List, string? Error)> LoadAllAsync()
        {
            var list = new List<PermissionTemplate>();
            try
            {
                const string sql = @"SELECT Id, Name, Description, ModuleKeys, Icon, Color, CreatedAt, CreatedBy
                                     FROM dbo.PermissionTemplates ORDER BY Name";
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    list.Add(new PermissionTemplate
                    {
                        Id = rd.GetInt32(0),
                        Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        Description = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        ModuleKeys = PermissionTemplate.ParseModuleKeys(rd.IsDBNull(3) ? "" : rd.GetString(3)),
                        Icon = rd.IsDBNull(4) ? "📋" : rd.GetString(4),
                        Color = rd.IsDBNull(5) ? "#3B82F6" : rd.GetString(5),
                        CreatedAt = rd.IsDBNull(6) ? null : rd.GetDateTime(6),
                        CreatedBy = rd.IsDBNull(7) ? "" : rd.GetString(7)
                    });
                }
                return (list, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.LoadAll] {ex}");
                return (list, ex.Message);
            }
        }

        public async Task<(int NewId, string? Error)> InsertAsync(PermissionTemplate t, string createdBy)
        {
            try
            {
                const string sql = @"INSERT INTO dbo.PermissionTemplates (Name, Description, ModuleKeys, Icon, Color, CreatedBy)
                                     OUTPUT INSERTED.Id
                                     VALUES (@name, @desc, @keys, @icon, @color, @by)";
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@name", t.Name ?? "");
                cmd.Parameters.AddWithValue("@desc", (object?)t.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@keys", t.ModuleKeysCsv ?? "");
                cmd.Parameters.AddWithValue("@icon", t.Icon ?? "📋");
                cmd.Parameters.AddWithValue("@color", t.Color ?? "#3B82F6");
                cmd.Parameters.AddWithValue("@by", createdBy ?? "");
                var newId = await cmd.ExecuteScalarAsync();
                if (newId == null || newId == DBNull.Value) return (-1, "SQL zwrócił NULL zamiast Id");
                int id = newId is int i ? i : Convert.ToInt32(newId);
                return (id, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.Insert] {ex}");
                return (-1, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Error)> UpdateAsync(PermissionTemplate t)
        {
            try
            {
                const string sql = @"UPDATE dbo.PermissionTemplates
                                     SET Name = @name, Description = @desc, ModuleKeys = @keys, Icon = @icon, Color = @color
                                     WHERE Id = @id";
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", t.Id);
                cmd.Parameters.AddWithValue("@name", t.Name ?? "");
                cmd.Parameters.AddWithValue("@desc", (object?)t.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@keys", t.ModuleKeysCsv ?? "");
                cmd.Parameters.AddWithValue("@icon", t.Icon ?? "📋");
                cmd.Parameters.AddWithValue("@color", t.Color ?? "#3B82F6");
                int affected = await cmd.ExecuteNonQueryAsync();
                if (affected <= 0) return (false, $"Nie znaleziono szablonu Id={t.Id} (mógł być usunięty równolegle)");
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.Update] {ex}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Error)> DeleteAsync(int id)
        {
            try
            {
                // Także usuwamy wszystkie powiązania z userami (cascade-light).
                const string sql = @"DELETE FROM dbo.OperatorTemplates WHERE TemplateId = @id;
                                     DELETE FROM dbo.PermissionTemplates WHERE Id = @id;";
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                int affected = await cmd.ExecuteNonQueryAsync();
                if (affected <= 0) return (false, $"Nie znaleziono szablonu Id={id}");
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.Delete] {ex}");
                return (false, ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // OPERATOR ↔ TEMPLATE assignments (many-to-many)
        // ─────────────────────────────────────────────────────────────────────

        public async Task<(bool Ok, string? Error)> EnsureAssignmentTableExistsAsync()
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.OperatorTemplates') AND type = 'U')
                BEGIN
                    CREATE TABLE dbo.OperatorTemplates (
                        OperatorId VARCHAR(20) NOT NULL,
                        TemplateId INT NOT NULL,
                        AssignedAt DATETIME DEFAULT GETDATE(),
                        AssignedBy VARCHAR(50),
                        PRIMARY KEY (OperatorId, TemplateId)
                    );
                    CREATE NONCLUSTERED INDEX IX_OperatorTemplates_Template ON dbo.OperatorTemplates (TemplateId);
                END";
            try
            {
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.EnsureAssignTable] {ex}");
                return (false, ex.Message);
            }
        }

        public async Task<(List<int> Ids, string? Error)> GetUserTemplateIdsAsync(string userId)
        {
            var ids = new List<int>();
            try
            {
                const string sql = "SELECT TemplateId FROM dbo.OperatorTemplates WHERE OperatorId = @uid";
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@uid", userId ?? "");
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync()) ids.Add(rd.GetInt32(0));
                return (ids, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.GetUserAssign] {ex}");
                return (ids, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Error)> AssignTemplateAsync(string userId, int templateId, string assignedBy)
        {
            try
            {
                // MERGE bezpieczne wobec duplikatów (PK = OperatorId+TemplateId)
                const string sql = @"
                    IF NOT EXISTS (SELECT 1 FROM dbo.OperatorTemplates WHERE OperatorId = @uid AND TemplateId = @tid)
                    BEGIN
                        INSERT INTO dbo.OperatorTemplates (OperatorId, TemplateId, AssignedBy)
                        VALUES (@uid, @tid, @by);
                    END";
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@uid", userId ?? "");
                cmd.Parameters.AddWithValue("@tid", templateId);
                cmd.Parameters.AddWithValue("@by", assignedBy ?? "");
                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.Assign] {ex}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Error)> UnassignTemplateAsync(string userId, int templateId)
        {
            try
            {
                const string sql = "DELETE FROM dbo.OperatorTemplates WHERE OperatorId = @uid AND TemplateId = @tid";
                await using var conn = new SqlConnection(_connStr);
                await conn.OpenAsync();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@uid", userId ?? "");
                cmd.Parameters.AddWithValue("@tid", templateId);
                await cmd.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Templates.Unassign] {ex}");
                return (false, ex.Message);
            }
        }
    }
}
