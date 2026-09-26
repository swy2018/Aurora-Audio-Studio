using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class StorageRegression
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-Storage-" + Guid.NewGuid().ToString("N"));
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var settings = new SettingsService(root);
        var queue = new TaskQueueService(settings);
        var record = queue.Create("p", "retained", "transcription", "source.wav", "transkun");
        var statePath = Path.Combine(root, "tasks.json");
        var original = File.ReadAllText(statePath);
        var blocker = statePath + ".tmp";
        Directory.CreateDirectory(blocker);
        var blocked = new TaskQueueService(settings);
        Check(blocked.StorageWarning is not null && blocked.Items.Single().Id == record.Id, "write failure retains loaded history and does not crash startup");
        Check(File.ReadAllText(statePath) == original, "write failure cannot replace the previous task store");
        var started = false;
        var result = await blocked.RunAsync(blocked.Items.Single(), (_, _) => { started = true; return Task.FromResult(new OperationResult(true, "unexpected")); });
        Check(!started && !result.Success && blocked.StorageWarning is not null, "unwritable history blocks execution without a second exception");
        var rejected = false;
        try { blocked.Create("p", "new", "transcription", "source.wav", "transkun"); } catch (IOException) { rejected = true; }
        Check(rejected && blocked.Items.Count == 1, "failed submission does not add an unrecorded task");
        Directory.Move(blocker, blocker + ".retained-fixture");
        Check(blocked.TryRestoreStorage() && blocked.StorageWarning is null, "storage retry resumes after the exact blocking condition is removed");
        TaskQueueService locked;
        using (var handle = File.Open(statePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            locked = new TaskQueueService(settings);
            Check(locked.StorageWarning is not null && !locked.TryRestoreStorage(), "unreadable store cannot be overwritten by an empty recovery state");
        }
        Check(locked.TryRestoreStorage() && locked.Items.Single().Id == record.Id, "recovery reloads the protected original after a file lock ends");
        File.WriteAllText(statePath, "[null]");
        var malformed = new TaskQueueService(settings);
        Check(malformed.Items.Count == 0 && Directory.GetFiles(root, "tasks.json.recovery-*").Any(), "malformed task rows retain a recoverable original");
        var projects = new ProjectService(settings, appVersion: "2.0.1-beta.1");
        var project = await projects.CreateAsync("transcription", "", "transkun");
        Check(project.Parameters["appVersion"] == "2.0.1-beta.1", "project stores product version rather than shared library version");
        await new ProjectService(settings, appVersion: "2.0.1-beta.2").SaveAsync(project);
        Check(project.Parameters["appVersion"] == "2.0.1-beta.1" && project.Parameters["lastSavedAppVersion"] == "2.0.1-beta.2", "editing preserves creation version and records current writer version");
        var unreadSettingsPath = Path.Combine(root, "settings.json");
        File.WriteAllText(unreadSettingsPath, "{broken");
        var unreadSettings = new SettingsService(root);
        Check(unreadSettings.StorageWarning is not null && !unreadSettings.TrySetLanguage("ja-JP", out _) && File.ReadAllText(unreadSettingsPath) == "{broken", "unreadable settings cannot be silently replaced by language or startup writes");
        var blockedRoot = Path.Combine(root, "blocked-root"); File.WriteAllText(blockedRoot, "retain");
        var blockedSettings = new SettingsService(blockedRoot);
        Check(blockedSettings.StorageWarning is not null, "an unavailable app-data directory does not crash settings initialization");
        Console.WriteLine($"Storage checks passed: {count}. Fixtures retained: {root}");
    }
}
