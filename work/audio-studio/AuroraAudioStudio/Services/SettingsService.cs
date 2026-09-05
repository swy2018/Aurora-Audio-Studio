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

    public static string DefaultDataRoot => Environment.GetEnvironmentVariable("AURORA_DATA_ROOT") is { Length: > 0 } isolated && Path.IsPathFullyQualified(isolated)
        ? Path.GetFullPath(isolated) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aurora Audio Studio");

    public string? StorageWarning { get; private set; }
    public SettingsService(string? appDataRoot = null)
    {
        appDataRoot ??= Environment.GetEnvironmentVariable("AURORA_DATA_ROOT");
        if (appDataRoot is not null && !Path.IsPathFullyQualified(appDataRoot)) throw new ArgumentException("AURORA_DATA_ROOT must be an absolute path.");
        if (appDataRoot is not null)
        {
            AppDataRoot = Path.GetFullPath(appDataRoot);
            Current = new AppSettings { LocalAiRoot = Path.Combine(AppDataRoot, "Models"), OutputRoot = Path.Combine(AppDataRoot, "Output"), ProjectsRoot = Path.Combine(AppDataRoot, "Projects") };
        }
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), options) ?? new AppSettings();
        }
        catch
        {
            StorageWarning = "设置文件无法读取，请检查设置。";
        }
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(UpdatesRoot);
        foreach (var path in new[] { Current.OutputRoot, Current.ProjectsRoot })
        {
            try { Directory.CreateDirectory(path); }
            catch (Exception ex) { StorageWarning = "保存目录暂时不可用，请到设置中修正：" + ex.Message; }
        }
    }

    public bool TrySave(AppSettings settings, out string error)
    {
        if (!SettingsPathValidator.TryValidate(settings.LocalAiRoot, settings.OutputRoot, settings.ProjectsRoot, out error)) return false;
        try
        {
            settings.LocalAiRoot = Path.GetFullPath(settings.LocalAiRoot.Trim());
            settings.OutputRoot = Path.GetFullPath(settings.OutputRoot.Trim());
            settings.ProjectsRoot = Path.GetFullPath(settings.ProjectsRoot.Trim());
            Directory.CreateDirectory(AppDataRoot);
            Directory.CreateDirectory(settings.LocalAiRoot);
            Directory.CreateDirectory(settings.OutputRoot);
            Directory.CreateDirectory(settings.ProjectsRoot);
            return Persist(settings, out error);
        }
        catch (Exception ex)
        {
            error = "无法保存设置：" + ex.Message;
            return false;
        }
    }

    public void Save(AppSettings settings)
    {
        if (!TrySave(settings, out var error)) throw new IOException(error);
    }

    public bool TrySetLanguage(string language, out string error)
    {
        if (language is not ("auto" or "zh-CN" or "zh-TW" or "en-US" or "ja-JP")) { error = "不支持的界面语言。"; return false; }
        // Do not save unfinished path edits or require model disks online to switch language.
        var candidate = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(Current))!;
        candidate.Language = language;
        return Persist(candidate, out error);
    }

    private bool Persist(AppSettings candidate, out string error)
    {
        try
        {
            Directory.CreateDirectory(AppDataRoot);
            var temporary = SettingsPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(candidate, options));
            File.Move(temporary, SettingsPath, true);
            Current = candidate;
            StorageWarning = null;
            error = "";
            return true;
        }
        catch (Exception ex) { error = "无法保存设置：" + ex.Message; return false; }
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
