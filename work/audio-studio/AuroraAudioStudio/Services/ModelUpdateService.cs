using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class ModelUpdateService(ModelCatalogService catalog, SettingsService settings)
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/swy2018/Aurora-Audio-Studio/main/model-manifest.json";
    private readonly HttpClient client = CreateClient();
    public string? FindRunningProcess(ModelDefinition model)
        => RunningProcessGuard.FindInRoot(Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot));

    public async Task<OperationResult> CheckAsync(ModelDefinition model)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (!catalog.IsInstalled(model)) return new(false, "尚未安装");
        if (model.UpdateKind.Equals("fixed-file", StringComparison.OrdinalIgnoreCase))
            return await CheckFixedFileAsync(model, root);
        if (model.UpdateKind.Equals("roformer-registry", StringComparison.OrdinalIgnoreCase))
            return new(true, "已安装；模型校验由 BS-RoFormer 官方注册表管理", "current");
        if (model.UpdateKind.Equals("github-release", StringComparison.OrdinalIgnoreCase))
            return await CheckGitHubReleaseAsync(model, root);
        if (model.UpdateKind.Equals("github-release-git", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await CheckGitHubRepositoryReleaseAsync(model, root);
        if ((model.UpdateKind.Equals("huggingface", StringComparison.OrdinalIgnoreCase) || model.UpdateKind.Equals("minimax-music3", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(model.Repository))
        {
            var remoteVersion = await GetHuggingFaceVersionAsync(model.Repository);
            if (remoteVersion is null) return new(false, "暂时无法连接 Hugging Face");
            var localRevision = ReadModelRevision(root);
            var current = localRevision.Equals(remoteVersion.Revision, StringComparison.OrdinalIgnoreCase);
            return new(true, current ? $"已是最新日期版 {remoteVersion.DateVersion}" : $"发现日期版 {remoteVersion.DateVersion}",
                current ? "current" : "available");
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
        if (model.UpdateKind.Equals("fixed-file", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await InstallFixedFileAsync(model, root, progress, cancellationToken);
        if (model.UpdateKind.Equals("roformer-registry", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await InstallRoformerRegistryModelAsync(model, root, progress, cancellationToken);
        if (model.UpdateKind.Equals("github-release", StringComparison.OrdinalIgnoreCase))
            return await InstallGitHubReleaseAsync(model, root, progress, cancellationToken);
        if (model.UpdateKind.Equals("github-release-git", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(model.Repository))
            return await InstallGitHubRepositoryReleaseAsync(model, root, cancellationToken);
        var entry = await FindManifestEntryAsync(model.Id);
        if (entry is not null) return await InstallManifestEntryAsync(model, entry, root, progress, cancellationToken);
        if (!catalog.IsInstalled(model)) return new(false, "此引擎需要通过 Aurora 安装程序添加运行环境。 ");
        if (model.UpdateKind.StartsWith("git", StringComparison.OrdinalIgnoreCase) && Directory.Exists(Path.Combine(root, ".git")))
            return await RunGitAsync(root, "pull --ff-only");
        return new(false, "当前没有可安装的新版本。");
    }

    private const string PianoCheckpointSha256 = "C3FA9730725BF4A762F1C14BC80CD5986EACDA01B026F5A4A2525CD607876141";

    private async Task<OperationResult> CheckFixedFileAsync(ModelDefinition model, string root)
    {
        var path = Path.Combine(root, model.Marker);
        try
        {
            await using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            return actual.Equals(PianoCheckpointSha256, StringComparison.OrdinalIgnoreCase)
                ? new(true, "已是上游固定版本，校验通过", "current")
                : new(true, "文件校验异常，可自动修复", "available");
        }
        catch (Exception ex) { return new(false, $"无法校验固定模型：{ex.Message}", "available"); }
    }

    private async Task<OperationResult> InstallFixedFileAsync(ModelDefinition model, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        ModelInstallTransaction.Prepare(root);
        var staging = ModelInstallTransaction.StagingPath(root);
        var destination = Path.Combine(staging, model.Marker);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await DownloadFileAsync(model.Repository!, destination, progress, cancellationToken);
        progress?.Report(new(null, "正在校验官方模型文件"));
        await using (var stream = File.OpenRead(destination))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!actual.Equals(PianoCheckpointSha256, StringComparison.OrdinalIgnoreCase))
                return new(false, "官方模型文件校验失败，正式模型目录未被修改。");
        }
        ModelInstallTransaction.Commit(root);
        WriteInstalledVersion(model.Id, "Zenodo 4034264");
        return new(true, $"{model.Name} 已安装并通过 SHA-256 校验", "current");
    }

    private async Task<OperationResult> InstallRoformerRegistryModelAsync(ModelDefinition model, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        var downloader = Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "roformer-env", "Scripts", "bs-roformer-download.exe");
        if (!File.Exists(downloader)) return new(false, "请先在模型中心安装 BS-RoFormer-SW 运行环境。");
        var modelsRoot = Path.GetDirectoryName(root)!;
        var info = new ProcessStartInfo(downloader) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "--output-dir", modelsRoot, "--model", model.Repository! }) info.ArgumentList.Add(value);
        progress?.Report(new(null, $"正在从 BS-RoFormer 官方注册表下载 {model.Name}"));
        var result = await RunProcessAsync(info, cancellationToken, progress);
        if (result.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(result.Error) ? "分轨模型下载失败。" : result.Error);
        if (!catalog.IsInstalled(model)) return new(false, "下载已结束，但模型校验未通过。");
        WriteInstalledVersion(model.Id, "registry");
        return new(true, $"{model.Name} 已安装并通过上游校验", "current");
    }

    private sealed record GitHubReleaseAsset(string Version, string Name, string Url, long Size, string? Digest, string PackageKind);
    private sealed record GitHubRepositoryRelease(string Owner, string Repository, string Tag, string Branch, string Commit, string DateVersion);

    private async Task<OperationResult> CheckGitHubRepositoryReleaseAsync(ModelDefinition model, string root)
    {
        var release = await GetGitHubRepositoryReleaseAsync(model.Repository!);
        if (release is null) return new(false, "暂时无法读取 GitHub 上游版本");
        var local = await RunGitAsync(root, "rev-parse HEAD");
        if (!local.Success || string.IsNullOrWhiteSpace(local.Path)) return new(false, $"暂时无法读取已安装的 {model.Name} 版本");
        if (string.IsNullOrWhiteSpace(release.Tag))
        {
            var current = local.Path.Equals(release.Commit, StringComparison.OrdinalIgnoreCase);
            return new(true, current ? $"已是最新日期版 {release.DateVersion}" : $"发现日期版 {release.DateVersion}", current ? "current" : "available");
        }
        try
        {
            var compareUrl = $"https://api.github.com/repos/{release.Owner}/{release.Repository}/compare/{local.Path}...{Uri.EscapeDataString(release.Tag)}";
            using var document = JsonDocument.Parse(await client.GetStringAsync(compareUrl));
            var status = document.RootElement.GetProperty("status").GetString();
            var available = status is "ahead" or "diverged";
            return new(true, available ? $"发现正式版 {release.Tag}" : $"已是最新正式版 {release.Tag}", available ? "available" : "current");
        }
        catch { return new(false, $"暂时无法比较 {model.Name} 正式版"); }
    }

    private async Task<OperationResult> InstallGitHubRepositoryReleaseAsync(ModelDefinition model, string root, CancellationToken cancellationToken)
    {
        var release = await GetGitHubRepositoryReleaseAsync(model.Repository!);
        if (release is null) return new(false, "暂时无法读取 GitHub 上游版本");
        var dirty = await RunGitAsync(root, "status --porcelain --untracked-files=no");
        if (!dirty.Success) return dirty;
        if (!string.IsNullOrWhiteSpace(dirty.Path)) return new(false, $"{model.Name} 存在本地代码修改，为避免覆盖，已停止更新。", "available");
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(release.Tag))
        {
            var fetchDate = await RunGitAsync(root, $"fetch --quiet --force {model.Repository} refs/heads/{release.Branch}");
            if (!fetchDate.Success) return fetchDate;
            cancellationToken.ThrowIfCancellationRequested();
            var checkoutDate = await RunGitAsync(root, $"checkout --detach {release.Commit}");
            if (!checkoutDate.Success) return checkoutDate;
            File.WriteAllText(Path.Combine(root, ".aurora-revision"), release.Commit);
            File.WriteAllText(Path.Combine(root, ".aurora-version"), release.DateVersion);
            WriteInstalledVersion(model.Id, release.DateVersion);
            return new(true, $"{model.Name} 已更新至日期版 {release.DateVersion}", "current");
        }
        var fetch = await RunGitAsync(root, $"fetch --quiet --force {model.Repository} refs/tags/{release.Tag}:refs/tags/{release.Tag}");
        if (!fetch.Success) return fetch;
        cancellationToken.ThrowIfCancellationRequested();
        var checkout = await RunGitAsync(root, $"checkout --detach refs/tags/{release.Tag}");
        if (!checkout.Success) return checkout;
        File.WriteAllText(Path.Combine(root, ".aurora-version"), release.Tag);
        WriteInstalledVersion(model.Id, release.Tag);
        return new(true, $"{model.Name} 已更新至正式版 {release.Tag}", "current");
    }

    private async Task<GitHubRepositoryRelease?> GetGitHubRepositoryReleaseAsync(string repositoryUrl)
    {
        try
        {
            var uri = new Uri(repositoryUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length != 2) return null;
            var owner = segments[0];
            var repository = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
            var apiRoot = $"https://api.github.com/repos/{owner}/{repository}";
            using var response = await client.GetAsync(apiRoot + "/releases/latest");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                using var repositoryDocument = JsonDocument.Parse(await client.GetStringAsync(apiRoot));
                var branch = repositoryDocument.RootElement.GetProperty("default_branch").GetString();
                if (string.IsNullOrWhiteSpace(branch)) return null;
                using var commitDocument = JsonDocument.Parse(await client.GetStringAsync(apiRoot + "/commits/" + Uri.EscapeDataString(branch)));
                var commit = commitDocument.RootElement.GetProperty("sha").GetString();
                var dateText = commitDocument.RootElement.GetProperty("commit").GetProperty("committer").GetProperty("date").GetString();
                if (string.IsNullOrWhiteSpace(commit) || !DateTimeOffset.TryParse(dateText, out var date)) return null;
                return new(owner, repository, "", branch, commit, date.UtcDateTime.ToString("yyyy.MM.dd"));
            }
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = document.RootElement.GetProperty("tag_name").GetString();
            return string.IsNullOrWhiteSpace(tag) ? null : new(owner, repository, tag, "", "", "");
        }
        catch { return null; }
    }

    private async Task<OperationResult> CheckGitHubReleaseAsync(ModelDefinition model, string root)
    {
        var release = await GetGitHubReleaseAssetAsync(model);
        if (release is null) return new(false, "暂时无法读取 GitHub Release");
        var local = InstalledFileVersion(Path.Combine(root, model.Marker));
        var same = VersionsEqual(local, release.Version);
        return new(true, same ? $"已是最新版本 {release.Version}" : $"发现新版本 {release.Version}", same ? "current" : "available");
    }

    private async Task<OperationResult> InstallGitHubReleaseAsync(ModelDefinition model, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        var release = await GetGitHubReleaseAssetAsync(model);
        if (release is null) return new(false, "暂时无法读取 GitHub Release");
        var folder = Path.Combine(settings.UpdatesRoot, "Models", model.Id, release.Version);
        Directory.CreateDirectory(folder);
        var package = Path.Combine(folder, release.Name);
        await DownloadFileAsync(release.Url, package, progress, cancellationToken, release.Size);
        var packageInfo = new FileInfo(package);
        if (packageInfo.Length != release.Size) return new(false, "下载文件大小与 GitHub Release 不一致，已停止更新。");
        if (!string.IsNullOrWhiteSpace(release.Digest))
        {
            progress?.Report(new(null, "正在校验 GitHub Release 摘要"));
            await using var stream = File.OpenRead(package);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            if (!actual.Equals(release.Digest, StringComparison.OrdinalIgnoreCase))
                return new(false, "GitHub Release SHA-256 校验失败，正式目录未被修改。");
        }

        try
        {
            ModelInstallTransaction.Prepare(root);
        }
        catch (IOException)
        {
            return new(false, $"{model.Name} 正在运行或文件被占用。请保存并关闭后重试；Aurora 不会强制关闭它。", "available");
        }
        var staging = ModelInstallTransaction.StagingPath(root);
        progress?.Report(new(null, $"正在安装 {model.Name}"));
        if (release.PackageKind == "zip")
        {
            ZipFile.ExtractToDirectory(package, staging, true);
        }
        else
        {
            var tar = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "tar.exe");
            if (!File.Exists(tar)) return new(false, "Windows 解压组件缺失，无法安装 7z 更新包。");
            var extract = new ProcessStartInfo(tar) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { "-xf", package, "-C", staging }) extract.ArgumentList.Add(value);
            var result = await RunProcessAsync(extract, cancellationToken, progress);
            if (result.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(result.Error) ? "更新包解压失败。" : result.Error);
        }

        var marker = Path.Combine(staging, model.Marker);
        if (!File.Exists(marker) && !NormalizeNestedPackageRoot(staging, model.Marker))
            return new(false, "更新包已解压，但完整性检查未通过。正式目录未被修改。");
        if (model.Id.Equals("subtitle-edit", StringComparison.OrdinalIgnoreCase))
        {
            var oldSettings = Path.Combine(root, "Settings.json");
            var newSettings = Path.Combine(staging, "Settings.json");
            if (File.Exists(oldSettings) && !File.Exists(newSettings)) File.Copy(oldSettings, newSettings);
        }
        try
        {
            ModelInstallTransaction.Commit(root);
        }
        catch (IOException)
        {
            return new(false, $"{model.Name} 正在运行或文件被占用。请保存并关闭后重试；Aurora 不会强制关闭它。", "available");
        }
        WriteInstalledVersion(model.Id, release.Version);
        return new(true, $"{model.Name} 已更新至 {release.Version}", "current");
    }

    private async Task<GitHubReleaseAsset?> GetGitHubReleaseAssetAsync(ModelDefinition model)
    {
        try
        {
            var endpoint = model.Id.Equals("subtitle-edit", StringComparison.OrdinalIgnoreCase)
                ? "https://api.github.com/repos/SubtitleEdit/subtitleedit/releases/latest"
                : "https://api.github.com/repos/Purfview/whisper-standalone-win/releases/tags/Faster-Whisper-XXL";
            using var document = JsonDocument.Parse(await client.GetStringAsync(endpoint));
            var assets = document.RootElement.GetProperty("assets").EnumerateArray().ToArray();
            JsonElement asset;
            string version;
            string kind;
            if (model.Id.Equals("subtitle-edit", StringComparison.OrdinalIgnoreCase))
            {
                asset = assets.First(x => x.GetProperty("name").GetString() == "SubtitleEdit-Windows-x64.zip");
                version = (document.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                kind = "zip";
            }
            else
            {
                var candidates = assets.Select(x => new
                {
                    Asset = x,
                    Name = x.GetProperty("name").GetString() ?? "",
                    Match = Regex.Match(x.GetProperty("name").GetString() ?? "", @"_r(?<version>\d+(?:\.\d+)+)_windows\.7z$", RegexOptions.IgnoreCase)
                })
                    .Where(x => x.Match.Success)
                    .Select(x => new { x.Asset, x.Name, Version = Version.Parse(x.Match.Groups["version"].Value) })
                    .OrderByDescending(x => x.Version)
                    .ToList();
                if (candidates.Count == 0) return null;
                asset = candidates[0].Asset;
                version = candidates[0].Version.ToString();
                kind = "7z";
            }
            var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
            if (digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true) digest = digest[7..];
            return new(version, asset.GetProperty("name").GetString()!, asset.GetProperty("browser_download_url").GetString()!,
                asset.GetProperty("size").GetInt64(), digest, kind);
        }
        catch { return null; }
    }

    private static bool NormalizeNestedPackageRoot(string staging, string marker)
    {
        var found = Directory.EnumerateFiles(staging, Path.GetFileName(marker), SearchOption.AllDirectories)
            .FirstOrDefault(path => Path.GetRelativePath(Path.GetDirectoryName(path)!, path).Equals(marker, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Equals(Path.GetFileName(marker), StringComparison.OrdinalIgnoreCase));
        if (found is null) return false;
        var packageRoot = Path.GetDirectoryName(found)!;
        while (!Path.GetRelativePath(packageRoot, found).Equals(marker, StringComparison.OrdinalIgnoreCase)
            && !packageRoot.Equals(staging, StringComparison.OrdinalIgnoreCase))
            packageRoot = Path.GetDirectoryName(packageRoot)!;
        if (packageRoot.Equals(staging, StringComparison.OrdinalIgnoreCase)) return File.Exists(Path.Combine(staging, marker));
        var normalized = staging + ".normalized";
        if (Directory.Exists(normalized)) Directory.Delete(normalized, true);
        Directory.Move(packageRoot, normalized);
        Directory.Delete(staging, true);
        Directory.Move(normalized, staging);
        return File.Exists(Path.Combine(staging, marker));
    }
    private static string InstalledFileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return info.ProductVersion ?? info.FileVersion ?? "";
        }
        catch { return ""; }
    }

    private static bool VersionsEqual(string local, string remote)
    {
        static string Normalize(string value)
        {
            var match = Regex.Match(value ?? "", @"\d+(?:\.\d+)+");
            if (!match.Success) return "";
            return Version.TryParse(match.Value, out var parsed) ? parsed.ToString() : match.Value;
        }
        return Normalize(local).Equals(Normalize(remote), StringComparison.OrdinalIgnoreCase);
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
        if (model.Id.Equals("roformer", StringComparison.OrdinalIgnoreCase))
        {
            var downloader = Path.Combine(root, "Scripts", "bs-roformer-download.exe");
            var modelsRoot = Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "roformer-models");
            Directory.CreateDirectory(modelsRoot);
            var assets = new ProcessStartInfo(downloader) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var value in new[] { "--output-dir", modelsRoot, "--model", "roformer-model-bs-roformer-sw-by-jarredou" }) assets.ArgumentList.Add(value);
            assets.Environment["PYTHONUTF8"] = "1";
            progress?.Report(new(null, "正在下载 BS-RoFormer-SW 多轨权重"));
            var assetResult = await RunProcessAsync(assets, cancellationToken, progress);
            if (assetResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(assetResult.Error) ? "BS-RoFormer-SW 权重下载失败。" : assetResult.Error);
        }
        if (model.Id.Equals("yourmt3", StringComparison.OrdinalIgnoreCase))
        {
            var downloader = Path.Combine(root, "Scripts", "mt3-infer.exe");
            var modelsRoot = Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "mt3-models");
            Directory.CreateDirectory(modelsRoot);
            var assets = new ProcessStartInfo(downloader) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            assets.ArgumentList.Add("download"); assets.ArgumentList.Add("yourmt3");
            assets.Environment["MT3_CHECKPOINT_DIR"] = modelsRoot;
            assets.Environment["PYTHONUTF8"] = "1";
            progress?.Report(new(null, "正在下载 YourMT3+ 多乐器权重"));
            var assetResult = await RunProcessAsync(assets, cancellationToken, progress);
            if (assetResult.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(assetResult.Error) ? "YourMT3+ 权重下载失败。" : assetResult.Error);
        }
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

        var version = await GetHuggingFaceVersionAsync(model.Repository!);
        if (version is null) return new(false, "暂时无法读取 MiniMax-Music3 官方版本。");
        var hf = Path.Combine(environmentRoot, "Scripts", "hf.exe");
        if (!File.Exists(hf)) return new(false, "MiniMax-Music3 下载组件未正确安装。");
        ModelInstallTransaction.Prepare(root);
        var staging = ModelInstallTransaction.StagingPath(root);
        var download = new ProcessStartInfo(hf) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var value in new[] { "download", model.Repository!, "--revision", version.Revision, "--local-dir", staging }) download.ArgumentList.Add(value);
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
        File.WriteAllText(Path.Combine(staging, ".aurora-revision"), version.Revision);
        File.WriteAllText(Path.Combine(staging, ".aurora-version"), version.DateVersion);
        ModelInstallTransaction.Commit(root);
        WriteInstalledVersion(model.Id, version.DateVersion);
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

    private sealed record HuggingFaceVersion(string Revision, string DateVersion);

    private async Task<HuggingFaceVersion?> GetHuggingFaceVersionAsync(string repository)
    {
        try
        {
            var json = await client.GetStringAsync("https://huggingface.co/api/models/" + repository);
            using var document = JsonDocument.Parse(json);
            var revision = document.RootElement.TryGetProperty("sha", out var sha) ? sha.GetString() : null;
            var modified = document.RootElement.TryGetProperty("lastModified", out var lastModified) ? lastModified.GetString() : null;
            if (string.IsNullOrWhiteSpace(revision) || !DateTimeOffset.TryParse(modified, out var date)) return null;
            return new(revision, date.UtcDateTime.ToString("yyyy.MM.dd"));
        }
        catch { return null; }
    }

    private async Task<OperationResult> UpdateHuggingFaceAsync(ModelDefinition model, string root, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new(null, "正在读取模型版本"));
        var version = await GetHuggingFaceVersionAsync(model.Repository!);
        if (version is null) return new(false, "暂时无法连接 Hugging Face");
        return await DownloadHuggingFaceRevisionAsync(model, root, version.Revision, progress, cancellationToken, version.DateVersion);
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

    private async Task<OperationResult> DownloadHuggingFaceRevisionAsync(ModelDefinition model, string root, string revision, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken, string? dateVersion = null)
    {
        var launcher = Path.Combine(settings.Current.LocalAiRoot, "Qwen3-TTS", "Python312", "Scripts", "hf.exe");
        if (!File.Exists(launcher)) return new(false, "Qwen3-TTS 更新组件缺失，请运行安装程序修复。");
        var info = new ProcessStartInfo(launcher) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("download"); info.ArgumentList.Add(model.Repository!);
        info.ArgumentList.Add("--revision"); info.ArgumentList.Add(revision);
        ModelInstallTransaction.Prepare(root);
        var staging = ModelInstallTransaction.StagingPath(root);
        info.ArgumentList.Add("--local-dir"); info.ArgumentList.Add(staging);
        if (model.Id.Equals("soulx-singer-svc", StringComparison.OrdinalIgnoreCase))
        {
            info.ArgumentList.Add("--include");
            foreach (var include in new[] { "model-svc.pt", "config.yaml", "README.md" }) info.ArgumentList.Add(include);
        }
        info.Environment["HF_XET_HIGH_PERFORMANCE"] = "1";
        info.Environment["HF_HUB_DOWNLOAD_TIMEOUT"] = "300";
        File.WriteAllText(Path.Combine(staging, ".aurora-installing"), DateTimeOffset.UtcNow.ToString("O"));
        progress?.Report(new(null, $"正在从 Hugging Face 下载 {model.Name}"));
        var result = await RunProcessAsync(info, cancellationToken, progress);
        if (result.ExitCode != 0) return new(false, string.IsNullOrWhiteSpace(result.Error) ? "模型更新失败" : result.Error);
        if (!File.Exists(Path.Combine(staging, model.Marker))) return new(false, "下载已结束，但模型完整性检查未通过。正式模型目录未被修改。");
        File.WriteAllText(Path.Combine(staging, ".aurora-revision"), revision);
        if (!string.IsNullOrWhiteSpace(dateVersion)) File.WriteAllText(Path.Combine(staging, ".aurora-version"), dateVersion);
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

    private async Task DownloadFileAsync(string url, string destination, IProgress<ModelInstallProgress>? progress, CancellationToken cancellationToken, long? expectedLength = null)
    {
        var partial = destination + ".part";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable && existing > 0)
            {
                var serverLength = response.Content.Headers.ContentRange?.Length;
                if (DownloadResumeGuard.CanPromotePartial(response.StatusCode, existing, expectedLength ?? serverLength))
                {
                    File.Move(partial, destination, true);
                    return;
                }
                File.Delete(partial);
                continue;
            }
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
            return;
        }
        throw new IOException("服务器拒绝了断点位置，重新下载仍未成功。");
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
        value.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora-Audio-Studio", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.0"));
        return value;
    }
}
