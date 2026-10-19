using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace DeviceBatteryTray
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool testMode = Array.Exists(args, a => string.Equals(a, "--test", StringComparison.OrdinalIgnoreCase));
            bool previewMode = Array.Exists(args, a => string.Equals(a, "--preview", StringComparison.OrdinalIgnoreCase));
            bool settingsPreview = Array.Exists(args, a => string.Equals(a, "--settings-preview", StringComparison.OrdinalIgnoreCase));

            try
            {
                NativeMethods.SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch
            {
            }

            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            }
            catch
            {
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (previewMode)
            {
                PopupForm.DiagnosticsEnabled = true;
            }

            if (testMode)
            {
                RunTest();
                return;
            }

            if (settingsPreview)
            {
                RunSettingsPreview();
                return;
            }

            using (var mutex = new System.Threading.Mutex(false, @"Local\DeviceBatteryTray_SingleInstance"))
            {
                bool owns = false;
                try
                {
                    owns = mutex.WaitOne(0);
                }
                catch (System.Threading.AbandonedMutexException)
                {
                    owns = true;
                }

                if (!owns)
                {
                    return;
                }

                try
                {
                    Application.Run(new TrayApplicationContext(previewMode));
                }
                finally
                {
                    try
                    {
                        mutex.ReleaseMutex();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void RunSettingsPreview()
        {
            AppConfig config = AppConfig.Load();
            using (SettingsForm form = new SettingsForm(config, ThemeColors.Create(config.Theme)))
            {
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 1500;
                timer.Tick += (sender, e) =>
                {
                    timer.Stop();
                    form.Close();
                };
                timer.Start();
                form.ShowDialog();
            }
        }

        private static void RunTest()
        {
            var lines = new List<string>();
            try
            {
                var reader = new BatteryReader();
                var devices = reader.ReadAll();
                foreach (var device in devices)
                {
                    lines.Add(string.Format(
                        "{0}\tConnected={1}\tBattery={2}\tCharging={3}\tSource={4}\tDetail={5}",
                        device.Name,
                        device.Connected,
                        device.Battery.HasValue ? device.Battery.Value + "%" : "--",
                        device.Charging,
                        device.Source,
                        device.Detail));
                }
            }
            catch (Exception ex)
            {
                lines.Add("ERROR: " + ex);
            }

            string path = Path.Combine(Path.GetTempPath(), "DeviceBatteryTray_test.txt");
            File.WriteAllLines(path, lines);
        }
    }
}
