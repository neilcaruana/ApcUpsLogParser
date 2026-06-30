using ApcUpsLogParser.Common.Models;
using ApcUpsLogParser.Common.DTOs;
using ApcUpsLogParser.Common.Configuration;

namespace ApcUpsLogParser.Common.Services
{
    public class VoltageAnalysisService
    {
        public static VoltageDataResponse GetVoltageData(VoltageDataRequest request)
        {
            var response = new VoltageDataResponse
            {
                LastRefreshTime = DateTime.Now
            };

            try
            {
                Console.WriteLine($"Loading data from: {Constants.DataFilePath}");
                Console.WriteLine($"Request parameters: IsLive={request.IsLive}, Days={request.Days}, Today={request.Today}, Compare={request.Compare}, Smooth={request.Smooth}, StartDate={request.StartDate}, EndDate={request.EndDate}");

                if (!File.Exists(Constants.DataFilePath))
                {
                    response.HasError = true;
                    response.ErrorMessage = $"The configured APC UPS log file was not found at '{Constants.DataFilePath}'. Set 'DataFilePath' in the appsettings file to the full APC UPS log file path.";
                    Console.WriteLine(response.ErrorMessage);
                    return response;
                }
                
                if (request.IsLive)
                {
                    Console.WriteLine("Loading live data (last 3 hours)");
                    response.CurrentEntries = LoadLiveData(request.Smooth);
                }
                else if (request.Compare && request.Today)
                {
                    Console.WriteLine("Loading comparison data (today vs yesterday)");
                    response.TodayEntries = LoadTodayData(request.Smooth);
                    response.YesterdayEntries = LoadYesterdayData(request.Smooth);
                    response.CurrentEntries = response.TodayEntries;
                }
                else if (request.Today)
                {
                    Console.WriteLine("Loading today's data only");
                    response.CurrentEntries = LoadTodayData(request.Smooth);
                }
                else if (request.StartDate.HasValue && request.EndDate.HasValue)
                {
                    Console.WriteLine($"Loading data for date range: {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}");
                    var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
                    var filteredEntries = allEntries
                        .Where(e => e.Timestamp.Date >= request.StartDate.Value.Date && e.Timestamp.Date <= request.EndDate.Value.Date)
                        .OrderBy(e => e.Timestamp)
                        .ToList();
                    response.CurrentEntries = DataProcessor.ProcessData(filteredEntries, null, false, request.Smooth);
                }
                else
                {
                    Console.WriteLine($"Loading static data for {request.Days ?? 1} days");
                    response.CurrentEntries = LoadStaticData(request.Days, false, request.Smooth);
                }

                // Calculate statistics
                if (response.CurrentEntries.Any())
                {
                    response.Statistics = CalculateStatistics(response.CurrentEntries);
                }

                // Calculate comparison statistics if in compare mode
                if (request.Compare && response.TodayEntries != null && response.YesterdayEntries != null)
                {
                    response.Statistics.TodayStats = CalculateStatistics(response.TodayEntries);
                    response.Statistics.YesterdayStats = CalculateStatistics(response.YesterdayEntries);
                }

                // Find data gaps
                response.Gaps = FindDataGaps(response.CurrentEntries, false);
                if (request.Compare && response.YesterdayEntries != null)
                {
                    response.Gaps.AddRange(FindDataGaps(response.YesterdayEntries, true));
                }

                Console.WriteLine($"Successfully loaded {response.CurrentEntries?.Count ?? 0} entries");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetVoltageData: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Don't generate sample data on error - return empty response
                response.CurrentEntries = new List<LogEntry>();
                response.Statistics = new VoltageStatistics();
                response.Gaps = new List<DataGap>();
            }

            return response;
        }

        private static List<LogEntry> LoadLiveData(int? smooth)
        {
            var cutoffTime = DateTime.Now.AddHours(-3);
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            var lastHourEntries = allEntries.Where(e => e.Timestamp >= cutoffTime).ToList();
            return DataProcessor.ProcessData(lastHourEntries, null, false, smooth);
        }

        private static List<LogEntry> LoadStaticData(int? days, bool today, int? smooth)
        {
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            return DataProcessor.ProcessData(allEntries, days, today, smooth);
        }

        private static List<LogEntry> LoadTodayData(int? smooth)
        {
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            return DataProcessor.ProcessData(allEntries, null, true, smooth);
        }

        private static List<LogEntry> LoadYesterdayData(int? smooth)
        {
            var allEntries = LogReader.ReadLogData(Constants.DataFilePath, Constants.VOLTAGE_COLUMN - 1);
            
            // Filter for yesterday's data
            var yesterday = DateTime.Now.Date.AddDays(-1);
            var yesterdayEntries = allEntries
                .Where(e => e.Timestamp.Date == yesterday)
                .OrderBy(e => e.Timestamp)
                .ToList();
            
            // Apply smoothing if requested
            if (smooth.HasValue && smooth.Value > 0 && yesterdayEntries.Count > smooth.Value)
            {
                var smoothedVoltages = new double[yesterdayEntries.Count];
                for (int i = 0; i < yesterdayEntries.Count; i++)
                {
                    var window = yesterdayEntries.Skip(Math.Max(0, i - smooth.Value / 2)).Take(smooth.Value).ToList();
                    smoothedVoltages[i] = window.Average(e => e.Voltage);
                }
                for (int i = 0; i < yesterdayEntries.Count; i++)
                    yesterdayEntries[i].Voltage = smoothedVoltages[i];
            }
            
            return yesterdayEntries;
        }

        private static VoltageStatistics CalculateStatistics(List<LogEntry> entries)
        {
            if (!entries.Any())
                return new VoltageStatistics();

            var voltages = entries.Select(e => e.Voltage).ToArray();
            
            var maxVoltage = voltages.Max();
            var minVoltage = voltages.Min();
            var avgVoltage = voltages.Average();
            var lastVoltage = voltages.Last();
            var voltageRange = maxVoltage - minVoltage;

            var withinStandard = voltages.Count(v => Math.Abs(v - Constants.NominalVoltage) <= Constants.VoltageTolerance);
            var compliancePercentage = (double)withinStandard / voltages.Length * 100;

            // Calculate hours above and below 230V
            var aboveStandardCount = voltages.Count(v => v > Constants.NominalVoltage);
            var belowStandardCount = voltages.Count(v => v < Constants.NominalVoltage);
            
            double hoursAbove230 = 0;
            double hoursBelow230 = 0;

            if (entries.Count > 1)
            {
                var timeSpan = entries.Last().Timestamp - entries.First().Timestamp;
                var totalHours = timeSpan.TotalHours;
                var avgIntervalHours = totalHours / (entries.Count - 1);
                hoursAbove230 = aboveStandardCount * avgIntervalHours;
                hoursBelow230 = belowStandardCount * avgIntervalHours;
            }

            return new VoltageStatistics
            {
                MaxVoltage = maxVoltage,
                MinVoltage = minVoltage,
                AvgVoltage = avgVoltage,
                LastVoltage = lastVoltage,
                VoltageRange = voltageRange,
                CompliancePercentage = compliancePercentage,
                HoursAbove230 = hoursAbove230,
                HoursBelow230 = hoursBelow230,
                TotalPoints = entries.Count
            };
        }

        private static List<DataGap> FindDataGaps(List<LogEntry> entries, bool isYesterday)
        {
            var gaps = new List<DataGap>();
            
            if (entries == null || entries.Count < 2) 
                return gaps;

            var sortedEntries = entries.OrderBy(e => e.Timestamp).ToList();
            
            for (int i = 1; i < sortedEntries.Count; i++)
            {
                var timeDiff = sortedEntries[i].Timestamp - sortedEntries[i - 1].Timestamp;
                
                if (timeDiff.TotalMinutes > Constants.GAP_THRESHOLD_MINUTES)
                {
                    var gap = new DataGap
                    {
                        StartTime = sortedEntries[i - 1].Timestamp,
                        EndTime = sortedEntries[i].Timestamp,
                        Duration = timeDiff,
                        IsYesterday = isYesterday,
                        FormattedDuration = FormatGapDuration(timeDiff)
                    };
                    gaps.Add(gap);
                }
            }
            
            return gaps;
        }

        private static string FormatGapDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{duration.TotalHours:F1}h";
            }
            else
            {
                return $"{duration.TotalMinutes:F0}m";
            }
        }
    }
}