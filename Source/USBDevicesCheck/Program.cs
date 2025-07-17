using USBDeviceMonitor.Win;

namespace USBDevicesCheck;

internal class Program
{
    static void Main(string[] args)
    {
        var monitor = new UsbDeviceMonitor();

        monitor.DeviceConnected.Subscribe(OnDeviceConnected);
        monitor.DeviceDisconnected.Subscribe(OnDeviceDisconnected);

        Console.WriteLine("Current USB devices:");
        foreach (var device in monitor.GetConnectedDevices())
        {
            Console.WriteLine(device.DeviceId);
            Console.WriteLine(device.ProductId);
            Console.WriteLine(device.VendorId);
            Console.WriteLine(device.Description);
            Console.WriteLine("-----");
        }

        monitor.StartMonitoring();
        Console.WriteLine("Monitoring USB devices. Press any key to exit...");
        Console.ReadKey();
        monitor.StopMonitoring();
    }

    private static void OnDeviceConnected(UsbDeviceInfo device)
    {
        Console.WriteLine();
        Console.WriteLine($"Connected:");
        Console.WriteLine(device.DeviceId);
        Console.WriteLine(device.ProductId);
        Console.WriteLine(device.VendorId);
        Console.WriteLine(device.Description);
        Console.WriteLine("-----");
    }

    private static void OnDeviceDisconnected(UsbDeviceInfo device)
    {
        Console.WriteLine();
        Console.WriteLine($"Disconnected:");
        Console.WriteLine(device.DeviceId);
        Console.WriteLine(device.ProductId);
        Console.WriteLine(device.VendorId);
        Console.WriteLine(device.Description);
        Console.WriteLine("-----");
    }
}