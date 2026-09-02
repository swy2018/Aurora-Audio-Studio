using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public static class ModelHealthPolicy
{
    public static bool IsReady(ModelDefinition model, string localAiRoot) => MissingRequirements(model, localAiRoot).Count == 0;

    public static IReadOnlyList<string> MissingRequirements(ModelDefinition model, string localAiRoot)
    {
        var root = Path.Combine(localAiRoot, model.RelativeRoot);
        var required = new List<(string Label, string Path)>
        {
            ("模型标记", Path.Combine(root, model.Marker))
        };

        switch (model.Id)
        {
            case "ace-step":
                if (!File.Exists(Path.Combine(root, "python_embeded", "python.exe")) && !File.Exists(Path.Combine(root, ".venv", "Scripts", "python.exe")))
                    required.Add(("Python 运行环境", Path.Combine(root, "python_embeded", "python.exe")));
                break;
            case "seed-vc":
                required.Add(("Python 运行环境", Path.Combine(root, ".venv", "Scripts", "python.exe")));
                required.Add(("Seed-VC 权重", Path.Combine(root, "checkpoints", "manual", "DiT_seed_v2_uvit_whisper_base_f0_44k_bigvgan_pruned_ft_ema_v2.pth")));
                required.Add(("Seed-VC 配置", Path.Combine(root, "checkpoints", "manual", "config_dit_mel_seed_uvit_whisper_base_f0_44k.yml")));
                break;
            case "qwen3-tts-base":
            case "qwen3-tts-custom":
            case "qwen3-tts-design":
            case "qwen3-tts-06b-base":
            case "qwen3-tts-06b-custom":
                required.Add(("Qwen3-TTS 启动器", Path.Combine(localAiRoot, "Qwen3-TTS", "Python312", "Scripts", "qwen-tts-demo.exe")));
                break;
            case "whisper-small":
            case "whisper-large-v3-turbo":
            case "whisper-large-v3":
                required.Add(("Faster-Whisper XXL", Path.Combine(localAiRoot, "Faster-Whisper-XXL", "Faster-Whisper-XXL", "faster-whisper-xxl.exe")));
                break;
            case "minimax-music3":
                required.Add(("MiniMax Python 运行环境", Path.Combine(localAiRoot, "AudioTools", "minimax-music3-env", "Scripts", "python.exe")));
                break;
        }

        var missing = required.Where(item => !File.Exists(item.Path)).Select(item => item.Label).ToList();
        if (model.Id.Equals("ace-step", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var folder in new[] { "acestep-v15-xl-turbo", "acestep-5Hz-lm-1.7B", "Qwen3-Embedding-0.6B", "vae" })
                if (!Directory.Exists(Path.Combine(root, "checkpoints", folder))) missing.Add("ACE-Step 权重 " + folder);
        }
        return missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
