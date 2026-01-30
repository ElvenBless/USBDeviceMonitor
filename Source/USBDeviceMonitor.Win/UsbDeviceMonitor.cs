#pragma warning disable CA1416 // Validate platform compatibility
using System.Management;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace USBDeviceMonitor.Win;

/// <summary>
/// Monitors USB device connections and disconnections on Windows platform.
/// Provides reactive observables for device events and supports waiting for drive letter initialization for storage devices.
/// </summary>
public partial class UsbDeviceMonitor : IUsbDeviceMonitor
{
    private readonly Subject<IUsbDeviceInfo> _deviceDisconnectedSubject = new();
    private readonly Subject<IUsbDeviceInfo> _rawDeviceConnectedSubject = new();

    /// <inheritdoc/>
    public IObservable<IUsbDeviceInfo> DeviceConnected { get; }

    /// <inheritdoc/>
    public IObservable<IUsbDeviceInfo> DeviceDisconnected { get; }

    private readonly ManagementEventWatcher _connectWatcher;
    private readonly ManagementEventWatcher _disconnectWatcher;
    private readonly IDriveLetterWaiter _driveLetterWaiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsbDeviceMonitor"/> class.
    /// </summary>
    /// <param name="useCompositeDevices">If true, multiple devices detected within the time window are combined into a <see cref="CompositeUsbDeviceInfo"/>. Defaults to true.</param>
    /// <param name="scheduler">The scheduler to use for buffering and async operations. If null, <see cref="DefaultScheduler.Instance"/> is used.</param>
    /// <param name="millisecondsCompositor">The time window in milliseconds for grouping multiple device events into a composite device. Defaults to 20ms.</param>
    /// <param name="driveLetterWaiter">Optional custom implementation of <see cref="IDriveLetterWaiter"/> for waiting drive letter initialization. If null, default implementation is used.</param>
    public UsbDeviceMonitor(bool useCompositeDevices = true, IScheduler? scheduler = null, int millisecondsCompositor = 20, IDriveLetterWaiter? driveLetterWaiter = null)
    {
        _connectWatcher = GetManagementEventWatcher("__InstanceCreationEvent");
        _disconnectWatcher = GetManagementEventWatcher("__InstanceDeletionEvent");
        _driveLetterWaiter = driveLetterWaiter ?? new DriveLetterWaiter();

        var sched = scheduler ?? DefaultScheduler.Instance;

        if (useCompositeDevices)
        {
            // First buffer devices for compositing, then wait for drive letters
            var bufferedDevices = _rawDeviceConnectedSubject
                .Buffer(TimeSpan.FromMilliseconds(millisecondsCompositor), sched)
                .Where(buffer => buffer.Any())
                .Select(buffer => buffer.Count == 1 ? buffer.First() : new CompositeUsbDeviceInfo([.. buffer]));

            // Wait for drive letters for each device/composite
            DeviceConnected = bufferedDevices
                .SelectMany(device => Observable.FromAsync(() => _driveLetterWaiter.WaitForDriveLetterAsync(device)))
                .ObserveOn(sched);

            DeviceDisconnected = _deviceDisconnectedSubject
                .Buffer(TimeSpan.FromMilliseconds(millisecondsCompositor), sched)
                .Where(buffer => buffer.Any())
                .Select(buffer => buffer.Count == 1 ? buffer.First() : new CompositeUsbDeviceInfo([.. buffer]));
        }
        else
        {
            // Wait for drive letters for each device
            DeviceConnected = _rawDeviceConnectedSubject
                .SelectMany(device => Observable.FromAsync(() => _driveLetterWaiter.WaitForDriveLetterAsync(device)))
                .ObserveOn(sched);
            DeviceDisconnected = _deviceDisconnectedSubject;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IUsbDeviceInfo> GetConnectedDevices(
        UsbDeviceType types = UsbDeviceType.All,
        Func<IUsbDeviceInfo, bool>? predicate = null)
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

    /// <inheritdoc/>
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
            _deviceDisconnectedSubject.OnNext(device);
    }

    private void ConnectWatcher_EventArrived(object sender, EventArrivedEventArgs e)
    {
        var device = GetDeviceFromEventArgs(e);
        if (device != null)
            _rawDeviceConnectedSubject.OnNext(device);
    }

    /// <inheritdoc/>
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
#pragma warning restore CA1416 // Validate platform compatibility