using Serilog;

namespace IpTvPlayer.Utilities;

public static class AppLogger
{
    public static void Initialize(string logDir = "Logs")
    {
        var fullLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logDir);
        if (!Directory.Exists(fullLogDir))
            Directory.CreateDirectory(fullLogDir);

        var logPath = Path.Combine(fullLogDir, "app-.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                fileSizeLimitBytes: 104857600,
                retainedFileCountLimit: 30
            )
            .CreateLogger();
    }

    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
