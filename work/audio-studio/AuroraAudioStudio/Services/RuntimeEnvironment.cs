namespace AuroraAudioStudio.Services;

// Versioned environments remain at their creation path: Windows entry points embed Python paths.
public static class RuntimeEnvironment
{
    public static string PythonPath(string root) => File.Exists(Path.Combine(root, "python.exe")) ? Path.Combine(root, "python.exe") : Path.Combine(root, "Scripts", "python.exe");
    private const string Marker = ".aurora-runtime";
    public static string Resolve(string logicalRoot)
    {
        var marker = Path.Combine(logicalRoot, Marker);
        if (!File.Exists(marker)) return logicalRoot;
        var path = File.ReadAllText(marker).Trim();
        return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : Path.Combine(logicalRoot, ".invalid-runtime");
    }

    public static string CreateCandidate(string localAiRoot)
    {
        // The logical model pointer retains identity. Keep native DLL paths short: CUDA's
        // dependent-library loader can reject paths before Python's long-path handling helps.
        var root = Path.GetFullPath(Path.Combine(localAiRoot, ".aurora", "envs", Guid.NewGuid().ToString("N")));
        if (OperatingSystem.IsWindows() && root.Length > 170)
            throw new PathTooLongException("模型目录过长，Windows 无法可靠加载运行组件。请在设置中选择较短的模型目录，例如 C:\\LocalAI；现有文件未被移动。");
        Directory.CreateDirectory(Path.GetDirectoryName(root)!);
        return root;
    }

    public static void Activate(string logicalRoot, string candidate)
    {
        if (!File.Exists(PythonPath(candidate))) throw new InvalidDataException("候选运行环境缺少 Python。");
        Directory.CreateDirectory(logicalRoot);
        var previous = Resolve(logicalRoot);
        if (File.Exists(PythonPath(previous))) File.WriteAllText(Path.Combine(logicalRoot, ".aurora-previous-runtime"), previous);
        var pending = Path.Combine(logicalRoot, Marker + ".tmp");
        File.WriteAllText(pending, Path.GetFullPath(candidate));
        File.Move(pending, Path.Combine(logicalRoot, Marker), true);
    }

    public static bool CanRollback(string logicalRoot) => File.Exists(Path.Combine(logicalRoot, ".aurora-previous-runtime")) || Directory.Exists(ModelInstallTransaction.PreviousPath(logicalRoot));
    public static bool Rollback(string logicalRoot)
    {
        var previous = Path.Combine(logicalRoot, ".aurora-previous-runtime");
        if (!File.Exists(previous)) return false;
        var candidate = File.ReadAllText(previous).Trim();
        Activate(logicalRoot, candidate);
        return true;
    }
}
