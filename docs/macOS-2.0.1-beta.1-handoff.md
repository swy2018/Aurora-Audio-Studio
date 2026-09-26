# Mac 2.0.1-beta.1 构建交接

本次先发布 Windows 与源码，Mac 公开安装包保持 2.0.0。请从 `v2.0.1-beta.1` 标签或同名 Source.zip 开始，不要使用历史的 1.9.x 源码。源码包不包含本机模型、运行环境、凭据或测试素材。

## 已同步的源码

- 共享 WAV／MIDI／SRT 完整结构校验、音轨预检和成品元信息。
- 任务记录写入失败保护、设置读取失败保护、产品版本及来源关联。
- 历史库文件缓存；更新与外部修改仍会使缓存失效。
- Mac 原生结果摘要、分轨送入 MIDI 草稿、功能／状态筛选、自定义预设与适用素材说明。
- Mac 活动消息四语言重渲染，以及由实际引擎结果回传的设备信息。工具类任务保留已实现的 CPU 路径；没有擅自启用未验收的 MPS。

Windows 上已完成 Mac 项目编译与 102 项可移植共享测试。原生 Unix 环境变量、进程树取消和退出码三项在 Windows 上明确跳过。这不等于 Mac UI、推理、签名或公证通过。

## 在 Mac 上验证

```sh
dotnet run --project work/audio-studio/AuroraAudioStudio.MacTests -c Release
python3 -m unittest discover -s work/audio-studio/AuroraAudioStudio.Mac/Runtime -p 'test_*.py'
```

运行原有 `work/audio-studio/tools/build-macos.sh`。本次源码的应用版本为 `2.0.1-beta.1`，该脚本会换算数值构建号。先设置独立的 `AURORA_TEST_DATA_ROOT` 和 `AURORA_BUILD_ROOT` 做 QA；**发布构建必须取消 QA 环境变量并重新构建**。沿用 `docs/macOS-release.md` 的 Developer ID 签名、公证和装订流程，不复制 Windows 模型运行目录。

必须在实机补验：

1. 四语言及日语字体、模型选择与自定义预设、音频预览和成品摘要。
2. 音乐、配音、声音克隆、歌声、二轨／多轨、钢琴／多乐器 MIDI、字幕的短样本；核对真实文件和设备，不能用“已连接”代替产出。
3. “全部分轨转 MIDI”保留已有草稿，六轨中的额外 instrumental 混音不重复导入；单独选择该混音仍可转换。
4. 禁止写入／拔掉保存目录后仍可查看记录，任务被安全阻止，恢复入口可用；检查退出与重新打开。
5. 核对应用内 `Runtime/bin` 的 FFmpeg、FFprobe 和动态库；PCM WAV 在无外部工具时仍可预检。
6. 下载通道、2.0.0 到 Beta 的升级、回退、签名、公证与首次打开。

验收通过后向已有 `v2.0.1-beta.1` **预发布**补充：

- `Aurora-Audio-Studio-2.0.1-beta.1-arm64.dmg`
- 同名 `.sha256`，内容必须包含准确 DMG 文件名。

不要替换 Windows 资产，不要把该 Beta 标成 Latest，不要删除现有正式版。补包后运行 `work/audio-studio/tools/export-release-assets.ps1`（需要 PowerShell 与 gh，或按相同 schema 导出真实 GitHub 资产），同步官网同源下载清单，再验证 Mac 的 Beta 下载实际指向新 DMG。

## 结果精度边界

结构合法不代表逐音符准确。MIDI 转写仍需人工校对、节奏整理与制谱；不应将其宣传为无需复核的演出总谱。
