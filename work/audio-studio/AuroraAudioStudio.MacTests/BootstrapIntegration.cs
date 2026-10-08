using AuroraAudioStudio.Core;
using AuroraAudioStudio.Services;

internal static class BootstrapIntegration
{
    public static async Task RunAsync(string scripts, string evidence, string model)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("This acceptance must run on a native Mac.");
        scripts = Path.GetFullPath(scripts);
        evidence = Path.GetFullPath(evidence);
        var settings = new SettingsService(evidence);
        if (Directory.Exists(settings.Current.LocalAiRoot)) throw new IOException("Use a fresh evidence directory for first-install acceptance.");
        using var log = new StreamWriter(Path.Combine(evidence, "bootstrap.log")) { AutoFlush = true };
        var progress = new InlineProgress<string>(line => { lock (log) log.WriteLine(line); Console.WriteLine(line); });
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "/usr/bin:/bin:/usr/sbin:/sbin");
            using var runtime = new MacRuntime(settings, scripts);
            foreach (var tool in new[] { "uv", "git", "ffmpeg", "ffprobe", "sox" })
                await runtime.RunAsync(BundledTools.Find(tool, scripts) ?? throw new IOException("Bundle missing " + tool),
                    [tool is "ffmpeg" or "ffprobe" ? "-version" : "--version"], progress, CancellationToken.None);
            await runtime.RunAsync(BundledTools.Find("git", scripts)!, ["ls-remote", "https://github.com/swy2018/Aurora-Audio-Studio.git", "HEAD"], progress, CancellationToken.None);
            await runtime.ManageAsync(model, "install", progress, CancellationToken.None);
            if (!runtime.IsReady(model)) throw new IOException("Fresh model was not fully installed.");
            await File.WriteAllTextAsync(Path.Combine(evidence, "bootstrap-result.txt"), "Native Mac first installation passed for " + model + ". No inference claim; no preinstalled development tools used.");
            Console.WriteLine("PASS native Mac first install: " + model);
        }
        finally { Environment.SetEnvironmentVariable("PATH", originalPath); }
    }
}
