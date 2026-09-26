## 2.0.1-beta.1 — 2026-09-27

- Windows 2.0.1-beta.1 与源码更新。Mac 当前正式版保持 2.0.0；新版安装包将在 Mac 构建与验收后补充。
- 修复日语选择框字体拥挤与对齐，活动记录可随四语言界面重新显示；空白预览不再叠加播放控件。
- 增加素材音轨和解码预检、成品时长／声道／MIDI 音符／字幕条数摘要；分轨可直接加入 MIDI 草稿并保留来源记录。MIDI 仍需人工校对。
- 加强 WAV、MIDI、SRT 结构校验，避免缺失或损坏的结果被报告为成功。存储异常时保留旧记录，暂停新任务并提供重试入口。
- 模型检查明确校验范围；支持固定版本缺失及损坏文件的定向修复。补充功能与状态筛选、预设说明和自定义模型状态。
- 优化大型处理记录库刷新，官网增加已核验下载清单回退；正式版与 Beta、Windows 与 Mac 下载保持独立。
- Windows 安装包未签名。Beta 建议先用短素材试用，升级前请备份重要配置与作品。

- Windows 2.0.1-beta.1 and updated source. Mac Stable remains 2.0.0; a new Mac package will follow native build and acceptance.
- Improve Japanese picker typography and alignment, re-render activity messages when switching among four UI languages, and remove playback controls from empty previews.
- Check input audio streams and decoding before processing. Show duration, channels, MIDI notes and subtitle counts. Send stems to a MIDI draft with source links preserved. MIDI still needs human review.
- Strengthen WAV, MIDI and SRT validation so missing or damaged output cannot be reported as success. Preserve records, pause new work and offer retry when storage is unavailable.
- State model verification scope clearly and repair missing or damaged files for the installed revision. Add feature/status filters, preset explanations and a Custom model state.
- Speed up large processing libraries and add a verified website download snapshot fallback. Stable/Beta and Windows/Mac packages remain separate.
- The Windows installer is unsigned. Test Beta with short inputs and back up important settings and work before upgrading.
