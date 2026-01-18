using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZPSP.Sales.Models;

namespace ZPSP.Sales.Repositories.Interfaces
{
    /// <summary>
    /// Interfejs repozytorium transportu (TransportPL)
    /// </summary>
    public interface ITransportRepository
    {
        /// <summary>
        /// Pobiera kursy transportowe na dany dzień
        /// </summary>
        Task<IEnumerable<TransportKurs>> GetCoursesForDateAsync(DateTime date);

        /// <summary>
        /// Pobiera kurs po ID
        /// </summary>
        Task<TransportKurs> GetCourseByIdAsync(long kursId);

        /// <summary>
        /// Pobiera ładunki dla kursu
        /// </summary>
        Task<IEnumerable<TransportLadunek>> GetLoadsForCourseAsync(long kursId);

        /// <summary>
        /// Pobiera kursy dla listy ID
        /// </summary>
        Task<IDictionary<long, TransportKurs>> GetCoursesByIdsAsync(IEnumerable<long> kursIds);
    }
}
