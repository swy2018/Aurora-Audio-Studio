using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Net;
using System.Runtime.InteropServices;
using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class BackendService(SettingsService settings)
{
    private readonly Dictionary<string, Process> processes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> activeModels = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? navigationCancellation;
    private string? startingKey;
    public event EventHandler<string>? StatusChanged;

    private string Root => settings.Current.LocalAiRoot;
    private string Logs => settings.LogsRoot;
    private string MusicRoot => Path.Combine(Root, "ACE-Step-1.5");
    private string TtsRoot => Path.Combine(Root, "Qwen3-TTS");
    private string SeedRoot => Path.Combine(Root, "Seed-VC");
    private string FfmpegRoot => Path.Combine(Root, "Faster-Whisper-XXL", "Faster-Whisper-XXL");

    public async Task<OperationResult> StartWorkbenchAsync(string feature, string model, string language)
    {
        try
        {
            return feature switch
            {
                "music" when model.Equals("minimax-music3", StringComparison.OrdinalIgnoreCase) => await StartMiniMaxMusic3Async(language),
                "music" => await StartMusicAsync(language),
                "voice" when model.StartsWith("qwen3-tts-", StringComparison.OrdinalIgnoreCase) => await StartTtsAsync(model),
                "voice" when model.Equals("f5-tts", StringComparison.OrdinalIgnoreCase) => await StartF5TtsAsync(),
                "singing" => await StartSeedVcAsync(),
                _ => new OperationResult(false, "The selected model does not expose an embedded workbench yet.")
            };
        }
        catch (Exception ex)
        {
            WriteLog("backend-error.log", ex.ToString());
            return new OperationResult(false, ex.Message);
        }
    }

    private async Task<OperationResult> StartMusicAsync(string language)
    {
        var python = File.Exists(Path.Combine(MusicRoot, "python_embeded", "python.exe"))
            ? Path.Combine(MusicRoot, "python_embeded", "python.exe")
            : Path.Combine(MusicRoot, ".venv", "Scripts", "python.exe");
        var script = Path.Combine(MusicRoot, "acestep", "acestep_v15_pipeline.py");
        var healthModel = new ModelDefinition("ace-step", "ACE-Step 1.5 XL Turbo", "music", "ACE-Step-1.5", @"acestep\acestep_v15_pipeline.py", "GitHub Release + Hugging Face", "github-release-git", "https://github.com/ACE-Step/ACE-Step-1.5.git", true);
        var missing = ModelHealthPolicy.MissingRequirements(healthModel, Root);
        if (missing.Count > 0) return new OperationResult(false, "ACE-Step 安装不完整：" + string.Join("、", missing) + "。请在模型管理中执行检查 / 修复。");
        if (!File.Exists(python) || !File.Exists(script)) return Missing("ACE-Step 1.5 XL Turbo");
        if (!HasXlLoadHeadroom(out var ram, out var commit))
            return new OperationResult(false, $"ACE-Step needs more memory headroom. Available RAM: {ram:F1} GB, commit headroom: {commit:F1} GB.");
        Stop("singing");
        if (!IsRunning("music") || !activeModels.TryGetValue("music", out var current) || !current.Equals("ace-step", StringComparison.OrdinalIgnoreCase))
        {
            Stop("music");
            processes["music"] = StartHidden("music", python, $"\"{script}\" --port 7860 --server-name 127.0.0.1 --language {(language.StartsWith("en") ? "en" : "zh")} --config_path acestep-v15-xl-turbo --lm_model_path acestep-5Hz-lm-1.7B --offload_to_cpu true --init_service true", MusicRoot,
                new Dictionary<string, string>
                {
                    ["HF_HUB_OFFLINE"] = "1",
                    ["ACESTEP_CHECKPOINTS_DIR"] = Path.Combine(MusicRoot, "checkpoints")
                });
            activeModels["music"] = "ace-step";
        }
        return await WaitForUrlAsync("music", "http://127.0.0.1:7860", "Starting ACE-Step 1.5 XL Turbo");
    }

    private async Task<OperationResult> StartMiniMaxMusic3Async(string language)
    {
        var modelRoot = Path.Combine(Root, "MiniMax-Music3");
        var environmentRoot = Path.Combine(Root, "AudioTools", "minimax-music3-env");
        var python = Path.Combine(environmentRoot, "Scripts", "python.exe");
        var script = Path.Combine(AppContext.BaseDirectory, "Tools", "minimax_music3_webui.py");
        if (!File.Exists(python) || !File.Exists(Path.Combine(modelRoot, "modular_model_index.json")) || !File.Exists(script)) return Missing("MiniMax-Music3");
        if (!HasXlLoadHeadroom(out var ram, out var commit))
            return new OperationResult(false, $"MiniMax-Music3 needs more memory headroom. Available RAM: {ram:F1} GB, commit headroom: {commit:F1} GB.");
        Stop("singing");
        if (!IsRunning("music") || !activeModels.TryGetValue("music", out var current) || !current.Equals("minimax-music3", StringComparison.OrdinalIgnoreCase))
        {
            Stop("music");
            var output = OutputFolder("AI音乐");
            processes["music"] = StartHidden("music", python, $"\"{script}\" --model \"{modelRoot}\" --output \"{output}\" --port 7860 --language {(language.StartsWith("en") ? "en" : "zh")}", environmentRoot,
                new Dictionary<string, string> { ["GRADIO_TEMP_DIR"] = output, ["PYTHONUTF8"] = "1", ["HF_HUB_OFFLINE"] = "1" });
            activeModels["music"] = "minimax-music3";
        }
        return await WaitForUrlAsync("music", "http://127.0.0.1:7860", "正在启动 MiniMax-Music3");
    }

    private async Task<OperationResult> StartTtsAsync(string modelId)
    {
        var launcher = Path.Combine(TtsRoot, "Python312", "Scripts", "qwen-tts-demo.exe");
        var modelFolder = modelId switch
        {
            "qwen3-tts-base" => "Qwen3-TTS-12Hz-1.7B-Base",
            "qwen3-tts-custom" => "Qwen3-TTS-12Hz-1.7B-CustomVoice",
            "qwen3-tts-design" => "Qwen3-TTS-12Hz-1.7B-VoiceDesign",
            "qwen3-tts-06b-base" => "Qwen3-TTS-12Hz-0.6B-Base",
            "qwen3-tts-06b-custom" => "Qwen3-TTS-12Hz-0.6B-CustomVoice",
            _ => ""
        };
        var checkpoint = Path.Combine(TtsRoot, "models", modelFolder);
        if (!File.Exists(launcher) || !File.Exists(Path.Combine(checkpoint, "model.safetensors"))) return Missing("Qwen3-TTS 1.7B");
        Stop("singing");
        if (!IsRunning("voice") || !activeModels.TryGetValue("voice", out var current) || !current.Equals(modelId, StringComparison.OrdinalIgnoreCase))
        {
            Stop("voice");
            var output = OutputFolder("AI配音");
            processes["voice"] = StartHidden("voice", launcher, $"\"{checkpoint}\" --device cuda:0 --dtype bfloat16 --no-flash-attn --ip 127.0.0.1 --port 7861 --no-share --concurrency 1", TtsRoot,
                new Dictionary<string, string>
                {
                    ["HF_HUB_OFFLINE"] = "1",
                    ["GRADIO_TEMP_DIR"] = output,
                    ["PYTHONUTF8"] = "1",
                    ["PATH"] = Path.Combine(TtsRoot, "Python312", "Scripts") + ";" + Environment.GetEnvironmentVariable("PATH")
                });
            activeModels["voice"] = modelId;
        }
        return await WaitForUrlAsync("voice", "http://127.0.0.1:7861", "正在启动 Qwen3-TTS 1.7B");
    }

    private async Task<OperationResult> StartF5TtsAsync()
    {
        var root = Path.Combine(Root, "AudioTools", "f5-tts-env");
        var launcher = Path.Combine(root, "Scripts", "f5-tts_infer-gradio.exe");
        if (!File.Exists(launcher)) return Missing("F5-TTS");
        Stop("singing");
        if (!IsRunning("voice") || !activeModels.TryGetValue("voice", out var current) || !current.Equals("f5-tts", StringComparison.OrdinalIgnoreCase))
        {
            Stop("voice");
            processes["voice"] = StartHidden("voice", launcher, "--host 127.0.0.1 --port 7861", root,
                new Dictionary<string, string> { ["GRADIO_TEMP_DIR"] = OutputFolder("AI配音"), ["PYTHONUTF8"] = "1" });
            activeModels["voice"] = "f5-tts";
        }
        return await WaitForUrlAsync("voice", "http://127.0.0.1:7861", "正在启动 F5-TTS");
    }

    private async Task<OperationResult> StartSeedVcAsync()
    {
        var python = Path.Combine(SeedRoot, ".venv", "Scripts", "python.exe");
        var script = Path.Combine(SeedRoot, "app_svc_local.py");
        var checkpoint = Path.Combine(SeedRoot, "checkpoints", "manual", "DiT_seed_v2_uvit_whisper_base_f0_44k_bigvgan_pruned_ft_ema_v2.pth");
        var config = Path.Combine(SeedRoot, "checkpoints", "manual", "config_dit_mel_seed_uvit_whisper_base_f0_44k.yml");
        var healthModel = new ModelDefinition("seed-vc", "Seed-VC 44.1k", "singing", "Seed-VC", "app_svc_local.py", "GitHub Release + Hugging Face", "github-release-git", "https://github.com/Plachtaa/seed-vc.git", true);
        var missing = ModelHealthPolicy.MissingRequirements(healthModel, Root);
        if (missing.Count > 0) return new OperationResult(false, "Seed-VC 安装不完整：" + string.Join("、", missing) + "。请在模型管理中执行检查 / 修复。");
        if (!File.Exists(python) || !File.Exists(script) || !File.Exists(checkpoint) || !File.Exists(config)) return Missing("Seed-VC 44.1k");
        Stop("music");
        Stop("voice");
        if (!IsRunning("singing"))
        {
            var output = OutputFolder("AI歌声克隆");
            var environment = new Dictionary<string, string>
            {
                ["PATH"] = FfmpegRoot + ";" + Environment.GetEnvironmentVariable("PATH"),
                ["GRADIO_SERVER_NAME"] = "127.0.0.1",
                ["GRADIO_SERVER_PORT"] = "7862",
                ["GRADIO_TEMP_DIR"] = output,
                ["AI_OUTPUT_DIR"] = output,
                ["HF_HUB_DISABLE_XET"] = "1",
                ["HF_HUB_OFFLINE"] = "1",
                ["TRANSFORMERS_OFFLINE"] = "1",
                ["HF_HUB_DISABLE_TELEMETRY"] = "1",
                ["NUMBA_CACHE_DIR"] = Path.Combine(Root, "AudioTools", "numba-cache"),
                ["PYTHONUTF8"] = "1"
            };
            processes["singing"] = StartHidden("singing", python, $"\"{script}\" --checkpoint \"{checkpoint}\" --config \"{config}\" --fp16 True", SeedRoot, environment);
        }
        return await WaitForUrlAsync("singing", "http://127.0.0.1:7862", "Starting Seed-VC 44.1k");
    }

    public async Task<OperationResult> RunUtilityAsync(string feature, string inputPath, string modelId, string language, IProgress<TaskExecutionProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsRunning("utility")) return new OperationResult(false, "Another local task is already running.");
        return feature switch
        {
            "separation" => await SeparateAsync(inputPath, modelId, progress, cancellationToken),
            "transcription" => await TranscribeAsync(inputPath, modelId, progress, cancellationToken),
            "subtitles" => await SubtitleAsync(inputPath, modelId, language, progress, cancellationToken),
            _ => new OperationResult(false, "Unsupported local task.")
        };
    }

    private async Task<OperationResult> SeparateAsync(string source, string modelId, IProgress<TaskExecutionProgress>? progress, CancellationToken cancellationToken)
    {
        if (modelId.Equals("demucs", StringComparison.OrdinalIgnoreCase))
        {
            var demucs = Path.Combine(Root, "AudioTools", "demucs-env", "Scripts", "demucs.exe");
            if (!File.Exists(demucs)) return Missing("Demucs 4");
            var demucsOutput = OutputFolder("AI分轨");
            var demucsInfo = Hidden(demucs, demucsOutput);
            demucsInfo.ArgumentList.Add("-o"); demucsInfo.ArgumentList.Add(demucsOutput); demucsInfo.ArgumentList.Add(source);
            demucsInfo.Environment["PYTHONUTF8"] = "1";
            return await RunCapturedAsync("utility", demucsInfo, "demucs", demucsOutput, progress, cancellationToken);
        }
        var exe = Path.Combine(Root, "AudioTools", "roformer-env", "Scripts", "bs-roformer-infer.exe");
        var models = Path.Combine(Root, "AudioTools", "roformer-models");
        if (!File.Exists(exe) || !Directory.Exists(models)) return Missing("BS-RoFormer-SW");
        var output = OutputFolder("AI分轨");
        var tempFolder = Path.Combine(Path.GetTempPath(), "Aurora-RoFormer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        var prepared = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(source) + ".wav");
        try
        {
            progress?.Report(new(.05, "正在准备音频素材"));
            await PrepareAudioAsync(source, prepared);
            var info = Hidden(exe, output);
            info.ArgumentList.Add("--input_folder"); info.ArgumentList.Add(tempFolder);
            info.ArgumentList.Add("--store_dir"); info.ArgumentList.Add(output);
            info.ArgumentList.Add("--models_dir"); info.ArgumentList.Add(models);
            if (modelId.Equals("roformer-vocals", StringComparison.OrdinalIgnoreCase))
            {
                info.ArgumentList.Add("--model");
                info.ArgumentList.Add("roformer-model-bs-roformer-vocals-revive-v3e-by-unwa");
            }
            info.Environment["PATH"] = FfmpegRoot + ";" + Environment.GetEnvironmentVariable("PATH");
            return await RunCapturedAsync("utility", info, "separator", output, progress, cancellationToken);
        }
        finally
        {
            if (File.Exists(prepared)) File.Delete(prepared);
            if (Directory.Exists(tempFolder) && !Directory.EnumerateFileSystemEntries(tempFolder).Any()) Directory.Delete(tempFolder);
        }
    }

    private async Task<OperationResult> TranscribeAsync(string source, string modelId, IProgress<TaskExecutionProgress>? progress, CancellationToken cancellationToken)
    {
        if (modelId.Equals("basic-pitch", StringComparison.OrdinalIgnoreCase))
        {
            var basicPitch = Path.Combine(Root, "AudioTools", "basic-pitch-env", "Scripts", "basic-pitch.exe");
            if (!File.Exists(basicPitch)) return Missing("Spotify Basic Pitch");
            var basicPitchOutput = OutputFolder("AI扒谱");
            var basicPitchInfo = Hidden(basicPitch, basicPitchOutput);
            basicPitchInfo.ArgumentList.Add(basicPitchOutput); basicPitchInfo.ArgumentList.Add(source);
            basicPitchInfo.Environment["PYTHONUTF8"] = "1";
            return await RunCapturedAsync("utility", basicPitchInfo, "basic-pitch", basicPitchOutput, progress, cancellationToken);
        }
        if (modelId.Equals("transkun", StringComparison.OrdinalIgnoreCase))
        {
            var transkun = Path.Combine(Root, "AudioTools", "transkun-env", "Scripts", "transkun.exe");
            if (!File.Exists(transkun)) return Missing("TransKun V2");
            var transkunOutput = OutputFolder("AI扒谱");
            var transkunMidi = Path.Combine(transkunOutput, $"{Path.GetFileNameWithoutExtension(source)}-{DateTime.Now:yyyyMMdd-HHmmss}.mid");
            var transkunPython = Path.Combine(Root, "AudioTools", "transkun-env", "Scripts", "python.exe");
            var device = await DetectTorchDeviceAsync(transkunPython, cancellationToken);
            progress?.Report(new(.02, device == "cuda" ? "TransKun 将使用 GPU" : "未检测到可用的 CUDA PyTorch，TransKun 将使用 CPU"));
            var transkunInfo = Hidden(transkun, transkunOutput);
            transkunInfo.ArgumentList.Add(source);
            transkunInfo.ArgumentList.Add(transkunMidi);
            transkunInfo.ArgumentList.Add("--device");
            transkunInfo.ArgumentList.Add(device);
            transkunInfo.Environment["PYTHONUTF8"] = "1";
            return await RunCapturedAsync("utility", transkunInfo, "transkun", transkunOutput, progress, cancellationToken);
        }
        var piano = modelId.Equals("piano", StringComparison.OrdinalIgnoreCase);
        var output = OutputFolder("AI扒谱");
        var midi = Path.Combine(output, $"{Path.GetFileNameWithoutExtension(source)}-{DateTime.Now:yyyyMMdd-HHmmss}.mid");
        ProcessStartInfo info;
        string? prepared = null;
        string? tempFolder = null;
        if (piano)
        {
            var python = Path.Combine(Root, "AudioTools", "piano-env", "Scripts", "python.exe");
            var checkpoint = Path.Combine(Root, "AudioTools", "piano-models", "note_F1=0.9677_pedal_F1=0.9186.pth");
            if (!File.Exists(python) || !File.Exists(checkpoint)) return Missing("ByteDance Piano");
            tempFolder = Path.Combine(Path.GetTempPath(), "Aurora-Piano-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);
            prepared = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(source) + ".wav");
            progress?.Report(new(.05, "正在准备钢琴音频"));
            await PrepareAudioAsync(source, prepared);
            info = Hidden(python, output);
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("from piano_transcription_inference import PianoTranscription,sample_rate; import soundfile as sf,numpy as np,math,sys; from scipy.signal import resample_poly; audio,sr=sf.read(sys.argv[1],dtype='float32'); audio=audio.mean(axis=1) if audio.ndim>1 else audio; g=math.gcd(sr,sample_rate); audio=resample_poly(audio,sample_rate//g,sr//g).astype(np.float32) if sr!=sample_rate else audio; PianoTranscription(device='cuda',checkpoint_path=sys.argv[3]).transcribe(audio,sys.argv[2])");
            info.ArgumentList.Add(prepared); info.ArgumentList.Add(midi); info.ArgumentList.Add(checkpoint);
            info.Environment["NUMBA_DISABLE_JIT"] = "1";
        }
        else
        {
            var exe = Path.Combine(Root, "AudioTools", "mt3-env", "Scripts", "mt3-infer.exe");
            if (!File.Exists(exe)) return Missing("YourMT3+");
            info = Hidden(exe, output);
            info.ArgumentList.Add("transcribe"); info.ArgumentList.Add(source);
            info.ArgumentList.Add("-o"); info.ArgumentList.Add(midi);
            info.ArgumentList.Add("-m"); info.ArgumentList.Add("yourmt3");
            info.ArgumentList.Add("--device"); info.ArgumentList.Add("cuda");
            info.Environment["MT3_CHECKPOINT_DIR"] = Path.Combine(Root, "AudioTools", "mt3-models");
        }
        info.Environment["PYTHONUTF8"] = "1";
        try { return await RunCapturedAsync("utility", info, piano ? "piano" : "yourmt3", output, progress, cancellationToken); }
        finally
        {
            if (prepared is not null && File.Exists(prepared)) File.Delete(prepared);
            if (tempFolder is not null && Directory.Exists(tempFolder) && !Directory.EnumerateFileSystemEntries(tempFolder).Any()) Directory.Delete(tempFolder);
        }
    }

    private async Task<OperationResult> SubtitleAsync(string source, string modelId, string language, IProgress<TaskExecutionProgress>? progress, CancellationToken cancellationToken)
    {
        var exe = Path.Combine(FfmpegRoot, "faster-whisper-xxl.exe");
        if (!File.Exists(exe)) return Missing("Faster-Whisper XXL");
        var output = OutputFolder("AI字幕");
        var modelName = modelId switch
        {
            "whisper-small" => "small",
            "whisper-large-v3-turbo" => "large-v3-turbo",
            "whisper-large-v3" => "large-v3",
            _ => null
        };
        if (modelName is null) return new OperationResult(false, $"不支持的字幕模型：{modelId}。请在视频 AI 字幕中选择可运行的 Whisper 模型。");
        var modelsRoot = Path.Combine(Root, "Faster-Whisper-XXL", "Models");
        var layout = EnsureWhisperModelLayout(modelsRoot, modelName);
        if (!layout.Success) return layout;

        ProcessStartInfo BuildInfo(string device, string computeType)
        {
            var info = Hidden(exe, output);
            info.ArgumentList.Add(source);
            info.ArgumentList.Add("-pp"); info.ArgumentList.Add("-o"); info.ArgumentList.Add(output);
            info.ArgumentList.Add("--standard");
            info.ArgumentList.Add("-f"); info.ArgumentList.Add("json"); info.ArgumentList.Add("srt");
            info.ArgumentList.Add("--model_dir"); info.ArgumentList.Add(modelsRoot);
            info.ArgumentList.Add("-m"); info.ArgumentList.Add(modelName);
            info.ArgumentList.Add("--device"); info.ArgumentList.Add(device);
            info.ArgumentList.Add("--compute_type"); info.ArgumentList.Add(computeType);
            if (language.StartsWith("zh")) { info.ArgumentList.Add("-l"); info.ArgumentList.Add("zh"); }
            else if (language.StartsWith("ja")) { info.ArgumentList.Add("-l"); info.ArgumentList.Add("ja"); }
            info.Environment["HF_HUB_OFFLINE"] = "1";
            info.Environment["PYTHONUTF8"] = "1";
            return info;
        }

        var started = DateTime.UtcNow;
        var gpu = await RunCapturedAsync("utility", BuildInfo("cuda", "float32"), "subtitles-gpu", output, progress, cancellationToken);
        if (gpu.Success && WhisperOutputIsFresh(output, source, started)) return gpu;
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new(.04, "GPU 字幕引擎未生成有效结果，正在自动切换到 CPU"));
        started = DateTime.UtcNow;
        var cpu = await RunCapturedAsync("utility", BuildInfo("cpu", "int8"), "subtitles-cpu", output, progress, cancellationToken);
        if (cpu.Success && WhisperOutputIsFresh(output, source, started)) return new OperationResult(true, "字幕已通过 CPU 回退完成。", output);
        if (!cpu.Success) return cpu;
        return new OperationResult(false, "字幕引擎已退出，但没有生成新的 SRT 与 JSON。请查看 Aurora 日志。", output);
    }

    private static OperationResult EnsureWhisperModelLayout(string modelsRoot, string modelName)
    {
        var canonical = Path.Combine(modelsRoot, "faster-whisper-" + modelName);
        if (File.Exists(Path.Combine(canonical, "model.bin"))) return new OperationResult(true, "Whisper 模型目录已就绪。", canonical);
        var legacy = Path.Combine(modelsRoot, modelName);
        if (!File.Exists(Path.Combine(legacy, "model.bin"))) return Missing("Faster-Whisper " + modelName);
        try
        {
            Directory.Move(legacy, canonical);
            return new OperationResult(true, "Whisper 旧模型目录已兼容迁移。", canonical);
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Whisper 模型目录需要迁移，但操作失败：{ex.Message}。请关闭占用模型文件的程序后重试。");
        }
    }

    private static bool WhisperOutputIsFresh(string output, string source, DateTime started)
    {
        var name = Path.GetFileNameWithoutExtension(source);
        return new[] { ".srt", ".json" }.All(extension =>
        {
            var file = Path.Combine(output, name + extension);
            return File.Exists(file) && File.GetLastWriteTimeUtc(file) >= started.AddSeconds(-2);
        });
    }

    private async Task PrepareAudioAsync(string source, string destination)
    {
        if (Path.GetExtension(source).Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(source, destination, true);
            return;
        }
        var ffmpeg = Path.Combine(FfmpegRoot, "ffmpeg.exe");
        if (!File.Exists(ffmpeg)) throw new FileNotFoundException("FFmpeg was not found.", ffmpeg);
        var info = Hidden(ffmpeg, Path.GetDirectoryName(destination)!);
        info.ArgumentList.Add("-y"); info.ArgumentList.Add("-i"); info.ArgumentList.Add(source);
        info.ArgumentList.Add("-ar"); info.ArgumentList.Add("44100"); info.ArgumentList.Add("-ac"); info.ArgumentList.Add("2"); info.ArgumentList.Add(destination);
        var result = await RunProcessAsync(info);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.Error);
    }

    private static async Task<string> DetectTorchDeviceAsync(string python, CancellationToken cancellationToken)
    {
        if (!File.Exists(python)) return "cpu";
        var info = Hidden(python, Path.GetDirectoryName(python)!);
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("import torch; print('cuda' if torch.cuda.is_available() else 'cpu')");
        using var process = new Process { StartInfo = info };
        try
        {
            process.Start();
            using var cancellation = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            });
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);
            await error;
            return process.ExitCode == 0 && (await output).Trim().Equals("cuda", StringComparison.OrdinalIgnoreCase) ? "cuda" : "cpu";
        }
        catch (OperationCanceledException) { throw; }
        catch { return "cpu"; }
    }

    private async Task<OperationResult> RunCapturedAsync(string key, ProcessStartInfo info, string logPrefix, string output, IProgress<TaskExecutionProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        StatusChanged?.Invoke(this, "running:" + logPrefix);
        var logPath = Path.Combine(Logs, $"{logPrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        using var process = new Process { StartInfo = info };
        processes[key] = process;
        try
        {
            progress?.Report(new(.04, "正在启动本地引擎"));
            process.Start();
            using var cancellation = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
            var lines = new List<string>();
            var sync = new object();
            void Capture(string? line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                lock (sync) lines.Add(line);
                progress?.Report(ParseProgress(line));
            }
            process.OutputDataReceived += (_, args) => Capture(args.Data);
            process.ErrorDataReceived += (_, args) => Capture(args.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);
            await Task.Delay(80, CancellationToken.None);
            lock (sync) File.WriteAllLines(logPath, lines);
            var success = process.ExitCode == 0;
            StatusChanged?.Invoke(this, success ? "completed:" + logPrefix : "failed:" + logPrefix);
            return new OperationResult(success, success ? "Task completed." : $"Task failed with code {process.ExitCode}.", success ? output : logPath);
        }
        finally
        {
            processes.Remove(key);
        }
    }

    private static TaskExecutionProgress ParseProgress(string line)
    {
        var compact = Regex.Replace(line.Trim(), @"\s+", " ");
        if (compact.Length > 220) compact = compact[..220];
        var match = Regex.Match(compact, @"(?<!\d)(?<value>\d{1,3}(?:\.\d+)?)\s*%");
        if (match.Success && double.TryParse(match.Groups["value"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percentage))
            return new(Math.Clamp(percentage / 100d, .04, .99), "正在本机处理", compact);
        return new(null, "正在本机处理", compact);
    }

    public string OutputFolder(string name)
    {
        var path = Path.Combine(settings.Current.OutputRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public int DiagnosticsLogCount() => Directory.Exists(Logs) ? Directory.EnumerateFiles(Logs, "*", SearchOption.AllDirectories).Count() : 0;

    public OperationResult CreateDiagnostics()
    {
        try
        {
            var destination = Path.Combine(settings.Current.OutputRoot, $"Aurora-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            Directory.CreateDirectory(settings.Current.OutputRoot);
            using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
            if (Directory.Exists(Logs))
            {
                foreach (var path in Directory.EnumerateFiles(Logs, "*", SearchOption.AllDirectories))
                {
                    var entry = archive.CreateEntry(Path.GetRelativePath(Logs, path), CompressionLevel.Fastest);
                    using var writer = new StreamWriter(entry.Open());
                    writer.Write(RedactDiagnostics(File.ReadAllText(path)));
                }
            }
            return new OperationResult(true, "diagnosticsExported", destination);
        }
        catch (Exception ex) { return new OperationResult(false, ex.Message); }
    }

    private string RedactDiagnostics(string value)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)] = "%USERPROFILE%",
            [Environment.UserName] = "%USERNAME%",
            [settings.Current.LocalAiRoot] = "%AURORA_MODEL_ROOT%",
            [settings.Current.OutputRoot] = "%AURORA_OUTPUT_ROOT%",
            [settings.Current.ProjectsRoot] = "%AURORA_RECORDS_ROOT%"
        };
        foreach (var item in replacements.Where(x => !string.IsNullOrWhiteSpace(x.Key)).OrderByDescending(x => x.Key.Length))
            value = value.Replace(item.Key, item.Value, StringComparison.OrdinalIgnoreCase);
        return value;
    }

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public void CancelWorkbenchStartup()
    {
        navigationCancellation?.Cancel();
        var key = startingKey;
        if (!string.IsNullOrWhiteSpace(key)) Stop(key);
        StatusChanged?.Invoke(this, "创作引擎启动已取消。");
    }

    public void StopWorkbench(string feature)
    {
        var key = feature switch
        {
            "music" => "music",
            "voice" => "voice",
            "singing" => "singing",
            _ => null
        };
        if (key is not null) Stop(key);
    }

    public void StopAll()
    {
        navigationCancellation?.Cancel();
        foreach (var key in processes.Keys.ToArray()) Stop(key);
        StatusChanged?.Invoke(this, "released");
    }

    private void Stop(string key)
    {
        activeModels.Remove(key);
        if (!processes.Remove(key, out var process)) return;
        try { if (!process.HasExited) process.Kill(true); } catch { }
        try { process.Dispose(); } catch { }
    }

    private bool IsRunning(string key) => processes.TryGetValue(key, out var process) && !process.HasExited;

    private async Task<OperationResult> WaitForUrlAsync(string key, string url, string message)
    {
        navigationCancellation?.Cancel();
        navigationCancellation = new CancellationTokenSource();
        var token = navigationCancellation.Token;
        startingKey = key;
        StatusChanged?.Invoke(this, "loading:" + message);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            for (var i = 0; i < 300 && !token.IsCancellationRequested; i++)
            {
                if (!processes.TryGetValue(key, out var process) || process.HasExited)
                {
                    var exitCode = process is null ? "unknown" : process.ExitCode.ToString();
                    var detail = ReadLogTail(key);
                    return new OperationResult(false, $"本地引擎启动失败（退出码 {exitCode}）。{detail} 请查看 Aurora 日志或在模型管理中执行检查 / 修复。");
                }
                try
                {
                    using var response = await client.GetAsync(url, token);
                    if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect)
                        return new OperationResult(true, "Connected to the local model.", Url: url);
                }
                catch when (!token.IsCancellationRequested) { }
                await Task.Delay(1000, token);
            }
            return token.IsCancellationRequested
                ? new OperationResult(false, "创作引擎启动已取消。")
                : new OperationResult(false, "模型启动等待超过 5 分钟。请查看 Aurora 日志或在模型管理中执行检查 / 修复。");
        }
        catch (OperationCanceledException)
        {
            return new OperationResult(false, "创作引擎启动已取消。");
        }
        finally
        {
            if (startingKey?.Equals(key, StringComparison.OrdinalIgnoreCase) == true) startingKey = null;
        }
    }

    private string ReadLogTail(string key)
    {
        var path = Path.Combine(Logs, key + ".log");
        if (!File.Exists(path)) return "未生成启动日志。";
        try
        {
            var tail = string.Join(" | ", File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(6));
            if (tail.Length > 900) tail = tail[^900..];
            return string.IsNullOrWhiteSpace(tail) ? "启动日志为空。" : "最近日志：" + tail;
        }
        catch { return "无法读取启动日志。"; }
    }

    private Process StartHidden(string key, string fileName, string arguments, string workingDirectory, IReadOnlyDictionary<string, string>? environment = null)
    {
        Directory.CreateDirectory(Logs);
        var logPath = Path.Combine(Logs, key + ".log");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName, Arguments = arguments, WorkingDirectory = workingDirectory,
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };
        if (environment is not null)
            foreach (var item in environment) process.StartInfo.Environment[item.Key] = item.Value;
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(logPath, e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendLog(logPath, e.Data); };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }

    private static ProcessStartInfo Hidden(string fileName, string workingDirectory) => new()
    {
        FileName = fileName, WorkingDirectory = workingDirectory, UseShellExecute = false,
        CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
        RedirectStandardOutput = true, RedirectStandardError = true
    };

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Process could not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
    }

    private static OperationResult Missing(string name) => new(false, $"{name} is not installed. Open Model Management to install it.");
    private void WriteLog(string name, string text) { Directory.CreateDirectory(Logs); File.AppendAllText(Path.Combine(Logs, name), text + Environment.NewLine); }
    private static void AppendLog(string path, string text) { try { File.AppendAllText(path, text + Environment.NewLine); } catch { } }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length; public uint MemoryLoad; public ulong TotalPhysical; public ulong AvailablePhysical;
        public ulong TotalPageFile; public ulong AvailablePageFile; public ulong TotalVirtual; public ulong AvailableVirtual; public ulong AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
    private static bool HasXlLoadHeadroom(out double ramGb, out double commitGb)
    {
        var state = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref state)) { ramGb = commitGb = double.MaxValue; return true; }
        const double gb = 1024d * 1024d * 1024d;
        ramGb = state.AvailablePhysical / gb; commitGb = state.AvailablePageFile / gb;
        return ramGb >= 10 && commitGb >= 20;
    }
}
