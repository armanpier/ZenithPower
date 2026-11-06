using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZenithPower.Core.Abstractions;
using ZenithPower.Core.Models;

namespace ZenithPower.Hardware.Governors
{
    public class MockHardwareGovernor : IHardwareGovernor
    {
        private PowerProfile _activeProfile = PowerProfile.CreateDefault(ProfileType.Normal);
        private readonly List<CpuCoreInfo> _cores = new();

        public PowerProfile ActiveProfile => _activeProfile;
        public bool IsMockMode => true;

        public MockHardwareGovernor(int coreCount = 8)
        {
            for (int i = 0; i < coreCount; i++)
            {
                bool isStar = i < 2;
                int rank = i + 1;
                _cores.Add(new CpuCoreInfo(
                    coreIndex: i,
                    isStarCore: isStar,
                    ranking: rank,
                    nominalMhz: isStar ? 5400.0 : 5000.0,
                    effectiveMhz: isStar ? 5390.0 : 4990.0,
                    curveOffset: 0));
            }
        }

        public Task<bool> ApplyPowerProfileAsync(PowerProfile profile)
        {
            _activeProfile = profile;

            // Adjust simulated clocks and voltages based on profile
            double nominalBase = profile.Type switch
            {
                ProfileType.SuperSaver => 2800.0,
                ProfileType.Saver => 3600.0,
                ProfileType.Normal => 4800.0,
                ProfileType.Performance => 5200.0,
                ProfileType.UltimatePerformance => 5500.0,
                _ => 4800.0
            };

            foreach (var core in _cores)
            {
                core.NominalMhz = core.IsStarCore ? nominalBase + 300.0 : nominalBase;
                // If negative curve offset is within safe zone, effective tracks closely.
                // If curve offset is deeper than -25 on non-stars, trigger mild clock stretching in mock
                if (core.CurveOffset < -26)
                {
                    core.EffectiveMhz = core.NominalMhz * 0.94; // Mild stretching simulation
                }
                else
                {
                    core.EffectiveMhz = core.NominalMhz * 0.995;
                }
            }

            return Task.FromResult(true);
        }

        public Task<bool> ApplyCurveOptimizerAsync(Dictionary<int, int> perCoreOffsets)
        {
            foreach (var kvp in perCoreOffsets)
            {
                if (kvp.Key >= 0 && kvp.Key < _cores.Count)
                {
                    _cores[kvp.Key].CurveOffset = kvp.Value;
                    if (kvp.Value < -26)
                    {
                        _cores[kvp.Key].EffectiveMhz = _cores[kvp.Key].NominalMhz * 0.94;
                    }
                    else
                    {
                        _cores[kvp.Key].EffectiveMhz = _cores[kvp.Key].NominalMhz * 0.995;
                    }
                }
            }

            return Task.FromResult(true);
        }

        public TelemetrySnapshot GetTelemetry()
        {
            double cpuTemp = _activeProfile.Type switch
            {
                ProfileType.SuperSaver => 41.5,
                ProfileType.Saver => 48.0,
                ProfileType.Normal => 62.0,
                ProfileType.Performance => 74.0,
                ProfileType.UltimatePerformance => 82.0,
                _ => 60.0
            };

            double cpuPower = _activeProfile.Type switch
            {
                ProfileType.SuperSaver => 32.0,
                ProfileType.Saver => 58.0,
                ProfileType.Normal => 102.0,
                ProfileType.Performance => 140.0,
                ProfileType.UltimatePerformance => 220.0,
                _ => 95.0
            };

            double gpuClock = _activeProfile.Type switch
            {
                ProfileType.SuperSaver => 800.0,
                ProfileType.Saver => 1400.0,
                ProfileType.Normal => 1950.0,
                ProfileType.Performance => 2350.0,
                ProfileType.UltimatePerformance => 2600.0,
                _ => 1800.0
            };

            double gpuPower = (_activeProfile.GpuPowerLimitPercent / 100.0) * 280.0 * (_activeProfile.Type == ProfileType.SuperSaver ? 0.3 : 0.85);

            double sumNominal = 0;
            double sumEffective = 0;
            foreach (var core in _cores)
            {
                sumNominal += core.NominalMhz;
                sumEffective += core.EffectiveMhz;
            }

            double avgNominal = sumNominal / _cores.Count;
            double avgEffective = sumEffective / _cores.Count;

            return new TelemetrySnapshot
            {
                CpuPackageTempC = cpuTemp,
                CpuPackagePowerW = cpuPower,
                AverageNominalMhz = avgNominal,
                AverageEffectiveMhz = avgEffective,
                OverallClockStretchingRatio = avgNominal > 0 ? avgEffective / avgNominal : 1.0,
                GpuTempC = cpuTemp - 5.0,
                GpuHotspotTempC = cpuTemp + 7.0,
                GpuPowerW = gpuPower,
                GpuUsagePercent = _activeProfile.Type == ProfileType.SuperSaver ? 15.0 : 65.0,
                GpuClockMhz = gpuClock,
                Cores = new List<CpuCoreInfo>(_cores)
            };
        }
    }
}
