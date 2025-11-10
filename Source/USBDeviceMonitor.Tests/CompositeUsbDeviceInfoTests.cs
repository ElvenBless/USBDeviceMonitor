using USBDeviceMonitor.Win;

namespace USBDeviceMonitor.Tests;

public class CompositeUsbDeviceInfoTests
{
    private class TestDevice : IUsbDeviceInfo
    {
        public string Description { get; init; } = string.Empty;
        public string DeviceId { get; init; } = string.Empty;
        public string DriveLetter { get; init; } = string.Empty;
        public string PNPDdeviceId { get; init; } = string.Empty;
        public string ProductId { get; init; } = string.Empty;
        public string Serial { get; init; } = string.Empty;
        public UsbDeviceType Type { get; init; } = UsbDeviceType.Generic;
        public string VendorId { get; init; } = string.Empty;
    }

    [Fact]
    public void Aggregates_All_String_Fields_Distinct()
    {
        var devices = new List<IUsbDeviceInfo>
        {
            new TestDevice { DeviceId = "A", ProductId = "P1", VendorId = "V1", Description = "D1", DriveLetter = "E:", Serial = "S1", PNPDdeviceId = "PNP1", Type = UsbDeviceType.Generic },
            new TestDevice { DeviceId = "B", ProductId = "P2", VendorId = "V2", Description = "D2", DriveLetter = "F:", Serial = "S2", PNPDdeviceId = "PNP2", Type = UsbDeviceType.Storage },
            new TestDevice { DeviceId = "A", ProductId = "P1", VendorId = "V1", Description = "D1", DriveLetter = "", Serial = "S1", PNPDdeviceId = "PNP1", Type = UsbDeviceType.HumanInterface },
        };

        var composite = new CompositeUsbDeviceInfo(devices);

        Assert.Contains("A", composite.DeviceId);
        Assert.Contains("B", composite.DeviceId);
        Assert.Contains("P1", composite.ProductId);
        Assert.Contains("P2", composite.ProductId);
        Assert.Contains("V1", composite.VendorId);
        Assert.Contains("V2", composite.VendorId);
        Assert.Contains("D1", composite.Description);
        Assert.Contains("D2", composite.Description);
        Assert.Contains("E:", composite.DriveLetter);
        Assert.Contains("F:", composite.DriveLetter);
        Assert.Contains("S1", composite.Serial);
        Assert.Contains("S2", composite.Serial);
        Assert.Contains("PNP1", composite.PNPDdeviceId);
        Assert.Contains("PNP2", composite.PNPDdeviceId);
        Assert.True(composite.Type.HasFlag(UsbDeviceType.Generic));
        Assert.True(composite.Type.HasFlag(UsbDeviceType.Storage));
        Assert.True(composite.Type.HasFlag(UsbDeviceType.HumanInterface));
    }
}
