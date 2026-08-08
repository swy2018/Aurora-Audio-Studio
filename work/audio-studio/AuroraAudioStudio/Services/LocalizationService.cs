namespace AuroraAudioStudio.Services;

public sealed class LocalizationService(SettingsService settings)
{
    private readonly Dictionary<string, string[]> values = new(StringComparer.OrdinalIgnoreCase)
    {
        ["home"] = ["首页", "首頁", "Home", "ホーム"],
        ["homeSubtitle"] = ["继续项目，或从一个清晰的创作任务开始。", "繼續專案，或從一個清楚的創作任務開始。", "Continue a project or start with a focused creative task.", "プロジェクトを続けるか、明確な制作タスクから始めます。"],
        ["tasks"] = ["任务中心", "任務中心", "Task Center", "タスクセンター"],
        ["tasksSubtitle"] = ["查看排队、处理中、已完成与可恢复的任务。", "查看排隊、處理中、已完成與可恢復的任務。", "Review queued, running, completed, and recoverable tasks.", "待機中、処理中、完了、復旧可能なタスクを確認します。"],
        ["music"] = ["音乐创作", "音樂創作", "Music", "音楽制作"],
        ["voice"] = ["AI配音与声音克隆", "AI配音與聲音複製", "AI Voice", "AI音声"],
        ["singing"] = ["歌声克隆", "歌聲複製", "Singing Voice", "歌声変換"],
        ["separation"] = ["去人声 / AI分轨", "去人聲 / AI分軌", "Stem Separation", "音源分離"],
        ["transcription"] = ["AI扒谱（MIDI）", "AI扒譜（MIDI）", "AI Transcription", "AI採譜（MIDI）"],
        ["subtitles"] = ["视频 AI 字幕", "影片 AI 字幕", "Video Subtitles", "動画 AI 字幕"],
        ["settings"] = ["设置", "設定", "Settings", "設定"],
        ["about"] = ["关于", "關於", "About", "このアプリについて"],
        ["release"] = ["结束当前引擎", "結束目前引擎", "Stop current engine", "現在のエンジンを終了"],
        ["output"] = ["我的成品", "我的成品", "My creations", "マイ作品"],
        ["update"] = ["检查更新", "檢查更新", "Check updates", "更新を確認"],
        ["releaseNotesTitle"] = ["版本更新日志", "版本更新記錄", "Release notes", "更新履歴"],
        ["currentVersion"] = ["当前版本", "目前版本", "Current", "現在"],
        ["close"] = ["关闭", "關閉", "Close", "閉じる"],
        ["workbenchLabel"] = ["本地 AI 音频创作工作台", "本機 AI 音訊創作工作台", "Local AI audio production workbench", "ローカル AI オーディオ制作ワークベンチ"],
        ["models"] = ["模型中心", "模型中心", "Model Center", "モデルセンター"],
        ["maintenance"] = ["维护与恢复", "維護與復原", "Maintenance", "メンテナンス"],
        ["maintenanceSubtitle"] = ["检查环境、恢复任务并导出可用于排障的诊断信息。", "檢查環境、恢復任務並匯出可用於除錯的診斷資訊。", "Check the environment, recover tasks, and export diagnostics.", "環境を確認し、タスクを復旧して診断情報を出力します。"],
        ["settingsSubtitle"] = ["按你的习惯调整 Aurora。", "依照你的習慣調整 Aurora。", "Adapt Aurora to the way you work.", "作業スタイルに合わせて Aurora を調整します。"],
        ["aboutSubtitle"] = ["认识 Aurora，查看新变化。", "認識 Aurora，查看最新變更。", "Learn about Aurora and review what's new.", "Aurora の情報と新機能を確認します。"],
        ["modelsSubtitle"] = ["了解每款创作引擎，并管理安装、版本与更新。", "瞭解每款創作引擎，並管理安裝、版本與更新。", "Understand each engine and manage installs, versions, and updates.", "各エンジンを確認し、インストール、バージョン、更新を管理します。"],
        ["diagnostics"] = ["导出诊断", "匯出診斷", "Diagnostics", "診断を出力"],
        ["website"] = ["GitHub", "GitHub", "GitHub", "GitHub"],
        ["back"] = ["返回工作台", "返回工作台", "Back to studio", "スタジオに戻る"],
        ["ready"] = ["本地创作", "本機創作", "Local creation", "ローカル制作"],
        ["updateChecking"] = ["正在连接 GitHub…", "正在連線至 GitHub…", "Connecting to GitHub…", "GitHub に接続しています…"],
        ["updateAlreadyRunning"] = ["更新检查或安装正在进行，请稍候。", "更新檢查或安裝正在進行，請稍候。", "An update check or installation is already in progress.", "更新の確認またはインストールが進行中です。"],
        ["updateDownloading"] = ["正在下载 Aurora {0}", "正在下載 Aurora {0}", "Downloading Aurora {0}", "Aurora {0} をダウンロード中"],
        ["updateVerifying"] = ["正在验证安装包完整性", "正在驗證安裝程式完整性", "Verifying installer integrity", "インストーラーを検証中"],
        ["updatePreparingInstall"] = ["正在启动安全安装程序", "正在啟動安全安裝程式", "Starting the verified installer", "検証済みインストーラーを起動中"],
        ["updateInstallerHandoff"] = ["下载与校验已完成。安装进度窗口即将接管，完成后会自动打开新版。", "下載與驗證已完成。安裝進度視窗即將接管，完成後會自動開啟新版。", "Download and verification are complete. The installer progress window will take over and reopen the new version when finished.", "ダウンロードと検証が完了しました。インストール進捗画面に切り替わり、完了後に新バージョンを自動起動します。"],
        ["updateUpToDate"] = ["当前已是最新版本。", "目前已是最新版本。", "Aurora Audio Studio is up to date.", "現在のバージョンは最新です。"],
        ["updateReady"] = ["已找到经过校验的正式更新。", "已找到經過驗證的正式更新。", "A verified update is ready to install.", "検証済みの正式アップデートをインストールできます。"],
        ["updateAssetsIncomplete"] = ["发现新版本，但安装包或校验文件尚未完整发布。", "發現新版本，但安裝程式或驗證檔案尚未完整發布。", "A new version exists, but its verified update assets are incomplete.", "新しいバージョンがありますが、検証用ファイルが揃っていません。"],
        ["updateCheckFailed"] = ["检查更新失败：{0}", "檢查更新失敗：{0}", "Update check failed: {0}", "更新の確認に失敗しました：{0}"],
        ["updateUnavailable"] = ["当前没有可校验的更新安装包。", "目前沒有可驗證的更新安裝程式。", "No verifiable update package is available.", "検証可能な更新パッケージがありません。"],
        ["updateChecksumInvalid"] = ["更新校验文件无效，已停止安装。", "更新驗證檔案無效，已停止安裝。", "The update checksum file is invalid. Installation was stopped.", "更新チェックサムが無効なため、インストールを中止しました。"],
        ["updateVerificationFailed"] = ["安装包校验失败，已删除下载文件。", "安裝程式驗證失敗，已刪除下載檔案。", "Update verification failed. The downloaded installer was removed.", "インストーラーの検証に失敗したため、ダウンロードを削除しました。"],
        ["updateStarted"] = ["安全安装程序已启动。请批准 Windows 权限提示；安装完成后 Aurora 会自动重新打开。", "安全安裝程式已啟動。請允許 Windows 權限提示；安裝完成後 Aurora 會自動重新開啟。", "The verified installer has started. Approve the Windows prompt; Aurora will reopen automatically after installation.", "検証済みインストーラーを起動しました。Windows の確認後、完了すると Aurora が自動的に再起動します。"],
        ["updateInstallFailed"] = ["更新安装失败：{0}", "更新安裝失敗：{0}", "Update installation failed: {0}", "更新のインストールに失敗しました：{0}"],
        ["updateFound"] = ["发现 {0}：{1}", "發現 {0}：{1}", "Version {0}: {1}", "バージョン {0}：{1}"],
        ["updateDialogTitle"] = ["Aurora Audio Studio 更新", "Aurora Audio Studio 更新", "Aurora Audio Studio update", "Aurora Audio Studio アップデート"],
        ["updateDialogBody"] = ["当前版本 {0}，最新版本 {1}。Aurora 将从 GitHub 下载并校验安装包，然后自动覆盖安装。", "目前版本 {0}，最新版本 {1}。Aurora 將從 GitHub 下載並驗證安裝程式，然後自動覆蓋安裝。", "Installed: {0}. Latest: {1}. Aurora will download and verify the installer from GitHub, then install it over the current version.", "現在のバージョンは {0}、最新は {1} です。GitHub からインストーラーを取得して検証し、現在のバージョンを更新します。"],
        ["updateInstall"] = ["下载并安装", "下載並安裝", "Download and install", "ダウンロードしてインストール"],
        ["later"] = ["稍后", "稍後", "Later", "後で"],
    };

    private readonly Dictionary<string, string[]> phrases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["今天想创作什么？"] = ["今天想创作什么？", "今天想創作什麼？", "What would you like to create?", "今日は何を作りますか？"],
        ["创作音乐"] = ["创作音乐", "創作音樂", "Create music", "音楽を作る"],
        ["制作配音"] = ["制作配音", "製作配音", "Create a voice", "音声を作る"],
        ["拆分混音"] = ["拆分混音", "拆分混音", "Split a mix", "ミックスを分離"],
        ["音频转 MIDI"] = ["音频转 MIDI", "音訊轉 MIDI", "Audio to MIDI", "音声を MIDI へ"],
        ["生成字幕"] = ["生成字幕", "產生字幕", "Generate subtitles", "字幕を生成"],
        ["最近项目"] = ["最近项目", "最近專案", "Recent projects", "最近のプロジェクト"],
        ["正在进行"] = ["正在进行", "進行中", "In progress", "進行中"],
        ["查看全部任务"] = ["查看全部任务", "查看全部任務", "View all tasks", "すべてのタスク"],
        ["所有本地任务"] = ["所有本地任务", "所有本機任務", "All local tasks", "すべてのローカルタスク"],
        ["继续"] = ["继续", "繼續", "Continue", "続ける"],
        ["打开"] = ["打开", "開啟", "Open", "開く"],
        ["重试"] = ["重试", "重試", "Retry", "再試行"],
        ["取消"] = ["取消", "取消", "Cancel", "キャンセル"],
        ["检查全部更新"] = ["检查全部更新", "檢查全部更新", "Check all updates", "すべて更新確認"],
        ["查看本地文件"] = ["查看本地文件", "查看本機檔案", "View local files", "ローカルファイル"],
        ["检查 / 修复"] = ["检查 / 修复", "檢查 / 修復", "Check / Repair", "確認 / 修復"],
        ["回退"] = ["回退", "回復", "Roll back", "ロールバック"],
        ["卸载"] = ["卸载", "解除安裝", "Uninstall", "アンインストール"],
        ["重新扫描"] = ["重新扫描", "重新掃描", "Scan again", "再スキャン"],
        ["导出诊断包"] = ["导出诊断包", "匯出診斷套件", "Export diagnostics", "診断を出力"],
        ["打开日志"] = ["打开日志", "開啟日誌", "Open logs", "ログを開く"],
        ["运行环境"] = ["运行环境", "執行環境", "Runtime environment", "実行環境"],
        ["安全模式"] = ["安全模式", "安全模式", "Safe mode", "セーフモード"],
        ["任务完成后释放显存"] = ["任务完成后释放显存", "任務完成後釋放顯示記憶體", "Release VRAM after tasks", "完了後に VRAM を解放"],
        ["外观与语言"] = ["外观与语言", "外觀與語言", "Appearance and language", "表示と言語"],
        ["界面语言"] = ["界面语言", "介面語言", "Interface language", "表示言語"],
        ["主题"] = ["主题", "主題", "Theme", "テーマ"],
        ["模型目录"] = ["模型目录", "模型目錄", "Model folder", "モデルフォルダー"],
        ["成品目录"] = ["成品目录", "成品目錄", "Output folder", "出力フォルダー"],
        ["项目目录"] = ["项目目录", "專案目錄", "Project folder", "プロジェクトフォルダー"],
        ["保存设置"] = ["保存设置", "儲存設定", "Save settings", "設定を保存"],
        ["每天首次启动时检查应用更新"] = ["每天首次启动时检查应用更新", "每天首次啟動時檢查應用程式更新", "Check app updates on the first launch each day", "毎日の初回起動時にアプリ更新を確認"],
        ["启动时检查模型更新"] = ["启动时检查模型更新", "啟動時檢查模型更新", "Check model updates at startup", "起動時にモデル更新を確認"],
        ["下载大型模型前始终确认"] = ["下载大型模型前始终确认", "下載大型模型前一律確認", "Confirm large model downloads", "大容量モデルの前に確認"],
        ["任务完成后自动释放显存"] = ["任务完成后自动释放显存", "任務完成後自動釋放顯示記憶體", "Release VRAM automatically", "VRAM を自動解放"],
        ["进入工作台"] = ["进入工作台", "進入工作台", "Open workbench", "ワークベンチを開く"],
        ["查看我的成品"] = ["查看我的成品", "查看我的成品", "View my creations", "作品を見る"],
        ["开始处理"] = ["开始处理", "開始處理", "Start processing", "処理を開始"],
        ["选择文件"] = ["选择文件", "選擇檔案", "Choose file", "ファイルを選択"],
        ["打开成品目录"] = ["打开成品目录", "開啟成品目錄", "Open output folder", "出力フォルダー"],
        ["检查更新"] = ["检查更新", "檢查更新", "Check for updates", "更新を確認"],
        ["导出诊断"] = ["导出诊断", "匯出診斷", "Export diagnostics", "診断を出力"],
        ["我的成品"] = ["我的成品", "我的成品", "My creations", "マイ作品"],
        ["结束当前引擎"] = ["结束当前引擎", "結束目前引擎", "Stop current engine", "現在のエンジンを終了"],
        ["创作引擎与模型"] = ["创作引擎与模型", "創作引擎與模型", "Creative engines and models", "制作エンジンとモデル"],
        ["正在读取本地模型…"] = ["正在读取本地模型…", "正在讀取本機模型…", "Reading local models…", "ローカルモデルを確認しています…"],
        ["全部模型"] = ["全部模型", "全部模型", "All models", "すべてのモデル"],
        ["已安装"] = ["已安装", "已安裝", "Installed", "インストール済み"],
        ["默认组件"] = ["默认组件", "預設元件", "Default components", "標準コンポーネント"],
        ["可选模型"] = ["可选模型", "選用模型", "Optional models", "オプションモデル"],
        ["0.9.8 · 完整汉化更新提示，扩展模型中心信息，并新增三档 Faster-Whisper 多语言字幕模型。"] = [
            "0.9.8 · 完整汉化更新提示，扩展模型中心信息，并新增三档 Faster-Whisper 多语言字幕模型。",
            "0.9.8 · 完整在地化更新提示、擴充模型中心資訊，並新增三種 Faster-Whisper 多語言字幕模型。",
            "0.9.8 · Localized update results, richer Model Center details, and three additional multilingual Faster-Whisper models.",
            "0.9.8 · 更新結果の完全なローカライズ、モデルセンターの情報拡充、3 種類の多言語 Faster-Whisper モデルを追加。"],
        ["0.9.8 · 本地 AI 音频创作工作台"] = ["0.9.8 · 本地 AI 音频创作工作台", "0.9.8 · 本機 AI 音訊創作工作台", "0.9.8 · Local AI audio production workbench", "0.9.8 · ローカル AI オーディオ制作ワークベンチ"],
        ["为音乐、声音与影像创作者打造的一站式本地工作台。灵感、素材与成品，始终由你掌控。"] = ["为音乐、声音与影像创作者打造的一站式本地工作台。灵感、素材与成品，始终由你掌控。", "為音樂、聲音與影像創作者打造的一站式本機工作台。靈感、素材與成品，始終由你掌控。", "A unified local workbench for music, voice, and video creators. Your ideas, source files, and finished work remain under your control.", "音楽・音声・映像クリエイターのための統合ローカルワークベンチ。アイデア、素材、完成作品は常に自分で管理できます。"],
        ["更新日志"] = ["更新日志", "更新記錄", "Release notes", "更新履歴"],
        ["Copyright © 2026 Aurora Contributors. Licensed under GNU GPL v3.0."] = ["版权所有 © 2026 Aurora Contributors。采用 GNU GPL v3.0 许可。", "版權所有 © 2026 Aurora Contributors。採用 GNU GPL v3.0 授權。", "Copyright © 2026 Aurora Contributors. Licensed under GNU GPL v3.0.", "Copyright © 2026 Aurora Contributors。GNU GPL v3.0 に基づき提供。"],
        ["0.9.7 · 模型选择更完整：新增轻量配音、声音克隆、通用分轨与轻量扒谱引擎，均由用户按需安装。"] = [
            "0.9.7 · 模型选择更完整：新增轻量配音、声音克隆、通用分轨与轻量扒谱引擎，均由用户按需安装。",
            "0.9.7 · 模型選擇更完整：新增輕量配音、聲音複製、通用分軌與輕量扒譜引擎，均由使用者按需安裝。",
            "0.9.7 · More model choices: lightweight speech, voice cloning, general-purpose stems, and MIDI engines are now available on demand.",
            "0.9.7 · モデル選択を拡充：軽量音声、ボイスクローン、汎用音源分離、MIDI エンジンを必要に応じて導入できます。"]
    };

    public string Get(string key)
    {
        if (!values.TryGetValue(key, out var options)) return key;
        return settings.EffectiveLanguage() switch
        {
            "zh-TW" => options[1],
            "en-US" => options[2],
            "ja-JP" => options[3],
            _ => options[0]
        };
    }

    public string Translate(string source)
    {
        var pair = phrases.FirstOrDefault(x => x.Value.Contains(source, StringComparer.OrdinalIgnoreCase));
        if (pair.Value is null) return source;
        return settings.EffectiveLanguage() switch { "zh-TW" => pair.Value[1], "en-US" => pair.Value[2], "ja-JP" => pair.Value[3], _ => pair.Value[0] };
    }

    public string Format(string key, params object[] args) => string.Format(Get(key), args);
}
