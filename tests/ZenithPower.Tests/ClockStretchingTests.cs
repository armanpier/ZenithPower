using Xunit;
using ZenithPower.Core.Diagnostics;

namespace ZenithPower.Tests
{
    public class ClockStretchingTests
    {
        [Theory]
        [InlineData(5000.0, 5000.0, 1.0, ClockStretchingSeverity.None)]
        [InlineData(5000.0, 4950.0, 0.99, ClockStretchingSeverity.None)]
        [InlineData(5000.0, 4900.0, 0.98, ClockStretchingSeverity.None)]
        [InlineData(5000.0, 4750.0, 0.95, ClockStretchingSeverity.Mild)]
        [InlineData(5000.0, 4550.0, 0.91, ClockStretchingSeverity.Mild)]
        [InlineData(5000.0, 4400.0, 0.88, ClockStretchingSeverity.Severe)]
        [InlineData(5000.0, 3500.0, 0.70, ClockStretchingSeverity.Severe)]
        public void Evaluate_ReturnsExpectedSeverityAndRatio(
            double nominal,
            double effective,
            double expectedRatio,
            ClockStretchingSeverity expectedSeverity)
        {
            var result = ClockStretchingDetector.Evaluate(nominal, effective);

            Assert.Equal(expectedRatio, result.Ratio, precision: 2);
            Assert.Equal(expectedSeverity, result.Severity);
            Assert.Equal(nominal, result.NominalMhz);
            Assert.Equal(effective, result.EffectiveMhz);
            Assert.Equal(nominal - effective, result.DeficitMhz, precision: 1);
        }

        [Fact]
        public void CalculateEffectiveFromRegisters_ComputesCorrectMhz()
        {
            // BCLK = 100.0 MHz, Delta APERF = 4000, Delta MPERF = 4000, Base Multiplier = 36
            // Effective = 100 * (4000 / 4000) * 36 = 3600 MHz
            double bclk = 100.0;
            ulong deltaAperf = 4000;
            ulong deltaMperf = 4000;
            double baseMultiplier = 36.0;

            double effectiveMhz = ClockStretchingDetector.CalculateEffectiveFromRegisters(
                bclk, deltaAperf, deltaMperf, baseMultiplier);

            Assert.Equal(3600.0, effectiveMhz, precision: 1);
        }

        [Fact]
        public void CalculateEffectiveFromRegisters_ZeroDeltaMperf_ReturnsZero()
        {
            double effective = ClockStretchingDetector.CalculateEffectiveFromRegisters(
                100.0, 100, 0, 36.0);

            Assert.Equal(0.0, effective);
        }
    }
}
