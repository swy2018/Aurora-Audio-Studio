using Avalonia;

namespace AuroraAudioStudio.Mac;

internal static class Program
{
    internal static AuroraAudioStudio.Core.StudioInstance? Instance { get; private set; }
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            using var instance = new AuroraAudioStudio.Core.StudioInstance(AuroraAudioStudio.Services.SettingsService.DefaultDataRoot);
            Instance = instance;
            if (!instance.IsPrimary) { instance.NotifyPrimaryAsync().GetAwaiter().GetResult(); return; }
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            var root = AuroraAudioStudio.Services.SettingsService.DefaultDataRoot;
            Directory.CreateDirectory(root);
            File.AppendAllText(Path.Combine(root, "startup-error.log"), ex + Environment.NewLine);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
