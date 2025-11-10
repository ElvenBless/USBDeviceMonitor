namespace USBDeviceMonitor.Win;

/// <summary>
/// Represents a composite USB device that may be reported as multiple separate devices by the system.
/// This class aggregates information from multiple <see cref="IUsbDeviceInfo"/> objects.
/// </summary>
public class CompositeUsbDeviceInfo : IUsbDeviceInfo
{
    private readonly List<IUsbDeviceInfo> _allDevices;

    public CompositeUsbDeviceInfo(IList<IUsbDeviceInfo> devices)
    {
        _allDevices = [.. devices];

        // Aggregate properties
        Type = _allDevices.Aggregate((UsbDeviceType)0, (current, d) => current | d.Type);
        Description = string.Join(" | ", _allDevices.Select(d => d.Description).Where(s => !string.IsNullOrEmpty(s)).Distinct());
    }

    public string Description { get; }
    public UsbDeviceType Type { get; }

    #region Aggregated Device Properties
    // Combine properties from all underlying devices. Non-empty distinct values are joined with " | "
    private static string JoinDistinct(IEnumerable<string?> values)
        => string.Join(" | ", values.Where(s => !string.IsNullOrEmpty(s)).Distinct());

    public string DeviceId => JoinDistinct(_allDevices.Select(d => d.DeviceId));
    public string PNPDdeviceId => JoinDistinct(_allDevices.Select(d => d.PNPDdeviceId));
    public string VendorId => JoinDistinct(_allDevices.Select(d => d.VendorId));
    public string ProductId => JoinDistinct(_allDevices.Select(d => d.ProductId));
    public string Serial => JoinDistinct(_allDevices.Select(d => d.Serial));
    public string DriveLetter => JoinDistinct(_allDevices.Select(d => d.DriveLetter));
    #endregion
}
