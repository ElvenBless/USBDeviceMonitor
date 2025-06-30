#pragma warning disable CA1416 // Validate platform compatibility
using System.Management;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;

namespace USBDeviceMonitor.Win
{
    public interface IUsbDeviceMonitor
    {
        IEnumerable<UsbDeviceInfo> GetConnectedDevices();
        IObservable<UsbDeviceInfo> DeviceConnected { get; }
        IObservable<UsbDeviceInfo> DeviceDisconnected { get; }
        void StartMonitoring();
        void StopMonitoring();
    }

    public class UsbDeviceInfo
    {
        private static readonly Regex _vidPidRegex = new(@"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);

        public string DeviceId { get; }
        public string VendorId { get; }
        public string ProductId { get; }
        public string Description { get; }

        public UsbDeviceInfo(string? deviceId, string? vendorId, string? productId, string? description)
        {
            DeviceId = deviceId ?? string.Empty;
            VendorId = vendorId ?? string.Empty;
            ProductId = productId ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public UsbDeviceInfo(ManagementBaseObject device)
        {
            string deviceId = device["DeviceID"]?.ToString() ?? string.Empty;
            string description = device["Description"]?.ToString() ?? string.Empty;

            var (vid, pid) = ParseDeviceIds(deviceId);

            DeviceId = deviceId;
            VendorId = vid;
            ProductId = pid;
            Description = description;
        }

        private static (string vid, string pid) ParseDeviceIds(string? deviceId)
        {
            try
            {
                if (deviceId is not null)
                {
                    var match = _vidPidRegex.Match(deviceId);
                    return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : (string.Empty, string.Empty);
                }
                return (string.Empty, string.Empty);
            }
            catch
            {
                return (string.Empty, string.Empty);
            }
        }

    }

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

        public IEnumerable<UsbDeviceInfo> GetConnectedDevices()
        {
            var devices = new List<UsbDeviceInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT DeviceID, Description FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB%'");
                devices.AddRange(from ManagementObject managementObject in searcher.Get()
                                 select new UsbDeviceInfo(managementObject));
            }
            catch (ManagementException) { /* Обработка ошибок WMI */ }
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