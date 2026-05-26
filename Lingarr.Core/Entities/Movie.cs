using Lingarr.Core.Interfaces;

namespace Lingarr.Core.Entities;

public class Movie : BaseEntity, IMedia
{
    public required int RadarrId { get; set; }
    public required string Title { get; set; }
    public required string? FileName { get; set; }
    public required string? Path { get; set; }
    public string? MediaHash { get; set; } = string.Empty;
    public required DateTime? DateAdded { get; set; }
    public List<Image> Images { get; set; } = new();
    public bool IncludeInTranslation { get; set; } = true;
    public int? TranslationAgeThreshold { get; set; }

    /// <summary>Full path to the video file (e.g. /movies/Title (2023)/Title (2023).mkv).</summary>
    public string? VideoFilePath { get; set; }

    /// <summary>
    /// Language codes of embedded subtitle tracks reported by Radarr (e.g. ["en","fr"]).
    /// Stored as a JSON string in the database.
    /// </summary>
    public List<string> EmbeddedSubtitleLanguages { get; set; } = [];
}