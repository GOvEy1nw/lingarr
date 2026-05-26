using Lingarr.Core.Data;
using Lingarr.Server.Attributes;
using Lingarr.Server.Interfaces.Services;
using Lingarr.Server.Models;
using Lingarr.Server.Models.FileSystem;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lingarr.Server.Controllers;

[ApiController]
[LingarrAuthorize]
[Route("api/[controller]")]
public class SubtitleController : ControllerBase
{
    private readonly ISubtitleService _subtitleService;
    private readonly LingarrDbContext _dbContext;

    public SubtitleController(ISubtitleService subtitleService, LingarrDbContext dbContext)
    {
        _subtitleService = subtitleService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retrieves a list of subtitle files located at the specified path.
    /// Includes both external subtitle files found on disk and any embedded subtitle tracks
    /// reported by Radarr/Sonarr for media files in the same directory.
    /// </summary>
    /// <param name="subtitlePath">The directory path to search for subtitle files.</param>
    /// <returns>Returns an HTTP 200 OK response with a list of <see cref="Subtitles"/> objects found at the specified path.</returns>
    [HttpPost("all")]
    public async Task<ActionResult<List<Subtitles>>> GetAllSubtitles([FromBody] SubtitlePath subtitlePath)
    {
        var subtitles = await _subtitleService.GetAllSubtitles(subtitlePath.Path);

        subtitles.AddRange(await GetEmbeddedSubtitlesForPath(subtitlePath.Path));

        return Ok(subtitles);
    }

    /// <summary>
    /// Queries the database for any movies or episodes whose directory path matches <paramref name="directoryPath"/>
    /// and returns embedded subtitle entries for each of their stored embedded subtitle tracks.
    /// </summary>
    private async Task<List<Subtitles>> GetEmbeddedSubtitlesForPath(string directoryPath)
    {
        var embedded = new List<Subtitles>();

        var matchingMovies = await _dbContext.Movies
            .Where(m => m.Path == directoryPath && m.VideoFilePath != null)
            .Select(m => new { m.VideoFilePath, m.EmbeddedSubtitleLanguages })
            .ToListAsync();

        foreach (var movie in matchingMovies)
        {
            foreach (var lang in movie.EmbeddedSubtitleLanguages)
            {
                embedded.Add(EmbeddedSubtitlePath.BuildSubtitle(movie.VideoFilePath!, lang));
            }
        }

        var matchingEpisodes = await _dbContext.Episodes
            .Where(e => e.Path == directoryPath && e.VideoFilePath != null)
            .Select(e => new { e.VideoFilePath, e.EmbeddedSubtitleLanguages })
            .ToListAsync();

        foreach (var episode in matchingEpisodes)
        {
            foreach (var lang in episode.EmbeddedSubtitleLanguages)
            {
                embedded.Add(EmbeddedSubtitlePath.BuildSubtitle(episode.VideoFilePath!, lang));
            }
        }

        return embedded;
    }
}