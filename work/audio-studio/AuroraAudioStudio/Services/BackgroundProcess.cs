using System.Diagnostics;

namespace AuroraAudioStudio.Services;

internal static class BackgroundProcess
{
    public static void Start(Process process)
    {
        // Desktop launchers can leave stdin invalid. Nested Python/SoX processes still
        // duplicate it, so provide a valid pipe and EOF instead of inheriting that handle.
        // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardinput
        process.StartInfo.RedirectStandardInput = true;
        if (!process.Start()) throw new InvalidOperationException("Process could not start.");
        process.StandardInput.Close();
    }
}
