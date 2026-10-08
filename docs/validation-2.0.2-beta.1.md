# Aurora 2.0.2-beta.1 验收记录

日期：2026-10-09。Windows x64 安装包与源码先发布，随后补充 Mac Apple Silicon 安装包。下文为 Windows 验收，Mac 独立实测见[Mac 验收记录](validation-mac-2.0.2-beta.1.md)。Stable 仍为 2.0.1。

## 来源与构建

- 产品提交：`ded330928ee5a015029274499bcc02d4e563835c`，标签 `v2.0.2-beta.1`。
- [GitHub Actions 构建与验收](https://github.com/swy2018/Aurora-Audio-Studio/actions/runs/37812244649)：两个任务均成功。
- 安装包必须使用该次 Actions 产物，不以本机重新构建替换。CI 产物 ID 为 `11566406102`，归档 SHA-256 为 `1edf7b3750f22ab0dd24900709b07cff435a8a958815c5006f5104a1151e8fd6`。
- Windows 安装包未签名。维护机的已安装程序仍为 2.0.1，未执行本机升级或卸载。

## 已通过

1. Windows 编译、更新流程、行为回归；最终本机回归有 333 条 PASS、无 FAIL。CI 使用明确随附的运行工具，不依赖开发机的全局 FFmpeg。
2. 仅系统 PATH 的基础环境验收：随包 Git/uv/FFmpeg/ffprobe/SoX 启动、Git HTTPS、隔离下载 Python 3.10/3.11/3.12、管理依赖安装与导入、依赖一致性检查及真实音频处理。
3. SoX 使用中文、日文、表情符号混合文件名完成实际音频处理，ffprobe 验证输出时长。本机与 CI 均通过；安装器压缩日志确认带入 `sox.exe.manifest`。上游 EXE 字节未修改，SHA-256 为 `e0e3cdc4bcdfbb5b91ac8f53b024964d092f89ba90130ba74b223a1df11b5439`。完整 Unicode 支持需要 Windows 10 1903 或更新版本。
4. 四语言检查：141 个自有文案键无缺失英文翻译，576 个工作台条目四语言齐全。27 项模型能力目录、版本同步及官网下载策略检查通过。
5. Mac 共享 C# 逻辑 113 项通过；Ubuntu 上的 Mac 环境与生命周期 29 项通过。它们不是原生 Mac UI、安装、推理、签名或公证验收。
6. CI 实际运行安装包，已安装应用出现可响应的 Aurora 窗口并正常关闭。实际卸载测试确认嵌套模型、作品、处理记录和无关文件内容不变，只清除明确归属的偏好设置。
7. Basic Pitch 在本轮 Beta 源码下再完成一次真实转写与入库：9.6 秒输入得到合法 MIDI，2 轨、12 音符、约 9.47 秒。此前冷安装及已知音高逐项检查也通过。Windows 使用 TensorFlow CPU 后端，不声称 CUDA 运行。

11 个 Windows 引擎的真实短样本及质量限制详见 [首次安装修复记录](clean-install-2026-10-08.md)。YourMT3 和经典钢琴样本有漏音或额外音符；执行成功不代表复杂音乐转写准确，更不代表可直接演出。

## 尚未覆盖

- 不预装 VC++、WebView2 或其他开发环境的全新 Windows 用户/虚拟机全流程；CI 运行器已有部分系统组件，不能代替该项验收。
- Mac 原生首次安装、真实输入输出、Finder 启动、签名与 Apple 公证。构建方应使用 [Mac 交接说明](macOS-2.0.2-beta.1-handoff.md)。
- 全部可选模型、长音频、训练与任意硬件/网络组合。MiniMax 等未获准下载的模型没有擅自安装。
- 本轮安装包未做维护机原位升级；现有模型、作品与设置没有迁移或删除。

## 发布资产

已下载 CI 安装包并与同一产物中的校验文件核对；PE 产品版本为 `2.0.2-beta.1`，文件版本为 `2.0.2.0`，Authenticode 为未签名。源码包从固定提交导出，267 个受版本管理的文件，不包含模型、虚拟环境、构建输出、凭据或测试音频。

| 资产 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `Aurora-Audio-Studio-2.0.2-beta.1-Setup-x64.exe` | 428210848 | `6591c0a7bf2655a0dc697187439ce291bea53894175bdbc4359741eaf5bc105f` |
| `Aurora-Audio-Studio-2.0.2-beta.1-Source.zip` | 13627045 | `24d475bd56d9d697a05043de64c50e329c3b4d70ce4f203983b9018300078329` |

[Release](https://github.com/swy2018/Aurora-Audio-Studio/releases/tag/v2.0.2-beta.1) 已于 2026-10-09 01:19:48（北京时间）公开，Release ID 为 `407076998`，标记为预发布而非 Latest。四个远端资产的文件名、大小、上传状态及 SHA-256 均与本地逐项一致。

- Windows 安装包 / 校验文件资产 ID：`622485559` / `622485556`。
- 源码 / 校验文件资产 ID：`622463792` / `622463790`。
- Stable Latest 仍为 `v2.0.1`（Release ID `402999075`）；没有移除或替换 Windows / Mac 2.0.1 资产。
- 本轮没有执行本地批量清理、旧 Release 删除或用户电脑安装。测试文件仍保留，后续清理须单独列准确清单并获批。
