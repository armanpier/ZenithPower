using System;
using System.Runtime.InteropServices;

namespace ZenithPower.Hardware.Interop
{
    public static class Win32Ppm
    {
        // PPM Subgroup GUID: 54533251-82be-4824-96c1-47b60b740d00
        public static readonly Guid GUID_PROCESSOR_SETTINGS_SUBGROUP = new("54533251-82be-4824-96c1-47b60b740d00");

        // Energy Performance Preference (EPP): 36687f9e-e3a5-4dbf-b1dc-15eb381c6863
        public static readonly Guid GUID_PERF_EPP = new("36687f9e-e3a5-4dbf-b1dc-15eb381c6863");

        // Processor Performance Boost Mode: be337238-0d82-4146-a960-4f3749d470c7
        public static readonly Guid GUID_PERF_BOOST_MODE = new("be337238-0d82-4146-a960-4f3749d470c7");

        // Increase Threshold: 06cadf0e-64ed-448a-8927-ce7bf90eb35d
        public static readonly Guid GUID_PERF_INCREASE_THRESHOLD = new("06cadf0e-64ed-448a-8927-ce7bf90eb35d");

        // Decrease Threshold: 12a07d16-6593-4ced-bc38-a8a4f6d63fc0
        public static readonly Guid GUID_PERF_DECREASE_THRESHOLD = new("12a07d16-6593-4ced-bc38-a8a4f6d63fc0");

        // Core Parking Min Cores: 0cc5b647-c1df-4637-891a-dec607318358
        public static readonly Guid GUID_CORE_PARKING_MIN = new("0cc5b647-c1df-4637-891a-dec607318358");

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerGetActiveScheme(
            IntPtr UserRootPowerKey,
            out IntPtr ActivePolicyGuid);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(
            IntPtr UserRootPowerKey,
            ref Guid SchemeGuid);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerWriteACValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            uint AcValueIndex);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadACValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint AcValueIndex);
    }
}
