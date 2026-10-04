## 2.0.1 — 2026-10-04

- Windows 与 Mac 2.0.1 正式版均已发布；Mac Apple Silicon 安装包已完成实机验收、Developer ID 签名与 Apple 公证。
- 修复部分桌面启动环境下 Qwen 工作台报“句柄无效”的问题，统一后台引擎与维护工具的非交互输入处理。
- 修正引擎日志中中文路径的编码；维护检查明确区分文件齐全与实际运行验证。
- 包含 2.0.1 测试版的任务取消后重试、批量成果保存、更新缓存恢复、字幕导出及卸载数据保护修复。
- 保留四语言界面、窄窗口和日语排版改进；旧版配置、处理记录与用户成品受到原有保护。
- Windows 安装包未签名。Mac 已验证八个已安装模型的短样本、四语言界面及升级回退；未安装模型、长素材与 Intel Mac 不在本次实机验收范围内。

- Windows and Mac 2.0.1 Stable are available. The Apple Silicon package has passed native Mac acceptance, Developer ID signing and Apple notarization.
- Fix Qwen workbench startup failing with an invalid handle in some desktop launch environments. Background engines and maintenance tools now receive valid non-interactive input.
- Preserve Unicode paths in engine logs and distinguish file completeness from verified operation in maintenance results.
- Includes the 2.0.1 Beta fixes for retry after cancellation, batch result preservation, update-cache recovery, subtitle export and uninstall data protection.
- Retains four-language, compact-window and Japanese typography improvements, with existing safeguards for settings, processing records and user outputs.
- The Windows installer is unsigned. Mac acceptance covers short samples on eight installed models, four UI languages, update and rollback. Missing models, long inputs and Intel Macs were not tested.
