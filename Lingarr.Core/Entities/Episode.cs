using System.ComponentModel.DataAnnotations.Schema;
using Lingarr.Core.Interfaces;

namespace Lingarr.Core.Entities;

public class Episode : BaseEntity, IMedia
{
    public required int SonarrId { get; set; }
    public required int EpisodeNumber { get; set; }
    public required string Title { get; set; }
    public string? FileName { get; set; } = string.Empty;
    public string? Path { get; set; } = string.Empty;
    public string? MediaHash { get; set; } = string.Empty;
    public DateTime? DateAdded { get; set; }

    public int SeasonId { get; set; }
    [ForeignKey(nameof(SeasonId))]
    public required Season Season { get; set; }
    public bool IncludeInTranslation { get; set; } = true;

    /// <summary>Full path to the video file (e.g. /tv/Show/Season 1/Episode.mkv).</summary>
    public string? VideoFilePath { get; set; }

    /// <summary>
    /// Language codes of embedded subtitle tracks reported by Sonarr (e.g. ["en","fr"]).
    /// Stored as a JSON string in the database.
    /// </summary>
    public List<string> EmbeddedSubtitleLanguages { get; set; } = [];
}