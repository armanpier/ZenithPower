using Xunit;
using ZenithPower.Core.Database;

namespace ZenithPower.Tests
{
    public class HardwareDatabaseTests
    {
        [Theory]
        [InlineData("AMD Ryzen 7 7800X3D", 120.0, 45.0, -25)]
        [InlineData("AMD Ryzen 9 7950X", 170.0, 65.0, -20)]
        [InlineData("Intel Core i9-14900K", 253.0, 65.0, -15)]
        [InlineData("Intel Core i7-13700K", 253.0, 65.0, -15)]
        public void LookupCpuProfile_KnownCpus_ReturnsValidHardwareSpecs(
            string cpuQuery,
            double expectedStockTdp,
            double expectedEcoTdp,
            int expectedRecommendedNegative)
        {
            var db = new HardwareDatabase();
            var preset = db.LookupCpuProfile(cpuQuery);

            Assert.NotNull(preset);
            Assert.Equal(expectedStockTdp, preset.StockTdpW);
            Assert.Equal(expectedEcoTdp, preset.EcoTdpW);
            Assert.True(preset.RecommendedMaxNegativeOffset <= expectedRecommendedNegative);
        }

        [Theory]
        [InlineData("NVIDIA GeForce RTX 4090", 450.0, 280.0, 950)]
        [InlineData("NVIDIA GeForce RTX 4080", 320.0, 200.0, 925)]
        [InlineData("AMD Radeon RX 7900 XTX", 355.0, 240.0, 1050)]
        public void LookupGpuProfile_KnownGpus_ReturnsValidSpecs(
            string gpuQuery,
            double expectedStockTdp,
            double expectedEcoTdp,
            int expectedVfAnchorMv)
        {
            var db = new HardwareDatabase();
            var preset = db.LookupGpuProfile(gpuQuery);

            Assert.NotNull(preset);
            Assert.Equal(expectedStockTdp, preset.StockTdpW);
            Assert.Equal(expectedEcoTdp, preset.EcoTdpW);
            Assert.Equal(expectedVfAnchorMv, preset.VfCurveAnchorMilliVolts);
        }

        [Fact]
        public void LookupMemoryProfile_ReturnsSafeVoltages()
        {
            var db = new HardwareDatabase();
            var ramPreset = db.LookupMemoryProfile("DDR5");

            Assert.NotNull(ramPreset);
            Assert.Equal(1.10, ramPreset.JedecVoltageV);
            Assert.Equal(1.25, ramPreset.MaxRecommendedSocVoltageV);
        }
    }
}
