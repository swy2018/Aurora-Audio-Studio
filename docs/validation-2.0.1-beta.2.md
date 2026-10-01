# Aurora 2.0.1-beta.2 修复与验收

状态：Windows 安装包、源码与 Mac Beta 2 安装包已发布；Mac 原生验收见文末补包记录。基线 `7b61bbd2fcfd0f17f37a81f411fe3a6790d9d2f6`，交付源码 `7f99ed4a00b3e647c3648210b42ca38fc7337588`，标签 `v2.0.1-beta.2`。

用户批准修复静态报告经复核成立的 A01–A07 并发布 Beta 2；A08 重复字典键结论已证伪，不修改诊断逻辑。Windows 先构建和发布，Mac 新安装包由 Mac 原生构建验收后补充。

## 修复与证据

- A01：卸载改为四个明确偏好/历史文件的白名单，不再递归删除配置树。新增安装器源码契约与 CI 真实安装/卸载保留测试。
- A02：Mac 设置先验证再写入；主题不释放工作台。运行目录/安全模式变更及工具启动/重试保护活动工作台。
- A03：两端显式重试调用队列原子入口；普通执行仍拒绝已取消记录，保留尝试身份和重复提交保护。
- A04：哈希读取句柄先释放再清坏缓存。Windows 模拟 HTTP 的坏 416、最终哈希失败和再次下载已通过，不执行安装器。
- A05：Windows 和 Mac 共享字幕导出实现；空 SRT 的识别 JSON 随行并避让同名文件。
- A06：项目语义结构校验发生在读取/导入保存前；坏项保留恢复原件，不使整库失败；旧默认字段和扩展字段仍可读。
- A07：Mac 模型与轨道模式同步，执行入口二次校验。
- R01：失败注入在旧实现复现了活动环境被改坏；新实现使用固定候选路径和验证后原子入口切换，失败/取消保留旧环境。
- R02：旧实现受控测试复现成果索引被后续旧对象覆盖；存储服务序列化读改写，保留追加式任务/成果历史并用唯一临时文件发布。
- R03：Mac 接入已有实例/config/DOM 就绪契约；Mac WebView 原生行为已在补包验收中验证，见文末记录。
- R04：隔离不同下载计划的暂存；明确只检查可追踪权重。Git/PyPI 自动更新没有在本次新增。
- UI 补充：自动控件检查之外，截图复核发现窄窗口日语空白状态标题裁切；改为按控件下方实际空间显示提示，并明确内容居中。操作入口不受隐藏提示影响。

## 验收边界

- Windows 完整行为回归、更新流程、27 项能力目录、版本同步与官网通道策略均通过；其中新增审阅专项 20 项通过（含两个空设置字段恢复测试）。
- Mac 共享逻辑 113 项、环境故障注入 4 项通过。Windows 与 Mac 源码最终版本编译均为零警告、零错误；Mac 编译不等于原生运行。
- Windows 独立配置的页面入口、版本和四语言切换：960×640 窄窗口 19 项、1280×900 常规窗口 8 项 UI 自动检查通过；四语言截图复核确认空白状态提示不再裁切。此次未启动工作台模型；只覆盖入口和当前可见区域，不声称完整生成流程已重验。
- 模型生命周期的卸载测试依赖 POSIX 打开文件的重命名语义，Windows 本机出现平台性拒绝访问，保留原断言并转到 Linux CI 执行，不将跳过视为通过。
- [最终候选 CI](https://github.com/swy2018/Aurora-Audio-Studio/actions/runs/36770129309) 全部通过：25 项 Python 环境/生命周期测试、安装包生成、安装后响应窗口、卸载保留五类嵌套用户文件并只清除四项偏好文件。

## 发布核验

[GitHub Release](https://github.com/swy2018/Aurora-Audio-Studio/releases/tag/v2.0.1-beta.2) 为非草稿预发布，标签对应上述已验收源码。Windows 安装包直接取自该次 CI，不以本机构建替换。两个交付文件及各自校验文件的远端大小、SHA-256 digest 均与本地一致。

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| Windows Setup x64 | 79,364,284 | `c5c28d9684a2e6a5ccadc1cfe61d55d812bf9d1ea2f0de8222dd3c282fe49fb5` |
| Source ZIP | 11,959,617 | `d1aa58e9fda8a1d87f25a96d47368f37b7a79681193cbddd8e0d8613cb0e92a6` |
| Mac arm64 DMG | 102,836,346 | `d5dcae17853f17bcf1be00ad3b61093c558c36a7c191be216d01ff138448e840` |

源码归档包含 239 个文件，含许可证、Windows/Mac 源码及 Mac 交接；没有构建输出、虚拟环境、模型或用户配置。归档内的验收页保留冻结时状态，本页与 Release 正文补充发布后的结果。Windows 包未签名。正式版 Latest 仍为 2.0.0，原正式版和 Mac Beta 1 的资产大小与 digest 均未改变。

Windows 发布阶段没有在用户电脑安装/卸载 Aurora，没有下载或重装用户模型，没有清理该阶段临时证据。Mac 原生界面、签名、公证、模型推理和长素材均不由 Windows/共享测试替代。当前工作不重截官网展示图片。

实现依据：[C# using 作用域](https://learn.microsoft.com/dotnet/csharp/language-reference/statements/using)、[Inno DeleteFile](https://jrsoftware.org/ishelp/topic_isxfunc_deletefile.htm)、[Python venv 固定路径](https://docs.python.org/3.11/library/venv.html)、[Avalonia NativeWebView](https://docs.avaloniaui.net/controls/web/nativewebview)。

## Mac 补包

2026-10-01：Mac 安装包已完成签名、公证和原生验收，见 [Mac Beta 2 验收记录](validation-mac-2.0.1-beta.2.md)。
