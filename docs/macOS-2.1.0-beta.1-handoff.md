# Aurora 2.1.0-beta.1：Mac 构建与验收交接

日期：2026-11-27。Windows 与源码先发布；Mac 当前 Beta 下载保持已验收的 2.0.2-beta.1，Stable 保持 2.0.1。本文件不是 Mac 验收通过声明。

从新目录检出最终 `v2.1.0-beta.1` 标签，或使用对应 Release 的 Source.zip 并核对同名 SHA-256。不要覆盖现有未提交源码、移动标签或使用 Windows 产物代替 Mac 构建。

## 本轮需要复测的变化

- 字幕保存取消、无效字幕、文件夹不可写时保留编辑；导出静音字幕时保留对应证据。
- 损坏设置/草稿不自动覆盖；显式恢复先保留原始副本。草稿保存失败时，“取消退出”与“不保存并退出”均需验证。
- 新 [MIDI 乐谱整理](score-preparation.md)：四语言、日语窄窗口、深色主题、文件选择、导出重试、取消、MusicXML 打开与校对回放。
- BS-RoFormer / YourMT3 正式源码、未发布提交和权重分开检查；精确提交安装、较新本地来源与分叉来源保护。失败的候选环境不得替换可工作环境。
- 分轨缺少声部、重复输出或时长截断不得记为成功；完整短样本仍应通过。

## 构建与工具完整性

沿用 [Mac 构建和签名流程](macOS-release.md)，目标仍是 Apple Silicon / macOS 26+，未新增 Intel 支持。开发机需要现有 Homebrew 工具链，最终用户不需要 Homebrew。

`tools/macos-runtime-lock.json` 锁定已知工具版本及 arm64；打包会核对每个原生工具/依赖架构、版本和许可证。若本机工具不同，先核对已有 Mac 验收记录并进行新版本验收，再有依据地更新锁定值，不绕过检查。

CA 从固定 SHA-256 的公开 certifi 包生成，不再复制构建机的系统信任库。它会优先使用校验通过的缓存，首次只下载约 128 KB。`Runtime/licenses/verified-inputs.json` 中原生文件哈希处于重定位/ad-hoc 签名阶段，**不是** Developer ID 签名后的最终哈希。签名、公证、装订后，重新计算最终 DMG 哈希。

在独立 `AURORA_BUILD_ROOT` / `AURORA_TEST_DATA_ROOT` 下构建、从 Finder 启动验证，不覆盖 `/Applications` 或个人模型。具体隔离命令沿用 [上轮交接](macOS-2.0.2-beta.1-handoff.md)，但使用本轮标签。必须补跑：

```bash
dotnet run --project work/audio-studio/AuroraAudioStudio.MacTests -c Release
cd work/audio-studio/AuroraAudioStudio.Mac/Runtime
python3 -B -m unittest test_source_updates test_environment test_lifecycle
```

Windows 上的 115 项共享测试、WSL 的 35 项 Python 测试不代替 Mac 原生界面、进程树取消、模型推理、签名及公证。Mac 仍需真实短样本和无开发环境的安装验证；未测范围如实列出。

通过后按授权补充到同一 Beta Release，保留 Windows/源码资产，不改变 Stable Latest。更新官方下载快照和平台文案，回传提交、系统、硬件、测试结果、签名公证及最终 DMG 大小/哈希。凭据、私人素材、配置和模型不随源码上传；清理另按准确清单审批。
