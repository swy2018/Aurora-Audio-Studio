# Aurora 2.0.0-beta.3 · Mac 构建交接

Windows 端先发布 Beta 3，Mac Beta 2 保留。请使用 Git 标签 v2.0.0-beta.3 或同一 Release 的 Source.zip，不要从旧 Beta 2 工作区直接打包。

## 构建前

1. 保留 Mac 本机未提交修改，再获取 v2.0.0-beta.3。不要强制覆盖工作区或用户配置。
2. 核对 AuroraAudioStudio.Mac.csproj 的 Version 为 2.0.0-beta.3。官网、README 和共享元数据已同步此源码版本，但 Mac 安装包尚未发布。
3. 阅读 docs/copy-rollback-fixes-2026-09-13.md。Windows 已跑 155 项回归，不能当作 Mac UI 或安装验收。

## 本轮 Mac 重点

- 四语言文案、设置和关于页新增“回退到上一个正式版”。
- MacAppUpdater 选择较早的正式 X.Y.Z 版本和准确的 arm64.dmg，并保留 SHA-256 与官方来源检查。普通更新仍禁止隐式降级。
- install-update.swift 接收显式 --rollback-to <正式版本>，核对正式构建号低于当前构建，仍执行 Developer ID（现有团队）、codesign 与 Gatekeeper/公证校验。
- 新应用替换并验证成功后删除本次事务的 previous.app，不再生成长期 .Aurora-backups。失败时尝试恢复旧应用；清理失败要明确显示，不得报告已清除。
- 不删除已有历史备份，不更改模型的备份/回退机制，也不修改模型或作品目录。
- Windows 新工作台任务观察仅在相关环境变量启用时生效；Mac 沿用现有回执路径，不要误设 AURORA_TASK_EVENTS。

## Mac 验收顺序

1. 按 docs/macOS-release.md 和 tools/build-macos.sh 的现有流程构建；保持签名身份和 Apple Silicon 目标。
2. 运行 MacTests、既有 Python 回归及 Swift 事务测试。Swift 自检：
   swiftc -D UPDATE_REPLACEMENT_TESTS work/audio-studio/AuroraAudioStudio.Mac/Runtime/install-update.swift -o <测试目录>/install-update-tests
   <测试目录>/install-update-tests <独立夹具目录>
3. 检查正常替换成功不留旧应用副本，替换失败恢复原应用；在隔离安装中验收真实升级与显式回退。不得绕过签名、公证或单实例保护。
4. 核对版本 2.0.0-beta.3、四语言按钮与长文案、工作台输入与生成、成品库、应用更新通道保持不变。至少完成受影响功能的短样本。
5. Apple Developer ID 签名、Apple 公证和 stapling 按既有说明完成，未通过不得标注为已公证。

## 上传与同步

验收成功后，将 Aurora-Audio-Studio-2.0.0-beta.3-arm64.dmg 及同名 .sha256 补到现有 v2.0.0-beta.3 Pre-release。不要另建同名版本，不覆盖 Windows 安装包、Source.zip 或其校验文件，不将 Beta 转为 Latest。
上传后核对远端大小和 SHA-256，再同步 Release 标题、官网、README 中 Mac 当前 Beta 2 的说明为 Beta 3。保留所有现有正式版和 Beta 2 资产；清理另行按准确清单批准。

当前线程只连接 Windows 主机，未向 Mac 自动派发构建。此交接文档与源码是 Mac 后续工作的起点，不代表 Mac 构建已启动或已通过。
