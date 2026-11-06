using System;
using System.Collections.Generic;
using ZenithPower.Core.Models;

namespace ZenithPower.Core.Engine
{
    public static class CurveOptimizerBalancer
    {
        public const int AbsoluteMinNegative = -30;
        public const int AbsoluteMaxPositive = 30;
        public const int StarCoreMaxNegativeDefault = -7;
        public const int StarCoreMaxNegativeGaming = -5;

        public static Dictionary<int, int> CalculateOptimalOffsets(
            IEnumerable<CpuCoreInfo> cores,
            int targetNegativeBias,
            WorkloadPreset preset = WorkloadPreset.Standard)
        {
            // Clamp target negative bias to safety limit [-30, 0]
            int clampedTarget = Math.Clamp(targetNegativeBias, AbsoluteMinNegative, 0);
            var result = new Dictionary<int, int>();

            foreach (var core in cores)
            {
                int assignedOffset;

                if (core.IsProblematic)
                {
                    // Problematic or weak cores must NEVER receive negative bias.
                    // Assign non-negative or slight positive compensation.
                    assignedOffset = Math.Max(0, core.CurveOffset >= 0 ? core.CurveOffset : 2);
                }
                else if (core.IsStarCore || core.Ranking <= 2)
                {
                    // Star Cores boost to peak frequencies; protect their V/F curve headroom
                    assignedOffset = preset switch
                    {
                        WorkloadPreset.SingleThreaded => 0, // 0 offset for maximum transient burst stability
                        WorkloadPreset.Gaming => Math.Max(StarCoreMaxNegativeGaming, clampedTarget / 3),
                        WorkloadPreset.Turbo => Math.Max(StarCoreMaxNegativeDefault, clampedTarget / 2),
                        _ => Math.Max(StarCoreMaxNegativeDefault, clampedTarget / 2)
                    };
                }
                else
                {
                    // Normal / secondary cores receive full requested negative bias
                    assignedOffset = preset switch
                    {
                        WorkloadPreset.SingleThreaded => clampedTarget, // Deep negative offset to suppress non-active core power
                        WorkloadPreset.Gaming => clampedTarget,
                        _ => clampedTarget
                    };
                }

                // Final clamp to safety floor
                result[core.CoreIndex] = Math.Clamp(assignedOffset, AbsoluteMinNegative, AbsoluteMaxPositive);
            }

            return result;
        }
    }
}
