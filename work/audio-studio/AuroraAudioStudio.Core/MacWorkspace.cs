using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Core;

public sealed class StudioDraft
{
    public string Feature { get; set; } = "music";
    public string Name { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Source { get; set; } = "";
    public string Reference { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string Option { get; set; } = "recommended";
    public string ProjectId { get; set; } = "";
    public List<string> Sources { get; set; } = [];
    public string TrackMode { get; set; } = "two-stem";
    public string SourceLanguage { get; set; } = "auto";
}

public interface IStudioEngine
{
    string ModelId { get; }
    bool IsReady => true;
    Task<OperationResult> ExecuteAsync(StudioDraft draft, IProgress<TaskExecutionProgress> progress, CancellationToken token);
}

public sealed class EngineRegistry
{
    private readonly Dictionary<string, IStudioEngine> engines = new(StringComparer.Ordinal);
    public bool IsAvailable(string modelId) => engines.TryGetValue(modelId, out var engine) && engine.IsReady;
    public void Register(IStudioEngine engine) => engines.Add(engine.ModelId, engine);
    public Task<OperationResult> ExecuteAsync(StudioDraft draft, IProgress<TaskExecutionProgress> progress, CancellationToken token)
        => engines.TryGetValue(draft.ModelId, out var engine)
            ? engine.ExecuteAsync(draft, progress, token)
            : throw new InvalidOperationException("No verified macOS engine is configured for this model.");
}

public sealed class MacWorkspace
{
    public static readonly string[] Features = ["music", "voice", "singing", "separation", "transcription", "subtitles"];
    public SettingsService Settings { get; }
    public LocalizationService Localization { get; }
    public ModelCatalogService Catalog { get; }
    public ProjectService Projects { get; }
    public TaskQueueService Queue { get; }
    public EngineRegistry Engines { get; } = new();
    public ModelWorkbenchRegistry Workbenches { get; } = new();
    public MacRuntime Runtime { get; }
    public WorkbenchResultService WorkbenchResults { get; }
    public Dictionary<string, StudioDraft> Drafts { get; private set; } = [];
    public string? RecoveryWarning { get; private set; }
    private string DraftsPath => Path.Combine(Settings.AppDataRoot, "mac-drafts.json");
    private string? lastSavedDrafts;

    public MacWorkspace(string? root = null)
    {
        Settings = new SettingsService(root);
        Localization = new(Settings);
        Catalog = new(Settings);
        Projects = new(Settings);
        Queue = new(Settings);
        Runtime = new(Settings);
        WorkbenchResults = new(Settings, Projects, Queue, Catalog, id => Runtime.ModelStatus(id).Version,
            (id, device, signature) => Runtime.RecordSuccessfulRun(id, device, signature));
        foreach (var id in Runtime.Models.Keys)
        {
            var definition = Catalog.Definitions.FirstOrDefault(m => m.Id == id);
            if (definition is null || !definition.IsRunnable || Runtime.Models[id].DownloadOnly) continue;
            if (definition.Feature is "music" or "voice" or "singing") Workbenches.Register(new MacWorkbenchAdapter(Runtime, id));
            else Engines.Register(new MacUtilityAdapter(Runtime, Settings, id));
        }
        if (File.Exists(DraftsPath))
        {
            try { Drafts = JsonSerializer.Deserialize<Dictionary<string, StudioDraft>>(File.ReadAllText(DraftsPath)) ?? []; }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                File.Copy(DraftsPath, DraftsPath + ".recovery-" + DateTimeOffset.UtcNow.Ticks);
                RecoveryWarning = ex.Message;
            }
        }
        foreach (var feature in Features)
        {
            if (!Drafts.ContainsKey(feature)) Drafts[feature] = new() { Feature = feature };
            var draft = Drafts[feature];
            draft.Feature = feature;
            if (draft.Sources.Count == 0 && !string.IsNullOrWhiteSpace(draft.Source)) draft.Sources.Add(draft.Source);
            if (!Catalog.Definitions.Any(m => m.Id == draft.ModelId && m.Feature == feature))
                draft.ModelId = DefaultModel(feature);
        }
    }

    public static string DefaultModel(string feature) => feature switch
    {
        "voice" => "qwen3-tts-custom", "singing" => "seed-vc", "separation" => "roformer-vocals",
        "transcription" => "transkun", "subtitles" => "whisper-large-v3-turbo", _ => "ace-step"
    };

    public void SaveDrafts()
    {
        var json = JsonSerializer.Serialize(Drafts, new JsonSerializerOptions { WriteIndented = true });
        if (json == lastSavedDrafts && File.Exists(DraftsPath)) return;
        var temporary = DraftsPath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, DraftsPath, true);
        lastSavedDrafts = json;
    }

    public async Task<AuroraProject> SaveProjectAsync(StudioDraft draft)
    {
        var project = string.IsNullOrWhiteSpace(draft.ProjectId) ? null : Projects.Find(draft.ProjectId);
        project ??= new AuroraProject { Feature = draft.Feature };
        project.Name = string.IsNullOrWhiteSpace(draft.Name) ? Localization.Get(draft.Feature) + " · " + DateTime.Now.ToString("MM-dd HH:mm") : draft.Name.Trim();
        project.Feature = draft.Feature;
        project.ModelId = draft.ModelId;
        project.SourcePath = draft.Source;
        project.Parameters["prompt"] = draft.Prompt;
        project.Parameters["reference"] = draft.Reference;
        project.Parameters["option"] = draft.Option;
        project.Parameters["preset"] = draft.Option;
        project.Parameters["trackMode"] = draft.TrackMode;
        project.Parameters["sourceLanguage"] = draft.SourceLanguage;
        project.Parameters["sources"] = JsonSerializer.Serialize(draft.Sources);
        project.Parameters["platform"] = "macOS";
        await Projects.SaveAsync(project);
        draft.ProjectId = project.Id;
        SaveDrafts();
        return project;
    }

    public StudioDraft LoadProject(AuroraProject project)
    {
        if (!Features.Contains(project.Feature)) throw new InvalidDataException("Unsupported project feature.");
        var draft = new StudioDraft
        {
            Feature = project.Feature, Name = project.Name, Prompt = project.Parameters.GetValueOrDefault("prompt", ""),
            Reference = project.Parameters.GetValueOrDefault("reference", ""), Option = project.Parameters.GetValueOrDefault("preset", project.Parameters.GetValueOrDefault("option", "recommended")),
            TrackMode = project.Parameters.GetValueOrDefault("trackMode", "two-stem"),
            SourceLanguage = project.Parameters.GetValueOrDefault("sourceLanguage", "auto"),
            ModelId = project.ModelId, Source = project.SourcePath, ProjectId = project.Id
        };
        if (project.Parameters.TryGetValue("sources", out var sources)) draft.Sources = JsonSerializer.Deserialize<List<string>>(sources) ?? [];
        if (draft.Sources.Count == 0 && !string.IsNullOrWhiteSpace(draft.Source)) draft.Sources.Add(draft.Source);
        if (!Catalog.Definitions.Any(m => m.Id == draft.ModelId && m.Feature == draft.Feature)) draft.ModelId = DefaultModel(draft.Feature);
        Drafts[draft.Feature] = draft;
        SaveDrafts();
        return draft;
    }

    public async Task<StudioDraft> ImportProjectAsync(string path)
    {
        var project = ProjectDocumentMigrator.Read(await File.ReadAllTextAsync(path)) ?? throw new InvalidDataException("Invalid project.");
        if (!Features.Contains(project.Feature)) throw new InvalidDataException("Unsupported project feature.");
        project.Id = Guid.NewGuid().ToString("N");
        project.FilePath = "";
        project.TaskIds = [];
        await Projects.SaveAsync(project);
        return LoadProject(project);
    }

    public async Task<OperationResult> RetryTaskAsync(AuroraTaskRecord task)
    {
        if (!task.CanRetry) return new(false, "此任务已在处理或已完成。");
        if (Settings.Current.SafeMode) return new(false, "安全模式已启用，无法执行或重试任务。");
        if (Runtime.BusyModel is not null) return new(false, "请先等待模型维护完成。");
        if (!Engines.IsAvailable(task.ModelId)) return new(false, "请先安装或修复选中的模型。");
        var draft = new StudioDraft { Feature = task.Feature, ModelId = task.ModelId, Source = task.InputPath,
            SourceLanguage = task.SourceLanguage, Option = task.Preset, TrackMode = task.TrackMode };
        // Retry the same record, preserving project TaskIds and the queue's ID-based deduplication.
        // Clear a completed cancellation before re-entering the shared queue.
        if (task.Status == AuroraTaskStates.Canceled) task.Status = AuroraTaskStates.Waiting;
        var result = await Queue.RunAsync(task, (progress, token) => Engines.ExecuteAsync(draft, progress, token));
        await Projects.CompleteTaskAsync(task.ProjectId, task);
        return result;
    }

    public async Task<AuroraProject> ImportArtifactAsync(string path)
    {
        ArtifactValidator.Validate(path);
        var feature = Path.GetExtension(path).ToLowerInvariant() switch { ".mid" or ".midi" => "transcription", ".srt" => "subtitles", _ => "music" };
        var directory = Path.Combine(Settings.Current.OutputRoot, "Imported", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, Path.GetFileName(path));
        File.Copy(path, target);
        var json = Path.ChangeExtension(path, ".json");
        if (feature == "subtitles" && File.Exists(json)) File.Copy(json, Path.ChangeExtension(target, ".json"));
        ArtifactValidator.Validate(target);
        var project = new AuroraProject { Name = Path.GetFileNameWithoutExtension(path), Feature = feature, SourcePath = path };
        project.Parameters["origin"] = "imported";
        project.Artifacts.Add(new AuroraArtifact { Path = target, Kind = feature });
        await Projects.SaveAsync(project);
        return project;
    }

    public static string ModelPath(string root, ModelDefinition model)
        => Path.Combine(root, Path.Combine(model.RelativeRoot.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)));
}
