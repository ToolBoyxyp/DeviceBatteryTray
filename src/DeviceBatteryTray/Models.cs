using System;

namespace DeviceBatteryTray
{
    public sealed class DeviceBatteryInfo
    {
        public string Name { get; set; }

        public string Address { get; set; }

        public bool Connected { get; set; }

        public int? Battery { get; set; }

        public int? LastBattery { get; set; }

        public bool Charging { get; set; }

        public string Source { get; set; }

        public DateTime? Updated { get; set; }

        public string Detail { get; set; }

        public DeviceBatteryInfo()
        {
            Name = string.Empty;
            Address = string.Empty;
            Source = string.Empty;
            Detail = string.Empty;
        }
    }
}
