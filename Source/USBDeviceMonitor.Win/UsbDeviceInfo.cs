#pragma warning disable CA1416 // Validate platform compatibility
using System.Management;
using System.Text.RegularExpressions;

namespace USBDeviceMonitor.Win
{
    public class UsbDeviceInfo
    {
        private static readonly Regex _vidPidRegex =
            new(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})\\(.+)", RegexOptions.IgnoreCase);

        public string DeviceId { get; }
        public string PNPDdeviceId { get; }
        public string VendorId { get; }
        public string ProductId { get; }
        public string Serial { get; }
        public string Description { get; }
        public string DriveLetter => FindDriveLetter(PNPDdeviceId);
        public UsbDeviceType Type { get; }

        public UsbDeviceInfo(ManagementBaseObject device)
        {
            string deviceId = device["DeviceID"]?.ToString() ?? string.Empty;
            string description = device["Description"]?.ToString() ?? string.Empty;
            string pNPDdeviceId = device["PNPDeviceID"]?.ToString() ?? string.Empty;

            var (vid, pid, serial) = ParseDeviceIds(deviceId);

            DeviceId = deviceId;
            PNPDdeviceId = pNPDdeviceId;
            VendorId = vid;
            ProductId = pid;
            Serial = serial;
            Description = description;
            Type = GetTypeFromDeviceId(deviceId);
        }

        private static UsbDeviceType GetTypeFromDeviceId(string deviceId)
        {
            if (deviceId.StartsWith("USBSTOR\\", StringComparison.OrdinalIgnoreCase))
                return UsbDeviceType.Storage;
            if (deviceId.StartsWith("HID\\", StringComparison.OrdinalIgnoreCase))
                return UsbDeviceType.HumanInterface;
            if (deviceId.StartsWith("USBPRINT\\", StringComparison.OrdinalIgnoreCase))
                return UsbDeviceType.Printer;
            if (deviceId.StartsWith("USBVIDEO\\", StringComparison.OrdinalIgnoreCase))
                return UsbDeviceType.Video;
            if (deviceId.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase))
                return UsbDeviceType.Bluetooth;
            if (deviceId.StartsWith("WPD\\", StringComparison.OrdinalIgnoreCase) ||
                deviceId.StartsWith("WPDBUSENUM\\", StringComparison.OrdinalIgnoreCase))
                return UsbDeviceType.MediaDevice;
            if (deviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                return UsbDeviceType.Generic;

            return UsbDeviceType.None;
        }

        private static (string vid, string pid, string serial) ParseDeviceIds(string? deviceId)
        {
            if (!string.IsNullOrEmpty(deviceId))
            {
                var match = _vidPidRegex.Match(deviceId);
                if (match.Success)
                {
                    return (
                        match.Groups[1].Value, // VID
                        match.Groups[2].Value, // PID
                        match.Groups[3].Value  // Серийный хвост
                    );
                }
            }

            return (string.Empty, string.Empty, string.Empty);
        }

        private static string FindDriveLetter(string? pnpDeviceId)
        {
            if (string.IsNullOrEmpty(pnpDeviceId))
                return string.Empty;

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");
                foreach (var logical in from ManagementObject drive in searcher.Get().Cast<ManagementObject>()
                                        where drive["PNPDeviceID"]?.ToString()?.Equals(pnpDeviceId, StringComparison.OrdinalIgnoreCase) == true
                                        from ManagementObject partition in drive.GetRelated("Win32_DiskPartition")
                                        from ManagementObject logical in partition.GetRelated("Win32_LogicalDisk")
                                        select logical)
                {
                    return logical["DeviceID"]?.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // ignore errors and return empty
            }

            return string.Empty;
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility