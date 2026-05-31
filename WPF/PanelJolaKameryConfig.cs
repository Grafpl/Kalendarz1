using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Kalendarz1.WPF
{
    public class PanelJolaKamera
    {
        public int Channel { get; set; }
        public string Name { get; set; } = "";
    }

    public class PanelJolaKameryConfig
    {
        public PanelJolaKamera Camera1 { get; set; } = new PanelJolaKamera { Channel = 6,  Name = "Kanał 6 - PROD_Waga" };
        public PanelJolaKamera Camera2 { get; set; } = new PanelJolaKamera { Channel = 21, Name = "Kanał 21 - Zew_Tyl" };

        private const string NvrHost = "192.168.0.125";
        private const int    NvrPort = 554;
        private const string NvrUser = "admin";
        private const string NvrPass = "terePacja12%24";

        public static string SettingsPath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                       "Kalendarz1");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "panel_jola_cameras.json");
            }
        }

        public static PanelJolaKameryConfig Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var cfg = JsonConvert.DeserializeObject<PanelJolaKameryConfig>(json);
                    if (cfg != null && cfg.Camera1 != null && cfg.Camera2 != null)
                        return cfg;
                }
            }
            catch { }
            return new PanelJolaKameryConfig();
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }

        public static string BuildRtspUrl(int channel, int stream)
        {
            return $"rtsp://{NvrUser}:{NvrPass}@{NvrHost}:{NvrPort}/unicast/c{channel}/s{stream}/live";
        }
    }
}
