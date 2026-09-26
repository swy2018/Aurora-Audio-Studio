using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public enum ModelRepairKind { Healthy, RuntimeOnly, MissingFiles, FullRedeploy, Blocked }
public sealed record RepairFile(string RelativePath, long Size, string Sha256, string? ExistingSha256 = null);
public sealed record ModelRepairPlan(ModelRepairKind Kind, string Root, string Detail,
    IReadOnlyList<RepairFile> Files, string Revision = "")
{
    public long DownloadBytes => Files.Sum(x => x.Size);
}

public sealed partial class ModelUpdateService
{
    private readonly LocalizationService repairLocalization = new(settings);
    private readonly Dictionary<string, IReadOnlyList<RepairFile>> repairManifests = new(StringComparer.Ordinal);

    public async Task<ModelRepairPlan> InspectRepairAsync(ModelDefinition model, CancellationToken token = default, IProgress<ModelInstallProgress>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (FindRunningProcess(model) is { } running) return new(ModelRepairKind.Blocked, root, running, []);
        var missing = ModelHealthPolicy.MissingRequirements(model, settings.Current.LocalAiRoot);
        var detail = string.Join("、", missing);
        if (missing.Count > 0 && ((model.UpdateKind == "uv-package" && !missing.Any(x => x.Contains("权重") || x.Contains("配置")))
            || (model.Id.StartsWith("qwen3-tts-", StringComparison.Ordinal) && missing.All(x => x is "Qwen3-TTS 启动器" or "Qwen3-TTS SoX 音频组件"))))
            return new(ModelRepairKind.RuntimeOnly, root, detail, []);
        var revision = ReadModelRevision(root);
        if (model.Id == "piano")
        {
            var existing = await FileHashAsync(RepairDestination(root, model.Marker), token);
            if (!string.Equals(existing, PianoCheckpointSha256, StringComparison.OrdinalIgnoreCase))
                return new(ModelRepairKind.MissingFiles, root, repairLocalization.Translate("钢琴权重校验未通过，将从官方固定版本修复。"),
                    [new(model.Marker, 0, PianoCheckpointSha256, existing)], PianoCheckpointSha256);
            return await InspectInstalledRuntimeAsync(model, root, token, repairLocalization.Translate("模型 SHA-256 校验通过。"));
        }
        if (model.UpdateKind != "huggingface" || !Regex.IsMatch(revision, "^[a-fA-F0-9]{40}$"))
        {
            if (missing.Count > 0) return new(ModelRepairKind.FullRedeploy, root, detail, []);
            if (model.UpdateKind == "huggingface")
                return new(ModelRepairKind.Blocked, root, repairLocalization.Translate("缺少已安装模型的版本记录，无法安全确认修复清单；未更改模型。"), []);
            return await InspectInstalledRuntimeAsync(model, root, token, repairLocalization.Translate("文件齐全；此来源未提供可直接核验的完整权重清单。"));
        }
        try
        {
            var required = new HashSet<string>(StringComparer.Ordinal) { model.Marker.Replace('\\', '/') };
            if (Directory.Exists(root))
                foreach (var index in Directory.EnumerateFiles(root, "*.index.json", SearchOption.AllDirectories))
                {
                    using var weights = JsonDocument.Parse(await File.ReadAllTextAsync(index, token));
                    if (weights.RootElement.TryGetProperty("weight_map", out var map))
                        foreach (var entry in map.EnumerateObject())
                            required.Add(Path.GetRelativePath(root, RepairDestination(Path.GetDirectoryName(index)!, entry.Value.GetString()!)).Replace('\\', '/'));
                }
            var key = model.Repository + "@" + revision;
            if (!repairManifests.TryGetValue(key, out var manifest))
            {
                // Pinned Hub metadata supplies reference hashes; never trust a local nonempty file.
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                using var document = JsonDocument.Parse(await metadataClient.GetStringAsync(
                    $"https://huggingface.co/api/models/{model.Repository}/revision/{revision}?blobs=true", deadline.Token));
                if (document.RootElement.GetProperty("sha").GetString() != revision) throw new InvalidDataException("模型版本不匹配。");
                var entries = new List<RepairFile>();
                foreach (var file in document.RootElement.GetProperty("siblings").EnumerateArray())
                {
                    if (!file.TryGetProperty("lfs", out var lfs) || lfs.ValueKind != JsonValueKind.Object) continue;
                    var hash = lfs.GetProperty("sha256").GetString()!;
                    if (!Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$")) continue;
                    entries.Add(new(file.GetProperty("rfilename").GetString()!, lfs.GetProperty("size").GetInt64(), hash));
                }
                repairManifests[key] = manifest = entries;
            }
            var files = new List<RepairFile>();
            var verified = 0;
            foreach (var entry in manifest.Where(entry => required.Contains(entry.RelativePath)))
            {
                progress?.Report(new(null, "正在校验模型文件完整性", LogLine: entry.RelativePath));
                var destination = RepairDestination(root, entry.RelativePath);
                var actual = await FileHashAsync(destination, token);
                if (actual is not null && new FileInfo(destination).Length == entry.Size && actual.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)) { verified++; continue; }
                files.Add(entry with { ExistingSha256 = actual });
            }
            if (files.Count > 0 && missing.All(x => x == "模型标记" || x.StartsWith("模型分片 ", StringComparison.Ordinal)))
                return new(ModelRepairKind.MissingFiles, root, repairLocalization.Translate("检测到缺失或损坏的权重，将修复当前固定版本。"), files, revision);
            if (missing.Count > 0) return new(ModelRepairKind.FullRedeploy, root, detail, []);
            var weightExtensions = new[] { ".bin", ".safetensors", ".pth", ".pt", ".onnx", ".ckpt" };
            if (verified == 0 || required.Where(path => weightExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Any(path => !manifest.Any(entry => entry.RelativePath == path)))
                return new(ModelRepairKind.Blocked, root, repairLocalization.Translate("上游未提供所需权重的可核验摘要，未宣告模型完整性通过。"), []);
            return await InspectInstalledRuntimeAsync(model, root, token, repairLocalization.Translate("必需权重 SHA-256 校验通过。"));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(ModelRepairKind.Blocked, root, repairLocalization.Translate("无法确认修复清单，未更改任何文件：") + ex.Message, []); }
    }

    private static async Task<string?> FileHashAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }

    private async Task<ModelRepairPlan> InspectInstalledRuntimeAsync(ModelDefinition model, string root, CancellationToken token, string integrity)
    {
        if (!model.IsRunnable || catalog.Find(model.Id) is null)
            return new(ModelRepairKind.Healthy, root, integrity + repairLocalization.Translate(" 不涉及推理环境检查。"), []);
        var failedKind = model.UpdateKind == "uv-package" || model.Id.StartsWith("qwen3-tts-", StringComparison.Ordinal)
            || model.Id is "piano" or "roformer-vocals" ? ModelRepairKind.RuntimeOnly : ModelRepairKind.FullRedeploy;
        try
        {
            OperationResult probe;
            if (model.Id is "ace-step" or "seed-vc") probe = await ProbeGitRuntimeAsync(model, root, token);
            else
            {
                var logical = model.Id.StartsWith("qwen3-tts-", StringComparison.Ordinal) ? Path.Combine(settings.Current.LocalAiRoot, "Qwen3-TTS", "Python312")
                    : model.Id == "piano" ? Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "piano-env")
                    : model.Id == "roformer-vocals" ? Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "roformer-env")
                    : model.Id == "minimax-music3" ? Path.Combine(settings.Current.LocalAiRoot, "AudioTools", "minimax-music3-env") : root;
                var imports = model.Id switch
                {
                    "piano" => "import torch, torchaudio, piano_transcription_inference",
                    "transkun" => "import torch, torchaudio, transkun.transcribe",
                    "roformer" or "roformer-vocals" => "import torch, bs_roformer",
                    "yourmt3" => "import torch, mt3_infer",
                    "basic-pitch" => "import basic_pitch.inference",
                    "demucs" => "import torch, demucs.pretrained",
                    "f5-tts" => "import torch, f5_tts.infer.utils_infer",
                    "minimax-music3" => "import torch; from diffusers import ModularPipeline",
                    _ when model.Id.StartsWith("qwen3-tts-", StringComparison.Ordinal) => "import torch, torchaudio, qwen_tts, gradio",
                    _ => ""
                };
                if (imports.Length == 0) return new(ModelRepairKind.Healthy, root, integrity + repairLocalization.Translate(" 原生组件的执行能力仍需短样本验证。"), []);
                probe = await ProbePythonRuntimeAsync(RuntimeEnvironment.PythonPath(RuntimeEnvironment.Resolve(logical)), imports, token);
            }
            return new(probe.Success ? ModelRepairKind.Healthy : failedKind, root, integrity + " " + repairLocalization.Translate(probe.Message), []);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(failedKind, root, integrity + repairLocalization.Translate(" 运行环境检查未通过：") + ex.Message, []); }
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
            if (model.Id == "piano")
            {
                var runtime = new ModelDefinition("piano-runtime", "Piano runtime", "transcription", @"AudioTools\piano-env", @"Scripts\python.exe", "PyPI", "uv-package", "piano-transcription-inference");
                return await InstallUvPackageAsync(runtime, Path.Combine(settings.Current.LocalAiRoot, runtime.RelativeRoot), progress, token, includeWeights: false);
            }
            if (model.Id == "roformer-vocals")
            {
                var runtime = catalog.Find("roformer")!;
                return await InstallUvPackageAsync(runtime, Path.Combine(settings.Current.LocalAiRoot, runtime.RelativeRoot), progress, token, includeWeights: false);
            }
            return model.Id.StartsWith("qwen3-tts-", StringComparison.Ordinal)
                ? await EnsureQwenTtsRuntimeAsync(progress, token)
                : await InstallUvPackageAsync(model, plan.Root, progress, token, includeWeights: false);
        }
        if (plan.Kind == ModelRepairKind.FullRedeploy) return await UpdateAsync(model, progress, token);
        if (model.Id != "piano" && ReadModelRevision(plan.Root) != plan.Revision) return new(false, "模型版本已改变，请重新检查。");
        var drive = new DriveInfo(Path.GetPathRoot(settings.UpdatesRoot)!);
        if (drive.AvailableFreeSpace < Math.Max(plan.DownloadBytes, model.Id == "piano" ? ModelInstallPlanner.RecommendedBytes(model.Id) : 0)) return new(false, "磁盘空间不足，未开始修复。");
        var staging = Path.Combine(settings.UpdatesRoot, "Repairs", model.Id, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        using var log = new StreamWriter(Path.Combine(staging, "repair.log")) { AutoFlush = true };
        try
        {
            foreach (var file in plan.Files)
            {
                var target = RepairDestination(staging, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var url = model.Id == "piano" ? model.Repository! : $"https://huggingface.co/{model.Repository}/resolve/{plan.Revision}/"
                    + string.Join("/", file.RelativePath.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
                log.WriteLine(file.RelativePath);
                await DownloadFileAsync(url, target, progress, token, file.Size > 0 ? file.Size : null);
                await using var input = File.OpenRead(target);
                if (!Convert.ToHexString(await SHA256.HashDataAsync(input, token)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("修复文件校验失败，原模型未被覆盖。");
            }
            token.ThrowIfCancellationRequested();
            if (FindRunningProcess(model) is not null || model.Id != "piano" && ReadModelRevision(plan.Root) != plan.Revision)
                throw new IOException("模型使用状态或版本已改变，修复文件已保留。");
            foreach (var file in plan.Files)
            {
                var target = RepairDestination(plan.Root, file.RelativePath);
                if (!string.Equals(await FileHashAsync(target, token), file.ExistingSha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("目标文件已改变，请重新检查。");
            }
            foreach (var file in plan.Files)
            {
                var target = RepairDestination(plan.Root, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target)) File.Move(target, target + ".replaced-" + Guid.NewGuid().ToString("N"));
                File.Move(RepairDestination(staging, file.RelativePath), target);
            }
            var missing = ModelHealthPolicy.MissingRequirements(model, settings.Current.LocalAiRoot);
            if (missing.Count > 0) return new(false, "部分文件已补齐，仍需处理：" + string.Join("、", missing));
            var checkedRuntime = await InspectInstalledRuntimeAsync(model, plan.Root, token, repairLocalization.Translate("本次修复文件 SHA-256 校验通过。"));
            return new(checkedRuntime.Kind == ModelRepairKind.Healthy, checkedRuntime.Detail,
                checkedRuntime.Kind == ModelRepairKind.Healthy ? "current" : plan.Root);
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
