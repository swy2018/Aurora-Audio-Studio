namespace AuroraAudioStudio.Services;

public static class MediaInputPolicy
{
    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase) { ".wav", ".flac", ".mp3", ".m4a", ".aac", ".ogg", ".opus", ".wma" };
    private static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v" };

    public static IReadOnlyList<string> Extensions(string feature) => feature == "subtitles" ? Audio.Concat(Video).ToArray() : Audio.Concat(feature == "separation" ? Video : []).ToArray();

    public static bool IsSupported(string feature, string path) => Extensions(feature).Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
