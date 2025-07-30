#pragma warning disable CA1416 // Validate platform compatibility
namespace USBDeviceMonitor.Win
{
    public interface IUsbDeviceMonitor
    {
        IEnumerable<UsbDeviceInfo> GetConnectedDevices(
            UsbDeviceType types = UsbDeviceType.All,
            Func<UsbDeviceInfo, bool>? predicate = null);

        IObservable<UsbDeviceInfo> DeviceConnected { get; }
        IObservable<UsbDeviceInfo> DeviceDisconnected { get; }
        void StartMonitoring();
        void StopMonitoring();
    }
}
#pragma warning restore CA1416 // Validate platform compatibility