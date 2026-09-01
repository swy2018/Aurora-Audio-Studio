using System.Diagnostics;

namespace AuroraAudioStudio.Services;

public static class RunningProcessGuard
{
    public static string? FindInRoot(string root)
    {
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var executable = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(executable)) continue;
                if (!Path.GetFullPath(executable).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) continue;
                return string.IsNullOrWhiteSpace(process.MainWindowTitle) ? process.ProcessName : process.MainWindowTitle;
            }
            catch
            {
                // Protected system processes cannot be inspected and are unrelated to a LocalAI component root.
            }
            finally
            {
                process.Dispose();
            }
        }
        return null;
    }
}