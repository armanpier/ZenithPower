using System.Collections.Generic;
using System.Threading.Tasks;
using ZenithPower.Core.Models;

namespace ZenithPower.Core.Abstractions
{
    public interface IHardwareGovernor
    {
        PowerProfile ActiveProfile { get; }
        bool IsMockMode { get; }
        Task<bool> ApplyPowerProfileAsync(PowerProfile profile);
        Task<bool> ApplyCurveOptimizerAsync(Dictionary<int, int> perCoreOffsets);
        TelemetrySnapshot GetTelemetry();
    }
}
