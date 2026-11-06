using System.Collections.Generic;
using Xunit;
using ZenithPower.Core.Engine;

namespace ZenithPower.Tests
{
    public class SafetySupervisorTests
    {
        [Theory]
        [InlineData(-30, -30)]
        [InlineData(-15, -15)]
        [InlineData(0, 0)]
        [InlineData(15, 15)]
        [InlineData(30, 30)]
        [InlineData(-45, -30)] // Clamped to -30
        [InlineData(45, 30)]   // Clamped to +30
        public void ValidateOffset_StrictlyClampsToSafeBounds(int requested, int expected)
        {
            int actual = SafetySupervisor.ValidateOffset(requested);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(70.0, 65.0, false)] // Normal temps
        [InlineData(94.0, 99.0, false)] // Just below limits
        [InlineData(95.1, 70.0, true)]  // CPU thermal trip (> 95C)
        [InlineData(70.0, 100.1, true)] // GPU hotspot thermal trip (> 100C)
        [InlineData(96.0, 105.0, true)] // Both exceeded
        public void CheckThermalLimit_ReturnsTrueWhenTripped(
            double cpuTemp, double gpuTemp, bool expectedTrip)
        {
            bool isTripped = SafetySupervisor.CheckThermalLimit(cpuTemp, gpuTemp);
            Assert.Equal(expectedTrip, isTripped);
        }

        [Fact]
        public void HandleWheaCorrectedError_IncrementsOffsetByFive_ToStabilizeCore()
        {
            var offsets = new Dictionary<int, int>
            {
                [0] = -25,
                [1] = -25
            };

            // Core 0 threw a WHEA Event 19 corrected error
            var updated = SafetySupervisor.HandleWheaCorrectedError(0, offsets);

            // Core 0 must step up by +5 (from -25 to -20)
            Assert.Equal(-20, updated[0]);
            // Core 1 untouched
            Assert.Equal(-25, updated[1]);
        }

        [Theory]
        [InlineData(5000, 4950, false)] // 0.99 - Healthy
        [InlineData(5000, 4850, false)] // 0.97 - Healthy threshold
        [InlineData(5000, 4800, true)]  // 0.96 - Tripped! Clock stretching early warning
        [InlineData(5000, 4200, true)]  // 0.84 - Tripped!
        public void ShouldTripEarlyWarning_DetectsClockStretchingBeforeCrash(
            double nominal, double effective, bool expectedTrip)
        {
            bool tripped = SafetySupervisor.ShouldTripEarlyWarning(nominal, effective);
            Assert.Equal(expectedTrip, tripped);
        }
    }
}
