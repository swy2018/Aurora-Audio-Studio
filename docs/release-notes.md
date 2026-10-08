## 2.0.2-beta.1 — 2026-10-09

- Windows 与 Mac 2.0.2-beta.1 安装包及完整源码现已提供；Mac Apple Silicon 包已签名、公证并完成原生验收，Stable 继续提供 2.0.1。
- 改进未预装开发环境时的模型安装：应用随附 Git、音频工具和环境管理组件，安装程序包含 Visual C++ 与 WebView2 前置组件；Python 与模型依赖按需部署到隔离目录。
- 修复 Whisper 共享组件缺失、部分模型依赖冲突、CUDA 依赖被替换、ACE 权重不完整及下载文件漏选等首次安装问题。
- Basic Pitch 的大型依赖支持保留断点、备用下载服务和官方 SHA-256 校验，并修复运行依赖缺失导致无法启动的问题。
- 改进长任务进度反馈与取消收尾，避免取消后下载进程继续运行；保留已有模型、设置和作品。
- 修复 SoX 在不同 Windows 系统语言下无法读取中文等文件名的问题；进程级 UTF-8 清单随包提供，不改系统设置。完整 Unicode 支持需要 Windows 10 1903 或更新版本。
- Windows 隔离环境已验证 11 个引擎的短样本输出；Basic Pitch 的 12 音符样本音高全部匹配，但不代表复杂乐曲或演出总谱精度。
- Windows 包未签名。Mac 已验证隔离首次安装、九个模型短样本及升级回退；全新操作系统、Intel Mac、全部可选模型与长素材未覆盖。首次模型下载需要网络。

- Windows and Mac 2.0.2-beta.1 installers and full source are available. The Apple Silicon package is signed, notarized and natively tested; Stable remains 2.0.1.
- Improve model setup without preinstalled developer tools. Aurora includes Git, audio tools and environment management; setup includes Visual C++ and WebView2 prerequisites. Python and model dependencies are provisioned on demand in isolated directories.
- Fix missing Whisper shared components, model dependency conflicts, CUDA dependency replacement, incomplete ACE weights and omitted download files during first installation.
- Basic Pitch retains partial downloads of its large dependency, supports an alternate download server and verifies the official SHA-256. Fix a missing runtime dependency that prevented startup.
- Improve progress feedback and cancellation cleanup so downloads do not keep running after cancellation. Existing models, settings and outputs are preserved.
- Fix SoX file access across Windows system languages using a bundled per-process UTF-8 manifest, without changing system settings. Full Unicode support requires Windows 10 1903 or newer.
- Short-output acceptance covers 11 engines in isolated Windows environments. All 12 pitches in the Basic Pitch test fixture matched; this does not establish complex-music or performance-score accuracy.
- The Windows installer is unsigned. Mac acceptance covers isolated first setup, short outputs from nine models, update and rollback. Pristine operating systems, Intel Macs, every optional model and long inputs were not tested. First model downloads require network access.
