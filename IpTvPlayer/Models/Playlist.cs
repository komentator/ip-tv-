namespace IpTvPlayer.Models;

public class Playlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? UpdatedDate { get; set; }
    public List<Channel> Channels { get; set; } = new();
    public bool IsFavorite { get; set; }
}
