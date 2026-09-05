using System.Text.Json;
using AuroraAudioStudio.Services;

internal static class CatalogExport
{
    public static void Run(string repository, bool check)
    {
        var settings = new SettingsService(Path.Combine(Path.GetTempPath(), "Aurora-Catalog-" + Guid.NewGuid().ToString("N")));
        settings.Current.Language = "en-US";
        var catalog = new ModelCatalogService(settings);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "docs", "release.json")));
        var version = document.RootElement.GetProperty("version").GetString();
        var states = catalog.GetStates().ToDictionary(x => x.Id);
        var capabilities = catalog.Definitions.Select(model => new
        {
            id = model.Id, name = model.Name, nameEn = catalog.DisplayName(model), feature = model.Feature, recommended = model.IsDefault,
            mode = model.Id == "subtitle-edit" ? "external-editor" : model.Id == "faster-whisper" ? "shared-runtime" : !model.IsRunnable ? "download-only" : model.Feature is "music" or "voice" or "singing" ? "embedded-workbench" : "native-task",
            source = model.Repository, license = states[model.Id].License,
            download = ModelInstallPlanner.EstimatedDownload(model.Id)
        }).ToArray();
        var text = JsonSerializer.Serialize(new { version, models = capabilities }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).Replace("\r\n", "\n") + "\n";
        var destination = Path.Combine(repository, "docs", "capabilities.json");
        if (check)
        {
            if (!File.Exists(destination) || File.ReadAllText(destination).Replace("\r\n", "\n") != text) throw new Exception("Public model capabilities differ from the client catalog.");
        }
        else File.WriteAllText(destination, text);
        Console.WriteLine("CATALOG_SYNC_PASS " + capabilities.Length);
    }
}
