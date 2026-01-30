namespace USBDeviceMonitor.Win;

/// <summary>
/// Represents information about a USB device connected to the system.
/// </summary>
public interface IUsbDeviceInfo
{
    /// <summary>
    /// Gets the device description as reported by Windows.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the unique device identifier (DeviceID) in the format used by Windows Device Manager.
    /// Example: "USB\VID_1234&PID_5678\SERIAL123456"
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Gets the drive letter(s) assigned to this device, if it is a storage device.
    /// Returns empty string if the device is not a storage device or no drive letter is assigned.
    /// For devices with multiple volumes, drive letters are joined with " | " separator.
    /// </summary>
    string DriveLetter { get; }

    /// <summary>
    /// Gets the Plug and Play device identifier (PNPDeviceID).
    /// This is typically the same as <see cref="DeviceId"/> but may differ for some device types.
    /// </summary>
    string PNPDdeviceId { get; }

    /// <summary>
    /// Gets the product identifier (PID) in hexadecimal format.
    /// Example: "0562"
    /// </summary>
    string ProductId { get; }

    /// <summary>
    /// Gets the device serial number, if available.
    /// May be empty for devices that do not report a serial number.
    /// </summary>
    string Serial { get; }

    /// <summary>
    /// Gets the type of USB device as a combination of flags.
    /// </summary>
    UsbDeviceType Type { get; }

    /// <summary>
    /// Gets the vendor identifier (VID) in hexadecimal format.
    /// Example: "0DD8"
    /// </summary>
    string VendorId { get; }
}