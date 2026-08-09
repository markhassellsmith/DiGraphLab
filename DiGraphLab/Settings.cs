using System;
using System.IO;
using System.Text.Json;

namespace DiGraphLab
{
    public class Settings
    {
        public string Theme { get; set; } = "Dark"; // "Light" or "Dark"
        public bool AssignDefaultColorToNew { get; set; } = true;
        public bool AutoScaleNodeLabels { get; set; } = true;
        // Controls for autoscaling behaviour
        public double OccupancyFactor { get; set; } = 0.25; // portion of viewer area reserved for nodes
        public int MinFontSize { get; set; } = 6;
        public int MaxFontSize { get; set; } = 14;
        public int MaxLabelChars { get; set; } = 30;

        private static string GetSettingsPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiGraphLab");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        public static Settings Load()
        {
            try
            {
                var path = GetSettingsPath();
                if (!File.Exists(path)) return new Settings();
                var json = File.ReadAllText(path);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var s = JsonSerializer.Deserialize<Settings>(json, opts);
                return s ?? new Settings();
            }
            catch
            {
                return new Settings();
            }
        }

        public void Save()
        {
            try
            {
                var path = GetSettingsPath();
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(this, opts);
                File.WriteAllText(path, json);
            }
            catch
            {
                // ignore errors saving settings
            }
        }
    }
}
