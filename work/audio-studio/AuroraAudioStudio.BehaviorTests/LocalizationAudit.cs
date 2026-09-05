using System.Xml.Linq;
using System.Text.RegularExpressions;
using AuroraAudioStudio.Services;

internal static class LocalizationAudit
{
    public static void Run(string repository)
    {
        var settings = new SettingsService(Path.Combine(Path.GetTempPath(), "Aurora-Strings-" + Guid.NewGuid().ToString("N")));
        settings.Current.Language = "en-US";
        var localization = new LocalizationService(settings);
        var document = XDocument.Load(Path.Combine(repository, "work/audio-studio/AuroraAudioStudio/MainPage.xaml"));
        var keys = document.Descendants().Attributes().Where(a => a.Name.LocalName.StartsWith("LocalizedText.")).Select(a => a.Value).Distinct().ToArray();
        var autonyms = new[] { "简体中文", "繁體中文", "日本語" };
        var missing = keys.Where(key => Regex.IsMatch(key, "[\\p{IsCJKUnifiedIdeographs}]") && localization.Get(key) == key && localization.Translate(key) == key && !key.Contains("苏晚颜") && !autonyms.Contains(key)).ToArray();
        foreach (var key in missing) Console.WriteLine("MISSING " + key);
        Console.WriteLine($"Authored keys: {keys.Length}, missing English translations: {missing.Length}");
        if (missing.Length > 0) Environment.ExitCode = 1;
        using var workbench = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "work/audio-studio/AuroraAudioStudio/Tools/workbench-i18n.json")));
        var entries = workbench.RootElement.EnumerateObject().ToArray();
        if (entries.Any(entry => entry.Value.GetArrayLength() != 4 || entry.Value.EnumerateArray().Any(value => string.IsNullOrWhiteSpace(value.GetString())))) throw new Exception("Every workbench phrase requires all four translations.");
        Console.WriteLine($"Workbench translation entries: {entries.Length}, all four languages present.");
    }
}
