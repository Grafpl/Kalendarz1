using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Manager zadań - pobiera zadania użytkownika
    /// </summary>
    public static class TasksManager
    {
        private static readonly string ConnectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        public class TaskInfo
        {
            public long Id { get; set; }
            public string Firma { get; set; }
            public string TypZadania { get; set; }
            public string Opis { get; set; }
            public DateTime TerminWykonania { get; set; }
            public int Priorytet { get; set; }
            public bool IsPilne { get; set; }
            public bool IsZalegle { get; set; }
        }

        public class TasksSummary
        {
            public int Total { get; set; }
            public int Done { get; set; }
            public int Pilne { get; set; }
            public int Zalegle { get; set; }
            public List<TaskInfo> TopTasks { get; set; } = new List<TaskInfo>();
        }

        /// <summary>
        /// Pobiera podsumowanie zadań na dziś dla operatora
        /// </summary>
        public static TasksSummary GetTodayTasksSummary(string operatorId, int maxTasks = 3)
        {
            var summary = new TasksSummary();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // Policz wszystkie zadania na dziś
                    var countCmd = new SqlCommand(@"
                        SELECT
                            COUNT(*) as Total,
                            SUM(CASE WHEN Wykonane = 1 THEN 1 ELSE 0 END) as Done,
                            SUM(CASE WHEN Wykonane = 0 AND TerminWykonania < DATEADD(hour, 2, GETDATE()) AND TerminWykonania >= GETDATE() THEN 1 ELSE 0 END) as Pilne,
                            SUM(CASE WHEN Wykonane = 0 AND TerminWykonania < GETDATE() THEN 1 ELSE 0 END) as Zalegle
                        FROM Zadania
                        WHERE OperatorID = @id
                          AND CAST(TerminWykonania AS DATE) = CAST(GETDATE() AS DATE)", conn);
                    countCmd.Parameters.AddWithValue("@id", operatorId);

                    using (var reader = countCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            summary.Total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                            summary.Done = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                            summary.Pilne = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                            summary.Zalegle = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        }
                    }

                    // Pobierz top zadania (niewykonane, posortowane wg pilności)
                    var tasksCmd = new SqlCommand(@"
                        SELECT TOP (@max)
                            Z.ID,
                            ISNULL(O.Nazwa, 'Brak firmy') AS Firma,
                            Z.TypZadania,
                            Z.Opis,
                            Z.TerminWykonania,
                            Z.Priorytet,
                            CASE WHEN Z.TerminWykonania < GETDATE() THEN 1 ELSE 0 END AS IsZalegle,
                            CASE WHEN Z.TerminWykonania < DATEADD(hour, 2, GETDATE()) AND Z.TerminWykonania >= GETDATE() THEN 1 ELSE 0 END AS IsPilne
                        FROM Zadania Z
                        LEFT JOIN OdbiorcyCRM O ON Z.IDOdbiorcy = O.ID
                        WHERE Z.OperatorID = @id
                          AND Z.Wykonane = 0
                          AND CAST(Z.TerminWykonania AS DATE) <= CAST(GETDATE() AS DATE)
                        ORDER BY
                            CASE WHEN Z.TerminWykonania < GETDATE() THEN 0 ELSE 1 END,
                            Z.Priorytet DESC,
                            Z.TerminWykonania", conn);
                    tasksCmd.Parameters.AddWithValue("@id", operatorId);
                    tasksCmd.Parameters.AddWithValue("@max", maxTasks);

                    using (var reader = tasksCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            summary.TopTasks.Add(new TaskInfo
                            {
                                Id = reader.GetInt64(0),
                                Firma = reader.GetString(1),
                                TypZadania = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Opis = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                TerminWykonania = reader.GetDateTime(4),
                                Priorytet = reader.GetInt32(5),
                                IsZalegle = reader.GetInt32(6) == 1,
                                IsPilne = reader.GetInt32(7) == 1
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // W przypadku błędu zwróć pusty summary
            }

            return summary;
        }
    }
}
