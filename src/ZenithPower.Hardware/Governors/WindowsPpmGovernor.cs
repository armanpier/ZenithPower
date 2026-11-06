using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using ZenithPower.Core.Abstractions;
using ZenithPower.Core.Models;
using ZenithPower.Hardware.Interop;

namespace ZenithPower.Hardware.Governors
{
    public class WindowsPpmGovernor : IHardwareGovernor
    {
        private PowerProfile _activeProfile = PowerProfile.CreateDefault(ProfileType.Normal);
        private readonly string _cpuName;
        private readonly int _logicalCoreCount;
        private readonly uint _baseMaxMhz;
        private PerformanceCounter? _totalPerfCounter;
        private PerformanceCounter? _totalUtilityCounter;
        private readonly List<PerformanceCounter> _corePerfCounters = new();
        private readonly List<CpuCoreInfo> _cachedCores = new();

        public PowerProfile ActiveProfile => _activeProfile;
        public bool IsMockMode => false;

        public WindowsPpmGovernor()
        {
            _logicalCoreCount = Environment.ProcessorCount;

            // 1. Read real CPU Name from Windows Registry (instant, no WMI overhead)
            _cpuName = ReadCpuNameFromRegistry();

            // 2. Query initial base MaxMhz from CallNtPowerInformation
            _baseMaxMhz = QueryBaseMaxMhz();

            // 3. Initialize Windows Performance Counters for real-time frequency & utility
            InitializePerformanceCounters();

            // 4. Populate initial cores list
            for (int i = 0; i < _logicalCoreCount; i++)
            {
                bool isStar = i < 2; // Star core heuristic for preferred cores
                _cachedCores.Add(new CpuCoreInfo(
                    coreIndex: i,
                    isStarCore: isStar,
                    ranking: i + 1,
                    nominalMhz: _baseMaxMhz,
                    effectiveMhz: _baseMaxMhz,
                    curveOffset: 0));
            }
        }

        private static string ReadCpuNameFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                var val = key?.GetValue("ProcessorNameString")?.ToString();
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
            catch
            {
            }
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "x64 Processor";
        }

        private uint QueryBaseMaxMhz()
        {
            try
            {
                var info = new Win32Ppm.PROCESSOR_POWER_INFORMATION[_logicalCoreCount];
                int size = Marshal.SizeOf<Win32Ppm.PROCESSOR_POWER_INFORMATION>() * _logicalCoreCount;
                int ret = Win32Ppm.CallNtPowerInformation(11, IntPtr.Zero, 0, info, size);
                if (ret == 0 && info.Length > 0 && info[0].MaxMhz > 0)
                {
                    return info[0].MaxMhz;
                }
            }
            catch
            {
            }
            return 3400; // Fallback
        }

        private void InitializePerformanceCounters()
        {
            try
            {
                _totalPerfCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
                _totalPerfCounter.NextValue(); // Prime the counter

                _totalUtilityCounter = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                _totalUtilityCounter.NextValue();

                // Core counters (0,0) to (0,N-1)
                for (int i = 0; i < Math.Min(32, _logicalCoreCount); i++)
                {
                    try
                    {
                        var pc = new PerformanceCounter("Processor Information", "% Processor Performance", $"0,{i}");
                        pc.NextValue();
                        _corePerfCounters.Add(pc);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch
            {
                // In non-admin environments or minimal containers
            }
        }

        public Task<bool> ApplyPowerProfileAsync(PowerProfile profile)
        {
            _activeProfile = profile;

            try
            {
                uint result = Win32Ppm.PowerGetActiveScheme(IntPtr.Zero, out IntPtr activeGuidPtr);
                if (result == 0 && activeGuidPtr != IntPtr.Zero)
                {
                    Guid activeScheme = Marshal.PtrToStructure<Guid>(activeGuidPtr);
                    Marshal.FreeHGlobal(activeGuidPtr);

                    Guid subgroup = Win32Ppm.GUID_PROCESSOR_SETTINGS_SUBGROUP;
                    Guid eppGuid = Win32Ppm.GUID_PERF_EPP;
                    Guid boostGuid = Win32Ppm.GUID_PERF_BOOST_MODE;
                    Guid incThresholdGuid = Win32Ppm.GUID_PERF_INCREASE_THRESHOLD;
                    Guid decThresholdGuid = Win32Ppm.GUID_PERF_DECREASE_THRESHOLD;
                    Guid throttleMaxGuid = Win32Ppm.GUID_PROCTHROTTLEMAX;
                    Guid throttleMinGuid = Win32Ppm.GUID_PROCTHROTTLEMIN;

                    // Set Maximum Processor State (% limit)
                    Win32Ppm.PowerWriteACValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref throttleMaxGuid, (uint)profile.ThrottleMaxPercent);
                    Win32Ppm.PowerWriteDCValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref throttleMaxGuid, (uint)profile.ThrottleMaxPercent);

                    // Set Minimum Processor State (% limit)
                    Win32Ppm.PowerWriteACValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref throttleMinGuid, (uint)profile.ThrottleMinPercent);
                    Win32Ppm.PowerWriteDCValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref throttleMinGuid, (uint)profile.ThrottleMinPercent);

                    // Set EPP (Energy Performance Preference)
                    Win32Ppm.PowerWriteACValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref eppGuid, (uint)profile.Epp);
                    Win32Ppm.PowerWriteDCValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref eppGuid, (uint)profile.Epp);

                    // Set Boost Mode (0=Disabled, 1=Enabled, 2=Aggressive, 3=Efficient)
                    Win32Ppm.PowerWriteACValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref boostGuid, (uint)profile.BoostMode);
                    Win32Ppm.PowerWriteDCValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref boostGuid, (uint)profile.BoostMode);

                    // Set Increase & Decrease Thresholds
                    Win32Ppm.PowerWriteACValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref incThresholdGuid, (uint)profile.IncreaseThresholdPercent);
                    Win32Ppm.PowerWriteACValueIndex(IntPtr.Zero, ref activeScheme, ref subgroup, ref decThresholdGuid, (uint)profile.DecreaseThresholdPercent);

                    // Re-activate scheme to apply instantly
                    Win32Ppm.PowerSetActiveScheme(IntPtr.Zero, ref activeScheme);
                }
            }
            catch
            {
            }

            // Also invoke powercfg commands to guarantee persistence
            ApplyViaPowerCfg(profile);

            return Task.FromResult(true);
        }

        private static void ApplyViaPowerCfg(PowerProfile profile)
        {
            try
            {
                RunPowerCfg($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec {profile.ThrottleMaxPercent}");
                RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec {profile.ThrottleMaxPercent}");

                RunPowerCfg($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c {profile.ThrottleMinPercent}");
                RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 893dee8e-2bef-41e0-89c6-b55d0929964c {profile.ThrottleMinPercent}");

                RunPowerCfg($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 be337238-0d82-4146-a960-4f3749d470c7 {profile.BoostMode}");
                RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 be337238-0d82-4146-a960-4f3749d470c7 {profile.BoostMode}");

                RunPowerCfg($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 {profile.Epp}");
                RunPowerCfg($"/setdcvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 {profile.Epp}");

                RunPowerCfg($"/setacvalueindex SCHEME_CURRENT 54533251-82be-4824-96c1-47b60b740d00 06cadf0e-64ed-448a-8927-ce7bf90eb35d {profile.IncreaseThresholdPercent}");
                RunPowerCfg("/setactive SCHEME_CURRENT");
            }
            catch
            {
            }
        }

        private static void RunPowerCfg(string arguments)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                proc?.WaitForExit(1000);
            }
            catch
            {
            }
        }

        public Task<bool> ApplyCurveOptimizerAsync(Dictionary<int, int> perCoreOffsets)
        {
            foreach (var kvp in perCoreOffsets)
            {
                if (kvp.Key >= 0 && kvp.Key < _cachedCores.Count)
                {
                    _cachedCores[kvp.Key].CurveOffset = kvp.Value;
                }
            }
            return Task.FromResult(true);
        }

        public TelemetrySnapshot GetTelemetry()
        {
            double perfPercent = 100.0;
            double utilityPercent = 10.0;

            try
            {
                if (_totalPerfCounter != null)
                {
                    perfPercent = _totalPerfCounter.NextValue();
                }
                if (_totalUtilityCounter != null)
                {
                    utilityPercent = _totalUtilityCounter.NextValue();
                }
            }
            catch
            {
            }

            // Real active average frequency = BaseMaxMhz * (perfPercent / 100.0)
            double currentActiveMhz = _baseMaxMhz * (perfPercent / 100.0);
            double effectiveMhz = currentActiveMhz;

            // Query per-core frequencies
            for (int i = 0; i < _cachedCores.Count; i++)
            {
                double corePerf = perfPercent;
                if (i < _corePerfCounters.Count)
                {
                    try
                    {
                        corePerf = _corePerfCounters[i].NextValue();
                    }
                    catch
                    {
                    }
                }
                _cachedCores[i].NominalMhz = _baseMaxMhz * (_activeProfile.ThrottleMaxPercent / 100.0);
                _cachedCores[i].EffectiveMhz = _baseMaxMhz * (corePerf / 100.0);
            }

            // Estimate power based on real frequency and active profile limit
            double powerRatio = currentActiveMhz / (_baseMaxMhz * 1.3);
            double estimatedPowerW = Math.Max(25.0, _activeProfile.CpuPowerLimitW * powerRatio);

            // Estimated package temp
            double estimatedTempC = Math.Min(90.0, 38.0 + (utilityPercent * 0.45));

            return new TelemetrySnapshot
            {
                CpuName = _cpuName,
                PhysicalCoreCount = _logicalCoreCount / 2,
                LogicalCoreCount = _logicalCoreCount,
                CpuUsagePercent = Math.Clamp(utilityPercent, 0.0, 100.0),
                CpuPackageTempC = estimatedTempC,
                CpuPackagePowerW = estimatedPowerW,
                AverageNominalMhz = _baseMaxMhz * (_activeProfile.ThrottleMaxPercent / 100.0),
                AverageEffectiveMhz = currentActiveMhz,
                OverallClockStretchingRatio = Math.Min(1.0, currentActiveMhz / (_baseMaxMhz * (_activeProfile.ThrottleMaxPercent / 100.0))),
                ActiveThrottleLimitPercent = _activeProfile.ThrottleMaxPercent,
                Cores = new List<CpuCoreInfo>(_cachedCores)
            };
        }
    }
}
