using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using IpTvPlayer.Models;
using IpTvPlayer.Services.Parsing;
using Serilog;

namespace IpTvPlayer.Utilities;

public class PlaylistImporter
{
    private readonly M3UParser _parser;
    private readonly HttpClient _httpClient;

    public PlaylistImporter()
    {
        _parser = new M3UParser();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<(bool Success, List<Channel> Channels, string Message)> ImportFromUrlAsync(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return (false, new List<Channel>(), "URL не может быть пустым");

            var uri = new Uri(url);
            var response = await _httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
                return (false, new List<Channel>(), $"Ошибка загрузки: {response.StatusCode}");

            var content = await response.Content.ReadAsStringAsync();
            var channels = _parser.ParseM3U(content);

            if (channels.Count == 0)
                return (false, new List<Channel>(), "В плейлисте нет каналов");

            Log.Information("Successfully imported {ChannelCount} channels from {Url}", channels.Count, url);
            return (true, channels, $"Загружено {channels.Count} каналов");
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "HTTP error importing from {Url}", url);
            return (false, new List<Channel>(), "Ошибка подключения к серверу");
        }
        catch (UriFormatException)
        {
            return (false, new List<Channel>(), "Неверный формат URL");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error importing from {Url}", url);
            return (false, new List<Channel>(), $"Ошибка: {ex.Message}");
        }
    }

    public async Task<(bool Success, List<Channel> Channels, string Message)> ImportFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, new List<Channel>(), "Файл не найден");

            var content = await File.ReadAllTextAsync(filePath);
            var channels = _parser.ParseM3U(content);

            if (channels.Count == 0)
                return (false, new List<Channel>(), "В файле нет каналов");

            Log.Information("Successfully imported {ChannelCount} channels from {FilePath}", channels.Count, filePath);
            return (true, channels, $"Загружено {channels.Count} каналов");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error importing from {FilePath}", filePath);
            return (false, new List<Channel>(), $"Ошибка: {ex.Message}");
        }
    }
}
