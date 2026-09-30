using System.Text.Json;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Models;

internal static class ReviewTests
{
    public static async Task RunAsync(string root, Action<bool,string> check, Func<Func<Task>,string,Task> reject)
    {
        var workspace = new MacWorkspace(Path.Combine(root, "review"));
        AppSettings Copy() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(workspace.Settings.Current))!;
        var invalid = Copy(); invalid.OutputRoot = "relative";
        check(!workspace.TrySaveSettings(invalid, true, out _) && workspace.Settings.Current.OutputRoot != "relative", "invalid settings rejected before any workbench teardown");
        var theme = Copy(); theme.Theme = "dark";
        check(workspace.TrySaveSettings(theme, true, out _) && workspace.Settings.Current.Theme == "dark", "theme can be saved with a connected workbench without releasing it");
        var changed = Copy(); changed.OutputRoot = Path.Combine(root, "new-output");
        check(!workspace.TrySaveSettings(changed, true, out _) && !Directory.Exists(changed.OutputRoot), "active workbench blocks storage changes before directories are created");
        var task = workspace.Queue.Create("p", "pending", "subtitles", "source", "whisper-small");
        check(!workspace.TrySaveSettings(changed, false, out _), "queued work also protects runtime settings");
        workspace.Queue.Cancel(task.Id);
        check(workspace.TrySaveSettings(changed, false, out _), "idle storage settings remain editable");
        var bad = Path.Combine(root, "invalid-project.arr"); await File.WriteAllTextAsync(bad, "{\"Feature\":\"music\",\"Artifacts\":null}");
        await reject(async () => { await workspace.ImportProjectAsync(bad); }, "invalid import is rejected before persisting in the library");
        check(workspace.Projects.Recent().Count == 0 && File.ReadAllText(bad).Contains("null"), "invalid import leaves project library and original untouched");
        var adapter = new MacUtilityAdapter(workspace.Runtime, workspace.Settings, "demucs");
        await reject(async () => { await adapter.ExecuteAsync(new StudioDraft { Feature="separation", TrackMode="two-stem" }, new Progress<TaskExecutionProgress>(), CancellationToken.None); }, "Mac execution rejects a mismatched stem mode before inference");
        var environments = Path.Combine(workspace.Settings.Current.LocalAiRoot, "envs"); Directory.CreateDirectory(environments);
        var candidate = "qwen-" + new string('a', 32);
        await File.WriteAllTextAsync(Path.Combine(environments, "qwen.active"), candidate);
        check(workspace.Runtime.Python("qwen") == Path.Combine(environments, candidate, "bin", "python"), "Mac runtime resolves an activated immutable environment");
        await File.WriteAllTextAsync(Path.Combine(environments, "qwen.active"), "../../other");
        await reject(() => { workspace.Runtime.Python("qwen"); return Task.CompletedTask; }, "runtime pointer cannot escape its family directory");
        await File.WriteAllTextAsync(Path.Combine(workspace.Settings.AppDataRoot, "mac-drafts.json"), "{\"music\":null}");
        var recovered = new MacWorkspace(workspace.Settings.AppDataRoot);
        check(recovered.Drafts["music"] is not null && recovered.StorageWarning is not null, "null saved draft cannot crash startup or overwrite its original");
    }
}
