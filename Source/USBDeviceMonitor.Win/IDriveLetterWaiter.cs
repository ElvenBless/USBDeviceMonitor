#pragma warning disable CA1416 // Validate platform compatibility
namespace USBDeviceMonitor.Win;

/// <summary>
/// Service for waiting for drive letter initialization for USB storage devices.
/// </summary>
public interface IDriveLetterWaiter
{
    /// <summary>
    /// Waits for drive letter to be initialized for a USB device.
    /// Returns the device with drive letter filled in if it's a storage device or composite device that may contain storage.
    /// </summary>
    /// <param name="device">The USB device to check</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds (default: 5000)</param>
    /// <param name="checkIntervalMs">Interval between checks in milliseconds (default: 100)</param>
    /// <returns>Device with drive letter if available, otherwise original device</returns>
    Task<IUsbDeviceInfo> WaitForDriveLetterAsync(IUsbDeviceInfo device, int timeoutMs = 5000, int checkIntervalMs = 100);
}
#pragma warning restore CA1416 // Validate platform compatibility
