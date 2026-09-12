using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Services;

internal static class AppRollbackRegression
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        async Task Reject(Func<Task> body, string name)
        {
            var rejected = false;
            try { await body(); } catch { rejected = true; }
            Check(rejected, name);
        }
        object Release(string version, bool pre = false, bool draft = false, bool win = true, bool mac = true) =>
            new { tag_name = "v" + version, prerelease = pre, draft, html_url = "https://github.com/swy2018/Aurora-Audio-Studio/releases",
                assets = (win ? new[] { WindowsReleasePolicy.InstallerName(version) } : [])
                    .Concat(mac ? new[] { $"Aurora-Audio-Studio-{version}-arm64.dmg" } : [])
                    .SelectMany(n => new[] { n, n + ".sha256" }).Select(n => new { name = n,
                        browser_download_url = $"https://github.com/swy2018/Aurora-Audio-Studio/releases/download/v{version}/{n}" }) };
        var json = JsonSerializer.Serialize(new[] {
            Release("3.0.0"), Release("2.1.0", draft: true), Release("2.0.0-beta.2", pre: true),
            Release("1.9.9"), Release("1.9.8", pre: true), Release("1.9.7-beta.1"),
            Release("1.9.6", mac: false), Release("1.9.5", win: false), Release("1.9.4") });
        foreach (var channel in new[] { "stable", "beta" })
        {
            Check(WindowsReleasePolicy.Select(json, channel, "2.0.0-beta.2")?.TagName == "v1.9.9", "Windows beta explicitly reverts to previous stable: " + channel);
            Check(WindowsReleasePolicy.Select(json, channel, "1.9.9")?.TagName == "v1.9.6", "Windows stable rollback skips drafts, prereleases and other platforms: " + channel);
            var mac = MacAppUpdater.SelectRelease(json, "2.0.0-beta.2", channel, rollback: true);
            Check(mac.UpdateAvailable && mac.IsRollback && mac.LatestVersion == "1.9.9", "Mac beta explicitly reverts to previous stable: " + channel);
            Check(MacAppUpdater.SelectRelease(json, "1.9.9", channel, rollback: true).LatestVersion == "1.9.5", "Mac stable rollback selects exact Mac package: " + channel);
        }
        Check(WindowsReleasePolicy.Select(json, rollbackFrom: "1.0.0") is null, "Windows does not invent an absent rollback release");
        Check(!MacAppUpdater.SelectRelease(json, "1.0.0", rollback: true).UpdateAvailable, "Mac reports absent rollback without offering a newer release");
        foreach (var invalid in new[] { "2.0.0", "2.0.0-beta.1", "1.9.0-mac.6", "../1.9.9" })
            Check(!AppReleaseVersion.IsPreviousStable(invalid, false, "2.0.0"), "rollback rejects same, beta, legacy or invalid target: " + invalid);
        var root = Path.Combine(Path.GetTempPath(), "Aurora-AppRollback-" + Guid.NewGuid().ToString("N"));
        var bytes = new byte[] { 21, 22, 23, 24 };
        var checksum = Convert.ToHexString(SHA256.HashData(bytes));
        using var client = new HttpClient(new Transport(request => new(HttpStatusCode.OK) {
            Content = request.RequestUri!.AbsolutePath.EndsWith(".sha256")
                ? new StringContent(checksum + "  Aurora-Audio-Studio-1.9.9-arm64.dmg") : new ByteArrayContent(bytes) }));
        var update = MacAppUpdater.SelectRelease(json, "2.0.0-beta.2", rollback: true);
        var updater = new MacAppUpdater(client, root, "2.0.0-beta.2");
        await Reject(() => updater.DownloadAsync(update with { IsRollback = false }, null, CancellationToken.None), "normal Mac update cannot silently downgrade");
        var downloaded = await updater.DownloadAsync(update, null, CancellationToken.None);
        Check(File.ReadAllBytes(downloaded).SequenceEqual(bytes), "explicit Mac rollback downloads and verifies selected stable package");
        await Reject(() => updater.DownloadAsync(update with { LatestVersion = "1.9.8-beta.1" }, null, CancellationToken.None), "rollback flag cannot authorize a beta target");
        await Reject(() => updater.DownloadAsync(update with { InstallerUrl = "https://example.com/untrusted.dmg" }, null, CancellationToken.None), "rollback retains official-source validation");
        var settings = new SettingsService(Path.Combine(root, "strings"));
        var localization = new LocalizationService(settings);
        foreach (var language in new[] { "zh-CN", "zh-TW", "en-US", "ja-JP" })
        {
            settings.Current.Language = language;
            Check(localization.Get("rollbackApp") != "rollbackApp"
                && localization.Format("rollbackDialogBody", "2.0.0-beta.2", "1.9.9").Contains("1.9.9"),
                "rollback actions and version warning translated: " + language);
        }
        // Reuse the original Mac updater suite without requiring Avalonia or a Mac host.
        await AppUpdaterTests.RunAsync(Path.Combine(root, "existing-mac"), Check, Reject);
        Console.WriteLine($"App rollback and Mac update checks passed: {count}. Fixtures: {root}");
    }
    private sealed class Transport(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(respond(request));
    }
}
