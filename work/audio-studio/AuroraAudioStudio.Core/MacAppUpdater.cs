using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuroraAudioStudio.Models;
using AuroraAudioStudio.Services;

namespace AuroraAudioStudio.Core;

public sealed class MacAppUpdater(HttpClient client, string updatesRoot, string currentVersion, Func<string, string>? localize = null, string channel = "stable")
{
    private string T(string value) => localize?.Invoke(value) ?? value;
    public const string ReleasesUrl = "https://github.com/swy2018/Aurora-Audio-Studio/releases";
    public const string ReleasesApi = "https://api.github.com/repos/swy2018/Aurora-Audio-Studio/releases?per_page=100";

    public static Version? ParseVersion(string value)
    {
        return AppReleaseVersion.Parse(value);
    }

    public static AppUpdateInfo SelectRelease(string json, string current, string channel = "stable", bool rollback = false)
    {
        var installed = ParseVersion(current) ?? throw new InvalidDataException("当前 Mac 版本号无效。");
        using var doc = JsonDocument.Parse(json);
        var releases = doc.RootElement.EnumerateArray().Where(r => !r.GetProperty("draft").GetBoolean()
                && AppReleaseVersion.Allowed(r.GetProperty("tag_name").GetString() ?? "", r.GetProperty("prerelease").GetBoolean(), channel))
            .Select(r => (Release: r, Version: ParseVersion(r.GetProperty("tag_name").GetString() ?? "")))
            .Where(r => !rollback || AppReleaseVersion.IsPreviousStable(r.Release.GetProperty("tag_name").GetString() ?? "", r.Release.GetProperty("prerelease").GetBoolean(), current))
            .Where(r => r.Version is not null).OrderByDescending(r => r.Version).ToArray();
        foreach (var item in releases)
        {
            var release = item.Release;
            var tag = release.GetProperty("tag_name").GetString()!.TrimStart('v');
            var name = $"Aurora-Audio-Studio-{tag}-arm64.dmg";
            var assets = release.GetProperty("assets").EnumerateArray().ToArray();
            var installer = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == name);
            if (installer.ValueKind == JsonValueKind.Undefined) continue;
            var checksum = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == name + ".sha256");
            if (checksum.ValueKind == JsonValueKind.Undefined) checksum = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == "SHA256SUMS.txt");
            var newer = rollback || item.Version! > installed;
            return new(newer, current, tag, ReleasesUrl,
                installer.GetProperty("browser_download_url").GetString(),
                checksum.ValueKind == JsonValueKind.Undefined ? null : checksum.GetProperty("browser_download_url").GetString(),
                !newer ? "当前已是最新 Mac 版本。" : checksum.ValueKind == JsonValueKind.Undefined ? "发现 Mac 新版，但缺少校验文件，暂不能安装。" : rollback ? "rollbackAvailable" : "发现可安装的 Mac 新版。", IsRollback: rollback);
        }
        return new(false, current, current, ReleasesUrl, null, null, rollback ? "rollbackUnavailable" : "官方发布页暂未提供 Mac 安装包；不能将 Windows 版本当作 Mac 更新。");
    }

    public async Task<AppUpdateInfo> CheckAsync(CancellationToken token = default, bool rollback = false)
    {
        try
        {
            using var request = Request(ReleasesApi);
            using var response = await client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            var result = SelectRelease(await response.Content.ReadAsStringAsync(token), currentVersion, rollback ? "stable" : channel, rollback);
            return result with { Message = T(result.Message) };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(false, currentVersion, currentVersion, ReleasesUrl, null, null, T("检查 Mac 更新失败：") + ex.Message, false); }
    }

    public static string ReadChecksum(string text, string filename, bool singleFile)
    {
        foreach (var line in text.Split('\n'))
        {
            var match = Regex.Match(line.Trim(), @"^([0-9a-fA-F]{64})(?:\s+\*?(.+))?$", RegexOptions.CultureInvariant);
            if (match.Success && (match.Groups[2].Value == filename || singleFile && !match.Groups[2].Success))
                return match.Groups[1].Value.ToLowerInvariant();
        }
        throw new InvalidDataException("校验文件没有当前 Mac 安装包的 SHA-256，已停止更新。");
    }

    public async Task<string> DownloadAsync(AppUpdateInfo update, IProgress<AppUpdateProgress>? progress, CancellationToken token)
    {
        if (!update.UpdateAvailable || ParseVersion(update.LatestVersion) is not { } latest
            || (update.IsRollback ? !AppReleaseVersion.IsPreviousStable(update.LatestVersion, false, currentVersion) : latest <= ParseVersion(currentVersion))
            || update.InstallerUrl is null || update.ChecksumUrl is null) throw new InvalidOperationException("没有可安装的 Mac 更新。");
        var name = $"Aurora-Audio-Studio-{update.LatestVersion}-arm64.dmg";
        ValidateAssetUrl(update.InstallerUrl, name);
        var checksumName = Path.GetFileName(new Uri(update.ChecksumUrl).AbsolutePath);
        if (checksumName != name + ".sha256" && checksumName != "SHA256SUMS.txt") throw new InvalidDataException("Mac 校验文件名无效。");
        ValidateAssetUrl(update.ChecksumUrl, checksumName);
        using var checksumRequest = Request(update.ChecksumUrl);
        using var checksumResponse = await client.SendAsync(checksumRequest, token);
        checksumResponse.EnsureSuccessStatusCode();
        var expected = ReadChecksum(await checksumResponse.Content.ReadAsStringAsync(token), name, checksumName == name + ".sha256");
        var directory = Path.Combine(updatesRoot, update.LatestVersion, expected);
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, name);
        if (File.Exists(destination))
        {
            if (await MatchesAsync(destination, expected, token)) return destination;
            PreserveRejected(destination);
        }
        var partial = destination + ".partial";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
            using var request = Request(update.InstallerUrl);
            if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
            // .NET 10 streams the body without buffering the entire DMG.
            // https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.sendasync?view=net-10.0
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && offset > 0)
            {
                if (await MatchesAsync(partial, expected, token)) { File.Move(partial, destination); return destination; }
                PreserveRejected(partial); continue;
            }
            response.EnsureSuccessStatusCode();
            var resume = response.StatusCode == HttpStatusCode.PartialContent;
            if (resume && (offset == 0 || response.Content.Headers.ContentRange?.From != offset))
                throw new InvalidDataException("下载服务器返回了不匹配的续传位置。");
            if (!resume && File.Exists(partial)) { PreserveRejected(partial); offset = 0; }
            var total = response.Content.Headers.ContentRange?.Length ?? (response.Content.Headers.ContentLength is { } size ? offset + size : null);
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(partial, resume ? FileMode.Append : FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
            {
                var buffer = new byte[131072];
                int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token); offset += read;
                    progress?.Report(new(total > 0 ? 85.0 * offset / total.Value : 0, T("正在下载 Mac 更新…"), total is null));
                }
            }
            if (total is not null && offset != total) throw new EndOfStreamException("下载尚未完整，下次可继续下载。");
            progress?.Report(new(90, T("正在校验 Mac 安装包…"), true));
            if (!await MatchesAsync(partial, expected, token)) { PreserveRejected(partial); throw new InvalidDataException("Mac 安装包校验失败，未安装。请重新下载。"); }
            File.Move(partial, destination);
            return destination;
        }
        throw new IOException("未能完成续传，请重试。");
    }

    private static HttpRequestMessage Request(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Aurora-Audio-Studio/1.9.0");
        return request;
    }

    private static void ValidateAssetUrl(string url, string name)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com"
            || uri.UserInfo.Length != 0 || !uri.AbsolutePath.StartsWith("/swy2018/Aurora-Audio-Studio/releases/download/", StringComparison.Ordinal)
            || Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)) != name)
            throw new InvalidDataException("更新文件不属于 Aurora 官方发布地址。");
    }

    private static async Task<bool> MatchesAsync(string path, string expected, CancellationToken token)
    {
        await using var file = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(file, token)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void PreserveRejected(string path) => File.Move(path, path + ".rejected-" + Guid.NewGuid().ToString("N"));
}
