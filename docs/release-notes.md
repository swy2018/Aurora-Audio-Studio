## 2.0.0 — 2026-09-13

- Windows 2.0 正式版现已提供。Mac 2.0 源码同步提供，正式安装包待 Mac 端构建与验收；Mac 当前正式版仍为 1.9.9。
- 统一引擎启动、任务完成、失败、取消及模型维护阶段的自有提示，并补齐简体中文、繁体中文、英文和日文。
- 新写入的自有运行日志使用当前设置语言，并与引擎及安装工具的原始输出分开标记；保留退出码、错误详情和文件路径，不重写历史日志。
- 延续 2.0 Beta 的工作台连接修复、处理设置保存、任务与成品关联、模型局部修复及正式版回退。
- 本轮通过既有回归及 61 项日志文案检查，覆盖真实子进程的成功、失败、取消和原始输出保留；不代表所有模型、设备与长任务均已验证。
- 正式版与 Beta 通道继续独立提供。Windows 安装包未代码签名；升级或回退前请备份重要配置与作品。

- Windows 2.0 Stable is available. Mac 2.0 source is included for a separate Mac build and acceptance; the current Mac stable release remains 1.9.9.
- Refine Aurora-authored engine startup, task completion, failure, cancellation, and model-maintenance messages in Simplified Chinese, Traditional Chinese, English, and Japanese.
- New Aurora-authored log entries use the selected language and are distinguished from original engine and installer output. Exit codes, error details, and paths are retained; historical logs are not rewritten.
- Retain the 2.0 Beta workbench connection fixes, saved processing settings, task-to-result links, targeted model repair, and stable-release rollback.
- Existing regressions and 61 runtime-copy checks passed, including real subprocess success, failure, cancellation, and original-output preservation. Not every model, device, or long-running task was tested.
- Stable and Beta remain separate download channels. The Windows installer is unsigned. Back up important settings and work before upgrading or reverting.
