using System.Reactive.Linq;
using USBDeviceMonitor.Win;

namespace USBDevicesCheck;

internal class Program
{
    static void Main(string[] args)
    {
        var monitor = new UsbDeviceMonitor();

        var subscription = monitor.DeviceConnected
            .Where(d => d.Type.HasFlag(UsbDeviceType.Generic))
            .Subscribe(OnDeviceConnected);

        monitor.DeviceDisconnected
            .Where(d => d.Type.HasFlag(UsbDeviceType.Generic))
            .Subscribe(OnDeviceDisconnected);

        Console.WriteLine("Current USB devices:");
        foreach (var device in monitor.GetConnectedDevices(UsbDeviceType.Generic))
            WriteDeviceInfoToConsole(device, null);

        monitor.StartMonitoring();
        Console.WriteLine("Monitoring USB devices. Press any key to exit...");
        Console.ReadKey();
        monitor.StopMonitoring();
    }

    private static void OnDeviceConnected(IUsbDeviceInfo device)
    {
        Console.WriteLine();
        WriteDeviceInfoToConsole(device, "Connected:");
    }

    private static void OnDeviceDisconnected(IUsbDeviceInfo device)
    {
        Console.WriteLine();
        WriteDeviceInfoToConsole(device, "Disconnected:");
    }

    // Helper to print device properties with their names. If header is null, no header is printed.
    private static void WriteDeviceInfoToConsole(IUsbDeviceInfo device, string? header)
    {
        if (!string.IsNullOrEmpty(header))
            Console.WriteLine(header);

        void Write(string name, object? value)
        {
            Console.WriteLine($"{name}: {value}");
        }

        Write(nameof(device.DeviceId), device.DeviceId);
        Write(nameof(device.ProductId), device.ProductId);
        Write(nameof(device.VendorId), device.VendorId);
        Write(nameof(device.Description), device.Description);
        Write(nameof(device.Serial), device.Serial);
        Write(nameof(device.Type), device.Type);
        Write(nameof(device.DriveLetter), device.DriveLetter);
        Console.WriteLine("-----");
    }
}