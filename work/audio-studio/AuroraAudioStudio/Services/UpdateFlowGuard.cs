using System.Globalization;
using System.Net;
using System.Threading;

namespace AuroraAudioStudio.Services;

public sealed class UpdateFlowGuard
{
    private int active;
    public bool IsRunning => Volatile.Read(ref active) != 0;

    public bool TryBegin() => Interlocked.CompareExchange(ref active, 1, 0) == 0;

    public void End() => Volatile.Write(ref active, 0);

    public static bool ShouldRunDailyCheck(string? lastCheckDate, DateOnly today) =>
        !DateOnly.TryParseExact(lastCheckDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var last) || last != today;

    public static string DateKey(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool SupportsSafeRestart(string targetVersion) =>
        AppReleaseVersion.Parse(targetVersion) is { } target && target >= AppReleaseVersion.Parse("2.1.0-beta.1")!;

    public static string BuildInstallerArguments(int currentProcessId, string logPath, bool automaticRestart = true) =>
        $"/SILENT /CLOSEAPPLICATIONS /NORESTART /KEEPUSERDATA{(automaticRestart ? $" /UPDATE /UPDATEPID={currentProcessId}" : "")} /LOG=\"{logPath}\"";
}

public static class DownloadResumeGuard
{
    public static bool CanPromotePartial(HttpStatusCode status, long existingLength, long? expectedLength) =>
        status == HttpStatusCode.RequestedRangeNotSatisfiable
        && existingLength > 0
        && (expectedLength is null || existingLength == expectedLength.Value);
}
