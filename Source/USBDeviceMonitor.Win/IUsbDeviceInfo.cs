namespace USBDeviceMonitor.Win;

public interface IUsbDeviceInfo
{
    string Description { get; }
    string DeviceId { get; }
    string DriveLetter { get; }
    string PNPDdeviceId { get; }
    string ProductId { get; }
    string Serial { get; }
    UsbDeviceType Type { get; }
    string VendorId { get; }
}