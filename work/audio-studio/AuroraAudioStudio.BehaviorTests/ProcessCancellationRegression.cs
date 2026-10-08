using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class ProcessCancellationRegression
{
    private static ProcessStartInfo Self(params string[] args)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    public static async Task FixtureAsync(string marker)
    {
        using var child = Process.Start(Self("--cancel-leaf"))!;
        await File.WriteAllTextAsync(marker, JsonSerializer.Serialize(new[] { Environment.ProcessId, child.Id }));
        try { await child.WaitForExitAsync(); }
        finally { if (!child.HasExited) child.Kill(true); }
    }

    public static async Task RunAsync(string? uv = null, string? python = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-cancel-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var settings = new SettingsService(root);
        var updater = new ModelUpdateService(new ModelCatalogService(settings), settings);
        foreach (var runner in new[] { typeof(ModelUpdateService), typeof(BackendService) })
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var marker = Path.Combine(root, runner.Name + attempt + ".json");
            var info = Self("--cancel-tree", marker);
            if (uv is not null)
            {
                info = new ProcessStartInfo(uv) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var arg in new[] { "run", "--no-project", "--no-config", "--offline", "--python", python!,
                    "python", "-c", "import os,time,json,sys; from pathlib import Path; Path(sys.argv[1]).write_text(json.dumps([os.getppid(),os.getpid()])); time.sleep(120)", marker })
                    info.ArgumentList.Add(arg);
            }
            info.RedirectStandardOutput = info.RedirectStandardError = true;
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var method = runner.GetMethod("RunProcessAsync", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task<(int, string, string)>)method.Invoke(runner == typeof(ModelUpdateService) ? updater : null,
                runner == typeof(ModelUpdateService) ? [info, cancel.Token, null] : [info, cancel.Token])!;
            var owned = new List<Process>();
            try
            {
                int[]? ids = null;
                var limit = Stopwatch.StartNew();
                while (ids is null && limit.Elapsed < TimeSpan.FromSeconds(10))
                {
                    if (task.IsCompleted) await task;
                    try { if (File.Exists(marker)) ids = JsonSerializer.Deserialize<int[]>(await File.ReadAllTextAsync(marker)); }
                    catch (JsonException) { }
                    catch (IOException) { }
                    if (ids is null) await Task.Delay(20);
                }
                if (ids is null) throw new Exception("Cancellation fixture did not start");
                foreach (var id in ids)
                {
                    var process = Process.GetProcessById(id);
                    _ = process.Handle; // Retain the exact process identity, not a reusable PID.
                    owned.Add(process);
                }
                cancel.CancelAfter(1);
                var canceled = false;
                try { await task.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (OperationCanceledException) { canceled = true; }
                if (!canceled || owned.Any(process => !process.HasExited))
                    throw new Exception(runner.Name + " returned from cancellation while its process tree was still alive");
            }
            finally
            {
                foreach (var process in owned)
                {
                    if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                    process.Dispose();
                }
            }
        }
        Console.WriteLine("PASS 10 cancellation cases join both owned processes before returning (" + (uv is null ? "fixture" : "real uv") + ")");
        Console.WriteLine("Fixtures retained: " + root);
    }
}
