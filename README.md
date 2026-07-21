# Aurora Audio Studio

Windows 本地 AI 音频制作工作室。面向音乐人、翻唱作者、配音作者、视频创作者和不懂编程的普通用户。

Current version: `0.8.0`

---

## 中文说明

Aurora Audio Studio 是一个本地优先的 Windows 桌面软件。它把常用的 AI 音频工作流集中到一个界面里：音乐创作、AI 配音、声音克隆、歌声克隆、去人声 / 分轨、AI 扒谱和视频字幕。

### 主要功能

- 音乐创作：默认入口为 ACE-Step 1.5。
- AI 配音与说话声音克隆：默认入口为 IndexTTS2。
- 歌声克隆：默认入口为 Seed-VC 44.1k。
- 去人声 / AI 分轨：输出纯人声 Vocal 和纯伴奏 Instrumental。
- AI 扒谱：生成 MIDI 和音符事件表。
- 视频 AI 字幕：打开 Subtitle Edit / Faster-Whisper 类工作流。
- 软件内置中文 / English 切换。
- 可选择模型安装位置和成品输出位置。
- 设置保存在系统 AppData，不在 exe 同目录乱生成配置文件。
- 不会开机自启动；只有启动模型时才占用显存。

### 普通用户怎么用

1. 打开 `Aurora Audio Studio.exe`。
2. 从左侧选择功能。
3. 在模型下拉框里选择模型。
4. 如果模型没安装，主按钮会变成“安装此模型”。
5. 选择安装位置，Aurora 会在你选的位置里创建 `LocalAI` 文件夹。
6. 如有需要，可设置输出位置；否则默认输出到桌面 `AI工作流`。

### 文件夹规则

- 设置文件：`%LOCALAPPDATA%\Aurora Audio Studio\settings.json`
- 默认成品目录：桌面 `AI工作流`
- 模型安装目录：用户选择的位置下面的 `LocalAI`

例如选择 `D:\AI Tools`，实际模型目录会是：

```text
D:\AI Tools\LocalAI
```

### 推荐环境

- Windows 10 / 11 x64
- NVIDIA 显卡推荐用于本地 AI 模型
- Microsoft Edge WebView2 Runtime
- Git 和 Python：用于部分模型安装器
- 如果使用自包含发布版，不需要用户额外安装 .NET 运行库

### 重要说明

Aurora 不内置大模型权重，也不重新分发第三方 AI 项目。它提供的是统一启动器、安装入口和输出整理。用户需要自行遵守所安装模型、工具和素材的许可证、模型说明与使用条款。

---

## English

Aurora Audio Studio is a local-first Windows desktop app for AI audio production. It gives musicians, cover creators, voice creators, video makers, and non-programmers one clean place to start local music, voice, stem separation, MIDI transcription, and subtitle workflows.

### Features

- Music generation with ACE-Step 1.5 as the default entry.
- AI voice and speaking voice cloning with IndexTTS2 as the default entry.
- Singing voice conversion with Seed-VC 44.1k as the default entry.
- Vocal removal / AI stem separation, producing clean Vocal and Instrumental outputs.
- MIDI transcription for melody and note-event extraction.
- Video subtitle workflow through Subtitle Edit / Faster-Whisper style tools.
- Built-in Chinese / English UI switch.
- User-selectable model install location and output location.
- Settings are stored in local AppData, not beside the executable.
- No startup auto-run. Models only use VRAM when launched.

### How to use

1. Open `Aurora Audio Studio.exe`.
2. Choose a feature from the left sidebar.
3. Pick a model from the dropdown.
4. If the model is missing, the main button changes to “Install this model”.
5. Choose an install location. Aurora creates a `LocalAI` folder inside it.
6. Set an output folder if needed. Otherwise Aurora uses the desktop AI workflow folder.

### Folder behavior

- Settings: `%LOCALAPPDATA%\Aurora Audio Studio\settings.json`
- Default output folder: the desktop AI workflow folder
- Model root: `LocalAI` under the location chosen by the user

For example, choosing `D:\AI Tools` creates:

```text
D:\AI Tools\LocalAI
```

### Recommended environment

- Windows 10 / 11 x64
- NVIDIA GPU recommended for local AI model workflows
- Microsoft Edge WebView2 Runtime
- Git and Python for optional model installers
- The self-contained release does not require users to install the .NET runtime separately

### Important note

Aurora does not include model weights and does not redistribute third-party AI projects. It provides a polished launcher and installation entry points. Users are responsible for respecting the licenses, model cards, and usage terms of the tools and models they install.

---

## Build from source

```powershell
dotnet restore .\work\audio-studio\AIAudioStudio.csproj
dotnet build .\work\audio-studio\AIAudioStudio.csproj -c Release
```

Self-contained single-file publish:

```powershell
dotnet publish .\work\audio-studio\AIAudioStudio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish\Aurora
```

## Repository layout

```text
work/audio-studio/
  Program.cs
  AIAudioStudio.csproj
  app.manifest
  assets/
  README-给音乐人的使用说明.md
```

## Public release checklist

- App starts on a clean Windows 10 / 11 x64 machine.
- UI can switch between Chinese and English.
- Settings are created in `%LOCALAPPDATA%`.
- Output directory selection works.
- Model install location creates a `LocalAI` folder under the selected directory.
- Missing models remain selectable and expose an install action.
- Closing the app does not leave model processes running unexpectedly.
- Release package does not include local model weights, caches, personal paths, generated outputs, or credentials.

## License

MIT License. See [LICENSE](LICENSE).

MIT was chosen because Aurora is a local launcher and workflow shell. The license is friendly for personal use, modification, redistribution, and commercial integration. Third-party models and tools still keep their own licenses; MIT only covers the Aurora source code in this repository.
