using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Lingarr.Server.Interfaces.Services;

namespace Lingarr.Server.Services;

/// <summary>
/// Uses the ffmpeg/ffprobe CLIs to extract embedded subtitle tracks from video files.
/// Both tools must be installed and available on the system PATH (or at the paths set via
/// the FFMPEG_PATH / FFPROBE_PATH environment variables).
/// </summary>
public class FfmpegService : IFfmpegService
{
    private readonly ILogger<FfmpegService> _logger;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FfmpegService(ILogger<FfmpegService> logger)
    {
        _logger = logger;
        _ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";
        _ffprobePath = Environment.GetEnvironmentVariable("FFPROBE_PATH") ?? "ffprobe";
    }

    /// <inheritdoc />
    public bool IsAvailable()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = BuildProcessInfo(_ffmpegPath, "-version");
            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task ExtractSubtitleAsync(string videoFilePath, string languageCode, string outputPath)
    {
        if (!IsAvailable())
        {
            throw new InvalidOperationException(
                "ffmpeg is not available. Install ffmpeg to enable embedded subtitle extraction.");
        }

        // Use ffprobe to find the subtitle stream index with flexible BCP47 language matching.
        // Modern mkvmerge (v95+) stores tags as "en-US" rather than "eng", so an exact match
        // against the three-letter ISO code alone would fail.
        var streamIndex = await FindSubtitleStreamIndexAsync(videoFilePath, languageCode);

        string mapArg;
        if (streamIndex >= 0)
        {
            // Use the subtitle-relative stream index: -map 0:s:<N>
            mapArg = $"0:s:{streamIndex}";
            _logger.LogInformation(
                "Matched subtitle stream index {Index} for language '{Language}' in '{VideoFile}'",
                streamIndex, languageCode, videoFilePath);
        }
        else
        {
            // Fall back to metadata language tag filter if ffprobe found nothing
            var threeLetterCode = GetThreeLetterCode(languageCode);
            mapArg = $"0:s:m:language:{threeLetterCode}";
            _logger.LogWarning(
                "ffprobe found no subtitle stream for language '{Language}' in '{VideoFile}'; " +
                "falling back to metadata tag filter with code '{ThreeLetter}'",
                languageCode, videoFilePath, threeLetterCode);
        }

        var arguments = $"-i \"{videoFilePath}\" -map {mapArg} -c:s srt \"{outputPath}\" -y";

        _logger.LogInformation(
            "Extracting embedded subtitle (lang={Language}) from |Green|{VideoFile}|/Green|",
            languageCode, videoFilePath);

        using var process = new Process();
        process.StartInfo = BuildProcessInfo(_ffmpegPath, arguments);
        process.Start();

        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "ffmpeg exited with code {ExitCode} while extracting subtitle (lang={Language}) " +
                "from '{VideoFile}'. stderr: {Stderr}",
                process.ExitCode, languageCode, videoFilePath, stderr);

            throw new InvalidOperationException(
                $"ffmpeg failed to extract subtitle track (language={languageCode}) from '{videoFilePath}'. " +
                $"The track may be image-based (PGS/VOBSUB) or missing. Exit code: {process.ExitCode}");
        }

        _logger.LogInformation(
            "Successfully extracted embedded subtitle to |Green|{OutputPath}|/Green|", outputPath);
    }

    /// <summary>
    /// Runs ffprobe to list subtitle streams in <paramref name="videoFilePath"/> and returns the
    /// subtitle-relative stream index (i.e. the N in <c>-map 0:s:N</c>) of the first stream whose
    /// language tag matches <paramref name="languageCode"/>.
    /// Matching is flexible: accepts ISO 639-1 ("en"), ISO 639-2 ("eng"), and BCP47 region tags
    /// ("en-US", "en-GB", etc.).
    /// Returns -1 if no match is found or if ffprobe fails.
    /// </summary>
    private async Task<int> FindSubtitleStreamIndexAsync(string videoFilePath, string languageCode)
    {
        var twoLetterCode = GetTwoLetterCode(languageCode);
        var threeLetterCode = GetThreeLetterCode(languageCode);

        var arguments =
            $"-v error -select_streams s " +
            $"-show_entries stream=index:stream_tags=language " +
            $"-of json \"{videoFilePath}\"";

        using var process = new Process();
        process.StartInfo = BuildProcessInfo(_ffprobePath, arguments);
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            _logger.LogWarning(
                "ffprobe failed or returned no output for '{VideoFile}'", videoFilePath);
            return -1;
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var streams = doc.RootElement.GetProperty("streams");

            // The array is already filtered to subtitle streams only (due to -select_streams s),
            // so the loop index `i` directly maps to the subtitle-relative stream index for -map 0:s:i
            for (var i = 0; i < streams.GetArrayLength(); i++)
            {
                var stream = streams[i];

                if (!stream.TryGetProperty("tags", out var tags))
                {
                    continue;
                }

                if (!tags.TryGetProperty("language", out var langProp))
                {
                    continue;
                }

                var streamLang = langProp.GetString() ?? string.Empty;

                if (LanguageTagMatches(streamLang, twoLetterCode, threeLetterCode))
                {
                    return i;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse ffprobe JSON output for '{VideoFile}'", videoFilePath);
        }

        return -1;
    }

    /// <summary>
    /// Returns true when <paramref name="streamLang"/> matches the two-letter ISO 639-1 code,
    /// the three-letter ISO 639-2 code, or a BCP47 region variant (e.g. "en-US", "en_US").
    /// </summary>
    private static bool LanguageTagMatches(
        string streamLang,
        string twoLetterCode,
        string threeLetterCode)
    {
        return
            string.Equals(streamLang, twoLetterCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(streamLang, threeLetterCode, StringComparison.OrdinalIgnoreCase) ||
            streamLang.StartsWith(twoLetterCode + "-", StringComparison.OrdinalIgnoreCase) ||
            streamLang.StartsWith(twoLetterCode + "_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a two-letter ISO 639-1 code from any recognised language code format.
    /// Falls back to the original value on failure.
    /// </summary>
    private string GetTwoLetterCode(string languageCode)
    {
        try
        {
            var culture = CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(c =>
                string.Equals(c.TwoLetterISOLanguageName, languageCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.ThreeLetterISOLanguageName, languageCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name, languageCode, StringComparison.OrdinalIgnoreCase));

            return culture?.TwoLetterISOLanguageName ?? languageCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Could not resolve two-letter code for '{LanguageCode}', using as-is.", languageCode);
            return languageCode;
        }
    }

    /// <summary>
    /// Resolves the three-letter ISO 639-2/T code used in ffmpeg stream metadata tags.
    /// Falls back to the original value on failure.
    /// </summary>
    private string GetThreeLetterCode(string languageCode)
    {
        try
        {
            var culture = CultureInfo.GetCultures(CultureTypes.AllCultures).FirstOrDefault(c =>
                string.Equals(c.TwoLetterISOLanguageName, languageCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.ThreeLetterISOLanguageName, languageCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name, languageCode, StringComparison.OrdinalIgnoreCase));

            return culture?.ThreeLetterISOLanguageName ?? languageCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "Could not resolve three-letter code for '{LanguageCode}', using as-is.", languageCode);
            return languageCode;
        }
    }

    private static ProcessStartInfo BuildProcessInfo(string fileName, string arguments)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }
}
