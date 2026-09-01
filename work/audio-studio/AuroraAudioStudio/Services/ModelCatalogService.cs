using AuroraAudioStudio.Models;

namespace AuroraAudioStudio.Services;

public sealed class ModelCatalogService(SettingsService settings)
{
    public IReadOnlyList<ModelDefinition> Definitions { get; } =
    [
        new("ace-step", "ACE-Step 1.5 XL Turbo", "music", "ACE-Step-1.5", @"acestep\acestep_v15_pipeline.py", "GitHub Release + Hugging Face", "github-release-git", "https://github.com/ACE-Step/ACE-Step-1.5.git", true),
        new("minimax-music3", "MiniMax-Music3", "music", "MiniMax-Music3", "modular_model_index.json", "MiniMax · Hugging Face", "minimax-music3", "MiniMaxAI/MiniMax-Music3"),
        new("heartmula-3b", "HeartMuLa 3B · Happy New Year", "music", @"AudioTools\heartmula-models\HeartMuLa-oss-3B-happy-new-year", "model.safetensors.index.json", "HeartMuLa · Hugging Face", "huggingface", "HeartMuLa/HeartMuLa-oss-3B-happy-new-year", false, false),
        new("qwen3-tts-base", "Qwen3-TTS 1.7B · 声音克隆", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-1.7B-Base", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-1.7B-Base", true),
        new("qwen3-tts-custom", "Qwen3-TTS 1.7B · 专业音色", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-1.7B-CustomVoice", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-1.7B-CustomVoice", true),
        new("qwen3-tts-design", "Qwen3-TTS 1.7B · 音色设计", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-1.7B-VoiceDesign", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign", true),
        new("qwen3-tts-06b-base", "Qwen3-TTS 0.6B · 轻量声音克隆", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-Base", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-0.6B-Base"),
        new("qwen3-tts-06b-custom", "Qwen3-TTS 0.6B · 轻量专业音色", "voice", @"Qwen3-TTS\models\Qwen3-TTS-12Hz-0.6B-CustomVoice", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice"),
        new("f5-tts", "F5-TTS · 多语言声音克隆", "voice", @"AudioTools\f5-tts-env", @"Scripts\f5-tts_infer-gradio.exe", "SWivid · PyPI", "uv-package", "f5-tts"),
        new("indextts-2-5", "IndexTTS-2.5 · 可控配音", "voice", @"AudioTools\indextts-models\IndexTTS-2.5", "config.yaml", "Index Team · Hugging Face", "huggingface", "IndexTeam/IndexTTS-2.5", false, false),
        new("seed-vc", "Seed-VC 44.1k", "singing", "Seed-VC", "app_svc_local.py", "GitHub Release + Hugging Face", "github-release-git", "https://github.com/Plachtaa/seed-vc.git", true),
        new("soulx-singer-svc", "SoulX-Singer-SVC · 零样本歌声转换", "singing", @"AudioTools\soulx-singer-models\SoulX-Singer-SVC", "model-svc.pt", "Soul AI Lab · Hugging Face", "huggingface", "Soul-AILab/SoulX-Singer", false, false),
        new("roformer", "BS-RoFormer-SW · 多轨高质量", "separation", @"AudioTools\roformer-env", @"Scripts\bs-roformer-infer.exe", "PyPI model registry", "uv-package", "bs-roformer-infer", true),
        new("roformer-vocals", "BS-RoFormer Vocals Revive V3e · 二轨", "separation", @"AudioTools\roformer-models\roformer-model-bs-roformer-vocals-revive-v3e-by-unwa", "bs_roformer_vocals_revive_v3e_unwa.ckpt", "BS-RoFormer model registry", "roformer-registry", "roformer-model-bs-roformer-vocals-revive-v3e-by-unwa", true),
        new("demucs", "Demucs 4 · 通用四轨分离", "separation", @"AudioTools\demucs-env", @"Scripts\demucs.exe", "Meta Research · PyPI", "uv-package", "demucs"),
        new("yourmt3", "YourMT3+ Multi-Instrument", "transcription", @"AudioTools\mt3-env", @"Scripts\mt3-infer.exe", "PyPI model registry", "uv-package", "mt3-infer"),
        new("transkun", "TransKun V2 · 钢琴扒谱", "transcription", @"AudioTools\transkun-env", @"Scripts\transkun.exe", "TransKun · PyPI", "uv-package", "transkun", true),
        new("piano", "ByteDance Piano · 经典模型", "transcription", @"AudioTools\piano-models", "note_F1=0.9677_pedal_F1=0.9186.pth", "Zenodo", "fixed-file", "https://zenodo.org/records/4034264/files/CRNN_note_F1%3D0.9677_pedal_F1%3D0.9186.pth?download=1"),
        new("basic-pitch", "Spotify Basic Pitch · 轻量扒谱", "transcription", @"AudioTools\basic-pitch-env", @"Scripts\basic-pitch.exe", "Spotify · PyPI", "uv-package", "basic-pitch"),
        new("faster-whisper", "Faster-Whisper XXL", "subtitles", @"Faster-Whisper-XXL\Faster-Whisper-XXL", "faster-whisper-xxl.exe", "GitHub Release", "github-release", "https://github.com/Purfview/whisper-standalone-win.git", true),
        new("whisper-small", "Faster-Whisper Small", "subtitles", @"Faster-Whisper-XXL\Models\small", "model.bin", "SYSTRAN · Hugging Face", "huggingface", "Systran/faster-whisper-small"),
        new("whisper-large-v3-turbo", "Faster-Whisper Large v3 Turbo", "subtitles", @"Faster-Whisper-XXL\Models\large-v3-turbo", "model.bin", "Mobius Labs · Hugging Face", "huggingface", "mobiuslabsgmbh/faster-whisper-large-v3-turbo"),
        new("whisper-large-v3", "Faster-Whisper Large v3", "subtitles", @"Faster-Whisper-XXL\Models\large-v3", "model.bin", "SYSTRAN · Hugging Face", "huggingface", "Systran/faster-whisper-large-v3"),
        new("qwen3-asr-06b", "Qwen3-ASR 0.6B · 快速识别", "subtitles", @"AudioTools\qwen3-asr-models\Qwen3-ASR-0.6B-hf", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-ASR-0.6B-hf", false, false),
        new("qwen3-asr-17b", "Qwen3-ASR 1.7B · 高质量识别", "subtitles", @"AudioTools\qwen3-asr-models\Qwen3-ASR-1.7B-hf", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-ASR-1.7B-hf", false, false),
        new("qwen3-forced-aligner", "Qwen3 ForcedAligner 0.6B · 精确时间轴", "subtitles", @"AudioTools\qwen3-asr-models\Qwen3-ForcedAligner-0.6B-hf", "model.safetensors", "Qwen · Hugging Face", "huggingface", "Qwen/Qwen3-ForcedAligner-0.6B-hf", false, false),
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
            RecommendedVram(model), FeatureDisplay(model.Feature), Purpose(model.Id), Languages(model.Id), ModelInstallPlanner.EstimatedDownload(model.Id), License(model.Id),
            DetailLine(version, RecommendedVram(model), ModelInstallPlanner.EstimatedDownload(model.Id), License(model.Id), model.Source),
            model.IsDefault ? DefaultEditionDisplay : Pick("可选模型", "選用模型", "Optional", "オプション"),
            installed ? Pick("检查 / 修复", "檢查 / 修復", "Check / Repair", "確認 / 修復") : Pick("安装", "安裝", "Install", "インストール"),
            Pick("回退", "回復", "Roll back", "ロールバック"), Pick("卸载", "解除安裝", "Uninstall", "アンインストール"));
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
        foreach (var marker in new[] { ".aurora-version", ".aurora-revision", "version.txt" })
        {
            var file = Path.Combine(path, marker);
            if (File.Exists(file)) return File.ReadAllText(file).Trim();
        }
        var target = Path.Combine(path, model.Marker);
        return File.Exists(target) ? File.GetLastWriteTime(target).ToString("yyyy.MM.dd") : "—";
    }

    private static string RecommendedVram(ModelDefinition model) => model.Id switch
    {
        "ace-step" => "12 GB+",
        "minimax-music3" => "8 GB+ · 16 GB 推荐",
        "heartmula-3b" => "12 GB+ · 16 GB 推荐",
        "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" => "8 GB+",
        "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "4 GB+",
        "f5-tts" => "6 GB+",
        "indextts-2-5" => "8 GB+",
        "seed-vc" => "8 GB+",
        "soulx-singer-svc" => "8 GB+",
        "roformer" or "roformer-vocals" => "8 GB+",
        "demucs" => "4 GB+",
        "faster-whisper" => "6 GB+",
        "whisper-small" => "2 GB+",
        "whisper-large-v3-turbo" => "6 GB+",
        "whisper-large-v3" => "10 GB+",
        "qwen3-asr-06b" or "qwen3-forced-aligner" => "4 GB+",
        "qwen3-asr-17b" => "6 GB+",
        _ => "4 GB+"
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
        "minimax-music3" => Pick("最长五分钟的完整歌曲与纯音乐生成", "最長五分鐘的完整歌曲與純音樂生成", "Full songs and instrumentals up to five minutes", "最長5分の楽曲・インスト生成"),
        "heartmula-3b" => Pick("多语言歌词驱动的完整歌曲生成", "多語言歌詞驅動的完整歌曲生成", "Full-song generation from multilingual lyrics", "多言語歌詞からのフル楽曲生成"),
        "qwen3-tts-base" => Pick("参考音频声音克隆", "參考音訊聲音複製", "Reference-audio voice cloning", "参照音声からのクローン"),
        "qwen3-tts-custom" => Pick("稳定的预设专业音色", "穩定的預設專業音色", "Consistent professional voices", "安定したプロ音声"),
        "qwen3-tts-design" => Pick("用文字设计新音色", "以文字設計新音色", "Design voices from text", "テキストから声を設計"),
        "qwen3-tts-06b-base" => Pick("低显存声音克隆", "低顯示記憶體聲音複製", "Low-VRAM voice cloning", "省 VRAM ボイスクローン"),
        "qwen3-tts-06b-custom" => Pick("低显存预设音色", "低顯示記憶體預設音色", "Low-VRAM preset voices", "省 VRAM プリセット音声"),
        "f5-tts" => Pick("多语言参考音频克隆", "多語言參考音訊複製", "Multilingual reference-audio cloning", "多言語ボイスクローン"),
        "indextts-2-5" => Pick("情绪、语速与发音可控的配音", "情緒、語速與發音可控的配音", "Voice cloning with emotion, speed, and pronunciation control", "感情・速度・発音を制御できる音声生成"),
        "seed-vc" => Pick("44.1 kHz 歌声与音色转换", "44.1 kHz 歌聲與音色轉換", "44.1 kHz singing and timbre conversion", "44.1 kHz 歌声・音色変換"),
        "soulx-singer-svc" => Pick("无需歌词或 MIDI 的离线歌声音色转换", "無需歌詞或 MIDI 的離線歌聲音色轉換", "Offline singing conversion without lyrics or MIDI", "歌詞や MIDI を使わないオフライン歌声変換"),
        "roformer" => Pick("精细六轨分离", "精細六軌分離", "Detailed six-stem separation", "高精度 6 ステム分離"),
        "roformer-vocals" => Pick("纯人声与纯伴奏二轨分离", "純人聲與純伴奏二軌分離", "Two-stem vocals and instrumental separation", "ボーカル・伴奏の 2 ステム分離"),
        "demucs" => Pick("通用快速四轨分离", "通用快速四軌分離", "General fast four-stem separation", "汎用高速 4 ステム分離"),
        "yourmt3" => Pick("多乐器 MIDI 转写", "多樂器 MIDI 轉寫", "Multi-instrument MIDI transcription", "複数楽器の MIDI 採譜"),
        "transkun" => Pick("默认高精度钢琴 MIDI 转写", "預設高精度鋼琴 MIDI 轉寫", "Default high-accuracy piano MIDI transcription", "標準の高精度ピアノ MIDI 採譜"),
        "piano" => Pick("高精度钢琴 MIDI 与踏板", "高精度鋼琴 MIDI 與踏板", "Detailed piano MIDI with pedals", "高精度ピアノ MIDI・ペダル"),
        "basic-pitch" => Pick("轻量快速旋律扒谱", "輕量快速旋律扒譜", "Lightweight melodic transcription", "軽量なメロディ採譜"),
        "whisper-small" => Pick("低占用快速多语言字幕", "低佔用快速多語言字幕", "Fast multilingual subtitles with low resource use", "軽量な多言語字幕"),
        "whisper-large-v3-turbo" => Pick("速度与准确率均衡的多语言字幕", "速度與準確率均衡的多語言字幕", "Balanced multilingual speed and accuracy", "速度と精度を両立した多言語字幕"),
        "whisper-large-v3" => Pick("优先准确率的多语言字幕", "優先準確率的多語言字幕", "Accuracy-first multilingual subtitles", "精度優先の多言語字幕"),
        "qwen3-asr-06b" => Pick("快速中文、多方言与多语言识别", "快速中文、多方言與多語言識別", "Fast Chinese, dialect, and multilingual recognition", "高速な中国語・方言・多言語認識"),
        "qwen3-asr-17b" => Pick("高准确率中文、多方言与歌曲识别", "高準確率中文、多方言與歌曲識別", "High-accuracy Chinese, dialect, and song recognition", "高精度な中国語・方言・歌唱認識"),
        "qwen3-forced-aligner" => Pick("为字幕生成词级精确时间轴", "為字幕生成詞級精確時間軸", "Word-level forced alignment for subtitles", "字幕向け単語レベル強制アライメント"),
        "subtitle-edit" => Pick("字幕校对、时间轴与导出", "字幕校對、時間軸與匯出", "Subtitle review, timing, and export", "字幕校正・タイミング・書き出し"),
        _ => Pick("本地语音转写运行引擎", "本機語音轉寫執行引擎", "Local speech transcription runtime", "ローカル音声認識ランタイム")
    };

    private string Languages(string id) => id switch
    {
        "minimax-music3" or "heartmula-3b" or "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" or "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" => "中文 · English · 日本語 · 多语言",
        "f5-tts" => Pick("中文 · 英语 · 日语 · 多语言", "中文 · 英語 · 日語 · 多語言", "Chinese · English · Japanese · multilingual", "中国語 · 英語 · 日本語 · 多言語"),
        "indextts-2-5" => Pick("中文 · 英语 · 日语 · 西班牙语 · 阿拉伯语", "中文 · 英語 · 日語 · 西班牙語 · 阿拉伯語", "Chinese · English · Japanese · Spanish · Arabic", "中国語 · 英語 · 日本語 · スペイン語 · アラビア語"),
        "qwen3-asr-06b" or "qwen3-asr-17b" => Pick("30 种语言 · 22 种中文方言", "30 種語言 · 22 種中文方言", "30 languages · 22 Chinese dialects", "30 言語 · 中国語 22 方言"),
        "qwen3-forced-aligner" => Pick("中文 · 英语 · 日语 · 11 种语言", "中文 · 英語 · 日語 · 11 種語言", "Chinese · English · Japanese · 11 languages", "中国語 · 英語 · 日本語 · 11 言語"),
        "faster-whisper" or "whisper-small" or "whisper-large-v3-turbo" or "whisper-large-v3" => Pick("中文 · 英语 · 日语 · 约 100 种语言", "中文 · 英語 · 日語 · 約 100 種語言", "Chinese · English · Japanese · about 100 languages", "中国語 · 英語 · 日本語 · 約 100 言語"),
        _ => Pick("不依赖文本语言", "不依賴文字語言", "Language-independent", "言語非依存")
    };

    private static string License(string id) => id switch
    {
        "ace-step" or "heartmula-3b" or "soulx-singer-svc" or "qwen3-asr-06b" or "qwen3-asr-17b" or "qwen3-forced-aligner" or "qwen3-tts-base" or "qwen3-tts-custom" or "qwen3-tts-design" or "qwen3-tts-06b-base" or "qwen3-tts-06b-custom" or "basic-pitch" => "Apache-2.0",
        "indextts-2-5" => "Bilibili Model License",
        "f5-tts" or "demucs" or "faster-whisper" or "whisper-small" or "whisper-large-v3-turbo" or "whisper-large-v3" or "transkun" => "MIT",
        "minimax-music3" => "MiniMax-Music3 Community License",
        "subtitle-edit" => "GPL-3.0",
        _ => "上游许可"
    };

    private string Pick(string zh, string zhTw, string en, string ja) => settings.EffectiveLanguage() switch
    {
        "zh-TW" => zhTw,
        "en-US" => en,
        "ja-JP" => ja,
        _ => zh
    };

    private string DetailLine(string version, string vram, string download, string license, string source) => settings.EffectiveLanguage() switch
    {
        "zh-TW" => $"版本 {version}  ·  顯示記憶體 {vram}  ·  下載 {download}  ·  授權 {license}  ·  來源 {source}",
        "en-US" => $"Version {version}  ·  VRAM {vram}  ·  Download {download}  ·  License {license}  ·  Source {source}",
        "ja-JP" => $"バージョン {version}  ·  VRAM {vram}  ·  ダウンロード {download}  ·  ライセンス {license}  ·  提供元 {source}",
        _ => $"版本 {version}  ·  显存 {vram}  ·  下载 {download}  ·  许可 {license}  ·  来源 {source}"
    };
}
