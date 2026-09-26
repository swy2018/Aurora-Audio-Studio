using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class ProjectService(SettingsService settings, ModelCatalogService? catalog = null, string? appVersion = null)
{
    private readonly string productVersion = appVersion ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    private readonly HashSet<string> recoveryPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object cacheGate = new();
    private readonly Dictionary<string, (DateTime Written, long Length, AuroraProject Project)> documentCache = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    public int RecoveryCount { get { lock (cacheGate) return recoveryPaths.Count; } }
    public IReadOnlyList<AuroraTemplate> Templates { get; } =
    [
        new("music", "从文字开始创作", "生成完整歌曲或纯音乐", "music", "ace-step", "\uE8D6"),
        new("voice", "制作一段配音", "设计音色、克隆声音并合成", "voice", "qwen3-tts-custom", "\uE720"),
        new("separation", "拆分一首混音", "分离人声、鼓、贝斯与伴奏", "separation", "roformer", "\uE9E9"),
        new("transcription", "把录音变成 MIDI", "默认使用 TransKun V2 识别钢琴演奏", "transcription", "transkun", "\uE70F"),
        new("subtitles", "为视频生成字幕", "本地识别并输出时间轴字幕", "subtitles", "faster-whisper", "\uE8BA")
    ];

    public async Task<AuroraProject> CreateAsync(string feature, string sourcePath, string modelId, CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(sourcePath) ? NewProjectName(feature) : Path.GetFileNameWithoutExtension(sourcePath);
        var project = new AuroraProject
        {
            Name = name,
            Feature = feature,
            SourcePath = sourcePath,
            ModelId = modelId,
            SourceSha256 = await HashSourceAsync(sourcePath, cancellationToken)
        };
        project.ModelVersion = catalog?.GetStates().FirstOrDefault(x => x.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase))?.Version ?? "";
        project.Parameters["appVersion"] = productVersion;
        var safeName = string.Join("-", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Aurora-Project";
        project.FilePath = Path.Combine(settings.Current.ProjectsRoot, $"{safeName}-{project.Id[..8]}.arr");
        await SaveAsync(project);
        return project;
    }

    public IReadOnlyList<AuroraProject> Recent(int count = 8)
    {
        if (!Directory.Exists(settings.Current.ProjectsRoot)) return [];
        // Enumeration already supplies timestamps and lengths; avoid a second filesystem stat per file.
        var directory = new DirectoryInfo(settings.Current.ProjectsRoot);
        var files = directory.EnumerateFiles("*.arr", SearchOption.TopDirectoryOnly)
            .Concat(directory.EnumerateFiles("*.aurora", SearchOption.TopDirectoryOnly))
            .DistinctBy(file => file.FullName, documentCache.Comparer)
            .ToArray();
        lock (cacheGate)
        {
            var existing = files.Select(file => file.FullName).ToHashSet(documentCache.Comparer);
            foreach (var removed in documentCache.Keys.Where(path => !existing.Contains(path)).ToArray()) documentCache.Remove(removed);
        }
        return files
            .Select(Load).Where(x => x is not null).Cast<AuroraProject>()
            .OrderByDescending(x => x.UpdatedAt).Take(count).ToList();
    }

    // Editing a returned record must not mutate the cached on-disk snapshot before Save succeeds.
    public AuroraProject? Find(string id) => Recent(int.MaxValue).FirstOrDefault(x => x.Id == id) is { } project
        ? JsonSerializer.Deserialize<AuroraProject>(JsonSerializer.Serialize(project, json), json) : null;

    public void LinkSource(AuroraProject project, string sourcePath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var origin in Recent(int.MaxValue).Where(x => x.Id != project.Id))
            if (origin.Artifacts.FirstOrDefault(x => string.Equals(x.Path, sourcePath, comparison)) is { } artifact)
            {
                project.Parameters["sourceProjectId"] = origin.Id;
                project.Parameters["sourceArtifactId"] = artifact.Id;
                project.Parameters["sourceArtifactPath"] = artifact.Path;
                return;
            }
    }

    public IReadOnlyList<string> TranscriptionSources(string projectId)
        => SelectStemSources(Find(projectId)?.Artifacts ?? []);

    public static IReadOnlyList<string> SelectStemSources(IEnumerable<AuroraArtifact> artifacts)
    {
        var sources = artifacts.Where(item => item.Kind == "separation" && MediaInputPolicy.IsSupported("transcription", item.Path));
        static bool IsMix(AuroraArtifact item) => Path.GetFileNameWithoutExtension(item.Path).EndsWith("_instrumental", StringComparison.OrdinalIgnoreCase);
        // BS-RoFormer emits a convenience instrumental mix alongside its six independent stems.
        // Keep both members of a two-stem result, but do not transcribe that aggregate a second time.
        return sources.GroupBy(item => Path.GetDirectoryName(item.Path), OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .SelectMany(group => group.Count(item => !IsMix(item)) > 1 ? group.Where(item => !IsMix(item)) : group)
            .Select(item => item.Path).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<ArtifactDisplay> Artifacts(int count = 120) => Recent(300)
        .SelectMany(project => project.Artifacts.Select(artifact => new ArtifactDisplay
        {
            ProjectId = project.Id,
            Info = artifact.Info,
            ProjectName = project.Name,
            Kind = artifact.Kind,
            Path = artifact.Path,
            CreatedAt = artifact.CreatedAt
        }))
        .Where(x => !string.IsNullOrWhiteSpace(x.Path))
        .OrderByDescending(x => x.CreatedAt)
        .Take(count)
        .ToList();

    public async Task AddTaskAsync(AuroraProject project, AuroraTaskRecord task)
    {
        if (!project.TaskIds.Contains(task.Id)) project.TaskIds.Add(task.Id);
        project.UpdatedAt = DateTimeOffset.Now;
        await SaveAsync(project);
    }

    public async Task CompleteTaskAsync(string projectId, AuroraTaskRecord task)
    {
        var project = Find(projectId);
        if (project is null) return;
        project.Parameters["preset"] = task.Preset;
        project.Parameters["trackMode"] = task.TrackMode;
        project.Parameters["sourceLanguage"] = task.SourceLanguage;
        project.Parameters["device"] = task.Device;
        if (task.InputInfo is not null) project.Parameters["inputAudio"] = JsonSerializer.Serialize(task.InputInfo);
        project.UpdatedAt = DateTimeOffset.Now;
        foreach (var path in ResolveArtifacts(task))
            if (!project.Artifacts.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                project.Artifacts.Add(new AuroraArtifact { Path = path, SourceTaskId = task.Id, Kind = task.Feature, CreatedAt = task.CompletedAt ?? DateTimeOffset.Now, Info = ArtifactValidator.Inspect(path) });
        await SaveAsync(project);
    }

    private static IReadOnlyList<string> ResolveArtifacts(AuroraTaskRecord task)
    {
        if (task.Status != AuroraTaskStates.Completed) return [];
        return task.OutputFiles.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task SaveAsync(AuroraProject project)
    {
        project.Parameters.TryAdd("appVersion", productVersion);
        project.Parameters["lastSavedAppVersion"] = productVersion;
        Directory.CreateDirectory(settings.Current.ProjectsRoot);
        if (string.IsNullOrWhiteSpace(project.FilePath)) project.FilePath = Path.Combine(settings.Current.ProjectsRoot, $"{project.Id}.arr");
        project.UpdatedAt = DateTimeOffset.Now;
        var temp = project.FilePath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(project, json));
        File.Move(temp, project.FilePath, true);
        lock (cacheGate) documentCache.Remove(project.FilePath);
    }

    private AuroraProject? Load(FileInfo stamp)
    {
        var path = stamp.FullName;
        try
        {
            var written = stamp.LastWriteTimeUtc;
            var length = stamp.Length;
            lock (cacheGate)
                if (documentCache.TryGetValue(path, out var cached) && cached.Written == written && cached.Length == length) return cached.Project;
            var project = ProjectDocumentMigrator.Read(File.ReadAllText(path));
            if (project is not null) project.FilePath = path;
            lock (cacheGate)
            {
                recoveryPaths.Remove(path);
                // Bound memory even for unusually large libraries; correctness never relies on caching.
                if (documentCache.Count >= 12000) documentCache.Clear();
                if (project is not null) documentCache[path] = (written, length, project);
            }
            return project;
        }
        catch (Exception ex)
        {
            lock (cacheGate) { recoveryPaths.Add(path); documentCache.Remove(path); }
            try
            {
                var recovery = path + ".recovery";
                if (!File.Exists(recovery)) File.Copy(path, recovery);
                Directory.CreateDirectory(settings.LogsRoot);
                File.AppendAllText(Path.Combine(settings.LogsRoot, "project-recovery.log"), $"[{DateTimeOffset.Now:O}] {path}{Environment.NewLine}{ex.Message}{Environment.NewLine}");
            }
            catch { }
            return null;
        }
    }

    private static async Task<string> HashSourceAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return "";
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string NewProjectName(string feature) => feature switch
    {
        "voice" => "新配音项目", "singing" => "新歌声项目", "separation" => "新分轨项目",
        "transcription" => "新扒谱项目", "subtitles" => "新字幕项目", _ => "新音乐项目"
    };
}
