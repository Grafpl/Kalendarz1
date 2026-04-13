using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Kalendarz1.MapaFloty
{
    /// <summary>
    /// Serwis raportów Webfleet — trip summary, events, KPIs
    /// </summary>
    public class WebfleetReportService
    {
        private static readonly string WfAccount = "942879", WfUser = "Administrator", WfPass = "kaazZVY5";
        private static readonly string WfKey = "7a538868-96cf-4149-a9db-6e090de7276c";
        private static readonly string WfUrl = "https://csv.webfleet.com/extern";
        private static readonly string _auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{WfUser}:{WfPass}"));
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

        // ══════════════════════════════════════════════════════════════════
        // showTripSummaryReportExtern — dzienne podsumowania per pojazd
        // ══════════════════════════════════════════════════════════════════

        public async Task<List<TripSummary>> GetTripSummaryAsync(string objectNo, string dateFrom, string dateTo)
        {
            var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json" +
                $"&action=showTripSummaryReportExtern&objectno={U(objectNo)}" +
                $"&useISO8601=true&rangefrom_string={dateFrom}T00:00:00&rangeto_string={dateTo}T23:59:59";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!body.TrimStart().StartsWith("[")) return new();
            var arr = JArray.Parse(body);
            return arr.Select(o =>
            {
                DateTime.TryParse(o["start_time"]?.ToString(), out var st);
                DateTime.TryParse(o["end_time"]?.ToString(), out var et);
                return new TripSummary
                {
                    ObjectNo = o["objectno"]?.ToString() ?? "",
                    ObjectName = o["objectname"]?.ToString() ?? "",
                    Date = st.Date,
                    StartTime = st,
                    EndTime = et,
                    DistanceKm = (o["distance"]?.Value<double>() ?? 0) / 1000.0,
                    TripTimeSec = o["triptime"]?.Value<int>() ?? 0,
                    OperatingTimeSec = o["operatingtime"]?.Value<int>() ?? 0,
                    StandstillSec = o["standstill"]?.Value<int>() ?? 0,
                    Tours = o["tours"]?.Value<int>() ?? 0,
                    FuelUsage = o["fuel_usage"]?.Value<double>() ?? 0,
                    EndAddress = o["end_postext"]?.ToString() ?? ""
                };
            }).ToList();
        }

        // ══════════════════════════════════════════════════════════════════
        // showTripReportExtern — szczegółowe wyjazdy
        // ══════════════════════════════════════════════════════════════════

        public async Task<List<TripDetail>> GetTripsAsync(string objectNo, string dateFrom, string dateTo)
        {
            var url = $"{WfUrl}?account={U(WfAccount)}&apikey={U(WfKey)}&lang=pl&outputformat=json" +
                $"&action=showTripReportExtern&objectno={U(objectNo)}" +
                $"&useISO8601=true&rangefrom_string={dateFrom}T00:00:00&rangeto_string={dateTo}T23:59:59";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", _auth);
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!body.TrimStart().StartsWith("[")) return new();
            var arr = JArray.Parse(body);
            return arr.Select(o =>
            {
                DateTime.TryParse(o["start_time"]?.ToString(), out var st);
                DateTime.TryParse(o["end_time"]?.ToString(), out var et);
                return new TripDetail
                {
                    TripId = o["tripid"]?.ToString() ?? "",
                    ObjectNo = o["objectno"]?.ToString() ?? "",
                    ObjectName = o["objectname"]?.ToString() ?? "",
                    StartTime = st, EndTime = et,
                    StartAddress = o["start_postext"]?.ToString() ?? "",
                    EndAddress = o["end_postext"]?.ToString() ?? "",
                    DistanceKm = (o["distance"]?.Value<double>() ?? 0) / 1000.0,
                    DurationMin = (o["duration"]?.Value<int>() ?? 0) / 60,
                    IdleMin = (o["idle_time"]?.Value<int>() ?? 0) / 60,
                    AvgSpeed = o["avg_speed"]?.Value<int>() ?? 0,
                    MaxSpeed = o["max_speed"]?.Value<int>() ?? 0,
                    DriverName = o["drivername"]?.ToString() ?? "",
                    FuelUsage = o["fuel_usage"]?.Value<double>() ?? 0
                };
            }).ToList();
        }

        private static string U(string s) => Uri.EscapeDataString(s);

        // ══════════════════════════════════════════════════════════════════
        // Modele
        // ══════════════════════════════════════════════════════════════════

        public class TripSummary
        {
            public string ObjectNo { get; set; } = "";
            public string ObjectName { get; set; } = "";
            public DateTime Date { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public double DistanceKm { get; set; }
            public int TripTimeSec { get; set; }
            public int OperatingTimeSec { get; set; }
            public int StandstillSec { get; set; }
            public int Tours { get; set; }
            public double FuelUsage { get; set; }
            public string EndAddress { get; set; } = "";

            public string TripTimeStr => $"{TripTimeSec / 3600}h {TripTimeSec % 3600 / 60}min";
            public string StandstillStr => $"{StandstillSec / 3600}h {StandstillSec % 3600 / 60}min";
        }

        public class TripDetail
        {
            public string TripId { get; set; } = "";
            public string ObjectNo { get; set; } = "";
            public string ObjectName { get; set; } = "";
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string StartAddress { get; set; } = "";
            public string EndAddress { get; set; } = "";
            public double DistanceKm { get; set; }
            public int DurationMin { get; set; }
            public int IdleMin { get; set; }
            public int AvgSpeed { get; set; }
            public int MaxSpeed { get; set; }
            public string DriverName { get; set; } = "";
            public double FuelUsage { get; set; }
        }
    }
}
