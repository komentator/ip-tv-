using System.Data.SQLite;
using System.Text.Json;
using IpTvPlayer.Models;
using Serilog;

namespace IpTvPlayer.Services.Storage;

public class StorageService
{
    private readonly string _connectionString;
    private const string DbPath = "Data/IpTvPlayer.db";

    public StorageService()
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
            Directory.CreateDirectory(dataDir);

        var dbFullPath = Path.Combine(dataDir, "IpTvPlayer.db");
        _connectionString = $"Data Source={dbFullPath};Version=3;";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var commands = new[]
            {
                @"CREATE TABLE IF NOT EXISTS Playlists (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Source TEXT,
                    CreatedDate TEXT NOT NULL,
                    UpdatedDate TEXT,
                    IsFavorite INTEGER DEFAULT 0
                )",
                @"CREATE TABLE IF NOT EXISTS Channels (
                    Id TEXT PRIMARY KEY,
                    PlaylistId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Url TEXT NOT NULL,
                    LogoUrl TEXT,
                    GroupTitle TEXT,
                    TvgId TEXT,
                    AddedDate TEXT NOT NULL,
                    LastWatchedDate TEXT,
                    FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id)
                )",
                @"CREATE TABLE IF NOT EXISTS WatchHistory (
                    Id TEXT PRIMARY KEY,
                    ChannelId TEXT NOT NULL,
                    WatchedDate TEXT NOT NULL,
                    FOREIGN KEY (ChannelId) REFERENCES Channels(Id)
                )"
            };

            foreach (var cmd in commands)
            {
                using var command = new SQLiteCommand(cmd, connection);
                command.ExecuteNonQuery();
            }

            Log.Information("Database initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error initializing database");
        }
    }

    public async Task SavePlaylistAsync(Playlist playlist)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                const string sql = @"INSERT OR REPLACE INTO Playlists (Id, Name, Source, CreatedDate, UpdatedDate, IsFavorite)
                    VALUES (@Id, @Name, @Source, @CreatedDate, @UpdatedDate, @IsFavorite)";

                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@Id", playlist.Id);
                command.Parameters.AddWithValue("@Name", playlist.Name);
                command.Parameters.AddWithValue("@Source", playlist.Source ?? "");
                command.Parameters.AddWithValue("@CreatedDate", playlist.CreatedDate.ToString("O"));
                command.Parameters.AddWithValue("@UpdatedDate", playlist.UpdatedDate?.ToString("O") ?? "");
                command.Parameters.AddWithValue("@IsFavorite", playlist.IsFavorite ? 1 : 0);

                await command.ExecuteNonQueryAsync();

                foreach (var channel in playlist.Channels)
                {
                    await SaveChannelAsync(channel, playlist.Id, connection);
                }

                transaction.Commit();
                Log.Information("Playlist saved: {PlaylistName}", playlist.Name);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving playlist");
        }
    }

    private async Task SaveChannelAsync(Channel channel, string playlistId, SQLiteConnection connection)
    {
        const string sql = @"INSERT OR REPLACE INTO Channels (Id, PlaylistId, Name, Url, LogoUrl, GroupTitle, TvgId, AddedDate, LastWatchedDate)
            VALUES (@Id, @PlaylistId, @Name, @Url, @LogoUrl, @GroupTitle, @TvgId, @AddedDate, @LastWatchedDate)";

        using var command = new SQLiteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", channel.Id);
        command.Parameters.AddWithValue("@PlaylistId", playlistId);
        command.Parameters.AddWithValue("@Name", channel.Name);
        command.Parameters.AddWithValue("@Url", channel.Url);
        command.Parameters.AddWithValue("@LogoUrl", channel.LogoUrl ?? "");
        command.Parameters.AddWithValue("@GroupTitle", channel.GroupTitle ?? "");
        command.Parameters.AddWithValue("@TvgId", channel.TvgId ?? "");
        command.Parameters.AddWithValue("@AddedDate", channel.AddedDate.ToString("O"));
        command.Parameters.AddWithValue("@LastWatchedDate", channel.LastWatchedDate?.ToString("O") ?? "");

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Playlist>> LoadAllPlaylistsAsync()
    {
        var playlists = new List<Playlist>();

        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "SELECT Id, Name, Source, CreatedDate, UpdatedDate, IsFavorite FROM Playlists";
            using var command = new SQLiteCommand(sql, connection);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var playlistId = reader.GetString(0);
                var playlist = new Playlist
                {
                    Id = playlistId,
                    Name = reader.GetString(1),
                    Source = reader.GetString(2),
                    CreatedDate = DateTime.Parse(reader.GetString(3)),
                    UpdatedDate = string.IsNullOrEmpty(reader.GetString(4)) ? null : DateTime.Parse(reader.GetString(4)),
                    IsFavorite = reader.GetInt32(5) == 1
                };

                playlist.Channels = await LoadChannelsForPlaylistAsync(playlistId, connection);
                playlists.Add(playlist);
            }

            Log.Information("Loaded {PlaylistCount} playlists", playlists.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading playlists");
        }

        return playlists;
    }

    private async Task<List<Channel>> LoadChannelsForPlaylistAsync(string playlistId, SQLiteConnection connection)
    {
        var channels = new List<Channel>();

        const string sql = @"SELECT Id, Name, Url, LogoUrl, GroupTitle, TvgId, AddedDate, LastWatchedDate
            FROM Channels WHERE PlaylistId = @PlaylistId";

        using var command = new SQLiteCommand(sql, connection);
        command.Parameters.AddWithValue("@PlaylistId", playlistId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            channels.Add(new Channel
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Url = reader.GetString(2),
                LogoUrl = reader.GetString(3),
                GroupTitle = reader.GetString(4),
                TvgId = reader.GetString(5),
                AddedDate = DateTime.Parse(reader.GetString(6)),
                LastWatchedDate = string.IsNullOrEmpty(reader.GetString(7)) ? null : DateTime.Parse(reader.GetString(7))
            });
        }

        return channels;
    }

    public async Task DeletePlaylistAsync(string playlistId)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                const string deleteChannels = "DELETE FROM Channels WHERE PlaylistId = @PlaylistId";
                using (var command = new SQLiteCommand(deleteChannels, connection))
                {
                    command.Parameters.AddWithValue("@PlaylistId", playlistId);
                    await command.ExecuteNonQueryAsync();
                }

                const string deletePlaylist = "DELETE FROM Playlists WHERE Id = @Id";
                using (var command = new SQLiteCommand(deletePlaylist, connection))
                {
                    command.Parameters.AddWithValue("@Id", playlistId);
                    await command.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                Log.Information("Playlist deleted: {PlaylistId}", playlistId);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error deleting playlist");
        }
    }
}
