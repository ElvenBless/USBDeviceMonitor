#pragma warning disable CA1416 // Validate platform compatibility
using System.Diagnostics;
using System.Management;

namespace USBDeviceMonitor.Win;

/// <summary>
/// Service that waits for drive letter initialization for USB storage devices.
/// </summary>
internal class DriveLetterWaiter : IDriveLetterWaiter
{
#if DEBUG
    private static void Log(string message)
    {
        Console.WriteLine(message);
    }
#else
    [Conditional("DEBUG")]
    private static void Log(string message)
    {
        // Empty in release builds
    }
#endif
    public async Task<IUsbDeviceInfo> WaitForDriveLetterAsync(IUsbDeviceInfo device, int timeoutMs = 5000, int checkIntervalMs = 100)
    {
        Log($"[WaitForDriveLetterAsync] ========================================");
        Log($"[WaitForDriveLetterAsync] Starting wait for drive letter");
        Log($"[WaitForDriveLetterAsync] DeviceId: {device.DeviceId}");
        Log($"[WaitForDriveLetterAsync] PNPDdeviceId: {device.PNPDdeviceId}");
        Log($"[WaitForDriveLetterAsync] Serial: {device.Serial}");
        Log($"[WaitForDriveLetterAsync] Type: {device.Type}");
        Log($"[WaitForDriveLetterAsync] Description: {device.Description}");
        
        // Check if device needs drive letter waiting
        if (!ShouldWaitForDriveLetter(device))
        {
            Log($"[WaitForDriveLetterAsync] Device does not need drive letter waiting, returning immediately");
            return device;
        }

        // Handle CompositeUsbDeviceInfo separately
        if (device is CompositeUsbDeviceInfo composite)
        {
            Log($"[WaitForDriveLetterAsync] Handling CompositeUsbDeviceInfo with {composite.Devices.Count} devices");
            return await WaitForCompositeDriveLetterAsync(composite, timeoutMs, checkIntervalMs).ConfigureAwait(false);
        }

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var checkInterval = TimeSpan.FromMilliseconds(checkIntervalMs);
        int attemptCount = 0;

        Log($"[WaitForDriveLetterAsync] Starting wait loop (timeout: {timeoutMs}ms, interval: {checkIntervalMs}ms)");

        while (DateTime.UtcNow - startTime < timeout)
        {
            attemptCount++;
            var elapsed = DateTime.UtcNow - startTime;
            Log($"[WaitForDriveLetterAsync] Attempt #{attemptCount} (elapsed: {elapsed.TotalMilliseconds:F0}ms)");
            
            var driveLetter = FindDriveLetter(device.PNPDdeviceId, device.Serial);
            if (!string.IsNullOrEmpty(driveLetter))
            {
                Log($"[WaitForDriveLetterAsync] ✓ Drive letter found: {driveLetter}");
                // Create device with drive letter
                return CreateDeviceWithDriveLetter(device, driveLetter);
            }

            Log($"[WaitForDriveLetterAsync] No drive letter yet, waiting {checkIntervalMs}ms...");
            await Task.Delay(checkInterval).ConfigureAwait(false);
        }

        // Timeout reached, return original device
        Log($"[WaitForDriveLetterAsync] ✗ Timeout reached, returning device without drive letter");
        Log($"[WaitForDriveLetterAsync] ========================================");
        return device;
    }

    private async Task<IUsbDeviceInfo> WaitForCompositeDriveLetterAsync(CompositeUsbDeviceInfo composite, int timeoutMs, int checkIntervalMs)
    {
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var checkInterval = TimeSpan.FromMilliseconds(checkIntervalMs);

        // Wait until at least one device in composite has a drive letter, or timeout
        while (DateTime.UtcNow - startTime < timeout)
        {
            var updatedDevices = new List<IUsbDeviceInfo>();
            var hasDriveLetter = false;

            foreach (var device in composite.Devices)
            {
                var driveLetter = FindDriveLetter(device.PNPDdeviceId, device.Serial);
                if (!string.IsNullOrEmpty(driveLetter))
                {
                    // Create device with drive letter
                    updatedDevices.Add(CreateDeviceWithDriveLetter(device, driveLetter));
                    hasDriveLetter = true;
                }
                else
                {
                    updatedDevices.Add(device);
                }
            }

            if (hasDriveLetter)
            {
                // Create new composite with updated devices that have drive letters
                return new CompositeUsbDeviceInfo(updatedDevices);
            }

            await Task.Delay(checkInterval).ConfigureAwait(false);
        }

        // Timeout reached, return original composite
        return composite;
    }

    private static bool ShouldWaitForDriveLetter(IUsbDeviceInfo device)
    {
        // Wait for Storage devices
        if (device.Type.HasFlag(UsbDeviceType.Storage))
            return true;

        // Wait for Generic devices (may be composite devices containing storage)
        if (device.Type.HasFlag(UsbDeviceType.Generic))
            return true;

        // Wait for CompositeUsbDeviceInfo if it contains Storage or Generic
        if (device is CompositeUsbDeviceInfo composite)
        {
            // Check if composite contains storage or generic devices
            // We can't access internal list, so we check by Type flags
            return composite.Type.HasFlag(UsbDeviceType.Storage) || 
                   composite.Type.HasFlag(UsbDeviceType.Generic);
        }

        return false;
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
            
            Log($"[FindDriveLetter] Searching for drive letter:");
            Log($"[FindDriveLetter]   PNPDeviceID: {pnpDeviceId ?? "(null)"}");
            Log($"[FindDriveLetter]   SerialNumber (provided): {serialNumber ?? "(null)"}");
            Log($"[FindDriveLetter]   SerialNumber (extracted from PNP): {pnpSerial ?? "(null)"}");
            Log($"[FindDriveLetter]   Using serial: {serial ?? "(null)"}");
            
            // First, let's see ALL disk drives without filter to understand what we have
            Log($"[FindDriveLetter] ===== ALL DISK DRIVES (no filter) =====");
            using var allDrivesSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            var allDrivesList = allDrivesSearcher.Get().Cast<ManagementObject>().ToList();
            Log($"[FindDriveLetter] Found {allDrivesList.Count} total disk drives");
            
            foreach (ManagementObject drive in allDrivesList)
            {
                Log($"[FindDriveLetter] --- Disk Drive ---");
                Log($"[FindDriveLetter]   Index: {drive["Index"] ?? "(null)"}");
                Log($"[FindDriveLetter]   Model: {drive["Model"] ?? "(null)"}");
                Log($"[FindDriveLetter]   InterfaceType: {drive["InterfaceType"] ?? "(null)"}");
                Log($"[FindDriveLetter]   MediaType: {drive["MediaType"] ?? "(null)"}");
                Log($"[FindDriveLetter]   PNPDeviceID: {drive["PNPDeviceID"] ?? "(null)"}");
                Log($"[FindDriveLetter]   SerialNumber: {drive["SerialNumber"] ?? "(null)"}");
                Log($"[FindDriveLetter]   DeviceID: {drive["DeviceID"] ?? "(null)"}");
                Log($"[FindDriveLetter]   Caption: {drive["Caption"] ?? "(null)"}");
                Log($"[FindDriveLetter]   Description: {drive["Description"] ?? "(null)"}");
                Log($"[FindDriveLetter]   Status: {drive["Status"] ?? "(null)"}");
                
                // Try to get partitions and logical disks for this drive
                try
                {
                    var partitions = drive.GetRelated("Win32_DiskPartition").Cast<ManagementObject>().ToList();
                    Log($"[FindDriveLetter]   Partitions: {partitions.Count}");
                    foreach (ManagementObject partition in partitions)
                    {
                        var logicalDisks = partition.GetRelated("Win32_LogicalDisk").Cast<ManagementObject>().ToList();
                        Log($"[FindDriveLetter]     Partition {partition["Name"] ?? "(null)"} -> {logicalDisks.Count} logical disk(s)");
                        foreach (ManagementObject logical in logicalDisks)
                        {
                            var deviceId = logical["DeviceID"]?.ToString();
                            Log($"[FindDriveLetter]       LogicalDisk DeviceID: {deviceId ?? "(null)"}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[FindDriveLetter]   Error getting partitions: {ex.Message}");
                }
            }
            Log($"[FindDriveLetter] ========================================");
            
            // Now try with USB filter and also SCSI for external drives
            using var usbSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");
            var usbDrives = usbSearcher.Get().Cast<ManagementObject>().ToList();
            Log($"[FindDriveLetter] Found {usbDrives.Count} USB disk drives (with InterfaceType='USB' filter)");
            
            // Also try SCSI interface for external drives (UAS devices often appear as SCSI)
            using var scsiSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='SCSI' AND MediaType='External hard disk media'");
            var scsiDrives = scsiSearcher.Get().Cast<ManagementObject>().ToList();
            Log($"[FindDriveLetter] Found {scsiDrives.Count} SCSI external disk drives");
            
            // Combine both lists (use Distinct to avoid duplicates)
            var allDrives = usbDrives.Concat(scsiDrives).GroupBy(d => d["DeviceID"]?.ToString()).Select(g => g.First()).ToList();
            Log($"[FindDriveLetter] Total drives to check: {allDrives.Count}");
            
            foreach (ManagementObject drive in allDrives)
            {
                var drivePnpId = drive["PNPDeviceID"]?.ToString();
                var driveSerialNumber = drive["SerialNumber"]?.ToString();
                var driveModel = drive["Model"]?.ToString();
                var driveIndex = drive["Index"]?.ToString();
                
                Log($"[FindDriveLetter] Checking drive:");
                Log($"[FindDriveLetter]   Index: {driveIndex ?? "(null)"}");
                Log($"[FindDriveLetter]   Model: {driveModel ?? "(null)"}");
                Log($"[FindDriveLetter]   PNPDeviceID: {drivePnpId ?? "(null)"}");
                Log($"[FindDriveLetter]   SerialNumber: {driveSerialNumber ?? "(null)"}");
                
                // Try exact PNPDeviceID match first
                bool isMatch = !string.IsNullOrEmpty(pnpDeviceId) && 
                              !string.IsNullOrEmpty(drivePnpId) &&
                              string.Equals(drivePnpId, pnpDeviceId, StringComparison.OrdinalIgnoreCase);
                
                Log($"[FindDriveLetter]   Exact PNPDeviceID match: {isMatch}");
                
                // If no exact match, try matching by serial number (for Generic USB devices)
                if (!isMatch && !string.IsNullOrEmpty(serial))
                {
                    // Try matching by SerialNumber property from Win32_DiskDrive (exact match)
                    if (!string.IsNullOrEmpty(driveSerialNumber))
                    {
                        isMatch = string.Equals(serial, driveSerialNumber, StringComparison.OrdinalIgnoreCase);
                        Log($"[FindDriveLetter]   SerialNumber exact match: {isMatch} (comparing '{serial}' with '{driveSerialNumber}')");
                    }
                    
                    // Try partial match - serial number from Win32_DiskDrive might be a substring
                    // For example: "MSFT30DD2024912665637" in PnPEntity vs "DD2024912665637" in DiskDrive
                    if (!isMatch && !string.IsNullOrEmpty(driveSerialNumber))
                    {
                        isMatch = serial.Contains(driveSerialNumber, StringComparison.OrdinalIgnoreCase) ||
                                  driveSerialNumber.Contains(serial, StringComparison.OrdinalIgnoreCase);
                        Log($"[FindDriveLetter]   SerialNumber partial match: {isMatch} (comparing '{serial}' with '{driveSerialNumber}')");
                    }
                    
                    // Also try extracting serial from drive's PNPDeviceID
                    if (!isMatch && !string.IsNullOrEmpty(drivePnpId))
                    {
                        var driveSerial = ExtractSerialFromPnpId(drivePnpId);
                        isMatch = !string.IsNullOrEmpty(driveSerial) && 
                                  string.Equals(serial, driveSerial, StringComparison.OrdinalIgnoreCase);
                        Log($"[FindDriveLetter]   Extracted serial from PNP match: {isMatch} (comparing '{serial}' with '{driveSerial ?? "(null)"}')");
                    }
                }
                
                if (!isMatch)
                {
                    Log($"[FindDriveLetter]   No match, skipping this drive");
                    continue;
                }

                Log($"[FindDriveLetter]   MATCH FOUND! Getting partitions and logical disks...");

                // Get all partitions for this drive
                var partitions = drive.GetRelated("Win32_DiskPartition").Cast<ManagementObject>().ToList();
                Log($"[FindDriveLetter]   Found {partitions.Count} partitions");
                
                foreach (ManagementObject partition in partitions)
                {
                    var partitionName = partition["Name"]?.ToString();
                    Log($"[FindDriveLetter]     Partition: {partitionName ?? "(null)"}");
                    
                    // Get all logical disks for this partition
                    var logicalDisks = partition.GetRelated("Win32_LogicalDisk").Cast<ManagementObject>().ToList();
                    Log($"[FindDriveLetter]       Found {logicalDisks.Count} logical disks");
                    
                    foreach (ManagementObject logical in logicalDisks)
                    {
                        var deviceId = logical["DeviceID"]?.ToString();
                        var volumeName = logical["VolumeName"]?.ToString();
                        var driveType = logical["DriveType"]?.ToString();
                        
                        Log($"[FindDriveLetter]         LogicalDisk:");
                        Log($"[FindDriveLetter]           DeviceID: {deviceId ?? "(null)"}");
                        Log($"[FindDriveLetter]           VolumeName: {volumeName ?? "(null)"}");
                        Log($"[FindDriveLetter]           DriveType: {driveType ?? "(null)"}");
                        
                        // Filter only drives with letter (format "X:")
                        // DeviceID for drives with letters is like "C:", "D:", "E:" etc.
                        if (!string.IsNullOrEmpty(deviceId) && 
                            deviceId.Length == 2 && 
                            deviceId[1] == ':' && 
                            char.IsLetter(deviceId[0]))
                        {
                            driveLetters.Add(deviceId);
                            Log($"[FindDriveLetter]           ✓ Added drive letter: {deviceId}");
                        }
                        else
                        {
                            Log($"[FindDriveLetter]           ✗ Skipped (no valid drive letter format)");
                        }
                    }
                }
            }

            // If still no drive letters found, try alternative approach: search all drives by serial (including partial match)
            // This is a fallback for cases where Win32_DiskDrive doesn't have matching PNPDeviceID
            if (driveLetters.Count == 0 && !string.IsNullOrEmpty(serial))
            {
                Log($"[FindDriveLetter] No drive letters found, trying fallback method with serial: {serial}");
                var fallbackLetters = FindDriveLettersBySerialFallback(serial).ToList();
                driveLetters.AddRange(fallbackLetters);
                Log($"[FindDriveLetter] Fallback method found {fallbackLetters.Count} drive letters: {string.Join(", ", fallbackLetters)}");
            }

            var result = driveLetters.Count > 0 ? string.Join(" | ", driveLetters.Distinct()) : string.Empty;
            Log($"[FindDriveLetter] Final result: {(string.IsNullOrEmpty(result) ? "(empty)" : result)}");
            Log($"[FindDriveLetter] ========================================");
            
            // Return all drive letters joined, or empty if none found
            return result;
        }
        catch (Exception ex)
        {
            Log($"[FindDriveLetter] ERROR: {ex.Message}");
            Log($"[FindDriveLetter] StackTrace: {ex.StackTrace}");
            // ignore errors and return empty
        }

        return string.Empty;
    }

    private static IEnumerable<string> FindDriveLettersBySerialFallback(string serial)
    {
        var driveLetters = new List<string>();
        
        try
        {
            Log($"[FindDriveLettersBySerialFallback] Searching by serial: {serial}");
            
            // Try without filter first to see all drives
            Log($"[FindDriveLettersBySerialFallback] ===== ALL DISK DRIVES (no filter) =====");
            using var allDrivesSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            var allDrivesList = allDrivesSearcher.Get().Cast<ManagementObject>().ToList();
            Log($"[FindDriveLettersBySerialFallback] Found {allDrivesList.Count} total disk drives");
            
            foreach (ManagementObject drive in allDrivesList)
            {
                var driveSerial = drive["SerialNumber"]?.ToString();
                var driveModel = drive["Model"]?.ToString();
                var interfaceType = drive["InterfaceType"]?.ToString();
                var pnpDeviceId = drive["PNPDeviceID"]?.ToString();
                
                Log($"[FindDriveLettersBySerialFallback] Drive: Model={driveModel ?? "(null)"}, InterfaceType={interfaceType ?? "(null)"}, SerialNumber={driveSerial ?? "(null)"}, PNPDeviceID={pnpDeviceId ?? "(null)"}");
            }
            
            // Get all USB disk drives and check their serial numbers
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");
            var allDrives = searcher.Get().Cast<ManagementObject>().ToList();
            
            Log($"[FindDriveLettersBySerialFallback] Found {allDrives.Count} USB disk drives (with InterfaceType='USB' filter)");
            
            // Also try without filter - search all drives by serial
            Log($"[FindDriveLettersBySerialFallback] Now searching ALL drives (no filter) by serial: {serial}");
            
            // First check USB drives
            foreach (ManagementObject drive in allDrives)
            {
                var driveSerial = drive["SerialNumber"]?.ToString();
                var driveModel = drive["Model"]?.ToString();
                
                Log($"[FindDriveLettersBySerialFallback] Checking USB drive Model: {driveModel ?? "(null)"}, SerialNumber: {driveSerial ?? "(null)"}");
                
                if (string.IsNullOrEmpty(driveSerial) || 
                    !string.Equals(driveSerial, serial, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"[FindDriveLettersBySerialFallback]   Serial mismatch, skipping");
                    continue;
                }

                Log($"[FindDriveLettersBySerialFallback]   Serial match! Getting partitions...");

                // Get all partitions for this drive
                var partitions = drive.GetRelated("Win32_DiskPartition").Cast<ManagementObject>().ToList();
                Log($"[FindDriveLettersBySerialFallback]   Found {partitions.Count} partitions");
                
                foreach (ManagementObject partition in partitions)
                {
                    // Get all logical disks for this partition
                    var logicalDisks = partition.GetRelated("Win32_LogicalDisk").Cast<ManagementObject>().ToList();
                    Log($"[FindDriveLettersBySerialFallback]     Found {logicalDisks.Count} logical disks");
                    
                    foreach (ManagementObject logical in logicalDisks)
                    {
                        var deviceId = logical["DeviceID"]?.ToString();
                        Log($"[FindDriveLettersBySerialFallback]       LogicalDisk DeviceID: {deviceId ?? "(null)"}");
                        
                        if (!string.IsNullOrEmpty(deviceId) && 
                            deviceId.Length == 2 && 
                            deviceId[1] == ':' && 
                            char.IsLetter(deviceId[0]))
                        {
                            driveLetters.Add(deviceId);
                            Log($"[FindDriveLettersBySerialFallback]       ✓ Added drive letter: {deviceId}");
                        }
                    }
                }
            }
            
            // If nothing found in USB drives, try ALL drives without filter (with partial match)
            if (driveLetters.Count == 0)
            {
                Log($"[FindDriveLettersBySerialFallback] No matches in USB drives, checking ALL drives with partial serial match...");
                foreach (ManagementObject drive in allDrivesList)
                {
                    var driveSerial = drive["SerialNumber"]?.ToString();
                    var driveModel = drive["Model"]?.ToString();
                    var interfaceType = drive["InterfaceType"]?.ToString();
                    var mediaType = drive["MediaType"]?.ToString();
                    var pnpDeviceId = drive["PNPDeviceID"]?.ToString();
                    
                    Log($"[FindDriveLettersBySerialFallback] Checking drive: Model={driveModel ?? "(null)"}, InterfaceType={interfaceType ?? "(null)"}, MediaType={mediaType ?? "(null)"}, SerialNumber={driveSerial ?? "(null)"}, PNPDeviceID={pnpDeviceId ?? "(null)"}");
                    
                    bool serialMatch = false;
                    
                    // Try exact match first
                    if (!string.IsNullOrEmpty(driveSerial))
                    {
                        serialMatch = string.Equals(driveSerial, serial, StringComparison.OrdinalIgnoreCase);
                        Log($"[FindDriveLettersBySerialFallback]   Exact serial match: {serialMatch}");
                    }
                    
                    // Try partial match (serial in Win32_DiskDrive might be substring of serial in Win32_PnPEntity)
                    if (!serialMatch && !string.IsNullOrEmpty(driveSerial) && !string.IsNullOrEmpty(serial))
                    {
                        serialMatch = serial.Contains(driveSerial, StringComparison.OrdinalIgnoreCase) ||
                                      driveSerial.Contains(serial, StringComparison.OrdinalIgnoreCase);
                        Log($"[FindDriveLettersBySerialFallback]   Partial serial match: {serialMatch} (comparing '{serial}' with '{driveSerial}')");
                    }
                    
                    if (!serialMatch)
                    {
                        Log($"[FindDriveLettersBySerialFallback]   Serial mismatch, skipping");
                        continue;
                    }

                    Log($"[FindDriveLettersBySerialFallback]   ✓ Serial match! Getting partitions...");

                    // Get all partitions for this drive
                    var partitions = drive.GetRelated("Win32_DiskPartition").Cast<ManagementObject>().ToList();
                    Log($"[FindDriveLettersBySerialFallback]   Found {partitions.Count} partitions");
                    
                    foreach (ManagementObject partition in partitions)
                    {
                        // Get all logical disks for this partition
                        var logicalDisks = partition.GetRelated("Win32_LogicalDisk").Cast<ManagementObject>().ToList();
                        Log($"[FindDriveLettersBySerialFallback]     Found {logicalDisks.Count} logical disks");
                        
                        foreach (ManagementObject logical in logicalDisks)
                        {
                            var deviceId = logical["DeviceID"]?.ToString();
                            Log($"[FindDriveLettersBySerialFallback]       LogicalDisk DeviceID: {deviceId ?? "(null)"}");
                            
                            if (!string.IsNullOrEmpty(deviceId) && 
                                deviceId.Length == 2 && 
                                deviceId[1] == ':' && 
                                char.IsLetter(deviceId[0]))
                            {
                                driveLetters.Add(deviceId);
                                Log($"[FindDriveLettersBySerialFallback]       ✓ Added drive letter: {deviceId}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[FindDriveLettersBySerialFallback] ERROR: {ex.Message}");
            Log($"[FindDriveLettersBySerialFallback] StackTrace: {ex.StackTrace}");
        }

        return driveLetters;
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

    private static IUsbDeviceInfo CreateDeviceWithDriveLetter(IUsbDeviceInfo original, string driveLetter)
    {
        // If it's already a UsbDeviceInfo, we need to create a wrapper
        // Since UsbDeviceInfo is immutable, we'll create a wrapper class
        if (original is UsbDeviceInfo usbDeviceInfo)
        {
            return new UsbDeviceInfoWithDriveLetter(usbDeviceInfo, driveLetter);
        }

        // For CompositeUsbDeviceInfo, we need to handle it differently
        // For now, return a wrapper
        return new UsbDeviceInfoWithDriveLetter(original, driveLetter);
    }

    /// <summary>
    /// Wrapper class that adds drive letter to an existing IUsbDeviceInfo
    /// </summary>
    private class UsbDeviceInfoWithDriveLetter : IUsbDeviceInfo
    {
        private readonly IUsbDeviceInfo _original;

        public UsbDeviceInfoWithDriveLetter(IUsbDeviceInfo original, string driveLetter)
        {
            _original = original;
            DriveLetter = driveLetter;
        }

        public string Description => _original.Description;
        public string DeviceId => _original.DeviceId;
        public string DriveLetter { get; }
        public string PNPDdeviceId => _original.PNPDdeviceId;
        public string ProductId => _original.ProductId;
        public string Serial => _original.Serial;
        public UsbDeviceType Type => _original.Type;
        public string VendorId => _original.VendorId;
    }
}
#pragma warning restore CA1416 // Validate platform compatibility
