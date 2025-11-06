#pragma warning disable CA1416 // Validate platform compatibility
namespace USBDeviceMonitor.Win
{
    public interface IUsbDeviceMonitor
    {
        IEnumerable<IUsbDeviceInfo> GetConnectedDevices(
            UsbDeviceType types = UsbDeviceType.All,
            Func<IUsbDeviceInfo, bool>? predicate = null);

        IObservable<IUsbDeviceInfo> DeviceConnected { get; }
        IObservable<IUsbDeviceInfo> DeviceDisconnected { get; }
        void StartMonitoring();
        void StopMonitoring();
    }
}
#pragma warning restore CA1416 // Validate platform compatibility