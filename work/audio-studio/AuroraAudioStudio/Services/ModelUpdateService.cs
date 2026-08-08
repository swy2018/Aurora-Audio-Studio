using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Reflection;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class ModelUpdateService(ModelCatalogService catalog, SettingsService settings)
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/swy2018/Aurora-Audio-Studio/main/model-manifest.json";
    private readonly HttpClient client = CreateClient();

    public async Task<OperationResult> CheckAsync(ModelDefinition model)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (!catalog.IsInstalled(model)) return new(false, "尚未安装");
        if (model.UpdateKind.Equals("huggingface", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
        {
            var remoteRevision = await GetHuggingFaceRevisionAsync(model.Repository);
            if (string.IsNullOrWhiteSpace(remoteRevision)) return new(false, "暂时无法连接 Hugging Face");
            var localRevision = ReadModelRevision(root);
            return new(true, localRevision.Equals(remoteRevision, StringComparison.OrdinalIgnoreCase) ? "已是最新版本" : "发现新版本",
                localRevision.Equals(remoteRevision, StringComparison.OrdinalIgnoreCase) ? "current" : "available");
        }
        var entry = await FindManifestEntryAsync(model.Id);
        if (entry is not null)
        {
            var localVersion = ReadInstalledVersion(model.Id);
            return new(true, localVersion == entry.Version ? "已是最新版本" : $"发现新版本 {entry.Version}", localVersion == entry.Version ? "current" : "available");
        }
        if (model.UpdateKind.StartsWith("git", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(root, ".git")))
        {
            var fetch = await RunGitAsync(root, "fetch --quiet origin");
            if (!fetch.Success) return fetch;
            var local = await RunGitAsync(root, "rev-parse HEAD");
            var remote = await RunGitAsync(root, "rev-parse @{u}");
            if (!local.Success || !remote.Success) return new(false, "暂时无法比较版本");
            return new(true, local.Path == remote.Path ? "已是最新版本" : "发现新版本", local.Path == remote.Path ? "current" : "available");
        }
        return new(true, "当前已是推荐版本", "current");
    }

    public async Task<OperationResult> UpdateAsync(ModelDefinition model)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (model.UpdateKind.Equals("huggingface", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await UpdateHuggingFaceAsync(model, root);
        if (model.UpdateKind.Equals("uv-package", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await InstallUvPackageAsync(model, root);
        var entry = await FindManifestEntryAsync(model.Id);
        if (entry is not null) return await InstallManifestEntryAsync(model, entry, root);
        if (!catalog.IsInstalled(model)) return new(false, "此引擎需要通过 Aurora 安装程序添加运行环境。 ");
        if (model.UpdateKind.StartsWith("git", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(root, ".git")))
            return await RunGitAsync(root, "pull --ff-only");
        return new(false, "当前没有可安装的新版本。");
    }

    private async Task<OperationResult> InstallUvPackageAsync(ModelDefinition model, string root)
    {
        var uv = ResolveUvExecutable();
        if (uv is null)
        {
            var installUv = new ProcessStartInfo("winget.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { "install", "--id", "astral-sh.uv", "-e", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity" }) installUv.ArgumentList.Add(value);
            var uvResult = await RunProcessAsync(installUv);
            if (uvResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(uvResult.Error) ? "无法安装模型部署组件 uv。" : uvResult.Error);
            uv = ResolveUvExecutable();
            if (uv is null) return new(false, "uv 已安装，但当前 Aurora 会话尚未找到它。请重新打开 Aurora 后重试。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        var create = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "venv", "--python", "3.11", root }) create.ArgumentList.Add(value);
        var createResult = await RunProcessAsync(create);
        if (createResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(createResult.Error) ? "无法创建模型隔离环境。" : createResult.Error);

        var python = Path.Combine(root, "Scripts", "python.exe");
        var install = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "pip", "install", "--upgrade", "--python", python, model.Repository! }) install.ArgumentList.Add(value);
        var installResult = await RunProcessAsync(install);
        if (installResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(installResult.Error) ? $"{model.Name} 部署失败。" : installResult.Error);
        if (!catalog.IsInstalled(model)) return new(false, $"{model.Name} 已完成环境安装，但启动组件未通过完整性检查。");
        WriteInstalledVersion(model.Id, DateTime.UtcNow.ToString("yyyy.MM.dd"));
        return new(true, $"{model.Name} 已下载并部署完成", "current");
    }

    private string? ResolveUvExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", "uv.exe"),
            Path.Combine(settings.Current.LocalAiRoot, "Qwen3-TTS", "Python312", "Scripts", "uv.exe")
        };
        foreach (var candidate in candidates) if (File.Exists(candidate)) return candidate;
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim(), "uv.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public async Task<IReadOnlyDictionary<string, OperationResult>> CheckAllAsync()
    {
        var result = new Dictionary<string, OperationResult>();
        foreach (var model in catalog.Definitions) result[model.Id] = await CheckAsync(model);
        return result;
    }

    private async Task<OperationResult> InstallManifestEntryAsync(ModelDefinition model, ModelUpdateEntry entry, string root)
    {
        if (string.IsNullOrWhiteSpace(entry.Url) || entry.Sha256.Length != 64) return new(false, "更新清单不完整，已停止更新。");
        var folder = Path.Combine(settings.UpdatesRoot, "Models", model.Id, entry.Version);
        Directory.CreateDirectory(folder);
        var package = Path.Combine(folder, entry.PackageKind.Equals("zip", StringComparison.OrdinalIgnoreCase) ? "package.zip" : "package.bin");
        using (var response = await client.GetAsync(entry.Url, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(package);
            await input.CopyToAsync(output);
        }
        await using (var stream = File.OpenRead(package))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!actual.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)) { File.Delete(package); return new(false, "更新包校验失败，已安全停止。"); }
        }
        Directory.CreateDirectory(root);
        if (entry.PackageKind.Equals("zip", StringComparison.OrdinalIgnoreCase)) ZipFile.ExtractToDirectory(package, root, true);
        else
        {
            if (string.IsNullOrWhiteSpace(entry.RelativePath)) return new(false, "更新清单缺少安装位置。");
            var destination = Path.GetFullPath(Path.Combine(root, entry.RelativePath));
            if (!destination.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) return new(false, "更新路径不安全，已停止更新。");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(package, destination, true);
        }
        WriteInstalledVersion(model.Id, entry.Version);
        return new(true, $"{model.Name} 已更新至 {entry.Version}");
    }

    private async Task<ModelUpdateEntry?> FindManifestEntryAsync(string id)
    {
        try
        {
            var json = await client.GetStringAsync(ManifestUrl);
            var manifest = JsonSerializer.Deserialize<ModelUpdateManifest>(json);
            return manifest?.Models.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }

    private async Task<string?> GetHuggingFaceRevisionAsync(string repository)
    {
        try
        {
            var json = await client.GetStringAsync("https://huggingface.co/api/models/" + repository);
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("sha", out var value) ? value.GetString() : null;
        }
        catch { return null; }
    }

    private async Task<OperationResult> UpdateHuggingFaceAsync(ModelDefinition model, string root)
    {
        var revision = await GetHuggingFaceRevisionAsync(model.Repository!);
        if (string.IsNullOrWhiteSpace(revision)) return new(false, "暂时无法连接 Hugging Face");
        var previous = ReadModelRevision(root);
        if (!string.IsNullOrWhiteSpace(previous) && !previous.Equals(revision, StringComparison.OrdinalIgnoreCase))
            File.WriteAllText(Path.Combine(root, ".aurora-previous-revision"), previous);
        return await DownloadHuggingFaceRevisionAsync(model, root, revision);
    }

    public async Task<OperationResult> RollbackAsync(ModelDefinition model)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        var snapshot = Path.Combine(root, ".aurora-previous-revision");
        if (!File.Exists(snapshot) || string.IsNullOrWhiteSpace(model.Repository)) return new(false, "此模型还没有可恢复的版本快照。 ");
        var revision = File.ReadAllText(snapshot).Trim();
        return await DownloadHuggingFaceRevisionAsync(model, root, revision);
    }

    private async Task<OperationResult> DownloadHuggingFaceRevisionAsync(ModelDefinition model, string root, string revision)
    {
        var launcher = Path.Combine(settings.Current.LocalAiRoot, "Qwen3-TTS", "Python312", "Scripts", "hf.exe");
        if (!File.Exists(launcher)) return new(false, "Qwen3-TTS 更新组件缺失，请运行安装程序修复。");
        var info = new ProcessStartInfo(launcher) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("download"); info.ArgumentList.Add(model.Repository!);
        info.ArgumentList.Add("--revision"); info.ArgumentList.Add(revision);
        info.ArgumentList.Add("--local-dir"); info.ArgumentList.Add(root);
        info.Environment["HF_XET_HIGH_PERFORMANCE"] = "1";
        info.Environment["HF_HUB_DOWNLOAD_TIMEOUT"] = "300";
        var result = await RunProcessAsync(info);
        if (result.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(result.Error) ? "模型更新失败" : result.Error);
        File.WriteAllText(Path.Combine(root, ".aurora-revision"), revision);
        return new(true, $"{model.Name} 已安装并校验", "current");
    }

    private static string ReadModelRevision(string root)
    {
        try
        {
            var path = Path.Combine(root, ".aurora-revision");
            if (File.Exists(path)) return File.ReadAllText(path).Trim();
            var downloadMetadata = Path.Combine(root, ".cache", "huggingface", "download", "model.safetensors.metadata");
            return File.Exists(downloadMetadata) ? File.ReadLines(downloadMetadata).FirstOrDefault()?.Trim() ?? "" : "";
        }
        catch { return ""; }
    }

    private string ReadInstalledVersion(string id)
    {
        var path = Path.Combine(settings.AppDataRoot, "Models", id + ".version");
        try { return File.Exists(path) ? File.ReadAllText(path).Trim() : ""; } catch { return ""; }
    }

    private void WriteInstalledVersion(string id, string version)
    {
        var folder = Path.Combine(settings.AppDataRoot, "Models");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, id + ".version"), version);
    }

    private static async Task<OperationResult> RunGitAsync(string root, string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Git could not start.");
            var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = (await outputTask).Trim(); var error = (await errorTask).Trim();
            return process.ExitCode == 0 ? new(true, string.IsNullOrWhiteSpace(output) ? "更新完成" : output, output) : new(false, string.IsNullOrWhiteSpace(error) ? "更新失败" : error);
        }
        catch (Exception ex) { return new(false, ex.Message); }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Process could not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
    }

    private static HttpClient CreateClient()
    {
        var value = new HttpClient { Timeout = TimeSpan.FromHours(8) };
        value.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora-Audio-Studio", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.1"));
        return value;
    }
}
