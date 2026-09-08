using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuroraAudioStudio.Core;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

static class AppUpdaterTests
{
    public static async Task RunAsync(string root, Action<bool, string> check, Func<Func<Task>, string, Task> reject)
    {
        var name = "Aurora-Audio-Studio-1.9.0-mac.6-arm64.dmg";
        var url = "https://github.com/swy2018/Aurora-Audio-Studio/releases/download/v1.9.0-mac.6/" + name;
        var bytes = Encoding.UTF8.GetBytes("fixture DMG bytes, never executable");
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        object Release(string tag, bool pre, params string[] names) => new { tag_name = tag, draft = false, prerelease = pre,
            assets = names.Select(n => new { name = n, browser_download_url = url[..(url.LastIndexOf('/') + 1)] + n }) };
        var json = JsonSerializer.Serialize(new[] { Release("v2.0.0", false, "Setup-x64.exe"), Release("v1.9.0-mac.6", false, name, name + ".sha256") });
        var update = MacAppUpdater.SelectRelease(json, "1.9.0-mac.5");
        var unifiedName = "Aurora-Audio-Studio-2.0.0-arm64.dmg";
        var unifiedJson = JsonSerializer.Serialize(new[] { Release("v2.0.0", false, "Aurora-Audio-Studio-2.0.0-Setup-x64.exe", unifiedName, unifiedName + ".sha256") });
        var betaName = "Aurora-Audio-Studio-2.0.0-beta.1-arm64.dmg";
        var channelJson = JsonSerializer.Serialize(new[] {
            Release("v1.9.9", false, "Aurora-Audio-Studio-1.9.9-arm64.dmg", "Aurora-Audio-Studio-1.9.9-arm64.dmg.sha256"),
            Release("v2.0.0-beta.1", true, betaName, betaName + ".sha256") });
        check(MacAppUpdater.SelectRelease(channelJson, "1.9.0-mac.6").LatestVersion == "1.9.9", "stable Mac channel excludes beta releases");
        check(MacAppUpdater.SelectRelease(channelJson, "1.9.9", "beta").LatestVersion == "2.0.0-beta.1", "beta Mac channel selects opt-in prerelease");
        check(!MacAppUpdater.SelectRelease(channelJson, "2.0.0-beta.1", "stable").UpdateAvailable, "Mac channel switch does not silently downgrade");
        check(MacAppUpdater.SelectRelease(unifiedJson, "2.0.0-beta.1", "beta").UpdateAvailable, "Mac beta upgrades to final release");
        check(MacAppUpdater.SelectRelease(unifiedJson, "1.9.0-mac.6").LatestVersion == "2.0.0"
            && MacAppUpdater.SelectRelease(unifiedJson, "1.9.0-mac.6").UpdateAvailable
            && !MacAppUpdater.SelectRelease(unifiedJson, "2.0.0").UpdateAvailable,
            "unified 2.0 release upgrades legacy Mac versions and never offers Windows assets or repeats itself");
        check(update.UpdateAvailable && update.LatestVersion == "1.9.0-mac.6", "Mac update selects Mac artifact despite newer Windows-only release");
        check(!MacAppUpdater.SelectRelease(json, "1.9.0-mac.6").UpdateAvailable, "installed Mac release is not offered again");
        check(MacAppUpdater.ParseVersion("1.9.0-mac.10") > MacAppUpdater.ParseVersion("1.9.0-mac.9")
            && MacAppUpdater.ParseVersion("../../bad") is null, "Mac build versions compare numerically and reject path-like versions");
        check(!MacAppUpdater.SelectRelease(JsonSerializer.Serialize(new[] { Release("v2.0.0", false, "Setup-x64.exe") }), "1.9.0-mac.5").UpdateAvailable,
            "Windows installer is never offered on Mac");
        check(!MacAppUpdater.SelectRelease(JsonSerializer.Serialize(new[] { Release("v1.9.0-mac.6", true, name, name + ".sha256") }), "1.9.0-mac.5").UpdateAvailable,
            "prerelease assets are not silently selected for stable updates");
        check(MacAppUpdater.ReadChecksum(new string('a', 64) + "  Setup-x64.exe\n" + sha + "  " + name, name, false) == sha,
            "multi-platform checksum manifest matches exact Mac filename rather than first hash");
        await reject(() => Task.Run(() => MacAppUpdater.ReadChecksum(new string('a', 64) + "  Windows.exe", name, false)), "checksum for another platform is rejected");
        var guard = new UpdateFlowGuard();
        check(guard.TryBegin() && !guard.TryBegin(), "concurrent app update checks share a single guard"); guard.End();
        check(guard.TryBegin() && !UpdateFlowGuard.ShouldRunDailyCheck("2026-09-09", new DateOnly(2026, 9, 9)), "daily app update check does not repeat on same date"); guard.End();

        foreach (var scenario in new[] { "fresh", "resume", "ignored-range", "complete-416", "bad-416", "empty-partial" })
        {
            var folder = Path.Combine(root, "updates-" + scenario);
            var directory = Path.Combine(folder, update.LatestVersion, sha); Directory.CreateDirectory(directory);
            var partial = Path.Combine(directory, name + ".partial");
            if (scenario != "fresh") await File.WriteAllBytesAsync(partial, scenario switch { "complete-416" => bytes, "bad-416" => [1, 2, 3], "empty-partial" => [], _ => bytes[..5] });
            var downloads = 0; long? firstRange = null;
            using var handler = new FixtureHandler(request =>
            {
                if (request.RequestUri!.AbsolutePath.EndsWith(".sha256")) return new(HttpStatusCode.OK) { Content = new StringContent(sha + "  " + name) };
                var range = request.Headers.Range?.Ranges.Single().From;
                if (downloads++ == 0) firstRange = range;
                if (scenario.EndsWith("416") && downloads == 1) return new(HttpStatusCode.RequestedRangeNotSatisfiable);
                var resume = scenario == "resume";
                var response = new HttpResponseMessage(resume ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = new ByteArrayContent(resume ? bytes[5..] : bytes) };
                if (resume) response.Content.Headers.ContentRange = new ContentRangeHeaderValue(5, bytes.Length - 1, bytes.Length);
                return response;
            });
            using var client = new HttpClient(handler);
            var updater = new MacAppUpdater(client, folder, "1.9.0-mac.5");
            var downloaded = await updater.DownloadAsync(update, null, CancellationToken.None);
            check(File.ReadAllBytes(downloaded).SequenceEqual(bytes) && (scenario != "resume" || firstRange == 5), "verified Mac download: " + scenario);
        }
        using (var client = new HttpClient(new FixtureHandler(_ => new(HttpStatusCode.ServiceUnavailable))))
            check(!(await new MacAppUpdater(client, root, "1.9.0-mac.5").CheckAsync()).CheckSucceeded, "network failure is not presented as up to date");
        using (var client = new HttpClient(new FixtureHandler(request => new(HttpStatusCode.OK) {
            Content = request.RequestUri!.AbsolutePath.EndsWith(".sha256") ? new StringContent(sha) : new ByteArrayContent([1, 2, 3]) })))
            await reject(() => new MacAppUpdater(client, Path.Combine(root, "corrupt-update"), "1.9.0-mac.5").DownloadAsync(update, null, CancellationToken.None), "corrupt download is never promoted to installer");
        using (var client = new HttpClient(new FixtureHandler(_ => throw new Exception("Network must not be reached"))))
            await reject(() => new MacAppUpdater(client, root, "1.9.0-mac.5").DownloadAsync(update with { InstallerUrl = "https://example.com/" + name }, null, CancellationToken.None), "unofficial installer URL rejected before request");
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
}
