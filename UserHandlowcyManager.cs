using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public static class UserHandlowcyManager
    {
        private static string _connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public static void SetConnectionString(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Tworzy tabelę UserHandlowcy i wypełnia ją listą handlowców
        /// </summary>
        public static void CreateTableIfNotExists()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // 1. Sprawdź czy tabela istnieje, jeśli nie - utwórz
                    string checkTableQuery = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserHandlowcy')
                        BEGIN
                            CREATE TABLE UserHandlowcy (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                UserID NVARCHAR(50) NOT NULL,
                                HandlowiecName NVARCHAR(255) NOT NULL,
                                CreatedBy NVARCHAR(100) NULL,
                                CreatedAt DATETIME DEFAULT GETDATE(),
                                CONSTRAINT UQ_UserHandlowcy UNIQUE (UserID, HandlowiecName)
                            );
                            
                            CREATE INDEX IX_UserHandlowcy_UserID ON UserHandlowcy(UserID);
                            CREATE INDEX IX_UserHandlowcy_HandlowiecName ON UserHandlowcy(HandlowiecName);
                        END";

                    using (SqlCommand cmd = new SqlCommand(checkTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Sprawdź czy tabela dostępnych handlowców istnieje
                    string checkAvailableHandlowcyQuery = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AvailableHandlowcy')
                        BEGIN
                            CREATE TABLE AvailableHandlowcy (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                HandlowiecName NVARCHAR(255) NOT NULL UNIQUE,
                                IsActive BIT DEFAULT 1,
                                CreatedAt DATETIME DEFAULT GETDATE()
                            );
                            
                            CREATE INDEX IX_AvailableHandlowcy_IsActive ON AvailableHandlowcy(IsActive);
                        END";

                    using (SqlCommand cmd = new SqlCommand(checkAvailableHandlowcyQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // 3. Wypełnij tabelę handlowców jeśli jest pusta
                    InitializeHandlowcyList(conn);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Błąd podczas tworzenia tabel:\n{ex.Message}",
                    "Błąd",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Inicjalizuje listę dostępnych handlowców z bazy Handel
        /// </summary>
        private static void InitializeHandlowcyList(SqlConnection libraConn)
        {
            try
            {
                // Sprawdź czy lista już istnieje
                string checkQuery = "SELECT COUNT(*) FROM AvailableHandlowcy";
                using (SqlCommand cmd = new SqlCommand(checkQuery, libraConn))
                {
                    int count = (int)cmd.ExecuteScalar();
                    if (count > 0) return; // Lista już istnieje
                }

                // Pobierz handlowców z ostatniego roku z bazy Handel
                string handelConnString = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

                var handlowcy = new HashSet<string>();

                using (SqlConnection handelConn = new SqlConnection(handelConnString))
                {
                    handelConn.Open();

                    string query = @"
                        SELECT DISTINCT CDim_Handlowiec_Val
                        FROM [HANDEL].[SSCommon].[ContractorClassification]
                        WHERE CDim_Handlowiec_Val IS NOT NULL 
                          AND CDim_Handlowiec_Val <> ''
                          AND EXISTS (
                              SELECT 1 
                              FROM [HANDEL].[HM].[DK] dk
                              WHERE dk.khid = ContractorClassification.ElementId
                                AND dk.data >= DATEADD(YEAR, -1, GETDATE())
                          )
                        ORDER BY CDim_Handlowiec_Val";

                    using (SqlCommand cmd = new SqlCommand(query, handelConn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string handlowiec = reader.GetString(0).Trim();
                                if (!string.IsNullOrEmpty(handlowiec))
                                {
                                    handlowcy.Add(handlowiec);
                                }
                            }
                        }
                    }
                }

                // Jeśli nie znaleziono handlowców z ostatniego roku, pobierz wszystkich
                if (handlowcy.Count == 0)
                {
                    using (SqlConnection handelConn = new SqlConnection(handelConnString))
                    {
                        handelConn.Open();

                        string query = @"
                            SELECT DISTINCT CDim_Handlowiec_Val
                            FROM [HANDEL].[SSCommon].[ContractorClassification]
                            WHERE CDim_Handlowiec_Val IS NOT NULL 
                              AND CDim_Handlowiec_Val <> ''
                            ORDER BY CDim_Handlowiec_Val";

                        using (SqlCommand cmd = new SqlCommand(query, handelConn))
                        {
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string handlowiec = reader.GetString(0).Trim();
                                    if (!string.IsNullOrEmpty(handlowiec))
                                    {
                                        handlowcy.Add(handlowiec);
                                    }
                                }
                            }
                        }
                    }
                }

                // Wstaw handlowców do tabeli AvailableHandlowcy
                foreach (string handlowiec in handlowcy.OrderBy(h => h))
                {
                    string insertQuery = @"
                        INSERT INTO AvailableHandlowcy (HandlowiecName, IsActive, CreatedAt)
                        VALUES (@handlowiec, 1, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, libraConn))
                    {
                        cmd.Parameters.AddWithValue("@handlowiec", handlowiec);
                        cmd.ExecuteNonQuery();
                    }
                }

                System.Windows.Forms.MessageBox.Show(
                    $"✓ Załadowano {handlowcy.Count} handlowców do systemu.",
                    "Sukces",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Błąd podczas inicjalizacji listy handlowców:\n{ex.Message}",
                    "Błąd",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Pobiera listę wszystkich dostępnych handlowców
        /// </summary>
        public static List<string> GetAvailableHandlowcy()
        {
            var handlowcy = new List<string>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT HandlowiecName 
                        FROM AvailableHandlowcy 
                        WHERE IsActive = 1
                        ORDER BY HandlowiecName";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                handlowcy.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Błąd podczas pobierania listy handlowców:\n{ex.Message}",
                    "Błąd",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }

            return handlowcy;
        }

        /// <summary>
        /// Pobiera listę handlowców przypisanych do użytkownika
        /// </summary>
        public static List<string> GetUserHandlowcy(string userId)
        {
            var handlowcy = new List<string>();

            if (string.IsNullOrEmpty(userId))
                return handlowcy;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT HandlowiecName 
                        FROM UserHandlowcy 
                        WHERE UserID = @userId 
                        ORDER BY HandlowiecName";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                handlowcy.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Błąd podczas pobierania handlowców użytkownika:\n{ex.Message}",
                    "Błąd", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

            return handlowcy;
        }

        public static bool HasAssignedHandlowcy(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "SELECT COUNT(*) FROM UserHandlowcy WHERE UserID = @userId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool AddHandlowiecToUser(string userId, string handlowiecName, string createdBy = null)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(handlowiecName))
                return false;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM UserHandlowcy WHERE UserID = @userId AND HandlowiecName = @handlowiec";
                    using (SqlCommand cmd = new SqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@handlowiec", handlowiecName);

                        if ((int)cmd.ExecuteScalar() > 0)
                            return false;
                    }

                    string insertQuery = @"
                        INSERT INTO UserHandlowcy (UserID, HandlowiecName, CreatedBy, CreatedAt) 
                        VALUES (@userId, @handlowiec, @createdBy, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@handlowiec", handlowiecName);
                        cmd.Parameters.AddWithValue("@createdBy", (object)createdBy ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Błąd podczas dodawania handlowca:\n{ex.Message}",
                    "Błąd", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        public static bool RemoveHandlowiecFromUser(string userId, string handlowiecName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(handlowiecName))
                return false;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "DELETE FROM UserHandlowcy WHERE UserID = @userId AND HandlowiecName = @handlowiec";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@handlowiec", handlowiecName);
                        int affected = cmd.ExecuteNonQuery();
                        return affected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Błąd podczas usuwania handlowca:\n{ex.Message}",
                    "Błąd", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        public static int RemoveAllHandlowcyFromUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "DELETE FROM UserHandlowcy WHERE UserID = @userId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        return cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Błąd podczas usuwania wszystkich handlowców:\n{ex.Message}",
                    "Błąd", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return 0;
            }
        }

        public static DataTable GetUserHandlowcyAsDataTable(string userId)
        {
            DataTable dt = new DataTable();

            if (string.IsNullOrEmpty(userId))
                return dt;

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    string query = @"
                        SELECT 
                            HandlowiecName AS [Handlowiec],
                            CreatedAt AS [Data przypisania],
                            CreatedBy AS [Przypisał]
                        FROM UserHandlowcy 
                        WHERE UserID = @userId 
                        ORDER BY HandlowiecName";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        adapter.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Błąd podczas pobierania danych handlowców:\n{ex.Message}",
                    "Błąd", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

            return dt;
        }

        public static string GetHandlowcyWhereClause(string userId, string handlowiecColumnAlias = "WYM.CDim_Handlowiec_Val")
        {
            if (string.IsNullOrEmpty(userId) || userId == "11111")
                return "";

            var handlowcy = GetUserHandlowcy(userId);

            if (handlowcy.Count == 0)
                return $" AND {handlowiecColumnAlias} IN ('____BRAK_UPRAWNIEN____')";

            var handlowcyList = string.Join("','", handlowcy.Select(h => h.Replace("'", "''")));
            return $" AND {handlowiecColumnAlias} IN ('{handlowcyList}')";
        }

        /// <summary>
        /// Odświeża listę dostępnych handlowców z bazy Handel
        /// </summary>
        public static void RefreshAvailableHandlowcy()
        {
            try
            {
                using (SqlConnection libraConn = new SqlConnection(_connectionString))
                {
                    libraConn.Open();

                    // Usuń wszystkich nieaktywnych
                    string deleteQuery = "DELETE FROM AvailableHandlowcy WHERE IsActive = 0";
                    using (SqlCommand cmd = new SqlCommand(deleteQuery, libraConn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    // Pobierz nowych handlowców
                    InitializeHandlowcyList(libraConn);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Błąd podczas odświeżania listy handlowców:\n{ex.Message}",
                    "Błąd",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}