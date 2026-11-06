using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZenithPower.Core.Abstractions;
using ZenithPower.Core.Engine;
using ZenithPower.Core.Models;

namespace ZenithPower.Watchdog
{
    public class WatchdogService : IDisposable
    {
        private readonly IHardwareGovernor _governor;
        private readonly string _stateDirectory;
        private CancellationTokenSource? _cts;
        private Task? _supervisionTask;

        public event EventHandler<string>? EmergencyThermalActionTriggered;
        public event EventHandler<RecoveryResult>? BootRecoveryTriggered;

        public WatchdogService(IHardwareGovernor governor, string? stateDirectory = null)
        {
            _governor = governor;
            _stateDirectory = stateDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZenithPower", "State");
        }

        public void Start()
        {
            // 1. Check for dirty reboot recovery on startup
            var recovery = RecoveryManager.CheckDirtyRebootAndRecover(_stateDirectory);
            if (recovery.Recovered)
            {
                _governor.ApplyPowerProfileAsync(recovery.SafeProfile).GetAwaiter().GetResult();
                BootRecoveryTriggered?.Invoke(this, recovery);
            }

            // 2. Start continuous thermal & safety loop
            _cts = new CancellationTokenSource();
            _supervisionTask = Task.Run(() => SupervisionLoopAsync(_cts.Token));
        }

        private async Task SupervisionLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var telemetry = _governor.GetTelemetry();

                    if (SafetySupervisor.CheckThermalLimit(telemetry.CpuPackageTempC, telemetry.GpuHotspotTempC))
                    {
                        EmergencyThermalActionTriggered?.Invoke(this,
                            $"[THERMAL EMERGENCY] CPU: {telemetry.CpuPackageTempC:F1}°C, GPU Hotspot: {telemetry.GpuHotspotTempC:F1}°C. Stepping down power limit by 25%.");

                        // Emergency thermal step-down
                        var reducedProfile = _governor.ActiveProfile;
                        reducedProfile.CpuPowerLimitW *= 0.75;
                        reducedProfile.GpuPowerLimitPercent = Math.Max(50.0, reducedProfile.GpuPowerLimitPercent * 0.75);
                        await _governor.ApplyPowerProfileAsync(reducedProfile);
                    }

                    await Task.Delay(1000, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch
                {
                    // Guard loop against unhandled driver exceptions
                }
            }
        }

        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
