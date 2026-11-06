# ZenithPower — Architectural Specification

## 1. Executive Summary

`ZenithPower` is an autonomous Windows CPU and GPU power governor, voltage tuner, and dynamic hardware balancer. It eliminates two historical flaws in traditional power managers:
1. **Sluggishness ("Dumb & Lazy") in Power Savers**: Traditional schemes clamp CPU base frequencies to 800 MHz–1.2 GHz, causing micro-stutters because interactive burst work cannot quickly execute.
2. **Silicon Lottery Instability**: Fixed undervoltage tables cause idle crashes and cache WHEA errors.

`ZenithPower` pairs **Windows Processor Power Management (PPM)** dynamic governors with **intelligent per-core Curve Optimizer tuning**, **real-time effective-vs-nominal clock stretching diagnosis**, and a **fail-safe crash recovery watchdog**.

---

## 2. Core Diagnostic: Effective Clock vs. Nominal Clock

Modern CPUs (AMD Zen and Intel Speed Shift) implement internal hardware clock modulation. If a core is starved of voltage under load, its internal voltage regulator triggers **clock stretching**—skipping clock cycles to prevent an immediate crash, but degrading instructions-per-cycle throughput while Task Manager reports a high multiplier.

### Mathematical Formulation
$$\text{Effective Clock} = \text{BCLK} \times \frac{\Delta \text{APERF}}{\Delta \text{MPERF}} \times \text{BaseRatio}$$

* **Execution Ratio**:
  $$\text{Ratio} = \frac{\text{Effective Clock}}{\text{Nominal Clock}}$$
* **Diagnostic Thresholds**:
  * $\ge 0.98$: **Healthy Full Throughput**. Voltage supply is stable.
  * $0.90 - 0.97$: **Mild Clock Stretching**. Voltage is starving performance; reduce negative offset by 3–5 counts.
  * $< 0.90$: **Severe Clock Stretching**. Instability imminent; revert negative offset immediately.

---

## 3. Smart Per-Core Curve Optimizer (CO) & Star-Core Balancing

* **Star Core Protection**: Queries CPPC2 / ACPI `_CPC` / SMU to identify factory Star (Preferred) cores (Rank 1 & 2). Because Star cores boost to the highest single-thread multipliers where voltage margins are tightest, they receive conservative offsets (`-5` to `0`).
* **Problematic Core Isolation**: Cores flagged as weak or unstable receive `0` or positive bias (`+2` to `+5`).
* **Secondary Core Efficiency**: Non-star cores receive aggressive negative offsets (`-20` to `-30`), dropping overall CPU package temperature and power draw by 15–25W without sacrificing single-core boost.

---

## 4. Workload Overlays & Power Profiles

| Profile | CPU EPP | Increase Threshold | Boost Mode | CPU Target | GPU Target | Primary Objective |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Super Saver** | 85–90 | 85% | Efficient Enabled | 35W–45W Eco | 50%–60% | Icy-cold idle, snappy burst response |
| **Saver** | 65–70 | 70% | Efficient Aggressive | 65W Base TDP | 70%–75% | Sustained battery / light office efficiency |
| **Normal** | 50 | 50% | Enabled | 100% Stock | 100% Stock | Standard balanced dynamic scaling |
| **Performance** | 25 | 30% | Aggressive | 125W–150W | 100%–105% | Low latency, fast multi-thread ramp-up |
| **Ultimate** | 0 | 10% | Aggressive | Max Board Limit | 115%–120% | Zero power gating, unlocked board power |

### Workload Presets
* **Gaming**: High single/dual-core boost priority. Secondary threads parked or directed to P-cores/cache CCD. GPU granted full thermal/power headroom with optimized V/F undervolt curve.
* **Rendering**: All-core sustained power priority with a strict thermal cap (85°C limit to prevent thermal throttling cliff).
* **Single-Threaded Task**: Amplifies Star Cores to peak boost multiplier, lowering other cores to save package power.
* **Turbo (Dual Amplification)**: Pushes both single-core burst frequency (via Star Core VID tuning) and all-core sustained throughput simultaneously.

---

## 5. Fail-Safe Watchdog & Zero-BSOD Testing

1. **Dual-Engine Architecture (`MockHardwareGovernor` vs Live Driver)**:
   * The mock governor emulates CPU MSRs, AMD SMU, CPPC star cores, and GPU NVAPI. 100% of UI, benchmark logic, and profile switching can be safely verified without touching ring-0.
2. **Instant Clock Stretching Tripwire**:
   * The benchmark polls APERF/MPERF ratios every 100ms. If effective frequency dips below 97% of nominal under load, the sweep halts and steps back up *before* hardware freeze occurs.
3. **WHEA Event 19 (Corrected Error) Listener**:
   * Monitors Windows WHEA Logger for corrected errors and automatically steps up core offset by `+5`.
4. **Boot Recovery Watchdog**:
   * Every tuning change is tracked in an `active_tuning.lock` token.
   * If an unexpected freeze, power cut, or BSOD occurs, the startup watchdog immediately resets all voltages and frequencies to factory stock BIOS defaults and logs the incident report.
