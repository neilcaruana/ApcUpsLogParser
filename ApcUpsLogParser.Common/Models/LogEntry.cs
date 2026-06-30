using System.Globalization;

namespace ApcUpsLogParser.Common.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public double Voltage { get; set; }
    }
}