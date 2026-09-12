using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public enum ModelRepairKind { Healthy, RuntimeOnly, MissingFiles, FullRedeploy, Blocked }
public sealed record RepairFile(string RelativePath, long Size, string Sha256);
public sealed record ModelRepairPlan(ModelRepairKind Kind, string Root, string Detail,
    IReadOnlyList<RepairFile> Files, string Revision = "")
{
    public long DownloadBytes => Files.Sum(x => x.Size);
}

public sealed partial class ModelUpdateService
{
    public async Task<ModelRepairPlan> InspectRepairAsync(ModelDefinition model, CancellationToken token = default)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (FindRunningProcess(model) is { } running) return new(ModelRepairKind.Blocked, root, running, []);
        var missing = ModelHealthPolicy.MissingRequirements(model, settings.Current.LocalAiRoot);
        if (missing.Count == 0) return new(ModelRepairKind.Healthy, root, "文件校验通过；不会重新下载或重装。", []);
        var detail = string.Join("、", missing);
        if ((model.UpdateKind == "uv-package" && !missing.Any(x => x.Contains("权重") || x.Contains("配置")))
            || (model.Id.StartsWith("qwen3-tts-", StringComparison.Ordinal) && missing.All(x => x is "Qwen3-TTS 启动器" or "Qwen3-TTS SoX 音频组件")))
            return new(ModelRepairKind.RuntimeOnly, root, detail, []);
        var revision = ReadModelRevision(root);
        if (model.UpdateKind != "huggingface" || !Regex.IsMatch(revision, "^[a-fA-F0-9]{40}$"))
            return new(ModelRepairKind.FullRedeploy, root, detail, []);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            // Hub model_info(files_metadata=True): pinned revision, LFS sizes and hashes.
            using var document = JsonDocument.Parse(await metadataClient.GetStringAsync(
                $"https://huggingface.co/api/models/{model.Repository}/revision/{revision}?blobs=true", timeout.Token));
            if (document.RootElement.GetProperty("sha").GetString() != revision) throw new InvalidDataException("模型版本不匹配。");
            var required = new HashSet<string>(StringComparer.Ordinal) { model.Marker.Replace('\\', '/') };
            if (Directory.Exists(root))
                foreach (var index in Directory.EnumerateFiles(root, "*.index.json", SearchOption.AllDirectories))
                {
                    using var weights = JsonDocument.Parse(await File.ReadAllTextAsync(index, timeout.Token));
                    if (weights.RootElement.TryGetProperty("weight_map", out var map))
                        foreach (var entry in map.EnumerateObject())
                            required.Add(Path.GetRelativePath(root, Path.Combine(Path.GetDirectoryName(index)!, entry.Value.GetString()!)).Replace('\\', '/'));
                }
            var files = new List<RepairFile>();
            foreach (var file in document.RootElement.GetProperty("siblings").EnumerateArray())
            {
                var name = file.GetProperty("rfilename").GetString()!;
                if (!required.Contains(name)) continue;
                var destination = RepairDestination(root, name);
                if (File.Exists(destination) && new FileInfo(destination).Length > 0) continue;
                if (!file.TryGetProperty("lfs", out var lfs) || lfs.ValueKind != JsonValueKind.Object) continue;
                var hash = lfs.GetProperty("sha256").GetString()!;
                if (!Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$")) continue;
                files.Add(new(name, lfs.GetProperty("size").GetInt64(), hash));
            }
            // Never mistake a broken runtime or index for missing weight blobs.
            if (files.Count > 0 && missing.All(x => x == "模型标记" || x.StartsWith("模型分片 ", StringComparison.Ordinal)))
                return new(ModelRepairKind.MissingFiles, root, detail, files, revision);
            return new(ModelRepairKind.FullRedeploy, root, detail, []);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(ModelRepairKind.Blocked, root, "无法确认修复清单，未更改任何文件：" + ex.Message, []); }
    }

    public async Task<OperationResult> RepairAsync(ModelDefinition model, ModelRepairPlan plan,
        IProgress<ModelInstallProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Path.GetFullPath(plan.Root) != Path.GetFullPath(Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot)))
            return new(false, "模型目录已改变，请重新检查。");
        if (FindRunningProcess(model) is { } running) return new(false, running + " 正在使用此模型。");
        if (plan.Kind == ModelRepairKind.Blocked) return new(false, plan.Detail);
        if (plan.Kind == ModelRepairKind.Healthy) return new(true, plan.Detail, "current");
        if (plan.Kind == ModelRepairKind.RuntimeOnly)
        {
            progress?.Report(new(null, "仅修复运行环境，保留模型权重。"));
            return model.Id.StartsWith("qwen3-tts-", StringComparison.Ordinal)
                ? await EnsureQwenTtsRuntimeAsync(progress, token)
                : await InstallUvPackageAsync(model, plan.Root, progress, token, includeWeights: false);
        }
        if (plan.Kind == ModelRepairKind.FullRedeploy) return await UpdateAsync(model, progress, token);
        if (ReadModelRevision(plan.Root) != plan.Revision) return new(false, "模型版本已改变，请重新检查。");
        var drive = new DriveInfo(Path.GetPathRoot(settings.UpdatesRoot)!);
        if (drive.AvailableFreeSpace < plan.DownloadBytes) return new(false, "磁盘空间不足，未开始修复。");
        var staging = Path.Combine(settings.UpdatesRoot, "Repairs", model.Id, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        using var log = new StreamWriter(Path.Combine(staging, "repair.log")) { AutoFlush = true };
        try
        {
            foreach (var file in plan.Files)
            {
                var target = RepairDestination(staging, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var url = $"https://huggingface.co/{model.Repository}/resolve/{plan.Revision}/"
                    + string.Join("/", file.RelativePath.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
                log.WriteLine(file.RelativePath);
                await DownloadFileAsync(url, target, progress, token, file.Size);
                await using var input = File.OpenRead(target);
                if (!Convert.ToHexString(await SHA256.HashDataAsync(input, token)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("修复文件校验失败，原模型未被覆盖。");
            }
            token.ThrowIfCancellationRequested();
            if (FindRunningProcess(model) is not null || ReadModelRevision(plan.Root) != plan.Revision)
                throw new IOException("模型使用状态或版本已改变，修复文件已保留。");
            foreach (var file in plan.Files)
            {
                var target = RepairDestination(plan.Root, file.RelativePath);
                if (File.Exists(target) && new FileInfo(target).Length > 0) throw new IOException("目标文件已改变，请重新检查。");
            }
            foreach (var file in plan.Files)
            {
                var target = RepairDestination(plan.Root, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target)) File.Move(target, target + ".empty-" + Guid.NewGuid().ToString("N"));
                File.Move(RepairDestination(staging, file.RelativePath), target);
            }
            var missing = ModelHealthPolicy.MissingRequirements(model, settings.Current.LocalAiRoot);
            return missing.Count == 0 ? new(true, "缺失文件已补齐，原有文件与运行环境保持不变。", "current")
                : new(false, "部分文件已补齐，仍需处理：" + string.Join("、", missing));
        }
        catch (Exception ex) { log.WriteLine(ex.ToString()); throw; }
    }

    internal static string RepairDestination(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathFullyQualified(relative) || relative.Contains(':')
            || relative.Replace('\\', '/').Split('/').Any(x => x is ".." or "." or "")) throw new InvalidDataException("修复文件路径无效。");
        var boundary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var destination = Path.GetFullPath(Path.Combine(boundary, relative));
        if (!destination.StartsWith(boundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("修复路径越界。");
        for (var current = destination; current is not null && current.Length >= boundary.Length; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("修复路径包含链接，请手动检查。");
        return destination;
    }
}
