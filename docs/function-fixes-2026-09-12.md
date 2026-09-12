# Aurora 功能修复记录（未发布）

日期：2026-09-12。基线：2.0.0-beta.1 / c5021d9。工作区：work/aurora-maintenance-2.0。

本记录接续 function-review-2026-09-12.md 的 F1–F9；保留上轮模型维护修复。没有改版本号、提交、推送、正式打包、安装覆盖或发布，也没有下载模型、启动图形窗口或执行模型推理。

## 处理结果与边界

| 审查项 | 本次处理 | 验证 |
| --- | --- | --- |
| F1 假连接 | 每次启动生成独立实例标记，经 Gradio 桥接进入 /config；同时要求可执行事件、输入和音频输出配置。WinUI 导航后继续检查实际 DOM 中可见输入、可点击的后端按钮及音频区域，才显示已连接。 | 错页/错实例拒绝、合法配置接受；真实 Gradio 6.17.3 的进程内 /config 路由测试通过；DOM 7 个夹具测试通过。没有真实 WebView/模型验收。 |
| F2 隐式中止生成 | 同时只允许一个创作工作台；切换到不同模型时要求用户返回原工作台手动结束引擎，同引擎可以重连。不再静默停止其他创作进程。失败/取消重连只清理本次新启动的进程，保留已有引擎。 | 真实服务调用配合存活进程句柄的隔离测试，通过阻止切换、保持进程及取消不误杀检查。没有中断真实模型。 |
| F3 路径变更 | 保存目录保护覆盖维护锁、批量更新、工作台启动、队列中待执行及暂停的重试，不只判断已启动的后端进程。 | 暂停重试拥有/释放操作状态的行为测试，以及 UI 调用契约检查通过。 |
| F4 静默检查占锁 | 所有手动维护入口先取消并等待静默检查退出，再取得维护锁；批量更新同样接管。取消后的静默结果不能回写。 | 维护互斥/取消测试及接管顺序源码契约通过；未执行可见 UI 点击验收。 |
| F5 无效显存开关 | Windows 两处及 Mac 设置禁用自动释放选项，明确“暂不支持，请手动结束引擎”；保留原设置字段兼容性，不假装已经支持卸载。 | 禁用状态/四语言检查通过。**自动卸载本身没有实现**：目前没有逐引擎验证过的任务完成/卸载契约，不能安全地靠结束进程实现。 |
| F6 旧进度串入重试 | 进度回调绑定该次执行的 CancellationTokenSource 身份；只允许当前轮次且未取消的回调写入。 | 原探针由 3% 被旧 85% 覆盖，变成稳定保持新任务 3%；永久回归通过。 |
| F7 分轨模式不一致 | 手动换模型时同步两轨/多轨模式并避免事件递归；模式明确传入执行与重试，后端拒绝历史记录里的不匹配组合。 | UI 传参契约和后端不匹配拒绝测试通过，未执行真实分轨。 |
| F8 丢失成品误建目录 | 成品不存在时提示；打开目录不再创建目录，缺失位置只报告错误。 | 缺失路径不生成目录、不启动 Explorer 的后台测试通过。 |
| F9 诊断凭据 | 在原有路径脱敏前处理 Authorization、常见 token/key/password/secret 字段，覆盖环境文本、JSON 和查询参数。保留导出前确认。 | 全部使用虚构凭据；敏感值被隐藏，非敏感查询参数保留。不是所有可能秘密格式的识别保证。 |

附带修复：安全模式开关使用候选设置原子保存；写入失败保留原值、还原开关并显示错误。Mac 更新下载进度使用同一 OperationProgress，在下载作用域结束后拒绝迟到回调。旧 Python 回执测试明确按 UTF-8 读取，修复在 Windows 中文路径上的测试编码误报。

## 最终验证

- 38 原有检查 + 20 维护回归 + 27 功能回归，共 85 项通过。部分新增项是源码/UI 契约检查，不等同运行窗口实测。
- Windows 非正式、框架依赖 Debug 编译：0 警告、0 错误。
- 更新流程回归通过；124 个原生本地化键无英文缺项，576 条工作台四语言翻译齐全；27 项模型目录一致性通过。
- Python 身份桥接 2 项、回执处理 2 项通过；Node DOM 谓词 7 个夹具通过。
- 本机 Gradio 6.17.3：构造纯表单，经 FastAPI 进程内客户端读取真实 /config，身份保留且 C# 正向检查通过。无监听服务、浏览器、模型加载或推理。测试客户端产生第三方弃用警告，不影响结果；没有为消除警告修改模型环境依赖。
- 原始三项故障探针均不再复现；合成有效音频的成品导入和去重仍通过。
- git diff --check 通过；没有清理测试证据或用户文件。

主要命令（仓库根执行）：

~~~powershell
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug --no-restore
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug --no-build -- --strings .
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug --no-build -- --catalog . --check
dotnet run --project work/audio-studio/AuroraAudioStudio.UpdateFlowTests -c Debug --no-build -- work/audio-studio/AuroraAudioStudio.iss
dotnet build work/audio-studio/AuroraAudioStudio/AuroraAudioStudio.csproj -c Debug -p:Platform=x64 -p:SelfContained=false -p:WindowsAppSDKSelfContained=false --no-restore
python work/audio-studio/AuroraAudioStudio.BehaviorTests/Tools/test_workbench_readiness.py
python work/audio-studio/AuroraAudioStudio.Mac/Runtime/test_result_bridge.py
~~~

真实 Gradio 配置证据在 audit/function-review-2026-09-12/gradio-config.json（本地忽略目录）。通过测试脚本的 --gradio-config 参数产生；C# 入口 --workbench-config 消费同一文件。DOM 测试脚本 test_workbench_ready.cjs 消费 FunctionRegression 产生的 ready-script.js，不使用另写的产品谓词副本。

## 未验证项

Mac 项目尝试按锁文件 osx-arm64 目标、本地缓存还原，缺少 Avalonia.Desktop / Themes.Fluent / Controls.WebView 及 macOS .NET 运行时，故 **Mac 编译未完成**。未更改锁文件或下载替代依赖；共享 Core 及 Windows 编译可用。

Windows 正式自包含构建仍须另行还原正式运行时包并验证；本轮没有执行正式包流程。没有安装替换本机应用，当前已安装版本不会因为这些源码修改而自动获得修复。

发布前仍需对六个功能分别完成真实输入、生成、成品收录、重新打开，及切换/取消/失败重试的可见 UI 验收。不得把这轮后台检查表述成“全部模型都已经实测可用”。

## 实现依据

- [Microsoft Progress<T>](https://learn.microsoft.com/en-us/dotnet/api/system.progress-1?view=net-10.0)：异步回调必须核对当前执行身份。
- [WebView2 ExecuteScriptAsync](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.executescriptasync)：读取返回的 JSON 布尔值，以 C# 有界等待轮询同步 DOM 谓词，不依赖脚本 Promise 自动等待。
- [Gradio 5.49.1 Blocks 配置实现](https://github.com/gradio-app/gradio/blob/gradio%405.49.1/gradio/blocks.py)：get_config_file 生成工作台配置。也读取了本机 Gradio 6.17.3 对应源码，并通过真实路由测试确认当前安装版本可用；未知未来上游变更会明确失败，不退回 HTTP 200 即成功。
