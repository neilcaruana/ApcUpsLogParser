using ApcUpsLogParser.Common.Models;

namespace ApcUpsLogParser.Common.Services
{
    public static class DataProcessor
    {
        public static List<LogEntry> ProcessData(List<LogEntry> entries, int? days, bool today, int? smooth)
        {
            var processedEntries = entries.OrderBy(e => e.Timestamp).ToList();

            if (today)
                processedEntries = processedEntries.Where(e => e.Timestamp.Date == DateTime.Now.Date).ToList();
            else if (days.HasValue)
            {
                var cutoff = DateTime.Now.AddDays(-days.Value);
                processedEntries = processedEntries.Where(e => e.Timestamp >= cutoff).ToList();
            }

            if (smooth.HasValue && smooth.Value > 0 && processedEntries.Count > smooth.Value)
            {
                var smoothedVoltages = new double[processedEntries.Count];
                for (int i = 0; i < processedEntries.Count; i++)
                {
                    var window = processedEntries.Skip(Math.Max(0, i - smooth.Value / 2)).Take(smooth.Value).ToList();
                    smoothedVoltages[i] = window.Average(e => e.Voltage);
                }
                for (int i = 0; i < processedEntries.Count; i++)
                    processedEntries[i].Voltage = smoothedVoltages[i];
            }

            return processedEntries;
        }
    }
}