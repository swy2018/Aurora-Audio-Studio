using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class UpdateService(SettingsService settings, LocalizationService localization)
{
#if UPDATE_VALIDATION
    private const string LatestReleaseApi = "https://api.github.com/repos/swy2018/Aurora-Audio-Studio/releases/tags/v0.9.9";
#else
    private const string LatestReleaseApi = "https://api.github.com/repos/swy2018/Aurora-Audio-Studio/releases/latest";
#endif
    private readonly HttpClient client = CreateClient();

    public async Task<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.0";
        try
        {
            using var response = await client.GetAsync(LatestReleaseApi, cancellationToken);
            response.EnsureSuccessStatusCode();
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("GitHub returned an empty release response.");
            var latestText = release.TagName.Trim().TrimStart('v', 'V');
            var available = Version.TryParse(latestText, out var latest) && Version.TryParse(current, out var installed) && latest > installed;
            var installer = release.Assets.FirstOrDefault(a => a.Name.EndsWith("Setup-x64.exe", StringComparison.OrdinalIgnoreCase));
            var checksum = release.Assets.FirstOrDefault(a => a.Name.Equals(installer?.Name + ".sha256", StringComparison.OrdinalIgnoreCase)
                || a.Name.EndsWith("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
            var message = available
                ? installer is null || checksum is null ? localization.Get("updateAssetsIncomplete") : localization.Get("updateReady")
                : localization.Get("updateUpToDate");
            return new(available, current, latestText, release.HtmlUrl, installer?.BrowserDownloadUrl, checksum?.BrowserDownloadUrl, message);
        }
        catch (Exception ex)
        {
            return new(false, current, current, "https://github.com/swy2018/Aurora-Audio-Studio/releases", null, null, localization.Format("updateCheckFailed", ex.Message), false);
        }
    }

    public async Task<OperationResult> DownloadAndInstallAsync(AppUpdateInfo update, IProgress<AppUpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!update.UpdateAvailable || update.InstallerUrl is null || update.ChecksumUrl is null)
            return new(false, localization.Get("updateUnavailable"));
        string? clientLogPath = null;
        try
        {
            var updateRoot = Path.Combine(settings.UpdatesRoot, update.LatestVersion);
            Directory.CreateDirectory(updateRoot);
            var installerPath = Path.Combine(updateRoot, "Aurora-Audio-Studio-Setup-x64.exe");
            clientLogPath = Path.Combine(settings.LogsRoot, $"update-client-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            WriteLog(clientLogPath, $"Update requested: {update.CurrentVersion} -> {update.LatestVersion}");
            progress?.Report(new(2, localization.Get("updateChecking"), true));
            var checksumText = await GetStringWithRetryAsync(update.ChecksumUrl, clientLogPath, cancellationToken);
            var expected = checksumText.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x => x.Length == 64 && x.All(Uri.IsHexDigit));
            if (expected is null) return new(false, localization.Get("updateChecksumInvalid"));

            progress?.Report(new(8, localization.Format("updateDownloading", update.LatestVersion)));
            await DownloadWithResumeAsync(update.InstallerUrl, installerPath, expected, clientLogPath, progress, update.LatestVersion, cancellationToken);
            WriteLog(clientLogPath, "Download completed.");
            progress?.Report(new(88, localization.Get("updateVerifying"), true));
            await using var stream = File.OpenRead(installerPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(installerPath);
                DeleteIfExists(installerPath + ".partial.sha256");
                WriteLog(clientLogPath, $"Verification failed: expected={expected}, actual={actual}");
                return new(false, localization.Get("updateVerificationFailed"));
            }
            DeleteIfExists(installerPath + ".partial.sha256");
            WriteLog(clientLogPath, $"Verification succeeded: sha256={actual}");
            var logPath = Path.Combine(settings.LogsRoot, $"update-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            progress?.Report(new(95, localization.Get("updatePreparingInstall"), true));
            var arguments = UpdateFlowGuard.BuildInstallerArguments(Environment.ProcessId, logPath);
            WriteLog(clientLogPath, $"Launching installer: {arguments}");
            var installer = Process.Start(new ProcessStartInfo(installerPath, arguments)
            {
                UseShellExecute = true,
                Verb = "runas"
            }) ?? throw new InvalidOperationException("The verified installer process could not be started.");
            await Task.Delay(500, cancellationToken);
            if (installer.HasExited && installer.ExitCode != 0)
                throw new InvalidOperationException($"The installer exited before handoff with code {installer.ExitCode}.");
            progress?.Report(new(100, localization.Get("updateInstallerHandoff")));
            WriteLog(clientLogPath, $"Installer handoff succeeded: pid={installer.Id}");
            return new(true, localization.Get("updateStarted"), installerPath);
        }
        catch (Exception ex)
        {
            if (clientLogPath is not null) WriteLog(clientLogPath, $"Update failed: {ex}");
            return new(false, localization.Format("updateInstallFailed", ex.Message));
        }
    }

    private async Task<string> GetStringWithRetryAsync(string url, string logPath, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            try { return await client.GetStringAsync(url, cancellationToken); }
            catch (Exception ex) when (ex is HttpRequestException or IOException && attempt < 4)
            {
                lastError = ex;
                WriteLog(logPath, $"Metadata request interrupted; retry {attempt}/3: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
        throw lastError ?? new HttpRequestException("The update metadata request failed.");
    }

    private async Task DownloadWithResumeAsync(
        string url,
        string installerPath,
        string expectedSha256,
        string logPath,
        IProgress<AppUpdateProgress>? progress,
        string version,
        CancellationToken cancellationToken)
    {
        var markerPath = installerPath + ".partial.sha256";
        var markerMatches = File.Exists(markerPath)
            && string.Equals((await File.ReadAllTextAsync(markerPath, cancellationToken)).Trim(), expectedSha256, StringComparison.OrdinalIgnoreCase);
        if (!markerMatches)
        {
            DeleteIfExists(installerPath);
            await File.WriteAllTextAsync(markerPath, expectedSha256, cancellationToken);
        }

        Exception? lastError = null;
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var existingLength = File.Exists(installerPath) ? new FileInfo(installerPath).Length : 0;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (existingLength > 0) request.Headers.Range = new RangeHeaderValue(existingLength, null);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existingLength > 0)
                {
                    await using var completedStream = File.OpenRead(installerPath);
                    var completedHash = Convert.ToHexString(await SHA256.HashDataAsync(completedStream, cancellationToken));
                    if (completedHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteLog(logPath, $"Existing partial file is already complete: bytes={existingLength}.");
                        return;
                    }
                    DeleteIfExists(installerPath);
                    WriteLog(logPath, "Server rejected the saved range and the local file was incomplete; restarting the download.");
                    continue;
                }
                response.EnsureSuccessStatusCode();

                var isResume = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent;
                if (!isResume) existingLength = 0;
                var total = response.Content.Headers.ContentRange?.Length
                    ?? (response.Content.Headers.ContentLength is long remaining ? existingLength + remaining : null);
                WriteLog(logPath, $"Download attempt {attempt}/8: offset={existingLength}, bytes={total?.ToString() ?? "unknown"}, resumed={isResume}");

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(
                    installerPath,
                    isResume ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    1024 * 128,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var buffer = new byte[1024 * 128];
                var received = existingLength;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    if (total > 0) progress?.Report(new(8 + (double)received / total.Value * 76, localization.Format("updateDownloading", version)));
                }
                await output.FlushAsync(cancellationToken);
                if (total is null || received >= total.Value) return;
                throw new EndOfStreamException($"The update stream ended at {received} of {total.Value} bytes.");
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException && attempt < 8)
            {
                lastError = ex;
                var saved = File.Exists(installerPath) ? new FileInfo(installerPath).Length : 0;
                WriteLog(logPath, $"Download interrupted; saved={saved}, retry={attempt}/7: {ex.Message}");
                progress?.Report(new(8, localization.Format("updateDownloading", version), true));
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 2, 12)), cancellationToken);
            }
        }
        throw lastError ?? new IOException("The update download could not be completed.");
    }

    private static HttpClient CreateClient()
    {
        var value = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        value.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Aurora-Audio-Studio", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.0"));
        value.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return value;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void WriteLog(string path, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
