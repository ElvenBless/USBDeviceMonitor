#pragma warning disable CA1416 // Validate platform compatibility
using System.Management;
using System.Text.RegularExpressions;

namespace USBDeviceMonitor.Win
{
    /// <summary>
    /// Represents information about a USB device parsed from Windows Management Instrumentation (WMI) data.
    /// </summary>
    public class UsbDeviceInfo : IUsbDeviceInfo
    {
        private static readonly Regex _vidPidRegex =
            new(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})\\(.+)", RegexOptions.IgnoreCase);

        /// <inheritdoc/>
        public string DeviceId { get; }

        /// <inheritdoc/>
        public string PNPDdeviceId { get; }

        /// <inheritdoc/>
        public string VendorId { get; }

        /// <inheritdoc/>
        public string ProductId { get; }

        /// <inheritdoc/>
        public string Serial { get; }

        /// <inheritdoc/>
        public string Description { get; }

        /// <inheritdoc/>
        public string DriveLetter => FindDriveLetter(PNPDdeviceId, Serial);

        /// <inheritdoc/>
        public UsbDeviceType Type { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UsbDeviceInfo"/> class from a WMI ManagementBaseObject.
        /// </summary>
        /// <param name="device">The WMI object containing device information from Win32_PnPEntity.</param>
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

        private static string FindDriveLetter(string? pnpDeviceId, string? serialNumber = null)
        {
            if (string.IsNullOrEmpty(pnpDeviceId) && string.IsNullOrEmpty(serialNumber))
                return string.Empty;

            try
            {
                var driveLetters = new List<string>();
                var pnpSerial = ExtractSerialFromPnpId(pnpDeviceId);
                // Use provided serial number if available, otherwise extract from PNPDeviceID
                var serial = !string.IsNullOrEmpty(serialNumber) ? serialNumber : pnpSerial;
                
                // First try: match by exact PNPDeviceID or serial number through Win32_DiskDrive
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");
                
                foreach (ManagementObject drive in searcher.Get().Cast<ManagementObject>())
                {
                    var drivePnpId = drive["PNPDeviceID"]?.ToString();
                    var driveSerialNumber = drive["SerialNumber"]?.ToString();
                    
                    // Try exact PNPDeviceID match first
                    bool isMatch = !string.IsNullOrEmpty(pnpDeviceId) && 
                                  !string.IsNullOrEmpty(drivePnpId) &&
                                  string.Equals(drivePnpId, pnpDeviceId, StringComparison.OrdinalIgnoreCase);
                    
                    // If no exact match, try matching by serial number (for Generic USB devices)
                    if (!isMatch && !string.IsNullOrEmpty(serial))
                    {
                        // Try matching by SerialNumber property from Win32_DiskDrive
                        if (!string.IsNullOrEmpty(driveSerialNumber))
                        {
                            isMatch = string.Equals(serial, driveSerialNumber, StringComparison.OrdinalIgnoreCase);
                        }
                        
                        // Also try extracting serial from drive's PNPDeviceID
                        if (!isMatch && !string.IsNullOrEmpty(drivePnpId))
                        {
                            var driveSerial = ExtractSerialFromPnpId(drivePnpId);
                            isMatch = !string.IsNullOrEmpty(driveSerial) && 
                                      string.Equals(serial, driveSerial, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    
                    if (!isMatch)
                        continue;

                    // Get all partitions for this drive
                    foreach (ManagementObject partition in drive.GetRelated("Win32_DiskPartition"))
                    {
                        // Get all logical disks for this partition
                        foreach (ManagementObject logical in partition.GetRelated("Win32_LogicalDisk"))
                        {
                            var deviceId = logical["DeviceID"]?.ToString();
                            // Filter only drives with letter (format "X:")
                            // DeviceID for drives with letters is like "C:", "D:", "E:" etc.
                            if (!string.IsNullOrEmpty(deviceId) && 
                                deviceId.Length == 2 && 
                                deviceId[1] == ':' && 
                                char.IsLetter(deviceId[0]))
                            {
                                driveLetters.Add(deviceId);
                            }
                        }
                    }
                }

                // Return all drive letters joined, or empty if none found
                return driveLetters.Count > 0 ? string.Join(" | ", driveLetters.Distinct()) : string.Empty;
            }
            catch
            {
                // ignore errors and return empty
            }

            return string.Empty;
        }

        private static string? ExtractSerialFromPnpId(string pnpDeviceId)
        {
            // Format: USB\VID_xxxx&PID_xxxx\SerialNumber
            // or: USBSTOR\Disk... (for Storage devices)
            var parts = pnpDeviceId.Split('\\');
            if (parts.Length >= 3)
            {
                return parts[2];
            }
            return null;
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility