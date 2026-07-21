# Aurora Audio Studio / 极光音频工作室

Aurora Audio Studio is a Windows desktop launcher for local AI audio production. It is designed for musicians, cover creators, voice creators, video makers, and non-programmers who want one clean place to start local music, voice, vocal separation, MIDI transcription, and subtitle workflows.

极光音频工作室是一个 Windows 本地 AI 音频制作启动器。它面向学音乐、做翻唱、做配音、做视频和不想碰命令行的用户，把本地音乐生成、AI 配音、歌声克隆、去人声分轨、AI 扒谱和字幕工具放进同一个桌面软件。

Current version / 当前版本：`0.8.0`

## What it does / 主要功能

- Music generation with ACE-Step 1.5 as the default local model.
- AI voice and speaking voice cloning with IndexTTS2 as the default entry.
- Singing voice conversion with Seed-VC 44.1k as the default entry.
- Vocal removal / AI stem separation, producing Vocal and Instrumental outputs.
- MIDI transcription with Basic Pitch style workflows.
- Video subtitle workflow through Subtitle Edit / Faster-Whisper style tools.
- Chinese / English UI switch inside the app.
- User-selectable model install location and output location.
- Config is stored in the user's local AppData, not beside the executable.
- No startup auto-run. Models only use VRAM when launched.

---

- 音乐创作：默认使用 ACE-Step 1.5 本地模型。
- AI 配音与说话声音克隆：默认入口为 IndexTTS2。
- 歌声克隆：默认入口为 Seed-VC 44.1k。
- 去人声 / AI 分轨：输出纯人声 Vocal 和纯伴奏 Instrumental。
- AI 扒谱：生成 MIDI 和音符事件表。
- 视频 AI 字幕：通过 Subtitle Edit / Faster-Whisper 类工作流处理。
- 软件内置中文 / English 切换。
- 可选择模型安装位置和成品输出位置。
- 配置保存在系统 AppData，不在 exe 同目录乱生成文件。
- 不会开机自启动；只有启动模型时才占用显存。

## Important note / 重要说明

Aurora does not include model weights and does not redistribute third-party AI projects. It provides a polished launcher and installation entry points. Users are responsible for respecting the licenses, model cards, and usage terms of the tools and models they install.

Aurora 本身不内置大模型权重，也不重新分发第三方 AI 项目。它提供的是统一启动器、安装入口和输出整理。用户需要自行遵守所安装模型、工具和素材的许可证、模型说明与使用条款。

## Folder behavior / 文件夹规则

- Settings / 设置：`%LOCALAPPDATA%\Aurora Audio Studio\settings.json`
- Default output / 默认成品目录：`Desktop\AI工作流`
- Model root after choosing install location / 选择安装位置后：`<selected folder>\LocalAI`

For example, if a user chooses `D:\AI Tools`, Aurora will create:

如果用户选择 `D:\AI Tools`，Aurora 会创建：

```text
D:\AI Tools\LocalAI
```

## Recommended requirements / 推荐环境

- Windows 10 / 11 x64
- NVIDIA GPU recommended for local AI model workflows
- Microsoft Edge WebView2 Runtime
- Git and Python for optional model installers
- .NET Desktop Runtime if using framework-dependent builds; not needed for self-contained single-file releases

## How normal users use it / 普通用户怎么用

1. Open `Aurora Audio Studio.exe`.
2. Choose a feature from the left sidebar.
3. Pick a model from the model dropdown.
4. If the model is not installed, the main button becomes “Install this model / 安装此模型”.
5. Choose an install location. Aurora creates a `LocalAI` folder there.
6. Choose an output folder if needed. Otherwise outputs go to `Desktop\AI工作流`.

---

1. 打开 `Aurora Audio Studio.exe`。
2. 从左侧选择功能。
3. 在模型下拉框里选择模型。
4. 如果模型没安装，主按钮会变成“安装此模型”。
5. 选择安装位置，Aurora 会在里面创建 `LocalAI` 文件夹。
6. 如有需要，可设置输出位置；否则默认输出到桌面 `AI工作流`。

## Build from source / 从源码构建

```powershell
dotnet restore .\work\audio-studio\AIAudioStudio.csproj
dotnet build .\work\audio-studio\AIAudioStudio.csproj -c Release
```

Self-contained single-file publish / 自包含单文件发布：

```powershell
dotnet publish .\work\audio-studio\AIAudioStudio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish\Aurora
```

## Repository layout / 仓库结构

```text
work/audio-studio/
  Program.cs                 Main Windows Forms application / 主程序
  AIAudioStudio.csproj        Project file / 项目文件
  app.manifest                Windows app manifest / Windows 清单
  assets/                     Icons and UI artwork / 图标和界面素材
  README-给音乐人的使用说明.md   Non-programmer user guide / 给普通创作者看的说明
```

## Public release checklist / 公开发布检查清单

- App starts on a clean Windows user account.
- UI can switch between Chinese and English.
- Settings are created in `%LOCALAPPDATA%`.
- Output directory selection works.
- Model install location creates a `LocalAI` folder under the selected directory.
- Missing models remain selectable and expose an install action.
- Closing the app does not leave model processes running unexpectedly.
- Release package does not include local model weights, caches, personal paths, generated outputs, or credentials.

---

- 在干净 Windows 用户环境可启动。
- UI 可以在中文和英文之间切换。
- 设置保存在 `%LOCALAPPDATA%`。
- 输出目录选择可用。
- 模型安装位置会在用户选择的目录下创建 `LocalAI`。
- 未安装模型仍可选择，并提供安装入口。
- 关闭软件后不会异常残留模型进程。
- 发布包不包含本地模型权重、缓存、个人路径、生成成品或凭据。

## License / 开源协议

MIT License. See [LICENSE](LICENSE).

选择 MIT 的原因：Aurora 是一个本地启动器和工作流外壳，MIT 对个人使用、二次开发、商业集成都比较友好，最适合让朋友或社区自由改造。但第三方模型和工具仍然受它们各自的许可证约束，MIT 只覆盖 Aurora 这部分源码。
