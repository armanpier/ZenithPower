using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZenithPower.Core.Abstractions;
using ZenithPower.Core.Models;

namespace ZenithPower.Core.Engine
{
    public class CpuBenchmarkReport
    {
        public string CpuName { get; set; } = string.Empty;
        public int ThreadCount { get; set; }
        public string ProfileName { get; set; } = string.Empty;
        public long MultiCoreScore { get; set; }
        public long SingleCoreScore { get; set; }
        public double OperationsPerSecond { get; set; }
        public double AverageMhzDuringRun { get; set; }
        public double ExecutionSeconds { get; set; }
        public string Rating { get; set; } = string.Empty;
    }

    public class CpuBenchmarkRunner
    {
        private readonly IHardwareGovernor _governor;

        public CpuBenchmarkRunner(IHardwareGovernor governor)
        {
            _governor = governor;
        }

        public async Task<CpuBenchmarkReport> RunFullCpuBenchmarkAsync(
            IProgress<string>? progress,
            CancellationToken ct = default)
        {
            var initialSnapshot = _governor.GetTelemetry();
            int threads = Environment.ProcessorCount;

            progress?.Report($"[1/3] Preparing {threads} threads for {_governor.ActiveProfile.Type} ({_governor.ActiveProfile.Preset})...");
            await Task.Delay(400, ct);

            // Phase 1: Multi-Threaded Sustained Compute Stress (2.0s duration)
            progress?.Report($"[2/3] Running All-Core Multi-Threaded Stress ({threads} threads for 2.0s)...");
            const int multiDurationMs = 2000;
            long totalMultiOps = 0;
            var swMulti = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                Parallel.For(0, threads, new ParallelOptions { CancellationToken = ct }, _ =>
                {
                    long localOps = 0;
                    var localSw = Stopwatch.StartNew();
                    while (localSw.ElapsedMilliseconds < multiDurationMs && !ct.IsCancellationRequested)
                    {
                        localOps += ComputeBatch(batchSize: 10_000);
                    }
                    Interlocked.Add(ref totalMultiOps, localOps);
                });
            }, ct);

            swMulti.Stop();
            double multiSeconds = Math.Max(0.01, swMulti.Elapsed.TotalSeconds);
            // Multi-Core Score is scaled to standard intuitive benchmark points
            long multiScore = (long)((totalMultiOps / multiSeconds) / 1000);

            progress?.Report($"Multi-Core Score: {multiScore:N0} pts ({multiSeconds:F2}s, {totalMultiOps:N0} total operations).");
            await Task.Delay(300, ct);

            // Phase 2: Single-Threaded Burst Benchmark (1.5s duration on 1 thread)
            progress?.Report("[3/3] Running Single-Threaded Peak Frequency Benchmark (1.5s)...");
            const int singleDurationMs = 1500;
            long totalSingleOps = 0;
            var swSingle = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                var localSw = Stopwatch.StartNew();
                while (localSw.ElapsedMilliseconds < singleDurationMs && !ct.IsCancellationRequested)
                {
                    totalSingleOps += ComputeBatch(batchSize: 10_000);
                }
            }, ct);

            swSingle.Stop();
            double singleSeconds = Math.Max(0.01, swSingle.Elapsed.TotalSeconds);
            long singleScore = (long)((totalSingleOps / singleSeconds) / 1000);

            // Query live telemetry after run
            var finalTelemetry = _governor.GetTelemetry();
            double totalOps = totalMultiOps + totalSingleOps;
            double totalSeconds = multiSeconds + singleSeconds;

            string rating = multiScore switch
            {
                > 55000 => "Elite Workstation / High-End Desktop Tier",
                > 35000 => "High Performance Enthusiast Tier",
                > 20000 => "Mainstream Performance Tier",
                _ => "Power Saver / Constrained Frequency Tier"
            };

            return new CpuBenchmarkReport
            {
                CpuName = initialSnapshot.CpuName,
                ThreadCount = threads,
                ProfileName = $"{_governor.ActiveProfile.Type} ({_governor.ActiveProfile.Preset})",
                MultiCoreScore = multiScore,
                SingleCoreScore = singleScore,
                OperationsPerSecond = totalOps / totalSeconds,
                AverageMhzDuringRun = finalTelemetry.AverageEffectiveMhz,
                ExecutionSeconds = totalSeconds,
                Rating = rating
            };
        }

        private static long ComputeBatch(int batchSize)
        {
            long ops = 0;
            for (int i = 2; i < batchSize; i++)
            {
                bool isPrime = true;
                int limit = (int)Math.Sqrt(i);
                for (int j = 2; j <= limit; j++)
                {
                    if (i % j == 0)
                    {
                        isPrime = false;
                        break;
                    }
                }
                if (isPrime) ops++;
            }
            return ops;
        }
    }
}
