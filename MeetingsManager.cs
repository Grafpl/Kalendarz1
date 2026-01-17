using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    /// <summary>
    /// Manager spotkań - pobiera nadchodzące spotkania użytkownika
    /// </summary>
    public static class MeetingsManager
    {
        private static readonly string ConnectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova";

        public class MeetingInfo
        {
            public long Id { get; set; }
            public string Tytul { get; set; }
            public DateTime DataOd { get; set; }
            public DateTime DataDo { get; set; }
            public string Miejsce { get; set; }
            public string KontrahentNazwa { get; set; }
            public TimeSpan TimeUntil => DataOd - DateTime.Now;
            public bool IsNow => DateTime.Now >= DataOd && DateTime.Now <= DataDo;
            public bool IsSoon => TimeUntil.TotalMinutes > 0 && TimeUntil.TotalMinutes <= 60;

            public string GetTimeUntilText()
            {
                if (IsNow) return "TERAZ";
                if (TimeUntil.TotalMinutes < 0) return "Rozpoczęte";
                if (TimeUntil.TotalMinutes < 60) return $"Za {(int)TimeUntil.TotalMinutes} min";
                if (TimeUntil.TotalHours < 24) return $"Za {(int)TimeUntil.TotalHours}h {TimeUntil.Minutes}min";
                return DataOd.ToString("dd.MM HH:mm");
            }
        }

        public class MeetingsSummary
        {
            public int TodayCount { get; set; }
            public MeetingInfo NextMeeting { get; set; }
            public List<MeetingInfo> UpcomingMeetings { get; set; } = new List<MeetingInfo>();
        }

        /// <summary>
        /// Pobiera podsumowanie spotkań dla operatora
        /// </summary>
        public static MeetingsSummary GetMeetingsSummary(string operatorId, int maxMeetings = 3)
        {
            var summary = new MeetingsSummary();

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // Policz spotkania na dziś
                    var countCmd = new SqlCommand(@"
                        SELECT COUNT(*)
                        FROM Spotkania
                        WHERE OperatorID = @id
                          AND CAST(DataOd AS DATE) = CAST(GETDATE() AS DATE)
                          AND Status != 'Anulowane'", conn);
                    countCmd.Parameters.AddWithValue("@id", operatorId);
                    summary.TodayCount = (int)countCmd.ExecuteScalar();

                    // Pobierz nadchodzące spotkania (dziś i jutro)
                    var meetingsCmd = new SqlCommand(@"
                        SELECT TOP (@max)
                            S.ID,
                            S.Tytul,
                            S.DataOd,
                            S.DataDo,
                            ISNULL(S.Miejsce, '') AS Miejsce,
                            ISNULL(
                                CASE
                                    WHEN S.KontrahentTyp = 'Odbiorca' THEN (SELECT TOP 1 Nazwa FROM OdbiorcyCRM WHERE ID = S.KontrahentID)
                                    WHEN S.KontrahentTyp = 'Hodowca' THEN (SELECT TOP 1 NazwaHodowcy FROM DaneHodowcy WHERE KodDostawcy = S.KontrahentID)
                                    ELSE ''
                                END, '') AS KontrahentNazwa
                        FROM Spotkania S
                        WHERE S.OperatorID = @id
                          AND S.DataOd >= GETDATE()
                          AND S.DataOd <= DATEADD(day, 2, GETDATE())
                          AND S.Status != 'Anulowane'
                        ORDER BY S.DataOd", conn);
                    meetingsCmd.Parameters.AddWithValue("@id", operatorId);
                    meetingsCmd.Parameters.AddWithValue("@max", maxMeetings);

                    using (var reader = meetingsCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var meeting = new MeetingInfo
                            {
                                Id = reader.GetInt64(0),
                                Tytul = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                DataOd = reader.GetDateTime(2),
                                DataDo = reader.GetDateTime(3),
                                Miejsce = reader.GetString(4),
                                KontrahentNazwa = reader.GetString(5)
                            };
                            summary.UpcomingMeetings.Add(meeting);
                        }
                    }

                    // Ustaw najbliższe spotkanie
                    if (summary.UpcomingMeetings.Count > 0)
                    {
                        summary.NextMeeting = summary.UpcomingMeetings[0];
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
