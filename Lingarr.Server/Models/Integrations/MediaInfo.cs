using System.Text.Json.Serialization;

namespace Lingarr.Server.Models.Integrations;

public class MediaInfo
{
    [JsonPropertyName("subtitles")]
    public string Subtitles { get; set; } = string.Empty;
}
