using System.Globalization;
using ApcUpsLogParser.Common.Models;

namespace ApcUpsLogParser.Common.Services
{
    public class LogReader
    {
        public static List<LogEntry> ReadLogData(string filePath, int voltageColumnIndex)
        {
            var logEntries = new List<LogEntry>();
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found at {filePath}");
                return logEntries;
            }

            Console.WriteLine($"Reading data from: {filePath}");

            int rowCount = 0;
            int successfullyParsed = 0;
            int failuresToLog = 5;

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            
            if (!reader.EndOfStream) reader.ReadLine();

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                rowCount++;
                if (string.IsNullOrWhiteSpace(line) || line.Length < 19) continue;

                var timestampString = line.Substring(0, 19);
                var dataString = line.Substring(19).Trim();
                var dataParts = dataString.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (!DateTime.TryParseExact(timestampString, "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                {
                    if (failuresToLog > 0)
                    {
                        Console.WriteLine($"Warning: Could not parse timestamp on row {rowCount}. Value: '{timestampString}'. Expected format: 'MM/dd/yyyy HH:mm:ss' (input file format)");
                        failuresToLog--;
                    }
                    continue;
                }

                if (dataParts.Length <= voltageColumnIndex)
                {
                    if (failuresToLog > 0)
                    {
                        Console.WriteLine($"Warning: Not enough data columns on row {rowCount} to get voltage from data column {voltageColumnIndex + 1}. Line: '{line}'");
                        failuresToLog--;
                    }
                    continue;
                }

                var voltageString = dataParts[voltageColumnIndex];

                if (!double.TryParse(voltageString, out var voltage))
                {
                    if (failuresToLog > 0)
                    {
                        Console.WriteLine($"Warning: Could not parse voltage on row {rowCount} from data column {voltageColumnIndex + 1}. Value: '{voltageString}'.");
                        failuresToLog--;
                    }
                    continue;
                }

                logEntries.Add(new LogEntry { Timestamp = timestamp, Voltage = voltage });
                successfullyParsed++;
            }

            Console.WriteLine($"Finished reading file. Total data rows processed: {rowCount}. Successfully parsed entries: {successfullyParsed}.");
            if (rowCount > 0 && successfullyParsed == 0)
            {
                Console.WriteLine("Troubleshooting: No entries were parsed. Please check the log file format.");
                Console.WriteLine("1. Ensure the date in the first 19 characters is in 'MM/dd/yyyy HH:mm:ss' format (input file format).");
                Console.WriteLine($"2. Ensure the voltage in data column {voltageColumnIndex + 1} is a valid number.");
            }
            return logEntries;
        }
    }
}