# Aurora Audio Studio 1.9.0 使用说明

完成一次分轨、扒谱或字幕任务时，Aurora 会在“处理记录目录”创建 `.arr` 记录文件。它只是保存源文件路径、模型、参数、任务与输出关系的轻量 JSON 清单，不是类似 PS、PR 的可编辑工程文件，也不会复制素材、模型或成品。旧版 `.aurora` 记录仍可继续读取。

- 从“首页”直接选择六个独立功能之一，或再次运行最近处理记录。
- 在“任务中心”查看排队、处理中、已完成和中断后可重试的任务。
- 在“模型中心”安装、修复、更新、回退或安全卸载模型；默认模型与可选扩展会明确区分。
- 启动异常时进入“维护与恢复”，开启安全模式并在确认预览后导出脱敏诊断包。

Aurora 1.9.0 强化六类任务的安装、输出校验、队列恢复与成品操作，并重做四语言切换。WebView2 用户数据继续保存到可写的 `%LOCALAPPDATA%\Aurora Audio Studio\WebView2`。引擎“文件齐全”与“短任务已验证”分开显示；具体实测范围见 [1.9.0 验收记录](../../docs/validation-1.9.0.md)。

Aurora 是一套 Windows 本地 AI 音频工作台。音乐生成、AI 配音、歌声克隆、去人声分轨、AI 扒谱和视频字幕，都可以从同一个窗口进入。

软件界面支持简体中文、繁体中文、English 和日本語，默认跟随 Windows 系统语言。

在设置中选择语言会立即生效并保存，不需要再点击底部的保存按钮；日语可直接切回简体中文。此操作不会保存尚未确认的目录修改。日语界面采用随软件携带的 Noto Sans JP，无需安装系统字体。Windows 文件选择对话框和原始诊断日志可能继续使用系统或上游语言。

结果可以试听、导出和复制路径；字幕编辑保存为副本，不覆盖原文件。MIDI 可查看有效音符数，试听和编辑仍需本机音乐软件。中断任务的“重试”是保留参数后重新执行，并非从中间推理步骤继续。

## 第一次使用

1. 从 GitHub Release 下载正式安装程序。
2. 允许 Windows 管理员授权，并阅读、同意 GNU GPL v3.0 协议。
3. 默认安装到 `C:\Program Files\Aurora Audio Studio`；也可以选择其他位置，并决定是否创建桌面快捷方式。
4. 打开 Aurora，直接从首页选择六个独立功能之一，不需要新建项目。
5. 进入功能后，再按当前任务提示添加素材、选择模型和确认输出位置。
6. 选择未安装的创作模型时，点击“自动安装模型”；确认目标目录、预计下载量和建议预留空间后才会下载或部署。

例如把模型位置设为 `D:\Creative Tools`，Aurora 会使用：

```text
D:\Creative Tools\LocalAI
```

AI 模型通常体积较大，建议使用空间充足的 SSD。

### 建议的第一次体验

如果只是想先确认 Aurora 的完整工作方式，不必一次安装所有模型：

1. 打开“视频 AI 字幕”，选择 Faster-Whisper Small。
2. 确认约 470 MB 的模型下载和安装位置。
3. 导入一段人声清晰的短视频，使用“推荐质量”生成 SRT。
4. 在“任务中心”查看过程，在“成品库”打开结果。

这个流程能一次验证模型安装、素材导入、任务记录和成品管理。需要其他功能时，再到“模型中心”按需安装对应模型。

## 创作入口

### 音乐创作

默认引擎为 ACE-Step 1.5 XL Turbo。MiniMax-Music3 可在模型管理中按需安装：确认约 27 GB 下载与约 55 GB 建议空间后，Aurora 自动配置独立 CUDA 环境并启用本地工作台；不会自动下载。

### AI 配音与声音克隆

用于生成旁白、对白或参考音色。干净、无背景音乐、无混响的参考录音通常效果更稳定。

### 歌声克隆

默认使用 Seed-VC 44.1k，在尽量保留旋律和唱法的前提下转换演唱音色。建议先用 AI 分轨获得较干净的人声。

### 去人声与 AI 分轨

需要纯人声和纯伴奏时选择“二轨”，Aurora 会使用专用 BS-RoFormer Vocals Revive V3e；需要鼓、贝斯、吉他、钢琴等独立轨道时选择“多轨”，再按速度或质量选择对应方案。

### AI 扒谱

TransKun V2 是默认钢琴扒谱引擎；YourMT3+ 适合多乐器内容，ByteDance Piano 作为经典钢琴模型保留，Basic Pitch 适合快速草稿。它们都输出可继续编辑的 MIDI。

### 视频 AI 字幕

Subtitle Edit 与 Faster-Whisper 用于语音识别、时间轴校对和 SRT 导出。快速、推荐和高质量分别对应 Small、Large v3 Turbo 与 Large v3；人名、术语、歌词和口音内容请人工复核。

## 模型与成品管理

- 已安装模型可直接进入工作台；未安装模型会先显示安装确认，完成并通过检查后自动进入工作台。
- MiniMax-Music3、Qwen3-TTS 0.6B、F5-TTS、Demucs 4 与 Spotify Basic Pitch 均按需提供；TransKun V2 是新的默认钢琴扒谱引擎。
- Qwen3-TTS 权重从 Hugging Face 获取；F5-TTS、Demucs 与 Basic Pitch 使用 uv 创建独立 Python 3.11 环境。
- 可选模型不会随 Aurora 安装包预装，未安装时也不会触发维护警告。
- 模型管理页可检查单个模型或全部模型的更新。
- 大型模型权重下载前会保留明确确认。
- 成品位置可在设置中修改，默认整理到桌面的 `AI工作流`。
- 点击“结束当前引擎”会停止 Aurora 启动的本地后端并释放资源。
- Aurora 不开机自启动。

## 应用更新

Aurora 每天首次启动时会自动检查一次，也可以在“关于”页随时点击“检查更新”。如果 GitHub 上有更新，确认后会显示全局下载和 SHA-256 校验进度；随后只显示 Windows 标准安装进度窗口。只有安装成功后才自动打开新版。个人设置、任务记录、模型、处理记录、成品和原安装目录都会保留。如果已经是最新版，会直接提示无需更新。“检查更新”旁的“更新日志”可查看当前和最近四个版本的更新内容。

## 卸载

请从 Windows“已安装的应用”或开始菜单运行标准卸载程序。卸载时可以选择保留个人配置，或删除设置、日志、任务记录、模型元数据和更新缓存。AI 模型、处理记录、生成成品与个人素材始终保留。

## 使用与版权提醒

请确认你有权使用输入的声音、歌曲、视频与参考素材。Aurora 不包含第三方模型权重；各模型和工具继续遵循各自的许可证、模型卡与使用条款。

---

## English quick start

Aurora Audio Studio is a local-first Windows workbench for music generation, speech and voice cloning, singing conversion, stem separation, MIDI transcription, and video subtitles.

1. Download the standard x64 installer from GitHub Releases.
2. Approve the Windows administrator prompt and accept the GNU GPL v3.0 license.
3. Aurora defaults to `C:\Program Files\Aurora Audio Studio`; choose another directory if needed and select the desktop-shortcut option.
4. Open Settings and choose model and output folders.
5. Pick a workflow and install only the models you need.

Aurora supports Simplified Chinese, Traditional Chinese, English, and Japanese UI. It does not start with Windows or bundle model weights. Uninstall can optionally remove Aurora settings and history, but never models, processing records, source media, or outputs.
