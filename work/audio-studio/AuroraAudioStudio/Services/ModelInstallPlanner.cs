using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed record ModelInstallPlan(string TargetPath, string EstimatedDownload, string RecommendedFreeSpace, string AvailableSpace, bool HasEnoughSpace, bool IsLarge);

public static class ModelInstallPlanner
{
    public static ModelInstallPlan Create(ModelDefinition model, string modelRoot)
    {
        var target = Path.GetFullPath(Path.Combine(modelRoot, model.RelativeRoot));
        var required = RecommendedBytes(model.Id);
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(target)!);
            return new(target, EstimatedDownload(model.Id), RecommendedFreeSpace(model.Id), FormatBytes(drive.AvailableFreeSpace), drive.AvailableFreeSpace >= required, required >= 4L * 1024 * 1024 * 1024);
        }
        catch { return new(target, EstimatedDownload(model.Id), RecommendedFreeSpace(model.Id), "未知", true, required >= 4L * 1024 * 1024 * 1024); }
    }

    public static string EstimatedDownload(string id) => id switch
    {
        "whisper-small" => "≈ 470 MB",
        "whisper-large-v3-turbo" => "≈ 1.6 GB",
        "whisper-large-v3" => "≈ 3.1 GB",
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => "≈ 4 GB",
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "≈ 1.5 GB",
        "minimax-music3" => "≈ 27 GB",
        "heartmula-3b" => "≈ 15.8 GB",
        "indextts-2-5" => "≈ 5.5 GB",
        "soulx-singer-svc" => "≈ 2.8 GB",
        "qwen3-asr-06b" => "≈ 1.6 GB",
        "qwen3-asr-17b" => "≈ 4.1 GB",
        "qwen3-forced-aligner" => "≈ 1.9 GB",
        "transkun" => "≈ 4.5 GB",
        "roformer" or "yourmt3" => "≈ 4–7 GB",
        "roformer-vocals" => "≈ 0.6–7 GB",
        "piano" => "≈ 165 MB",
        "faster-whisper" => "≈ 1.4 GB",
        "subtitle-edit" => "≈ 110 MB",
        "ace-step" => "≈ 32–36 GB",
        "seed-vc" => "≈ 8–10 GB",
        "f5-tts" or "demucs" => "≈ 4–7 GB",
        "basic-pitch" => "≈ 1 GB",
        _ => "—"
    };

    private static string RecommendedFreeSpace(string id) => id switch
    {
        "whisper-small" => "≈ 1 GB",
        "whisper-large-v3-turbo" => "≈ 3 GB",
        "whisper-large-v3" => "≈ 6 GB",
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => "≈ 8 GB",
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "≈ 3 GB",
        "minimax-music3" => "≈ 55 GB",
        "heartmula-3b" => "≈ 32 GB",
        "indextts-2-5" => "≈ 12 GB",
        "soulx-singer-svc" => "≈ 6 GB",
        "qwen3-asr-06b" => "≈ 4 GB",
        "qwen3-asr-17b" => "≈ 9 GB",
        "qwen3-forced-aligner" => "≈ 4 GB",
        "transkun" => "≈ 10 GB",
        "roformer" or "yourmt3" => "≈ 12 GB",
        "roformer-vocals" => "≈ 12 GB",
        "piano" => "≈ 500 MB",
        "faster-whisper" => "≈ 3 GB",
        "subtitle-edit" => "≈ 300 MB",
        "ace-step" => "≈ 50 GB",
        "seed-vc" => "≈ 20 GB",
        "f5-tts" or "demucs" => "≈ 12 GB",
        "basic-pitch" => "≈ 3 GB",
        _ => "请参考模型来源"
    };

    private static long RecommendedBytes(string id) => id switch
    {
        "whisper-small" => 1L * 1024 * 1024 * 1024,
        "whisper-large-v3-turbo" => 3L * 1024 * 1024 * 1024,
        "whisper-large-v3" => 6L * 1024 * 1024 * 1024,
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => 8L * 1024 * 1024 * 1024,
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => 3L * 1024 * 1024 * 1024,
        "minimax-music3" => 55L * 1024 * 1024 * 1024,
        "heartmula-3b" => 32L * 1024 * 1024 * 1024,
        "indextts-2-5" => 12L * 1024 * 1024 * 1024,
        "soulx-singer-svc" => 6L * 1024 * 1024 * 1024,
        "qwen3-asr-06b" => 4L * 1024 * 1024 * 1024,
        "qwen3-asr-17b" => 9L * 1024 * 1024 * 1024,
        "qwen3-forced-aligner" => 4L * 1024 * 1024 * 1024,
        "transkun" => 10L * 1024 * 1024 * 1024,
        "roformer" or "yourmt3" or "roformer-vocals" => 12L * 1024 * 1024 * 1024,
        "piano" => 512L * 1024 * 1024,
        "faster-whisper" => 3L * 1024 * 1024 * 1024,
        "subtitle-edit" => 512L * 1024 * 1024,
        "ace-step" => 50L * 1024 * 1024 * 1024,
        "seed-vc" => 20L * 1024 * 1024 * 1024,
        "f5-tts" or "demucs" => 12L * 1024 * 1024 * 1024,
        "basic-pitch" => 3L * 1024 * 1024 * 1024,
        _ => 2L * 1024 * 1024 * 1024
    };

    private static string FormatBytes(long value) => value >= 1024L * 1024 * 1024
        ? $"{value / 1073741824d:0.#} GB"
        : $"{value / 1048576d:0.#} MB";
}
