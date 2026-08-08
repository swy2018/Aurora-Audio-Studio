using System.Text.Json.Serialization;

namespace AuroraAudioStudio.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "auto";
    public string Theme { get; set; } = "light";
    public string LocalAiRoot { get; set; } = @"C:\LocalAI";
    public string OutputRoot { get; set; } = SettingsDefaults.OutputRoot();
    public bool AutoCheckAppUpdates { get; set; } = true;
    public string? LastAppUpdateCheckDate { get; set; }
    public bool AutoCheckModelUpdates { get; set; } = true;
    public bool ConfirmLargeModelDownloads { get; set; } = true;
    public bool AutoReleaseVram { get; set; } = true;
    public bool SafeMode { get; set; }
    public string ProjectsRoot { get; set; } = SettingsDefaults.ProjectsRoot();
    public int TaskHistoryLimit { get; set; } = 100;
}

public static class SettingsDefaults
{
    public static string OutputRoot()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var preferred = Path.Combine(profile, "OneDrive", "云", "桌面", "AI工作流");
        if (Directory.Exists(Path.GetDirectoryName(preferred)!)) return preferred;
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(string.IsNullOrWhiteSpace(desktop) ? profile : desktop, "AI工作流");
    }

    public static string ProjectsRoot() => Path.Combine(OutputRoot(), "Aurora Projects");
}

public sealed record ModelDefinition(
    string Id,
    string Name,
    string Feature,
    string RelativeRoot,
    string Marker,
    string Source,
    string UpdateKind,
    string? Repository = null,
    bool IsDefault = false);

public sealed record ModelState(
    string Id,
    string Name,
    string Feature,
    bool Installed,
    string Status,
    string Source,
    string LocalPath,
    string Version,
    string Health,
    string RecommendedVram,
    string FeatureDisplay,
    string Purpose,
    string Languages,
    string EstimatedDownload,
    string License,
    string DetailsDisplay,
    string EditionDisplay,
    string PrimaryAction);

public static class AuroraTaskStates
{
    public const string Waiting = "waiting";
    public const string Preparing = "preparing";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Canceled = "canceled";
    public const string Interrupted = "interrupted";
}

public sealed class AuroraTaskRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Feature { get; set; } = "";
    public string InputPath { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string Status { get; set; } = AuroraTaskStates.Waiting;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public double Progress { get; set; }
    public string Stage { get; set; } = "Queued";
    public string Message { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string LogPath { get; set; } = "";
    [JsonIgnore] public bool CanCancel => Status is AuroraTaskStates.Waiting or AuroraTaskStates.Preparing or AuroraTaskStates.Running;
    [JsonIgnore] public bool CanRetry => Status is AuroraTaskStates.Failed or AuroraTaskStates.Canceled or AuroraTaskStates.Interrupted;
    [JsonIgnore] public string CreatedDisplay => CreatedAt.LocalDateTime.ToString("MM-dd HH:mm");
    [JsonIgnore] public string StatusDisplay => Status switch
    {
        AuroraTaskStates.Waiting => "等待中",
        AuroraTaskStates.Preparing => "准备中",
        AuroraTaskStates.Running => "处理中",
        AuroraTaskStates.Completed => "已完成",
        AuroraTaskStates.Failed => "未完成",
        AuroraTaskStates.Canceled => "已取消",
        AuroraTaskStates.Interrupted => "已恢复",
        _ => Status
    };
}

public sealed class AuroraArtifact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "output";
    public string Path { get; set; } = "";
    public string SourceTaskId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class AuroraProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled project";
    public string Feature { get; set; } = "music";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public string SourcePath { get; set; } = "";
    public string SourceSha256 { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string ModelVersion { get; set; } = "";
    public Dictionary<string, string> Parameters { get; set; } = [];
    public List<string> TaskIds { get; set; } = [];
    public List<AuroraArtifact> Artifacts { get; set; } = [];
    public string FilePath { get; set; } = "";
    [JsonIgnore] public string UpdatedDisplay => UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    [JsonIgnore] public string FeatureDisplay => Feature switch { "separation" => "AI 分轨", "transcription" => "AI 扒谱", "subtitles" => "视频字幕", "voice" => "AI 配音", "singing" => "歌声克隆", _ => "音乐创作" };
}

public sealed record AuroraTemplate(string Id, string Title, string Description, string Feature, string ModelId, string Glyph);

public sealed record HealthCheckItem(string Name, string State, string Detail, string Glyph);

public sealed record AppUpdateInfo(
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string? InstallerUrl,
    string? ChecksumUrl,
    string Message,
    bool CheckSucceeded = true);

public sealed record OperationResult(bool Success, string Message, string? Path = null, string? Url = null);

public sealed record AppUpdateProgress(double Percentage, string Message, bool IsIndeterminate = false);

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";
    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}

public sealed class ModelUpdateManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }
    [JsonPropertyName("models")]
    public List<ModelUpdateEntry> Models { get; set; } = [];
}

public sealed class ModelUpdateEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
    [JsonPropertyName("packageKind")]
    public string PackageKind { get; set; } = "file";
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = "";
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}
