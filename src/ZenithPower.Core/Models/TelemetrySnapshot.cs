using System;
using System.Collections.Generic;

namespace ZenithPower.Core.Models
{
    public class TelemetrySnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double CpuPackageTempC { get; set; }
        public double CpuPackagePowerW { get; set; }
        public double AverageNominalMhz { get; set; }
        public double AverageEffectiveMhz { get; set; }
        public double OverallClockStretchingRatio { get; set; }
        public double GpuTempC { get; set; }
        public double GpuHotspotTempC { get; set; }
        public double GpuPowerW { get; set; }
        public double GpuUsagePercent { get; set; }
        public double GpuClockMhz { get; set; }
        public List<CpuCoreInfo> Cores { get; set; } = new();
    }
}
