using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeviceBatteryTray
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly BatteryReader _reader = new BatteryReader();
        private readonly Timer _refreshTimer;
        private readonly Timer _startupTimer;
        private Timer _previewTimer;
        private Timer _previewExitTimer;
        private AppConfig _config;
        private ThemeColors _theme;
        private List<DeviceBatteryInfo> _devices = new List<DeviceBatteryInfo>();
        private DateTime? _lastUpdate;
        private string _lastError;
        private bool _refreshing;
        private PopupForm _popup;
        private Icon _currentIcon;
        private DateTime _lastPopupClose = DateTime.MinValue;
        private string _lastPopupMode = string.Empty;
        private readonly bool _preview;

        public TrayApplicationContext(bool preview)
        {
            _preview = preview;
            _config = AppConfig.Load();
            _theme = ThemeColors.Create(_config.Theme);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "外接设备电量";
            _notifyIcon.Icon = TrayIconFactory.Create(null, _config, _theme);
            _currentIcon = _notifyIcon.Icon;
            _notifyIcon.Visible = true;
            _notifyIcon.MouseClick += OnTrayMouseClick;
            _notifyIcon.DoubleClick += OnTrayDoubleClick;

            _refreshTimer = new Timer();
            _refreshTimer.Interval = Math.Max(10, _config.RefreshSeconds) * 1000;
            _refreshTimer.Tick += (sender, e) => RefreshAsync();

            _startupTimer = new Timer();
            _startupTimer.Interval = 120;
            _startupTimer.Tick += OnStartupTick;
            _startupTimer.Start();

            if (_preview)
            {
                _devices = CreatePreviewData();
                _lastUpdate = DateTime.Now;
                UpdateTray();

                _previewTimer = new Timer();
                _previewTimer.Interval = 500;
                _previewTimer.Tick += (sender, e) =>
                {
                    _previewTimer.Stop();
                    ShowPopup("full");
                };
                _previewTimer.Start();

                _previewExitTimer = new Timer();
                _previewExitTimer.Interval = 6000;
                _previewExitTimer.Tick += (sender, e) =>
                {
                    _previewExitTimer.Stop();
                    ExitApp();
                };
                _previewExitTimer.Start();
            }
        }

        private void OnStartupTick(object sender, EventArgs e)
        {
            _startupTimer.Stop();
            _startupTimer.Dispose();
            if (_preview)
            {
                return;
            }

            _refreshTimer.Start();
            RefreshAsync();
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowPopup("compact");
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowPopup("full");
            }
        }

        private void OnTrayDoubleClick(object sender, EventArgs e)
        {
            RefreshAsync();
        }

        private async void RefreshAsync()
        {
            if (_refreshing || _preview)
            {
                return;
            }

            _refreshing = true;
            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.SetRefreshing(true);
            }

            try
            {
                _devices = await Task.Run(() => _reader.ReadAll());
                _lastUpdate = DateTime.Now;
                _lastError = null;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _lastUpdate = DateTime.Now;
            }
            finally
            {
                _refreshing = false;
                UpdateTray();
                if (_popup != null && !_popup.IsDisposed)
                {
                    _popup.SetRefreshing(false);
                    _popup.UpdateData(_devices, _lastUpdate, _lastError, _config, _theme);
                }
            }
        }

        private void UpdateTray()
        {
            int? minimum = null;
            foreach (DeviceBatteryInfo device in _devices)
            {
                if (device.Battery.HasValue &&
                    (!minimum.HasValue || device.Battery.Value < minimum.Value))
                {
                    minimum = device.Battery;
                }
            }

            Icon icon = TrayIconFactory.Create(minimum, _config, _theme);
            Icon old = _notifyIcon.Icon;
            _notifyIcon.Icon = icon;
            _currentIcon = icon;
            if (old != null)
            {
                old.Dispose();
            }

            List<string> parts = new List<string>();
            foreach (DeviceBatteryInfo device in _devices)
            {
                if (device.Battery.HasValue)
                {
                    string part = device.Name + " " + device.Battery.Value + "%";
                    if (device.Charging)
                    {
                        part += " 充电中";
                    }
                    parts.Add(part);
                }
            }

            string text;
            if (parts.Count > 0)
            {
                text = string.Join(" | ", parts);
            }
            else if (!string.IsNullOrEmpty(_lastError))
            {
                text = "外接设备电量：读取失败";
            }
            else if (_devices.Count > 0)
            {
                text = "外接设备电量：暂无在线设备";
            }
            else
            {
                text = "外接设备电量：正在读取...";
            }

            if (text.Length > 63)
            {
                text = text.Substring(0, 60) + "...";
            }
            _notifyIcon.Text = text;
        }

        private void ShowPopup(string mode)
        {
            if (mode != "compact" && mode != "full")
            {
                mode = "full";
            }

            if (_popup != null && !_popup.IsDisposed && _popup.Visible)
            {
                if (string.Equals(_popup.Mode, mode, StringComparison.Ordinal))
                {
                    _popup.Close();
                    _lastPopupClose = DateTime.Now;
                    _lastPopupMode = mode;
                    return;
                }

                _popup.Close();
                _lastPopupClose = DateTime.MinValue;
                _popup = null;
            }

            if ((DateTime.Now - _lastPopupClose).TotalMilliseconds < 300 &&
                string.Equals(mode, _lastPopupMode, StringComparison.Ordinal))
            {
                return;
            }

            PopupForm popup = new PopupForm(
                mode, _config, _theme, _devices, _lastUpdate, _lastError, IsAutoStartEnabled());
            popup.RefreshRequested = () => RefreshAsync();
            popup.SettingsRequested = OpenSettings;
            popup.AutoStartToggleRequested = ToggleAutoStart;
            popup.BluetoothRequested = OpenBluetoothSettings;
            popup.ExitRequested = ExitApp;
            popup.ClosedByDeactivate = m =>
            {
                _lastPopupClose = DateTime.Now;
                _lastPopupMode = m;
            };
            popup.FormClosed += (sender, e) =>
            {
                if (ReferenceEquals(_popup, sender))
                {
                    _popup = null;
                }
            };

            _popup = popup;
            popup.ShowNearCursor();
        }

        private void OpenSettings()
        {
            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.Close();
                _popup = null;
            }

            using (SettingsForm form = new SettingsForm(_config, _theme))
            {
                if (form.ShowDialog() == DialogResult.OK && form.Result != null)
                {
                    _config = form.Result;
                    _config.Normalize();
                    _config.Save();
                    ApplyConfig();
                }
            }
        }

        private void ApplyConfig()
        {
            _theme = ThemeColors.Create(_config.Theme);
            _refreshTimer.Interval = Math.Max(10, _config.RefreshSeconds) * 1000;
            UpdateTray();

            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.Close();
                _popup = null;
            }
        }

        private bool ToggleAutoStart()
        {
            bool enabled = !IsAutoStartEnabled();
            SetAutoStart(enabled);
            return enabled;
        }

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    return key != null && key.GetValue("DeviceBatteryTray") != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void SetAutoStart(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (key == null)
                    {
                        return;
                    }

                    if (enabled)
                    {
                        key.SetValue("DeviceBatteryTray", "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue("DeviceBatteryTray", false);
                    }
                }
            }
            catch
            {
            }
        }

        private static void OpenBluetoothSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:bluetooth")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private void ExitApp()
        {
            _refreshTimer.Stop();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();

            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.Close();
            }

            if (_currentIcon != null)
            {
                _currentIcon.Dispose();
                _currentIcon = null;
            }

            ExitThread();
        }

        private static List<DeviceBatteryInfo> CreatePreviewData()
        {
            List<DeviceBatteryInfo> list = new List<DeviceBatteryInfo>();
            list.Add(new DeviceBatteryInfo
            {
                Name = "ROSE OpenFeel MK2",
                Connected = true,
                Battery = 60,
                LastBattery = 60,
                Source = "HFP",
                Updated = DateTime.Now,
                Detail = "经典蓝牙 HFP"
            });
            list.Add(new DeviceBatteryInfo
            {
                Name = "VXE 无线鼠标（2.4G）",
                Connected = true,
                Battery = 95,
                LastBattery = 95,
                Source = "HID",
                Updated = DateTime.Now,
                Detail = "2.4G · 4076 mV"
            });
            list.Add(new DeviceBatteryInfo
            {
                Name = "vivo TWS 4",
                Connected = false,
                Battery = null,
                LastBattery = 100,
                Source = "BLE",
                Updated = DateTime.Now.AddDays(-4),
                Detail = "蓝牙 LE"
            });
            list.Add(new DeviceBatteryInfo
            {
                Name = "VGN V98pro BT1",
                Connected = false,
                Battery = null,
                LastBattery = null,
                Source = "BLE",
                Detail = "蓝牙 LE"
            });
            list.Add(new DeviceBatteryInfo
            {
                Name = "Xbox Wireless Controller",
                Connected = false,
                Battery = null,
                LastBattery = null,
                Source = "BLE",
                Detail = "蓝牙 LE"
            });
            return list;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_refreshTimer != null) _refreshTimer.Dispose();
                if (_startupTimer != null) _startupTimer.Dispose();
                if (_previewTimer != null) _previewTimer.Dispose();
                if (_previewExitTimer != null) _previewExitTimer.Dispose();
                if (_notifyIcon != null) _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
