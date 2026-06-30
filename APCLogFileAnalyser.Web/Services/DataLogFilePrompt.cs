using ApcUpsLogParser.Common.Configuration;

namespace ApcUpsLogParser.Web.Services;

internal static class DataLogFilePrompt
{
    private const string DataFilePathConfigurationKey = "DataFilePath";

    public static void EnsureDataLogFileExists(IConfiguration configuration, string environmentName)
    {
        var configuredPath = configuration[DataFilePathConfigurationKey];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            Environment.SetEnvironmentVariable(Constants.DataFilePathEnvironmentVariable, configuredPath);
        }

        var filePath = Constants.DataFilePath;
        if (File.Exists(filePath))
        {
            return;
        }

        Console.WriteLine($"Configured APC UPS log file was not found: {filePath}");
        Console.WriteLine($"Set '{DataFilePathConfigurationKey}' in user secrets, or in the appsettings file for the {environmentName} environment, to the full APC UPS log file path.");
    }
}
