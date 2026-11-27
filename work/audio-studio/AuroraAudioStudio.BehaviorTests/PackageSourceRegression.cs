using System.Net;
using System.Text.Json;
using AuroraAudioStudio.Services;

internal static class PackageSourceRegression
{
    private sealed class Metadata : HttpMessageHandler
    {
        public string Comparison { get; set; } = "behind";
        public bool Prerelease { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.PathAndQuery;
            object value = path.EndsWith("/releases/latest") ? new { tag_name = "v0.1.5", prerelease = Prerelease, draft = false, published_at = "2026-11-27T00:00:00Z" }
                : path.Contains("/compare/") ? new { status = Comparison }
                : path.Contains("?per_page=") ? new[] { new { sha = new string('b', 40) } }
                : new { sha = new string('a', 40), commit = new { committer = new { date = "2026-11-27T00:00:00Z" } } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) });
        }
    }

    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Aurora-SourcePolicy-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsService(Path.Combine(root, "settings"));
        var catalog = new ModelCatalogService(settings);
        var handler = new Metadata();
        using var http = new HttpClient(handler);
        var service = new ModelUpdateService(catalog, settings, metadataTransport: http);
        var model = catalog.Find("roformer")!;
        var info = Path.Combine(root, "Lib", "site-packages", "bs_roformer_infer-0.1.5.dist-info");
        Directory.CreateDirectory(info);
        await File.WriteAllTextAsync(Path.Combine(info, "METADATA"), "Version: 0.1.5\n");
        async Task Check(string expected, string name)
        {
            var result = await service.CheckPackageSourceAsync(model, root, default);
            if (result.Path != expected) throw new Exception(name + ": " + result.Message);
            Console.WriteLine("PASS " + name);
        }
        await Check("current", "legacy PyPI formal version remains current without treating HEAD as a release");
        await File.WriteAllTextAsync(Path.Combine(info, "METADATA"), "Version: 0.1.4\n");
        await Check("available", "older PyPI version can update to pinned formal source");
        await File.WriteAllTextAsync(Path.Combine(info, "METADATA"), "Version: 0.1.6\n");
        await Check("current", "higher local package version is never offered a downgrade");
        await File.WriteAllTextAsync(Path.Combine(info, "direct_url.json"), JsonSerializer.Serialize(new { url = "https://github.com/openmirlab/bs-roformer-infer.git", vcs_info = new { vcs = "git", commit_id = new string('b', 40) } }));
        await Check("current", "manual Git install newer than formal target is preserved");
        handler.Comparison = "ahead";
        await Check("available", "older Git revision can update to the formal target");
        handler.Comparison = "diverged";
        await Check("unverified", "divergent local source is not silently replaced");
        handler.Prerelease = true;
        try { await service.ResolvePackageSourceAsync(ModelUpdateService.PackageSourceRepository("roformer")!, default); throw new Exception("Prerelease accepted"); }
        catch (IOException) { Console.WriteLine("PASS source resolver rejects a prerelease as a formal target"); }
    }
}
