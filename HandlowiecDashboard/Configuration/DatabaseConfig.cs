using System;
using System.Configuration;

namespace Kalendarz1.HandlowiecDashboard.Configuration
{
    public static class DatabaseConfig
    {
        private static string _handelConnectionString;
        private static string _libraNetConnectionString;

        public static string HandelConnectionString
        {
            get
            {
                if (_handelConnectionString == null)
                {
                    _handelConnectionString =
                        ConfigurationManager.ConnectionStrings["Handel"]?.ConnectionString
                        ?? Environment.GetEnvironmentVariable("HANDEL_CONNECTION_STRING")
                        ?? "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
                }
                return _handelConnectionString;
            }
        }

        public static string LibraNetConnectionString
        {
            get
            {
                if (_libraNetConnectionString == null)
                {
                    _libraNetConnectionString =
                        ConfigurationManager.ConnectionStrings["LibraNet"]?.ConnectionString
                        ?? Environment.GetEnvironmentVariable("LIBRANET_CONNECTION_STRING")
                        ?? "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                }
                return _libraNetConnectionString;
            }
        }

        public static void Reset()
        {
            _handelConnectionString = null;
            _libraNetConnectionString = null;
        }
    }
}
