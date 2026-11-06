using System;

namespace ZenithPower.Core.Models
{
    public class CpuCoreInfo
    {
        public const int MinCurveOffset = -30;
        public const int MaxCurveOffset = 30;

        public int CoreIndex { get; set; }
        public bool IsStarCore { get; set; }
        public int Ranking { get; set; }
        public double NominalMhz { get; set; }
        public double EffectiveMhz { get; set; }
        public int CurveOffset { get; set; }
        public bool IsProblematic { get; set; }

        public CpuCoreInfo(
            int coreIndex,
            bool isStarCore,
            int ranking,
            double nominalMhz,
            double effectiveMhz,
            int curveOffset = 0,
            bool isProblematic = false)
        {
            if (curveOffset < MinCurveOffset || curveOffset > MaxCurveOffset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(curveOffset),
                    curveOffset,
                    $"Curve offset must be between {MinCurveOffset} and {MaxCurveOffset}.");
            }

            CoreIndex = coreIndex;
            IsStarCore = isStarCore;
            Ranking = ranking;
            NominalMhz = nominalMhz;
            EffectiveMhz = effectiveMhz;
            CurveOffset = curveOffset;
            IsProblematic = isProblematic;
        }

        public double ClockStretchingRatio =>
            NominalMhz > 0 ? EffectiveMhz / NominalMhz : 1.0;
    }
}
