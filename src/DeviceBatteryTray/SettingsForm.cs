using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeviceBatteryTray
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppConfig _working;
        private readonly ThemeColors _theme;
        private ComboBox _themeCombo;
        private ComboBox _iconCombo;
        private Button _accentButton;
        private CheckBox _colorByLevelCheck;
        private NumericUpDown _warnNum;
        private NumericUpDown _critNum;
        private CheckBox _progressCheck;
        private CheckBox _sourceCheck;
        private NumericUpDown _refreshNum;
        private NumericUpDown _fontNum;
        private NumericUpDown _widthNum;
        private NumericUpDown _cornerNum;

        public AppConfig Result { get; private set; }

        public SettingsForm(AppConfig config, ThemeColors theme)
        {
            _working = config.Clone();
            _theme = theme;

            Text = "外观设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 556);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = theme.Bg;
            ForeColor = theme.Text;
            Font = new Font(UiFonts.Family, 9f, FontStyle.Regular, GraphicsUnit.Point);

            BuildUi();
        }

        private void BuildUi()
        {
            Label title = new Label();
            title.Text = "外观设置";
            title.Font = new Font(UiFonts.Family, 13f, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = _theme.Text;
            title.AutoSize = true;
            title.Location = new Point(20, 16);
            Controls.Add(title);

            Label description = new Label();
            description.Text = "修改后立即生效，配置保存在 %APPDATA%\\DeviceBatteryTray\\settings.json";
            description.ForeColor = _theme.Muted;
            description.AutoSize = true;
            description.Location = new Point(22, 46);
            Controls.Add(description);

            AddLabel("主题", 22, 88);
            AddLabel("图标样式", 22, 124);
            AddLabel("强调色", 22, 160);
            AddLabel("警告阈值", 22, 228);
            AddLabel("严重阈值", 238, 228);
            AddLabel("刷新间隔", 22, 330);
            AddLabel("字号", 22, 366);
            AddLabel("弹窗宽度", 22, 402);
            AddLabel("圆角", 22, 438);

            _themeCombo = new ComboBox();
            _themeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _themeCombo.Items.AddRange(new object[] { "深色", "浅色", "跟随系统" });
            _themeCombo.Location = new Point(150, 84);
            _themeCombo.Width = 220;
            _themeCombo.FlatStyle = FlatStyle.Flat;
            _themeCombo.BackColor = _theme.Card;
            _themeCombo.ForeColor = _theme.Text;
            if (string.Equals(_working.Theme, "light", StringComparison.OrdinalIgnoreCase))
            {
                _themeCombo.SelectedIndex = 1;
            }
            else if (string.Equals(_working.Theme, "system", StringComparison.OrdinalIgnoreCase))
            {
                _themeCombo.SelectedIndex = 2;
            }
            else
            {
                _themeCombo.SelectedIndex = 0;
            }
            Controls.Add(_themeCombo);

            _iconCombo = new ComboBox();
            _iconCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _iconCombo.Items.AddRange(new object[] { "电池", "圆环", "数字" });
            _iconCombo.Location = new Point(150, 120);
            _iconCombo.Width = 220;
            _iconCombo.FlatStyle = FlatStyle.Flat;
            _iconCombo.BackColor = _theme.Card;
            _iconCombo.ForeColor = _theme.Text;
            if (string.Equals(_working.IconStyle, "ring", StringComparison.OrdinalIgnoreCase))
            {
                _iconCombo.SelectedIndex = 1;
            }
            else if (string.Equals(_working.IconStyle, "number", StringComparison.OrdinalIgnoreCase))
            {
                _iconCombo.SelectedIndex = 2;
            }
            else
            {
                _iconCombo.SelectedIndex = 0;
            }
            Controls.Add(_iconCombo);

            _accentButton = new Button();
            _accentButton.Text = _working.AccentColor;
            _accentButton.Location = new Point(150, 156);
            _accentButton.Size = new Size(220, 28);
            _accentButton.FlatStyle = FlatStyle.Flat;
            _accentButton.BackColor = _theme.GetAccent(_working);
            _accentButton.ForeColor = ThemeColors.GetContrast(_accentButton.BackColor);
            _accentButton.Click += OnAccentClick;
            Controls.Add(_accentButton);

            _colorByLevelCheck = new CheckBox();
            _colorByLevelCheck.Text = "按电量变色（绿 / 橙 / 红）";
            _colorByLevelCheck.AutoSize = true;
            _colorByLevelCheck.Location = new Point(22, 196);
            _colorByLevelCheck.ForeColor = _theme.Text;
            _colorByLevelCheck.BackColor = _theme.Bg;
            _colorByLevelCheck.Checked = _working.ColorByLevel;
            Controls.Add(_colorByLevelCheck);

            _warnNum = CreateNumeric(150, 226, 70, 5, 90, 1, _working.WarnThreshold);
            _critNum = CreateNumeric(320, 226, 50, 1, 50, 1, _working.CriticalThreshold);

            _progressCheck = new CheckBox();
            _progressCheck.Text = "显示电量进度条";
            _progressCheck.AutoSize = true;
            _progressCheck.Location = new Point(22, 264);
            _progressCheck.ForeColor = _theme.Text;
            _progressCheck.BackColor = _theme.Bg;
            _progressCheck.Checked = _working.ShowProgressBar;
            Controls.Add(_progressCheck);

            _sourceCheck = new CheckBox();
            _sourceCheck.Text = "显示设备来源 / 更新时间";
            _sourceCheck.AutoSize = true;
            _sourceCheck.Location = new Point(22, 294);
            _sourceCheck.ForeColor = _theme.Text;
            _sourceCheck.BackColor = _theme.Bg;
            _sourceCheck.Checked = _working.ShowSource;
            Controls.Add(_sourceCheck);

            _refreshNum = CreateNumeric(150, 328, 100, 10, 300, 1, _working.RefreshSeconds);
            _fontNum = CreateNumeric(150, 364, 100, 8, 12, 1, (decimal)_working.MenuFontSize);
            _fontNum.DecimalPlaces = 1;
            _fontNum.Increment = 0.5m;
            _widthNum = CreateNumeric(150, 400, 100, 280, 520, 10, _working.PopupWidth);
            _cornerNum = CreateNumeric(150, 436, 100, 0, 24, 2, _working.CornerRadius);

            Label hint = new Label();
            hint.Text = "提示：也可以直接编辑 settings.json 后重启程序。";
            hint.ForeColor = _theme.Muted;
            hint.AutoSize = true;
            hint.Location = new Point(22, 472);
            Controls.Add(hint);

            Button saveButton = new Button();
            saveButton.Text = "保存";
            saveButton.Location = new Point(196, 502);
            saveButton.Size = new Size(86, 32);
            saveButton.FlatStyle = FlatStyle.Flat;
            saveButton.BackColor = _theme.GetAccent(_working);
            saveButton.ForeColor = ThemeColors.GetContrast(saveButton.BackColor);
            saveButton.Click += OnSaveClick;
            Controls.Add(saveButton);

            Button cancelButton = new Button();
            cancelButton.Text = "取消";
            cancelButton.Location = new Point(292, 502);
            cancelButton.Size = new Size(86, 32);
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.BackColor = _theme.Card;
            cancelButton.ForeColor = _theme.Text;
            cancelButton.FlatAppearance.BorderColor = _theme.Border;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void AddLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = _theme.Muted;
            label.AutoSize = true;
            label.Location = new Point(x, y);
            Controls.Add(label);
        }

        private NumericUpDown CreateNumeric(
            int x, int y, int width, decimal minimum, decimal maximum,
            decimal increment, decimal value)
        {
            NumericUpDown control = new NumericUpDown();
            control.Minimum = minimum;
            control.Maximum = maximum;
            control.Increment = increment;
            control.Value = Math.Max(minimum, Math.Min(maximum, value));
            control.Location = new Point(x, y);
            control.Width = width;
            control.BackColor = _theme.Card;
            control.ForeColor = _theme.Text;
            control.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(control);
            return control;
        }

        private void OnAccentClick(object sender, EventArgs e)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = _accentButton.BackColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _accentButton.BackColor = dialog.Color;
                    _accentButton.ForeColor = ThemeColors.GetContrast(dialog.Color);
                    _accentButton.Text = string.Format(
                        "#{0:X2}{1:X2}{2:X2}", dialog.Color.R, dialog.Color.G, dialog.Color.B);
                }
            }
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            int warn = (int)_warnNum.Value;
            int critical = (int)_critNum.Value;
            if (critical > warn)
            {
                int swap = critical;
                critical = warn;
                warn = swap;
            }

            string[] themes = { "dark", "light", "system" };
            string[] icons = { "battery", "ring", "number" };
            Color accent = _accentButton.BackColor;

            AppConfig updated = _working.Clone();
            updated.Theme = themes[Math.Max(0, _themeCombo.SelectedIndex)];
            updated.IconStyle = icons[Math.Max(0, _iconCombo.SelectedIndex)];
            updated.AccentColor = string.Format("#{0:X2}{1:X2}{2:X2}", accent.R, accent.G, accent.B);
            updated.ColorByLevel = _colorByLevelCheck.Checked;
            updated.WarnThreshold = warn;
            updated.CriticalThreshold = critical;
            updated.ShowProgressBar = _progressCheck.Checked;
            updated.ShowSource = _sourceCheck.Checked;
            updated.RefreshSeconds = (int)_refreshNum.Value;
            updated.MenuFontSize = (double)_fontNum.Value;
            updated.PopupWidth = (int)_widthNum.Value;
            updated.CornerRadius = (int)_cornerNum.Value;
            updated.Normalize();

            Result = updated;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
