using System;
using System.IO;
using System.Text.Json;
using ZenithPower.Core.Models;

namespace ZenithPower.Watchdog
{
    public record RecoveryResult(
        bool Recovered,
        bool IsClean,
        string Message,
        PowerProfile SafeProfile);

    public static class RecoveryManager
    {
        public const string LockFileName = "active_tuning.lock";
        public const string RecoveryReportFileName = "recovery_report.json";

        public static void CreatePendingLock(string stateDirectory, PowerProfile profile)
        {
            Directory.CreateDirectory(stateDirectory);
            string lockPath = Path.Combine(stateDirectory, LockFileName);
            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(lockPath, json);
        }

        public static void ReleasePendingLock(string stateDirectory)
        {
            string lockPath = Path.Combine(stateDirectory, LockFileName);
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
            }
        }

        public static RecoveryResult CheckDirtyRebootAndRecover(string stateDirectory)
        {
            string lockPath = Path.Combine(stateDirectory, LockFileName);

            if (!File.Exists(lockPath))
            {
                return new RecoveryResult(
                    Recovered: false,
                    IsClean: true,
                    Message: "Clean system state. No pending tuning locks found.",
                    SafeProfile: PowerProfile.CreateDefault(ProfileType.Normal));
            }

            // An unexpected reboot, hang, or BSOD occurred while active_tuning.lock was present!
            string pendingJson = File.ReadAllText(lockPath);
            File.Delete(lockPath);

            // Create safe stock default profile
            var safeProfile = PowerProfile.CreateDefault(ProfileType.Normal);
            safeProfile.AllCoreCurveOffset = 0;

            // Log recovery incident
            var report = new
            {
                IncidentTimestamp = DateTime.UtcNow,
                Reason = "System restarted abnormally while tuning was pending probation.",
                ActionTaken = "Restored stock BIOS and Windows PPM defaults with 0 Curve Optimizer offset.",
                AbortedProfileData = pendingJson
            };

            string reportPath = Path.Combine(stateDirectory, RecoveryReportFileName);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

            return new RecoveryResult(
                Recovered: true,
                IsClean: false,
                Message: "Abnormal shutdown detected during tuning. Hardware voltages and power limits have been restored to stock defaults for system safety.",
                SafeProfile: safeProfile);
        }
    }
}
