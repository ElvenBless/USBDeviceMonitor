#pragma warning disable CA1416 // Validate platform compatibility
namespace USBDeviceMonitor.Win
{
    /// <summary>
    /// Represents the type of USB device. Values can be combined using bitwise OR operations.
    /// </summary>
    [Flags]
    public enum UsbDeviceType
    {
        /// <summary>
        /// No device type specified.
        /// </summary>
        None = -1,

        /// <summary>
        /// Generic USB device (DeviceID starts with "USB\").
        /// </summary>
        Generic = 1,

        /// <summary>
        /// USB storage device such as flash drives, external hard drives (DeviceID starts with "USBSTOR\").
        /// </summary>
        Storage = 2,

        /// <summary>
        /// Human Interface Device such as keyboards, mice, game controllers (DeviceID starts with "HID\").
        /// </summary>
        HumanInterface = 4,

        /// <summary>
        /// USB printer device (DeviceID starts with "USBPRINT\").
        /// </summary>
        Printer = 8,

        /// <summary>
        /// USB video device such as webcams (DeviceID starts with "USBVIDEO\").
        /// </summary>
        Video = 16,

        /// <summary>
        /// Bluetooth device enumerated via USB (DeviceID starts with "BTHENUM\").
        /// </summary>
        Bluetooth = 32,

        /// <summary>
        /// Media device such as portable media players (DeviceID starts with "WPD\" or "WPDBUSENUM\").
        /// </summary>
        MediaDevice = 64,

        /// <summary>
        /// All device types combined. Used as a filter to include all USB device types.
        /// </summary>
        All = Generic | Storage | HumanInterface | Printer | Video | Bluetooth | MediaDevice
    }
}
#pragma warning restore CA1416 // Validate platform compatibility