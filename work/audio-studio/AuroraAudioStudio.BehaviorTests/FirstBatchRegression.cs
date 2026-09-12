using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class FirstBatchRegression
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var root = Path.Combine(Path.GetTempPath(), "Aurora-FirstBatch-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(root);
        var store = new UtilityDraftStore(settings);
        var draft = new UtilityDraft([Path.Combine(root, "暂时离线🎵.wav")], "", "demucs", "fast", "multi-stem", "ja");
        Check(store.TrySave("separation", draft, out _), "draft saves without modifying source media");
        Check(store.TrySave("subtitles", draft with { ModelId = "whisper-small", SourceLanguage = "en" }, out _), "each feature owns an independent draft");
        var restored = new UtilityDraftStore(settings);
        Check(restored.Get("separation")?.ModelId == "demucs" && restored.Get("subtitles")?.SourceLanguage == "en", "draft parameters survive restarting the store");
        Check(restored.Get("separation")!.Sources.Single().Contains("🎵"), "missing Unicode source paths survive draft recovery");
        var before = File.ReadAllText(Path.Combine(root, "utility-drafts.json"));
        Directory.CreateDirectory(Path.Combine(root, "utility-drafts.json.tmp"));
        Check(!store.TrySave("separation", draft with { ModelId = "roformer" }, out _) && File.ReadAllText(Path.Combine(root, "utility-drafts.json")) == before, "failed draft save preserves the last good file");
        var malformedSettings = new SettingsService(Path.Combine(root, "malformed"));
        var malformedPath = Path.Combine(malformedSettings.AppDataRoot, "utility-drafts.json");
        File.WriteAllText(malformedPath, "{\"separation\":{\"Sources\":null}}");
        var malformed = new UtilityDraftStore(malformedSettings);
        Check(malformed.Warning is not null && malformed.Get("separation") is null, "malformed draft fields cannot crash app startup");
        Check(malformed.TrySave("separation", draft, out _) && File.ReadAllText(malformedPath + ".recovery").Contains("\"Sources\":null"), "recovering a malformed draft retains its original data");

        var catalog = new ModelCatalogService(settings);
        var queue = new TaskQueueService(settings);
        var projects = new ProjectService(settings, catalog);
        var id = Guid.NewGuid().ToString("N"); var instance = Guid.NewGuid().ToString("N");
        queue.ObserveWorkbench(id, "voice", "qwen3-tts-base", 100, instance, 1, "running", "");
        Check(queue.Items.Single().IsIndeterminate && !queue.Items.Single().CanRetry, "workbench progress is indeterminate and cannot use native retry");
        Check(!queue.ObserveWorkbench(id, "voice", "qwen3-tts-base", 100, instance, 1, "failed", "old"), "duplicate activity sequence cannot overwrite current state");
        queue.CancelWorkbench(100);
        Check(!queue.ObserveWorkbench(id, "voice", "qwen3-tts-base", 100, instance, 3, "running", "") && queue.Items.Single().Status == AuroraTaskStates.Canceled, "late workbench events cannot resurrect canceled tasks");
        var completedId = Guid.NewGuid().ToString("N");
        queue.ObserveWorkbench(completedId, "voice", "qwen3-tts-base", 101, instance, 1, "running", "");
        var receipts = Path.Combine(root, "WorkbenchReceipts"); Directory.CreateDirectory(receipts);
        for (var i = 0; i < 2; i++)
        {
            var wave = Path.Combine(settings.Current.OutputRoot, "result" + i + ".wav");
            using (var writer = new BinaryWriter(File.Create(wave)))
            {
                writer.Write("RIFF"u8); writer.Write(40); writer.Write("WAVEfmt "u8); writer.Write(16);
                writer.Write((ushort)1); writer.Write((ushort)1); writer.Write(16000); writer.Write(32000);
                writer.Write((ushort)2); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(4); writer.Write((short)100); writer.Write((short)-100);
            }
            var receipt = Guid.NewGuid().ToString("N");
            File.WriteAllText(Path.Combine(receipts, receipt + ".json"), JsonSerializer.Serialize(new { id = receipt, taskId = completedId, feature = "voice", modelId = "qwen3-tts-base", path = wave, device = "fixture" }));
        }
        var importer = new WorkbenchResultService(settings, projects, queue, catalog);
        Check(await importer.ImportAsync() == 2, "both outputs of one generation are imported");
        Check(queue.Items.Count == 2 && queue.Items.Single(x => x.Id == completedId).OutputFiles.Count == 2, "multiple results complete one existing workbench task");
        Check(await new WorkbenchResultService(settings, projects, queue, catalog).ImportAsync() == 0, "workbench result deduplication survives restart");
        Check(!queue.ObserveWorkbench(completedId, "voice", "qwen3-tts-base", 101, instance, 4, "running", ""), "late workbench status cannot replace successful completion");
        var otherId = Guid.NewGuid().ToString("N");
        queue.ObserveWorkbench(otherId, "voice", "qwen3-tts-base", 100, "another-instance", 1, "running", "");
        queue.CancelWorkbench(100, instance);
        Check(queue.Items.Single(x => x.Id == otherId).Status == AuroraTaskStates.Running, "cancel never targets a reused PID from another launch");

        var model = new ModelDefinition("repair-fixture", "Fixture", "voice", "fixture", "model.bin", "fixture", "huggingface", "fixture/repo");
        var modelRoot = Path.Combine(settings.Current.LocalAiRoot, "fixture");
        Directory.CreateDirectory(modelRoot);
        var revision = new string('a', 40);
        File.WriteAllText(Path.Combine(modelRoot, ".aurora-revision"), revision);
        File.WriteAllText(Path.Combine(modelRoot, "keep.txt"), "unchanged");
        var bytes = new byte[] { 1, 2, 3 };
        var handler = new RepairTransport(revision, bytes);
        using var client = new HttpClient(handler);
        var updater = new ModelUpdateService(catalog, settings, client, client);
        var plan = await updater.InspectRepairAsync(model);
        Check(plan.Kind == ModelRepairKind.MissingFiles && plan.Files.Count == 1 && plan.DownloadBytes == 3, "repair plan includes only required missing weights, not optional siblings");
        Check(File.ReadAllText(Path.Combine(modelRoot, "keep.txt")) == "unchanged" && !File.Exists(Path.Combine(modelRoot, "model.bin")), "inspection performs no model writes");
        var result = await updater.RepairAsync(model, plan, null, CancellationToken.None);
        Check(result.Success && File.ReadAllBytes(Path.Combine(modelRoot, "model.bin")).SequenceEqual(bytes), "pinned missing weight is downloaded and hash-verified");
        Check(File.ReadAllText(Path.Combine(modelRoot, "keep.txt")) == "unchanged" && File.ReadAllText(Path.Combine(modelRoot, ".aurora-revision")) == revision, "repair preserves existing files and revision");
        Check((await updater.InspectRepairAsync(model)).Kind == ModelRepairKind.Healthy, "healthy files do not trigger deployment");
        var requests = handler.Requests;
        Check((await updater.RepairAsync(model, await updater.InspectRepairAsync(model), null, CancellationToken.None)).Success && handler.Requests == requests, "healthy repair is a no-network no-write operation");
        var escaped = false; try { ModelUpdateService.RepairDestination(modelRoot, "../outside.bin"); } catch (InvalidDataException) { escaped = true; }
        Check(escaped, "repair destination rejects traversal");
        var brokenModel = model with { Id = "repair-bad-hash", RelativeRoot = "bad-hash" };
        var brokenRoot = Path.Combine(settings.Current.LocalAiRoot, brokenModel.RelativeRoot);
        Directory.CreateDirectory(brokenRoot);
        File.WriteAllText(Path.Combine(brokenRoot, ".aurora-revision"), revision);
        File.WriteAllText(Path.Combine(brokenRoot, "keep.txt"), "keep-original");
        var brokenPlan = await updater.InspectRepairAsync(brokenModel);
        handler.CorruptDownload = true;
        var rejected = false;
        try { await updater.RepairAsync(brokenModel, brokenPlan, null, CancellationToken.None); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected && !File.Exists(Path.Combine(brokenRoot, "model.bin"))
            && File.ReadAllText(Path.Combine(brokenRoot, "keep.txt")) == "keep-original", "bad download hash cannot promote files or overwrite good model data");
        handler.CorruptDownload = false;
        using var canceledRepair = new CancellationTokenSource();
        canceledRepair.Cancel();
        requests = handler.Requests;
        rejected = false;
        try { await updater.RepairAsync(brokenModel, brokenPlan, null, canceledRepair.Token); }
        catch (OperationCanceledException) { rejected = true; }
        Check(rejected && handler.Requests == requests && !File.Exists(Path.Combine(brokenRoot, "model.bin")), "canceled repair starts no request and changes no model files");
        File.WriteAllText(Path.Combine(brokenRoot, "model.bin"), "another-writer");
        rejected = false;
        try { await updater.RepairAsync(brokenModel, brokenPlan, null, CancellationToken.None); }
        catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllText(Path.Combine(brokenRoot, "model.bin")) == "another-writer", "stale repair plan cannot overwrite a newly present file");
        Console.WriteLine($"First batch regressions passed: {count}. Fixtures: {root}");
    }

    private sealed class RepairTransport(string revision, byte[] bytes) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public bool CorruptDownload { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = request.RequestUri!.AbsolutePath.Contains("/api/models/")
                ? new StringContent(JsonSerializer.Serialize(new { sha = revision, siblings = new[] {
                    new { rfilename = "model.bin", lfs = new { size = bytes.Length, sha256 = hash } },
                    new { rfilename = "optional.bin", lfs = new { size = bytes.Length, sha256 = hash } } } }))
                : new ByteArrayContent(CorruptDownload ? bytes.Select(x => (byte)(x + 1)).ToArray() : bytes);
            return Task.FromResult(response);
        }
    }
}
