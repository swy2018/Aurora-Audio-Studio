## 2.0.0-beta.3 — 2026-09-13

- Windows 2.0 Beta 3：改进工作台连接、任务管理和模型维护。Mac 当前仍为 Beta 2，Beta 3 将由 Mac 端单独构建。
- 修复部分 Gradio 工作台已连接但操作区空白的问题，以及 ACE-Step 启动兼容问题。
- Windows 分轨、MIDI 扒谱和字幕工作区自动保存素材与处理设置，切换页面或重启后可继续使用。
- 支持的工作台生成任务可在任务中心跟踪，并与最终成品关联；改进取消和重复结果处理。
- 模型维护区分缺失文件、运行环境和完整重装，仅在可确认的范围内执行局部修复。
- 新增“回退到上一个正式版”，正式版和 Beta 均可使用；回退前显示目标版本与数据兼容提醒。
- 统一自有四语言文案，修正设置页更新通道对齐。模型、素材和成品不会因应用回退被删除。
- 本轮 Windows 六类功能已完成短样本测试；未覆盖所有可选模型和设备。Mac 安装与回退仍需单独验收。

- Windows 2.0 Beta 3 improves workbench connections, task tracking, and model maintenance. Mac remains on Beta 2 until Beta 3 is built and verified on Mac.
- Fix blank controls in some connected Gradio workbenches and an ACE-Step startup compatibility issue.
- Windows stem separation, MIDI transcription, and subtitle workspaces retain sources and settings across navigation and restarts.
- Track supported workbench generation tasks in Task Center and associate them with final results. Improve cancellation and duplicate-result handling.
- Model maintenance distinguishes missing files, runtime repair, and full reinstallation; partial repairs run only when their scope can be verified.
- Add Revert to previous stable release for both Stable and Beta, with target-version confirmation and a data-compatibility warning.
- Refine Aurora-authored copy in four languages and align the update-channel control. App rollback does not delete models, source media, or results.
- Short samples passed for all six Windows workflow categories. Not every optional model or device was tested. Mac installation and rollback require separate acceptance.
