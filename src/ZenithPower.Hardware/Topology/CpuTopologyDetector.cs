using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ZenithPower.Core.Models;

namespace ZenithPower.Hardware.Topology
{
    public class PhysicalCoreData
    {
        public int PhysicalIndex { get; set; }
        public int PrimaryThreadId { get; set; }
        public int SecondaryThreadId { get; set; } = -1;
        public string ThreadsLabel { get; set; } = string.Empty;
        public int MaxPerfPercentage { get; set; } = 100;
        public int Ranking { get; set; }
        public bool IsStar { get; set; }
        public string StarBadge { get; set; } = string.Empty;
    }

    public static class CpuTopologyDetector
    {
        private const int RelationProcessorCore = 0;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformationEx(
            int relationshipType,
            IntPtr buffer,
            ref uint returnedLength);

        public static List<PhysicalCoreData> DetectTopology()
        {
            var rawCores = QueryPhysicalCoreAffinity();
            var cppcRatings = QueryCppcEventLogRatings();

            // Match CPPC performance rating for each physical core
            foreach (var core in rawCores)
            {
                if (cppcRatings.TryGetValue(core.PrimaryThreadId, out int rating))
                {
                    core.MaxPerfPercentage = rating;
                }
                else if (core.SecondaryThreadId >= 0 && cppcRatings.TryGetValue(core.SecondaryThreadId, out int rating2))
                {
                    core.MaxPerfPercentage = rating2;
                }
                else
                {
                    // Heuristic fallback if CPPC event log was cleared
                    core.MaxPerfPercentage = 100 + Math.Max(0, 10 - core.PhysicalIndex);
                }
            }

            // Rank physical cores by their MaxPerfPercentage descending
            var ranked = rawCores
                .OrderByDescending(c => c.MaxPerfPercentage)
                .ThenBy(c => c.PhysicalIndex)
                .ToList();

            for (int r = 0; r < ranked.Count; r++)
            {
                var core = ranked[r];
                core.Ranking = r + 1;

                // Designate the top 2 preferred cores as Star Cores (★ #1, ★ #2)
                if (r == 0)
                {
                    core.IsStar = true;
                    core.StarBadge = "★ STAR #1";
                }
                else if (r == 1)
                {
                    core.IsStar = true;
                    core.StarBadge = "★ STAR #2";
                }
                else
                {
                    core.IsStar = false;
                    core.StarBadge = string.Empty;
                }
            }

            // Return in natural physical core order (Core 0, Core 1, ..., Core N-1)
            return rawCores.OrderBy(c => c.PhysicalIndex).ToList();
        }

        private static List<PhysicalCoreData> QueryPhysicalCoreAffinity()
        {
            var cores = new List<PhysicalCoreData>();
            uint len = 0;
            GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref len);

            if (len > 0)
            {
                IntPtr buf = Marshal.AllocHGlobal((int)len);
                try
                {
                    if (GetLogicalProcessorInformationEx(RelationProcessorCore, buf, ref len))
                    {
                        int offset = 0;
                        int coreIdx = 0;
                        while (offset < len)
                        {
                            int size = Marshal.ReadInt32(buf, offset + 4);
                            if (size <= 0) break;

                            // In SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX for RelationProcessorCore:
                            // GroupAffinity starts at offset + 32
                            ulong mask = (ulong)Marshal.ReadInt64(buf, offset + 32);

                            var threads = new List<int>();
                            for (int b = 0; b < 64; b++)
                            {
                                if (((mask >> b) & 1UL) != 0)
                                {
                                    threads.Add(b);
                                }
                            }

                            int t1 = threads.Count > 0 ? threads[0] : coreIdx * 2;
                            int t2 = threads.Count > 1 ? threads[1] : -1;
                            string label = t2 >= 0 ? $"Threads {t1}, {t2}" : $"Thread {t1}";

                            cores.Add(new PhysicalCoreData
                            {
                                PhysicalIndex = coreIdx,
                                PrimaryThreadId = t1,
                                SecondaryThreadId = t2,
                                ThreadsLabel = label
                            });

                            coreIdx++;
                            offset += size;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }

            // Fallback if API returned nothing
            if (cores.Count == 0)
            {
                int logical = Environment.ProcessorCount;
                int physical = Math.Max(1, logical / 2);
                for (int i = 0; i < physical; i++)
                {
                    int t1 = i * 2;
                    int t2 = (i * 2 + 1) < logical ? (i * 2 + 1) : -1;
                    cores.Add(new PhysicalCoreData
                    {
                        PhysicalIndex = i,
                        PrimaryThreadId = t1,
                        SecondaryThreadId = t2,
                        ThreadsLabel = t2 >= 0 ? $"Threads {t1}, {t2}" : $"Thread {t1}"
                    });
                }
            }

            return cores;
        }

        private static Dictionary<int, int> QueryCppcEventLogRatings()
        {
            var map = new Dictionary<int, int>();
            try
            {
                string query = "*[System[Provider[@Name='Microsoft-Windows-Kernel-Processor-Power'] and (EventID=55)]]";
                var elQuery = new EventLogQuery("System", PathType.LogName, query);
                using var reader = new EventLogReader(elQuery);

                EventRecord? rec;
                while ((rec = reader.ReadEvent()) != null)
                {
                    using (rec)
                    {
                        string desc = rec.FormatDescription() ?? string.Empty;
                        var match = Regex.Match(
                            desc,
                            @"Processor\s+(\d+)\s+in group\s+\d+.*?Maximum performance percentage:\s+(\d+)",
                            RegexOptions.Singleline);

                        if (match.Success)
                        {
                            int procId = int.Parse(match.Groups[1].Value);
                            int maxPerf = int.Parse(match.Groups[2].Value);
                            map[procId] = maxPerf;
                        }
                    }
                }
            }
            catch
            {
                // EventLog reading might be restricted in some environments
            }

            return map;
        }
    }
}
