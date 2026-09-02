using System.Text.Json;

namespace AuroraAudioStudio.Services;

public sealed record WindowState(int Width = 1560, int Height = 960, bool IsMaximized = true);

public sealed class WindowStateService
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aurora Audio Studio", "window-state.json");

    public WindowState Load()
    {
        try
        {
            if (!File.Exists(path)) return new();
            var value = JsonSerializer.Deserialize<WindowState>(File.ReadAllText(path)) ?? new();
            return value with { Width = Math.Clamp(value.Width, 960, 7680), Height = Math.Clamp(value.Height, 640, 4320) };
        }
        catch { return new(); }
    }

    public void Save(WindowState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state));
            File.Move(temp, path, true);
        }
        catch { }
    }
}
