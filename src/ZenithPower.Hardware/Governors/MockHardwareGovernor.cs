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

        public MockHardwareGovernor(int coreCount = 16)
        {
            for (int i = 0; i < coreCount; i++)
            {
                bool isStar = i < 2;
                int rank = i + 1;
                _cores.Add(new CpuCoreInfo(
                    coreIndex: i,
                    isStarCore: isStar,
                    ranking: rank,
                    nominalMhz: isStar ? 5050.0 : 4800.0,
                    effectiveMhz: isStar ? 5040.0 : 4790.0,
                    curveOffset: 0));
            }
        }

        public Task<bool> ApplyPowerProfileAsync(PowerProfile profile)
        {
            _activeProfile = profile;

            double nominalBase = profile.Type switch
            {
                ProfileType.SuperSaver => 2200.0,
                ProfileType.Saver => 2900.0,
                ProfileType.Normal => 3700.0,
                ProfileType.Performance => 4500.0,
                ProfileType.UltimatePerformance => 5000.0,
                _ => 3700.0
            };

            foreach (var core in _cores)
            {
                core.NominalMhz = core.IsStarCore ? nominalBase + 250.0 : nominalBase;
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
                ProfileType.SuperSaver => 38.0,
                ProfileType.Saver => 45.0,
                ProfileType.Normal => 58.0,
                ProfileType.Performance => 70.0,
                ProfileType.UltimatePerformance => 78.0,
                _ => 55.0
            };

            double cpuPower = _activeProfile.Type switch
            {
                ProfileType.SuperSaver => 28.0,
                ProfileType.Saver => 48.0,
                ProfileType.Normal => 85.0,
                ProfileType.Performance => 125.0,
                ProfileType.UltimatePerformance => 180.0,
                _ => 85.0
            };

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
                CpuName = "AMD Ryzen 9 5950X (Simulated)",
                PhysicalCoreCount = _cores.Count / 2,
                LogicalCoreCount = _cores.Count,
                CpuUsagePercent = _activeProfile.Type == ProfileType.SuperSaver ? 8.0 : 35.0,
                CpuPackageTempC = cpuTemp,
                CpuPackagePowerW = cpuPower,
                AverageNominalMhz = avgNominal,
                AverageEffectiveMhz = avgEffective,
                OverallClockStretchingRatio = avgNominal > 0 ? avgEffective / avgNominal : 1.0,
                ActiveThrottleLimitPercent = _activeProfile.ThrottleMaxPercent,
                Cores = new List<CpuCoreInfo>(_cores)
            };
        }
    }
}
