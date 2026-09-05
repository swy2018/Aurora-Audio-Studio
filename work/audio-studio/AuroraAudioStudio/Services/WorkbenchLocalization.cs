using System.Text.Json;

namespace AuroraAudioStudio.Services;

public static class WorkbenchLocalization
{
    public static string Script(LocalizationService localization, string language)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Tools");
        var entries = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(Path.Combine(root, "workbench-i18n.json")))!;
        foreach (var translations in localization.Translations) entries.TryAdd(translations[0], translations);
        var index = language switch { "zh-TW" => 1, "en-US" => 2, "ja-JP" => 3, _ => 0 };
        return File.ReadAllText(Path.Combine(root, "workbench-ui.js")).Replace("__AURORA_TRANSLATIONS__", JsonSerializer.Serialize(entries)).Replace("__AURORA_LANGUAGE_INDEX__", index.ToString()).Replace("__AURORA_STYLE__", JsonSerializer.Serialize(File.ReadAllText(Path.Combine(root, "workbench-ui.css"))));
    }
}
