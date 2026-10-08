# Mac 2.0.2-beta.1 原生验收

2026-10-09，Apple M5 Pro / 24 GB / macOS 27.0.1（26A434）。Mac 安装包原生构建自已发布标签 `v2.0.2-beta.1` 的提交 `ded330928ee5a015029274499bcc02d4e563835c`；标签未移动，Windows 安装包与源码 ZIP 未替换。应用显示 `2.0.2-beta.1`，CFBundleVersion 为 `2000201`。

## 本轮重点：自带工具与首次安装

- 签名包包含 Git 2.56.0、HTTPS helper、模板和 CA；uv 0.12.23、FFmpeg 9.0.2 及 SoX 随包提供。62 个 Mach-O 均含 arm64，外部绝对依赖仅为系统库，没有 Homebrew 构建路径依赖；6 个运行时 Python 文件与标签源码一致。
- 在全新隔离数据目录中，将 PATH 限制为 `/usr/bin:/bin:/usr/sbin:/sbin`，使用包内工具完成版本检查、Git HTTPS `ls-remote`、uv 管理的 Python 3.11.17、管理环境和 Whisper Small 首次下载安装；收据、文件及真实推理模块导入检查通过。
- 使用 Launch Services 启动正式签名二进制，仅向该测试进程传递隔离数据路径和上述受限 PATH。原生界面调用新安装的 Whisper Small，生成 2 条 SRT 字幕；预览显示测试语音内容，导出副本与入库字幕逐字节一致。
- 简体、繁体、英语、日语入口与设置实际切换；About 显示正确 Beta 版本，更新通道为 Beta。141 个界面键无英语缺失，576 个工作台翻译条目覆盖四语言。

受限 PATH 和隔离目录不等同新装操作系统。本轮未验证无开发工具的新用户账户或全新 Mac，也未验证 Intel Mac、全部可选模型、长素材或所有安装失败场景。

## 回归与取消测试边界

- 117 项 MacTests、43 项 Python 环境／生命周期／结果／工具／字幕编辑测试通过；20 项修复专项和 24 项工作区检查通过。NuGet 审计未报告已知漏洞。
- MacTests 的原生 Unix 非零退出、环境和取消检查通过。追加使用本轮 `MacRuntime`、包内 uv 和实际 Python 子进程执行 5 轮取消后立即再次启动：取消返回后心跳文件保持不变，在约 0.3–0.5 秒的复核点，系统进程表确认父子进程均已消失。
- 两个额外通用测试没有计为通过：`BootstrapRegression` 假定 Windows 的路径长度限制，在 Mac 不适用；`ProcessCancellationRegression` 的即时 `Process.HasExited` 断言在本机失败。诊断中系统 `ps` 已无该进程，200 毫秒后 .NET 状态也更新为退出。因此本轮声明上述实际 Mac 验证范围，不声明通用套件全通过或跨平台的零延迟子进程状态同步。
- 验收脚本中的参数／转义错误已修正后重跑；产品源码没有为通过测试而修改。临时诊断插桩已恢复为标签源码。

## 代表性短样本

| 模型 | 本轮真实结果 | 设备 |
|---|---|---|
| Qwen3-TTS 0.6B CustomVoice | 238080 帧 / 24 kHz，最终 WAV 与成果回执一致 | MPS |
| Qwen3-TTS 0.6B Base | 32640 帧 / 24 kHz，最终 WAV 与成果回执一致 | MPS |
| Seed-VC | 340992 帧 / 44.1 kHz，最终 WAV 与成果回执一致 | MPS |
| ACE-Step | 6 秒 / 48 kHz，最终 WAV 与成果回执一致 | MPS + MLX |
| RoFormer | 六条分轨和伴奏混合，共 7 WAV | CPU |
| Demucs | 4 WAV | CPU |
| Transkun | 非空、有效音符 MIDI | CPU |
| Basic Pitch | 18 个有效音符 MIDI | CPU |
| Whisper Small | 既有环境及全新安装环境均生成 2 条 SRT | CPU |

各音频输出非空且数值有限。这些是短样本功能检查，不代表复杂音乐的转谱精度、全部模型首次安装或主观音质评价。

## 签名、公证与升级

- Developer ID：`Daozhi Aixinjueluo (V9JR96Z9YW)`。Apple 公证提交 `3c9ebc19-fbe6-44b1-9a89-48b6de293a81`，状态 Accepted。
- 深度严格签名、DMG 装订验证和 Gatekeeper 检查通过；Info.plist 无测试 `LSEnvironment`。
- 隔离应用实际升级 `2000199 → 2000201`（2.0.1 → 本 Beta），并显式回退 `2000201 → 2000199`。安装器等待原进程退出后替换并重新启动；测试配置、项目及成品哈希保持不变。
- 本机 `/Applications/Aurora Audio Studio.app` 仍为原有 2.0.0，未被发布测试替换；用户既有模型、设置和作品保留。
- DMG：`Aurora-Audio-Studio-2.0.2-beta.1-arm64.dmg`，106000128 字节。
- 装订后 SHA-256：`9bf87ae4aadac1127f3a53d2fe538269ef90d4bb7b3090777b066192398e63fb`。

发布只向同一 Beta Release 追加 Mac DMG 与校验文件，不抢占 Stable Latest；Stable 仍为 2.0.1。GitHub 资产大小、摘要及公开回下载逐字节校验均通过；回下载 DMG 的 hdiutil 验证通过，Windows／源码资产的 ID、大小与摘要未改变。清理按本轮准确清单另行批准，源码、本地 Git、工具链和凭据保留。
