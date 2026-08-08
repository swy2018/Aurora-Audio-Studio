using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed record ModelInstallPlan(string TargetPath, string EstimatedDownload, string RecommendedFreeSpace, string AvailableSpace, bool HasEnoughSpace);

public static class ModelInstallPlanner
{
    public static ModelInstallPlan Create(ModelDefinition model, string modelRoot)
    {
        var target = Path.GetFullPath(Path.Combine(modelRoot, model.RelativeRoot));
        var required = RecommendedBytes(model.Id);
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(target)!);
            return new(target, EstimatedDownload(model.Id), RecommendedFreeSpace(model.Id), FormatBytes(drive.AvailableFreeSpace), drive.AvailableFreeSpace >= required);
        }
        catch { return new(target, EstimatedDownload(model.Id), RecommendedFreeSpace(model.Id), "未知", true); }
    }

    public static string EstimatedDownload(string id) => id switch
    {
        "whisper-small" => "≈ 470 MB",
        "whisper-large-v3-turbo" => "≈ 1.6 GB",
        "whisper-large-v3" => "≈ 3.1 GB",
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => "≈ 4 GB",
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "≈ 1.5 GB",
        "ace-step" => "≈ 8 GB",
        "seed-vc" => "≈ 5 GB",
        "f5-tts" or "demucs" or "basic-pitch" => "< 1 GB",
        _ => "—"
    };

    private static string RecommendedFreeSpace(string id) => id switch
    {
        "whisper-small" => "≈ 1 GB",
        "whisper-large-v3-turbo" => "≈ 3 GB",
        "whisper-large-v3" => "≈ 6 GB",
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => "≈ 8 GB",
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "≈ 3 GB",
        "ace-step" => "≈ 16 GB",
        "seed-vc" => "≈ 10 GB",
        "f5-tts" or "demucs" or "basic-pitch" => "≈ 2 GB",
        _ => "请参考模型来源"
    };

    private static long RecommendedBytes(string id) => id switch
    {
        "whisper-small" => 1L * 1024 * 1024 * 1024,
        "whisper-large-v3-turbo" => 3L * 1024 * 1024 * 1024,
        "whisper-large-v3" => 6L * 1024 * 1024 * 1024,
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => 8L * 1024 * 1024 * 1024,
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => 3L * 1024 * 1024 * 1024,
        "ace-step" => 16L * 1024 * 1024 * 1024,
        "seed-vc" => 10L * 1024 * 1024 * 1024,
        _ => 2L * 1024 * 1024 * 1024
    };

    private static string FormatBytes(long value) => value >= 1024L * 1024 * 1024
        ? $"{value / 1073741824d:0.#} GB"
        : $"{value / 1048576d:0.#} MB";
}
