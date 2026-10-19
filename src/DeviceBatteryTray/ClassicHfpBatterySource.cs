using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DeviceBatteryTray
{
    internal sealed class ClassicHfpBatterySource
    {
        private const int DIGCF_PRESENT = 0x2;
        private const int DIGCF_ALLCLASSES = 0x4;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        private static readonly Guid BatteryPropertyGuid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5");
        private static readonly Regex AddressRegex = new Regex(@"&0&([0-9A-Fa-f]{12})_C", RegexOptions.Compiled);

        public List<DeviceBatteryInfo> Read()
        {
            var result = new List<DeviceBatteryInfo>();
            Dictionary<string, bool> classicStatus = GetClassicStatus();
            IntPtr set = IntPtr.Zero;

            try
            {
                set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
                if (set == IntPtr.Zero || set == new IntPtr(-1))
                {
                    return result;
                }

                for (int index = 0; ; index++)
                {
                    SP_DEVINFO_DATA data = new SP_DEVINFO_DATA();
                    data.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                    if (!SetupDiEnumDeviceInfo(set, index, ref data))
                    {
                        break;
                    }

                    StringBuilder instanceBuilder = new StringBuilder(1024);
                    int required;
                    if (!SetupDiGetDeviceInstanceId(
                        set, ref data, instanceBuilder, instanceBuilder.Capacity, out required))
                    {
                        continue;
                    }

                    string instanceId = instanceBuilder.ToString();
                    if (!instanceId.StartsWith(@"BTHENUM\{0000111E", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int? battery = GetIntProperty(set, ref data, BatteryPropertyGuid, 2);
                    if (!battery.HasValue)
                    {
                        continue;
                    }

                    DateTime? updated = GetTimeProperty(set, ref data, BatteryPropertyGuid, 7);

                    string address = string.Empty;
                    Match match = AddressRegex.Match(instanceId);
                    if (match.Success)
                    {
                        address = match.Groups[1].Value.ToUpperInvariant();
                    }

                    bool connected = false;
                    if (!string.IsNullOrEmpty(address) && classicStatus.ContainsKey(address))
                    {
                        connected = classicStatus[address];
                    }

                    string name = GetFriendlyName(set, ref data);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = "蓝牙耳机";
                    }
                    name = Regex.Replace(name, @"\s+Hands-Free AG$", string.Empty, RegexOptions.IgnoreCase);

                    result.Add(new DeviceBatteryInfo
                    {
                        Name = name,
                        Address = address,
                        Connected = connected,
                        Battery = connected ? battery : (int?)null,
                        LastBattery = battery,
                        Charging = false,
                        Source = "HFP",
                        Updated = updated,
                        Detail = "经典蓝牙 HFP"
                    });
                }
            }
            catch
            {
            }
            finally
            {
                if (set != IntPtr.Zero && set != new IntPtr(-1))
                {
                    SetupDiDestroyDeviceInfoList(set);
                }
            }

            return result;
        }

        private static Dictionary<string, bool> GetClassicStatus()
        {
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                DeviceInformationCollection devices = WithTimeout(
                    DeviceInformation.FindAllAsync(selector), 8000).GetAwaiter().GetResult();
                if (devices == null)
                {
                    return map;
                }

                foreach (DeviceInformation info in devices)
                {
                    BluetoothDevice device = null;
                    try
                    {
                        device = WithTimeout(
                            BluetoothDevice.FromIdAsync(info.Id), 4000).GetAwaiter().GetResult();
                        if (device != null)
                        {
                            map[device.BluetoothAddress.ToString("X12")] =
                                device.ConnectionStatus == BluetoothConnectionStatus.Connected;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        if (device != null)
                        {
                            device.Dispose();
                        }
                    }
                }
            }
            catch
            {
            }

            return map;
        }

        private static async Task<T> WithTimeout<T>(Windows.Foundation.IAsyncOperation<T> operation, int milliseconds)
        {
            if (operation == null)
            {
                return default(T);
            }

            Task<T> task = operation.AsTask();
            Task completed = await Task.WhenAny(task, Task.Delay(milliseconds)).ConfigureAwait(false);
            if (completed != task)
            {
                return default(T);
            }

            return await task.ConfigureAwait(false);
        }

        private static string GetFriendlyName(IntPtr set, ref SP_DEVINFO_DATA data)
        {
            try
            {
                byte[] buffer = new byte[1024];
                uint type;
                int required;
                if (SetupDiGetDeviceRegistryProperty(
                    set, ref data, SPDRP_FRIENDLYNAME, out type, buffer, buffer.Length, out required) &&
                    required > 2)
                {
                    return Encoding.Unicode.GetString(buffer, 0, required - 2).TrimEnd('\0');
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static int? GetIntProperty(IntPtr set, ref SP_DEVINFO_DATA data, Guid guid, uint pid)
        {
            try
            {
                DEVPROPKEY key = new DEVPROPKEY { fmtid = guid, pid = pid };
                byte[] buffer = new byte[1024];
                uint type;
                int required;
                if (!SetupDiGetDeviceProperty(
                    set, ref data, ref key, out type, buffer, buffer.Length, out required, 0))
                {
                    return null;
                }

                switch (type)
                {
                    case 3:
                        return buffer[0];
                    case 4:
                        return BitConverter.ToUInt16(buffer, 0);
                    case 6:
                        return BitConverter.ToInt32(buffer, 0);
                    case 7:
                        return (int)BitConverter.ToUInt32(buffer, 0);
                    case 0x12:
                        int parsed;
                        if (int.TryParse(
                            Encoding.Unicode.GetString(buffer, 0, Math.Max(0, required - 2)).TrimEnd('\0'),
                            out parsed))
                        {
                            return parsed;
                        }
                        return null;
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? GetTimeProperty(IntPtr set, ref SP_DEVINFO_DATA data, Guid guid, uint pid)
        {
            try
            {
                DEVPROPKEY key = new DEVPROPKEY { fmtid = guid, pid = pid };
                byte[] buffer = new byte[1024];
                uint type;
                int required;
                if (!SetupDiGetDeviceProperty(
                    set, ref data, ref key, out type, buffer, buffer.Length, out required, 0))
                {
                    return null;
                }

                if (type == 0x40 && required >= 8)
                {
                    long fileTime = BitConverter.ToInt64(buffer, 0);
                    return DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
                }

                if (type == 0x12)
                {
                    DateTime parsed;
                    string text = Encoding.Unicode.GetString(
                        buffer, 0, Math.Max(0, required - 2)).TrimEnd('\0');
                    if (DateTime.TryParse(text, out parsed))
                    {
                        return parsed;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public int DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            IntPtr classGuid, string enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet, int memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceProperty(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, ref DEVPROPKEY propertyKey,
            out uint propertyType, byte[] propertyBuffer, int propertyBufferSize,
            out int requiredSize, int flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property,
            out uint propertyRegDataType, byte[] propertyBuffer, int propertyBufferSize,
            out int requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    }
}
