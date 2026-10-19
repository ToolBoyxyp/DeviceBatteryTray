using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceBatteryTray
{
    internal sealed class VxeHidBatterySource
    {
        private const int DIGCF_PRESENT = 0x02;
        private const int DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 1;
        private const uint FILE_SHARE_WRITE = 2;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        private const int ERROR_IO_PENDING = 997;

        public List<DeviceBatteryInfo> Read()
        {
            var result = new List<DeviceBatteryInfo>();
            VxeHidTarget target = null;
            foreach (VxeHidTarget candidate in EnumerateTargets())
            {
                if (candidate.OutputReportLength >= 17)
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
            {
                return result;
            }

            VxeBatteryResult battery = QueryBattery(target.Path);
            if (battery.Success)
            {
                result.Add(new DeviceBatteryInfo
                {
                    Name = "VXE 无线鼠标（2.4G）",
                    Address = "HID:3554",
                    Connected = true,
                    Battery = battery.Capacity,
                    LastBattery = battery.Capacity,
                    Charging = battery.Charging,
                    Source = "HID",
                    Updated = DateTime.Now,
                    Detail = string.Format("2.4G · {0} mV", battery.Voltage)
                });
            }
            else
            {
                result.Add(new DeviceBatteryInfo
                {
                    Name = "VXE 无线鼠标（2.4G）",
                    Address = "HID:3554",
                    Connected = false,
                    Battery = null,
                    LastBattery = null,
                    Charging = false,
                    Source = "HID",
                    Updated = null,
                    Detail = "接收器在线，鼠标未响应"
                });
            }

            return result;
        }

        private static List<VxeHidTarget> EnumerateTargets()
        {
            var list = new List<VxeHidTarget>();
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            IntPtr set = SetupDiGetClassDevs(
                ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == IntPtr.Zero || set == new IntPtr(-1))
            {
                return list;
            }

            try
            {
                SP_DEVICE_INTERFACE_DATA data = new SP_DEVICE_INTERFACE_DATA();
                data.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

                for (int index = 0; ; index++)
                {
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref data))
                    {
                        break;
                    }

                    int required;
                    SP_DEVICE_INTERFACE_DETAIL_DATA detail = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                    detail.cbSize = IntPtr.Size == 8 ? 8 : 6;
                    if (!SetupDiGetDeviceInterfaceDetail(
                        set, ref data, ref detail, Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA)),
                        out required, IntPtr.Zero))
                    {
                        continue;
                    }

                    IntPtr handle = CreateFile(
                        detail.DevicePath, GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (handle == new IntPtr(-1))
                    {
                        continue;
                    }

                    try
                    {
                        HIDD_ATTRIBUTES attributes = new HIDD_ATTRIBUTES();
                        attributes.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                        if (!HidD_GetAttributes(handle, ref attributes))
                        {
                            continue;
                        }

                        if (attributes.VendorID != 0x3554)
                        {
                            continue;
                        }

                        IntPtr preparsed;
                        HIDP_CAPS caps = new HIDP_CAPS();
                        if (!HidD_GetPreparsedData(handle, out preparsed))
                        {
                            continue;
                        }

                        HidP_GetCaps(preparsed, ref caps);
                        HidD_FreePreparsedData(preparsed);

                        list.Add(new VxeHidTarget
                        {
                            Path = detail.DevicePath,
                            VendorId = attributes.VendorID,
                            ProductId = attributes.ProductID,
                            UsagePage = caps.UsagePage,
                            Usage = caps.Usage,
                            InputReportLength = caps.InputReportByteLength,
                            OutputReportLength = caps.OutputReportByteLength
                        });
                    }
                    finally
                    {
                        CloseHandle(handle);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }

            return list;
        }

        private static VxeBatteryResult QueryBattery(string path)
        {
            byte[] request = new byte[17]
            {
                0x08, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x49
            };

            var result = new VxeBatteryResult();
            IntPtr handle = CreateFile(
                path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                return result;
            }

            IntPtr ev = CreateEvent(IntPtr.Zero, true, false, null);
            try
            {
                if (!HidD_SetOutputReport(handle, request, request.Length))
                {
                    return result;
                }

                for (int attempt = 0; attempt < 8; attempt++)
                {
                    byte[] buffer = new byte[64];
                    OVERLAPPED overlapped = new OVERLAPPED();
                    overlapped.hEvent = ev;
                    ResetEvent(ev);

                    int read;
                    bool ok = ReadFile(handle, buffer, buffer.Length, out read, ref overlapped);
                    if (!ok)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ERROR_IO_PENDING)
                        {
                            uint wait = WaitForSingleObject(ev, 1200);
                            if (wait != 0)
                            {
                                break;
                            }

                            if (!GetOverlappedResult(handle, ref overlapped, out read, false))
                            {
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (read >= 10 && buffer[0] == 8 && buffer[1] == 4)
                    {
                        result.Success = true;
                        result.Capacity = buffer[6];
                        result.Charging = buffer[7] != 0;
                        result.Voltage = (buffer[8] << 8) | buffer[9];
                        break;
                    }
                }
            }
            finally
            {
                CloseHandle(ev);
                CloseHandle(handle);
            }

            return result;
        }

        private sealed class VxeHidTarget
        {
            public string Path;
            public ushort VendorId;
            public ushort ProductId;
            public ushort UsagePage;
            public ushort Usage;
            public ushort InputReportLength;
            public ushort OutputReportLength;
        }

        private sealed class VxeBatteryResult
        {
            public bool Success;
            public int Capacity;
            public bool Charging;
            public int Voltage;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
            public string DevicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OVERLAPPED
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public int Offset;
            public int OffsetHigh;
            public IntPtr hEvent;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
            int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("hid.dll")]
        private static extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll")]
        private static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll")]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, ref HIDP_CAPS capabilities);

        [DllImport("hid.dll")]
        private static extern bool HidD_SetOutputReport(
            IntPtr hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToRead,
            out int lpNumberOfBytesRead, ref OVERLAPPED lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetOverlappedResult(
            IntPtr hFile, ref OVERLAPPED lpOverlapped,
            out int lpNumberOfBytesTransferred, bool bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateEvent(
            IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ResetEvent(IntPtr hEvent);
    }
}
