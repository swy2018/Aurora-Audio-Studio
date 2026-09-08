# Mac 本地构建、签名与公证

## 范围

Mac 版沿用现有 Avalonia/.NET 实现，输出在源码根目录的 `dist` 下；不构建 Windows，不执行 GitHub 或 App Store 发布，不把模型和用户成果装入安装包。
本流程保留构建中间文件，不自动清理旧产物，避免误删并行任务和用户数据。

## 构建

需要 .NET 10 SDK、Xcode、Python 3，以及构建机的 uv、ffmpeg、ffprobe、sox。工具及依赖、许可证随包复制；运行电脑不依赖构建机的 Homebrew 路径。
依赖收集同时覆盖 Homebrew 版 .NET 自带原生库（包括压缩库的 Brotli 依赖），而不只扫描四个音频/环境工具；库自身的 install ID 与实际加载依赖分开判断。

```bash
export AURORA_BUILD_ROOT="$PWD/dist/macos-1.9.0-mac.5-arm64"
export AURORA_SIGN_IDENTITY='Developer ID Application: YOUR NAME (TEAMID)'
bash work/audio-studio/tools/package-macos.sh
```

构建目录必须尚不存在，失败后使用另一新目录或在列明清单并获得批准后处理旧目录。
不提供身份时，`build-macos.sh` 仍可生成 ad-hoc 本机验证应用，但 `package-macos.sh` 必须显式提供签名身份。

## 签名决策

使用 Apple Developer ID Application 签名，每个内嵌 Mach-O 从内到外签署，最后签主应用；不使用 `--deep` 执行正式签名。
沿用 Avalonia 的平铺 `Contents/MacOS` 布局；该目录下的 DLL、脚本及其他文件也受 macOS nested-code 规则约束，须逐一签署。主 apphost 的签名会解析到应用包，所以只在全部组件签完后签整个应用。此顺序见 [Avalonia Mac 部署指南](https://docs.avaloniaui.net/docs/deployment/macos/)。
启用 hardened runtime 和安全时间戳。主程序只添加 .NET JIT 所需的 `com.apple.security.cs.allow-jit`，不添加调试或关闭库验证权限。
模型子进程设置 `PYTHONDONTWRITEBYTECODE=1`，防止管理脚本导入时在已签名应用内写入 `__pycache__` 而破坏资源封印；已有模型环境中的缓存仍可读取。[Python 说明](https://docs.python.org/3/using/cmdline.html#envvar-PYTHONDONTWRITEBYTECODE)
现有 .NET 运行时、Avalonia 原生库和音频工具作为此应用的内嵌组件签署；不单独发布第三方工具，不签署模型。Windows SignPath 政策不变。

依据：[Microsoft .NET macOS 部署](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos)、[Apple 公证要求](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution)。

## Apple 公证：由账户持有人先配置

在 Apple 账户网站生成 App 专用密码，并在自己的终端交互输入，勿将密码或私钥发送到聊天、写入脚本或版本库：

```bash
xcrun notarytool store-credentials Aurora-Notary
```

账户须属于实际签名的开发者团队。配置成功后，公证命令仅引用钥匙串配置名。
提交会把指定安装包上传给 Apple 扫描；不单独上传源代码、模型、设置或用户作品。

```bash
xcrun notarytool submit /absolute/path/to/Aurora.dmg --keychain-profile Aurora-Notary --wait
# 只有状态为 Accepted 才继续；失败时用提交 ID 查询 notarytool log。
xcrun stapler staple /absolute/path/to/Aurora.dmg
xcrun stapler validate /absolute/path/to/Aurora.dmg
spctl --assess --type open --context context:primary-signature --verbose=4 /absolute/path/to/Aurora.dmg
```

不得把仅签名、尚未获得 Accepted 和 stapler/Gatekeeper 验证的包称为已公证。
公证后重新计算最终 DMG 的 SHA-256，因为装订票据会改变文件。
具体流程依据：[Apple 自定义公证流程](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow)。

## 验收与回退

- .NET 回归、Python 模型生命周期回归、NuGet 漏洞检查。
- 检查应用及所有内嵌原生组件签名、arm64 架构、外部依赖和最小系统版本。
- DMG 校验、只读挂载，确认应用和 Applications 快捷方式存在。
- 实际打开签名应用，检查中文界面、模型中心、内嵌工作台和代表性真实生成任务。
- 保留原应用和既有模型/输出，升级出现问题可退出新版、重新打开原应用；不靠删除模型恢复。
- 只有完成公证及安装来源验证后，才能声称可直接对外分发。本机通过不等于所有 macOS 版本通过。
