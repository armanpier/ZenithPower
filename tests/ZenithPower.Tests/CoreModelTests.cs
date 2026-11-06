using System;
using Xunit;
using ZenithPower.Core.Models;

namespace ZenithPower.Tests
{
    public class CoreModelTests
    {
        [Fact]
        public void SuperSaverProfile_HasExpectedEnergyDefaults()
        {
            var profile = PowerProfile.CreateDefault(ProfileType.SuperSaver);

            Assert.Equal(ProfileType.SuperSaver, profile.Type);
            Assert.Equal(85, profile.Epp);
            Assert.Equal(85, profile.IncreaseThresholdPercent);
            Assert.Equal(3, profile.BoostMode); // Efficient Enabled
            Assert.Equal(35.0, profile.CpuPowerLimitW);
            Assert.Equal(50.0, profile.GpuPowerLimitPercent);
        }

        [Fact]
        public void UltimatePerformanceProfile_HasExpectedPerformanceDefaults()
        {
            var profile = PowerProfile.CreateDefault(ProfileType.UltimatePerformance);

            Assert.Equal(ProfileType.UltimatePerformance, profile.Type);
            Assert.Equal(0, profile.Epp);
            Assert.Equal(10, profile.IncreaseThresholdPercent);
            Assert.Equal(2, profile.BoostMode); // Aggressive
            Assert.Equal(250.0, profile.CpuPowerLimitW);
            Assert.Equal(115.0, profile.GpuPowerLimitPercent);
        }

        [Theory]
        [InlineData(-30)]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(30)]
        public void CpuCoreInfo_ValidCurveOffset_Succeeds(int offset)
        {
            var core = new CpuCoreInfo(
                coreIndex: 0,
                isStarCore: true,
                ranking: 1,
                nominalMhz: 4500.0,
                effectiveMhz: 4490.0,
                curveOffset: offset);

            Assert.Equal(offset, core.CurveOffset);
        }

        [Theory]
        [InlineData(-31)]
        [InlineData(31)]
        [InlineData(-100)]
        public void CpuCoreInfo_InvalidCurveOffset_ThrowsArgumentOutOfRangeException(int offset)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CpuCoreInfo(
                    coreIndex: 0,
                    isStarCore: false,
                    ranking: 3,
                    nominalMhz: 4500.0,
                    effectiveMhz: 4490.0,
                    curveOffset: offset));
        }
    }
}
