using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZenithPower.Core.Database
{
    public record CpuHardwarePreset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("architecture")] string Architecture,
        [property: JsonPropertyName("socket")] string Socket,
        [property: JsonPropertyName("stockTdpW")] double StockTdpW,
        [property: JsonPropertyName("ecoTdpW")] double EcoTdpW,
        [property: JsonPropertyName("maxTdpW")] double MaxTdpW,
        [property: JsonPropertyName("recommendedMaxNegativeOffset")] int RecommendedMaxNegativeOffset,
        [property: JsonPropertyName("recommendedStarCoreNegativeOffset")] int RecommendedStarCoreNegativeOffset,
        [property: JsonPropertyName("maxSafePackageTempC")] double MaxSafePackageTempC);

    public record GpuHardwarePreset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("architecture")] string Architecture,
        [property: JsonPropertyName("stockTdpW")] double StockTdpW,
        [property: JsonPropertyName("ecoTdpW")] double EcoTdpW,
        [property: JsonPropertyName("maxTdpW")] double MaxTdpW,
        [property: JsonPropertyName("vfCurveAnchorMilliVolts")] int VfCurveAnchorMilliVolts,
        [property: JsonPropertyName("targetBoostClockMhz")] double TargetBoostClockMhz);

    public record MemoryHardwarePreset(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("jedecVoltageV")] double JedecVoltageV,
        [property: JsonPropertyName("xmpExpoTypicalVoltageV")] double XmpExpoTypicalVoltageV,
        [property: JsonPropertyName("maxRecommendedSocVoltageV")] double MaxRecommendedSocVoltageV,
        [property: JsonPropertyName("guideline")] string Guideline);

    internal record DatabaseRoot(
        [property: JsonPropertyName("cpus")] List<CpuHardwarePreset> Cpus,
        [property: JsonPropertyName("gpus")] List<GpuHardwarePreset> Gpus,
        [property: JsonPropertyName("memory")] List<MemoryHardwarePreset> Memory);

    public class HardwareDatabase
    {
        private readonly List<CpuHardwarePreset> _cpus = new();
        private readonly List<GpuHardwarePreset> _gpus = new();
        private readonly List<MemoryHardwarePreset> _memory = new();

        public IReadOnlyList<CpuHardwarePreset> Cpus => _cpus;
        public IReadOnlyList<GpuHardwarePreset> Gpus => _gpus;
        public IReadOnlyList<MemoryHardwarePreset> Memory => _memory;

        public HardwareDatabase()
        {
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string directPath = Path.Combine(baseDir, "Database", "hardware-profiles.json");

            string? json = null;
            if (File.Exists(directPath))
            {
                json = File.ReadAllText(directPath);
            }
            else
            {
                // Try locating from assembly directory or relative source directory
                string fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "src", "ZenithPower.Core", "Database", "hardware-profiles.json");
                if (File.Exists(fallbackPath))
                {
                    json = File.ReadAllText(fallbackPath);
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var root = JsonSerializer.Deserialize<DatabaseRoot>(json, options);
                if (root != null)
                {
                    _cpus.AddRange(root.Cpus ?? new());
                    _gpus.AddRange(root.Gpus ?? new());
                    _memory.AddRange(root.Memory ?? new());
                }
            }
        }

        public CpuHardwarePreset? LookupCpuProfile(string cpuName)
        {
            if (string.IsNullOrWhiteSpace(cpuName)) return null;

            return _cpus.FirstOrDefault(c =>
                c.Name.Equals(cpuName, StringComparison.OrdinalIgnoreCase) ||
                cpuName.Contains(c.Name, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(cpuName, StringComparison.OrdinalIgnoreCase));
        }

        public GpuHardwarePreset? LookupGpuProfile(string gpuName)
        {
            if (string.IsNullOrWhiteSpace(gpuName)) return null;

            return _gpus.FirstOrDefault(g =>
                g.Name.Equals(gpuName, StringComparison.OrdinalIgnoreCase) ||
                gpuName.Contains(g.Name, StringComparison.OrdinalIgnoreCase) ||
                g.Name.Contains(gpuName, StringComparison.OrdinalIgnoreCase));
        }

        public MemoryHardwarePreset? LookupMemoryProfile(string memoryType)
        {
            if (string.IsNullOrWhiteSpace(memoryType)) return null;

            return _memory.FirstOrDefault(m =>
                m.Type.Equals(memoryType, StringComparison.OrdinalIgnoreCase) ||
                memoryType.Contains(m.Type, StringComparison.OrdinalIgnoreCase));
        }
    }
}
