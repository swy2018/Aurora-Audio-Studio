## 2.0.0-beta.2 — 2026-09-12

- 2.0 Beta 2 提供 Windows x64 与 macOS Apple Silicon 安装包；Mac 版已完成 Developer ID 签名与 Apple 公证。
- 修复模型检查或安装结束后进度面板不关闭、空日志下拉、下载文件占用、超时误判及批量取消后继续安装。
- 工作台校验本次引擎身份与操作控件；切换模型不再静默结束其他引擎，失败重连保留原有引擎。
- 修复重试进度串线、分轨模式不一致、维护期间路径变更及丢失成品误建目录；增强诊断凭据脱敏与设置保存失败处理。
- 自动释放显存尚未支持，已禁用无效开关并提示手动结束引擎。模型支持范围因平台而异，完整六功能推理与自动升级安装流程仍待进一步验证。

- Beta 2 is available for Windows x64 and macOS Apple Silicon. The Mac app is Developer ID signed and notarized by Apple.
- Fix maintenance panels surviving completion, empty log expanders, download file locks, timeout classification, and batch installs continuing after cancellation.
- Verify the launched engine identity and workbench controls. Switching models no longer silently stops another engine; failed reconnection preserves existing engines.
- Fix stale retry progress, inconsistent stem modes, storage changes during maintenance, and missing results creating folders. Improve credential redaction and failed settings saves.
- Automatic VRAM release remains unsupported; use the manual engine controls. Model support varies by platform. Full six-workflow inference and automatic update installation still need further validation.
