namespace AuroraAudioStudio.Services;

public static class AudioRuntime
{
    public static string? FindFfmpeg(string localAiRoot)
    {
        var bundled = Path.Combine(localAiRoot, "Faster-Whisper-XXL", "Faster-Whisper-XXL", "ffmpeg.exe");
        if (File.Exists(bundled)) return bundled;
        var paths = new[] { Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User), Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) };
        foreach (var folder in paths.Where(x => x is not null).SelectMany(x => x!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(folder.Trim('"'), "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
