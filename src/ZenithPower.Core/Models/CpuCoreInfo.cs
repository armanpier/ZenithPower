using System;

namespace ZenithPower.Core.Models
{
    public class CpuCoreInfo
    {
        public const int MinCurveOffset = -30;
        public const int MaxCurveOffset = 30;

        public int CoreIndex { get; set; }
        public bool IsStarCore { get; set; }
        public string StarBadgeText { get; set; } = string.Empty;
        public int Ranking { get; set; }
        public int MaxPerfRating { get; set; } = 100;
        public string LogicalThreadsLabel { get; set; } = string.Empty;
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
            bool isProblematic = false,
            string starBadgeText = "",
            int maxPerfRating = 100,
            string logicalThreadsLabel = "")
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
            StarBadgeText = !string.IsNullOrEmpty(starBadgeText)
                ? starBadgeText
                : (isStarCore ? $"★ STAR #{ranking}" : string.Empty);
            MaxPerfRating = maxPerfRating;
            LogicalThreadsLabel = !string.IsNullOrEmpty(logicalThreadsLabel)
                ? logicalThreadsLabel
                : $"Core {coreIndex}";
        }

        public double ClockStretchingRatio =>
            NominalMhz > 0 ? EffectiveMhz / NominalMhz : 1.0;
    }
}
