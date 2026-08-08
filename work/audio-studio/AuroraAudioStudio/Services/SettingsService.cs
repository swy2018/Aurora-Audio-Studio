using System.Globalization;
using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    public string AppDataRoot { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aurora Audio Studio");
    public string SettingsPath => Path.Combine(AppDataRoot, "settings.json");
    public string LogsRoot => Path.Combine(AppDataRoot, "Logs");
    public string UpdatesRoot => Path.Combine(AppDataRoot, "Updates");
    public AppSettings Current { get; private set; } = new();

    public SettingsService() => Load();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), options) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(UpdatesRoot);
        Directory.CreateDirectory(Current.OutputRoot);
        Directory.CreateDirectory(Current.ProjectsRoot);
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(Current.OutputRoot);
        Directory.CreateDirectory(Current.ProjectsRoot);
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(Current, options));
        File.Move(temp, SettingsPath, true);
    }

    public string EffectiveLanguage()
    {
        if (!Current.Language.Equals("auto", StringComparison.OrdinalIgnoreCase)) return Current.Language;
        var culture = CultureInfo.CurrentUICulture.Name;
        if (culture.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) || culture.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) || culture.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)) return "zh-TW";
        if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        if (culture.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        return "en-US";
    }
}
