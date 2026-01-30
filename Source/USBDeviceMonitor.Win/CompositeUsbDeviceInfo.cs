namespace USBDeviceMonitor.Win;

/// <summary>
/// Represents a composite USB device that may be reported as multiple separate devices by the system.
/// This class aggregates information from multiple <see cref="IUsbDeviceInfo"/> objects.
/// </summary>
public class CompositeUsbDeviceInfo : IUsbDeviceInfo
{
    private readonly List<IUsbDeviceInfo> _allDevices;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeUsbDeviceInfo"/> class from a list of USB device information objects.
    /// </summary>
    /// <param name="devices">The list of <see cref="IUsbDeviceInfo"/> objects to aggregate into a composite device.</param>
    public CompositeUsbDeviceInfo(IList<IUsbDeviceInfo> devices)
    {
        _allDevices = [.. devices];

        // Aggregate properties
        Type = _allDevices.Aggregate((UsbDeviceType)0, (current, d) => current | d.Type);
        Description = string.Join(" | ", _allDevices.Select(d => d.Description).Where(s => !string.IsNullOrEmpty(s)).Distinct());
    }

    /// <summary>
    /// Gets the aggregated description combining descriptions from all underlying devices.
    /// Distinct non-empty descriptions are joined with " | " separator.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the combined device type flags from all underlying devices.
    /// </summary>
    public UsbDeviceType Type { get; }
    
    /// <summary>
    /// Gets the list of underlying devices that make up this composite device.
    /// </summary>
    public IReadOnlyList<IUsbDeviceInfo> Devices => _allDevices;

    #region Aggregated Device Properties
    // Combine properties from all underlying devices. Non-empty distinct values are joined with " | "
    private static string JoinDistinct(IEnumerable<string?> values)
        => string.Join(" | ", values.Where(s => !string.IsNullOrEmpty(s)).Distinct());

    /// <summary>
    /// Gets the aggregated device IDs from all underlying devices, joined with " | " separator.
    /// </summary>
    public string DeviceId => JoinDistinct(_allDevices.Select(d => d.DeviceId));

    /// <summary>
    /// Gets the aggregated PNP device IDs from all underlying devices, joined with " | " separator.
    /// </summary>
    public string PNPDdeviceId => JoinDistinct(_allDevices.Select(d => d.PNPDdeviceId));

    /// <summary>
    /// Gets the aggregated vendor IDs from all underlying devices, joined with " | " separator.
    /// </summary>
    public string VendorId => JoinDistinct(_allDevices.Select(d => d.VendorId));

    /// <summary>
    /// Gets the aggregated product IDs from all underlying devices, joined with " | " separator.
    /// </summary>
    public string ProductId => JoinDistinct(_allDevices.Select(d => d.ProductId));

    /// <summary>
    /// Gets the aggregated serial numbers from all underlying devices, joined with " | " separator.
    /// </summary>
    public string Serial => JoinDistinct(_allDevices.Select(d => d.Serial));

    /// <summary>
    /// Gets the aggregated drive letters from all underlying devices, joined with " | " separator.
    /// </summary>
    public string DriveLetter => JoinDistinct(_allDevices.Select(d => d.DriveLetter));
    #endregion
}
