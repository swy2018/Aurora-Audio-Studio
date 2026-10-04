# Aurora 2.0.1 Windows 实机验收

日期：2026-10-04。源码基线为 `453a65289221b7b77a994935010cb51667106cee`；已验收并用于交付的冻结提交为 `f59d87422737f5ad5cb41996d357abe89cccfed0`。正式包及远端核验结果见文末。

## 本轮修复

- 从桌面启动 Qwen 时复现 `WinError 6`：Python/SoX 创建子进程时复制了无效的继承标准输入。控制台启动成功不能覆盖这个场景。受控无效句柄测试在旧代码退出 23，在修复后通过。
- 后台引擎、维护命令、音频预检与显卡检测统一使用有效输入管道，并在启动后关闭写端提供 EOF；保留原有输出捕获、取消、超时和进程归属管理。没有改动模型权重或上游 Python 包。
- 引擎输出与错误流采用 UTF-8，并为 Python 输出指定一致编码，保留原文而非翻译路径或原始异常。
- 维护页明确说明文件齐全不等于运行验证；四语言同步。

实现依据：[.NET RedirectStandardInput](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardinput?view=net-10.0) 和 [Windows 标准句柄契约](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfow)。

## 实机与隔离范围

Windows x64，RTX 5080 16 GB，驱动 617.14。使用独立 `AURORA_DATA_ROOT`，没有安装或卸载用户软件，没有下载、重装模型，没有修改真实用户配置。测试输入为短合成语音、既有授权歌曲的 12 秒片段、生成的钢琴片段，以及语音制成的短视频。

本轮初测的 10 个已安装引擎均完成真实输出。随后发现桌面 Qwen 启动缺陷，因此暂停发布并修复，没有将控制台成功当作桌面可用的证明。

修复后的原生 Aurora 按原来失败的方式启动，**没有给应用进程额外重定向输入来绕过缺陷**：

| 原生操作 | 结果与任务 ID |
| --- | --- |
| Qwen 1.7B 声音克隆：上传参考音频、填写匹配文本、生成 | 已完成并入库；`1f4f20380eb35dccb45119e47048a0f5` |
| Qwen 1.7B 专业音色：输入文本、生成 | 已完成并入库；`61cc109b5ff95be8bb3c895a71e40edd` |
| Qwen 1.7B 音色设计：描述音色、生成 | 已完成并入库；`a68b7443a691597bbd699ab3155c5dd6` |
| BS-RoFormer-SW：启动、取消、任务中心重试 | 同一任务成功，6 轨加伴奏；`9dd6fef9b9ff46bcaccacafffe9a00af` |
| TransKun：原生输入到 MIDI | 已完成并入库；`bb4e2b7a0eeb4fb5bace84160cf35533` |
| Whisper：视频输入到字幕 | 已完成并入库；`5e275d982ecc495898a631a6f23052f9` |

上述任务均记录 `cuda`。ACE 与 Seed-VC 也在本轮原生工作台完成真实生成：ACE `38b30ca22d46509d9a9a1370ecafedcb`，Seed-VC `b52c0dd4bca65e42a246b4f68150dfa0`；Seed-VC 使用不同源音频和参考音色。

启动与编码修复后的 10 引擎复测全部通过，均使用 `cuda`，通过生产后端与工作台实际回调生成、入库并校验本次输出：

| 引擎 | 输出证据 |
| --- | --- |
| ACE-Step 1.5 XL Turbo | 10 秒 WAV |
| Qwen3-TTS 1.7B Base / CustomVoice / VoiceDesign | 5.84 / 7.92 / 4.16 秒 WAV |
| Seed-VC 44.1k | 11.99 秒 WAV；后端复测为同源参考，原生界面另测不同参考 |
| BS-RoFormer Vocals Revive | 2 个 12 秒 WAV |
| BS-RoFormer-SW | 6 个独立音轨及伴奏，均 12 秒 |
| TransKun / YourMT3+ | 标准 MIDI，分别 40 / 61 个配对音符事件 |
| Whisper Large v3 Turbo | SRT；已知测试语句规范化后逐词一致 |

波形非空且数值有限，生成音频非静音，工作台音频与入库结果一致；MIDI 无悬空音符或逆向时序。分轨素材中缺少的乐器可能产生极低电平声部，不据此承诺分离质量。Qwen 和 Seed-VC 的新日志确认中文成品路径完整。

另外完成模型检查全部更新并自动收起进度、素材选择与草稿恢复、成品试听控件、同名导出避让与字节一致性验证。已有同名文件未被覆盖。

## 回归与发布门槛

- 完整 Windows 行为回归、更新流程、四语言字典和官网发布选择策略通过；新增 6 个后台进程回归覆盖无效、空、正常继承输入和输出保留。
- Windows 编译零警告、零错误。共享 Mac 测试 113 项通过；其中原生 Unix 测试明确跳过，不能作为 Mac 实机证明。
- 最终版本号 2.0.1 编译零警告、零错误；1280×900 原生界面 19 项检查通过，12 页入口与四语言切换截图已人工复核，日语可切回中文。
- [冻结提交 CI](https://github.com/swy2018/Aurora-Audio-Studio/actions/runs/37200267482) 两个作业均通过：Windows 全部回归、打包、实际安装后响应窗口、真实卸载保留 5 类嵌套用户文件并清除 4 项偏好，以及 Linux 25 项 POSIX 环境/生命周期测试。CI 没有 GPU 模型推理；推理证据来自上述维护机实测。
- 最终 2.0.1 原生程序再次按原始启动方式完成 Qwen 声音克隆：任务 `2a6acdcf5fea55a88a57c14d8ba28df4`，CUDA，4.08 秒/24 kHz WAV，自动入库且中文日志路径正确。再次检查全部模型更新约 4 秒完成：13 个最新、14 个未安装或待修复、0 个失败，进度面板收起、按钮恢复。

## 安装包

Windows 安装包直接下载自上述成功 CI，不用本机开发构建替换；产品版本 2.0.1，签名状态为未签名，SHA-256 与 CI 文件一致。源码从同一冻结提交导出，273 个归档条目，包含许可证及 Mac 交接，不含模型、bin/obj、用户配置或测试音频。源码包内的验收页保留冻结时状态，本页追加后续核验。

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| Windows Setup x64 | 79,383,124 | `e9066c7626ad9cdebdbd6ab6cbb3e0acb14dab8657b2782d075af01b3397d058` |
| Source ZIP | 11,966,124 | `e8ec8b35598f93b51072a9e84c6fd03419a1e2d31e2b0b6afd6bf58cca1d5277` |

[2.0.1 Release](https://github.com/swy2018/Aurora-Audio-Studio/releases/tag/v2.0.1) 已发布为非草稿正式版并设为 Latest；标签指向上述 CI 提交。4 个交付文件（安装包、源码和各自校验文件）的远端大小与 SHA-256 digest 均匹配本地。既有 1.9.9、2.0.0、Beta 2 的资产 ID、名称、大小和摘要均未改变。

官网真实下载清单与 GitHub About 同步区分 Windows 2.0.1、Mac 正式版 2.0.0 和 Mac Beta 2；Mac 展示截图仍明确标注实际的 2.0.0，不冒充新截图。线上部署及交互核验记录保留在维护机的本轮证据目录。

## 不承诺的范围

9 个未安装的可运行选项没有实测：MiniMax-Music3、Qwen 0.6B 两种模式、F5-TTS、Demucs、ByteDance Piano、Basic Pitch、Whisper Small、Whisper Large v3。仅下载管理的模型不视为可生成引擎。真实安装、卸载与恢复边界由独立夹具和 CI 验证，不在用户现有模型目录做破坏性实验。

短样本通过不等于所有素材、所有设备或所有模型可靠；合法 MIDI 不等于音符准确或可直接演出。未覆盖长音频压力、多 GPU、无 NVIDIA 的整机环境。Seed-VC 上游工作台有 4 条字体资源 404，界面使用回退字体；本轮按钮操作和生成不受影响，但不宣称上游页面没有资源错误，也未修改上游文件。

Windows 安装包未签名。Mac 2.0.1 正式包、原生 UI、模型推理、签名与公证必须在 Mac 完成；沿用已有 Mac 2.0.0 正式版和 2.0.1-beta.2，不将旧 DMG 改名为新正式版。

原始日志、任务记录、输出检查与截图保存在维护机 `.maintenance/strict-20261004/evidence`，不把测试音频、用户路径配置或模型打入公开源码包。本轮未执行清理。
