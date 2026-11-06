using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ZenithPower.Core.Abstractions;
using ZenithPower.Core.Database;
using ZenithPower.Core.Diagnostics;
using ZenithPower.Core.Engine;
using ZenithPower.Core.Models;
using ZenithPower.Hardware.Governors;
using ZenithPower.Watchdog;

namespace ZenithPower.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private IHardwareGovernor _governor;
        private readonly WatchdogService _watchdog;
        private readonly HardwareDatabase _database;
        private readonly DispatcherTimer _telemetryTimer;

        private ProfileType _selectedProfileType = ProfileType.Normal;
        private WorkloadPreset _selectedWorkloadPreset = WorkloadPreset.Standard;
        private int _currentEpp = 50;
        private int _currentIncreaseThreshold = 50;
        private int _currentThrottleMaxPercent = 100;
        private int _currentThrottleMinPercent = 10;
        private int _currentBoostMode = 1;
        private double _currentCpuPowerLimitW = 105.0;
        private bool _isMockMode = false; // REAL HARDWARE BY DEFAULT

        // Real Telemetry properties
        private string _cpuName = "Loading CPU...";
        private int _physicalCoreCount = Environment.ProcessorCount / 2;
        private int _logicalCoreCount = Environment.ProcessorCount;
        private double _cpuUsagePercent;
        private double _cpuTempC;
        private double _cpuPowerW;
        private double _nominalMhz;
        private double _effectiveMhz;
        private double _clockStretchingRatio = 1.0;
        private string _clockStretchingStatus = "Healthy";

        // CPU Benchmark state
        private bool _isBenchmarking;
        private string _benchmarkStatus = "Ready to run CPU Benchmark";
        private CpuBenchmarkReport? _lastBenchmarkReport;

        // Alerts / Watchdog
        private string _systemAlert = string.Empty;
        private bool _hasAlert;

        public ObservableCollection<CpuCoreInfo> Cores { get; } = new();
        public ObservableCollection<string> BenchmarkLogs { get; } = new();
        public ObservableCollection<CpuHardwarePreset> KnownCpus { get; } = new();
        public ObservableCollection<MemoryHardwarePreset> KnownMemory { get; } = new();

        public string CpuName { get => _cpuName; set => SetProperty(ref _cpuName, value); }
        public int PhysicalCoreCount { get => _physicalCoreCount; set => SetProperty(ref _physicalCoreCount, value); }
        public int LogicalCoreCount { get => _logicalCoreCount; set => SetProperty(ref _logicalCoreCount, value); }
        public double CpuUsagePercent { get => _cpuUsagePercent; set => SetProperty(ref _cpuUsagePercent, value); }

        public ProfileType SelectedProfileType
        {
            get => _selectedProfileType;
            set
            {
                if (SetProperty(ref _selectedProfileType, value))
                {
                    NotifyProfileSelectionChanged();
                    ApplyProfile(value, SelectedWorkloadPreset);
                }
            }
        }

        public WorkloadPreset SelectedWorkloadPreset
        {
            get => _selectedWorkloadPreset;
            set
            {
                if (SetProperty(ref _selectedWorkloadPreset, value))
                {
                    NotifyPresetSelectionChanged();
                    ApplyProfile(SelectedProfileType, value);
                }
            }
        }

        // Selection state helpers for XAML Triggers
        public bool IsSuperSaverSelected => SelectedProfileType == ProfileType.SuperSaver;
        public bool IsSaverSelected => SelectedProfileType == ProfileType.Saver;
        public bool IsNormalSelected => SelectedProfileType == ProfileType.Normal;
        public bool IsPerformanceSelected => SelectedProfileType == ProfileType.Performance;
        public bool IsUltimateSelected => SelectedProfileType == ProfileType.UltimatePerformance;

        public bool IsStandardPresetSelected => SelectedWorkloadPreset == WorkloadPreset.Standard;
        public bool IsGamingPresetSelected => SelectedWorkloadPreset == WorkloadPreset.Gaming;
        public bool IsRenderingPresetSelected => SelectedWorkloadPreset == WorkloadPreset.Rendering;
        public bool IsSingleThreadedPresetSelected => SelectedWorkloadPreset == WorkloadPreset.SingleThreaded;
        public bool IsTurboPresetSelected => SelectedWorkloadPreset == WorkloadPreset.Turbo;

        private void NotifyProfileSelectionChanged()
        {
            OnPropertyChanged(nameof(IsSuperSaverSelected));
            OnPropertyChanged(nameof(IsSaverSelected));
            OnPropertyChanged(nameof(IsNormalSelected));
            OnPropertyChanged(nameof(IsPerformanceSelected));
            OnPropertyChanged(nameof(IsUltimateSelected));
        }

        private void NotifyPresetSelectionChanged()
        {
            OnPropertyChanged(nameof(IsStandardPresetSelected));
            OnPropertyChanged(nameof(IsGamingPresetSelected));
            OnPropertyChanged(nameof(IsRenderingPresetSelected));
            OnPropertyChanged(nameof(IsSingleThreadedPresetSelected));
            OnPropertyChanged(nameof(IsTurboPresetSelected));
        }

        public int CurrentEpp
        {
            get => _currentEpp;
            set
            {
                if (SetProperty(ref _currentEpp, value))
                {
                    _governor.ActiveProfile.Epp = value;
                    _governor.ApplyPowerProfileAsync(_governor.ActiveProfile);
                }
            }
        }

        public int CurrentIncreaseThreshold
        {
            get => _currentIncreaseThreshold;
            set
            {
                if (SetProperty(ref _currentIncreaseThreshold, value))
                {
                    _governor.ActiveProfile.IncreaseThresholdPercent = value;
                    _governor.ApplyPowerProfileAsync(_governor.ActiveProfile);
                }
            }
        }

        public int CurrentThrottleMaxPercent
        {
            get => _currentThrottleMaxPercent;
            set
            {
                if (SetProperty(ref _currentThrottleMaxPercent, value))
                {
                    _governor.ActiveProfile.ThrottleMaxPercent = value;
                    _governor.ApplyPowerProfileAsync(_governor.ActiveProfile);
                }
            }
        }

        public int CurrentThrottleMinPercent
        {
            get => _currentThrottleMinPercent;
            set
            {
                if (SetProperty(ref _currentThrottleMinPercent, value))
                {
                    _governor.ActiveProfile.ThrottleMinPercent = value;
                    _governor.ApplyPowerProfileAsync(_governor.ActiveProfile);
                }
            }
        }

        public int CurrentBoostMode
        {
            get => _currentBoostMode;
            set
            {
                if (SetProperty(ref _currentBoostMode, value))
                {
                    _governor.ActiveProfile.BoostMode = value;
                    _governor.ApplyPowerProfileAsync(_governor.ActiveProfile);
                }
            }
        }

        public double CurrentCpuPowerLimitW
        {
            get => _currentCpuPowerLimitW;
            set
            {
                if (SetProperty(ref _currentCpuPowerLimitW, value))
                {
                    _governor.ActiveProfile.CpuPowerLimitW = value;
                    _governor.ApplyPowerProfileAsync(_governor.ActiveProfile);
                }
            }
        }

        public bool IsMockMode
        {
            get => _isMockMode;
            set
            {
                if (SetProperty(ref _isMockMode, value))
                {
                    SwitchGovernorMode(value);
                }
            }
        }

        public double CpuTempC { get => _cpuTempC; set => SetProperty(ref _cpuTempC, value); }
        public double CpuPowerW { get => _cpuPowerW; set => SetProperty(ref _cpuPowerW, value); }
        public double NominalMhz { get => _nominalMhz; set => SetProperty(ref _nominalMhz, value); }
        public double EffectiveMhz { get => _effectiveMhz; set => SetProperty(ref _effectiveMhz, value); }
        public double ClockStretchingRatio { get => _clockStretchingRatio; set => SetProperty(ref _clockStretchingRatio, value); }
        public string ClockStretchingStatus { get => _clockStretchingStatus; set => SetProperty(ref _clockStretchingStatus, value); }

        public bool IsBenchmarking { get => _isBenchmarking; set => SetProperty(ref _isBenchmarking, value); }
        public string BenchmarkStatus { get => _benchmarkStatus; set => SetProperty(ref _benchmarkStatus, value); }
        public CpuBenchmarkReport? LastBenchmarkReport { get => _lastBenchmarkReport; set => SetProperty(ref _lastBenchmarkReport, value); }

        public string SystemAlert { get => _systemAlert; set => SetProperty(ref _systemAlert, value); }
        public bool HasAlert { get => _hasAlert; set => SetProperty(ref _hasAlert, value); }

        public ICommand SelectProfileCommand { get; }
        public ICommand SelectPresetCommand { get; }
        public ICommand StartBenchmarkCommand { get; }
        public ICommand DismissAlertCommand { get; }
        public ICommand AutoTuneAllCoresCommand { get; }

        public MainViewModel()
        {
            // Default to REAL Windows PPM Hardware Governor
            _governor = new WindowsPpmGovernor();
            _watchdog = new WatchdogService(_governor);
            _database = new HardwareDatabase();

            foreach (var cpu in _database.Cpus) KnownCpus.Add(cpu);
            foreach (var mem in _database.Memory) KnownMemory.Add(mem);

            SelectProfileCommand = new RelayCommand(p =>
            {
                if (p is ProfileType type) SelectedProfileType = type;
                else if (Enum.TryParse<ProfileType>(p?.ToString(), out var parsed)) SelectedProfileType = parsed;
            });

            SelectPresetCommand = new RelayCommand(p =>
            {
                if (p is WorkloadPreset preset) SelectedWorkloadPreset = preset;
                else if (Enum.TryParse<WorkloadPreset>(p?.ToString(), out var parsed)) SelectedWorkloadPreset = parsed;
            });

            StartBenchmarkCommand = new RelayCommand(async _ => await RunCpuBenchmarkAsync(), _ => !IsBenchmarking);
            DismissAlertCommand = new RelayCommand(() => HasAlert = false);
            AutoTuneAllCoresCommand = new RelayCommand(ApplySmartCurveOptimizer);

            _watchdog.BootRecoveryTriggered += (s, r) =>
            {
                SystemAlert = $"[FAIL-SAFE RECOVERY] {r.Message}";
                HasAlert = true;
            };

            _watchdog.EmergencyThermalActionTriggered += (s, msg) =>
            {
                SystemAlert = msg;
                HasAlert = true;
            };

            _watchdog.Start();

            // Telemetry update timer (1000ms)
            _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _telemetryTimer.Tick += (s, e) => UpdateTelemetry();
            _telemetryTimer.Start();

            // Apply initial profile to real system
            ApplyProfile(SelectedProfileType, SelectedWorkloadPreset);
        }

        private void SwitchGovernorMode(bool mock)
        {
            if (mock)
            {
                _governor = new MockHardwareGovernor(coreCount: _logicalCoreCount);
                SystemAlert = "Switched to Mock Simulation Mode.";
                HasAlert = true;
            }
            else
            {
                _governor = new WindowsPpmGovernor();
                SystemAlert = "Connected to Live Windows CPU Governor.";
                HasAlert = true;
            }

            ApplyProfile(SelectedProfileType, SelectedWorkloadPreset);
        }

        private void ApplyProfile(ProfileType type, WorkloadPreset preset)
        {
            var profile = PowerProfile.CreateDefault(type);
            profile.Preset = preset;

            // Apply Workload Preset adaptations
            switch (preset)
            {
                case WorkloadPreset.Gaming:
                    profile.BoostMode = Math.Max(profile.BoostMode, 2); // Aggressive Boost
                    profile.Epp = Math.Min(profile.Epp, 15);           // Rapid response ramp
                    profile.IncreaseThresholdPercent = Math.Min(profile.IncreaseThresholdPercent, 15);
                    break;

                case WorkloadPreset.Rendering:
                    profile.BoostMode = Math.Max(profile.BoostMode, 1);
                    profile.ThrottleMaxPercent = 100;
                    profile.CpuPowerLimitW = Math.Max(profile.CpuPowerLimitW, 145.0);
                    break;

                case WorkloadPreset.SingleThreaded:
                    profile.BoostMode = Math.Max(profile.BoostMode, 2); // Aggressive Boost on Star Cores
                    profile.Epp = Math.Min(profile.Epp, 5);             // Instant ramp
                    profile.IncreaseThresholdPercent = Math.Min(profile.IncreaseThresholdPercent, 10);
                    break;

                case WorkloadPreset.Turbo:
                    profile.BoostMode = 2;                             // Aggressive Boost
                    profile.ThrottleMaxPercent = 100;
                    profile.ThrottleMinPercent = 100;                  // Lock max P-State
                    profile.Epp = 0;                                   // Max throughput
                    profile.IncreaseThresholdPercent = 5;
                    profile.CpuPowerLimitW = Math.Max(profile.CpuPowerLimitW, 200.0);
                    break;
            }

            // Power Profile overrides for power-saving modes
            if (type == ProfileType.SuperSaver)
            {
                profile.ThrottleMaxPercent = 65; // Frequency capped to 65% of base
                profile.ThrottleMinPercent = 5;
                profile.BoostMode = 0;           // Disable CPPC boost to eliminate heat spikes
                profile.Epp = 85;
                profile.IncreaseThresholdPercent = 85;
            }
            else if (type == ProfileType.Saver)
            {
                profile.ThrottleMaxPercent = 85; // Frequency capped to 85%
                profile.ThrottleMinPercent = 5;
                profile.BoostMode = 3;           // Efficient boost
                profile.Epp = 65;
            }

            _currentEpp = profile.Epp;
            _currentIncreaseThreshold = profile.IncreaseThresholdPercent;
            _currentThrottleMaxPercent = profile.ThrottleMaxPercent;
            _currentThrottleMinPercent = profile.ThrottleMinPercent;
            _currentBoostMode = profile.BoostMode;
            _currentCpuPowerLimitW = profile.CpuPowerLimitW;

            OnPropertyChanged(nameof(CurrentEpp));
            OnPropertyChanged(nameof(CurrentIncreaseThreshold));
            OnPropertyChanged(nameof(CurrentThrottleMaxPercent));
            OnPropertyChanged(nameof(CurrentThrottleMinPercent));
            OnPropertyChanged(nameof(CurrentBoostMode));
            OnPropertyChanged(nameof(CurrentCpuPowerLimitW));

            _governor.ApplyPowerProfileAsync(profile);

            var offsets = CurveOptimizerBalancer.CalculateOptimalOffsets(
                _governor.GetTelemetry().Cores, profile.AllCoreCurveOffset, preset);
            _governor.ApplyCurveOptimizerAsync(offsets);

            UpdateTelemetry();
        }

        private void ApplySmartCurveOptimizer()
        {
            var telemetry = _governor.GetTelemetry();
            int baseBias = SelectedProfileType == ProfileType.SuperSaver ? -25 : -20;
            var optimal = CurveOptimizerBalancer.CalculateOptimalOffsets(telemetry.Cores, baseBias, SelectedWorkloadPreset);
            _governor.ApplyCurveOptimizerAsync(optimal);

            SystemAlert = $"Smart Curve Optimizer applied! Star Cores protected (0 to -7), Secondary cores set to {baseBias}.";
            HasAlert = true;
            UpdateTelemetry();
        }

        private async Task RunCpuBenchmarkAsync()
        {
            IsBenchmarking = true;
            BenchmarkLogs.Clear();
            BenchmarkStatus = "Starting built-in CPU compute benchmark...";

            var runner = new CpuBenchmarkRunner(_governor);
            var progress = new Progress<string>(msg =>
            {
                BenchmarkLogs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
                BenchmarkStatus = msg;
            });

            try
            {
                LastBenchmarkReport = await runner.RunFullCpuBenchmarkAsync(progress);
                BenchmarkStatus = $"Benchmark complete! Multi-Core: {LastBenchmarkReport.MultiCoreScore:N0} pts • Single-Core: {LastBenchmarkReport.SingleCoreScore:N0} pts.";
                SystemAlert = $"Benchmark Finished: {LastBenchmarkReport.MultiCoreScore:N0} multi-core score on profile {LastBenchmarkReport.ProfileName}.";
                HasAlert = true;
            }
            catch (Exception ex)
            {
                BenchmarkStatus = $"Benchmark failed: {ex.Message}";
            }
            finally
            {
                IsBenchmarking = false;
                UpdateTelemetry();
            }
        }

        private void UpdateTelemetry()
        {
            var snap = _governor.GetTelemetry();
            CpuName = snap.CpuName;
            PhysicalCoreCount = snap.PhysicalCoreCount;
            LogicalCoreCount = snap.LogicalCoreCount;
            CpuUsagePercent = snap.CpuUsagePercent;
            CpuTempC = snap.CpuPackageTempC;
            CpuPowerW = snap.CpuPackagePowerW;
            NominalMhz = snap.AverageNominalMhz;
            EffectiveMhz = snap.AverageEffectiveMhz;
            ClockStretchingRatio = snap.OverallClockStretchingRatio;

            var stretchEval = ClockStretchingDetector.Evaluate(NominalMhz, EffectiveMhz);
            ClockStretchingStatus = stretchEval.Severity switch
            {
                ClockStretchingSeverity.None => "Healthy (100% Execution)",
                ClockStretchingSeverity.Mild => "Mild Stretching (Volts Starving)",
                ClockStretchingSeverity.Severe => "Severe Stretching (Instability)",
                _ => "Unknown"
            };

            // Update core count collection smoothly
            if (Cores.Count != snap.Cores.Count)
            {
                Cores.Clear();
                foreach (var core in snap.Cores)
                {
                    Cores.Add(core);
                }
            }
            else
            {
                for (int i = 0; i < snap.Cores.Count; i++)
                {
                    Cores[i].NominalMhz = snap.Cores[i].NominalMhz;
                    Cores[i].EffectiveMhz = snap.Cores[i].EffectiveMhz;
                    Cores[i].CurveOffset = snap.Cores[i].CurveOffset;
                }
            }
        }
    }
}
