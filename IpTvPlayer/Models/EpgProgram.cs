namespace IpTvPlayer.Models;

public class EpgProgram
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ChannelTvgId { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Category { get; set; }

    public string TimeRange => $"{StartTime:HH:mm} - {EndTime:HH:mm}";
    public bool IsLive => DateTime.Now >= StartTime && DateTime.Now < EndTime;
}
