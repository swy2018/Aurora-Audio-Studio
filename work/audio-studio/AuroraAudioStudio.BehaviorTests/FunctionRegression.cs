using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class FunctionRegression
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var root = Path.Combine(Path.GetTempPath(), "Aurora-FunctionTests-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(root);
        var queue = new TaskQueueService(settings);
        var task = queue.Create("p", "retry", "subtitles", "fixture.wav", "whisper-small");
        var held = new HeldContext();
        var context = SynchronizationContext.Current;
        Task<OperationResult> first;
        try
        {
            SynchronizationContext.SetSynchronizationContext(held);
            first = queue.RunAsync(task, (progress, _) => { progress.Report(new(.85, "stale")); return Task.FromResult(new OperationResult(false, "failed")); });
        }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
        await first;
        var done = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var retry = queue.RunAsync(task, (_, _) => done.Task);
        held.Drain();
        Check(task.Progress == .03 && task.Stage != "stale", "old attempt cannot mutate the retried task");
        Check(queue.HasPendingOperations, "executing retry owns a pending-operation guard");
        done.SetResult(new(false, "fixture finished"));
        await retry;
        Check(!queue.HasPendingOperations, "finished retry releases its operation guard");
        queue.Pause();
        var paused = queue.RunAsync(task, (_, _) => throw new Exception("paused work must not run"));
        Check(queue.HasPendingOperations, "paused retry still blocks storage changes");
        queue.Cancel(task.Id);
        await paused;
        Check(!queue.HasPendingOperations, "canceling paused retry releases its operation guard");

        var config = JsonSerializer.Serialize(new
        {
            aurora_instance = "this-launch",
            components = new object[] { new { id = 1, type = "textbox" }, new { id = 2, type = "button" }, new { id = 3, type = "audio" } },
            dependencies = new[] { new { backend_fn = true, inputs = new[] { 1 }, outputs = new[] { 3 }, targets = new object[] { new object[] { 2, "click" } } } }
        });
        Check(WorkbenchReadiness.IsReady(config, "this-launch"), "valid instance with input action and audio output is accepted");
        Check(!WorkbenchReadiness.IsReady(config, "other-launch"), "another engine instance is rejected");
        Check(!WorkbenchReadiness.IsReady(config.Replace("\"backend_fn\":true", "\"backend_fn\":false"), "this-launch"), "unwired audio controls do not count as a workbench");
        Check(!WorkbenchReadiness.IsReady("<html>empty</html>", "this-launch"), "ordinary HTML is not a workbench config");
        var backend = new BackendService(settings);
        foreach (var (body, expected) in new[] { (config, true), ("<html>empty</html>", false), (config.Replace("this-launch", "old-launch"), false) })
            Check(await ProbeReadinessAsync(backend, body) == expected, "HTTP readiness matches instance and controls: " + expected);

        using (var self = Process.GetCurrentProcess())
        {
            var processes = Field<ConcurrentDictionary<string, Process>>(backend, "processes");
            var models = Field<Dictionary<string, string>>(backend, "activeModels");
            processes["voice"] = self; models["voice"] = "qwen3-tts-base";
            try
            {
                Check(backend.CanStartWorkbench("voice", "qwen3-tts-base"), "same engine may reconnect without being stopped");
                Check(!backend.CanStartWorkbench("singing", "seed-vc") && !backend.CanStartWorkbench("voice", "f5-tts"), "cross-feature and same-feature replacement require explicit release");
                var blocked = await backend.StartWorkbenchAsync("singing", "seed-vc", "zh-CN");
                Check(!blocked.Success && processes["voice"] == self && !self.HasExited, "blocked switch leaves the existing process untouched");
                backend.CancelWorkbenchStartup();
                Check(processes["voice"] == self && !self.HasExited, "canceling navigation does not stop an already running workbench");
            }
            finally { processes.TryRemove("voice", out _); models.Remove("voice"); }
        }
        Check(!(await backend.RunUtilityAsync("separation", "fixture", "demucs", "auto", trackMode: "two-stem")).Success, "backend refuses a saved stem mode inconsistent with the model");
        var missing = Path.Combine(root, "missing.wav");
        string? message = null;
        backend.StatusChanged += (_, value) => message = value;
        backend.OpenFolder(missing);
        Check(!Directory.Exists(missing) && message is not null, "opening a missing location never creates a directory or launches Explorer");
        var secret = "SYNTHETIC_CREDENTIAL_VALUE";
        var input = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            + "\nAuthorization: Bearer " + secret + "\nhf_token=" + secret + "\n{\"api_key\":\"" + secret + "\"}\nhttps://fixture.invalid/?access_token=" + secret + "&page=2";
        var redacted = (string)typeof(BackendService).GetMethod("RedactDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(backend, [input])!;
        Check(!redacted.Contains(secret) && redacted.Contains("page=2"), "diagnostics redact headers JSON env and query credentials without losing unrelated fields");

        var safe = new SettingsService(Path.Combine(root, "safe-mode"));
        Check(safe.TrySetSafeMode(true, out _) && new SettingsService(safe.AppDataRoot).Current.SafeMode, "safe mode persists atomically");
        Directory.CreateDirectory(safe.SettingsPath + ".tmp");
        Check(!safe.TrySetSafeMode(false, out var error) && safe.Current.SafeMode && error.Length > 0, "failed setting write preserves the previous safe mode without throwing");

        var repo = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "work/audio-studio/AuroraAudioStudio/MainPage.xaml.cs"))) repo = repo.Parent;
        if (repo is null) throw new DirectoryNotFoundException("Run functional regressions from the repository.");
        var main = File.ReadAllText(Path.Combine(repo.FullName, "work/audio-studio/AuroraAudioStudio/MainPage.xaml.cs"));
        var xaml = System.Xml.Linq.XDocument.Load(Path.Combine(repo.FullName, "work/audio-studio/AuroraAudioStudio/MainPage.xaml"));
        var save = main[main.IndexOf("private void SaveSettingsButton_Click")..main.IndexOf("private void LanguagePicker_SelectionChanged")];
        Check(new[] { "updateFlow.IsRunning", "modelBatchUpdating", "taskQueue.HasPendingOperations", "x.CanCancel", "workbenchStartupCancellation" }.All(save.Contains), "UI contract: storage changes include maintenance startup and queued tasks");
        var cancel = main[main.IndexOf("private async Task CancelSilentModelCheckAsync")..main.IndexOf("private async Task<bool> TryBeginMaintenanceAsync")];
        Check(cancel.IndexOf("?.Cancel()") < cancel.IndexOf("await check") && cancel.Contains("await check"), "UI contract: manual maintenance waits for silent cancellation");
        var silent = main[main.IndexOf("private async Task CheckModelsSilentlyAsync")..main.IndexOf("private void ShowUtility")];
        Check(silent.Contains("cancellation.Token.ThrowIfCancellationRequested()") && !silent.Contains("CancellationToken.None") && silent.Contains("updateFlow.End()"), "UI contract: canceled silent check cannot write results and releases its lock");
        var disabled = xaml.Descendants().Where(e => e.Attributes().Any(a => a.Name.LocalName == "Name" && a.Value is "AutoReleaseToggle" or "AutoReleaseSettingsToggle")).ToArray();
        Check(disabled.Length == 2 && disabled.All(e => e.Attribute("IsEnabled")?.Value == "False"), "UI contract: unsupported automatic release is not actionable");
        Check(main.Contains("syncingTrackMode") && main.Contains("token, old.TrackMode") && main.Contains("token, entry.Task.TrackMode"), "UI contract: selected and retried stem modes reach execution");
        Check(main.Contains("pid != previousPid"), "UI contract: failed reconnection only cleans up newly launched engines");
        File.WriteAllText(Path.Combine(root, "ready-script.js"), WorkbenchReadiness.DomScript("this-launch"));
        Console.WriteLine($"Functional regression checks passed: {count}. Fixtures: {root}");
    }

    private static T Field<T>(BackendService backend, string name) => (T)typeof(BackendService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(backend)!;

    private static async Task<bool> ProbeReadinessAsync(BackendService backend, string body)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        async Task Serve()
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true);
            var request = await reader.ReadLineAsync(deadline.Token);
            if (request?.StartsWith("GET /config ") != true) throw new Exception("Expected /config readiness request");
            while (await reader.ReadLineAsync(deadline.Token) is { Length: > 0 }) { }
            var response = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
            await stream.WriteAsync(response, deadline.Token);
        }
        var server = Serve();
        using var self = Process.GetCurrentProcess();
        var processes = Field<ConcurrentDictionary<string, Process>>(backend, "processes");
        var instances = Field<Dictionary<string, string>>(backend, "workbenchInstances");
        processes["voice"] = self; instances["voice"] = "this-launch";
        try
        {
            var url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            var operation = (Task<OperationResult>)typeof(BackendService).GetMethod("WaitForUrlAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(backend, ["voice", url, "fixture"] )!;
            var result = await operation.WaitAsync(deadline.Token);
            await server;
            return result.Success;
        }
        finally { processes.TryRemove("voice", out _); instances.Remove("voice"); }
    }

    private sealed class HeldContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> pending = new();
        public override void Post(SendOrPostCallback callback, object? state) => pending.Enqueue((callback, state));
        public void Drain() { while (pending.TryDequeue(out var item)) item.Callback(item.State); }
    }
}
