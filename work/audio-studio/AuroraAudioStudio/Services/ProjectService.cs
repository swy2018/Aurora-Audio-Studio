using System.Security.Cryptography;
using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class ProjectService(SettingsService settings, ModelCatalogService? catalog = null)
{
    private readonly JsonSerializerOptions json = new() { WriteIndented = true };
    private readonly HashSet<string> recoveryPaths = new(StringComparer.OrdinalIgnoreCase);
    public int RecoveryCount => recoveryPaths.Count;
    public IReadOnlyList<AuroraTemplate> Templates { get; } =
    [
        new("music", "从文字开始创作", "生成完整歌曲或纯音乐", "music", "ace-step", "\uE8D6"),
        new("voice", "制作一段配音", "设计音色、克隆声音并合成", "voice", "qwen3-tts-custom", "\uE720"),
        new("separation", "拆分一首混音", "分离人声、鼓、贝斯与伴奏", "separation", "roformer", "\uE9E9"),
        new("transcription", "把录音变成 MIDI", "默认使用 TransKun V2 识别钢琴演奏", "transcription", "transkun", "\uE70F"),
        new("subtitles", "为视频生成字幕", "本地识别并输出时间轴字幕", "subtitles", "faster-whisper", "\uE8BA")
    ];

    public async Task<AuroraProject> CreateAsync(string feature, string sourcePath, string modelId)
    {
        var name = string.IsNullOrWhiteSpace(sourcePath) ? NewProjectName(feature) : Path.GetFileNameWithoutExtension(sourcePath);
        var project = new AuroraProject
        {
            Name = name,
            Feature = feature,
            SourcePath = sourcePath,
            ModelId = modelId,
            SourceSha256 = await HashSourceAsync(sourcePath)
        };
        project.ModelVersion = catalog?.GetStates().FirstOrDefault(x => x.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase))?.Version ?? "";
        project.Parameters["appVersion"] = typeof(ProjectService).Assembly.GetName().Version?.ToString(3) ?? "1.7.0";
        var safeName = string.Join("-", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Aurora-Project";
        project.FilePath = Path.Combine(settings.Current.ProjectsRoot, $"{safeName}-{project.Id[..8]}.arr");
        await SaveAsync(project);
        return project;
    }

    public IReadOnlyList<AuroraProject> Recent(int count = 8)
    {
        Directory.CreateDirectory(settings.Current.ProjectsRoot);
        return Directory.EnumerateFiles(settings.Current.ProjectsRoot, "*.arr", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(settings.Current.ProjectsRoot, "*.aurora", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(Load).Where(x => x is not null).Cast<AuroraProject>()
            .OrderByDescending(x => x.UpdatedAt).Take(count).ToList();
    }

    public AuroraProject? Find(string id) => Recent(200).FirstOrDefault(x => x.Id == id);

    public IReadOnlyList<ArtifactDisplay> Artifacts(int count = 120) => Recent(300)
        .SelectMany(project => project.Artifacts.Select(artifact => new ArtifactDisplay
        {
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
        project.UpdatedAt = DateTimeOffset.Now;
        foreach (var path in ResolveArtifacts(task))
            if (!project.Artifacts.Any(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                project.Artifacts.Add(new AuroraArtifact { Path = path, SourceTaskId = task.Id, Kind = task.Feature, CreatedAt = task.CompletedAt ?? DateTimeOffset.Now });
        await SaveAsync(project);
    }

    private static IReadOnlyList<string> ResolveArtifacts(AuroraTaskRecord task)
    {
        if (string.IsNullOrWhiteSpace(task.OutputPath)) return [];
        if (File.Exists(task.OutputPath)) return [task.OutputPath];
        if (!Directory.Exists(task.OutputPath)) return [];
        var earliest = (task.StartedAt ?? task.CreatedAt).UtcDateTime.AddSeconds(-2);
        try
        {
            return Directory.EnumerateFiles(task.OutputPath, "*", SearchOption.AllDirectories)
                .Where(path => File.GetLastWriteTimeUtc(path) >= earliest)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(200)
                .ToList();
        }
        catch { return []; }
    }

    public async Task SaveAsync(AuroraProject project)
    {
        Directory.CreateDirectory(settings.Current.ProjectsRoot);
        if (string.IsNullOrWhiteSpace(project.FilePath)) project.FilePath = Path.Combine(settings.Current.ProjectsRoot, $"{project.Id}.arr");
        project.UpdatedAt = DateTimeOffset.Now;
        var temp = project.FilePath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(project, json));
        File.Move(temp, project.FilePath, true);
    }

    private AuroraProject? Load(string path)
    {
        try
        {
            var project = ProjectDocumentMigrator.Read(File.ReadAllText(path));
            if (project is not null) project.FilePath = path;
            recoveryPaths.Remove(path);
            return project;
        }
        catch (Exception ex)
        {
            recoveryPaths.Add(path);
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

    private static async Task<string> HashSourceAsync(string path)
    {
        if (!File.Exists(path)) return "";
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private static string NewProjectName(string feature) => feature switch
    {
        "voice" => "新配音项目", "singing" => "新歌声项目", "separation" => "新分轨项目",
        "transcription" => "新扒谱项目", "subtitles" => "新字幕项目", _ => "新音乐项目"
    };
}
