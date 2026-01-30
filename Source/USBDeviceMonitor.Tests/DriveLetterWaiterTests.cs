using USBDeviceMonitor.Win;

namespace USBDeviceMonitor.Tests;

public class DriveLetterWaiterTests
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
    public async Task WaitForDriveLetterAsync_NonStorageDevice_ReturnsImmediately()
    {
        IDriveLetterWaiter waiter = new DriveLetterWaiter();
        var device = new TestDevice 
        { 
            Type = UsbDeviceType.HumanInterface,
            PNPDdeviceId = "HID\\VID_1234&PID_5678\\SERIAL",
            Serial = "SERIAL"
        };

        var result = await waiter.WaitForDriveLetterAsync(device, timeoutMs: 100);

        Assert.Same(device, result);
    }

    [Fact]
    public async Task WaitForDriveLetterAsync_StorageDevice_ReturnsDevice()
    {
        IDriveLetterWaiter waiter = new DriveLetterWaiter();
        var device = new TestDevice 
        { 
            Type = UsbDeviceType.Storage,
            PNPDdeviceId = "USBSTOR\\Disk&Ven_Test&Prod_Device\\SERIAL",
            Serial = "SERIAL"
        };

        var result = await waiter.WaitForDriveLetterAsync(device, timeoutMs: 100);

        // Should return device (may or may not have drive letter depending on system state)
        Assert.NotNull(result);
    }

    [Fact]
    public async Task WaitForDriveLetterAsync_GenericDevice_ReturnsDevice()
    {
        IDriveLetterWaiter waiter = new DriveLetterWaiter();
        var device = new TestDevice 
        { 
            Type = UsbDeviceType.Generic,
            PNPDdeviceId = "USB\\VID_1234&PID_5678\\SERIAL",
            Serial = "SERIAL"
        };

        var result = await waiter.WaitForDriveLetterAsync(device, timeoutMs: 100);

        // Should return device (may or may not have drive letter depending on system state)
        Assert.NotNull(result);
    }

    [Fact]
    public async Task WaitForDriveLetterAsync_CompositeDevice_ReturnsDevice()
    {
        IDriveLetterWaiter waiter = new DriveLetterWaiter();
        var devices = new List<IUsbDeviceInfo>
        {
            new TestDevice { Type = UsbDeviceType.Generic, PNPDdeviceId = "USB\\VID_1234&PID_5678\\SERIAL1", Serial = "SERIAL1" },
            new TestDevice { Type = UsbDeviceType.Storage, PNPDdeviceId = "USBSTOR\\Disk&Ven_Test\\SERIAL2", Serial = "SERIAL2" }
        };
        var composite = new CompositeUsbDeviceInfo(devices);

        var result = await waiter.WaitForDriveLetterAsync(composite, timeoutMs: 100);

        Assert.NotNull(result);
        Assert.IsType<CompositeUsbDeviceInfo>(result);
    }

    [Fact]
    public async Task WaitForDriveLetterAsync_ReturnsOriginalDeviceOnTimeout()
    {
        IDriveLetterWaiter waiter = new DriveLetterWaiter();
        var device = new TestDevice 
        { 
            Type = UsbDeviceType.Storage,
            PNPDdeviceId = "USBSTOR\\Disk&Ven_Test&Prod_Device\\NONEXISTENT_SERIAL_12345",
            Serial = "NONEXISTENT_SERIAL_12345"
        };

        // Use short timeout for non-existent device
        var result = await waiter.WaitForDriveLetterAsync(device, timeoutMs: 100, checkIntervalMs: 20);

        Assert.NotNull(result);
        // Should return the original device when timeout occurs (no drive letter found for non-existent device)
        // The device should be the same instance or equivalent
        Assert.Equal(device.DeviceId, result.DeviceId);
        Assert.Equal(device.Serial, result.Serial);
    }
}
