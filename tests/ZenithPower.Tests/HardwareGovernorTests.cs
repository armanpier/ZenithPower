using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZenithPower.Core.Abstractions;
using ZenithPower.Core.Models;
using ZenithPower.Hardware.Governors;

namespace ZenithPower.Tests
{
    public class HardwareGovernorTests
    {
        [Fact]
        public async Task MockGovernor_AppliesSuperSaverProfile_ReflectsLowPowerTelemetry()
        {
            IHardwareGovernor governor = new MockHardwareGovernor();
            var profile = PowerProfile.CreateDefault(ProfileType.SuperSaver);

            bool success = await governor.ApplyPowerProfileAsync(profile);
            var telemetry = governor.GetTelemetry();

            Assert.True(success);
            Assert.Equal(ProfileType.SuperSaver, governor.ActiveProfile.Type);
            Assert.True(telemetry.CpuPackagePowerW <= 40.0);
            Assert.True(telemetry.CpuPackageTempC <= 50.0);
            Assert.Equal(65, telemetry.ActiveThrottleLimitPercent);
        }

        [Fact]
        public async Task MockGovernor_AppliesUltimateProfile_ReflectsHighPerformanceTelemetry()
        {
            IHardwareGovernor governor = new MockHardwareGovernor();
            var profile = PowerProfile.CreateDefault(ProfileType.UltimatePerformance);

            bool success = await governor.ApplyPowerProfileAsync(profile);
            var telemetry = governor.GetTelemetry();

            Assert.True(success);
            Assert.Equal(ProfileType.UltimatePerformance, governor.ActiveProfile.Type);
            Assert.True(telemetry.CpuPackagePowerW >= 150.0);
            Assert.True(telemetry.AverageNominalMhz >= 4500.0);
        }

        [Fact]
        public async Task MockGovernor_ApplyCurveOptimizer_UpdatesPerCoreOffsets()
        {
            IHardwareGovernor governor = new MockHardwareGovernor();
            var offsets = new Dictionary<int, int>
            {
                [0] = -5,
                [1] = -5,
                [2] = -25,
                [3] = -25
            };

            bool success = await governor.ApplyCurveOptimizerAsync(offsets);
            var telemetry = governor.GetTelemetry();

            Assert.True(success);
            Assert.Equal(-5, telemetry.Cores[0].CurveOffset);
            Assert.Equal(-25, telemetry.Cores[2].CurveOffset);
        }
    }
}
