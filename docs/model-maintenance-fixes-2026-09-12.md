# Windows 模型维护修复记录（未发布）

日期：2026-09-12。源码基线：`c5021d9f379b7a7d910d67851f96487b60f1c185`，版本标签仍为 `2.0.0-beta.1`。

本机安装版为 2.0.0-beta.1，旧 `work/aurora-fix` 仍是 1.9.0。因此本次从官方仓库另建 `work/aurora-maintenance-2.0` 修复，未覆盖旧源码／交接文档，未提交、推送、安装或发布。

## 根因与修复

| 问题 | 证据与处理 |
| --- | --- |
| P1：检查／安装结束后进度提示重现 | 全量模型检查、模型安装和 Windows 应用更新三处 `Progress<T>` 回调均能调用 `ShowGlobalUpdate`，旧回调没有完成状态保护。改为可结束的 `OperationProgress<T>`，在 UI 队列执行时再次检查生命周期及取消状态；结束后不可重开面板。 |
| P2：空白“安装详情” | XAML 中无条件显示 Expander。改名“运行日志”，无日志时隐藏，每次操作重置展开状态，有日志才显示，详情横向撑满。 |
| P1：下载完成仍报文件占用 | `FileStream(... FileShare.Read ...)` 尚未释放就 `File.Move`。后台测试真实复现 Windows IOException；改为释放输出流后再提交，并检查下载长度与续传起点。旧文件和不完整下载不会被错误提升为成功。 |
| P1：一个上游超时使全部检查失败／被误判取消 | 各模型检查设置有界超时，区分网络超时和用户取消。单个失败返回该模型的结果，不丢弃其余结果。安装阶段的网络超时也不再报成用户暂停。 |
| P2：“检查／修复”最新版不修复 | 原逻辑只在未安装或有新版时调用安装流程。现允许确认后重新校验并部署环境，并明确提示可能重新下载文件；仍保留运行中进程检查和原事务保护。 |
| P1：取消批量更新却继续下一个模型 | 单项取消返回明确标记，批量循环停止。检查、安装、回退、卸载、应用更新及启动自动检查共享互斥，避免竞争全局提示或运行环境。 |
| P2：失败原因难以找回 | 安装／修复按操作保存阶段和异常日志至 `Logs/model-maintenance-*.log`；模型中心标题下新增“查看维护日志”。浮层只保留最近活动，操作结束可以关闭而不丢掉诊断记录。 |

新增界面文本提供简中、繁中、英文、日文。未更改六功能分组、默认模型、正式版本／日期版本检测规则或可选模型下载确认规则。

## 同类问题检查

从已知的 `new Progress<ModelCheckProgress>` 扩展到全部 `new Progress<...>` 以及 `ShowGlobalUpdate` 调用。Windows 全局面板的三条路径已统一保护。任务队列已有结束状态防护，不重复替换；Mac 独立应用更新页仍使用普通 Progress，其末端状态覆盖风险作为 Mac 后续实测项，本次没有修改 Mac UI 或宣称 Mac 验收通过。

同时检索 `File.Move(partial...)`：MacAppUpdater 的正常下载在流作用域结束后再校验和移动，不具有本次 Windows 下载的同一个文件占用问题。

原更新回归中只认旧字段名 `modelInstallFlow` 的断言，已改为检查安装方法必须获取并释放共享维护锁，并补充三类进度、空日志和批量取消契约。新行为测试纳入默认业务测试入口，现有 CI 执行该入口即可覆盖，无需新增发布触发器。

## 验证命令与范围

最终后台验证结果：38 项原有业务检查、20 项维护回归、更新流程回归、123 个原生界面本地化键、576 条工作台四语言翻译、27 项能力目录一致性均通过；共享 Core 和框架依赖模式 Windows Debug 编译均为 0 警告／0 错误，`git diff --check` 通过。模型卡片的实际 XAML 点击事件仍绑定本次修改的 `ModelUpdateButton_Click`。

```powershell
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug -- --maintenance
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Debug -- --strings .
dotnet run --project work/audio-studio/AuroraAudioStudio.UpdateFlowTests -c Debug -- work/audio-studio/AuroraAudioStudio.iss
dotnet build work/audio-studio/AuroraAudioStudio.Core -c Debug
```

维护回归采用内存 HTTP 响应和独立临时目录，包含完成后排队回调、取消后回调、下一次操作、真实 Windows 文件句柄提交、续传、短文件保留、元数据超时、批量部分失败、安装超时及持久日志。没有访问实际模型下载端点，也没有创建或修改真实模型环境。

Windows 自包含构建的默认还原缺少 `Microsoft.NETCore.App.Runtime.win-x64` 10.0.12，联网还原未完成。为验证 C#/XAML，使用现有缓存完成框架依赖 Debug 编译，命令行设置 `SelfContained=false`、`WindowsAppSDKSelfContained=false`，未改源码发布配置或降级依赖。正式自包含打包仍需恢复相应运行时包后单独验证。

按用户不弹窗要求，本次不启动新 UI、不做真实模型安装／推理、不替换正在运行的程序。因此这些结果不能当作安装版 UI 或全部模型的实机验收；发布前仍需确认右上角提示自动消失、日志入口及真实安装／修复流程。

## 官方依据

- [Progress 回调通过捕获的上下文异步交付](https://learn.microsoft.com/en-us/dotnet/api/system.progress-1?view=net-10.0)
- [FileShare 对打开文件的共享限制](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0)
