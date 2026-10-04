## 2.0.1 — 2026-10-04

- Windows 2.0.1 正式版发布；Mac 2.0.1 安装包待 Mac 原生构建与验收，现有 Mac 正式版 2.0.0 和 2.0.1-beta.2 继续提供。
- 修复部分桌面启动环境下 Qwen 工作台报“句柄无效”的问题，统一后台引擎与维护工具的非交互输入处理。
- 修正引擎日志中中文路径的编码；维护检查明确区分文件齐全与实际运行验证。
- 包含 2.0.1 测试版的任务取消后重试、批量成果保存、更新缓存恢复、字幕导出及卸载数据保护修复。
- 保留四语言界面、窄窗口和日语排版改进；旧版配置、处理记录与用户成品受到原有保护。
- Windows 安装包未签名。验收覆盖本机已安装引擎的短样本；未安装模型、长素材与 Mac 新正式包不在本次实机验收范围内。

- Windows 2.0.1 Stable is available. Mac 2.0.1 requires a native Mac build and acceptance; Mac Stable 2.0.0 and 2.0.1-beta.2 remain available.
- Fix Qwen workbench startup failing with an invalid handle in some desktop launch environments. Background engines and maintenance tools now receive valid non-interactive input.
- Preserve Unicode paths in engine logs and distinguish file completeness from verified operation in maintenance results.
- Includes the 2.0.1 Beta fixes for retry after cancellation, batch result preservation, update-cache recovery, subtitle export and uninstall data protection.
- Retains four-language, compact-window and Japanese typography improvements, with existing safeguards for settings, processing records and user outputs.
- The Windows installer is unsigned. Acceptance covers short samples on locally installed engines, not missing models, long inputs or a new native Mac release.
