using System;

namespace ZenithPower.Core.Models
{
    public enum ProfileType
    {
        SuperSaver,
        Saver,
        Normal,
        Performance,
        UltimatePerformance
    }

    public enum WorkloadPreset
    {
        Standard,
        Gaming,
        Rendering,
        SingleThreaded,
        Turbo
    }

    public class PowerProfile
    {
        public ProfileType Type { get; set; }
        public WorkloadPreset Preset { get; set; } = WorkloadPreset.Standard;
        public int Epp { get; set; }
        public int IncreaseThresholdPercent { get; set; }
        public int DecreaseThresholdPercent { get; set; }
        public int BoostMode { get; set; } // 0=Disabled, 1=Enabled, 2=Aggressive, 3=Efficient Enabled, 4=Efficient Aggressive
        public int ThrottleMaxPercent { get; set; } = 100; // PROCTHROTTLEMAX (e.g. 65%, 85%, 100%)
        public int ThrottleMinPercent { get; set; } = 5;   // PROCTHROTTLEMIN (e.g. 5%, 10%, 100%)
        public double CpuPowerLimitW { get; set; }
        public double GpuPowerLimitPercent { get; set; } = 100.0;
        public int AllCoreCurveOffset { get; set; }

        public static PowerProfile CreateDefault(ProfileType type)
        {
            return type switch
            {
                ProfileType.SuperSaver => new PowerProfile
                {
                    Type = ProfileType.SuperSaver,
                    Epp = 85,
                    IncreaseThresholdPercent = 85,
                    DecreaseThresholdPercent = 60,
                    BoostMode = 0, // Disabled: cold idle, zero boost heat spikes
                    ThrottleMaxPercent = 65, // Caps frequency down to 65% of max clock
                    ThrottleMinPercent = 5,
                    CpuPowerLimitW = 35.0,
                    GpuPowerLimitPercent = 50.0,
                    AllCoreCurveOffset = -20
                },
                ProfileType.Saver => new PowerProfile
                {
                    Type = ProfileType.Saver,
                    Epp = 65,
                    IncreaseThresholdPercent = 70,
                    DecreaseThresholdPercent = 45,
                    BoostMode = 3, // Efficient Enabled
                    ThrottleMaxPercent = 85, // Caps frequency down to 85% of max clock
                    ThrottleMinPercent = 5,
                    CpuPowerLimitW = 65.0,
                    GpuPowerLimitPercent = 70.0,
                    AllCoreCurveOffset = -15
                },
                ProfileType.Normal => new PowerProfile
                {
                    Type = ProfileType.Normal,
                    Epp = 50,
                    IncreaseThresholdPercent = 50,
                    DecreaseThresholdPercent = 30,
                    BoostMode = 1, // Enabled
                    ThrottleMaxPercent = 100,
                    ThrottleMinPercent = 10,
                    CpuPowerLimitW = 105.0,
                    GpuPowerLimitPercent = 100.0,
                    AllCoreCurveOffset = -10
                },
                ProfileType.Performance => new PowerProfile
                {
                    Type = ProfileType.Performance,
                    Epp = 20,
                    IncreaseThresholdPercent = 25,
                    DecreaseThresholdPercent = 15,
                    BoostMode = 2, // Aggressive
                    ThrottleMaxPercent = 100,
                    ThrottleMinPercent = 20,
                    CpuPowerLimitW = 145.0,
                    GpuPowerLimitPercent = 100.0,
                    AllCoreCurveOffset = -15
                },
                ProfileType.UltimatePerformance => new PowerProfile
                {
                    Type = ProfileType.UltimatePerformance,
                    Epp = 0,
                    IncreaseThresholdPercent = 10,
                    DecreaseThresholdPercent = 5,
                    BoostMode = 2, // Aggressive
                    ThrottleMaxPercent = 100,
                    ThrottleMinPercent = 100, // Maximum P-State locked, zero throttle delay
                    CpuPowerLimitW = 250.0,
                    GpuPowerLimitPercent = 100.0,
                    AllCoreCurveOffset = -10
                },
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
