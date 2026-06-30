namespace ApcUpsLogParser.Common.Configuration
{
    public static class Constants
    {
        public const int VOLTAGE_COLUMN = 3;
        public const string DataFilePathEnvironmentVariable = "APC_UPS_LOG_PATH";
        public static string DataFilePath => Environment.GetEnvironmentVariable(DataFilePathEnvironmentVariable) ?? Path.Combine(AppContext.BaseDirectory, "DataLog");
        public const double NominalVoltage = 230.0;
        public const double VoltageTolerance = 23.0;
        public const double GAP_THRESHOLD_MINUTES = 5.0;
    }
}