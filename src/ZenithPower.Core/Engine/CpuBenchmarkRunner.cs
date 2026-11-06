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

            progress?.Report($"[1/3] Warming up {threads} CPU threads on {initialSnapshot.CpuName}...");
            await Task.Delay(500, ct);

            // Phase 1: Multi-Threaded Stress & Benchmark
            progress?.Report($"[2/3] Running All-Core Multi-Threaded Compute Benchmark ({threads} threads)...");
            var swMulti = Stopwatch.StartNew();
            long totalMultiOps = 0;

            await Task.Run(() =>
            {
                Parallel.For(0, threads, new ParallelOptions { CancellationToken = ct }, i =>
                {
                    long ops = ComputeStressKernel(iterations: 150_000);
                    Interlocked.Add(ref totalMultiOps, ops);
                });
            }, ct);

            swMulti.Stop();
            double multiSeconds = Math.Max(0.1, swMulti.Elapsed.TotalSeconds);
            long multiScore = (long)((totalMultiOps / multiSeconds) / 100);

            progress?.Report($"Multi-Core Score: {multiScore:N0} points ({multiSeconds:F2}s).");
            await Task.Delay(300, ct);

            // Phase 2: Single-Threaded Burst Benchmark
            progress?.Report("[3/3] Running Single-Threaded Peak Frequency Benchmark...");
            var swSingle = Stopwatch.StartNew();
            long singleOps = 0;

            await Task.Run(() =>
            {
                singleOps = ComputeStressKernel(iterations: 80_000);
            }, ct);

            swSingle.Stop();
            double singleSeconds = Math.Max(0.1, swSingle.Elapsed.TotalSeconds);
            long singleScore = (long)((singleOps / singleSeconds) / 100);

            // Query live telemetry after run
            var finalTelemetry = _governor.GetTelemetry();
            double totalOps = totalMultiOps + singleOps;
            double totalSeconds = multiSeconds + singleSeconds;

            string rating = multiScore switch
            {
                > 50000 => "Elite Workstation / High-End Desktop Tier",
                > 30000 => "High Performance Enthusiast Tier",
                > 15000 => "Mainstream Performance Tier",
                _ => "Power Saver / Standard Tier"
            };

            return new CpuBenchmarkReport
            {
                CpuName = initialSnapshot.CpuName,
                ThreadCount = threads,
                ProfileName = _governor.ActiveProfile.Type.ToString(),
                MultiCoreScore = multiScore,
                SingleCoreScore = singleScore,
                OperationsPerSecond = totalOps / totalSeconds,
                AverageMhzDuringRun = finalTelemetry.AverageEffectiveMhz,
                ExecutionSeconds = totalSeconds,
                Rating = rating
            };
        }

        private static long ComputeStressKernel(int iterations)
        {
            long ops = 0;
            // Prime Sieve + floating-point transcendentals
            for (int i = 2; i < iterations; i++)
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
