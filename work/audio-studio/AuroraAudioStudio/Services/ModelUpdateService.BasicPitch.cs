using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed partial class ModelUpdateService
{
    internal const string BasicPitchOnnxVersion = "0.4.0+aurora.onnx.1";
    internal const string BasicPitchUpstreamSha256 = "738adb503aae7fdfc7d1e1511aa0ce35052315f260a19531ef4c356708425db0";
    internal string BasicPitchOnnxWheelPath => Path.Combine(settings.Current.LocalAiRoot, ".aurora", "packages", $"basic_pitch-{BasicPitchOnnxVersion}-py2.py3-none-any.whl");

    internal async Task EnsureBasicPitchOnnxWheelAsync(IProgress<ModelInstallProgress>? progress, CancellationToken token)
    {
        const string name = "basic_pitch-0.4.0-py2.py3-none-any.whl";
        const string url = "https://files.pythonhosted.org/packages/99/0e/3a36d22562daeb0ae3c78ac78da3f8dba96543c5576ed57d4acb8ddbab5b/" + name;
        var original = Path.Combine(Path.GetDirectoryName(BasicPitchOnnxWheelPath)!, name);
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        if (!string.Equals(await FileHashAsync(original, token), BasicPitchUpstreamSha256, StringComparison.OrdinalIgnoreCase))
        {
            var download = original + ".download";
            await DownloadFileAsync(url, download, progress, token, 758279);
            if (!string.Equals(await FileHashAsync(download, token), BasicPitchUpstreamSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(new LocalizationService(settings).Get("dependencyHashMismatch"));
            token.ThrowIfCancellationRequested();
            File.Move(download, original, true);
        }
        token.ThrowIfCancellationRequested();
        CreateBasicPitchOnnxWheel(original, BasicPitchOnnxWheelPath);
    }

    internal static void CreateBasicPitchOnnxWheel(string original, string destination)
    {
        using (var input = File.OpenRead(original))
            if (Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant() != BasicPitchUpstreamSha256)
                throw new InvalidDataException("Basic Pitch upstream wheel checksum mismatch.");
        var pending = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        using (var source = ZipFile.OpenRead(original))
        using (var output = new ZipArchive(File.Open(pending, FileMode.CreateNew), ZipArchiveMode.Create))
        {
            var records = new List<string>();
            var oldInfo = "basic_pitch-0.4.0.dist-info/";
            var newInfo = $"basic_pitch-{BasicPitchOnnxVersion}.dist-info/";
            void Add(string path, byte[] bytes)
            {
                var entry = output.CreateEntry(path, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using (var stream = entry.Open()) stream.Write(bytes);
                var hash = Convert.ToBase64String(SHA256.HashData(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                records.Add($"{path},sha256={hash},{bytes.Length}");
            }
            foreach (var entry in source.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName == oldInfo + "RECORD") continue;
                using var content = new MemoryStream();
                using (var stream = entry.Open()) stream.CopyTo(content);
                var bytes = content.ToArray();
                var path = entry.FullName.StartsWith(oldInfo, StringComparison.Ordinal) ? newInfo + entry.FullName[oldInfo.Length..] : entry.FullName;
                if (entry.FullName == oldInfo + "METADATA")
                {
                    // Upstream 0.4.0 supports ONNX but makes TensorFlow mandatory on
                    // Python >=3.11. Declare the chosen runtime honestly, so pip check
                    // still validates the entire installed graph without exceptions.
                    var text = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n");
                    var split = text.IndexOf("\n\n", StringComparison.Ordinal);
                    var lines = text[..split].Split('\n').Where(line => !line.StartsWith("Provides-Extra:", StringComparison.Ordinal)
                        && !(line.StartsWith("Requires-Dist:", StringComparison.Ordinal) &&
                            (line.Contains("extra ==", StringComparison.Ordinal) || line.StartsWith("Requires-Dist: tensorflow", StringComparison.Ordinal)
                            || line.StartsWith("Requires-Dist: coremltools", StringComparison.Ordinal) || line.StartsWith("Requires-Dist: tflite-runtime", StringComparison.Ordinal)
                            || line.StartsWith("Requires-Dist: onnxruntime", StringComparison.Ordinal))))
                        .Select(line => line == "Version: 0.4.0" ? "Version: " + BasicPitchOnnxVersion : line);
                    bytes = Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\nRequires-Dist: onnxruntime>=1.17,<2\nRequires-Dist: numpy<2\nRequires-Dist: setuptools<81" + text[split..]);
                }
                Add(path, bytes);
            }
            Add(newInfo + "AURORA-ONNX.txt", Encoding.UTF8.GetBytes(
                "Aurora ONNX runtime packaging profile. Only distribution metadata and RECORD were changed.\n" +
                "Spotify Basic Pitch Python code, model bytes and upstream license notices are unchanged.\n" +
                "Upstream 0.4.0 wheel SHA-256: " + BasicPitchUpstreamSha256 + "\n"));
            var record = output.CreateEntry(newInfo + "RECORD");
            record.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var writer = new StreamWriter(record.Open(), new UTF8Encoding(false));
            writer.Write(string.Join('\n', records) + "\n" + newInfo + "RECORD,,\n");
        }
        File.Move(pending, destination, true);
    }
}
