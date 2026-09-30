## 2.0.1-beta.2 — 2026-10-01

- Windows 2.0.1-beta.2 与源码更新。Mac Beta 2 待实机构建与验收；Mac Beta 1 和双端正式版 2.0.0 继续提供。
- Windows 卸载仅清除明确的设置与任务历史文件，不递归删除配置目录，保护自定义目录中的模型、素材和成品。
- 修复取消后重试、更新失败缓存恢复，以及静音字幕连同识别记录导出；保留原有取消和完整性校验。
- 处理记录增加字段结构校验与原件保留，批量任务保存不再覆盖较新的任务和成果索引。
- Mac 源码增加工作台忙碌保护、分轨模式一致性，以及引擎身份和可操作界面就绪检查。
- Mac 环境修复在独立目录安装并通过检查后切换，失败保留旧环境；隔离不同版本的下载暂存，并明确权重更新检查范围。
- Windows 安装包未签名。本次回归不代表所有模型、长素材或 Mac 原生推理已经重新验收。

- Windows 2.0.1-beta.2 and updated source are available. Mac Beta 2 awaits native build and acceptance; Mac Beta 1 and Stable 2.0.0 for both platforms remain available.
- Windows uninstall clears only explicitly listed preference and task-history files. It no longer recursively deletes the settings directory, preserving custom model, media and output folders.
- Fix explicit retry after cancellation, recovery from corrupt update caches, and silent-subtitle exports with recognition evidence. Cancellation and integrity protections remain in place.
- Validate processing-record fields while preserving invalid originals. Batch saves retain newer task and result indexes.
- Mac source adds active-workbench protection, matching stem modes, and engine identity plus usable-interface readiness checks.
- Mac environment repair installs and validates an independent candidate before activation, preserving the old environment on failure. Download staging is separated by revision and weight-update coverage is stated explicitly.
- The Windows installer is unsigned. This regression pass does not re-validate every model, long input, or native Mac inference workflow.
