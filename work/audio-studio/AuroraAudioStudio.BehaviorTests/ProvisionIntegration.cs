using System.Reflection;
using System.Text.Json;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

internal static class ProvisionIntegration
{
    public static async Task RunAsync(string modelId, string localAiRoot, string evidence)
    {
        var settings = new SettingsService(Path.Combine(Path.GetFullPath(evidence), "provision-state"));
        settings.Current.LocalAiRoot = Path.GetFullPath(localAiRoot);
        var catalog = new ModelCatalogService(settings);
        var model = catalog.Find(modelId) ?? throw new Exception("Unknown model");
        var updater = new ModelUpdateService(catalog, settings);
        var target = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        if (Directory.Exists(target)) throw new Exception("First-install acceptance requires a new target");
        var stage = ModelInstallTransaction.StagingPath(target);
        if (!Directory.Exists(Path.Combine(stage, ".git"))) throw new Exception("Prepare an isolated upstream checkout and cached model copies first");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        var progress = new Progress<ModelInstallProgress>(value => { if (!string.IsNullOrWhiteSpace(value.Stage)) Console.WriteLine(value.Stage); });
        var provision = typeof(ModelUpdateService).GetMethod("ProvisionGitRepositoryAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = await (Task<OperationResult>)provision.Invoke(updater, [model, stage, progress, timeout.Token])!;
        Console.WriteLine(JsonSerializer.Serialize(result));
        if (!result.Success) throw new Exception(result.Message);
        ModelInstallTransaction.Commit(target);
        var probe = typeof(ModelUpdateService).GetMethod("ProbeGitRuntimeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        result = await (Task<OperationResult>)probe.Invoke(updater, [model, target, timeout.Token])!;
        if (!result.Success) throw new Exception(result.Message);
        await File.WriteAllTextAsync(Path.Combine(evidence, modelId + "-provision.json"), JsonSerializer.Serialize(new { at = DateTimeOffset.Now, result, target, runtime = RuntimeEnvironment.Resolve(Path.Combine(target, ".venv")) }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PROVISION_ACCEPTANCE_PASS " + modelId);
    }
}
