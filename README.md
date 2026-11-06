# ⚡ ZenithPower

> **Smart Windows CPU & GPU Power, Voltage & Governor System**  
> *Autonomous power management, per-core Curve Optimizer tuning, APERF/MPERF clock stretching diagnosis, and fail-safe crash recovery for modern AMD and Intel systems.*

[![Build & Test](https://img.shields.io/badge/tests-54%20passed-brightgreen.svg)](https://github.com/armanpier/ZenithPower)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011%20(x64)-blue.svg)](https://github.com/armanpier/ZenithPower)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF-purple.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## 🌟 Overview

Default Windows power plans suffer from two persistent flaws:
1. **The "Dumb & Lazy" Problem**: Traditional power savers clamp CPU multipliers down to 800 MHz–1.2 GHz. While power drops, the system becomes sluggish and stutters during mouse movement, typing, or opening browser tabs because interactive burst work cannot complete.
2. **The "Silicon Lottery" Trap**: Generic undervolting guides prescribe fixed voltages across all silicon. Because every CPU/GPU die has unique factory-fused voltage/frequency characteristics, static voltages cause idle BSODs, cache WHEA errors, or hidden **clock stretching**.

**ZenithPower** solves both issues through a modern **dynamic governor engine** paired with **intelligent per-core Curve Optimizer balancing**, **real-time APERF/MPERF clock stretching diagnostics**, and a **fail-safe crash recovery watchdog**.

---

## 🚀 Key Features

### 1. 🍃 Super Saver & 5 Dynamic Power Profiles
* **Super Saver**: Icy-cold idle temperatures and ultra-low power consumption (35W–45W Eco TDP) without sluggishness. Modulates **Energy Performance Preference (EPP: 85–90)** and **Increase Thresholds (85%)** so light background tasks do not wake up full boost, while interactive foreground clicks burst instantly to full base clocks.
* **Saver**: Balanced Eco (65W, EPP 65, efficient boost).
* **Normal**: Balanced factory scaling with safe negative baseline.
* **Performance**: EPP 25, quick clock ramp-up, aggressive boost.
* **Ultimate Performance**: EPP 0, core parking disabled, zero throttle delay, max unlocked board limits.

### 2. 🎯 4 Workload Preset Overlays
* **🎮 Gaming**: Prioritizes single/dual-core peak boost, parks background threads, and grants the GPU maximum thermal headroom with an optimized V/F undervolt curve.
* **🎨 Rendering**: Sustained all-core power priority with an 85°C thermal ceiling to prevent thermal throttling cliffs.
* **⚡ Single-Threaded Burst**: Directs maximum voltage to factory Star Cores, lowering secondary cores to eliminate package power waste.
* **💥 Turbo (Dual Amplification)**: Pushes single-core burst and multi-core throughput simultaneously.

### 3. 🔬 Effective Clock vs. Nominal Clock (Clock Stretching Detection)
* When a CPU core starves for voltage under load, its internal voltage regulator skips clock cycles (**clock stretching**). Task Manager deceptively reports high multipliers, but real instruction throughput plunges.
* ZenithPower measures actual hardware performance counters:
  $$\text{Effective Clock} = \text{BCLK} \times \frac{\Delta \text{APERF}}{\Delta \text{MPERF}} \times \text{BaseRatio}$$
* **Diagnostic Thresholds**:
  * $\ge 98\%$: **Healthy Full Throughput**.
  * $90\% - 97\%$: **Mild Clock Stretching** (voltage starving; reduce negative offset by 3–5 counts).
  * $< 90\%$: **Severe Clock Stretching** (performance regression; step back immediately).

### 4. 🌟 Smart Per-Core Curve Optimizer (CO) & Star-Core Balancing
* **Star Core Protection**: Automatically queries CPPC2 / ACPI `_CPC` / SMU to detect factory Star (Preferred) cores (Rank 1 & 2). Star cores boost to the highest MHz and receive conservative offsets (`-5` to `0`).
* **Problematic Core Isolation**: Cores flagged as weak or unstable receive `0` or positive bias (`+2` to `+5`).
* **Secondary Core Efficiency**: Non-star cores receive aggressive negative offsets (`-20` to `-30`), dropping package temperature and power draw by 15–25W without sacrificing single-core boost.

### 5. 🛡️ Zero-BSOD Safe Testing Framework & Crash Recovery Watchdog
* **Zero-BSOD Mock Simulation Mode**: Built-in mock governor emulates CPU MSRs, AMD SMU, CPPC star cores, and GPU NVAPI. 100% of the UI, state machine, and calibration logic can be tested without hardware risk.
* **Instant Clock Stretching Tripwire**: The benchmark polls APERF/MPERF ratios every 100ms. If effective clock dips below 97% under load, the sweep halts and steps back up *before* a hardware freeze occurs.
* **Real-time WHEA Event 19 Listener**: Catches corrected hardware errors from the Windows WHEA Logger and automatically steps up core offset by `+5`.
* **Boot Recovery Watchdog**: Every tuning change is tracked in an `active_tuning.lock` token. On dirty reboot, ZenithPower automatically resets all voltages and frequencies to stock BIOS defaults and logs an incident report.
* **Thermal Emergency Guard**: Background monitor steps down power limits by 25% if CPU exceeds 95°C or GPU hotspot exceeds 100°C.

### 6. 📚 Curated Hardware Knowledge Base
Includes built-in presets and safe operating boundaries for:
* **AMD Ryzen**: Zen 3 (5800X3D), Zen 4 (7800X3D, 7950X, 7700X), Zen 5 (9800X3D).
* **Intel Core**: 13th & 14th Gen (i9-14900K, i7-14700K, i7-13700K, i5-13600K).
* **NVIDIA GeForce RTX**: RTX 4090, 4080, 4070 Ti, 3080.
* **AMD Radeon**: RX 7900 XTX, RX 7800 XT.
* **Memory Standards**: DDR4 / DDR5 JEDEC, XMP, EXPO, and safe SoC voltage limits ($\le 1.25\text{V}$ on AM5).

---

## 📦 Downloads & Installation

### Option A: Portable Single-File (Recommended)
Download `ZenithPower.exe` directly from the [Latest Release](https://github.com/armanpier/ZenithPower/releases).
* Single-file, self-contained executable.
* Zero external prerequisites (no .NET runtime installation required).
* Right-click and **Run as Administrator** to enable hardware access.

### Option B: Build from Source
```powershell
# Clone the repository
git clone https://github.com/armanpier/ZenithPower.git
cd ZenithPower

# Build and run the test suite
dotnet test ZenithPower.sln

# Publish single-file portable executable
powershell -ExecutionPolicy Bypass -File .\scripts\publish-portable.ps1
```

### Option C: Build MSI Installer (WiX)
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1
```

---

## 🏗️ Architecture

```
ZenithPower/
├── src/
│   ├── ZenithPower.Core/       # Domain models, Curve Optimizer balancer, benchmark runner, hardware DB
│   ├── ZenithPower.Hardware/   # Windows PPM Win32 interop, Mock simulation engine, driver bridge
│   ├── ZenithPower.Watchdog/   # Crash recovery manager, dirty reboot detector, thermal guard
│   └── ZenithPower.UI/         # Modern reactive WPF dashboard, telemetry gauges, per-core sliders
├── tests/
│   └── ZenithPower.Tests/      # Comprehensive xUnit test suite (54 unit/integration tests)
├── packaging/                  # WiX toolset configuration for MSI installer
└── scripts/                    # Portable publish and MSI build scripts
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed mathematical formulas, PPM GUIDs, and tuning workflows.

---

## ⚖️ License

Distributed under the [MIT License](LICENSE).
