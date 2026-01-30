# USBDeviceMonitor

Небольшая .NET-библиотека для Windows, которая позволяет:

- получать список подключенных в данный момент USB‑устройств;
- отслеживать события подключения и отключения устройств в реальном времени;
- агрегировать «составные» устройства (когда Windows сообщает сразу несколько логических устройств на одно физическое подключение) в один объект.

Проект состоит из библиотечного пакета `USBDeviceMonitor.Win` и простого примера консольного приложения `USBDevicesCheck`. Для тестов используется xUnit + System.Reactive.Testing.

Лицензия: MIT.

## Возможности

- WMI‑запросы к `Win32_PnPEntity` для получения актуального списка устройств USB.
- Подписка на события появления/исчезновения устройств через `ManagementEventWatcher`.
- Буферизация событий (по умолчанию 20 мс) и объединение нескольких событий в `CompositeUsbDeviceInfo` для удобной работы с составными устройствами.
- Классификация устройства по типу: Generic, Storage, HID, Printer, Video, Bluetooth, MediaDevice (см. `UsbDeviceType`).
- Выделение VID/PID/Serial из идентификатора устройства и определение буквы диска для USB‑накопителей.
- **Автоматическое ожидание инициализации буквы диска** для устройств хранения данных перед отправкой события подключения. Поддерживает USB, SCSI и UAS устройства.
- Поддержка частичного совпадения серийных номеров для корректной работы с UAS устройствами, где серийный номер в `Win32_PnPEntity` может отличаться от `Win32_DiskDrive`.

## Поддерживаемые платформы

- Windows (WMI, `System.Management`), .NET 7+ для библиотеки, .NET 8/9 для тестов и примера.
	- При сборке под .NET 7 возможны предупреждения совместимости некоторых пакетов NuGet. Рекомендуем целиться в .NET 8 или новее.

## Установка

Добавьте ссылку на проект `USBDeviceMonitor.Win` в вашем решении либо установите соответствующий пакет NuGet, если он публикуется в вашем окружении. В рамках этого репозитория можно просто сослаться на проект.

## Быстрый старт

Пример из `Source/USBDevicesCheck/Program.cs` (упрощённо):

```csharp
using System.Reactive.Linq;
using USBDeviceMonitor.Win;

var monitor = new UsbDeviceMonitor();

// Подписка на подключения только устройств типа Generic
var connSub = monitor.DeviceConnected
		.Where(d => d.Type.HasFlag(UsbDeviceType.Generic))
		.Subscribe(d => Console.WriteLine($"Connected: {d.Description} ({d.DeviceId})"));

var disconnSub = monitor.DeviceDisconnected
		.Where(d => d.Type.HasFlag(UsbDeviceType.Generic))
		.Subscribe(d => Console.WriteLine($"Disconnected: {d.Description} ({d.DeviceId})"));

// Текущие устройства
foreach (var device in monitor.GetConnectedDevices(UsbDeviceType.Generic))
		Console.WriteLine($"{device.Description} | {device.DeviceId} | {device.DriveLetter}");

monitor.StartMonitoring();
Console.WriteLine("Monitoring... Press any key to exit");
Console.ReadKey();
monitor.StopMonitoring();
```

## Основные API

Пространство имён: `USBDeviceMonitor.Win`

- `IUsbDeviceMonitor`
	- `IEnumerable<IUsbDeviceInfo> GetConnectedDevices(UsbDeviceType types = UsbDeviceType.All, Func<IUsbDeviceInfo, bool>? predicate = null)` — получить список подключённых устройств (с фильтром по типу и произвольным предикатом).
	- `IObservable<IUsbDeviceInfo> DeviceConnected` — поток событий о подключениях. **Для устройств хранения данных событие отправляется только после инициализации буквы диска** (ожидание до 5 секунд по умолчанию).
	- `IObservable<IUsbDeviceInfo> DeviceDisconnected` — поток событий об отключениях.
	- `void StartMonitoring()` / `void StopMonitoring()` — управление подписками на системные события.

- `UsbDeviceMonitor` — реализация `IUsbDeviceMonitor`.
	- Конструктор: `UsbDeviceMonitor(bool useCompositeDevices = true, IScheduler? scheduler = null, int millisecondsCompositor = 20, IDriveLetterWaiter? driveLetterWaiter = null)`
		- `useCompositeDevices = true` включает буферизацию и склейку нескольких событий в один `CompositeUsbDeviceInfo`.
		- `scheduler` — планировщик Rx для буферизации (по умолчанию `DefaultScheduler.Instance`).
		- `millisecondsCompositor` — время буферизации в миллисекундах для группировки событий (по умолчанию 20 мс).
		- `driveLetterWaiter` — опциональная реализация `IDriveLetterWaiter` для ожидания инициализации буквы диска (по умолчанию используется встроенная реализация).

- `IUsbDeviceInfo` — описание устройства:
	- `string Description`, `string DeviceId`, `string PNPDdeviceId`, `string VendorId`, `string ProductId`, `string Serial`, `string DriveLetter`, `UsbDeviceType Type`.
	- Для устройств с несколькими томами `DriveLetter` содержит все буквы дисков, разделённые `" | "`.

- `CompositeUsbDeviceInfo` — агрегирует несколько `IUsbDeviceInfo`:
	- Строковые свойства объединяются с удалением дубликатов через разделитель `" | "`.
	- Тип устройства — побитовое объединение флагов.
	- Свойство `Devices` предоставляет доступ к списку всех агрегированных устройств.

- `UsbDeviceType` — флаги типов устройств: `Generic`, `Storage`, `HumanInterface`, `Printer`, `Video`, `Bluetooth`, `MediaDevice`, `All`.

## Сборка и запуск (PowerShell)

Требования:

- .NET SDK 8.0+ (подойдёт и 9.0+). Проверьте: `dotnet --version`.
- Windows с поддержкой WMI (`System.Management`).

Собрать решение и запустить тесты:

```powershell
dotnet test Source\USBDeviceMonitor.sln -c Release
```

Запустить пример консольного приложения:

```powershell
dotnet run --project Source\USBDevicesCheck\USBDevicesCheck.csproj -c Debug
```

Примечания:

- При сборке проекта `USBDeviceMonitor.Win` на .NET 7 вы увидите предупреждения совместимости зависимостей `System.Management` и `System.CodeDom`. Рекомендуется целиться в .NET 8+ для продакшена.
- Библиотека использует WMI‑запросы и может требовать запуска с достаточными правами в корпоративных средах с ограничениями.

## Проверка

В репозитории есть тесты (`Source/USBDeviceMonitor.Tests`) для проверки:

- буферизации и агрегации событий в `CompositeUsbDeviceInfo`;
- прозрачной работы в режиме без агрегации;
- корректной агрегации строковых полей и флагов типов;
- функциональности ожидания инициализации буквы диска (`DriveLetterWaiter`).

На момент подготовки README тесты выполняются успешно (xUnit, .NET 8).

## Особенности работы с буквами дисков

Библиотека автоматически ожидает инициализацию буквы диска для устройств типа `Storage` и `Generic` (которые могут быть накопителями) перед отправкой события `DeviceConnected`. Это решает проблему, когда Windows ещё не успела назначить букву диска к моменту обнаружения устройства.

- Ожидание выполняется до 5 секунд (настраивается через `IDriveLetterWaiter`).
- Поддерживаются USB, SCSI и UAS устройства.
- Для UAS устройств используется частичное совпадение серийных номеров, так как формат может отличаться между `Win32_PnPEntity` и `Win32_DiskDrive`.
- Если буква диска не найдена в течение таймаута, событие всё равно отправляется с пустой `DriveLetter`.

## Лицензия

MIT — см. файл `LICENSE` в корне репозитория.