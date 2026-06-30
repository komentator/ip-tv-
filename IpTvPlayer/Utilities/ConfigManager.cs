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
}
