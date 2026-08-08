using System.Globalization;
using System.Threading;

namespace AuroraAudioStudio.Services;

public sealed class UpdateFlowGuard
{
    private int active;

    public bool TryBegin() => Interlocked.CompareExchange(ref active, 1, 0) == 0;

    public void End() => Volatile.Write(ref active, 0);

    public static bool ShouldRunDailyCheck(string? lastCheckDate, DateOnly today) =>
        !DateOnly.TryParseExact(lastCheckDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) || last != today;

    public static string DateKey(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string BuildInstallerArguments(int currentProcessId, string logPath) =>
        $"/SILENT /CLOSEAPPLICATIONS /NORESTART /KEEPUSERDATA /UPDATE /UPDATEPID={currentProcessId} /LOG=\"{logPath}\"";
}
