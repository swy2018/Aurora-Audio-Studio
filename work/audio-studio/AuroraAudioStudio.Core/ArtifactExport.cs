using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Core;

public static class ArtifactExport
{
    public static async Task<string> CopyAsync(string source, string folder, string? subtitleText = null)
    {
        ArtifactValidator.Validate(source);
        if (subtitleText is not null) ArtifactValidator.ValidateSubtitleText(subtitleText);
        var name = Path.GetFileNameWithoutExtension(source) + (subtitleText is null ? "" : "-edited");
        var extension = Path.GetExtension(source);
        // Silence evidence must travel with an empty SRT so the exported artifact remains valid.
        var silence = subtitleText is null && extension.Equals(".srt", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(await File.ReadAllTextAsync(source));
        var destination = Path.Combine(folder, name + extension);
        for (var i = 1; File.Exists(destination) || (silence && File.Exists(Path.ChangeExtension(destination, ".json"))); i++)
            destination = Path.Combine(folder, $"{name}-{i}{extension}");
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            if (subtitleText is not null)
            {
                await using var writer = new StreamWriter(output);
                await writer.WriteAsync(subtitleText);
            }
            else
            {
                await using var input = File.OpenRead(source);
                await input.CopyToAsync(output);
            }
        }
        if (silence) File.Copy(Path.ChangeExtension(source, ".json"), Path.ChangeExtension(destination, ".json"), false);
        ArtifactValidator.Validate(destination);
        return destination;
    }
}
