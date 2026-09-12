using System.Text.Json;
using System.Text.RegularExpressions;

namespace AuroraAudioStudio.Services;

public static class WindowsReleasePolicy
{
    public const string ReleasesApi = "https://api.github.com/repos/swy2018/Aurora-Audio-Studio/releases?per_page=100";

    // GitHub's releases list includes assets for every platform; select the exact Windows asset.
    // https://docs.github.com/en/rest/releases/releases#list-releases
    public static Models.GitHubRelease? Select(string json, string channel = "stable", string? rollbackFrom = null)
    {
        using var document = JsonDocument.Parse(json);
        var entries = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToArray() : [document.RootElement];
        return entries.Where(r => !r.GetProperty("draft").GetBoolean()
                && AppReleaseVersion.Allowed(r.GetProperty("tag_name").GetString() ?? "", r.GetProperty("prerelease").GetBoolean(), channel)
                && (rollbackFrom is null || AppReleaseVersion.IsPreviousStable(r.GetProperty("tag_name").GetString() ?? "", r.GetProperty("prerelease").GetBoolean(), rollbackFrom)))
            .Select(r => JsonSerializer.Deserialize<Models.GitHubRelease>(r.GetRawText())!)
            .Where(r => r.Assets.Any(a => a.Name == InstallerName(r.TagName)))
            .OrderByDescending(r => AppReleaseVersion.Parse(r.TagName)).FirstOrDefault();
    }

    public static string InstallerName(string tag) => $"Aurora-Audio-Studio-{tag.TrimStart('v', 'V')}-Setup-x64.exe";

    public static string ReadChecksum(string text, string filename, bool singleFile)
    {
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^([0-9a-fA-F]{64})(?:\s+\*?(.+))?$", RegexOptions.CultureInvariant);
            if (match.Success && (match.Groups[2].Value == filename || singleFile && !match.Groups[2].Success))
                return match.Groups[1].Value.ToLowerInvariant();
        }
        throw new InvalidDataException("No checksum matches the Windows installer filename.");
    }
}
