# MIDI 乐谱整理 / MIDI score preparation

适用范围：Windows 2.1.0-beta.1 及同版本 Mac 源码。Mac 安装包需另行原生验收；旧 Mac 下载包不包含这一入口。

在“AI 扒谱（MIDI）”页选择“整理 MIDI 为乐谱”，可同时选取多个 `.mid` / `.midi` 文件；也可从成品库打开一个 MIDI 后进入整理。多个输入须使用同一时间起点。文件名和声部名会用于新总谱，原文件始终只读。

1. 选择节奏量化网格，包括普通音符和三连音；设置固定拍号、调号与首拍偏移。默认保留首个 MIDI 的速度变化，也可手动指定四分音符 BPM。多个输入按实际秒数对齐；源拍号变化不会自动保留，会有明确提示。
2. 编辑声部名称、选择乐器和音域。默认保留全部音符；只有主动选择“最高音旋律”或“同起音最多四音和弦”才会简化。音域提示不会自动移调或删音。
3. 点击“检查整理结果”查看声部、合并/省略音符和音域提示，再导出到选定目录。导出完成后可在窗口中播放合成音色校对，也可打开 MusicXML，交给 MuseScore Studio 等制谱软件继续编辑。

每次导出创建独立目录，包含：

- `score.musicxml`：MusicXML 4.0 实音总谱，可用于支持该格式的制谱软件。
- `score-arranged.mid`：经过所选量化与简化的独立 MIDI。
- `score-review.wav`：节奏、音高校对用合成音色，不是原乐器音色。
- `score-report.json`：源文件路径、选项及整理统计。分享前请注意其中的本地路径。

限制：不内置完整制谱编辑器，不自动校正声学识别错误；原始弯音、踏板等演奏控制仍保留在原始 MIDI，不复制为整理版的完整演奏表达。支持 PPQ 的 Standard MIDI 0/1，不支持独立时序的格式 2 或 SMPTE 时基。单文件 32 MB、合计 128 声部、10 万源音符、4,000 小节、30 万跨小节片段及 30 分钟校对音频为安全上限。取消或失败的未完成导出保留为 `.partial`，不会被当成成功结果。正式演出仍需音乐人员复核音符、配器及排版。

## English

Windows 2.1.0-beta.1 adds **Prepare MIDI score** on the Transcription page and in MIDI result previews. Select synchronized MIDI files, choose a rhythm grid/meter/key/offset, and review each part's name, instrument and range. Original files are read-only. Melody or four-note chord simplification is opt-in.

Each export creates a new folder with MusicXML 4.0, arranged MIDI, synthesized reference audio and a JSON arrangement receipt. The score is concert pitch. Open MusicXML in your notation application and use the reference player to check rhythm/pitch. This is not a full notation editor or a claim of performance-ready transcription. The selected meter is fixed; expressive controls remain in original MIDI. Check local paths in the receipt before sharing. Mac source includes the workflow, but a Mac binary requires separate native acceptance.
