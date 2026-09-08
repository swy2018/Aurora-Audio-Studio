<div align="center">
  <img src="docs/assets/aurora-icon.png" width="104" alt="Aurora Audio Studio">
  <h1>Aurora Audio Studio</h1>
  <p><strong>让声音创作，回到创作本身。</strong></p>
  <p>面向 Windows 的本地 AI 音频创作工作台</p>
  <p>
    <a href="https://swy2018.github.io/Aurora-Audio-Studio/"><img alt="官方网站" src="docs/assets/readme-button-website.svg"></a>
    <a href="https://github.com/swy2018/Aurora-Audio-Studio/releases/latest"><img alt="下载 Aurora Audio Studio 1.9.9" src="docs/assets/readme-button-download.svg"></a>
    <a href="CHANGELOG.md"><img alt="更新日志" src="docs/assets/readme-button-changelog.svg"></a>
    <a href="#english"><img alt="English" src="docs/assets/readme-button-english.svg"></a>
  </p>
</div>

![Aurora Audio Studio 音乐创作工作台](docs/assets/aurora-workbench-music.png)

Aurora 把音乐生成、AI 配音、声音克隆、歌声转换、音轨分离、MIDI 扒谱和视频字幕集中到同一个本地入口。六个功能互相独立，可直接开始当前任务，不再需要手动管理多个启动器、端口和结果目录。

Mac 用户：Apple Silicon 本地版的安装与功能范围请看 [Mac 使用说明](docs/macOS-user-guide.md)，开发者构建及公证流程见 [Mac 发布说明](docs/macOS-release.md)。下文的 Windows 默认引擎和发布链接不代表 Mac 版已公开发布。

## 1.9.9：可靠性与成果操作

<!-- release-notes-zh:start -->
- Windows 与 Mac 统一版本号；同一个 Release 提供各自安装包与独立 SHA-256 校验文件。
- 新增正式版与测试版更新通道，默认正式版。测试版需主动选择，切回正式版不自动降级。
- 修复混合平台发布的安装包选择与校验；支持 Beta 数字排序及 Beta 升级到后续正式版。
- Mac 接入六类本地音频工作流与官方字幕编辑器；原有模型、设置及作品继续保留。
<!-- release-notes-zh:end -->

“文件齐全”不代表已完成推理。模型中心在真实任务成功后记录当前模型版本、时间和设备。未接入工作台的模型明确标为“仅模型管理”，MiniMax 等未在本机验收的可选模型不承诺实测通过。

队列恢复指保留素材与参数后重新执行，不是从中间推理步骤继续；断点下载限同一修订及支持续传的上游。升级在候选目录完成检查，保留旧文件/环境供回退，不自动删除旧模型。

音频可在成品库试听和导出；MIDI 显示音符信息并交给默认音乐软件编辑，Aurora 不内置 MIDI 合成器；SRT 可编辑后保存副本，已安装 Subtitle Edit 时也可直接交给它校对。

## 工作流

| 工作流 | 默认引擎 | 可选引擎 | 主要输出 |
|---|---|---|---|
| 音乐创作 | ACE-Step 1.5 XL Turbo | MiniMax-Music3 | 完整歌曲、纯音乐与草稿 |
| AI 配音与声音克隆 | Qwen3-TTS 1.7B | Qwen3-TTS 0.6B、F5-TTS | 配音与克隆音频 |
| 歌声克隆 | Seed-VC 44.1k | 按模型中心扩展 | 歌声与音色转换 |
| 去人声 / AI 分轨 | BS-RoFormer Vocals Revive V3e（二轨） | BS-RoFormer-SW 六轨、Demucs 4 | 独立 WAV 音轨 |
| AI 扒谱 | TransKun V2 | YourMT3+、ByteDance Piano、Basic Pitch | 标准 MIDI |
| 视频 AI 字幕 | Faster-Whisper XXL | Small、Large v3 Turbo、Large v3 | SRT 与转写数据 |

模型与第三方工具保留各自上游许可。模型大小、显存建议、语言能力和来源会在模型中心逐项显示。

<!-- model-capabilities:start -->
<details>
<summary>全部模型接入状态 / All model interfaces</summary>

| 模型 / Model | 操作入口 / Interface | 上游许可 / License |
|---|---|---|
| ACE-Step 1.5 XL Turbo | 嵌入式工作台 / Embedded | Apache-2.0 |
| MiniMax-Music3 | 嵌入式工作台 / Embedded | MiniMax-Music3 Community License |
| HeartMuLa 3B · Happy New Year | 仅下载管理 / Download only | Apache-2.0 |
| Qwen3-TTS 1.7B · 声音克隆 | 嵌入式工作台 / Embedded | Apache-2.0 |
| Qwen3-TTS 1.7B · 专业音色 | 嵌入式工作台 / Embedded | Apache-2.0 |
| Qwen3-TTS 1.7B · 音色设计 | 嵌入式工作台 / Embedded | Apache-2.0 |
| Qwen3-TTS 0.6B · 轻量声音克隆 | 嵌入式工作台 / Embedded | Apache-2.0 |
| Qwen3-TTS 0.6B · 轻量专业音色 | 嵌入式工作台 / Embedded | Apache-2.0 |
| F5-TTS · 多语言声音克隆 | 嵌入式工作台 / Embedded | MIT code / CC-BY-NC-4.0 weights (noncommercial) |
| IndexTTS-2.5 · 可控配音 | 仅下载管理 / Download only | Bilibili Model License |
| Seed-VC 44.1k | 嵌入式工作台 / Embedded | Review upstream license |
| SoulX-Singer-SVC · 零样本歌声转换 | 仅下载管理 / Download only | Apache-2.0 |
| BS-RoFormer-SW · 多轨高质量 | 原生任务 / Native | Review upstream license |
| BS-RoFormer Vocals Revive V3e · 二轨 | 原生任务 / Native | Review upstream license |
| Demucs 4 · 通用四轨分离 | 原生任务 / Native | MIT |
| YourMT3+ Multi-Instrument | 原生任务 / Native | Review upstream license |
| TransKun V2 · 钢琴扒谱 | 原生任务 / Native | MIT |
| ByteDance Piano · 经典模型 | 原生任务 / Native | Review upstream license |
| Spotify Basic Pitch · 轻量扒谱 | 原生任务 / Native | Apache-2.0 |
| Faster-Whisper XXL | 共享组件 / Runtime | MIT |
| Faster-Whisper Small | 原生任务 / Native | MIT |
| Faster-Whisper Large v3 Turbo | 原生任务 / Native | MIT |
| Faster-Whisper Large v3 | 原生任务 / Native | MIT |
| Qwen3-ASR 0.6B · 快速识别 | 仅下载管理 / Download only | Apache-2.0 |
| Qwen3-ASR 1.7B · 高质量识别 | 仅下载管理 / Download only | Apache-2.0 |
| Qwen3 ForcedAligner 0.6B · 精确时间轴 | 仅下载管理 / Download only | Apache-2.0 |
| Subtitle Edit | 外部编辑器 / External editor | GPL-3.0 |

</details>
<!-- model-capabilities:end -->

## 安装

### 系统要求

- Windows 10 或 Windows 11 x64
- 建议使用 NVIDIA RTX 显卡
- 模型根据实际工作流单独下载
- 大型模型安装前请预留模型中心建议的磁盘空间

### 标准安装

1. 打开 [Releases](https://github.com/swy2018/Aurora-Audio-Studio/releases/latest)。
2. 下载 `Aurora-Audio-Studio-1.9.9-Setup-x64.exe` 和同名 `.sha256` 文件。
3. 运行安装程序，阅读并接受 GNU GPL v3.0，选择安装位置和桌面快捷方式。
4. 首次打开 Aurora，直接选择需要的功能；需要时再确认模型、处理记录和成品目录。

默认安装位置是 `C:\Program Files\Aurora Audio Studio`。覆盖升级会保留用户设置、任务记录、模型、处理记录和成品；卸载时可选择是否清除个人配置。

### 第一次使用建议

1. 在首页直接选择音乐、配音、歌声、分轨、扒谱或字幕功能，不需要先新建项目。
2. Aurora 会根据当前功能提示所需模型；下载前会显示体积、目标位置和可用空间。
3. 导入素材的功能会在进入后提示添加文件；音乐、配音和歌声工作台可直接选择引擎进入。
4. 先处理一份 5–10 秒短样本。在成品库检查实际结果；分轨、扒谱和字幕可从原记录恢复参数后再次处理。

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
dotnet publish .\work\audio-studio\AuroraAudioStudio\AuroraAudioStudio.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o .\publish\Aurora-Audio-Studio-1.9.9
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

日语字体 Noto Sans JP 随附 [OFL-1.1 许可](work/audio-studio/AuroraAudioStudio/Assets/Fonts/NotoSansJP-OFL.txt)。[F5-TTS 官方模型权重](https://huggingface.co/SWivid/F5-TTS) 使用 CC-BY-NC-4.0，不能把代码的 MIT 许可理解为模型允许商用。

---

<a id="english"></a>

## English

Aurora Audio Studio is a local AI audio production workspace for Windows. Its six independent features provide direct entry points for music generation, voice cloning, singing conversion, stem separation, MIDI transcription, and video subtitles.

### Version 1.9.9: reliability and results

<!-- release-notes-en:start -->
- Windows and Mac share one version and Release, with separate installers and SHA-256 checksum files.
- Add Stable and Beta update channels, defaulting to Stable. Betas are opt-in; switching back never silently downgrades.
- Select and verify the exact platform package, order beta revisions numerically, and upgrade betas to later stable releases.
- Bring six local audio workflows and the official subtitle editor to Mac while preserving models, settings, and results.
<!-- release-notes-en:end -->

Read the [capability matrix](docs/capabilities.json) and [acceptance report](docs/validation-1.9.9.md) for exact scope. Download-only models are not runnable workbenches. Retrying an interrupted task restarts inference from its saved inputs and parameters. MIDI editing/playback requires your own music application; audio playback and subtitle-copy editing are available in Results.

### Local by design

Aurora does not operate a cloud generation service. Media and generated output remain in the directories chosen by the user. App updates and model deployment connect only to GitHub, Hugging Face, or the official source identified for each model.

### Install

1. Open the latest [Release](https://github.com/swy2018/Aurora-Audio-Studio/releases/latest).
2. Download `Aurora-Audio-Studio-1.9.9-Setup-x64.exe` and its `.sha256` file.
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

Noto Sans JP includes its OFL-1.1 license. [F5-TTS model weights](https://huggingface.co/SWivid/F5-TTS) are CC-BY-NC-4.0; the code's MIT license does not grant commercial rights to the pretrained weights.
