## 2.0.1-beta.2 — 2026-10-01

- Windows 与 Mac 均提供 2.0.1-beta.2。Mac Apple Silicon 安装包已完成实机验收、Developer ID 签名与 Apple 公证；正式版仍为 2.0.0。
- Windows 卸载仅清除明确的设置与任务历史文件，不递归删除配置目录，保护自定义目录中的模型、素材和成品。
- 修复取消后重试、更新失败缓存恢复，以及静音字幕连同识别记录导出；保留原有取消和完整性校验。
- 处理记录增加字段结构校验与原件保留，批量任务保存不再覆盖较新的任务和成果索引。
- Mac 增加工作台忙碌保护、分轨模式一致性，以及引擎身份和可操作界面就绪检查。
- Mac 环境修复在独立目录安装并通过检查后切换，失败保留旧环境；隔离不同版本的下载暂存，并明确权重更新检查范围。
- 修复窄窗口下工作台空白状态的居中和日语标题裁切，布局按实际可用空间适配。
- Windows 安装包未签名。Mac 实测覆盖代表性短样本，不代表所有模型、长素材或其他 macOS 版本均已验收。

- Windows and Mac 2.0.1-beta.2 are available. The Mac Apple Silicon package has passed native acceptance, Developer ID signing and Apple notarization; Stable remains 2.0.0.
- Windows uninstall clears only explicitly listed preference and task-history files. It no longer recursively deletes the settings directory, preserving custom model, media and output folders.
- Fix explicit retry after cancellation, recovery from corrupt update caches, and silent-subtitle exports with recognition evidence. Cancellation and integrity protections remain in place.
- Validate processing-record fields while preserving invalid originals. Batch saves retain newer task and result indexes.
- Mac adds active-workbench protection, matching stem modes, and engine identity plus usable-interface readiness checks.
- Mac environment repair installs and validates an independent candidate before activation, preserving the old environment on failure. Download staging is separated by revision and weight-update coverage is stated explicitly.
- Fix empty-workbench alignment and clipped Japanese headings in compact windows using the actual available viewport.
- The Windows installer is unsigned. Mac acceptance covers representative short samples, not every model, long input, or other macOS version.
