## 2.1.0-beta.1 — 2026-11-27

- 本轮提供 Windows 2.1.0-beta.1 与完整源码；Mac Beta 暂保留已验收的 2.0.2-beta.1，待 Mac 原生构建、签名及验收后补包。Stable 仍为 2.0.1。
- 字幕校对在格式错误、取消保存或写入失败时保留编辑内容；导出采用临时目录校验后完成，避免不完整文件被误认为成品。
- 增加设置与草稿恢复入口，恢复前保留原始文件；改进 Mac 草稿保存失败时的退出选择、工作台启动状态和深色主题对比度。
- Windows 更新后优先以普通桌面用户身份启动；回退至不支持安全重启的旧正式版时提示手动重新打开，保留用户设置和作品。
- BS-RoFormer 与 YourMT3 区分正式源码、未发布修订和权重更新。正式版优先，无正式发布时检测日期版本；较新或分叉的本地源码不会被自动降级。
- Windows Basic Pitch 新安装与主动维护使用较轻的 ONNX 环境，保留可工作的旧环境。短样本与 TensorFlow 输出音符、时间一致；不代表识别精度提升。
- 新增 MIDI 乐谱整理：多文件声部合并、节拍量化、乐器与音域提示、可选音符简化、MusicXML 和整理版 MIDI 导出、合成音色回放校对。原始 MIDI 保留，正式演奏前仍需人工复核。
- 补充分轨完整性和时长检查、关键回归及随包工具完整性核验。Windows 包未签名；全新操作系统、所有可选模型和长素材不在本轮验收范围。

- This release provides Windows 2.1.0-beta.1 and full source. Mac Beta remains the accepted 2.0.2-beta.1 until native building, signing and acceptance are completed. Stable remains 2.0.1.
- Subtitle edits survive validation errors, canceled saving and write failures. Exports are staged and validated before final promotion so incomplete files are not presented as finished results.
- Add explicit settings and draft recovery with original-file preservation. Improve Mac exit choices after draft-save failures, workbench startup state and dark-theme contrast.
- Windows updates request an ordinary desktop-user restart. Rollback to older stable versions without safe restart support asks users to reopen manually, preserving settings and creations.
- Track BS-RoFormer and YourMT3 formal source releases, unreleased revisions and weights separately. Prefer formal releases; use dated revisions only when no formal release exists. Newer or diverged local source is not automatically downgraded.
- New Windows Basic Pitch installations and user-requested maintenance use a lighter ONNX environment while preserving working legacy environments. Notes and timing matched TensorFlow on the short fixture; this is not a transcription-accuracy improvement.
- Add MIDI score preparation: synchronized part merging, rhythm quantization, instrument/range warnings, optional note simplification, MusicXML and arranged-MIDI export, and synthesized reference playback. Original MIDI is preserved; human review is required before performance.
- Extend stem completeness/duration checks, critical regression coverage and bundled-tool verification. The Windows installer is unsigned. Pristine operating systems, every optional model and long inputs were not covered in this acceptance.
