using ApcUpsLogParser.Common.Models;

namespace ApcUpsLogParser.Common.DTOs
{
    public class VoltageDataRequest
    {
        public bool IsLive { get; set; }
        public int? Days { get; set; }
        public bool Today { get; set; }
        public bool Compare { get; set; }
        public int? Smooth { get; set; }
        public DateTime? StartDate { get; set; } // Added for range filtering
        public DateTime? EndDate { get; set; }   // Added for range filtering
    }

    public class VoltageDataResponse
    {
        public List<LogEntry> CurrentEntries { get; set; } = new();
        public List<LogEntry>? TodayEntries { get; set; }
        public List<LogEntry>? YesterdayEntries { get; set; }
        public VoltageStatistics Statistics { get; set; } = new();
        public List<DataGap> Gaps { get; set; } = new();
        public DateTime LastRefreshTime { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class VoltageStatistics
    {
        public double MaxVoltage { get; set; }
        public double MinVoltage { get; set; }
        public double AvgVoltage { get; set; }
        public double LastVoltage { get; set; }
        public double VoltageRange { get; set; }
        public double CompliancePercentage { get; set; }
        public double HoursAbove230 { get; set; }
        public double HoursBelow230 { get; set; }
        public int TotalPoints { get; set; }
        
        // Comparison statistics
        public VoltageStatistics? TodayStats { get; set; }
        public VoltageStatistics? YesterdayStats { get; set; }
    }

    public class DataGap
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsYesterday { get; set; }
        public string FormattedDuration { get; set; } = string.Empty;
    }
}