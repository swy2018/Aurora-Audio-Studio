namespace AuroraAudioStudio.Services;

public sealed record ReleaseNoteDisplay(string Version, string Date, string Body, bool IsCurrent);

public static class ReleaseNotesCatalog
{
    private sealed record Entry(Version Version, string Date, string[] Bodies);

    private static readonly Entry[] Entries =
    [
        new(new(1, 0, 0), "2026-08-08", [
            "• 首个正式版，整合本地音乐、配音、歌声、分轨、扒谱与字幕工作流。\n• 新增支持断点续传和自动重试的可靠更新下载。\n• 自动更新改用 Aurora 专属进度窗口，后台覆盖完成后才重启新版。\n• 优化 A-wave 图标并使用版本化图标路径，解决桌面与任务栏缓存旧图的问题。\n• 完成保持功能、界面、交互逻辑与输出不变的代码审计和内部精简。",
            "• 首個正式版，整合本機音樂、配音、歌聲、分軌、扒譜與字幕工作流程。\n• 新增支援斷點續傳與自動重試的可靠更新下載。\n• 自動更新改用 Aurora 專屬進度視窗，背景覆蓋完成後才重新啟動新版。\n• 最佳化 A-wave 圖示並使用版本化圖示路徑，解決桌面與工作列快取舊圖的問題。\n• 完成維持功能、介面、互動邏輯與輸出不變的程式碼稽核和內部精簡。",
            "• First stable release, unifying local music, voice, singing, stem, transcription, and subtitle workflows.\n• Added resumable update downloads with automatic retry.\n• Automatic updates now use a dedicated Aurora progress window and relaunch only after background installation succeeds.\n• Refined the A-wave icon and versioned its icon path to eliminate stale desktop and taskbar caches.\n• Audited and simplified internals without changing features, UI, interaction logic, or outputs.",
            "• ローカル音楽、音声、歌声、ステム、採譜、字幕制作を統合した最初の正式版。\n• 再開可能なダウンロードと自動再試行を追加。\n• Aurora 専用の進捗画面でバックグラウンド更新を行い、成功後のみ新版を起動。\n• A-wave アイコンを改善し、バージョン別パスでデスクトップとタスクバーの古いキャッシュを解消。\n• 機能、UI、操作ロジック、出力を変えずに内部コードを監査・簡素化。"]),
        new(new(0, 9, 9), "2026-08-08", [
            "• 修复自动检查与手动检查重叠时可能闪退的问题。\n• 每天首次启动自动检查，也可随时手动检查。\n• 新增全局更新进度，覆盖下载、校验和安装交接；安装完成后自动打开新版。\n• 更新确认、模型操作等弹窗统一排队，避免界面冲突。",
            "• 修正自動檢查與手動檢查重疊時可能閃退的問題。\n• 每天首次啟動自動檢查，也可隨時手動檢查。\n• 新增全域更新進度，涵蓋下載、驗證與安裝交接；完成後自動開啟新版。\n• 更新確認與模型操作等對話框統一排隊，避免介面衝突。",
            "• Fixed a crash when automatic and manual update checks overlapped.\n• Checks automatically on the first launch each day, with manual checks always available.\n• Added global progress for download, verification, and installer handoff; Aurora reopens after installation.\n• Serialized update and model dialogs to prevent UI conflicts.",
            "• 自動確認と手動確認が重なった際のクラッシュを修正。\n• 毎日の初回起動時に自動確認し、手動確認も常時利用可能。\n• ダウンロード、検証、インストール移行を示す全体進捗を追加し、完了後に新版を自動起動。\n• 更新確認とモデル操作のダイアログを順番に表示して競合を防止。"]),
        new(new(0, 9, 8), "2026-08-08", [
            "• 新增三档 Faster-Whisper 多语言字幕模型与更完整的模型信息。\n• 更新结果完整支持简中、繁中、英语和日语。\n• 全面启用新的深色圆角 A-wave 图标。\n• 新项目使用 .arr，旧 .aurora 项目保持兼容。",
            "• 新增三種 Faster-Whisper 多語言字幕模型與更完整的模型資訊。\n• 更新結果完整支援簡中、繁中、英語與日語。\n• 全面啟用新的深色圓角 A-wave 圖示。\n• 新專案使用 .arr，舊 .aurora 專案保持相容。",
            "• Added three Faster-Whisper subtitle models and richer model details.\n• Localized update results across four interface languages.\n• Adopted the new dark rounded A-wave icon throughout the product.\n• New projects use .arr while legacy .aurora files remain compatible.",
            "• Faster-Whisper 字幕モデルを3種類追加し、モデル情報を拡充。\n• 更新結果を4つの表示言語に完全対応。\n• 新しいダーク A-wave アイコンを製品全体に適用。\n• 新規プロジェクトは .arr を使用し、旧 .aurora も引き続き対応。"]),
        new(new(0, 9, 7), "2026-08-04", [
            "• 模型中心新增 Qwen3-TTS、F5-TTS、Demucs 4 与 Basic Pitch。\n• 可选模型改为用户确认后按需部署，默认模型套件保持不变。\n• 首页入口与左侧底部导航重新排版。",
            "• 模型中心新增 Qwen3-TTS、F5-TTS、Demucs 4 與 Basic Pitch。\n• 選用模型改為使用者確認後按需部署，預設模型套件保持不變。\n• 首頁入口與左側底部導覽重新排版。",
            "• Added Qwen3-TTS, F5-TTS, Demucs 4, and Basic Pitch to Model Center.\n• Optional models deploy on demand after confirmation while defaults remain unchanged.\n• Refined home shortcuts and lower navigation.",
            "• モデルセンターに Qwen3-TTS、F5-TTS、Demucs 4、Basic Pitch を追加。\n• オプションモデルは確認後に導入し、標準構成は維持。\n• ホームショートカットと下部ナビゲーションを改善。"]),
        new(new(0, 9, 6), "2026-08-04", [
            "• 默认安装到 Program Files，并采用标准管理员授权。\n• 升级时安全移除旧版应用文件并保留个人设置、项目、模型与成品。\n• 卸载时可选择是否删除个人配置。",
            "• 預設安裝至 Program Files，並採用標準管理員授權。\n• 升級時安全移除舊版程式並保留個人設定、專案、模型與成品。\n• 解除安裝時可選擇是否刪除個人設定。",
            "• Defaulted installation to Program Files with standard administrator consent.\n• Safe upgrades preserve settings, projects, models, and outputs.\n• Uninstall now offers an optional personal-settings cleanup.",
            "• Program Files への標準インストールと管理者承認に対応。\n• 安全な更新で設定、プロジェクト、モデル、出力を保持。\n• アンインストール時に個人設定の削除を選択可能。"]),
        new(new(0, 9, 5), "2026-08-04", [
            "• 新增可迁移项目、首页、最近项目与持久任务中心。\n• 支持任务取消、重试、恢复和结果访问。\n• 新增维护与恢复中心以及完整模型生命周期管理。",
            "• 新增可攜式專案、首頁、最近專案與持久任務中心。\n• 支援任務取消、重試、恢復與結果存取。\n• 新增維護與復原中心以及完整模型生命週期管理。",
            "• Added portable projects, Home, recent projects, and a persistent Task Center.\n• Added cancellation, retry, recovery, and result access.\n• Introduced Maintenance and Recovery plus full model lifecycle management.",
            "• 可搬プロジェクト、ホーム、最近のプロジェクト、永続タスクセンターを追加。\n• キャンセル、再試行、復旧、結果アクセスに対応。\n• メンテナンスと復旧、モデルライフサイクル管理を追加。"]),
        new(new(0, 7, 0), "2026-08-03", [
            "• 使用 .NET 10、WinUI 3 与 Windows App SDK 重构原生桌面工作台。\n• 将本地 AI 工作台直接集成进 Aurora。\n• 建立四语言、标准安装卸载、模型管理与 GitHub 更新基础。",
            "• 使用 .NET 10、WinUI 3 與 Windows App SDK 重構原生桌面工作台。\n• 將本機 AI 工作台直接整合進 Aurora。\n• 建立四語言、標準安裝移除、模型管理與 GitHub 更新基礎。",
            "• Rebuilt the native desktop workbench with .NET 10, WinUI 3, and Windows App SDK.\n• Embedded local AI workbenches directly in Aurora.\n• Established four-language UI, standard setup, model management, and GitHub updates.",
            "• .NET 10、WinUI 3、Windows App SDK でネイティブデスクトップを再構築。\n• ローカル AI ワークベンチを Aurora に統合。\n• 4言語、標準セットアップ、モデル管理、GitHub 更新の基盤を追加。"])
    ];

    public static IReadOnlyList<ReleaseNoteDisplay> CurrentAndRecent(string currentVersion, string language, int count = 5)
    {
        if (!Version.TryParse(currentVersion, out var current)) current = Entries[0].Version;
        var languageIndex = language switch { "zh-TW" => 1, "en-US" => 2, "ja-JP" => 3, _ => 0 };
        return Entries.Where(x => x.Version <= current).OrderByDescending(x => x.Version).Take(count)
            .Select((x, index) => new ReleaseNoteDisplay(x.Version.ToString(3), x.Date, x.Bodies[languageIndex], index == 0)).ToList();
    }
}
