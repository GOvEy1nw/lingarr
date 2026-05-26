using System.Globalization;

namespace Lingarr.Server.Services;

/// <summary>
/// Provides validation and conversion utilities for language codes.
/// Supports both two-letter ISO codes (en, pt, zh) and region-specific codes (pt-BR, nl-NL, zh-TW).
/// </summary>
public class LanguageCodeService
{
    private static readonly CultureInfo[] Cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
    private readonly ILogger<LanguageCodeService> _logger;

    /// <summary>
    /// Mapping of legacy/common Chinese language codes to .NET CultureInfo codes.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyChineseCodeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "zh-TW", "zh-Hant-TW" },  // Traditional Chinese (Taiwan)
        { "zh-CN", "zh-Hans-CN" },  // Simplified Chinese (China)
        { "zh-HK", "zh-Hant-HK" },  // Traditional Chinese (Hong Kong)
        { "zh-SG", "zh-Hans-SG" },  // Simplified Chinese (Singapore)
        { "zh-MO", "zh-Hant-MO" }   // Traditional Chinese (Macao)
    };

    public LanguageCodeService(ILogger<LanguageCodeService> logger)
    {
        _logger = logger;
    }

    public bool Validate(string? languageCode) => FindCulture(languageCode) != null;
    
    public string GetCultureName(string languageCode)
    {
        var culture = GetCulture(languageCode);
        return culture.EnglishName;
    }

    /// <summary>
    /// Finds the CultureInfo for a given language code, handling legacy Chinese codes.
    /// Returns null if the code is invalid or not found.
    /// </summary>
    private static CultureInfo? FindCulture(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var trimmedCode = languageCode.Trim();

        // Map legacy Chinese codes to .NET equivalents
        var normalizedCode = LegacyChineseCodeMapping.TryGetValue(trimmedCode, out var mapped)
            ? mapped
            : trimmedCode;

        return Cultures.FirstOrDefault(c =>
            string.Equals(normalizedCode, c.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedCode, c.ThreeLetterISOLanguageName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedCode, c.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the CultureInfo for a language code, throwing if invalid.
    /// </summary>
    private static CultureInfo GetCulture(string languageCode)
    {
        var culture = FindCulture(languageCode);
        if (culture == null)
        {
            throw new ArgumentException($"Invalid language code: '{languageCode}'", nameof(languageCode));
        }

        return culture;
    }

    /// <summary>
    /// Gets the matched culture code in lowercase, preserving region information.
    /// For legacy Chinese codes (zh-TW, zh-CN), returns them in their original format
    /// (e.g., "zh-tw", not "zh-hant-tw") for compatibility with subtitle files and translation services.
    /// </summary>
    public string GetNormalizedCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("Language code cannot be empty", nameof(languageCode));
        }

        var trimmedCode = languageCode.Trim();

        // Preserve legacy Chinese codes in their original format
        if (LegacyChineseCodeMapping.ContainsKey(trimmedCode))
        {
            return trimmedCode.ToLowerInvariant();
        }

        var culture = GetCulture(trimmedCode);
        return culture.Name.ToLowerInvariant();
    }

    /// <summary>
    /// Tries to resolve a two-letter ISO language code from an English language name such as those
    /// returned by Radarr/Sonarr in <c>mediaInfo.subtitles</c> (e.g. "English" → "en", "French" → "fr").
    /// Only neutral cultures (no region suffix) are considered so that "English" never resolves to
    /// "en-US" by accident.
    /// </summary>
    /// <param name="englishName">The English language name to look up.</param>
    /// <param name="code">The resolved two-letter ISO 639-1 code, or <c>null</c> on failure.</param>
    /// <returns><c>true</c> when a match was found.</returns>
    public bool TryGetCodeFromEnglishName(string englishName, out string? code)
    {
        code = null;
        if (string.IsNullOrWhiteSpace(englishName))
        {
            return false;
        }

        var trimmed = englishName.Trim();

        // First try an ordinary code lookup in case the caller already passes a code
        if (Validate(trimmed))
        {
            code = GetNormalizedCode(trimmed);
            return true;
        }

        // Search neutral cultures by their English name (e.g. CultureInfo("en").EnglishName == "English")
        var match = Cultures.FirstOrDefault(c =>
            c.IsNeutralCulture &&
            string.Equals(c.EnglishName, trimmed, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            return false;
        }

        code = match.TwoLetterISOLanguageName.ToLowerInvariant();
        return true;
    }

    /// <summary>
    /// Parses the subtitle languages string returned by Radarr/Sonarr in
    /// <c>movieFile.mediaInfo.subtitles</c> or <c>episodeFile.mediaInfo.subtitles</c>.
    /// The string may use " / " or ", " as a separator (e.g. "English / French" or "English, French").
    /// Each token is resolved to a two-letter ISO 639-1 code; unrecognised tokens are skipped.
    /// </summary>
    /// <param name="subtitlesString">The raw subtitle languages string from Radarr/Sonarr.</param>
    /// <returns>A deduplicated list of two-letter language codes.</returns>
    public List<string> ParseEmbeddedSubtitleLanguages(string subtitlesString)
    {
        if (string.IsNullOrWhiteSpace(subtitlesString))
        {
            return [];
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tokens = subtitlesString
            .Split([" / ", ", ", "/", ","], StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (!TryGetCodeFromEnglishName(trimmed, out var code) || code == null)
            {
                _logger.LogDebug("Could not resolve embedded subtitle language token '{Token}' to a language code.", trimmed);
                continue;
            }

            if (seen.Add(code))
            {
                result.Add(code);
            }
        }

        return result;
    }
}
