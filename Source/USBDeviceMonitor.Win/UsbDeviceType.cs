#pragma warning disable CA1416 // Validate platform compatibility
namespace USBDeviceMonitor.Win
{
    [Flags]
    public enum UsbDeviceType
    {
        None = -1,
        Generic = 1,          // USB\
        Storage = 2,          // USBSTOR\
        HumanInterface = 4,   // HID\
        Printer = 8,          // USBPRINT\
        Video = 16,           // USBVIDEO\
        Bluetooth = 32,       // BTHENUM\
        MediaDevice = 64,     // WPD\, WPDBUSENUM\
        All = Generic | Storage | HumanInterface | Printer | Video | Bluetooth | MediaDevice
    }
}
#pragma warning restore CA1416 // Validate platform compatibility