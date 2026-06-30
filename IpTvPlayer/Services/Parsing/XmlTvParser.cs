using System.Globalization;
using System.Xml.Linq;
using IpTvPlayer.Models;
using Serilog;

namespace IpTvPlayer.Services.Parsing;

public class XmlTvParser
{
    public List<EpgProgram> Parse(string xml)
    {
        var programs = new List<EpgProgram>();

        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return programs;

            foreach (var prog in root.Elements("programme"))
            {
                var channel = (string?)prog.Attribute("channel") ?? "";
                var startStr = (string?)prog.Attribute("start") ?? "";
                var stopStr = (string?)prog.Attribute("stop") ?? "";

                if (!TryParseXmlTvTime(startStr, out var start) || !TryParseXmlTvTime(stopStr, out var stop))
                    continue;

                programs.Add(new EpgProgram
                {
                    ChannelTvgId = channel,
                    Title = (string?)prog.Element("title") ?? "",
                    Description = (string?)prog.Element("desc"),
                    Category = (string?)prog.Element("category"),
                    StartTime = start,
                    EndTime = stop
                });
            }

            Log.Information("XMLTV parsed: {Count} programs", programs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error parsing XMLTV");
        }

        return programs;
    }

    public async Task<List<EpgProgram>> ParseFromUrlAsync(string url)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);
            var content = await httpClient.GetStringAsync(url);
            return Parse(content);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching XMLTV from {Url}", url);
            return new List<EpgProgram>();
        }
    }

    private static bool TryParseXmlTvTime(string value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var formats = new[]
        {
            "yyyyMMddHHmmss zzz",
            "yyyyMMddHHmmss zzzz",
            "yyyyMMddHHmmsszzz",
            "yyyyMMddHHmmss",
            "yyyyMMddHHmm zzz",
            "yyyyMMddHHmm"
        };

        return DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out result);
    }
}
