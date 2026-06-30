using System.Text.RegularExpressions;
using IpTvPlayer.Models;
using Serilog;

namespace IpTvPlayer.Services.Parsing;

public class M3UParser
{
    public List<Channel> ParseM3U(string content)
    {
        var channels = new List<Channel>();
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        if (lines.Length == 0 || !lines[0].StartsWith("#EXTM3U"))
        {
            Log.Warning("Invalid M3U file format");
            return channels;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (!line.StartsWith("#EXTINF"))
                continue;

            var extinfLine = line;
            var streamUrl = i + 1 < lines.Length ? lines[i + 1].Trim() : "";

            if (string.IsNullOrEmpty(streamUrl) || streamUrl.StartsWith("#"))
            {
                continue;
            }

            var channel = ParseExtinf(extinfLine, streamUrl);
            if (channel != null && !string.IsNullOrWhiteSpace(channel.Url))
            {
                channels.Add(channel);
            }
        }

        return DeduplicateChannels(channels);
    }

    public async Task<List<Channel>> ParseM3UFromUrlAsync(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return ParseM3U(content);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error parsing M3U from URL: {Url}", url);
            return new List<Channel>();
        }
    }

    private Channel? ParseExtinf(string extinfLine, string streamUrl)
    {
        try
        {
            var tvgIdMatch = Regex.Match(extinfLine, @"tvg-id=""([^""]*)""");
            var tvgNameMatch = Regex.Match(extinfLine, @"tvg-name=""([^""]*)""");
            var tvgLogoMatch = Regex.Match(extinfLine, @"tvg-logo=""([^""]*)""");
            var groupMatch = Regex.Match(extinfLine, @"group-title=""([^""]*)""");

            var channelNameMatch = Regex.Match(extinfLine, @",\s*(.+?)\s*$");
            var channelName = channelNameMatch.Success ? channelNameMatch.Groups[1].Value : "Unknown";

            return new Channel
            {
                TvgId = tvgIdMatch.Success ? tvgIdMatch.Groups[1].Value : "",
                Name = channelName,
                LogoUrl = tvgLogoMatch.Success ? tvgLogoMatch.Groups[1].Value : null,
                GroupTitle = groupMatch.Success ? groupMatch.Groups[1].Value : "Без группы",
                Url = streamUrl.Trim()
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error parsing EXTINF line");
            return null;
        }
    }

    private List<Channel> DeduplicateChannels(List<Channel> channels)
    {
        return channels
            .GroupBy(c => c.Url)
            .Select(g => g.First())
            .ToList();
    }
}
