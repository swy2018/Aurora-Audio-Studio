using System.Text.RegularExpressions;

namespace AuroraAudioStudio.Services;

public static class AppReleaseVersion
{
    // Supported release contract: X.Y.Z, X.Y.Z-beta.N (1..98), and historical Mac builds.
    // A final release sorts after every beta of the same version.
    public static Version? Parse(string value)
    {
        var match = Regex.Match(value, @"^[vV]?(\d+\.\d+\.\d+)(?:-(beta|mac)\.(\d+))?$", RegexOptions.CultureInvariant);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var core)) return null;
        var rank = 99;
        if (match.Groups[2].Success)
        {
            if (!int.TryParse(match.Groups[3].Value, out var number) || number < 1 || number > 98) return null;
            rank = match.Groups[2].Value == "beta" ? number : 100 + number;
        }
        return new Version(core.Major, core.Minor, core.Build, rank);
    }

    public static bool Allowed(string tag, bool prerelease, string channel) => Parse(tag) is not null
        && (channel == "beta" || !prerelease && !tag.Contains("-beta.", StringComparison.Ordinal));

    public static bool IsPreviousStable(string tag, bool prerelease, string current) =>
        !prerelease && Regex.IsMatch(tag, @"^[vV]?\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)
        && Parse(tag) is { } target && Parse(current) is { } installed && target < installed;
}
