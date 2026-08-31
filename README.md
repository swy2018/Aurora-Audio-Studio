<div align="center">
  <img src="docs/assets/aurora-icon.png" width="104" alt="Aurora Audio Studio">
  <h1>Aurora Audio Studio</h1>
  <p><strong>让声音创作，回到创作本身。</strong></p>
  <p>面向 Windows 的本地 AI 音频创作工作台</p>
  <p>
    <a href="https://swy2018.github.io/Aurora-Audio-Studio/"><img alt="官方网站" src="docs/assets/readme-button-website.svg"></a>
    <a href="https://github.com/swy2018/Aurora-Audio-Studio/releases/latest"><img alt="下载 Aurora Audio Studio 1.4.1" src="docs/assets/readme-button-download.svg"></a>
    <a href="CHANGELOG.md"><img alt="更新日志" src="docs/assets/readme-button-changelog.svg"></a>
    <a href="#english"><img alt="English" src="docs/assets/readme-button-english.svg"></a>
  </p>
</div>

![Aurora Audio Studio 音乐创作工作台](docs/assets/aurora-workbench-music.png)

Aurora 把音乐生成、AI 配音、声音克隆、歌声转换、音轨分离、MIDI 扒谱和视频字幕集中到同一个本地入口。六个功能互相独立，可直接开始当前任务，不再需要手动管理多个启动器、端口和结果目录。

## 1.4.1 带来了什么

### 五个固定组件也能自动更新

- BS-RoFormer、YourMT3+、ByteDance Piano、Faster-Whisper XXL 与 Subtitle Edit 现在都接入模型中心的自动检查、安装、修复和更新流程。
- PyPI 组件使用各自隔离环境升级，并通过官方命令下载对应权重；ByteDance Piano 仅接受原始 Zenodo 文件的固定 SHA-256。
- GitHub Release 组件会选择匹配的 Windows x64 资产，校验上游摘要或固定资产信息后，才从暂存目录切换为正式版本。

### 未安装模型可直接继续

- 在任务工作台选择未安装的模型时，主按钮会立即变为“安装此模型”，使用同一套位置、空间和确认流程。
- Aurora 启动时仍不会自动下载任何模型；只有用户明确点击安装后才开始部署。
- 安装通过标记文件与完整性检查后，才会继续打开对应工作台。

### 更清楚的分轨与字幕选择

- 去人声 / AI 分轨新增“二轨”和“多轨”：二轨使用专用 BS-RoFormer Vocals Revive V3e，输出纯人声与纯伴奏；多轨继续提供质量与速度方案。
- 字幕的快速、推荐与高质量预设分别对应 Faster-Whisper Small、Large v3 Turbo 与 Large v3。
- 所有更新继续采用暂存、校验、原子切换和上一版本恢复机制。

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
2. 下载 `Aurora-Audio-Studio-1.4.1-Setup-x64.exe` 和同名 `.sha256` 文件。
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
dotnet publish .\work\audio-studio\AuroraAudioStudio\AuroraAudioStudio.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o .\publish\Aurora-Audio-Studio-1.4.1
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

### What version 1.4.1 adds

- BS-RoFormer, YourMT3+, ByteDance Piano, Faster-Whisper XXL, and Subtitle Edit now support automatic checks, installation, repair, and updates from Model Management.
- Selecting an uninstalled model in a task workbench exposes the install action immediately, while Aurora still downloads nothing without explicit confirmation.
- Separation now distinguishes two-stem vocals/instrumental output from multi-stem processing. The dedicated Vocals Revive V3e model powers the two-stem path.
- Subtitle presets map clearly to Faster-Whisper Small, Large v3 Turbo, and Large v3.
- Every replacement remains staged and verified before activation, with the previous working version retained for recovery.

### Local by design

Aurora does not operate a cloud generation service. Media and generated output remain in the directories chosen by the user. App updates and model deployment connect only to GitHub, Hugging Face, or the official source identified for each model.

### Install

1. Open the latest [Release](https://github.com/swy2018/Aurora-Audio-Studio/releases/latest).
2. Download `Aurora-Audio-Studio-1.4.1-Setup-x64.exe` and its `.sha256` file.
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
