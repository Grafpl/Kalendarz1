namespace MobileAPI.Models;

public class DashboardData
{
    public DateTime Data { get; set; }
    public decimal SumaZamowien { get; set; }
    public int LiczbaZamowien { get; set; }
    public int LiczbaKlientow { get; set; }
    public int SumaPalet { get; set; }
    public int LiczbaAnulowanych { get; set; }
}
