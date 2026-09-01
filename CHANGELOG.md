# Aurora Audio Studio 更新日志 / Changelog

本文件记录正式发布版本的重要变化。每个版本先提供中文说明，再提供英文说明。

This file documents notable changes in stable releases. Chinese release notes are listed first, followed by English.

## 1.6.1 — 2026-09-02

### 中文

- 模型安装改为单任务串行执行；已有安装进行时再次点击其他模型，会明确提示等待，不再启动第二个部署进程写入同一环境。
- 安装进度增加当前模型名称，取消操作始终绑定正在运行的安装任务，不会被后一次点击覆盖；下载大型 PyTorch CUDA 组件时保留明确的等待提示，不再被 `uv` 的 Python 环境标题覆盖。
- 增加模型安装并发保护与进度归属回归检查，保留 1.6.0 的六类模型分组、正式 Release 优先和日期版更新检测。
- 同步应用版本、四语内置更新日志、README、官网、About、产品文档与音乐人使用说明；界面布局未变，继续使用现有截图。

### English

- Model installation is now serialized. Starting another model while one is active returns a clear wait message instead of launching a second deployment into the same environment.
- Installation progress identifies the active model, and Cancel remains bound to that operation rather than being replaced by a later click. Large PyTorch CUDA downloads retain an explicit wait message instead of being overwritten by uv's Python environment header.
- Added regression coverage for installation concurrency and progress ownership while retaining the six workflow groups, stable-Release-first policy, and date-version fallback from 1.6.0.
- Synchronized application metadata, four-language in-app notes, README, website, About, product documentation, and the musician guide. Existing screenshots remain because the interface layout is unchanged.

## 1.6.0 — 2026-09-02

### 中文

- 模型管理按音乐创作、AI 配音与声音克隆、歌声克隆、去人声 / AI 分轨、AI 扒谱、视频 AI 字幕六个产品功能分组，模型较多时也能快速定位。
- ACE-Step、Seed-VC 等 GitHub 模型优先检测正式 Release；没有正式 Release 时，改用默认分支最新提交日期作为日期版，并以精确提交比较更新。
- Hugging Face 模型使用官方 `lastModified` 日期展示版本，并用精确快照 SHA 判断是否需要更新。
- 新增 HeartMuLa、IndexTTS 2.5、SoulX-Singer SVC、Qwen3-ASR 0.6B / 1.7B 与 Qwen3-ForcedAligner 的模型管理入口；不会自动下载，需用户主动安装。
- 同步应用版本、四语内置更新日志、README、官网、About、产品文档与音乐人使用说明。

### English

- Grouped Model Management by the six product workflows: music, voice and cloning, singing conversion, stem separation, MIDI transcription, and video subtitles.
- GitHub-backed models such as ACE-Step and Seed-VC now prefer stable Releases. When no stable Release exists, Aurora presents the latest default-branch commit date and compares the exact commit.
- Hugging Face models display the official `lastModified` date and use the exact snapshot SHA for update decisions.
- Added optional management entries for HeartMuLa, IndexTTS 2.5, SoulX-Singer SVC, Qwen3-ASR 0.6B / 1.7B, and Qwen3-ForcedAligner. They are never downloaded automatically and require a user-initiated installation.
- Synchronized application metadata, four-language in-app notes, README, website, About, product documentation, and the musician guide.

## 1.5.1 — 2026-09-01

### 中文

- 任务工作台选择未安装模型时，改为显示独立的“下载安装模型”按钮；安装完成后才显示“进入工作台”，切换模型会立即刷新状态。
- 修复组件包已经完整下载却因断点续传收到 HTTP 416 而被误判为失败的问题；恢复流程会核对 GitHub Release 官方文件大小，再继续执行 SHA-256 完整性校验。
- 增加工作台安装入口、断点文件大小、HTTP 416 恢复路径和发布版本同步回归检查。
- 同步应用版本、四语内置更新日志、README、官网、About、产品文档与音乐人使用说明。

### English

- Uninstalled workbench models now expose a dedicated Download and install model action. Enter workbench appears only after installation, and model changes refresh state immediately.
- Fixed completed component packages being reported as failed when a resume request receives HTTP 416. Aurora checks the official GitHub Release asset size before continuing through SHA-256 integrity validation.
- Added regression coverage for the workbench install action, partial-file sizing, HTTP 416 recovery, and release-version synchronization.
- Synchronized application metadata, four-language in-app notes, README, website, About, product documentation, and the musician guide.
## 1.5.0 — 2026-09-01

### 中文

- 模型中心重构为更直观的分层卡片，集中展示用途、功能、语言、版本、许可、状态和本地路径；过长路径可通过提示查看完整内容。
- 检查模型更新后，存在新版的组件会直接显示“更新”，并提供带数量的“更新全部”；批量更新逐项执行，失败项保留为可更新状态供重试。
- 更新前检测正在运行的组件。Aurora 不会强制关闭 Subtitle Edit 等工具，而会提示先保存工作、关闭组件后重试，避免未保存内容丢失。
- 文件占用竞态改为明确的可重试提示，并加入真实进程路径、更新按钮、批量更新和无障碍模型卡片的回归检查。
- 同步应用版本、四语内置更新日志、README、官网、About、问题模板、产品文档与音乐人使用说明。

### English

- Rebuilt Model Management with clearer layered cards for purpose, workflow, languages, version, license, status, and local path; full paths remain available through tooltips.
- Detected updates now expose direct Update actions and a counted Update all flow. Batch updates run sequentially and failed items remain retryable.
- Aurora detects running components before replacement. It never force-closes tools such as Subtitle Edit, protecting unsaved work and asking users to close the component before retrying.
- File-lock races now return actionable retry guidance, backed by regression checks for real process detection, update actions, batch updates, and accessible model cards.
- Synchronized application metadata, four-language in-app notes, README, website, About, issue template, product documentation, and the musician guide.
## 1.4.1 — 2026-09-01

### 中文

- 为 BS-RoFormer、YourMT3+、ByteDance Piano、Faster-Whisper XXL 与 Subtitle Edit 增加可验证的自动检查、安装、修复和更新通道；所有替换仍在暂存目录完成校验后再切换。
- 任务工作台选择未安装模型时会直接提供安装按钮，并沿用模型位置、磁盘空间和明确确认流程。
- 去人声 / AI 分轨新增二轨与多轨选择；二轨使用专用 BS-RoFormer Vocals Revive V3e 输出纯人声和纯伴奏，多轨继续提供质量与速度方案。
- 字幕预设明确对应 Faster-Whisper Small、Large v3 Turbo 与 Large v3。
- 新增自动更新适配器 ADR、组件来源与完整性回归检查，并同步应用内更新日志、README、官网、About 与音乐人指南。

### English

- Added verified automatic check, install, repair, and update paths for BS-RoFormer, YourMT3+, ByteDance Piano, Faster-Whisper XXL, and Subtitle Edit. Replacements still stage and verify before activation.
- Task workbenches now expose the install action immediately when an uninstalled model is selected, using the existing location, disk-space, and explicit-confirmation flow.
- Added two-stem and multi-stem separation. The dedicated BS-RoFormer Vocals Revive V3e model produces clean vocals and instrumental output, while multi-stem retains quality and speed choices.
- Mapped subtitle presets explicitly to Faster-Whisper Small, Large v3 Turbo, and Large v3.
- Added an updater ADR and regression coverage for component sources and integrity, and synchronized in-app notes, README, website, About, and the musician guide.
## 1.4.0 — 2026-08-30

### 中文

- 模型管理新增 MiniMax-Music3 按需安装入口；确认后自动创建独立 Python/CUDA 环境、下载所需官方 Diffusers 权重并在音乐创作中启用，不会随安装包预装或未经确认下载。
- TransKun V2 加入模型管理并成为默认钢琴扒谱引擎；YourMT3+、ByteDance Piano 与 Spotify Basic Pitch 继续保留为可选模型。
- Hugging Face、Git 与 PyPI 模型现在会比较真实上游版本；固定运行包不再误报“最新”，而会明确提示随 Aurora 或上游安装器升级。
- 关于页新增“反馈问题 / 建议”，GitHub 新增结构化问题与功能建议模板。
- README、官方网站、关于页、音乐人使用说明、产品与架构文档同步至 1.4.0；界面布局未改变，因此沿用现有高清截图。

### English

- Added on-demand MiniMax-Music3 installation to Model Management. After confirmation, Aurora creates an isolated Python/CUDA environment, downloads only the required official Diffusers weights, and enables the Music workbench; it is never bundled or downloaded silently.
- Added TransKun V2 to Model Management and made it the default piano transcription engine, while retaining YourMT3+, ByteDance Piano, and Spotify Basic Pitch as options.
- Hugging Face, Git, and PyPI models now compare real upstream versions. Fixed runtime bundles no longer claim to be current and instead state whether Aurora or the upstream installer owns upgrades.
- Added a feedback action to About and structured GitHub forms for bugs and feature requests.
- Synchronized the README, official website, About page, musician guide, product, and architecture documentation for 1.4.0. Existing high-resolution screenshots remain because the layout is unchanged.

## 1.3.0 — 2026-08-12

### 中文

- CI 现在会在构建安装包前执行更新、模型事务、单实例与处理记录兼容回归检查。
- Aurora 改为单实例运行，避免多个进程同时写入任务、设置和模型状态。
- 模型安装与更新改为暂存、完整性检查和目录切换；上一可用版本保留在本机，可直接回退，失败不会覆盖正式模型目录。
- 动态任务状态、恢复提示和主要运行文案补齐简体中文、繁體中文、English 与日本語。
- `.arr` 处理记录开始显式检查格式版本；旧记录自动迁移，损坏或过新的记录保留 `.recovery` 恢复副本并写入诊断日志。
- 诊断导出新增内容预览并自动隐藏用户目录、用户名、模型、成品和处理记录路径。
- 六个首页入口新增访问键，主要导航、工作台和进度状态补充屏幕阅读器名称与实时状态提示；高对比度主题继续使用 Windows 系统颜色。
- README、官方网站、关于页、音乐人使用说明和版本信息同步至 1.3.0；界面布局未改变，因此沿用现有高清产品截图。

### English

- CI now runs update, model transaction, single-instance, and processing-record compatibility regression checks before packaging.
- Aurora now runs as a single instance to prevent concurrent writes to tasks, settings, and model state.
- Model installs and updates now use staging, integrity verification, and directory switching. The previous working version remains locally recoverable, and failures do not overwrite the active model.
- Dynamic task states, recovery messages, and primary runtime copy now cover Simplified Chinese, Traditional Chinese, English, and Japanese.
- `.arr` processing records now enforce schema compatibility: legacy records migrate, while damaged or newer records receive `.recovery` copies and diagnostic log entries.
- Diagnostics export now previews the included log count and redacts user, profile, model, output, and processing-record paths.
- The six Home actions now have access keys, while navigation, workbench, and progress states expose screen-reader names and live status; high contrast continues to use Windows system colors.
- Updated the README, official website, About page, musician guide, and version metadata for 1.3.0. Existing high-resolution screenshots remain because the visual layout is unchanged.

## 1.2.5 — 2026-08-12

### 中文

- 修复首页首次使用引导中“设置保存位置”和“按需安装模型”按钮无响应的问题；统一支持主导航与底部导航跳转。
- 首页改为六个独立功能直接进入，不再要求用户先创建并不存在的统一工程。
- 将 `.arr` 的用户可见定位从“项目文件”纠正为分轨、扒谱和字幕任务的轻量处理记录，并同步调整最近记录、设置与恢复提示。
- README、官方网站、关于页、音乐人使用说明、版本信息与产品截图全部同步至 1.2.5。

### English

- Fixed unresponsive first-use actions for storage and model setup by resolving both main and footer navigation destinations.
- Rebuilt Home around six independent feature entry points, with no fictional unified-project setup step.
- Clarified `.arr` as a lightweight processing record for separation, transcription, and subtitle tasks, and updated related history, settings, and recovery labels.
- Synchronized the README, official website, About page, musician guide, version metadata, and product screenshots for 1.2.5.

## 1.2.0 — 2026-08-11

### 中文

- 首页新增三步首次使用引导，可直接设置保存位置、按需安装模型并开始第一个字幕任务。
- README、官方网站与音乐人使用说明同步补充低门槛首次体验流程。
- README 与官方网站的音乐、声音和字幕三张产品截图全部重新实拍为 1.2.0 当前界面。
- 修正更新回归测试命令，明确 `model-manifest.json` 仅用于带固定地址与 SHA-256 的下载包。

### English

- Added a three-step first-use guide on Home for storage setup, on-demand model installation, and a first subtitle task.
- Synchronized the README, official website, and musician guide with a lower-friction first-run path.
- Recaptured all Music, Voice, and Subtitle product screenshots for the current 1.2.0 interface across the README and website.
- Fixed the update regression command and clarified that `model-manifest.json` is reserved for fixed packages with stable URLs and SHA-256 values.

## 1.1.0 — 2026-08-09

### 中文

Aurora 1.1 将原本分散的本地音频工具进一步整合为可持续使用的专业工作流，重点完善素材进入、任务执行、模型部署和结果回收。

#### 创作与批处理

- 支持一次选择多个音频或视频，也可把素材直接拖入处理区。
- 新增内置媒体预览，在提交任务前确认素材内容、格式与文件大小。
- 新增快速草稿、推荐质量和高质量三档预设，并为分轨、扒谱和字幕匹配对应引擎。
- 批量任务按顺序使用本地 GPU，每个任务独立保存状态与结果。

#### 任务中心与成品库

- 接入引擎输出的实时百分比和状态信息，替代固定的占位进度。
- 显示当前阶段、已用时间、最近一条引擎信息和持久任务日志。
- 支持暂停和继续等待中的队列，当前运行任务不会被强行中断。
- 新增成品库，按项目与时间集中展示音轨、MIDI、字幕及其他输出。

#### 模型部署

- 安装前同时显示预计下载量、建议空间与目标磁盘当前可用空间。
- 支持下载进度、传输速度、取消和断点续传。
- 模型包继续执行 SHA-256 校验；新安装先在临时位置完成完整性检查，再进入正式目录。
- Hugging Face 与 Python 环境部署过程可以安全取消，未完成下载保留用于下次继续。

#### 官网与产品表达

- 全面重构官方网站的排版、信息层级和响应式布局。
- 三张真实工作台截图使用 1672 × 941 原始比例展示，不再裁切或透视拉伸。
- 新增原尺寸灯箱预览、键盘可操作标签页、语言适配和低动态模式。
- README、软件介绍和中英双语发布说明与 1.1 功能同步更新。

### English

Aurora 1.1 turns the local audio toolkit into a more continuous professional workflow, strengthening media intake, task execution, model deployment, and result management.

#### Creation and batches

- Select multiple audio or video files at once, or drop them directly into the processing workspace.
- Preview media before submission with format and file-size details.
- Choose Fast, Recommended, or Quality presets mapped to the appropriate separation, transcription, and subtitle engines.
- Batch tasks use the local GPU in sequence and preserve each status and result independently.

#### Task Center and Results

- Replaced fixed placeholder percentages with live progress and state reported by local engines.
- Added current stage, elapsed time, latest engine message, and persistent task logs.
- Pause and resume pending work without forcing the active process to stop.
- Added a Results library for stems, MIDI, subtitles, and other output, grouped by project and time.

#### Model deployment

- Review estimated download, recommended capacity, and currently available disk space before installation.
- Added progress, transfer speed, cancellation, and resumable downloads.
- Model packages retain SHA-256 verification; new packages complete integrity checks in staging before entering the final directory.
- Hugging Face and Python environment deployment can stop safely and continue from retained partial downloads.

#### Website and product presentation

- Rebuilt the official site with a new visual hierarchy and responsive system.
- All three real product screenshots display at their full 1672 × 941 ratio without cropping or perspective distortion.
- Added full-resolution viewing, keyboard-accessible tabs, language adaptation, and reduced-motion support.
- Updated the README, product description, and bilingual release material for version 1.1.

## 1.0.1 — 2026-08-08

### 中文

#### 按需安装模型

- 选择未安装的创作模型时，“进入工作台”会自动改为“自动安装模型”，不再以禁用按钮阻断操作。
- 安装确认窗口会显示精确目标目录、预计下载量与建议预留空间，并允许改选 Aurora 模型目录。
- 更改模型目录前会明确说明：它会更新 Aurora 的全局模型目录，现有模型不会自动移动。
- 模型部署完成后执行完整性检测；检测通过即继续进入对应工作台，失败则保留明确错误，不会假装安装成功。

#### 更清晰的自动更新

- 移除与 Inno Setup 同时出现的 Aurora 自制安装进度窗口。
- Aurora 继续负责 GitHub 检查、断点续传、自动重试、SHA-256 校验与安全交接；开始覆盖安装后只显示 Windows 标准安装进度界面。
- 安装成功后仍会自动打开新版，个人设置、任务、模型、项目、素材、成品与安装目录保持不变。

### English

#### On-demand model installation

- Selecting an uninstalled creative model now changes Open workbench to Install model instead of disabling the action.
- The confirmation dialog shows the exact target path, estimated download, and recommended free space, and lets the user change Aurora's model folder.
- Aurora clearly explains that changing this location updates the global model folder and does not move existing models.
- After deployment, Aurora verifies the expected model files and enters the workbench only when that check passes.

#### Clearer automatic updates

- Removed the custom Aurora installation-progress window that appeared alongside Inno Setup.
- Aurora still owns GitHub checks, resumable downloads, retry, SHA-256 verification, and safe handoff; only the standard Windows installer progress UI remains visible during replacement.
- Successful installation still reopens the new version while preserving settings, tasks, models, projects, media, outputs, and the chosen install directory.

## 1.0.0 — 2026-08-08

### 中文

#### 首个正式版本

- Aurora Audio Studio 进入 1.0 正式版，集中提供本地音乐生成、AI 配音与声音克隆、歌声转换、去人声与 AI 分轨、AI 扒谱以及视频字幕工作流。
- 保留可迁移 `.arr` 项目、持久任务中心、最近项目、模板、模型中心、维护诊断与四语言界面。

#### 可靠自动更新

- 更新下载支持断点续传和最多八次自动重试，网络中断后从已完成位置继续，不再每次从零开始。
- 更新请求固定使用 HTTP/1.1，改善部分代理、CDN 与 Windows TLS 环境下的长连接稳定性。
- 新增 Aurora 专属更新进度窗口；Inno 安装向导在后台静默完成覆盖，只有安装成功后才重新启动新版。
- 安装器采用同一 AppId 原位升级并保持单实例，保留设置、任务、模型、项目、素材、成品和用户选择的安装目录。
- 更新日志记录下载偏移、续传次数、校验结果、安装参数与失败堆栈，便于准确定位问题。

#### 图标与桌面体验

- 重新优化 A-wave 图标：保持清晰的 A 形轮廓，使用更克制、间距明确的压缩柱状波形。
- 桌面、开始菜单、任务栏、标题栏、安装器和卸载列表统一使用同一套多分辨率图标。
- 图标文件采用版本化路径，避免 Windows Shell 在覆盖升级后继续显示旧图标缓存。

#### 代码质量

- 按最小完整实现原则审计正式版代码，删除无效事件处理、重复条件、重复目录操作和未启用的 MSIX 模板脚手架。
- 保留更新恢复、完整性校验、任务串行化、旧项目兼容和安装交接等有明确现实用途的保护机制；功能、界面、交互逻辑与输出保持不变。

### English

#### First stable release

- Aurora Audio Studio reaches 1.0 with local music generation, AI voice and cloning, singing conversion, vocal removal and stem separation, transcription, and video subtitle workflows.
- Retains portable `.arr` projects, a persistent Task Center, recent projects, templates, Model Center, maintenance diagnostics, and a four-language interface.

#### Reliable automatic updates

- Update downloads now resume from partial files and retry automatically up to eight times instead of restarting from zero after a network interruption.
- Update requests use HTTP/1.1 to improve long-transfer reliability across some proxies, CDNs, and Windows TLS environments.
- Added a dedicated Aurora Updater progress window while Inno Setup performs the replacement silently in the background; the new version launches only after success.
- In-place, single-instance upgrades preserve settings, tasks, models, projects, media, outputs, and the selected installation directory.
- Update logs now record byte offsets, retry attempts, digest verification, installer arguments, and failure stacks.

#### Icon and desktop experience

- Refined the A-wave icon with a clearer A silhouette and a restrained, well-spaced compact column waveform.
- Unified desktop, Start menu, taskbar, title bar, installer, and uninstall-list icons across the same multi-resolution asset set.
- Versioned icon paths prevent Windows Shell from retaining stale icons after an in-place update.

#### Code quality

- Audited the stable codebase for the smallest complete implementation, removing inert event handlers, duplicate conditions, redundant directory operations, and disabled MSIX template scaffolding.
- Retained protections with active responsibilities, including resumable updates, integrity verification, serialized tasks, legacy project compatibility, and installer handoff; features, UI, interaction logic, and outputs remain unchanged.

## 0.9.9 — 2026-08-08

### 中文

#### 自动更新

- 修复启动自动检查与手动检查重叠时同时打开两个确认窗口、导致 WinUI 未处理异常并闪退的问题。
- 应用更新调整为每天首次启动时自动检查一次，同时保留“关于”页面的手动检查入口。
- 所有更新确认、模型安装、模型回退与模型卸载窗口统一进入同一弹窗队列，避免界面竞争。
- 新增跨页面全局更新进度，连续显示连接 GitHub、下载安装包、SHA-256 校验与安装程序交接状态。
- 下载和校验完成后由可见的安装进度窗口接管；只有安装成功后才以当前 Windows 用户身份自动启动新版。
- 自动更新继续保留个人设置、任务记录、模型、项目、源素材、成品与用户选择的安装目录。
- 新增客户端更新日志，记录目标版本、下载大小、校验摘要、安装参数和安装程序进程，便于诊断失败位置。
- 安装器改用同一 AppId 的原位覆盖升级，不再预先调用旧版卸载程序；并新增安装器单实例保护，避免重复启动造成升级冲突。

#### 界面与版本记录

- “检查更新”旁新增“更新日志”按钮，以可滚动窗口显示当前版本与最近四个版本的更新内容。
- 软件内版本记录完整支持简体中文、繁体中文、英语与日语，并随当前程序版本自动选择显示范围。
- 加粗应用图标的 A 形主笔画与柱状波形，扩大透明圆角，并同步更新桌面、任务栏、标题栏与安装器图标资源。

### English

#### Automatic updates

- Fixed an unhandled WinUI exception and crash caused by automatic and manual update checks opening two confirmation dialogs at the same time.
- App updates now run automatically on the first launch each day while retaining the manual check in About.
- Update, model-install, rollback, and uninstall confirmations now share one serialized dialog queue.
- Added global update progress across GitHub connection, installer download, SHA-256 verification, and installer handoff.
- After download and verification, a visible installer progress window takes over. Aurora relaunches as the signed-in Windows user only after installation succeeds.
- Automatic updates preserve settings, task history, models, projects, source media, outputs, and the user's chosen installation directory.
- Added client-side update logs for target version, download size, digest verification, installer arguments, and process handoff.
- Switched upgrades to an in-place install under the same AppId instead of invoking the previous uninstaller, and added a single-instance installer mutex to prevent duplicate upgrade conflicts.

#### Interface and release history

- Added a Release notes button beside Check for updates, showing the current release and four preceding releases in a scrollable dialog.
- In-app release notes support Simplified Chinese, Traditional Chinese, English, and Japanese and select their range from the running app version.
- Increased the weight and clarity of the A mark and column waveform, enlarged the transparent corner radius, and synchronized desktop, taskbar, title-bar, and installer icon assets.

## 0.9.8 — 2026-08-08

### 中文

#### 新增

- 模型中心新增 Faster-Whisper Small、Large v3 Turbo 与 Large v3 三档多语言字幕模型，均由用户确认后从 Hugging Face 按需下载。
- 模型列表新增用途、语言、预计下载量、显存建议、来源、许可、版本和本地路径，并支持按全部、已安装、默认及可选模型筛选。
- 全面启用新的深色圆角 A-wave 应用图标，并为 Windows 提供 16 至 256 像素的标准多分辨率 ICO。
- 安装向导的简体中文界面现显示 GPLv3 非正式中文译文，并明确保留英文原文的法律效力。

#### 改进与修复

- 修复“关于”页面检查更新结果始终显示英文的问题；成功、失败、安装包不完整、校验失败和安装启动提示现均适配简体中文、繁体中文、英语与日语。
- 更新检查失败时改为错误状态，不再误显示为成功提示。
- 新建项目文件扩展名由 `.aurora` 缩短为 `.arr`；旧 `.aurora` 项目仍可继续读取和使用。
- 保持现有默认模型套件不变，新增字幕模型不会自动下载，也不会替用户修改本地模型环境。

### English

#### Added

- Added optional Faster-Whisper Small, Large v3 Turbo, and Large v3 multilingual subtitle models, downloaded from Hugging Face only after explicit confirmation.
- Expanded Model Center with purpose, language coverage, estimated download size, VRAM guidance, source, license, version, local path, and focused filters.
- Adopted the new dark rounded A-wave application icon, including a standard multi-resolution Windows ICO from 16 to 256 pixels.
- Added an unofficial Simplified Chinese GPLv3 reading copy to the Chinese installer while retaining the original English license as the legally authoritative text.

#### Improved and fixed

- Localized About-page update results across Simplified Chinese, Traditional Chinese, English, and Japanese, including success, network failure, incomplete assets, checksum failure, and installer-start states.
- Update-check failures now use an error state instead of appearing as successful checks.
- New projects now use the shorter `.arr` extension; existing `.aurora` projects remain readable.
- Kept the default model suite unchanged. New subtitle models never download automatically or alter the user's local model environment.

## 0.9.7 — 2026-08-04

### 中文

#### 新增

- 模型中心新增五项可选引擎：Qwen3-TTS 0.6B Base、Qwen3-TTS 0.6B CustomVoice、F5-TTS、Demucs 4 与 Spotify Basic Pitch。
- 新增按需部署流程。Qwen3-TTS 权重通过 Hugging Face 获取；F5-TTS、Demucs 与 Basic Pitch 使用 uv 创建独立 Python 3.11 环境，避免污染系统 Python。
- 新增可选模型状态标识，明确区分默认配置与用户主动安装的扩展能力。

#### 改进

- 首页创作入口改为图标在前、文字在后的横向布局，提升扫读效率与卡片一致性。
- 左侧导航底部重新排序为“模型中心、维护与恢复、设置、关于”，“关于”固定为最后一项。
- 默认模型套件保持不变；缺少可选模型不会触发维护警告，也不会影响既有工作流的健康状态。
- 模型安装、运行与更新状态继续集中在 Aurora 内管理，安装前保留明确确认。

#### 安装与兼容性

- Aurora 不捆绑或预装新增模型。所有可选引擎均由用户在模型中心主动选择后下载和部署。
- 继续提供 Windows x64 标准安装版，默认安装到 Program Files，并保留现有安全升级、SHA-256 校验与个人配置保护机制。

### English

#### Added

- Added five optional engines to Model Center: Qwen3-TTS 0.6B Base, Qwen3-TTS 0.6B CustomVoice, F5-TTS, Demucs 4, and Spotify Basic Pitch.
- Added on-demand deployment. Qwen3-TTS weights are retrieved from Hugging Face, while F5-TTS, Demucs, and Basic Pitch use isolated Python 3.11 environments created with uv.
- Added explicit optional-model states so the default suite and user-selected extensions remain clearly separated.

#### Improved

- Reworked home workflow cards into an icon-first horizontal layout for faster scanning and more consistent alignment.
- Reordered the lower navigation to Model Center, Maintenance and Recovery, Settings, then About, with About fixed as the final item.
- Kept the existing default model suite unchanged. Missing optional engines no longer create maintenance warnings or affect the health of existing workflows.
- Kept model installation, runtime state, and updates inside Aurora, with explicit confirmation before installation.

#### Installation and compatibility

- Aurora does not bundle or preinstall the new engines. Optional models are downloaded and deployed only after the user selects them in Model Center.
- Distribution remains a standard Windows x64 installer with a Program Files default, verified updates, and settings-safe upgrades.

## 0.9.6 — 2026-08-04

### 中文

#### 安装与升级

- 默认安装位置调整为 `C:\Program Files\Aurora Audio Studio`，采用 Windows 标准的管理员授权流程。
- 从旧版本升级时，安装程序会先安全卸载旧版应用文件，再安装新版，避免残留文件与版本混用。
- 升级过程默认保留个人设置、任务记录、模型目录、项目、源素材和生成成品；若旧版卸载失败，安装会立即中止并保留原安装。
- 主动卸载时新增个人配置清理选项。用户可以保留配置以便后续重装，也可以删除设置、日志、任务记录、模型元数据和更新缓存。
- AI 模型、项目文件、源素材与生成成品不属于应用卸载范围，无论是否清理个人配置都不会被删除。

#### 改进

- 应用内更新继续对安装包执行 SHA-256 校验，并在启动安装前明确提示 Windows 管理员授权。
- 统一应用、安装程序、模型更新请求、关于页面及网站中的版本标识。

### English

#### Installation and upgrades

- The default installation directory is now `C:\Program Files\Aurora Audio Studio`, using the standard Windows administrator-consent flow.
- When upgrading from an older release, Setup safely removes the previous application files before installing the new version, preventing mixed or stale binaries.
- Upgrades preserve personal settings, task history, model locations, projects, source media, and generated outputs. If the previous version cannot be removed cleanly, Setup stops and leaves the existing installation intact.
- The uninstaller now offers a clear personal-data choice: keep settings for a future reinstall, or remove Aurora settings, logs, task history, model metadata, and update cache.
- AI models, project files, source media, and generated outputs are outside the application uninstall scope and are never deleted by this option.

#### Improved

- In-app updates continue to verify the installer with SHA-256 and now explain the Windows administrator prompt before installation begins.
- Version information is synchronized across the application, installer, model-update requests, About page, documentation, and website.

## 0.9.5 — 2026-08-04

### 中文

#### 新增

- 新增可迁移的 `.aurora` 项目文件，记录素材 SHA-256、模型选择、任务历史、参数与成品关系。
- 新增首页，集中展示创作模板、最近项目与正在执行的任务。
- 保留音乐生成、AI 配音与声音克隆、歌声转换、AI 分轨、MIDI 扒谱和视频字幕等既有工作流。

#### 稳定性与恢复

- 新增持久任务中心。任务按顺序使用本地 GPU，并支持安全取消、失败重试、结果打开与重启恢复。
- 设置、任务状态和项目文件改为原子写入，降低异常中断造成的数据损坏风险。
- 新增维护与恢复中心，集中检查 GPU、磁盘、模型与任务恢复状态，并提供安全模式、显存释放和诊断导出。

#### 改进

- 模型中心升级为完整生命周期管理，支持安装与修复、版本和健康状态、更新快照、回退及回收站卸载。
- 扩展简体中文、繁体中文、English 与日本語的本地化覆盖范围，并保留系统语言自动识别。

### English

#### Added

- Added portable `.aurora` project files that preserve source SHA-256 values, model choices, task history, parameters, and output lineage.
- Added a home surface for creative templates, recent projects, and active tasks.
- Preserved all existing music generation, AI speech and voice cloning, singing conversion, stem separation, MIDI transcription, and subtitle workflows.

#### Reliability and recovery

- Added a persistent Task Center with serialized local-GPU execution, safe cancellation, retry, output access, and restart recovery.
- Switched settings, task state, and project files to atomic writes to reduce corruption after unexpected interruptions.
- Added Maintenance and Recovery checks for GPU, storage, models, and task state, plus safe mode, VRAM release, and diagnostic export.

#### Improved

- Expanded Model Center into complete lifecycle management with install and repair, version and health status, update snapshots, rollback, and recycle-bin uninstall.
- Expanded Simplified Chinese, Traditional Chinese, English, and Japanese localization while retaining automatic system-language detection.

## 0.7.0 — 2026-08-03

### 中文

#### 新增

- 使用 .NET 10、WinUI 3、Windows App SDK 与 WebView2 重构为原生 Windows 桌面工作台。
- 采用全新的白色与薄荷绿视觉系统、紧凑 A 字波形图标和更大的默认窗口。
- 将音乐生成、配音、声音克隆与歌声转换的本地 Web 工作台直接嵌入 Aurora，不再依赖独立启动器窗口。

#### 创作与本地化

- 集中提供音乐创作、AI 配音与声音克隆、歌声克隆、AI 分轨、MIDI 扒谱、视频字幕和模型管理入口。
- 设置与关于固定在左栏底部；关于页包含作者名片、更新日志、版权信息、检查更新和诊断导出。
- 支持简体中文、繁体中文、English、日本語跟随系统语言，也可手动切换。

#### 安装与维护

- 新增 GitHub Release 自动更新：下载正式安装包和校验文件，通过 SHA-256 验证后执行安装。
- 模型管理器支持逐项与批量检查更新；Git 模型仅允许 fast-forward 更新，大型权重下载保留明确确认。
- 发布方式统一为标准 Windows x64 安装版，包含 GPL v3.0 协议、安装位置、进度、日志、桌面快捷方式与标准卸载流程。
- 官网和 README 使用统一的 Aurora 品牌系统重新设计；停止提供旧版 WinForms、旧视觉素材和便携版。

### English

#### Added

- Rebuilt Aurora as a native Windows desktop workbench with .NET 10, WinUI 3, Windows App SDK, and WebView2.
- Introduced a white-and-mint visual system, a compact A-wave icon, and a larger default window.
- Embedded local music, speech, voice-cloning, and singing-conversion web workbenches directly inside Aurora.

#### Workflows and localization

- Consolidated music, AI speech and voice cloning, singing conversion, stem separation, MIDI transcription, subtitles, and model management in one shell.
- Anchored Settings and About at the bottom of the navigation pane, with author details, release notes, copyright, update checks, and diagnostic export.
- Added system-aware Simplified Chinese, Traditional Chinese, English, and Japanese with manual language switching.

#### Installation and maintenance

- Added GitHub Release updates with installer and checksum downloads followed by mandatory SHA-256 verification.
- Added individual and batch model-update checks, fast-forward-only Git updates, and explicit confirmation for large weight downloads.
- Standardized distribution on a formal Windows x64 installer with GPL v3.0 acceptance, directory selection, progress, logs, desktop-shortcut choice, and a standard uninstall flow.
- Redesigned the website and README around one Aurora brand system and retired the legacy WinForms, visual assets, and portable distribution path.
