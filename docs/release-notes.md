## 1.9.0 — 2026-09-05

- 安装与升级先验证候选目录，Python 环境保持固定路径；同一修订可继续下载，保留可回退版本。
- 队列保留未完成任务与完整参数，支持重新执行；修复重复提交、晚到进度、取消误停和同名素材冲突。
- 六类结果使用明确文件清单，创作工作台自动收录完成音频；新增试听、MIDI 信息、字幕编辑副本、导出与复制路径。
- 模型中心区分文件齐全、短任务验证、仅下载管理与外部工具；字幕素材语言不再跟随界面语言。
- 修正小窗口工作台布局、网站语言与键盘语义；官网、README、关于和更新日志共享发布数据。
- 重做简体中文、繁体中文、英语、日语本地化；语言选择立即生效，保留工作台输入。日语采用随附 Noto Sans JP 字体与独立排版。
- 修正音频试听关闭时的播放器释放顺序；ACE-Step 改用 PyTorch 后端与分阶段卸载，Seed-VC 的 CUDA 与界面依赖统一解析并校验。

- Validate candidate deployments before activation. Python environments stay at fixed paths; same-revision downloads resume and previous versions remain recoverable.
- Preserve unfinished tasks and their full parameters for reruns; fix duplicate submissions, late progress, cross-task cancellation, and filename collisions.
- Register explicit output manifests, including completed creative-workbench audio. Add audio playback, MIDI information, subtitle-edit copies, export, and path copying.
- Distinguish files present, short-task verification, download-only models, and external tools. Source-language selection is independent of UI language.
- Improve narrow-window workspaces, website localization and keyboard semantics; public release information shares one source.
- Rebuild Simplified Chinese, Traditional Chinese, English, and Japanese localization. Language changes apply immediately without losing workbench inputs. Japanese uses bundled Noto Sans JP and language-specific typography.
- Correct audio-preview disposal; use ACE-Step's PyTorch backend with staged offloading, and resolve and validate Seed-VC CUDA and UI dependencies together.
