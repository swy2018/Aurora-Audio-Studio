namespace AuroraAudioStudio.Services;

public static class ModelInstallTransaction
{
    public static string StagingPath(string target) => target + ".aurora-staging";
    public static string PreviousPath(string target) => target + ".aurora-previous";

    public static void Prepare(string target, string? identity = null)
    {
        var staging = StagingPath(target);
        var marker = Path.Combine(staging, ".aurora-download-identity");
        if (identity is not null && File.Exists(marker) && File.ReadAllText(marker) == identity) return;
        if (Directory.Exists(staging)) Directory.Move(staging, staging + ".abandoned-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        if (identity is not null) File.WriteAllText(marker, identity);
    }

    public static void Commit(string target)
    {
        var staging = StagingPath(target);
        var previous = PreviousPath(target);
        if (!Directory.Exists(staging)) throw new DirectoryNotFoundException($"Model staging directory was not found: {staging}");

        if (Directory.Exists(previous)) Directory.Move(previous, previous + ".retained-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(target)) Directory.Move(target, previous);
        try
        {
            Directory.Move(staging, target);
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(previous)) Directory.Move(previous, target);
            throw;
        }
    }

    public static bool RestorePrevious(string target)
    {
        var previous = PreviousPath(target);
        if (!Directory.Exists(previous)) return false;

        var failed = target + ".aurora-failed";
        if (Directory.Exists(failed)) Directory.Move(failed, failed + ".retained-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(target)) Directory.Move(target, failed);
        try
        {
            Directory.Move(previous, target);
            if (Directory.Exists(failed)) Directory.Move(failed, previous);
            return true;
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(failed)) Directory.Move(failed, target);
            throw;
        }
    }
}
