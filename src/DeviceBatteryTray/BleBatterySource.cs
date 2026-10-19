using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace DeviceBatteryTray
{
    internal sealed class BleBatterySource
    {
        private static readonly Guid BatteryServiceUuid = new Guid("0000180f-0000-1000-8000-00805f9b34fb");
        private static readonly Guid BatteryLevelUuid = new Guid("00002a19-0000-1000-8000-00805f9b34fb");

        public List<DeviceBatteryInfo> Read()
        {
            return ReadAsync().GetAwaiter().GetResult();
        }

        private async Task<List<DeviceBatteryInfo>> ReadAsync()
        {
            var result = new List<DeviceBatteryInfo>();
            string selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
            DeviceInformationCollection paired = await WithTimeout(
                DeviceInformation.FindAllAsync(selector), 8000).ConfigureAwait(false);
            if (paired == null)
            {
                return result;
            }

            foreach (DeviceInformation info in paired)
            {
                BluetoothLEDevice device = null;
                try
                {
                    device = await WithTimeout(
                        BluetoothLEDevice.FromIdAsync(info.Id), 5000).ConfigureAwait(false);
                    if (device == null)
                    {
                        continue;
                    }

                    string address = device.BluetoothAddress.ToString("X12");
                    bool connected = device.ConnectionStatus == BluetoothConnectionStatus.Connected;
                    int? battery = null;
                    DateTime? updated = null;

                    if (connected)
                    {
                        var values = new List<int>();
                        GattDeviceServicesResult serviceResult = await WithTimeout(
                            device.GetGattServicesForUuidAsync(BatteryServiceUuid, BluetoothCacheMode.Uncached), 8000).ConfigureAwait(false);

                        if (serviceResult != null &&
                            serviceResult.Status == GattCommunicationStatus.Success &&
                            serviceResult.Services != null)
                        {
                            foreach (GattDeviceService service in serviceResult.Services)
                            {
                                try
                                {
                                    GattCharacteristicsResult charResult = await WithTimeout(
                                        service.GetCharacteristicsAsync(), 5000).ConfigureAwait(false);
                                    if (charResult == null || charResult.Characteristics == null)
                                    {
                                        continue;
                                    }

                                    foreach (GattCharacteristic characteristic in charResult.Characteristics)
                                    {
                                        if (characteristic.Uuid != BatteryLevelUuid)
                                        {
                                            continue;
                                        }

                                        GattReadResult readResult = await WithTimeout(
                                            characteristic.ReadValueAsync(), 5000).ConfigureAwait(false);
                                        if (readResult == null ||
                                            readResult.Status != GattCommunicationStatus.Success ||
                                            readResult.Value == null)
                                        {
                                            continue;
                                        }

                                        byte[] bytes = new byte[readResult.Value.Length];
                                        using (DataReader reader = DataReader.FromBuffer(readResult.Value))
                                        {
                                            reader.ReadBytes(bytes);
                                        }

                                        if (bytes.Length > 0 && bytes[0] <= 100)
                                        {
                                            values.Add(bytes[0]);
                                        }
                                    }
                                }
                                finally
                                {
                                    service.Dispose();
                                }
                            }
                        }

                        if (values.Count > 0)
                        {
                            int minimum = values[0];
                            for (int i = 1; i < values.Count; i++)
                            {
                                if (values[i] < minimum)
                                {
                                    minimum = values[i];
                                }
                            }

                            battery = minimum;
                            updated = DateTime.Now;
                        }
                    }

                    result.Add(new DeviceBatteryInfo
                    {
                        Name = string.IsNullOrWhiteSpace(info.Name) ? "未命名设备" : info.Name,
                        Address = address,
                        Connected = connected,
                        Battery = battery,
                        LastBattery = battery,
                        Charging = false,
                        Source = "BLE",
                        Updated = updated,
                        Detail = "蓝牙 LE"
                    });
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

            return result;
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
    }
}
