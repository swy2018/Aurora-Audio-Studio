using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Mac;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

if (args.FirstOrDefault() == "instance-client")
{
    using var second = new StudioInstance(args[1]);
    if (second.IsPrimary) return 7;
    await second.NotifyPrimaryAsync();
    return 0;
}
if (args.FirstOrDefault() == "webview-api")
{
    foreach (var name in new[] { "NavigationStarted", "NewWindowRequested", "WebMessageReceived" })
    {
        var ev = typeof(Avalonia.Controls.NativeWebView).GetEvent(name)!;
        var type = ev.EventHandlerType!.GenericTypeArguments.Last();
        Console.WriteLine(name + ": " + type.FullName);
        foreach (var p in type.GetProperties()) Console.WriteLine("  " + p.Name + " " + p.PropertyType);
    }
    return 0;
}
var root = Path.Combine(Path.GetTempPath(), "Aurora-framework-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); count++; }
async Task Reject(Func<Task> action, string name)
{
    var rejected = false;
    try { await action(); } catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException) { rejected = true; }
    Check(rejected, name);
}
try
{
    await AppUpdaterTests.RunAsync(root, Check, Reject);
    var workspace = new MacWorkspace(Path.Combine(root, "工作区 日本語"));
    Check(workspace.Catalog.Definitions.Count == 27, "original 27-component catalog preserved");
    var managedIds = new[] { "heartmula-3b", "indextts-2-5", "soulx-singer-svc", "qwen3-asr-06b", "qwen3-asr-17b", "qwen3-forced-aligner" };
    Check(managedIds.All(id => workspace.Runtime.Models[id].DownloadOnly && !workspace.Catalog.Find(id)!.IsRunnable),
        "all six Windows download-only models retain management without invented inference support");
    var utilityPath = MacUtilityAdapter.OutputPath(root, "transcription", "/素材/中文.wav");
    Check(Path.GetFileName(Path.GetDirectoryName(utilityPath)) == "AI扒谱" && Path.GetFileName(utilityPath).Contains("-中文-")
        && !Directory.Exists(utilityPath), "utility output naming matches Windows while Python retains directory creation ownership");
    Check(MacWorkspace.Features.All(f => workspace.Catalog.Definitions.Any(m => m.Feature == f)), "six feature groups preserved");
    Check(!Directory.Exists(workspace.Settings.Current.LocalAiRoot), "startup does not create or download model environments");
    Check(workspace.Catalog.Definitions.All(m => !workspace.Engines.IsAvailable(m.Id)), "unconfigured models cannot generate");
    Check(workspace.Catalog.Definitions.All(m => !workspace.Workbenches.IsAvailable(m.Id)), "model workbench entry does not imply an installed engine");
    await Reject(() => workspace.Workbenches.StartAsync("ace-step", "zh-CN", CancellationToken.None), "missing original workbench fails immediately without downloading");
    await Reject(() => workspace.Engines.ExecuteAsync(workspace.Drafts["music"], new Progress<TaskExecutionProgress>(), CancellationToken.None), "missing engine rejects execution");
    var settingsPathBefore = workspace.Settings.Current.OutputRoot;
    var unsaved = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(workspace.Settings.Current))!;
    unsaved.OutputRoot = Path.Combine(root, "not-saved");
    Check(workspace.Settings.TrySetLanguage("ja-JP", out _), "Japanese language persists immediately");
    var reread = new SettingsService(workspace.Settings.AppDataRoot);
    Check(reread.Current.Language == "ja-JP" && reread.Current.OutputRoot == settingsPathBefore, "language switch does not commit unsaved paths");
    Check(workspace.Settings.TrySetLanguage("zh-CN", out _), "can return from Japanese to Chinese");
    foreach (var language in new[] { "zh-CN", "zh-TW", "en-US", "ja-JP" })
    {
        workspace.Settings.TrySetLanguage(language, out _);
        var text = new StudioText(workspace);
        Check(StudioText.Extra.All(p => p.Value.Length == 4 && p.Value.All(v => !string.IsNullOrWhiteSpace(v))), "complete shell translations " + language);
        Check(MacWorkspace.Features.All(f => text[f] != f), "feature navigation localized " + language);
    }
    var draft = workspace.Drafts["voice"];
    draft.Name = "中文 日本語 draft"; draft.Prompt = "保留输入\nHello 日本語"; draft.Reference = @"C:\old\voice.wav";
    var project = await workspace.SaveProjectAsync(draft);
    var secondWorkspace = new MacWorkspace(workspace.Settings.AppDataRoot);
    Check(secondWorkspace.Drafts["voice"].Prompt == draft.Prompt, "draft survives relaunch");
    Check(secondWorkspace.Projects.Find(project.Id)?.Parameters["prompt"] == draft.Prompt, "ARR project preserves prompt");
    Check(secondWorkspace.Projects.Find(project.Id)?.TaskIds.Count == 0 && secondWorkspace.Queue.Items.Count == 0, "draft is not a fake inference task");
    var hostilePath = Path.Combine(root, "must-not-overwrite.txt"); File.WriteAllText(hostilePath, "original");
    project.FilePath = hostilePath;
    var external = Path.Combine(root, "Windows.arr"); File.WriteAllText(external, JsonSerializer.Serialize(project));
    var imported = await workspace.ImportProjectAsync(external);
    Check(File.ReadAllText(hostilePath) == "original" && imported.ProjectId != project.Id, "project import ignores embedded destination and clones ID");
    Check(imported.Reference == @"C:\old\voice.wav", "Windows source path is preserved for explicit relocation");
    var utility = workspace.Drafts["subtitles"];
    utility.Sources = ["/素材/first.mp4", "/素材/second.wav"]; utility.Source = utility.Sources[0];
    utility.SourceLanguage = "ja"; utility.Option = "quality"; utility.TrackMode = "multi-stem";
    var utilityProject = await workspace.SaveProjectAsync(utility);
    var reopened = workspace.LoadProject(utilityProject);
    Check(reopened.Sources.SequenceEqual(utility.Sources) && reopened.SourceLanguage == "ja" && reopened.Option == "quality", "batch sources, preset and source language survive ARR reopen");
    Check(UtilityPresets.Model("separation", "two-stem", "quality") == "roformer-vocals"
        && UtilityPresets.Model("separation", "multi-stem", "fast") == "demucs"
        && UtilityPresets.Model("separation", "multi-stem", "recommended") == "roformer", "separation presets match Windows two-stem and multi-stem behavior");
    Check(UtilityPresets.Model("transcription", "two-stem", "fast") == "basic-pitch"
        && UtilityPresets.Model("transcription", "two-stem", "quality") == "transkun", "transcription presets match Windows engines");
    Check(UtilityPresets.Model("subtitles", "two-stem", "fast") == "whisper-small"
        && UtilityPresets.Model("subtitles", "two-stem", "recommended") == "whisper-large-v3-turbo"
        && UtilityPresets.Model("subtitles", "two-stem", "quality") == "whisper-large-v3", "subtitle presets match Windows quality mapping");
    Check(!ModelWorkbenchConnection.IsLocal(new Uri("https://example.com")) && !ModelWorkbenchConnection.IsLocal(new Uri("file:///tmp/example.html"))
        && !ModelWorkbenchConnection.IsLocal(new Uri("http://user:password@127.0.0.1:7860")), "workbench registry accepts only local HTTP model UIs without URL credentials");
    var releases = 0;
    var connection = new ModelWorkbenchConnection(new Uri("http://127.0.0.1:7860"), () => releases++);
    Check(connection.Owns(new Uri("http://127.0.0.1:7860/gradio/")) && !connection.Owns(new Uri("http://127.0.0.1:7861/")), "embedded workbench navigation stays within its model origin");
    connection.Dispose(); connection.Dispose();
    Check(releases == 1, "model workbench process lifetime is released exactly once");
    var localizationScript = WorkbenchLocalization.Script(workspace.Localization, "ja-JP");
    Check(!localizationScript.Contains("__AURORA_TRANSLATIONS__") && !localizationScript.Contains("__AURORA_LANGUAGE_INDEX__") && localizationScript.Contains("NotoSansJP"), "original Windows workbench styling and translations are bundled");
    var model = workspace.Catalog.Find("qwen3-tts-base")!;
    Check(!MacWorkspace.ModelPath(root, model).Contains('\\'), "Windows catalog segments resolve as Unix paths");
    var invalid = Path.Combine(root, "invalid.wav"); File.WriteAllText(invalid, "not audio");
    await Reject(() => workspace.ImportArtifactAsync(invalid), "invalid audio cannot enter results");
    var wave = Path.Combine(root, "音频 日本語.wav");
    using (var w = new BinaryWriter(File.Create(wave)))
    {
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + 16000); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(8000); w.Write(16000); w.Write((short)2); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(16000);
        for (var i = 0; i < 8000; i++) w.Write((short)(Math.Sin(2 * Math.PI * 220 * i / 8000) * 5000));
    }
    var result = await workspace.ImportArtifactAsync(wave);
    Check(File.Exists(wave) && result.Artifacts.Count == 1 && result.Artifacts[0].Path != wave, "valid audio import copies and retains source");
    Check(result.Parameters["origin"] == "imported" && workspace.Queue.Items.Count == 0, "import is not claimed as generated output");
    var exportFolder = Directory.CreateDirectory(Path.Combine(root, "exports")).FullName;
    var exported = await ArtifactExport.CopyAsync(wave, exportFolder);
    var duplicateExport = await ArtifactExport.CopyAsync(wave, exportFolder);
    Check(exported != duplicateExport && File.ReadAllBytes(exported).SequenceEqual(File.ReadAllBytes(wave)), "export preserves original bytes and numbers duplicate filenames");
    var silent = Path.Combine(root, "silent.srt");
    File.WriteAllText(silent, ""); File.WriteAllText(Path.ChangeExtension(silent, ".json"), "{\"segments\":[]}");
    var silentExport = await ArtifactExport.CopyAsync(silent, exportFolder);
    ArtifactValidator.Validate(silentExport);
    Check(File.Exists(Path.ChangeExtension(silentExport, ".json")), "exported silent subtitles retain required recognition evidence");
    var receiptWorkspace = new MacWorkspace(Path.Combine(root, "receipt-appdata"));
    var realOutputs = Directory.CreateDirectory(Path.Combine(root, "AI成果")).FullName;
    var outputLink = Path.Combine(root, "OutputLink"); Directory.CreateSymbolicLink(outputLink, realOutputs);
    receiptWorkspace.Settings.Current.OutputRoot = outputLink;
    receiptWorkspace.Settings.Current.ProjectsRoot = Path.Combine(realOutputs, "projects");
    Check(receiptWorkspace.Settings.TrySave(receiptWorkspace.Settings.Current, out _), "receipt fixture uses user's symlink-style output layout");
    var receiptId = Guid.NewGuid().ToString("N");
    var receiptAudio = Path.Combine(realOutputs, "generated.wav"); File.Copy(wave, receiptAudio);
    var receipts = Directory.CreateDirectory(Path.Combine(receiptWorkspace.Settings.AppDataRoot, "WorkbenchReceipts")).FullName;
    await File.WriteAllTextAsync(Path.Combine(receipts, receiptId + ".json"), JsonSerializer.Serialize(new {
        id = receiptId, feature = "voice", modelId = "qwen3-tts-custom", path = receiptAudio, device = "mps", modelVersion = "fixture-version", runtimeSignature = "stale-fixture-signature"
    }));
    Check(await receiptWorkspace.WorkbenchResults.ImportAsync() == 1, "completed workbench audio imports through output symlink");
    Check(receiptWorkspace.Projects.Find(receiptId)?.ModelVersion == "fixture-version" && receiptWorkspace.Queue.Items.Single().Device == "mps", "receipt preserves generation-time version and device");
    Check(await receiptWorkspace.WorkbenchResults.ImportAsync() == 0, "duplicate file notifications do not duplicate results");
    var replayWorkspace = new MacWorkspace(receiptWorkspace.Settings.AppDataRoot);
    Check(await replayWorkspace.WorkbenchResults.ImportAsync() == 0 && replayWorkspace.Queue.Items.Count == 1, "relaunch replays receipts without duplicating completed projects or tasks");
    var outsideId = Guid.NewGuid().ToString("N");
    await File.WriteAllTextAsync(Path.Combine(receipts, outsideId + ".json"), JsonSerializer.Serialize(new { id = outsideId, feature = "voice", modelId = "qwen3-tts-custom", path = wave, device = "mps" }));
    Check(await replayWorkspace.WorkbenchResults.ImportAsync() == 0 && replayWorkspace.Projects.Find(outsideId) is null, "receipt outside configured output root is not imported");
    var invalidSettings = new AppSettings { LocalAiRoot = "/", OutputRoot = root, ProjectsRoot = root };
    Check(!workspace.Settings.TrySave(invalidSettings, out _), "disk-root storage rejected");
    var task = workspace.Queue.Create(project.Id, "cancellation fixture", "voice", wave, "test-engine");
    var started = new TaskCompletionSource();
    var running = workspace.Queue.RunAsync(task, async (_, token) => { started.SetResult(); await Task.Delay(10000, token); return new OperationResult(true, "unexpected"); });
    await started.Task; workspace.Queue.Cancel(task.Id); await running;
    Check(task.Status == AuroraTaskStates.Canceled && task.OutputFiles.Count == 0, "cancel clears active work without reporting success");
    workspace.Queue.Pause();
    var pausedTask = workspace.Queue.Create(project.Id, "paused fixture", "transcription", wave, "test");
    var enteredPaused = false;
    var pausedRun = workspace.Queue.RunAsync(pausedTask, (_, _) => { enteredPaused = true; return Task.FromResult(new OperationResult(true, "done", wave, Outputs: [wave])); });
    await Task.Delay(100);
    Check(!enteredPaused && pausedTask.Status == AuroraTaskStates.Waiting, "paused queue does not start new work");
    workspace.Queue.Resume(); await pausedRun;
    Check(enteredPaused && pausedTask.Status == AuroraTaskStates.Completed, "resume runs queued work and saves results");
    workspace.Settings.Current.SafeMode = true;
    var safeTask = workspace.Queue.Create(project.Id, "safe mode fixture", "transcription", wave, "test");
    var enteredSafe = false;
    var safeResult = await workspace.Queue.RunAsync(safeTask, (_, _) => { enteredSafe = true; return Task.FromResult(new OperationResult(true, "unexpected")); });
    Check(!safeResult.Success && !enteredSafe, "safe mode blocks inference before invoking the engine");
    workspace.Settings.Current.SafeMode = false;
    workspace.Engines.Register(new RetryFixtureEngine(wave));
    await workspace.Projects.AddTaskAsync(workspace.Projects.Find(project.Id)!, safeTask);
    var countBeforeRetry = workspace.Queue.Items.Count;
    workspace.Queue.Pause();
    var retryRun = workspace.RetryTaskAsync(safeTask);
    var duplicateRetry = await workspace.RetryTaskAsync(safeTask);
    Check(!duplicateRetry.Success && workspace.Queue.Items.Count == countBeforeRetry && !safeTask.CanRetry,
        "retry reuses the original ID and rejects duplicate submissions while queued");
    workspace.Queue.Resume(); await retryRun;
    Check(safeTask.Status == AuroraTaskStates.Completed && workspace.Projects.Find(project.Id)!.TaskIds.Contains(safeTask.Id),
        "retry completes the original project-linked record");
    safeTask.Status = AuroraTaskStates.Canceled;
    await workspace.RetryTaskAsync(safeTask);
    Check(safeTask.Status == AuroraTaskStates.Completed && workspace.Queue.Items.Count == countBeforeRetry,
        "a previously canceled task can be retried without creating another record");
    workspace.Queue.Create(project.Id, "recovery fixture", "music", "", "test");
    var recovered = new MacWorkspace(workspace.Settings.AppDataRoot);
    Check(recovered.Queue.Items.Any(t => t.Title == "recovery fixture" && t.Status == AuroraTaskStates.Interrupted), "unfinished task recovers as interrupted");
    using (var server = new LocalWorkbenchServer())
    using (var client = new HttpClient())
    {
        var url = server.AddPage("<p>test 日本語</p>");
        Check((await client.GetStringAsync(url)).Contains("日本語"), "embedded server serves UTF-8 local page");
        Check((await client.GetAsync(new Uri(server.BaseUri, "unknown"))).StatusCode == HttpStatusCode.NotFound, "unknown resource rejected");
        Check((await client.GetAsync(new Uri(server.BaseUri, "/etc/passwd"))).StatusCode == HttpStatusCode.NotFound, "filesystem paths are not exposed");
        Check((await client.PostAsync(url, new StringContent("test"))).StatusCode == HttpStatusCode.NotFound, "web server has no mutation endpoint");
        Check(!server.Owns(new Uri("https://example.com/")), "external workbench origin rejected");
        var audioUrl = server.AddFile(wave, "audio/wav");
        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, audioUrl);
        rangeRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(40, 49);
        using var partial = await client.SendAsync(rangeRequest);
        Check(partial.StatusCode == HttpStatusCode.PartialContent && (await partial.Content.ReadAsByteArrayAsync()).SequenceEqual(File.ReadAllBytes(wave).Skip(40).Take(10)), "audio seek returns correct byte range");
        using var suffixRequest = new HttpRequestMessage(HttpMethod.Get, audioUrl);
        suffixRequest.Headers.TryAddWithoutValidation("Range", "bytes=-8");
        using var suffix = await client.SendAsync(suffixRequest);
        Check((await suffix.Content.ReadAsByteArrayAsync()).SequenceEqual(File.ReadAllBytes(wave).TakeLast(8)), "audio suffix range supported");
        using var invalidRange = new HttpRequestMessage(HttpMethod.Get, audioUrl);
        invalidRange.Headers.TryAddWithoutValidation("Range", "bytes=999999-");
        Check((await client.SendAsync(invalidRange)).StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable, "out of bounds audio range rejected");
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, audioUrl));
        Check(head.Content.Headers.ContentLength == new FileInfo(wave).Length && (await head.Content.ReadAsByteArrayAsync()).Length == 0, "audio HEAD reports size without body");
        server.Remove(audioUrl);
        Check((await client.GetAsync(audioUrl)).StatusCode == HttpStatusCode.NotFound, "old audio resource is released");
        server.Dispose(); await server.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Check(server.Completion.IsCompletedSuccessfully, "local server shuts down cleanly");
    }
    using (var primary = new StudioInstance(Path.Combine(root, "instance")))
    {
        var activated = new TaskCompletionSource(); primary.ActivationRequested += () => activated.TrySetResult();
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!) == "dotnet") info.ArgumentList.Add(typeof(StudioText).Assembly.Location.Replace("Aurora.dll", "AuroraAudioStudio.MacTests.dll"));
        info.ArgumentList.Add("instance-client"); info.ArgumentList.Add(Path.Combine(root, "instance"));
        using var child = Process.Start(info)!; await child.WaitForExitAsync();
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(child.ExitCode == 0, "second process activates owner without another instance");
    }
    using (var runtime = new MacRuntime(workspace.Settings))
    {
        runtime.Models["managed-fixture"] = new("download", 0, true, ["weights.bin"]);
        var managedRoot = runtime.Current("managed-fixture"); Directory.CreateDirectory(managedRoot);
        File.WriteAllText(Path.Combine(managedRoot, "weights.bin"), "abc");
        File.WriteAllText(Path.Combine(managedRoot, "receipt.json"), JsonSerializer.Serialize(new { installed_at = 1788600000, assets = Array.Empty<object>(), files = new[] { new { path = "weights.bin", size = 3 } } }));
        Check(runtime.IsInstalled("managed-fixture") && !runtime.IsReady("managed-fixture") && runtime.ModelStatus("managed-fixture").Health.StartsWith("仅模型管理"),
            "download-only model can be installed without an inference environment and is never runnable");
        runtime.Models["acceptance-model"] = new("acceptance", 0.001);
        var modelRoot = runtime.Current("acceptance-model"); Directory.CreateDirectory(modelRoot);
        var envRoot = Path.GetDirectoryName(Path.GetDirectoryName(runtime.Python("acceptance")))!; Directory.CreateDirectory(Path.Combine(envRoot, "bin"));
        File.WriteAllText(runtime.Python("acceptance"), "fixture"); File.WriteAllText(Path.Combine(envRoot, "aurora-runtime.json"), "{}");
        File.WriteAllText(Path.Combine(modelRoot, "weights.bin"), "abc");
        File.WriteAllText(Path.Combine(modelRoot, "receipt.json"), JsonSerializer.Serialize(new { installed_at = 1788600000, assets = new[] { new { revision = "1234567890abcdef" } }, files = new[] { new { path = "weights.bin", size = 3 } } }));
        Check(runtime.ModelStatus("acceptance-model") is { Installed: true, Version: "12345678", HasUpdate: false }, "Mac card uses actual receipt version and installed state");
        runtime.RecordSuccessfulRun("acceptance-model", "CPU");
        Check(runtime.ModelStatus("acceptance-model").Health.StartsWith("短任务已验证"), "successful run is shown against its exact model and environment signature");
        File.WriteAllText(Path.Combine(envRoot, "aurora-runtime.json"), "{\"repaired\":true}");
        Check(!runtime.ModelStatus("acceptance-model").Health.StartsWith("短任务已验证"), "environment change invalidates old run verification");
        File.WriteAllText(Path.Combine(modelRoot, "update-status.json"), "{\"available\":true,\"message\":\"new version\"}");
        Check(runtime.ModelStatus("acceptance-model").HasUpdate, "available update persists across card refreshes");
        using (var reopenedRuntime = new MacRuntime(workspace.Settings))
        {
            reopenedRuntime.Models["acceptance-model"] = new("acceptance", 0.001);
            Check(reopenedRuntime.ModelStatus("acceptance-model").HasUpdate, "update result survives application restart");
        }
        File.WriteAllText(Path.Combine(modelRoot, "update-status.json"), "{\"available\":false,\"message\":\"current\"}");
        Check(!runtime.ModelStatus("acceptance-model").HasUpdate, "successful update removes update-all candidate");
        File.WriteAllText(Path.Combine(modelRoot, "health.json"), "{\"at\":1700000000,\"failed\":true,\"message\":\"import failed\"}");
        Check(!runtime.IsReady("acceptance-model") && runtime.ModelStatus("acceptance-model").Health.StartsWith("需要修复"), "failed health check blocks startup and replaces stale verified status");
        File.WriteAllText(Path.Combine(modelRoot, "weights.bin"), "corrupt size");
        Check(!runtime.ModelStatus("acceptance-model").Installed, "damaged model cannot be shown as installed");
        File.WriteAllText(Path.Combine(modelRoot, "receipt.json"), "invalid json");
        Check(runtime.ModelStatus("acceptance-model").Version == "—", "damaged receipt is displayed without crashing model center");
        string bytecodeSetting = "";
        await runtime.RunAsync("/bin/sh", ["-c", "printenv PYTHONDONTWRITEBYTECODE"], new InlineProgress<string>(line => bytecodeSetting = line), CancellationToken.None);
        Check(bytecodeSetting == "1", "model subprocesses cannot write Python bytecode into the signed app");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var canceled = false;
        try { await runtime.RunAsync("/bin/sh", ["-c", "sleep 30 &\nwait"], new InlineProgress<string>(_ => { }), cancellation.Token); }
        catch (OperationCanceledException) { canceled = true; }
        Check(canceled, "runtime process tree cancels promptly and drains output streams");
        await Reject(() => runtime.ManageAsync("../../outside", "uninstall", new Progress<string>(), CancellationToken.None), "maintenance rejects models outside the catalog");
        await Reject(() => runtime.RunAsync("/bin/sh", ["-c", "exit 7"], new Progress<string>(), CancellationToken.None), "nonzero engine exits are failures");
    }
    Console.WriteLine($"{count} checks passed. No model downloads or inference were performed.");
    return 0;
}
finally { Console.WriteLine("Test files retained: " + root); }

sealed class RetryFixtureEngine(string output) : IStudioEngine
{
    public string ModelId => "test";
    public Task<OperationResult> ExecuteAsync(StudioDraft draft, IProgress<TaskExecutionProgress> progress, CancellationToken token)
        => Task.FromResult(new OperationResult(true, "retry done", Path.GetDirectoryName(output), Outputs: [output]));
}
