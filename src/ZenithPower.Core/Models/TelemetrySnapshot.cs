using System;
using System.Collections.Generic;

namespace ZenithPower.Core.Models
{
    public class TelemetrySnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string CpuName { get; set; } = "x64 Processor";
        public int PhysicalCoreCount { get; set; } = Environment.ProcessorCount / 2;
        public int LogicalCoreCount { get; set; } = Environment.ProcessorCount;
        public double CpuUsagePercent { get; set; }
        public double CpuPackageTempC { get; set; }
        public double CpuPackagePowerW { get; set; }
        public double AverageNominalMhz { get; set; }
        public double AverageEffectiveMhz { get; set; }
        public double OverallClockStretchingRatio { get; set; }
        public int ActiveThrottleLimitPercent { get; set; } = 100;
        public List<CpuCoreInfo> Cores { get; set; } = new();
    }
}
