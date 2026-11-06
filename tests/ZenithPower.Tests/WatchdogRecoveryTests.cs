using System;
using System.IO;
using Xunit;
using ZenithPower.Core.Models;
using ZenithPower.Watchdog;

namespace ZenithPower.Tests
{
    public class WatchdogRecoveryTests : IDisposable
    {
        private readonly string _testDir;

        public WatchdogRecoveryTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ZenithPower_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, recursive: true);
                }
            }
            catch
            {
            }
        }

        [Fact]
        public void CheckDirtyReboot_NoLockFile_ReturnsClean()
        {
            var result = RecoveryManager.CheckDirtyRebootAndRecover(_testDir);

            Assert.False(result.Recovered);
            Assert.True(result.IsClean);
        }

        [Fact]
        public void CheckDirtyReboot_LockFilePresent_RecoversToStockAndCleansLock()
        {
            var dangerousProfile = PowerProfile.CreateDefault(ProfileType.UltimatePerformance);
            dangerousProfile.AllCoreCurveOffset = -30;

            // Simulate application crash/reboot while lock was active
            RecoveryManager.CreatePendingLock(_testDir, dangerousProfile);

            string lockPath = Path.Combine(_testDir, "active_tuning.lock");
            Assert.True(File.Exists(lockPath));

            // Startup watchdog runs
            var result = RecoveryManager.CheckDirtyRebootAndRecover(_testDir);

            Assert.True(result.Recovered);
            Assert.False(File.Exists(lockPath), "Lock file must be deleted upon recovery.");
            Assert.Equal(ProfileType.Normal, result.SafeProfile.Type);
            Assert.Equal(0, result.SafeProfile.AllCoreCurveOffset);

            // Verify recovery report was written
            string reportPath = Path.Combine(_testDir, "recovery_report.json");
            Assert.True(File.Exists(reportPath));
        }

        [Fact]
        public void ReleasePendingLock_RemovesLockFile()
        {
            var profile = PowerProfile.CreateDefault(ProfileType.Performance);
            RecoveryManager.CreatePendingLock(_testDir, profile);

            string lockPath = Path.Combine(_testDir, "active_tuning.lock");
            Assert.True(File.Exists(lockPath));

            RecoveryManager.ReleasePendingLock(_testDir);
            Assert.False(File.Exists(lockPath));
        }
    }
}
