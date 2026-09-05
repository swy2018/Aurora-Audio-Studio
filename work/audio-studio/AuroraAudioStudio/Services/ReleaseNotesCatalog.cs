namespace AuroraAudioStudio.Services;

public sealed record ReleaseNoteDisplay(string Version, string Date, string Body, bool IsCurrent);

public static class ReleaseNotesCatalog
{
    private sealed record Entry(Version Version, string Date, string[] Bodies);

    private static readonly Entry[] Entries =
    [
        ReadCurrentEntry(),
        new(new(1, 8, 1), "2026-09-03", [
            "• 修复安装到 Program Files 后创作引擎显示‘已连接’、中央工作区却为空白的问题。WebView2 用户数据现在固定保存到可写的本地应用数据目录，音乐、配音和歌声工作台可真正显示。\n• 新增安装目录回归门禁，并验证 Qwen3-TTS、Seed-VC 嵌入式工作台与分轨、扒谱、字幕原生操作页。",
            "• 修正安裝到 Program Files 後創作引擎顯示「已連線」、中央工作區卻空白的問題。WebView2 使用者資料現在固定儲存在可寫入的本機應用程式資料目錄，音樂、配音和歌聲工作台可正常顯示。\n• 新增安裝目錄回歸門檻，並驗證 Qwen3-TTS、Seed-VC 嵌入式工作台及分軌、扒譜、字幕原生操作頁。",
            "• Fixed installed Program Files builds reporting Connected while the central creative workspace remained blank. WebView2 user data now lives in writable LocalAppData so music, voice, and singing workbenches render correctly.\n• Added an installed-location regression gate and verified the Qwen3-TTS and Seed-VC embedded workbenches plus the native separation, MIDI, and subtitle pages.",
            "• Program Files へのインストール後、接続済みと表示されても中央の制作画面が空白になる問題を修正しました。WebView2 のユーザーデータを LocalAppData に保存し、音楽・音声・歌声ワークベンチを正しく表示します。\n• インストール先を想定した回帰テストを追加し、Qwen3-TTS、Seed-VC、分離、MIDI、字幕の操作画面を確認しました。"]),
        new(new(1, 8, 0), "2026-09-03", [
            "• 修复正式安装版后端已就绪却仍停在正在启动的问题；WebView2 初始化和导航均有 30 秒超时、取消和对应引擎清理。六条工作流补齐真实输入、参数、执行、进度、取消与结果入口。\n• GPU 可用时优先 CUDA；字幕只在 GPU 实际失败后回退 CPU。模型健康检查会同时验证权重、运行时和关键依赖。\n• Qwen3-TTS 补齐 SoX，Seed-VC 下载全部固定依赖，TransKun 防止 CUDA PyTorch 被 CPU 包覆盖。\n• 文件选择、窄窗口滚动、动态本地化、状态播报与执行门禁得到加强。",
            "• 六條工作流程補齊真實輸入、參數、執行、進度、取消與結果入口；ACE-Step 啟動會檢查程序、連接埠、逾時和記錄，失敗時不再留下空白框架。\n• GPU 可用時優先 CUDA；字幕只在 GPU 實際失敗後退回 CPU。模型健康檢查會同時驗證權重、執行環境和關鍵相依套件。\n• Qwen3-TTS 補齊 SoX，Seed-VC 下載全部固定相依套件，TransKun 防止 CUDA PyTorch 被 CPU 套件覆蓋。\n• WebView2、檔案選擇、窄視窗捲動、動態本地化、狀態播報與執行門檻得到加強。",
            "• Fixed installed builds remaining on Starting after the backend was ready. WebView2 initialization and navigation have 30-second timeouts, cancellation, and scoped engine cleanup. All six workflows expose real inputs, parameters, execution, progress, cancellation, and results.\n• CUDA is preferred whenever a GPU is available; subtitles fall back to CPU only after an explicit GPU failure. Readiness checks validate weights, runtimes, and critical dependencies together.\n• Qwen3-TTS includes SoX, Seed-VC downloads every pinned dependency, and TransKun prevents CPU packages from overwriting CUDA PyTorch.\n• File picking, narrow-window scrolling, dynamic localization, live status, and execution gates are more robust.",
            "• 6つのワークフローに実際の入力、設定、実行、進捗、キャンセル、結果操作を追加しました。ACE-Step はプロセス、ポート、タイムアウト、ログを確認し、失敗時に空の画面を残しません。\n• GPU 利用時は CUDA を優先し、字幕は GPU の実行失敗後にのみ CPU へ切り替えます。モデル確認では重み、実行環境、主要依存関係をまとめて検証します。\n• Qwen3-TTS に SoX を追加し、Seed-VC の固定依存関係をすべて取得し、TransKun の CUDA PyTorch が CPU パッケージで上書きされないようにしました。\n• WebView2、ファイル選択、狭い画面のスクロール、動的翻訳、状態通知、実行条件を強化しました。"]),
        new(new(1, 7, 0), "2026-09-02", [
            "• 修复已取消的排队任务仍可能启动，以及取消单个任务误停其他引擎的问题；重试会保留原预设，批处理异常后界面也能正确恢复。\n• 模型安装不再依赖 Qwen 环境提供下载器；新增独立 Hugging Face 自举、ACE-Step / Seed-VC 完整运行环境部署、四路并发更新检查、真实运行时完整性判断和可读安装详情。\n• 设置路径先校验再保存，素材按功能过滤；健康扫描与诊断导出不再阻塞界面，处理记录保存真实模型版本与实际成品文件。\n• 第二次启动会唤醒现有窗口，并恢复上次窗口大小与最大化状态。",
            "• 修正已取消的排隊任務仍可能啟動，以及取消單一任務誤停其他引擎的問題；重試會保留原預設，批次處理異常後介面也能正確恢復。\n• 模型安裝不再依賴 Qwen 環境提供下載器；新增獨立 Hugging Face 啟動環境、ACE-Step / Seed-VC 完整執行環境部署、四路並行更新檢查、真實執行環境完整性判斷與可讀安裝詳情。\n• 設定路徑先驗證再儲存，素材依功能篩選；健康掃描與診斷匯出不再阻塞介面，處理記錄儲存真實模型版本與實際成品檔案。\n• 第二次啟動會喚醒現有視窗，並還原上次視窗大小與最大化狀態。",
            "• Fixed canceled queued tasks still starting and single-task cancellation stopping unrelated engines. Retry preserves the original preset, and batch UI recovers after failures.\n• Model downloads no longer depend on the Qwen environment. Added an independent Hugging Face bootstrap, complete ACE-Step and Seed-VC deployment, four-way bounded update checks, runtime-aware health checks, and readable installation details.\n• Settings paths validate before commit and media is filtered per workflow. Health scans and diagnostics no longer block the UI, while records capture the actual model version and output files.\n• A second launch now restores the existing window, and window size and maximized state persist between sessions.",
            "• キャンセル済みの待機タスクが起動する問題と、単一タスクのキャンセルが別のエンジンを停止する問題を修正しました。再試行では元のプリセットを保持し、バッチ失敗後も画面状態を復元します。\n• モデル取得を Qwen 環境から独立させ、Hugging Face 専用環境、ACE-Step / Seed-VC の完全導入、4 並列の更新確認、実行環境を含む整合性確認、読みやすい導入詳細を追加しました。\n• 保存前の設定パス検証と機能別の素材形式制限を追加し、ヘルスチェックと診断出力を非同期化しました。履歴には実際のモデル版と成果物を保存します。\n• 二重起動時は既存ウィンドウを前面に戻し、ウィンドウサイズと最大化状態も保持します。"]),
        new(new(1, 6, 1), "2026-09-02", [
            "• 模型安装改为单任务串行执行；已有安装进行时再次点击其他模型，会明确提示等待，不再启动第二个部署进程写入同一环境。\n• 安装进度会标明当前模型；大型 PyTorch CUDA 下载会保留等待提示，取消操作始终绑定正在运行的安装。\n• 增加安装并发保护与进度归属回归检查；1.6.0 的六类分组和可靠版本检测继续保留。",
            "• 模型安裝改為單一任務依序執行；已有安裝進行時再次點擊其他模型，會清楚提示等待，不再啟動第二個部署程序寫入同一環境。\n• 安裝進度會標明目前模型；大型 PyTorch CUDA 下載會保留等待提示，取消操作始終綁定正在執行的安裝。\n• 新增安裝並行保護與進度歸屬回歸檢查；1.6.0 的六類分組和可靠版本偵測繼續保留。",
            "• Model installation is now serialized. Starting another model while one is active returns a clear wait message instead of deploying concurrently into the same environment.\n• Progress identifies the active model. Large PyTorch CUDA downloads retain a clear wait message, and Cancel remains bound to the active operation.\n• Added regression coverage for installation concurrency and progress ownership while retaining the six workflow groups and reliable version checks from 1.6.0.",
            "• モデルのインストールを一件ずつ実行するようにしました。進行中に別のモデルを選んだ場合は待機案内を表示し、同じ環境へ二重に導入しません。\n• 進捗には対象モデル名を表示し、大容量 PyTorch CUDA の取得中も待機案内を保ち、キャンセルは実行中の導入だけに作用します。\n• 同時導入防止と進捗の所属を回帰テストで保護し、1.6.0 の6分類と信頼できる版確認を引き続き提供します。"]),
        new(new(1, 6, 0), "2026-09-02", [
            "• 模型管理按音乐创作、AI 配音与声音克隆、歌声克隆、去人声 / AI 分轨、AI 扒谱、视频 AI 字幕六类分组，模型较多时也能快速定位。\n• ACE-Step、Seed-VC 等 GitHub 模型优先检测正式 Release；没有正式 Release 的仓库改用默认分支最新提交日期作为日期版，Hugging Face 模型使用官方更新时间与精确快照比较。\n• 新增 HeartMuLa、IndexTTS 2.5、SoulX-Singer SVC、Qwen3-ASR 0.6B / 1.7B 与 Qwen3-ForcedAligner 的模型管理入口；它们不会自动下载，需用户主动安装。",
            "• 模型管理依音樂創作、AI 配音與聲音複製、歌聲複製、去人聲 / AI 分軌、AI 扒譜、影片 AI 字幕六類分組，模型較多時也能快速定位。\n• ACE-Step、Seed-VC 等 GitHub 模型優先偵測正式 Release；沒有正式 Release 的儲存庫改用預設分支最新提交日期作為日期版，Hugging Face 模型使用官方更新時間與精確快照比較。\n• 新增 HeartMuLa、IndexTTS 2.5、SoulX-Singer SVC、Qwen3-ASR 0.6B / 1.7B 與 Qwen3-ForcedAligner 的模型管理入口；它們不會自動下載，需由使用者主動安裝。",
            "• Model Management is now grouped into the six product workflows: music, voice and cloning, singing conversion, stem separation, MIDI transcription, and video subtitles.\n• GitHub-backed models such as ACE-Step and Seed-VC prefer stable Releases. Repositories without a stable Release use the latest default-branch commit date, while Hugging Face models compare official update dates and exact snapshots.\n• Added optional management entries for HeartMuLa, IndexTTS 2.5, SoulX-Singer SVC, Qwen3-ASR 0.6B / 1.7B, and Qwen3-ForcedAligner. Aurora never downloads them automatically; installation remains user-initiated.",
            "• モデル管理を音楽制作、AI 音声・音声クローン、歌声クローン、ボーカル除去・ステム分離、MIDI 採譜、動画 AI 字幕の 6 機能別に整理しました。\n• ACE-Step や Seed-VC などの GitHub モデルは安定版 Release を優先します。安定版がない場合は既定ブランチの最新コミット日を日付版として扱い、Hugging Face は公式更新日と正確なスナップショットを比較します。\n• HeartMuLa、IndexTTS 2.5、SoulX-Singer SVC、Qwen3-ASR 0.6B / 1.7B、Qwen3-ForcedAligner を任意モデルとして追加しました。自動ダウンロードは行わず、導入はユーザー操作時のみです。"]),
        new(new(1, 5, 1), "2026-09-01", [
            "• 任务工作台选择未安装模型时，现在会显示独立的“下载安装模型”按钮；安装完成后才切换为“进入工作台”，模型切换会立即刷新状态。\n• 修复已完整下载的组件包因断点续传收到 HTTP 416 而被误判为失败的问题；Aurora 会核对官方文件大小，并继续执行原有 SHA-256 完整性校验。\n• 增加工作台安装入口、断点文件大小与下载恢复路径的回归检查。",
            "• 任務工作台選擇未安裝模型時，現在會顯示獨立的「下載安裝模型」按鈕；安裝完成後才切換為「進入工作台」，切換模型會立即更新狀態。\n• 修正已完整下載的元件套件因續傳收到 HTTP 416 而被誤判為失敗的問題；Aurora 會核對官方檔案大小，並繼續執行原有 SHA-256 完整性驗證。\n• 新增工作台安裝入口、續傳檔案大小與下載恢復路徑的回歸檢查。",
            "• Selecting an uninstalled workbench model now shows a dedicated Download and install model action. Enter workbench appears only after installation, and switching models refreshes the state immediately.\n• Fixed completed component downloads being reported as failures when a resume request receives HTTP 416. Aurora checks the official asset size and then continues through the existing SHA-256 integrity validation.\n• Added regression coverage for the workbench install action, partial-file sizing, and download recovery path.",
            "• ワークベンチで未導入モデルを選ぶと、専用の「モデルをダウンロードしてインストール」操作を表示します。導入完了後にのみワークベンチ操作へ切り替わり、モデル変更時も状態を即時更新します。\n• ダウンロード済みのコンポーネントに対する再開要求が HTTP 416 を返した際、失敗と誤判定する問題を修正しました。公式ファイルサイズを確認し、既存の SHA-256 完全性検証を続行します。\n• ワークベンチ導入操作、部分ファイルサイズ、ダウンロード復旧経路の回帰テストを追加しました。"]),
        new(new(1, 5, 0), "2026-09-01", [
            "• 模型中心改为更直观的分层卡片，集中展示用途、功能、语言、版本、许可、状态与本地路径。\n• 检查更新后，存在新版的组件会直接显示更新按钮，并提供可重试的更新全部入口。\n• 更新前会检测正在运行的组件；Aurora 不会强制关闭它们，而会提示先保存工作并关闭后重试，避免未保存内容丢失。\n• 更新失败仍保留可更新状态，文件占用竞态会返回明确提示；相关行为加入回归测试。",
            "• 模型中心改為更直觀的分層卡片，集中顯示用途、功能、語言、版本、授權、狀態與本機路徑。\n• 檢查更新後，存在新版本的元件會直接顯示更新按鈕，並提供可重試的全部更新入口。\n• 更新前會偵測正在執行的元件；Aurora 不會強制關閉它們，而會提示先儲存工作並關閉後重試，避免未儲存內容遺失。\n• 更新失敗仍保留可更新狀態，檔案佔用競態會顯示明確提示；相關行為已加入回歸測試。",
            "• Model Management now uses clearer layered cards that group purpose, workflow, languages, version, license, status, and local path.\n• After checking, components with newer versions expose an Update action plus a retryable Update all flow.\n• Aurora detects running components before replacement. It never force-closes them; users are asked to save work, close the component, and retry so unsaved work is protected.\n• Failed updates remain available to retry, file-lock races return actionable guidance, and regression coverage protects the flow.",
            "• モデル管理を見やすい階層カードに変更し、用途、機能、言語、バージョン、ライセンス、状態、保存先をまとめて表示します。\n• 更新確認後、新しい版があるコンポーネントには更新操作と、再試行可能な一括更新を表示します。\n• 置換前に実行中のコンポーネントを検出し、強制終了せず、作業を保存して終了後に再試行するよう案内します。\n• 失敗した更新は再試行可能な状態を保ち、ファイル占有競合を分かりやすく案内し、回帰テストで保護します。"]),
        new(new(1, 4, 1), "2026-09-01", [
            "• BS-RoFormer、YourMT3+、ByteDance Piano、Faster-Whisper XXL 与 Subtitle Edit 现在都可在模型中心自动检查、安装、修复或更新；更新仍先暂存校验再安全切换。\n• 任务工作台选择未安装模型时会直接显示安装入口，不再留下无法继续的选择状态。\n• 分轨新增二轨与多轨选择：二轨使用专用 Vocals Revive 模型生成纯人声与纯伴奏，多轨继续提供质量与速度方案。\n• 字幕预设现在明确对应 Small、Large v3 Turbo 与 Large v3，方便在速度、显存和准确率之间选择。",
            "• BS-RoFormer、YourMT3+、ByteDance Piano、Faster-Whisper XXL 與 Subtitle Edit 現在都可在模型中心自動檢查、安裝、修復或更新；更新仍會先暫存驗證再安全切換。\n• 任務工作台選擇未安裝模型時會直接顯示安裝入口，不再留下無法繼續的選擇狀態。\n• 分軌新增二軌與多軌選擇：二軌使用專用 Vocals Revive 模型產生純人聲與純伴奏，多軌繼續提供品質與速度方案。\n• 字幕預設現在明確對應 Small、Large v3 Turbo 與 Large v3，方便在速度、顯示記憶體和準確率之間選擇。",
            "• BS-RoFormer, YourMT3+, ByteDance Piano, Faster-Whisper XXL, and Subtitle Edit can now be checked, installed, repaired, or updated automatically from Model Management; replacements still stage and verify before switching.\n• Selecting an uninstalled model in a task workbench now exposes the install action immediately instead of leaving a blocked selection.\n• Separation now offers two-stem and multi-stem modes. Two-stem uses the dedicated Vocals Revive model for clean vocals and instrumental output, while multi-stem retains quality and speed choices.\n• Subtitle presets now map explicitly to Small, Large v3 Turbo, and Large v3 for clearer speed, VRAM, and accuracy trade-offs.",
            "• BS-RoFormer、YourMT3+、ByteDance Piano、Faster-Whisper XXL、Subtitle Edit をモデル管理から自動確認、導入、修復、更新できるようにしました。更新は従来どおり一時領域で検証してから安全に切り替えます。\n• タスク画面で未導入モデルを選ぶと、その場でインストール操作を表示し、進めない状態を解消しました。\n• 分離に 2 ステムとマルチステムを追加しました。2 ステムは専用 Vocals Revive でボーカルと伴奏を出力し、マルチステムは品質と速度を選べます。\n• 字幕プリセットを Small、Large v3 Turbo、Large v3 に明確に対応させ、速度、VRAM、精度の選択を分かりやすくしました。"]),
        new(new(1, 4, 0), "2026-08-30", [
            "• 模型管理新增 MiniMax-Music3 按需安装，可自动配置独立 CUDA 环境并在音乐创作中启用；不会未经确认下载模型。\n• TransKun V2 加入模型管理并替换为默认钢琴扒谱引擎，ByteDance Piano 作为经典可选模型保留。\n• Hugging Face、Git 与 PyPI 模型现在可按官方版本真实比对；固定运行组件不再误报最新，而会明确提示其升级渠道。\n• 关于页新增反馈问题与建议入口，并同步更新网站、README 与发布文档。",
            "• 模型管理新增 MiniMax-Music3 隨選安裝，可自動設定獨立 CUDA 環境並在音樂創作中啟用；不會未經確認下載模型。\n• TransKun V2 加入模型管理並取代為預設鋼琴扒譜引擎，ByteDance Piano 保留為經典選用模型。\n• Hugging Face、Git 與 PyPI 模型現在可依官方版本真實比對；固定執行元件不再誤報最新，並會清楚提示升級方式。\n• 關於頁新增問題與建議回饋入口，並同步更新網站、README 與發布文件。",
            "• Added on-demand MiniMax-Music3 installation to Model Management, including automatic isolated CUDA setup and Music workbench enablement; Aurora never downloads it without confirmation.\n• Added TransKun V2 to Model Management and made it the default piano transcription engine, while retaining ByteDance Piano as a classic option.\n• Hugging Face, Git, and PyPI models now compare real upstream versions; fixed runtime components no longer claim to be current and instead identify their upgrade path.\n• Added a feedback entry to About and synchronized the website, README, and release documentation.",
            "• モデル管理に MiniMax-Music3 のオンデマンド導入を追加し、独立 CUDA 環境を自動設定して音楽制作から利用可能にしました。確認なしのダウンロードは行いません。\n• TransKun V2 をモデル管理に追加して標準のピアノ採譜エンジンとし、ByteDance Piano は従来モデルとして残しました。\n• Hugging Face、Git、PyPI モデルは公式版と実際に比較し、固定ランタイムは最新版と誤表示せず更新経路を明示します。\n• 情報ページに問題・提案のフィードバック入口を追加し、サイト、README、公開資料を同期しました。"]),
        new(new(1, 3, 0), "2026-08-12", [
            "• CI 在生成安装包前自动执行关键回归检查。\n• Aurora 改为单实例运行，模型更新先暂存校验再切换，并保留上一可用版本。\n• 动态任务状态完整支持四语言；.arr 记录增加版本迁移和恢复副本。\n• 诊断导出新增隐私预览与路径脱敏。\n• 首页访问键、屏幕阅读器名称和实时进度提示提升键盘与辅助功能体验。",
            "• CI 在產生安裝程式前自動執行關鍵回歸檢查。\n• Aurora 改為單一執行個體，模型更新會先暫存驗證再切換，並保留上一個可用版本。\n• 動態任務狀態完整支援四種語言；.arr 記錄加入版本遷移與復原副本。\n• 診斷匯出新增隱私預覽與路徑遮蔽。\n• 首頁快速鍵、螢幕閱讀器名稱與即時進度提示改善鍵盤及輔助功能體驗。",
            "• CI now runs critical regression checks before packaging.\n• Aurora now runs as a single instance; model updates stage and verify before switching while retaining the previous working version.\n• Dynamic task states cover all four languages, and .arr records gain schema migration and recovery copies.\n• Diagnostics export adds privacy preview and path redaction.\n• Home access keys, screen-reader names, and live progress improve keyboard and assistive use.",
            "• インストーラー作成前に CI が重要な回帰テストを自動実行します。\n• Aurora を単一インスタンス化し、モデル更新は一時領域で検証後に切り替え、直前の正常版を保持します。\n• 動的タスク表示を4言語に対応し、.arr に形式移行と復旧コピーを追加しました。\n• 診断出力にプライバシー確認とパスの伏せ字を追加しました。\n• ホームのアクセスキー、読み上げ名、進捗通知でキーボードと支援技術の操作性を改善しました。"]),
        new(new(1, 2, 5), "2026-08-12", [
            "• 修复首次使用引导中保存位置和模型按钮无响应的问题，主导航与底部导航现在都能正确跳转。\n• 首页改为六个独立功能直接进入，不再要求先创建统一项目。\n• 明确 .arr 只是分轨、扒谱和字幕任务的轻量处理记录，并同步调整相关界面文案。\n• README、官网、关于页、音乐人使用说明、版本信息与产品截图全部同步至 1.2.5。",
            "• 修正首次使用引導中儲存位置和模型按鈕無回應的問題，主導覽與底部導覽現在都能正確跳轉。\n• 首頁改為六個獨立功能直接進入，不再要求先建立統一專案。\n• 明確 .arr 只是分軌、扒譜和字幕任務的輕量處理記錄，並同步調整相關介面文字。\n• README、官網、關於頁、音樂人使用說明、版本資訊與產品截圖全部同步至 1.2.5。",
            "• Fixed unresponsive storage and model actions in the first-use guide; main and footer navigation destinations now resolve correctly.\n• Rebuilt Home around six independent feature entry points with no unified-project setup step.\n• Clarified .arr as a lightweight processing record for separation, transcription, and subtitle tasks, with matching UI terminology.\n• Synchronized the README, website, About page, musician guide, version metadata, and product screenshots for 1.2.5.",
            "• 初回ガイドの保存先とモデルボタンが反応しない問題を修正し、メインとフッターの両ナビゲーションへ正しく移動できるようにしました。\n• ホームを6つの独立した機能から直接開始できる構成へ変更し、統一プロジェクトの作成手順を廃止しました。\n• .arr を分離、採譜、字幕タスク用の軽量な処理履歴として明確化し、関連する画面表記も更新しました。\n• README、公式サイト、情報ページ、利用ガイド、バージョン情報、製品画像を 1.2.5 に同期しました。"]),
        new(new(1, 2, 0), "2026-08-11", [
            "• 首页新增三步首次使用引导，可直接设置保存位置、按需安装模型并开始第一个字幕任务。\n• README、官网与音乐人使用说明同步补充低门槛首次体验流程。\n• README 与官网的音乐、声音和字幕产品截图全部重新实拍为 1.2.0 当前界面。\n• 修正开发文档中的更新回归测试命令，并明确固定下载包模型清单的用途。",
            "• 首頁新增三步首次使用引導，可直接設定儲存位置、按需安裝模型並開始第一個字幕任務。\n• README、官網與音樂人使用說明同步補充低門檻首次體驗流程。\n• README 與官網的音樂、聲音和字幕產品截圖全部重新拍攝為 1.2.0 當前介面。\n• 修正開發文件中的更新回歸測試指令，並說明固定下載套件模型清單的用途。",
            "• Added a three-step first-use guide on Home for storage setup, on-demand model installation, and a first subtitle task.\n• Synchronized the README, website, and musician guide with a lower-friction first-run path.\n• Recaptured all Music, Voice, and Subtitle product screenshots for the current 1.2.0 interface across the README and website.\n• Fixed the documented update regression command and clarified the role of the verified fixed-package model manifest.",
            "• ホームに、保存先設定、必要なモデル導入、最初の字幕タスクへ進む3ステップガイドを追加。\n• README、公式サイト、利用ガイドに初回体験フローを反映。\n• README と公式サイトの音楽、音声、字幕の製品画像を 1.2.0 の現行画面で再撮影。\n• 更新回帰テストのコマンドを修正し、固定パッケージ用モデルマニフェストの役割を明確化。"]),
        new(new(1, 1, 0), "2026-08-09", [
            "• 新增批量素材工作流，可多选或拖入音频与视频，并在提交前直接预览。\n• 新增快速草稿、推荐质量与高质量三档处理预设。\n• 任务中心接入引擎实时进度、当前阶段、持续时间与持久日志，并可暂停后续队列。\n• 新增成品库，按项目与时间汇总音轨、MIDI、字幕及其他结果。\n• 模型部署新增磁盘空间检查、下载进度与速度、取消、断点续传及安装完整性检查。\n• 全面重构官方网站，以原始比例清晰展示真实工作台并支持全屏查看。",
            "• 新增批次素材工作流程，可多選或拖入音訊與影片，並在提交前直接預覽。\n• 新增快速草稿、建議品質與高品質三種處理預設。\n• 任務中心接入引擎即時進度、目前階段、持續時間與持久記錄，並可暫停後續佇列。\n• 新增成品庫，依專案與時間彙整音軌、MIDI、字幕及其他結果。\n• 模型部署新增磁碟空間檢查、下載進度與速度、取消、續傳及安裝完整性檢查。\n• 全面重構官方網站，以原始比例清楚展示真實工作台並支援全螢幕查看。",
            "• Added batch media workflows with multi-select, drag and drop, and built-in preview before submission.\n• Added Fast, Recommended, and Quality processing presets.\n• Task Center now shows live engine progress, stage, duration, and persistent logs, with control over the pending queue.\n• Added a Results library that groups stems, MIDI, subtitles, and other output by project and time.\n• Model deployment now includes disk-space checks, download progress and speed, cancel, resume, and installation integrity checks.\n• Rebuilt the website around clear, full-ratio product imagery with full-screen viewing.",
            "• 音声・動画の複数選択、ドラッグ＆ドロップ、送信前プレビューに対応したバッチ素材ワークフローを追加。\n• 高速、推奨品質、高品質の3つの処理プリセットを追加。\n• タスクセンターにエンジンのリアルタイム進捗、現在段階、所要時間、永続ログを追加し、待機キューを一時停止可能に。\n• ステム、MIDI、字幕などをプロジェクトと時刻でまとめる成果ライブラリを追加。\n• モデル導入に空き容量確認、速度付き進捗、キャンセル、再開、整合性確認を追加。\n• 実際の画面を元の比率で鮮明に表示し、全画面確認できる公式サイトへ刷新。"]),
        new(new(1, 0, 1), "2026-08-08", [
            "• 未安装的创作模型现在会显示“自动安装模型”，不再禁用入口。\n• 安装前明确显示目标目录、预计下载量与建议预留空间，并可更改 Aurora 模型目录。\n• 模型安装完成并通过完整性检测后，自动继续进入工作台。\n• 自动更新只保留 Windows 标准安装进度界面，移除重复的 Aurora 自制安装窗口。",
            "• 未安裝的創作模型現在會顯示「自動安裝模型」，不再停用入口。\n• 安裝前清楚顯示目標目錄、預計下載量與建議預留空間，並可變更 Aurora 模型目錄。\n• 模型安裝完成並通過完整性檢查後，自動繼續進入工作台。\n• 自動更新只保留 Windows 標準安裝進度介面，移除重複的 Aurora 自製安裝視窗。",
            "• Uninstalled creative models now show an Install model action instead of a disabled workbench button.\n• Before installation, Aurora shows the exact target path, estimated download, and recommended free space, with an option to change the model folder.\n• After installation passes the integrity check, Aurora continues into the workbench automatically.\n• Automatic updates now show only the standard Windows installer progress UI; the duplicate custom updater window was removed.",
            "• 未インストールの制作モデルでは、無効なボタンではなく「モデルを自動インストール」を表示。\n• インストール前に保存先、推定ダウンロード量、推奨空き容量を表示し、モデルフォルダーも変更可能。\n• インストールと整合性確認の完了後、そのままワークベンチを起動。\n• 自動更新は Windows 標準の進捗画面だけを表示し、重複していた独自更新画面を削除。"]),
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

    private static Entry ReadCurrentEntry()
    {
        using var stream = typeof(ReleaseNotesCatalog).Assembly.GetManifestResourceStream("Aurora.Release.json") ?? throw new InvalidDataException("Missing release metadata.");
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        var entry = document.RootElement;
        return new(Version.Parse(entry.GetProperty("version").GetString()!), entry.GetProperty("date").GetString()!, entry.GetProperty("notes").EnumerateArray().Select(x => x.GetString()!).ToArray());
    }

    public static IReadOnlyList<ReleaseNoteDisplay> CurrentAndRecent(string currentVersion, string language, int count = 5)
    {
        if (!Version.TryParse(currentVersion, out var current)) current = Entries[0].Version;
        var languageIndex = language switch { "zh-TW" => 1, "en-US" => 2, "ja-JP" => 3, _ => 0 };
        return Entries.Where(x => x.Version <= current).OrderByDescending(x => x.Version).Take(count)
            .Select((x, index) => new ReleaseNoteDisplay(x.Version.ToString(3), x.Date, x.Bodies[languageIndex], index == 0)).ToList();
    }
}
