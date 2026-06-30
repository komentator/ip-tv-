using System.Text.Json;
using Serilog;

namespace IpTvPlayer.Utilities;

public class ConfigManager
{
    private readonly string _configPath;
    private Dictionary<string, JsonElement> _config;

    public ConfigManager()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(appDir, "config.json");
        _config = new Dictionary<string, JsonElement>();
        LoadConfig();
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    _config[prop.Name] = prop.Value;
                }
            }
            else
            {
                CreateDefaultConfig();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading config");
            CreateDefaultConfig();
        }
    }

    private void CreateDefaultConfig()
    {
        _config = new Dictionary<string, JsonElement>
        {
            { "DefaultVolume", JsonDocument.Parse("100").RootElement },
            { "WindowWidth", JsonDocument.Parse("1280").RootElement },
            { "WindowHeight", JsonDocument.Parse("720").RootElement },
            { "Theme", JsonDocument.Parse("\"Dark\"").RootElement }
        };

        SaveConfig();
    }

    public string GetString(string key, string defaultValue = "")
    {
        if (_config.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? defaultValue;
        return defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        if (_config.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number)
            return value.GetInt32();
        return defaultValue;
    }

    public void SetValue(string key, object value)
    {
        var jsonValue = JsonSerializer.SerializeToElement(value);
        _config[key] = jsonValue;
        SaveConfig();
    }

    private void SaveConfig()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving config");
        }
    }

    private static ConfigManager? _shared;
    private static ConfigManager Shared => _shared ??= new ConfigManager();

    public static AppConfig Load()
    {
        var c = Shared;
        return new AppConfig
        {
            EpgUrl = c.GetString("EpgUrl"),
            DefaultVolume = c.GetInt("DefaultVolume", 100),
            WindowWidth = c.GetInt("WindowWidth", 1280),
            WindowHeight = c.GetInt("WindowHeight", 720),
            WindowLeft = c.GetInt("WindowLeft", -1),
            WindowTop = c.GetInt("WindowTop", -1),
            WindowMaximized = c.GetInt("WindowMaximized") == 1,
            Theme = c.GetString("Theme", "Dark"),
            SnapshotDir = c.GetString("SnapshotDir"),
            RecordingDir = c.GetString("RecordingDir"),
            LastChannelId = c.GetString("LastChannelId"),
        };
    }

    public static void Save(AppConfig cfg)
    {
        var c = Shared;
        c.SetValue("EpgUrl", cfg.EpgUrl ?? "");
        c.SetValue("DefaultVolume", cfg.DefaultVolume);
        c.SetValue("WindowWidth", cfg.WindowWidth);
        c.SetValue("WindowHeight", cfg.WindowHeight);
        c.SetValue("WindowLeft", cfg.WindowLeft);
        c.SetValue("WindowTop", cfg.WindowTop);
        c.SetValue("WindowMaximized", cfg.WindowMaximized ? 1 : 0);
        c.SetValue("Theme", cfg.Theme);
        c.SetValue("SnapshotDir", cfg.SnapshotDir ?? "");
        c.SetValue("RecordingDir", cfg.RecordingDir ?? "");
        c.SetValue("LastChannelId", cfg.LastChannelId ?? "");
    }
}

public class AppConfig
{
    public string? EpgUrl { get; set; }
    public int DefaultVolume { get; set; } = 100;
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;
    public int WindowLeft { get; set; } = -1;
    public int WindowTop { get; set; } = -1;
    public bool WindowMaximized { get; set; }
    public string Theme { get; set; } = "Dark";
    public string? SnapshotDir { get; set; }
    public string? RecordingDir { get; set; }
    public string? LastChannelId { get; set; }
}
