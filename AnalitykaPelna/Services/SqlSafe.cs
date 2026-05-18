using System;
using System.Data;

namespace Kalendarz1.AnalitykaPelna.Services
{
    /// <summary>
    /// DBNull-safe odczytywanie z SqlDataReader. Wspólne dla wszystkich serwisów Analityki.
    /// </summary>
    internal static class SqlSafe
    {
        public static string ReadString(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? "" : r.GetValue(idx)?.ToString() ?? "";

        public static int ReadInt(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? 0 : Convert.ToInt32(r.GetValue(idx));

        public static decimal ReadDecimal(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? 0m : Convert.ToDecimal(r.GetValue(idx));

        public static DateTime ReadDate(IDataRecord r, int idx)
            => r.IsDBNull(idx) ? DateTime.MinValue : Convert.ToDateTime(r.GetValue(idx));

        public static DateTime ParseDate(string s)
        {
            DateTime.TryParse(s, out var d);
            return d;
        }

        public static DateTime ParseGodzina(DateTime data, string godzinaStr)
        {
            if (string.IsNullOrEmpty(godzinaStr)) return data;
            if (TimeSpan.TryParse(godzinaStr, out var ts)) return data.Date.Add(ts);
            DateTime.TryParse(godzinaStr, out var d);
            return d == DateTime.MinValue ? data : d;
        }
    }
}
