<div align="center">
  <img src="docs/assets/aurora-icon.png" width="104" alt="Aurora Audio Studio">
  <h1>Aurora Audio Studio 2.0</h1>
  <p><strong>让声音创作，回到创作本身。</strong></p>
  <p>Windows 与 macOS 的本地 AI 音频创作工作台</p>
  <p>
    <a href="https://swy2018.github.io/Aurora-Audio-Studio/"><img width="184" height="36" alt="官方网站" src="docs/assets/readme-button-website.svg"></a>
    <a href="https://swy2018.github.io/Aurora-Audio-Studio/#download"><img width="184" height="36" alt="Windows / macOS 下载" src="docs/assets/readme-button-platforms.svg"></a>
    <a href="https://github.com/swy2018/Aurora-Audio-Studio/releases"><img width="184" height="36" alt="正式版与测试版" src="docs/assets/readme-button-releases.svg"></a>
  </p>
  <p>
    <a href="CHANGELOG.md"><img width="184" height="36" alt="更新日志" src="docs/assets/readme-button-history.svg"></a>
    <a href="#english"><img width="184" height="36" alt="English README" src="docs/assets/readme-button-language.svg"></a>
  </p>
</div>

![Aurora Mac 实机首页](docs/assets/mac-2.0-home.png)

Aurora 提供正式版与 Beta 两个更新通道，支持 Windows 与 macOS。下方为 Mac 客户端实机截图；不同平台保留各自的系统控件。

Aurora 将音乐生成、配音与声音克隆、歌声转换、音轨分离、MIDI 扒谱和字幕制作整合到一个本地工作台。选择所需功能即可开始，素材、任务与成品集中管理。

## 选择你的平台

| | Windows | macOS |
|---|---|---|
| 系统 | Windows 10 / 11 x64 | macOS 26+ · Apple Silicon |
| 安装包 | `Aurora-Audio-Studio-1.9.9-Setup-x64.exe` | `Aurora-Audio-Studio-1.9.9-arm64.dmg` |
| 获取 | [正式版下载](https://github.com/swy2018/Aurora-Audio-Studio/releases/latest) | [正式版下载](https://github.com/swy2018/Aurora-Audio-Studio/releases/latest) |
| 安装 | 运行标准安装程序 | 打开 DMG，拖入 Applications |
| 加速 | NVIDIA RTX 推荐，依模型要求 | Apple Silicon 的 MPS / MLX 或 CPU，依模型实现 |
| 指南 | [Windows 使用说明](work/audio-studio/README-给音乐人的使用说明.md) | [Mac 使用说明](docs/macOS-user-guide.md) |

请选择与你的系统匹配的安装包；同名 `.sha256` 文件用于校验下载完整性。Windows 最新测试版为 Beta 3；Mac 当前为 Beta 2，已完成 Developer ID 签名与 Apple 公证。Mac Beta 3 将单独构建。模型按需安装，不随安装包捆绑。各平台签名说明见[代码签名政策](CODE_SIGNING_POLICY.md)。

正式版与 Beta 分别提供下载。Mac Beta 3 开发与构建信息见[构建交接](docs/beta.3-mac-handoff.md)。

### 正式版与测试版更新

在设置中选择“应用更新通道”，保存后生效。默认“正式版”；主动选择“测试版（含正式版）”才会收到 Beta。检查到更新并经你确认后，Aurora 会下载、校验并启动对应平台的安装流程；系统权限确认仍需你处理。

日常使用推荐选择正式版；如需体验预发布版本，可选择 Beta 通道。切换通道不会自动降级。使用“回退到上一个正式版”可确认并安装适用于当前平台的较早正式版，正式版和 Beta 均可使用。回退保留模型与成品，但旧版可能无法读取新版配置或记录，请先备份重要数据。

应用更新成功后不再保留旧应用副本；安装失败时仍保留必要的恢复保护。历史备份不会自动删除，模型自身的备份与回退机制不变。

## 当前版本更新

<!-- current-packages:start -->
当前源码构建 / Current source builds: `Aurora-Audio-Studio-2.0.0-beta.3-Setup-x64.exe` · `Aurora-Audio-Studio-2.0.0-beta.3-arm64.dmg`
<!-- current-packages:end -->

<!-- release-notes-zh:start -->
- Windows 2.0 Beta 3：改进工作台连接、任务管理和模型维护。Mac 当前仍为 Beta 2，Beta 3 将由 Mac 端单独构建。
- 修复部分 Gradio 工作台已连接但操作区空白的问题，以及 ACE-Step 启动兼容问题。
- Windows 分轨、MIDI 扒谱和字幕工作区自动保存素材与处理设置，切换页面或重启后可继续使用。
- 支持的工作台生成任务可在任务中心跟踪，并与最终成品关联；改进取消和重复结果处理。
- 模型维护区分缺失文件、运行环境和完整重装，仅在可确认的范围内执行局部修复。
- 新增“回退到上一个正式版”，正式版和 Beta 均可使用；回退前显示目标版本与数据兼容提醒。
- 统一自有四语言文案，修正设置页更新通道对齐。模型、素材和成品不会因应用回退被删除。
- 本轮 Windows 六类功能已完成短样本测试；未覆盖所有可选模型和设备。Mac 安装与回退仍需单独验收。
<!-- release-notes-zh:end -->

模型中心分别显示文件状态和运行记录。成功完成任务后，会记录模型版本、时间及计算设备。“仅模型管理”表示支持下载与维护，但不能在 Aurora 内生成内容。可选模型的兼容性与运行表现取决于具体设备，请先使用短素材确认。

恢复任务时会使用保留的素材与参数重新处理，不会从中断步骤继续计算。模型下载在版本未变且来源支持续传时可继续；模型更新保留原文件与运行环境，以便回退，不自动删除旧模型。

音频可在成品库试听和导出；MIDI 显示音符信息并交给默认音乐软件编辑，Aurora 不内置 MIDI 合成器；SRT 可编辑后保存副本，已安装 Subtitle Edit 时也可直接交给它校对。

## 工作流

| 工作流 | 默认引擎 | 可选引擎 | 主要输出 |
|---|---|---|---|
| 音乐创作 | ACE-Step 1.5 XL Turbo | MiniMax-Music3（当前仅 Windows CUDA 后端） | 完整歌曲、纯音乐与草稿 |
| AI 配音与声音克隆 | Qwen3-TTS 1.7B | Qwen3-TTS 0.6B、F5-TTS | 配音与克隆音频 |
| 歌声克隆 | Seed-VC 44.1k | 按模型中心扩展 | 歌声与音色转换 |
| 去人声 / AI 分轨 | BS-RoFormer Vocals Revive V3e（二轨） | BS-RoFormer-SW 六轨、Demucs 4 | 独立 WAV 音轨 |
| AI 扒谱 | TransKun V2 | YourMT3+、ByteDance Piano、Basic Pitch | 标准 MIDI |
| 视频 AI 字幕 | Windows：Faster-Whisper XXL；Mac：原生 Whisper | Small、Large v3 Turbo、Large v3 | SRT 与转写数据 |

模型与第三方工具保留各自上游许可。模型大小、显存建议、语言能力和来源会在模型中心逐项显示。下表是共同模型目录：MiniMax-Music3 的当前 CUDA 后端及 Faster-Whisper XXL 二进制不支持 Mac；标注为“仅下载管理”的模型在 Windows 和 Mac 上均不支持生成内容。

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

## Windows 安装

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

Aurora 不提供云端生成服务，素材与结果保存在你指定的本地目录。下载应用更新或安装模型时，会连接 GitHub、Hugging Face 或模型列明的官方来源。

- [隐私说明](PRIVACY.md)
- [代码签名政策](CODE_SIGNING_POLICY.md)
- [GNU GPL v3.0](LICENSE)
- [反馈问题或提出建议](https://github.com/swy2018/Aurora-Audio-Studio/issues/new/choose)

## 开发

两端均使用 .NET 10。Windows 使用 WinUI 3 / Windows App SDK，Mac 使用 Avalonia，并共享设置、任务和成果等服务。官网使用原生 HTML、CSS 与 ES Modules，部署到 GitHub Pages。Mac 的本地编译、签名和公证见 [Mac 发布说明](docs/macOS-release.md)。

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
work/audio-studio/AuroraAudioStudio.Mac/   Avalonia Mac 桌面端
work/audio-studio/AuroraAudioStudio.Core/  共享服务与 Mac 运行时桥接
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

Aurora Audio Studio brings music, voice, singing, stem separation, MIDI transcription, and subtitles into one local workspace for Windows and macOS. Windows offers Beta 3. Mac remains on the signed and notarized Beta 2 until its Beta 3 build is ready. See the platform table above, the [Mac guide](docs/macOS-user-guide.md), and the [Beta 3 build handoff](docs/beta.3-mac-handoff.md).

Choose Stable or Beta in Settings and save. Stable is the default; Beta includes prereleases and subsequent stable versions. Updates download and verify the package for your platform before installer handoff. Switching from Beta to Stable never silently downgrades. Beta releases are intended for users who want early access; back up important settings before upgrading.

Use “Revert to previous stable release” to review and install the latest stable version older than your current app, for your platform. This is available from both Stable and Beta and does not change your update channel. Models and results are preserved, but older versions may not read newer settings or records. Back up important data first.

Successful app installation no longer retains an old app copy. Replacement failures retain recovery protection. Existing backups and model-specific backup and rollback behavior are unchanged.

### Current release notes

<!-- release-notes-en:start -->
- Windows 2.0 Beta 3 improves workbench connections, task tracking, and model maintenance. Mac remains on Beta 2 until Beta 3 is built and verified on Mac.
- Fix blank controls in some connected Gradio workbenches and an ACE-Step startup compatibility issue.
- Windows stem separation, MIDI transcription, and subtitle workspaces retain sources and settings across navigation and restarts.
- Track supported workbench generation tasks in Task Center and associate them with final results. Improve cancellation and duplicate-result handling.
- Model maintenance distinguishes missing files, runtime repair, and full reinstallation; partial repairs run only when their scope can be verified.
- Add Revert to previous stable release for both Stable and Beta, with target-version confirmation and a data-compatibility warning.
- Refine Aurora-authored copy in four languages and align the update-channel control. App rollback does not delete models, source media, or results.
- Short samples passed for all six Windows workflow categories. Not every optional model or device was tested. Mac installation and rollback require separate acceptance.
<!-- release-notes-en:end -->

Read the [capability matrix](docs/capabilities.json) and [Mac verification scope](docs/beta.2-mac-handoff.md) for exact scope. Download-only models are not runnable workbenches. Retrying an interrupted task restarts inference from its saved inputs and parameters. MIDI editing/playback requires your own music application; audio playback and subtitle-copy editing are available in Results.

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
