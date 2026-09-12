using System.Text.Json;

namespace AuroraAudioStudio.Services;

public sealed class WorkbenchActivityService(SettingsService settings, TaskQueueService queue, ModelCatalogService catalog,
    Func<string, string?>? currentInstance = null)
{
    public int Import()
    {
        var root = Path.Combine(settings.AppDataRoot, "WorkbenchTasks");
        if (!Directory.Exists(root)) return 0;
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.json"))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var data = document.RootElement;
                var id = data.GetProperty("id").GetString()!;
                var feature = data.GetProperty("feature").GetString()!;
                var model = data.GetProperty("modelId").GetString()!;
                var instance = data.GetProperty("instance").GetString()!;
                var status = data.GetProperty("status").GetString()!;
                if (!Guid.TryParseExact(id, "N", out _) || !Guid.TryParseExact(instance, "N", out _)
                    || feature is not ("music" or "voice" or "singing") || catalog.Find(model)?.Feature != feature
                    || status is not ("running" or "saving" or "failed")) continue;
                if (currentInstance is not null && currentInstance(feature) != instance)
                {
                    if (!queue.Items.Any(x => x.Id == id)) continue;
                    status = "failed";
                }
                if (queue.ObserveWorkbench(id, feature, model, data.GetProperty("pid").GetInt32(), instance,
                    data.GetProperty("sequence").GetInt64(), status, data.GetProperty("message").GetString() ?? "")) count++;
            }
            catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
        }
        return count;
    }
}
