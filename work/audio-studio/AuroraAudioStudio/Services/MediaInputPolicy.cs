using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public static class MediaInputPolicy
{
    private static readonly HashSet<string> Audio = new(StringComparer.OrdinalIgnoreCase) { ".wav", ".flac", ".mp3", ".m4a", ".aac", ".ogg", ".opus", ".wma" };
    private static readonly HashSet<string> Video = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v" };

    public static IReadOnlyList<string> Extensions(string feature) => feature == "subtitles" ? Audio.Concat(Video).ToArray() : Audio.Concat(feature == "separation" ? Video : []).ToArray();

    public static bool IsSupported(string feature, string path) => Extensions(feature).Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static async Task<MediaInfo> InspectAsync(string path, string localAiRoot, CancellationToken token = default, bool decode = true)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidDataException("素材文件不存在或为空，请重新选择素材。");
        var probe = AudioRuntime.FindFfprobe(localAiRoot);
        // Uncompressed WAV needs no external codec. Preserve this workflow on minimal model installs.
        if ((probe is null || decode && AudioRuntime.FindFfmpeg(localAiRoot) is null) && Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            return InspectPcmWave(path);
        if (probe is null) throw new InvalidOperationException("缺少 FFprobe，请安装包含 FFprobe 的完整 FFmpeg 组件。");
        // Structured stream metadata avoids parsing translated or human-oriented FFmpeg banners.
        // https://ffmpeg.org/ffprobe.html#Main-options
        var json = await CaptureAsync(probe, ["-v", "error", "-select_streams", "a:0", "-show_entries",
            "stream=codec_name,sample_rate,channels,duration:format=duration", "-of", "json", "-i", path], token);
        var metadata = ParseProbe(json, new FileInfo(path).Length);
        if (decode)
        {
            var ffmpeg = AudioRuntime.FindFfmpeg(localAiRoot) ?? throw new InvalidOperationException("缺少 FFmpeg 音频组件。");
            await CaptureAsync(ffmpeg, ["-nostdin", "-v", "error", "-xerror", "-i", path, "-map", "0:a:0", "-t", "1", "-f", "null", "-"], token);
        }
        return metadata;
    }

    internal static MediaInfo ParseProbe(string json, long bytes)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("streams", out var streams) || streams.GetArrayLength() == 0)
            throw new InvalidDataException("素材中没有可处理的音轨，请选择包含声音的文件。");
        var stream = streams[0];
        static double? Number(JsonElement element, string name)
            => element.TryGetProperty(name, out var value) && double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && double.IsFinite(number) && number >= 0 ? number : null;
        var rate = Number(stream, "sample_rate") ?? 0;
        var channels = Number(stream, "channels") ?? 0;
        var duration = Number(stream, "duration");
        if (duration is null && document.RootElement.TryGetProperty("format", out var format)) duration = Number(format, "duration");
        if (rate <= 0 || rate > int.MaxValue || channels <= 0 || channels > int.MaxValue || duration == 0)
            throw new InvalidDataException("素材音轨参数无效，请重新导出音频后再试。");
        return new(duration, (int)rate, (int)channels, stream.TryGetProperty("codec_name", out var codec) ? codec.GetString() ?? "" : "", bytes);
    }

    internal static MediaInfo InspectPcmWave(string path)
    {
        var info = ArtifactValidator.Inspect(path);
        if (info.Format != "WAV") throw new InvalidDataException("素材音轨参数无效，请重新导出音频后再试。");
        return new(info.DurationSeconds, info.SampleRate, info.Channels, "WAV", info.Bytes);
    }

    private static async Task<string> CaptureAsync(string tool, IEnumerable<string> arguments, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var info = new ProcessStartInfo(tool) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = info };
        BackgroundProcess.Start(process);
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try
        {
            await BackgroundProcess.WaitForExitAsync(process, timeout.Token);
            await Task.WhenAll(output, error);
            if (process.ExitCode != 0) throw new InvalidDataException("音频预检未通过：" + (await error).Trim());
            return await output;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("音频预检超时，请检查素材文件与存储设备。"); }
        finally
        {
            await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
