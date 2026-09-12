# 2.0.0-beta.2 Mac 构建交接

Windows beta.2 先发布，Mac 包尚未在本轮构建、签名、公证或实机验证。Mac beta.1 继续可下载。不要重命名 beta.1 的 DMG 冒充 beta.2。

## 获取精确源码

建议在新的目录克隆，避免覆盖 Mac 上未提交的开发：

~~~bash
git clone --branch v2.0.0-beta.2 https://github.com/swy2018/Aurora-Audio-Studio.git Aurora-beta2
cd Aurora-beta2
git status --short
git rev-parse HEAD
~~~

GitHub Release 自带 Source code ZIP 也对应同一标签。归档没有模型、缓存、Windows 运行环境或凭据；Mac 自行登录所需服务。不要将本地模型环境提交到仓库。

## 本次 Mac 相关改动

- 共享任务队列：进度按执行轮次归属，旧回调不会污染重试；增加待执行操作状态。
- Mac 更新页：下载进度作用域结束后丢弃迟到回调。
- Mac 设置：无效的自动显存释放选项禁用，提示手动结束引擎；不承诺已实现自动卸载。
- Gradio 回执桥接保留既有 Mac 行为：只有启动方提供 AURORA_WORKBENCH_INSTANCE 时才添加 Windows 新就绪契约。没有给 Mac 偷换启动或 GPU 策略。
- Mac 回执测试按 UTF-8 读取中文路径。项目版本已同步为 2.0.0-beta.2。

Windows 特有的工作台身份/DOM 就绪、维护锁、目录保护等不代表 Mac 已经完成对应实机验收。完整变更及限制见 [修复记录](function-fixes-2026-09-12.md)。

## 构建与验收

沿用 [Mac 发布流程](macOS-release.md)，先检查 .NET 10、Xcode、uv、ffmpeg、ffprobe、sox 和现有 Apple 签名身份。Windows 本机缺少 Avalonia/macOS 依赖，未完成 Mac 编译；必须在 Mac 上重新还原并执行项目测试。

~~~bash
dotnet restore work/audio-studio/AuroraAudioStudio.Mac -r osx-arm64 --locked-mode
dotnet build work/audio-studio/AuroraAudioStudio.Mac -c Release -r osx-arm64 --no-restore
dotnet run --project work/audio-studio/AuroraAudioStudio.MacTests -c Release
python3 work/audio-studio/AuroraAudioStudio.Mac/Runtime/test_result_bridge.py
~~~

检查其他 Runtime/test_*.py 的适用环境后执行，尤其真实模型测试会使用模型与显存，不能把离线单元测试当推理验收。至少验证实际窗口、语言切换、模型中心、六功能输入/运行/结果收录，以及取消、重试和更新进度收尾。先保存用户作品；不要为了测试批量重装或删除模型。

签名包使用不存在的新构建目录（示例目录如已存在，请换一个，不删除）：

~~~bash
export AURORA_BUILD_ROOT="$PWD/dist/macos-2.0.0-beta.2-arm64"
export AURORA_SIGN_IDENTITY='Developer ID Application: YOUR NAME (TEAMID)'
bash work/audio-studio/tools/package-macos.sh
~~~

证书和公证凭据只在本机钥匙串配置，不发聊天、不提交。按 macOS-release.md 完成 notarytool Accepted、stapler validate、Gatekeeper 和最终 DMG 挂载/启动验证后，再计算最终 SHA-256。

## 补入同一 Release

最终文件名必须为：

- Aurora-Audio-Studio-2.0.0-beta.2-arm64.dmg
- Aurora-Audio-Studio-2.0.0-beta.2-arm64.dmg.sha256

校验文件为“最终 DMG 的小写 SHA-256 + 两个空格 + 完整文件名”，不是签名前的摘要。上传到 v2.0.0-beta.2，不覆盖 Windows 包或摘要；补齐后同步 README/官网平台状态和 Release 说明。

如果构建需要额外源码修复，先提交并记录确切构建提交；不要把不同源码的产物说成原标签构建。不要强推移动已发布标签，需要时发布下一个 beta。
