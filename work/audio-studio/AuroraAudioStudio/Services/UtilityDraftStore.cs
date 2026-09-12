using System.Text.Json;

namespace AuroraAudioStudio.Services;

public sealed record UtilityDraft(List<string> Sources, string SelectedPath, string ModelId,
    string Preset, string TrackMode, string SourceLanguage);

public sealed class UtilityDraftStore
{
    private readonly string path;
    private Dictionary<string, UtilityDraft> drafts = [];
    public string? Warning { get; }

    public UtilityDraftStore(SettingsService settings)
    {
        path = Path.Combine(settings.AppDataRoot, "utility-drafts.json");
        try
        {
            if (File.Exists(path)) drafts = JsonSerializer.Deserialize<Dictionary<string, UtilityDraft>>(File.ReadAllText(path)) ?? [];
            if (drafts.Values.Any(x => x is null || x.Sources is null || x.Sources.Any(string.IsNullOrWhiteSpace) || x.SelectedPath is null || x.ModelId is null
                || x.Preset is null || x.TrackMode is null || x.SourceLanguage is null)) throw new InvalidDataException("Invalid draft fields");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            drafts = [];
            Warning = "草稿读取失败，原文件已保留。";
        }
    }

    public UtilityDraft? Get(string feature) => drafts.GetValueOrDefault(feature);

    public bool TrySave(string feature, UtilityDraft draft, out string error)
    {
        error = "";
        if (feature is not ("separation" or "transcription" or "subtitles")) return true;
        try
        {
            if (Warning is not null && File.Exists(path) && !File.Exists(path + ".recovery"))
                File.Copy(path, path + ".recovery");
            var next = new Dictionary<string, UtilityDraft>(drafts) { [feature] = draft with { Sources = [.. draft.Sources] } };
            var text = JsonSerializer.Serialize(next);
            if (File.Exists(path) && File.ReadAllText(path) == text) return true;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", text);
            File.Move(path + ".tmp", path, true);
            drafts = next;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = "草稿保存失败：" + ex.Message;
            return false;
        }
    }
}
