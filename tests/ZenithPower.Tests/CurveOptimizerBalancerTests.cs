using System.Collections.Generic;
using Xunit;
using ZenithPower.Core.Engine;
using ZenithPower.Core.Models;

namespace ZenithPower.Tests
{
    public class CurveOptimizerBalancerTests
    {
        private List<CpuCoreInfo> CreateSampleCores()
        {
            return new List<CpuCoreInfo>
            {
                new CpuCoreInfo(0, isStarCore: true, ranking: 1, nominalMhz: 5400, effectiveMhz: 5390),
                new CpuCoreInfo(1, isStarCore: true, ranking: 2, nominalMhz: 5400, effectiveMhz: 5385),
                new CpuCoreInfo(2, isStarCore: false, ranking: 3, nominalMhz: 5000, effectiveMhz: 4990),
                new CpuCoreInfo(3, isStarCore: false, ranking: 4, nominalMhz: 5000, effectiveMhz: 4995),
                new CpuCoreInfo(4, isStarCore: false, ranking: 5, nominalMhz: 5000, effectiveMhz: 4990, isProblematic: true),
                new CpuCoreInfo(5, isStarCore: false, ranking: 6, nominalMhz: 5000, effectiveMhz: 4990)
            };
        }

        [Fact]
        public void CalculateOptimalOffsets_ProtectsStarCores_AndFlagsProblematic()
        {
            var cores = CreateSampleCores();
            int requestedTargetNegative = -25;

            var offsets = CurveOptimizerBalancer.CalculateOptimalOffsets(
                cores, requestedTargetNegative, WorkloadPreset.Standard);

            // Core 0 (Star Core Rank 1): Should have conservative offset (between -10 and 0)
            Assert.True(offsets[0] >= -10 && offsets[0] <= 0);

            // Core 1 (Star Core Rank 2): Should have conservative offset (between -10 and 0)
            Assert.True(offsets[1] >= -10 && offsets[1] <= 0);

            // Core 2 & 3 (Normal Cores): Should get full requested target negative bias (-25)
            Assert.Equal(-25, offsets[2]);
            Assert.Equal(-25, offsets[3]);

            // Core 4 (Problematic Core): Should NEVER be negative, must be >= 0
            Assert.True(offsets[4] >= 0);

            // Core 5 (Normal Core): Should get full requested target negative bias (-25)
            Assert.Equal(-25, offsets[5]);
        }

        [Fact]
        public void CalculateOptimalOffsets_SingleThreadedPreset_MaximizesStarCoreVoltage()
        {
            var cores = CreateSampleCores();
            int requestedTargetNegative = -30;

            var offsets = CurveOptimizerBalancer.CalculateOptimalOffsets(
                cores, requestedTargetNegative, WorkloadPreset.SingleThreaded);

            // Star Core 0 must be 0 for maximum transient burst stability
            Assert.Equal(0, offsets[0]);
            // Non-star normal cores must be aggressive -30
            Assert.Equal(-30, offsets[2]);
        }

        [Fact]
        public void CalculateOptimalOffsets_ClampsBeyondSafetyFloor()
        {
            var cores = CreateSampleCores();
            int unsafeRequest = -50; // Beyond safety floor of -30

            var offsets = CurveOptimizerBalancer.CalculateOptimalOffsets(
                cores, unsafeRequest, WorkloadPreset.Standard);

            // All normal cores must be clamped to -30
            Assert.Equal(-30, offsets[2]);
        }
    }
}
