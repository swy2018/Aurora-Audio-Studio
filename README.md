<div align="center">
  <img src="docs/assets/aurora-icon.png" width="104" alt="Aurora Audio Studio">
  <h1>Aurora Audio Studio</h1>
  <p><strong>让声音创作，回到创作本身。</strong></p>
  <p>面向 Windows 的本地 AI 音频创作工作台</p>
  <p>
    <a href="https://swy2018.github.io/Aurora-Audio-Studio/"><img alt="官方网站" src="docs/assets/readme-button-website.svg"></a>
    <a href="https://github.com/swy2018/Aurora-Audio-Studio/releases/latest"><img alt="下载 Aurora Audio Studio 1.5.0" src="docs/assets/readme-button-download.svg"></a>
    <a href="CHANGELOG.md"><img alt="更新日志" src="docs/assets/readme-button-changelog.svg"></a>
    <a href="#english"><img alt="English" src="docs/assets/readme-button-english.svg"></a>
  </p>
</div>

![Aurora Audio Studio 音乐创作工作台](docs/assets/aurora-workbench-music.png)

Aurora 把音乐生成、AI 配音、声音克隆、歌声转换、音轨分离、MIDI 扒谱和视频字幕集中到同一个本地入口。六个功能互相独立，可直接开始当前任务，不再需要手动管理多个启动器、端口和结果目录。

## 1.5.0 带来了什么

### 更新不再只告诉你“有新版”

- 检查完成后，有新版的模型或组件会直接显示“更新”，并在页首提供带数量的“更新全部”。
- 批量更新按顺序处理，成功项立即变为最新；失败项保留为可更新状态，可关闭占用程序后直接重试。
- Aurora 会在替换文件前检测仍在运行的组件，不会强制关闭 Subtitle Edit 等工具，避免未保存内容丢失。

### 模型中心更容易看懂

- 每个模型改为分层卡片，名称、用途、功能、语言、版本、许可、状态、本地路径和操作区不再挤在同一行。
- 本地路径过长时会省略显示，但鼠标悬停仍可查看完整位置。
- 更新、回退和卸载动作保持四语言，并补充屏幕阅读器名称与状态说明。
## 工作流

| 工作流 | 默认引擎 | 可选引擎 | 主要输出 |
|---|---|---|---|
| 音乐创作 | ACE-Step 1.5 XL Turbo | MiniMax-Music3 | 完整歌曲、纯音乐与草稿 |
| AI 配音与声音克隆 | Qwen3-TTS 1.7B | Qwen3-TTS 0.6B、F5-TTS | 配音与克隆音频 |
| 歌声克隆 | Seed-VC 44.1k | 按模型中心扩展 | 歌声与音色转换 |
| 去人声 / AI 分轨 | BS-RoFormer-SW 6-Stem | Demucs 4 | 独立 WAV 音轨 |
| AI 扒谱 | TransKun V2 | YourMT3+、ByteDance Piano、Basic Pitch | 标准 MIDI |
| 视频 AI 字幕 | Faster-Whisper XXL | Small、Large v3 Turbo、Large v3 | SRT 与转写数据 |

模型与第三方工具保留各自上游许可。模型大小、显存建议、语言能力和来源会在模型中心逐项显示。

## 安装

### 系统要求

- Windows 10 或 Windows 11 x64
- 建议使用 NVIDIA RTX 显卡
- 模型根据实际工作流单独下载
- 大型模型安装前请预留模型中心建议的磁盘空间

### 标准安装

1. 打开 [Releases](https://github.com/swy2018/Aurora-Audio-Studio/releases/latest)。
2. 下载 `Aurora-Audio-Studio-1.5.0-Setup-x64.exe` 和同名 `.sha256` 文件。
3. 运行安装程序，阅读并接受 GNU GPL v3.0，选择安装位置和桌面快捷方式。
4. 首次打开 Aurora，直接选择需要的功能；需要时再确认模型、处理记录和成品目录。

默认安装位置是 `C:\Program Files\Aurora Audio Studio`。覆盖升级会保留用户设置、任务记录、模型、处理记录和成品；卸载时可选择是否清除个人配置。

### 第一次使用建议

1. 在首页直接选择音乐、配音、歌声、分轨、扒谱或字幕功能，不需要先新建项目。
2. Aurora 会根据当前功能提示所需模型；下载前会显示体积、目标位置和可用空间。
3. 导入素材的功能会在进入后提示添加文件；音乐、配音和歌声工作台可直接选择引擎进入。
4. 分轨、扒谱和字幕完成后，可在“最近处理记录”再次处理，在“成品库”打开输出。

## 数据与隐私

Aurora 本身不提供云端生成服务。素材与生成结果留在用户指定的本地目录。应用更新和模型部署会连接 GitHub、Hugging Face 或模型注明的官方来源。

- [隐私说明](PRIVACY.md)
- [代码签名政策](CODE_SIGNING_POLICY.md)
- [GNU GPL v3.0](LICENSE)
- [反馈问题或提出建议](https://github.com/swy2018/Aurora-Audio-Studio/issues/new/choose)

## 开发

Aurora 桌面端使用 .NET 10、WinUI 3 和 Windows App SDK 构建，官网使用原生 HTML、CSS 与 ES Modules，可直接部署到 GitHub Pages。

```powershell
dotnet restore .\work\audio-studio\AuroraAudioStudio\AuroraAudioStudio.csproj --runtime win-x64
dotnet build .\work\audio-studio\AuroraAudioStudio\AuroraAudioStudio.csproj -c Release -p:Platform=x64
dotnet publish .\work\audio-studio\AuroraAudioStudio\AuroraAudioStudio.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o .\publish\Aurora-Audio-Studio-1.5.0
```

运行回归检查：

```powershell
dotnet run --project .\work\audio-studio\AuroraAudioStudio.UpdateFlowTests\AuroraAudioStudio.UpdateFlowTests.csproj -- .\work\audio-studio\AuroraAudioStudio.iss
```

## 项目结构

```text
docs/                                      官方网站
work/audio-studio/AuroraAudioStudio/       WinUI 3 桌面端
work/audio-studio/AuroraAudioStudio.iss    Inno Setup 安装脚本
model-manifest.json                        固定下载包的可验证模型更新清单
CHANGELOG.md                               中英双语更新日志
```

## 许可

Aurora Audio Studio 以 [GNU General Public License v3.0](LICENSE) 开源。模型、运行时和第三方组件遵循各自许可。

---

<a id="english"></a>

## English

Aurora Audio Studio is a local AI audio production workspace for Windows. Its six independent features provide direct entry points for music generation, voice cloning, singing conversion, stem separation, MIDI transcription, and video subtitles.

### What version 1.5.0 adds

- Model Management now turns detected updates into direct Update actions and exposes a counted Update all flow.
- Batch updates run sequentially; successful items become current immediately, while failures remain retryable.
- Aurora detects running components before file replacement and never force-closes tools such as Subtitle Edit, protecting unsaved work.
- Model cards now separate purpose, workflow, languages, version, license, status, local path, and actions for faster scanning.
- Long paths remain available through tooltips, and actions retain four-language and screen-reader support.
### Local by design

Aurora does not operate a cloud generation service. Media and generated output remain in the directories chosen by the user. App updates and model deployment connect only to GitHub, Hugging Face, or the official source identified for each model.

### Install

1. Open the latest [Release](https://github.com/swy2018/Aurora-Audio-Studio/releases/latest).
2. Download `Aurora-Audio-Studio-1.5.0-Setup-x64.exe` and its `.sha256` file.
3. Run Setup, review GNU GPL v3.0, and choose the destination and shortcut options.
4. Choose a feature on first launch; confirm model, processing-record, and output folders only when needed.

Aurora defaults to `C:\Program Files\Aurora Audio Studio`. In-place upgrades preserve settings, task history, models, processing records, and output. Uninstall offers an optional personal-configuration cleanup.

### Technology

- .NET 10
- WinUI 3
- Windows App SDK
- Inno Setup
- Native HTML, CSS, and ES Modules for the website

Aurora Audio Studio is licensed under the [GNU General Public License v3.0](LICENSE). Models, runtimes, and third-party components retain their own licenses.
