using IpTvPlayer.Models;
using IpTvPlayer.Services.Parsing;
using IpTvPlayer.Services.Storage;
using Serilog;

namespace IpTvPlayer.Services.Epg;

public class EpgService
{
    private readonly StorageService _storage;
    private readonly XmlTvParser _parser;
    private static readonly TimeSpan RefreshTtl = TimeSpan.FromHours(6);

    public EpgService(StorageService storage)
    {
        _storage = storage;
        _parser = new XmlTvParser();
    }

    public async Task RefreshFromUrlAsync(string url, bool force = false)
    {
        if (!force)
        {
            var last = await _storage.GetEpgLastFetchAsync(url);
            if (last.HasValue && DateTime.Now - last.Value < RefreshTtl)
            {
                Log.Information("EPG fresh enough, skipping fetch: {Url}", url);
                return;
            }
        }

        var programs = await _parser.ParseFromUrlAsync(url);
        if (programs.Count == 0)
        {
            Log.Warning("No EPG programs parsed from {Url}", url);
            return;
        }

        await _storage.SaveEpgProgramsAsync(programs);
        await _storage.MarkEpgFetchedAsync(url);
        await _storage.CleanupOldEpgAsync(DateTime.Now.AddDays(-1));
    }

    public Task<EpgProgram?> GetCurrentAsync(Channel channel) =>
        _storage.GetCurrentProgramAsync(channel.TvgId ?? "");

    public Task<List<EpgProgram>> GetScheduleAsync(Channel channel, DateTime from, DateTime to) =>
        _storage.GetProgramsForChannelAsync(channel.TvgId ?? "", from, to);
}
