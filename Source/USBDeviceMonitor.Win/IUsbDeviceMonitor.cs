#pragma warning disable CA1416 // Validate platform compatibility
namespace USBDeviceMonitor.Win
{
    /// <summary>
    /// Provides monitoring capabilities for USB devices on Windows platform.
    /// Allows querying currently connected devices and subscribing to device connection/disconnection events.
    /// </summary>
    public interface IUsbDeviceMonitor
    {
        /// <summary>
        /// Gets a list of currently connected USB devices matching the specified criteria.
        /// </summary>
        /// <param name="types">Bit flags indicating which device types to include. Defaults to <see cref="UsbDeviceType.All"/>.</param>
        /// <param name="predicate">Optional predicate function to filter devices. If null, all devices of the specified types are returned.</param>
        /// <returns>An enumerable collection of <see cref="IUsbDeviceInfo"/> objects representing connected USB devices.</returns>
        IEnumerable<IUsbDeviceInfo> GetConnectedDevices(
            UsbDeviceType types = UsbDeviceType.All,
            Func<IUsbDeviceInfo, bool>? predicate = null);

        /// <summary>
        /// Gets an observable sequence that emits an event when a USB device is connected.
        /// For storage devices, the event is emitted only after the drive letter has been initialized.
        /// </summary>
        IObservable<IUsbDeviceInfo> DeviceConnected { get; }

        /// <summary>
        /// Gets an observable sequence that emits an event when a USB device is disconnected.
        /// </summary>
        IObservable<IUsbDeviceInfo> DeviceDisconnected { get; }

        /// <summary>
        /// Starts monitoring USB device connection and disconnection events.
        /// Must be called before subscribing to <see cref="DeviceConnected"/> or <see cref="DeviceDisconnected"/> observables.
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Stops monitoring USB device connection and disconnection events.
        /// Unsubscribes from system events and stops emitting to observables.
        /// </summary>
        void StopMonitoring();
    }
}
#pragma warning restore CA1416 // Validate platform compatibility