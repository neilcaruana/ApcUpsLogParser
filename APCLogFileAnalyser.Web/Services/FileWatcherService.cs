using ApcUpsLogParser.Common.Configuration;
using ApcUpsLogParser.Common.Services;
using ApcUpsLogParser.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ApcUpsLogParser.Web.Services;

public class FileWatcherService : BackgroundService
{
    private readonly IHubContext<VoltageHub> _hub;
    private readonly ILogger<FileWatcherService> _logger;
    private DateTime _lastModified = DateTime.MinValue;

    public FileWatcherService(IHubContext<VoltageHub> hub, ILogger<FileWatcherService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var filePath = Constants.DataFilePath;
                if (File.Exists(filePath))
                {
                    var lastWrite = File.GetLastWriteTimeUtc(filePath);
                    if (lastWrite > _lastModified)
                    {
                        _lastModified = lastWrite;
                        var entries = LogReader.ReadLogData(filePath, Constants.VOLTAGE_COLUMN - 1);
                        var latest = entries.OrderByDescending(e => e.Timestamp).Take(1).FirstOrDefault();
                        if (latest != null)
                        {
                            await _hub.Clients.All.SendAsync("NewReading", new
                            {
                                timestamp = latest.Timestamp,
                                voltage = latest.Voltage
                            }, stoppingToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "File watcher error");
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}
