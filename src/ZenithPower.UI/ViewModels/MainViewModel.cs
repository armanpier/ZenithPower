using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        private double _currentCpuPowerLimitW = 105.0;
        private double _currentGpuPowerLimitPercent = 100.0;
        private bool _isMockMode = true;

        // Telemetry properties
        private double _cpuTempC;
        private double _cpuPowerW;
        private double _nominalMhz;
        private double _effectiveMhz;
        private double _clockStretchingRatio = 1.0;
        private string _clockStretchingStatus = "Healthy";
        private double _gpuTempC;
        private double _gpuPowerW;
        private double _gpuClockMhz;

        // Benchmark state
        private bool _isBenchmarking;
        private string _benchmarkStatus = "Ready to calibrate";
        private string _benchmarkSummary = string.Empty;

        // Alerts / Watchdog
        private string _systemAlert = string.Empty;
        private bool _hasAlert;

        public ObservableCollection<CpuCoreInfo> Cores { get; } = new();
        public ObservableCollection<string> BenchmarkLogs { get; } = new();
        public ObservableCollection<CpuHardwarePreset> KnownCpus { get; } = new();
        public ObservableCollection<GpuHardwarePreset> KnownGpus { get; } = new();
        public ObservableCollection<MemoryHardwarePreset> KnownMemory { get; } = new();

        public ProfileType SelectedProfileType
        {
            get => _selectedProfileType;
            set
            {
                if (SetProperty(ref _selectedProfileType, value))
                {
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
                    ApplyProfile(SelectedProfileType, value);
                }
            }
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

        public double CurrentGpuPowerLimitPercent
        {
            get => _currentGpuPowerLimitPercent;
            set
            {
                if (SetProperty(ref _currentGpuPowerLimitPercent, value))
                {
                    _governor.ActiveProfile.GpuPowerLimitPercent = value;
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
        public double GpuTempC { get => _gpuTempC; set => SetProperty(ref _gpuTempC, value); }
        public double GpuPowerW { get => _gpuPowerW; set => SetProperty(ref _gpuPowerW, value); }
        public double GpuClockMhz { get => _gpuClockMhz; set => SetProperty(ref _gpuClockMhz, value); }

        public bool IsBenchmarking { get => _isBenchmarking; set => SetProperty(ref _isBenchmarking, value); }
        public string BenchmarkStatus { get => _benchmarkStatus; set => SetProperty(ref _benchmarkStatus, value); }
        public string BenchmarkSummary { get => _benchmarkSummary; set => SetProperty(ref _benchmarkSummary, value); }

        public string SystemAlert { get => _systemAlert; set => SetProperty(ref _systemAlert, value); }
        public bool HasAlert { get => _hasAlert; set => SetProperty(ref _hasAlert, value); }

        public ICommand SelectProfileCommand { get; }
        public ICommand SelectPresetCommand { get; }
        public ICommand StartBenchmarkCommand { get; }
        public ICommand DismissAlertCommand { get; }
        public ICommand AutoTuneAllCoresCommand { get; }

        public MainViewModel()
        {
            _governor = new MockHardwareGovernor(coreCount: 8);
            _watchdog = new WatchdogService(_governor);
            _database = new HardwareDatabase();

            // Populate hardware database collections
            foreach (var cpu in _database.Cpus) KnownCpus.Add(cpu);
            foreach (var gpu in _database.Gpus) KnownGpus.Add(gpu);
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

            StartBenchmarkCommand = new RelayCommand(async _ => await RunBenchmarkAsync(), _ => !IsBenchmarking);
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

            // Initial telemetry read
            UpdateTelemetry();
        }

        private void SwitchGovernorMode(bool mock)
        {
            if (mock)
            {
                _governor = new MockHardwareGovernor(coreCount: 8);
                SystemAlert = "Switched to Zero-BSOD Mock Simulation Mode. Safe to test without hardware risk.";
                HasAlert = true;
            }
            else
            {
                _governor = new WindowsPpmGovernor();
                SystemAlert = "Connected to Live Windows PPM Governor. Applying real system settings.";
                HasAlert = true;
            }

            ApplyProfile(SelectedProfileType, SelectedWorkloadPreset);
        }

        private void ApplyProfile(ProfileType type, WorkloadPreset preset)
        {
            var profile = PowerProfile.CreateDefault(type);
            profile.Preset = preset;

            _currentEpp = profile.Epp;
            _currentIncreaseThreshold = profile.IncreaseThresholdPercent;
            _currentCpuPowerLimitW = profile.CpuPowerLimitW;
            _currentGpuPowerLimitPercent = profile.GpuPowerLimitPercent;

            OnPropertyChanged(nameof(CurrentEpp));
            OnPropertyChanged(nameof(CurrentIncreaseThreshold));
            OnPropertyChanged(nameof(CurrentCpuPowerLimitW));
            OnPropertyChanged(nameof(CurrentGpuPowerLimitPercent));

            _governor.ApplyPowerProfileAsync(profile);

            // Also balance Curve Optimizer per core for this preset
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

        private async Task RunBenchmarkAsync()
        {
            IsBenchmarking = true;
            BenchmarkLogs.Clear();
            BenchmarkStatus = "Calibrating voltage & frequency ratios...";
            BenchmarkSummary = string.Empty;

            var runner = new BenchmarkRunner(_governor);
            var progress = new Progress<string>(msg =>
            {
                BenchmarkLogs.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
                BenchmarkStatus = msg;
            });

            try
            {
                var result = await runner.RunQuickCalibrationAsync(progress, quickModeSeconds: 2);
                BenchmarkSummary = result.Summary;
                BenchmarkStatus = "Calibration complete!";
                SystemAlert = $"Calibration Success: {result.Summary}";
                HasAlert = true;
            }
            catch (Exception ex)
            {
                BenchmarkStatus = $"Calibration failed: {ex.Message}";
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
            CpuTempC = snap.CpuPackageTempC;
            CpuPowerW = snap.CpuPackagePowerW;
            NominalMhz = snap.AverageNominalMhz;
            EffectiveMhz = snap.AverageEffectiveMhz;
            ClockStretchingRatio = snap.OverallClockStretchingRatio;
            GpuTempC = snap.GpuTempC;
            GpuPowerW = snap.GpuPowerW;
            GpuClockMhz = snap.GpuClockMhz;

            var stretchEval = ClockStretchingDetector.Evaluate(NominalMhz, EffectiveMhz);
            ClockStretchingStatus = stretchEval.Severity switch
            {
                ClockStretchingSeverity.None => "Healthy (100% Throughput)",
                ClockStretchingSeverity.Mild => "Mild Stretching (Volts Starving)",
                ClockStretchingSeverity.Severe => "Severe Stretching (Instability)",
                _ => "Unknown"
            };

            Cores.Clear();
            foreach (var core in snap.Cores)
            {
                Cores.Add(core);
            }
        }
    }
}
