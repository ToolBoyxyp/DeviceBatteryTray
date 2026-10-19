using System;
using System.Collections.Generic;
using System.Linq;

namespace DeviceBatteryTray
{
    internal sealed class BatteryReader
    {
        public List<DeviceBatteryInfo> ReadAll()
        {
            var entries = new List<DeviceBatteryInfo>();

            try
            {
                entries.AddRange(new BleBatterySource().Read());
            }
            catch
            {
            }

            try
            {
                entries.AddRange(new ClassicHfpBatterySource().Read());
            }
            catch
            {
            }

            try
            {
                entries.AddRange(new VxeHidBatterySource().Read());
            }
            catch
            {
            }

            return Merge(entries);
        }

        private static List<DeviceBatteryInfo> Merge(List<DeviceBatteryInfo> entries)
        {
            var merged = new List<DeviceBatteryInfo>();
            var groups = entries.GroupBy(
                item => string.IsNullOrWhiteSpace(item.Address) ? "NAME:" + item.Name : item.Address,
                StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, DeviceBatteryInfo> group in groups)
            {
                List<DeviceBatteryInfo> list = group.ToList();
                if (list.Count == 1)
                {
                    merged.Add(list[0]);
                    continue;
                }

                DeviceBatteryInfo ble = list.FirstOrDefault(item => item.Source == "BLE");
                DeviceBatteryInfo hfp = list.FirstOrDefault(item => item.Source == "HFP");
                DeviceBatteryInfo baseItem = ble ?? hfp ?? list[0];

                bool connected = list.Any(item => item.Connected);
                bool charging = list.Any(item => item.Charging);
                int? battery = null;
                int? lastBattery = null;
                DateTime? updated = null;
                string source = string.Empty;
                string detail = string.Empty;
                string name = baseItem.Name;

                if (ble != null && ble.Battery.HasValue)
                {
                    battery = ble.Battery;
                    updated = ble.Updated;
                    detail = ble.Detail;
                    source = "BLE";
                }
                else if (hfp != null && hfp.Battery.HasValue)
                {
                    battery = hfp.Battery;
                    updated = hfp.Updated;
                    detail = hfp.Detail;
                    source = "HFP";
                }
                else if (ble != null)
                {
                    updated = ble.Updated;
                    detail = ble.Detail;
                    source = "BLE";
                }
                else if (hfp != null)
                {
                    updated = hfp.Updated;
                    detail = hfp.Detail;
                    source = "HFP";
                }

                if (hfp != null && hfp.LastBattery.HasValue)
                {
                    lastBattery = hfp.LastBattery;
                }
                else if (ble != null && ble.LastBattery.HasValue)
                {
                    lastBattery = ble.LastBattery;
                }

                if (!battery.HasValue && hfp != null)
                {
                    lastBattery = hfp.LastBattery;
                    if (!updated.HasValue) updated = hfp.Updated;
                }

                merged.Add(new DeviceBatteryInfo
                {
                    Name = name,
                    Address = group.Key,
                    Connected = connected,
                    Battery = battery,
                    LastBattery = lastBattery,
                    Charging = charging,
                    Source = source,
                    Updated = updated,
                    Detail = detail
                });
            }

            return merged
                .OrderByDescending(item => item.Connected)
                .ThenBy(item => item.Battery == null)
                .ThenBy(item => item.Name, StringComparer.CurrentCulture)
                .ToList();
        }
    }
}
