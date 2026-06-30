namespace IpTvPlayer.Models;

public class Channel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public string Url { get; set; }
    public string? LogoUrl { get; set; }
    public string? GroupTitle { get; set; }
    public string? TvgId { get; set; }
    public DateTime AddedDate { get; set; } = DateTime.Now;
    public DateTime? LastWatchedDate { get; set; }
}
