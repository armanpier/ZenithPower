using System.Collections.Generic;

namespace ZenithPower.Core.Models
{
    public class TuningResult
    {
        public int OptimalNegativeBias { get; set; }
        public Dictionary<int, int> PerCoreOffsets { get; set; } = new();
        public double PowerSavedWatts { get; set; }
        public double MaxSafeEffectiveMhz { get; set; }
        public double BaselineEffectiveMhz { get; set; }
        public double OverclockingHeadroomMhz { get; set; }
        public double DownclockingPowerSavingsPercent { get; set; }
        public bool ClockStretchingEncountered { get; set; }
        public string Summary { get; set; } = string.Empty;
    }
}
