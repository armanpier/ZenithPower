using System;

namespace ZenithPower.Core.Diagnostics
{
    public enum ClockStretchingSeverity
    {
        None,
        Mild,
        Severe
    }

    public record ClockStretchingResult(
        double Ratio,
        ClockStretchingSeverity Severity,
        double NominalMhz,
        double EffectiveMhz,
        double DeficitMhz,
        string Recommendation);

    public static class ClockStretchingDetector
    {
        public const double HealthyThreshold = 0.98;
        public const double MildThreshold = 0.90;

        public static ClockStretchingResult Evaluate(double nominalMhz, double effectiveMhz)
        {
            if (nominalMhz <= 0)
            {
                return new ClockStretchingResult(
                    Ratio: 1.0,
                    Severity: ClockStretchingSeverity.None,
                    NominalMhz: nominalMhz,
                    EffectiveMhz: effectiveMhz,
                    DeficitMhz: 0.0,
                    Recommendation: "Invalid nominal clock reported.");
            }

            double ratio = effectiveMhz / nominalMhz;
            double deficit = Math.Max(0.0, nominalMhz - effectiveMhz);

            ClockStretchingSeverity severity;
            string recommendation;

            if (ratio >= HealthyThreshold)
            {
                severity = ClockStretchingSeverity.None;
                recommendation = "Full execution throughput. Voltage supply is healthy.";
            }
            else if (ratio >= MildThreshold)
            {
                severity = ClockStretchingSeverity.Mild;
                recommendation = "Mild clock stretching detected. Core is skipping cycles due to marginal voltage. Reduce negative offset by 3-5 counts.";
            }
            else
            {
                severity = ClockStretchingSeverity.Severe;
                recommendation = "Severe clock stretching! CPU internal voltage regulator is starving performance. Revert negative offset immediately.";
            }

            return new ClockStretchingResult(ratio, severity, nominalMhz, effectiveMhz, deficit, recommendation);
        }

        public static double CalculateEffectiveFromRegisters(
            double bclkMhz,
            ulong deltaAperf,
            ulong deltaMperf,
            double baseMultiplier)
        {
            if (deltaMperf == 0)
            {
                return 0.0;
            }

            return bclkMhz * ((double)deltaAperf / deltaMperf) * baseMultiplier;
        }
    }
}
