using System.Text.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class ReviewRegression
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-ReviewTests-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(root);
        var failures = new List<string>();
        var passed = 0;
        void Check(bool condition, string name) { if (!condition) failures.Add(name); else { passed++; Console.WriteLine("PASS " + name); } }
        foreach (var field in new[] { "Language", "Theme" })
        {
            var profile = Path.Combine(root, "null-" + field); Directory.CreateDirectory(profile);
            var original = "{\"" + field + "\":null}";
            var path = Path.Combine(profile, "settings.json"); await File.WriteAllTextAsync(path, original);
            var recovered = new SettingsService(profile);
            Check(recovered.StorageWarning is not null && !string.IsNullOrWhiteSpace(recovered.EffectiveLanguage())
                && !recovered.TrySetLanguage("zh-CN", out _) && File.ReadAllText(path) == original,
                "null " + field + " uses safe defaults and preserves the unreadable settings file");
        }
        foreach (var input in new[] { "{\"Artifacts\":null}", "{\"Artifacts\":[null]}", "{\"Parameters\":null}", "{\"TaskIds\":[null]}", "{\"Parameters\":{\"sources\":\"[null]\"}}" })
        {
            var rejected = false;
            try { ProjectDocumentMigrator.Read(input); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "invalid project structure rejected: " + input);
        }
        var projects = new ProjectService(settings);
        var project = await projects.CreateAsync("subtitles", "", "whisper-small");
        var first = new AuroraTaskRecord { ProjectId=project.Id, Feature="subtitles" };
        await projects.AddTaskAsync(project, first);
        var subtitle = Path.Combine(root, "result.srt");
        await File.WriteAllTextAsync(subtitle, "1\n00:00:00,000 --> 00:00:01,000\nfixture\n");
        first.Status = AuroraTaskStates.Completed; first.OutputFiles = [subtitle];
        await projects.CompleteTaskAsync(project.Id, first);
        var next = new AuroraTaskRecord { ProjectId=project.Id, Feature="subtitles" };
        await projects.AddTaskAsync(project, next);
        var saved = projects.Find(project.Id)!;
        Check(saved.Artifacts.Any(a => a.Path == subtitle), "registering a later batch item preserves completed output from a newer project snapshot");
        Check(saved.TaskIds.Contains(first.Id) && saved.TaskIds.Contains(next.Id), "batch registration preserves both task identifiers");
        var concurrent = Enumerable.Range(0, 20).Select(_ => new AuroraTaskRecord { ProjectId=project.Id, Feature="subtitles" }).ToArray();
        await Task.WhenAll(concurrent.Select(t => projects.AddTaskAsync(project, t)));
        Check(concurrent.All(t => projects.Find(project.Id)!.TaskIds.Contains(t.Id)), "concurrent project registration loses no task IDs");
        await projects.SaveAsync(project);
        Check(projects.Find(project.Id)!.Artifacts.Any(a => a.Path == subtitle), "saving a stale draft preserves append-only result history");

        var badPath = Path.Combine(settings.Current.ProjectsRoot, "bad.arr");
        await File.WriteAllTextAsync(badPath, "{\"Artifacts\":[null]}");
        Check(projects.Artifacts().Any(a => a.Path == subtitle) && projects.RecoveryCount == 1 && File.Exists(badPath + ".recovery"), "a corrupt record is retained for recovery without breaking the library");
        Check(ProjectDocumentMigrator.Read("{\"Name\":\"legacy\",\"ExtraField\":true}").Name == "legacy", "legacy defaults and unknown extension fields remain supported");

        var queue = new TaskQueueService(settings);
        var canceled = queue.Create(project.Id, "explicit retry", "subtitles", "fixture.wav", "whisper-small");
        queue.Cancel(canceled.Id);
        var calls = 0;
        Task<OperationResult> Work(IProgress<TaskExecutionProgress> _, CancellationToken token) { calls++; return Task.FromResult(new OperationResult(true, "ok")); }
        await queue.RunAsync(canceled, Work);
        Check(calls == 0, "ordinary submission cannot revive a canceled task");
        var retried = await queue.RetryAsync(canceled, Work);
        Check(retried.Success && calls == 1 && canceled.Status == AuroraTaskStates.Completed, "explicit canceled-task retry runs exactly once");
        Check(!(await queue.RetryAsync(canceled, Work)).Success && calls == 1, "completed task cannot be retried by a duplicate action");

        var silent = Path.Combine(root, "silent.srt");
        await File.WriteAllTextAsync(silent, ""); await File.WriteAllTextAsync(Path.ChangeExtension(silent, ".json"), "{\"segments\":[]}");
        var exportRoot = Path.Combine(root, "exports"); Directory.CreateDirectory(exportRoot);
        await File.WriteAllTextAsync(Path.Combine(exportRoot, "silent.json"), "user-owned");
        var exported = await AuroraAudioStudio.Core.ArtifactExport.CopyAsync(silent, exportRoot);
        Check(ArtifactValidator.Inspect(exported).Subtitles == 0 && File.ReadAllText(Path.Combine(exportRoot, "silent.json")) == "user-owned", "silent subtitle export carries evidence and avoids sidecar collisions");

        foreach (var scenario in new[] { "bad-416", "bad-final-hash" })
        {
            var downloadSettings = new SettingsService(Path.Combine(root, scenario));
            var version = "2.0.1-beta.2";
            var cache = Path.Combine(downloadSettings.UpdatesRoot, version, "Aurora-Audio-Studio-Setup-x64.exe");
            var good = Encoding.UTF8.GetBytes("verified installer fixture");
            var expected = Convert.ToHexString(SHA256.HashData(good));
            var requests = 0;
            using var client = new HttpClient(new Handler(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith(".sha256")) return new(HttpStatusCode.OK) { Content=new StringContent(expected+"  "+WindowsReleasePolicy.InstallerName(version)) };
                requests++;
                if (scenario == "bad-416" && requests == 1) return new(HttpStatusCode.RequestedRangeNotSatisfiable);
                return new(HttpStatusCode.OK) { Content=new ByteArrayContent(scenario == "bad-final-hash" && requests == 1 ? Encoding.UTF8.GetBytes("corrupt") : good) };
            }));
            var updater = new UpdateService(downloadSettings, new LocalizationService(downloadSettings), client);
            var url="https://github.com/swy2018/Aurora-Audio-Studio/releases/download/v"+version+"/"+WindowsReleasePolicy.InstallerName(version);
            var update = new AppUpdateInfo(true, "2.0.1-beta.1", version, url, url, url+".sha256", "fixture");
            if (scenario == "bad-416") { Directory.CreateDirectory(Path.GetDirectoryName(cache)!); await File.WriteAllBytesAsync(cache, new byte[good.Length+1]); await File.WriteAllTextAsync(cache+".partial.sha256", expected); }
            await updater.DownloadAndInstallAsync(update, canInstall: () => false);
            if (scenario == "bad-final-hash")
            {
                Check(!File.Exists(cache) && !File.Exists(cache+".partial.sha256"), "bad final hash closes handles and removes invalid cache");
                await updater.DownloadAndInstallAsync(update, canInstall: () => false);
            }
            Check(File.Exists(cache) && File.ReadAllBytes(cache).SequenceEqual(good) && !File.Exists(cache+".partial.sha256"), "Windows update recovers without executing an installer: " + scenario);
        }
        Console.WriteLine($"Review checks passed: {passed}; failures: {failures.Count}. Fixtures retained: {root}");
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
