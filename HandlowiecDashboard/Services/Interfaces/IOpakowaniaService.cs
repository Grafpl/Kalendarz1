using System.Collections.Generic;
using System.Threading.Tasks;
using Kalendarz1.HandlowiecDashboard.Models;

namespace Kalendarz1.HandlowiecDashboard.Services.Interfaces
{
    public interface IOpakowaniaService
    {
        Task<OpakowaniaKPI> PobierzKPIAsync();
        Task<List<SaldoOpakowanKontrahenta>> PobierzSaldaZRyzykiemAsync(string handlowiec = null);
        Task<List<AgingOpakowan>> PobierzAgingAsync();
        Task<List<TrendOpakowan>> PobierzTrendAsync(int miesiecy = 12);
        Task<List<AlertOpakowania>> PobierzAlertyAsync();
        Task<List<RiskMapPoint>> PobierzMapeRyzykaAsync(string handlowiec = null);
    }
}
