namespace Lingarr.Server.Models.FileSystem;

public class Subtitles
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// True when this subtitle is embedded inside the video container rather than an external file.
    /// When true, <see cref="Path"/> contains a virtual <c>embedded://</c> URI that identifies the
    /// video file and the language track.
    /// </summary>
    public bool IsEmbedded { get; set; }
}