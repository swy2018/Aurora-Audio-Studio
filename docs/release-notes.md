## 2.0.2-beta.1 — 2026-10-09

- 本次提供 Windows 2.0.2-beta.1 安装包与完整源码；Mac 下载继续提供已验收的 2.0.1 正式版。
- 改进未预装开发环境时的模型安装：应用随附 Git、音频工具和环境管理组件，安装程序包含 Visual C++ 与 WebView2 前置组件；Python 与模型依赖按需部署到隔离目录。
- 修复 Whisper 共享组件缺失、部分模型依赖冲突、CUDA 依赖被替换、ACE 权重不完整及下载文件漏选等首次安装问题。
- Basic Pitch 的大型依赖支持保留断点、备用下载服务和官方 SHA-256 校验，并修复运行依赖缺失导致无法启动的问题。
- 改进长任务进度反馈与取消收尾，避免取消后下载进程继续运行；保留已有模型、设置和作品。
- Windows 隔离环境已验证 11 个引擎的短样本输出；Basic Pitch 的 12 音符样本音高全部匹配，但不代表复杂乐曲或演出总谱精度。
- Windows 包未签名。全新 Windows 系统及本轮 Mac 原生安装仍待进一步验收；模型首次下载需要网络，具体加速能力取决于引擎和硬件。

- This release provides the Windows 2.0.2-beta.1 installer and full source. Mac downloads remain on the verified 2.0.1 Stable release.
- Improve model setup without preinstalled developer tools. Aurora includes Git, audio tools and environment management; setup includes Visual C++ and WebView2 prerequisites. Python and model dependencies are provisioned on demand in isolated directories.
- Fix missing Whisper shared components, model dependency conflicts, CUDA dependency replacement, incomplete ACE weights and omitted download files during first installation.
- Basic Pitch retains partial downloads of its large dependency, supports an alternate download server and verifies the official SHA-256. Fix a missing runtime dependency that prevented startup.
- Improve progress feedback and cancellation cleanup so downloads do not keep running after cancellation. Existing models, settings and outputs are preserved.
- Short-output acceptance covers 11 engines in isolated Windows environments. All 12 pitches in the Basic Pitch test fixture matched; this does not establish complex-music or performance-score accuracy.
- The Windows installer is unsigned. Pristine Windows systems and this round's native Mac installation still need further acceptance. First model downloads require a network connection; acceleration depends on the engine and hardware.
