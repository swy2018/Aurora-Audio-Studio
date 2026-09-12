# Aurora 第一批改进与回归验收（未发布）

日期：2026-09-13。源码基线：cfabf93c2c9b062cfd569848d879558d3f9f6497（beta.2，含已同步的 Mac beta.2 文档）。
范围：本轮建议的 1–4 项。未包含创作配方、功能串联、自动显存卸载、大规模 UI/框架重写。没有正式打包、提交、推送、改版本号或覆盖已安装应用。

## 修改与兼容边界

| 项目 | 实现 | 保护措施 |
| --- | --- | --- |
| 可重复验收 | 新增 FirstBatchRegression、Python 任务/成品桥接回归及 Windows 草稿 UI 脚本；本机运行六类功能短样本。 | 使用隔离配置、自建音频、独立成品目录；不下载/重装模型，不使用私人录音。 |
| 原生工作区草稿 | 分轨、扒谱、字幕分别保存素材、选中项、模型、预设、分轨模式和源语言；导航/重启恢复。 | 原子保存、写入失败保留旧数据、损坏草稿保留 recovery 副本；缺失素材路径保留，不删除媒体。修正异步预览串页。 |
| 工作台任务关联 | 支持的 Gradio 生成回调开始时进入任务中心，结束后与成品关联；同一生成的多个输出合并到一个任务。 | 按启动实例和递增序号识别事件；旧进度不复活已完成/取消任务；不伪造百分比；工作台不能走原生文件任务重试。 |
| 定向维护 | 区分文件齐全、仅环境修复、当前固定版本缺失权重补齐、完整部署及无法确认。 | 补齐前列出大小与文件；固定 revision，大小与 SHA256 校验通过后才放入模型目录；不覆盖已有非空文件。无法确认时不假装局部修复。 |

原有模型更新与回退流程继续保留。仅环境修复复用原运行环境安装机制，依赖可能重新下载，旧环境保留；不是任意引擎的通用依赖修复器。

工作台取消需确认，会结束该引擎及其未完成任务，不是单个 Gradio 请求取消。未支持的上游 API 回调保持原行为，不承诺全部 27 个组件都有实时任务跟踪。任务中心观察到的是开始执行后的任务，不是 Gradio 内部尚未执行的排队请求。

Mac 不启用本轮 Windows 任务观察环境变量，原回执路径保持兼容；本轮 Windows 草稿和维护入口没有宣称已移植到 Mac。

## 实测发现并修复的问题

1. 当前 Gradio 6.17.3 不再提供旧代码假定的 component-N DOM ID。实际页面有表单，但原就绪检查使其空白/超时。为缺少自定义 ID 的按钮和音频控件使用 Gradio 的 elem_id 属性，保留上游显式 ID；就绪检查还要求音频确为后端输出，不把参考音频算作成品区。真实 Windows WebView 已复测可见。
2. 新增任务桥接最初在 ACE 的嵌入式 Python 中导入失败。改为按同目录明确路径加载模块后，ACE 重新生成成功；加入不依赖脚本目录位于 sys.path 的测试。
3. 重复使用同一上游缓存音频时，仍为不同任务保存独立回执；若已保存成品被移走，则重新保存，不复用失效路径。
4. 修复损坏草稿字段导致启动异常的边界；失败/恢复保留原文件。
5. 分轨说明不再固定宣称六轨；重复导入已有素材不误报不支持，取消文件选择不再显示失败；其他功能任务不串入当前原生页进度。

## 验证结果

- Windows 非正式 Debug 构建：0 警告、0 错误；运行时使用 WindowsAppSDKSelfContained=true、SelfContained=false。
- 原有 38 行为 + 20 维护 + 27 功能回归 = 85 项通过；新增第一批 25 项通过，总计 110 项。
- 更新流程回归通过。124 个原生静态本地化键无英文缺项；576 条工作台翻译四语言齐全；27 项模型目录一致。
- Python：任务桥接 3、身份/Gradio 6 适配 3、任务回执/缓存结果 1、原有 Mac 回执兼容 2 项通过。
- 精确产品 DOM 谓词：7 个夹具通过。夹具不替代实际 WebView 验收。
- Windows UI：真实文件选择器导入、草稿恢复、跨页保留素材与模式、恢复后处理按钮可用，4 项通过；已打开并目视检查截图。
- Core 共享项目编译通过。Mac 完整项目本地编译未完成：缺少 Avalonia.Desktop / Themes.Fluent / Controls.WebView 和 osx-arm64 运行时缓存（NU1101），未为此替换依赖或改变锁文件。Mac 原生 UI/模型推理未实测。

### 真实模型验收

| 功能 | 已安装模型 | 本轮证据 |
| --- | --- | --- |
| 音乐 | ACE-Step 1.5 XL Turbo | 短音乐生成、有效 WAV、自动收录；修复嵌入 Python 导入后复测成功。 |
| 配音 | Qwen3-TTS CustomVoice | Gradio 回调实际生成短音频并入库。 |
| 声音克隆 | Qwen3-TTS Base | Windows 内嵌页面实际上传自建语音、输入文本、点击生成；任务由 running 到 completed；成品预览可打开。 |
| 歌声转换 | Seed-VC 44.1k | 自建语音短样本实际推理并收录最终 WAV，不把流式播放列表作为最终成品。不是长歌曲质量评测。 |
| 分轨 | BS-RoFormer Vocals | 9.6 秒自建音乐素材输出人声/伴奏；最终构建另经原生“开始处理”按钮完成并入库。 |
| 扒谱 | Transkun | 同一自建音乐素材输出可解析、含音符事件的 MIDI。 |
| 字幕 | Whisper large-v3-turbo | 自建合成语音输出可解析字幕。 |

以上均记录 CUDA。验证的是短样本实际完成、文件可解析及成品关联，不是专业听感、音色相似度、长素材性能或全部备选模型的质量保证。

本轮没有实际执行用户模型的安装、更新、局部修复或卸载；维护的写入/取消/坏哈希/并发变化保护使用独立夹具验证。正式发布前仍需专门覆盖安装/升级包、真实中途取消、长任务与 Mac 实机。

## 本地证据

证据根目录：../audit/first-batch-2026-09-13/（Git 忽略，未上传）。

- behavior-final.log、update-flow-final.log：回归输出。
- ui-verified/ui-results.json、ui-verified/draft-ui.png：草稿和文件选择验收。
- ui-final-build/ui-results.json：最终构建再次四项通过；重复导入显示“所选素材已在列表中，无需重复添加”，没有不支持素材的误报。
- voice-ui-ready.png：默认声音克隆工作台实际可见。
- task-ui-running.png：真实生成中的任务；最终版本另去掉重复“处理中”字样。
- result-preview.png：真实生成成品预览；未自动播放。
- results-final.png：最终构建原生分轨完成后成品库。
- ui-state/tasks.json 与 Projects：默认克隆和最终分轨任务及成品关联。
- engines/{model}/acceptance.json：Qwen Custom、Seed-VC、RoFormer、Transkun、Whisper 的结果。
- engines-recheck/ace-step/acceptance.json：修复后的 ACE 成功证据。
- baseline-app/：修改前非正式构建对照。保留，没有执行清理。

测试器早期失败也保留：文件选择器多窗口 JSON 被脚本误判为错误，以及控件查找时序问题。修正测试器后四项通过；没有将误报记录当作通过。ACE 首次真实运行失败经修复后才计为通过。

## 复测命令

在仓库根目录运行：

~~~powershell
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug
dotnet run --project work/audio-studio/AuroraAudioStudio.UpdateFlowTests -c Debug -- work/audio-studio/AuroraAudioStudio.iss
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug --no-build -- --strings .
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug --no-build -- --catalog . --check
py -3 -m unittest discover -s work/audio-studio/AuroraAudioStudio.BehaviorTests/Tools -p 'test_*.py' -v
py -3 work/audio-studio/AuroraAudioStudio.Mac/Runtime/test_result_bridge.py
dotnet build work/audio-studio/AuroraAudioStudio/AuroraAudioStudio.csproj -c Debug -p:Platform=x64 -p:SelfContained=false -p:WindowsAppSDKSelfContained=true --no-restore
~~~

实机短样本入口需要显式批准计算资源占用，不能在无授权时作为普通单元测试运行：

~~~powershell
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug --no-build -- --engine <feature> <model-id> <isolated-evidence-root> <sample-path>
./work/audio-studio/tools/Test-UtilityDrafts.ps1 -AppPid <isolated-app-pid> -Sample <sample-path> -EvidenceRoot <evidence-root> -TestPicker
~~~

UI 脚本使用 winapp；可见内嵌页面测试使用 Playwright CLI，仅连接隔离 WebView 调试端口。正式应用没有增加调试端口。

## 实现依据

[Hugging Face Hub model_info](https://huggingface.co/docs/huggingface_hub/package_reference/hf_api)：固定 revision 查询文件元数据与 LFS 哈希。Gradio 适配另核对本机实际 6.17.3 源码、配置与 WebView DOM，不依据 HTTP 200 或仅有配置就声称工作台可用。
