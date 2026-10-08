namespace AuroraAudioStudio.Services;

public static class AudioRuntime
{
    public static string? FindFfmpeg(string localAiRoot) => FindTool(localAiRoot, "ffmpeg");
    public static string? FindFfprobe(string localAiRoot) => FindTool(localAiRoot, "ffprobe");

    private static string? FindTool(string localAiRoot, string tool)
    {
        if (BundledTools.Find(tool) is { } packaged) return packaged;
        var executable = tool + (OperatingSystem.IsWindows() ? ".exe" : "");
        var bundled = OperatingSystem.IsWindows()
            ? Path.Combine(localAiRoot, "Faster-Whisper-XXL", "Faster-Whisper-XXL", executable)
            : Path.Combine(AppContext.BaseDirectory, "Runtime", "bin", executable);
        if (File.Exists(bundled)) return bundled;
        var paths = OperatingSystem.IsWindows()
            ? new[] { Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User), Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) }
            : new[] { Environment.GetEnvironmentVariable("PATH"), "/opt/homebrew/bin:/usr/local/bin:/usr/bin" };
        foreach (var folder in paths.Where(x => x is not null).SelectMany(x => x!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(folder.Trim('"'), executable);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
