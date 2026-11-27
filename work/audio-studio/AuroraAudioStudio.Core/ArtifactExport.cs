using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Core;

public static class ArtifactExport
{
    public static void ValidateSubtitleEdit(string source, string text)
    {
        var original = ArtifactValidator.Inspect(source);
        if (original.Format == "SRT" && original.Subtitles == 0 && string.IsNullOrWhiteSpace(text)) return;
        ArtifactValidator.ValidateSubtitleText(text);
    }

    public static async Task<string> CopyAsync(string source, string folder, string? subtitleText = null)
    {
        ArtifactValidator.Validate(source);
        if (subtitleText is not null) ValidateSubtitleEdit(source, subtitleText);
        var name = Path.GetFileNameWithoutExtension(source) + (subtitleText is null ? "" : "-edited");
        var extension = Path.GetExtension(source);
        // Silence evidence must travel with an empty SRT so the exported artifact remains valid.
        var silence = extension.Equals(".srt", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(subtitleText ?? await File.ReadAllTextAsync(source));
        // Validate in the destination filesystem before promoting any final filename.
        // Failed exports remain unmistakably temporary rather than valid-looking results.
        var staging = Path.Combine(folder, ".aurora-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var pending = Path.Combine(staging, "output" + extension);
        await using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
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
        var pendingEvidence = Path.ChangeExtension(pending, ".json");
        if (silence) File.Copy(Path.ChangeExtension(source, ".json"), pendingEvidence, false);
        ArtifactValidator.Validate(pending);
        for (var i = 0; ; i++)
        {
            var destination = Path.Combine(folder, name + (i == 0 ? "" : "-" + i) + extension);
            var evidence = Path.ChangeExtension(destination, ".json");
            if (File.Exists(destination) || (silence && File.Exists(evidence))) continue;
            var ownsEvidence = false;
            try
            {
                if (silence) { File.Copy(pendingEvidence, evidence, false); ownsEvidence = true; }
                File.Move(pending, destination, false);
            }
            catch (IOException) when (!ownsEvidence && (File.Exists(destination) || (silence && File.Exists(evidence))))
            { continue; }
            catch
            {
                if (ownsEvidence) File.Delete(evidence);
                throw;
            }
            // These exact files belong to this export, never the user's prior outputs.
            try
            {
                if (silence) File.Delete(pendingEvidence);
                Directory.Delete(staging, false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return destination;
        }
    }
}
