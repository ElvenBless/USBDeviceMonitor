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
        {
            Console.WriteLine(device.DeviceId);
            Console.WriteLine(device.ProductId);
            Console.WriteLine(device.VendorId);
            Console.WriteLine(device.Description);
            Console.WriteLine(device.DriveLetter);
            Console.WriteLine(device.Serial);
            Console.WriteLine(device.Type);
            Console.WriteLine("-----");
        }

        monitor.StartMonitoring();
        Console.WriteLine("Monitoring USB devices. Press any key to exit...");
        Console.ReadKey();
        monitor.StopMonitoring();
    }

    private static void OnDeviceConnected(IUsbDeviceInfo device)
    {
        Console.WriteLine();
        Console.WriteLine($"Connected:");
        Console.WriteLine(device.DeviceId);
        Console.WriteLine(device.ProductId);
        Console.WriteLine(device.VendorId);
        Console.WriteLine(device.Description);
        Console.WriteLine(device.DriveLetter);
        Console.WriteLine(device.Serial);
        Console.WriteLine(device.Type);
        Console.WriteLine("-----");
    }

    private static void OnDeviceDisconnected(IUsbDeviceInfo device)
    {
        Console.WriteLine();
        Console.WriteLine($"Disconnected:");
        Console.WriteLine(device.DeviceId);
        Console.WriteLine(device.ProductId);
        Console.WriteLine(device.VendorId);
        Console.WriteLine(device.Description);
        Console.WriteLine(device.Description);
        Console.WriteLine(device.DriveLetter);
        Console.WriteLine(device.Serial);
        Console.WriteLine(device.Type);
        Console.WriteLine("-----");
    }
}