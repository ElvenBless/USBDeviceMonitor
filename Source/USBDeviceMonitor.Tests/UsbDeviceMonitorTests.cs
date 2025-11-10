using Microsoft.Reactive.Testing;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using USBDeviceMonitor.Win;

namespace USBDeviceMonitor.Tests;

public class UsbDeviceMonitorTests
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
    public void CompositeMode_GroupsMultipleConnectionsWithinWindow()
    {
        var scheduler = new TestScheduler();
        var monitor = new UsbDeviceMonitor(useCompositeDevices: true, scheduler: scheduler);

        IUsbDeviceInfo? received = null;
        monitor.DeviceConnected.Subscribe(d => received = d);

        // Emit three devices quickly
        var subjectField = typeof(UsbDeviceMonitor).GetField("_deviceConnectedSubject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var subj = (ISubject<IUsbDeviceInfo>)subjectField.GetValue(monitor)!;

        subj.OnNext(new TestDevice { DeviceId = "USB1" });
        subj.OnNext(new TestDevice { DeviceId = "USB2" });
        subj.OnNext(new TestDevice { DeviceId = "USB3" });

        // Advance time less than buffer window - nothing yet
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
        Assert.Null(received);

        // Advance past20ms window
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(15).Ticks);

        Assert.NotNull(received);
        var composite = Assert.IsType<CompositeUsbDeviceInfo>(received);
        Assert.Contains("USB1", composite.DeviceId);
        Assert.Contains("USB2", composite.DeviceId);
        Assert.Contains("USB3", composite.DeviceId);
    }

    [Fact]
    public void NonCompositeMode_PassesThroughSingleEvents()
    {
        var scheduler = new TestScheduler();
        var monitor = new UsbDeviceMonitor(useCompositeDevices: false, scheduler: scheduler);
        IUsbDeviceInfo? received = null;
        monitor.DeviceConnected.Subscribe(d => received = d);

        var subjectField = typeof(UsbDeviceMonitor).GetField("_deviceConnectedSubject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var subj = (ISubject<IUsbDeviceInfo>)subjectField.GetValue(monitor)!;

        var dev = new TestDevice { DeviceId = "USB_A" };
        subj.OnNext(dev);
        Assert.Same(dev, received);
    }

    [Fact]
    public void CompositeMode_SingleEvent_NotWrapped()
    {
        var scheduler = new TestScheduler();
        var monitor = new UsbDeviceMonitor(useCompositeDevices: true, scheduler: scheduler);
        IUsbDeviceInfo? received = null;
        monitor.DeviceConnected.Subscribe(d => received = d);

        var subjectField = typeof(UsbDeviceMonitor).GetField("_deviceConnectedSubject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var subj = (ISubject<IUsbDeviceInfo>)subjectField.GetValue(monitor)!;

        subj.OnNext(new TestDevice { DeviceId = "ONLY" });
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(25).Ticks);

        Assert.NotNull(received);
        Assert.IsNotType<CompositeUsbDeviceInfo>(received);
        Assert.Equal("ONLY", received!.DeviceId);
    }

    [Fact]
    public void CompositeMode_DisconnectEventsBuffered()
    {
        var scheduler = new TestScheduler();
        var monitor = new UsbDeviceMonitor(useCompositeDevices: true, scheduler: scheduler);
        IUsbDeviceInfo? received = null;
        monitor.DeviceDisconnected.Subscribe(d => received = d);

        var subjectField = typeof(UsbDeviceMonitor).GetField("_deviceDisconnectedSubject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var subj = (ISubject<IUsbDeviceInfo>)subjectField.GetValue(monitor)!;

        subj.OnNext(new TestDevice { DeviceId = "USB_X" });
        subj.OnNext(new TestDevice { DeviceId = "USB_Y" });
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(22).Ticks);

        Assert.NotNull(received);
        var composite = Assert.IsType<CompositeUsbDeviceInfo>(received);
        Assert.Contains("USB_X", composite.DeviceId);
        Assert.Contains("USB_Y", composite.DeviceId);
    }
}
