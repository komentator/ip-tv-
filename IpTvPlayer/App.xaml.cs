using System.Windows;
using Serilog;

namespace IpTvPlayer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        InitializeLogging();
    }

    private void InitializeLogging()
    {
        var logPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Logs",
            "IpTvPlayer-.txt"
        );

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        Log.Information("IP TV Player started");
    }
}
