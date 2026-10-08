using System.Diagnostics;
using System.Text;

namespace AuroraAudioStudio.Services;

// Tools belong to the application, not to the user's PATH or package manager.
public static class BundledTools
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "Runtime");

    public static string? Find(string name, string? root = null)
    {
        root ??= Root;
        var relative = OperatingSystem.IsWindows() ? name switch
        {
            "uv" => "bin/uv/uv.exe",
            "git" => "bin/git/cmd/git.exe",
            "ffmpeg" => "bin/ffmpeg/bin/ffmpeg.exe",
            "ffprobe" => "bin/ffmpeg/bin/ffprobe.exe",
            "sox" => "bin/sox/sox.exe",
            _ => throw new ArgumentException("Unknown bundled tool.", nameof(name))
        } : "bin/" + name;
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) && new FileInfo(path).Length > 0 ? path : null;
    }

    public static void Configure(ProcessStartInfo info, string? root = null)
    {
        root ??= Root;
        var directories = new[] { "uv", "git", "ffmpeg", "sox" }.Select(name => Find(name, root))
            .Where(path => path is not null).Select(path => Path.GetDirectoryName(path)!).Distinct();
        info.Environment.TryGetValue("PATH", out var current);
        info.Environment["PATH"] = string.Join(Path.PathSeparator, directories.Concat((current ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(path => path.Trim('"')).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));
        if (OperatingSystem.IsWindows())
        {
            // A global OpenSSL preference makes MinGit fail to load its CA file under
            // Unicode install paths. Use Windows trust for Aurora's child processes only.
            // https://git-scm.com/docs/git-config#Documentation/git-config.txt-httpsslBackend
            SetGitConfig(info, "http.sslBackend", "schannel");
            SetGitConfig(info, "http.sslVerify", "true");
            // Git's own path limit is separate from the Windows system setting.
            // https://gitforwindows.org/git-cannot-create-a-file-or-directory-with-a-long-path.html
            SetGitConfig(info, "core.longpaths", "true");
            info.Environment.Remove("GIT_SSL_NO_VERIFY");
        }
        else if (Find("git", root) is not null)
        {
            // https://git-scm.com/docs/git#Documentation/git.txt-codeGITEXECPATHcode
            info.Environment["GIT_EXEC_PATH"] = Path.Combine(root, "bin", "git-core");
            info.Environment["GIT_TEMPLATE_DIR"] = Path.Combine(root, "share", "git-core", "templates");
            info.Environment["GIT_SSL_CAINFO"] = Path.Combine(root, "share", "certs", "ca-bundle.crt");
        }
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["PYTHONIOENCODING"] = "utf-8";
        info.Environment["PYTHONUNBUFFERED"] = "1";
        if (info.RedirectStandardOutput) info.StandardOutputEncoding = Encoding.UTF8;
        if (info.RedirectStandardError) info.StandardErrorEncoding = Encoding.UTF8;
    }

    private static void SetGitConfig(ProcessStartInfo info, string key, string value)
    {
        info.Environment.TryGetValue("GIT_CONFIG_COUNT", out var existing);
        var count = string.IsNullOrEmpty(existing) ? 0 : int.Parse(existing, System.Globalization.CultureInfo.InvariantCulture);
        for (var index = count - 1; index >= 0; index--)
            if (info.Environment.TryGetValue("GIT_CONFIG_KEY_" + index, out var configured) && string.Equals(configured, key, StringComparison.OrdinalIgnoreCase))
            {
                info.Environment["GIT_CONFIG_VALUE_" + index] = value;
                return;
            }
        info.Environment["GIT_CONFIG_KEY_" + count] = key;
        info.Environment["GIT_CONFIG_VALUE_" + count] = value;
        info.Environment["GIT_CONFIG_COUNT"] = (count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void ConfigureInstaller(ProcessStartInfo info, string modelRoot, string? root = null)
    {
        Configure(info, root);
        // uv supplies Python itself; never bind a new venv to a developer's Python.
        // https://docs.astral.sh/uv/reference/environment/
        info.Environment["UV_PYTHON_PREFERENCE"] = "only-managed";
        info.Environment["UV_PYTHON_DOWNLOADS"] = "automatic";
        info.Environment["UV_PYTHON_INSTALL_DIR"] = Path.Combine(modelRoot, ".aurora", "python");
        info.Environment["UV_CACHE_DIR"] = Path.Combine(modelRoot, ".aurora", "uv-cache");
        info.Environment["UV_HTTP_TIMEOUT"] = "60";
        info.Environment["UV_HTTP_RETRIES"] = "2";
        info.Environment.Remove("PYTHONHOME");
        info.Environment.Remove("PYTHONPATH");
        info.Environment.Remove("VIRTUAL_ENV");
    }
}
