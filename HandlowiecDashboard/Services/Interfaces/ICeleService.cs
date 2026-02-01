using System.Collections.Generic;
using System.Threading.Tasks;
using Kalendarz1.HandlowiecDashboard.Models;

namespace Kalendarz1.HandlowiecDashboard.Services.Interfaces
{
    /// <summary>
    /// Interfejs serwisu celów sprzedażowych handlowców
    /// </summary>
    public interface ICeleService
    {
        Task<RealizacjaCelu> PobierzRealizacjeCeluAsync(string handlowiec, int rok, int miesiac);
        Task<List<RealizacjaCelu>> PobierzRealizacjeWszystkichAsync(int rok, int miesiac);
        Task ZapiszCelAsync(CelHandlowca cel);
    }
}
