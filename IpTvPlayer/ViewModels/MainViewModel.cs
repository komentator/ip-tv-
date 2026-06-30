using System.Collections.ObjectModel;
using IpTvPlayer.Models;
using IpTvPlayer.Services.Parsing;
using IpTvPlayer.Services.Playback;
using IpTvPlayer.Services.Storage;
using LibVLCSharp.Shared;
using Serilog;

namespace IpTvPlayer.ViewModels;

public class MainViewModel : IDisposable
{
    private readonly StorageService _storageService;
    private readonly M3UParser _parser;
    private readonly PlaybackService _playbackService;

    public ObservableCollection<Playlist> Playlists { get; }
    public ObservableCollection<Channel> CurrentChannels { get; }
    public ObservableCollection<string> Groups { get; }

    public MediaPlayer? MediaPlayer => _playbackService.MediaPlayer;

    private Playlist? _selectedPlaylist;
    private Channel? _selectedChannel;
    private string _searchText = "";
    private string _selectedGroup = "";

    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    public MainViewModel()
    {
        _storageService = new StorageService();
        _parser = new M3UParser();
        _playbackService = new PlaybackService();

        Playlists = new ObservableCollection<Playlist>();
        CurrentChannels = new ObservableCollection<Channel>();
        Groups = new ObservableCollection<string>();

        _playbackService.PlaybackStateChanged += (s, e) =>
        {
            PlaybackStateChanged?.Invoke(this, e);
        };

        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        try
        {
            var playlists = await _storageService.LoadAllPlaylistsAsync();
            foreach (var pl in playlists)
            {
                Playlists.Add(pl);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing MainViewModel");
        }
    }

    public async Task ImportPlaylistFromUrlAsync(string url, string playlistName)
    {
        try
        {
            var channels = await _parser.ParseM3UFromUrlAsync(url);
            if (channels.Count == 0)
            {
                Log.Warning("No channels parsed from URL: {Url}", url);
                return;
            }

            var playlist = new Playlist
            {
                Name = playlistName,
                Source = url,
                Channels = channels
            };

            Playlists.Add(playlist);
            await _storageService.SavePlaylistAsync(playlist);
            Log.Information("Playlist imported from URL: {Url}", url);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error importing playlist from URL");
        }
    }

    public async Task ImportPlaylistFromFileAsync(string filePath, string playlistName)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var channels = _parser.ParseM3U(content);

            if (channels.Count == 0)
            {
                Log.Warning("No channels parsed from file: {FilePath}", filePath);
                return;
            }

            var playlist = new Playlist
            {
                Name = playlistName,
                Source = filePath,
                Channels = channels
            };

            Playlists.Add(playlist);
            await _storageService.SavePlaylistAsync(playlist);
            Log.Information("Playlist imported from file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error importing playlist from file");
        }
    }

    public void SelectPlaylist(Playlist playlist)
    {
        _selectedPlaylist = playlist;
        UpdateChannelsList();
        UpdateGroups();
    }

    public void SelectChannel(Channel channel)
    {
        _selectedChannel = channel;
        _playbackService.Play(channel);
        channel.LastWatchedDate = DateTime.Now;
    }

    public void SearchChannels(string searchText)
    {
        _searchText = searchText;
        UpdateChannelsList();
    }

    public void FilterByGroup(string group)
    {
        _selectedGroup = group;
        UpdateChannelsList();
    }

    private void UpdateChannelsList()
    {
        CurrentChannels.Clear();

        if (_selectedPlaylist == null)
            return;

        var channels = _selectedPlaylist.Channels.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_selectedGroup) && _selectedGroup != "Все")
            channels = channels.Where(c => c.GroupTitle == _selectedGroup);

        if (!string.IsNullOrWhiteSpace(_searchText))
            channels = channels.Where(c => c.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var channel in channels)
        {
            CurrentChannels.Add(channel);
        }
    }

    private void UpdateGroups()
    {
        Groups.Clear();
        Groups.Add("Все");

        if (_selectedPlaylist == null)
            return;

        var groups = _selectedPlaylist.Channels
            .Select(c => c.GroupTitle ?? "Без группы")
            .Distinct()
            .OrderBy(g => g);

        foreach (var group in groups)
        {
            Groups.Add(group);
        }
    }

    public void SetVolume(int volume)
    {
        _playbackService.SetVolume(volume);
    }

    public void PlayPause()
    {
        if (_playbackService.IsPlaying)
            _playbackService.Pause();
        else
            _playbackService.Resume();
    }

    public void Stop()
    {
        _playbackService.Stop();
    }

    public async Task DeletePlaylistAsync(string playlistId)
    {
        var playlist = Playlists.FirstOrDefault(p => p.Id == playlistId);
        if (playlist != null)
        {
            Playlists.Remove(playlist);
            await _storageService.DeletePlaylistAsync(playlistId);
        }
    }

    public void Dispose()
    {
        _playbackService?.Dispose();
    }
}
