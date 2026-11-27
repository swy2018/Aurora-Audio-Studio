using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using AuroraAudioStudio.Services;

// This isolated probe never creates Aurora settings, model paths or a mutex.
// Its after-update child must work after CreateEnvironmentBlock drops test env vars.
UpdateRelaunch.HandleBeforeStartup();
using var identity = WindowsIdentity.GetCurrent();
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
