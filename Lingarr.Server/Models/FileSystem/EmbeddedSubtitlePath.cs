namespace Lingarr.Server.Models.FileSystem;

/// <summary>
/// Helper for constructing and parsing the virtual path used to identify embedded subtitle tracks.
/// Format: <c>embedded://&lt;videoFilePath&gt;?lang=&lt;languageCode&gt;</c>
/// Example: <c>embedded:///movies/Movie (2023)/Movie (2023).mkv?lang=en</c>
/// </summary>
public static class EmbeddedSubtitlePath
{
    private const string Scheme = "embedded://";

    /// <summary>Builds a virtual embedded subtitle path from a video file path and a language code.</summary>
    public static string Build(string videoFilePath, string languageCode)
    {
        return $"{Scheme}{videoFilePath}?lang={languageCode}";
    }

    /// <summary>Returns true if the given path uses the embedded subtitle scheme.</summary>
    public static bool IsEmbedded(string? path)
    {
        return path != null && path.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a virtual embedded subtitle path into its video file path and language code components.
    /// Returns false if the path is not a valid embedded subtitle path.
    /// </summary>
    public static bool TryParse(string path, out string videoFilePath, out string languageCode)
    {
        videoFilePath = string.Empty;
        languageCode = string.Empty;

        if (!IsEmbedded(path))
        {
            return false;
        }

        // Strip the scheme prefix then split on "?lang="
        var withoutScheme = path[Scheme.Length..];
        var langSeparatorIndex = withoutScheme.LastIndexOf("?lang=", StringComparison.OrdinalIgnoreCase);
        if (langSeparatorIndex < 0)
        {
            return false;
        }

        videoFilePath = withoutScheme[..langSeparatorIndex];
        languageCode = withoutScheme[(langSeparatorIndex + "?lang=".Length)..];
        return !string.IsNullOrEmpty(videoFilePath) && !string.IsNullOrEmpty(languageCode);
    }

    /// <summary>
    /// Builds a <see cref="Subtitles"/> object for an embedded subtitle track.
    /// </summary>
    public static Subtitles BuildSubtitle(string videoFilePath, string languageCode)
    {
        return new Subtitles
        {
            Path = Build(videoFilePath, languageCode),
            FileName = System.IO.Path.GetFileNameWithoutExtension(videoFilePath),
            Language = languageCode,
            Caption = string.Empty,
            Format = ".srt",
            IsEmbedded = true
        };
    }
}
