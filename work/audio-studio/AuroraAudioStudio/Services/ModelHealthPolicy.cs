using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public static class ModelHealthPolicy
{
    public static bool IsReady(ModelDefinition model, string localAiRoot) => MissingRequirements(model, localAiRoot).Count == 0;

    public static IReadOnlyList<string> MissingRequirements(ModelDefinition model, string localAiRoot)
    {
        var root = Path.Combine(localAiRoot, model.RelativeRoot);
        if (model.Id.StartsWith("whisper-", StringComparison.OrdinalIgnoreCase) && !File.Exists(Path.Combine(root, model.Marker)))
        {
            var folder = Path.GetFileName(root);
            const string prefix = "faster-whisper-";
            if (folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var legacy = Path.Combine(Path.GetDirectoryName(root)!, folder[prefix.Length..]);
                if (File.Exists(Path.Combine(legacy, model.Marker))) root = legacy;
            }
        }
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
            case "transkun":
                required.Add(("TransKun setuptools 运行组件", Path.Combine(root, "Lib", "site-packages", "pkg_resources", "__init__.py")));
                required.Add(("TransKun 音频运行组件", Path.Combine(root, "Lib", "site-packages", "torchaudio", "lib", "libtorchaudio.pyd")));
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
        if (model.Id.Equals("transkun", StringComparison.OrdinalIgnoreCase) && !TorchAudioVersionsMatch(root)) missing.Add("TransKun PyTorch / torchaudio 版本匹配");
        if (model.Id.StartsWith("qwen3-tts-", StringComparison.OrdinalIgnoreCase) && !ExecutableOnPath("sox.exe")) missing.Add("Qwen3-TTS SoX 音频组件");
        if (model.Id.Equals("ace-step", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var folder in new[] { "acestep-v15-turbo", "acestep-v15-xl-turbo", "acestep-5Hz-lm-1.7B", "Qwen3-Embedding-0.6B", "vae" })
            {
                var checkpoint = Path.Combine(root, "checkpoints", folder);
                if (!ContainsModelWeights(checkpoint)) missing.Add("ACE-Step 权重 " + folder);
            }
        }
        if (model.Id.Equals("seed-vc", StringComparison.OrdinalIgnoreCase))
        {
            var checkpoints = Path.Combine(root, "checkpoints");
            var hubCache = Path.Combine(checkpoints, "hf_cache");
            if (!HuggingFaceSnapshotContains(checkpoints, "models--funasr--campplus", "campplus_cn_common.bin"))
                missing.Add("Seed-VC CAMPPlus 声纹模型");
            if (!HuggingFaceSnapshotContains(checkpoints, "models--lj1995--VoiceConversionWebUI", "rmvpe.pt"))
                missing.Add("Seed-VC RMVPE 音高模型");
            if (!HuggingFaceSnapshotContains(hubCache, "models--nvidia--bigvgan_v2_44khz_128band_512x", "bigvgan_generator.pt", "config.json"))
                missing.Add("Seed-VC BigVGAN 声码器");
            if (!HuggingFaceSnapshotContains(hubCache, "models--openai--whisper-small", "model.safetensors", "config.json", "preprocessor_config.json"))
                missing.Add("Seed-VC Whisper 编码器");
        }
        return missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool ContainsModelWeights(string directory)
    {
        if (!Directory.Exists(directory)) return false;
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any(path =>
            {
                var extension = Path.GetExtension(path);
                if (!new[] { ".safetensors", ".bin", ".pth", ".pt", ".ckpt" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;
                try { return new FileInfo(path).Length > 0; }
                catch { return false; }
            });
        }
        catch { return false; }
    }

    private static bool HuggingFaceSnapshotContains(string cacheRoot, string repositoryCache, params string[] files)
    {
        try
        {
            var repositoryRoot = Path.Combine(cacheRoot, repositoryCache);
            var reference = Path.Combine(repositoryRoot, "refs", "main");
            if (!File.Exists(reference)) return false;
            var revision = File.ReadAllText(reference).Trim();
            if (string.IsNullOrWhiteSpace(revision) || revision.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            var snapshot = Path.Combine(repositoryRoot, "snapshots", revision);
            return files.All(file =>
            {
                var path = Path.Combine(snapshot, file);
                return File.Exists(path) && new FileInfo(path).Length > 0;
            });
        }
        catch { return false; }
    }

    private static bool TorchAudioVersionsMatch(string root)
    {
        var sitePackages = Path.Combine(root, "Lib", "site-packages");
        if (!Directory.Exists(sitePackages)) return false;
        try
        {
            var torch = PackageSeries(sitePackages, "torch");
            var audio = PackageSeries(sitePackages, "torchaudio");
            return torch.Count == 1 && audio.Count == 1 && torch.SetEquals(audio);
        }
        catch { return false; }
    }

    private static HashSet<string> PackageSeries(string sitePackages, string package)
        => Directory.EnumerateDirectories(sitePackages, $"{package}-*.dist-info", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name![package.Length..^".dist-info".Length].TrimStart('-').Split('+')[0].Split('.'))
            .Where(parts => parts.Length >= 2)
            .Select(parts => parts[0] + "." + parts[1])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool ExecutableOnPath(string fileName)
    {
        var paths = string.Join(';', new[]
        {
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        foreach (var directory in paths.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try { if (File.Exists(Path.Combine(directory.Trim('"'), fileName))) return true; } catch { }
        }
        return false;
    }
}
