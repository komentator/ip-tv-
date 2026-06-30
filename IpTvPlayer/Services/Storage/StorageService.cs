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

    private static void EnsureColumn(SQLiteConnection conn, string table, string column, string definition)
    {
        using var pragma = new SQLiteCommand($"PRAGMA table_info({table})", conn);
        using var reader = pragma.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }
        using var alter = new SQLiteCommand($"ALTER TABLE {table} ADD COLUMN {column} {definition}", conn);
        alter.ExecuteNonQuery();
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
                    IsFavorite INTEGER DEFAULT 0,
                    FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id)
                )",
                @"CREATE TABLE IF NOT EXISTS WatchHistory (
                    Id TEXT PRIMARY KEY,
                    ChannelId TEXT NOT NULL,
                    WatchedDate TEXT NOT NULL,
                    FOREIGN KEY (ChannelId) REFERENCES Channels(Id)
                )",
                @"CREATE TABLE IF NOT EXISTS EpgPrograms (
                    Id TEXT PRIMARY KEY,
                    ChannelTvgId TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    Category TEXT,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT NOT NULL
                )",
                @"CREATE INDEX IF NOT EXISTS IX_Epg_ChannelTvgId ON EpgPrograms(ChannelTvgId)",
                @"CREATE INDEX IF NOT EXISTS IX_Epg_StartTime ON EpgPrograms(StartTime)",
                @"CREATE TABLE IF NOT EXISTS EpgSource (
                    Url TEXT PRIMARY KEY,
                    LastFetched TEXT NOT NULL
                )"
            };

            foreach (var cmd in commands)
            {
                using var command = new SQLiteCommand(cmd, connection);
                command.ExecuteNonQuery();
            }

            EnsureColumn(connection, "Channels", "IsFavorite", "INTEGER DEFAULT 0");

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
        const string sql = @"INSERT OR REPLACE INTO Channels (Id, PlaylistId, Name, Url, LogoUrl, GroupTitle, TvgId, AddedDate, LastWatchedDate, IsFavorite)
            VALUES (@Id, @PlaylistId, @Name, @Url, @LogoUrl, @GroupTitle, @TvgId, @AddedDate, @LastWatchedDate, @IsFavorite)";

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
        command.Parameters.AddWithValue("@IsFavorite", channel.IsFavorite ? 1 : 0);

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

        const string sql = @"SELECT Id, Name, Url, LogoUrl, GroupTitle, TvgId, AddedDate, LastWatchedDate, IsFavorite
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
                LastWatchedDate = string.IsNullOrEmpty(reader.GetString(7)) ? null : DateTime.Parse(reader.GetString(7)),
                IsFavorite = !reader.IsDBNull(8) && reader.GetInt32(8) == 1
            });
        }

        return channels;
    }

    public async Task<Channel?> GetChannelByIdAsync(string channelId)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"SELECT Id, Name, Url, LogoUrl, GroupTitle, TvgId, AddedDate, LastWatchedDate, IsFavorite
                FROM Channels WHERE Id = @Id LIMIT 1";

            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", channelId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Channel
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Url = reader.GetString(2),
                    LogoUrl = reader.GetString(3),
                    GroupTitle = reader.GetString(4),
                    TvgId = reader.GetString(5),
                    AddedDate = DateTime.Parse(reader.GetString(6)),
                    LastWatchedDate = string.IsNullOrEmpty(reader.GetString(7)) ? null : DateTime.Parse(reader.GetString(7)),
                    IsFavorite = !reader.IsDBNull(8) && reader.GetInt32(8) == 1
                };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting channel by id");
        }
        return null;
    }

    public async Task SaveEpgProgramsAsync(IEnumerable<EpgProgram> programs)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();
            try
            {
                const string sql = @"INSERT OR REPLACE INTO EpgPrograms (Id, ChannelTvgId, Title, Description, Category, StartTime, EndTime)
                    VALUES (@Id, @ChannelTvgId, @Title, @Description, @Category, @StartTime, @EndTime)";

                int count = 0;
                foreach (var p in programs)
                {
                    using var cmd = new SQLiteCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Id", p.Id);
                    cmd.Parameters.AddWithValue("@ChannelTvgId", p.ChannelTvgId);
                    cmd.Parameters.AddWithValue("@Title", p.Title);
                    cmd.Parameters.AddWithValue("@Description", p.Description ?? "");
                    cmd.Parameters.AddWithValue("@Category", p.Category ?? "");
                    cmd.Parameters.AddWithValue("@StartTime", p.StartTime.ToString("O"));
                    cmd.Parameters.AddWithValue("@EndTime", p.EndTime.ToString("O"));
                    await cmd.ExecuteNonQueryAsync();
                    count++;
                }

                transaction.Commit();
                Log.Information("EPG saved: {Count} programs", count);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving EPG");
        }
    }

    public async Task<EpgProgram?> GetCurrentProgramAsync(string channelTvgId)
    {
        if (string.IsNullOrWhiteSpace(channelTvgId)) return null;

        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"SELECT Id, Title, Description, Category, StartTime, EndTime
                FROM EpgPrograms
                WHERE ChannelTvgId = @ChannelTvgId AND StartTime <= @Now AND EndTime > @Now
                LIMIT 1";

            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ChannelTvgId", channelTvgId);
            cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("O"));

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new EpgProgram
                {
                    Id = reader.GetString(0),
                    ChannelTvgId = channelTvgId,
                    Title = reader.GetString(1),
                    Description = reader.GetString(2),
                    Category = reader.GetString(3),
                    StartTime = DateTime.Parse(reader.GetString(4)),
                    EndTime = DateTime.Parse(reader.GetString(5))
                };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading current EPG program");
        }

        return null;
    }

    public async Task<List<EpgProgram>> GetProgramsForChannelAsync(string channelTvgId, DateTime from, DateTime to)
    {
        var programs = new List<EpgProgram>();
        if (string.IsNullOrWhiteSpace(channelTvgId)) return programs;

        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"SELECT Id, Title, Description, Category, StartTime, EndTime
                FROM EpgPrograms
                WHERE ChannelTvgId = @ChannelTvgId AND EndTime > @From AND StartTime < @To
                ORDER BY StartTime";

            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ChannelTvgId", channelTvgId);
            cmd.Parameters.AddWithValue("@From", from.ToString("O"));
            cmd.Parameters.AddWithValue("@To", to.ToString("O"));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                programs.Add(new EpgProgram
                {
                    Id = reader.GetString(0),
                    ChannelTvgId = channelTvgId,
                    Title = reader.GetString(1),
                    Description = reader.GetString(2),
                    Category = reader.GetString(3),
                    StartTime = DateTime.Parse(reader.GetString(4)),
                    EndTime = DateTime.Parse(reader.GetString(5))
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading EPG range");
        }

        return programs;
    }

    public async Task CleanupOldEpgAsync(DateTime cutoff)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "DELETE FROM EpgPrograms WHERE EndTime < @Cutoff";
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Cutoff", cutoff.ToString("O"));
            var removed = await cmd.ExecuteNonQueryAsync();
            if (removed > 0)
                Log.Information("EPG cleanup: removed {Count} old programs", removed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning EPG");
        }
    }

    public async Task<DateTime?> GetEpgLastFetchAsync(string sourceUrl)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "SELECT LastFetched FROM EpgSource WHERE Url = @Url";
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Url", sourceUrl);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return null;
            return DateTime.Parse((string)result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading EPG source");
            return null;
        }
    }

    public async Task MarkEpgFetchedAsync(string sourceUrl)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "INSERT OR REPLACE INTO EpgSource (Url, LastFetched) VALUES (@Url, @Now)";
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Url", sourceUrl);
            cmd.Parameters.AddWithValue("@Now", DateTime.Now.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error marking EPG fetched");
        }
    }

    public async Task AddWatchHistoryAsync(string channelId)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "INSERT INTO WatchHistory (Id, ChannelId, WatchedDate) VALUES (@Id, @ChannelId, @Date)";
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@ChannelId", channelId);
            cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding watch history");
        }
    }

    public async Task<List<(string ChannelId, DateTime WatchedDate)>> GetRecentWatchHistoryAsync(int limit = 30)
    {
        var result = new List<(string, DateTime)>();
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"SELECT ChannelId, MAX(WatchedDate) as LastWatched
                FROM WatchHistory
                GROUP BY ChannelId
                ORDER BY LastWatched DESC
                LIMIT @Limit";
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add((reader.GetString(0), DateTime.Parse(reader.GetString(1))));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error reading watch history");
        }
        return result;
    }

    public async Task SetChannelFavoriteAsync(string channelId, bool isFavorite)
    {
        try
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "UPDATE Channels SET IsFavorite = @Fav WHERE Id = @Id";
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Fav", isFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("@Id", channelId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error setting favorite");
        }
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
