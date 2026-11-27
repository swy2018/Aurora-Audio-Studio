using System.Text.Json;
using System.Text.RegularExpressions;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed partial class ModelUpdateService
{
    internal sealed record PackageSource(string Repository, string Commit, string Tag, string Date)
    {
        public string Version => Tag.Length > 0 ? Tag : Date + "+" + Commit[..8];
    }

    internal static string? PackageSourceRepository(string id) => id switch
    {
        "roformer" => "https://github.com/openmirlab/bs-roformer-infer.git",
        "yourmt3" => "https://github.com/openmirlab/mt3-infer.git",
        _ => null
    };

    internal async Task<PackageSource> ResolvePackageSourceAsync(string repository, CancellationToken token)
    {
        var release = await GetGitHubRepositoryReleaseAsync(repository, token)
            ?? throw new IOException("暂时无法读取 GitHub 上游版本");
        var commit = release.Commit;
        var date = release.DateVersion;
        if (release.Tag.Length > 0)
        {
            var url = $"https://api.github.com/repos/{release.Owner}/{release.Repository}/commits/{Uri.EscapeDataString(release.Tag)}";
            using var document = JsonDocument.Parse(await metadataClient.GetStringAsync(url, token));
            commit = document.RootElement.GetProperty("sha").GetString() ?? "";
            date = DateTimeOffset.Parse(document.RootElement.GetProperty("commit").GetProperty("committer").GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime.ToString("yyyy.MM.dd");
        }
        if (!Regex.IsMatch(commit, "^[0-9a-fA-F]{40}$")) throw new InvalidDataException("Invalid upstream source revision.");
        return new(repository, commit.ToLowerInvariant(), release.Tag, date);
    }

    internal async Task<OperationResult> CheckPackageSourceAsync(ModelDefinition model, string root, CancellationToken token)
    {
        var latest = await ResolvePackageSourceAsync(PackageSourceRepository(model.Id)!, token);
        var receipt = Path.Combine(RuntimeEnvironment.Resolve(root), ".aurora-source.json");
        PackageSource? installed = null;
        if (File.Exists(receipt))
        {
            try { installed = JsonSerializer.Deserialize<PackageSource>(await File.ReadAllTextAsync(receipt, token)); }
            catch (JsonException) { return new(false, "无法读取已安装版本", "unverified"); }
            if (installed is null || installed.Repository != latest.Repository || !Regex.IsMatch(installed.Commit ?? "", "^[0-9a-fA-F]{40}$"))
                return new(false, "无法读取已安装版本", "unverified");
        }
        var localVersion = "";
        var site = Path.Combine(RuntimeEnvironment.Resolve(root), "Lib", "site-packages");
        if (Directory.Exists(site))
        {
            var package = PyPiPackageName(model.Repository!).Replace('-', '_');
            var metadata = Directory.EnumerateDirectories(site, package + "-*.dist-info").Select(path => Path.Combine(path, "METADATA")).Where(File.Exists).ToArray();
            if (metadata.Length == 1)
            {
                localVersion = File.ReadLines(metadata[0]).FirstOrDefault(line => line.StartsWith("Version: ", StringComparison.Ordinal))?[9..].Trim() ?? "";
                var direct = Path.Combine(Path.GetDirectoryName(metadata[0])!, "direct_url.json");
                if (installed is null && File.Exists(direct))
                {
                    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(direct, token));
                    var info = document.RootElement;
                    if (!info.TryGetProperty("vcs_info", out var vcs) || vcs.GetProperty("vcs").GetString() != "git"
                        || info.GetProperty("url").GetString()?.TrimEnd('/').Replace(".git", "", StringComparison.Ordinal) != latest.Repository[..^4])
                        return new(false, "无法读取已安装版本", "unverified");
                    var sha = vcs.GetProperty("commit_id").GetString() ?? "";
                    if (!Regex.IsMatch(sha, "^[0-9a-fA-F]{40}$")) return new(false, "无法读取已安装版本", "unverified");
                    installed = new(latest.Repository, sha.ToLowerInvariant(), "", "");
                }
            }
        }
        var current = installed?.Commit == latest.Commit;
        var ahead = false;
        if (installed is not null && !current)
        {
            var repo = new Uri(latest.Repository).AbsolutePath.Trim('/')[..^4];
            using var comparison = JsonDocument.Parse(await metadataClient.GetStringAsync($"https://api.github.com/repos/{repo}/compare/{installed.Commit}...{latest.Commit}", token));
            var status = comparison.RootElement.GetProperty("status").GetString();
            if (status is not ("ahead" or "behind" or "identical")) return new(false, "无法读取已安装版本", "unverified");
            ahead = status == "behind";
            current = status is "behind" or "identical";
        }
        else if (installed is null)
        {
            if (!Version.TryParse(localVersion, out var local) || !Version.TryParse(latest.Tag.TrimStart('v', 'V'), out var target))
                return new(false, "无法读取已安装版本", "unverified");
            ahead = local > target;
            current = local >= target;
        }
        var text = new LocalizationService(settings);
        var version = latest.Tag.Length > 0 ? text.Format("modelFormalSource", latest.Tag, latest.Commit[..8])
            : text.Format("modelDatedSource", latest.Date, latest.Commit[..8]);
        var message = text.Format(ahead ? "modelSourceAhead" : current ? "modelSourceCurrent" : "modelSourceAvailable", version);
        if (latest.Tag.Length > 0)
        {
            // Formal releases remain the automatic-update target. Surface newer
            // unreleased maintenance code separately instead of silently installing it.
            var repo = new Uri(latest.Repository).AbsolutePath.Trim('/')[..^4];
            using var document = JsonDocument.Parse(await metadataClient.GetStringAsync($"https://api.github.com/repos/{repo}/commits?per_page=1", token));
            var head = document.RootElement[0];
            var commit = head.GetProperty("sha").GetString() ?? "";
            if (!Regex.IsMatch(commit, "^[0-9a-fA-F]{40}$")) throw new InvalidDataException("Invalid upstream source revision.");
            if (commit != latest.Commit)
                message += " " + text.Format("modelUnreleasedSource", commit[..8]);
        }
        return new(true, message + " " + text.Get("modelWeightsSeparate"), current ? "current" : "available");
    }
}
