using System.Globalization;
using System.Text.RegularExpressions;

namespace AndroidCleanerPro.Services;

public sealed record InfoField(string Label, string Value);

public sealed record PhoneDetails(
    string Heading,
    string ModelNumber,
    string AndroidVersion,
    string BatteryLevel,
    string StorageSummary,
    IReadOnlyList<InfoField> Identity,
    IReadOnlyList<InfoField> Software,
    IReadOnlyList<InfoField> Battery,
    IReadOnlyList<InfoField> Hardware,
    IReadOnlyList<InfoField> Storage,
    IReadOnlyList<InfoField> Display);

/// <summary>Read-only device metadata using ordinary, owner-authorized ADB shell commands.</summary>
public sealed class PhoneDetailsService(AdbService adb)
{
    private const string Unknown = "Not reported by device";

    public async Task<PhoneDetails> ReadAsync(DeviceInfo device)
    {
        if (device.State != "device")
            throw new InvalidOperationException("The device must be connected and authorized in ADB.");

        // Individual optional diagnostics can fail on different OEM ROMs.
        // Getprop is the primary source; everything else is best-effort.
        var properties = await adb.ExecuteAsync(device.Serial, "shell", "getprop");
        var batteryTask = OptionalAsync(device.Serial, "shell", "dumpsys", "battery");
        var storageTask = OptionalAsync(device.Serial, "shell", "df", "-h", "/data");
        var memoryTask = OptionalAsync(device.Serial, "shell", "cat", "/proc/meminfo");
        var screenTask = OptionalAsync(device.Serial, "shell", "wm", "size");
        var densityTask = OptionalAsync(device.Serial, "shell", "wm", "density");
        await Task.WhenAll(batteryTask, storageTask, memoryTask, screenTask, densityTask);

        var props = ParseProperties(properties);
        string Prop(params string[] names)
        {
            foreach (var name in names)
            {
                if (props.TryGetValue(name, out var value) &&
                    !string.IsNullOrWhiteSpace(value) &&
                    !value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
                    return value;
            }
            return Unknown;
        }

        var manufacturer = Prop("ro.product.manufacturer", "ro.product.vendor.manufacturer");
        var model = Prop("ro.product.model", "ro.product.vendor.model");
        var marketedName = Prop("ro.product.marketname", "ro.product.odm.marketname",
            "ro.product.vendor.marketname", "ro.config.marketing_name");
        // Samsung's published model reference identifies SM-S938 as Galaxy S25 Ultra.
        // Explicitly label this as a model lookup instead of a device-reported name.
        if (marketedName == Unknown && model.StartsWith("SM-S938", StringComparison.OrdinalIgnoreCase))
            marketedName = "Galaxy S25 Ultra (model lookup)";
        var brand = Prop("ro.product.brand", "ro.product.vendor.brand");
        var product = Prop("ro.product.name", "ro.product.vendor.name");
        var codeName = Prop("ro.product.device", "ro.product.vendor.device");
        var android = Prop("ro.build.version.release");
        var api = Prop("ro.build.version.sdk");

        var batteryRaw = batteryTask.Result;
        var levelRaw = BatteryValue(batteryRaw, "level");
        var level = int.TryParse(levelRaw, out var parsedLevel) && parsedLevel >= 0 && parsedLevel <= 100
            ? parsedLevel.ToString(CultureInfo.InvariantCulture) + "%" : Unknown;
        var tempRaw = BatteryValue(batteryRaw, "temperature");
        var temperature = double.TryParse(tempRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var temp)
            ? (temp / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + " °C" : Unknown;
        var millivolts = BatteryValue(batteryRaw, "voltage");
        var voltage = int.TryParse(millivolts, out var mv)
            ? (mv / 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + " V" : Unknown;
        var batteryStatus = BatteryValue(batteryRaw, "status") switch
        {
            "2" => "Charging", "3" => "Discharging", "4" => "Not charging", "5" => "Full",
            "1" => "Unknown", _ => Unknown
        };
        var batteryHealth = BatteryValue(batteryRaw, "health") switch
        {
            "2" => "Good", "3" => "Overheat", "4" => "Dead", "5" => "Overvoltage",
            "6" => "Unspecified failure", "7" => "Cold", _ => Unknown
        };
        var charging = BatteryValue(batteryRaw, "USB powered") == "true" ? "USB"
            : BatteryValue(batteryRaw, "AC powered") == "true" ? "AC"
            : BatteryValue(batteryRaw, "Wireless powered") == "true" ? "Wireless"
            : "Not reported / not plugged in";

        var storage = StorageFields(storageTask.Result);
        var ram = MemorySummary(memoryTask.Result);
        var screen = FirstMatch(screenTask.Result, @"(?:Physical|Override) size:\s*([0-9]+x[0-9]+)");
        var density = FirstMatch(densityTask.Result, @"(?:Physical|Override) density:\s*(\d+)");

        var identity = new List<InfoField>
        {
            new("Manufacturer", manufacturer),
            new("Brand", brand),
            new("Model name (marketed)", marketedName),
            new("Model number", model),
            new("Product name", product),
            new("Device code name", codeName),
            new("Serial number / S/N", Prop("ro.serialno", "ro.boot.serialno")),
            new("ADB connection serial", device.Serial),
            new("Connection status", device.State),
            new("Sales / CSC region", Prop("ro.csc.sales_code", "ro.boot.sales_code"))
        };
        var software = new List<InfoField>
        {
            new("Android version", android),
            new("API level", api),
            new("Security patch", Prop("ro.build.version.security_patch")),
            new("Build number", Prop("ro.build.display.id")),
            new("Build fingerprint", Prop("ro.build.fingerprint")),
            new("Build incremental", Prop("ro.build.version.incremental")),
            new("Bootloader version", Prop("ro.bootloader")),
            new("Build type", Prop("ro.build.type"))
        };
        var batteryInfo = new List<InfoField>
        {
            new("Battery level", level),
            new("Battery status", batteryStatus),
            new("Battery health flag", batteryHealth),
            new("Temperature", temperature),
            new("Voltage", voltage),
            new("Power source", charging),
            new("Battery chemistry", ValueOrUnknown(BatteryValue(batteryRaw, "technology")))
        };
        var hardware = new List<InfoField>
        {
            new("Processor / SoC identifier", Prop("ro.board.platform", "ro.soc.model", "ro.hardware")),
            new("Hardware", Prop("ro.hardware", "ro.boot.hardware")),
            new("Primary CPU ABI", Prop("ro.product.cpu.abi")),
            new("Supported CPU ABIs", Prop("ro.product.cpu.abilist")),
            new("Installed RAM (usable)", ram),
            new("SIM configuration", Prop("persist.radio.multisim.config"))
        };
        var screenInfo = new List<InfoField>
        {
            new("Reported screen pixels", screen),
            new("Reported density (dpi)", density)
        };
        var heading = manufacturer == Unknown ? model :
            marketedName != Unknown ? $"{manufacturer} {marketedName.Replace(" (model lookup)", "")}" : $"{manufacturer} {model}";
        if (heading == Unknown) heading = device.Serial;

        return new PhoneDetails(heading, model, android, level,
            storage.FirstOrDefault(x => x.Label == "Storage use")?.Value ?? Unknown,
            identity, software, batteryInfo, hardware, storage, screenInfo);
    }

    private async Task<string> OptionalAsync(string serial, params string[] args)
    {
        try { return await adb.ExecuteAsync(serial, args); }
        catch { return ""; }
    }

    private static Dictionary<string, string> ParseProperties(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^\[([^\]]+)\]:\s*\[(.*)\]$");
            if (match.Success) values[match.Groups[1].Value] = match.Groups[2].Value;
        }
        return values;
    }

    private static string BatteryValue(string raw, string key)
    {
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                return trimmed[(key.Length + 1)..].Trim();
        }
        return "";
    }

    private static string FirstMatch(string source, string regex)
    {
        var match = Regex.Match(source, regex, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : Unknown;
    }

    private static string ValueOrUnknown(string? value)
        => string.IsNullOrWhiteSpace(value) ? Unknown : value.Trim();

    private static string MemorySummary(string source)
    {
        var value = FirstMatch(source, @"(?m)^MemTotal:\s*(\d+)\s+kB");
        return long.TryParse(value, out var kib)
            ? (kib / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " GiB usable"
            : Unknown;
    }

    private static List<InfoField> StorageFields(string raw)
    {
        // df output differs by Android version/OEM; do not assume a partition capacity.
        foreach (var line in raw.Split('\n').Skip(1))
        {
            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 6) continue;
            return
            [
                new("Storage use", $"{parts[2]} used of {parts[1]} ({parts[4]})"),
                new("Available space", parts[3]),
                new("Mounted at", parts[5]),
                new("Filesystem", parts[0])
            ];
        }
        return [new("Storage use", Unknown), new("Available space", Unknown)];
    }
}
