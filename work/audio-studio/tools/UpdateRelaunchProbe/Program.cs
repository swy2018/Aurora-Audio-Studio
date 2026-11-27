using System.Diagnostics;
using System.ComponentModel;
using System.Security.Principal;
using System.Text.Json;
using AuroraAudioStudio.Services;

// This isolated probe never creates Aurora settings, model paths or a mutex.
// Its child is launched by the interactive desktop, not the administrator's environment.
using var identity = WindowsIdentity.GetCurrent();
// Only the test parent uses this mode. Return a source line + error code to
// its ordinary-user caller; never write diagnostic files as administrator.
if (args.SequenceEqual(new[] { "--diagnose" }))
{
    if (!UpdateRelaunch.IsElevated(identity.AccessToken)) return 2;
    try { UpdateRelaunch.StartAsDesktopUser(Environment.ProcessPath!); return 0; }
    catch (Exception ex)
    {
        var frame = new StackTrace(ex, true).GetFrames()?.FirstOrDefault(f => f.GetMethod()?.DeclaringType == typeof(UpdateRelaunch));
        return ((frame?.GetFileLineNumber() ?? 0) << 16) | ((ex is Win32Exception native ? native.NativeErrorCode : ex.HResult) & 0xffff);
    }
}
UpdateRelaunch.HandleBeforeStartup();
var result = new
{
    at = DateTimeOffset.UtcNow,
    sid = identity.User?.Value,
    elevated = UpdateRelaunch.IsElevated(identity.AccessToken),
    session = Process.GetCurrentProcess().SessionId,
    profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    executable = Environment.ProcessPath
};
var name = args.Contains("--baseline") ? "baseline.json" : "after-update.json";
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, name), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
return 0;
