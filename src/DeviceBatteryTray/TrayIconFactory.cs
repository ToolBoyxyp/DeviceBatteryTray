using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeviceBatteryTray
{
    internal static class TrayIconFactory
    {
        public static Icon Create(int? level, AppConfig config, ThemeColors theme)
        {
            uint dpi = 96;
            try
            {
                dpi = NativeMethods.GetDpiForSystem();
            }
            catch
            {
            }

            float scale = dpi / 96f;
            int size = (int)Math.Round(16 * scale);
            int systemSize = SystemInformation.SmallIconSize.Width;
            if (systemSize > size)
            {
                size = systemSize;
            }
            if (size < 16)
            {
                size = 16;
            }

            float k = size / 32f;
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                Color color = theme.GetLevelColor(config, level);
                string style = config.IconStyle ?? "battery";
                TextFormatFlags centerFlags = TextFormatFlags.HorizontalCenter |
                                             TextFormatFlags.VerticalCenter |
                                             TextFormatFlags.NoPrefix;

                if (string.Equals(style, "ring", StringComparison.OrdinalIgnoreCase))
                {
                    float penWidth = 4 * k;
                    using (Pen trackPen = new Pen(theme.Track, penWidth))
                    {
                        graphics.DrawEllipse(trackPen, 4 * k, 4 * k, 24 * k, 24 * k);
                    }

                    if (level.HasValue)
                    {
                        using (Pen pen = new Pen(color, penWidth))
                        {
                            float sweep = Math.Max(1, Math.Min(100, level.Value)) * 3.6f;
                            graphics.DrawArc(pen, 4 * k, 4 * k, 24 * k, 24 * k, -90, sweep);
                        }
                    }
                }
                else if (string.Equals(style, "number", StringComparison.OrdinalIgnoreCase))
                {
                    using (SolidBrush brush = new SolidBrush(color))
                    using (GraphicsPath path = RoundedPath(2 * k, 2 * k, 28 * k, 28 * k, 7 * k))
                    {
                        graphics.FillPath(brush, path);
                    }

                    string text = level.HasValue ? level.Value.ToString() : "--";
                    using (Font font = new Font(UiFonts.Family, 13 * k, FontStyle.Bold, GraphicsUnit.Point))
                    {
                        TextRenderer.DrawText(
                            graphics, text, font, new Rectangle(0, 0, size, size),
                            ThemeColors.GetContrast(color), centerFlags);
                    }
                }
                else
                {
                    using (Pen pen = new Pen(color, 2.5f * k))
                    using (GraphicsPath body = RoundedPath(2 * k, 9 * k, 22 * k, 15 * k, 3 * k))
                    {
                        graphics.DrawPath(pen, body);
                    }

                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        using (GraphicsPath cap = RoundedPath(25 * k, 13 * k, 4 * k, 7 * k, 2 * k))
                        {
                            graphics.FillPath(brush, cap);
                        }

                        if (level.HasValue && level.Value > 0)
                        {
                            float innerWidth = Math.Max(2, 16f * k * level.Value / 100f);
                            using (GraphicsPath fill = RoundedPath(5 * k, 12.5f * k, innerWidth, 9 * k, 2 * k))
                            {
                                graphics.FillPath(brush, fill);
                            }
                        }
                    }
                }
            }

            IntPtr handle = bitmap.GetHicon();
            try
            {
                using (Icon temporary = Icon.FromHandle(handle))
                {
                    return (Icon)temporary.Clone();
                }
            }
            finally
            {
                NativeMethods.DestroyIcon(handle);
                bitmap.Dispose();
            }
        }

        private static GraphicsPath RoundedPath(float x, float y, float width, float height, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (width <= 0 || height <= 0)
            {
                return path;
            }

            float diameter = radius * 2;
            if (diameter > width) diameter = width;
            if (diameter > height) diameter = height;
            if (diameter <= 0)
            {
                path.AddRectangle(new RectangleF(x, y, width, height));
                return path;
            }

            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
