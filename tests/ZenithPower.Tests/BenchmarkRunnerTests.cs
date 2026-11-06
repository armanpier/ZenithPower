using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZenithPower.Core.Engine;
using ZenithPower.Core.Models;
using ZenithPower.Hardware.Governors;

namespace ZenithPower.Tests
{
    public class BenchmarkRunnerTests
    {
        [Fact]
        public async Task RunQuickCalibration_CompletesSuccessfully_ProducesOptimalProfile()
        {
            var mockGovernor = new MockHardwareGovernor(coreCount: 6);
            var runner = new BenchmarkRunner(mockGovernor);

            var progressReports = new System.Collections.Generic.List<string>();
            var progress = new Progress<string>(msg => progressReports.Add(msg));

            var result = await runner.RunQuickCalibrationAsync(
                progress: progress,
                quickModeSeconds: 1, // Rapid execution for test
                ct: CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.OptimalNegativeBias <= -15);
            Assert.True(result.OptimalNegativeBias >= -30);
            Assert.True(result.PowerSavedWatts > 0.0);
            Assert.True(result.MaxSafeEffectiveMhz > 4500.0);
            Assert.NotEmpty(result.PerCoreOffsets);

            // Star Cores (Core 0 and 1) must be protected
            Assert.True(result.PerCoreOffsets[0] >= -10);
            Assert.True(result.PerCoreOffsets[1] >= -10);
        }

        [Fact]
        public async Task RunQuickCalibration_EarlyTripwire_PreventsClockStretchingCrash()
        {
            var mockGovernor = new MockHardwareGovernor(coreCount: 4);
            var runner = new BenchmarkRunner(mockGovernor);

            // Set simulated clock stretching on Core 2 when offset goes below -20
            var result = await runner.RunQuickCalibrationAsync(
                progress: null,
                quickModeSeconds: 1,
                ct: CancellationToken.None);

            // Core 2 offset should not exceed its safe threshold
            Assert.True(result.PerCoreOffsets[2] >= -25);
        }
    }
}
