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
        if ((model.UpdateKind.Equals("huggingface", StringComparison.OrdinalIgnoreCase) || model.UpdateKind.Equals("minimax-music3", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(model.Repository))
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
        if (model.UpdateKind.Equals("uv-package", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await CheckPyPiPackageAsync(model, root);
        if (model.UpdateKind.StartsWith("git", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(root, ".git")))
        {
            var fetch = await RunGitAsync(root, "fetch --quiet origin");
            if (!fetch.Success) return fetch;
            var local = await RunGitAsync(root, "rev-parse HEAD");
            var remote = await RunGitAsync(root, "rev-parse @{u}");
            if (!local.Success || !remote.Success) return new(false, "暂时无法比较版本");
            return new(true, local.Path == remote.Path ? "已是最新版本" : "发现新版本", local.Path == remote.Path ? "current" : "available");
        }
        return new(false, model.UpdateKind switch
        {
            "python-tool" => "此固定运行组件随 Aurora 安装程序升级",
            "github-release" => "此独立程序需使用上游安装包升级",
            "direct" => "此固定模型需随 Aurora 安装程序升级",
            _ => "此组件暂不支持自动升级"
        }, "manual");
    }

    public async Task<OperationResult> UpdateAsync(ModelDefinition model, IProgress<ModelInstallProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (model.UpdateKind.Equals("minimax-music3", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await InstallMiniMaxMusic3Async(model, root, progress, cancellationToken);
        if (model.UpdateKind.Equals("huggingface", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await UpdateHuggingFaceAsync(model, root, progress, cancellationToken);
        if (model.UpdateKind.Equals("uv-package", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await InstallUvPackageAsync(model, root, progress, cancellationToken);
        var entry = await FindManifestEntryAsync(model.Id);
        if (entry is not null) return await InstallManifestEntryAsync(model, entry, root, progress, cancellationToken);
        if (!catalog.IsInstalled(model)) return new(false, "此引擎需要通过 Aurora 安装程序添加运行环境。 ");
        if (model.UpdateKind.StartsWith("git", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(root, ".git")))
            return await RunGitAsync(root, "pull --ff-only");
        return new(false, "当前没有可安装的新版本。");
    }

    private async Task<OperationResult> InstallUvPackageAsync(ModelDefinition model, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new(null, "正在检查模型部署组件"));
        var uv = ResolveUvExecutable();
        if (uv is null)
        {
            var installUv = new ProcessStartInfo("winget.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { "install", "--id", "astral-sh.uv", "-e", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity" }) installUv.ArgumentList.Add(value);
            var uvResult = await RunProcessAsync(installUv, cancellationToken, progress);
            if (uvResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(uvResult.Error) ? "无法安装模型部署组件 uv。" : uvResult.Error);
            uv = ResolveUvExecutable();
            if (uv is null) return new(false, "uv 已安装，但当前 Aurora 会话尚未找到它。请重新打开 Aurora 后重试。");
        }

        var python = Path.Combine(root, "Scripts", "python.exe");
        var environment = await EnsureUvEnvironmentAsync(uv, root, python, progress, cancellationToken);
        if (!environment.Success) return environment;
        if (model.Id.Equals("transkun", StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(new(null, "正在配置 TransKun CUDA 运行环境"));
            var torch = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { "pip", "install", "--upgrade", "--python", python, "torch", "torchaudio", "--index-url", "https://download.pytorch.org/whl/cu128" }) torch.ArgumentList.Add(value);
            var torchResult = await RunProcessAsync(torch, cancellationToken, progress);
            if (torchResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(torchResult.Error) ? "TransKun CUDA 环境配置失败。" : torchResult.Error);
        }
        var install = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "pip", "install", "--upgrade", "--python", python, model.Repository! }) install.ArgumentList.Add(value);
        progress?.Report(new(null, $"正在部署 {model.Name}"));
        var installResult = await RunProcessAsync(install, cancellationToken, progress);
        if (installResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(installResult.Error) ? $"{model.Name} 部署失败。" : installResult.Error);
        if (!catalog.IsInstalled(model)) return new(false, $"{model.Name} 已完成环境安装，但启动组件未通过完整性检查。");
        WriteInstalledVersion(model.Id, DateTime.UtcNow.ToString("yyyy.MM.dd"));
        return new(true, $"{model.Name} 已下载并部署完成", "current");
    }

    private async Task<OperationResult> CheckPyPiPackageAsync(ModelDefinition model, string root)
    {
        var python = Path.Combine(root, "Scripts", "python.exe");
        if (!File.Exists(python)) return new(false, "隔离运行环境不完整", "manual");
        var package = PyPiPackageName(model.Repository!);
        var localInfo = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        localInfo.ArgumentList.Add("-c");
        localInfo.ArgumentList.Add("import importlib.metadata as m,sys; print(m.version(sys.argv[1]))");
        localInfo.ArgumentList.Add(package);
        var local = await RunProcessAsync(localInfo);
        if (local.ExitCode != 0 || string.IsNullOrWhiteSpace(local.Output)) return new(false, "无法读取已安装版本", "manual");
        try
        {
            using var document = JsonDocument.Parse(await client.GetStringAsync($"https://pypi.org/pypi/{Uri.EscapeDataString(package)}/json"));
            var latest = document.RootElement.GetProperty("info").GetProperty("version").GetString() ?? "";
            var current = local.Output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "";
            var same = current.Equals(latest, StringComparison.OrdinalIgnoreCase);
            return new(true, same ? $"已是最新版本 {current}" : $"发现新版本 {latest}", same ? "current" : "available");
        }
        catch { return new(false, "暂时无法连接 PyPI"); }
    }

    public static string PyPiPackageName(string repository)
    {
        var end = repository.IndexOfAny(['[', '=', '<', '>', ' ', ';']);
        return (end < 0 ? repository : repository[..end]).Trim();
    }

    private static async Task<OperationResult> EnsureUvEnvironmentAsync(string uv, string root, string python, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (File.Exists(python)) return new(true, "隔离运行环境已就绪", "current");
        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        var create = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "venv", "--python", "3.11", root }) create.ArgumentList.Add(value);
        progress?.Report(new(null, "正在创建隔离运行环境"));
        var result = await RunProcessAsync(create, cancellationToken, progress);
        return result.ExitCode == 0 && File.Exists(python)
            ? new(true, "隔离运行环境已就绪", "current")
            : new(false, string.IsNullOrWhiteSpace(result.Error) ? "无法创建模型隔离环境。" : result.Error);
    }

    private async Task<OperationResult> InstallMiniMaxMusic3Async(ModelDefinition model, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        var uv = ResolveUvExecutable();
        if (uv is null)
        {
            progress?.Report(new(null, "正在安装模型部署组件 uv"));
            var installUv = new ProcessStartInfo("winget.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { "install", "--id", "astral-sh.uv", "-e", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity" }) installUv.ArgumentList.Add(value);
            var uvResult = await RunProcessAsync(installUv, cancellationToken, progress);
            if (uvResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(uvResult.Error) ? "无法安装模型部署组件 uv。" : uvResult.Error);
            uv = ResolveUvExecutable();
            if (uv is null) return new(false, "uv 已安装，但当前 Aurora 会话尚未找到它。请重新打开 Aurora 后重试。");
        }
        var environmentRoot = Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "minimax-music3-env");
        var python = Path.Combine(environmentRoot, "Scripts", "python.exe");
        var environment = await EnsureUvEnvironmentAsync(uv, environmentRoot, python, progress, cancellationToken);
        if (!environment.Success) return environment;

        progress?.Report(new(null, "正在配置 MiniMax-Music3 CUDA 运行环境"));
        var torch = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "pip", "install", "--upgrade", "--python", python, "torch", "torchaudio", "--index-url", "https://download.pytorch.org/whl/cu128" }) torch.ArgumentList.Add(value);
        var torchResult = await RunProcessAsync(torch, cancellationToken, progress);
        if (torchResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(torchResult.Error) ? "MiniMax-Music3 CUDA 环境配置失败。" : torchResult.Error);

        var dependencies = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "pip", "install", "--upgrade", "--python", python, "git+https://github.com/huggingface/diffusers@dafe3733fcfdbf3c48915fe77be3aef65b5d6a2d", "transformers", "accelerate", "soundfile", "gradio", "huggingface_hub[hf_xet]" }) dependencies.ArgumentList.Add(value);
        var dependencyResult = await RunProcessAsync(dependencies, cancellationToken, progress);
        if (dependencyResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(dependencyResult.Error) ? "MiniMax-Music3 依赖配置失败。" : dependencyResult.Error);

        var revision = await GetHuggingFaceRevisionAsync(model.Repository!);
        if (string.IsNullOrWhiteSpace(revision)) return new(false, "暂时无法读取 MiniMax-Music3 官方版本。");
        var hf = Path.Combine(environmentRoot, "Scripts", "hf.exe");
        if (!File.Exists(hf)) return new(false, "MiniMax-Music3 下载组件未正确安装。");
        ModelInstallTransaction.Prepare(root);
        var staging = ModelInstallTransaction.StagingPath(root);
        var download = new ProcessStartInfo(hf) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "download", model.Repository!, "--revision", revision, "--local-dir", staging }) download.ArgumentList.Add(value);
        download.ArgumentList.Add("--include");
        foreach (var include in new[] { "modular_model_index.json", "config.json", "condition_encoder/*", "language_model/*", "rvq_depth_decoder/*", "scheduler/*", "tokenizer/*", "transformer/*", "vocoder/*" })
            download.ArgumentList.Add(include);
        download.Environment["HF_XET_HIGH_PERFORMANCE"] = "1";
        download.Environment["HF_HUB_DOWNLOAD_TIMEOUT"] = "300";
        progress?.Report(new(null, "正在下载 MiniMax-Music3 官方模型（约 27 GB）"));
        var result = await RunProcessAsync(download, cancellationToken, progress);
        if (result.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(result.Error) ? "MiniMax-Music3 下载失败。" : result.Error);
        if (!File.Exists(Path.Combine(staging, model.Marker)) || !Directory.Exists(Path.Combine(staging, "language_model")) || !Directory.Exists(Path.Combine(staging, "transformer")))
            return new(false, "MiniMax-Music3 下载完成，但完整性检查未通过。正式模型目录未被修改。");
        File.WriteAllText(Path.Combine(staging, ".aurora-revision"), revision);
        ModelInstallTransaction.Commit(root);
        WriteInstalledVersion(model.Id, revision);
        return new(true, "MiniMax-Music3 已安装、自动配置并可在音乐创作中启用", "current");
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

    private async Task<OperationResult> InstallManifestEntryAsync(ModelDefinition model, ModelUpdateEntry entry, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.Url) || entry.Sha256.Length != 64) return new(false, "更新清单不完整，已停止更新。");
        var folder = Path.Combine(settings.UpdatesRoot, "Models", model.Id, entry.Version);
        Directory.CreateDirectory(folder);
        var package = Path.Combine(folder, entry.PackageKind.Equals("zip", StringComparison.OrdinalIgnoreCase) ? "package.zip" : "package.bin");
        await DownloadFileAsync(entry.Url, package, progress, cancellationToken);
        progress?.Report(new(null, "正在校验模型包完整性"));
        await using (var stream = File.OpenRead(package))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!actual.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)) { File.Delete(package); return new(false, "更新包校验失败，已安全停止。"); }
        }
        ModelInstallTransaction.Prepare(root);
        var installRoot = ModelInstallTransaction.StagingPath(root);
        progress?.Report(new(null, "正在安装模型文件"));
        if (entry.PackageKind.Equals("zip", StringComparison.OrdinalIgnoreCase)) ZipFile.ExtractToDirectory(package, installRoot, true);
        else
        {
            if (string.IsNullOrWhiteSpace(entry.RelativePath)) return new(false, "更新清单缺少安装位置。");
            var destination = Path.GetFullPath(Path.Combine(installRoot, entry.RelativePath));
            var installBoundary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot)) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(installBoundary, StringComparison.OrdinalIgnoreCase)) return new(false, "更新路径不安全，已停止更新。");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(package, destination, true);
        }
        if (!File.Exists(Path.Combine(installRoot, model.Marker))) return new(false, "模型包已解压，但完整性检查未通过。正式模型目录未被修改。");
        ModelInstallTransaction.Commit(root);
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

    private async Task<OperationResult> UpdateHuggingFaceAsync(ModelDefinition model, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new(null, "正在读取模型版本"));
        var revision = await GetHuggingFaceRevisionAsync(model.Repository!);
        if (string.IsNullOrWhiteSpace(revision)) return new(false, "暂时无法连接 Hugging Face");
        return await DownloadHuggingFaceRevisionAsync(model, root, revision, progress, cancellationToken);
    }

    public async Task<OperationResult> RollbackAsync(ModelDefinition model)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        try
        {
            if (ModelInstallTransaction.RestorePrevious(root))
            {
                var restoredRevision = ReadModelRevision(root);
                if (!string.IsNullOrWhiteSpace(restoredRevision)) WriteInstalledVersion(model.Id, restoredRevision);
                return new(true, $"{model.Name} 已恢复到上一个可用版本。", "current");
            }
        }
        catch (Exception ex) { return new(false, $"恢复失败：{ex.Message}"); }
        var snapshot = Path.Combine(root, ".aurora-previous-revision");
        if (!File.Exists(snapshot) || string.IsNullOrWhiteSpace(model.Repository)) return new(false, "此模型还没有可恢复的版本快照。 ");
        var revision = File.ReadAllText(snapshot).Trim();
        return await DownloadHuggingFaceRevisionAsync(model, root, revision, null, CancellationToken.None);
    }

    private async Task<OperationResult> DownloadHuggingFaceRevisionAsync(ModelDefinition model, string root, string revision, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        var launcher = Path.Combine(settings.Current.LocalAiRoot, "Qwen3-TTS", "Python312", "Scripts", "hf.exe");
        if (!File.Exists(launcher)) return new(false, "Qwen3-TTS 更新组件缺失，请运行安装程序修复。");
        var info = new ProcessStartInfo(launcher) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("download"); info.ArgumentList.Add(model.Repository!);
        info.ArgumentList.Add("--revision"); info.ArgumentList.Add(revision);
        ModelInstallTransaction.Prepare(root);
        var staging = ModelInstallTransaction.StagingPath(root);
        info.ArgumentList.Add("--local-dir"); info.ArgumentList.Add(staging);
        info.Environment["HF_XET_HIGH_PERFORMANCE"] = "1";
        info.Environment["HF_HUB_DOWNLOAD_TIMEOUT"] = "300";
        File.WriteAllText(Path.Combine(staging, ".aurora-installing"), DateTimeOffset.UtcNow.ToString("O"));
        progress?.Report(new(null, $"正在从 Hugging Face 下载 {model.Name}"));
        var result = await RunProcessAsync(info, cancellationToken, progress);
        if (result.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(result.Error) ? "模型更新失败" : result.Error);
        if (!File.Exists(Path.Combine(staging, model.Marker))) return new(false, "下载已结束，但模型完整性检查未通过。正式模型目录未被修改。");
        File.WriteAllText(Path.Combine(staging, ".aurora-revision"), revision);
        File.Delete(Path.Combine(staging, ".aurora-installing"));
        ModelInstallTransaction.Commit(root);
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

    private async Task DownloadFileAsync(string url, string destination, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        var partial = destination + ".part";
        var existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var resumed = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
        if (!resumed) existing = 0;
        long? total = response.Content.Headers.ContentLength is { } length ? length + existing : null;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(partial, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 1024, true);
        var buffer = new byte[1024 * 1024];
        var received = existing;
        var watch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (watch.Elapsed - lastReport < TimeSpan.FromMilliseconds(180)) continue;
            lastReport = watch.Elapsed;
            var speed = (received - existing) / Math.Max(.1, watch.Elapsed.TotalSeconds);
            progress?.Report(new(total is > 0 ? received * 100d / total.Value : null, "正在下载模型包", received, total, speed));
        }
        await output.FlushAsync(cancellationToken);
        File.Move(partial, destination, true);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(ProcessStartInfo info, CancellationToken cancellationToken = default, IProgress<ModelInstallProgress>? progress = null)
    {
        using var process = new Process { StartInfo = info };
        var output = new List<string>();
        var errors = new List<string>();
        var sync = new object();
        process.OutputDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) { lock (sync) output.Add(args.Data); progress?.Report(new(null, LogLine(args.Data))); } };
        process.ErrorDataReceived += (_, args) => { if (!string.IsNullOrWhiteSpace(args.Data)) { lock (sync) errors.Add(args.Data); progress?.Report(new(null, LogLine(args.Data))); } };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        await process.WaitForExitAsync(cancellationToken);
        await Task.Delay(60, CancellationToken.None);
        lock (sync) return (process.ExitCode, string.Join(Environment.NewLine, output), string.Join(Environment.NewLine, errors));
    }

    private static string LogLine(string value) => value.Length > 180 ? value[..180] : value;

    private static HttpClient CreateClient()
    {
        var value = new HttpClient { Timeout = TimeSpan.FromHours(8) };
        value.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora-Audio-Studio", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.4.0"));
        return value;
    }
}
