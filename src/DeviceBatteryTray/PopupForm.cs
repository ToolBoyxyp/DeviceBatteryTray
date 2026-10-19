using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DeviceBatteryTray
{
    internal sealed class PopupForm : Form
    {
        private string _mode;
        private AppConfig _config;
        private ThemeColors _theme;
        private List<DeviceBatteryInfo> _devices;
        private DateTime? _lastUpdate;
        private string _lastError;
        private bool _autoStart;
        private bool _refreshing;
        private string _hover = string.Empty;
        private readonly List<RowHit> _rows = new List<RowHit>();
        private readonly List<FooterHit> _footers = new List<FooterHit>();
        private Rectangle _refreshRect;
        private Font _fontTitle;
        private Font _fontSub;
        private Font _fontName;
        private Font _fontPercent;
        private Font _fontFooter;
        private Font _fontSmall;
        private float _scale = 1f;
        private bool _closedByDeactivate;

        public string Mode
        {
            get { return _mode; }
        }

        public Action RefreshRequested;
        public Action SettingsRequested;
        public Func<bool> AutoStartToggleRequested;
        public Action BluetoothRequested;
        public Action ExitRequested;
        public Action<string> ClosedByDeactivate;
        public static bool DiagnosticsEnabled;

        public PopupForm(
            string mode,
            AppConfig config,
            ThemeColors theme,
            List<DeviceBatteryInfo> devices,
            DateTime? lastUpdate,
            string lastError,
            bool autoStart)
        {
            _mode = string.Equals(mode, "compact", StringComparison.Ordinal) ? "compact" : "full";
            _config = config;
            _theme = theme;
            _devices = devices ?? new List<DeviceBatteryInfo>();
            _lastUpdate = lastUpdate;
            _lastError = lastError;
            _autoStart = autoStart;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = theme.Bg;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint,
                true);

            Deactivate += OnDeactivate;
            MouseMove += OnMouseMove;
            MouseClick += OnMouseClick;
            MouseLeave += OnMouseLeave;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scale = DeviceDpi / 96f;
            if (_scale < 1f) _scale = 1f;
            CreateFonts();
            UpdateLayout();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            _scale = e.DeviceDpiNew / 96f;
            if (_scale < 1f) _scale = 1f;
            CreateFonts();
            UpdateLayout();
            Reposition();
        }

        public void ShowNearCursor()
        {
            // 先创建句柄，让 OnHandleCreated 按当前 DPI 算好最终尺寸，
            // 再定位，避免按默认尺寸定位后被 Show() 放大而超出屏幕。
            CreateControl();
            UpdateLayout();
            Reposition();
            Show();
            Activate();
            EnsureOnScreen();

            if (DiagnosticsEnabled)
            {
                try
                {
                    Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DeviceBatteryTray_popup.log"),
                        string.Format(
                            "{0:HH:mm:ss} mode={1} dpi={2} size={3}x{4} loc={5},{6} work={7}\n",
                            DateTime.Now, _mode, DeviceDpi, Width, Height,
                            Location.X, Location.Y, work));
                }
                catch
                {
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            EnsureOnScreen();
        }

        public void UpdateData(
            List<DeviceBatteryInfo> devices,
            DateTime? lastUpdate,
            string lastError,
            AppConfig config,
            ThemeColors theme)
        {
            _devices = devices ?? new List<DeviceBatteryInfo>();
            _lastUpdate = lastUpdate;
            _lastError = lastError;

            bool fontChanged = config.MenuFontSize != _config.MenuFontSize;
            _config = config;
            _theme = theme;
            BackColor = theme.Bg;
            if (fontChanged)
            {
                CreateFonts();
            }

            UpdateLayout();
        }

        public void SetRefreshing(bool refreshing)
        {
            if (_refreshing == refreshing) return;
            _refreshing = refreshing;
            Invalidate();
        }

        private void CreateFonts()
        {
            DisposeFonts();
            _fontTitle = new Font(UiFonts.Family, 12.5f, FontStyle.Bold, GraphicsUnit.Point);
            _fontSub = new Font(UiFonts.Family, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _fontName = new Font(UiFonts.Family, (float)_config.MenuFontSize, FontStyle.Regular, GraphicsUnit.Point);
            _fontPercent = new Font(UiFonts.Family, 13f, FontStyle.Bold, GraphicsUnit.Point);
            _fontFooter = new Font(UiFonts.Family, 9f, FontStyle.Regular, GraphicsUnit.Point);
            _fontSmall = new Font(UiFonts.Family, 8f, FontStyle.Regular, GraphicsUnit.Point);
        }

        private void DisposeFonts()
        {
            if (_fontTitle != null) { _fontTitle.Dispose(); _fontTitle = null; }
            if (_fontSub != null) { _fontSub.Dispose(); _fontSub = null; }
            if (_fontName != null) { _fontName.Dispose(); _fontName = null; }
            if (_fontPercent != null) { _fontPercent.Dispose(); _fontPercent = null; }
            if (_fontFooter != null) { _fontFooter.Dispose(); _fontFooter = null; }
            if (_fontSmall != null) { _fontSmall.Dispose(); _fontSmall = null; }
        }

        private int Px(double value)
        {
            return (int)Math.Round(value * _scale);
        }

        private int RowHeight
        {
            get
            {
                if (_mode == "compact") return Px(44);
                int height = 60;
                if (!_config.ShowSource) height -= 6;
                if (!_config.ShowProgressBar) height -= 4;
                return Px(height);
            }
        }

        private int PopupWidth
        {
            get
            {
                if (_mode == "compact")
                {
                    return Px(Math.Min(300, Math.Max(240, _config.PopupWidth - 60)));
                }
                return Px(_config.PopupWidth);
            }
        }

        private void UpdateLayout()
        {
            int width = PopupWidth;
            int rowHeight = RowHeight;
            int height;

            if (_mode == "compact")
            {
                int connected = _devices.Count(device => device.Connected);
                int body = connected == 0 ? Px(56) : connected * rowHeight;
                height = Px(60) + body + Px(38);
            }
            else
            {
                int count = _devices.Count;
                int body = count == 0 ? Px(72) : count * rowHeight;
                height = Px(68) + body + Px(58);
            }

            int maxHeight = Screen.FromPoint(Cursor.Position).WorkingArea.Height - Px(24);
            if (height > maxHeight)
            {
                height = maxHeight;
            }

            if (ClientSize.Width != width || ClientSize.Height != height)
            {
                ClientSize = new Size(width, height);
                UpdateRegion();
            }

            Invalidate();
        }

        private void UpdateRegion()
        {
            using (GraphicsPath path = RoundedPath(0, 0, Width, Height, Px(_config.CornerRadius)))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        private void Reposition()
        {
            Point cursor = Cursor.Position;
            Screen screen = Screen.FromPoint(cursor);
            Rectangle work = screen.WorkingArea;

            int x = cursor.X - Width + Px(24);
            int y = cursor.Y - Height - Px(12);
            if (y < work.Top + Px(8))
            {
                y = cursor.Y + Px(24);
            }

            x = Math.Max(work.Left + Px(8), Math.Min(x, work.Right - Width - Px(8)));
            y = Math.Max(work.Top + Px(8), Math.Min(y, work.Bottom - Height - Px(8)));
            Location = new Point(x, y);
        }

        private void EnsureOnScreen()
        {
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            int margin = Px(8);
            int x = Math.Max(work.Left + margin, Math.Min(Location.X, work.Right - Width - margin));
            int y = Math.Max(work.Top + margin, Math.Min(Location.Y, work.Bottom - Height - margin));
            if (x != Location.X || y != Location.Y)
            {
                Location = new Point(x, y);
            }
        }

        private void OnDeactivate(object sender, EventArgs e)
        {
            if (_closedByDeactivate)
            {
                return;
            }

            _closedByDeactivate = true;
            if (ClosedByDeactivate != null)
            {
                ClosedByDeactivate(_mode);
            }
            Close();
        }

        private void OnMouseLeave(object sender, EventArgs e)
        {
            SetHover(string.Empty);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            string hover = string.Empty;
            if (_refreshRect.Contains(e.Location))
            {
                hover = "refresh";
            }
            else
            {
                foreach (RowHit row in _rows)
                {
                    if (row.Rect.Contains(e.Location))
                    {
                        hover = "row:" + row.Index;
                        break;
                    }
                }

                if (hover.Length == 0)
                {
                    foreach (FooterHit footer in _footers)
                    {
                        if (footer.Rect.Contains(e.Location))
                        {
                            hover = "footer:" + footer.Key;
                            break;
                        }
                    }
                }
            }

            SetHover(hover);
        }

        private void SetHover(string hover)
        {
            if (string.Equals(_hover, hover, StringComparison.Ordinal)) return;
            _hover = hover ?? string.Empty;
            Cursor = _hover.Length == 0 ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }

        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (_refreshRect.Contains(e.Location))
            {
                if (RefreshRequested != null) RefreshRequested();
                return;
            }

            foreach (RowHit row in _rows)
            {
                if (!row.Rect.Contains(e.Location)) continue;
                if (_mode == "compact")
                {
                    _mode = "full";
                    UpdateLayout();
                    Reposition();
                    Invalidate();
                }
                else
                {
                    if (RefreshRequested != null) RefreshRequested();
                }
                return;
            }

            foreach (FooterHit footer in _footers)
            {
                if (!footer.Rect.Contains(e.Location)) continue;
                switch (footer.Key)
                {
                    case "settings":
                        if (SettingsRequested != null) SettingsRequested();
                        break;
                    case "autostart":
                        if (AutoStartToggleRequested != null)
                        {
                            _autoStart = AutoStartToggleRequested();
                            Invalidate();
                        }
                        break;
                    case "bluetooth":
                        if (BluetoothRequested != null) BluetoothRequested();
                        break;
                    case "exit":
                        if (ExitRequested != null) ExitRequested();
                        break;
                }
                return;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int width = ClientSize.Width;
            int height = ClientSize.Height;

            using (SolidBrush background = new SolidBrush(_theme.Bg))
            {
                g.FillRectangle(background, 0, 0, width, height);
            }

            using (Pen borderPen = new Pen(_theme.Border, 1))
            using (GraphicsPath border = RoundedPath(0.5f, 0.5f, width - 1, height - 1, Px(_config.CornerRadius)))
            {
                g.DrawPath(borderPen, border);
            }

            TextFormatFlags leftFlags = TextFormatFlags.Left |
                                        TextFormatFlags.VerticalCenter |
                                        TextFormatFlags.NoPrefix;
            TextFormatFlags rightFlags = TextFormatFlags.Right |
                                         TextFormatFlags.VerticalCenter |
                                         TextFormatFlags.NoPrefix;
            TextFormatFlags centerFlags = TextFormatFlags.HorizontalCenter |
                                          TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.NoPrefix;
            TextFormatFlags ellipsisFlags = leftFlags | TextFormatFlags.EndEllipsis;

            string title = _mode == "compact" ? "已连接设备" : "外接设备电量";
            TextRenderer.DrawText(
                g, title, _fontTitle,
                new Rectangle(Px(18), Px(12), width - Px(110), Px(30)),
                _theme.Text, leftFlags);

            string subtitle = "正在读取设备...";
            Color subtitleColor = _theme.Muted;
            if (!string.IsNullOrEmpty(_lastError))
            {
                subtitle = "读取失败，点击刷新重试";
                subtitleColor = ThemeColors.FromHtml("#E5484D");
            }
            else if (_lastUpdate.HasValue)
            {
                subtitle = _lastUpdate.Value.ToString("HH:mm:ss") + " 更新";
                if (_mode != "compact")
                {
                    subtitle += " · 每 " + _config.RefreshSeconds + " 秒刷新";
                }
            }

            TextRenderer.DrawText(
                g, subtitle, _fontSub,
                new Rectangle(Px(18), Px(38), width - Px(110), Px(18)),
                subtitleColor, leftFlags);

            _refreshRect = new Rectangle(width - Px(78), Px(16), Px(60), Px(28));
            Color refreshColor = _hover == "refresh" ? _theme.Hover : _theme.Card;
            using (SolidBrush refreshBrush = new SolidBrush(refreshColor))
            using (GraphicsPath refreshPath = RoundedPath(
                _refreshRect.X, _refreshRect.Y, _refreshRect.Width, _refreshRect.Height, Px(8)))
            {
                g.FillPath(refreshBrush, refreshPath);
            }

            TextRenderer.DrawText(
                g, _refreshing ? "刷新中" : "刷新", _fontFooter,
                _refreshRect, _theme.Text, centerFlags);

            _rows.Clear();
            _footers.Clear();

            if (_mode == "compact")
            {
                DrawCompact(g, width, height, rightFlags, centerFlags, ellipsisFlags);
            }
            else
            {
                DrawFull(g, width, height, leftFlags, rightFlags, centerFlags, ellipsisFlags);
            }
        }

        private void DrawCompact(
            Graphics g, int width, int height,
            TextFormatFlags rightFlags, TextFormatFlags centerFlags, TextFormatFlags ellipsisFlags)
        {
            List<DeviceBatteryInfo> connected = _devices.Where(device => device.Connected).ToList();
            int rowHeight = RowHeight;
            int rowY = Px(62);

            if (connected.Count == 0)
            {
                TextRenderer.DrawText(
                    g, "暂无已连接设备", _fontName,
                    new Rectangle(Px(18), Px(70), width - Px(36), Px(44)),
                    _theme.Muted, centerFlags);
            }
            else
            {
                for (int index = 0; index < connected.Count; index++)
                {
                    DeviceBatteryInfo device = connected[index];
                    Rectangle rowRect = new Rectangle(Px(10), rowY, width - Px(20), rowHeight - Px(4));
                    _rows.Add(new RowHit(index, rowRect));

                    if (_hover == "row:" + index)
                    {
                        using (SolidBrush hoverBrush = new SolidBrush(_theme.Hover))
                        using (GraphicsPath hoverPath = RoundedPath(
                            rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height, Px(10)))
                        {
                            g.FillPath(hoverBrush, hoverPath);
                        }
                    }

                    Color levelColor = _theme.GetLevelColor(_config, device.Battery);
                    using (SolidBrush dotBrush = new SolidBrush(levelColor))
                    {
                        g.FillEllipse(dotBrush, Px(22), rowY + Px(16), Px(7), Px(7));
                    }

                    TextRenderer.DrawText(
                        g, device.Name, _fontName,
                        new Rectangle(Px(38), rowY + Px(6), width - Px(140), Px(24)),
                        _theme.Text, ellipsisFlags);

                    string percentText = "无数据";
                    Color percentColor = _theme.Muted;
                    Font percentFont = _fontName;
                    if (device.Battery.HasValue)
                    {
                        percentText = device.Battery.Value + "%";
                        percentColor = levelColor;
                        percentFont = _fontPercent;
                    }

                    TextRenderer.DrawText(
                        g, percentText, percentFont,
                        new Rectangle(width - Px(110), rowY + Px(6), Px(90), Px(26)),
                        percentColor, rightFlags);

                    rowY += rowHeight;
                }
            }

            TextRenderer.DrawText(
                g, "右键查看全部设备", _fontSmall,
                new Rectangle(0, height - Px(30), width, Px(20)),
                _theme.Muted, centerFlags);
        }

        private void DrawFull(
            Graphics g, int width, int height,
            TextFormatFlags leftFlags, TextFormatFlags rightFlags,
            TextFormatFlags centerFlags, TextFormatFlags ellipsisFlags)
        {
            int rowHeight = RowHeight;
            int rowY = Px(66);

            if (_devices.Count == 0)
            {
                string emptyText = _lastUpdate.HasValue ? "未发现可显示的外接设备" : "正在读取设备...";
                TextRenderer.DrawText(
                    g, emptyText, _fontName,
                    new Rectangle(Px(18), Px(88), width - Px(36), Px(44)),
                    _theme.Muted, centerFlags);
            }
            else
            {
                for (int index = 0; index < _devices.Count; index++)
                {
                    DeviceBatteryInfo device = _devices[index];
                    Rectangle rowRect = new Rectangle(Px(10), rowY, width - Px(20), rowHeight - Px(4));
                    _rows.Add(new RowHit(index, rowRect));

                    if (_hover == "row:" + index)
                    {
                        using (SolidBrush hoverBrush = new SolidBrush(_theme.Hover))
                        using (GraphicsPath hoverPath = RoundedPath(
                            rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height, Px(10)))
                        {
                            g.FillPath(hoverBrush, hoverPath);
                        }
                    }

                    bool hasBattery = device.Battery.HasValue;
                    Color levelColor = _theme.GetLevelColor(_config, device.Battery);
                    using (SolidBrush dotBrush = new SolidBrush(levelColor))
                    {
                        g.FillEllipse(dotBrush, Px(22), rowY + Px(16), Px(7), Px(7));
                    }

                    Color nameColor = device.Connected ? _theme.Text : _theme.Muted;
                    TextRenderer.DrawText(
                        g, device.Name, _fontName,
                        new Rectangle(Px(38), rowY + Px(5), width - Px(150), Px(22)),
                        nameColor, ellipsisFlags);

                    string percentText = "未连接";
                    Color percentColor = _theme.Muted;
                    Font percentFont = _fontName;
                    if (hasBattery)
                    {
                        percentText = device.Battery.Value + "%";
                        percentColor = levelColor;
                        percentFont = _fontPercent;
                    }
                    else if (device.Connected)
                    {
                        percentText = "无数据";
                    }

                    TextRenderer.DrawText(
                        g, percentText, percentFont,
                        new Rectangle(width - Px(120), rowY + Px(5), Px(100), Px(26)),
                        percentColor, rightFlags);

                    if (_config.ShowSource)
                    {
                        string sourceText = device.Detail ?? string.Empty;
                        if (device.Charging)
                        {
                            sourceText = "充电中 · " + sourceText;
                        }
                        if (!device.Connected && device.LastBattery.HasValue)
                        {
                            sourceText = "上次 " + device.LastBattery.Value + "%";
                            if (device.Updated.HasValue)
                            {
                                sourceText += " · " + device.Updated.Value.ToString("MM-dd HH:mm");
                            }
                        }
                        if (string.IsNullOrWhiteSpace(sourceText))
                        {
                            sourceText = device.Connected ? "在线" : "离线";
                        }

                        TextRenderer.DrawText(
                            g, sourceText, _fontSub,
                            new Rectangle(Px(38), rowY + Px(27), width - Px(160), Px(16)),
                            _theme.Muted, ellipsisFlags);
                    }

                    if (_config.ShowProgressBar && hasBattery)
                    {
                        int barX = Px(38);
                        int barY = rowY + rowHeight - Px(17);
                        int barWidth = width - Px(58);
                        using (SolidBrush trackBrush = new SolidBrush(_theme.Track))
                        using (GraphicsPath trackPath = RoundedPath(
                            barX, barY, barWidth, Px(4), Px(2)))
                        {
                            g.FillPath(trackBrush, trackPath);
                        }

                        int fillWidth = (int)Math.Round(barWidth * device.Battery.Value / 100.0);
                        if (fillWidth < Px(3)) fillWidth = Px(3);
                        using (SolidBrush fillBrush = new SolidBrush(levelColor))
                        using (GraphicsPath fillPath = RoundedPath(
                            barX, barY, fillWidth, Px(4), Px(2)))
                        {
                            g.FillPath(fillBrush, fillPath);
                        }
                    }

                    rowY += rowHeight;
                }
            }

            int footerY = height - Px(44);
            using (Pen separator = new Pen(_theme.Border, 1))
            {
                g.DrawLine(separator, Px(16), footerY - Px(10), width - Px(16), footerY - Px(10));
            }

            string[] keys = { "settings", "autostart", "bluetooth", "exit" };
            string[] labels = { "设置", _autoStart ? "自启 ✓" : "自启", "蓝牙", "退出" };
            int buttonWidth = Px(64);
            int gap = Px(8);
            int startX = (width - (4 * buttonWidth + 3 * gap)) / 2;

            for (int index = 0; index < keys.Length; index++)
            {
                Rectangle buttonRect = new Rectangle(
                    startX + index * (buttonWidth + gap), footerY, buttonWidth, Px(28));
                _footers.Add(new FooterHit(keys[index], buttonRect));

                if (_hover == "footer:" + keys[index])
                {
                    using (SolidBrush hoverBrush = new SolidBrush(_theme.Hover))
                    using (GraphicsPath hoverPath = RoundedPath(
                        buttonRect.X, buttonRect.Y, buttonRect.Width, buttonRect.Height, Px(8)))
                    {
                        g.FillPath(hoverBrush, hoverPath);
                    }
                }

                Color color = keys[index] == "exit" ? ThemeColors.FromHtml("#E5484D") : _theme.Text;
                TextRenderer.DrawText(
                    g, labels[index], _fontFooter, buttonRect, color, centerFlags);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            DisposeFonts();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeFonts();
            }
            base.Dispose(disposing);
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

        private sealed class RowHit
        {
            public RowHit(int index, Rectangle rect)
            {
                Index = index;
                Rect = rect;
            }

            public int Index { get; private set; }
            public Rectangle Rect { get; private set; }
        }

        private sealed class FooterHit
        {
            public FooterHit(string key, Rectangle rect)
            {
                Key = key;
                Rect = rect;
            }

            public string Key { get; private set; }
            public Rectangle Rect { get; private set; }
        }
    }
}
