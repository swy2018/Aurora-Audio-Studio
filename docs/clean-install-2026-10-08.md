# 首次安装修复与验收记录（未发布）

日期：2026-10-08（实测）。目标是最终用户不需要预装开发环境，不等于离线首次安装或任意硬件都能运行全部模型。以下保留当日修复与失败证据；2026-10-09 用户另行授权发布 Windows 2.0.2-beta.1 与源码，Mac 新包不在本次交付中。实际发布状态以该版本 Release 为准；本机已安装应用未替换。

## 已实施

Windows 基础工具改为随应用分发，停止运行 WinGet。模型环境自动使用 Aurora 管理的 Python；Windows Whisper 先补齐共享运行组件。Mac 补齐 Git/HTTPS 依赖，并能重试尚未完成的管理环境安装。加入阶段超时、取消收尾、等待反馈和 Python UTF-8 日志。策略依据及兼容边界见 [ADR 0005](decisions/0005-application-owned-bootstrap-tools.md)。

本轮继续补齐 Windows VC++ 运行库前置安装；同版/新版运行库不重复安装，失败或需要重启时不替换 Aurora、不自动重启。CUDA 依赖改为与模型一次解析安装，并缩短新环境路径，修复实测出现的 `WinError 206`。F5 的 Windows TorchCodec 轮子单独固定到与 PyTorch 匹配、带校验值的官方 PyPI 文件。

同时补齐 WebView2 缺失时的官方离线安装，不以 Edge 浏览器存在与否替代运行库检测。VC++ 与 WebView2 安装程序均由 Inno 嵌入，仅缺失/需要时运行，不作为应用常驻工具保留。开发机只下载、验签和检查脚本，没有执行这两个安装程序。

第二轮首次安装测试继续修复：Windows TransKun 固定官方 NCLS 0.0.68 轮子与 NumPy 1.x，Mac 保留官方 ARM NCLS 0.0.70 轮子，均禁止用户侧编译 NCLS。MiniMax 改成依赖与 CUDA 一次解析；Windows Git 子进程使用系统证书库并开启 Git 自身的长路径支持，保留证书校验及无关配置，不改全局 Git 或系统注册表。

随后修复 Windows 首次安装确认中的容量遗漏：Whisper Small 原先仅建议 1 GB，但本次共享程序与权重的实际文件内容已达 5,201,089,491 字节，另有下载归档。首次安装现建议 8 GB；Qwen 首次安装计入 Python/CUDA，经典钢琴不再按单独 165 MB 权重估算整个环境。有现成 Whisper/Qwen 共享组件时保留原权重预算，避免重复计算。新增测试先在旧代码上失败，修正后通过；估算会随上游内容变化，不能保证任何未来版本都恰好占用该大小。

完整链路继续发现并修复：YourMT3 的隐含 Git LFS 要求及 Transformers 5 不兼容；HF 新版 CLI 多文件筛选造成 Seed-VC 漏下主权重，MiniMax/SoulX 同类参数也已修正；Windows 缓存软链接被当作零字节文件；ACE-Step 中断后仅凭目录存在跳过缺失分片。没有修改上游源码或移除完整性、内存保护。

后续实机新装又复现 Demucs 4.1.0 未声明 Windows NumPy 依赖，以及经典钢琴被 Aurora 强制关闭 Numba JIT 后在 `librosa.util.pad_center` 导入时崩溃。分别补齐依赖、恢复该子进程的默认 JIT，实际分轨和转写均复测通过。Mac 的安装与维护原先各维护一份导入规则，导致 YourMT3 维护仍只检查顶层包；现在共用推理导入规则，并验证失败会覆盖旧的健康记录。

Basic Pitch 慢下载的一小时限时测试另外发现真实取消缺陷：测试任务已经抛出取消异常，uv 下载进程仍在运行。核对路径、命令行后仅结束了本次残留 PID 17664，其他应用未关闭。随后用短进程树及真实 uv 离线测试重现旧代码失败，修复为在等待结束的 `finally` 内终止仍存活的任务进程树并有界等待退出，不再仅依赖可释放的取消回调。安装、Git 操作、设备探测、媒体预检、后台处理及 Mac 同步命令调用统一使用此边界；长期工作台的显式生命周期控制没有混入短命令等待器。

## 当前验证结果

- Windows 应用编译：0 警告、0 错误；没有启动 GUI。
- Windows 完整行为回归：Basic Pitch 补验后的源码 333 条 PASS 记录；更新流程复测通过。新增取消检查包括 10 个进程树夹具场景，另有 10 个真实 uv 离线场景，均检查取消返回后的父子进程状态；旧代码在两组检查中均失败。四语言条目此前审计通过：141 个自有键无缺失英文翻译、576 个工作台条目四语言齐全；本次新增两个依赖下载提示键，四语言均补齐。两个旧 ACE 夹具曾把任意 `.bin` 或四分片中的单片当作完整模型，现改为真实加载器接受的文件结构，并新增缺分片必须失败的断言；没有跳过测试或放宽完整性检查。
- Windows 真实隔离工具启动：仅保留系统 PATH，应用自带五个工具启动、Git HTTPS、独立下载 Python 3.10/3.11/3.12、安装并导入 Hugging Face 管理依赖、中文路径音频生成/SoX 处理/ffprobe 校验，共 11 项通过。这一组只证明基础工具；下述模型测试单独记录。
- BS-RoFormer：在新的模型目录内经生产安装器下载 Python、CUDA 依赖、约 699 MB 权重并通过依赖检查。随后用生产后端和任务队列处理 2 秒合成音频，实际使用 CUDA，产生六个分轨及伴奏，共 7 个有效 WAV 并入库。测试后已停止该引擎。此结果不代表原生 UI 自动化、分离质量评测或纯净 Windows 系统验收。
- Qwen3-TTS CustomVoice：独立下载 Python 3.12、CUDA 依赖及权重并通过安装校验；随后从新环境启动真实工作台，通过 `/run_instruct` 提交配音，生成 6.08 秒、24 kHz、单声道 WAV，并成功进入 Aurora 成品库，设备记录为 CUDA。测试后引擎已关闭。当前上游 Gradio 6 有 theme/css 参数弃用警告，未影响这次生成；未把后台 API 验收写成原生 UI 视觉验收。
- F5-TTS：第二轮已经通过生产安装入口完成新环境、依赖和权重准备，再经真实工作台 `/basic_tts` 生成 4.224 秒、24 kHz 单声道 WAV 并入库，设备为 CUDA，测试引擎已关闭。参考语音来自本轮 Qwen 自有生成样本，不是私人录音。
- TransKun：修复用户侧 C 编译依赖后，从新的候选目录完成安装、`pip check`、FNCLS 原生查询和启动验证。实际以 CUDA 转写 9.6 秒合成音符，输出有效 MIDI，12 个音高的顺序与已知输入逐个一致并成功入库。这不是复杂歌曲的转写精度保证。
- YourMT3：实际复现上游 Git LFS 临时仓库下载失败，以及 Transformers 5 缺失 `model_parallel_utils` 的推理导入错误。固定 Windows Transformers 4.44.2，使用官方固定修订与 SHA-256 下载 561,544,628 字节权重后，安装、推理导入和 CUDA 转写通过，MIDI 已入库。输出为 2 轨、5 音符，不是输入 12 音符的精确还原；这一项证明执行链路，不是转写质量达标。Mac 保留已有 4.45.2 配置，只加强实际推理模块检查。
- Seed-VC：首轮因测试的一小时总上限取消，重试复用了依赖缓存；继而检出并修复主权重漏下载与软链接误判。修复后安装、完整工作台及 `/predict` 转换通过，输出 6.072 秒、44.1 kHz 单声道 WAV 并入库，使用 CUDA，测试引擎已关闭。
- ACE-Step：官方 v0.1.8 新运行环境、基础权重和 XL Turbo 四分片均安装通过。测试中实际验证了超时后保留缓存、完整基础组件不重下、缺失 XL 分片能够继续下载。首轮因可用提交空间 19.6 GB 未达到 20 GB 保护阈值而拒绝启动；后续实测可用物理内存 10.68 GB、提交空间 24.07 GB 时，经生产工作台 `/generation_wrapper` 生成 10 秒、48 kHz、双声道 WAV 并入库，记录 CUDA，测试引擎已退出。没有绕过保护或关闭其他应用。仍有上游 TorchAO 扩展兼容警告，但此次生成完成；不据此保证长曲或训练可用。
- Demucs：新环境实测 `pip check` 通过，但 `demucs --help` 因缺失 NumPy 失败。官方 4.1.0 元数据只在 Intel Mac 条件下声明 NumPy；Windows 明确补齐，Mac 也显式约束并探测 `demucs.api`。修复后重新安装并处理 9.6 秒自制音符，生成四个 44.1 kHz 双声道 WAV 并入库，记录 CUDA。生成测试仅系统 PATH，HF/Torch 缓存指向隔离模型根目录；默认 Demucs 权重仍由上游在第一次处理时联网获取，不声称安装后已离线可用。
- 经典钢琴：完整安装 Python/CUDA 环境并下载、核验官方固定权重；首次转写在 Librosa/Numba 处失败。实际失败调用的对照实验只改变 `NUMBA_DISABLE_JIT`，`1` 失败、`0` 通过。修复后相同输入生成有效 MIDI 并入库，记录 CUDA。输出 2 轨、30 音符，输入为 12 音符，存在额外识别，因此仅证明执行链路，不作为准确扒谱的验收。
- 取消修复后的正常路径复测：经典钢琴再次完成 CUDA 转写；Demucs 使用已经下载的测试缓存，在 `HF_HUB_OFFLINE=1` 条件下再次完成 CUDA 四轨分离。均入库成功，未将“杀进程成功”代替正常任务验收。
- Basic Pitch：上轮一小时限时内未完成，后续补验发现大包传输重头下载及 `pkg_resources` 缺失，均已修复并复测。Windows 隔离环境的自动下载、安装、实际转写及成品入库已通过；9.6 秒自制样本输出合法 MIDI，12 个音符的音高顺序全部匹配。使用 TensorFlow CPU 后端，不是 CUDA 验收。详见下方补验记录；原失败证据保留。
- Whisper Small：从无共享引擎状态下载安装 1,424,256,246 字节的官方 r245.4 组件包，再安装 Small 权重，实际以 CUDA 识别 Qwen 测试语音，生成两条非空 SRT，文字与测试句一致并入库。官方该资产未返回 SHA-256，不能把本次长度与解包检查写成官方摘要核验。
- 视频输入：用捆绑 FFmpeg 将上述自有语音制成 6.08 秒 MP4，再经生产字幕入口处理，生成两条非空 SRT 并入库，设备为 CUDA。输入使用中文文件名，未使用私人或外部授权不明的视频。
- Inno 安装脚本：`/O-` 语法检查通过；没有生成安装包，也没有在主机安装或修复 VC++ / WebView2。整套工具准备脚本复跑通过，两项微软前置文件的签名、SHA-256 和安装器版本均已校验。MSBuild 内容项检查确认这两个安装程序不会复制进应用目录。
- Whisper 夹具验证共享程序自动安装、摘要校验、再次安装复用；不执行夹具中的假引擎。
- 共享 Mac C# 测试：本轮复测 113 项通过，构建 0 警告、0 错误；不包含原生 macOS 启动。沙箱内并行构建曾在项目引用目标中无有效错误地退出，保留了 binlog；在普通用户环境单节点重建及运行成功，没有为通过测试修改源码或跳过断言。不能据此断言已定位 MSBuild 的底层故障原因。
- Python 回归：已有 Ubuntu/WSL 隔离环境中 43 项通过，包含环境、生命周期、输入处理、结果入库与字幕编辑器单元测试，新增 YourMT3 实际推理导入失败记录、Demucs 依赖/导入约束。此前 Windows 直接运行有 1 项 POSIX 锁内目录重命名失败，已在真实 POSIX 文件系统复测；没有跳过该测试或放宽断言。

日志保存在工作区 `.maintenance/clean-install-20261008/`。测试文件保留，未执行清理；没有修改真实模型、用户配置或系统工具安装。

关键证据：当前复测为 `windows-build-final.log`、`windows-behavior-final.log`、`update-flow-final.log`、`mac-shared-final.log`；Python 回归为 `posix-fifth.log`，取消测试为 `cancel-fixture-before/after.log`、`cancel-uv-before/after.log`、`cancel-uv-final.log`。模型新装结果在 `model-install/*-install.json`，生成验收在 `new-environment-inference/<模型>/acceptance.json`；新增 ACE 和钢琴分别在 `ace-inference-retry/ace-step/acceptance.json`、`piano-inference-fixed/piano/acceptance.json`，最终正常路径复测在 `final-inference-regression/<模型>/acceptance.json`，视频字幕在 `video-inference/whisper-small/acceptance.json`。早期失败日志仍保留。沙箱的长临时路径、目录移动权限曾阻止测试；改在普通用户短临时路径复测，未放宽生产保护。更新检查器的一次调用漏传 Inno 文件参数，补齐参数后完整检查通过；失败记录未覆盖。

收尾核对：2026-10-08 21:55:57（北京时间），本轮测试目录所属 Python、uv 和行为测试进程为 0，Basic Pitch 候选未激活。没有执行批量清理、Git 提交/推送、正式打包、Release 发布或已安装程序替换。

## 同类依赖检查

由 Windows 通用 PyTorch 安装命令扩展检查 TransKun、BS-RoFormer、YourMT3、Demucs、F5、钢琴转写和 Basic Pitch。实际网络依赖解析发现 F5 的 TorchCodec 被路由到缺少 Windows 轮子的 CUDA 索引；固定匹配的官方 Windows 轮子后解析通过。随后真实导入发现 Datasets 2.14.4 / PyArrow 25 的 `PyExtensionType` 不兼容，已在 Windows 与 Mac 的 F5 安装配置加入 `datasets>=3`。该约束只用于 F5；TransKun 和 MiniMax 的独立修复见下表。

F5 第一轮只做了禁止联网的依赖导入检查，第二轮已经补齐整模型安装和实际生成，见上节。MiniMax 完成固定 Diffusers 提交拉取、依赖 dry-run 与修正后的 HF 文件清单 dry-run（23 个文件），**没有安装 MiniMax 或下载其权重，也没有生成验收**。SoulX 只检查了三个预期文件的 dry-run 清单，没有下载模型。Demucs 与经典钢琴已补完实际安装和输出；Basic Pitch 的独立 `basic-install` 冷安装限时测试未通过，不计入已验收引擎。当前共有 10 个 Windows 引擎有真实输出记录，不代表全部可选模型及所有平台均已通过。

本机网络对照：同一官方 NumPy 文件的 4 MiB 范围请求，HTTP/1.1 用时 1.62 秒；HTTP/2 在 30 秒截止时只读到 2,390,840 字节。独立空环境的 uv 单包安装用时 184.62 秒。该结果解释了当前网络下依赖下载显著变慢的一部分，不能推广为所有电脑或所有网络的结论。没有为此关闭 TLS 校验、改系统代理或切换非官方镜像；证据为 `pypi-range-probe.log`、`pypi-http2-range-probe-02.log`、`uv-single-download-probe.log`。换用受限 Torch 索引的独立解析也没有显示加速（6 分 49 秒，对照原流程 3 分 53 秒），因此未按这个假设修改 Seed-VC 安装源。

后续 Basic Pitch 冷安装出现长时间低速，且大包在约 43 分钟时重新开始下载。缓存文件数量、写入时间持续变化，不是仅凭“仍在处理”心跳判断有进展。独立托管 Python 探针中，pip 首次下载同一 NumPy 用时 44.61 秒，所得文件 SHA-256 与官方值一致；随后完整的无缓存下载安装对照在 39.22 秒只收到约 4.3/15.8 MB，触发 SHA-256 不匹配并拒绝安装。前一次“只下载”的时间不能直接和 uv 的“下载加安装”作性能结论；也不能把一次传输截断推断为上游恶意变更。没有采用 pip 替换、下载源切换或并发参数改动。证据为 `pip-probe-bootstrap.log`、`pip-download-probe.log`、`pip-full-probe.log`、`basic-first-install.log`；Basic Pitch 最终在一小时测试限额取消，候选环境未激活。

### 同类缺陷追踪（2026-10-08，正确性问题而非安全漏洞结论）

根因是不受控的宿主环境和后续依赖解析改变了已经准备好的运行条件。源码检索使用 `ncls`、`"--upgrade"`、`torch==` 和 Git 启动配置，逐一读取调用者与实际安装输出；没有把字面相似直接当作缺陷。

| 位置 / 旧行为 | 影响 / 置信度 | 证据与处置 |
| --- | --- | --- |
| TransKun 自动解析 NCLS 0.0.70 | 高 / 高 | 没有 Windows 轮子，触发 C 编译；实测链接阶段失败。固定官方兼容 Windows 轮子，检查实际 FNCLS 查询。Mac 有官方 ARM 轮子，单独保留其版本。 |
| MiniMax 的第二次 `pip install --upgrade` | 高 / 高 | dry-run 明确显示删除 `torch==2.8.0+cu128`、安装 PyPI `torch==2.14.1`。改为完整依赖同次解析，修复后不替换 CUDA / torchaudio 组合。 |
| 捆绑 Git 继承全局 `http.sslBackend=openssl` | 中 / 高 | 中文路径下加载 CA 文件失败；命令级 Schannel 复测可以获取同一官方提交。只修改子进程配置，未关闭 TLS 校验。 |
| Git 深层缓存目录的路径限制 | 中 / 高 | 实际 Diffusers checkout 报 `Filename too long`；启用子进程 `core.longpaths` 后完整依赖解析通过。未启用 Windows 功能或修改注册表。 |
| YourMT3 上游下载器调用 Git LFS | 高 / 高 | 捆绑 Git 不含 LFS，下载失败后的上游清理又报拒绝访问。改为获取官方同一固定权重，并核验长度、SHA-256 后才替换；旧文件保留，坏响应不能覆盖。 |
| YourMT3 自动解析 Transformers 5 | 高 / 高 | `mt3_infer` 顶层导入和 `--help` 不覆盖真实 T5 模块。实际推理导入报缺失模块；Windows 固定兼容 4.44.2，Windows/Mac 安装及修复检查均加载实际推理模块。 |
| 一次 `--include` 后追加多个文件/通配符 | 高 / 高 | HF CLI 明确警告忽略 include，Seed 仅下到配置。显式文件用位置参数，每个通配符分别附带 `--include`；Seed 实装与 MiniMax/SoulX 清单 dry-run 通过。 |
| Windows `FileInfo.Length` 读取软链接自身 | 高 / 高 | 实际 CAMPPlus/Whisper 快照链接显示 0，但目标 blob 非空。健康检查解析最终目标，同时保留空文件、悬空链接失败检查。 |
| ACE 中断后目录存在即跳过 | 高 / 高 | XL 目录只有索引和辅助 tensor，没有四个权重分片，上游却返回 already exists。依据真实权重/完整索引判断，仅不完整组件使用上游 `--force` 重新进入下载器；底层仍复用缓存。 |
| Mac YourMT3 维护仍只 `import mt3_infer` | 中 / 高 | 安装器已检查实际推理模块，但维护复制表未同步；新增用例在旧代码误报成功，修复后记录失败并覆盖旧健康状态。现在共用安装器的导入表。 |
| Demucs 4.1.0 没有安装 NumPy | 高 / 高 | 官方元数据只对 Intel Mac 声明，真实 Windows 空环境 `pip check` 无报错但 CLI 导入失败。显式补齐 NumPy，随后真实分轨通过。Mac 原来会间接从 SoundFile 获得 NumPy，本次改为显式约束及实际 API 导入，未将它描述成已复现的 Mac 崩溃。 |
| 钢琴子进程强制 `NUMBA_DISABLE_JIT=1` | 高 / 高 | 实际 Librosa 0.11.0 / Numba 0.68.0 `pad_center` 导入报 `get_call_template`；同一环境将单个标志恢复默认后通过，完整 CUDA 转写通过。Mac 没有该强制标志，不做无依据的同类修改。 |
| 取消等待后未确认子进程退出 | 高 / 高 | Basic Pitch 限时测试结束后 uv 仍存活，短进程树及真实 uv 离线测试均重现旧逻辑失败。取消回调不能替代 `finally` 内的终止与有界等待；共享等待器修复后两组共 20 场景通过，正常钢琴/Demucs 处理也复测通过。 |

排除项：独立 Hugging Face 下载环境中的 `--upgrade` 不包含 Torch，不属于 GPU 覆盖问题；Qwen 第二阶段没有 `--upgrade`，已完成实际 CUDA 验收。新增 `BootstrapRegression` 断言固定免编译约束、MiniMax GPU 配对、Git 配置幂等性与父进程隔离，作为持续回归约束。

关键新增证据：`transkun-first-install.log`、`transkun-first-install-final.log`、`transkun-fixture-pitches.log`、`f5-first-install.log`、`f5-inference.log`、`whisper-first-install.log`、`whisper-inference.log`、`minimax-shared-dependencies-before.log`、`minimax-dependency-final.log`、`minimax-git-probe.log`。TransKun 中间验证还暴露出新增探针误用了 `find_overlap`，已改为引擎实际使用的 `all_overlaps_both`，随后重跑安装和转写通过。

后续证据：`yourmt3-first-install.log`、`yourmt3-runtime-before.log`、`yourmt3-official-checkpoint.json`、`yourmt3-first-install-fixed.log`、`yourmt3-inference.log`；`seed-first-install-retry.log`、`seed-first-install-corrected.log`、`seed-inference.log`；`ace-first-install-retry.log`、`ace-first-install-resume-fixed.log`、`ace-inference.log`；`minimax-hub-dry-run.log`、`soulx-hub-dry-run.log`、`bootstrap-hub-regression.log`、`bootstrap-ace-resume-regression.log`。

防回归约束位于 `BootstrapRegression`（真实命令参数、长路径提前拒绝、共享组件补齐）和 `UpdateFlowTests`（禁止 WinGet、禁止先装 CPU 后换 CUDA、只允许两个受控微软前置程序）。仅测试夹具中的预期旧代码字符串匹配，不算残留 WinGet 调用。Seed-VC 虽然单独构造安装命令，但统一的进程启动边界仍会配置隔离环境，不属于遗漏。

本轮窄范围同类检查：从 `IMPORTS.update(mt3="mt3_infer"...)` 扩展到仓库所有 `mt3_infer`、`IMPORTS` 定义及调用者，确认 1 处维护表遗漏；实际转写中的 `from mt3_infer import transcribe` 是功能调用，不是错误的健康探针。`NUMBA_DISABLE_JIT` 全库只发现钢琴这 1 处强制禁用；Seed 的 `NUMBA_CACHE_DIR` 仅设置缓存目录，不属同类缺陷。CI 可直接运行 `test_lifecycle.LifecycleTests.test_yourmt3_health_check_loads_inference_and_records_failure` 和 `--bootstrap-regression` 防止回归。

进程取消同类搜索使用 `Register`、`Kill(true)`、`WaitForExitAsync`：Windows 安装/Git、设备探测、后台任务、媒体预检共 7 处短命令调用改用统一收尾；媒体预检原有 `finally` 终止，所以不将其列为相同漏杀的既定复现。Mac 同步命令原来也有 `finally Stop`，本次加强等待退出，不声称已实机复现 Mac 残留。长期工作台已有独立显式 Stop 路径，与“等待一个短命令完成”不同，不机械替换。进程树夹具已纳入默认 Windows 行为测试，真实 uv 测试通过 `--process-cancel <uv绝对路径> <隔离Python绝对路径>` 单独运行。

新增原始证据：`demucs-first-install.log`、`demucs-first-install-fixed.log`、`demucs-inference.log`；`piano-first-install.log`、`piano-inference.log`、`piano-pad-disabled-probe.log`、`piano-pad-enabled-probe.log`、`piano-inference-fixed.log`；`mac-health-before.log`、`mac-health-after.log`。较早的 Mel 滤波器探针两种标志均通过，未覆盖实际失败位置，不能用作定因；随后改测堆栈里的 `pad_center` 才形成有效对照。

依据：[Demucs 4.1.0 官方包元数据](https://pypi.org/pypi/demucs/4.1.0/json)、[Numba JIT 环境变量](https://numba.readthedocs.io/en/stable/reference/envvars.html#envvar-NUMBA_DISABLE_JIT)、[.NET 取消等待不等于退出进程](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexitasync?view=net-10.0)、[Kill 的异步退出语义及后代进程限制](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0)。

## Basic Pitch 补验（2026-10-08）

本次补验基于测试分支 `d7fd214` 继续修复；2026-10-08 完成时尚未提交、推送或发版，后续纳入 2.0.2-beta.1 发布准备。没有替换已安装 Aurora，没有修改 `C:\LocalAI`、用户配置或全局 Python/包源。

发现与修复：

- 原安装器将 TensorFlow 大包交给 uv 边下载边解压；本轮实测在约 25 分钟后重新下载。独立 curl 诊断也遇到官方 CDN 后半段约 46 KB/s 的传输，确认不是单纯 UI 不刷新。先保留官方部分文件，再通过清华大学 PyPI 镜像续传，所得完整包与 PyPI 官方长度 `300919984`、SHA-256 `4710b0ea84defaafc0d6cc51162ebef8b07da015fc68375661451861c5bf4421` 一致。
- Windows Basic Pitch 0.4.0 的指定 TensorFlow wheel 改由 Aurora 现有断点下载器保存到 `.aurora/packages`。官方源单次尝试最多两分钟，网络失败或到时后从已保留字节切换清华镜像；最终必须通过长度及官方 SHA-256 验证，再交 uv 与其他依赖一同解析安装。只作用于这个固定版本文件，不改变全局源、不关闭 TLS、不跳过依赖校验；用户主动取消不触发备用下载。未来不同 Basic Pitch 版本不强套这个固定 wheel。
- resampy 0.4.2 仍导入 `pkg_resources`，但新环境解析到 setuptools 84.0.0 后缺少此模块。补上与 Mac 既有配置一致的 `setuptools<81`；实际安装得到 80.10.2。Mac 原有约束未改，不能把本次 Windows 结果写成 Mac 实机通过。

真实验收：

1. 使用本轮源码重新编译验证程序，并清除开发者 PATH；Python、模型及缓存均留在专用 `basic-install/Models`。最初误用旧编译产物的重试已主动停止，其日志不计入验收。
2. 确认应用的 wheel 缓存尚不存在后重走生产安装入口，未预置手工诊断下载文件。官方源限时结束后，应用自动切换镜像、续传并完成校验，66 个包检查兼容。首次 `--help` 实际检出了缺少 `pkg_resources`，候选未启用；补齐约束后再次创建候选，复用应用刚校验的缓存，安装和启动检查通过。期间还保留了一次 PyPI 元数据 TLS EOF 失败；同请求重试后恢复，未放宽证书校验。
3. 从 Aurora 的生产后端、任务队列与项目记录处理 `model-install/piano-notes.wav`：9.6 秒、44.1 kHz、单声道。任务在约 20.65 秒内生成并登记 `piano-notes_basic_pitch.mid`；文件 2261 字节，SMF 类型 1，两条轨道（含元数据轨），12 个音符，时长约 9.470 秒。Mido 和 PrettyMIDI 独立解析通过，音高序列 `60,64,67,72,67,64,62,65,69,74,69,65` 全部匹配，最大起音绝对偏差约 22.7 毫秒。这只是短样本功能/基础音符检查，不代表复杂乐曲或演出总谱精度。
4. 独立设备探针确认 TensorFlow 2.15.0 的 `cuda_build=false`、GPU 列表为空，使用 CPU。任务回执的 `Device` 字段目前为空，因此设备结论来自运行时探针，不能从回执推断 CUDA。
5. 最终 Windows 行为回归 333 条 PASS，更新流程通过；Windows/Mac 项目编译均零警告、零错误，Mac 共享逻辑 113 项通过（原生 Unix 项明确跳过）。真实 uv 取消 10 个场景通过，测试所属进程检查无残留。更新流程首次被沙箱临时目录的移动权限阻止，普通用户环境复测通过，未修改断言。

证据均位于 `.maintenance/clean-install-20261008/`：`basic-first-install-fixed-retry.log`（自动下载及实际导入失败）、`basic-pkg-resources-failure.json`、`basic-first-install-complete.log`、`basic-install/basic-pitch-install.json`、`basic-inference/basic-pitch/acceptance.json`、`basic-note-check.json`、`basic-device-check.log`、`basic-all-regressions-final.log`、`basic-update-flow-final-retry.log`、`basic-mac-shared-final.log`。回归修复前失败分别在 `basic-download-regression-before.log` 和 `basic-dependency-regression-before.log`。模型、包和样本不会提交到源码仓库，当前未清理测试证据。

依据：[PyPI 官方 wheel 元数据](https://pypi.org/pypi/tensorflow-intel/2.15.0/json)、[清华镜像官方说明](https://mirrors.tuna.tsinghua.edu.cn/help/pypi/)、[Setuptools 82 移除 pkg_resources](https://setuptools.pypa.io/en/latest/history.html#v82-0-0)、[TensorFlow 原生 Windows GPU 限制](https://www.tensorflow.org/install/pip#windows-native)。

## Windows 复测命令（开发/CI，不是最终用户操作）

2026-10-09 发布前的英文 Windows CI 额外发现 SoX 14.4.2 使用系统代码页，无法处理中文文件名；本机也用表情符号文件名复现失败。现在随未修改的上游 EXE 分发进程级 UTF-8 清单，并更新提取文件的时间戳，避免 Windows 沿用此前无清单的启动缓存。不修改系统语言、注册表或上游二进制内容。重新准备工具后，中文、日文和表情符号混合文件名的 0.2 秒输入已得到有效的 0.1 秒处理结果；CI 保留并强化这项真实音频检查，不能改用 ASCII 文件名绕过。完整 Unicode 支持需要 Windows 10 1903 或更新版本，更早的系统未在本轮验收。

CI 的另一个问题是测试程序没有收到应用随附的工具，开发机上的全局 FFmpeg 掩盖了缺失。工作流现显式传入 `AuroraRuntimeSource`，再执行仅系统 PATH 的首次环境准备测试；没有放宽原有超时断言。最终拟发布提交的完整构建和安装包验收另记于版本验收记录。

```powershell
./work/audio-studio/tools/Prepare-WindowsRuntime.ps1
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Release -- --bootstrap-tools work/audio-studio/AuroraAudioStudio/Runtime .maintenance/first-install-new
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Release -- --bootstrap-regression

# 真实模型新装；只在隔离目录执行，需要网络和空间。
$runtime = (Resolve-Path work/audio-studio/AuroraAudioStudio/Runtime).Path
dotnet run --project work/audio-studio/AuroraAudioStudio.BehaviorTests -c Release -p:AuroraRuntimeSource="$runtime" -- --first-install roformer .maintenance/first-model-install
```

基础工具归档在构建时验证并提取，不在用户安装模型时下载。`Runtime` 为生成内容，不提交二进制到源码仓库；正式构建前必须执行准备脚本。不得用已安装到维护机的工具冒充安装包内的工具。

## Mac 原生交接

以下是首次独立测试分支 `test/zero-env-bootstrap-20261008` 的交接快照，当时仅授权上传测试源码。后续 Windows 2.0.2-beta.1 的 Mac 构建应改用 [2.0.2-beta.1 交接说明](macOS-2.0.2-beta.1-handoff.md)和对应发布标签；不要将本轮修复误认为线上 2.0.1 已包含。此前“未提交/未推送”描述均是对应时点的状态。

建议在 Mac 使用独立新目录，不切换或覆盖有未提交改动的现有工作区。下面命令从当前目录创建一个新的源码目录；同名目录已存在时，选另一个名字，不删除旧目录。

```bash
git clone --single-branch --branch test/zero-env-bootstrap-20261008 \
  https://github.com/swy2018/Aurora-Audio-Studio.git Aurora-bootstrap-test-20261008
cd Aurora-bootstrap-test-20261008
git status --short
git rev-parse HEAD
```

先按 [Mac 构建要求](macOS-release.md)检查构建机工具。构建机需要开发环境；最终用户不应需要 Homebrew、Xcode、系统 Python 或 Git。当前目标是 Apple Silicon / macOS 26+，不把此分支描述成已支持 Intel Mac。

以下只构建 ad-hoc 本机验证 `.app`，不生成正式交付包。新建的临时目录用于隔离应用设置、模型和输出，需保留以便排查；不会覆盖 `/Applications` 中已安装的 Aurora。

```bash
aurora_qa_root="$(mktemp -d "$PWD/.aurora-mac-qa.XXXXXX")"
export AURORA_BUILD_ROOT="$aurora_qa_root/build"
export AURORA_TEST_DATA_ROOT="$aurora_qa_root/user-data"
unset AURORA_SIGN_IDENTITY
dotnet run --project work/audio-studio/AuroraAudioStudio.MacTests -c Release
bash work/audio-studio/tools/build-macos.sh
```

不要拿现有 2.0.1 `.app` 冒充本轮构建。新 `.app` 必须内含 Git 及 HTTPS helper，随后用同一隔离数据目录执行首次安装：

```bash
dotnet run --project work/audio-studio/AuroraAudioStudio.MacTests -c Release -- \
  --bootstrap-tools "$AURORA_BUILD_ROOT/Aurora Audio Studio.app/Contents/MacOS/Runtime" \
  "$AURORA_TEST_DATA_ROOT" whisper-small
```

此命令主动清除 Homebrew/user PATH，真实创建管理环境并安装一个模型；目录内必须尚无 `Models`，所以先运行它，再从 Finder 打开验证应用。文件全部保留。验收其他受支持引擎时，使用另一个全新数据目录，替换最后的模型 ID。该步骤需要网络和下载空间，不能用 Windows/WSL 上的跨平台测试替代；安装成功也不代表推理通过。

在 Finder 打开刚构建的 `.app`（它已写入隔离数据目录），用短音频实际生成字幕，确认可预览、导出、重新打开且输出非空；再覆盖中断下载后重试、取消时进程退出、重新启动后恢复操作。每个宣称可推理的引擎还需分别做首次安装和实际输入/输出验收。不要改动现有模型目录，不默认安装 MiniMax。

回传时记录源码完整 SHA、Mac 型号/系统版本、工具打包及签名检查结果、模型 ID、首次安装日志、实际输出路径/时长或条目数、失败堆栈与取消后进程状态。日志中的用户名或私有路径在对外发布前脱敏，不上传音频素材、模型、令牌、证书或私钥。`bootstrap-result.txt` 只证明指定模型安装完成，不能用作“全功能通过”的回执。

## 正式版与跨平台完整验收的剩余门槛

1. 干净 Windows 用户/虚拟机，不预装 Git、Python、uv、FFmpeg、SoX、开发编译器、VC++ 或 WebView2，确认原生安装程序补齐前置组件，应用完成首次安装；核验失败/需要重启分支与已有环境保留。
2. Mac 原生验证新收集的 Mach-O、HTTPS CA、签名/公证、从 Finder 启动后的管理环境创建；确认模型依赖不要求用户安装 Xcode/Homebrew。
3. 每个宣称可运行的模型完成首次安装与短样本输出；分别记录硬件条件、下载失败/中断后重试、共享依赖复用和旧环境不受影响。仅下载管理的项目不声称可推理。

以上项目尚未完成，因此不能宣称“所有 Windows/Mac 电脑、所有模型均已从零安装验收通过”。
