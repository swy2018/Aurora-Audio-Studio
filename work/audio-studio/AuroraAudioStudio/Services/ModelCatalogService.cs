using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class ModelCatalogService(SettingsService settings)
{
    public IReadOnlyList<ModelDefinition> Definitions { get; } =
    [
        new("ace-step", "ACE-Step 1.5 XL Turbo", "music", "ACE-Step-1.5", @"acestep\acestep_v15_pipeline.py", "GitHub + Hugging Face", "git-hf", "https://github.com/ACE-Step/ACE-Step-1.5.git", true),
        new("qwen3-tts-base", "Qwen3-TTS 1.7B · 声音克隆", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-1.7B-Base", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-1.7B-Base", true),
        new("qwen3-tts-custom", "Qwen3-TTS 1.7B · 专业音色", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-1.7B-CustomVoice", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice", true),
        new("qwen3-tts-design", "Qwen3-TTS 1.7B · 音色设计", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-1.7B-VoiceDesign", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign", true),
        new("qwen3-tts-06b-base", "Qwen3-TTS 0.6B · 轻量声音克隆", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-Base", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-0.6B-Base"),
        new("qwen3-tts-06b-custom", "Qwen3-TTS 0.6B · 轻量专业音色", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-CustomVoice", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice"),
        new("f5-tts", "F5-TTS · 多语言声音克隆", "voice", @"AudioTools\f5-tts-env", @"Scripts\f5-tts_infer-gradio.exe", "SWivid · PyPI", "uv-package", "f5-tts"),
        new("seed-vc", "Seed-VC 44.1k", "singing", "Seed-VC", "app_svc_local.py", "GitHub + Hugging Face", "git-hf", "https://github.com/Plachtaa/seed-vc.git", true),
        new("roformer", "BS-RoFormer-SW 6-Stem", "separation", @"AudioTools\roformer-env", @"Scripts\bs-roformer-infer.exe", "PyPI model registry", "python-tool", null, true),
        new("demucs", "Demucs 4 · 通用四轨分离", "separation", @"AudioTools\demucs-env", @"Scripts\demucs.exe", "Meta Research · PyPI", "uv-package", "demucs"),
        new("yourmt3", "YourMT3+ Multi-Instrument", "transcription", @"AudioTools\mt3-env", @"Scripts\mt3-infer.exe", "PyPI model registry", "python-tool", null, true),
        new("piano", "ByteDance Piano", "transcription", @"AudioTools\piano-models", "note_F1=0.9677_pedal_F1=0.9186.pth", "Zenodo", "direct", null, true),
        new("basic-pitch", "Spotify Basic Pitch · 轻量扒谱", "transcription", @"AudioTools\basic-pitch-env", @"Scripts\basic-pitch.exe", "Spotify · PyPI", "uv-package", "basic-pitch"),
        new("faster-whisper", "Faster-Whisper XXL", "subtitles", @"Faster-Whisper-XXL\Faster-Whisper-XXL", "faster-whisper-xxl.exe", "GitHub Release", "github-release", "https://github.com/Purfview/whisper-standalone-win.git", true),
        new("whisper-small", "Faster-Whisper Small", "subtitles", @"Faster-Whisper-XXL\Models\small", "model.bin", "SYSTRAN · Hugging Face", "huggingface", "Systran/faster-whisper-small"),
        new("whisper-large-v3-turbo", "Faster-Whisper Large v3 Turbo", "subtitles", @"Faster-Whisper-XXL\Models\large-v3-turbo", "model.bin", "Mobius Labs · Hugging Face", "huggingface", "mobiuslabsgmbh/faster-whisper-large-v3-turbo"),
        new("whisper-large-v3", "Faster-Whisper Large v3", "subtitles", @"Faster-Whisper-XXL\Models\large-v3", "model.bin", "SYSTRAN · Hugging Face", "huggingface", "Systran/faster-whisper-large-v3"),
        new("subtitle-edit", "Subtitle Edit", "subtitles", "SubtitleEdit", "SubtitleEdit.exe", "GitHub Release", "github-release", "https://github.com/SubtitleEdit/subtitleedit.git", true)
    ];

    public string DefaultEditionDisplay => Pick("默认组件", "預設元件", "Default", "標準");

    public IReadOnlyList<ModelState> GetStates() => Definitions.Select(ToState).ToList();
    public IReadOnlyList<ModelState> GetDefaultStates() => Definitions.Where(x => x.IsDefault).Select(ToState).ToList();
    public ModelDefinition? Find(string id) => Definitions.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public bool IsInstalled(ModelDefinition model)
    {
        var root = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        return File.Exists(Path.Combine(root, model.Marker));
    }

    private ModelState ToState(ModelDefinition model)
    {
        var path = Path.Combine(settings.Current.LocalAiRoot, model.RelativeRoot);
        var installed = IsInstalled(model);
        var version = ReadVersion(path, model);
        return new ModelState(model.Id, model.Name, model.Feature, installed,
            installed ? Pick("可用", "可用", "Ready", "利用可能") : Pick("未安装", "未安裝", "Not installed", "未インストール"),
            model.Source, path, version,
            installed ? Pick("完整性检查通过", "完整性檢查通過", "Integrity check passed", "整合性チェック済み")
                : model.IsDefault ? Pick("默认配置 · 需要安装或修复", "預設配置 · 需要安裝或修復", "Default component · install or repair required", "標準コンポーネント · インストールまたは修復が必要")
                : Pick("可选模型 · 仅在确认后下载", "選用模型 · 僅在確認後下載", "Optional model · downloads only after confirmation", "オプションモデル · 確認後にのみダウンロード"),
            RecommendedVram(model), FeatureDisplay(model.Feature), Purpose(model.Id), Languages(model.Id), EstimatedDownload(model.Id), License(model.Id),
            DetailLine(version, RecommendedVram(model), EstimatedDownload(model.Id), License(model.Id), model.Source),
            model.IsDefault ? DefaultEditionDisplay : Pick("可选模型", "選用模型", "Optional", "オプション"),
            installed ? Pick("检查 / 修复", "檢查 / 修復", "Check / Repair", "確認 / 修復") : Pick("安装", "安裝", "Install", "インストール"));
    }

    public string FormatSummary(IReadOnlyList<ModelState> states)
    {
        var installed = states.Count(x => x.Installed);
        var optional = Definitions.Count(x => !x.IsDefault);
        return settings.EffectiveLanguage() switch
        {
            "zh-TW" => $"{states.Count} 個元件 · {installed} 個已就緒 · {optional} 個可選模型",
            "en-US" => $"{states.Count} components · {installed} ready · {optional} optional models",
            "ja-JP" => $"{states.Count} コンポーネント · {installed} 利用可能 · {optional} オプションモデル",
            _ => $"{states.Count} 个组件 · {installed} 个已就绪 · {optional} 个可选模型"
        };
    }

    private static string ReadVersion(string path, ModelDefinition model)
    {
        foreach (var marker in new[] { ".aurora-revision", ".aurora-version", "version.txt" })
        {
            var file = Path.Combine(path, marker);
            if (File.Exists(file)) return File.ReadAllText(file).Trim();
        }
        var target = Path.Combine(path, model.Marker);
        return File.Exists(target) ? File.GetLastWriteTime(target).ToString("yyyy.MM.dd") : "—";
    }

    private static string RecommendedVram(ModelDefinition model) => model.Id switch
    {
        "ace-step" => "12 GB+", "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => "8 GB+",
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "4 GB+", "f5-tts" => "6 GB+",
        "seed-vc" => "8 GB+", "roformer" => "8 GB+", "demucs" => "4 GB+", "faster-whisper" => "6 GB+",
        "whisper-small" => "2 GB+", "whisper-large-v3-turbo" => "6 GB+", "whisper-large-v3" => "10 GB+", _ => "4 GB+"
    };

    private string FeatureDisplay(string feature) => feature switch
    {
        "music" => Pick("音乐生成", "音樂生成", "Music generation", "音楽生成"),
        "voice" => Pick("配音与声音克隆", "配音與聲音複製", "Voice and cloning", "音声・ボイスクローン"),
        "singing" => Pick("歌声转换", "歌聲轉換", "Singing conversion", "歌声変換"),
        "separation" => Pick("音轨分离", "音軌分離", "Stem separation", "音源分離"),
        "transcription" => Pick("MIDI 扒谱", "MIDI 扒譜", "MIDI transcription", "MIDI 採譜"),
        _ => Pick("字幕与转写", "字幕與轉寫", "Subtitles and transcription", "字幕・文字起こし")
    };

    private string Purpose(string id) => id switch
    {
        "ace-step" => Pick("完整歌曲与纯音乐生成", "完整歌曲與純音樂生成", "Full songs and instrumental generation", "楽曲・インスト生成"),
        "qwen3-tts-base" => Pick("参考音频声音克隆", "參考音訊聲音複製", "Reference-audio voice cloning", "参照音声からのクローン"),
        "qwen3-tts-custom" => Pick("稳定的预设专业音色", "穩定的預設專業音色", "Consistent professional voices", "安定したプロ音声"),
        "qwen3-tts-design" => Pick("用文字设计新音色", "以文字設計新音色", "Design voices from text", "テキストから声を設計"),
        "qwen3-tts-06b-base" => Pick("低显存声音克隆", "低顯示記憶體聲音複製", "Low-VRAM voice cloning", "省 VRAM ボイスクローン"),
        "qwen3-tts-06b-custom" => Pick("低显存预设音色", "低顯示記憶體預設音色", "Low-VRAM preset voices", "省 VRAM プリセット音声"),
        "f5-tts" => Pick("多语言参考音频克隆", "多語言參考音訊複製", "Multilingual reference-audio cloning", "多言語ボイスクローン"),
        "seed-vc" => Pick("44.1 kHz 歌声与音色转换", "44.1 kHz 歌聲與音色轉換", "44.1 kHz singing and timbre conversion", "44.1 kHz 歌声・音色変換"),
        "roformer" => Pick("精细六轨分离", "精細六軌分離", "Detailed six-stem separation", "高精度 6 ステム分離"),
        "demucs" => Pick("通用快速四轨分离", "通用快速四軌分離", "General fast four-stem separation", "汎用高速 4 ステム分離"),
        "yourmt3" => Pick("多乐器 MIDI 转写", "多樂器 MIDI 轉寫", "Multi-instrument MIDI transcription", "複数楽器の MIDI 採譜"),
        "piano" => Pick("高精度钢琴 MIDI 与踏板", "高精度鋼琴 MIDI 與踏板", "Detailed piano MIDI with pedals", "高精度ピアノ MIDI・ペダル"),
        "basic-pitch" => Pick("轻量快速旋律扒谱", "輕量快速旋律扒譜", "Lightweight melodic transcription", "軽量なメロディ採譜"),
        "whisper-small" => Pick("低占用快速多语言字幕", "低佔用快速多語言字幕", "Fast multilingual subtitles with low resource use", "軽量な多言語字幕"),
        "whisper-large-v3-turbo" => Pick("速度与准确率均衡的多语言字幕", "速度與準確率均衡的多語言字幕", "Balanced multilingual speed and accuracy", "速度と精度を両立した多言語字幕"),
        "whisper-large-v3" => Pick("优先准确率的多语言字幕", "優先準確率的多語言字幕", "Accuracy-first multilingual subtitles", "精度優先の多言語字幕"),
        "subtitle-edit" => Pick("字幕校对、时间轴与导出", "字幕校對、時間軸與匯出", "Subtitle review, timing, and export", "字幕校正・タイミング・書き出し"),
        _ => Pick("本地语音转写运行引擎", "本機語音轉寫執行引擎", "Local speech transcription runtime", "ローカル音声認識ランタイム")
    };

    private string Languages(string id) => id switch
    {
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" or "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "中文 · English · 日本語 · 多语言",
        "f5-tts" => Pick("中文 · 英语 · 日语 · 多语言", "中文 · 英語 · 日語 · 多語言", "Chinese · English · Japanese · multilingual", "中国語 · 英語 · 日本語 · 多言語"),
        "faster-whisper" or "whisper-small" or "whisper-large-v3-turbo" or "whisper-large-v3" => Pick("中文 · 英语 · 日语 · 约 100 种语言", "中文 · 英語 · 日語 · 約 100 種語言", "Chinese · English · Japanese · about 100 languages", "中国語 · 英語 · 日本語 · 約 100 言語"),
        _ => Pick("不依赖文本语言", "不依賴文字語言", "Language-independent", "言語非依存")
    };

    private static string EstimatedDownload(string id) => id switch
    {
        "whisper-small" => "≈ 470 MB", "whisper-large-v3-turbo" => "≈ 1.6 GB", "whisper-large-v3" => "≈ 3.1 GB",
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => "≈ 4 GB", "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "≈ 1.5 GB",
        "ace-step" => "≈ 8 GB", "f5-tts" or "demucs" or "basic-pitch" => "< 1 GB", _ => "—"
    };

    private static string License(string id) => id switch
    {
        "ace-step" or "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" or "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" or "basic-pitch" => "Apache-2.0",
        "f5-tts" or "demucs" or "faster-whisper" or "whisper-small" or "whisper-large-v3-turbo" or "whisper-large-v3" => "MIT",
        "subtitle-edit" => "GPL-3.0",
        _ => "上游许可"
    };

    private string Pick(string zh, string zhTw, string en, string ja) => settings.EffectiveLanguage() switch
    {
        "zh-TW" => zhTw, "en-US" => en, "ja-JP" => ja, _ => zh
    };

    private string DetailLine(string version, string vram, string download, string license, string source) => settings.EffectiveLanguage() switch
    {
        "zh-TW" => $"版本 {version}  ·  顯示記憶體 {vram}  ·  下載 {download}  ·  授權 {license}  ·  來源 {source}",
        "en-US" => $"Version {version}  ·  VRAM {vram}  ·  Download {download}  ·  License {license}  ·  Source {source}",
        "ja-JP" => $"バージョン {version}  ·  VRAM {vram}  ·  ダウンロード {download}  ·  ライセンス {license}  ·  提供元 {source}",
        _ => $"版本 {version}  ·  显存 {vram}  ·  下载 {download}  ·  许可 {license}  ·  来源 {source}"
    };
}
