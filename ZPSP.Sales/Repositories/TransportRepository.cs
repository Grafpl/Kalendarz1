using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ZPSP.Sales.Infrastructure;
using ZPSP.Sales.Models;
using ZPSP.Sales.Repositories.Interfaces;
using ZPSP.Sales.SQL;

namespace ZPSP.Sales.Repositories
{
    /// <summary>
    /// Repozytorium transportu - dostęp do TransportPL
    /// </summary>
    public class TransportRepository : BaseRepository, ITransportRepository
    {
        public TransportRepository() : base(DatabaseConnections.Instance.TransportPL)
        {
        }

        public TransportRepository(string connectionString) : base(connectionString)
        {
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<TransportKurs>> GetCoursesForDateAsync(DateTime date)
        {
            var courses = await QueryAsync(
                SqlQueries.GetTransportCoursesForDate,
                new { Day = date.Date },
                MapCourse);

            // Pobierz ładunki dla wszystkich kursów
            var courseIds = courses.Select(c => c.KursId).ToList();
            if (courseIds.Any())
            {
                var allLoads = await GetLoadsForCoursesAsync(courseIds);
                foreach (var course in courses)
                {
                    if (allLoads.TryGetValue(course.KursId, out var loads))
                    {
                        course.Ladunki = loads;
                    }
                }
            }

            return courses;
        }

        /// <inheritdoc/>
        public async Task<TransportKurs> GetCourseByIdAsync(long kursId)
        {
            var course = await QuerySingleAsync(
                SqlQueries.GetTransportCourseById,
                new { KursId = kursId },
                MapCourse);

            if (course != null)
            {
                course.Ladunki = (await GetLoadsForCourseAsync(kursId)).ToList();
            }

            return course;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<TransportLadunek>> GetLoadsForCourseAsync(long kursId)
        {
            return await QueryAsync(
                SqlQueries.GetLoadsForCourse,
                new { KursId = kursId },
                MapLoad);
        }

        /// <inheritdoc/>
        public async Task<IDictionary<long, TransportKurs>> GetCoursesByIdsAsync(IEnumerable<long> kursIds)
        {
            var idList = kursIds?.ToList() ?? new List<long>();
            if (!idList.Any())
                return new Dictionary<long, TransportKurs>();

            var sql = $@"
                SELECT
                    k.KursID,
                    k.DataKursu,
                    k.Trasa,
                    k.GodzWyjazdu,
                    k.GodzPowrotu,
                    k.Status,
                    CONCAT(ki.Imie, ' ', ki.Nazwisko) AS Kierowca,
                    ki.Telefon AS TelefonKierowcy,
                    p.Rejestracja,
                    p.Marka AS MarkaPojazdu,
                    p.Model AS ModelPojazdu,
                    ISNULL(p.PaletyH1, 33) AS MaxPalety
                FROM dbo.Kurs k
                LEFT JOIN dbo.Kierowca ki ON k.KierowcaID = ki.KierowcaID
                LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                WHERE k.KursID IN ({string.Join(",", idList)})";

            var courses = await QueryAsync(sql, null, MapCourse);

            // Pobierz ładunki
            var allLoads = await GetLoadsForCoursesAsync(idList);
            foreach (var course in courses)
            {
                if (allLoads.TryGetValue(course.KursId, out var loads))
                {
                    course.Ladunki = loads;
                }
            }

            return courses.ToDictionary(c => c.KursId, c => c);
        }

        #region Private Methods

        private async Task<IDictionary<long, List<TransportLadunek>>> GetLoadsForCoursesAsync(IEnumerable<long> kursIds)
        {
            var idList = kursIds?.ToList() ?? new List<long>();
            if (!idList.Any())
                return new Dictionary<long, List<TransportLadunek>>();

            var sql = $@"
                SELECT
                    l.LadunekID AS LadunekId,
                    l.KursID AS KursId,
                    l.Kolejnosc,
                    l.KodKlienta,
                    l.PaletyH1 AS Palety,
                    l.PojemnikiE2 AS Pojemniki,
                    l.Uwagi
                FROM dbo.Ladunek l
                WHERE l.KursID IN ({string.Join(",", idList)})
                ORDER BY l.KursID, l.Kolejnosc";

            var loads = await QueryAsync(sql, null, MapLoad);

            return loads
                .GroupBy(l => l.KursId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private TransportKurs MapCourse(IDataReader reader)
        {
            return new TransportKurs
            {
                KursId = GetInt64(reader, "KursID"),
                DataKursu = GetDateTime(reader, "DataKursu"),
                Trasa = GetString(reader, "Trasa"),
                GodzWyjazdu = GetNullableTimeSpan(reader, "GodzWyjazdu"),
                GodzPowrotu = GetNullableTimeSpan(reader, "GodzPowrotu"),
                Status = GetString(reader, "Status"),
                Kierowca = GetString(reader, "Kierowca"),
                TelefonKierowcy = GetString(reader, "TelefonKierowcy"),
                Rejestracja = GetString(reader, "Rejestracja"),
                MarkaPojazdu = GetString(reader, "MarkaPojazdu"),
                ModelPojazdu = GetString(reader, "ModelPojazdu"),
                MaxPalety = GetInt32(reader, "MaxPalety")
            };
        }

        private TransportLadunek MapLoad(IDataReader reader)
        {
            var ladunek = new TransportLadunek
            {
                LadunekId = GetInt64(reader, "LadunekId"),
                KursId = GetInt64(reader, "KursId"),
                Kolejnosc = GetInt32(reader, "Kolejnosc"),
                KodKlienta = GetString(reader, "KodKlienta"),
                Palety = GetInt32(reader, "Palety"),
                Pojemniki = GetInt32(reader, "Pojemniki"),
                Uwagi = GetString(reader, "Uwagi")
            };

            // Parsuj ZamowienieId jeśli KodKlienta = ZAM_xxx
            if (ladunek.KodKlienta?.StartsWith("ZAM_") == true)
            {
                if (int.TryParse(ladunek.KodKlienta.Substring(4), out var zamId))
                {
                    ladunek.ZamowienieId = zamId;
                }
            }

            return ladunek;
        }

        #endregion
    }
}
