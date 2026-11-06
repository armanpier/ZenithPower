using System;
using System.Collections.Generic;

namespace ZenithPower.Core.Engine
{
    public static class SafetySupervisor
    {
        public const int SafeMinNegative = -30;
        public const int SafeMaxPositive = 30;
        public const double CpuThermalTripCelsius = 95.0;
        public const double GpuHotspotThermalTripCelsius = 100.0;
        public const double ClockStretchingTripThreshold = 0.97;
        public const int WheaStabilizationStep = 5;

        public static int ValidateOffset(int requestedOffset)
        {
            return Math.Clamp(requestedOffset, SafeMinNegative, SafeMaxPositive);
        }

        public static bool CheckThermalLimit(double cpuTempCelsius, double gpuHotspotTempCelsius)
        {
            return cpuTempCelsius > CpuThermalTripCelsius ||
                   gpuHotspotTempCelsius > GpuHotspotThermalTripCelsius;
        }

        public static bool ShouldTripEarlyWarning(double nominalMhz, double effectiveMhz)
        {
            if (nominalMhz <= 0)
            {
                return false;
            }

            double ratio = effectiveMhz / nominalMhz;
            return ratio < ClockStretchingTripThreshold;
        }

        public static Dictionary<int, int> HandleWheaCorrectedError(
            int coreIndex,
            Dictionary<int, int> currentOffsets)
        {
            var updated = new Dictionary<int, int>(currentOffsets);

            if (updated.TryGetValue(coreIndex, out int current))
            {
                // Increment offset by +5 counts to add voltage/relieve undervolt
                updated[coreIndex] = Math.Clamp(current + WheaStabilizationStep, SafeMinNegative, SafeMaxPositive);
            }

            return updated;
        }
    }
}
