using System.Diagnostics;

namespace AuroraAudioStudio.Services;

internal static class BackgroundProcess
{
    public static void Start(Process process)
    {
        BundledTools.Configure(process.StartInfo);
        // Desktop launchers can leave stdin invalid. Nested Python/SoX processes still
        // duplicate it, so provide a valid pipe and EOF instead of inheriting that handle.
        // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardinput
        process.StartInfo.RedirectStandardInput = true;
        if (!process.Start()) throw new InvalidOperationException("Process could not start.");
        process.StandardInput.Close();
    }

    public static async Task WaitForExitAsync(Process process, CancellationToken token = default)
    {
        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            // Canceling WaitForExitAsync only cancels the wait; Kill is asynchronous.
            // Always stop/join in this scope, not solely in a disposable token callback.
            // https://learn.microsoft.com/dotnet/api/system.diagnostics.process.kill
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
    }
}
