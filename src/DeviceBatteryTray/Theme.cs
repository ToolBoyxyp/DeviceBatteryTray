using System;
using System.Drawing;
using System.Drawing.Text;
using Microsoft.Win32;

namespace DeviceBatteryTray
{
    public sealed class ThemeColors
    {
        public bool Dark { get; private set; }
        public Color Bg { get; private set; }
        public Color Card { get; private set; }
        public Color Text { get; private set; }
        public Color Muted { get; private set; }
        public Color Border { get; private set; }
        public Color Hover { get; private set; }
        public Color Track { get; private set; }

        public static ThemeColors Create(string themeName)
        {
            bool dark = true;
            if (string.Equals(themeName, "light", StringComparison.OrdinalIgnoreCase))
            {
                dark = false;
            }
            else if (string.Equals(themeName, "system", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                        if (value != null)
                        {
                            dark = Convert.ToInt32(value) == 0;
                        }
                    }
                }
                catch
                {
                    dark = true;
                }
            }

            ThemeColors colors = new ThemeColors();
            colors.Dark = dark;
            if (dark)
            {
                colors.Bg = FromHtml("#1B1E24");
                colors.Card = FromHtml("#23272F");
                colors.Text = FromHtml("#F3F5F9");
                colors.Muted = FromHtml("#98A2B3");
                colors.Border = FromHtml("#343A46");
                colors.Hover = FromHtml("#2E3440");
                colors.Track = FromHtml("#3A4150");
            }
            else
            {
                colors.Bg = FromHtml("#FFFFFF");
                colors.Card = FromHtml("#F6F7F9");
                colors.Text = FromHtml("#1B1F24");
                colors.Muted = FromHtml("#6B7280");
                colors.Border = FromHtml("#E4E7EC");
                colors.Hover = FromHtml("#F0F2F5");
                colors.Track = FromHtml("#E7EAF0");
            }

            return colors;
        }

        public Color GetAccent(AppConfig config)
        {
            try
            {
                return FromHtml(config.AccentColor);
            }
            catch
            {
                return Color.FromArgb(76, 194, 255);
            }
        }

        public Color GetLevelColor(AppConfig config, int? level)
        {
            if (!level.HasValue)
            {
                return Muted;
            }

            if (!config.ColorByLevel)
            {
                return GetAccent(config);
            }

            if (level.Value <= config.CriticalThreshold)
            {
                return FromHtml("#E5484D");
            }

            if (level.Value <= config.WarnThreshold)
            {
                return FromHtml("#F5A524");
            }

            return FromHtml("#30A46C");
        }

        public static Color GetContrast(Color color)
        {
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            return luminance > 0.6 ? Color.FromArgb(22, 24, 28) : Color.White;
        }

        public static Color FromHtml(string html)
        {
            try
            {
                return ColorTranslator.FromHtml(html);
            }
            catch
            {
                return Color.FromArgb(76, 194, 255);
            }
        }
    }

    public static class UiFonts
    {
        public static readonly string Family = PickFamily();

        private static string PickFamily()
        {
            try
            {
                using (InstalledFontCollection fonts = new InstalledFontCollection())
                {
                    foreach (FontFamily family in fonts.Families)
                    {
                        if (string.Equals(family.Name, "Microsoft YaHei UI", StringComparison.OrdinalIgnoreCase))
                        {
                            return "Microsoft YaHei UI";
                        }
                    }
                }
            }
            catch
            {
            }

            return "Segoe UI";
        }
    }
}
