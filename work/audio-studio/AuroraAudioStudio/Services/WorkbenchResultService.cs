using System.Text.Json;
using System.Text.Json.Serialization;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class WorkbenchResultService(SettingsService settings, ProjectService projects, TaskQueueService queue, ModelCatalogService catalog)
{
    private readonly HashSet<string> imported = [];
    private bool busy;
    private bool pending;
    public async Task<int> ImportAsync()
    {
        pending = true;
        if (busy) return 0;
        busy = true;
        try
        {
            var root = Path.Combine(settings.AppDataRoot, "WorkbenchReceipts");
            if (!Directory.Exists(root)) return 0;
            var count = 0;
            do
            {
            pending = false;
            foreach (var receiptPath in Directory.EnumerateFiles(root, "*.json"))
            {
                if (imported.Contains(receiptPath)) continue;
                try
                {
                var receipt = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath));
                if (receipt is null || !Guid.TryParseExact(receipt.Id, "N", out _) || receipt.Feature is not ("music" or "voice" or "singing")) continue;
                if (catalog.Find(receipt.ModelId) is not { } model || model.Feature != receipt.Feature) continue;
                var outputRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(settings.Current.OutputRoot)) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(receipt.Path).StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase)) continue;
                var projectPath = Path.Combine(settings.Current.ProjectsRoot, receipt.Id + ".arr");
                var existing = File.Exists(projectPath) ? projects.Find(receipt.Id) : null;
                if (existing?.TaskIds.Count > 0) { imported.Add(receiptPath); continue; }
                ArtifactValidator.Validate(receipt.Path);
                var project = existing ?? new AuroraProject { Id = receipt.Id, Name = catalog.DisplayName(model) + " · " + File.GetLastWriteTime(receipt.Path).ToString("yyyy-MM-dd HH:mm:ss"), Feature = receipt.Feature, ModelId = receipt.ModelId, FilePath = projectPath };
                project.ModelVersion = catalog.GetStates().FirstOrDefault(x => x.Id == receipt.ModelId)?.Version ?? "";
                if (!project.Artifacts.Any(x => x.Path == receipt.Path)) project.Artifacts.Add(new AuroraArtifact { Path = receipt.Path, Kind = receipt.Feature });
                project.Parameters["device"] = receipt.Device;
                await projects.SaveAsync(project);
                var task = queue.Items.FirstOrDefault(x => x.ProjectId == project.Id) ?? queue.Create(project.Id, project.Name, receipt.Feature, "", receipt.ModelId);
                task.Device = receipt.Device;
                queue.RegisterCompleted(task, [receipt.Path]);
                await projects.AddTaskAsync(project, task);
                catalog.RecordSuccessfulRun(receipt.ModelId, receipt.Device);
                imported.Add(receiptPath); count++;
                }
                catch (Exception ex) when (ex is IOException or JsonException or ArgumentException)
                {
                    Directory.CreateDirectory(settings.LogsRoot);
                    await File.AppendAllTextAsync(Path.Combine(settings.LogsRoot, "workbench-import.log"), $"{DateTimeOffset.Now:O} {receiptPath}: {ex.Message}{Environment.NewLine}");
                }
            }
            } while (pending);
            return count;
        }
        finally { busy = false; }
    }

    private sealed record Receipt(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("feature")] string Feature,
        [property: JsonPropertyName("modelId")] string ModelId,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("device")] string Device);
}
