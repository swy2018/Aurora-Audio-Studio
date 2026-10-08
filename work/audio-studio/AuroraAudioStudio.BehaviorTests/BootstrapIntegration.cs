using System.Diagnostics;
using System.Text.Json;
using AuroraAudioStudio.Services;
using AuroraAudioStudio.Models;

internal static class BootstrapIntegration
{
    public static async Task InstallModelAsync(string id, string evidence)
    {
        evidence = Path.GetFullPath(evidence);
        var settings = new SettingsService(evidence);
        var catalog = new ModelCatalogService(settings);
        var model = catalog.Find(id) ?? throw new ArgumentException("Unknown model");
        if (catalog.IsInstalled(model)) throw new IOException("Use an empty model target for first-install acceptance.");
        foreach (var tool in new[] { "uv", "git", "ffmpeg", "ffprobe", "sox" })
            if (BundledTools.Find(tool) is null) throw new IOException("Test output must contain the actual application tools: " + tool);
        var oldPath = Environment.GetEnvironmentVariable("PATH");
        var oldHfHome = Environment.GetEnvironmentVariable("HF_HOME");
        var oldImplicitToken = Environment.GetEnvironmentVariable("HF_HUB_DISABLE_IMPLICIT_TOKEN");
        using var timeout = new CancellationTokenSource(TimeSpan.FromHours(1));
        try
        {
            Environment.SetEnvironmentVariable("PATH", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"));
            Environment.SetEnvironmentVariable("HF_HOME", Path.Combine(settings.Current.LocalAiRoot, ".aurora", "huggingface"));
            Environment.SetEnvironmentVariable("HF_HUB_DISABLE_IMPLICIT_TOKEN", "1");
            var updater = new ModelUpdateService(catalog, settings);
            var result = await updater.UpdateAsync(model, new Reporter(value => Console.WriteLine(value.LogLine ?? value.Detail)), timeout.Token);
            await File.WriteAllTextAsync(Path.Combine(evidence, id + "-install.json"), JsonSerializer.Serialize(new { id, result, installed = catalog.IsInstalled(model), modelRoot = settings.Current.LocalAiRoot }, new JsonSerializerOptions { WriteIndented = true }));
            if (!result.Success || !catalog.IsInstalled(model)) throw new Exception(result.Message);
            Console.WriteLine("FIRST_INSTALL_PASS " + id);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", oldPath);
            Environment.SetEnvironmentVariable("HF_HOME", oldHfHome);
            Environment.SetEnvironmentVariable("HF_HUB_DISABLE_IMPLICIT_TOKEN", oldImplicitToken);
        }
    }

    private sealed class Reporter(Action<ModelInstallProgress> action) : IProgress<ModelInstallProgress>
    {
        public void Report(ModelInstallProgress value) => action(value);
    }

    // Real downloads of Python + manager dependencies, not model weights. No global installs.
    public static async Task RunAsync(string runtime, string evidence)
    {
        runtime = Path.GetFullPath(runtime);
        evidence = Path.GetFullPath(evidence);
        Directory.CreateDirectory(evidence);
        using var log = new StreamWriter(Path.Combine(evidence, "bootstrap.log")) { AutoFlush = true };
        var checks = new List<string>();
        var modelRoot = Path.Combine(evidence, "空白模型 日本語");
        async Task<string> Run(string executable, params string[] arguments)
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            // No developer Python, Git, uv, FFmpeg, SoX, WinGet or user PATH.
            info.Environment["PATH"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
            info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            info.Environment["GIT_CONFIG_GLOBAL"] = "NUL";
            BundledTools.ConfigureInstaller(info, modelRoot, runtime);
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = new Process { StartInfo = info };
            BackgroundProcess.Start(process);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            using var canceled = timeout.Token.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } });
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(timeout.Token);
            var text = await output + await errors;
            log.WriteLine(executable + " " + string.Join(' ', arguments));
            log.WriteLine(text);
            if (process.ExitCode != 0) throw new Exception(text);
            return text;
        }
        void Pass(string check) { checks.Add(check); Console.WriteLine("PASS " + check); }
        string Tool(string name) => BundledTools.Find(name, runtime) ?? throw new FileNotFoundException("Bundle incomplete: " + name);
        foreach (var name in new[] { "uv", "git", "ffmpeg", "ffprobe", "sox" })
        {
            await Run(Tool(name), name is "ffmpeg" or "ffprobe" ? "-version" : "--version");
            Pass("bundled " + name + " starts with system-only PATH");
        }
        var remote = await Run(Tool("git"), "ls-remote", "https://github.com/swy2018/Aurora-Audio-Studio.git", "HEAD");
        if (!remote.Contains("HEAD")) throw new Exception("Git HTTPS transport failed");
        Pass("Git HTTPS works without installed Git or user Git config");
        foreach (var version in new[] { "3.10", "3.11", "3.12" })
        {
            var environment = Path.Combine(modelRoot, "python-" + version);
            await Run(Tool("uv"), "venv", "--python", version, environment);
            var python = Path.Combine(environment, "Scripts", "python.exe");
            var result = await Run(python, "-c", "import sys; print(sys.base_prefix); print('中文 日本語 Ω')");
            if (!result.Contains(Path.Combine(modelRoot, ".aurora", "python")) || !result.Contains("中文 日本語 Ω"))
                throw new Exception("Managed Python or UTF-8 isolation failed: " + result);
            Pass("managed Python " + version + " downloaded, isolated and UTF-8 output preserved");
        }
        var manager = Path.Combine(modelRoot, "python-3.11", "Scripts", "python.exe");
        await Run(Tool("uv"), "pip", "install", "--python", manager, "huggingface_hub>=0.34,<2", "filelock");
        await Run(manager, "-c", "import huggingface_hub, filelock; print('MANAGER_READY')");
        await Run(Tool("uv"), "pip", "check", "--python", manager);
        Pass("download manager installs and imports in a fresh Python environment");
        var wav = Path.Combine(evidence, "原始音频.wav");
        await Run(Tool("ffmpeg"), "-nostdin", "-y", "-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.2", wav);
        await Run(Tool("sox"), wav, Path.Combine(evidence, "处理音频.wav"), "trim", "0", "0.1");
        var probe = await Run(Tool("ffprobe"), "-v", "error", "-show_entries", "format=duration", "-of", "json", Path.Combine(evidence, "处理音频.wav"));
        using var audio = JsonDocument.Parse(probe);
        if (double.Parse(audio.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture) <= 0)
            throw new Exception("No valid audio output");
        Pass("FFmpeg, SoX and ffprobe process real audio with Unicode paths");
        await File.WriteAllTextAsync(Path.Combine(evidence, "bootstrap-result.json"), JsonSerializer.Serialize(new { checks, modelRoot, runtime, platform = "Windows; isolated PATH, not a clean VM" }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Evidence retained: " + evidence);
    }
}
