#pragma warning disable CA1416 // Validate platform compatibility
using System.Management;
using System.Reactive.Subjects;

namespace USBDeviceMonitor.Win
{

    public partial class UsbDeviceMonitor : IUsbDeviceMonitor
    {
        private readonly Subject<UsbDeviceInfo> _deviceConnectedSubject = new();
        private readonly Subject<UsbDeviceInfo> _deviceDisconnectedSubject = new();

        public IObservable<UsbDeviceInfo> DeviceConnected => _deviceConnectedSubject;
        public IObservable<UsbDeviceInfo> DeviceDisconnected => _deviceDisconnectedSubject;

        private readonly ManagementEventWatcher _connectWatcher;
        private readonly ManagementEventWatcher _disconnectWatcher;

        public UsbDeviceMonitor()
        {
            _connectWatcher = GetManagementEventWatcher("__InstanceCreationEvent");
            _disconnectWatcher = GetManagementEventWatcher("__InstanceDeletionEvent");
        }

        public IEnumerable<UsbDeviceInfo> GetConnectedDevices(
            UsbDeviceType types = UsbDeviceType.All,
            Func<UsbDeviceInfo, bool>? predicate = null)
        {
            var devices = new List<UsbDeviceInfo>();

            const string query = @"
                SELECT DeviceID, Description, PNPDeviceID 
                FROM Win32_PnPEntity 
                WHERE DeviceID LIKE 'USB%'";

            try
            {
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject obj in searcher.Get())
                {
                    string? deviceId = obj["DeviceID"]?.ToString();
                    if (string.IsNullOrEmpty(deviceId))
                        continue;

                    var info = new UsbDeviceInfo(obj);
                    if (!types.HasFlag(info.Type))
                        continue;

                    if (predicate == null || predicate(info))
                        devices.Add(info);
                }
            }
            catch (ManagementException)
            {
                // WMI failed, return partial or empty result
            }

            return devices;
        }

        private static ManagementEventWatcher GetManagementEventWatcher(string eventClassName)
        {
            TimeSpan withinInterval = new(0, 0, 1);
            string condition = "TargetInstance isa 'Win32_PnPEntity' AND TargetInstance.DeviceID LIKE 'USB%'";
            var disconnectQuery = new WqlEventQuery(eventClassName, withinInterval, condition);
            return new ManagementEventWatcher(disconnectQuery);
        }

        public void StartMonitoring()
        {
            _connectWatcher.EventArrived += ConnectWatcher_EventArrived;
            _disconnectWatcher.EventArrived += DisconnectWatcher_EventArrived;
            _connectWatcher.Start();
            _disconnectWatcher.Start();
        }

        private void DisconnectWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var device = GetDeviceFromEventArgs(e);
            if (device != null)
                _deviceDisconnectedSubject?.OnNext(device);
        }

        private void ConnectWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            var device = GetDeviceFromEventArgs(e);
            if (device != null)
                _deviceConnectedSubject?.OnNext(device);
        }

        public void StopMonitoring()
        {
            _connectWatcher.EventArrived -= ConnectWatcher_EventArrived;
            _disconnectWatcher.EventArrived -= DisconnectWatcher_EventArrived;
            _connectWatcher?.Stop();
            _disconnectWatcher?.Stop();
        }

        private static UsbDeviceInfo? GetDeviceFromEventArgs(EventArrivedEventArgs args)
        {
            try
            {
                if (args.NewEvent["TargetInstance"] is ManagementBaseObject targetInstance)
                    return new UsbDeviceInfo(targetInstance);

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility