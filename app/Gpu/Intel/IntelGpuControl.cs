using GHelper.Helpers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GHelper.Gpu.Intel;

public sealed class IntelGpuControl : IDisposable
{
    private const int ZeResultSuccess = 0;
    private const int ZesStructureTypeFreqProperties = 0x9;
    private const int ZesStructureTypePowerProperties = 0xd;
    private const int ZesStructureTypeTempProperties = 0x14;
    private const int ZesStructureTypeFreqState = 0x1b;
    private const int ZesFreqDomainGpu = 0;
    private const int ZesTempSensorsGlobal = 0;
    private const int ZesTempSensorsGpu = 1;
    private const int ZesTempSensorsGpuBoard = 6;

    private static readonly object LogLock = new();
    private static readonly HashSet<string> LoggedMessages = new();

    private readonly object _sensorFailureLock = new();
    private readonly HashSet<string> _loggedSensorFailures = new();

    private nint _temperatureSensor;
    private nint _frequencyDomain;
    private nint _powerDomain;
    private ZesPowerEnergyCounter? _lastEnergyCounter;

    public IntelGpuControl()
    {
        if (!PawnIO.CpuInfo.IsIntel) return;

        try
        {
            Initialize();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            LogOnce("Intel iGPU Sysman unavailable: " + ex.Message);
        }
        catch (Exception ex)
        {
            LogOnce("Intel iGPU Sysman init failed: " + ex.Message);
        }
    }

    public bool IsValid => _temperatureSensor != nint.Zero || _frequencyDomain != nint.Zero || _powerDomain != nint.Zero;

    private void Initialize()
    {
        int result = zesInit(0);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU Sysman init failed: " + FormatResult(result));
            return;
        }

        foreach (nint driver in GetDrivers())
        {
            foreach (nint device in GetDevices(driver))
            {
                _temperatureSensor = FindTemperatureSensor(device);
                _frequencyDomain = FindGpuFrequencyDomain(device);
                _powerDomain = FindPowerDomain(device);

                if (IsValid)
                {
                    Logger.WriteLine("Intel iGPU sensors: Level Zero Sysman"
                        + $" (temperature: {(_temperatureSensor != nint.Zero ? "yes" : "no")}, clock: {(_frequencyDomain != nint.Zero ? "yes" : "no")}, power: {(_powerDomain != nint.Zero ? "yes" : "no")})");
                    return;
                }
            }
        }

        LogOnce("Intel iGPU Sysman: no GPU temperature, frequency or power sensors found");
    }

    public int? GetIntegratedTemperature()
    {
        if (_temperatureSensor == nint.Zero) return null;

        try
        {
            int result = zesTemperatureGetState(_temperatureSensor, out double temperature);
            if (result != ZeResultSuccess)
            {
                LogSensorFailure("Intel iGPU temperature", result);
                return null;
            }

            return temperature > 0 && temperature < 125 ? (int)Math.Round(temperature) : null;
        }
        catch (Exception ex)
        {
            LogSensorFailure("Intel iGPU temperature", ex);
            return null;
        }
    }

    public int? GetIntegratedGpuClock()
    {
        if (_frequencyDomain == nint.Zero) return null;

        try
        {
            var state = new ZesFreqState { stype = ZesStructureTypeFreqState };
            int result = zesFrequencyGetState(_frequencyDomain, ref state);
            if (result != ZeResultSuccess)
            {
                LogSensorFailure("Intel iGPU clock", result);
                return null;
            }

            return state.actual > 0 && state.actual < 5000 ? (int)Math.Round(state.actual) : null;
        }
        catch (Exception ex)
        {
            LogSensorFailure("Intel iGPU clock", ex);
            return null;
        }
    }

    public float? GetIntegratedGpuPower()
    {
        if (_powerDomain == nint.Zero) return null;

        try
        {
            int result = zesPowerGetEnergyCounter(_powerDomain, out ZesPowerEnergyCounter current);
            if (result != ZeResultSuccess)
            {
                _lastEnergyCounter = null;
                LogSensorFailure("Intel iGPU power", result);
                return null;
            }

            if (_lastEnergyCounter is not { } previous)
            {
                _lastEnergyCounter = current;
                return null;
            }

            _lastEnergyCounter = current;

            if (current.timestamp <= previous.timestamp || current.energy <= previous.energy)
                return null;

            double watts = (double)(current.energy - previous.energy) / (current.timestamp - previous.timestamp);
            return watts > 0 && watts < 300 ? (float)watts : null;
        }
        catch (Exception ex)
        {
            _lastEnergyCounter = null;
            LogSensorFailure("Intel iGPU power", ex);
            return null;
        }
    }

    private static nint[] GetDrivers()
    {
        uint count = 0;
        int result = zesDriverGet(ref count, null);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU Sysman driver enumeration failed: " + FormatResult(result));
            return [];
        }
        if (count == 0) return [];

        nint[] drivers = new nint[count];
        result = zesDriverGet(ref count, drivers);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU Sysman driver enumeration failed: " + FormatResult(result));
            return [];
        }

        return drivers.Take((int)count).ToArray();
    }

    private static nint[] GetDevices(nint driver)
    {
        uint count = 0;
        int result = zesDeviceGet(driver, ref count, null);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU Sysman device enumeration failed: " + FormatResult(result));
            return [];
        }
        if (count == 0) return [];

        nint[] devices = new nint[count];
        result = zesDeviceGet(driver, ref count, devices);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU Sysman device enumeration failed: " + FormatResult(result));
            return [];
        }

        return devices.Take((int)count).ToArray();
    }

    private static nint FindTemperatureSensor(nint device)
    {
        nint bestSensor = nint.Zero;
        int bestPriority = int.MaxValue;

        foreach (nint sensor in GetTemperatureSensors(device))
        {
            var properties = new ZesTempProperties { stype = ZesStructureTypeTempProperties };
            int result = zesTemperatureGetProperties(sensor, ref properties);
            if (result != ZeResultSuccess) continue;

            int priority = properties.type switch
            {
                ZesTempSensorsGpu => 0,
                ZesTempSensorsGlobal => 1,
                ZesTempSensorsGpuBoard => 2,
                _ => int.MaxValue
            };

            if (priority < bestPriority)
            {
                bestPriority = priority;
                bestSensor = sensor;
            }
        }

        return bestSensor;
    }

    private static nint[] GetTemperatureSensors(nint device)
    {
        uint count = 0;
        int result = zesDeviceEnumTemperatureSensors(device, ref count, null);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU temperature sensor enumeration failed: " + FormatResult(result));
            return [];
        }
        if (count == 0) return [];

        nint[] sensors = new nint[count];
        result = zesDeviceEnumTemperatureSensors(device, ref count, sensors);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU temperature sensor enumeration failed: " + FormatResult(result));
            return [];
        }

        return sensors.Take((int)count).ToArray();
    }

    private static nint FindGpuFrequencyDomain(nint device)
    {
        foreach (nint domain in GetFrequencyDomains(device))
        {
            var properties = new ZesFreqProperties { stype = ZesStructureTypeFreqProperties };
            int result = zesFrequencyGetProperties(domain, ref properties);
            if (result == ZeResultSuccess && properties.type == ZesFreqDomainGpu)
                return domain;
        }

        return nint.Zero;
    }

    private static nint FindPowerDomain(nint device)
    {
        nint subdevicePower = nint.Zero;

        foreach (nint domain in GetPowerDomains(device))
        {
            var properties = new ZesPowerProperties { stype = ZesStructureTypePowerProperties };
            int result = zesPowerGetProperties(domain, ref properties);
            if (result != ZeResultSuccess) continue;

            result = zesPowerGetEnergyCounter(domain, out _);
            if (result != ZeResultSuccess) continue;

            if (properties.onSubdevice == 0)
                return domain;

            subdevicePower = domain;
        }

        return subdevicePower;
    }

    private static nint[] GetPowerDomains(nint device)
    {
        uint count = 0;
        int result = zesDeviceEnumPowerDomains(device, ref count, null);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU power domain enumeration failed: " + FormatResult(result));
            return [];
        }
        if (count == 0) return [];

        nint[] domains = new nint[count];
        result = zesDeviceEnumPowerDomains(device, ref count, domains);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU power domain enumeration failed: " + FormatResult(result));
            return [];
        }

        return domains.Take((int)count).ToArray();
    }

    private static nint[] GetFrequencyDomains(nint device)
    {
        uint count = 0;
        int result = zesDeviceEnumFrequencyDomains(device, ref count, null);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU frequency domain enumeration failed: " + FormatResult(result));
            return [];
        }
        if (count == 0) return [];

        nint[] domains = new nint[count];
        result = zesDeviceEnumFrequencyDomains(device, ref count, domains);
        if (result != ZeResultSuccess)
        {
            LogOnce("Intel iGPU frequency domain enumeration failed: " + FormatResult(result));
            return [];
        }

        return domains.Take((int)count).ToArray();
    }

    private void LogSensorFailure(string source, int result)
    {
        LogSensorFailure(source, FormatResult(result));
    }

    private void LogSensorFailure(string source, Exception ex)
    {
        LogSensorFailure(source, ex.Message);
    }

    private void LogSensorFailure(string source, string message)
    {
        lock (_sensorFailureLock)
        {
            if (_loggedSensorFailures.Add(source))
                Logger.WriteLine(source + " read failed: " + message);
        }

        Debug.WriteLine(source + " read failed: " + message);
    }

    private static void LogOnce(string message)
    {
        lock (LogLock)
        {
            if (LoggedMessages.Add(message))
                Logger.WriteLine(message);
        }
    }

    private static string FormatResult(int result)
    {
        return "0x" + result.ToString("X8");
    }

    public void Dispose()
    {
        _temperatureSensor = nint.Zero;
        _frequencyDomain = nint.Zero;
        _powerDomain = nint.Zero;
        _lastEnergyCounter = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ZesFreqProperties
    {
        public int stype;
        public nint pNext;
        public int type;
        public byte onSubdevice;
        public uint subdeviceId;
        public byte canControl;
        public byte isThrottleEventSupported;
        public double min;
        public double max;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ZesFreqState
    {
        public int stype;
        public nint pNext;
        public double currentVoltage;
        public double request;
        public double tdp;
        public double efficient;
        public double actual;
        public uint throttleReasons;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ZesPowerProperties
    {
        public int stype;
        public nint pNext;
        public byte onSubdevice;
        public uint subdeviceId;
        public byte canControl;
        public byte isEnergyThresholdSupported;
        public int defaultLimit;
        public int minLimit;
        public int maxLimit;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ZesPowerEnergyCounter
    {
        public ulong energy;
        public ulong timestamp;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ZesTempProperties
    {
        public int stype;
        public nint pNext;
        public int type;
        public byte onSubdevice;
        public uint subdeviceId;
        public double maxTemperature;
        public byte isCriticalTempSupported;
        public byte isThreshold1Supported;
        public byte isThreshold2Supported;
    }

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesInit(uint flags);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesDriverGet(ref uint pCount, [Out] nint[]? phDrivers);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesDeviceGet(nint hDriver, ref uint pCount, [Out] nint[]? phDevices);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesDeviceEnumFrequencyDomains(nint hDevice, ref uint pCount, [Out] nint[]? phFrequency);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesFrequencyGetProperties(nint hFrequency, ref ZesFreqProperties pProperties);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesFrequencyGetState(nint hFrequency, ref ZesFreqState pState);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesDeviceEnumPowerDomains(nint hDevice, ref uint pCount, [Out] nint[]? phPower);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesPowerGetProperties(nint hPower, ref ZesPowerProperties pProperties);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesPowerGetEnergyCounter(nint hPower, out ZesPowerEnergyCounter pEnergy);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesDeviceEnumTemperatureSensors(nint hDevice, ref uint pCount, [Out] nint[]? phTemperature);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesTemperatureGetProperties(nint hTemperature, ref ZesTempProperties pProperties);

    [DllImport("ze_loader.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int zesTemperatureGetState(nint hTemperature, out double pTemperature);
}
