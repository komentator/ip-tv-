using LibVLCSharp.Shared;
using IpTvPlayer.Models;
using Serilog;

namespace IpTvPlayer.Services.Playback;

public class PlaybackService : IDisposable
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Channel? _currentChannel;

    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
    public event EventHandler<float>? Buffering;
    public event EventHandler<string>? StreamInfoChanged;

    public PlaybackService()
    {
        try
        {
            Core.Initialize();
            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);

            _mediaPlayer.EncounteredError += (s, e) =>
            {
                Log.Error("Playback error encountered");
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { IsPlaying = false, Error = "Ошибка воспроизведения" });
            };

            _mediaPlayer.EndReached += (s, e) =>
            {
                Log.Information("Playback ended");
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { IsPlaying = false });
            };

            _mediaPlayer.Buffering += (s, e) =>
            {
                Buffering?.Invoke(this, e.Cache);
            };

            _mediaPlayer.Playing += (s, e) =>
            {
                Buffering?.Invoke(this, 100f);
                Task.Run(async () =>
                {
                    await Task.Delay(800);
                    try { StreamInfoChanged?.Invoke(this, BuildStreamInfo()); } catch { }
                });
            };

            Log.Information("PlaybackService initialized");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing PlaybackService");
        }
    }

    public void Play(Channel channel)
    {
        try
        {
            if (_mediaPlayer == null)
                throw new InvalidOperationException("Media player not initialized");

            _currentChannel = channel;
            var media = new Media(_libVlc, channel.Url, FromType.FromLocation);
            _mediaPlayer.Media = media;
            _mediaPlayer.Play();

            Log.Information("Playing channel: {ChannelName}", channel.Name);
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { IsPlaying = true, Channel = channel });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error playing channel: {ChannelName}", channel.Name);
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { IsPlaying = false, Error = ex.Message });
        }
    }

    public void Pause()
    {
        try
        {
            _mediaPlayer?.Pause();
            Log.Information("Playback paused");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error pausing playback");
        }
    }

    public void Resume()
    {
        try
        {
            _mediaPlayer?.Play();
            Log.Information("Playback resumed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error resuming playback");
        }
    }

    public void Stop()
    {
        try
        {
            _mediaPlayer?.Stop();
            _currentChannel = null;
            Log.Information("Playback stopped");
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs { IsPlaying = false });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping playback");
        }
    }

    public void SetVolume(int volume)
    {
        try
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting volume");
        }
    }

    public Channel? GetCurrentChannel() => _currentChannel;

    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;

    private string BuildStreamInfo()
    {
        if (_mediaPlayer == null) return "";
        try
        {
            var media = _mediaPlayer.Media;
            if (media == null) return "";

            var parts = new List<string>();

            uint width = 0, height = 0;
            if (_mediaPlayer.Size(0, ref width, ref height) && width > 0 && height > 0)
            {
                parts.Add($"{width}x{height}");
            }

            var fps = _mediaPlayer.Fps;
            if (fps > 0.1f) parts.Add($"{fps:0.#} fps");

            foreach (var t in media.Tracks)
            {
                if (t.TrackType == TrackType.Audio)
                {
                    var lang = string.IsNullOrEmpty(t.Language) ? "" : $" {t.Language}";
                    parts.Add($"🔊{lang} {t.Data.Audio.Channels}ch");
                    break;
                }
            }

            return string.Join("  •  ", parts);
        }
        catch
        {
            return "";
        }
    }

    public void Dispose()
    {
        try
        {
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disposing PlaybackService");
        }
    }
}

public class PlaybackStateChangedEventArgs
{
    public bool IsPlaying { get; set; }
    public Channel? Channel { get; set; }
    public string? Error { get; set; }
}
