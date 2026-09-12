using System.Net;
using System.Reflection;
using System.Net.Http.Headers;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class MaintenanceRegression
{
    public static async Task RunAsync()
    {
        var queued = new Queue<Action>();
        var displayed = -1;
        var old = new OperationProgress<int>(value => displayed = value, queued.Enqueue);
        old.Report(27);
        old.Dispose();
        displayed = 100;
        while (queued.TryDequeue(out var callback)) callback();
        Check(displayed == 100, "queued progress cannot resurrect a completed operation");
        old.Report(28);
        Check(queued.Count == 0, "finished producers cannot enqueue more progress");
        using var cancellation = new CancellationTokenSource();
        using var canceled = new OperationProgress<int>(value => displayed = value, queued.Enqueue, cancellation.Token);
        canceled.Report(1);
        cancellation.Cancel();
        while (queued.TryDequeue(out var callback)) callback();
        Check(displayed == 100, "queued progress cannot undo cancellation UI");
        using var next = new OperationProgress<int>(value => displayed = value, queued.Enqueue);
        next.Report(2);
        old.Report(3);
        while (queued.TryDequeue(out var callback)) callback();
        Check(displayed == 2, "new operations are independent of stale reporters");

        var root = Path.Combine(Path.GetTempPath(), "Aurora-MaintenanceTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settings = new SettingsService(Path.Combine(root, "state"));
        var catalog = new ModelCatalogService(settings);
        var content = "downloaded model fixture"u8.ToArray();
        using var transport = new HttpClient(new Handler((request, token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) })));
        var updater = new ModelUpdateService(catalog, settings, transport);
        var destination = Path.Combine(root, "model.bin");
        await Download(updater, destination);
        Check(File.ReadAllBytes(destination).SequenceEqual(content), "download closes the file before promoting its partial on Windows");
        Check(!File.Exists(destination + ".part"), "successful download leaves no partial filename");
        var resumePath = Path.Combine(root, "resume.bin");
        File.WriteAllBytes(resumePath + ".part", content[..5]);
        using var resumeTransport = new HttpClient(new Handler((request, token) =>
        {
            Check(request.Headers.Range?.Ranges.Single().From == 5, "resume requests the retained byte offset");
            var remaining = new ByteArrayContent(content[5..]);
            remaining.Headers.ContentRange = new ContentRangeHeaderValue(5, content.Length - 1, content.Length);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = remaining });
        }));
        await Download(new ModelUpdateService(catalog, settings, resumeTransport), resumePath);
        Check(File.ReadAllBytes(resumePath).SequenceEqual(content), "resumed download is complete without duplicate bytes");

        var shortPath = Path.Combine(root, "short.bin");
        File.WriteAllText(shortPath, "existing file");
        using var shortTransport = new HttpClient(new Handler((request, token) =>
        {
            var body = new ByteArrayContent(content);
            body.Headers.ContentLength = content.Length + 4;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
        }));
        var rejected = false;
        try { await Download(new ModelUpdateService(catalog, settings, shortTransport), shortPath); }
        catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllText(shortPath) == "existing file" && File.Exists(shortPath + ".part"), "incomplete download preserves the old file and partial for retry");

        using var timeoutTransport = new HttpClient(new Handler((request, token) => throw new TaskCanceledException("simulated metadata timeout")));
        var timeoutUpdater = new ModelUpdateService(catalog, settings, metadataTransport: timeoutTransport);
        var probe = new ModelDefinition("maintenance-probe", "Probe", "voice", "probe", "marker", "test", "huggingface", "test/probe");
        var probeRoot = Path.Combine(settings.Current.LocalAiRoot, probe.RelativeRoot);
        Directory.CreateDirectory(probeRoot);
        File.WriteAllText(Path.Combine(probeRoot, probe.Marker), "test");
        Check(catalog.IsInstalled(probe), "metadata fixture is ready without installing a model");
        var timeout = await timeoutUpdater.CheckAsync(probe);
        Check(!timeout.Success && timeout.Path == "timeout", "metadata timeout is a failed check, not user cancellation");
        using var userCanceled = new CancellationTokenSource();
        userCanceled.Cancel();
        var stopped = false;
        try { await timeoutUpdater.CheckAsync(probe, userCanceled.Token); }
        catch (OperationCanceledException) { stopped = true; }
        Check(stopped, "explicit user cancellation still propagates");
        var missing = await timeoutUpdater.CheckAllAsync();
        Check(missing.Count == catalog.Definitions.Count && missing.Values.All(x => x.Path == "not-installed"), "all absent models are reported without network checks");
        foreach (var id in new[] { "indextts-2-5", "soulx-singer-svc" })
        {
            var item = catalog.Find(id)!;
            var path = Path.Combine(settings.Current.LocalAiRoot, item.RelativeRoot, item.Marker);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fixture");
            Check(catalog.IsInstalled(item), "batch metadata fixture is present: " + id);
        }
        var partialChecks = await timeoutUpdater.CheckAllAsync();
        Check(partialChecks.Count == catalog.Definitions.Count && partialChecks.Values.Count(x => x.Path == "timeout") == 2,
            "two upstream timeouts do not discard the other 25 model check results");
        var safeResult = await timeoutUpdater.UpdateAsync(probe);
        Check(!safeResult.Success && safeResult.Path == "timeout", "installation timeout is not reported as user cancellation");
        Check(Directory.GetFiles(settings.LogsRoot, "model-maintenance-*.log").Any(x => File.ReadAllText(x).Contains("simulated metadata timeout")), "failed maintenance retains its diagnostic exception in a log");
        var guard = new UpdateFlowGuard();
        Check(guard.TryBegin() && guard.IsRunning && !guard.TryBegin(), "overlapping maintenance cannot acquire the operation guard");
        guard.End();
        Check(!guard.IsRunning && guard.TryBegin(), "completion releases the guard for the next operation");
        Console.WriteLine("Maintenance regression checks passed. Fixtures: " + root);
    }

    private static Task Download(ModelUpdateService updater, string destination) =>
        (Task)typeof(ModelUpdateService).GetMethod("DownloadFileAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(updater, ["https://example.invalid/model", destination, null, CancellationToken.None, null])!;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
