namespace Lingarr.Server.Interfaces.Services;

/// <summary>
/// Provides ffmpeg-based operations for embedded subtitle extraction.
/// </summary>
public interface IFfmpegService
{
    /// <summary>
    /// Checks whether ffmpeg is available on the current system.
    /// </summary>
    bool IsAvailable();

    /// <summary>
    /// Extracts the first embedded subtitle track that matches <paramref name="languageCode"/> from
    /// <paramref name="videoFilePath"/> and writes it to <paramref name="outputPath"/> in SRT format.
    /// </summary>
    /// <param name="videoFilePath">Absolute path to the source video file.</param>
    /// <param name="languageCode">Two-letter ISO 639-1 language code (e.g. "en", "fr").</param>
    /// <param name="outputPath">Absolute path for the extracted SRT file.</param>
    /// <exception cref="InvalidOperationException">Thrown when ffmpeg is not available.</exception>
    /// <exception cref="Exception">Thrown when ffmpeg exits with a non-zero exit code.</exception>
    Task ExtractSubtitleAsync(string videoFilePath, string languageCode, string outputPath);
}
