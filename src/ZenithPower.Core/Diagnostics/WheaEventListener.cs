using System;
using System.Diagnostics.Eventing.Reader;
using System.Threading;

namespace ZenithPower.Core.Diagnostics
{
    public record WheaErrorEventArgs(int CoreIndex, int EventId, DateTime Timestamp, string Message);

    public class WheaEventListener : IDisposable
    {
        private EventLogWatcher? _watcher;
        public event EventHandler<WheaErrorEventArgs>? WheaErrorOccurred;

        public void Start()
        {
            try
            {
                // Query WHEA Logger Event 19 (Corrected Machine Check / Hardware Error)
                string query = "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and (EventID=19)]]";
                var eventQuery = new EventLogQuery("System", PathType.LogName, query);

                _watcher = new EventLogWatcher(eventQuery);
                _watcher.EventRecordWritten += OnEventRecordWritten;
                _watcher.Enabled = true;
            }
            catch
            {
                // In non-admin environments or mock tests, gracefully handle lack of EventLog access
            }
        }

        private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null) return;

            try
            {
                int eventId = e.EventRecord.Id;
                DateTime timestamp = e.EventRecord.TimeCreated ?? DateTime.UtcNow;
                string description = e.EventRecord.FormatDescription() ?? "WHEA Corrected Error";

                // Parse Core / Processor APIC ID if available
                int coreIndex = 0;
                if (description.Contains("Processor APIC ID:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = description.Split("Processor APIC ID:", StringSplitOptions.TrimEntries);
                    if (parts.Length > 1 && int.TryParse(parts[1].Split(' ')[0], out int parsedApic))
                    {
                        coreIndex = parsedApic;
                    }
                }

                WheaErrorOccurred?.Invoke(this, new WheaErrorEventArgs(coreIndex, eventId, timestamp, description));
            }
            catch
            {
                // Guard against format failures
            }
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.Enabled = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
