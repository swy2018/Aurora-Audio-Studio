using System.Diagnostics;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class EngineIntegration
{
    // Opt-in real-model acceptance. Never run in CI, download models, or use the user's app state.
    public static async Task RunAsync(string[] args)
    {
        var feature = args[0]; var model = args[1]; var evidence = Path.GetFullPath(args[2]);
        var source = args.Length > 3 ? Path.GetFullPath(args[3]) : "";
        var settings = new SettingsService(Path.Combine(evidence, model, "state"));
        settings.Current.LocalAiRoot = args.Length > 4 ? Path.GetFullPath(args[4]) : @"C:\LocalAI";
        var backend = new BackendService(settings);
        var catalog = new ModelCatalogService(settings);
        var projects = new ProjectService(settings, catalog);
        var queue = new TaskQueueService(settings);
        var receipts = new WorkbenchResultService(settings, projects, queue, catalog);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        backend.StatusChanged += (_, status) => Console.WriteLine(status);
        try
        {
            OperationResult result;
            if (feature is "music" or "voice" or "singing")
            {
                result = await backend.StartWorkbenchAsync(feature, model, "en-US", timeout.Token);
                Console.WriteLine(JsonSerializer.Serialize(result));
                if (!result.Success) throw new Exception(result.Message);
                var python = feature == "singing" ? @"C:\LocalAI\Seed-VC\.venv\Scripts\python.exe" : @"C:\LocalAI\Qwen3-TTS\Python312\python.exe";
                var info = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var value in new[] { "-u", Path.Combine(AppContext.BaseDirectory, "Tools", "exercise_workbench.py"), feature, model, result.Url!, Path.Combine(evidence, model), source }) info.ArgumentList.Add(value);
                info.Environment["PYTHONUTF8"] = "1";
                using var client = Process.Start(info) ?? throw new Exception("Could not launch acceptance client");
                var stdout = client.StandardOutput.ReadToEndAsync(); var stderr = client.StandardError.ReadToEndAsync();
                using var cancel = timeout.Token.Register(() => { try { if (!client.HasExited) client.Kill(true); } catch { } });
                await client.WaitForExitAsync(timeout.Token);
                Console.WriteLine(await stdout); Console.WriteLine(await stderr);
                await File.WriteAllTextAsync(Path.Combine(evidence, model, "client.log"), (await stdout) + (await stderr));
                if (client.ExitCode != 0) throw new Exception("Workbench generation failed");
                var imported = await receipts.ImportAsync();
                if (imported < 1) throw new Exception("Real generation did not reach Aurora's result library");
                var task = queue.Items.First(x => x.Status == AuroraTaskStates.Completed);
                result = new(true, "Generated and registered", task.OutputPath, Outputs: task.OutputFiles, Device: task.Device);
            }
            else
            {
                var project = await projects.CreateAsync(feature, source, model, timeout.Token);
                var task = queue.Create(project.Id, model + " acceptance", feature, source, model);
                await projects.AddTaskAsync(project, task);
                result = await queue.RunAsync(task, (progress, token) => backend.RunUtilityAsync(feature, source, model, "auto", progress, token));
                await projects.CompleteTaskAsync(project.Id, task);
                if (!result.Success) throw new Exception(result.Message);
                if (projects.Artifacts().Count == 0) throw new Exception("No registered artifacts");
            }
            foreach (var path in result.Outputs ?? []) ArtifactValidator.Validate(path);
            await File.WriteAllTextAsync(Path.Combine(evidence, model, "acceptance.json"), JsonSerializer.Serialize(new { at = DateTimeOffset.Now, feature, model, result, artifacts = projects.Artifacts() }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("ENGINE_ACCEPTANCE_PASS " + model);
        }
        finally { backend.StopAll(); }
    }
}
