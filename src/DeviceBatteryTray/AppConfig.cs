using System;
using System.IO;
using System.Text.Json;

namespace DeviceBatteryTray
{
    public sealed class AppConfig
    {
        public string Theme { get; set; } = "system";
        public string AccentColor { get; set; } = "#4CC2FF";
        public string IconStyle { get; set; } = "battery";
        public bool ColorByLevel { get; set; } = true;
        public int WarnThreshold { get; set; } = 30;
        public int CriticalThreshold { get; set; } = 15;
        public double MenuFontSize { get; set; } = 9.5;
        public bool ShowProgressBar { get; set; } = true;
        public bool ShowSource { get; set; } = true;
        public int RefreshSeconds { get; set; } = 30;
        public int PopupWidth { get; set; } = 340;
        public int CornerRadius { get; set; } = 14;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static string ConfigDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DeviceBatteryTray");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDirectory, "settings.json"); }
        }

        public static AppConfig Load()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                string path = ConfigPath;
                if (!File.Exists(path))
                {
                    string legacy = Path.Combine(AppContext.BaseDirectory, "settings.json");
                    if (File.Exists(legacy))
                    {
                        path = legacy;
                    }
                }

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    AppConfig config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                    if (config != null)
                    {
                        config.Normalize();
                        config.Save();
                        return config;
                    }
                }
            }
            catch
            {
            }

            AppConfig defaults = new AppConfig();
            defaults.Save();
            return defaults;
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Theme)) Theme = "system";
            if (string.IsNullOrWhiteSpace(AccentColor)) AccentColor = "#4CC2FF";
            if (string.IsNullOrWhiteSpace(IconStyle)) IconStyle = "battery";

            WarnThreshold = Math.Max(5, Math.Min(90, WarnThreshold));
            CriticalThreshold = Math.Max(1, Math.Min(50, CriticalThreshold));
            if (CriticalThreshold > WarnThreshold)
            {
                int swap = CriticalThreshold;
                CriticalThreshold = WarnThreshold;
                WarnThreshold = swap;
            }

            MenuFontSize = Math.Max(8, Math.Min(12, MenuFontSize));
            RefreshSeconds = Math.Max(10, Math.Min(300, RefreshSeconds));
            PopupWidth = Math.Max(280, Math.Min(520, PopupWidth));
            CornerRadius = Math.Max(0, Math.Min(24, CornerRadius));
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch
            {
            }
        }

        public AppConfig Clone()
        {
            return new AppConfig
            {
                Theme = Theme,
                AccentColor = AccentColor,
                IconStyle = IconStyle,
                ColorByLevel = ColorByLevel,
                WarnThreshold = WarnThreshold,
                CriticalThreshold = CriticalThreshold,
                MenuFontSize = MenuFontSize,
                ShowProgressBar = ShowProgressBar,
                ShowSource = ShowSource,
                RefreshSeconds = RefreshSeconds,
                PopupWidth = PopupWidth,
                CornerRadius = CornerRadius
            };
        }
    }
}
