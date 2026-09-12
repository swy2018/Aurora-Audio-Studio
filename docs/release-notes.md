## 2.0.0-beta.2 — 2026-09-12

- 2.0 Beta 2 本次提供 Windows 安装包；Mac 源码已同步，安装包待 Mac 构建、签名、公证及验收。
- 修复模型检查或安装结束后进度面板不关闭、空日志下拉、下载文件占用、超时误判及批量取消后继续安装。
- 工作台校验本次引擎身份与操作控件；切换模型不再静默结束其他引擎，失败重连保留原有引擎。
- 修复重试进度串线、分轨模式不一致、维护期间路径变更及丢失成品误建目录；增强诊断凭据脱敏与设置保存失败处理。
- 自动释放显存尚未支持，已禁用无效开关并提示手动结束引擎。85 项后台回归通过；六功能真实推理和可见 UI 全流程仍待实测，不承诺所有模型已验收。

- Beta 2 provides a Windows installer. Mac source is synchronized; its package awaits a Mac build, signing, notarization, and acceptance.
- Fix maintenance panels surviving completion, empty log expanders, download file locks, timeout classification, and batch installs continuing after cancellation.
- Verify the launched engine identity and workbench controls. Switching models no longer silently stops another engine; failed reconnection preserves existing engines.
- Fix stale retry progress, inconsistent stem modes, storage changes during maintenance, and missing results creating folders. Improve credential redaction and failed settings saves.
- Automatic VRAM release remains unsupported: the ineffective switch is disabled with manual-release guidance. 85 background regressions passed; real inference and full visible UI workflows still need testing. Not all models are claimed as verified.
