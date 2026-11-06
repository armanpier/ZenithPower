using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZenithPower.Core.Engine;
using ZenithPower.Hardware.Governors;

namespace ZenithPower.Tests
{
    public class CpuBenchmarkRunnerTests
    {
        [Fact]
        public async Task CpuBenchmarkRunner_ExecutesStressTest_ReturnsScoresAndRating()
        {
            var governor = new MockHardwareGovernor(coreCount: 8);
            var runner = new CpuBenchmarkRunner(governor);

            var report = await runner.RunFullCpuBenchmarkAsync(progress: null, ct: CancellationToken.None);

            Assert.NotNull(report);
            Assert.True(report.MultiCoreScore > 0);
            Assert.True(report.SingleCoreScore > 0);
            Assert.True(report.OperationsPerSecond > 0);
            Assert.NotEmpty(report.Rating);
            Assert.NotEmpty(report.CpuName);
        }
    }
}
